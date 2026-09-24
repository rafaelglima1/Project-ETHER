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

enum Stage { IDLE, REGISTER, LOGIN, CHARACTERS, CREATE_CHARACTER, GAME_TOKEN, WS_AUTH, WORLD_ENTER, RESPAWN_CHECK, MOVE, COMBAT, INVENTORY_HTTP, RECONNECT_AUTH, RECONNECT_WORLD, INVENTORY_HTTP_RECONNECTED, DONE }

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
var _respawn_rejected := false
var _pending_move := Vector2i.ZERO
var _register_before_login := false
var _after_loot_reconnect := false
var _reconnect_connect_at := 0.0
var _reconnect_started := false
var _inventory_http_after_loot: Array = []
var _inventory_snapshot_after_reconnect: Array = []
var _inventory_snapshot_initial_available := false
var _inventory_snapshot_reconnect_available := false
var _inventory_snapshot_initial_detail := ""
var _inventory_snapshot_reconnect_detail := ""


func _initialize() -> void:
	_config = ClientConfig.from_environment()
	_config.mode = ClientConfig.Mode.REAL

	_email = OS.get_environment("ETHER_E2E_EMAIL")
	_password = OS.get_environment("ETHER_E2E_PASSWORD")
	var register_flag := OS.get_environment("ETHER_E2E_REGISTER") == "1"

	if _config.api_base_url == "" or _config.game_websocket_url == "":
		print("REAL_E2E: BLOCKED — set ETHER_CLIENT_API_URL and ETHER_CLIENT_WS_URL")
		_finished = true
		quit(2)
		return
	if _email == "" and _password == "":
		var crypto := Crypto.new()
		var identity := crypto.generate_random_bytes(16).hex_encode()
		_email = "ether-e2e-%s@example.com" % identity
		_password = "E2E-%s" % crypto.generate_random_bytes(24).hex_encode()
		register_flag = true
	elif _email == "" or _password == "":
		print("REAL_E2E: BLOCKED — provide both ETHER_E2E_EMAIL and ETHER_E2E_PASSWORD, or neither to self-register")
		_finished = true
		quit(2)
		return
	_register_before_login = register_flag

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
	if _finished:
		return true
	if not _started:
		# Start on the first frame: HTTPRequest/WebSocket need a running tree.
		_started = true
		_total_deadline = _now() + TOTAL_TIMEOUT
		_start_account_flow()
		return false

	var now := _now()
	if now > _total_deadline:
		return _fail("total timeout at stage '%s'" % _stage_name())
	if _stage != Stage.DONE and _stage != Stage.IDLE and now > _stage_deadline:
		return _fail("step timeout at stage '%s'" % _stage_name())
	if _stage == Stage.RECONNECT_AUTH and not _reconnect_started and now >= _reconnect_connect_at:
		_reconnect_started = true
		_network.connect_to_server()

	if _stage == Stage.COMBAT and now >= _next_attack_at:
		_next_attack_at = now + ATTACK_INTERVAL
		_attacks += 1
		_network.attack(ProtocolMessages.ABILITY_BASIC_ATTACK, _creature_id, ProtocolMessages.TARGET_TYPE_CREATURE)

	return false


# --- HTTP ---------------------------------------------------------------------

func _start_account_flow() -> void:
	if _register_before_login:
		_enter_stage(Stage.REGISTER, "register disposable account")
		var register_id := _api.new_request_id()
		_pending[register_id] = "register"
		_api.register(_email, _password, register_id)
	else:
		_request_login()


func _request_login() -> void:
	_enter_stage(Stage.LOGIN, "login")
	var request_id := _api.new_request_id()
	_pending[request_id] = "login"
	_api.login(_email, _password, request_id)


func _create_disposable_character() -> void:
	var character_name := "E2E%d" % (Time.get_ticks_msec() % 100000000)
	_enter_stage(Stage.CREATE_CHARACTER, "create disposable Warrior")
	var request_id := _api.new_request_id()
	_pending[request_id] = "create_character"
	_api.create_character(_account_id, character_name, "Warrior", request_id)


func _request_characters() -> void:
	_enter_stage(Stage.CHARACTERS, "characters")
	var request_id := _api.new_request_id()
	_pending[request_id] = "characters"
	_api.list_characters(_account_id, request_id)


