extends TestCase

## Mobile movement input: the virtual stick's direction mapping and the input
## controller turning a stick direction into tile steps (movement.move intent).


func test_joystick_deadzone_returns_zero() -> void:
	assert_eq(MoveStick.compute_direction(Vector2(5, 0), 100.0, 0.22), Vector2.ZERO)


func test_joystick_full_deflection() -> void:
	var direction := MoveStick.compute_direction(Vector2(100, 0), 100.0, 0.22)
	assert_approx(direction.x, 1.0, 0.001)
	assert_approx(direction.y, 0.0, 0.001)


func test_joystick_partial_magnitude() -> void:
	var direction := MoveStick.compute_direction(Vector2(50, 0), 100.0, 0.22)
	assert_approx(direction.x, 0.5, 0.01)


func test_joystick_clamps_beyond_radius() -> void:
	var direction := MoveStick.compute_direction(Vector2(400, 0), 100.0, 0.22)
	assert_approx(direction.length(), 1.0, 0.001)


func test_input_controller_emits_step_from_joystick() -> void:
	var controller := InputController.new()
	var steps: Array = []
	controller.move_step.connect(func(direction): steps.append(direction))

	controller.set_joystick(Vector2(1, 0))
	for i in range(5):
		controller._process(0.1)

	assert_true(steps.size() > 0, "stick produces movement steps")
	assert_eq(steps[0], Vector2i(1, 0))
	controller.free()


func test_input_controller_diagonal_joystick() -> void:
	var controller := InputController.new()
	var steps: Array = []
	controller.move_step.connect(func(direction): steps.append(direction))

	controller.set_joystick(Vector2(0.9, 0.9))
	for i in range(5):
		controller._process(0.1)

	assert_true(steps.size() > 0)
	assert_eq(steps[0], Vector2i(1, 1))
	controller.free()


func test_input_controller_no_step_when_released() -> void:
	var controller := InputController.new()
	var steps: Array = []
	controller.move_step.connect(func(direction): steps.append(direction))

	controller.set_joystick(Vector2.ZERO)
	for i in range(5):
		controller._process(0.1)

	assert_eq(steps.size(), 0, "released stick produces no movement")
	controller.free()


func test_input_controller_respects_step_interval() -> void:
	var controller := InputController.new()
	var steps: Array = []
	controller.move_step.connect(func(direction): steps.append(direction))

	controller.set_joystick(Vector2(0, -1))
	controller._process(0.05)
	controller._process(0.05)
	assert_eq(steps.size(), 0, "below interval: no step")
	controller._process(0.1)
	assert_eq(steps.size(), 1, "one step after interval")
	controller.free()
