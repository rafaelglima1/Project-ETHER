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
signal target_changed(target_id: String)
signal combat_result(attacker_id: String, target_id: String, damage: int, critical: bool, target_health: int, target_max_health: int, defeated: bool)
signal creature_defeated(creature_id: String)
signal experience_changed(level: int, experience: int, gained: int, levels_gained: int)
signal loot_received(items: Array)
signal player_died
signal player_respawned

var config: ClientConfig = null
var network: NetworkClient = null
var api: ApiClient = null
var client_state := ClientState.new()
var world_state := WorldState.new()

## Currently selected entity id (client-side selection only; server authority).
var selected_target_id := ""

var last_event_name := ""
var last_error_code := ""

var _backend: MockBackend = null
var _pending: Dictionary = {}
var _entered_world := false
var _mode := "mock"
var _autoplay := false
var _autoplay_started := false
var _autoplay_created := false
var _player_was_dead := false


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
	if api.has_signal("http_trace"):
		api.http_trace.connect(_on_http_trace)

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
	log_line("Client ready (mode=%s, profile=%s, platform=%s)" % [_mode, config.profile_name(), OS.get_name()])
	if config.is_real() and not config.has_endpoints():
		report_error("CONFIG", "Backend endpoints are not configured.")
		return
	if config.is_real() and config.has_endpoints() and not config.is_secure():
		log_line("WARNING: REAL mode is using non-secure endpoints (%s / %s)" % [
			config.api_base_url, config.game_websocket_url])
	if network.is_connected_to_server() or network.state.current() == AppState.State.CONNECTING:
		return
	network.connect_to_server()
	if _autoplay:
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
	if network.state.current() != AppState.State.IN_WORLD:
		return
	if world_state.is_player_dead():
		feedback.emit("You are dead.")
		return
	network.move_to(x, y)


## True when the local player is mirrored as dead (server-authoritative).
func is_player_dead() -> bool:
	return world_state.is_player_dead()


## Emits death/respawn transitions exactly once, based on the authoritative
## mirrored state. Called after any update that can change the player's life
## state (combat result, snapshot, movement).
func _sync_player_life_state() -> void:
	var dead := world_state.is_player_dead()
	if dead and not _player_was_dead:
		_player_was_dead = true
		clear_target()
		log_line("Player died.")
		player_died.emit()
	elif not dead and _player_was_dead:
		_player_was_dead = false
		log_line("Player respawned (state=%s)." % String(world_state.player.get("state", "")))
		player_respawned.emit()


## Selects a target entity for the HUD/attack assist. Selection is client-side
## only; it never changes authoritative state.
func select_target(target_id: String) -> void:
	if target_id == selected_target_id:
		return
	selected_target_id = target_id
	target_changed.emit(target_id)


func clear_target() -> void:
	select_target("")


## Sends an attack intent (M5). The server decides range, cooldown, damage and
## death; the client sends nothing but target + ability.
func request_attack(target_id: String = "", ability_id: String = ProtocolMessages.ABILITY_BASIC_ATTACK) -> void:
	if network.state.current() != AppState.State.IN_WORLD:
		feedback.emit("You are not in the world.")
		return
	if bool(world_state.player.get("dead", false)):
		feedback.emit("You cannot attack while dead.")
		return
	var resolved := target_id
	if resolved == "":
		resolved = selected_target_id
	if resolved == "":
		resolved = world_state.nearest_creature(world_state.player_position(), 1)
	if resolved == "" or not world_state.has_entity(resolved):
		feedback.emit("No target selected.")
		return
	var kind := String(world_state.get_entity(resolved).get("kind", "creature"))
	var target_type := ProtocolMessages.TARGET_TYPE_CHARACTER if kind == "player" else ProtocolMessages.TARGET_TYPE_CREATURE
	network.attack(ability_id, resolved, target_type)


func request_attack_ability(ability_id: String) -> void:
	request_attack(selected_target_id, ability_id)


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
		"creatures": world_state.creature_count(),
		"target": selected_target_id,
		"dead": world_state.is_player_dead(),
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
		if _autoplay and characters.is_empty() and not _autoplay_created:
			_autoplay_created = true
			request_create_character("Probe%d" % (Time.get_ticks_msec() % 100000), "Warrior")
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


func _on_http_trace(method: String, path: String, status: int) -> void:
	log_line("HTTP %s %s -> %d" % [method, path, status])


func _on_pong(rtt_ms: int) -> void:
	rtt_changed.emit(rtt_ms)


