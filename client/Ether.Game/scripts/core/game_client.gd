extends Node

## GameClient — autoloaded facade for the Godot client.
##
## Single entry point the screens/world talk to. Wires the network layer, HTTP
## API client and client-side state together, then re-emits server messages as
## UI-friendly signals. It never decides gameplay outcomes.
##
## Real M4 flow: login -> characters -> game-token -> WS /game ->
## game.authenticate -> game.authenticated -> world.enter -> world.snapshot ->
## movement.move -> movement.accepted.

signal state_changed(state: int)
signal connection_status(text: String)
signal log_message(text: String)
signal authenticated(display_name: String)
signal characters_changed(characters: Array)
signal world_entered
signal error_received(code: String, message: String)
signal feedback(text: String)
signal inventory_changed(items: Array)
signal rtt_changed(ms: int)

var config: ClientConfig = null
var network: NetworkClient = null
var api: ApiClient = null
var client_state := ClientState.new()
var world_state := WorldState.new()

var last_event_name := ""
var last_error_code := ""

var _backend: MockBackend = null
var _pending: Dictionary = {}
var _entered_world := false
var _mode := "mock"
var _autoplay := false
var _autoplay_started := false


func _ready() -> void:
	config = ClientConfig.from_environment()
	_autoplay = OS.get_environment("ETHER_AUTOPLAY") == "1"
	_build(config)


func _build(p_config: ClientConfig) -> void:
	config = p_config
	_mode = config.mode_name()

	if config.is_mock():
		_backend = MockBackend.new()

	network = NetworkClient.new()
	network.name = "NetworkClient"
	add_child(network)
	network.configure(config)
	if config.is_mock():
		network.set_transport(MockTransport.new(_backend, config.mock_latency_seconds))
		api = MockApiClient.new(_backend)
	else:
		api = HttpApiClient.new(config.api_base_url)
	api.name = "ApiClient"
	add_child(api)
	api.completed.connect(_on_api_completed)

	network.state_changed.connect(_on_network_state_changed)
	network.server_connected.connect(_on_server_connected)
	network.server_disconnected.connect(_on_server_disconnected)
	network.event_received.connect(_on_event)
	network.snapshot_received.connect(_on_snapshot)
	network.error_received.connect(_on_protocol_error)
	network.log_message.connect(_on_network_log)
	network.pong_received.connect(_on_pong)


# --- Public API (used by screens/world) ---------------------------------------

func start() -> void:
	log_line("Client ready (mode=%s, profile=%s)" % [_mode, config.profile_name()])
	if config.is_real() and not config.has_endpoints():
		report_error("CONFIG", "Backend endpoints are not configured.")
		return
	if network.is_connected_to_server() or network.state.current() == AppState.State.CONNECTING:
		return
	network.connect_to_server()
	if _autoplay and config.is_mock():
		call_deferred("_autoplay_login")


func request_login(email: String, password: String) -> void:
	client_state.email = email
	var request_id := api.new_request_id()
	_pending[request_id] = "login"
	api.login(email, password, request_id)


func request_register(email: String, password: String) -> void:
	client_state.email = email
	var request_id := api.new_request_id()
	_pending[request_id] = "register"
	api.register(email, password, request_id)


func request_characters() -> void:
	var request_id := api.new_request_id()
	_pending[request_id] = "characters"
	api.list_characters(client_state.account_id, request_id)


func request_create_character(name: String, character_class: String) -> void:
	var request_id := api.new_request_id()
	_pending[request_id] = "create_character"
	api.create_character(client_state.account_id, name, character_class, request_id)


## Selecting a character obtains its game token; the WS connection follows.
func request_select_character(character_id: String) -> void:
	client_state.select(character_id)
	request_game_token(character_id)


func request_game_token(character_id: String) -> void:
	var request_id := api.new_request_id()
	_pending[request_id] = "game_token"
	api.request_game_token(character_id, request_id)


func request_enter_world() -> void:
	if client_state.selected_character_id == "":
		report_error("NO_CHARACTER", "Select a character first.")
		return
	_ensure_authenticated()


