# ADR-0004 — Combat foundation

- Status: Accepted
- Date: M5
- Deciders: Project owner + engineering

## Context

M4 delivered the realtime transport. M5 opens the first server-authoritative
gameplay system on top of it: combat. The goal is a correct, testable, extensible
foundation — not the full combat system.

## Decision — authority

The client sends **intent only** (`combat.attack` with an ability id and a target
id). The server decides whether the attack happens and every resulting number:
damage, critical, armor mitigation, resistance, HP and death. No damage, critical,
armor or HP value is ever accepted from the client. The attacker's identity comes
from the session/token, never from the payload.

## Decision — damage pipeline

A single pure function implements the canonical pipeline (Blueprint v5.0 §22):

```
RawDamage          = AbilityBase + Power * PowerScaling      (Power aggregates Weapon + Attribute + Skill)
ArmorMultiplier    = 100 / (100 + max(0, EffectiveArmor))
ResistanceMultiplier = 1 - clamp(Resistance, 0, ResistanceCap)
FinalDamage        = max(MinimumDamage, round(RawDamage * ArmorMultiplier * ResistanceMultiplier * CriticalMultiplier))
```

Each step is exposed on the result (`RawDamage`, `Damage`, `Critical`,
`ArmorMultiplier`, `ResistanceMultiplier`) so it is independently testable.

## Decision — randomness

Critical hits are the only randomness in M5. Randomness is injected through
`IRandomSource`; the domain never calls a global RNG. The production source uses
`System.Random.Shared`; tests inject a fixed roll. Critical chance/multiplier are
configuration (`Combat` section).

## Decision — stats

Attributes, skills and equipment are not implemented yet, so `ICombatStatsProvider`
resolves provisional **level-based** stats from configuration (`Combat` section).
The damage pipeline is unchanged when a richer stats source replaces it.

## Decision — abilities

Abilities are `AbilityDefinition` value objects (id, damage type, base damage,
power scaling, cooldown, range) held in a domain catalog for the Warrior vertical
slice: **Basic Attack** and **Power Strike**. The content pipeline will replace the
catalog with data files. Ids are stable keys (e.g. `warrior.power_strike`), not
random Guids, because abilities are content.

## Decision — range and targeting

Range uses the world's **Chebyshev** distance (integer tiles), consistent with
movement. Validation order: ability exists → not self → ownership → attacker alive
→ attacker in world → target exists → same map → target alive → target attackable
→ range → cooldown. Rejections carry a machine-readable reason mapped by the
transport to deterministic protocol codes.

## Decision — cooldown

The **server** owns cooldowns (`IAbilityCooldownStore`). M5 uses a process-local
store; a distributed store (Redis) replaces it when the world shards. No
`Task.Delay` loops: cooldown state is a timestamp that is checked, not scheduled.

## Decision — HP and death

Characters now carry `MaxHealth`/`Health` (persisted, with `health >= 0`,
`health <= max_health`, `max_health >= 1` constraints). Damage floors HP at 0 and
moves the character to `Dead`. **Respawn, death penalty and loot are deliberately
out of scope** and remain `PRODUCT_DECISION_REQUIRED` where the Blueprint says so.

## Decision — concurrency

Two attacks against the same target must not lose damage. Mutations acquire
per-entity locks (`IEntityLockProvider`) in a deterministic order (no deadlock, no
global lock). Locks are process-local in M5 and would move to a distributed lock
with horizontal scaling. Commands of a single session are already serialized by the
session pump.

## Decision — persistence

Combat transient state (cooldowns, in-memory locks) is not persisted. Character HP
and state are persisted because they must survive reconnect/restart. No event
sourcing, no combat log tables yet.

## Decision — protocol (additive)

New realtime messages are **additive**; M4 messages are unchanged:

| Direction | Name | Payload |
| --- | --- | --- |
| C→S | `combat.attack` | `{ abilityId, targetId }` |
| S→C | `combat.result` | `{ attackerId, targetId, abilityId, rawDamage, damage, critical, targetHealth, targetMaxHealth, targetState, targetDefeated }` |
| S→C | `combat.rejected` | `{ code, message }` |

New error codes: `ABILITY_NOT_FOUND`, `TARGET_NOT_FOUND`, `TARGET_DEAD`,
`ATTACKER_DEAD`, `OUT_OF_RANGE`, `COOLDOWN_ACTIVE`, `SELF_TARGET`.

## Decision — combat state

`EnterCombat` moves `InWorld → Combat` through the existing state machine; attacks
put both attacker and target in `Combat`. No separate combat state machine is
introduced for M5.

## Consequences

- Combat rules live in Domain/Application; the WebSocket handler only maps
  transport ↔ use case.
- Damage is deterministic given a fixed random source.
- New abilities/classes plug into the catalog and dispatcher without touching the
  transport.
