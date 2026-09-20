extends TestCase

## Reconnect backoff scheduling.


func test_start_is_active() -> void:
	var manager := ReconnectManager.new(1.0, 10.0, 3)
	manager.start()
	assert_true(manager.is_active())
	assert_false(manager.update(0.5), "should wait")


func test_fires_after_delay() -> void:
	var manager := ReconnectManager.new(1.0, 10.0, 3)
	manager.start()
	assert_true(manager.update(1.5), "should attempt after delay")


func test_backoff_grows() -> void:
	var manager := ReconnectManager.new(1.0, 10.0, 5)
	manager.start()
	var first := manager.current_delay()
	manager.update(2.0)
	manager.on_attempt_failed()
	var second := manager.current_delay()
	assert_true(second > first, "delay should increase")


func test_backoff_is_capped() -> void:
	var manager := ReconnectManager.new(1.0, 4.0, 10)
	manager.start()
	for i in range(6):
		manager.on_attempt_failed()
	assert_true(manager.current_delay() <= 4.0, "delay should be capped")


func test_exhausts_after_max_attempts() -> void:
	var manager := ReconnectManager.new(0.1, 1.0, 3)
	manager.start()
	manager.on_attempt_failed()
	manager.on_attempt_failed()
	manager.on_attempt_failed()
	assert_true(manager.is_exhausted())
	assert_false(manager.is_active())


func test_success_resets() -> void:
	var manager := ReconnectManager.new(1.0, 10.0, 3)
	manager.start()
	manager.on_attempt_failed()
	manager.on_attempt_succeeded()
	assert_false(manager.is_active())
	assert_eq(manager.attempts(), 0)
