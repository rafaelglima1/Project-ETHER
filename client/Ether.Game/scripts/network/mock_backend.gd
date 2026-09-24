class_name MockBackend
extends RefCounted

## In-process fake GameServer used by MockTransport.
##
## Server-role code standing in for the authoritative backend. It implements the
## canonical M4 + M5 + M6 realtime contract so the client flow is identical in
## mock and real modes:
##   commands: game.authenticate, world.enter, movement.move, combat.attack, system.ping
##   events:   game.authenticated, world.snapshot, movement.accepted, combat.result, system.pong
##   creature: world.creature_moved (position + health + state; also respawn)
##   errors:   protocol.error, *.rejected (movement/world.enter/game.authenticate/combat)
##
## Damage, criticals, HP, death and creature AI are computed *here* precisely
## because the real client must never do so.

const MAP_WIDTH := 32
const MAP_HEIGHT := 32
const MAP_ID := 1
const MAX_MOVE_DISTANCE := 12
const START_X := 0
const START_Y := 0

const STATE_CONNECTING := 0
const STATE_AUTHENTICATED := 1
const STATE_IN_WORLD := 2

# Player provisional stats (server-role; mirrors LevelBasedCombatStatsProvider).
const PLAYER_MAX_HEALTH := 100
const PLAYER_ATTACK_POWER := 6.0
const CRITICAL_CHANCE := 0.1
const CRITICAL_MULTIPLIER := 1.5

# Abilities (mirror AbilityCatalog).
const ABILITY_BASIC := "warrior.basic_attack"
const ABILITY_POWER := "warrior.power_strike"

# Creature definitions (mirror CreatureCatalog).
const CREATURE_TEMPLATES := [
	{"definitionId": "creature.slime", "name": "Slime", "level": 1, "maxHealth": 30, "attackPower": 5.0, "armor": 1.0, "moveSpeed": 1, "aggroRange": 6, "attackRange": 1, "attackCooldown": 2.0, "leashRange": 10, "respawnDelay": 30.0, "xpReward": 12},
	{"definitionId": "creature.wolf", "name": "Wolf", "level": 2, "maxHealth": 45, "attackPower": 8.0, "armor": 2.0, "moveSpeed": 2, "aggroRange": 8, "attackRange": 1, "attackCooldown": 1.5, "leashRange": 14, "respawnDelay": 45.0, "xpReward": 25},
	{"definitionId": "creature.spider", "name": "Spider", "level": 2, "maxHealth": 35, "attackPower": 7.0, "armor": 1.0, "moveSpeed": 2, "aggroRange": 7, "attackRange": 2, "attackCooldown": 2.0, "leashRange": 12, "respawnDelay": 40.0, "xpReward": 18},
]

const SPAWNS := [
	{"template": 0, "x": 4, "y": 0},
	{"template": 1, "x": 9, "y": 0},
	{"template": 2, "x": 0, "y": 4},
]

var account_id := "00000000-0000-4000-8000-0000000000a1"
var account_email := "hero@example.com"
var session_id := ""
var selected_character_id := ""
var characters: Array = []
## Authoritative (server-role) inventory mirror for the mock.
var inventory: Array = []
var ai_enabled := true
var criticals_enabled := true
var loot_guaranteed := false

var _state: int = STATE_CONNECTING
var _outbound_sequence := 0
var _last_inbound_sequence := 0
var _clock := 0.0

var _player_x := START_X
var _player_y := START_Y
var _player_hp := PLAYER_MAX_HEALTH
var _player_level := 1
var _player_xp := 0
var _player_dead := false
var _ability_ready_at := {}
var _creatures: Dictionary = {}
var _rng := RandomNumberGenerator.new()


func _init() -> void:
	_rng.randomize()
	session_id = _new_guid()
	_seed_characters()


## Rebuilds session-scoped state for a (re)connection. Persisted character
## state — position, HP, level, XP and dead — deliberately survives, mirroring
## the database: that is what makes reconnect-while-dead recoverable.
func reset() -> void:
	_state = STATE_CONNECTING
	_outbound_sequence = 0
	_last_inbound_sequence = 0
	_ability_ready_at = {}
	_creatures = {}
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


func set_ai_enabled(enabled: bool) -> void:
	ai_enabled = enabled


func set_critical_enabled(enabled: bool) -> void:
	criticals_enabled = enabled


func set_loot_guaranteed(enabled: bool) -> void:
	loot_guaranteed = enabled


