# Project Ether — Godot Client (`client/Ether.Game`)

Mobile-first 2D MMORPG client for Project Ether. Godot 4.x, **GDScript**
(Blueprint v5.0 §2/§49). The client is *presentation + intent only* — every
authoritative value (identity, position, world state) comes from the backend.

**Current milestone: M4 — real GameServer integration.** The client implements
the canonical realtime protocol (ADR-0003) and runs the full flow against the
real backend over HTTPS/WSS, while keeping an offline mock for development/CI.

---

## The M4 flow

```
Godot
  │  POST /auth/login                  (email, password)
  │  GET  /accounts/{id}/characters
  │  POST /characters/{id}/game-token
  ▼
gameToken
  │  WSS  <game-host>/game
  │  ──▶ game.authenticate  { gameToken }
  │  ◀── game.authenticated  { sessionId, accountId, characterId }
  │  ──▶ world.enter         {}
  │  ◀── world.snapshot      { mapId, width, height, player { characterId, x, y, state }, serverTime }
  │  ──▶ movement.move       { x, y }
  │  ◀── movement.accepted   { characterId, mapId, x, y }
```

The client never moves itself ahead of `movement.accepted`, never adopts a
character the server did not authenticate, and never sends combat commands
(M4 has no M5 combat).

---

## Run

Requires Godot **4.x** (validated with 4.7.2).

```bash
# Offline (mock) — default
godot --path client/Ether.Game

# Real backend
ETHER_CLIENT_MODE=real \
ETHER_CLIENT_PROFILE=remote \
ETHER_CLIENT_API_URL=https://<api-host> \
ETHER_CLIENT_WS_URL=wss://<game-host>/game \
godot --path client/Ether.Game
```

### Configuration (centralized in `scripts/core/client_config.gd`)

| Env var | Values | Default |
| --- | --- | --- |
| `ETHER_CLIENT_MODE` | `mock` \| `real` | `mock` |
| `ETHER_CLIENT_PROFILE` | `local` \| `remote` | `local` |
| `ETHER_CLIENT_API_URL` | e.g. `https://api.example` | local preset |
| `ETHER_CLIENT_WS_URL` | e.g. `wss://game.example/game` | local preset |

`PROFILE=local` presets developer endpoints; `remote` requires explicit URLs
(no domain is hardcoded). `ETHER_CLIENT_DEBUG=1` shows the diagnostics overlay
(toggle any time with **F3**). Secrets are never logged or shown.

### Desktop development input
- Left click / tap = move (tap-to-move); tap a tile to send `movement.move`.
- `WASD` / arrows = one-tile steps (development convenience; mobile-first).

---

## Architecture

```
Transport ──► ProtocolSerializer ──► EventDispatcher ──► typed signals
   ▲                                                        │
   │ (MockTransport | WebSocketTransport)                   ▼
CommandSender ◄──────────── NetworkClient ────────────► GameClient (autoload)
                                                          │
                                       ClientState / WorldState (mirrors)
                                                          │
                                                    Scenes + HUD + DebugOverlay
```

```
client/Ether.Game/
  project.godot                 main scene = scenes/bootstrap/bootstrap.tscn
  scenes/  bootstrap, auth, character, world, ui
  scripts/
    core/      client_config, app_state, game_client (autoload), bootstrap
    network/   transport, mock_transport, websocket_transport, protocol_messages,
               protocol_serializer, protocol_errors, command_sender,
               event_dispatcher, heartbeat_manager, reconnect_manager,
               network_client, api_client, mock_api_client, http_api_client,
               mock_backend
    state/     client_state, world_state, snapshot_processor, delta_processor
    world/     world_scene
    entities/  entity_view, player, creature
    combat/    combat_controller        (intent only; inert in M4)
    input/     input_controller
    camera/    camera_controller
    ui/        login_screen, character_select_screen, hud, debug_overlay
  tests/       test_runner + suites + real_e2e harness
```

### Session state machine
`Disconnected → Connecting → Connected → Authenticated → InWorld`, plus
`Reconnecting`. Illegal transitions are refused (`scripts/core/app_state.gd`).

### Protocol (canonical, `scripts/network/protocol_messages.gd`)
- Envelope: `{ version, type, name, requestId, sequence, payload }`, camelCase.
- Commands: `game.authenticate`, `world.enter`, `movement.move`, `system.ping`.
- Events: `game.authenticated`, `world.snapshot`, `movement.accepted`, `system.pong`.
- Errors: `protocol.error`, `movement.rejected`, `world.enter.rejected`,
  `game.authenticate.rejected` with deterministic `code`/`message`.

`sequence` is a single monotonic per-session counter (`CommandSender`); each
command gets a fresh GUID `requestId` (the server deserializes it as `Guid?`).
`ProtocolErrors.user_message()` maps codes to non-technical UI text.

### Server authority
`WorldState` is written only from server payloads. `world.snapshot` sets the
authoritative position; `movement.accepted` updates it; movement for a different
character/map is ignored. Reconnect simply rebuilds the session
(`game.authenticate → world.enter → world.snapshot`) — no fake grace period.

### Mock ↔ real seam
`ClientConfig.mode` selects the transport/API pair in `game_client.gd`:

| mode | transport | API |
| --- | --- | --- |
| `MOCK` | `MockTransport` (`MockBackend`) | `MockApiClient` |
| `REAL` | `WebSocketTransport` | `HttpApiClient` |

`MockBackend` implements the identical canonical contract, so the flow above is
exercised offline with no code changes above the transport layer.

---

## Tests

Zero-dependency suites; run headless (exits non-zero on failure):

```bash
godot --headless --path client/Ether.Game --script res://tests/test_runner.gd
```

Covers: canonical envelope + hostile input + GUID requestId + sequence,
state machine, heartbeat, reconnect backoff, mock transport round trips
(authenticate/snapshot/movement/ping/rejections/invalid sequence), canonical
snapshot & movement acceptance/rejection, and the full mock playable flow
including reconnect.

### Real end-to-end (against the deployed backend)

```bash
ETHER_CLIENT_MODE=real ETHER_CLIENT_PROFILE=remote \
ETHER_CLIENT_API_URL=https://<api-host> ETHER_CLIENT_WS_URL=wss://<game-host>/game \
ETHER_E2E_EMAIL=hero@example.com ETHER_E2E_PASSWORD=... \
godot --headless --path client/Ether.Game --script res://tests/real_e2e.gd
```

Prints `1/8 … 8/8` step evidence and `REAL_E2E: PASS`. It never starts a local
backend; if the Oracle endpoint is unreachable it exits `2` (BLOCKED).

---

## Backend findings

None required a client-side workaround; the client was adapted to the canonical
contract. Two contract clarifications were resolved by the backend M4 (ADR-0003)
and are now implemented exactly:

- Envelope uses `type` = category plus a semantic `name` (former §13/§35 ambiguity).
- Auth/character/game-token HTTP surface (`/auth/*`, `/accounts/*/characters`,
  `/characters/{id}/game-token`).

`BACKEND_FILES_CHANGED: NONE` — no file outside `client/` was touched.

---

## Scope (M4)

Implemented: authentication, characters, game token, real WebSocket,
world enter, snapshot, movement (server-authoritative), heartbeat, reconnect,
plus the retained offline mock. **Not implemented** (out of scope): combat,
creatures, AI, XP, loot, inventory, quests, NPCs, chat, guild, party, PvP,
market, crafting, monetization. The HUD/attack affordance is inert by design.
