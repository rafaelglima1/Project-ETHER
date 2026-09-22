class_name NetworkClient
extends Node

## Owns the transport and the gameplay protocol pipeline:
##
##   Transport -> ProtocolSerializer -> EventDispatcher -> typed signals
##
## Commands flow the other way through CommandSender (canonical M4 commands:
## game.authenticate, world.enter, movement.move, system.ping). UI/world/state
## code never touches WebSocket or JSON directly.

signal state_changed(previous: int, current: int)
signal server_connected
signal server_disconnected(reason: String)
signal event_received(name: String, payload: Dictionary)
signal snapshot_received(payload: Dictionary)
signal error_received(name: String, code: String, message: String)
signal log_message(text: String)
signal pong_received(rtt_ms: int)

var config: ClientConfig = null
var state := AppState.new()
var serializer := ProtocolSerializer.new()
var dispatcher := EventDispatcher.new()
var sender: CommandSender = null
var heartbeat: HeartbeatManager = null
var reconnect: ReconnectManager = null

var last_rtt_ms: int = -1

var _transport: Transport = null
var _endpoint: String = ""
var _reconnecting := false
var _manual_disconnect := false
var _ping_sent_msec: int = -1


func _init() -> void:
	dispatcher.event_received.connect(_forward_event)
	dispatcher.snapshot_received.connect(_forward_snapshot)
	dispatcher.error_received.connect(_forward_error)


func configure(p_config: ClientConfig) -> void:
	config = p_config
	sender = CommandSender.new(serializer)
	sender.send_failed.connect(_on_send_failed)
	heartbeat = HeartbeatManager.new(config.heartbeat_interval_seconds, config.heartbeat_timeout_seconds)
	reconnect = ReconnectManager.new(
		config.reconnect_initial_delay_seconds,
		config.reconnect_max_delay_seconds,
		config.reconnect_max_attempts)
	set_transport(_make_transport())


func _make_transport() -> Transport:
	if config != null and config.mode == ClientConfig.Mode.REAL:
		return WebSocketTransport.new()
	var latency := config.mock_latency_seconds if config != null else 0.05
	return MockTransport.new(null, latency)


func set_transport(transport: Transport) -> void:
	if _transport != null:
		_detach_transport(_transport)
	_transport = transport
	if sender == null:
		sender = CommandSender.new(serializer)
	sender.set_transport(_transport)
	_attach_transport(_transport)


func transport() -> Transport:
	return _transport


func connect_to_server(endpoint: String = "") -> void:
	if config == null:
		configure(ClientConfig.create_default())
	# Idempotent: never tear down a live/opening session by reconnecting.
	if not _manual_disconnect and (state.current() == AppState.State.CONNECTING or is_connected_to_server()):
		return
	_manual_disconnect = false
	_reconnecting = false
	_endpoint = endpoint if endpoint != "" else config.game_websocket_url
	sender.reset_sequence()
	_ping_sent_msec = -1
	if heartbeat != null:
		heartbeat.reset()
	_transition(AppState.State.CONNECTING)
	_transport.connect_to(_endpoint)


func disconnect_from_server() -> void:
	_manual_disconnect = true
	_reconnecting = false
	if reconnect != null:
		reconnect.stop()
	if _transport != null:
		_transport.close()
	_transition(AppState.State.DISCONNECTED)


func is_connected_to_server() -> bool:
	return _transport != null and _transport.is_open()


func is_reconnecting() -> bool:
	return _reconnecting


func mark_authenticated() -> void:
	_transition(AppState.State.AUTHENTICATED)


func mark_in_world() -> void:
	_transition(AppState.State.IN_WORLD)


func reset_state() -> void:
	state.reset()


# --- Canonical commands -------------------------------------------------------

func authenticate(game_token: String) -> String:
	return sender.send(ProtocolMessages.CMD_GAME_AUTHENTICATE, {"gameToken": game_token})


func enter_world() -> String:
	return sender.send(ProtocolMessages.CMD_WORLD_ENTER, {})


func move_to(x: int, y: int) -> String:
	return sender.send(ProtocolMessages.CMD_MOVEMENT_MOVE, {"x": x, "y": y})


func ping() -> String:
	return sender.send(ProtocolMessages.CMD_SYSTEM_PING, {})


