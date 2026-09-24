# ETHER AUTONOMOUS ENGINEERING PROTOCOL

Version: 2.0

Status: CANONICAL

Applies to autonomous engineering agents working on Project ETHER.

The Engineering Constitution is mandatory and takes precedence over this protocol.

## 1. CORE PRINCIPLE

Agents receive missions and invariants.

They are expected to discover implementation details from the repository.

Default behavior is NOT:

> human specifies classes → agent writes code

Default behavior is:

> repository → understand → discover milestone → design → implement → attack → correct → verify → handoff

## 2. START OF EVERY SESSION

Before implementation, establish reality.

Inspect:

- `git status`
- current branch
- HEAD
- remote branch state
- recent commits
- concurrent modifications
- canonical Blueprint
- ADRs
- Engineering Constitution
- latest handoff
- relevant implementation
- known findings

Do not trust stale milestone text over repository evidence.

Do not assume a prompt knows the latest HEAD.

## 3. ROADMAP DISCOVERY

Unless the human explicitly selects a feature, determine the next canonical milestone from repository evidence.

Select work that:

- follows completed dependencies;
- exists in the canonical roadmap;
- materially advances the product;
- does not require inventing an unresolved product decision;
- is appropriate to the agent's ownership area.

If the immediate milestone requires missing product specification:

> do not invent it.

Look for another explicitly canonical independent milestone.

If no appropriate milestone exists, report the product blocker.

## 4. ONE SUBSTANTIAL MILESTONE PER STANDARD SESSION

Normal autonomous sessions should complete one substantial coherent milestone.

This protects against session budget exhaustion and improves handoff reliability.

After completion:

> commit → push → handoff → report → stop.

Long-running multi-milestone execution requires an explicit orchestration mechanism rather than relying on one model session to survive for a wall-clock duration.

"Work until 07:00" is not sufficient orchestration.

## 5. SESSION BUDGET IS FINITE

Agents must assume their execution budget is finite.

Do not depend on surviving for many hours.

Maintain resumability throughout the task.

Use coherent commits.

Freeze external contracts at useful checkpoints when parallel agents depend on them.

Never leave the only explanation of current state inside model context.

## 6. ENGINEERING LOOP

Every substantial milestone follows:

> UNDERSTAND → AUDIT → DISCOVER CONTRACTS → PLAN → IMPLEMENT → TEST → HOSTILE SELF-AUDIT → CORRECT → RE-TEST → ARCHITECTURE CHECK → SCOPE CHECK → RUNTIME VALIDATION → COMMIT → PUSH → HANDOFF → REPORT

Steps may iterate.

A failed attempt is not a reason to stop.

Diagnose and correct.

## 7. IMPLEMENTATION AUTONOMY

Agents own ordinary engineering decisions.

Do not ask the human to specify:

- class names
- handler names
- repository names
- folder layouts
- DI registrations
- algorithm details that are derivable from project requirements
- ordinary validation rules
- test construction

Escalate only genuine unresolved product choices or external blockers.

## 8. CONTRACT-FIRST PARALLELISM

For backend/client features:

Backend discovers and freezes the authoritative external contract.

Client consumes the contract.

Backend must not implement client UI.

Client must not patch backend behavior.

When practical, a backend contract may be committed before the complete backend milestone if that enables safe parallelism.

The contract must be sufficiently explicit that the client does not need chat context to guess semantics.

## 9. BACKEND AGENT RESPONSIBILITIES

Backend agents autonomously determine where applicable:

- domain model
- Application use cases
- contracts
- authorization
- persistence
- transaction boundaries
- concurrency
- idempotency
- migrations
- runtime behavior
- tests

Backend agents must preserve GameServer boundaries and PostgreSQL authority.

They must not modify client/.

## 10. CLIENT AGENT RESPONSIBILITIES

Client agents autonomously determine where applicable:

- state representation
- UI composition
- signal flow
- input behavior
- reconnect behavior
- loading/errors
- protocol consumption
- mobile interaction
- Android lifecycle handling
- test design

Client agents must not create authoritative gameplay state.

They must not modify backend-owned code.

## 11. PHYSICAL ANDROID POLICY

At session start:

```text
adb devices -l
```

If a physical Android device is available:

use it for milestone-level mobile validation.

Do not start or configure an emulator unless explicitly authorized.

For applicable features:

> BUILD APK → INSTALL → LAUNCH → CONNECT TO REAL ORACLE → EXERCISE REAL TOUCH FLOW

Mock/headless remains useful for automation but cannot produce ANDROID-VERIFIED evidence.

## 12. ORACLE POLICY

Runtime-sensitive backend/client behavior should be validated against:

```text
https://game.rotagov.com.br
```

and/or

```text
wss://game.rotagov.com.br/game
```

as appropriate.

Before diagnosing a runtime discrepancy, verify:

- running revision
- deployed image
- effective configuration
- expected protocol response

Do not assume the deployed container is running the latest local code.

## 13. E2E PROTOCOL DISCIPLINE

E2E harnesses must assert semantic success before reading payload.

For example:

> expected envelope type AND expected message name AND expected status THEN inspect payload.

Never use logic equivalent to:

