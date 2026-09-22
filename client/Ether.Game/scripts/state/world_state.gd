class_name WorldState
extends RefCounted

## Client-side mirror of the authoritative world.
##
## Read-only from the client's perspective: it is only ever written from
## snapshot/delta payloads produced by the server (mock or real). Ideal for the
## view layer to query, and for tests to assert against.

signal snapshot_applied
signal entity_upserted(id: String)
signal entity_removed(id: String)
signal player_updated

var map: Dictionary = {}
var map_id := 0
var player_id := ""
var player: Dictionary = {}
var entities: Dictionary = {}


func clear() -> void:
	map = {}
	map_id = 0
	player_id = ""
	player = {}
	entities = {}


func apply_snapshot(payload: Dictionary) -> void:
	var snapshot := SnapshotProcessor.process(payload)
	map = snapshot.get("map", {})
	map_id = int(map.get("id", 0))
	entities = {}

	var player_data: Dictionary = snapshot.get("player", {})
	if not player_data.is_empty():
		_set_player(player_data)

	for entity in snapshot.get("entities", []):
		_upsert_entity(entity)

	snapshot_applied.emit()


func apply_delta(payload: Dictionary) -> void:
	var delta := DeltaProcessor.process(payload)

	if delta.has("map"):
		map = delta["map"]
		map_id = int(map.get("id", map_id))

	if delta.has("player"):
		_update_player(delta["player"])

	for entity in delta.get("upserts", []):
		_upsert_entity(entity)

	for id in delta.get("removes", []):
		remove_entity(String(id))


func remove_entity(id: String) -> void:
	if not entities.has(id):
		return
	entities.erase(id)
	entity_removed.emit(id)


func get_entity(id: String) -> Dictionary:
	return entities.get(id, {})


## Applies a `movement.accepted` payload. Rejects updates for a different
## character or map (the server is authoritative; the client never guesses).
## Returns { applied: bool, reason: String }.
func apply_movement(payload: Dictionary) -> Dictionary:
	var character_id := String(payload.get("characterId", ""))
	var map_id := int(payload.get("mapId", -1))

	if character_id == "" or character_id != player_id:
		return {"applied": false, "reason": "character_mismatch"}
	if map_id != self.map_id:
		return {"applied": false, "reason": "map_mismatch"}

	_upsert_entity({
		"id": character_id,
		"x": int(payload.get("x", 0)),
		"y": int(payload.get("y", 0)),
	})
	return {"applied": true, "reason": ""}


# --- Creature replication (M6) -------------------------------------------------

func apply_creature_spawned(payload: Dictionary) -> bool:
	var creature := SnapshotProcessor.normalize_creature(payload)
	if creature.is_empty():
		return false
	_upsert_entity(creature)
	return true


func apply_creature_moved(payload: Dictionary) -> bool:
	var id := String(payload.get("creatureId", payload.get("id", "")))
	if id == "" or not has_entity(id):
		return false
	_upsert_entity({"id": id, "x": int(payload.get("x", 0)), "y": int(payload.get("y", 0))})
	return true


func apply_creature_state(payload: Dictionary) -> bool:
	var id := String(payload.get("creatureId", payload.get("id", "")))
	if id == "" or not has_entity(id):
		return false
	var state := String(payload.get("state", ""))
	_upsert_entity({"id": id, "state": state, "dead": is_dead_state(state)})
	return true


func apply_creature_health(payload: Dictionary) -> bool:
	var id := String(payload.get("creatureId", payload.get("id", "")))
	if id == "" or not has_entity(id):
		return false
	_upsert_entity({
		"id": id,
		"hp": int(payload.get("health", payload.get("hp", 0))),
		"maxHp": int(payload.get("maxHealth", payload.get("maxHp", 0))),
	})
	return true


func apply_creature_despawned(payload: Dictionary) -> bool:
	var id := String(payload.get("creatureId", payload.get("id", "")))
	if id == "":
		return false
	remove_entity(id)
	return true


