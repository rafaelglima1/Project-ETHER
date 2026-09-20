class_name InputController
extends Node

## Translates raw input into *intent*. Mobile-first (tap-to-move + tap target,
## Blueprint v5.0 §51); keyboard WASD/arrows exist only as a desktop testing
## convenience.

signal pointer_pressed(screen_position: Vector2)
signal move_step(direction: Vector2i)
signal cancel_pressed

var _move_accumulator := 0.0
var _move_interval := 0.18


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventScreenTouch:
		if event.pressed:
			pointer_pressed.emit(event.position)
	elif event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_LEFT:
			pointer_pressed.emit(event.position)
		elif event.button_index == MOUSE_BUTTON_RIGHT:
			cancel_pressed.emit()


func _process(delta: float) -> void:
	_move_accumulator += delta
	if _move_accumulator < _move_interval:
		return

	var direction := Vector2i.ZERO
	if Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP):
		direction.y -= 1
	if Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN):
		direction.y += 1
	if Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT):
		direction.x -= 1
	if Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT):
		direction.x += 1

	if direction != Vector2i.ZERO:
		_move_accumulator = 0.0
		move_step.emit(direction)
