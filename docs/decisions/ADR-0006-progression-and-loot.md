# ADR-0006 — Progression (XP) and loot

- Status: Accepted
- Date: M7
- Deciders: Project owner + engineering

## Context

M6 made creatures killable. M7 closes the loop: a kill must grant experience and
loot, server-authoritatively and exactly once, and that progression must persist.

## Decision — one canonical XP curve

The only definition of the curve lives in `Ether.Domain.Progression.ExperienceCurve`
(Blueprint v5.0 §29):

```
ExperienceToAdvanceFrom(level) = floor(100 × level^1.65)
```

Semantics: the experience required to go **from `level` to `level + 1`**.
`FormulaVersion = 1` is bumped whenever the curve changes. `TotalExperienceToReach`
is derived from the same function; no other code may redefine the formula.

No artificial level cap is enforced (ADR-0001 D7 leaves the cap open); the loop
terminates naturally because the requirement grows with level.

## Decision — level up is a domain rule

`Character.GrantExperience(amount, now)` adds experience and applies any number of
level-ups by consuming the curve, returning the levels gained. `Level` and
`Experience` remain the persisted columns (`characters.level` / `experience`); no
new progression table is needed.

## Decision — rewards are granted exactly once

Experience and loot are granted **on the lethal transition only**
(`CreatureInstance.ApplyDamage` returns `true` exactly once; the creature then
cannot take damage again — further attacks are rejected with `TARGET_DEAD`).
`KillRewardService` is therefore called once per death. The kill, the XP change and
the loot rows are persisted in the **same unit of work** (one `SaveChanges`), so a
failure rolls back the whole reward.

The killer is the session/attacker identity resolved server-side, never a
client-provided id.

## Decision — loot is content + injected randomness

Loot tables are content (`LootTableCatalog`, keyed by creature definition id) with
`LootEntry(item, chance, min, max)`. `LootRoller.Roll(table, IRandomSource)` is a
pure function: one draw for the chance, one for the quantity. Randomness stays
behind `IRandomSource`, so tests use a scripted sequence and production uses
`Random.Shared`.

## Decision — minimal item/inventory foundation

`ItemDefinitionId`, `ItemDefinition`, `ItemCatalog`, `ItemInstance` and
`ItemLocation` form the canonical item foundation (no parallel systems). Items are
created **only** through `ItemInstance.CreateLoot` (server-side loot) for M7.
Persistence is a single `item_instances` table (id, definition_id, quantity,
owner_character_id, location_type) with `quantity > 0` and a `Restrict` FK to
`characters`. Definitions stay content-driven (no `item_definitions` table yet).

At M7, inventory slots/capacity, stacking into existing stacks, equipment,
trade/market escrow and gold/currency were deferred. The M8 addendum below closes
capacity and stacking; equipment, trade/market escrow and gold/currency remain deferred.

## M8 addendum — inventory locking and persistence ownership

`InventoryService.AddLootAsync` owns the inventory read-modify-write boundary. A
direct caller that does not already hold the owner's entity lock is serialized by
the service using `IEntityLockProvider`, through the shared unit-of-work save. A
larger operation such as creature combat owns its multi-entity lease and passes that
lease through `KillRewardService` to the inventory service; the inventory service
verifies that the lease covers the owner and does not reacquire the same semaphore.
This makes the nested lock ownership explicit and prevents the former self-deadlock.

Capacity is checked before mutating stacks, so an inventory-full result cannot
persist a partial add. Locking covers the owner query, stack merges/new rows, and
`SaveChangesAsync`. The current lock provider is process-local; PostgreSQL remains
the source of truth, and multi-GameServer deployment requires cross-process database
serialization/versioning before it is enabled.

## Decision — protocol (additive)

| Direction | Message | Addition |
| --- | --- | --- |
| S→C | `combat.result` | `experienceGained`, `level`, `experience`, `levelsGained`, `loot[]` |
| S→C | `world.snapshot` | player `level`, `experience`, `experienceToNextLevel`, `health`, `maxHealth` |

All fields are optional with defaults, so M4–M6 clients keep working. `loot[]`
entries carry `itemDefinitionId`, `name`, `quantity`, `itemInstanceId`.

## Consequences

- A kill now yields verifiable progression and owned items, all server-side.
- The XP formula exists in one place and is versioned.
- The inventory foundation is small but real, and grows into the inventory
  milestone without a rewrite.

## Deferred

- Gold/currency rewards and economy sinks/sources.
- Equipment and item use.
- Death/respawn penalties (Blueprint `PRODUCT_DECISION_REQUIRED`).
