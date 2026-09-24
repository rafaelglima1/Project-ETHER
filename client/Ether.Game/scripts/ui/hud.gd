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
var _inventory_button: Button
var _inventory_count: Label
var _inventory_backdrop: ColorRect
var _inventory_modal: Control
var _death_overlay: ColorRect
var _death_panel: Control
var _death_message: Label
var _respawn_button: Button


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
		if game.has_signal("player_died"):
			game.player_died.connect(_on_player_died)
		if game.has_signal("player_respawned"):
			game.player_respawned.connect(_on_player_respawned)
		game.world_state.player_updated.connect(_refresh)
		_refresh()
		_apply_death_state(game.is_player_dead() if game.has_method("is_player_dead") else false)
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

	# Inventory toggle (top-right). The modal itself is added later so it draws
	# above the world but below the death overlay.
	_inventory_button = Button.new()
	_inventory_button.text = "Inventory"
	_inventory_button.anchor_left = 1.0
	_inventory_button.anchor_right = 1.0
	_inventory_button.offset_left = -212.0
	_inventory_button.offset_right = -12.0
	_inventory_button.offset_top = 12.0
	_inventory_button.offset_bottom = 76.0
	_inventory_button.pressed.connect(_on_inventory_pressed)
	root.add_child(_inventory_button)

	_inventory_count = Label.new()
	_inventory_count.anchor_left = 1.0
	_inventory_count.anchor_right = 1.0
	_inventory_count.offset_left = -212.0
	_inventory_count.offset_right = -12.0
	_inventory_count.offset_top = 80.0
	_inventory_count.offset_bottom = 108.0
	_inventory_count.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_inventory_count.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_inventory_count.text = "0"
	root.add_child(_inventory_count)

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
	_feedback_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(_feedback_label)

	var move_hint := Label.new()
	move_hint.text = "Drag the stick to move  |  Tap the map"
	move_hint.anchor_top = 1.0
	move_hint.anchor_bottom = 1.0
	move_hint.offset_left = 30.0
	move_hint.offset_top = -268.0
	move_hint.offset_right = 420.0
	move_hint.offset_bottom = -244.0
	move_hint.mouse_filter = Control.MOUSE_FILTER_IGNORE
	move_hint.add_theme_font_size_override("font_size", 14)
	move_hint.modulate = Color(0.75, 0.82, 0.92)
	root.add_child(move_hint)

	# Inventory modal: backdrop blocks stray world taps; modal carries the list.
	# Added before the death overlay so death always stays on top.
	_inventory_backdrop = ColorRect.new()
	_inventory_backdrop.color = Color(0.0, 0.0, 0.0, 0.45)
	_inventory_backdrop.set_anchors_preset(Control.PRESET_FULL_RECT)
	_inventory_backdrop.mouse_filter = Control.MOUSE_FILTER_STOP
	_inventory_backdrop.visible = false
	_inventory_backdrop.gui_input.connect(func(event):
		if event is InputEventMouseButton and event.pressed:
			_close_inventory())
	add_child(_inventory_backdrop)

	_inventory_modal = CenterContainer.new()
	_inventory_modal.set_anchors_preset(Control.PRESET_FULL_RECT)
	_inventory_modal.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_inventory_modal.visible = false
	add_child(_inventory_modal)

	var modal_panel := PanelContainer.new()
	_inventory_modal.add_child(modal_panel)

	var modal_column := VBoxContainer.new()
	modal_column.custom_minimum_size = Vector2(340, 0)
	modal_column.add_theme_constant_override("separation", 10)
	modal_panel.add_child(modal_column)

	var modal_title := Label.new()
	modal_title.text = "Inventory"
	modal_title.add_theme_font_size_override("font_size", 22)
	modal_column.add_child(modal_title)

	_inventory_list = VBoxContainer.new()
	_inventory_list.add_theme_constant_override("separation", 6)
	modal_column.add_child(_inventory_list)

	var close_button := Button.new()
	close_button.text = "Close"
	close_button.custom_minimum_size = Vector2(0, 64)
	close_button.pressed.connect(_close_inventory)
	modal_column.add_child(close_button)

	# Minimal death state: the overlay blocks stray taps but stays translucent so
	# the world remains visible; the panel carries the message + Respawn action.
	_death_overlay = ColorRect.new()
	_death_overlay.color = Color(0.0, 0.0, 0.0, 0.55)
	_death_overlay.set_anchors_preset(Control.PRESET_FULL_RECT)
	_death_overlay.mouse_filter = Control.MOUSE_FILTER_STOP
	_death_overlay.visible = false
	add_child(_death_overlay)

	_death_panel = CenterContainer.new()
	_death_panel.set_anchors_preset(Control.PRESET_FULL_RECT)
	_death_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_death_panel.visible = false
	add_child(_death_panel)

	var death_column := VBoxContainer.new()
	death_column.add_theme_constant_override("separation", 20)
	_death_panel.add_child(death_column)

	_death_message = Label.new()
	_death_message.text = "YOU HAVE DIED"
	_death_message.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_death_message.add_theme_font_size_override("font_size", 34)
	_death_message.add_theme_color_override("font_color", Color(1.0, 0.35, 0.3))
	death_column.add_child(_death_message)

	_respawn_button = Button.new()
	_respawn_button.text = "Respawn"
	_respawn_button.custom_minimum_size = Vector2(280, 92)
	_respawn_button.add_theme_font_size_override("font_size", 26)
	_respawn_button.visible = false
	_respawn_button.pressed.connect(_on_respawn_pressed)
	death_column.add_child(_respawn_button)


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
	_render_inventory(items)
	# Mirror the authoritative stack count next to the button.
	if _inventory_count != null:
		_inventory_count.text = str(items.size())
	if _inventory_modal != null and _inventory_modal.visible and game != null and game.has_method("log_line"):
		game.log_line("Inventory refresh: %d stack(s)." % items.size())


func _on_inventory_pressed() -> void:
	if _inventory_modal != null:
		_inventory_modal.visible = true
	if _inventory_backdrop != null:
		_inventory_backdrop.visible = true
	# Refresh from the authoritative endpoint when opening.
	if game != null and game.has_method("request_inventory"):
		game.request_inventory()
	_render_inventory(game.client_state.inventory if game != null else [])


func _close_inventory() -> void:
	if _inventory_modal != null:
		_inventory_modal.visible = false
	if _inventory_backdrop != null:
		_inventory_backdrop.visible = false


func _render_inventory(items: Array) -> void:
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
		var row := Label.new()
		row.text = "%s  x%d" % [String(item.get("name", "Item")), int(item.get("quantity", 1))]
		row.add_theme_font_size_override("font_size", 17)
		_inventory_list.add_child(row)


func _on_player_died() -> void:
	_apply_death_state(true)


func _on_respawn_pressed() -> void:
	if game != null and game.has_method("request_respawn"):
		game.request_respawn()


func _on_player_respawned() -> void:
	_apply_death_state(false)
	_refresh()


## Shows/hides the death overlay and gates the action buttons. HP and position
## are never faked here — they come from the server.
func _apply_death_state(dead: bool) -> void:
	if _death_overlay != null:
		_death_overlay.visible = dead
	if _death_panel != null:
		_death_panel.visible = dead
	if _respawn_button != null:
		_respawn_button.visible = dead
		_respawn_button.disabled = not dead
	if _attack_button != null:
		_attack_button.disabled = dead
	if _power_button != null:
		_power_button.disabled = dead
	if dead:
		_on_feedback("You have died. Respawn to continue.")
