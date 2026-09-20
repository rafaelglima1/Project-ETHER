class_name MockBackend
extends RefCounted

## In-process fake GameServer used by MockTransport.
##
## IMPORTANT: this is *server-role* code standing in for the authoritative
## backend. It computes damage, death, XP and loot precisely because the real
## client must never do so (Blueprint v5.0 §89). The client-side code that
## consumes it stays 100% server-authoritative: swap this backend for the real
## WebSocket GameServer and nothing above the transport layer changes.
##
## Numbers here are throwaway mock values, not canonical game rules.

const MAP_WIDTH := 24
const MAP_HEIGHT := 16
const TILE_SIZE := 32
const MAX_MOVE_DISTANCE := 8
const PLAYER_SPAWN := Vector2i(5, 5)

const CREATURE_TEMPLATES := [
	{"name": "Slime", "hp": 35, "xp": 12, "attack": 3},
	{"name": "Rat", "hp": 24, "xp": 8, "attack": 2},
	{"name": "Boar", "hp": 48, "xp": 18, "attack": 5},
]

var session_id := ""
var server_sequence := 0
var connected := false
var authenticated := false
var account_id := "mock-account-0001"
var display_name := "Adventurer"
var in_world := false

var characters: Array = []
var selected_character_id := ""
var player: Dictionary = {}
var creatures: Dictionary = {}
var inventory: Array = []

var _id_counter := 0
var _creature_timer := 0.0
var _creature_interval := 0.9
var _respawn_timer := 0.0
var _pending_respawn := false
var _rng := RandomNumberGenerator.new()


func _init() -> void:
	_rng.randomize()
	session_id = _new_id("session")
	_seed_characters()


func reset() -> void:
	server_sequence = 0
	connected = false
	authenticated = false
	in_world = false
	selected_character_id = ""
	player = {}
	creatures = {}
	inventory = []
	characters = []
	_pending_respawn = false
	_respawn_timer = 0.0
	_seed_characters()


# --- Transport-facing API -----------------------------------------------------

func on_connect() -> Array:
	connected = true
	return [_event(ProtocolMessages.EVT_CONNECTED, {"sessionId": session_id})]


func on_disconnect() -> void:
	connected = false
	in_world = false


## Accepts a raw command string and returns an Array of envelope dictionaries.
func handle_text(text: String) -> Array:
	var parsed: Variant = JSON.parse_string(text)
	if typeof(parsed) != TYPE_DICTIONARY:
		return [_error(ProtocolMessages.ERR_INVALID_COMMAND, "Malformed command envelope.")]

	var envelope: Dictionary = parsed
	var command := String(envelope.get("name", ""))
	if command == "":
		command = String(envelope.get("type", ""))

	var raw_payload: Variant = envelope.get("payload", {})
	var payload: Dictionary = raw_payload if typeof(raw_payload) == TYPE_DICTIONARY else {}

	if command == ProtocolMessages.CMD_AUTHENTICATE:
		return _handle_authenticate(payload)
	elif command == ProtocolMessages.CMD_CREATE_CHARACTER:
		return _handle_create_character(payload)
	elif command == ProtocolMessages.CMD_SELECT_CHARACTER:
		return _handle_select_character(payload)
	elif command == ProtocolMessages.CMD_ENTER_WORLD:
		return _handle_enter_world(payload)
	elif command == ProtocolMessages.CMD_MOVE:
		return _handle_move(payload)
	elif command == ProtocolMessages.CMD_ATTACK or command == ProtocolMessages.CMD_CAST_ABILITY:
		return _handle_attack(payload)
	elif command == ProtocolMessages.CMD_INTERACT:
		return _handle_interact(payload)
	elif command == ProtocolMessages.CMD_PICKUP_ITEM:
		return _handle_pickup(payload)
	elif command == ProtocolMessages.CMD_PING:
		return [_event(ProtocolMessages.EVT_PONG, {})]

	return [_error(ProtocolMessages.ERR_INVALID_COMMAND, "Unknown command: '%s'." % command)]


