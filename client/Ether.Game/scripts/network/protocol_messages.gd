class_name ProtocolMessages
extends RefCounted

## Canonical realtime protocol (ADR-0003) — mirrors the backend contract exactly.
##
## Envelope:
##   { version, type, name, requestId, sequence, payload }
## where type is one of command | event | error, and name is the semantic
## message (e.g. "movement.move"). JSON uses camelCase.

const VERSION := 1

const TYPE_COMMAND := "command"
const TYPE_EVENT := "event"
const TYPE_ERROR := "error"

const KNOWN_TYPES := [TYPE_COMMAND, TYPE_EVENT, TYPE_ERROR]

# --- Client -> Server commands ---
const CMD_GAME_AUTHENTICATE := "game.authenticate"
const CMD_WORLD_ENTER := "world.enter"
const CMD_MOVEMENT_MOVE := "movement.move"
const CMD_SYSTEM_PING := "system.ping"

# --- Server -> Client events ---
const EVT_GAME_AUTHENTICATED := "game.authenticated"
const EVT_WORLD_SNAPSHOT := "world.snapshot"
const EVT_MOVEMENT_ACCEPTED := "movement.accepted"
const EVT_SYSTEM_PONG := "system.pong"

# --- Server -> Client error message names ---
const ERR_PROTOCOL := "protocol.error"
const ERR_MOVEMENT_REJECTED := "movement.rejected"
const ERR_WORLD_ENTER_REJECTED := "world.enter.rejected"
const ERR_GAME_AUTHENTICATE_REJECTED := "game.authenticate.rejected"

# --- Deterministic error codes (ProtocolErrorCodes on the server) ---
const CODE_INVALID_ENVELOPE := "INVALID_ENVELOPE"
const CODE_UNSUPPORTED_VERSION := "UNSUPPORTED_VERSION"
const CODE_UNKNOWN_MESSAGE := "UNKNOWN_MESSAGE"
const CODE_INVALID_PAYLOAD := "INVALID_PAYLOAD"
const CODE_MESSAGE_TOO_LARGE := "MESSAGE_TOO_LARGE"
const CODE_NOT_AUTHENTICATED := "NOT_AUTHENTICATED"
const CODE_ALREADY_AUTHENTICATED := "ALREADY_AUTHENTICATED"
const CODE_INVALID_TOKEN := "INVALID_TOKEN"
const CODE_TOKEN_EXPIRED := "TOKEN_EXPIRED"
const CODE_WRONG_TOKEN_PURPOSE := "WRONG_TOKEN_PURPOSE"
const CODE_NOT_AUTHORIZED := "NOT_AUTHORIZED"
const CODE_NOT_IN_WORLD := "NOT_IN_WORLD"
const CODE_ALREADY_IN_WORLD := "ALREADY_IN_WORLD"
const CODE_INVALID_MAP := "INVALID_MAP"
const CODE_OUT_OF_BOUNDS := "OUT_OF_BOUNDS"
const CODE_TOO_FAR := "TOO_FAR"
const CODE_INVALID_STATE := "INVALID_STATE"
const CODE_INVALID_SEQUENCE := "INVALID_SEQUENCE"
const CODE_RATE_LIMITED := "RATE_LIMITED"
const CODE_SERVER_BUSY := "SERVER_BUSY"
const CODE_INTERNAL_ERROR := "INTERNAL_ERROR"
