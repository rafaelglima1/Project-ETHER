class_name Transport
extends RefCounted

## Abstract text transport.
##
## Deliberately dumb: it moves strings in/out and reports lifecycle events.
## It knows nothing about JSON, commands, world state or the UI, so the same
## NetworkClient runs over MockTransport (local development) or
## WebSocketTransport (real GameServer) unchanged.

signal opened
signal closed(code: int, reason: String)
signal message_received(text: String)
signal transport_error(message: String)

var _open := false


## Begin connecting to the endpoint. Never blocks.
func connect_to(_endpoint: String) -> void:
	push_error("Transport.connect_to() must be overridden")


func send_text(_text: String) -> bool:
	return false


## Advance the transport. Must be called every frame (and from tests).
func poll(_delta: float) -> void:
	pass


func close(_code: int = 1000, _reason: String = "") -> void:
	_open = false


func is_open() -> bool:
	return _open


func transport_name() -> String:
	return "transport"
