class_name HeartbeatManager
extends RefCounted

## Client-side heartbeat bookkeeping (Blueprint v5.0 §40).
##
## The client pings every `interval` seconds; a session is considered stale
## after `timeout` seconds without traffic. Time is injected via update() so
## the logic is deterministic and testable without an engine timer.

var interval: float = 20.0
var timeout: float = 60.0

var _since_ping: float = 0.0
var _since_receive: float = 0.0


func _init(p_interval: float = 20.0, p_timeout: float = 60.0) -> void:
	interval = maxf(0.5, p_interval)
	timeout = maxf(interval, p_timeout)


## Call whenever any packet arrives (not just pongs).
func note_received() -> void:
	_since_receive = 0.0


## Returns true when a ping should be sent this tick.
func update(delta: float) -> bool:
	_since_ping += delta
	_since_receive += delta
	if _since_ping >= interval:
		_since_ping = 0.0
		return true
	return false


func is_stale() -> bool:
	return _since_receive >= timeout


func time_since_receive() -> float:
	return _since_receive


func time_until_ping() -> float:
	return maxf(0.0, interval - _since_ping)


func reset() -> void:
	_since_ping = 0.0
	_since_receive = 0.0
