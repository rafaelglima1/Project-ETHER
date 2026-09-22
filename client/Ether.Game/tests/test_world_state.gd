extends TestCase

## WorldState snapshot/movement application and queries.

const CHARACTER_ID := "11111111-1111-4111-8111-111111111111"


func test_apply_canonical_snapshot() -> void:
	var world := WorldState.new()
	world.apply_snapshot(_canonical_snapshot())

	assert_eq(world.map_id, 1)
	assert_eq(int(world.map["width"]), 32)
	assert_eq(world.player_id, CHARACTER_ID)
	assert_eq(world.player_position(), Vector2i(10, 12))
	assert_eq(String(world.player.get("state", "")), "InWorld")


func test_apply_movement_updates_position() -> void:
	var world := WorldState.new()
	world.apply_snapshot(_canonical_snapshot())
	var result := world.apply_movement({"characterId": CHARACTER_ID, "mapId": 1, "x": 11, "y": 12})
	assert_true(bool(result["applied"]))
	assert_eq(world.player_position(), Vector2i(11, 12))


func test_apply_movement_rejects_wrong_character() -> void:
	var world := WorldState.new()
	world.apply_snapshot(_canonical_snapshot())
	var result := world.apply_movement({"characterId": "99999999-9999-4999-8999-999999999999", "mapId": 1, "x": 30, "y": 30})
	assert_false(bool(result["applied"]))
	assert_eq(String(result["reason"]), "character_mismatch")
	assert_eq(world.player_position(), Vector2i(10, 12))


func test_apply_movement_rejects_wrong_map() -> void:
	var world := WorldState.new()
	world.apply_snapshot(_canonical_snapshot())
	var result := world.apply_movement({"characterId": CHARACTER_ID, "mapId": 2, "x": 11, "y": 12})
	assert_false(bool(result["applied"]))
	assert_eq(String(result["reason"]), "map_mismatch")
	assert_eq(world.player_position(), Vector2i(10, 12))


func test_legacy_snapshot_is_tolerated() -> void:
	var world := WorldState.new()
	world.apply_snapshot({
		"map": {"id": 1, "width": 20, "height": 15, "tileSize": 32},
		"player": {"id": "p1", "kind": "player", "x": 3, "y": 4},
		"entities": [{"id": "c1", "kind": "creature", "x": 8, "y": 8, "hp": 10, "maxHp": 10}],
	})
	assert_eq(world.player_position(), Vector2i(3, 4))
	assert_eq(world.creatures().size(), 1)


func test_malformed_snapshot_does_not_crash() -> void:
	var world := WorldState.new()
	world.apply_snapshot({"mapId": "not-an-int", "player": 5, "entities": "nope"})
	assert_eq(world.creatures().size(), 0)


func test_update_player_from_patch_preserves_fields() -> void:
	var world := WorldState.new()
	world.apply_snapshot(_canonical_snapshot())
	world.apply_delta({"player": {"id": CHARACTER_ID, "x": 20}})
	assert_eq(world.player_position(), Vector2i(20, 12))
	assert_eq(String(world.player.get("state", "")), "InWorld", "unrelated field preserved")
	assert_eq(String(world.player.get("kind", "")), "player", "kind preserved")


func _canonical_snapshot() -> Dictionary:
	return {
		"mapId": 1,
		"width": 32,
		"height": 32,
		"player": {"characterId": CHARACTER_ID, "x": 10, "y": 12, "state": "InWorld"},
		"serverTime": "2026-01-01T00:00:00Z",
	}
