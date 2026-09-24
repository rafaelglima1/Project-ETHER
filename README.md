# Project Ether

> "Um mundo que continua existindo quando você fecha o jogo."

Project Ether is a **2D mobile-first MMORPG**: a persistent world with real-time
combat, deep progression, player-driven economy and a strong social layer —
designed to work both in ~5-minute sessions and in long sessions, without
auto-play and without pay-to-win.

The product intent is captured in the blueprints under [`docs/blueprints`](docs/blueprints/README.md).
The current technical contract is **Blueprint v5.0**.

## Status

**Backend status:** M0–M8 are complete, and the canonical content pipeline
(ETHER-039) is implemented. The next equipment task is held at a documented
specification gap rather than inventing item stat rules. See the backend handoff
([`docs/operations/backend-handoff.md`](docs/operations/backend-handoff.md)) and
[`docs/decisions/`](docs/decisions) for current details.

| Milestone | Scope | Status |
| --- | --- | --- |
| M0 | Solution, projects, architecture tests, health checks, Docker, CI | ✅ Done |
| M1 | Account + Character foundation (persistence, use cases, HTTP) | ✅ Done |
| M2 | Auth/session foundation (credentials, JWT access/refresh, game token) | ✅ Done |
| M3 | World/movement foundation (bounded map, enter world, authoritative move) | ✅ Done |
| M4 | GameServer WebSocket / first realtime world | ✅ Done |
| M5 | Combat foundation (server-authoritative attack, damage, HP) | ✅ Done |
| M6 | Creatures + AI (spawns, chase/attack/return, respawn) | ✅ Done |
| M7 | Progression (XP) + loot + item foundation | ✅ Done |
| M8 | Inventory (authoritative ownership, capacity, stacking) | ✅ Done |
| ETHER-039 | Content pipeline and validator for implemented gameplay content | ✅ Done |
| ETHER-021 | Equipment | ⏸ Spec gap — item modifiers and attribute/combat mapping are undefined |


### Realtime (GameServer WebSocket)

Public endpoint (Oracle): `wss://game.rotagov.com.br/game` (HTTPS API:
`https://game.rotagov.com.br`). See
[`docs/operations/oracle-https-endpoint.md`](docs/operations/oracle-https-endpoint.md).

`GET /game` (WebSocket, `WebSocket:Path`) is the canonical realtime endpoint; the
envelope, message names, error codes, session lifecycle, heartbeat, sequence and
authority rules are defined in **ADR-0003**, combat in **ADR-0004** and creatures
in **ADR-0005**. Flow:

```
connect → game.authenticate (game token) → game.authenticated
        → world.enter → world.snapshot (player + creatures)
        → movement.move → movement.accepted | movement.rejected
        → combat.attack { abilityId, targetId, targetType? } → combat.result | combat.rejected
        → world.creature_moved (server-initiated, creature AI)
        → system.ping → system.pong
```

Access and refresh tokens are rejected at authentication; the character identity
used by the server always comes from the token, never from client input. Creature
AI runs server-side (5 Hz) and only broadcasts to sessions on the same map.




## Architecture

Modular monolith — **not** microservices. Strong module boundaries so future
extraction is possible without rewriting.

```
Mobile client (Godot 4.x)
        │  WebSocket (gameplay)
        │  HTTPS (auth, account, characters)
        ▼
┌────────────────────────────────────────────┐
│              Backend (.NET 10)             │
│  Ether.Api          Ether.GameServer       │
│  Ether.Worker                              │
│        │                                   │
│  Ether.Application  (use cases)            │
│  Ether.Domain       (rules, no infra)      │
│  Ether.Infrastructure (EF Core, Redis)     │
│  Ether.Contracts    (protocol, DTOs)       │
└────────────────────────────────────────────┘
        │                       │
   PostgreSQL               Redis
   (source of truth)        (coordination / transient only)
```

### Dependency rules (enforced by architecture tests)

