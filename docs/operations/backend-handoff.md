# Backend handoff

## Current state

- Branch: `develop`
- Content pipeline implementation commit: `612afbb`
- Oracle runtime was built from `612afbb`; API, GameServer and Worker are healthy.
- The final handoff document is a documentation-only follow-up to the implementation
  commit. Resolve the exact repository HEAD with `git rev-parse HEAD`.
- M0–M8 are complete. Canonical content task ETHER-039 is complete.

## Content contract

- Bundle: `content/manifest.json`, `content/{abilities,creatures,items,loot-tables,maps,spawns}/`.
- JSON schemas: `content/schemas/`; current schema version 1, bundle content version 1.
- Application consumers depend on context-specific catalog abstractions; file access
  and cross-reference validation remain in Infrastructure.
- API, GameServer and Worker load and validate the bundle before accepting work.
- No database migration or external HTTP/WebSocket contract change was made.
- Content changes require deploying/restarting the hosts; hot reload is not implemented.

## Verification

- Debug: Domain 117, Application 83, Infrastructure 52, API 34, GameServer 52,
  Architecture 11 — **349 passed**.
- Release: same suites and counts — **349 passed**.
- Debug/Release solution builds: 0 warnings, 0 errors.
- Oracle: `/health` and `/ready` return 200. Each workload logged content version 1;
  each final image contains `/app/content/manifest.json`.
- Oracle E2E: a self-registered player killed a creature, loot persisted in
  PostgreSQL, HTTP inventory returned it, and a reconnect `world.snapshot` returned
  the same item and quantity.
- Oracle image IDs: API `sha256:935be7cce2cc59025df4d72b1e9504b0ce94190dd59dbf25099041881538ce3c`;
  GameServer `sha256:377f2ce3d0aeeb05340be1142a37bde0eaf84898a1a8b6b2748591e9e243b1ef`;
  Worker `sha256:747d27b1f3cc68c85caa9f7046f83a2ee2b0832fad36c95814824e26ae46afba`.

## Deferred / next canonical task

- Next: ETHER-021 Equipment (Blueprint v5.0 §28), **READY** under the approved
  Equipment Model v1 in ADR-0008; runtime implementation has not started.
- Equipment product semantics for v1 (six slots, item-instance ownership, flat
  modifiers, effective stats, health, safe-state, inventory swap, death/respawn,
  starter Warrior items, UX and out-of-scope rules) are canonical in ADR-0008.
- Equipment definitions and starter item values must be added to the versioned
  Content Pipeline during ETHER-021; do not hard-code them in gameplay logic.
- Death penalty remains `PRODUCT_DECISION_REQUIRED` per ADR-0001 D5; it does not block
  ETHER-021's approved equipment contract.
- The client-owned README still contains the former ETHER-021 `SPEC_GAP` note; it was
  not edited from the backend scope and should be refreshed by the client owner.
- Permanent engineering rules and autonomous workflow are now recorded in
  `docs/engineering/ETHER-ENGINEERING-CONSTITUTION.md` and
  `docs/engineering/ETHER-AUTONOMOUS-ENGINEERING-PROTOCOL-V2.md`.
- No client files were modified by the backend content-pipeline work.
