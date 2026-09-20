class_name DeltaProcessor
extends RefCounted

## Normalizes a WorldDelta payload (Blueprint v5.0 §54).
##
## Accepts the canonical `upserts`/`removes` shape and also tolerates an
## `entities` array (treated as upserts) so a slightly different server shape
## does not break the client.

static func process(payload: Dictionary) -> Dictionary:
	var upserts: Array = []
	_collect_entities(payload.get("upserts", []), upserts)
	if upserts.is_empty():
		_collect_entities(payload.get("entities", []), upserts)

	var removes: Array = []
	var raw_removes: Variant = payload.get("removes", [])
	if typeof(raw_removes) == TYPE_ARRAY:
		for id in raw_removes:
			var value := String(id)
			if value != "":
				removes.append(value)

	var result := {"upserts": upserts, "removes": removes}
	if payload.has("map"):
		result["map"] = SnapshotProcessor.normalize_map(SnapshotProcessor.as_dict(payload.get("map")))
	if payload.has("player"):
		var player := SnapshotProcessor.normalize_patch(SnapshotProcessor.as_dict(payload.get("player")))
		if not player.is_empty():
			result["player"] = player
	return result


static func _collect_entities(raw: Variant, out: Array) -> void:
	if typeof(raw) != TYPE_ARRAY:
		return
	for item in raw:
		var entity := SnapshotProcessor.normalize_patch(item)
		if not entity.is_empty():
			out.append(entity)
