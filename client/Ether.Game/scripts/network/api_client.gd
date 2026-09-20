class_name ApiClient
extends Node

## HTTP surface for auth / accounts / characters (Blueprint v5.0 §47/§48).
##
## Implemented by MockApiClient (local) and HttpApiClient (real API). The
## gameplay WebSocket never carries account/character CRUD.

signal completed(request_id: String, ok: bool, data: Dictionary, error: String)

var _counter := 0


## Callers pass a request id so they can register the request *before* the call;
## mock implementations may complete synchronously.

func login(_username: String, _password: String, _request_id: String = "") -> String:
	push_error("ApiClient.login() must be overridden")
	return ""


func register(_username: String, _password: String, _request_id: String = "") -> String:
	push_error("ApiClient.register() must be overridden")
	return ""


func list_characters(_account_id: String, _request_id: String = "") -> String:
	push_error("ApiClient.list_characters() must be overridden")
	return ""


func create_character(_account_id: String, _name: String, _character_class: String, _request_id: String = "") -> String:
	push_error("ApiClient.create_character() must be overridden")
	return ""


func select_character(_character_id: String, _request_id: String = "") -> String:
	push_error("ApiClient.select_character() must be overridden")
	return ""


func resolve_request_id(request_id: String) -> String:
	return request_id if request_id != "" else new_request_id()


func set_token(_token: String) -> void:
	pass


func new_request_id() -> String:
	_counter += 1
	return "api-%d-%d" % [Time.get_ticks_usec(), _counter]


func _complete(request_id: String, ok: bool, data: Dictionary, error: String) -> void:
	completed.emit(request_id, ok, data, error)
