class_name ClientState
extends RefCounted

## Non-authoritative client-side session data.
##
## Holds the account/session tokens, the character list and an inventory
## *mirror*. Nothing here decides a gameplay outcome — it only stores what the
## server (mock or real) already decided, for the UI to render.

var account_id := ""
var display_name := ""
var access_token := ""
var refresh_token := ""
var game_token := ""

var characters: Array = []
var selected_character_id := ""
var inventory: Array = []


func set_session(data: Dictionary) -> void:
	if data.has("accountId"):
		account_id = String(data["accountId"])
	if data.has("displayName"):
		display_name = String(data["displayName"])
	if data.has("accessToken"):
		access_token = String(data["accessToken"])
	if data.has("refreshToken"):
		refresh_token = String(data["refreshToken"])
	if data.has("gameToken"):
		game_token = String(data["gameToken"])


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


func is_authenticated() -> bool:
	return account_id != "" or game_token != ""


func clear_session() -> void:
	access_token = ""
	refresh_token = ""
	game_token = ""
	selected_character_id = ""
	characters = []
	inventory = []
