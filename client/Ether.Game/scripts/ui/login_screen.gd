extends Control

## Login / session screen. Emits login/register intent to GameClient; the
## bootstrap switches screens when authentication succeeds.

var game: Node = null
var _username: LineEdit
var _password: LineEdit
var _login_button: Button
var _register_button: Button
var _status: Label


func _ready() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	game = get_node_or_null("/root/GameClient")
	_build()
	if game != null:
		game.error_received.connect(_on_error)
		game.authenticated.connect(_on_authenticated)
		game.log_message.connect(_on_log)


func _build() -> void:
	var background := ColorRect.new()
	background.color = Color(0.06, 0.07, 0.10)
	background.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(background)

	var center := CenterContainer.new()
	center.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(center)

	var panel := PanelContainer.new()
	center.add_child(panel)

	var column := VBoxContainer.new()
	column.custom_minimum_size = Vector2(320, 0)
	column.add_theme_constant_override("separation", 10)
	panel.add_child(column)

	var title := Label.new()
	title.text = "PROJECT ETHER"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size", 24)
	column.add_child(title)

	var subtitle := Label.new()
	subtitle.text = "Tap to begin your journey"
	subtitle.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(subtitle)

	_username = LineEdit.new()
	_username.placeholder_text = "Account name"
	_username.text = "Adventurer"
	column.add_child(_username)

	_password = LineEdit.new()
	_password.placeholder_text = "Password"
	_password.secret = true
	column.add_child(_password)

	_login_button = Button.new()
	_login_button.text = "Enter World"
	_login_button.custom_minimum_size = Vector2(0, 52)
	_login_button.pressed.connect(_on_login)
	column.add_child(_login_button)

	_register_button = Button.new()
	_register_button.text = "Create account"
	_register_button.pressed.connect(_on_register)
	column.add_child(_register_button)

	_status = Label.new()
	_status.text = "Disconnected"
	_status.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(_status)


func _on_login() -> void:
	_set_busy(true, "Signing in...")
	game.request_login(_username.text, _password.text)


func _on_register() -> void:
	_set_busy(true, "Creating account...")
	game.request_register(_username.text, _password.text)


func _on_authenticated(display_name: String) -> void:
	_status.text = "Welcome, %s" % display_name


func _on_error(_code: String, message: String) -> void:
	_set_busy(false, message)


func _on_log(text: String) -> void:
	_status.text = text


func _set_busy(busy: bool, message: String) -> void:
	if _login_button != null:
		_login_button.disabled = busy
	if _register_button != null:
		_register_button.disabled = busy
	if _status != null:
		_status.text = message
