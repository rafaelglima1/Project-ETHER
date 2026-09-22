class_name MockBackend
extends RefCounted

## In-process fake GameServer used by MockTransport.
##
## IMPORTANT: this is *server-role* code standing in for the authoritative
## backend. It implements the canonical M4 realtime contract exactly
## (ADR-0003) so the client flow is identical in mock and real modes:
##   commands: game.authenticate, world.enter, movement.move, system.ping
##   events:   game.authenticated, world.snapshot, movement.accepted, system.pong
##   errors:   protocol.error, movement.rejected, world.enter.rejected,
##             game.authenticate.rejected
##
## It keeps just enough account/character state for offline play; it is not a
## business-rule authority (no combat, XP, loot or inventory in M4).

const MAP_WIDTH := 32
const MAP_HEIGHT := 32
const MAP_ID := 1
const MAX_MOVE_DISTANCE := 12
const START_X := 0
const START_Y := 0

const STATE_CONNECTING := 0
const STATE_AUTHENTICATED := 1
const STATE_IN_WORLD := 2

var account_id := "00000000-0000-4000-8000-0000000000a1"
var account_email := "hero@example.com"
var session_id := ""
var selected_character_id := ""

var characters: Array = []

var _state: int = STATE_CONNECTING
var _outbound_sequence := 0
var _last_inbound_sequence := 0
var _player_x := START_X
var _player_y := START_Y
var _rng := RandomNumberGenerator.new()


func _init() -> void:
	_rng.randomize()
	session_id = _new_guid()
	_seed_characters()


func reset() -> void:
	_state = STATE_CONNECTING
	_outbound_sequence = 0
	_last_inbound_sequence = 0
	_player_x = START_X
	_player_y = START_Y
	session_id = _new_guid()


func authenticate_account(email: String) -> void:
	var trimmed := email.strip_edges()
	if trimmed != "":
		account_email = trimmed


func display_name() -> String:
	var at := account_email.find("@")
	return account_email.substr(0, at) if at > 0 else account_email


func issue_game_token(character_id: String) -> void:
	selected_character_id = character_id


# --- Transport-facing API -----------------------------------------------------

func on_connect() -> Array:
	reset()
	return []


func on_disconnect() -> void:
	_state = STATE_CONNECTING
	_player_x = START_X
	_player_y = START_Y


func tick(_delta: float) -> Array:
	return []


## Accepts a raw command string and returns an Array of envelope dictionaries.
func handle_text(text: String) -> Array:
	var parsed: Variant = JSON.parse_string(text)
	if typeof(parsed) != TYPE_DICTIONARY:
		return [_error(ProtocolMessages.ERR_PROTOCOL, ProtocolMessages.CODE_INVALID_ENVELOPE, "Malformed envelope.", "", 0)]

	var envelope: Dictionary = parsed
	var name := String(envelope.get("name", ""))
	var request_id := String(envelope.get("requestId", "")) if envelope.get("requestId", null) != null else ""
	var sequence := int(envelope.get("sequence", 0))
	var raw_payload: Variant = envelope.get("payload", null)
	var payload: Dictionary = raw_payload if typeof(raw_payload) == TYPE_DICTIONARY else {}

	if int(envelope.get("version", 0)) != ProtocolMessages.VERSION:
		return [_error(ProtocolMessages.ERR_PROTOCOL, ProtocolMessages.CODE_UNSUPPORTED_VERSION, "Unsupported protocol version.", request_id, sequence)]

	if sequence <= _last_inbound_sequence:
		return [_error(name, ProtocolMessages.CODE_INVALID_SEQUENCE, "Sequence is duplicate or stale.", request_id, sequence)]
	_last_inbound_sequence = sequence

	if name == ProtocolMessages.CMD_GAME_AUTHENTICATE:
		return _handle_authenticate(payload, request_id, sequence)
	elif name == ProtocolMessages.CMD_WORLD_ENTER:
		return _handle_enter_world(request_id, sequence)
	elif name == ProtocolMessages.CMD_MOVEMENT_MOVE:
		return _handle_move(payload, request_id, sequence)
	elif name == ProtocolMessages.CMD_SYSTEM_PING:
		return [_event(ProtocolMessages.EVT_SYSTEM_PONG, {"serverTime": _now()}, request_id, sequence)]

	return [_error(ProtocolMessages.ERR_PROTOCOL, ProtocolMessages.CODE_UNKNOWN_MESSAGE, "Unknown message '%s'." % name, request_id, sequence)]


# --- Command handlers ---------------------------------------------------------

func _handle_authenticate(payload: Dictionary, request_id: String, sequence: int) -> Array:
	if _state != STATE_CONNECTING:
		return [_error(ProtocolMessages.ERR_GAME_AUTHENTICATE_REJECTED, ProtocolMessages.CODE_ALREADY_AUTHENTICATED, "Session is already authenticated.", request_id, sequence)]

	var token := String(payload.get("gameToken", ""))
	if token.strip_edges() == "":
		return [_error(ProtocolMessages.ERR_GAME_AUTHENTICATE_REJECTED, ProtocolMessages.CODE_INVALID_PAYLOAD, "gameToken is required.", request_id, sequence)]

	var character_id := selected_character_id
	if character_id == "" and not characters.is_empty():
		character_id = String(characters[0].get("characterId", ""))

	_state = STATE_AUTHENTICATED
	return [_event(ProtocolMessages.EVT_GAME_AUTHENTICATED, {
		"sessionId": session_id,
		"accountId": account_id,
		"characterId": character_id,
	}, request_id, sequence)]


