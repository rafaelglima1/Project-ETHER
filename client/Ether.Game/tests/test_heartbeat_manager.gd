extends TestCase

## Heartbeat scheduling and staleness detection (Blueprint v5.0 §40).


func test_ping_fires_on_interval() -> void:
	var heartbeat := HeartbeatManager.new(1.0, 5.0)
	var fired := false
	for i in range(10):
		fired = fired or heartbeat.update(0.2)
	assert_true(fired, "should ping within the window")


func test_no_ping_before_interval() -> void:
	var heartbeat := HeartbeatManager.new(1.0, 5.0)
	assert_false(heartbeat.update(0.5))


func test_stale_after_timeout() -> void:
	var heartbeat := HeartbeatManager.new(1.0, 2.0)
	heartbeat.update(2.5)
	assert_true(heartbeat.is_stale())


func test_receive_resets_staleness() -> void:
	var heartbeat := HeartbeatManager.new(1.0, 2.0)
	heartbeat.update(1.5)
	heartbeat.note_received()
	assert_false(heartbeat.is_stale())
