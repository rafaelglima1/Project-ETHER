extends Control

## Bootstrap / screen manager — the client's main scene.
##
## Boots the client and swaps screens based on GameClient state:
##   Login -> Character select -> World
## The client always starts, even with no backend reachable.

const LOGIN_SCENE := preload("res://scenes/auth/login.tscn")
const CHARACTER_SCENE := preload("res://scenes/character/character_select.tscn")
const WORLD_SCENE := preload("res://scenes/world/world.tscn")
const DEBUG_OVERLAY_SCENE := preload("res://scenes/ui/debug_overlay.tscn")

var game: Node = null
var _current: Node = null


func _ready() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	game = get_node_or_null("/root/GameClient")
	if game == null:
		push_error("GameClient autoload is missing.")
		return

	game.authenticated.connect(_on_authenticated)
	game.world_entered.connect(_on_world_entered)
	add_child(DEBUG_OVERLAY_SCENE.instantiate())
	game.start()
	_show_login()


func _show(scene: PackedScene) -> void:
	if _current != null and is_instance_valid(_current):
		_current.queue_free()
	_current = scene.instantiate()
	add_child(_current)


func _show_login() -> void:
	_show(LOGIN_SCENE)


func _on_authenticated(_display_name: String) -> void:
	_show(CHARACTER_SCENE)


func _on_world_entered() -> void:
	_show(WORLD_SCENE)
