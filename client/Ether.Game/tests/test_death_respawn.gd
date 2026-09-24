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


func _setup() -> void:
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


func test_mock_respawn_command_recovers_player() -> void:
	_setup()
	_run_to_world()
	_kill_player_in_mirror()
	backend.force_player_dead()
	assert_true(world.is_player_dead())

	# Canonical flow: client requests respawn, server replies with world.snapshot.
	network.respawn_character()
	_pump(0.5)

	assert_true(snapshots.size() >= 2, "respawn snapshot received")
	assert_false(world.is_player_dead(), "respawn snapshot clears death")
	assert_eq(int(world.player.get("hp", 0)), 100, "authoritative HP restored")
	assert_eq(world.player_position(), Vector2i(0, 0), "authoritative spawn position")
	assert_true(world.can_player_act(), "controls restored")


func test_respawn_rejected_when_not_dead() -> void:
	_setup()
	_run_to_world()
	network.respawn_character()
	_pump(0.3)
	assert_true(
		_has_error(ProtocolMessages.ERR_CHARACTER_RESPAWN_REJECTED, ProtocolMessages.CODE_CHARACTER_NOT_DEAD),
		"server refuses to respawn a living character")
	assert_true(world.can_player_act(), "player unaffected")


func test_world_enter_rejected_for_dead_character() -> void:
	_setup()
	network.authenticate(TOKEN)
	_pump(0.2)
	network.mark_authenticated()
	backend.force_player_dead()
	network.enter_world()
	_pump(0.3)
	assert_true(
		_has_error(ProtocolMessages.ERR_WORLD_ENTER_REJECTED, ProtocolMessages.CODE_CHARACTER_DEAD),
		"world.enter rejected while dead")
	assert_eq(network.state.current(), AppState.State.AUTHENTICATED, "did not enter world")

	# The client then requests respawn -> snapshot -> alive and in world.
	network.respawn_character()
	_pump(0.3)
	network.mark_in_world()
	assert_true(snapshots.size() >= 1, "snapshot after respawn")
	assert_false(world.is_player_dead())
	assert_eq(network.state.current(), AppState.State.IN_WORLD)


# --- Reconnect scenarios ------------------------------------------------------

func test_reconnect_while_dead_requires_respawn_then_recovers() -> void:
	_setup()
	_run_to_world()
	_kill_player_in_mirror()
	backend.force_player_dead()
	assert_true(world.is_player_dead())

	network.disconnect_from_server()
	_pump(0.2)
	network.connect_to_server()
	_pump(0.2)

	# Re-authenticate and try to enter — the server rejects a dead character.
	network.authenticate(TOKEN)
	_pump(0.2)
	network.mark_authenticated()
	network.enter_world()
	_pump(0.3)
	assert_true(
		_has_error(ProtocolMessages.ERR_WORLD_ENTER_REJECTED, ProtocolMessages.CODE_CHARACTER_DEAD),
		"reconnect world.enter rejected while dead")

	# Client requests the canonical respawn; the fresh snapshot clears stale state.
	network.respawn_character()
	_pump(0.3)
	network.mark_in_world()

	assert_true(snapshots.size() >= 2, "fresh snapshot after reconnect+respawn")
	assert_false(world.is_player_dead(), "stale death replaced by snapshot")
	assert_eq(int(world.player.get("hp", 0)), 100, "authoritative HP restored")
	assert_true(world.can_player_act(), "input restored")
	assert_eq(world.creature_count(), 3, "world restored")
	assert_eq(network.state.current(), AppState.State.IN_WORLD, "back in world")


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


# --- Command-level input gating (real GameClient autoload) ---------------------

