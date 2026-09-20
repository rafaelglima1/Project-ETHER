extends Node

## GameClient — autoloaded facade for the Godot client.
##
## Single entry point the screens/world talk to. It wires the network layer,
## API client and client-side state together and re-emits server messages as
## UI-friendly signals. It never decides gameplay outcomes.

signal state_changed(state: int)
signal connection_status(text: String)
signal log_message(text: String)
signal authenticated(display_name: String)
signal characters_changed(characters: Array)
signal character_selected(character: Dictionary)
signal world_entered
signal error_received(code: String, message: String)

signal damage_dealt(target_id: String, amount: int, target_hp: int, target_max_hp: int)
signal creature_defeated(target_id: String)
signal experience_gained(level: int, total: int, amount: int)
signal level_up(level: int)
signal loot_received(items: Array)
signal inventory_changed(items: Array)
signal combat_feedback(text: String)
signal player_died
signal player_respawned
signal reconnecting(attempt: int)

var config: ClientConfig = null
var network: NetworkClient = null
var api: ApiClient = null
var client_state := ClientState.new()
var world_state := WorldState.new()

var _backend: MockBackend = null
var _pending: Dictionary = {}
var _entered_world := false
var _mode := "mock"
var _autoplay := false
var _autoplay_combat_started := false


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
	network.delta_received.connect(_on_delta)
	network.error_received.connect(_on_error)
	network.log_message.connect(_on_network_log)


# --- Public API (used by screens/world) ---------------------------------------

func start() -> void:
	log_line("Client ready (mode=%s)" % _mode)
	if network.is_connected_to_server() or network.state.current() == AppState.State.CONNECTING:
		return
	network.connect_to_server()
	if _autoplay and config.is_mock():
		call_deferred("_autoplay_login")


## Development/smoke helper: drives login -> character -> world without input.
## Gated behind the ETHER_AUTOPLAY environment variable; never production.
func _autoplay_login() -> void:
	log_line("Autoplay enabled; signing in with a mock account.")
	request_login("Adventurer", "")


func request_login(username: String, password: String) -> void:
	var request_id := api.new_request_id()
	_pending[request_id] = "login"
	api.login(username, password, request_id)


func request_register(username: String, password: String) -> void:
	var request_id := api.new_request_id()
	_pending[request_id] = "register"
	api.register(username, password, request_id)


func request_characters() -> void:
	var request_id := api.new_request_id()
	_pending[request_id] = "characters"
	api.list_characters(client_state.account_id, request_id)


func request_create_character(name: String, character_class: String) -> void:
	var request_id := api.new_request_id()
	_pending[request_id] = "create_character"
	api.create_character(client_state.account_id, name, character_class, request_id)


func request_select_character(character_id: String) -> void:
	var request_id := api.new_request_id()
	_pending[request_id] = "select_character"
	api.select_character(character_id, request_id)


func request_enter_world() -> void:
	if client_state.selected_character_id != "":
		network.select_character(client_state.selected_character_id)


func request_move(x: int, y: int) -> void:
	if network.state.current() == AppState.State.IN_WORLD:
		network.move_to(x, y)


func request_attack(target_id: String) -> void:
	if network.state.current() == AppState.State.IN_WORLD:
		network.attack(target_id)


func request_interact(target_id: String) -> void:
	if network.state.current() == AppState.State.IN_WORLD:
		network.interact(target_id)


func request_disconnect() -> void:
	_entered_world = false
	network.disconnect_from_server()


func state() -> int:
	return network.state.current()


func state_name() -> String:
	return AppState.state_name(network.state.current())


func mode_name() -> String:
	return _mode


func _maybe_autoplay_select(characters: Array) -> void:
	if _autoplay and client_state.selected_character_id == "" and not characters.is_empty():
		request_select_character(String(characters[0].get("characterId", "")))


func log_line(text: String) -> void:
	print("[client] ", text)
	log_message.emit(text)


# --- API results --------------------------------------------------------------

