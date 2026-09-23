class_name MoveStick
extends Control

## On-screen movement stick for touch devices.
##
## Emits a normalized direction (magnitude 0..1). It is pure *intent*: the world
## scene turns the direction into tile steps and the server remains authoritative
## over the resulting position.

signal direction_changed(direction: Vector2)

const BASE_RADIUS := 108.0
const KNOB_RADIUS := 46.0
const DEADZONE := 0.22

var _active := false
var _direction := Vector2.ZERO
var _knob := Vector2.ZERO


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_STOP
	custom_minimum_size = Vector2(BASE_RADIUS * 2.0, BASE_RADIUS * 2.0)
	# Anchored bottom-left with a comfortable margin; scales with the viewport.
	anchor_left = 0.0
	anchor_top = 1.0
	anchor_right = 0.0
	anchor_bottom = 1.0
	offset_left = 28.0
	offset_top = -(BASE_RADIUS * 2.0) - 28.0
	offset_right = 28.0 + BASE_RADIUS * 2.0
	offset_bottom = -28.0
	set_process(false)


func direction() -> Vector2:
	return _direction


func _center() -> Vector2:
	return size * 0.5


## Pure mapping from a knob offset to a direction; exposed for tests.
static func compute_direction(offset: Vector2, radius: float, deadzone: float) -> Vector2:
	if radius <= 0.0:
		return Vector2.ZERO
	var length := offset.length()
	if length < deadzone * radius:
		return Vector2.ZERO
	var magnitude := minf(1.0, length / radius)
	return offset / length * magnitude


func _gui_input(event: InputEvent) -> void:
	if event is InputEventScreenTouch:
		if event.pressed:
			_begin(event.position)
		else:
			_end()
	elif event is InputEventScreenDrag:
		_update(event.position)
	elif event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_LEFT:
			if event.pressed:
				_begin(event.position)
			else:
				_end()
	elif event is InputEventMouseMotion:
		if _active:
			_update(event.position)


func _begin(local_position: Vector2) -> void:
	_active = true
	_update(local_position)


func _end() -> void:
	_active = false
	_knob = Vector2.ZERO
	if _direction != Vector2.ZERO:
		_direction = Vector2.ZERO
		direction_changed.emit(_direction)
	queue_redraw()


func _update(local_position: Vector2) -> void:
	var offset := local_position - _center()
	_knob = offset.limit_length(BASE_RADIUS)
	var next := compute_direction(_knob, BASE_RADIUS, DEADZONE)
	if not next.is_equal_approx(_direction):
		_direction = next
		direction_changed.emit(_direction)
	queue_redraw()


func _draw() -> void:
	var center := _center()
	draw_circle(center, BASE_RADIUS, Color(0.15, 0.18, 0.24, 0.55))
	draw_arc(center, BASE_RADIUS, 0.0, TAU, 48, Color(0.45, 0.62, 0.85, 0.8), 3.0)
	draw_circle(center + _knob, KNOB_RADIUS, Color(0.40, 0.72, 1.0, 0.9))
	draw_arc(center + _knob, KNOB_RADIUS, 0.0, TAU, 32, Color(0.85, 0.93, 1.0, 0.9), 2.0)
