extends TestCase

## Death + respawn (server-authoritative) coverage.
##
## The server owns death, HP and respawn. These tests prove the client mirrors
## those facts, gates input while dead, and clears all stale state when the
## server reports the player alive again (via a fresh world.snapshot).

const TOKEN := "mock-game-token"

var network: NetworkClient
var backend: MockBackend
var world: WorldState
var events: Array = []
var snapshots: Array = []
var errors: Array = []


func _setup(respawn_seconds: float = 3.0) -> void:
	network = NetworkClient.new()
	world = WorldState.new()
	events = []
	snapshots = []
	errors = []

	var config := ClientConfig.new()
	config.mock_latency_seconds = 0.0
	network.configure(config)

	backend = network.transport().backend()
	backend.set_ai_enabled(false)
	backend.set_critical_enabled(false)
	backend.set_respawn_delay(respawn_seconds)
	backend.issue_game_token(String(backend.characters[0]["characterId"]))

	network.event_received.connect(_on_event)
	network.snapshot_received.connect(_on_snapshot)
	network.error_received.connect(_on_error)

	network.connect_to_server()
	_pump(0.2)


func _on_event(name: String, _payload: Dictionary) -> void:
	events.append(name)


func _on_snapshot(payload: Dictionary) -> void:
	snapshots.append(payload)
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


## Applies an authoritative death to the mirrored player (as combat.result does).
func _kill_player_in_mirror() -> void:
	world.apply_combat_result({
		"attackerId": "creature",
		"targetId": world.player_id,
		"targetHealth": 0,
		"targetMaxHealth": 100,
		"targetState": "Dead",
		"targetDefeated": true,
	})


func _cleanup() -> void:
	if network != null and is_instance_valid(network):
		network.free()
	network = null


# --- Death ---------------------------------------------------------------------

func test_death_from_combat_result_marks_player_dead() -> void:
	var local := WorldState.new()
	local.apply_snapshot({
		"mapId": 1, "width": 32, "height": 32,
		"player": {"characterId": "me", "x": 4, "y": 4, "state": "InWorld", "health": 10, "maxHealth": 100},
	})
	assert_false(local.is_player_dead())

	local.apply_combat_result({
		"attackerId": "creature", "targetId": "me",
		"targetHealth": 0, "targetMaxHealth": 100,
		"targetState": "Dead", "targetDefeated": true,
	})
	assert_true(local.is_player_dead())
	assert_false(local.can_player_act(), "no actions while dead")
	assert_eq(int(local.player.get("hp", -1)), 0, "authoritative HP 0")
	assert_eq(String(local.player.get("state", "")), "Dead")


func test_death_blocks_actions_until_alive_again() -> void:
	var local := WorldState.new()
	local.apply_snapshot({
		"mapId": 1, "width": 32, "height": 32,
		"player": {"characterId": "me", "x": 4, "y": 4, "state": "InWorld"},
	})
	assert_true(local.can_player_act())
	local.apply_combat_result({
		"attackerId": "creature", "targetId": "me",
		"targetHealth": 0, "targetMaxHealth": 100,
		"targetState": "Dead", "targetDefeated": true,
	})
	assert_false(local.can_player_act())


# --- Respawn (authoritative snapshot / state) ---------------------------------

func test_respawn_snapshot_restores_authoritative_player() -> void:
	var local := WorldState.new()
	local.apply_snapshot({
		"mapId": 1, "width": 32, "height": 32,
		"player": {"characterId": "me", "x": 9, "y": 9, "state": "InWorld", "health": 100, "maxHealth": 100},
	})
	local.apply_combat_result({
		"attackerId": "creature", "targetId": "me",
		"targetHealth": 0, "targetMaxHealth": 100,
		"targetState": "Dead", "targetDefeated": true,
	})
	assert_true(local.is_player_dead())

	# Fresh snapshot from the server after respawn (position, HP, state all server-side).
	local.apply_snapshot({
		"mapId": 1, "width": 32, "height": 32,
		"player": {"characterId": "me", "x": 0, "y": 0, "state": "InWorld", "health": 100, "maxHealth": 100},
	})
	assert_false(local.is_player_dead(), "stale dead flag cleared")
	assert_false(local.player.get("dead", false), "dead key replaced by fresh snapshot")
	assert_eq(int(local.player.get("hp", 0)), 100, "authoritative HP restored")
	assert_eq(int(local.player.get("maxHp", 0)), 100)
	assert_eq(local.player_position(), Vector2i(0, 0), "authoritative spawn position")
	assert_true(local.can_player_act(), "input restored")


