extends CanvasLayer

## Minimal, touch-friendly HUD for the M4 realtime loop.
##
## Renders server-provided state only: identity, HP/XP (when present), map and
## authoritative position, connection status, latency and feedback. Combat is
## intentionally inert in M4.

var game: Node = null
var _name_label: Label
var _level_label: Label
var _hp_label: Label
var _hp_bar: ProgressBar
var _xp_label: Label
var _xp_bar: ProgressBar
var _position_label: Label
var _status_label: Label
var _ping_label: Label
var _feedback_label: Label
var _attack_button: Button
var _inventory_list: VBoxContainer


func _ready() -> void:
	game = get_node_or_null("/root/GameClient")
	_build()
	if game != null:
		game.connection_status.connect(_on_status)
		game.feedback.connect(_on_feedback)
		game.inventory_changed.connect(_on_inventory)
		game.rtt_changed.connect(_on_rtt)
		game.world_state.player_updated.connect(_refresh)
		_refresh()


func _build() -> void:
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)

	var stats_panel := PanelContainer.new()
	stats_panel.position = Vector2(12, 12)
	root.add_child(stats_panel)

	var stats := VBoxContainer.new()
	stats.add_theme_constant_override("separation", 4)
	stats_panel.add_child(stats)

	_name_label = Label.new()
	stats.add_child(_name_label)
	_level_label = Label.new()
	stats.add_child(_level_label)

	_hp_label = Label.new()
	stats.add_child(_hp_label)
	_hp_bar = ProgressBar.new()
	_hp_bar.custom_minimum_size = Vector2(210, 16)
	_hp_bar.max_value = 100
	stats.add_child(_hp_bar)

	_xp_label = Label.new()
	stats.add_child(_xp_label)
	_xp_bar = ProgressBar.new()
	_xp_bar.custom_minimum_size = Vector2(210, 10)
	_xp_bar.max_value = 100
	stats.add_child(_xp_bar)

	_position_label = Label.new()
	stats.add_child(_position_label)

	_status_label = Label.new()
	stats.add_child(_status_label)

	_ping_label = Label.new()
	stats.add_child(_ping_label)

	var inventory_panel := PanelContainer.new()
	inventory_panel.anchor_left = 1.0
	inventory_panel.anchor_right = 1.0
	inventory_panel.offset_left = -232.0
	inventory_panel.offset_right = -12.0
	inventory_panel.offset_top = 12.0
	root.add_child(inventory_panel)

	var inventory_column := VBoxContainer.new()
	inventory_column.custom_minimum_size = Vector2(200, 0)
	inventory_panel.add_child(inventory_column)

	var inventory_title := Label.new()
	inventory_title.text = "Inventory"
	inventory_column.add_child(inventory_title)

	_inventory_list = VBoxContainer.new()
	inventory_column.add_child(_inventory_list)

	_attack_button = Button.new()
	_attack_button.text = "Attack"
	_attack_button.disabled = true
	_attack_button.anchor_left = 1.0
	_attack_button.anchor_top = 1.0
	_attack_button.anchor_right = 1.0
	_attack_button.anchor_bottom = 1.0
	_attack_button.offset_left = -164.0
	_attack_button.offset_top = -100.0
	_attack_button.offset_right = -24.0
	_attack_button.offset_bottom = -24.0
	_attack_button.pressed.connect(_on_attack)
	root.add_child(_attack_button)

	_feedback_label = Label.new()
	_feedback_label.anchor_top = 1.0
	_feedback_label.anchor_bottom = 1.0
	_feedback_label.offset_left = 16.0
	_feedback_label.offset_top = -44.0
	_feedback_label.offset_right = 760.0
	_feedback_label.offset_bottom = -16.0
	root.add_child(_feedback_label)


func _on_attack() -> void:
	if game != null:
		game.request_attack("")


func _refresh() -> void:
	if game == null:
		return
	var player: Dictionary = game.world_state.player
	if player.is_empty():
		_name_label.text = "No character"
		_position_label.text = "Map %d" % int(game.world_state.map_id)
		return

	_name_label.text = String(player.get("name", player.get("id", "Hero")))
	_level_label.text = "Level %d  -  %s" % [
		int(player.get("level", 1)),
		String(player.get("characterClass", "")),
	]

	if int(player.get("maxHp", 0)) > 0:
		var hp := int(player.get("hp", 0))
		var max_hp := maxi(1, int(player.get("maxHp", 1)))
		_hp_label.text = "HP %d / %d" % [hp, max_hp]
		_hp_bar.visible = true
		_hp_bar.max_value = max_hp
		_hp_bar.value = hp
	else:
		_hp_label.text = "HP -"
		_hp_bar.visible = false

	if int(player.get("experienceToNext", 0)) > 0:
		var xp := int(player.get("experience", 0))
		var xp_next := maxi(1, int(player.get("experienceToNext", 100)))
		_xp_label.text = "XP %d / %d" % [xp, xp_next]
		_xp_bar.visible = true
		_xp_bar.max_value = xp_next
		_xp_bar.value = xp
	else:
		_xp_label.text = "XP -"
		_xp_bar.visible = false

	_position_label.text = "Map %d  at (%d, %d)  %s" % [
		int(game.world_state.map_id),
		int(player.get("x", 0)),
		int(player.get("y", 0)),
		String(player.get("state", "")),
	]


func _on_status(text: String) -> void:
	if _status_label != null:
		_status_label.text = text


func _on_rtt(rtt_ms: int) -> void:
	if _ping_label != null:
		_ping_label.text = "Ping %d ms" % rtt_ms


func _on_feedback(text: String) -> void:
	if _feedback_label != null and text != "":
		_feedback_label.text = text


func _on_inventory(items: Array) -> void:
	if _inventory_list == null:
		return
	for child in _inventory_list.get_children():
		child.queue_free()
	if items.is_empty():
		var empty := Label.new()
		empty.text = "(empty)"
		_inventory_list.add_child(empty)
		return
	for item in items:
		var label := Label.new()
		label.text = "%s x%d" % [String(item.get("name", "Item")), int(item.get("quantity", 1))]
		_inventory_list.add_child(label)