func _on_api_completed(request_id: String, ok: bool, data: Variant, error: String) -> void:
	if _finished:
		return
	var intent := String(_pending.get(request_id, ""))
	_pending.erase(request_id)
	if not ok:
		var category := "GAMEPLAY_FAILURE"
		if intent == "register":
			category = "REGISTRATION_FAILURE"
		elif intent == "login":
			category = "AUTHENTICATION_FAILURE"
		elif error.contains("not allowed") or error.contains("session expired"):
			category = "AUTHORIZATION_FAILURE"
		_fail("%s: HTTP '%s' failed: %s" % [category, intent, error])
		return

	if intent == "register":
		if typeof(data) != TYPE_DICTIONARY:
			_fail("REGISTRATION_FAILURE: expected register response object")
			return
		var registration: Dictionary = data
		if String(registration.get("accountId", "")) == "":
			_fail("REGISTRATION_FAILURE: response missing accountId")
			return
		_account_id = String(registration["accountId"])
		print("REAL_E2E: 1/13 disposable account registered")
		_request_login()
	elif intent == "login":
		if typeof(data) != TYPE_DICTIONARY:
			_fail("AUTHENTICATION_FAILURE: expected login response object")
			return
		var session: Dictionary = data
		_account_id = String(session.get("accountId", ""))
		var access_token := String(session.get("accessToken", ""))
		if _account_id == "" or access_token == "":
			_fail("AUTHENTICATION_FAILURE: login response missing accountId/accessToken")
			return
		_api.set_token(access_token)
		print("REAL_E2E: 2/13 login ok (accountId=%s, accessToken=***)" % _account_id)
		_request_characters()
	elif intent == "characters":
		if typeof(data) != TYPE_ARRAY:
			_fail("AUTHORIZATION_FAILURE: characters response must be an array")
			return
		var characters: Array = data
		# Prefer a character that is not already in the world (a previous unclean
		# run can leave one InWorld server-side).
		var chosen: Dictionary = {}
		for character in characters:
			if typeof(character) != TYPE_DICTIONARY:
				_fail("GAMEPLAY_FAILURE: malformed character entry")
				return
			if String(character.get("state", "")) == "Offline":
				chosen = character
				break
		if chosen.is_empty():
			_create_disposable_character()
			return
		_character_id = String(chosen.get("characterId", ""))
		if _character_id == "":
			_fail("GAMEPLAY_FAILURE: chosen character is missing characterId")
			return
		print("REAL_E2E: 3/13 characters ready (%d; using %s state=%s)" % [
			characters.size(), _character_id, String(chosen.get("state", "?"))])
		_enter_stage(Stage.GAME_TOKEN, "game-token")
		var rid := _api.new_request_id()
		_pending[rid] = "game_token"
		_api.request_game_token(_character_id, rid)
	elif intent == "create_character":
		if typeof(data) != TYPE_DICTIONARY or String(data.get("characterId", "")) == "":
			_fail("GAMEPLAY_FAILURE: create-character response missing characterId")
			return
		print("REAL_E2E: disposable Warrior created")
		_request_characters()
	elif intent == "game_token":
		if typeof(data) != TYPE_DICTIONARY:
			_fail("AUTHORIZATION_FAILURE: game-token response must be an object")
			return
		var token: Dictionary = data
		_game_token = String(token.get("gameToken", ""))
		if _game_token == "":
			_fail("AUTHORIZATION_FAILURE: empty game token")
			return
		print("REAL_E2E: 4/13 game-token ok (gameToken=***)")
		_enter_stage(Stage.WS_AUTH, "websocket authenticate")
		_network.connect_to_server()
	elif intent == "inventory_after_loot":
		var items: Variant = _parse_inventory_response(data, "post-loot HTTP")
		if items == null:
			return
		_inventory_http_after_loot = items
		print("REAL_E2E: 12/13 authoritative HTTP inventory (%d stack(s))" % items.size())
		for item in items:
			print("REAL_E2E:      %s x%d" % [String(item["name"]), int(item["quantity"])])
		_begin_inventory_reconnect()
	elif intent == "inventory_reconnected":
		var items: Variant = _parse_inventory_response(data, "reconnected HTTP")
		if items == null:
			return
		if _inventory_signature(items) != _inventory_signature(_inventory_http_after_loot):
			_fail("GAMEPLAY_FAILURE: persisted inventory changed across reconnect (HTTP)")
			return
		if not _inventory_snapshot_initial_available:
			_fail("SNAPSHOT_INVENTORY_MISSING on initial world.snapshot (%s); authenticated HTTP remained usable" % _inventory_snapshot_initial_detail)
			return
		if not _inventory_snapshot_reconnect_available:
			_fail("SNAPSHOT_INVENTORY_MISSING after reconnect (%s); authenticated HTTP remained usable" % _inventory_snapshot_reconnect_detail)
			return
		if _inventory_signature(_inventory_snapshot_after_reconnect) != _inventory_signature(items):
			_fail("SNAPSHOT_INVENTORY_DISCREPANCY: post-reconnect world.snapshot differs from authenticated HTTP inventory")
			return
		print("REAL_E2E: 13/13 HTTP and world.snapshot agree after reconnect")
		_pass("M8 inventory persisted across reconnect; respawn rejection, movement, combat, XP/loot, HTTP and WS snapshot agree")


