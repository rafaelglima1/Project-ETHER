extends TestCase

## End-to-end mock flow through the real networking pipeline:
## M4 (session/world/movement) + M5 (combat) + M6 (creatures + AI).
## No engine tree needed: the NetworkClient loop is pumped manually.

const TOKEN := "mock-game-token"

var network: NetworkClient
var backend: MockBackend
var world: WorldState
var events: Array = []
var errors: Array = []
var combat_results: Array = []


func _setup(ai_enabled: bool = false, heartbeat_interval: float = 20.0) -> void:
	network = NetworkClient.new()
	world = WorldState.new()
	events = []
	errors = []
	combat_results = []

	var config := ClientConfig.new()
	config.mock_latency_seconds = 0.0
	config.heartbeat_interval_seconds = heartbeat_interval
	config.heartbeat_timeout_seconds = maxf(heartbeat_interval * 2.0, 1.0)
	network.configure(config)

	backend = network.transport().backend()
	backend.set_ai_enabled(ai_enabled)
	backend.set_critical_enabled(false)
	backend.issue_game_token(String(backend.characters[0]["characterId"]))

	network.event_received.connect(_on_event)
	network.snapshot_received.connect(_on_snapshot)
	network.error_received.connect(_on_error)

	network.connect_to_server()
	_pump(0.2)


func _on_event(name: String, payload: Dictionary) -> void:
	events.append(name)
	if name == ProtocolMessages.EVT_MOVEMENT_ACCEPTED:
		world.apply_movement(payload)
	elif name == ProtocolMessages.EVT_COMBAT_RESULT:
		combat_results.append(payload)
		world.apply_combat_result(payload)
	elif name == ProtocolMessages.EVT_WORLD_CREATURE_MOVED:
		world.apply_creature_update(payload)


func _on_snapshot(payload: Dictionary) -> void:
	world.apply_snapshot(payload)


func _on_error(name: String, code: String, _message: String) -> void:
	errors.append({"name": name, "code": code})


func _pump(seconds: float) -> void:
	var steps := maxi(1, int(ceil(seconds / 0.05)))
	for i in range(steps):
		network._process(0.05)


func _run_to_world() -> void:
	network.authenticate(TOKEN)
	_pump(0.2)
	network.mark_authenticated()
	network.enter_world()
	_pump(0.2)
	network.mark_in_world()


func _move_adjacent_to_first_creature() -> String:
	var creature_id := String(world.creatures()[0]["id"])
	var position := world.entity_position(creature_id)
	network.move_to(maxi(0, position.x - 1), position.y)
	_pump(0.3)
	return creature_id


func _cleanup() -> void:
	if network != null and is_instance_valid(network):
		network.free()
	network = null


func _has_error(name: String, code: String) -> bool:
	for entry in errors:
		if entry["name"] == name and entry["code"] == code:
			return true
	return false


func _player_attacked_combat_results() -> Array:
	var result: Array = []
	for payload in combat_results:
		if String(payload.get("targetId", "")) == world.player_id:
			result.append(payload)
	return result


# --- M4: session, world, movement ---------------------------------------------

func test_connect_and_authenticate() -> void:
	_setup()
	assert_eq(network.state.current(), AppState.State.CONNECTED)
	network.authenticate(TOKEN)
	_pump(0.2)
	assert_true(events.has(ProtocolMessages.EVT_GAME_AUTHENTICATED))
	network.mark_authenticated()
	assert_eq(network.state.current(), AppState.State.AUTHENTICATED)


func test_enter_world_applies_snapshot() -> void:
	_setup()
	_run_to_world()
	assert_eq(network.state.current(), AppState.State.IN_WORLD)
	assert_eq(world.player_id, String(backend.characters[0]["characterId"]))
	assert_eq(world.map_id, MockBackend.MAP_ID)
	assert_eq(world.player_position(), Vector2i(MockBackend.START_X, MockBackend.START_Y))