func _on_api_completed(request_id: String, ok: bool, data: Dictionary, error: String) -> void:
	var intent := String(_pending.get(request_id, ""))
	_pending.erase(request_id)

	if not ok:
		error_received.emit("ApiError", error)
		log_line("API '%s' failed: %s" % [intent, error])
		return

	if intent == "login" or intent == "register":
		client_state.set_session(data)
		api.set_token(client_state.access_token)
		authenticated.emit(client_state.display_name)
		log_line("Authenticated as %s" % client_state.display_name)
		_begin_gameplay_session()
	elif intent == "characters":
		var characters: Array = data.get("characters", [])
		client_state.set_characters(characters)
		characters_changed.emit(characters)
		_maybe_autoplay_select(characters)
	elif intent == "create_character":
		var created: Array = data.get("characters", [])
		client_state.set_characters(created)
		characters_changed.emit(created)
		log_line("Character created.")
	elif intent == "select_character":
		var character_id := String(data.get("characterId", client_state.selected_character_id))
		client_state.select(character_id)
		character_selected.emit(client_state.selected_character())
		if network.is_connected_to_server():
			network.select_character(character_id)


func _begin_gameplay_session() -> void:
	if network.is_connected_to_server():
		network.authenticate(client_state.game_token, client_state.account_id)
	else:
		network.connect_to_server()


# --- Network callbacks --------------------------------------------------------

func _on_network_state_changed(_previous: int, current: int) -> void:
	state_changed.emit(current)
	connection_status.emit(AppState.state_name(current))
	if current == AppState.State.RECONNECTING and network.reconnect != null:
		reconnecting.emit(network.reconnect.attempts() + 1)


func _on_server_connected() -> void:
	if client_state.game_token != "":
		network.authenticate(client_state.game_token, client_state.account_id)


func _on_server_disconnected(reason: String) -> void:
	_entered_world = false
	log_line("Disconnected: %s" % reason)
	combat_feedback.emit("Connection lost. Reconnecting...")


func _on_network_log(text: String) -> void:
	log_line(text)


func _on_error(code: String, message: String) -> void:
	log_line("Server error %s: %s" % [code, message])
	error_received.emit(code, message)


func _on_snapshot(payload: Dictionary) -> void:
	world_state.apply_snapshot(payload)
	var player: Dictionary = payload.get("player", {}) if typeof(payload.get("player")) == TYPE_DICTIONARY else {}
	if player.has("inventory"):
		client_state.set_inventory(player.get("inventory", []))
		inventory_changed.emit(client_state.inventory)
	_entered_world = true
	network.mark_in_world()
	log_line("World snapshot applied (%d entities)." % world_state.entities.size())
	world_entered.emit()
	if _autoplay and not _autoplay_combat_started:
		_autoplay_combat_started = true
		if _backend != null:
			_backend.set_wandering(false)
		_autoplay_combat_demo()


## Development/smoke helper: walk to a creature and fight it, exercising the
## world scene, HUD and combat feedback without input. See ETHER_AUTOPLAY.
func _autoplay_combat_demo() -> void:
	await get_tree().create_timer(0.3).timeout
	var creature_id := world_state.nearest_creature(world_state.player_position(), 100)
	if creature_id == "":
		log_line("Autoplay: no creature found.")
		return
	var target := world_state.entity_position(creature_id)
	request_move(target.x - 1, target.y)
	await get_tree().create_timer(0.5).timeout
	for i in range(30):
		if not world_state.has_entity(creature_id):
			break
		request_attack(creature_id)
		await get_tree().create_timer(0.3).timeout
	log_line("Autoplay combat demo done. Creature defeated=%s, XP=%d, inventory=%d" % [
		str(not world_state.has_entity(creature_id)),
		int(world_state.player.get("experience", 0)),
		client_state.inventory.size(),
	])


func _on_delta(payload: Dictionary) -> void:
	world_state.apply_delta(payload)