- `Ether.Domain` depends on nothing else.
- `Ether.Application` depends on `Domain` + `Contracts` only.
- `Ether.Infrastructure` depends on `Application` + `Domain` + `Contracts`.
- `Ether.Api` / `Ether.GameServer` depend on `Application` + `Infrastructure` + `Contracts`.
- `Ether.Worker` depends on `Application` + `Infrastructure`.
- `Ether.Domain` must **not** reference EF Core, ASP.NET Core, PostgreSQL, Redis, Godot.
- No project may reference the Godot client.

These rules are verified in `tests/Ether.Architecture.Tests` at the IL level
(base types, interfaces, fields, signatures, generic arguments and instructions).

## Stack

| Layer | Technology |
| --- | --- |
| Backend | .NET 10, C# 14, ASP.NET Core |
| Persistence | PostgreSQL, EF Core 10 |
| Coordination | Redis |
| Realtime | Raw WebSockets (no SignalR) |
| Client | Godot 4.x (GDScript) — future |
| Infra | Docker, Docker Compose, GitHub Actions |
| Observability | OpenTelemetry (planned) |

## Repository layout

```
ProjectEther.sln
src/
  Ether.Domain/           pure domain (no infrastructure)
  Ether.Application/      use cases
  Ether.Infrastructure/   EF Core, Redis, technical services
  Ether.Contracts/        protocol, DTOs, configuration contracts
  Ether.Api/              HTTP surface, /health, /ready
  Ether.GameServer/       authoritative world loop (WebSocket endpoint)
  Ether.Worker/           async/scheduled jobs
tests/
  Ether.Domain.Tests/
  Ether.Application.Tests/
  Ether.Infrastructure.Tests/
  Ether.Api.Tests/
  Ether.GameServer.Tests/
  Ether.Architecture.Tests/
docs/
  blueprints/             product/technical blueprints (v1.0–v5.0)
  decisions/              architecture decision records (ADRs)
content/                  versioned gameplay content + JSON schemas
client/                   Godot client
```

## Environments

| Environment | Role |
| --- | --- |
| Windows (local) | **Development environment.** Code, build and tests run here. |
| GitHub (`rafaelglima1/Project-ETHER`) | Versioning, audit and CI. |
| Oracle Cloud | **Official runtime.** Where the project actually runs. |
| Docker Engine + Docker Compose | **Official runtime technology on Oracle** (mandatory there). |
| Docker Desktop | Optional for local development. **Not a project requirement.** |

Container runtime validation (images, volumes, persistence, networking, restart,
healthchecks) is performed on the **Oracle Cloud** environment. The absence of
Docker Desktop on a Windows development machine does **not** block a milestone.
See [`docs/operations/oracle-runtime.md`](docs/operations/oracle-runtime.md) for
the runtime host, connection and deployment outline.

## Getting started

### Requirements

- .NET SDK **10.0.400** (pinned in `global.json`)
- Git
- Docker Desktop — **optional**, only if you want the local compose stack
  (PostgreSQL/Redis). Not required for build, test or development.

### Restore, build, test

```bash
dotnet restore
dotnet build
dotnet test
```

Architecture tests only:

```bash
dotnet test tests/Ether.Architecture.Tests/Ether.Architecture.Tests.csproj
```

Build output goes to `artifacts/` (see `Directory.Build.props`).

> NuGet sources are pinned to nuget.org in `NuGet.config` so restore is
> reproducible and does not depend on machine-level private feeds.

### Run locally

```bash
# API
dotnet run --project src/Ether.Api

# Game server
dotnet run --project src/Ether.GameServer

# Worker
dotnet run --project src/Ether.Worker
```

Health endpoints:

- `GET /health` — liveness (200 while the process is up)
- `GET /ready` — readiness (checks configured dependencies; 503 if a configured dependency is unreachable)

With no dependency configured, `/ready` returns 200 and reports them as
`not configured`.

### HTTP endpoints

Authentication:

| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| `POST` | `/auth/register` | — | Register an account → `201` |
| `POST` | `/auth/login` | — | Login → `200` access + refresh tokens |
| `POST` | `/auth/refresh` | — | Exchange a refresh token → `200` new pair |
| `GET` | `/auth/me` | Bearer (access) | Current account → `200` |

Accounts and characters (all require a Bearer **access** token):

