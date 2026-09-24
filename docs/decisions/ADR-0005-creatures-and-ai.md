# ADR-0005 — Creatures and AI

- Status: Accepted
- Date: M6
- Deciders: Project owner + engineering

## Context

M5 delivered the combat pipeline. M6 introduces creatures as the first meaningful
combat target and gives them server-side AI, without inventing persistence or
replication infrastructure the game does not need yet.

## Decision — creatures are transient world state

Creature **definitions** and **spawn points** are content in
`content/creatures/creatures.json` and `content/spawns/creature-spawns.json`.
Creature **instances** live in memory (`ICreatureWorld`) and are materialized from
spawn definitions on world entry, then respawned after death. **No creature tables
are created.** PostgreSQL remains the source of truth for characters (including HP).

Rationale: creature instances are high-churn simulation state; persisting them
would add cost and complexity with no gameplay benefit at this stage. The Blueprint
`creature_definitions`/`spawn` tables belong to a future content/persistence
milestone; ETHER-039 keeps the current definitions in validated files.

## Decision — AI is pure and testable

`CreatureAi.Decide(definition, instance, targetInfo)` is a pure function returning a
decision (`None | Move | Attack | Return`). It uses the world's **Chebyshev**
distance and the creature's aggro/attack/leash ranges. No I/O, no randomness.

Orchestration lives in `CreatureAiTickHandler` (Application): it respawns due
creatures, resolves the nearest eligible player, applies move/attack/return, reuses
`DamageCalculator`, `IAbilityCooldownStore`, `IEntityLockProvider` and persists
character changes once per tick. The **GameServer only schedules the tick and
translates outcomes into protocol messages** (`CreatureAiTickService`).

## Decision — combat reuse

- Player → creature: `AttackCreatureCommandHandler` reuses the M5 damage pipeline,
  ability catalog, cooldown store, entity locks and `ICombatStatsProvider`.
- Creature → player: same `DamageCalculator`, creature stats as attacker and the
  character's stats as defender; the character's HP/state are persisted.
- Both directions report the existing `combat.result` event with additive
  `attackerType`/`targetType` (`character` | `creature`) defaulting to `character`.

## Decision — protocol (additive)

| Direction | Name | Payload |
| --- | --- | --- |
| C→S | `combat.attack` (extended) | `{ abilityId, targetId, targetType? }` (default `character`) |
| S→C | `world.snapshot` (extended) | adds `creatures: [{ creatureId, definitionId, name, x, y, health, maxHealth, state }]` |
| S→C | `world.creature_moved` (new) | `{ creatureId, mapId, x, y, health, maxHealth, state }` |

M4/M5 messages are unchanged; the new fields are optional and default to the old
behaviour, so existing clients keep working.

## Decision — AI tick and limits

The AI runs at `GameServer:AiTickRateHz` (default 5 Hz). Each creature's attack is
rate-limited by its own attack cooldown via the shared cooldown store. Movement is
bounded by the creature's `MoveSpeed` per tick and by map bounds. Total creatures
are bounded by the spawn catalog.

## Decision — broadcasting

Creature updates are sent only to sessions whose character is on the same map
(resolved once per tick from the repository). Full interest management /
`InterestRadius` replication remains deferred; at M6 scale this per-map broadcast is
sufficient and avoids leaking other maps' state.

## Decision — HP/death

Creatures use their own `CreatureState` machine (Idle/Patrol/Investigate/Chase/
Attack/Flee/Return/Dead/Respawning). On death the creature schedules a respawn
(`RespawnDelay`) and is removed from snapshots while dead. **Loot and XP rewards on
creature death are deferred** to the progression/loot milestone.

## Deliberately deferred

- Patrol/Investigate/Flee/Assist/CallForHelp behaviours (states reserved).
- XP, loot, item rewards on kill.
- Creature persistence and content data files.
- Interest-radius replication and multi-map worlds.

## Consequences

- Creatures are the first real PvE target with server-authoritative AI.
- No new database surface; the schema stays truthful to what is implemented.
- New behaviours plug into `CreatureAi` and the tick handler without touching the
  transport.
