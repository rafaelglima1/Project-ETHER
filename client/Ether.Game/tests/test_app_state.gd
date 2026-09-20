extends TestCase

## Connection/session state machine transitions.


func test_starts_disconnected() -> void:
	var state := AppState.new()
	assert_eq(state.current(), AppState.State.DISCONNECTED)


func test_valid_transitions() -> void:
	var state := AppState.new()
	assert_true(state.transition(AppState.State.CONNECTING))
	assert_true(state.transition(AppState.State.CONNECTED))
	assert_true(state.transition(AppState.State.AUTHENTICATED))
	assert_true(state.transition(AppState.State.IN_WORLD))
	assert_eq(state.current(), AppState.State.IN_WORLD)


func test_invalid_transition_is_refused() -> void:
	var state := AppState.new()
	assert_false(state.transition(AppState.State.IN_WORLD))
	assert_eq(state.current(), AppState.State.DISCONNECTED)


func test_reconnecting_from_in_world() -> void:
	var state := AppState.new()
	state.transition(AppState.State.CONNECTING)
	state.transition(AppState.State.CONNECTED)
	state.transition(AppState.State.AUTHENTICATED)
	state.transition(AppState.State.IN_WORLD)
	assert_true(state.transition(AppState.State.RECONNECTING))
	assert_true(state.transition(AppState.State.CONNECTING))


func test_reset_returns_to_disconnected() -> void:
	var state := AppState.new()
	state.transition(AppState.State.CONNECTING)
	state.reset()
	assert_eq(state.current(), AppState.State.DISCONNECTED)


func test_state_names() -> void:
	assert_eq(AppState.state_name(AppState.State.IN_WORLD), "InWorld")
	assert_eq(AppState.state_name(AppState.State.RECONNECTING), "Reconnecting")