func test_snapshot_includes_creatures() -> void:
	_setup()
	_run_to_world()
	assert_eq(world.creature_count(), 3, "three creatures in the snapshot")
	var creature: Dictionary = world.creatures()[0]
	assert_eq(String(creature.get("kind", "")), "creature")
	assert_true(String(creature.get("name", "")).length() > 0)
	assert_true(int(creature.get("maxHp", 0)) > 0)


func test_movement_accepted_updates_position() -> void:
	_setup()
	_run_to_world()
	network.move_to(5, 5)
	_pump(0.2)
	assert_eq(world.player_position(), Vector2i(5, 5))
	assert_true(events.has(ProtocolMessages.EVT_MOVEMENT_ACCEPTED))


func test_movement_out_of_bounds_is_rejected() -> void:
	_setup()
	_run_to_world()
	network.move_to(999, 999)
	_pump(0.2)
	assert_eq(world.player_position(), Vector2i(0, 0))
	assert_true(_has_error(ProtocolMessages.ERR_MOVEMENT_REJECTED, ProtocolMessages.CODE_OUT_OF_BOUNDS))


func test_movement_too_far_is_rejected() -> void:
	_setup()
	_run_to_world()
	network.move_to(MockBackend.MAX_MOVE_DISTANCE + 5, 0)
	_pump(0.2)
	assert_eq(world.player_position(), Vector2i(0, 0))
	assert_true(_has_error(ProtocolMessages.ERR_MOVEMENT_REJECTED, ProtocolMessages.CODE_TOO_FAR))


func test_heartbeat_ping_pong_sets_rtt() -> void:
	_setup(false, 0.2)
	_run_to_world()
	_pump(0.6)
	assert_true(network.last_rtt_ms >= 0)


func test_disconnect_and_reconnect_restores_world() -> void:
	_setup()
	_run_to_world()
	network.disconnect_from_server()
	_pump(0.2)
	assert_eq(network.state.current(), AppState.State.DISCONNECTED)
	network.connect_to_server()
	_pump(0.2)
	_run_to_world()
	assert_eq(network.state.current(), AppState.State.IN_WORLD)
	assert_eq(world.creature_count(), 3, "creatures restored after reconnect")


func test_reconnect_during_combat_restores_world_and_combat() -> void:
	_setup(true)
	_run_to_world()

	# Let the AI reach and hit the player.
	_pump(4.0)
	assert_true(_player_attacked_combat_results().size() > 0, "combat happened before reconnect")

	network.disconnect_from_server()
	_pump(0.2)
	assert_eq(network.state.current(), AppState.State.DISCONNECTED)

	network.connect_to_server()
	_pump(0.2)
	_run_to_world()
	assert_eq(network.state.current(), AppState.State.IN_WORLD)
	assert_eq(world.creature_count(), 3, "world restored after combat reconnect")

	# Combat still works after the reconnect.
	var before := combat_results.size()
	var creature_id := _move_adjacent_to_first_creature()
	for i in range(6):
		network.attack(ProtocolMessages.ABILITY_BASIC_ATTACK, creature_id, ProtocolMessages.TARGET_TYPE_CREATURE)
		_pump(0.2)
		if combat_results.size() > before:
			break
	assert_true(combat_results.size() > before, "combat works after reconnect")


func test_state_machine_refuses_illegal_jump() -> void:
	var state := AppState.new()
	assert_false(state.transition(AppState.State.IN_WORLD))
	assert_eq(state.current(), AppState.State.DISCONNECTED)


# --- M5: combat ---------------------------------------------------------------

func test_attack_damages_and_kills_creature() -> void:
	_setup()
	_run_to_world()
	var creature_id := _move_adjacent_to_first_creature()
	assert_eq(world.player_distance_to_chebyshev(creature_id), 1, "adjacent to creature")

	for i in range(20):
		network.attack(ProtocolMessages.ABILITY_BASIC_ATTACK, creature_id, ProtocolMessages.TARGET_TYPE_CREATURE)
		_pump(0.2)
		if bool(world.get_entity(creature_id).get("dead", false)):
			break

	assert_true(bool(world.get_entity(creature_id).get("dead", false)), "creature defeated")
	assert_true(combat_results.size() > 0, "combat.result received")
	var last: Dictionary = combat_results[combat_results.size() - 1]
	assert_eq(String(last.get("targetId", "")), creature_id)
	assert_true(bool(last.get("targetDefeated", false)))
	assert_eq(world.creature_count(), 2, "defeated creature no longer targetable")


