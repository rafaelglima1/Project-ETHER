extends TestCase

## End-to-end mock flow through the real networking pipeline:
## connect -> game.authenticate -> world.enter -> world.snapshot -> movement ->
## movement.accepted/rejected -> disconnect -> reconnect. No engine tree needed:
## the NetworkClient loop is pumped manually.

const TOKEN := "mock-game-token"

var network: NetworkClient
var backend: MockBackend
var world: WorldState
var events: Array = []
var errors: Array = []


func _setup(heartbeat_interval: float = 20.0) -> void:
	network = NetworkClient.new()
	world = WorldState.new()
	events = []
	errors = []

	var config := ClientConfig.new()
	config.mock_latency_seconds = 0.0
	config.heartbeat_interval_seconds = heartbeat_interval
	config.heartbeat_timeout_seconds = maxf(heartbeat_interval * 2.0, 1.0)
	network.configure(config)

	backend = network.transport().backend()
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


func _cleanup() -> void:
	if network != null and is_instance_valid(network):
		network.free()
	network = null


func test_connect_and_authenticate() -> void:
	_setup()
	assert_eq(network.state.current(), AppState.State.CONNECTED)

	network.authenticate(TOKEN)
	_pump(0.2)
	assert_true(events.has(ProtocolMessages.EVT_GAME_AUTHENTICATED), "authenticated event")
	network.mark_authenticated()
	assert_eq(network.state.current(), AppState.State.AUTHENTICATED)


func test_enter_world_applies_snapshot() -> void:
	_setup()
	_run_to_world()

	assert_eq(network.state.current(), AppState.State.IN_WORLD)
	assert_eq(world.player_id, String(backend.characters[0]["characterId"]))
	assert_eq(world.map_id, MockBackend.MAP_ID)
	assert_eq(int(world.map["width"]), MockBackend.MAP_WIDTH)
	assert_eq(world.player_position(), Vector2i(MockBackend.START_X, MockBackend.START_Y))


func test_movement_accepted_updates_position() -> void:
	_setup()
	_run_to_world()

	network.move_to(5, 5)
	_pump(0.2)
	assert_eq(world.player_position(), Vector2i(5, 5))
	assert_true(events.has(ProtocolMessages.EVT_MOVEMENT_ACCEPTED), "movement.accepted")


func test_movement_out_of_bounds_is_rejected() -> void:
	_setup()
	_run_to_world()

	network.move_to(999, 999)
	_pump(0.2)
	assert_eq(world.player_position(), Vector2i(0, 0), "position unchanged")
	assert_true(_has_error(ProtocolMessages.ERR_MOVEMENT_REJECTED, ProtocolMessages.CODE_OUT_OF_BOUNDS))


func test_movement_too_far_is_rejected() -> void:
	_setup()
	_run_to_world()

	network.move_to(MockBackend.MAX_MOVE_DISTANCE + 5, 0)
	_pump(0.2)
	assert_eq(world.player_position(), Vector2i(0, 0))
	assert_true(_has_error(ProtocolMessages.ERR_MOVEMENT_REJECTED, ProtocolMessages.CODE_TOO_FAR))


func test_wrong_character_movement_is_ignored() -> void:
	var local := WorldState.new()
	local.apply_snapshot({
		"mapId": 1, "width": 32, "height": 32,
		"player": {"characterId": "me", "x": 1, "y": 1, "state": "InWorld"},
	})
	var result := local.apply_movement({"characterId": "someone-else", "mapId": 1, "x": 9, "y": 9})
	assert_false(bool(result["applied"]))
	assert_eq(local.player_position(), Vector2i(1, 1))


func test_heartbeat_ping_pong_sets_rtt() -> void:
	_setup(0.2)
	_run_to_world()
	_pump(0.6)
	assert_true(network.last_rtt_ms >= 0, "rtt measured from system.pong")


func test_disconnect_and_reconnect_restores_world() -> void:
	_setup()
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
	assert_eq(world.player_id, String(backend.characters[0]["characterId"]))


func test_state_machine_refuses_illegal_jump() -> void:
	var state := AppState.new()
	assert_false(state.transition(AppState.State.IN_WORLD), "cannot skip authentication")
	assert_eq(state.current(), AppState.State.DISCONNECTED)


func _has_error(name: String, code: String) -> bool:
	for entry in errors:
		if entry["name"] == name and entry["code"] == code:
			return true
	return false
