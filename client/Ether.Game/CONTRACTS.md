# Ether client — realtime contract

This file documents the wire contract the Godot client implements. It is
**client-owned documentation**: it mirrors the backend contract (ADR-0003,
ADR-0004) and, for M6, records the contract the client expects while the backend
creature replication is not yet frozen.

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

## M6 — creatures + AI (client contract; backend replication not yet frozen)

> **Status:** the backend M6 domain (`Ether.Domain.Creatures`) exists, but the
> realtime replication of creatures is not yet committed. The client implements
> the contract below and is tolerant of its absence (a snapshot without
> `creatures` simply yields an empty creature set). This is a
> `BACKEND_CONTRACT_REQUEST` — see the client README.

### Snapshot extension (additive)

`world.snapshot` may include a `creatures` array (alias: `entities`):

```json
{
  "mapId": 1, "width": 32, "height": 32,
  "player": { "characterId": "...", "x": 10, "y": 12, "state": "InWorld" },
  "creatures": [
    { "creatureId": "...", "definitionId": "creature.slime", "name": "Slime",
      "level": 1, "x": 6, "y": 7, "health": 30, "maxHealth": 30, "state": "Idle" }
  ],
  "serverTime": "..."
}
```

### Creature lifecycle events (additive)

| Dir | Name | Payload |
| --- | --- | --- |
| S→C | `creature.spawned` | `{ creatureId, definitionId, name, level, x, y, health, maxHealth, state }` |
| S→C | `creature.moved` | `{ creatureId, mapId, x, y }` |
| S→C | `creature.state` | `{ creatureId, state }` |
| S→C | `creature.health` | `{ creatureId, health, maxHealth }` |
| S→C | `creature.despawned` | `{ creatureId }` |

`state` ∈ `Idle | Patrol | Investigate | Chase | Attack | Flee | Return | Dead | Respawning`.
Creature attacks on the player are delivered as ordinary `combat.result`
messages with `attackerId` = creature id and `targetId` = player id.

The client treats these events as presentation updates only: positions, HP,
state and death always come from the server. The client never runs creature AI
or computes creature damage.
