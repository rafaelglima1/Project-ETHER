extends CanvasLayer

## Minimal, touch-friendly HUD for the First Playable loop.
##
## Renders server-provided state only: HP/XP/level/gold, inventory mirror,
## connection status, combat feedback and a single Attack action.

var game: Node = null
var _name_label: Label
var _level_label: Label
var _hp_label: Label
var _hp_bar: ProgressBar
var _xp_label: Label
var _xp_bar: ProgressBar
var _gold_label: Label
var _status_label: Label
var _feedback_label: Label
var _attack_button: Button
var _inventory_list: VBoxContainer


func _ready() -> void:
	game = get_node_or_null("/root/GameClient")
	_build()
	if game != null:
		game.connection_status.connect(_on_status)
		game.combat_feedback.connect(_on_feedback)
		game.experience_gained.connect(_on_experience)
		game.level_up.connect(_on_level_up)
		game.loot_received.connect(_on_loot)
		game.inventory_changed.connect(_on_inventory)
		game.player_died.connect(_on_player_died)
		game.player_respawned.connect(_on_player_respawned)
		game.world_state.player_updated.connect(_refresh)
		_refresh()


func _build() -> void:
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)

	# --- top-left: character stats ---
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

	_gold_label = Label.new()
	stats.add_child(_gold_label)

	_status_label = Label.new()
	stats.add_child(_status_label)

	# --- top-right: inventory mirror ---
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

	# --- bottom-right: action button (touch friendly) ---
	_attack_button = Button.new()
	_attack_button.text = "Attack"
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

	# --- bottom-left: combat / interaction feedback ---
	_feedback_label = Label.new()
	_feedback_label.anchor_top = 1.0
	_feedback_label.anchor_bottom = 1.0
	_feedback_label.offset_left = 16.0
	_feedback_label.offset_top = -44.0
	_feedback_label.offset_right = 760.0
	_feedback_label.offset_bottom = -16.0
	root.add_child(_feedback_label)


func _on_attack() -> void:
	if game == null:
		return
	var world_state: WorldState = game.world_state
	if world_state.player_id == "":
		return
	var target := world_state.nearest_creature(world_state.player_position(), 1)
	if target == "":
		_on_feedback("No creature in range.")
		return
	game.request_attack(target)


func _refresh() -> void:
	if game == null:
		return
	var player: Dictionary = game.world_state.player
	if player.is_empty():
		_name_label.text = "No character"
		return

	_name_label.text = String(player.get("name", "Hero"))
	_level_label.text = "Level %d  -  %s" % [
		int(player.get("level", 1)),
		String(player.get("characterClass", "")),
	]

	var hp := int(player.get("hp", 0))
	var max_hp := maxi(1, int(player.get("maxHp", 1)))
	_hp_label.text = "HP %d / %d" % [hp, max_hp]
	_hp_bar.max_value = max_hp
	_hp_bar.value = hp

	var xp := int(player.get("experience", 0))
	var xp_next := maxi(1, int(player.get("experienceToNext", 100)))
	_xp_label.text = "XP %d / %d" % [xp, xp_next]
	_xp_bar.max_value = xp_next
	_xp_bar.value = xp

	_gold_label.text = "Gold %d" % int(player.get("gold", 0))


func _on_status(text: String) -> void:
	if _status_label != null:
		_status_label.text = text


func _on_feedback(text: String) -> void:
	if _feedback_label != null and text != "":
		_feedback_label.text = text


func _on_experience(_level: int, _total: int, _amount: int) -> void:
	_refresh()


func _on_level_up(level: int) -> void:
	_on_feedback("Level up! You are now level %d." % level)
	_refresh()


func _on_loot(items: Array) -> void:
	if items.is_empty():
		return
	var names: Array = []
	for item in items:
		names.append("%s x%d" % [String(item.get("name", "Item")), int(item.get("quantity", 1))])
	_on_feedback("Loot: %s" % ", ".join(names))
	_refresh()


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


func _on_player_died() -> void:
	_on_feedback("You have fallen.")


func _on_player_respawned() -> void:
	_on_feedback("You have respawned.")
	_refresh()