func _handle_enter_world(request_id: String, sequence: int) -> Array:
	if _state == STATE_CONNECTING:
		return [_error(ProtocolMessages.ERR_WORLD_ENTER_REJECTED, ProtocolMessages.CODE_NOT_AUTHENTICATED, "Session is not authenticated.", request_id, sequence)]
	if _state == STATE_IN_WORLD:
		return [_error(ProtocolMessages.ERR_WORLD_ENTER_REJECTED, ProtocolMessages.CODE_ALREADY_IN_WORLD, "Character is already in the world.", request_id, sequence)]

	_state = STATE_IN_WORLD
	_player_x = START_X
	_player_y = START_Y
	return [_event(ProtocolMessages.EVT_WORLD_SNAPSHOT, {
		"mapId": MAP_ID,
		"width": MAP_WIDTH,
		"height": MAP_HEIGHT,
		"player": {
			"characterId": selected_character_id,
			"x": _player_x,
			"y": _player_y,
			"state": "InWorld",
		},
		"serverTime": _now(),
	}, request_id, sequence)]


func _handle_move(payload: Dictionary, request_id: String, sequence: int) -> Array:
	if _state != STATE_IN_WORLD:
		return [_error(ProtocolMessages.ERR_MOVEMENT_REJECTED, ProtocolMessages.CODE_NOT_IN_WORLD, "Character is not in the world.", request_id, sequence)]

	if not payload.has("x") or not payload.has("y"):
		return [_error(ProtocolMessages.ERR_MOVEMENT_REJECTED, ProtocolMessages.CODE_INVALID_PAYLOAD, "Movement payload is invalid.", request_id, sequence)]

	var x := int(payload["x"])
	var y := int(payload["y"])

	if x < 0 or y < 0 or x >= MAP_WIDTH or y >= MAP_HEIGHT:
		return [_error(ProtocolMessages.ERR_MOVEMENT_REJECTED, ProtocolMessages.CODE_OUT_OF_BOUNDS, "Destination is outside the map.", request_id, sequence)]

	var distance := maxi(absi(x - _player_x), absi(y - _player_y))
	if distance > MAX_MOVE_DISTANCE:
		return [_error(ProtocolMessages.ERR_MOVEMENT_REJECTED, ProtocolMessages.CODE_TOO_FAR, "Destination is too far.", request_id, sequence)]

	_player_x = x
	_player_y = y
	return [_event(ProtocolMessages.EVT_MOVEMENT_ACCEPTED, {
		"characterId": selected_character_id,
		"mapId": MAP_ID,
		"x": x,
		"y": y,
	}, request_id, sequence)]


# --- Account / character helpers (offline) ------------------------------------

func api_create_character(name: String, character_class: String) -> Dictionary:
	var trimmed := name.strip_edges()
	if trimmed.length() < 2 or trimmed.length() > 24:
		return {"ok": false, "error": "Character name must be 2-24 characters."}
	for existing in characters:
		if String(existing.get("name", "")).to_lower() == trimmed.to_lower():
			return {"ok": false, "error": "That character name is already taken."}

	var character := {
		"characterId": _new_guid(),
		"accountId": account_id,
		"name": trimmed,
		"characterClass": character_class,
		"state": "Offline",
		"level": 1,
		"experience": 0,
		"mapId": MAP_ID,
		"positionX": START_X,
		"positionY": START_Y,
	}
	characters.append(character)
	return {"ok": true, "error": "", "character": character}


func _seed_characters() -> void:
	characters = [{
		"characterId": _new_guid(),
		"accountId": account_id,
		"name": "Aria",
		"characterClass": "Warrior",
		"state": "Offline",
		"level": 1,
		"experience": 0,
		"mapId": MAP_ID,
		"positionX": START_X,
		"positionY": START_Y,
	}]


# --- Envelope builders --------------------------------------------------------

func _event(name: String, payload: Dictionary, request_id: String, _sequence: int) -> Dictionary:
	return _envelope(ProtocolMessages.TYPE_EVENT, name, payload, request_id)


func _error(name: String, code: String, message: String, request_id: String, _sequence: int) -> Dictionary:
	return _envelope(ProtocolMessages.TYPE_ERROR, name, {"code": code, "message": message}, request_id)


func _envelope(type: String, name: String, payload: Dictionary, request_id: String) -> Dictionary:
	_outbound_sequence += 1
	return {
		"version": ProtocolMessages.VERSION,
		"type": type,
		"name": name,
		"requestId": request_id if request_id != "" else null,
		"sequence": _outbound_sequence,
		"payload": payload,
	}


func _new_guid() -> String:
	var bytes := PackedByteArray()
	bytes.resize(16)
	for i in range(16):
		bytes[i] = _rng.randi() & 0xFF
	bytes[6] = (bytes[6] & 0x0F) | 0x40
	bytes[8] = (bytes[8] & 0x3F) | 0x80
	var hex := ""
	for i in range(16):
		hex += "%02x" % bytes[i]
	return "%s-%s-%s-%s-%s" % [hex.substr(0, 8), hex.substr(8, 4), hex.substr(12, 4), hex.substr(16, 4), hex.substr(20, 12)]


func _now() -> String:
	return Time.get_datetime_string_from_system(true) + "Z"