## Drives the real GameClient autoload and proves that no movement / attack /
## respawn command is emitted while dead (the sequence counter only advances
## when CommandSender actually builds a command).
func test_game_client_emits_no_commands_while_dead() -> void:
	var game: Node = TestCase.tree.root.get_node_or_null("/root/GameClient")
	if game == null or not game.has_method("request_move"):
		return

	var saved_world: WorldState = game.world_state
	var saved_state: int = game.network.state.current()
	game.world_state = WorldState.new()
	game.network.state.set_current(AppState.State.IN_WORLD)
	game.world_state.apply_snapshot({
		"mapId": 1, "width": 32, "height": 32,
		"player": {"characterId": "gate", "x": 1, "y": 1, "state": "InWorld", "health": 100, "maxHealth": 100},
		"creatures": [{"creatureId": "c1", "name": "Slime", "x": 2, "y": 1, "health": 10, "maxHealth": 10}],
	})
	var sender: CommandSender = game.network.sender
	var sequence: int

	# Alive: movement / attack / respawn-forbidden gate behaviour.
	sequence = sender.current_sequence()
	game.request_move(2, 1)
	assert_true(sender.current_sequence() > sequence, "alive: movement command emitted")

	sequence = sender.current_sequence()
	game.request_attack("c1")
	assert_true(sender.current_sequence() > sequence, "alive: attack command emitted")

	sequence = sender.current_sequence()
	game.request_respawn()
	assert_eq(sender.current_sequence(), sequence, "alive: respawn refused (not dead)")

	# Dead: no movement, no attack, respawn allowed (one command).
	game.world_state.apply_combat_result({
		"attackerId": "c1", "targetId": "gate",
		"targetHealth": 0, "targetMaxHealth": 100,
		"targetState": "Dead", "targetDefeated": true,
	})

	sequence = sender.current_sequence()
	game.request_move(3, 1)
	assert_eq(sender.current_sequence(), sequence, "dead: no movement command")

	sequence = sender.current_sequence()
	game.request_attack("c1")
	assert_eq(sender.current_sequence(), sequence, "dead: no attack command")

	sequence = sender.current_sequence()
	game.request_respawn()
	assert_true(sender.current_sequence() > sequence, "dead: respawn command emitted")

	# Restore global autoload state for the rest of the suite.
	game.world_state = saved_world
	game.network.state.set_current(saved_state)


func test_respawn_in_flight_blocks_duplicate_commands() -> void:
	_setup()
	_run_to_world()
	backend.force_player_dead()
	var sender: CommandSender = network.sender
	var before := sender.current_sequence()

	var first := network.respawn_character()
	var second := network.respawn_character()

	assert_ne(first, "", "first respawn sent")
	assert_eq(second, "", "second respawn blocked while pending")
	assert_eq(sender.current_sequence(), before + 1, "exactly one command emitted")
	assert_true(network.resawn_pending(), "pending until server replies")

	_pump(0.4)
	assert_false(network.resawn_pending(), "snapshot clears pending")


func test_hud_death_overlay_and_button_gating() -> void:
	if TestCase.tree == null:
		return
	var hud: Node = load("res://scripts/ui/hud.gd").new()
	hud._ready()

	assert_false(hud._death_overlay.visible, "hidden while alive")
	assert_false(hud._respawn_button.visible, "respawn hidden while alive")
	assert_eq(hud._respawn_button.pressed.get_connections().size(), 1, "respawn button wired")

	hud._apply_death_state(true)
	assert_true(hud._death_overlay.visible, "overlay shown while dead")
	assert_true(hud._death_panel.visible, "panel shown while dead")
	assert_true(hud._respawn_button.visible, "respawn button shown while dead")
	assert_false(hud._respawn_button.disabled, "respawn button clickable")
	assert_true(hud._attack_button.disabled, "attack disabled while dead")
	assert_true(hud._power_button.disabled, "power strike disabled while dead")

	hud._apply_death_state(false)
	assert_false(hud._death_overlay.visible, "overlay cleared")
	assert_false(hud._attack_button.disabled, "attack re-enabled")
	assert_false(hud._power_button.disabled, "power strike re-enabled")

	hud.free()