## Advances the simulated world and returns any resulting envelopes.
func tick(delta: float) -> Array:
	if not in_world:
		return []

	var out: Array = []

	if _pending_respawn:
		_respawn_timer -= delta
		if _respawn_timer <= 0.0:
			_pending_respawn = false
			_respawn_player(out)
		return out

	_wander_creatures(delta, out)
	_creatures_attack_player(delta, out)
	return out


# --- Command handlers ---------------------------------------------------------

func _handle_authenticate(_payload: Dictionary) -> Array:
	authenticated = true
	return [_event(ProtocolMessages.EVT_AUTHENTICATED, {
		"accountId": account_id,
		"sessionId": session_id,
		"displayName": display_name,
		"gameToken": "mock-game-token",
	})]


func _handle_create_character(payload: Dictionary) -> Array:
	authenticated = true
	var name := String(payload.get("name", "")).strip_edges()
	var character_class := String(payload.get("characterClass", "Warrior"))
	if name.length() < 2 or name.length() > 24:
		return [_error("InvalidCommand", "Character name must be 2-24 characters.")]
	for existing in characters:
		if String(existing.get("name", "")).to_lower() == name.to_lower():
			return [_error("InvalidCommand", "That character name is already taken.")]

	characters.append({
		"characterId": _new_id("char"),
		"accountId": account_id,
		"name": name,
		"characterClass": character_class,
		"level": 1,
		"experience": 0,
		"state": "Offline",
	})
	return [_event(ProtocolMessages.EVT_CHARACTER_LIST, {"characters": characters})]


## HTTP-style helper mirroring the character-creation command.
func api_create_character(name: String, character_class: String) -> Dictionary:
	var envelopes := _handle_create_character({"name": name, "characterClass": character_class})
	var first: Dictionary = envelopes[0] if not envelopes.is_empty() else {}
	if String(first.get("type", "")) == ProtocolMessages.TYPE_ERROR:
		var payload: Dictionary = first.get("payload", {})
		return {"ok": false, "error": String(payload.get("message", "Unable to create character."))}
	return {"ok": true, "error": "", "characters": characters.duplicate(true)}


func _handle_select_character(payload: Dictionary) -> Array:
	authenticated = true
	var character_id := String(payload.get("characterId", ""))
	var character := _find_character(character_id)
	if character.is_empty():
		return [_error("CharacterNotFound", "Character not found.")]
	selected_character_id = character_id
	return [_event(ProtocolMessages.EVT_CHARACTER_SELECTED, {"character": character})]


func _handle_enter_world(_payload: Dictionary) -> Array:
	if selected_character_id == "":
		return [_error("CharacterNotFound", "No character selected.")]

	var character := _find_character(selected_character_id)
	if character.is_empty():
		return [_error("CharacterNotFound", "Character not found.")]

	_enter_world(character)
	var events: Array = [_event(ProtocolMessages.EVT_WORLD_ENTERED, {
		"characterId": selected_character_id,
	})]
	events.append(_snapshot(_build_snapshot()))
	return events


func _handle_move(payload: Dictionary) -> Array:
	if not in_world:
		return [_error("CharacterNotInWorld", "Character is not in the world.")]

	var target := Vector2i(int(payload.get("x", 0)), int(payload.get("y", 0)))
	var origin := Vector2i(int(player.get("x", 0)), int(player.get("y", 0)))

	if not _is_walkable(target.x, target.y):
		return [_error(ProtocolMessages.ERR_TILE_BLOCKED, "That tile is blocked.")]
	if _distance(origin, target) > MAX_MOVE_DISTANCE:
		return [_error(ProtocolMessages.ERR_OUT_OF_RANGE, "That tile is too far away.")]

	player["x"] = target.x
	player["y"] = target.y
	return [_event(ProtocolMessages.EVT_CHARACTER_MOVED, {
		"entityId": selected_character_id,
		"x": target.x,
		"y": target.y,
	})]


