extends SceneTree

## Real end-to-end harness against the deployed backend (Oracle Cloud).
##
## Drives the *real* client stack (HttpApiClient + WebSocketTransport) through the
## full First Playable loop and prints each step as evidence:
##   login -> characters -> game-token -> WSS -> game.authenticate ->
##   game.authenticated -> world.enter -> world.snapshot -> movement.move ->
##   movement.accepted -> combat.attack -> combat.result (defeat + XP + loot).
##
## Usage (client-only, never starts a local backend):
##   set ETHER_CLIENT_API_URL=https://<api-host>
##   set ETHER_CLIENT_WS_URL=wss://<game-host>/game
##   set ETHER_E2E_EMAIL=hero@example.com
##   set ETHER_E2E_PASSWORD=<password>
##   godot --headless --path client/Ether.Game --script res://tests/real_e2e.gd
##
## Exit codes: 0 = full flow PASS, 1 = flow FAIL, 2 = not configured (BLOCKED).

enum Stage { IDLE, LOGIN, CHARACTERS, GAME_TOKEN, WS_AUTH, WORLD_ENTER, MOVE, COMBAT, DONE }

const STEP_TIMEOUT := 20.0
const TOTAL_TIMEOUT := 90.0
const ATTACK_INTERVAL := 0.4

var _config: ClientConfig
var _api: HttpApiClient
var _network: NetworkClient

var _pending: Dictionary = {}
var _stage: int = Stage.IDLE
var _stage_deadline := 0.0
var _total_deadline := 0.0

var _email := ""
var _password := ""
var _account_id := ""
var _character_id := ""
var _game_token := ""
var _player_id := ""
var _map_id := -1
var _position := Vector2i.ZERO

var _creature_id := ""
var _next_attack_at := 0.0
var _attacks := 0
var _started := false
var _finished := false


func _initialize() -> void:
	_config = ClientConfig.from_environment()
	_config.mode = ClientConfig.Mode.REAL

	_email = OS.get_environment("ETHER_E2E_EMAIL")
	_password = OS.get_environment("ETHER_E2E_PASSWORD")

	if _config.api_base_url == "" or _config.game_websocket_url == "":
		print("REAL_E2E: BLOCKED — set ETHER_CLIENT_API_URL and ETHER_CLIENT_WS_URL")
		quit(2)
		return
	if _email == "" or _password == "":
		print("REAL_E2E: BLOCKED — set ETHER_E2E_EMAIL and ETHER_E2E_PASSWORD")
		quit(2)
		return

	print("REAL_E2E: start api=%s ws=%s" % [_config.api_base_url, _config.game_websocket_url])

	_api = HttpApiClient.new(_config.api_base_url)
	root.add_child(_api)
	_api.completed.connect(_on_api_completed)

	_network = NetworkClient.new()
	root.add_child(_network)
	_network.configure(_config)
	_network.event_received.connect(_on_event)
	_network.snapshot_received.connect(_on_snapshot)
	_network.error_received.connect(_on_error)
	_network.server_connected.connect(_on_ws_connected)


func _on_ws_connected() -> void:
	# Mirrors GameClient: authenticate as soon as the socket opens.
	_network.authenticate(_game_token)


func _process(_delta: float) -> bool:
	if not _started:
		# Start on the first frame: HTTPRequest/WebSocket need a running tree.
		_started = true
		_total_deadline = _now() + TOTAL_TIMEOUT
		_enter_stage(Stage.LOGIN, "login")
		var request_id := _api.new_request_id()
		_pending[request_id] = "login"
		_api.login(_email, _password, request_id)
		return false

	var now := _now()
	if _finished:
		return true
	if now > _total_deadline:
		return _fail("total timeout at stage '%s'" % _stage_name())
	if _stage != Stage.DONE and _stage != Stage.IDLE and now > _stage_deadline:
		return _fail("step timeout at stage '%s'" % _stage_name())

	if _stage == Stage.COMBAT and now >= _next_attack_at:
		_next_attack_at = now + ATTACK_INTERVAL
		_attacks += 1
		_network.attack(ProtocolMessages.ABILITY_BASIC_ATTACK, _creature_id, ProtocolMessages.TARGET_TYPE_CREATURE)

	return false


