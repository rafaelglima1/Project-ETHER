class_name HttpApiClient
extends ApiClient

## Real HTTP auth/account/character/game-token client.
##
## Endpoints and payloads follow the backend contract exactly. The account id is
## taken from the login response; the game token is issued per selected character.

const PATH_LOGIN := "/auth/login"
const PATH_REGISTER := "/auth/register"
const PATH_REFRESH := "/auth/refresh"
const PATH_ACCOUNT_CHARACTERS := "/accounts/%s/characters"
const PATH_GAME_TOKEN := "/characters/%s/game-token"

var base_url: String
var _http: HTTPRequest = null
var _token := ""
var _active_request_id := ""


func _init(p_base_url: String = "http://localhost:8080") -> void:
	base_url = p_base_url.trim_suffix("/")


func _ready() -> void:
	_ensure_http()


## Creates the underlying HTTPRequest lazily. In some entry points (e.g. a
## headless SceneTree script) _ready may not have run before the first request.
func _ensure_http() -> void:
	if _http != null:
		return
	_http = HTTPRequest.new()
	_http.timeout = 15.0
	add_child(_http)
	_http.request_completed.connect(_on_request_completed)


func set_token(token: String) -> void:
	_token = token


func login(email: String, password: String, p_request_id: String = "") -> String:
	return _request(HTTPClient.METHOD_POST, PATH_LOGIN, {"email": email, "password": password}, false, p_request_id)


func register(email: String, password: String, p_request_id: String = "") -> String:
	return _request(HTTPClient.METHOD_POST, PATH_REGISTER, {"email": email, "password": password}, false, p_request_id)


func refresh(refresh_token: String, p_request_id: String = "") -> String:
	return _request(HTTPClient.METHOD_POST, PATH_REFRESH, {"refreshToken": refresh_token}, false, p_request_id)


func list_characters(account_id: String, p_request_id: String = "") -> String:
	return _request(HTTPClient.METHOD_GET, PATH_ACCOUNT_CHARACTERS % account_id, {}, true, p_request_id)


func create_character(account_id: String, name: String, character_class: String, p_request_id: String = "") -> String:
	return _request(
		HTTPClient.METHOD_POST,
		PATH_ACCOUNT_CHARACTERS % account_id,
		{"name": name, "characterClass": character_class},
		true,
		p_request_id)


func request_game_token(character_id: String, p_request_id: String = "") -> String:
	return _request(HTTPClient.METHOD_POST, PATH_GAME_TOKEN % character_id, {}, true, p_request_id)


func _request(method: int, path: String, body: Dictionary, authenticated: bool, p_request_id: String = "") -> String:
	var request_id := resolve_request_id(p_request_id)
	_ensure_http()
	if _http == null:
		_complete(request_id, false, null, "HTTP client not ready")
		return request_id

	_active_request_id = request_id
	var headers := PackedStringArray(["Content-Type: application/json"])
	if authenticated and _token != "":
		headers.append("Authorization: Bearer %s" % _token)

	var payload := "" if body.is_empty() else JSON.stringify(body)
	var err := _http.request(base_url + path, headers, method, payload)
	if err != OK:
		_active_request_id = ""
		_complete(request_id, false, null, "Request failed to start (error %d)" % err)
	return request_id


func _on_request_completed(result: int, response_code: int, _headers: PackedStringArray, body: PackedByteArray) -> void:
	var request_id := _active_request_id
	_active_request_id = ""
	if request_id == "":
		return

	var text := body.get_string_from_utf8()
	if result != HTTPRequest.RESULT_SUCCESS:
		_complete(request_id, false, null, "Transport error (result %d)" % result)
		return
	if response_code < 200 or response_code >= 300:
		_complete(request_id, false, null, _describe_failure(response_code, text))
		return

	if text.strip_edges() == "":
		_complete(request_id, true, {}, "")
		return

	var parsed: Variant = JSON.parse_string(text)
	if parsed == null:
		_complete(request_id, false, null, "The server returned an unreadable response.")
		return
	_complete(request_id, true, parsed, "")


func _describe_failure(response_code: int, text: String) -> String:
	if response_code == 401:
		return "Invalid credentials or session expired."
	if response_code == 403:
		return "You are not allowed to do that."
	if response_code == 404:
		return "Not found."
	if response_code == 409:
		return "That already exists."
	if response_code == 400:
		return "The request was rejected."
	return "Request failed (HTTP %d)." % response_code