## Applies a `combat.result`: updates the target's HP/state/death.
## Returns { target_id, defeated, target_health, target_max_health, target_state }.
func apply_combat_result(payload: Dictionary) -> Dictionary:
	var target_id := String(payload.get("targetId", ""))
	var health := int(payload.get("targetHealth", 0))
	var max_health := int(payload.get("targetMaxHealth", 0))
	var state := String(payload.get("targetState", ""))
	var defeated := bool(payload.get("targetDefeated", false))

	if target_id != "" and has_entity(target_id):
		_upsert_entity({
			"id": target_id,
			"hp": health,
			"maxHp": max_health,
			"state": state,
			"dead": defeated or is_dead_state(state),
		})

	return {
		"target_id": target_id,
		"defeated": defeated,
		"target_health": health,
		"target_max_health": max_health,
		"target_state": state,
	}


static func is_dead_state(state: String) -> bool:
	return state == ProtocolMessages.CREATURE_STATE_DEAD or state == ProtocolMessages.CREATURE_STATE_RESPAWNING


func has_entity(id: String) -> bool:
	return entities.has(id)


func entities_of_kind(kind: String) -> Array:
	var result: Array = []
	for id in entities.keys():
		var entity: Dictionary = entities[id]
		if String(entity.get("kind", "")) == kind and not bool(entity.get("dead", false)):
			result.append(entity)
	return result


func creatures() -> Array:
	return entities_of_kind("creature")


func entity_position(id: String) -> Vector2i:
	var entity := get_entity(id)
	return Vector2i(int(entity.get("x", 0)), int(entity.get("y", 0)))


func player_position() -> Vector2i:
	return Vector2i(int(player.get("x", 0)), int(player.get("y", 0)))


## Returns the id of a creature occupying the given tile, or "".
func creature_at(x: int, y: int) -> String:
	for creature in creatures():
		if int(creature.get("x", -1)) == x and int(creature.get("y", -1)) == y:
			return String(creature.get("id", ""))
	return ""


## Returns the id of the nearest creature within `max_distance` (Chebyshev, matching
## server range semantics), or "".
func nearest_creature(origin: Vector2i, max_distance: int = 8) -> String:
	var best := ""
	var best_distance := max_distance + 1
	for creature in creatures():
		var position := Vector2i(int(creature.get("x", 0)), int(creature.get("y", 0)))
		var distance := maxi(absi(origin.x - position.x), absi(origin.y - position.y))
		if distance < best_distance:
			best_distance = distance
			best = String(creature.get("id", ""))
	return best


func creature_count() -> int:
	return creatures().size()


## Chebyshev distance from the player to an entity (server range semantics).
func player_distance_to_chebyshev(id: String) -> int:
	var position := entity_position(id)
	var origin := player_position()
	return maxi(absi(origin.x - position.x), absi(origin.y - position.y))


func _set_player(data: Dictionary) -> void:
	player = data.duplicate(true)
	player_id = String(player.get("id", ""))
	if player_id != "":
		entities[player_id] = player
		entity_upserted.emit(player_id)
	player_updated.emit()


func _update_player(data: Dictionary) -> void:
	if player.is_empty():
		_set_player(data)
		return
	player.merge(data, true)
	player_id = String(player.get("id", player_id))
	if player_id != "":
		entities[player_id] = player
	player_updated.emit()


func _upsert_entity(data: Dictionary) -> void:
	var id := String(data.get("id", ""))
	if id == "":
		return
	if entities.has(id):
		var existing: Dictionary = entities[id]
		existing.merge(data, true)
	else:
		var created := data.duplicate(true)
		if not created.has("kind"):
			created["kind"] = "creature"
		entities[id] = created

	if id == player_id:
		player = entities[id]
		player_updated.emit()
	entity_upserted.emit(id)
