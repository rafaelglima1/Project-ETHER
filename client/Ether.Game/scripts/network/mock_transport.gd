class_name MockTransport
extends Transport

## Local, in-process transport backed by MockBackend.
##
## Simulates round-trip latency so the client exercises the same async paths as
## the real WebSocket transport. Because the backend is preserved across
## reconnects, the mock can demonstrate session/world restoration.

const DEFAULT_LATENCY := 0.05

var _backend: MockBackend
var _latency: float
var _clock: float = 0.0
var _endpoint: String = ""
var _pending_open: float = -1.0
var _outbox: Array = []


func _init(backend: MockBackend = null, latency: float = DEFAULT_LATENCY) -> void:
	_backend = backend if backend != null else MockBackend.new()
	_latency = maxf(0.0, latency)


func backend() -> MockBackend:
	return _backend


func connect_to(endpoint: String) -> void:
	_endpoint = endpoint
	_open = false
	_pending_open = _clock + _latency


func poll(delta: float) -> void:
	_clock += delta

	if _pending_open >= 0.0 and _clock >= _pending_open:
		_pending_open = -1.0
		_open = true
		opened.emit()
		for envelope in _backend.on_connect():
			_queue(envelope)

	if _open:
		for envelope in _backend.tick(delta):
			_queue(envelope)

	while not _outbox.is_empty() and float(_outbox[0]["time"]) <= _clock:
		var item: Dictionary = _outbox.pop_front()
		message_received.emit(String(item["text"]))


func send_text(text: String) -> bool:
	if not _open:
		return false
	for envelope in _backend.handle_text(text):
		_queue(envelope)
	return true


func close(code: int = 1000, reason: String = "") -> void:
	if not _open:
		return
	_open = false
	_backend.on_disconnect()
	closed.emit(code, reason)


func transport_name() -> String:
	return "mock"


func _queue(envelope: Dictionary) -> void:
	_outbox.append({"time": _clock + _latency, "text": JSON.stringify(envelope)})
