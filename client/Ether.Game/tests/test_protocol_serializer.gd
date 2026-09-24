extends TestCase

## Canonical protocol envelope encoding/decoding and hostile-input handling.


func test_encode_command_is_canonical() -> void:
	var serializer := ProtocolSerializer.new()
	var text := serializer.encode_command(ProtocolMessages.CMD_MOVEMENT_MOVE, {"x": 10, "y": 7}, 42, "req-1")

	# camelCase keys.
	assert_true(text.contains("\"requestId\""), "requestId key")
	assert_false(text.contains("request_id"), "no snake_case key")

	var result := serializer.decode(text)
	assert_true(bool(result["ok"]), "should decode")
	var envelope: Dictionary = result["envelope"]
	assert_eq(envelope["version"], 1)
	assert_eq(envelope["type"], ProtocolMessages.TYPE_COMMAND)
	assert_eq(envelope["name"], ProtocolMessages.CMD_MOVEMENT_MOVE)
	assert_eq(envelope["sequence"], 42)
	assert_eq(envelope["requestId"], "req-1")
	assert_eq(int(envelope["payload"]["x"]), 10)


func test_generated_request_id_is_guid() -> void:
	var serializer := ProtocolSerializer.new()
	var request_id := serializer.new_request_id()
	assert_eq(request_id.length(), 36, "guid length")
	assert_eq(request_id[8], "-")
	assert_eq(request_id[13], "-")
	assert_eq(request_id[18], "-")
	assert_eq(request_id[23], "-")


func test_request_ids_are_unique() -> void:
	var serializer := ProtocolSerializer.new()
	var seen := {}
	for i in range(500):
		seen[serializer.new_request_id()] = true
	assert_eq(seen.size(), 500, "no accidental reuse")


func test_decode_event_with_null_request_id() -> void:
	var serializer := ProtocolSerializer.new()
	var text := '{"version":1,"type":"event","name":"game.authenticated","requestId":null,"sequence":1,"payload":{}}'
	var result := serializer.decode(text)
	assert_true(bool(result["ok"]))
	assert_eq(result["envelope"]["requestId"], "")


func test_decode_event_without_request_id() -> void:
	var serializer := ProtocolSerializer.new()
	var text := '{"version":1,"type":"event","name":"system.pong","sequence":2,"payload":{"serverTime":"x"}}'
	assert_true(bool(serializer.decode(text)["ok"]))


func test_decode_error_envelope() -> void:
	var serializer := ProtocolSerializer.new()
	var text := '{"version":1,"type":"error","name":"movement.rejected","requestId":"r","sequence":3,"payload":{"code":"OUT_OF_BOUNDS","message":"nope"}}'
	var result := serializer.decode(text)
	assert_true(bool(result["ok"]))
	assert_eq(result["envelope"]["type"], ProtocolMessages.TYPE_ERROR)


func test_rejects_empty() -> void:
	assert_false(bool(ProtocolSerializer.new().decode("")["ok"]))


func test_rejects_malformed_json() -> void:
	var result := ProtocolSerializer.new().decode("{not json")
	assert_false(bool(result["ok"]))


func test_rejects_non_object() -> void:
	assert_false(bool(ProtocolSerializer.new().decode("[1, 2, 3]")["ok"]))
	assert_false(bool(ProtocolSerializer.new().decode("42")["ok"]))


func test_rejects_wrong_version() -> void:
	var text := '{"version": 99, "type": "event", "name": "x", "sequence": 1, "payload": {}}'
	assert_false(bool(ProtocolSerializer.new().decode(text)["ok"]))


func test_rejects_unknown_type() -> void:
	var text := '{"version": 1, "type": "banana", "name": "x", "sequence": 1, "payload": {}}'
	assert_false(bool(ProtocolSerializer.new().decode(text)["ok"]))


func test_rejects_missing_name() -> void:
	var text := '{"version": 1, "type": "event", "sequence": 1, "payload": {}}'
	assert_false(bool(ProtocolSerializer.new().decode(text)["ok"]))


func test_rejects_missing_sequence() -> void:
	var text := '{"version": 1, "type": "event", "name": "x", "payload": {}}'
	assert_false(bool(ProtocolSerializer.new().decode(text)["ok"]))


