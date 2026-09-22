extends CanvasLayer

## Minimal, touch-friendly HUD.
##
## Renders server-provided state only: identity, HP/XP, map and authoritative
## position, target, creatures, connection status, latency and combat feedback.
## The action buttons express intent; the server resolves everything.

var game: Node = null
var _name_label: Label
var _level_label: Label
var _hp_label: Label
var _hp_bar: ProgressBar
var _xp_label: Label
var _position_label: Label
var _target_label: Label
var _status_label: Label
var _ping_label: Label
var _feedback_label: Label
var _attack_button: Button
var _power_button: Button
var _inventory_list: VBoxContainer


func _ready() -> void:
	game = get_node_or_null("/root/GameClient")
	_build()
	if game != null:
		game.connection_status.connect(_on_status)
		game.feedback.connect(_on_feedback)
		game.inventory_changed.connect(_on_inventory)
		game.rtt_changed.connect(_on_rtt)
		if game.has_signal("target_changed"):
			game.target_changed.connect(_on_target_changed)
		if game.has_signal("experience_changed"):
			game.experience_changed.connect(_on_experience)
		if game.has_signal("loot_received"):
			game.loot_received.connect(_on_loot)
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

	_position_label = Label.new()
	stats.add_child(_position_label)

	_target_label = Label.new()
	stats.add_child(_target_label)

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

	# Action bar (bottom-right, touch friendly).
	var actions := HBoxContainer.new()
	actions.anchor_left = 1.0
	actions.anchor_top = 1.0
	actions.anchor_right = 1.0
	actions.anchor_bottom = 1.0
	actions.offset_left = -320.0
	actions.offset_top = -104.0
	actions.offset_right = -24.0
	actions.offset_bottom = -24.0
	actions.alignment = BoxContainer.ALIGNMENT_END
	actions.add_theme_constant_override("separation", 12)
	root.add_child(actions)

	_power_button = Button.new()
	_power_button.text = "Power Strike"
	_power_button.custom_minimum_size = Vector2(140, 80)
	_power_button.pressed.connect(_on_power_strike)
	actions.add_child(_power_button)

	_attack_button = Button.new()
	_attack_button.text = "Attack"
	_attack_button.custom_minimum_size = Vector2(140, 80)
	_attack_button.pressed.connect(_on_attack)
	actions.add_child(_attack_button)

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
		game.request_attack()


func _on_power_strike() -> void:
	if game != null:
		game.request_attack_ability(ProtocolMessages.ABILITY_POWER_STRIKE)


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

	var hp := int(player.get("hp", 0))
	var max_hp := int(player.get("maxHp", 0))
	if max_hp > 0:
		_hp_label.text = "HP %d / %d" % [hp, max_hp]
		_hp_bar.visible = true
		_hp_bar.max_value = max_hp
		_hp_bar.value = hp
	else:
		_hp_label.text = "HP -"
		_hp_bar.visible = false

	_position_label.text = "Map %d  at (%d, %d)  %s  |  creatures %d" % [
		int(game.world_state.map_id),
		int(player.get("x", 0)),
		int(player.get("y", 0)),
		String(player.get("state", "")),
		game.world_state.creature_count(),
	]

	if _xp_label != null:
		if player.has("experience"):
			_xp_label.text = "XP %d" % int(player.get("experience", 0))
		else:
			_xp_label.text = "XP -"


func _on_experience(_level: int, _experience: int, _gained: int, _levels_gained: int) -> void:
	_refresh()


func _on_loot(items: Array) -> void:
	if items.is_empty():
		return
	var names: Array = []
	for item in items:
		names.append("%s x%d" % [String(item.get("name", "Item")), int(item.get("quantity", 1))])
	_on_feedback("Loot: %s" % ", ".join(names))


func _on_status(text: String) -> void:
	if _status_label != null:
		_status_label.text = text


func _on_rtt(rtt_ms: int) -> void:
	if _ping_label != null:
		_ping_label.text = "Ping %d ms" % rtt_ms


func _on_feedback(text: String) -> void:
	if _feedback_label != null and text != "":
		_feedback_label.text = text


func _on_target_changed(target_id: String) -> void:
	if _target_label == null:
		return
	if target_id == "":
		_target_label.text = "Target: -"
		return
	var entity: Dictionary = game.world_state.get_entity(target_id)
	var name := String(entity.get("name", target_id))
	var hp := int(entity.get("hp", 0))
	var max_hp := int(entity.get("maxHp", 0))
	var state := String(entity.get("state", ""))
	_target_label.text = "Target: %s (%d/%d) %s" % [name, hp, max_hp, state]


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