func request_move(x: int, y: int) -> void:
	if network.state.current() == AppState.State.IN_WORLD:
		network.move_to(x, y)


## M4 has no combat: the client must not emit combat commands.
func request_attack(_target_id: String) -> void:
	feedback.emit("Combat is not available yet.")


func request_interact(_target_id: String) -> void:
	feedback.emit("Nothing to interact with yet.")


func request_disconnect() -> void:
	_entered_world = false
	network.disconnect_from_server()
	client_state.clear_session()
	world_state.clear()
	network.reset_state()


func state() -> int:
	return network.state.current()


func state_name() -> String:
	return AppState.state_name(network.state.current())


func mode_name() -> String:
	return _mode


func log_line(text: String) -> void:
	print("[client] ", text)
	log_message.emit(text)


func report_error(code: String, message: String) -> void:
	last_error_code = code
	log_line("Error %s: %s" % [code, message])
	error_received.emit(code, message)
	feedback.emit(message)


## Diagnostics for the debug overlay. Never includes credentials.
func debug_snapshot() -> Dictionary:
	return {
		"mode": _mode,
		"server": config.game_websocket_url,
		"api": config.api_base_url,
		"connection": AppState.state_name(network.state.current()),
		"session": "Authenticated" if network.state.current() >= AppState.State.AUTHENTICATED else "No session",
		"rtt_ms": network.last_rtt_ms,
		"character_id": client_state.selected_character_id,
		"map_id": world_state.map_id,
		"position": "%d, %d" % [int(world_state.player.get("x", 0)), int(world_state.player.get("y", 0))],
		"last_event": last_event_name,
		"last_error": last_error_code,
	}


# --- API results --------------------------------------------------------------

func _on_api_completed(request_id: String, ok: bool, data: Variant, error: String) -> void:
	var intent := String(_pending.get(request_id, ""))
	_pending.erase(request_id)

	if not ok:
		report_error("ApiError", error)
		log_line("API '%s' failed: %s" % [intent, error])
		return

	if intent == "login" or intent == "register":
		client_state.set_session(_as_dictionary(data))
		api.set_token(client_state.access_token)
		authenticated.emit(client_state.display_name())
		log_line("Authenticated as %s" % client_state.display_name())
		request_characters()
	elif intent == "characters":
		var characters := _as_array(data)
		client_state.set_characters(characters)
		characters_changed.emit(characters)
		_maybe_autoplay_select(characters)
	elif intent == "create_character":
		log_line("Character created.")
		request_characters()
	elif intent == "game_token":
		client_state.game_token = String(_as_dictionary(data).get("gameToken", ""))
		client_state.game_token_expires_at = String(_as_dictionary(data).get("expiresAt", ""))
		log_line("Game token acquired.")
		_ensure_authenticated()


func _ensure_authenticated() -> void:
	if client_state.game_token == "":
		if client_state.selected_character_id != "":
			request_game_token(client_state.selected_character_id)
		return
	if network.is_connected_to_server():
		network.authenticate(client_state.game_token)
	else:
		# Connect if idle; if already connecting, _on_server_connected authenticates.
		network.connect_to_server()


# --- Network callbacks --------------------------------------------------------

func _on_network_state_changed(_previous: int, current: int) -> void:
	state_changed.emit(current)
	connection_status.emit(AppState.state_name(current))
	if current == AppState.State.RECONNECTING:
		feedback.emit("Reconnecting...")


func _on_server_connected() -> void:
	if client_state.game_token != "":
		network.authenticate(client_state.game_token)


func _on_server_disconnected(reason: String) -> void:
	_entered_world = false
	log_line("Disconnected: %s" % reason)
	feedback.emit("Connection lost. Reconnecting...")


func _on_network_log(text: String) -> void:
	log_line(text)


func _on_pong(rtt_ms: int) -> void:
	rtt_changed.emit(rtt_ms)


