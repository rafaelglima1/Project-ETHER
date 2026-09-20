class_name Creature
extends EntityView

## Creature placeholder presentation. No AI or damage logic lives here.


func _init() -> void:
	entity_kind = "creature"
	_radius = 10.0
	interpolation_speed = 8.0
