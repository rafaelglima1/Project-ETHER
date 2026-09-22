class_name ApiClient
extends Node

## HTTP surface for auth / accounts / characters / game tokens.
##
## Matches the M2/M3/M4 backend contract exactly:
##   POST /auth/register                     { email, password }
##   POST /auth/login                        { email, password }
##   POST /auth/refresh                      { refreshToken }
##   GET  /accounts/{accountId}/characters
##   POST /accounts/{accountId}/characters   { name, characterClass }
##   POST /characters/{characterId}/game-token
##
## Implemented by MockApiClient (offline) and HttpApiClient (real API). The
## gameplay WebSocket never carries account/character CRUD.

signal completed(request_id: String, ok: bool, data: Variant, error: String)

var _counter := 0


func login(_email: String, _password: String, _request_id: String = "") -> String:
	push_error("ApiClient.login() must be overridden")
	return ""


func register(_email: String, _password: String, _request_id: String = "") -> String:
	push_error("ApiClient.register() must be overridden")
	return ""


func refresh(_refresh_token: String, _request_id: String = "") -> String:
	push_error("ApiClient.refresh() must be overridden")
	return ""


func list_characters(_account_id: String, _request_id: String = "") -> String:
	push_error("ApiClient.list_characters() must be overridden")
	return ""


func create_character(_account_id: String, _name: String, _character_class: String, _request_id: String = "") -> String:
	push_error("ApiClient.create_character() must be overridden")
	return ""


func request_game_token(_character_id: String, _request_id: String = "") -> String:
	push_error("ApiClient.request_game_token() must be overridden")
	return ""


func set_token(_token: String) -> void:
	pass


func new_request_id() -> String:
	_counter += 1
	return "api-%d-%d" % [Time.get_ticks_usec(), _counter]


func resolve_request_id(request_id: String) -> String:
	return request_id if request_id != "" else new_request_id()


func _complete(request_id: String, ok: bool, data: Variant, error: String) -> void:
	completed.emit(request_id, ok, data, error)
