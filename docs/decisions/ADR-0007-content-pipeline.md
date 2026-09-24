# ADR-0007 — Versioned gameplay content pipeline

- Status: Accepted (implementation of Blueprint v5.0 §§55–56 and ADR-0001 D10)
- Date: ETHER-039
- Deciders: Engineering implementation

## Context

The M5/M6/M7 runtime data was embedded in static domain catalogs. The blueprint
requires content-driven definitions and production startup validation. The current
playable content can be externalized without changing balance or introducing a
product decision.

## Decision

- JSON content is packaged under `content/`; `manifest.json` carries the content
  schema and bundle versions. JSON schemas for the supported assets are published
  under `content/schemas/`.
- The initial bundle contains maps, abilities, items, creatures, creature loot
  tables and creature spawn points. Values are copied from the previous catalogs and
  map configuration unchanged.
- `Ether.Application` consumes bounded-context-specific read abstractions
  (`IAbilityCatalog`, `ICreatureCatalog`, `ICreatureSpawnCatalog`, `IItemCatalog`,
  `ILootTableCatalog`, and `IWorldMapProvider`). Infrastructure owns file I/O and
  supplies one immutable, process-lifetime `JsonGameContentCatalog` implementation.
  Domain remains file-system and transport independent.
- Loading uses strict camelCase JSON: unknown properties, missing required values,
  duplicate IDs, invalid domain values, unresolved map/creature/item references,
  duplicate/out-of-bounds spawn points, invalid loot entries and missing required
  Warrior abilities fail validation.
- API, GameServer and Worker force catalog construction and starting-location
  validation during host startup. Invalid or missing content prevents the host from
  accepting work; no fallback to compiled catalogs exists.
- `Content:RootPath` selects the bundle directory, relative to the application
  directory by default. Content is immutable for a running process; deploying a new
  version requires a process restart. PostgreSQL remains reserved for durable player
  state; no content migration or Redis cache is introduced.

## Scope boundary

This pipeline validates only implemented content types. NPCs, quests, tile-level
map collision, dialogue and equipment definitions remain future content additions.
Equipment Model v1 is now approved in ADR-0008. ETHER-021 will extend the item
content contract with its equipment slot and flat modifiers; this ETHER-039 task does
not implement those definitions or runtime behavior.

## Consequences

- Combat, loot, spawn and inventory metadata now have one data source and can be
  changed without editing domain/application source code.
- Domain constructors remain the authority for invariants; Infrastructure validates
  cross-references and startup configuration before exposing the catalogs.
- The bundle is packaged into each host/test output, so the Oracle runtime receives
  and validates the same versioned data files as the tested code.
