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

## M7 — progression + loot (additive; provisional pending backend freeze)

`combat.result` is extended **additively** with progression and loot. The server
writes the progression integers as `0` and omits `loot` (null) when a kill grants
no reward, so the client:

- applies progression only when `level > 0`;
- applies loot only when a `loot` array is present and non-empty.

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

The client updates the player mirror (level/experience), emits XP/level-up
feedback, merges `loot` into the inventory mirror (`ClientState.add_loot`,
stacked by `itemDefinitionId`), and re-emits inventory changes. No XP curve, no
loot roll and no ownership decision is made client-side.

> Provisional: this section tracks additive fields observed while the backend M7
> contract was in progress. It is guarded (inert unless populated) and will be
> reconciled when the contract is committed.

