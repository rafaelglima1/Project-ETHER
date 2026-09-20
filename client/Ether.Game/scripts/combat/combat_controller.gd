class_name CombatController
extends RefCounted

## Client-side combat *intent* only (Blueprint v5.0 §89).
##
## Tracks the selected target and raises an attack request. It never computes
## damage, HP, death, XP or loot — those arrive from the server.

signal target_changed(target_id: String)
signal attack_requested(target_id: String)

var selected_target_id := ""


func select_target(target_id: String) -> void:
	if target_id == selected_target_id:
		return
	selected_target_id = target_id
	target_changed.emit(target_id)


func clear_target() -> void:
	select_target("")


func has_target() -> bool:
	return selected_target_id != ""


func request_attack() -> bool:
	if selected_target_id == "":
		return false
	attack_requested.emit(selected_target_id)
	return true
