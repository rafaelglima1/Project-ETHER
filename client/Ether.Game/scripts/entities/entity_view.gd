class_name EntityView
extends Node2D

## Placeholder 2D presentation for a world entity.
##
## Visual interpolation only (Blueprint v5.0 §7/§16): the authoritative tile
## position always arrives from WorldState; this node just eases towards it.

var entity_id := ""
var entity_kind := "creature"
var display_name := ""
var tile_size := 32
var hp := 0
var max_hp := 0
var dead := false
var selected := false

var target_tile := Vector2i.ZERO
var _visual_position := Vector2.ZERO
var interpolation_speed := 12.0
var _body_color := Color(0.75, 0.78, 0.9)
var _radius := 11.0


func setup(data: Dictionary, p_tile_size: int) -> void:
	entity_id = String(data.get("id", ""))
	entity_kind = String(data.get("kind", "creature"))
	display_name = String(data.get("name", ""))
	tile_size = maxi(8, p_tile_size)
	_apply_body_color()
	set_logical_position(int(data.get("x", 0)), int(data.get("y", 0)), true)
	apply_stats(data)
	queue_redraw()


func update_from(data: Dictionary) -> void:
	if data.has("kind"):
		entity_kind = String(data["kind"])
	if data.has("name"):
		display_name = String(data["name"])
	_apply_body_color()
	set_logical_position(int(data.get("x", target_tile.x)), int(data.get("y", target_tile.y)), false)
	apply_stats(data)


func apply_stats(data: Dictionary) -> void:
	if data.has("hp"):
		hp = int(data["hp"])
	if data.has("maxHp"):
		max_hp = int(data["maxHp"])
	if data.has("dead"):
		dead = bool(data["dead"])
	queue_redraw()


func set_logical_position(x: int, y: int, snap: bool) -> void:
	target_tile = Vector2i(x, y)
	if snap:
		_visual_position = tile_center(target_tile)
		position = _visual_position


func set_selected(value: bool) -> void:
	if selected == value:
		return
	selected = value
	queue_redraw()


func tile_center(tile: Vector2i) -> Vector2:
	return Vector2((float(tile.x) + 0.5) * tile_size, (float(tile.y) + 0.5) * tile_size)


func _process(delta: float) -> void:
	var target := tile_center(target_tile)
	_visual_position = _visual_position.lerp(target, clampf(interpolation_speed * delta, 0.0, 1.0))
	position = _visual_position


func _draw() -> void:
	var body := _body_color
	if dead:
		body = body.darkened(0.6)
	draw_circle(Vector2.ZERO, _radius, body)
	draw_arc(Vector2.ZERO, _radius, 0.0, TAU, 24, body.darkened(0.5), 2.0)

	if selected:
		draw_arc(Vector2.ZERO, _radius + 5.0, 0.0, TAU, 28, Color(1.0, 0.85, 0.2), 2.0)

	if max_hp > 0 and hp < max_hp:
		var bar_width := _radius * 2.4
		var ratio := clampf(float(hp) / float(max_hp), 0.0, 1.0)
		var origin := Vector2(-bar_width * 0.5, -_radius - 8.0)
		draw_rect(Rect2(origin, Vector2(bar_width, 4.0)), Color(0.1, 0.1, 0.12, 0.9), true)
		draw_rect(Rect2(origin, Vector2(bar_width * ratio, 4.0)), Color(0.35, 0.85, 0.4), true)


func _apply_body_color() -> void:
	if entity_kind == "player":
		_body_color = Color(0.35, 0.75, 1.0)
	else:
		_body_color = Color(0.85, 0.4, 0.4)
