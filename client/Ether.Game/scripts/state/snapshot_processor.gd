class_name SnapshotProcessor
extends RefCounted

## Normalizes a WorldSnapshot payload into the client's internal shape.
##
## Isolates the wire format from the rest of the client (Blueprint v5.0 §54).
## Tolerates missing fields and drops malformed entries instead of crashing.

static func process(payload: Dictionary) -> Dictionary:
	var entities: Array = []
	var raw_entities: Variant = payload.get("entities", [])
	if typeof(raw_entities) == TYPE_ARRAY:
		for item in raw_entities:
			var entity := normalize_entity(item)
			if not entity.is_empty():
				entities.append(entity)

	return {
		"map": normalize_map(as_dict(payload.get("map", {}))),
		"player": normalize_entity(as_dict(payload.get("player", {}))),
		"entities": entities,
	}


static func normalize_map(data: Dictionary) -> Dictionary:
	return {
		"id": int(data.get("id", 0)),
		"name": String(data.get("name", "")),
		"width": int(data.get("width", 0)),
		"height": int(data.get("height", 0)),
		"tileSize": int(data.get("tileSize", 32)),
	}


static func normalize_entity(item: Variant) -> Dictionary:
	if typeof(item) != TYPE_DICTIONARY:
		return {}
	var data: Dictionary = item

	var id := String(data.get("id", data.get("entityId", "")))
	if id == "":
		return {}

	var inventory: Array = []
	var raw_inventory: Variant = data.get("inventory", [])
	if typeof(raw_inventory) == TYPE_ARRAY:
		inventory = raw_inventory

	return {
		"id": id,
		"kind": String(data.get("kind", "creature")),
		"name": String(data.get("name", "")),
		"characterClass": String(data.get("characterClass", "")),
		"x": int(data.get("x", 0)),
		"y": int(data.get("y", 0)),
		"hp": int(data.get("hp", 0)),
		"maxHp": int(data.get("maxHp", 0)),
		"level": int(data.get("level", 1)),
		"experience": int(data.get("experience", 0)),
		"experienceToNext": int(data.get("experienceToNext", 0)),
		"gold": int(data.get("gold", 0)),
		"dead": bool(data.get("dead", false)),
		"inventory": inventory,
	}


static func as_dict(value: Variant) -> Dictionary:
	return value if typeof(value) == TYPE_DICTIONARY else {}


## Normalizes a *partial* entity update (delta upsert / player patch).
##
## Unlike normalize_entity this only carries keys that are actually present, so
## merging a patch never overwrites unrelated fields with defaults.
static func normalize_patch(item: Variant) -> Dictionary:
	if typeof(item) != TYPE_DICTIONARY:
		return {}
	var data: Dictionary = item

	var id := String(data.get("id", data.get("entityId", "")))
	if id == "":
		return {}

	var patch := {"id": id}
	for key in ["kind", "name", "characterClass"]:
		if data.has(key):
			patch[key] = String(data[key])
	for key in ["x", "y", "hp", "maxHp", "level", "experience", "experienceToNext", "gold"]:
		if data.has(key):
			patch[key] = int(data[key])
	if data.has("dead"):
		patch["dead"] = bool(data["dead"])
	if data.has("inventory") and typeof(data["inventory"]) == TYPE_ARRAY:
		patch["inventory"] = data["inventory"]
	return patch
