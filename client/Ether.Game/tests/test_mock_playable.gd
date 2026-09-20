extends TestCase

## End-to-end mock playable flow through the real networking pipeline:
## connect -> authenticate -> select -> world -> move -> attack -> XP -> loot
## -> inventory -> disconnect -> reconnect. No engine tree required: the
## NetworkClient loop is pumped manually.

var network: NetworkClient
var backend: MockBackend
var world: WorldState
var events: Array = []
var last_inventory: Array = []


func _setup_mock() -> void:
	network = NetworkClient.new()
	world = WorldState.new()
	events = []
	last_inventory = []

	var config := ClientConfig.new()
	config.mock_latency_seconds = 0.0
	network.configure(config)

	backend = network.transport().backend()
	backend.set_wandering(false)

	network.event_received.connect(_on_event)
	network.snapshot_received.connect(_on_snapshot)
	network.delta_received.connect(_on_delta)

	network.connect_to_server()
	_pump(0.2)


func _on_event(name: String, payload: Dictionary) -> void:
	events.append(name)
	if name == ProtocolMessages.EVT_CHARACTER_MOVED or name == ProtocolMessages.EVT_CREATURE_MOVED:
		_apply_move(payload)
	elif name == ProtocolMessages.EVT_ENTITY_DEATH or name == ProtocolMessages.EVT_CREATURE_DIED:
		world.remove_entity(String(payload.get("entityId", "")))
	elif name == ProtocolMessages.EVT_EXPERIENCE_GAINED:
		world.apply_delta({"player": {
			"id": world.player_id,
			"experience": int(payload.get("total", 0)),
			"level": int(payload.get("level", 1)),
		}})
	elif name == ProtocolMessages.EVT_COMBAT_RESULT or name == ProtocolMessages.EVT_DAMAGE_APPLIED:
		var target_id := String(payload.get("targetId", ""))
		var hp: Variant = payload.get("targetHp", payload.get("remainingHp", null))
		if target_id != "" and hp != null:
			world.apply_delta({"upserts": [{"id": target_id, "hp": int(hp)}]})
	elif name == "InventoryUpdated":
		last_inventory = payload.get("inventory", [])


func _cleanup() -> void:
	if network != null and is_instance_valid(network):
		network.free()
	network = null


func _apply_move(payload: Dictionary) -> void:
	var entity_id := String(payload.get("entityId", ""))
	if entity_id == "":
		return
	world.apply_delta({"upserts": [{
		"id": entity_id,
		"x": int(payload.get("x", 0)),
		"y": int(payload.get("y", 0)),
	}]})


func _on_snapshot(payload: Dictionary) -> void:
	world.apply_snapshot(payload)


func _on_delta(payload: Dictionary) -> void:
	world.apply_delta(payload)


func _pump(seconds: float) -> void:
	var steps := maxi(1, int(ceil(seconds / 0.05)))
	for i in range(steps):
		network._process(0.05)


func _run_to_world() -> void:
	network.authenticate("mock-token")
	network.mark_authenticated()
	_pump(0.2)
	network.select_character(String(backend.characters[0]["characterId"]))
	_pump(0.2)
	network.enter_world()
	network.mark_in_world()
	_pump(0.2)


func test_connect_and_authenticate() -> void:
	_setup_mock()
	assert_eq(network.state.current(), AppState.State.CONNECTED)
	assert_true(events.has(ProtocolMessages.EVT_CONNECTED), "connected event")

	network.authenticate("mock-token")
	network.mark_authenticated()
	_pump(0.2)
	assert_eq(network.state.current(), AppState.State.AUTHENTICATED)
	assert_true(events.has(ProtocolMessages.EVT_AUTHENTICATED), "authenticated event")


func test_enter_world_applies_snapshot() -> void:
	_setup_mock()
	_run_to_world()

	var character_id := String(backend.characters[0]["characterId"])
	assert_eq(network.state.current(), AppState.State.IN_WORLD)
	assert_eq(world.player_id, character_id)
	assert_true(world.creatures().size() >= 1, "creatures spawned")
	assert_eq(int(world.map["width"]), MockBackend.MAP_WIDTH)


func test_move_is_server_accepted() -> void:
	_setup_mock()
	_run_to_world()

	network.move_to(6, 5)
	_pump(0.2)
	assert_eq(world.player_position(), Vector2i(6, 5))
	assert_true(events.has(ProtocolMessages.EVT_CHARACTER_MOVED), "movement event")


func test_move_out_of_range_is_rejected() -> void:
	_setup_mock()
	_run_to_world()

	network.move_to(22, 14)
	_pump(0.2)
	assert_eq(world.player_position(), MockBackend.PLAYER_SPAWN, "player should not move")
	assert_false(events.has(ProtocolMessages.EVT_CHARACTER_MOVED), "movement should be rejected")


func test_attack_kills_creature_and_grants_xp() -> void:
	_setup_mock()
	_run_to_world()

	var creature_id := String(world.creatures()[0]["id"])
	var creature_position := world.entity_position(creature_id)
	var adjacent := Vector2i(creature_position.x - 1, creature_position.y)

	network.move_to(adjacent.x, adjacent.y)
	_pump(0.3)
	assert_eq(world.player_position(), adjacent, "player adjacent to creature")

	for i in range(20):
		network.attack(creature_id)
		_pump(0.2)
		if not world.has_entity(creature_id):
			break

	assert_false(world.has_entity(creature_id), "creature should die")
	assert_true(events.has(ProtocolMessages.EVT_EXPERIENCE_GAINED), "xp granted")
	assert_true(int(world.player.get("experience", 0)) > 0, "experience increased")


func test_pickup_updates_inventory() -> void:
	_setup_mock()
	_run_to_world()

	network.pickup_item("item.test", "Test Item")
	_pump(0.2)
	assert_true(events.has("InventoryUpdated"), "inventory event")
	assert_eq(last_inventory.size(), 1)
	assert_eq(String(last_inventory[0]["name"]), "Test Item")


func test_disconnect_and_reconnect_restores_world() -> void:
	_setup_mock()
	_run_to_world()

	network.disconnect_from_server()
	_pump(0.2)
	assert_eq(network.state.current(), AppState.State.DISCONNECTED)
	assert_false(network.is_connected_to_server())

	network.connect_to_server()
	_pump(0.2)
	assert_eq(network.state.current(), AppState.State.CONNECTED)

	_run_to_world()
	assert_eq(network.state.current(), AppState.State.IN_WORLD)
	assert_true(world.creatures().size() >= 1, "world restored after reconnect")
