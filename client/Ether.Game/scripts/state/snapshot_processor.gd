class_name SnapshotProcessor
extends RefCounted

## Normalizes a `world.snapshot` payload into the client's internal shape.
##
## Canonical M4 payload (backend):
##   { mapId, width, height, player: { characterId, x, y, state }, serverTime }
##
## The client keeps a single normalized shape:
##   { map: { id, name, width, height, tileSize },
##     player: { id, kind, x, y, state, ... }, entities: [] }
##
## A legacy/richer shape (map/player.id/entities) is tolerated so the same code
## serves older snapshots and the local mock. Isolates the wire format from the
## rest of the client.

const DEFAULT_TILE_SIZE := 32


static func process(payload: Dictionary) -> Dictionary:
	if payload.has("mapId") or (payload.has("player") and typeof(payload.get("player")) == TYPE_DICTIONARY and as_dict(payload.get("player")).has("characterId")):
		return _process_canonical(payload)
	return _process_legacy(payload)


static func _process_canonical(payload: Dictionary) -> Dictionary:
	var map := normalize_map({
		"id": payload.get("mapId", 0),
		"name": payload.get("mapName", ""),
		"width": payload.get("width", 0),
		"height": payload.get("height", 0),
		"tileSize": payload.get("tileSize", DEFAULT_TILE_SIZE),
	})

	var player := normalize_player(as_dict(payload.get("player")))
	var entities := _snapshot_entities(payload)

	return {"map": map, "player": player, "entities": entities}


static func _process_legacy(payload: Dictionary) -> Dictionary:
	return {
		"map": normalize_map(as_dict(payload.get("map", {}))),
		"player": normalize_entity(as_dict(payload.get("player", {}))),
		"entities": _snapshot_entities(payload),
	}


static func normalize_map(data: Dictionary) -> Dictionary:
	return {
		"id": int(data.get("id", data.get("mapId", 0))),
		"name": String(data.get("name", "")),
		"width": int(data.get("width", 0)),
		"height": int(data.get("height", 0)),
		"tileSize": int(data.get("tileSize", DEFAULT_TILE_SIZE)),
	}


## Canonical player (M4): identity + authoritative position + state.
static func normalize_player(data: Dictionary) -> Dictionary:
	var id := String(data.get("characterId", data.get("id", "")))
	if id == "":
		return {}
	var player := {
		"id": id,
		"kind": "player",
		"x": int(data.get("x", 0)),
		"y": int(data.get("y", 0)),
		"state": String(data.get("state", "")),
	}
	_copy_optional(data, player)
	return player


## Full entity normalization (legacy snapshots / new entities).
static func normalize_entity(item: Variant) -> Dictionary:
	if typeof(item) != TYPE_DICTIONARY:
		return {}
	var data: Dictionary = item
	var id := String(data.get("id", data.get("entityId", data.get("characterId", ""))))
	if id == "":
		return {}
	var entity := {
		"id": id,
		"kind": String(data.get("kind", "creature")),
		"x": int(data.get("x", 0)),
		"y": int(data.get("y", 0)),
		"state": String(data.get("state", "")),
	}
	_copy_optional(data, entity)
	return entity


## Canonical M6 creature normalization (snapshot entry or creature.* payload).
## Accepts `creatureId`/`id`, `health`/`hp`, `maxHealth`/`maxHp`.
static func normalize_creature(item: Variant) -> Dictionary:
	if typeof(item) != TYPE_DICTIONARY:
		return {}
	var data: Dictionary = item
	var id := String(data.get("creatureId", data.get("id", data.get("entityId", ""))))
	if id == "":
		return {}
	return {
		"id": id,
		"kind": "creature",
		"definitionId": String(data.get("definitionId", "")),
		"name": String(data.get("name", "")),
		"level": int(data.get("level", 1)),
		"x": int(data.get("x", 0)),
		"y": int(data.get("y", 0)),
		"hp": int(data.get("health", data.get("hp", 0))),
		"maxHp": int(data.get("maxHealth", data.get("maxHp", 0))),
		"state": String(data.get("state", "")),
		"dead": bool(data.get("dead", false)),
	}


## Present-keys-only patch for delta upserts (never clobbers unrelated fields).
static func normalize_patch(item: Variant) -> Dictionary:
	if typeof(item) != TYPE_DICTIONARY:
		return {}
	var data: Dictionary = item
	var id := String(data.get("id", data.get("entityId", data.get("characterId", ""))))
	if id == "":
		return {}
	var patch := {"id": id}
	for key in ["kind", "name", "characterClass", "state"]:
		if data.has(key):
			patch[key] = String(data[key])
	for key in ["x", "y", "hp", "maxHp", "level", "experience", "experienceToNext", "gold"]:
		if data.has(key):
			patch[key] = int(data[key])
	if data.has("health"):
		patch["hp"] = int(data["health"])
	if data.has("maxHealth"):
		patch["maxHp"] = int(data["maxHealth"])
	if data.has("experienceToNextLevel"):
		patch["experienceToNext"] = int(data["experienceToNextLevel"])
	if data.has("dead"):
		patch["dead"] = bool(data["dead"])
	if data.has("inventory") and typeof(data["inventory"]) == TYPE_ARRAY:
		patch["inventory"] = data["inventory"]
	return patch


static func as_dict(value: Variant) -> Dictionary:
	return value if typeof(value) == TYPE_DICTIONARY else {}


static func _copy_optional(data: Dictionary, target: Dictionary) -> void:
	for key in ["name", "characterClass"]:
		if data.has(key):
			target[key] = String(data[key])
	for key in ["hp", "maxHp", "level", "experience", "experienceToNext", "gold"]:
		if data.has(key):
			target[key] = int(data[key])
	# Canonical M7 snapshot aliases (health/maxHealth/experienceToNextLevel).
	if data.has("health"):
		target["hp"] = int(data["health"])
	if data.has("maxHealth"):
		target["maxHp"] = int(data["maxHealth"])
	if data.has("experienceToNextLevel"):
		target["experienceToNext"] = int(data["experienceToNextLevel"])
	if data.has("dead"):
		target["dead"] = bool(data["dead"])
	if data.has("inventory") and typeof(data["inventory"]) == TYPE_ARRAY:
		target["inventory"] = data["inventory"]


## Collects entities from a snapshot. M6 canonical field is `creatures`; the
## legacy `entities` array is also accepted. Deduped by id.
static func _snapshot_entities(payload: Dictionary) -> Array:
	var by_id := {}
	_collect(payload.get("creatures", []), by_id, true)
	_collect(payload.get("entities", []), by_id, false)

	var entities: Array = []
	for id in by_id.keys():
		entities.append(by_id[id])
	return entities


static func _collect(raw: Variant, by_id: Dictionary, as_creature: bool) -> void:
	if typeof(raw) != TYPE_ARRAY:
		return
	for item in raw:
		var entity := normalize_creature(item) if as_creature else normalize_entity(item)
		if not entity.is_empty():
			by_id[String(entity.get("id", ""))] = entity