## Test hook: force the mock server-side player into the dead state. Respawn is
## command-driven (character.respawn), exactly like the frozen backend contract.
func force_player_dead() -> void:
	_player_dead = true
	_player_hp = 0


# --- Transport-facing API -----------------------------------------------------

func on_connect() -> Array:
	reset()
	return []


func on_disconnect() -> void:
	_state = STATE_CONNECTING
	_creatures = {}


func tick(delta: float) -> Array:
	_clock += delta
	if _state != STATE_IN_WORLD:
		return []

	var out: Array = []
	if ai_enabled:
		out.append_array(_run_creature_ai())
	return out


## Server-role respawn: authoritative HP/position restored, reported to the
## client through a canonical world.snapshot (mirrors RespawnCommandHandler).
func _respawn_player() -> void:
	_player_dead = false
	_player_hp = PLAYER_MAX_HEALTH
	_player_x = START_X
	_player_y = START_Y


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
	elif name == ProtocolMessages.CMD_COMBAT_ATTACK:
		return _handle_attack(payload, request_id, sequence)
	elif name == ProtocolMessages.CMD_CHARACTER_RESPAWN:
		return _handle_respawn(request_id, sequence)
	elif name == ProtocolMessages.CMD_SYSTEM_PING:
		return [_event(ProtocolMessages.EVT_SYSTEM_PONG, {"serverTime": _now()}, request_id, sequence)]

	return [_error(ProtocolMessages.ERR_PROTOCOL, ProtocolMessages.CODE_UNKNOWN_MESSAGE, "Unknown message '%s'." % name, request_id, sequence)]


# --- Session commands ---------------------------------------------------------

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
	# Mirrors the backend fix (0131215): a dead character must be told to
	# respawn (CHARACTER_DEAD), not to ALREADY_IN_WORLD.
	if _player_dead:
		return [_error(ProtocolMessages.ERR_WORLD_ENTER_REJECTED, ProtocolMessages.CODE_CHARACTER_DEAD, "Character is dead; respawn first.", request_id, sequence)]
	if _state == STATE_IN_WORLD:
		return [_error(ProtocolMessages.ERR_WORLD_ENTER_REJECTED, ProtocolMessages.CODE_ALREADY_IN_WORLD, "Character is already in the world.", request_id, sequence)]

	# Character state (position / HP / level / XP) is persisted server-side and
	# echoed back here, exactly like EnterWorldHandler; only the session moves.
	_state = STATE_IN_WORLD
	_ensure_creatures_spawned()
	return [_event(ProtocolMessages.EVT_WORLD_SNAPSHOT, _snapshot_payload(), request_id, sequence)]


## Mirrors the frozen backend RespawnCommandHandler: replies with world.snapshot
## on success, character.respawn.rejected otherwise.
func _handle_respawn(request_id: String, sequence: int) -> Array:
	if _state == STATE_CONNECTING:
		return [_error(ProtocolMessages.ERR_CHARACTER_RESPAWN_REJECTED, ProtocolMessages.CODE_NOT_AUTHENTICATED, "Session is not authenticated.", request_id, sequence)]
	if not _player_dead:
		return [_error(ProtocolMessages.ERR_CHARACTER_RESPAWN_REJECTED, ProtocolMessages.CODE_CHARACTER_NOT_DEAD, "Character is not dead.", request_id, sequence)]

	_respawn_player()
	_state = STATE_IN_WORLD
	# WorldSnapshotFactory builds map + player + living creatures for respawn too.
	_ensure_creatures_spawned()
	return [_event(ProtocolMessages.EVT_WORLD_SNAPSHOT, _snapshot_payload(), request_id, sequence)]


## WorldSnapshotFactory.EnsureSpawned equivalent: spawn once per connection,
## keep existing living creatures (idempotent).
func _ensure_creatures_spawned() -> void:
	if _creatures.is_empty():
		_spawn_creatures()


func _handle_move(payload: Dictionary, request_id: String, sequence: int) -> Array:
	if _state != STATE_IN_WORLD:
		return [_error(ProtocolMessages.ERR_MOVEMENT_REJECTED, ProtocolMessages.CODE_NOT_IN_WORLD, "Character is not in the world.", request_id, sequence)]
	if _player_dead:
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


# --- Combat (M5/M6) -----------------------------------------------------------

