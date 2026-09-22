extends TestCase

## MockTransport + MockBackend canonical protocol round trips.

const TOKEN := "mock-game-token"

var _backend: MockBackend
var _transport: MockTransport
var _serializer: ProtocolSerializer
var _messages: Array = []


func _setup() -> void:
	_backend = MockBackend.new()
	_transport = MockTransport.new(_backend, 0.0)
	_serializer = ProtocolSerializer.new()
	_messages = []
	_transport.message_received.connect(func(text): _messages.append(text))
	_transport.connect_to("mock://")
	_transport.poll(0.016)


func _send(name: String, payload: Dictionary, sequence: int) -> void:
	_transport.send_text(_serializer.encode_command(name, payload, sequence))
	_transport.poll(0.016)


func _last_envelope() -> Dictionary:
	if _messages.is_empty():
		return {}
	return _serializer.decode(String(_messages[_messages.size() - 1]))["envelope"]


func _last_error_code() -> String:
	var envelope := _last_envelope()
	if String(envelope.get("type", "")) != ProtocolMessages.TYPE_ERROR:
		return ""
	var payload: Dictionary = envelope.get("payload", {})
	return String(payload.get("code", ""))


func test_connect_opens_transport() -> void:
	_setup()
	assert_true(_transport.is_open(), "transport open")
	assert_eq(_messages.size(), 0, "mock sends nothing unsolicited")


func test_authenticate_gets_authenticated_event() -> void:
	_setup()
	_send(ProtocolMessages.CMD_GAME_AUTHENTICATE, {"gameToken": TOKEN}, 1)
	var envelope := _last_envelope()
	assert_eq(String(envelope["type"]), ProtocolMessages.TYPE_EVENT)
	assert_eq(String(envelope["name"]), ProtocolMessages.EVT_GAME_AUTHENTICATED)
	assert_eq(String(envelope["payload"]["accountId"]), _backend.account_id)


func test_missing_token_is_rejected() -> void:
	_setup()
	_send(ProtocolMessages.CMD_GAME_AUTHENTICATE, {}, 1)
	assert_eq(_last_error_code(), ProtocolMessages.CODE_INVALID_PAYLOAD)


func test_world_enter_before_auth_is_rejected() -> void:
	_setup()
	_send(ProtocolMessages.CMD_WORLD_ENTER, {}, 1)
	assert_eq(String(_last_envelope()["name"]), ProtocolMessages.ERR_WORLD_ENTER_REJECTED)
	assert_eq(_last_error_code(), ProtocolMessages.CODE_NOT_AUTHENTICATED)


func test_movement_before_world_is_rejected() -> void:
	_setup()
	_send(ProtocolMessages.CMD_GAME_AUTHENTICATE, {"gameToken": TOKEN}, 1)
	_send(ProtocolMessages.CMD_MOVEMENT_MOVE, {"x": 1, "y": 1}, 2)
	assert_eq(String(_last_envelope()["name"]), ProtocolMessages.ERR_MOVEMENT_REJECTED)
	assert_eq(_last_error_code(), ProtocolMessages.CODE_NOT_IN_WORLD)


func test_duplicate_sequence_is_rejected() -> void:
	_setup()
	_send(ProtocolMessages.CMD_GAME_AUTHENTICATE, {"gameToken": TOKEN}, 1)
	_send(ProtocolMessages.CMD_SYSTEM_PING, {}, 1)
	assert_eq(_last_error_code(), ProtocolMessages.CODE_INVALID_SEQUENCE)


func test_malformed_message_returns_protocol_error() -> void:
	_setup()
	_transport.send_text("{not a valid envelope")
	_transport.poll(0.016)
	assert_eq(String(_last_envelope()["type"]), ProtocolMessages.TYPE_ERROR)
	assert_eq(_last_error_code(), ProtocolMessages.CODE_INVALID_ENVELOPE)


func test_ping_gets_pong() -> void:
	_setup()
	_send(ProtocolMessages.CMD_SYSTEM_PING, {}, 1)
	assert_eq(String(_last_envelope()["name"]), ProtocolMessages.EVT_SYSTEM_PONG)


func test_movement_flow_returns_accepted() -> void:
	_setup()
	_backend.issue_game_token(String(_backend.characters[0]["characterId"]))
	_send(ProtocolMessages.CMD_GAME_AUTHENTICATE, {"gameToken": TOKEN}, 1)
	_send(ProtocolMessages.CMD_WORLD_ENTER, {}, 2)
	assert_eq(String(_last_envelope()["name"]), ProtocolMessages.EVT_WORLD_SNAPSHOT)
	_send(ProtocolMessages.CMD_MOVEMENT_MOVE, {"x": 4, "y": 6}, 3)
	var accepted := _last_envelope()
	assert_eq(String(accepted["name"]), ProtocolMessages.EVT_MOVEMENT_ACCEPTED)
	assert_eq(int(accepted["payload"]["x"]), 4)
	assert_eq(int(accepted["payload"]["y"]), 6)
