class_name ProtocolSerializer
extends RefCounted

## Encodes/decodes the canonical realtime envelope (ADR-0003):
##
##   { version, type, name, requestId, sequence, payload }
##
## The client only *encodes commands*; the server only *sends events/errors*.
## decode() never raises: it returns a result dictionary so malformed input
## (which a hostile or buggy peer can always send) is handled as data.

const VERSION := ProtocolMessages.VERSION

## Upper bound mirroring the server's MaxMessageSizeBytes default (16 KiB).
const MAX_MESSAGE_BYTES := 16 * 1024


func encode_command(command: String, payload: Dictionary, sequence: int, request_id: String = "") -> String:
	var envelope := {
		"version": VERSION,
		"type": ProtocolMessages.TYPE_COMMAND,
		"name": command,
		"requestId": request_id if request_id != "" else new_request_id(),
		"sequence": sequence,
		"payload": payload,
	}
	return JSON.stringify(envelope)


## Returns { ok: bool, error: String, envelope: Dictionary }.
func decode(text: String) -> Dictionary:
	if text == null or text.strip_edges() == "":
		return _fail("Empty message")

	if text.to_utf8_buffer().size() > MAX_MESSAGE_BYTES:
		return _fail("Message exceeds the maximum allowed size")

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

	var name := String(envelope.get("name", ""))
	if name.strip_edges() == "":
		return _fail("Missing 'name'")

	var sequence: Variant = envelope.get("sequence", null)
	if typeof(sequence) != TYPE_INT and typeof(sequence) != TYPE_FLOAT:
		return _fail("Missing or invalid 'sequence'")

	var raw_payload: Variant = envelope.get("payload", null)
	if raw_payload != null and typeof(raw_payload) != TYPE_DICTIONARY:
		return _fail("'payload' must be an object when present")

	envelope["version"] = int(version)
	envelope["type"] = type
	envelope["name"] = name
	envelope["requestId"] = String(envelope.get("requestId", "")) if envelope.get("requestId", null) != null else ""
	envelope["sequence"] = int(sequence)
	envelope["payload"] = raw_payload if typeof(raw_payload) == TYPE_DICTIONARY else {}

	return {"ok": true, "error": "", "envelope": envelope}


## Generates a RFC-4122 v4-shaped GUID string. The server deserializes requestId
## as `Guid?`, so a non-GUID value would invalidate the whole envelope.
func new_request_id() -> String:
	var bytes := PackedByteArray()
	bytes.resize(16)
	for i in range(16):
		bytes[i] = randi() & 0xFF
	bytes[6] = (bytes[6] & 0x0F) | 0x40
	bytes[8] = (bytes[8] & 0x3F) | 0x80

	var hex := ""
	for i in range(16):
		hex += "%02x" % bytes[i]

	return "%s-%s-%s-%s-%s" % [
		hex.substr(0, 8),
		hex.substr(8, 4),
		hex.substr(12, 4),
		hex.substr(16, 4),
		hex.substr(20, 12),
	]


func _fail(reason: String) -> Dictionary:
	return {"ok": false, "error": reason, "envelope": {}}
