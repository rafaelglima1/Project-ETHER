# Project Ether — Godot Client (`client/Ether.Game`)

Mobile-first 2D MMORPG client for Project Ether. Godot 4.x, **GDScript**
(Blueprint v5.0 §2/§49). The client is *presentation + intent only* — identity,
position, world state, creature AI, health and damage all come from the backend.

**Current milestone: M6 — creatures + AI** (built on the M4 realtime foundation
and the M5 combat foundation). It runs the full loop against the real backend
over HTTPS/WSS and keeps an offline mock (with the same contract) for
development/CI.

---

## Flow

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
  │  ◀── world.snapshot      { mapId, width, height, player, creatures[] }
  │  ──▶ movement.move       { x, y }        ◀── movement.accepted { characterId, mapId, x, y }
  │  ──▶ combat.attack       { abilityId, targetId, targetType }
  │  ◀── combat.result       { attackerId, targetId, damage, critical, targetHealth, targetMaxHealth, targetState, targetDefeated, attackerType, targetType }
  │  ◀── world.creature_moved { creatureId, mapId, x, y, health, maxHealth, state }
  │
  │  (death -> overlay, no commands)
  │  ──▶ character.respawn   {}      ◀── world.snapshot (success)
  │                              ◀── character.respawn.rejected { CHARACTER_NOT_DEAD }
  │
  │  snapshot.inventory[]  ◀──  GET /characters/{id}/inventory  (authoritative)
```

The client never moves itself ahead of `movement.accepted`, never runs creature
AI, never computes damage/HP/death, and never restores HP/position on respawn.
Creature attacks arrive as ordinary `combat.result` messages with the creature
as `attackerId`; respawn and inventory always come from the server.

See [`CONTRACTS.md`](CONTRACTS.md) for the full wire contract (M4 envelope, M5
combat, M6 creature replication, M7 progression/loot, respawn, M8 inventory).

---

## Run

Requires Godot **4.x** (validated with 4.7.2).

```bash
# Offline (mock) — default
godot --path client/Ether.Game

# Real backend
ETHER_CLIENT_MODE=real ETHER_CLIENT_PROFILE=remote \
ETHER_CLIENT_API_URL=https://<api-host> ETHER_CLIENT_WS_URL=wss://<game-host>/game \
godot --path client/Ether.Game
```

Configuration is centralized in `scripts/core/client_config.gd`
(`ETHER_CLIENT_MODE`, `ETHER_CLIENT_PROFILE`, `ETHER_CLIENT_API_URL`,
`ETHER_CLIENT_WS_URL`). `ETHER_CLIENT_DEBUG=1` shows the diagnostics overlay
(toggle any time with **F3**). Credentials are never logged or displayed.

### Controls
- Tap / left click a tile = move; tap a creature = target it (and attack when adjacent).
- `WASD` / arrows = one-tile steps (desktop dev convenience).
- Buttons: **Attack** (`warrior.basic_attack`), **Power Strike** (`warrior.power_strike`).

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
                                       Scenes + HUD + creature views + overlay
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
    input/     input_controller
    camera/    camera_controller
    ui/        login_screen, character_select_screen, hud, debug_overlay
  tests/       test_runner + suites + real_e2e harness
```

### Server authority
`WorldState` is written only from server payloads. Movement, combat results and
creature lifecycle events all update the mirror; the client only sends intent.
Creature AI states (Idle/Chase/Attack/Return/Dead/Respawning) are rendered, not
decided. Dead creatures are excluded from targeting client-side too.

### Mock ↔ real seam
`ClientConfig.mode` selects `MockTransport`+`MockApiClient` (`MOCK`) or
`WebSocketTransport`+`HttpApiClient` (`REAL`). `MockBackend` implements the
identical contract including server-role damage, criticals, creature AI and
respawn, so the loop above runs offline with no code changes above the transport.

---

## Tests

```bash
godot --headless --path client/Ether.Game --script res://tests/test_runner.gd
```

Covers (128 headless tests): canonical envelope/hostile input/GUID
requestId/sequence, state machine, heartbeat, reconnect, mock transport round
trips, canonical snapshot + creature ingestion, movement acceptance/rejection,
combat damage/death/cooldown/range rejections, creature AI chase+attack,
**death state + input gating + respawn (incl. reconnect-while-dead)**, **M8
inventory (snapshot/HTTP/stacking/reconnect/UI)**, duplicate-frame idempotency,
stale snapshot replacement, and Android resume→reconnect.

### Real end-to-end (against the deployed backend)

```bash
ETHER_CLIENT_MODE=real ETHER_CLIENT_PROFILE=remote \
ETHER_CLIENT_API_URL=https://game.rotagov.com.br \
ETHER_CLIENT_WS_URL=wss://game.rotagov.com.br/game \
ETHER_E2E_EMAIL=... ETHER_E2E_PASSWORD=... \
godot --headless --path client/Ether.Game --script res://tests/real_e2e.gd
```

11 stages; proves the loop end-to-end on Oracle and validates `character.respawn`
(negative path) plus authoritative inventory over HTTP. Never starts a local
backend; exits `2` (BLOCKED) if endpoints are not configured.

---

## Contract

The client implements the backend contract exactly: ADR-0003 realtime envelope,
ADR-0004 combat, ADR-0005 creatures + AI, ADR-0006 progression/loot, plus the
frozen **respawn** (`character.respawn`, backend `b68acc7`) and **M8 inventory**
(`world.snapshot.inventory[]` + `GET /characters/{id}/inventory`, backend
`d0aec66`). Full details in [`CONTRACTS.md`](CONTRACTS.md).
pending. `BACKEND_FILES_CHANGED: NONE` — no file outside `client/` was touched.

---

## Scope

Implemented: authentication, characters, game token, realtime transport, world
enter, snapshot, server-authoritative movement, combat (`combat.attack` →
`combat.result`), creatures + AI replication/rendering, progression (XP/level)
and loot, **death state + authoritative respawn**, **authoritative inventory
(snapshot + HTTP + mobile modal)**, heartbeat, reconnect, Android
pause/resume reconnection, offline mock.

**Not implemented** (out of scope): equipment, quests, NPCs, chat, party, guild,
PvP, market, crafting, monetization.

### Validation (client)

- **128/128** headless tests (protocol, transport, robustness, state, combat, AI,
  progression, death/respawn, inventory, hardening).
- Autoplay mock smoke: `kill → loot=1 → died → respawned → dead=false hp=100
  pos=(0,0) creatures=3 inv=1`.
- `tests/real_e2e.gd` against Oracle: **11/11 PASS** (login → character →
  game-token → WSS → authenticate → snapshot → respawn verified → movement →
  combat → defeat → XP/loot → authoritative inventory HTTP).
- Oracle: `https://game.rotagov.com.br` / `wss://game.rotagov.com.br/game`.
- Android APK: `com.rotagov.ether` debug export (see build section).
