class_name MockApiClient
extends ApiClient

## Offline auth/character API backed by MockBackend.
##
## Responses are synchronous (no artificial latency) so the mock flow is
## deterministic and unit-testable.

var backend: MockBackend


func _init(p_backend: MockBackend = null) -> void:
	backend = p_backend if p_backend != null else MockBackend.new()


func login(username: String, _password: String, p_request_id: String = "") -> String:
	var request_id := resolve_request_id(p_request_id)
	var display := username.strip_edges()
	if display == "":
		display = "Adventurer"
	backend.display_name = display
	backend.authenticated = true
	_complete(request_id, true, {
		"accountId": backend.account_id,
		"displayName": display,
		"accessToken": "mock-access-token",
		"refreshToken": "mock-refresh-token",
		"gameToken": "mock-game-token",
	}, "")
	return request_id


func register(username: String, password: String, p_request_id: String = "") -> String:
	return login(username, password, p_request_id)


func list_characters(_account_id: String, p_request_id: String = "") -> String:
	var request_id := resolve_request_id(p_request_id)
	_complete(request_id, true, {"characters": backend.characters.duplicate(true)}, "")
	return request_id


func create_character(_account_id: String, name: String, character_class: String, p_request_id: String = "") -> String:
	var request_id := resolve_request_id(p_request_id)
	var result := backend.api_create_character(name, character_class)
	_complete(request_id, bool(result.get("ok", false)), result, String(result.get("error", "")))
	return request_id


func select_character(character_id: String, p_request_id: String = "") -> String:
	var request_id := resolve_request_id(p_request_id)
	backend.selected_character_id = character_id
	_complete(request_id, true, {"characterId": character_id}, "")
	return request_id
