class_name CommandSender
extends RefCounted

## Serializes and sends client -> server commands with a monotonic per-session
## sequence (strictly increasing, enforced by the GameServer). Centralizes
## sequence and requestId generation so neither is ever duplicated in gameplay
## code (Blueprint v5.0 §13/§14; ADR-0003).

signal send_failed(command: String, reason: String)

var _serializer: ProtocolSerializer
var _transport: Transport
var _sequence: int = 0

## requestId of the most recent successfully sent command (for correlation).
var last_request_id := ""


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


## Sends a command, stamping the next sequence and a fresh GUID requestId.
## Returns the requestId on success, or "" on failure.
func send(command: String, payload: Dictionary = {}) -> String:
	if _transport == null or not _transport.is_open():
		send_failed.emit(command, "transport not open")
		return ""

	var request_id := _serializer.new_request_id()
	var sequence := next_sequence()
	var text := _serializer.encode_command(command, payload, sequence, request_id)

	if not _transport.send_text(text):
		send_failed.emit(command, "transport refused the message")
		return ""

	last_request_id = request_id
	return request_id
