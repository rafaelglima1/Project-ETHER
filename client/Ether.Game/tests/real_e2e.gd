extends SceneTree

## Real end-to-end harness against the deployed backend (Oracle Cloud).
##
## Drives the *real* client stack (HttpApiClient + WebSocketTransport) through
## the full M4 flow and prints each step as evidence:
##   login -> characters -> game-token -> WSS -> game.authenticate ->
##   game.authenticated -> world.enter -> world.snapshot -> movement.move ->
##   movement.accepted
##
## Usage (client-only, never starts a local backend):
##   set ETHER_CLIENT_API_URL=https://<api-host>
##   set ETHER_CLIENT_WS_URL=wss://<game-host>/game
##   set ETHER_E2E_EMAIL=hero@example.com
##   set ETHER_E2E_PASSWORD=<password>
##   godot --headless --path client/Ether.Game --script res://tests/real_e2e.gd
##
## Exit codes: 0 = full flow PASS, 1 = flow FAIL, 2 = not configured (BLOCKED).

enum Stage { IDLE, LOGIN, CHARACTERS, GAME_TOKEN, WS_AUTH, WORLD_ENTER, MOVE, DONE }

const STEP_TIMEOUT := 15.0
const TOTAL_TIMEOUT := 60.0

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

	var now := _now()
	_total_deadline = now + TOTAL_TIMEOUT
	_enter_stage(Stage.LOGIN, "login")
	var request_id := _api.new_request_id()
	_pending[request_id] = "login"
	_api.login(_email, _password, request_id)


func _process(_delta: float) -> bool:
	var now := _now()
	if now > _total_deadline:
		return _fail("total timeout at stage '%s'" % _stage_name())
	if _stage != Stage.DONE and _stage != Stage.IDLE and now > _stage_deadline:
		return _fail("step timeout at stage '%s'" % _stage_name())
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
		print("REAL_E2E: 1/8 login ok (accountId=%s, accessToken=***)" % _account_id)
		_enter_stage(Stage.CHARACTERS, "characters")
		var rid := _api.new_request_id()
		_pending[rid] = "characters"
		_api.list_characters(_account_id, rid)
	elif intent == "characters":
		var characters: Array = data if typeof(data) == TYPE_ARRAY else []
		if characters.is_empty():
			_fail("account has no characters; create one via the API first")
			return
		_character_id = String(characters[0].get("characterId", ""))
		print("REAL_E2E: 2/8 characters ok (%d; using %s)" % [characters.size(), _character_id])
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
		print("REAL_E2E: 3/8 game-token ok (gameToken=***)")
		_enter_stage(Stage.WS_AUTH, "websocket authenticate")
		_network.connect_to_server()


# --- WebSocket ----------------------------------------------------------------

func _on_event(name: String, payload: Dictionary) -> void:
	if name == ProtocolMessages.EVT_GAME_AUTHENTICATED:
		var character_id := String(payload.get("characterId", ""))
		print("REAL_E2E: 4/8 connected + game.authenticated ok")
		if character_id != _character_id:
			_fail("server authenticated unexpected character %s" % character_id)
			return
		print("REAL_E2E: 5/8 game.authenticated (sessionId=%s)" % String(payload.get("sessionId", "")))
		_enter_stage(Stage.WORLD_ENTER, "world.enter")
		_network.enter_world()
	elif name == ProtocolMessages.EVT_MOVEMENT_ACCEPTED:
		var character_id := String(payload.get("characterId", ""))
		if character_id != _player_id:
			return
		_position = Vector2i(int(payload.get("x", 0)), int(payload.get("y", 0)))
		print("REAL_E2E: 8/8 movement.accepted (%d,%d)" % [_position.x, _position.y])
		_pass()


func _on_snapshot(payload: Dictionary) -> void:
	var player: Dictionary = payload.get("player", {}) if typeof(payload.get("player")) == TYPE_DICTIONARY else {}
	_player_id = String(player.get("characterId", _character_id))
	_map_id = int(payload.get("mapId", -1))
	_position = Vector2i(int(player.get("x", 0)), int(player.get("y", 0)))
	print("REAL_E2E: 6/8 world.snapshot ok (mapId=%d, width=%d, height=%d, player=%s @ %d,%d)" % [
		_map_id,
		int(payload.get("width", 0)),
		int(payload.get("height", 0)),
		_player_id,
		_position.x,
		_position.y,
	])

	# Move to an adjacent in-bounds tile (server validates; client never decides).
	var target := Vector2i(_position.x + 1, _position.y)
	_enter_stage(Stage.MOVE, "movement.move")
	print("REAL_E2E: 7/8 movement.move -> (%d,%d)" % [target.x, target.y])
	_network.move_to(target.x, target.y)


func _on_error(name: String, code: String, _message: String) -> void:
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
		Stage.DONE: return "done"
		_: return "idle"


func _pass() -> void:
	print("REAL_E2E: PASS — full M4 flow verified end-to-end")
	quit(0)


func _fail(reason: String) -> bool:
	print("REAL_E2E: FAIL — %s" % reason)
	quit(1)
	return true


func _now() -> float:
	return float(Time.get_ticks_msec()) / 1000.0
