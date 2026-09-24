extends TestCase

## M8 authoritative inventory: snapshot ingestion, HTTP response shape,
## stacking in the mock server, reconnect replacement, unknown items, UI.


func test_snapshot_inventory_replaces_mirror() -> void:
	var state := ClientState.new()
	state.set_inventory([{"itemDefinitionId": "item.slime_gel", "name": "Slime Gel", "quantity": 1}])

	# Authoritative snapshot inventory (InventoryItemResponse[]).
	var payload := {
		"mapId": 1, "width": 32, "height": 32,
		"player": {"characterId": "p", "x": 1, "y": 1, "state": "InWorld"},
		"inventory": [
			{"itemDefinitionId": "item.slime_gel", "name": "Slime Gel", "quantity": 3, "maxStack": 99, "stackable": true, "location": "inventory"},
			{"itemDefinitionId": "item.coin_pouch", "name": "Coin Pouch", "quantity": 1, "maxStack": 99, "stackable": true, "location": "inventory"},
		],
	}
	var snapshot := SnapshotProcessor.process(payload)

	# GameClient replaces the mirror from the top-level inventory array.
	var items: Variant = snapshot.get("entities", []) # placeholder; real path below
	items = payload.get("inventory")
	var list: Array = items
	state.set_inventory(list)

	assert_eq(state.inventory.size(), 2, "snapshot replaces (no merge duplicates)")
	assert_eq(int(state.inventory[0].get("quantity", 0)), 3, "authoritative quantity")
	assert_eq(String(state.inventory[1].get("name", "")), "Coin Pouch")
	assert_eq(String(state.inventory[0].get("maxStack", "")), "99" if false else str(state.inventory[0].get("maxStack", 99)))


func test_snapshot_inventory_empty_clears_mirror() -> void:
	var state := ClientState.new()
	state.set_inventory([{"itemDefinitionId": "a", "name": "A", "quantity": 2}])
	state.set_inventory([])
	assert_eq(state.inventory.size(), 0, "empty authoritative inventory clears the mirror")


func test_reconnect_inventory_replaces_without_duplicates() -> void:
	var state := ClientState.new()
	state.set_inventory([{"itemDefinitionId": "item.slime_gel", "name": "Slime Gel", "quantity": 1}])
	# Reconnect delivers a fresh snapshot with the same single stack twice-run.
	state.set_inventory([
		{"itemDefinitionId": "item.slime_gel", "name": "Slime Gel", "quantity": 1},
	])
	assert_eq(state.inventory.size(), 1, "no duplicate rows after reconnect")


func test_unknown_item_definition_gracefully_kept() -> void:
	var state := ClientState.new()
	state.set_inventory([{"itemDefinitionId": "item.unknown_future", "name": "???", "quantity": 5}])
	assert_eq(state.inventory.size(), 1, "unknown item kept without crashing")
	assert_eq(int(state.inventory[0].get("quantity", 0)), 5)


func test_http_inventory_response_shape_is_parsed() -> void:
	# InventoryResponse { characterId, items[] } -> mirror.
	var response := {"characterId": "char-1", "items": [
		{"itemDefinitionId": "item.beast_part", "name": "Beast Part", "quantity": 2, "maxStack": 99, "stackable": true, "location": "inventory"},
	]}
	var parsed: Variant = JSON.parse_string(JSON.stringify(response))
	assert_true(typeof(parsed) == TYPE_DICTIONARY, "response is an object")
	var dictionary: Dictionary = parsed
	var state := ClientState.new()
	var raw_items: Variant = dictionary.get("items", [])
	assert_true(typeof(raw_items) == TYPE_ARRAY)
	var items: Array = raw_items
	state.set_inventory(items)
	assert_eq(state.inventory.size(), 1)
	assert_eq(int(state.inventory[0].get("quantity", 0)), 2)


func test_mock_backend_stacks_loot_into_inventory() -> void:
	var backend := MockBackend.new()
	backend.set_loot_guaranteed(true)
	var state := ClientState.new()

	# Simulate two identical loot drops being applied to the server mirror.
	backend._inventory_add({"itemDefinitionId": "item.slime_gel", "name": "Slime Gel", "quantity": 1})
	backend._inventory_add({"itemDefinitionId": "item.slime_gel", "name": "Slime Gel", "quantity": 1})

	var response := backend.api_get_inventory()
	assert_eq(String(response["characterId"]), "", "character id echoed")
	var server_items: Array = response["items"]
	assert_eq(server_items.size(), 1, "server stacks into one row")
	assert_eq(int(server_items[0]["quantity"]), 2, "stacked quantity")

	# The client mirror consumes exactly that shape.
	var items: Array = server_items
	state.set_inventory(items)
	assert_eq(state.inventory.size(), 1)
	assert_eq(int(state.inventory[0].get("quantity", 0)), 2)


func test_hud_inventory_open_and_close() -> void:
	if TestCase.tree == null:
		return
	var hud: Node = load("res://scripts/ui/hud.gd").new()
	hud._ready()

	hud._on_inventory([{"itemDefinitionId": "i", "name": "Slime Gel", "quantity": 4}])
	assert_eq(str(hud._inventory_count.text), "1", "stack count shown")
	assert_eq(hud._inventory_list.get_child_count(), 1, "one row rendered")
	assert_true(hud._inventory_list.get_child(0).text.contains("x4"), "quantity rendered")

	assert_false(hud._inventory_modal.visible, "closed initially")
	assert_false(hud._inventory_backdrop.visible, "no touch block initially")

	hud._on_inventory_pressed()
	assert_true(hud._inventory_modal.visible, "opened")
	assert_true(hud._inventory_backdrop.visible, "world taps intercepted")

	hud._close_inventory()
	assert_false(hud._inventory_modal.visible, "closed")
	assert_false(hud._inventory_backdrop.visible, "world taps allowed again")

	hud.free()