func test_rejects_non_object_payload() -> void:
	var text := '{"version": 1, "type": "event", "name": "x", "sequence": 1, "payload": []}'
	assert_false(bool(ProtocolSerializer.new().decode(text)["ok"]))


func test_command_sequence_is_monotonic() -> void:
	var serializer := ProtocolSerializer.new()
	var sender := CommandSender.new(serializer)
	assert_eq(sender.next_sequence(), 1)
	assert_eq(sender.next_sequence(), 2)
	assert_eq(sender.next_sequence(), 3)
	sender.reset_sequence()
	assert_eq(sender.next_sequence(), 1)


func test_error_user_messages_are_non_technical() -> void:
	var message := ProtocolErrors.user_message(ProtocolMessages.CODE_OUT_OF_BOUNDS)
	assert_false(message.contains("OUT_OF_BOUNDS"), "should not leak the raw code")
	assert_true(message.length() > 0)


func test_combat_and_creature_names_are_canonical() -> void:
	assert_eq(ProtocolMessages.CMD_COMBAT_ATTACK, "combat.attack")
	assert_eq(ProtocolMessages.EVT_COMBAT_RESULT, "combat.result")
	assert_eq(ProtocolMessages.ERR_COMBAT_REJECTED, "combat.rejected")
	assert_eq(ProtocolMessages.EVT_WORLD_CREATURE_MOVED, "world.creature_moved")
	assert_eq(ProtocolMessages.CREATURE_STATE_CHASE, "Chase")
	assert_eq(ProtocolMessages.TARGET_TYPE_CREATURE, "creature")


func test_combat_error_messages_are_non_technical() -> void:
	var codes := [
		ProtocolMessages.CODE_ABILITY_NOT_FOUND,
		ProtocolMessages.CODE_TARGET_NOT_FOUND,
		ProtocolMessages.CODE_TARGET_DEAD,
		ProtocolMessages.CODE_ATTACKER_DEAD,
		ProtocolMessages.CODE_OUT_OF_RANGE,
		ProtocolMessages.CODE_COOLDOWN_ACTIVE,
		ProtocolMessages.CODE_SELF_TARGET,
	]
	for code in codes:
		var message := ProtocolErrors.user_message(code)
		assert_true(message.length() > 0)
		assert_false(message.contains(code), "should not leak code %s" % code)


func test_combat_attack_command_encodes_target_type() -> void:
	var serializer := ProtocolSerializer.new()
	var text := serializer.encode_command(ProtocolMessages.CMD_COMBAT_ATTACK, {
		"abilityId": ProtocolMessages.ABILITY_BASIC_ATTACK,
		"targetId": "abc",
		"targetType": ProtocolMessages.TARGET_TYPE_CREATURE,
	}, 5)
	var result := serializer.decode(text)
	assert_true(bool(result["ok"]))
	var payload: Dictionary = result["envelope"]["payload"]
	assert_eq(String(payload["targetType"]), "creature")
	assert_eq(String(payload["abilityId"]), "warrior.basic_attack")


func test_respawn_contract_matches_backend() -> void:
	assert_eq(ProtocolMessages.CMD_CHARACTER_RESPAWN, "character.respawn")
	assert_eq(ProtocolMessages.ERR_CHARACTER_RESPAWN_REJECTED, "character.respawn.rejected")
	assert_eq(ProtocolMessages.CODE_CHARACTER_DEAD, "CHARACTER_DEAD")
	assert_eq(ProtocolMessages.CODE_CHARACTER_NOT_DEAD, "CHARACTER_NOT_DEAD")

	var dead := ProtocolErrors.user_message(ProtocolMessages.CODE_CHARACTER_DEAD)
	assert_true(dead.length() > 0)
	assert_false(dead.contains(ProtocolMessages.CODE_CHARACTER_DEAD), "no raw code in UI text")
	assert_true(dead.to_lower().contains("respawn"), "tells the player what to do")

	var not_dead := ProtocolErrors.user_message(ProtocolMessages.CODE_CHARACTER_NOT_DEAD)
	assert_true(not_dead.length() > 0)
	assert_false(not_dead.contains(ProtocolMessages.CODE_CHARACTER_NOT_DEAD))
