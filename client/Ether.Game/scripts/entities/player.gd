class_name Player
extends EntityView

## Player placeholder presentation. No gameplay logic lives here.


func _init() -> void:
	entity_kind = "player"
	_radius = 13.0
	interpolation_speed = 14.0
