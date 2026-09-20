class_name WebSocketTransport
extends Transport

## Real gameplay transport (Blueprint v5.0 §35) over raw WebSockets.
##
## A fresh WebSocketPeer is created on every connect attempt so reconnect is a
## clean handshake. No JSON/command knowledge lives here.

var _socket: WebSocketPeer = null
var _endpoint: String = ""
var _started := false
var _notified_closed := false


func connect_to(endpoint: String) -> void:
	_endpoint = endpoint
	_started = true
	_notified_closed = false
	_open = false
	_socket = WebSocketPeer.new()
	var err := _socket.connect_to_url(endpoint)
	if err != OK:
		transport_error.emit("WebSocket connect failed (error %d) for %s" % [err, endpoint])


func poll(_delta: float) -> void:
	if not _started or _socket == null:
		return

	_socket.poll()
	var socket_state := _socket.get_ready_state()

	if socket_state == WebSocketPeer.STATE_OPEN:
		if not _open:
			_open = true
			opened.emit()
		while _socket.get_available_packet_count() > 0:
			var packet := _socket.get_packet()
			message_received.emit(packet.get_string_from_utf8())
	elif socket_state == WebSocketPeer.STATE_CLOSED:
		_open = false
		if not _notified_closed:
			_notified_closed = true
			closed.emit(_socket.get_close_code(), _socket.get_close_reason())


func send_text(text: String) -> bool:
	if _socket == null or _socket.get_ready_state() != WebSocketPeer.STATE_OPEN:
		return false
	return _socket.send_text(text) == OK


func close(code: int = 1000, reason: String = "") -> void:
	if _socket != null:
		_socket.close(code, reason)
	_started = false
	_open = false


func is_open() -> bool:
	if _socket == null:
		return false
	return _socket.get_ready_state() == WebSocketPeer.STATE_OPEN


func transport_name() -> String:
	return "websocket"
