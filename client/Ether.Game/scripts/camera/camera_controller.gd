class_name CameraController
extends Camera2D

## Player-following camera bounded by the map (Blueprint v5.0 §52).
## Smooth follow, map clamping, no cinematic behaviour.

var target: Node2D = null
var bounds_size := Vector2.ZERO
var smoothing := 8.0


func setup(p_target: Node2D, p_bounds_size: Vector2) -> void:
	target = p_target
	bounds_size = p_bounds_size
	if target != null:
		global_position = target.position
	make_current()


func _process(delta: float) -> void:
	if target == null:
		return
	var weight := clampf(smoothing * delta, 0.0, 1.0)
	global_position = global_position.lerp(target.position, weight)
	_clamp_to_bounds()


func _clamp_to_bounds() -> void:
	if bounds_size == Vector2.ZERO:
		return
	var view_size := get_viewport_rect().size / zoom
	var half := view_size * 0.5

	if bounds_size.x > view_size.x:
		global_position.x = clampf(global_position.x, half.x, bounds_size.x - half.x)
	else:
		global_position.x = bounds_size.x * 0.5

	if bounds_size.y > view_size.y:
		global_position.y = clampf(global_position.y, half.y, bounds_size.y - half.y)
	else:
		global_position.y = bounds_size.y * 0.5
