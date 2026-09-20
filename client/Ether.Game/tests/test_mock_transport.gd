extends TestCase

## MockTransport + MockBackend round trips and error handling.


func test_connect_emits_opened_and_connected() -> void:
	var backend := MockBackend.new()
	var transport := MockTransport.new(backend, 0.0)
	var opened := [false]
	var messages: Array = []
	transport.opened.connect(func(): opened[0] = true)
	transport.message_received.connect(func(text): messages.append(text))

	transport.connect_to("mock://")
	transport.poll(0.016)

	assert_true(opened[0], "opened signal")
	assert_true(transport.is_open(), "transport open")
	assert_true(messages.size() >= 1, "connected event delivered")
	var envelope := _last_envelope(messages)
	assert_eq(String(envelope["name"]), ProtocolMessages.EVT_CONNECTED)


func test_command_receives_response() -> void:
	var backend := MockBackend.new()
	var transport := MockTransport.new(backend, 0.0)
	var serializer := ProtocolSerializer.new()
	var messages: Array = []
	transport.message_received.connect(func(text): messages.append(text))

	transport.connect_to("mock://")
	transport.poll(0.016)
	transport.send_text(serializer.encode_command(ProtocolMessages.CMD_AUTHENTICATE, {"gameToken": "t"}, 1))
	transport.poll(0.016)

	assert_true(_has_event(messages, ProtocolMessages.EVT_AUTHENTICATED), "authenticated event")


func test_malformed_command_returns_error_envelope() -> void:
	var backend := MockBackend.new()
	var transport := MockTransport.new(backend, 0.0)
	var serializer := ProtocolSerializer.new()
	var messages: Array = []
	transport.message_received.connect(func(text): messages.append(text))

	transport.connect_to("mock://")
	transport.poll(0.016)
	transport.send_text("{not a valid envelope")
	transport.poll(0.016)

	var envelope := _last_envelope(messages)
	assert_eq(String(envelope["type"]), ProtocolMessages.TYPE_ERROR)


func test_move_before_world_is_rejected() -> void:
	var backend := MockBackend.new()
	var transport := MockTransport.new(backend, 0.0)
	var serializer := ProtocolSerializer.new()
	var messages: Array = []
	transport.message_received.connect(func(text): messages.append(text))

	transport.connect_to("mock://")
	transport.poll(0.016)
	transport.send_text(serializer.encode_command(ProtocolMessages.CMD_MOVE, {"x": 3, "y": 3}, 2))
	transport.poll(0.016)

	var envelope := _last_envelope(messages)
	assert_eq(String(envelope["type"]), ProtocolMessages.TYPE_ERROR)


func _last_envelope(messages: Array) -> Dictionary:
	assert_true(messages.size() > 0, "expected at least one message")
	if messages.is_empty():
		return {}
	var serializer := ProtocolSerializer.new()
	var result := serializer.decode(String(messages[messages.size() - 1]))
	return result["envelope"]


func _has_event(messages: Array, event_name: String) -> bool:
	var serializer := ProtocolSerializer.new()
	for text in messages:
		var result := serializer.decode(String(text))
		if not bool(result["ok"]):
			continue
		var envelope: Dictionary = result["envelope"]
		if String(envelope.get("name", "")) == event_name:
			return true
	return false
