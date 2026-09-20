class_name AppState
extends RefCounted

## Client connection/session state machine.
##
## Mirrors the states required by the client foundation. The client is never
## authoritative over gameplay; this state only tracks the *connection/session*
## lifecycle so screens and the network layer agree on where we are.

enum State {
	DISCONNECTED,
	CONNECTING,
	CONNECTED,
	AUTHENTICATED,
	IN_WORLD,
	RECONNECTING,
}

const _TRANSITIONS := {
	State.DISCONNECTED: [State.CONNECTING],
	State.CONNECTING: [State.CONNECTED, State.DISCONNECTED, State.RECONNECTING],
	State.CONNECTED: [State.AUTHENTICATED, State.DISCONNECTED, State.RECONNECTING],
	State.AUTHENTICATED: [State.IN_WORLD, State.DISCONNECTED, State.RECONNECTING],
	State.IN_WORLD: [State.DISCONNECTED, State.RECONNECTING, State.AUTHENTICATED],
	State.RECONNECTING: [State.CONNECTING, State.DISCONNECTED, State.AUTHENTICATED],
}

var _current: int = State.DISCONNECTED


func current() -> int:
	return _current


func set_current(state: int) -> void:
	if state >= State.DISCONNECTED and state <= State.RECONNECTING:
		_current = state


func can_transition(to: int) -> bool:
	if to == _current:
		return true
	var allowed: Array = _TRANSITIONS.get(_current, [])
	return allowed.has(to)


## Returns true when the transition was applied. Invalid transitions are
## refused so the state machine can never silently enter a bad state.
func transition(to: int) -> bool:
	if not can_transition(to):
		return false
	_current = to
	return true


## Escape hatch for hard resets (e.g. user logs out). Always allowed.
func reset() -> void:
	_current = State.DISCONNECTED


static func state_name(state: int) -> String:
	match state:
		State.DISCONNECTED:
			return "Disconnected"
		State.CONNECTING:
			return "Connecting"
		State.CONNECTED:
			return "Connected"
		State.AUTHENTICATED:
			return "Authenticated"
		State.IN_WORLD:
			return "InWorld"
		State.RECONNECTING:
			return "Reconnecting"
		_:
			return "Unknown"