# --- HTTP ---------------------------------------------------------------------

func _on_api_completed(request_id: String, ok: bool, data: Variant, error: String) -> void:
	var intent := String(_pending.get(request_id, ""))
	_pending.erase(request_id)
	if not ok:
		_fail("HTTP '%s' failed: %s" % [intent, error])
		return

	if intent == "login":
		var session: Dictionary = data if typeof(data) == TYPE_DICTIONARY else {}
		_account_id = String(session.get("accountId", ""))
		_api.set_token(String(session.get("accessToken", "")))
		print("REAL_E2E: 1/9 login ok (accountId=%s, accessToken=***)" % _account_id)
		_enter_stage(Stage.CHARACTERS, "characters")
		var rid := _api.new_request_id()
		_pending[rid] = "characters"
		_api.list_characters(_account_id, rid)
	elif intent == "characters":
		var characters: Array = data if typeof(data) == TYPE_ARRAY else []
		if characters.is_empty():
			_fail("account has no characters; create one via the API first")
			return
		# Prefer a character that is not already in the world (a previous unclean
		# run can leave one InWorld server-side).
		var chosen: Dictionary = {}
		for character in characters:
			if typeof(character) == TYPE_DICTIONARY and String(character.get("state", "")) == "Offline":
				chosen = character
				break
		if chosen.is_empty():
			chosen = characters[0]
		_character_id = String(chosen.get("characterId", ""))
		print("REAL_E2E: 2/9 characters ok (%d; using %s state=%s)" % [
			characters.size(), _character_id, String(chosen.get("state", "?"))])
		_enter_stage(Stage.GAME_TOKEN, "game-token")
		var rid := _api.new_request_id()
		_pending[rid] = "game_token"
		_api.request_game_token(_character_id, rid)
	elif intent == "game_token":
		var token: Dictionary = data if typeof(data) == TYPE_DICTIONARY else {}
		_game_token = String(token.get("gameToken", ""))
		if _game_token == "":
			_fail("empty game token")
			return
		print("REAL_E2E: 3/9 game-token ok (gameToken=***)")
		_enter_stage(Stage.WS_AUTH, "websocket authenticate")
		_network.connect_to_server()


# --- WebSocket ----------------------------------------------------------------

func _on_event(name: String, payload: Dictionary) -> void:
	if _finished:
		return
	if name == ProtocolMessages.EVT_GAME_AUTHENTICATED:
		print("REAL_E2E: 4/9 game.authenticated ok (sessionId=%s)" % String(payload.get("sessionId", "")))
		if String(payload.get("characterId", "")) != _character_id:
			_fail("server authenticated unexpected character")
			return
		_enter_stage(Stage.WORLD_ENTER, "world.enter")
		_network.enter_world()
	elif name == ProtocolMessages.EVT_MOVEMENT_ACCEPTED:
		_position = Vector2i(int(payload.get("x", 0)), int(payload.get("y", 0)))
		print("REAL_E2E: 6/9 movement.accepted (%d,%d)" % [_position.x, _position.y])
		if _creature_id == "":
			_pass("world + movement")
			return
		_enter_stage(Stage.COMBAT, "combat.attack")
		print("REAL_E2E: 7/9 combat.attack -> %s" % _creature_id)
		_next_attack_at = _now() + ATTACK_INTERVAL
	elif name == ProtocolMessages.EVT_COMBAT_RESULT:
		var target_id := String(payload.get("targetId", ""))
		if target_id != _creature_id:
			return
		print("REAL_E2E:    hit %s for %d (target %d/%d)" % [
			target_id,
			int(payload.get("damage", 0)),
			int(payload.get("targetHealth", 0)),
			int(payload.get("targetMaxHealth", 0)),
		])
		if bool(payload.get("targetDefeated", false)):
			print("REAL_E2E: 8/9 creature defeated after %d attack(s)" % _attacks)
			var loot_count := 0
			var loot_raw: Variant = payload.get(ProtocolMessages.FIELD_LOOT, [])
			if typeof(loot_raw) == TYPE_ARRAY:
				var loot_arr: Array = loot_raw
				loot_count = loot_arr.size()
			print("REAL_E2E: 9/9 reward experienceGained=%d level=%d experience=%d loot=%d" % [
				int(payload.get(ProtocolMessages.FIELD_EXPERIENCE_GAINED, 0)),
				int(payload.get(ProtocolMessages.FIELD_LEVEL, 0)),
				int(payload.get(ProtocolMessages.FIELD_EXPERIENCE, 0)),
				loot_count,
			])
			_pass("full first-playable loop")


