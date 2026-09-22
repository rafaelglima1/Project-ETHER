class_name ClientConfig
extends RefCounted

## Centralized client configuration.
##
## Endpoints are never hardcoded in gameplay classes: everything derives from
## the selected profile plus environment overrides. Two modes exist:
##   MOCK — offline development/CI (MockTransport + MockApiClient)
##   REAL — HTTPS/WSS against the deployed backend (Oracle Cloud)
##
## Environment:
##   ETHER_CLIENT_MODE        mock | real            (default mock)
##   ETHER_CLIENT_PROFILE     local | remote         (default local)
##   ETHER_CLIENT_API_URL     e.g. https://api.example
##   ETHER_CLIENT_WS_URL      e.g. wss://game.example/game

enum Mode { MOCK, REAL }
enum Profile { LOCAL, REMOTE }

var mode: int = Mode.MOCK
var profile: int = Profile.LOCAL

## HTTP surface (auth, accounts, characters, game token).
var api_base_url: String = "http://127.0.0.1:5052"

## Gameplay WebSocket endpoint.
var game_websocket_url: String = "ws://127.0.0.1:5000/game"

## Protocol envelope version (ADR-0003).
var protocol_version: int = 1

## Client ping interval / server timeout, in seconds (system.ping).
var heartbeat_interval_seconds: float = 20.0
var heartbeat_timeout_seconds: float = 60.0

## Reconnect backoff (infrastructure only; policy is a backend concern).
var reconnect_initial_delay_seconds: float = 1.0
var reconnect_max_delay_seconds: float = 15.0
var reconnect_max_attempts: int = 8

## Simulated round-trip latency used by the mock transport, in seconds.
var mock_latency_seconds: float = 0.05

## When true the client never crashes if the network is unavailable.
var offline_safe: bool = true


static func create_default() -> ClientConfig:
	return ClientConfig.new()


static func from_environment() -> ClientConfig:
	var config := ClientConfig.new()

	var requested_mode := OS.get_environment("ETHER_CLIENT_MODE").to_lower()
	if requested_mode == "real" or requested_mode == "websocket":
		config.mode = Mode.REAL
	elif requested_mode == "mock":
		config.mode = Mode.MOCK

	var requested_profile := OS.get_environment("ETHER_CLIENT_PROFILE").to_lower()
	if requested_profile == "remote":
		config.profile = Profile.REMOTE
	elif requested_profile == "local":
		config.profile = Profile.LOCAL

	config.apply_profile()

	var api := OS.get_environment("ETHER_CLIENT_API_URL")
	if api != "":
		config.api_base_url = api.trim_suffix("/")

	var ws := OS.get_environment("ETHER_CLIENT_WS_URL")
	if ws != "":
		config.game_websocket_url = ws

	return config


func apply_profile() -> void:
	# LOCAL: developer-run endpoints (never started by the client itself).
	# REMOTE: host comes exclusively from environment configuration.
	if profile == Profile.LOCAL:
		api_base_url = "http://127.0.0.1:5052"
		game_websocket_url = "ws://127.0.0.1:5000/game"
	elif profile == Profile.REMOTE:
		api_base_url = ""
		game_websocket_url = ""


func is_mock() -> bool:
	return mode == Mode.MOCK


func is_real() -> bool:
	return mode == Mode.REAL


func mode_name() -> String:
	return "mock" if mode == Mode.MOCK else "real"


func profile_name() -> String:
	return "local" if profile == Profile.LOCAL else "remote"


func has_endpoints() -> bool:
	return api_base_url != "" and game_websocket_url != ""


func duplicate_config() -> ClientConfig:
	var copy := ClientConfig.new()
	copy.mode = mode
	copy.profile = profile
	copy.api_base_url = api_base_url
	copy.game_websocket_url = game_websocket_url
	copy.protocol_version = protocol_version
	copy.heartbeat_interval_seconds = heartbeat_interval_seconds
	copy.heartbeat_timeout_seconds = heartbeat_timeout_seconds
	copy.reconnect_initial_delay_seconds = reconnect_initial_delay_seconds
	copy.reconnect_max_delay_seconds = reconnect_max_delay_seconds
	copy.reconnect_max_attempts = reconnect_max_attempts
	copy.mock_latency_seconds = mock_latency_seconds
	copy.offline_safe = offline_safe
	return copy