func _handle_attack(payload: Dictionary) -> Array:
	if not in_world:
		return [_error("CharacterNotInWorld", "Character is not in the world.")]
	if bool(player.get("dead", false)):
		return [_error(ProtocolMessages.ERR_UNAUTHORIZED, "You are dead.")]

	var target_id := String(payload.get("targetId", ""))
	if not creatures.has(target_id):
		return [_error(ProtocolMessages.ERR_INVALID_TARGET, "Target not found.")]

	var creature: Dictionary = creatures[target_id]
	var cpos := Vector2i(int(creature.get("x", 0)), int(creature.get("y", 0)))
	var ppos := Vector2i(int(player.get("x", 0)), int(player.get("y", 0)))
	if _distance(ppos, cpos) > 1:
		return [_error(ProtocolMessages.ERR_OUT_OF_RANGE, "Target is out of range.")]

	var out: Array = []
	var damage: int = _rng.randi_range(6, 12)
	creature["hp"] = maxi(0, int(creature.get("hp", 0)) - damage)
	out.append(_event(ProtocolMessages.EVT_COMBAT_RESULT, {
		"attackerId": selected_character_id,
		"targetId": target_id,
		"damage": damage,
		"targetHp": creature["hp"],
		"targetMaxHp": creature.get("maxHp", 0),
	}))
	out.append(_event(ProtocolMessages.EVT_DAMAGE_APPLIED, {
		"targetId": target_id,
		"amount": damage,
		"remainingHp": creature["hp"],
	}))

	if int(creature["hp"]) <= 0:
		_kill_creature(target_id, creature, out)

	return out


func _handle_interact(payload: Dictionary) -> Array:
	var target_id := String(payload.get("targetId", ""))
	var name := "something"
	if creatures.has(target_id):
		name = String(creatures[target_id].get("name", "creature"))
	return [_event("Interaction", {
		"targetId": target_id,
		"message": "You study the %s." % name,
	})]


func _handle_pickup(payload: Dictionary) -> Array:
	var item_id := String(payload.get("itemId", ""))
	var item_name := String(payload.get("name", "Item"))
	_add_to_inventory(item_id, item_name, 1)
	return [
		_event(ProtocolMessages.EVT_ITEM_PICKED_UP, {"itemId": item_id}),
		_event("InventoryUpdated", {"inventory": inventory}),
	]


# --- World simulation ---------------------------------------------------------

func _enter_world(character: Dictionary) -> void:
	in_world = true
	player = {
		"id": selected_character_id,
		"kind": "player",
		"name": character.get("name", "Hero"),
		"characterClass": character.get("characterClass", "Warrior"),
		"x": PLAYER_SPAWN.x,
		"y": PLAYER_SPAWN.y,
		"hp": 100,
		"maxHp": 100,
		"level": int(character.get("level", 1)),
		"experience": int(character.get("experience", 0)),
		"experienceToNext": xp_to_next(int(character.get("level", 1))),
		"gold": 0,
		"dead": false,
	}
	inventory = []
	creatures = {}
	_spawn_creature(9, 5)
	_spawn_creature(14, 8)
	_spawn_creature(18, 11)


func _spawn_creature(x: int, y: int) -> void:
	var template: Dictionary = CREATURE_TEMPLATES[_rng.randi_range(0, CREATURE_TEMPLATES.size() - 1)]
	var id := _new_id("creature")
	creatures[id] = {
		"id": id,
		"kind": "creature",
		"name": template["name"],
		"x": x,
		"y": y,
		"hp": template["hp"],
		"maxHp": template["hp"],
		"level": 1,
		"xpReward": template["xp"],
		"attack": template["attack"],
	}


