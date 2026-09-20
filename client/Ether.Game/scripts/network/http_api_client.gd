class_name HttpApiClient
extends ApiClient

## Real HTTP auth/account/character client (Blueprint v5.0 §47/§48).
##
## Endpoint paths follow the blueprint. The M1 backend currently exposes a
## different (unauthenticated) shape — see BACKEND_CONTRACT_REQUEST in the
## client README; these paths are centralized so adopting the final contract is
## a one-line change.

const PATH_LOGIN := "/auth/login"
const PATH_REGISTER := "/auth/register"
const PATH_CHARACTERS := "/characters"
const PATH_CREATE_CHARACTER := "/characters"
const PATH_SELECT_CHARACTER := "/characters/%s/select"

var base_url: String
var _http: HTTPRequest = null
var _token := ""
var _active_request_id := ""


func _init(p_base_url: String = "http://localhost:8080") -> void:
	base_url = p_base_url.trim_suffix("/")


func _ready() -> void:
	_http = HTTPRequest.new()
	_http.timeout = 10.0
	add_child(_http)
	_http.request_completed.connect(_on_request_completed)


func set_token(token: String) -> void:
	_token = token


func login(username: String, password: String, p_request_id: String = "") -> String:
	return _request(HTTPClient.METHOD_POST, PATH_LOGIN, {"username": username, "password": password}, false, p_request_id)


func register(username: String, password: String, p_request_id: String = "") -> String:
	return _request(HTTPClient.METHOD_POST, PATH_REGISTER, {"username": username, "password": password}, false, p_request_id)


func list_characters(_account_id: String, p_request_id: String = "") -> String:
	return _request(HTTPClient.METHOD_GET, PATH_CHARACTERS, {}, true, p_request_id)


func create_character(_account_id: String, name: String, character_class: String, p_request_id: String = "") -> String:
	return _request(HTTPClient.METHOD_POST, PATH_CREATE_CHARACTER, {"name": name, "characterClass": character_class}, true, p_request_id)


func select_character(character_id: String, p_request_id: String = "") -> String:
	return _request(HTTPClient.METHOD_POST, PATH_SELECT_CHARACTER % character_id, {}, true, p_request_id)


func _request(method: int, path: String, body: Dictionary, authenticated: bool, p_request_id: String = "") -> String:
	var request_id := resolve_request_id(p_request_id)
	if _http == null:
		_complete(request_id, false, {}, "HTTP client not ready")
		return request_id

	_active_request_id = request_id
	var headers := PackedStringArray(["Content-Type: application/json"])
	if authenticated and _token != "":
		headers.append("Authorization: Bearer %s" % _token)

	var payload := "" if body.is_empty() else JSON.stringify(body)
	var err := _http.request(base_url + path, headers, method, payload)
	if err != OK:
		_active_request_id = ""
		_complete(request_id, false, {}, "Request failed to start (error %d)" % err)
	return request_id


func _on_request_completed(result: int, response_code: int, _headers: PackedStringArray, body: PackedByteArray) -> void:
	var request_id := _active_request_id
	_active_request_id = ""
	if request_id == "":
		return

	var text := body.get_string_from_utf8()
	if result != HTTPRequest.RESULT_SUCCESS:
		_complete(request_id, false, {}, "Transport error (result %d)" % result)
		return
	if response_code < 200 or response_code >= 300:
		_complete(request_id, false, {}, "HTTP %d: %s" % [response_code, text])
		return

	var parsed: Variant = JSON.parse_string(text)
	var data: Dictionary = parsed if typeof(parsed) == TYPE_DICTIONARY else {}
	_complete(request_id, true, data, "")
