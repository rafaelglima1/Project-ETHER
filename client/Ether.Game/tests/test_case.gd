class_name TestCase
extends RefCounted

## Tiny zero-dependency test base.
##
## The runner sets `_current` before invoking each `test_*` method; assertions
## append human-readable failures that the runner reports.

var failures: Array = []
var _current := ""

## SceneTree provided by the runner so suites can mount nodes/scenes.
static var tree: SceneTree = null


func assert_true(condition: bool, message: String = "") -> void:
	if not condition:
		_fail("expected true. %s" % message)


func assert_false(condition: bool, message: String = "") -> void:
	if condition:
		_fail("expected false. %s" % message)


func assert_eq(actual: Variant, expected: Variant, message: String = "") -> void:
	if actual != expected:
		_fail("expected %s but got %s. %s" % [str(expected), str(actual), message])


func assert_ne(actual: Variant, unexpected: Variant, message: String = "") -> void:
	if actual == unexpected:
		_fail("did not expect %s. %s" % [str(unexpected), message])


func assert_approx(actual: float, expected: float, epsilon: float = 0.0001, message: String = "") -> void:
	if absf(actual - expected) > epsilon:
		_fail("expected %f ~= %f. %s" % [expected, actual, message])


func assert_has_key(container: Dictionary, key: Variant, message: String = "") -> void:
	if not container.has(key):
		_fail("expected key '%s'. %s" % [str(key), message])


func _fail(message: String) -> void:
	failures.append("%s -> %s" % [_current, message])
