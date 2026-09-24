class_name EventDispatcher
extends RefCounted

## Turns decoded envelopes into typed client signals.
##
## This is the seam that keeps UI/world code away from raw JSON. Routing is by
## semantic `name` (canonical protocol), falling back to `type` for errors so a
## rejected command is delivered with its code, message and correlation id.

signal event_received(name: String, payload: Dictionary)
signal snapshot_received(payload: Dictionary)
signal error_received(name: String, code: String, message: String, request_id: String)


func dispatch(envelope: Dictionary) -> void:
	var type := String(envelope.get("type", ""))
	var name := String(envelope.get("name", ""))
	var payload: Variant = envelope.get("payload", {})
	var body: Dictionary = payload if typeof(payload) == TYPE_DICTIONARY else {}
	var request_id := String(envelope.get("requestId", ""))

	if type == ProtocolMessages.TYPE_ERROR:
		error_received.emit(name, _code(body), _message(body), request_id)
		return
	if type != ProtocolMessages.TYPE_EVENT:
		# A command envelope from the server is not a success event, even if its
		# name matches one. Never let an error/command become an empty snapshot.
		return

	if name == ProtocolMessages.EVT_WORLD_SNAPSHOT:
		snapshot_received.emit(body)
		return

	event_received.emit(name, body)


func _code(payload: Dictionary) -> String:
	var code := String(payload.get("code", ""))
	return code if code != "" else ProtocolMessages.CODE_INTERNAL_ERROR


func _message(payload: Dictionary) -> String:
	var message := String(payload.get("message", ""))
	return message if message != "" else "Server reported an error."
