# ADR-0001 — Blueprint precedence, canonical decisions and M0 scope

- Status: Accepted
- Date: M0 (bootstrap)
- Context: Blueprint v5.0 (repository bootstrap) with prior versions v1.0–v4.0
- Deciders: Project owner + engineering

## Context

The blueprint series contains a few contradictions between versions. To allow
implementation to start without inventing business rules, the following
decisions were fixed for M0 and later milestones.

## Decisions

### D1 — Precedence

`v5.0 > v4.0 > v3.0 > v2.0 > v1.0`. Older documents never override newer ones.
Open decisions in the current blueprint remain `PRODUCT_DECISION_REQUIRED`.

### D2 — Database schema

The old schemas are not reconciled. The canonical schema is defined progressively,
per bounded context, as each milestone is implemented.

**M0 creates no gameplay tables** (no `items`, `inventories`, `quests`, `trades`,
`market`, `dialogue`, `skills`). The EF Core context exists but is intentionally empty.

### D3 — MapId

`MapId(int Value)` per Blueprint v5.0 §5. The earlier "all aggregates use UUID"
rule (v3.0 §88) is obsolete for map identifiers.

### D4 — XP curve

Canonical formula: `XP(level) = floor(100 × level^1.65)`.
The earlier `BaseXP` placeholder (v2.0 §13) is obsolete.

**XP is not implemented in M0.**

### D5 — Death penalty

`PRODUCT_DECISION_REQUIRED`. Must be configurable; must not be invented.

### D6 — Open-world PvP

Not implemented in the MVP.

### D7 — Level cap

Not implemented. No artificial cap is created.

### D8 — Premium currency, final name, lore

Must not block architecture. Monetization is not implemented in M0.

### D9 — Dialogue and character skills

Not implemented in M0. `dialogue_definitions` and the full skills system arrive in
their milestones. The architecture is prepared to receive them.

### D10 — Content JSON

No complete JSON Schema in M0. The Content Pipeline has its own milestone.

### D11 — Secrets

Development uses environment variables / local configuration. Secrets are never
versioned. Vault/KMS/provider selection is deferred to the deployment milestone.

## Consequences

- M0 stays a compilable, testable foundation with no gameplay.
- Contradictions are resolved by precedence rather than silent invention.
- Each subsequent milestone defines the schema for its own bounded context.
