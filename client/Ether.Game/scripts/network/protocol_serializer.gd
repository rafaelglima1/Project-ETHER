class_name ProtocolSerializer
extends RefCounted

## Encodes/decodes the gameplay envelope (Blueprint v5.0 §35).
##
## Envelope:
## {
##   "version": 1,
##   "type": "command|event|snapshot|delta|error",
##   "name": "Move",
##   "requestId": "uuid",
##   "sequence": 123,
##   "payload": {}
## }
##
## decode() never raises: it returns a result dictionary so malformed input
## (which a hostile or buggy server can always send) is handled as data.

const VERSION := ProtocolMessages.VERSION


func encode(
		envelope_type: String,
		name: String,
		payload: Dictionary,
		sequence: int,
		request_id: String = ""
) -> String:
	var envelope := {
		"version": VERSION,
		"type": envelope_type,
		"name": name,
		"requestId": request_id if request_id != "" else new_request_id(),
		"sequence": sequence,
		"payload": payload,
	}
	return JSON.stringify(envelope)


func encode_command(command: String, payload: Dictionary, sequence: int, request_id: String = "") -> String:
	return encode(ProtocolMessages.TYPE_COMMAND, command, payload, sequence, request_id)


## Returns { ok: bool, error: String, envelope: Dictionary }.
func decode(text: String) -> Dictionary:
	if text == null or text.strip_edges() == "":
		return _fail("Empty message")

	var parsed: Variant = JSON.parse_string(text)
	if parsed == null:
		return _fail("Malformed JSON")
	if typeof(parsed) != TYPE_DICTIONARY:
		return _fail("Envelope must be a JSON object")

	var envelope: Dictionary = parsed

	var version: Variant = envelope.get("version", null)
	if typeof(version) != TYPE_INT and typeof(version) != TYPE_FLOAT:
		return _fail("Missing or invalid 'version'")
	if int(version) != VERSION:
		return _fail("Unsupported protocol version: %s" % str(version))

	var type := String(envelope.get("type", ""))
	if not ProtocolMessages.KNOWN_TYPES.has(type):
		return _fail("Unknown envelope type: '%s'" % type)

	var sequence: Variant = envelope.get("sequence", null)
	if typeof(sequence) != TYPE_INT and typeof(sequence) != TYPE_FLOAT:
		return _fail("Missing or invalid 'sequence'")

	var payload: Variant = envelope.get("payload", {})
	if typeof(payload) != TYPE_DICTIONARY:
		return _fail("'payload' must be an object")

	envelope["version"] = int(version)
	envelope["type"] = type
	envelope["name"] = String(envelope.get("name", ""))
	envelope["requestId"] = String(envelope.get("requestId", ""))
	envelope["sequence"] = int(sequence)
	envelope["payload"] = payload

	return {"ok": true, "error": "", "envelope": envelope}


func new_request_id() -> String:
	return "%d-%d" % [Time.get_ticks_usec(), randi()]


func _fail(reason: String) -> Dictionary:
	return {"ok": false, "error": reason, "envelope": {}}
