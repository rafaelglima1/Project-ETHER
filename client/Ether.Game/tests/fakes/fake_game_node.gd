class_name FakeGameNode
extends Node

## Minimal stand-in for the GameClient autoload used by UI tests. Records the
## calls a screen makes so tests can assert screens do not duplicate work.

signal characters_changed(characters: Array)
signal error_received(code: String, message: String)
signal log_message(text: String)
signal connection_status(text: String)
signal feedback(text: String)
signal rtt_changed(ms: int)
signal target_changed(target_id: String)
signal experience_changed(level: int, experience: int, gained: int, levels_gained: int)
signal loot_received(items: Array)

var request_characters_calls := 0
var select_character_calls := 0
var create_character_calls := 0


func request_characters() -> void:
	request_characters_calls += 1


func request_select_character(_id: String) -> void:
	select_character_calls += 1


func request_create_character(_name: String, _class: String) -> void:
	create_character_calls += 1
