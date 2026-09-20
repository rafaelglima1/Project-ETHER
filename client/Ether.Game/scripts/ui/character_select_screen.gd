extends Control

## Character list / selection / creation screen.

const CLASS_OPTIONS := ["Warrior", "Ranger", "Arcanist"]

var game: Node = null
var _list: VBoxContainer
var _status: Label
var _new_name: LineEdit
var _class_option: OptionButton


func _ready() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	game = get_node_or_null("/root/GameClient")
	_build()
	if game != null:
		game.characters_changed.connect(_on_characters_changed)
		game.character_selected.connect(_on_character_selected)
		game.error_received.connect(_on_error)
		game.request_characters()


func _build() -> void:
	var background := ColorRect.new()
	background.color = Color(0.06, 0.07, 0.10)
	background.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(background)

	var margin := MarginContainer.new()
	margin.set_anchors_preset(Control.PRESET_FULL_RECT)
	margin.add_theme_constant_override("margin_left", 24)
	margin.add_theme_constant_override("margin_right", 24)
	margin.add_theme_constant_override("margin_top", 24)
	margin.add_theme_constant_override("margin_bottom", 24)
	add_child(margin)

	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 12)
	margin.add_child(column)

	var title := Label.new()
	title.text = "Choose your character"
	title.add_theme_font_size_override("font_size", 22)
	column.add_child(title)

	_list = VBoxContainer.new()
	_list.add_theme_constant_override("separation", 8)
	column.add_child(_list)

	var divider := HSeparator.new()
	column.add_child(divider)

	var create_row := HBoxContainer.new()
	create_row.add_theme_constant_override("separation", 8)
	column.add_child(create_row)

	_new_name = LineEdit.new()
	_new_name.placeholder_text = "New character name"
	_new_name.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	create_row.add_child(_new_name)

	_class_option = OptionButton.new()
	for class_choice in CLASS_OPTIONS:
		_class_option.add_item(class_choice)
	create_row.add_child(_class_option)

	var create_button := Button.new()
	create_button.text = "Create"
	create_button.pressed.connect(_on_create)
	create_row.add_child(create_button)

	_status = Label.new()
	_status.text = "Loading characters..."
	column.add_child(_status)


func _on_characters_changed(characters: Array) -> void:
	for child in _list.get_children():
		child.queue_free()

	if characters.is_empty():
		_status.text = "No characters yet. Create one below."
		return

	_status.text = "%d character(s)" % characters.size()
	for character in characters:
		var button := Button.new()
		var class_value := String(character.get("characterClass", "Warrior"))
		button.text = "%s  -  %s  (Lv %d)" % [
			String(character.get("name", "?")),
			class_value,
			int(character.get("level", 1)),
		]
		button.custom_minimum_size = Vector2(0, 48)
		var character_id := String(character.get("characterId", ""))
		button.pressed.connect(func(): _on_select(character_id))
		_list.add_child(button)


func _on_select(character_id: String) -> void:
	_status.text = "Entering world..."
	game.request_select_character(character_id)


func _on_create() -> void:
	var name := _new_name.text.strip_edges()
	if name.length() < 2:
		_status.text = "Name must be at least 2 characters."
		return
	_status.text = "Creating character..."
	game.request_create_character(name, _class_option.get_item_text(_class_option.selected))


func _on_character_selected(character: Dictionary) -> void:
	_status.text = "Selected %s" % String(character.get("name", "character"))


func _on_error(_code: String, message: String) -> void:
	_status.text = message