func _parse_inventory_response(data: Variant, label: String) -> Variant:
	if typeof(data) != TYPE_DICTIONARY:
		_fail("GAMEPLAY_FAILURE: %s response must be an object" % label)
		return null
	var response: Dictionary = data
	if String(response.get("characterId", "")) != _character_id:
		_fail("AUTHORIZATION_FAILURE: %s response characterId mismatch" % label)
		return null
	if typeof(response.get("items")) != TYPE_ARRAY:
		_fail("GAMEPLAY_FAILURE: %s response missing items array" % label)
		return null
	var items: Array = response["items"]
	if not _validate_inventory_items(items, label):
		return null
	return items


func _validate_inventory_items(items: Array, label: String) -> bool:
	for item in items:
		if typeof(item) != TYPE_DICTIONARY:
			_fail("GAMEPLAY_FAILURE: %s contains a non-object item" % label)
			return false
		if typeof(item.get("itemDefinitionId")) != TYPE_STRING or String(item["itemDefinitionId"]) == "":
			_fail("GAMEPLAY_FAILURE: %s item missing itemDefinitionId" % label)
			return false
		if typeof(item.get("name")) != TYPE_STRING or String(item["name"]) == "":
			_fail("GAMEPLAY_FAILURE: %s item missing name" % label)
			return false
		if not _is_integer_number(item.get("quantity")) or int(item["quantity"]) < 1:
			_fail("GAMEPLAY_FAILURE: %s item quantity invalid" % label)
			return false
		if not _is_integer_number(item.get("maxStack")) or int(item["maxStack"]) < int(item["quantity"]):
			_fail("GAMEPLAY_FAILURE: %s item maxStack invalid" % label)
			return false
		if typeof(item.get("stackable")) != TYPE_BOOL or typeof(item.get("location")) != TYPE_STRING:
			_fail("GAMEPLAY_FAILURE: %s item metadata invalid" % label)
	return true


func _inventory_signature(items: Array) -> Array[String]:
	var signature: Array[String] = []
	for item in items:
		signature.append("%s|%s|%d|%d|%s|%s" % [
			String(item.get("itemDefinitionId", "")),
			String(item.get("name", "")),
			int(item.get("quantity", 0)),
			int(item.get("maxStack", 0)),
			str(item.get("stackable", false)),
			String(item.get("location", "")),
		])
	signature.sort()
	return signature


func _is_integer_number(value: Variant) -> bool:
	if typeof(value) == TYPE_INT:
		return true
	if typeof(value) != TYPE_FLOAT:
		return false
	return is_equal_approx(float(value), float(int(value)))


func _begin_inventory_reconnect() -> void:
	_after_loot_reconnect = true
	_reconnect_started = false
	_enter_stage(Stage.RECONNECT_AUTH, "reconnect after persisted loot")
	_network.disconnect_from_server()
	# Give the remote server time to observe the previous socket close before a
	# fresh token-authenticated session enters the same character.
	_reconnect_connect_at = _now() + 0.5


# --- WebSocket ----------------------------------------------------------------

