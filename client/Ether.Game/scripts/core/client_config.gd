class_name ClientConfig
extends RefCounted

## Runtime configuration for the Godot client.
##
## Kept deliberately small and swappable so the same UI/world/state code runs
## against either a local mock backend or the real GameServer over WebSocket
## (Blueprint v5.0 §26: replace MockTransport with RealWebSocketTransport
## without rewriting gameplay code).

enum Mode { MOCK, WEBSOCKET }

## Transport/backend selection. Mock is the default so the client always boots.
var mode: int = Mode.MOCK

## HTTP surface (auth, accounts, characters) — Blueprint v5.0 §47/§48.
var api_base_url: String = "http://localhost:8080"

## Gameplay WebSocket endpoint — Blueprint v5.0 §35.
var websocket_url: String = "ws://localhost:8081/game"

## Protocol envelope version — Blueprint v5.0 §13/§35.
var protocol_version: int = 1

## Client ping interval in seconds — Blueprint v5.0 §40.
var heartbeat_interval_seconds: float = 20.0

## Server timeout used to detect a dead link — Blueprint v5.0 §40.
var heartbeat_timeout_seconds: float = 60.0

## Reconnect backoff (infrastructure only; final policy is a backend decision).
var reconnect_initial_delay_seconds: float = 1.0
var reconnect_max_delay_seconds: float = 15.0
var reconnect_max_attempts: int = 8

## Simulated round-trip latency used by the mock transport, in seconds.
var mock_latency_seconds: float = 0.05

## When true the client never tries to reach the network and never crashes.
var offline_safe: bool = true


static func create_default() -> ClientConfig:
	return ClientConfig.new()


static func from_environment() -> ClientConfig:
	var config := ClientConfig.new()
	var requested := OS.get_environment("ETHER_CLIENT_MODE")
	if requested == "websocket":
		config.mode = Mode.WEBSOCKET
	elif requested == "mock":
		config.mode = Mode.MOCK

	var api := OS.get_environment("ETHER_CLIENT_API_URL")
	if api != "":
		config.api_base_url = api

	var ws := OS.get_environment("ETHER_CLIENT_WS_URL")
	if ws != "":
		config.websocket_url = ws

	return config


func is_mock() -> bool:
	return mode == Mode.MOCK


func mode_name() -> String:
	return "mock" if mode == Mode.MOCK else "websocket"


func duplicate_config() -> ClientConfig:
	var copy := ClientConfig.new()
	copy.mode = mode
	copy.api_base_url = api_base_url
	copy.websocket_url = websocket_url
	copy.protocol_version = protocol_version
	copy.heartbeat_interval_seconds = heartbeat_interval_seconds
	copy.heartbeat_timeout_seconds = heartbeat_timeout_seconds
	copy.reconnect_initial_delay_seconds = reconnect_initial_delay_seconds
	copy.reconnect_max_delay_seconds = reconnect_max_delay_seconds
	copy.reconnect_max_attempts = reconnect_max_attempts
	copy.mock_latency_seconds = mock_latency_seconds
	copy.offline_safe = offline_safe
	return copy