func _kill_creature(target_id: String, creature: Dictionary, out: Array) -> void:
	creatures.erase(target_id)
	out.append(_event(ProtocolMessages.EVT_ENTITY_DEATH, {"entityId": target_id}))
	out.append(_event(ProtocolMessages.EVT_CREATURE_DIED, {"entityId": target_id}))

	var reward := int(creature.get("xpReward", 0))
	player["experience"] = int(player.get("experience", 0)) + reward
	out.append(_event(ProtocolMessages.EVT_EXPERIENCE_GAINED, {
		"amount": reward,
		"total": player["experience"],
		"level": player.get("level", 1),
	}))

	var next := xp_to_next(int(player.get("level", 1)))
	player["experienceToNext"] = next
	while int(player["experience"]) >= next:
		player["level"] = int(player.get("level", 1)) + 1
		player["maxHp"] = int(player.get("maxHp", 100)) + 20
		player["hp"] = player["maxHp"]
		out.append(_event(ProtocolMessages.EVT_LEVEL_UP, {
			"level": player["level"],
			"experienceToNext": xp_to_next(int(player["level"])),
		}))
		next = xp_to_next(int(player["level"]))
		player["experienceToNext"] = next

	var loot := _roll_loot(creature)
	if not loot.is_empty():
		out.append(_event(ProtocolMessages.EVT_LOOT_RECEIVED, {"items": loot}))
		for item in loot:
			_add_to_inventory(String(item.get("itemId", "")), String(item.get("name", "Item")), int(item.get("quantity", 1)))
		out.append(_event("InventoryUpdated", {"inventory": inventory}))


func _roll_loot(_creature: Dictionary) -> Array:
	var loot: Array = []
	if _rng.randf() < 0.7:
		loot.append({"itemId": "item.slime_gel", "name": "Slime Gel", "quantity": 1})
	if _rng.randf() < 0.25:
		loot.append({"itemId": "item.coin_pouch", "name": "Coin Pouch", "quantity": 1})
	return loot


func _wander_creatures(delta: float, out: Array) -> void:
	_creature_timer += delta
	if _creature_timer < _creature_interval:
		return
	_creature_timer = 0.0

	for id in creatures.keys():
		var creature: Dictionary = creatures[id]
		if _rng.randf() > 0.5:
			continue
		var dx := _rng.randi_range(-1, 1)
		var dy := _rng.randi_range(-1, 1)
		var nx := int(creature.get("x", 0)) + dx
		var ny := int(creature.get("y", 0)) + dy
		if not _is_walkable(nx, ny):
			continue
		creature["x"] = nx
		creature["y"] = ny
		out.append(_event(ProtocolMessages.EVT_CREATURE_MOVED, {"entityId": id, "x": nx, "y": ny}))


func _creatures_attack_player(delta: float, out: Array) -> void:
	if bool(player.get("dead", false)):
		return
	for id in creatures.keys():
		var creature: Dictionary = creatures[id]
		var cpos := Vector2i(int(creature.get("x", 0)), int(creature.get("y", 0)))
		var ppos := Vector2i(int(player.get("x", 0)), int(player.get("y", 0)))
		if _distance(cpos, ppos) > 1:
			continue
		var cooldown := float(creature.get("attackCooldown", 0.0)) - delta
		if cooldown > 0.0:
			creature["attackCooldown"] = cooldown
			continue
		creature["attackCooldown"] = 2.0
		var damage := int(creature.get("attack", 1)) + _rng.randi_range(0, 2)
		player["hp"] = maxi(0, int(player.get("hp", 0)) - damage)
		out.append(_event(ProtocolMessages.EVT_COMBAT_RESULT, {
			"attackerId": id,
			"targetId": selected_character_id,
			"damage": damage,
			"targetHp": player["hp"],
			"targetMaxHp": player.get("maxHp", 100),
		}))
		if int(player["hp"]) <= 0:
			_player_died(out)
		return


func _player_died(out: Array) -> void:
	player["dead"] = true
	player["hp"] = 0
	_pending_respawn = true
	_respawn_timer = 3.0
	out.append(_event(ProtocolMessages.EVT_CHARACTER_DIED, {"characterId": selected_character_id}))


func _respawn_player(out: Array) -> void:
	player["dead"] = false
	player["hp"] = int(player.get("maxHp", 100))
	player["x"] = PLAYER_SPAWN.x
	player["y"] = PLAYER_SPAWN.y
	out.append(_event(ProtocolMessages.EVT_CHARACTER_RESPAWNED, {
		"characterId": selected_character_id,
		"x": PLAYER_SPAWN.x,
		"y": PLAYER_SPAWN.y,
		"hp": player["hp"],
	}))


# --- Payload builders ---------------------------------------------------------

