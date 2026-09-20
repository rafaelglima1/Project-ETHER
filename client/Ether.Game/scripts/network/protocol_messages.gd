class_name ProtocolMessages
extends RefCounted

## Canonical message names and envelope types.
##
## Two sources in Blueprint v5.0 describe the wire format:
##   §13/§15 show a command with a top-level `type` carrying the command name.
##   §35 defines the envelope `type` as the *category* (command/event/snapshot/
##   delta/error).
##
## The client treats §35 as authoritative (later section wins) and additionally
## carries a `name` field for the concrete command/event. This is a documented
## client-side interpretation — see BACKEND_CONTRACT_REQUEST in the client
## README. Processors tolerate both shapes so the real server can decide.

const VERSION := 1

# Envelope categories (Blueprint v5.0 §35)
const TYPE_COMMAND := "command"
const TYPE_EVENT := "event"
const TYPE_SNAPSHOT := "snapshot"
const TYPE_DELTA := "delta"
const TYPE_ERROR := "error"

const KNOWN_TYPES := [TYPE_COMMAND, TYPE_EVENT, TYPE_SNAPSHOT, TYPE_DELTA, TYPE_ERROR]

# Client -> Server commands (Blueprint v5.0 §15)
const CMD_AUTHENTICATE := "Authenticate"
const CMD_CREATE_CHARACTER := "CreateCharacter"
const CMD_SELECT_CHARACTER := "SelectCharacter"
const CMD_ENTER_WORLD := "EnterWorld"
const CMD_MOVE := "Move"
const CMD_ATTACK := "Attack"
const CMD_CAST_ABILITY := "CastAbility"
const CMD_INTERACT := "Interact"
const CMD_PICKUP_ITEM := "PickupItem"
const CMD_PING := "Ping"

# Server -> Client events (Blueprint v5.0 §36 + client contract list)
const EVT_CONNECTED := "Connected"
const EVT_AUTHENTICATED := "Authenticated"
const EVT_CHARACTER_LIST := "CharacterList"
const EVT_CHARACTER_SELECTED := "CharacterSelected"
const EVT_WORLD_ENTERED := "WorldEntered"
const EVT_CHARACTER_MOVED := "CharacterMoved"
const EVT_CREATURE_SPAWNED := "CreatureSpawned"
const EVT_CREATURE_MOVED := "CreatureMoved"
const EVT_ENTITY_SPAWN := "EntitySpawn"
const EVT_ENTITY_UPDATE := "EntityUpdate"
const EVT_ENTITY_DESPAWN := "EntityDespawn"
const EVT_COMBAT_STARTED := "CombatStarted"
const EVT_DAMAGE_APPLIED := "DamageApplied"
const EVT_COMBAT_RESULT := "CombatResult"
const EVT_CREATURE_DIED := "CreatureDied"
const EVT_ENTITY_DEATH := "EntityDeath"
const EVT_LOOT_DROPPED := "LootDropped"
const EVT_LOOT_RECEIVED := "LootReceived"
const EVT_ITEM_PICKED_UP := "ItemPickedUp"
const EVT_EXPERIENCE_GAINED := "ExperienceGained"
const EVT_LEVEL_UP := "LevelUp"
const EVT_CHARACTER_DIED := "CharacterDied"
const EVT_CHARACTER_RESPAWNED := "CharacterRespawned"
const EVT_WORLD_SNAPSHOT := "WorldSnapshot"
const EVT_WORLD_DELTA := "WorldDelta"
const EVT_MOVEMENT_ACCEPTED := "MovementAccepted"
const EVT_PONG := "Pong"
const EVT_ERROR := "Error"

# Error codes kept client-side for display only (Blueprint v5.0 §37).
const ERR_UNAUTHORIZED := "Unauthorized"
const ERR_INVALID_COMMAND := "InvalidCommand"
const ERR_INVALID_SEQUENCE := "InvalidSequence"
const ERR_INVALID_POSITION := "InvalidPosition"
const ERR_TILE_BLOCKED := "TileBlocked"
const ERR_OUT_OF_RANGE := "OutOfRange"
const ERR_COOLDOWN_ACTIVE := "CooldownActive"
const ERR_INVALID_TARGET := "InvalidTarget"
const ERR_INTERNAL_ERROR := "InternalError"
