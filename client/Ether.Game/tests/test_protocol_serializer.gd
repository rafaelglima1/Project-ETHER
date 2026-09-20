extends TestCase

## Protocol envelope encoding/decoding and hostile-input handling.


func test_encode_decode_round_trip() -> void:
	var serializer := ProtocolSerializer.new()
	var text := serializer.encode_command(ProtocolMessages.CMD_MOVE, {"x": 10, "y": 7}, 42, "req-1")

	var result := serializer.decode(text)
	assert_true(bool(result["ok"]), "should decode")
	var envelope: Dictionary = result["envelope"]
	assert_eq(envelope["type"], ProtocolMessages.TYPE_COMMAND)
	assert_eq(envelope["name"], ProtocolMessages.CMD_MOVE)
	assert_eq(envelope["sequence"], 42)
	assert_eq(envelope["requestId"], "req-1")
	assert_eq(int(envelope["payload"]["x"]), 10)


func test_rejects_empty() -> void:
	var serializer := ProtocolSerializer.new()
	assert_false(bool(serializer.decode("")["ok"]))


func test_rejects_malformed_json() -> void:
	var serializer := ProtocolSerializer.new()
	var result := serializer.decode("{not json")
	assert_false(bool(result["ok"]))
	assert_true(String(result["error"]).length() > 0)


func test_rejects_non_object() -> void:
	var serializer := ProtocolSerializer.new()
	assert_false(bool(serializer.decode("[1, 2, 3]")["ok"]))
	assert_false(bool(serializer.decode("42")["ok"]))


func test_rejects_wrong_version() -> void:
	var serializer := ProtocolSerializer.new()
	var text := '{"version": 99, "type": "event", "name": "x", "sequence": 1, "payload": {}}'
	var result := serializer.decode(text)
	assert_false(bool(result["ok"]))


func test_rejects_unknown_type() -> void:
	var serializer := ProtocolSerializer.new()
	var text := '{"version": 1, "type": "banana", "name": "x", "sequence": 1, "payload": {}}'
	assert_false(bool(serializer.decode(text)["ok"]))


func test_rejects_missing_sequence() -> void:
	var serializer := ProtocolSerializer.new()
	var text := '{"version": 1, "type": "event", "name": "x", "payload": {}}'
	assert_false(bool(serializer.decode(text)["ok"]))


func test_rejects_non_object_payload() -> void:
	var serializer := ProtocolSerializer.new()
	var text := '{"version": 1, "type": "event", "name": "x", "sequence": 1, "payload": []}'
	assert_false(bool(serializer.decode(text)["ok"]))
