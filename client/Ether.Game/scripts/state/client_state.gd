class_name ClientState
extends RefCounted

## Non-authoritative client-side session data.
##
## Holds identity/session tokens and the character list mirror. Nothing here
## decides a gameplay outcome — it only stores what the backend already decided,
## for the UI to render. Tokens are never logged or shown in the UI.

var account_id := ""
var email := ""
var session_id := ""
var access_token := ""
var refresh_token := ""
var game_token := ""
var game_token_expires_at := ""

var characters: Array = []
var selected_character_id := ""
var inventory: Array = []


func set_session(data: Dictionary) -> void:
	if data.has("accountId"):
		account_id = String(data["accountId"])
	if data.has("accessToken"):
		access_token = String(data["accessToken"])
	if data.has("refreshToken"):
		refresh_token = String(data["refreshToken"])


func display_name() -> String:
	var source := email if email != "" else account_id
	var at := source.find("@")
	return source.substr(0, at) if at > 0 else source


func set_characters(list: Array) -> void:
	characters = list.duplicate(true)


func select(character_id: String) -> void:
	selected_character_id = character_id


func selected_character() -> Dictionary:
	for character in characters:
		if String(character.get("characterId", "")) == selected_character_id:
			return character
	return {}


func set_inventory(items: Array) -> void:
	inventory = items.duplicate(true)


## Merges server-granted loot into the inventory mirror (stacks by definition id,
## falling back to the item name). Purely a mirror: the server decides loot.
func add_loot(items: Array) -> void:
	for item in items:
		if typeof(item) != TYPE_DICTIONARY:
			continue
		var definition_id := String(item.get(ProtocolMessages.FIELD_ITEM_DEFINITION_ID, ""))
		var item_name := String(item.get(ProtocolMessages.FIELD_ITEM_NAME, "Item"))
		var quantity := int(item.get(ProtocolMessages.FIELD_ITEM_QUANTITY, 1))
		var key := definition_id if definition_id != "" else item_name
		var merged := false
		for existing in inventory:
			var existing_key := String(existing.get(ProtocolMessages.FIELD_ITEM_DEFINITION_ID, ""))
			if existing_key == "":
				existing_key = String(existing.get(ProtocolMessages.FIELD_ITEM_NAME, ""))
			if existing_key == key:
				existing[ProtocolMessages.FIELD_ITEM_QUANTITY] = int(existing.get(ProtocolMessages.FIELD_ITEM_QUANTITY, 0)) + quantity
				merged = true
				break
		if not merged:
			inventory.append({
				ProtocolMessages.FIELD_ITEM_DEFINITION_ID: definition_id,
				ProtocolMessages.FIELD_ITEM_INSTANCE_ID: String(item.get(ProtocolMessages.FIELD_ITEM_INSTANCE_ID, "")),
				ProtocolMessages.FIELD_ITEM_NAME: item_name,
				ProtocolMessages.FIELD_ITEM_QUANTITY: quantity,
			})


func is_authenticated() -> bool:
	return access_token != "" or game_token != ""


func clear_session() -> void:
	access_token = ""
	refresh_token = ""
	game_token = ""
	game_token_expires_at = ""
	selected_character_id = ""
	session_id = ""
	characters = []
	inventory = []