func test_respawn_delta_clears_stale_dead_flag() -> void:
	var local := WorldState.new()
	local.apply_snapshot({
		"mapId": 1, "width": 32, "height": 32,
		"player": {"characterId": "me", "x": 9, "y": 9, "state": "InWorld", "health": 100, "maxHealth": 100},
	})
	local.apply_combat_result({
		"attackerId": "creature", "targetId": "me",
		"targetHealth": 0, "targetMaxHealth": 100,
		"targetState": "Dead", "targetDefeated": true,
	})
	assert_true(local.is_player_dead())

	# A respawn carried in a player delta (state changes, no explicit dead key).
	local.apply_delta({"player": {"id": "me", "state": "InWorld", "health": 100}})
	assert_false(local.is_player_dead(), "delta state change clears stale dead flag")
	assert_true(local.can_player_act())
	assert_eq(int(local.player.get("hp", 0)), 100)
	assert_eq(local.player_position(), Vector2i(9, 9), "position preserved until server moves it")


# --- Server-role input gating (mock as server double) -------------------------

func test_mock_server_rejects_move_while_dead() -> void:
	_setup()
	_run_to_world()
	backend.force_player_dead()
	network.move_to(5, 5)
	_pump(0.2)
	assert_true(
		_has_error(ProtocolMessages.ERR_MOVEMENT_REJECTED, ProtocolMessages.CODE_NOT_IN_WORLD),
		"movement rejected while dead (server authority)")
	assert_eq(world.player_position(), Vector2i(0, 0), "position unchanged")


func test_mock_server_rejects_attack_while_dead() -> void:
	_setup()
	_run_to_world()
	var creature_id := String(world.creatures()[0]["id"])
	backend.force_player_dead()
	network.attack(ProtocolMessages.ABILITY_BASIC_ATTACK, creature_id, ProtocolMessages.TARGET_TYPE_CREATURE)
	_pump(0.2)
	assert_true(
		_has_error(ProtocolMessages.ERR_COMBAT_REJECTED, ProtocolMessages.CODE_ATTACKER_DEAD),
		"attack rejected while dead (server authority)")


func test_mock_respawn_emits_snapshot_and_client_recovers() -> void:
	_setup(0.05)
	_run_to_world()
	_kill_player_in_mirror()
	assert_true(world.is_player_dead())

	# Server-side respawn fires on the next tick; reported as a world.snapshot.
	backend.schedule_respawn_now()
	_pump(0.5)

	assert_true(snapshots.size() >= 2, "respawn snapshot received")
	assert_false(world.is_player_dead(), "respawn snapshot clears death")
	assert_eq(int(world.player.get("hp", 0)), 100, "authoritative HP restored")
	assert_eq(world.player_position(), Vector2i(0, 0), "authoritative spawn position")
	assert_true(world.can_player_act(), "controls restored")


# --- Reconnect scenarios ------------------------------------------------------

func test_reconnect_after_death_restores_alive_player() -> void:
	_setup()
	_run_to_world()
	_kill_player_in_mirror()
	assert_true(world.is_player_dead())

	network.disconnect_from_server()
	_pump(0.2)
	network.connect_to_server()
	_pump(0.2)
	_run_to_world()

	assert_true(snapshots.size() >= 2, "fresh snapshot after reconnect")
	assert_false(world.is_player_dead(), "stale death replaced by snapshot")
	assert_eq(int(world.player.get("hp", 0)), 100)
	assert_true(world.can_player_act())
	assert_eq(world.creature_count(), 3, "world restored")


func test_duplicate_world_enter_returns_rejection_and_world_survives() -> void:
	_setup()
	_run_to_world()
	var before := snapshots.size()
	network.enter_world()
	_pump(0.2)
	assert_true(
		_has_error(ProtocolMessages.ERR_WORLD_ENTER_REJECTED, ProtocolMessages.CODE_ALREADY_IN_WORLD),
		"duplicate world.enter rejected by server")
	assert_eq(world.player_id, String(backend.characters[0]["characterId"]), "world intact")
	assert_true(snapshots.size() == before, "no duplicate snapshot applied")
	assert_eq(network.state.current(), AppState.State.IN_WORLD, "state unchanged")


func _has_error(name: String, code: String) -> bool:
	for entry in errors:
		if entry["name"] == name and entry["code"] == code:
			return true
	return false
