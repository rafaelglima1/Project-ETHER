extends TestCase

## M7 client consumption of additive progression + loot on combat.result.
##
## The MockBackend mirrors the backend's additive M7 shape: combat.result carries
## experienceGained/level/experience/levelsGained (0 when no reward) and an
## optional loot[] array. The client applies progression only when level > 0 and
## loot only when the array is present.

const TOKEN := "mock-game-token"

var network: NetworkClient
var backend: MockBackend
var world: WorldState
var combat_results: Array = []
var client_state := ClientState.new()


func _setup() -> void:
	network = NetworkClient.new()
	world = WorldState.new()
	combat_results = []
	client_state = ClientState.new()

	var config := ClientConfig.new()
	config.mock_latency_seconds = 0.0
	network.configure(config)

	backend = network.transport().backend()
	backend.set_ai_enabled(false)
	backend.set_critical_enabled(false)
	backend.issue_game_token(String(backend.characters[0]["characterId"]))

	network.event_received.connect(_on_event)
	network.snapshot_received.connect(func(payload): world.apply_snapshot(payload))

	network.connect_to_server()
	_pump(0.2)


func _on_event(name: String, payload: Dictionary) -> void:
	if name == ProtocolMessages.EVT_COMBAT_RESULT:
		combat_results.append(payload)
		world.apply_combat_result(payload)
		# Mirror GameClient's additive application.
		if int(payload.get(ProtocolMessages.FIELD_LEVEL, 0)) > 0 and world.player_id != "":
			world.apply_delta({"player": {
				"id": world.player_id,
				"level": int(payload.get(ProtocolMessages.FIELD_LEVEL, 1)),
				"experience": int(payload.get(ProtocolMessages.FIELD_EXPERIENCE, 0)),
			}})
		if payload.has(ProtocolMessages.FIELD_LOOT) and typeof(payload[ProtocolMessages.FIELD_LOOT]) == TYPE_ARRAY:
			client_state.add_loot(payload[ProtocolMessages.FIELD_LOOT])


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


func _kill_first_creature() -> String:
	var creature_id := String(world.creatures()[0]["id"])
	var position := world.entity_position(creature_id)
	network.move_to(maxi(0, position.x - 1), position.y)
	_pump(0.3)
	for i in range(20):
		network.attack(ProtocolMessages.ABILITY_BASIC_ATTACK, creature_id, ProtocolMessages.TARGET_TYPE_CREATURE)
		_pump(0.2)
		if bool(world.get_entity(creature_id).get("dead", false)):
			break
	return creature_id


func _last_result_for(creature_id: String) -> Dictionary:
	for i in range(combat_results.size() - 1, -1, -1):
		var payload: Dictionary = combat_results[i]
		if String(payload.get("targetId", "")) == creature_id:
			return payload
	return {}


func _cleanup() -> void:
	if network != null and is_instance_valid(network):
		network.free()
	network = null


func test_non_kill_hit_has_no_reward() -> void:
	_setup()
	_run_to_world()
	var creature_id := String(world.creatures()[0]["id"])
	var position := world.entity_position(creature_id)
	network.move_to(maxi(0, position.x - 1), position.y)
	_pump(0.3)

	network.attack(ProtocolMessages.ABILITY_BASIC_ATTACK, creature_id, ProtocolMessages.TARGET_TYPE_CREATURE)
	_pump(0.2)

	var result := _last_result_for(creature_id)
	assert_false(bool(result.get("targetDefeated", false)), "not a kill")
	assert_eq(int(result.get(ProtocolMessages.FIELD_LEVEL, 0)), 0, "no progression until a kill")
	assert_false(result.has(ProtocolMessages.FIELD_LOOT), "no loot until a kill")


func test_kill_grants_experience_and_level() -> void:
	_setup()
	_run_to_world()
	var creature_id := _kill_first_creature()

	var result := _last_result_for(creature_id)
	assert_true(bool(result.get("targetDefeated", false)), "kill")
	assert_true(int(result.get(ProtocolMessages.FIELD_EXPERIENCE_GAINED, 0)) > 0, "xp gained")
	assert_true(int(result.get(ProtocolMessages.FIELD_LEVEL, 0)) > 0, "level populated")
	assert_eq(int(world.player.get("experience", 0)), int(result.get(ProtocolMessages.FIELD_EXPERIENCE, 0)))


func test_kill_grants_loot_and_mirror_updates() -> void:
	_setup()
	backend.set_loot_guaranteed(true)
	_run_to_world()
	var creature_id := _kill_first_creature()

	var result := _last_result_for(creature_id)
	assert_true(result.has(ProtocolMessages.FIELD_LOOT), "loot present")
	var loot: Array = result[ProtocolMessages.FIELD_LOOT]
	assert_true(loot.size() >= 1)
	assert_eq(client_state.inventory.size(), 1, "inventory mirror updated")
	assert_true(int(client_state.inventory[0].get("quantity", 0)) >= 1)


func test_add_loot_stacks_by_definition() -> void:
	var state := ClientState.new()
	state.add_loot([{"itemDefinitionId": "item.slime_gel", "name": "Slime Gel", "quantity": 1, "itemInstanceId": "a"}])
	state.add_loot([{"itemDefinitionId": "item.slime_gel", "name": "Slime Gel", "quantity": 2, "itemInstanceId": "b"}])
	assert_eq(state.inventory.size(), 1, "stacked by definition id")
	assert_eq(int(state.inventory[0].get("quantity", 0)), 3)


func test_add_loot_ignores_malformed_entries() -> void:
	var state := ClientState.new()
	state.add_loot([42, "nope", {"name": "Odd Item", "quantity": 1}])
	assert_eq(state.inventory.size(), 1, "only the dictionary entry is kept")
	assert_eq(String(state.inventory[0].get("name", "")), "Odd Item")