func _handle_attack(payload: Dictionary, request_id: String, sequence: int) -> Array:
	if _state != STATE_IN_WORLD:
		return [_error(ProtocolMessages.ERR_COMBAT_REJECTED, ProtocolMessages.CODE_NOT_IN_WORLD, "Character is not in the world.", request_id, sequence)]
	if _player_dead:
		return [_error(ProtocolMessages.ERR_COMBAT_REJECTED, ProtocolMessages.CODE_ATTACKER_DEAD, "Attacker is dead.", request_id, sequence)]

	var ability_id := String(payload.get("abilityId", ""))
	var target_id := String(payload.get("targetId", ""))

	var ability := _ability(ability_id)
	if ability.is_empty():
		return [_error(ProtocolMessages.ERR_COMBAT_REJECTED, ProtocolMessages.CODE_ABILITY_NOT_FOUND, "Ability does not exist.", request_id, sequence)]
	if target_id == "" or not _creatures.has(target_id):
		return [_error(ProtocolMessages.ERR_COMBAT_REJECTED, ProtocolMessages.CODE_TARGET_NOT_FOUND, "Target does not exist.", request_id, sequence)]

	var creature: Dictionary = _creatures[target_id]
	if not _creature_alive(creature):
		return [_error(ProtocolMessages.ERR_COMBAT_REJECTED, ProtocolMessages.CODE_TARGET_DEAD, "Target is dead.", request_id, sequence)]

	var distance := maxi(absi(int(creature["x"]) - _player_x), absi(int(creature["y"]) - _player_y))
	if distance > int(ability["range"]):
		return [_error(ProtocolMessages.ERR_COMBAT_REJECTED, ProtocolMessages.CODE_OUT_OF_RANGE, "Target is out of range.", request_id, sequence)]

	if _clock < float(_ability_ready_at.get(ability_id, 0.0)):
		return [_error(ProtocolMessages.ERR_COMBAT_REJECTED, ProtocolMessages.CODE_COOLDOWN_ACTIVE, "Ability is on cooldown.", request_id, sequence)]

	_ability_ready_at[ability_id] = _clock + float(ability["cooldown"])

	var hit := _resolve_damage(float(ability["base"]) + PLAYER_ATTACK_POWER * float(ability["scaling"]), float(creature["armor"]))
	creature["hp"] = maxi(0, int(creature["hp"]) - int(hit["damage"]))
	var defeated := int(creature["hp"]) <= 0
	var xp_gained := 0
	var loot: Array = []
	if defeated:
		creature["state"] = ProtocolMessages.CREATURE_STATE_DEAD
		creature["targetId"] = ""
		creature["respawnAt"] = _clock + float(creature["respawnDelay"])
		xp_gained = int(creature["xpReward"])
		_player_xp += xp_gained
		loot = _roll_loot(creature)

	var result_payload := {
		"attackerId": selected_character_id,
		"targetId": target_id,
		"abilityId": ability_id,
		"rawDamage": int(hit["raw"]),
		"damage": int(hit["damage"]),
		"critical": bool(hit["critical"]),
		"targetHealth": int(creature["hp"]),
		"targetMaxHealth": int(creature["maxHp"]),
		"targetState": String(creature["state"]),
		"targetDefeated": defeated,
		"attackerType": ProtocolMessages.TARGET_TYPE_CHARACTER,
		"targetType": ProtocolMessages.TARGET_TYPE_CREATURE,
		# M7 additive progression fields (0 when no reward is granted).
		"experienceGained": xp_gained,
		"level": _player_level if defeated else 0,
		"experience": _player_xp if defeated else 0,
		"levelsGained": 0,
	}
	if not loot.is_empty():
		result_payload["loot"] = loot
		for entry in loot:
			_inventory_add(entry)

	var out: Array = [_event(ProtocolMessages.EVT_COMBAT_RESULT, result_payload, request_id, sequence)]
	return out


func _roll_loot(creature: Dictionary) -> Array:
	if loot_guaranteed:
		return [_loot_entry(creature)]
	var loot: Array = []
	if _rng.randf() < 0.75:
		loot.append(_loot_entry(creature))
	if _rng.randf() < 0.2:
		loot.append({
			"itemDefinitionId": "item.coin_pouch",
			"name": "Coin Pouch",
			"quantity": 1,
			"itemInstanceId": _new_guid(),
		})
	return loot