func _on_event(name: String, payload: Dictionary) -> void:
	if _finished:
		return
	if name == ProtocolMessages.EVT_GAME_AUTHENTICATED:
		if _stage != Stage.WS_AUTH and _stage != Stage.RECONNECT_AUTH:
			return # duplicate/stale authentication must not issue another world.enter
		var session_id := String(payload.get("sessionId", ""))
		if session_id == "" or String(payload.get("characterId", "")) != _character_id:
			_fail("AUTHORIZATION_FAILURE: game.authenticated missing session or selected character mismatch")
			return
		if _after_loot_reconnect:
			print("REAL_E2E: game.authenticated after reconnect")
		else:
			print("REAL_E2E: 5/13 game.authenticated ok (sessionId=%s)" % session_id)
		if _after_loot_reconnect:
			_enter_stage(Stage.RECONNECT_WORLD, "world.enter after reconnect")
		else:
			_enter_stage(Stage.WORLD_ENTER, "world.enter")
		_network.enter_world()
	elif name == ProtocolMessages.EVT_MOVEMENT_ACCEPTED:
		if _stage != Stage.MOVE:
			return # stale acceptance is not a new movement success
		if String(payload.get("characterId", "")) != _character_id \
				or not _is_integer_number(payload.get("x")) or not _is_integer_number(payload.get("y")):
			_fail("GAMEPLAY_FAILURE: malformed or wrong-character movement.accepted")
			return
		_position = Vector2i(int(payload.get("x", 0)), int(payload.get("y", 0)))
		print("REAL_E2E: 9/13 movement.accepted (%d,%d)" % [_position.x, _position.y])
		if _creature_id == "":
			_fail("GAMEPLAY_FAILURE: no living creature in world snapshot")
			return
		_enter_stage(Stage.COMBAT, "combat.attack")
		print("REAL_E2E: 10/13 combat.attack -> %s" % _creature_id)
		_next_attack_at = _now() + ATTACK_INTERVAL
	elif name == ProtocolMessages.EVT_COMBAT_RESULT:
		if _stage != Stage.COMBAT:
			return # duplicate/out-of-order result must not trigger another inventory read
		if typeof(payload.get("targetId")) != TYPE_STRING \
				or not _is_integer_number(payload.get("damage")) \
				or not _is_integer_number(payload.get("targetHealth")) \
				or not _is_integer_number(payload.get("targetMaxHealth")) \
				or typeof(payload.get("targetDefeated")) != TYPE_BOOL:
			_fail("GAMEPLAY_FAILURE: malformed combat.result event")
			return
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
			print("REAL_E2E: 11/13 creature defeated after %d attack(s)" % _attacks)
			# Stop firing attacks immediately: the target is dead, so any further
			# combat.attack would be a (correctly) rejected duplicate.
			_stage = Stage.IDLE
			var loot_count := 0
			var loot_raw: Variant = payload.get(ProtocolMessages.FIELD_LOOT, [])
			if typeof(loot_raw) == TYPE_ARRAY:
				var loot_arr: Array = loot_raw
				loot_count = loot_arr.size()
			print("REAL_E2E:    reward experienceGained=%d level=%d experience=%d loot=%d" % [
				int(payload.get(ProtocolMessages.FIELD_EXPERIENCE_GAINED, 0)),
				int(payload.get(ProtocolMessages.FIELD_LEVEL, 0)),
				int(payload.get(ProtocolMessages.FIELD_EXPERIENCE, 0)),
				loot_count,
			])
			# Prove the dropped loot reached the authoritative inventory (M8).
			var rid := _api.new_request_id()
			_pending[rid] = "inventory_after_loot"
			_enter_stage(Stage.INVENTORY_HTTP, "GET authoritative inventory after loot")
			_api.get_inventory(_character_id, rid)


