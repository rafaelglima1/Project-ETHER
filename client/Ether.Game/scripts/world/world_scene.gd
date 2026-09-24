extends Node2D

## World presentation scene.
##
## Renders a small bounded map, spawns/updates/despawns entity views from
## WorldState, and converts pointer input into movement / target / attack
## intents. It holds no authoritative gameplay logic: positions, creature AI,
## health, death and damage all come from the server.

var game: Node = null
var world_state: WorldState
var input_controller: InputController
var camera: CameraController
var joystick: MoveStick
var tile_size := 32

const HUD_SCENE := preload("res://scenes/ui/hud.tscn")

var _hud: CanvasLayer
var _views: Dictionary = {}
var _tap_marker := Vector2i(-99, -99)
var _tap_marker_time := 0.0


func _ready() -> void:
	game = get_node_or_null("/root/GameClient")
	if game != null:
		world_state = game.world_state
	else:
		world_state = WorldState.new()

	tile_size = int(world_state.map.get("tileSize", 32))

	input_controller = InputController.new()
	input_controller.name = "InputController"
	add_child(input_controller)
	input_controller.pointer_pressed.connect(_on_pointer_pressed)
	input_controller.move_step.connect(_on_move_step)
	input_controller.cancel_pressed.connect(_on_cancel_pressed)

	camera = CameraController.new()
	camera.name = "Camera"
	add_child(camera)

	_hud = HUD_SCENE.instantiate()
	add_child(_hud)

	# Virtual stick lives on the HUD layer (bottom-left) so it renders above the
	# world and never blocks the action bar / HUD panels.
	joystick = MoveStick.new()
	joystick.name = "MoveStick"
	_hud.add_child(joystick)
	joystick.direction_changed.connect(_on_joystick)

	world_state.entity_upserted.connect(_on_entity_upserted)
	world_state.entity_removed.connect(_on_entity_removed)
	world_state.player_updated.connect(_on_player_updated)
	world_state.snapshot_applied.connect(_on_snapshot_applied)

	if game != null and game.has_signal("target_changed"):
		game.target_changed.connect(_on_target_changed)
	if game != null and game.has_signal("player_died"):
		game.player_died.connect(_on_player_died)
	if game != null and game.has_signal("player_respawned"):
		game.player_respawned.connect(_on_player_respawned)

	_sync_all()
	_refresh_camera()
	queue_redraw()


# --- View lifecycle -----------------------------------------------------------

func _sync_all() -> void:
	for id in world_state.entities.keys():
		_on_entity_upserted(String(id))


func _on_entity_upserted(id: String) -> void:
	var data := world_state.get_entity(id)
	if data.is_empty():
		return
	if not _views.has(id):
		_spawn_view(data)
	var view: EntityView = _views[id]
	if view != null and is_instance_valid(view):
		view.update_from(data)


func _spawn_view(data: Dictionary) -> void:
	var kind := String(data.get("kind", "creature"))
	var view: EntityView = Player.new() if kind == "player" else Creature.new()
	view.name = "Entity_%s" % String(data.get("id", "")).replace("-", "_")
	add_child(view)
	view.setup(data, tile_size)
	_views[String(data.get("id", ""))] = view


func _on_entity_removed(id: String) -> void:
	if _views.has(id):
		var view: EntityView = _views[id]
		_views.erase(id)
		if view != null and is_instance_valid(view):
			view.queue_free()
	queue_redraw()


func _on_player_updated() -> void:
	if camera.target == null:
		_refresh_camera()
	queue_redraw()


func _on_snapshot_applied() -> void:
	for id in _views.keys():
		var view: EntityView = _views[id]
		if view != null and is_instance_valid(view):
			view.queue_free()
	_views.clear()
	tile_size = int(world_state.map.get("tileSize", tile_size))
	_sync_all()
	_refresh_camera()
	queue_redraw()


