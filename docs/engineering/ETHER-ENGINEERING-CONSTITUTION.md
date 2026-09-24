# ETHER ENGINEERING CONSTITUTION

Version: 2.0

Status: CANONICAL

Purpose:

This document defines permanent engineering invariants for Project ETHER.

These rules have higher authority than temporary task prompts, handoff notes and README summaries.

They may only be changed intentionally as an architectural/product decision.

## 1. SOURCE-OF-TRUTH PRECEDENCE

When project sources disagree, use this precedence:

1. Explicit current product decisions
2. Canonical Blueprint v5.x
3. Accepted ADRs
4. Implemented repository state and executable contracts
5. Current agent handoff
6. README/status summaries
7. Historical prompts/reports

README status text must never override canonical architecture, ADRs or verified repository state.

Historical chat context is not required for project correctness.

The repository must contain enough information for a fresh engineer or agent to continue.

## 2. SERVER AUTHORITY

ETHER is server-authoritative.

The client must never become authoritative for:

- player position
- health
- death
- respawn
- combat results
- cooldowns
- creature state
- XP
- level
- loot
- inventory
- item ownership
- item quantity
- gold
- transactions

Client-side prediction or immediate visual feedback is allowed only when it cannot become durable or authoritative state.

Authoritative state must eventually reconcile to the server.

## 3. PERSISTENCE AUTHORITY

PostgreSQL is the durable source of truth for persistent gameplay data.

Redis may be used for:

- cache
- ephemeral coordination
- short-lived session data
- performance optimization
- distributed infrastructure where explicitly designed

Redis must NOT become the authoritative source for:

- inventory
- item ownership
- XP
- level
- gold
- transactions
- durable character progression

Durable gameplay truth must remain recoverable from PostgreSQL.

## 4. MODULAR MONOLITH FIRST

ETHER intentionally begins as a modular monolith / separated runtime workloads.

Microservices are not introduced merely for architectural fashion.

A service is extracted only when justified by evidence such as:

- independent scaling needs
- fault isolation
- deployment independence
- operational ownership
- clearly different workload characteristics

Current bounded contexts must remain extractable.

Therefore:

- each bounded context owns its rules;
- modules must not casually manipulate another context's internal persistence;
- communication should occur through Application contracts, interfaces or explicit events;
- domain logic must remain independent of transport;
- cross-context transactions must be deliberate rather than accidental;
- external contracts should remain versionable.

A future network boundary should not require rewriting the core business rules.

## 5. GAMESERVER BOUNDARY

GameServer owns realtime transport and orchestration.

GameServer must not directly depend on:

- Entity Framework Core
- Npgsql
- concrete Persistence implementation

Persistence access must flow through Application abstractions.

Realtime handlers must not contain hidden database architecture.

## 6. CLIENT ARCHITECTURE

The Godot client must preserve clear layering.

Conceptually:

Transport → Protocol serialization → Event dispatch → Game/application facade →
Client feature state → UI / rendering / input

UI code must not become a second networking layer.

Raw protocol parsing should not spread throughout UI components.

Do not create duplicate transports, command senders or competing sources of gameplay state.

## 7. CONTRACT-FIRST DEVELOPMENT

Features crossing backend/client boundaries require an explicit discoverable contract.

Contracts should be frozen early enough to allow parallel work.

Prefer additive evolution when practical.

Do not create duplicate protocol concepts when an existing contract can represent the behavior cleanly.

Expected protocol state must be distinguishable from errors.

Realtime consumers must validate envelope type/name before reading success payload fields.

An error envelope missing a field must never be interpreted as empty successful state.

## 8. CONTENT VS PLAYER STATE

Static/versioned gameplay content and durable player state are different concerns.

Static content may be versioned and deployed as validated data files.

Player state remains durable persistence.

Do not move static content into PostgreSQL merely because PostgreSQL exists.

Do not move player ownership/progression into content files.

## 9. CONCURRENCY

Every mutating gameplay operation must have an explicit concurrency policy.

The implementation must determine:

- lock ownership
- transaction boundaries
- retry semantics
- idempotency expectations

Do not rely on implicit assumptions such as:

> "the caller probably already owns the lock."

If a caller-owned lease is supported, ownership must be explicit and verifiable.

Process-local locks protect only one process.

Before multiple GameServer instances can concurrently own the same gameplay entity, cross-process coordination must be deliberately designed.

Do not prematurely add distributed locking before that deployment model exists.

## 10. TRANSACTIONAL CORRECTNESS

Multi-step operations must not leave partial authoritative state.

Where relevant:

validate/preflight → mutate → persist atomically