func _on_snapshot(payload: Dictionary) -> void:
	world_state.apply_snapshot(payload)
	clear_target()
	_sync_player_life_state()
	var player := world_state.player
	if player.has("inventory"):
		client_state.set_inventory(player.get("inventory", []))
		inventory_changed.emit(client_state.inventory)
	_entered_world = true
	network.mark_in_world()
	log_line("World snapshot: map %d (%dx%d) at (%d,%d), %d creature(s)" % [
		world_state.map_id,
		int(world_state.map.get("width", 0)),
		int(world_state.map.get("height", 0)),
		int(player.get("x", 0)),
		int(player.get("y", 0)),
		world_state.creature_count(),
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
	elif name == ProtocolMessages.EVT_COMBAT_RESULT:
		_handle_combat_result(payload)
	elif name == ProtocolMessages.EVT_WORLD_CREATURE_MOVED:
		world_state.apply_creature_update(payload)
		if selected_target_id != "" and not bool(world_state.get_entity(selected_target_id).get("dead", false)):
			target_changed.emit(selected_target_id)
	elif name == ProtocolMessages.EVT_SYSTEM_PONG:
		pass
	else:
		log_line("Unhandled event '%s'." % name)


func _handle_combat_result(payload: Dictionary) -> void:
	var result := world_state.apply_combat_result(payload)
	var attacker_id := String(payload.get("attackerId", ""))
	var target_id := String(result.get("target_id", ""))
	var damage := int(payload.get("damage", 0))
	var critical := bool(payload.get("critical", false))
	var defeated := bool(result.get("defeated", false))

	combat_result.emit(
		attacker_id,
		target_id,
		damage,
		critical,
		int(result.get("target_health", 0)),
		int(result.get("target_max_health", 0)),
		defeated)

	if attacker_id == world_state.player_id:
		feedback.emit("%s for %d damage%s" % ["Critical hit" if critical else "You hit", damage, "!" if critical else ""])
	elif target_id == world_state.player_id:
		feedback.emit("You took %d damage." % damage)

	if defeated and target_id != "" and target_id != world_state.player_id:
		creature_defeated.emit(target_id)
		if selected_target_id == target_id:
			clear_target()

	# M7 additive: progression and loot may ride along on combat.result. The
	# client applies them only when the server actually populated them.
	_apply_combat_progression(payload)
	_apply_combat_loot(payload)

	# Player death (creature attacks arrive as combat.result with the player as
	# target) and any respawn signalled through a state change are reflected here.
	_sync_player_life_state()


## Applies additive progression fields. The server writes level=0 when no reward
## is granted, so progression is applied only when level > 0.
func _apply_combat_progression(payload: Dictionary) -> void:
	var level := int(payload.get(ProtocolMessages.FIELD_LEVEL, 0))
	if level <= 0:
		return
	var experience := int(payload.get(ProtocolMessages.FIELD_EXPERIENCE, 0))
	var gained := int(payload.get(ProtocolMessages.FIELD_EXPERIENCE_GAINED, 0))
	var levels_gained := int(payload.get(ProtocolMessages.FIELD_LEVELS_GAINED, 0))

	if world_state.player_id != "":
		world_state.apply_delta({"player": {
			"id": world_state.player_id,
			"level": level,
			"experience": experience,
		}})

	if gained > 0:
		feedback.emit("+%d XP" % gained)
	if levels_gained > 0:
		feedback.emit("Level up! You are now level %d." % level)

	experience_changed.emit(level, experience, gained, levels_gained)


## Applies additive loot from combat.result to the inventory mirror.
func _apply_combat_loot(payload: Dictionary) -> void:
	if not payload.has(ProtocolMessages.FIELD_LOOT):
		return
	var raw: Variant = payload.get(ProtocolMessages.FIELD_LOOT)
	if typeof(raw) != TYPE_ARRAY or raw.is_empty():
		return
	var items: Array = raw
	client_state.add_loot(items)
	loot_received.emit(items)
	inventory_changed.emit(client_state.inventory)


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
	_sync_player_life_state()


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
	var email := OS.get_environment("ETHER_E2E_EMAIL")
	var password := OS.get_environment("ETHER_E2E_PASSWORD")
	if email == "":
		email = "hero@example.com"
	if password == "":
		password = "password"
	log_line("Autoplay enabled; signing in as %s." % email)
	request_login(email, password)


func _maybe_autoplay_select(characters: Array) -> void:
	if _autoplay and client_state.selected_character_id == "" and not characters.is_empty():
		request_select_character(String(characters[0].get("characterId", "")))


func _autoplay_smoke() -> void:
	await get_tree().create_timer(0.3).timeout
	var creature_id := world_state.nearest_creature(world_state.player_position(), 64)
	if creature_id == "":
		log_line("Autoplay: no creatures in snapshot.")
		return

	var creature_position := world_state.entity_position(creature_id)
	var adjacent := Vector2i(creature_position.x - 1, creature_position.y)
	if adjacent.x < 0:
		adjacent.x = creature_position.x + 1
	request_move(adjacent.x, adjacent.y)
	await get_tree().create_timer(0.8).timeout

	for i in range(40):
		if _autoplay_target_is_done(creature_id):
			break
		request_attack(creature_id)
		await get_tree().create_timer(0.35).timeout

	log_line("Autoplay M6/M7 smoke done. creature_defeated=%s player_hp=%d level=%d xp=%d loot=%d player_dead=%s creatures=%d" % [
		str(bool(world_state.get_entity(creature_id).get("dead", false))),
		int(world_state.player.get("hp", 0)),
		int(world_state.player.get("level", 1)),
		int(world_state.player.get("experience", 0)),
		client_state.inventory.size(),
		str(bool(world_state.player.get("dead", false))),
		world_state.creature_count(),
	])


func _autoplay_target_is_done(creature_id: String) -> bool:
	if bool(world_state.player.get("dead", false)):
		return true
	if not world_state.has_entity(creature_id):
		return true
	return bool(world_state.get_entity(creature_id).get("dead", false))


# --- Helpers ------------------------------------------------------------------

func _as_dictionary(value: Variant) -> Dictionary:
	return value if typeof(value) == TYPE_DICTIONARY else {}


func _as_array(value: Variant) -> Array:
	if typeof(value) == TYPE_ARRAY:
		return value
	if typeof(value) == TYPE_DICTIONARY and typeof(value.get("characters", null)) == TYPE_ARRAY:
		return value["characters"]
	return []
