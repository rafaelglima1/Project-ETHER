# ADR-0008 — Equipment Model v1

- Status: Accepted — approved product decision
- Date: 2026-09-24
- Deciders: Project owner

## Context

Blueprint v5.0 §§25–28 defines item instances, locations and equipment slots, but
leaves the v1 equipment modifier contract open. This decision resolves that product
specification gap and makes ETHER-021 ready for implementation. This ADR records the
contract only; it does not implement equipment behavior.

## Decision — slots

V1 has exactly these equipment slots:

- `MainHand`
- `OffHand`
- `Head`
- `Chest`
- `Legs`
- `Feet`

At most one item instance may be equipped in a slot. Rings, amulets, accessories,
cosmetic slots, sets, two-handed behavior and dual wield are out of scope.

## Decision — item instance ownership and eligibility

- Equip and unequip identify an `ItemInstance` by `itemInstanceId`, never only by
  `itemDefinitionId`.
- The character must own the instance. To equip, it must be available in that
  character's inventory, equipment-capable, and its definition slot must match the
  requested slot.
- V1 equipment definitions are non-stackable with `maxStack = 1`; equipment
  instances have `quantity = 1`.
- Equipping changes the instance location from `Inventory` to `Equipment(slot)`;
  ownership remains with the same character. Equipped instances do not consume
  inventory capacity.

## Decision — flat modifiers and effective stats

Equipment Model v1 supports only flat modifiers:

- `weaponPower`
- `armor`
- `maxHealth`

No percentage modifiers exist in v1. Equipment does not modify critical chance or
multiplier, resistance, attack range or speed, cooldown, movement speed,
`AttributeScaling`, or `SkillScaling`.

For each character, the effective values are:

```text
EffectiveWeaponPower = BaseWeaponPower + sum(equipped.weaponPower)
EffectiveArmor = BaseArmor + sum(equipped.armor)
EffectiveMaxHealth = BaseMaxHealth + sum(equipped.maxHealth)
```

The existing combat formulas consume `EffectiveWeaponPower` and `EffectiveArmor` in
place of their corresponding unequipped inputs. Existing attribute and skill scaling
terms and all other combat formula behavior remain unchanged.

## Decision — health semantics

Increasing `EffectiveMaxHealth` never increases `CurrentHealth` automatically. For
example, `100/100` becomes `100/120` after equipping `maxHealth +20`.

Decreasing `EffectiveMaxHealth` clamps `CurrentHealth` down to the new maximum. For
example, `115/120` becomes `100/100` after removing `maxHealth +20`.

Equip and unequip therefore cannot be used as free healing.

## Decision — permitted character states

Equipment mutations are rejected in `Combat`, `Dead`, `Respawning`, and
`DisconnectGrace`. V1 permits mutation while `Offline` or while `InWorld` and not in
combat. Combat equipment switching is not supported.

## Decision — inventory transitions and atomic swap

Equip moves an instance from `Inventory` to its definition's `Equipment(slot)`.
Unequip moves it from `Equipment(slot)` to `Inventory` and requires available
inventory capacity.

Replacing an occupied slot is one atomic swap: the incoming inventory instance
becomes equipped while the displaced instance returns to inventory. The incoming
instance leaves inventory as the outgoing instance enters it, so a normal one-for-one
swap does not require an additional free slot. The operation must not lose or
duplicate either instance if validation or persistence fails.

## Decision — death, respawn and persistence

Equipment remains equipped through death, respawn and reconnect. V1 adds no
durability loss, repair, corpse/equipment drop or death equipment penalty.

Equipment is durable authoritative character state. After reconnect or respawn, the
same instances remain equipped and contribute the same effective stats.

## Decision — Warrior starter equipment

Every Warrior created after ETHER-021 is implemented receives these two instances
in inventory, initially unequipped:

| Item ID | Slot | Stackable | Max stack | Weapon power | Armor | Max health |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| `item.recruit_sword` | `MainHand` | No | 1 | +5 | 0 | 0 |
| `item.recruit_vest` | `Chest` | No | 1 | 0 | +8 | +10 |

These values are provisional content tuning. Their definitions and modifiers belong
in the versioned Content Pipeline, not hard-coded gameplay logic. This starter grant
applies to newly created Warriors; retroactive grants to existing characters are not
defined by this decision.

V1 adds no generalized level, stat, skill or class requirement system. The Warrior
starter assignment is content/setup behavior, not a generalized equipment
restriction.

## Decision — client interaction

The MVP provides explicit Equip and Unequip actions. Drag-and-drop is not required;
the UI may use simple mobile-friendly slot controls.

## Out of scope

V1 excludes rarity mechanics, random affixes, item level, durability/repair,
two-handed weapons, dual wield, weapon-type mechanics, generalized level or class
requirements, sockets/gems, enchanting, set bonuses, transmog/cosmetics, consumable
use, crafting, trading, marketplace, vendor economy, gold and equipment loss on
death.

## Consequences

- ETHER-021 is ready to implement against a product-approved contract.
- ETHER-021 must extend the current content item schema with equipment slot and flat
  modifier data, and add the two starter definitions without hard-coding their stats
  into gameplay logic.
- No runtime behavior, persistence schema or client implementation is changed by
  this ADR alone.