func _build_snapshot() -> Dictionary:
	var entities: Array = []
	for id in creatures.keys():
		entities.append(_creature_view(creatures[id]))

	return {
		"map": {
			"id": 1,
			"name": "Town",
			"width": MAP_WIDTH,
			"height": MAP_HEIGHT,
			"tileSize": TILE_SIZE,
		},
		"player": _player_view(),
		"entities": entities,
	}


func _player_view() -> Dictionary:
	return {
		"id": selected_character_id,
		"kind": "player",
		"name": player.get("name", "Hero"),
		"characterClass": player.get("characterClass", "Warrior"),
		"x": player.get("x", 0),
		"y": player.get("y", 0),
		"hp": player.get("hp", 0),
		"maxHp": player.get("maxHp", 0),
		"level": player.get("level", 1),
		"experience": player.get("experience", 0),
		"experienceToNext": player.get("experienceToNext", 0),
		"gold": player.get("gold", 0),
		"inventory": inventory,
	}


func _creature_view(creature: Dictionary) -> Dictionary:
	return {
		"id": creature.get("id", ""),
		"kind": "creature",
		"name": creature.get("name", ""),
		"x": creature.get("x", 0),
		"y": creature.get("y", 0),
		"hp": creature.get("hp", 0),
		"maxHp": creature.get("maxHp", 0),
		"level": creature.get("level", 1),
	}


# --- Helpers ------------------------------------------------------------------

func xp_to_next(level: int) -> int:
	var table := [0, 100, 250, 500, 900, 1500, 2300, 3400, 5000]
	var index := clampi(level, 0, table.size() - 1)
	return int(table[index])


func map_size() -> Vector2i:
	return Vector2i(MAP_WIDTH, MAP_HEIGHT)


## Enables/disables idle creature wandering. Disable for deterministic runs.
func set_wandering(enabled: bool) -> void:
	_creature_interval = 0.9 if enabled else 100000.0


func _find_character(character_id: String) -> Dictionary:
	for character in characters:
		if String(character.get("characterId", "")) == character_id:
			return character
	return {}


func _seed_characters() -> void:
	characters = [{
		"characterId": _new_id("char"),
		"accountId": account_id,
		"name": "Aria",
		"characterClass": "Warrior",
		"level": 1,
		"experience": 0,
		"state": "Offline",
	}]


func _add_to_inventory(item_id: String, name: String, quantity: int) -> void:
	for entry in inventory:
		if String(entry.get("itemId", "")) == item_id:
			entry["quantity"] = int(entry.get("quantity", 0)) + quantity
			return
	inventory.append({"itemId": item_id, "name": name, "quantity": quantity})


func _is_walkable(x: int, y: int) -> bool:
	if x <= 0 or y <= 0 or x >= MAP_WIDTH - 1 or y >= MAP_HEIGHT - 1:
		return false
	return true


func _distance(a: Vector2i, b: Vector2i) -> int:
	return absi(a.x - b.x) + absi(a.y - b.y)


func _new_id(prefix: String) -> String:
	_id_counter += 1
	return "%s-%04d" % [prefix, _id_counter]


func _next_sequence() -> int:
	server_sequence += 1
	return server_sequence


func _event(name: String, payload: Dictionary) -> Dictionary:
	return {
		"version": ProtocolMessages.VERSION,
		"type": ProtocolMessages.TYPE_EVENT,
		"name": name,
		"requestId": "",
		"sequence": _next_sequence(),
		"payload": payload,
	}


func _snapshot(payload: Dictionary) -> Dictionary:
	return {
		"version": ProtocolMessages.VERSION,
		"type": ProtocolMessages.TYPE_SNAPSHOT,
		"name": ProtocolMessages.EVT_WORLD_SNAPSHOT,
		"requestId": "",
		"sequence": _next_sequence(),
		"payload": payload,
	}


func _error(code: String, message: String) -> Dictionary:
	return {
		"version": ProtocolMessages.VERSION,
		"type": ProtocolMessages.TYPE_ERROR,
		"name": ProtocolMessages.EVT_ERROR,
		"requestId": "",
		"sequence": _next_sequence(),
		"payload": {"code": code, "message": message},
	}
