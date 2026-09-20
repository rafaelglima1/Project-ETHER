class_name EventDispatcher
extends RefCounted

## Turns decoded envelopes into typed client signals.
##
## This is the seam that keeps UI/world code away from raw JSON: everything
## downstream subscribes to these signals, never to the wire format.

signal event_received(name: String, payload: Dictionary)
signal snapshot_received(payload: Dictionary)
signal delta_received(payload: Dictionary)
signal error_received(code: String, message: String)


func dispatch(envelope: Dictionary) -> void:
	var type := String(envelope.get("type", ""))
	var name := String(envelope.get("name", ""))
	var payload: Variant = envelope.get("payload", {})
	var body: Dictionary = payload if typeof(payload) == TYPE_DICTIONARY else {}

	if type == ProtocolMessages.TYPE_SNAPSHOT:
		snapshot_received.emit(body)
	elif type == ProtocolMessages.TYPE_DELTA:
		delta_received.emit(body)
	elif type == ProtocolMessages.TYPE_ERROR:
		error_received.emit(_error_code(body), _error_message(body))
	elif type == ProtocolMessages.TYPE_EVENT:
		_dispatch_event(name, body)
	# TYPE_COMMAND is never expected from the server; ignore it quietly.


func _dispatch_event(name: String, payload: Dictionary) -> void:
	if name == ProtocolMessages.EVT_WORLD_SNAPSHOT:
		snapshot_received.emit(payload)
	elif name == ProtocolMessages.EVT_WORLD_DELTA:
		delta_received.emit(payload)
	else:
		event_received.emit(name, payload)


func _error_code(payload: Dictionary) -> String:
	var code := String(payload.get("code", ""))
	if code == "":
		code = String(payload.get("errorCode", ProtocolMessages.ERR_INTERNAL_ERROR))
	return code


func _error_message(payload: Dictionary) -> String:
	var message := String(payload.get("message", ""))
	if message == "":
		message = String(payload.get("detail", "Server reported an error."))
	return message