func _on_snapshot(payload: Dictionary) -> void:
	var player: Dictionary = payload.get("player", {}) if typeof(payload.get("player")) == TYPE_DICTIONARY else {}
	_player_id = String(player.get("characterId", _character_id))
	_map_id = int(payload.get("mapId", -1))
	_position = Vector2i(int(player.get("x", 0)), int(player.get("y", 0)))
	var creature_count := 0
	var creatures_raw: Variant = payload.get("creatures", [])
	if typeof(creatures_raw) == TYPE_ARRAY:
		var creatures_arr: Array = creatures_raw
		creature_count = creatures_arr.size()
	print("REAL_E2E: 5/9 world.snapshot ok (mapId=%d %dx%d player=%s @ %d,%d, creatures=%d)" % [
		_map_id,
		int(payload.get("width", 0)),
		int(payload.get("height", 0)),
		_player_id,
		_position.x,
		_position.y,
		creature_count,
	])

	_creature_id = _nearest_creature(payload.get("creatures", []), _position)
	var target := _position
	if _creature_id != "":
		var creature_position := _creature_position(payload.get("creatures", []), _creature_id)
		target = Vector2i(maxi(0, creature_position.x - 1), creature_position.y)

	_enter_stage(Stage.MOVE, "movement.move")
	print("REAL_E2E:    movement.move -> (%d,%d)" % [target.x, target.y])
	_network.move_to(target.x, target.y)


func _nearest_creature(creatures: Variant, origin: Vector2i) -> String:
	if typeof(creatures) != TYPE_ARRAY:
		return ""
	var best := ""
	var best_distance := 1 << 30
	for creature in creatures:
		if typeof(creature) != TYPE_DICTIONARY:
			continue
		var position := Vector2i(int(creature.get("x", 0)), int(creature.get("y", 0)))
		var distance := maxi(absi(origin.x - position.x), absi(origin.y - position.y))
		if distance < best_distance:
			best_distance = distance
			best = String(creature.get("creatureId", ""))
	return best


func _creature_position(creatures: Variant, creature_id: String) -> Vector2i:
	if typeof(creatures) != TYPE_ARRAY:
		return Vector2i.ZERO
	for creature in creatures:
		if typeof(creature) == TYPE_DICTIONARY and String(creature.get("creatureId", "")) == creature_id:
			return Vector2i(int(creature.get("x", 0)), int(creature.get("y", 0)))
	return Vector2i.ZERO


func _on_error(name: String, code: String, _message: String) -> void:
	if _finished:
		return
	_fail("server error '%s' (%s)" % [name, code])


# --- Helpers ------------------------------------------------------------------

func _enter_stage(stage: int, label: String) -> void:
	_stage = stage
	_stage_deadline = _now() + STEP_TIMEOUT
	print("REAL_E2E: stage -> %s" % label)


func _stage_name() -> String:
	match _stage:
		Stage.LOGIN: return "login"
		Stage.CHARACTERS: return "characters"
		Stage.GAME_TOKEN: return "game-token"
		Stage.WS_AUTH: return "websocket authenticate"
		Stage.WORLD_ENTER: return "world.enter"
		Stage.MOVE: return "movement.move"
		Stage.COMBAT: return "combat.attack"
		Stage.DONE: return "done"
		_: return "idle"


func _pass(what: String) -> void:
	_stage = Stage.DONE
	_finished = true
	print("REAL_E2E: PASS — %s verified end-to-end" % what)
	quit(0)


func _fail(reason: String) -> bool:
	_finished = true
	print("REAL_E2E: FAIL — %s" % reason)
	quit(1)
	return true


func _now() -> float:
	return float(Time.get_ticks_msec()) / 1000.0