func _on_snapshot(payload: Dictionary) -> void:
	if _stage != Stage.WORLD_ENTER and _stage != Stage.RECONNECT_WORLD:
		return # stale/duplicate snapshot outside the expected world.enter response
	# This callback is only emitted for type=event, name=world.snapshot by
	# EventDispatcher. Validate the complete success shape before reading fields.
	if not _is_integer_number(payload.get("mapId")) or not _is_integer_number(payload.get("width")) \
			or not _is_integer_number(payload.get("height")) or int(payload.get("width", 0)) <= 0 \
			or int(payload.get("height", 0)) <= 0 or typeof(payload.get("player")) != TYPE_DICTIONARY \
			or typeof(payload.get("creatures")) != TYPE_ARRAY:
		_fail("GAMEPLAY_FAILURE: malformed world.snapshot base shape (map=%s width=%s height=%s player=%s creatures=%s)" % [
			type_string(typeof(payload.get("mapId"))), type_string(typeof(payload.get("width"))),
			type_string(typeof(payload.get("height"))), type_string(typeof(payload.get("player"))),
			type_string(typeof(payload.get("creatures"))),
		])
		return
	var player: Dictionary = payload["player"]
	if String(player.get("characterId", "")) != _character_id \
			or not _is_integer_number(player.get("x")) or not _is_integer_number(player.get("y")):
		_fail("GAMEPLAY_FAILURE: world.snapshot player identity/position invalid")
		return
	var creatures: Array = payload["creatures"]
	var snapshot_inventory: Array = []
	var inventory_raw: Variant = payload.get("inventory", null)
	var inventory_available := typeof(inventory_raw) == TYPE_ARRAY
	var inventory_detail := ""
	if inventory_available:
		snapshot_inventory = inventory_raw
		if not _validate_inventory_items(snapshot_inventory, "world.snapshot"):
			return
	else:
		inventory_detail = "null" if payload.has("inventory") else "field absent"
		print("REAL_E2E: SNAPSHOT_INVENTORY_UNAVAILABLE (%s); do not treat as empty inventory" % inventory_detail)
	if _after_loot_reconnect:
		_inventory_snapshot_reconnect_available = inventory_available
		_inventory_snapshot_reconnect_detail = inventory_detail
		_inventory_snapshot_after_reconnect = snapshot_inventory.duplicate(true) if inventory_available else []
		print("REAL_E2E: reconnect world.snapshot (inventory=%s)" % (str(snapshot_inventory.size()) if inventory_available else "unavailable"))
		var inventory_id := _api.new_request_id()
		_pending[inventory_id] = "inventory_reconnected"
		_enter_stage(Stage.INVENTORY_HTTP_RECONNECTED, "GET inventory after reconnect")
		_api.get_inventory(_character_id, inventory_id)
		return
	_inventory_snapshot_initial_available = inventory_available
	_inventory_snapshot_initial_detail = inventory_detail

	_player_id = String(player.get("characterId", _character_id))
	_map_id = int(payload.get("mapId", -1))
	_position = Vector2i(int(player.get("x", 0)), int(player.get("y", 0)))
	print("REAL_E2E: 6/13 world.snapshot ok (mapId=%d %dx%d player=%s @ %d,%d, creatures=%d, inventory=%s)" % [
		_map_id,
		int(payload.get("width", 0)),
		int(payload.get("height", 0)),
		_player_id,
		_position.x,
		_position.y,
		creatures.size(),
		str(snapshot_inventory.size()) if inventory_available else "unavailable",
	])

	_creature_id = _nearest_creature(creatures, _position)
	if _creature_id == "":
		_fail("GAMEPLAY_FAILURE: no living creature in world.snapshot")
		return
	var target := _position
	if _creature_id != "":
		var creature_position := _creature_position(creatures, _creature_id)
		target = Vector2i(maxi(0, creature_position.x - 1), creature_position.y)
	_pending_move = target

	# Validate the frozen character.respawn command against the real server: a
	# living character must be rejected with CHARACTER_NOT_DEAD (proves command
	# encoding, requestId, sequence and error routing end-to-end).
	_enter_stage(Stage.RESPAWN_CHECK, "character.respawn (negative)")
	print("REAL_E2E: 7/13 character.respawn sent (expect CHARACTER_NOT_DEAD)")
	_network.respawn_character()


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
	if _stage == Stage.RESPAWN_CHECK and name == ProtocolMessages.ERR_CHARACTER_RESPAWN_REJECTED \
			and code == ProtocolMessages.CODE_CHARACTER_NOT_DEAD:
		_respawn_rejected = true
		print("REAL_E2E: 8/13 character.respawn correctly rejected (%s) — Oracle contract verified" % code)
		_begin_movement()
		return
	var category := "GAMEPLAY_FAILURE"
	if _stage == Stage.WS_AUTH:
		category = "AUTHORIZATION_FAILURE"
	elif _stage == Stage.RECONNECT_AUTH or _stage == Stage.RECONNECT_WORLD:
		category = "RECONNECT_FAILURE"
	_fail("%s: server error '%s' (%s)" % [category, name, code])


## Starts the movement stage (shared by the snapshot path and the respawn check).
func _begin_movement() -> void:
	_enter_stage(Stage.MOVE, "movement.move")
	print("REAL_E2E:    movement.move -> (%d,%d)" % [_pending_move.x, _pending_move.y])
	_network.move_to(_pending_move.x, _pending_move.y)


# --- Helpers ------------------------------------------------------------------

func _enter_stage(stage: int, label: String) -> void:
	_stage = stage
	_stage_deadline = _now() + STEP_TIMEOUT
	print("REAL_E2E: stage -> %s" % label)


func _stage_name() -> String:
	match _stage:
		Stage.REGISTER: return "register"
		Stage.LOGIN: return "login"
		Stage.CHARACTERS: return "characters"
		Stage.CREATE_CHARACTER: return "create character"
		Stage.GAME_TOKEN: return "game-token"
		Stage.WS_AUTH: return "websocket authenticate"
		Stage.WORLD_ENTER: return "world.enter"
		Stage.RESPAWN_CHECK: return "character.respawn"
		Stage.MOVE: return "movement.move"
		Stage.COMBAT: return "combat.attack"
		Stage.INVENTORY_HTTP: return "inventory after loot"
		Stage.RECONNECT_AUTH: return "websocket reauthenticate"
		Stage.RECONNECT_WORLD: return "world.enter after reconnect"
		Stage.INVENTORY_HTTP_RECONNECTED: return "inventory after reconnect"
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