## Escape hatch used only by the local mock for offline extras (never the real
## server). Keeps gameplay code from learning WebSocket details.
func send_command(command: String, payload: Dictionary = {}) -> String:
	return sender.send(command, payload)


# --- Engine loop --------------------------------------------------------------

func _process(delta: float) -> void:
	if _transport == null or config == null:
		return

	_transport.poll(delta)

	if _reconnecting and reconnect.update(delta):
		log_message.emit("Reconnect attempt %d" % (reconnect.attempts() + 1))
		_transport.connect_to(_endpoint)

	if _transport.is_open() and heartbeat != null:
		if heartbeat.update(delta):
			_ping_sent_msec = Time.get_ticks_msec()
			ping()
		if heartbeat.is_stale():
			log_message.emit("No traffic for %.0fs; reconnecting" % heartbeat.time_since_receive())
			_begin_reconnect()


# --- Transport callbacks ------------------------------------------------------

func _on_opened() -> void:
	if heartbeat != null:
		heartbeat.reset()
	if _reconnecting:
		_reconnecting = false
		if reconnect != null:
			reconnect.on_attempt_succeeded()
		_transition(AppState.State.CONNECTING)
		log_message.emit("Reconnected to %s" % _transport.transport_name())
	else:
		_transition(AppState.State.CONNECTED)
		log_message.emit("Connected to %s (%s)" % [_transport.transport_name(), _endpoint])
	server_connected.emit()


func _on_closed(code: int, reason: String) -> void:
	if state.current() == AppState.State.DISCONNECTED:
		return
	log_message.emit("Connection closed (%d %s)" % [code, reason])
	server_disconnected.emit(reason)
	if _manual_disconnect or config == null or config.reconnect_max_attempts <= 0:
		_transition(AppState.State.DISCONNECTED)
	else:
		_begin_reconnect()


func _on_message(text: String) -> void:
	if heartbeat != null:
		heartbeat.note_received()

	var result := serializer.decode(text)
	if not bool(result.get("ok", false)):
		log_message.emit("Ignoring malformed message: %s" % String(result.get("error", "")))
		return

	var envelope: Dictionary = result["envelope"]
	if String(envelope.get("name", "")) == ProtocolMessages.EVT_SYSTEM_PONG:
		_handle_pong(String(envelope.get("requestId", "")))
	dispatcher.dispatch(envelope)


func _on_transport_error(message: String) -> void:
	log_message.emit(message)
	error_received.emit(ProtocolMessages.ERR_PROTOCOL, ProtocolMessages.CODE_INTERNAL_ERROR, message)


func _on_send_failed(command: String, reason: String) -> void:
	log_message.emit("Command '%s' not sent: %s" % [command, reason])


func _handle_pong(request_id: String) -> void:
	if _ping_sent_msec < 0:
		return
	if request_id != "" and sender != null and request_id != sender.last_request_id:
		# Not the most recent ping; still a valid heartbeat.
		pass
	last_rtt_ms = Time.get_ticks_msec() - _ping_sent_msec
	_ping_sent_msec = -1
	pong_received.emit(last_rtt_ms)


func _begin_reconnect() -> void:
	if _reconnecting:
		return
	_reconnecting = true
	if reconnect != null:
		reconnect.start()
	_transition(AppState.State.RECONNECTING)


func _transition(to: int) -> void:
	var previous := state.current()
	if previous == to:
		return
	if state.transition(to):
		state_changed.emit(previous, to)


func _attach_transport(transport: Transport) -> void:
	transport.opened.connect(_on_opened)
	transport.closed.connect(_on_closed)
	transport.message_received.connect(_on_message)
	transport.transport_error.connect(_on_transport_error)


func _detach_transport(transport: Transport) -> void:
	if transport.opened.is_connected(_on_opened):
		transport.opened.disconnect(_on_opened)
	if transport.closed.is_connected(_on_closed):
		transport.closed.disconnect(_on_closed)
	if transport.message_received.is_connected(_on_message):
		transport.message_received.disconnect(_on_message)
	if transport.transport_error.is_connected(_on_transport_error):
		transport.transport_error.disconnect(_on_transport_error)


func _forward_event(name: String, payload: Dictionary) -> void:
	event_received.emit(name, payload)


func _forward_snapshot(payload: Dictionary) -> void:
	snapshot_received.emit(payload)


func _forward_error(name: String, code: String, message: String, _request_id: String) -> void:
	error_received.emit(name, code, message)