func _on_snapshot(payload: Dictionary) -> void:
	world_state.apply_snapshot(payload)
	var player := world_state.player
	if player.has("inventory"):
		client_state.set_inventory(player.get("inventory", []))
		inventory_changed.emit(client_state.inventory)
	_entered_world = true
	network.mark_in_world()
	log_line("World snapshot: map %d (%dx%d) at (%d,%d)" % [
		world_state.map_id,
		int(world_state.map.get("width", 0)),
		int(world_state.map.get("height", 0)),
		int(player.get("x", 0)),
		int(player.get("y", 0)),
	])
	world_entered.emit()
	if _autoplay and not _autoplay_started:
		_autoplay_started = true
		_autoplay_smoke()


func _on_event(name: String, payload: Dictionary) -> void:
	last_event_name = name

	if name == ProtocolMessages.EVT_GAME_AUTHENTICATED:
		_handle_authenticated(payload)
	elif name == ProtocolMessages.EVT_MOVEMENT_ACCEPTED:
		_handle_movement_accepted(payload)
	elif name == ProtocolMessages.EVT_SYSTEM_PONG:
		pass
	else:
		log_line("Unhandled event '%s'." % name)


func _handle_authenticated(payload: Dictionary) -> void:
	network.mark_authenticated()
	var character_id := String(payload.get("characterId", ""))
	client_state.session_id = String(payload.get("sessionId", ""))

	if client_state.selected_character_id != "" and character_id != client_state.selected_character_id:
		# The server is authoritative; never adopt a mismatched character.
		log_line("Authenticated character mismatch; refusing to enter world.")
		report_error("AUTH_CHARACTER_MISMATCH", "The server authenticated an unexpected character.")
		return

	network.enter_world()


func _handle_movement_accepted(payload: Dictionary) -> void:
	var result := world_state.apply_movement(payload)
	if not result.get("applied", false):
		log_line("Ignored movement.accepted (%s)." % String(result.get("reason", "")))
		return
	log_line("Movement accepted to (%d, %d)." % [
		int(payload.get("x", 0)),
		int(payload.get("y", 0)),
	])


func _on_protocol_error(name: String, code: String, _server_message: String) -> void:
	var user_message := ProtocolErrors.user_message(code)
	report_error(code, user_message)
	log_line("Server error '%s' (%s)." % [name, code])

	if name == ProtocolMessages.ERR_GAME_AUTHENTICATE_REJECTED:
		if code == ProtocolMessages.CODE_TOKEN_EXPIRED or code == ProtocolMessages.CODE_INVALID_TOKEN:
			if client_state.selected_character_id != "":
				log_line("Game token rejected; requesting a fresh one.")
				request_game_token(client_state.selected_character_id)
	elif name == ProtocolMessages.ERR_WORLD_ENTER_REJECTED:
		if code == ProtocolMessages.CODE_NOT_AUTHENTICATED and client_state.game_token != "":
			network.authenticate(client_state.game_token)


# --- Autoplay (dev/smoke only, gated by ETHER_AUTOPLAY) -----------------------

func _autoplay_login() -> void:
	log_line("Autoplay enabled; signing in with a mock account.")
	request_login("hero@example.com", "password")


func _maybe_autoplay_select(characters: Array) -> void:
	if _autoplay and client_state.selected_character_id == "" and not characters.is_empty():
		request_select_character(String(characters[0].get("characterId", "")))


func _autoplay_smoke() -> void:
	await get_tree().create_timer(0.3).timeout
	request_move(5, 5)
	await get_tree().create_timer(0.5).timeout
	var after_move := world_state.player_position()
	request_move(999, 999)
	await get_tree().create_timer(0.5).timeout
	var after_reject := world_state.player_position()
	log_line("Autoplay smoke done. move=%s rejected_kept=%s" % [str(after_move), str(after_reject)])


# --- Helpers ------------------------------------------------------------------

func _as_dictionary(value: Variant) -> Dictionary:
	return value if typeof(value) == TYPE_DICTIONARY else {}


func _as_array(value: Variant) -> Array:
	if typeof(value) == TYPE_ARRAY:
		return value
	if typeof(value) == TYPE_DICTIONARY and typeof(value.get("characters", null)) == TYPE_ARRAY:
		return value["characters"]
	return []
