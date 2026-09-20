class_name ReconnectManager
extends RefCounted

## Reconnect backoff infrastructure (Blueprint v5.0 §39).
##
## The final reconnect policy is a backend decision; this only implements the
## client-side scheduling so the UI can show "Reconnecting..." and the network
## layer can retry without a rewrite. Time is injected for testability.

enum Phase { IDLE, WAITING, ATTEMPTING, EXHAUSTED }

var initial_delay: float = 1.0
var max_delay: float = 15.0
var max_attempts: int = 8
var multiplier: float = 2.0

var _phase: int = Phase.IDLE
var _attempts: int = 0
var _timer: float = 0.0


func _init(p_initial_delay: float = 1.0, p_max_delay: float = 15.0, p_max_attempts: int = 8) -> void:
	initial_delay = maxf(0.05, p_initial_delay)
	max_delay = maxf(initial_delay, p_max_delay)
	max_attempts = maxi(1, p_max_attempts)


func start() -> void:
	_phase = Phase.WAITING
	_attempts = 0
	_timer = current_delay()


func stop() -> void:
	_phase = Phase.IDLE
	_attempts = 0
	_timer = 0.0


func current_delay() -> float:
	return minf(max_delay, initial_delay * pow(multiplier, float(_attempts)))


## Returns true when it is time to attempt a reconnect.
func update(delta: float) -> bool:
	if _phase != Phase.WAITING:
		return false
	_timer -= delta
	if _timer <= 0.0:
		_phase = Phase.ATTEMPTING
		return true
	return false


func on_attempt_succeeded() -> void:
	_phase = Phase.IDLE
	_attempts = 0
	_timer = 0.0


func on_attempt_failed() -> void:
	_attempts += 1
	if _attempts >= max_attempts:
		_phase = Phase.EXHAUSTED
	else:
		_phase = Phase.WAITING
		_timer = current_delay()


func phase() -> int:
	return _phase


func attempts() -> int:
	return _attempts


func is_active() -> bool:
	return _phase == Phase.WAITING or _phase == Phase.ATTEMPTING


func is_exhausted() -> bool:
	return _phase == Phase.EXHAUSTED