func test_attack_out_of_range_is_rejected() -> void:
	_setup()
	_run_to_world()
	var creature_id := String(world.creatures()[1]["id"])
	var hp_before := int(world.get_entity(creature_id).get("hp", 0))
	network.attack(ProtocolMessages.ABILITY_BASIC_ATTACK, creature_id, ProtocolMessages.TARGET_TYPE_CREATURE)
	_pump(0.2)
	assert_true(_has_error(ProtocolMessages.ERR_COMBAT_REJECTED, ProtocolMessages.CODE_OUT_OF_RANGE))
	assert_eq(int(world.get_entity(creature_id).get("hp", 0)), hp_before, "no damage from a rejected attack")


func test_attack_unknown_ability_is_rejected() -> void:
	_setup()
	_run_to_world()
	var creature_id := _move_adjacent_to_first_creature()
	network.attack("warrior.does_not_exist", creature_id, ProtocolMessages.TARGET_TYPE_CREATURE)
	_pump(0.2)
	assert_true(_has_error(ProtocolMessages.ERR_COMBAT_REJECTED, ProtocolMessages.CODE_ABILITY_NOT_FOUND))


func test_power_strike_cooldown_is_rejected() -> void:
	_setup()
	_run_to_world()
	var creature_id := _move_adjacent_to_first_creature()
	network.attack(ProtocolMessages.ABILITY_POWER_STRIKE, creature_id, ProtocolMessages.TARGET_TYPE_CREATURE)
	_pump(0.2)
	network.attack(ProtocolMessages.ABILITY_POWER_STRIKE, creature_id, ProtocolMessages.TARGET_TYPE_CREATURE)
	_pump(0.2)
	assert_true(_has_error(ProtocolMessages.ERR_COMBAT_REJECTED, ProtocolMessages.CODE_COOLDOWN_ACTIVE))


# --- M6: creatures + AI -------------------------------------------------------

func test_creature_ai_chases_and_attacks_player() -> void:
	_setup(true)
	_run_to_world()
	_pump(8.0)

	assert_true(events.has(ProtocolMessages.EVT_WORLD_CREATURE_MOVED), "creature moved")
	assert_true(_player_attacked_combat_results().size() > 0, "creature attacked the player")
	assert_true(int(world.player.get("hp", 100)) < 100, "player took damage")


func test_creature_respawns_after_death() -> void:
	_setup(true)
	_run_to_world()
	var creature_id := _move_adjacent_to_first_creature()
	for i in range(20):
		network.attack(ProtocolMessages.ABILITY_BASIC_ATTACK, creature_id, ProtocolMessages.TARGET_TYPE_CREATURE)
		_pump(0.2)
		if bool(world.get_entity(creature_id).get("dead", false)):
			break
	assert_true(bool(world.get_entity(creature_id).get("dead", false)), "creature defeated")
	assert_eq(world.creature_count(), 2)

	# Respawn delay for the first spawn (Slime) is 30s.
	_pump(32.0)
	assert_false(bool(world.get_entity(creature_id).get("dead", false)), "creature respawned")
	assert_eq(world.creature_count(), 3, "creature back in the world")
	assert_true(events.has(ProtocolMessages.EVT_WORLD_CREATURE_MOVED), "creature update received")


func test_wrong_character_movement_is_ignored() -> void:
	var local := WorldState.new()
	local.apply_snapshot({
		"mapId": 1, "width": 32, "height": 32,
		"player": {"characterId": "me", "x": 1, "y": 1, "state": "InWorld"},
	})
	var result := local.apply_movement({"characterId": "someone-else", "mapId": 1, "x": 9, "y": 9})
	assert_false(bool(result["applied"]))
	assert_eq(local.player_position(), Vector2i(1, 1))
