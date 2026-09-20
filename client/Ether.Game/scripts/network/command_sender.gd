class_name CommandSender
extends RefCounted

## Serializes and sends client -> server commands with a monotonic per-session
## sequence (Blueprint v5.0 §13/§14). The server remains the validator: this
## class only stamps intent.

signal send_failed(command: String, reason: String)

var _serializer: ProtocolSerializer
var _transport: Transport
var _sequence: int = 0


func _init(serializer: ProtocolSerializer, transport: Transport = null) -> void:
	_serializer = serializer
	_transport = transport


func set_transport(transport: Transport) -> void:
	_transport = transport
	reset_sequence()


func reset_sequence() -> void:
	_sequence = 0


func current_sequence() -> int:
	return _sequence


func next_sequence() -> int:
	_sequence += 1
	return _sequence


func send(command: String, payload: Dictionary = {}) -> bool:
	if _transport == null or not _transport.is_open():
		send_failed.emit(command, "transport not open")
		return false
	var sequence := next_sequence()
	var text := _serializer.encode_command(command, payload, sequence)
	var ok := _transport.send_text(text)
	if not ok:
		send_failed.emit(command, "transport refused the message")
	return ok
