# ADR-0003 — Realtime protocol (GameServer WebSocket)

- Status: Accepted
- Date: M4
- Context: GameServer realtime foundation
- Deciders: Project owner + engineering

## Context

The Blueprint described the realtime transport with slight variations over time.
With the first playable approaching, the backend must publish a single canonical
contract for the WebSocket connection so the Godot client can integrate against a
stable surface. **The backend is the authority of the realtime protocol.**

## Decision — envelope

Every frame is a JSON object with this shape:

```json
{
  "version": 1,
  "type": "command | event | error",
  "name": "semantic.message.name",
  "requestId": "uuid | null",
  "sequence": 123,
  "payload": {}
}
```

- `version` — protocol version. Breaking changes increment it. The server rejects
  unknown versions with `UNSUPPORTED_VERSION`.
- `type` — `command` (client→server), `event` (server→client), `error`
  (server→client).
- `name` — semantic identifier (see below).
- `requestId` — correlates a command with its outcome; echoed in the matching
  event/error. `null` for server-initiated events.
- `sequence` — monotonic per session, strictly increasing.
- `payload` — message-specific body (absent when the message has none).

Payloads use camelCase JSON. Unknown fields are ignored.

## Decision — messages

| Direction | Name | Payload |
| --- | --- | --- |
| C→S | `game.authenticate` | `{ gameToken }` |
| C→S | `world.enter` | `{}` |
| C→S | `movement.move` | `{ x, y }` |
| C→S | `system.ping` | `{}` |
| S→C | `game.authenticated` | `{ sessionId, accountId, characterId }` |
| S→C | `world.snapshot` | `{ mapId, width, height, player{characterId,x,y,state}, serverTime }` |
| S→C | `movement.accepted` | `{ characterId, mapId, x, y }` |
| S→C | `movement.rejected` | `{ code, message }` |
| S→C | `system.pong` | `{ serverTime }` |
| S→C | `protocol.error` | `{ code, message }` |

Rejections use `type = error` with a name matching the failed command
(`movement.rejected`, `world.enter.rejected`, `game.authenticate.rejected`).

## Decision — error codes

Deterministic codes only; never stack traces:
`INVALID_ENVELOPE`, `UNSUPPORTED_VERSION`, `UNKNOWN_MESSAGE`, `INVALID_PAYLOAD`,
`MESSAGE_TOO_LARGE`, `NOT_AUTHENTICATED`, `ALREADY_AUTHENTICATED`,
`INVALID_TOKEN`, `TOKEN_EXPIRED`, `WRONG_TOKEN_PURPOSE`, `NOT_AUTHORIZED`,
`NOT_IN_WORLD`, `ALREADY_IN_WORLD`, `INVALID_MAP`, `OUT_OF_BOUNDS`, `TOO_FAR`,
`INVALID_STATE`, `INVALID_SEQUENCE`, `RATE_LIMITED`, `SERVER_BUSY`,
`INTERNAL_ERROR`.

## Decision — authentication

The endpoint is `/game`. The connection starts in `Connecting`. The client sends
`game.authenticate` carrying a **game token** (from
`POST /characters/{id}/game-token`). The server validates signature, lifetime,
issuer/audience and the `use=game` claim. Access and refresh tokens are rejected
(`WRONG_TOKEN_PURPOSE`). The `accountId`/`characterId` used afterwards come from
the token, never from client input. Before authentication, no gameplay message is
accepted.

## Decision — session lifecycle

`Connecting → Authenticated → InWorld → Disconnecting → Disconnected`.

A session owns its identity, socket, heartbeat and sequence counter. Commands of a
single session are processed sequentially through a per-session queue, so a session
never races against itself while sessions remain independent (no global lock).

## Decision — sequence

The server tracks `LastReceivedSequence` per session. A command whose `sequence`
is not strictly greater than the last accepted one is rejected with
`INVALID_SEQUENCE`. This prevents duplicate/replayed commands. Documented here so
the behaviour is deterministic for clients.

## Decision — heartbeat and timeout

Clients send `system.ping` (responded with `system.pong`); any inbound message
refreshes the heartbeat. The server closes sessions silent for longer than
`GameServer:ConnectionTimeoutSeconds` (default 60s). Client ping interval default
20s. Both are configuration, not magic numbers.

## Decision — reconnect

Reconnect is intentionally simple in M4: the socket closes, the session is removed
and cleaned up, and the client connects again, re-authenticates with a fresh game
token and re-enters the world, receiving a fresh snapshot. A grace period that
preserves the in-memory session across a short disconnect (Blueprint
`DisconnectGrace`) is deferred to a later milestone.

## Decision — movement authority

The client sends an intent (`movement.move`). The server validates ownership,
session state, map and bounds, reuses the domain movement rule (`Character.MoveTo`)
and only then persists the authoritative position and answers `movement.accepted`.
The client never decides whether movement happened.

## Decision — limits and safety

`GameServer:MaxMessageSizeBytes` (default 16 KB) bounds frames; oversized frames
close the connection with `MESSAGE_TOO_BIG`. `MaxConnections` bounds concurrency
(`SERVER_BUSY`/503). `MaxMovementCommandsPerSecond` provides basic per-session
rate limiting (`RATE_LIMITED`). Malformed JSON produces `protocol.error` without
terminating the process.

## Client compatibility note

The frozen Godot client (`bf32519`) contains tolerance for older envelope variants.
That tolerance is **not** removed here (the client is out of backend scope), but the
official server contract is the one defined in this ADR; new client work must target
it.

## Consequences

- One stable, versioned contract for the realtime surface.
- Transport stays thin: authenticate → deserialize → dispatch → serialize.
- New commands (combat, inventory, …) plug into the dispatcher without touching
  the transport.
