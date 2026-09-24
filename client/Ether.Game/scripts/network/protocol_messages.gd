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
const CMD_COMBAT_ATTACK := "combat.attack"
const CMD_CHARACTER_RESPAWN := "character.respawn"
const CMD_SYSTEM_PING := "system.ping"

# --- Server -> Client events ---
const EVT_GAME_AUTHENTICATED := "game.authenticated"
const EVT_WORLD_SNAPSHOT := "world.snapshot"
const EVT_MOVEMENT_ACCEPTED := "movement.accepted"
const EVT_COMBAT_RESULT := "combat.result"
const EVT_SYSTEM_PONG := "system.pong"

# --- Server -> Client creature replication (M6, additive; frozen in ADR-0005) ---
const EVT_WORLD_CREATURE_MOVED := "world.creature_moved"

# --- Server -> Client error message names ---
const ERR_PROTOCOL := "protocol.error"
const ERR_MOVEMENT_REJECTED := "movement.rejected"
const ERR_WORLD_ENTER_REJECTED := "world.enter.rejected"
const ERR_GAME_AUTHENTICATE_REJECTED := "game.authenticate.rejected"
const ERR_COMBAT_REJECTED := "combat.rejected"
const ERR_CHARACTER_RESPAWN_REJECTED := "character.respawn.rejected"

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

# --- Combat error codes (ADR-0004) ---
const CODE_ABILITY_NOT_FOUND := "ABILITY_NOT_FOUND"
const CODE_TARGET_NOT_FOUND := "TARGET_NOT_FOUND"
const CODE_TARGET_DEAD := "TARGET_DEAD"
const CODE_ATTACKER_DEAD := "ATTACKER_DEAD"
const CODE_OUT_OF_RANGE := "OUT_OF_RANGE"
const CODE_COOLDOWN_ACTIVE := "COOLDOWN_ACTIVE"
const CODE_SELF_TARGET := "SELF_TARGET"

# --- Death / respawn error codes ---
const CODE_CHARACTER_DEAD := "CHARACTER_DEAD"
const CODE_CHARACTER_NOT_DEAD := "CHARACTER_NOT_DEAD"

# --- Content keys (mirror the M5 catalog until the content pipeline lands) ---
const ABILITY_BASIC_ATTACK := "warrior.basic_attack"
const ABILITY_POWER_STRIKE := "warrior.power_strike"

const TARGET_TYPE_CHARACTER := "character"
const TARGET_TYPE_CREATURE := "creature"

# --- Creature state names (mirror Ether.Domain.Creatures.CreatureState) ---
const CREATURE_STATE_IDLE := "Idle"
const CREATURE_STATE_PATROL := "Patrol"
const CREATURE_STATE_INVESTIGATE := "Investigate"
const CREATURE_STATE_CHASE := "Chase"
const CREATURE_STATE_ATTACK := "Attack"
const CREATURE_STATE_FLEE := "Flee"
const CREATURE_STATE_RETURN := "Return"
const CREATURE_STATE_DEAD := "Dead"
const CREATURE_STATE_RESPAWNING := "Respawning"

# --- M7 additive combat.result fields (progression + loot) ---
# Additive to the frozen M5/M6 combat.result; the server writes the progression
# integers as 0 and omits loot (null) when no reward is granted, so the client
# only applies progression when level > 0 and loot when the array is present.
const FIELD_EXPERIENCE_GAINED := "experienceGained"
const FIELD_LEVEL := "level"
const FIELD_EXPERIENCE := "experience"
const FIELD_LEVELS_GAINED := "levelsGained"
const FIELD_LOOT := "loot"
const FIELD_ITEM_DEFINITION_ID := "itemDefinitionId"
const FIELD_ITEM_INSTANCE_ID := "itemInstanceId"
const FIELD_ITEM_NAME := "name"
const FIELD_ITEM_QUANTITY := "quantity"