func _refresh_camera() -> void:
	if world_state.map.is_empty():
		return
	var width := int(world_state.map.get("width", 0)) * tile_size
	var height := int(world_state.map.get("height", 0)) * tile_size
	camera.setup(_player_view_node(), Vector2(width, height))


func _player_view_node() -> Node2D:
	if _views.has(world_state.player_id):
		return _views[world_state.player_id]
	return null


# --- Input --------------------------------------------------------------------

func _on_joystick(direction: Vector2) -> void:
	input_controller.set_joystick(direction)


func _process(delta: float) -> void:
	if _tap_marker_time > 0.0:
		_tap_marker_time -= delta
		queue_redraw()


func _on_pointer_pressed(screen_position: Vector2) -> void:
	if world_state.map.is_empty() or game == null:
		return

	var world_position := get_canvas_transform().affine_inverse() * screen_position
	var tile := Vector2i(int(floor(world_position.x / tile_size)), int(floor(world_position.y / tile_size)))

	# Immediate, non-authoritative feedback: mark where the player tapped.
	_tap_marker = tile
	_tap_marker_time = 0.5
	queue_redraw()

	var creature_id := world_state.creature_at(tile.x, tile.y)

	if creature_id != "":
		game.select_target(creature_id)
		if _is_adjacent(tile, world_state.player_position()):
			game.request_attack(creature_id)
		else:
			game.request_move(tile.x, tile.y)
	else:
		game.clear_target()
		game.request_move(tile.x, tile.y)


func _on_move_step(direction: Vector2i) -> void:
	var next := world_state.player_position() + direction
	game.request_move(next.x, next.y)


func _on_cancel_pressed() -> void:
	if game != null:
		game.clear_target()
	queue_redraw()


func _on_target_changed(_target_id: String) -> void:
	queue_redraw()


## While dead the stick must not keep emitting movement; the GameClient also
## gates commands, this just stops the visual drift immediately.
func _on_player_died() -> void:
	if joystick != null:
		joystick.reset()
	queue_redraw()


func _on_player_respawned() -> void:
	queue_redraw()


func _is_adjacent(a: Vector2i, b: Vector2i) -> bool:
	return maxi(absi(a.x - b.x), absi(a.y - b.y)) <= 1


# --- Rendering ----------------------------------------------------------------

func _draw() -> void:
	var map := world_state.map
	if map.is_empty():
		return

	var width := int(map.get("width", 0))
	var height := int(map.get("height", 0))
	var extent := Vector2(width * tile_size, height * tile_size)

	draw_rect(Rect2(Vector2.ZERO, extent), Color(0.10, 0.12, 0.16), true)
	for x in range(width + 1):
		draw_line(Vector2(x * tile_size, 0.0), Vector2(x * tile_size, extent.y), Color(0.16, 0.19, 0.24), 1.0)
	for y in range(height + 1):
		draw_line(Vector2(0.0, y * tile_size), Vector2(extent.x, y * tile_size), Color(0.16, 0.19, 0.24), 1.0)
	draw_rect(Rect2(Vector2.ZERO, extent), Color(0.30, 0.50, 0.70), false, 2.0)

	var target_id := ""
	if game != null:
		target_id = String(game.selected_target_id)
	if target_id != "" and world_state.has_entity(target_id):
		var tile := world_state.entity_position(target_id)
		draw_rect(
			Rect2(Vector2(tile.x * tile_size, tile.y * tile_size), Vector2(tile_size, tile_size)),
			Color(1.0, 0.85, 0.2, 0.6), false, 2.0)

	if _tap_marker_time > 0.0:
		var origin := Vector2(_tap_marker.x * tile_size, _tap_marker.y * tile_size)
		var alpha := clampf(_tap_marker_time / 0.5, 0.0, 1.0)
		draw_rect(Rect2(origin, Vector2(tile_size, tile_size)), Color(0.55, 0.85, 1.0, 0.30 * alpha), true)
		draw_rect(Rect2(origin, Vector2(tile_size, tile_size)), Color(0.70, 0.92, 1.0, 0.9 * alpha), false, 2.0)
