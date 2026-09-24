class_name MockApiClient
extends ApiClient

## Offline auth/character/game-token API backed by MockBackend.
##
## Mirrors the real HTTP contract so the client flow is identical in both modes.
## Responses are synchronous (no artificial latency) so the mock flow is
## deterministic and unit-testable.

var backend: MockBackend


func _init(p_backend: MockBackend = null) -> void:
	backend = p_backend if p_backend != null else MockBackend.new()


func login(email: String, _password: String, p_request_id: String = "") -> String:
	var request_id := resolve_request_id(p_request_id)
	backend.authenticate_account(email)
	_complete(request_id, true, {
		"accountId": backend.account_id,
		"accessToken": "mock-access-token",
		"refreshToken": "mock-refresh-token",
	}, "")
	return request_id


func register(email: String, password: String, p_request_id: String = "") -> String:
	var request_id := resolve_request_id(p_request_id)
	backend.authenticate_account(email)
	_complete(request_id, true, {
		"accountId": backend.account_id,
		"email": email,
		"status": "Active",
	}, "")
	return request_id


func refresh(_refresh_token: String, p_request_id: String = "") -> String:
	var request_id := resolve_request_id(p_request_id)
	_complete(request_id, true, {
		"accountId": backend.account_id,
		"accessToken": "mock-access-token",
		"refreshToken": "mock-refresh-token",
	}, "")
	return request_id


func list_characters(_account_id: String, p_request_id: String = "") -> String:
	var request_id := resolve_request_id(p_request_id)
	_complete(request_id, true, backend.characters.duplicate(true), "")
	return request_id


func create_character(_account_id: String, name: String, character_class: String, p_request_id: String = "") -> String:
	var request_id := resolve_request_id(p_request_id)
	var result := backend.api_create_character(name, character_class)
	if bool(result.get("ok", false)):
		_complete(request_id, true, result.get("character", {}), "")
	else:
		_complete(request_id, false, {}, String(result.get("error", "Unable to create character.")))
	return request_id


func request_game_token(character_id: String, p_request_id: String = "") -> String:
	var request_id := resolve_request_id(p_request_id)
	backend.issue_game_token(character_id)
	_complete(request_id, true, {"gameToken": "mock-game-token"}, "")
	return request_id


func get_inventory(character_id: String, p_request_id: String = "") -> String:
	var request_id := resolve_request_id(p_request_id)
	backend.selected_character_id = character_id
	_complete(request_id, true, backend.api_get_inventory(), "")
	return request_id
