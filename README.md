# Project Ether

> "Um mundo que continua existindo quando você fecha o jogo."

Project Ether is a **2D mobile-first MMORPG**: a persistent world with real-time
combat, deep progression, player-driven economy and a strong social layer —
designed to work both in ~5-minute sessions and in long sessions, without
auto-play and without pay-to-win.

The product intent is captured in the blueprints under [`docs/blueprints`](docs/blueprints/README.md).
The current technical contract is **Blueprint v5.0**.

## Status

**Milestone M0 — Foundation.**

M0 delivers a clean, compilable, testable base for the project. No gameplay is
implemented. See [`docs/decisions/ADR-0001`](docs/decisions/ADR-0001-blueprint-precedence-and-m0-scope.md)
for the canonical decisions fixed at M0.

| Milestone | Scope | Status |
| --- | --- | --- |
| M0 | Solution, projects, architecture tests, health checks, Docker, CI | ✅ Done |
| M1 | First character (account, auth, character, persistence) | ⏭ Next |
| M2 | First world (WebSocket, spawn, movement, snapshot/delta, reconnect) | — |
| M3 | First combat (creatures, AI, damage, death, loot) | — |
| M4 | First progression (inventory, equipment, XP, level death/respawn) | — |
| M5 | First social (two players, chat, party) | — |
| M6 | First quest (NPC, dialogue, objectives, rewards) | — |

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
client/                   Godot client (future milestone)
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
