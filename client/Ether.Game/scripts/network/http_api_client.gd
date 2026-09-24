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
const PATH_INVENTORY := "/characters/%s/inventory"

## Emits the HTTP method/path/status for diagnostics. Never carries secrets.
signal http_trace(method: String, path: String, status: int)

var base_url: String
var _http: HTTPRequest = null
var _token := ""
var _active_request_id := ""
var _active_method := ""
var _active_path := ""
var _in_flight := false
var _queue: Array = []


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


func get_inventory(character_id: String, p_request_id: String = "") -> String:
	return _request(HTTPClient.METHOD_GET, PATH_INVENTORY % character_id, {}, true, p_request_id)


func _request(method: int, path: String, body: Dictionary, authenticated: bool, p_request_id: String = "") -> String:
	var request_id := resolve_request_id(p_request_id)
	_ensure_http()
	if _http == null:
		_complete(request_id, false, null, "HTTP client not ready")
		return request_id

	# A single HTTPRequest can only process one request at a time, so requests
	# are queued and served sequentially. This prevents overlapping calls (e.g.
	# a screen refreshing while the facade also refreshes) from failing with
	# ERR_BUSY and from misrouting responses.
	_queue.append({
		"method": method,
		"path": path,
		"body": body,
		"auth": authenticated,
		"id": request_id,
	})
	_start_next()
	return request_id


func _start_next() -> void:
	if _in_flight or _queue.is_empty():
		return
	var request: Dictionary = _queue.pop_front()
	_in_flight = true
	_active_request_id = String(request["id"])
	_active_method = "GET" if int(request["method"]) == HTTPClient.METHOD_GET else "POST"
	_active_path = String(request["path"])

	var headers := PackedStringArray(["Content-Type: application/json"])
	if bool(request["auth"]) and _token != "":
		headers.append("Authorization: Bearer %s" % _token)

	var body: Dictionary = request["body"]
	var payload := "" if body.is_empty() else JSON.stringify(body)
	var err := _http.request(base_url + _active_path, headers, int(request["method"]), payload)
	if err != OK:
		var failed_id := _active_request_id
		_active_request_id = ""
		http_trace.emit(_active_method, _active_path, 0)
		_complete(failed_id, false, null, "Request failed to start (error %d)" % err)
		_in_flight = false
		_start_next()


func _on_request_completed(result: int, response_code: int, _headers: PackedStringArray, body: PackedByteArray) -> void:
	var request_id := _active_request_id
	var method := _active_method
	var path := _active_path
	_active_request_id = ""
	http_trace.emit(method, path, response_code)

	# _in_flight stays true while completing so any request issued by a handler
	# is queued (FIFO) rather than jumped ahead.
	if request_id != "":
		var text := body.get_string_from_utf8()
		if result != HTTPRequest.RESULT_SUCCESS:
			_complete(request_id, false, null, "Transport error (result %d)" % result)
		elif response_code < 200 or response_code >= 300:
			_complete(request_id, false, null, _describe_failure(response_code, text))
		elif text.strip_edges() == "":
			_complete(request_id, true, {}, "")
		else:
			var parsed: Variant = JSON.parse_string(text)
			if parsed == null:
				_complete(request_id, false, null, "The server returned an unreadable response.")
			else:
				_complete(request_id, true, parsed, "")

	_in_flight = false
	_start_next()


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
