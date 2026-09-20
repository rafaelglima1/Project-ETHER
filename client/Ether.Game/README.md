# Project Ether — Godot Client (`client/Ether.Game`)

Mobile-first 2D MMORPG client for Project Ether. Godot 4.x, **GDScript**
(Blueprint v5.0 §2/§49). The client is *presentation + intent only* — every
authoritative value (HP, XP, gold, damage, loot, movement outcome, death) comes
from the server (Blueprint v5.0 §89).

This is the foundation for the **First Playable** track. It boots and runs the
complete loop offline against a mock backend, and the same gameplay/UI layers run
unchanged against the real GameServer over WebSocket.

---

## Run

Requires Godot **4.x** (validated with 4.7.2).

```bash
# Open the project
godot --path client/Ether.Game

# Headless boot smoke (no window)
godot --headless --path client/Ether.Game --quit-after 180
```

Press **Enter World** on the login screen: the mock path signs in, lists a
character, enters the world, and lets you move by tapping the ground, tapping a
creature to target, and pressing **Attack**.

### Desktop development input
- Left mouse click = tap-to-move / tap target.
- Right mouse click = clear target.
- `WASD` / arrow keys = one-tile steps (development convenience only; mobile is
  the primary target).

### Automated smoke (`ETHER_AUTOPLAY`)
Set `ETHER_AUTOPLAY=1` to drive login → character → world → combat without input
(used for headless validation):

```bash
ETHER_AUTOPLAY=1 godot --headless --path client/Ether.Game --fixed-fps 60 --quit-after 1200
```

---

## Tests

Zero-dependency suite (no addons). Runs headless and exits non-zero on failure:

```bash
godot --headless --path client/Ether.Game --script res://tests/test_runner.gd
```

Covers: envelope encode/decode incl. hostile input, connection state machine,
heartbeat, reconnect backoff, mock transport round trips, snapshot/delta
application, and the full mock playable flow (connect → auth → character →
world → move → attack → XP → loot → inventory → disconnect → reconnect).

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
                                                    Scenes + HUD
```

```
client/Ether.Game/
  project.godot                 main scene = scenes/bootstrap/bootstrap.tscn
  scenes/                       bootstrap, auth, character, world, ui
  scripts/
    core/       client_config, app_state, game_client (autoload), bootstrap
    network/    transport, mock_transport, websocket_transport,
                protocol_serializer, protocol_messages, command_sender,
                event_dispatcher, heartbeat_manager, reconnect_manager,
                network_client, api_client, mock_api_client,
                http_api_client, mock_backend
    state/      client_state, world_state, snapshot_processor, delta_processor
    world/      world_scene
    entities/   entity_view, player, creature
    combat/     combat_controller
    input/      input_controller
    camera/     camera_controller
    ui/         login_screen, character_select_screen, hud
  tests/        test_runner + suites
```

### State machine
`Disconnected → Connecting → Connected → Authenticated → InWorld`, plus
`Reconnecting`. Invalid transitions are refused (`scripts/core/app_state.gd`).

### Server authority
The client updates its mirrors **only** from server payloads:
- `WorldSnapshot` / `WorldDelta` → `WorldState`
- `CharacterMoved` / `CreatureMoved` / `EntityDeath` / `CombatResult` /
  `ExperienceGained` / `LevelUp` / `LootReceived` → applied to `WorldState` /
  `ClientState`, then re-emitted as UI signals.

`CombatController` only *requests* attacks. No damage, XP, loot or ownership is
computed client-side. XP progress is rendered from a server-provided
`experienceToNext`, never from a client-side curve.

### Mock ↔ real seam
`ClientConfig.mode` selects the transport/API pair in
`scripts/core/game_client.gd`:

| mode        | transport            | API                   |
| ----------- | -------------------- | --------------------- |
| `MOCK`      | `MockTransport`      | `MockApiClient`       |
| `WEBSOCKET` | `WebSocketTransport` | `HttpApiClient`       |

Nothing above the transport layer changes when switching. `MockBackend` plays the
*server* role (it computes damage/XP/loot precisely so the client doesn't have
to). Environment overrides: `ETHER_CLIENT_MODE`, `ETHER_CLIENT_API_URL`,
`ETHER_CLIENT_WS_URL`.

---

## Wire envelope (client interpretation)

Blueprint v5.0 §13/§15 show a command with a top-level `type` carrying the
command name; §35 defines the envelope `type` as the *category*
(`command|event|snapshot|delta|error`). The client treats **§35 as
authoritative** (later section wins) and adds a `name` field for the concrete
command/event:

```json
{ "version": 1, "type": "command", "name": "Move",
  "requestId": "uuid", "sequence": 154, "payload": { "x": 105, "y": 87 } }
```

The client tolerates servers that place the concrete name in `type` and omit
`name` (§13 shape) — `MockBackend` accepts both, and `EventDispatcher` /
`SnapshotProcessor` / `DeltaProcessor` normalize both. Commands carry a monotonic
per-session `sequence`.

---

## Backend contract requests

> **BACKEND_CONTRACT_REQUEST**

```text
AGENT_ID: OPENCODE-2
ROLE: GODOT-CLIENT

REQUEST:
Confirm the canonical gameplay envelope and the auth/character HTTP surface.

REASON:
Blueprint v5.0 §13/§15 and §35 describe the envelope differently, and the M1
backend currently exposes an unauthenticated /accounts/*/*characters surface
that differs from Blueprint §47/§48 (/auth/login, /characters, ...). The client
cannot pin its DTOs until the contract is finalized.

EXPECTED_CONTRACT:
1) Envelope: { version, type(category), name(concrete), requestId, sequence,
   payload } — or an explicit alternative.
2) HTTP: POST /auth/register, POST /auth/login, POST /auth/refresh,
   GET /characters, POST /characters, POST /characters/{id}/select.
3) GameServer WebSocket path (default /game) and that the client Authenticate
   command carries the GameToken.

AFFECTED_CLIENT_AREA:
scripts/network/protocol_serializer.gd, protocol_messages.gd,
scripts/network/http_api_client.gd (endpoint constants), scripts/network/mock_backend.gd
```

No backend files were modified. `BACKEND_CHANGE_REQUIRED: NONE`.

---

## Open decisions (not silently decided)

- `PRODUCT_DECISION_REQUIRED` — client orientation. Landscape is used as a
  reversible default; the blueprint does not fix it.
- `PRODUCT_DECISION_REQUIRED` — final reconnect policy (grace/backoff). The
  client implements infrastructure only (`ReconnectManager`), no policy claims.

---

## Validation status (validated with Godot 4.7.2 headless)

| Check | Result |
| --- | --- |
| Project imports / all scripts parse | ✅ PASS |
| Headless boot, mock connect | ✅ PASS |
| Unit + integration tests | ✅ 41/41 PASS |
| Full mock playable (login→world→combat→XP→loot→inventory→reconnect) | ✅ PASS (`ETHER_AUTOPLAY`) |
| Android export | ⛔ `ANDROID_BUILD_UNVERIFIED` (no Android SDK/export templates in the dev environment) |
| On-device rendering / touch gestures | ⛔ not verifiable headless |
