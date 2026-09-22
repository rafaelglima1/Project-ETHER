extends CanvasLayer

## Development diagnostics overlay. Off by default; toggle with F3 or set
## ETHER_CLIENT_DEBUG=1. Shows connection/session/position/last event/last error.
## Never shows tokens, passwords or any credential.

var game: Node = null
var _panel: PanelContainer
var _label: Label
var _accumulator := 0.0
var _shown := false


func _ready() -> void:
	game = get_node_or_null("/root/GameClient")
	_build()
	_shown = OS.get_environment("ETHER_CLIENT_DEBUG") == "1"
	_panel.visible = _shown
	set_process(true)


func _build() -> void:
	_panel = PanelContainer.new()
	_panel.offset_left = 12.0
	_panel.offset_top = 150.0
	add_child(_panel)

	var column := VBoxContainer.new()
	column.custom_minimum_size = Vector2(340, 0)
	_panel.add_child(column)

	var title := Label.new()
	title.text = "Diagnostics (F3)"
	column.add_child(title)

	_label = Label.new()
	_label.add_theme_font_size_override("font_size", 12)
	column.add_child(_label)


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_F3:
		_shown = not _shown
		_panel.visible = _shown


func _process(delta: float) -> void:
	if not _shown:
		return
	_accumulator += delta
	if _accumulator < 0.25:
		return
	_accumulator = 0.0
	_refresh()


func _refresh() -> void:
	if game == null or not game.has_method("debug_snapshot"):
		return
	var info: Dictionary = game.debug_snapshot()
	var rtt: Variant = info.get("rtt_ms", -1)
	_label.text = "\n".join([
		"mode: %s" % info.get("mode", "?"),
		"server: %s" % info.get("server", "?"),
		"connection: %s" % info.get("connection", "?"),
		"session: %s" % info.get("session", "?"),
		"ping: %s" % ("%s ms" % rtt if int(rtt) >= 0 else "-"),
		"character: %s" % info.get("character_id", ""),
		"map: %s" % info.get("map_id", 0),
		"position: %s" % info.get("position", "?"),
		"last event: %s" % info.get("last_event", ""),
		"last error: %s" % info.get("last_error", ""),
	])