```python
payload.get("inventory", [])
```

before proving the response is actually a successful inventory-bearing message.

Error absence is not success.

Missing payload is not empty state.

## 14. HOSTILE SELF-AUDIT

The hostile pass is mandatory.

The agent should attempt realistic attacks relevant to the feature.

Backend examples:

- unauthorized account
- ownership violation
- duplicate request
- race
- retry
- invalid state
- process restart
- partial transaction
- persistence mismatch
- DI lifetime mismatch
- API/GameServer configuration mismatch

Client examples:

- repeated taps
- reconnect
- stale event
- error envelope
- duplicate update
- delayed response
- modal touch-through
- pause/resume
- stale target/state
- mobile layout

Do not manufacture meaningless tests simply to claim hostile validation.

Focus on plausible failure modes.

## 15. MILESTONE DEFINITION OF DONE

A milestone is complete only when applicable requirements have been satisfied:

- implementation
- tests
- negative paths
- hostile self-audit
- architecture invariants
- scope ownership
- Debug/Release or relevant build
- Oracle validation
- physical Android validation for client behavior when device is available
- clean coherent commits
- push
- handoff
- accurate evidence reporting

Unverified required behavior must remain explicitly UNVERIFIED.

## 16. DOCUMENTATION PRECEDENCE

When sources disagree:

> Constitution → canonical Blueprint → accepted ADRs → repository/executable contracts → current handoff → README summaries

Historical prompt text is context, not canonical project state.

Agents may fix stale README/status documentation when discovered during a relevant milestone.

Do not rewrite unrelated documentation for cosmetic consistency.

## 17. BRANCH POLICY

Agents must inspect branch topology rather than assuming main and develop should match.

Report divergence.

Follow documented repository policy.

If branching policy is missing:

do not invent merges merely to make branches equal.

Report the documentation gap.

Never force push without explicit human instruction.

## 18. TEST DATA

Use legitimate disposable test identities where possible.

Automated Oracle validation may self-register an account and test character.

Do not require personal credentials when self-service registration exists.

Never commit generated credentials.

## 19. FINDING CLASSIFICATION

Agents distinguish:

- IMPLEMENTED
- TEST-VERIFIED
- ORACLE-VERIFIED
- ANDROID-VERIFIED
- PARTIAL
- BLOCKED
- DEFERRED
- PRODUCT_DECISION_REQUIRED

Do not describe PARTIAL runtime verification as PASS.

## 20. REPORTING

Final reports must include:

- identity / role
- start and end commits
- branch
- roadmap discovery
- milestone selected
- why it was selected
- implementation summary
- contracts
- persistence/migrations where relevant
- concurrency/transactions where relevant
- tests
- Oracle evidence
- Android evidence where relevant
- files changed
- findings
- deferred work
- product decisions required
- scope leakage
- handoff status

The report also includes an autonomy section during model-evaluation runs:

- ROADMAP_DISCOVERY
- MAJOR_ENGINEERING_DECISIONS
- ASSUMPTIONS_REJECTED
- SELF_FOUND_DEFECTS
- CORRECTIONS
- HUMAN_INPUT_REQUIRED
- MISSING_PROJECT_DOCUMENTATION
- NEXT_AGENT_HANDOFF_QUALITY

Do not expose private chain-of-thought.

Concise engineering rationale is sufficient.

## 21. HANDOFF

A fresh agent should be able to continue without previous chat history.

Use the established repository handoff location.

Handoff should state:

- HEAD
- milestone completed
- contracts frozen
- migrations
- verification level
- known findings
- product blockers
- exact canonical next candidate

Do not use handoff to silently make product decisions.

## 22. PRODUCT BLOCKER BEHAVIOR

When encountering a genuine SPEC_GAP:

- identify precisely what is unspecified;
- explain why implementation would require inventing product behavior;
- determine whether independent canonical work exists;
- continue independent work when appropriate;
- otherwise stop with PRODUCT_DECISION_REQUIRED.

Do not implement plausible-but-unapproved semantics merely to maintain momentum.

## 23. ARCHITECTURAL EVOLUTION

Do not prematurely migrate ETHER to microservices.

Preserve extractability through module boundaries.

When future evidence requires distributed deployment, reevaluate:

- data ownership
- cross-process concurrency
- events
- messaging
- consistency
- service boundaries

Architecture evolves in response to measured need.

## 24. CURRENT MOBILE VALIDATION RULE

A physical device is intentionally available during current development.

Therefore the expected client-validation order is:

> automated tests → Oracle E2E → APK → physical Android

Do not default to emulator-based workflows.

## 25. CURRENT KNOWN PRODUCT SPEC GAP

At the time Protocol v2.0 was established, ETHER-021 Equipment cannot safely begin because equipment modifier semantics and effective-stat mapping are not canonically defined.

An agent must re-check repository state before relying on this note because the decision may later be resolved.

This note is informational, not a permanent blocker.

## 26. SUCCESS CRITERION FOR AUTONOMOUS ENGINEERING

The goal is not maximum generated code.

The goal is:

> correct product progress + preserved architecture + verified behavior + recoverable project state + minimal unnecessary human intervention.

END OF PROTOCOL