func _loot_entry(creature: Dictionary) -> Dictionary:
	var is_slime := String(creature["definitionId"]) == "creature.slime"
	return {
		"itemDefinitionId": "item.slime_gel" if is_slime else "item.beast_part",
		"name": "Slime Gel" if is_slime else "Beast Part",
		"quantity": 1,
		"itemInstanceId": _new_guid(),
	}


func _ability(ability_id: String) -> Dictionary:
	if ability_id == ABILITY_BASIC:
		return {"range": 1, "cooldown": 0.0, "base": 4.0, "scaling": 1.0}
	if ability_id == ABILITY_POWER:
		return {"range": 1, "cooldown": 6.0, "base": 8.0, "scaling": 1.5}
	return {}


func _resolve_damage(raw: float, armor: float) -> Dictionary:
	var armor_multiplier := 100.0 / (100.0 + maxf(0.0, armor))
	var final := maxf(1.0, raw * armor_multiplier)
	var critical := false
	if criticals_enabled and _rng.randf() < CRITICAL_CHANCE:
		critical = true
		final *= CRITICAL_MULTIPLIER
	return {"raw": int(round(raw)), "damage": int(round(final)), "critical": critical}


# --- Creature AI --------------------------------------------------------------

func _run_creature_ai() -> Array:
	var out: Array = []
	for id in _creatures.keys():
		var creature: Dictionary = _creatures[id]

		if not _creature_alive(creature):
			if creature.get("respawnAt", null) != null and _clock >= float(creature["respawnAt"]):
				_respawn_creature(creature)
				out.append(_creature_moved_event(creature))
			continue

		var before_x := int(creature["x"])
		var before_y := int(creature["y"])
		var before_state := String(creature["state"])

		var distance_to_player := maxi(absi(_player_x - int(creature["x"])), absi(_player_y - int(creature["y"])))
		var attack_range := int(creature["attackRange"])

		if _player_dead or distance_to_player > int(creature["aggroRange"]):
			var home := maxi(absi(int(creature["spawnX"]) - int(creature["x"])), absi(int(creature["spawnY"]) - int(creature["y"])))
			if home > 0:
				creature["state"] = ProtocolMessages.CREATURE_STATE_RETURN
				_move_creature_toward(creature, int(creature["spawnX"]), int(creature["spawnY"]))
		elif distance_to_player <= attack_range:
			creature["state"] = ProtocolMessages.CREATURE_STATE_ATTACK
			if _clock >= float(creature.get("attackReadyAt", 0.0)):
				creature["attackReadyAt"] = _clock + float(creature["attackCooldown"])
				_creature_attacks_player(creature, out)
		else:
			creature["state"] = ProtocolMessages.CREATURE_STATE_CHASE
			_move_creature_toward(creature, _player_x, _player_y)

		if int(creature["x"]) != before_x or int(creature["y"]) != before_y or String(creature["state"]) != before_state:
			out.append(_creature_moved_event(creature))

	return out


func _move_creature_toward(creature: Dictionary, target_x: int, target_y: int) -> void:
	if _clock < float(creature.get("nextMoveAt", 0.0)):
		return
	creature["nextMoveAt"] = _clock + (1.0 / maxf(1.0, float(creature["moveSpeed"])))

	var x := int(creature["x"])
	var y := int(creature["y"])
	var nx := x + signi(target_x - x)
	var ny := y + signi(target_y - y)
	if nx < 0 or ny < 0 or nx >= MAP_WIDTH or ny >= MAP_HEIGHT:
		return
	if nx == x and ny == y:
		return

	creature["x"] = nx
	creature["y"] = ny


func _creature_attacks_player(creature: Dictionary, out: Array) -> void:
	var hit := _resolve_damage(float(creature["attackPower"]), 0.0)
	_player_hp = maxi(0, _player_hp - int(hit["damage"]))
	if _player_hp <= 0:
		_player_dead = true

	out.append(_event(ProtocolMessages.EVT_COMBAT_RESULT, {
		"attackerId": creature["id"],
		"targetId": selected_character_id,
		"abilityId": "%s.attack" % creature["definitionId"],
		"rawDamage": int(hit["raw"]),
		"damage": int(hit["damage"]),
		"critical": bool(hit["critical"]),
		"targetHealth": _player_hp,
		"targetMaxHealth": PLAYER_MAX_HEALTH,
		"targetState": "Dead" if _player_dead else "Combat",
		"targetDefeated": _player_dead,
		"attackerType": ProtocolMessages.TARGET_TYPE_CREATURE,
		"targetType": ProtocolMessages.TARGET_TYPE_CHARACTER,
	}, "", 0))


