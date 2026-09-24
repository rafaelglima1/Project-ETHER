extends SceneTree

## Headless test runner.
##
## Run with:
##   godot --headless --path client/Ether.Game --script res://tests/test_runner.gd
##
## Exits with code 0 when all tests pass, 1 otherwise.

const SUITES := [
	preload("res://tests/test_protocol_serializer.gd"),
	preload("res://tests/test_app_state.gd"),
	preload("res://tests/test_heartbeat_manager.gd"),
	preload("res://tests/test_reconnect_manager.gd"),
	preload("res://tests/test_mock_transport.gd"),
	preload("res://tests/test_world_state.gd"),
	preload("res://tests/test_client_robustness.gd"),
	preload("res://tests/test_progression.gd"),
	preload("res://tests/test_character_flow.gd"),
	preload("res://tests/test_movement_input.gd"),
	preload("res://tests/test_death_respawn.gd"),
	preload("res://tests/test_mock_playable.gd"),
]


func _initialize() -> void:
	TestCase.tree = self
	var total := 0
	var failed := 0

	for suite_script in SUITES:
		if not suite_script.can_instantiate():
			print("SKIP  %s (did not compile)" % suite_script.resource_path.get_file())
			failed += 1
			continue
		var suite: TestCase = suite_script.new()
		for method in _test_methods(suite):
			suite.failures.clear()
			suite._current = method
			suite.call(method)
			total += 1
			if suite.has_method("_cleanup"):
				suite.call("_cleanup")
			if suite.failures.is_empty():
				print("PASS  %s.%s" % [suite_script.resource_path.get_file(), method])
			else:
				failed += 1
				for failure in suite.failures:
					print("FAIL  %s" % failure)

	print("----------------------------------------")
	print("Tests: %d  Passed: %d  Failed: %d" % [total, total - failed, failed])
	quit(1 if failed > 0 else 0)


func _test_methods(suite: Object) -> Array:
	var methods: Array = []
	for entry in suite.get_method_list():
		var name := String(entry["name"])
		if name.begins_with("test_"):
			methods.append(name)
	methods.sort()
	return methods
