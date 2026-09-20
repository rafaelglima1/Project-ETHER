extends TestCase

## WorldState snapshot/delta application and queries.


func test_apply_snapshot_populates_world() -> void:
	var world := WorldState.new()
	world.apply_snapshot(_snapshot())

	assert_eq(int(world.map["width"]), 20)
	assert_eq(world.player_id, "p1")
	assert_eq(world.player_position(), Vector2i(3, 4))
	assert_eq(world.creatures().size(), 1)


func test_apply_delta_moves_entity() -> void:
	var world := WorldState.new()
	world.apply_snapshot(_snapshot())
	world.apply_delta({"upserts": [{"id": "c1", "x": 9, "y": 9}]})
	assert_eq(world.entity_position("c1"), Vector2i(9, 9))


func test_delta_upsert_creates_unknown_entity() -> void:
	var world := WorldState.new()
	world.apply_snapshot(_snapshot())
	world.apply_delta({"upserts": [{"id": "c2", "kind": "creature", "x": 2, "y": 2}]})
	assert_true(world.has_entity("c2"))
	assert_eq(world.creatures().size(), 2)


func test_remove_entity_emits_signal() -> void:
	var world := WorldState.new()
	var removed: Array = []
	world.entity_removed.connect(func(id): removed.append(id))
	world.apply_snapshot(_snapshot())

	world.remove_entity("c1")
	assert_false(world.has_entity("c1"))
	assert_eq(removed.size(), 1)


func test_tolerates_malformed_snapshot() -> void:
	var world := WorldState.new()
	world.apply_snapshot({"entities": "not-an-array", "player": 5})
	assert_eq(world.creatures().size(), 0)


func test_creature_queries() -> void:
	var world := WorldState.new()
	world.apply_snapshot(_snapshot())
	assert_eq(world.creature_at(8, 8), "c1")
	assert_eq(world.creature_at(0, 0), "")
	assert_eq(world.nearest_creature(Vector2i(7, 8), 5), "c1")
	assert_eq(world.nearest_creature(Vector2i(0, 0), 1), "")


func _snapshot() -> Dictionary:
	return {
		"map": {"id": 1, "name": "Town", "width": 20, "height": 15, "tileSize": 32},
		"player": {"id": "p1", "kind": "player", "name": "Hero", "x": 3, "y": 4, "hp": 100, "maxHp": 100, "level": 1, "experience": 0, "experienceToNext": 100},
		"entities": [
			{"id": "c1", "kind": "creature", "name": "Slime", "x": 8, "y": 8, "hp": 35, "maxHp": 35},
		],
	}
