# Ether client — realtime contract

This file documents the wire contract the Godot client implements. It is
**client-owned documentation**: it mirrors the frozen backend contracts
(ADR-0003 through ADR-0006), plus the accepted respawn and M8 inventory contracts.

Envelope (all messages):

```json
{ "version": 1, "type": "command|event|error", "name": "...", "requestId": "uuid|null", "sequence": 1, "payload": {} }
```

`requestId` is a GUID on commands (the server deserializes it as `Guid?`);
`sequence` is strictly increasing per session.

---

## M4 — session, world, movement (frozen)

| Dir | Name | Payload |
| --- | --- | --- |
| C→S | `game.authenticate` | `{ gameToken }` |
| C→S | `world.enter` | `{}` |
| C→S | `movement.move` | `{ x, y }` |
| C→S | `system.ping` | `{}` |
| S→C | `game.authenticated` | `{ sessionId, accountId, characterId }` |
| S→C | `world.snapshot` | `{ mapId, width, height, player { characterId, x, y, state }, serverTime }` |
| S→C | `movement.accepted` | `{ characterId, mapId, x, y }` |
| S→C | `system.pong` | `{ serverTime }` |
| S→C | `protocol.error` | `{ code, message }` |
| S→C | `game.authenticate.rejected` / `world.enter.rejected` / `movement.rejected` | `{ code, message }` |

---

## M5 — combat (frozen in ADR-0004)

| Dir | Name | Payload |
| --- | --- | --- |
| C→S | `combat.attack` | `{ abilityId, targetId, targetType? }` — `targetType` is `"character"` (default) or `"creature"` |
| S→C | `combat.result` | `{ attackerId, targetId, abilityId, rawDamage, damage, critical, targetHealth, targetMaxHealth, targetState, targetDefeated }` |
| S→C | `combat.rejected` | `{ code, message }` |

Error codes: `ABILITY_NOT_FOUND`, `TARGET_NOT_FOUND`, `TARGET_DEAD`,
`ATTACKER_DEAD`, `OUT_OF_RANGE`, `COOLDOWN_ACTIVE`, `SELF_TARGET`.

Ability ids are content keys: `warrior.basic_attack`, `warrior.power_strike`.
The client sends intent only; it never sends damage, criticals or HP.

---

## M6 — creatures + AI (frozen in ADR-0005)

Creature instances are transient world state. The server replicates them
additively; M4/M5 messages are unchanged.

### Snapshot extension (additive)

`world.snapshot` includes a `creatures` array:

```json
{
  "mapId": 1, "width": 32, "height": 32,
  "player": { "characterId": "...", "x": 10, "y": 12, "state": "InWorld" },
  "creatures": [
    { "creatureId": "...", "definitionId": "creature.slime", "name": "Slime",
      "x": 6, "y": 7, "health": 30, "maxHealth": 30, "state": "Idle" }
  ],
  "serverTime": "..."
}
```

### Creature replication event (additive)

| Dir | Name | Payload |
| --- | --- | --- |
| S→C | `world.creature_moved` | `{ creatureId, mapId, x, y, health, maxHealth, state }` |

A single event carries position, health and AI state (also used for respawn).
Creature attacks on the player are ordinary `combat.result` messages with
`attackerType="creature"`, `targetType="character"`.

`state` ∈ `Idle | Patrol | Investigate | Chase | Attack | Flee | Return | Dead | Respawning`.

### Combat additive fields

`combat.attack` payload: `{ abilityId, targetId, targetType }` where `targetType`
is `"character"` (default) or `"creature"`.
`combat.result` payload adds `attackerType` / `targetType` (default `"character"`).

The client treats all creature data as presentation only: positions, HP, state
and death always come from the server. The client never runs creature AI or
computes creature damage.

---

## M7 — progression + loot (frozen in ADR-0006)

Additive to M5/M6; M4–M6 clients keep working.

### `combat.result` (additive)

```json
{
  "attackerId": "...", "targetId": "...", "abilityId": "warrior.basic_attack",
  "damage": 12, "critical": false,
  "targetHealth": 0, "targetMaxHealth": 30, "targetState": "Dead", "targetDefeated": true,
  "attackerType": "character", "targetType": "creature",
  "experienceGained": 12, "level": 2, "experience": 120, "levelsGained": 0,
  "loot": [
    { "itemDefinitionId": "item.slime_gel", "name": "Slime Gel",
      "quantity": 1, "itemInstanceId": "..." }
  ]
}
```

The server writes the progression integers as `0` and omits `loot` (null) when a
kill grants no reward, so the client:

- applies progression only when `level > 0`;
- applies loot only when a `loot` array is present and non-empty.

### `world.snapshot.player` (additive)

```json
{
  "characterId": "...", "x": 10, "y": 12, "state": "InWorld",
  "level": 3, "experience": 250, "experienceToNextLevel": 400,
  "health": 80, "maxHealth": 100
}
```

The client maps `health → hp`, `maxHealth → maxHp`, `experienceToNextLevel →
experienceToNext` and renders the player mirror.

The client updates the player mirror (level/experience), emits XP/level-up
feedback, merges `loot` into the inventory mirror (`ClientState.add_loot`,
stacked by `itemDefinitionId`) and re-emits inventory changes. No XP curve, no
loot roll and no ownership decision is made client-side.

---

## Respawn (frozen — backend `b68acc7`, order fix `0131215`)

| Dir | Name | Payload |
| --- | --- | --- |
| C→S | `character.respawn` | `{}` |
| S→C | `world.snapshot` (success) | canonical snapshot |
| S→C | `character.respawn.rejected` | `{ code, message }` |

Additional error code on `world.enter`:

| Code | Meaning |
| --- | --- |
| `CHARACTER_DEAD` | character must respawn before entering the world |
| `CHARACTER_NOT_DEAD` | respawn requested for a living character |

Rules the client follows:

- death arrives via `combat.result` (`targetState: "Dead"`, `targetDefeated: true`);
- while dead the client emits **no** movement / attack commands, shows a death
  overlay and gates the action buttons;
- respawn is **only** requested (`character.respawn`); HP, position and state are
  taken exclusively from the returned `world.snapshot`;
- a duplicate `character.respawn` while one is in flight is suppressed
  (cleared on snapshot, on reconnect and on rejection);
- reconnecting with a dead character (`world.enter` → `CHARACTER_DEAD`)
  automatically requests a respawn.

---

## M8 Inventory (frozen — backend `d0aec66`)

`world.snapshot` carries the authoritative inventory (additive):

```json
"inventory": [
  { "itemDefinitionId": "item.slime_gel", "name": "Slime Gel", "quantity": 3,
    "maxStack": 99, "stackable": true, "location": "inventory" }
]
```

| Dir | Name | Payload |
| --- | --- | --- |
| HTTP | `GET /characters/{characterId}/inventory` | `InventoryResponse { characterId, items[] }` |

- The snapshot (or the HTTP read) **replaces** the client mirror — no merging,
  so reconnects cannot duplicate stacks.
- Loot from `combat.result` merges into the mirror by `itemDefinitionId` for
  immediacy; the next snapshot/HTTP read is the authority.
- Capacity (`Inventory:MaxSlots = 30`) is server config and is **not** in any
  payload, so the client shows no capacity bar (not invented).
- Unknown `itemDefinitionId` values are displayed but never interpreted.
