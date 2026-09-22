extends TestCase

## Client robustness around the realtime link (NIGHT RUN hardening):
## malformed frames, unsupported versions, unknown events, missing payloads,
## commands sent before entering the world, and duplicate updates.
## These assert the client degrades safely and never invents state.

const TOKEN := "mock-game-token"

var network: NetworkClient
var backend: MockBackend
var world: WorldState
var events: Array = []
var errors: Array = []


func _setup() -> void:
	network = NetworkClient.new()
	world = WorldState.new()
	events = []
	errors = []

	var config := ClientConfig.new()
	config.mock_latency_seconds = 0.0
	network.configure(config)

	backend = network.transport().backend()
	backend.set_ai_enabled(false)
	backend.set_critical_enabled(false)
	backend.issue_game_token(String(backend.characters[0]["characterId"]))

	network.event_received.connect(func(name, _payload): events.append(name))
	network.error_received.connect(func(name, code, _message): errors.append({"name": name, "code": code}))
	network.snapshot_received.connect(func(payload): world.apply_snapshot(payload))

	network.connect_to_server()
	_pump(0.2)


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


func _has_error(name: String, code: String) -> bool:
	for entry in errors:
		if entry["name"] == name and entry["code"] == code:
			return true
	return false


func test_malformed_server_frame_is_ignored() -> void:
	_setup()
	var before := network.state.current()
	network._on_message("{ this is not json")
	assert_eq(network.state.current(), before, "state unchanged")
	assert_eq(errors.size(), 0, "no spurious error surfaced")


func test_unsupported_version_is_ignored() -> void:
	_setup()
	network._on_message('{"version":99,"type":"event","name":"x","sequence":1,"payload":{}}')
	assert_eq(errors.size(), 0)


func test_unknown_event_is_ignored() -> void:
	_setup()
	network._on_message('{"version":1,"type":"event","name":"something.unknown","sequence":1,"payload":{}}')
	assert_eq(errors.size(), 0)
	assert_eq(network.state.current(), AppState.State.CONNECTED)


func test_event_without_payload_is_tolerated() -> void:
	_setup()
	_run_to_world()
	network._on_message('{"version":1,"type":"event","name":"world.creature_moved","sequence":99}')
	assert_eq(errors.size(), 0, "payload-less creature update tolerated")


func test_combat_before_world_is_rejected() -> void:
	_setup()
	network.attack(ProtocolMessages.ABILITY_BASIC_ATTACK, "target", ProtocolMessages.TARGET_TYPE_CREATURE)
	_pump(0.2)
	assert_true(_has_error(ProtocolMessages.ERR_COMBAT_REJECTED, ProtocolMessages.CODE_NOT_IN_WORLD))


func test_duplicate_movement_update_is_idempotent() -> void:
	var local := WorldState.new()
	local.apply_snapshot({
		"mapId": 1, "width": 32, "height": 32,
		"player": {"characterId": "me", "x": 1, "y": 1, "state": "InWorld"},
	})
	var first := local.apply_movement({"characterId": "me", "mapId": 1, "x": 4, "y": 4})
	var second := local.apply_movement({"characterId": "me", "mapId": 1, "x": 4, "y": 4})
	assert_true(bool(first["applied"]))
	assert_true(bool(second["applied"]))
	assert_eq(local.player_position(), Vector2i(4, 4))


func test_stale_sequence_is_rejected_by_server() -> void:
	_setup()
	_run_to_world()
	var serializer := ProtocolSerializer.new()
	# CommandSender already used sequences 1..3; replay a stale sequence 1.
	network.transport().send_text(serializer.encode_command(ProtocolMessages.CMD_SYSTEM_PING, {}, 1))
	_pump(0.2)
	assert_true(_has_error(ProtocolMessages.ERR_PROTOCOL, ProtocolMessages.CODE_INVALID_SEQUENCE) or
		_has_error("system.ping", ProtocolMessages.CODE_INVALID_SEQUENCE), "stale sequence rejected")
