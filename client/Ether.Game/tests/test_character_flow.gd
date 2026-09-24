extends TestCase

## Regression coverage for the character flow.
##
## The Android "create character -> 401/expired" bug came from a duplicated
## character fetch: the character screen requested the list on _ready while
## GameClient requested it too, and the single-slot HTTPRequest dropped the
## first response (ERR_BUSY). This suite locks in the fix.

var _login_token := ""
var _login_account := ""
var _created := false


func _on_login_create_completed(_id: String, ok: bool, data: Variant, _err: String) -> void:
	if not ok or typeof(data) != TYPE_DICTIONARY:
		return
	if data.has("accessToken"):
		_login_token = String(data["accessToken"])
		_login_account = String(data["accountId"])
	if data.has("characterId"):
		_created = true


func test_character_screen_does_not_fetch_on_ready() -> void:
	if TestCase.tree == null:
		return
	var root := TestCase.tree.root

	var fake := FakeGameNode.new()
	root.add_child(fake)

	# Invoke _ready() explicitly: during _initialize the tree is not yet running,
	# so a mounted node's _ready would be deferred.
	var screen: Node = load("res://scripts/ui/character_select_screen.gd").new()
	screen.injected_game = fake
	screen._ready()

	assert_eq(fake.request_characters_calls, 0, "screen must not duplicate the character fetch")
	assert_eq(fake.create_character_calls, 0, "screen must not create on ready")

	screen.free()
	fake.free()


func test_login_stores_token_and_create_character_uses_it() -> void:
	var backend := MockBackend.new()
	var api := MockApiClient.new(backend)
	_login_token = ""
	_login_account = ""
	_created = false
	api.completed.connect(_on_login_create_completed)

	api.login("hero@example.com", "pw")
	assert_true(_login_token.length() > 0, "access token stored after login")
	assert_true(_login_account.length() > 0, "account id captured from login")

	api.set_token(_login_token)
	var before := backend.characters.size()
	api.create_character(_login_account, "Tester", "Warrior")
	assert_true(_created, "create_character succeeded after storing the token")
	assert_eq(backend.characters.size(), before + 1, "character added")


func test_register_response_is_followed_by_login_before_fetching_characters() -> void:
	var game: Node = load("res://scripts/core/game_client.gd").new()
	var config := ClientConfig.new()
	config.mode = ClientConfig.Mode.MOCK
	game._build(config)
	var authenticated_names: Array[String] = []
	var character_lists: Array = []
	game.authenticated.connect(func(name): authenticated_names.append(name))
	game.characters_changed.connect(func(characters): character_lists.append(characters))

	game.request_register("fresh-player@example.com", "temporary-test-password")

	assert_eq(authenticated_names.size(), 1, "registration completes only after login")
	assert_eq(game.client_state.email, "fresh-player@example.com")
	assert_eq(game.client_state.access_token, "mock-access-token", "register response itself has no token")
	assert_eq(character_lists.size(), 1, "character list requested after authenticated login")
	assert_eq(game._registration_password, "", "registration password cleared after follow-up login")
	game.free()


func test_oracle_profile_is_used_for_real_defaults() -> void:
	var config := ClientConfig.new()
	config.profile = ClientConfig.Profile.ORACLE
	config.apply_profile()
	assert_eq(config.api_base_url, "https://game.rotagov.com.br")
	assert_eq(config.game_websocket_url, "wss://game.rotagov.com.br/game")