Examples include:

- inventory capacity
- loot rewards
- item stacking
- progression rewards
- transaction processing

Failures must not silently lose or duplicate durable gameplay resources.

## 11. ORACLE IS THE RUNTIME TRUTH

Oracle Cloud is the official runtime environment.

Local tests are necessary but do not replace Oracle verification.

A runtime-sensitive milestone is not Oracle-verified until it is actually tested against the Oracle deployment.

Deployment verification must also prove that the running workload corresponds to the intended revision/image.

Never destroy persistent Oracle data as a convenience.

Never use:

```text
docker compose down -v
```

PostgreSQL remains a private dedicated container.

## 12. PHYSICAL ANDROID IS THE MOBILE TRUTH

When a physical Android device is connected through ADB, it is the canonical mobile validation target.

Do not use an emulator unless explicitly authorized.

Do not waste engineering budget configuring an emulator while a physical device is available.

Headless and Mock testing are useful automated layers but are not substitutes for physical-device validation.

ANDROID-VERIFIED means the behavior was actually exercised on a physical Android device.

## 13. TEST IDENTITY

Automated E2E testing must not depend on human personal credentials when the product supports legitimate self-service registration.

Prefer:

- explicit test credentials when intentionally provided

otherwise:

disposable test account → disposable character → E2E

Never:

- hardcode human credentials;
- commit secrets;
- weaken authentication for testing.

Test identities are disposable test data.

## 14. TEST CLEANLINESS

A numerical PASS count is not sufficient.

A successful run must also be free of unexpected:

- parser errors
- script errors
- runtime exceptions
- swallowed failures
- misleading diagnostics

"132/132 passed" while the runner emits real script errors is NOT a clean PASS.

## 15. EVIDENCE LEVELS

Use explicit evidence levels.

**CODE-VERIFIED**

The implementation exists and was inspected.

**TEST-VERIFIED**

Applicable automated tests execute successfully.

**ORACLE-VERIFIED**

The behavior was exercised against the official Oracle runtime.

**ANDROID-VERIFIED**

The behavior was exercised on a physical Android device.

**UNVERIFIED**

Evidence does not establish the claim.

Never promote one evidence level into another.

## 16. PRODUCT DECISIONS VS ENGINEERING DECISIONS

Engineering agents own technical implementation decisions.

Examples:

- class design
- repository structure
- transaction implementation
- serialization
- validation
- test strategy
- DI lifetime
- file organization

Humans own genuinely unresolved product decisions.

Examples:

- death penalty
- equipment stat semantics
- monetization
- PvP rules
- player-facing progression choices

Agents must not escalate routine implementation decisions to humans.

Agents must not invent unresolved product behavior merely to keep coding.

## 17. HOSTILE SELF-AUDIT

Every substantial milestone must undergo adversarial self-review.

After implementation appears complete:

> ASSUME IT IS WRONG.

Attack relevant dimensions such as:

- authorization
- cross-account access
- concurrency
- retries
- duplicate commands
- reconnect
- stale state
- partial persistence
- process restart
- invalid state transitions
- malformed protocol
- runtime deployment
- Android lifecycle
- UI touch leakage
- duplicate rewards
- transaction failure

Do not manufacture meaningless tests simply to claim hostile validation.

Focus on plausible failure modes.

## 18. REGRESSION

New milestones must preserve previously verified gameplay unless an intentional product change says otherwise.

The project must not gain a new feature by silently breaking an established loop.

## 19. SCOPE OWNERSHIP

Backend agent owns backend/server/persistence/backend tests/backend documentation.

Client agent owns client/.

Agents must preserve concurrent work.

Do not:

- stage another agent's files;
- reset another agent's modifications;
- use destructive cleanup to obtain a clean tree;
- force push.

## 20. NO SPECULATIVE COMPLEXITY

Prefer the smallest coherent architecture that satisfies the current requirement.

Do not introduce:

- microservices
- distributed coordination
- frameworks
- abstraction layers
- major infrastructure

for hypothetical future needs.

Preserve future evolution without paying its operational cost prematurely.

## 21. HANDOFF IS PART OF DONE

A milestone is not operationally complete if a fresh agent cannot understand what happened.

The repository must record enough information to recover:

- current milestone
- completed work
- contracts
- migrations
- runtime verification
- known findings
- blockers
- next canonical work

Project continuity must not depend on chat history.

## 22. QUALITY OVER FEATURE COUNT

A completely implemented, tested and verified milestone is more valuable than several partially implemented milestones.

Do not leave a knowingly broken repository to maximize apparent progress.

END OF CONSTITUTION