func _spawn_creatures() -> void:
	_creatures = {}
	for spawn in SPAWNS:
		var template: Dictionary = CREATURE_TEMPLATES[int(spawn["template"])]
		var creature := {
			"id": _new_guid(),
			"definitionId": template["definitionId"],
			"name": template["name"],
			"level": template["level"],
			"x": int(spawn["x"]),
			"y": int(spawn["y"]),
			"spawnX": int(spawn["x"]),
			"spawnY": int(spawn["y"]),
			"hp": template["maxHealth"],
			"maxHp": template["maxHealth"],
			"state": ProtocolMessages.CREATURE_STATE_IDLE,
			"attackPower": template["attackPower"],
			"armor": template["armor"],
			"moveSpeed": template["moveSpeed"],
			"aggroRange": template["aggroRange"],
			"attackRange": template["attackRange"],
			"attackCooldown": template["attackCooldown"],
			"respawnDelay": template["respawnDelay"],
			"leashRange": template["leashRange"],
			"xpReward": template["xpReward"],
			"attackReadyAt": 0.0,
			"nextMoveAt": 0.0,
			"respawnAt": null,
		}
		_creatures[creature["id"]] = creature


func _respawn_creature(creature: Dictionary) -> void:
	creature["hp"] = int(creature["maxHp"])
	creature["x"] = int(creature["spawnX"])
	creature["y"] = int(creature["spawnY"])
	creature["state"] = ProtocolMessages.CREATURE_STATE_IDLE
	creature["respawnAt"] = null
	creature["attackReadyAt"] = 0.0
	creature["nextMoveAt"] = 0.0


func _creature_alive(creature: Dictionary) -> bool:
	var state := String(creature.get("state", ""))
	return int(creature.get("hp", 0)) > 0 and state != ProtocolMessages.CREATURE_STATE_DEAD and state != ProtocolMessages.CREATURE_STATE_RESPAWNING


func _creature_moved_event(creature: Dictionary) -> Dictionary:
	return _event(ProtocolMessages.EVT_WORLD_CREATURE_MOVED, {
		"creatureId": creature["id"],
		"mapId": MAP_ID,
		"x": creature["x"],
		"y": creature["y"],
		"health": creature["hp"],
		"maxHealth": creature["maxHp"],
		"state": creature["state"],
	}, "", 0)


func _creature_view(creature: Dictionary) -> Dictionary:
	return {
		"creatureId": creature["id"],
		"definitionId": creature["definitionId"],
		"name": creature["name"],
		"level": creature["level"],
		"x": creature["x"],
		"y": creature["y"],
		"health": creature["hp"],
		"maxHealth": creature["maxHp"],
		"state": creature["state"],
	}


func _snapshot_payload() -> Dictionary:
	var creatures: Array = []
	for id in _creatures.keys():
		creatures.append(_creature_view(_creatures[id]))
	return {
		"mapId": MAP_ID,
		"width": MAP_WIDTH,
		"height": MAP_HEIGHT,
		"player": {
			"characterId": selected_character_id,
			"x": _player_x,
			"y": _player_y,
			"state": "Dead" if _player_dead else "InWorld",
			"level": _player_level,
			"experience": _player_xp,
			"experienceToNextLevel": 0,
			"health": _player_hp,
			"maxHealth": PLAYER_MAX_HEALTH,
		},
		"creatures": creatures,
		"inventory": inventory.duplicate(true),
		"serverTime": _now(),
	}


## Server-role inventory: stacks by item definition (InventoryService parity).
func _inventory_add(entry: Dictionary) -> void:
	var definition_id := String(entry.get("itemDefinitionId", entry.get("name", "item")))
	var quantity := int(entry.get("quantity", 1))
	for item in inventory:
		if String(item.get("itemDefinitionId", "")) == definition_id:
			item["quantity"] = int(item.get("quantity", 0)) + quantity
			return
	inventory.append({
		"itemDefinitionId": definition_id,
		"name": String(entry.get("name", "Item")),
		"quantity": quantity,
		"maxStack": 99,
		"stackable": true,
		"location": "inventory",
	})


## GET /characters/{id}/inventory equivalent (InventoryResponse shape).
func api_get_inventory() -> Dictionary:
	return {
		"characterId": selected_character_id,
		"items": inventory.duplicate(true),
	}


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