| Method | Route | Description |
| --- | --- | --- |
| `POST` | `/accounts/{accountId}/characters` | Create a character → `201` |
| `GET` | `/accounts/{accountId}/characters` | List characters → `200` |
| `GET` | `/characters/{characterId}` | Get a character → `200` |
| `POST` | `/characters/{characterId}/enter` | Enter the world → `200` |
| `POST` | `/characters/{characterId}/move` | Move (server-validated) → `200` |
| `POST` | `/characters/{characterId}/game-token` | Issue a game token → `200` |
| `GET` | `/characters/{characterId}/inventory` | Read owned inventory → `200` |

Rules and status codes:

- Every character/account endpoint requires authentication → `401` without a token.
- Ownership is enforced: an account can only access its own characters, and the
  `accountId` in the token must match the route → `403` otherwise.
- Character names are globally unique (database constraint) → `409`.
- Unknown character → `404`; invalid input → `400`; unknown email/password → `401`.
- Tokens are purpose-scoped: refresh tokens and game tokens are **rejected** as
  bearer credentials.

### Game token

`POST /characters/{characterId}/game-token` returns a short-lived JWT
(`Authentication:GameTokenLifetimeMinutes`, default 5) whose claims carry
`accountId` and `characterId` with `use=game`. It is the credential the
**GameServer WebSocket** consumes at `/game`; the authenticated session provides
the character identity used by realtime commands and snapshots. See ADR-0003 for
the frozen envelope and session contract.

Persistence is PostgreSQL; migrations are in
`src/Ether.Infrastructure/Migrations` (`InitialAccountCharacter`,
`AddAccountCredentials`, `AddCharacterCombatStats`, `AddItemInstances`). ETHER-039
changes no database schema.

Apply migrations (script-based, controlled — no automatic migration at runtime):

```bash
dotnet ef migrations script --idempotent \
  --project src/Ether.Infrastructure --startup-project src/Ether.Infrastructure \
  --output migrations.sql
```


### Docker (optional locally, required on Oracle)

```bash
cp .env.example .env          # optional; adjust local values
docker compose up -d
docker compose down
```

Services: `postgres`, `redis`, `api` (8080), `game-server` (8081), `worker`.

> Docker Desktop is only needed to run the compose stack on Windows. If it is
> not available, the runtime stack is validated on Oracle Cloud instead — this is
> not a blocker. The `.env` file is git-ignored — never commit it.

### Production signing key

In `Production` the API, GameServer and Worker **fail fast at startup** if
`Authentication:SigningKey` is missing, too short (minimum 32 characters) or set
to the development placeholder (`development-only-signing-key-change-me`).
Development and Test keep the convenient defaults.


## Configuration

Configuration is bound from environment variables (double underscore for nesting):

| Option | Section | Notes |
| --- | --- | --- |
| `DatabaseOptions` | `Database` | PostgreSQL connection string |
| `RedisOptions` | `Redis` | Redis connection string |
| `AuthenticationOptions` | `Authentication` | JWT signing key, token lifetimes |
| `WebSocketOptions` | `WebSocket` | Gameplay endpoint path, heartbeat |
| `GameServerOptions` | `GameServer` | Tick rates, interest radius, grace period |
| `ContentOptions` | `Content` | Content bundle root directory (`content` by default) |

Example:

```bash
Database__ConnectionString="Host=localhost;Database=ether;Username=ether;Password=ether"
Redis__ConnectionString="localhost:6379"
```

Secrets are **never** versioned. Use environment variables or user secrets locally.

## Git workflow

Branches:

- `main` — stable, releasable.
- `develop` — integration branch; development happens here.
- `feature/*` — short-lived branches from `develop`.

Flow: `develop` → `feature/*` → `develop`. Feature branches are preferred even for
small changes.

Commit messages are small, focused and semantic:

```
feat:     new feature
fix:      bug fix
refactor: behaviour-preserving change
test:     tests
chore:    tooling, build, dependencies
docs:     documentation
```

Before committing, always review `git status` and `git diff`. Never commit build
output, local databases, logs or secrets.

## License

Proprietary. All rights reserved.