func _on_event(name: String, payload: Dictionary) -> void:
	if name == ProtocolMessages.EVT_AUTHENTICATED:
		network.mark_authenticated()
		client_state.set_session(payload)
		if client_state.selected_character_id != "":
			network.select_character(client_state.selected_character_id)
		request_characters()
	elif name == ProtocolMessages.EVT_CHARACTER_LIST:
		var characters: Array = payload.get("characters", [])
		client_state.set_characters(characters)
		characters_changed.emit(characters)
		_maybe_autoplay_select(characters)
	elif name == ProtocolMessages.EVT_CHARACTER_SELECTED:
		var character: Dictionary = payload.get("character", {}) if typeof(payload.get("character")) == TYPE_DICTIONARY else {}
		if character.has("characterId"):
			client_state.select(String(character["characterId"]))
		character_selected.emit(client_state.selected_character())
		network.enter_world()
	elif name == ProtocolMessages.EVT_WORLD_ENTERED:
		log_line("Entering world...")
	elif name == ProtocolMessages.EVT_CHARACTER_MOVED or name == ProtocolMessages.EVT_CREATURE_MOVED:
		_apply_entity_delta(payload)
	elif name == ProtocolMessages.EVT_CREATURE_SPAWNED or name == ProtocolMessages.EVT_ENTITY_SPAWN:
		_apply_entity_delta(payload)
	elif name == ProtocolMessages.EVT_ENTITY_UPDATE:
		_apply_entity_delta(payload)
	elif name == ProtocolMessages.EVT_ENTITY_DEATH or name == ProtocolMessages.EVT_CREATURE_DIED:
		var entity_id := String(payload.get("entityId", ""))
		world_state.remove_entity(entity_id)
		creature_defeated.emit(entity_id)
		combat_feedback.emit("A creature was defeated.")
	elif name == ProtocolMessages.EVT_COMBAT_RESULT or name == ProtocolMessages.EVT_DAMAGE_APPLIED:
		_handle_combat(payload)
	elif name == ProtocolMessages.EVT_EXPERIENCE_GAINED:
		var xp_total := int(payload.get("total", 0))
		var xp_level := int(payload.get("level", 1))
		var xp_amount := int(payload.get("amount", 0))
		world_state.apply_delta({"player": {
			"id": world_state.player_id,
			"experience": xp_total,
			"level": xp_level,
		}})
		experience_gained.emit(xp_level, xp_total, xp_amount)
		combat_feedback.emit("+%d XP" % xp_amount)
	elif name == ProtocolMessages.EVT_LEVEL_UP:
		var new_level := int(payload.get("level", 1))
		world_state.apply_delta({"player": {
			"id": world_state.player_id,
			"level": new_level,
			"experienceToNext": int(payload.get("experienceToNext", 0)),
		}})
		level_up.emit(new_level)
		combat_feedback.emit("Level up! You are now level %d." % new_level)
	elif name == ProtocolMessages.EVT_LOOT_RECEIVED or name == ProtocolMessages.EVT_LOOT_DROPPED:
		var items: Array = payload.get("items", [])
		loot_received.emit(items)
		if not items.is_empty():
			combat_feedback.emit("Loot received.")
	elif name == "InventoryUpdated" or name == ProtocolMessages.EVT_ITEM_PICKED_UP:
		var inventory: Array = payload.get("inventory", [])
		if not inventory.is_empty():
			client_state.set_inventory(inventory)
			inventory_changed.emit(inventory)
	elif name == ProtocolMessages.EVT_CHARACTER_DIED:
		world_state.apply_delta({"player": {
			"id": world_state.player_id,
			"hp": 0,
			"dead": true,
		}})
		player_died.emit()
		combat_feedback.emit("You have fallen. Respawning...")
	elif name == ProtocolMessages.EVT_CHARACTER_RESPAWNED:
		world_state.apply_delta({"player": {
			"id": world_state.player_id,
			"hp": int(payload.get("hp", 1)),
			"x": int(payload.get("x", 0)),
			"y": int(payload.get("y", 0)),
			"dead": false,
		}})
		player_respawned.emit()
		combat_feedback.emit("You have respawned.")
	elif name == "Interaction":
		combat_feedback.emit(String(payload.get("message", "")))
	elif name == ProtocolMessages.EVT_PONG or name == ProtocolMessages.EVT_CONNECTED:
		pass
	else:
		log_line("Unhandled event '%s'." % name)


func _handle_combat(payload: Dictionary) -> void:
	var target_id := String(payload.get("targetId", ""))
	var damage := int(payload.get("damage", payload.get("amount", 0)))
	var target_hp := int(payload.get("targetHp", payload.get("remainingHp", 0)))
	var target_max := int(payload.get("targetMaxHp", 0))
	if target_id != "" and world_state.has_entity(target_id):
		world_state.apply_delta({"upserts": [{"id": target_id, "hp": target_hp}]})
	damage_dealt.emit(target_id, damage, target_hp, target_max)
	if target_id == world_state.player_id:
		combat_feedback.emit("You took %d damage." % damage)


func _apply_entity_delta(payload: Dictionary, include_position: bool = true) -> void:
	var entity_id := String(payload.get("entityId", payload.get("id", "")))
	if entity_id == "":
		return
	var upsert := {"id": entity_id}
	if include_position and payload.has("x"):
		upsert["x"] = int(payload["x"])
	if include_position and payload.has("y"):
		upsert["y"] = int(payload["y"])
	if payload.has("hp"):
		upsert["hp"] = int(payload["hp"])
	if payload.has("level"):
		upsert["level"] = int(payload["level"])
	world_state.apply_delta({"upserts": [upsert]})
