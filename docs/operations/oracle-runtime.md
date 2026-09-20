# Oracle Cloud runtime

The official runtime for Project Ether is **Oracle Cloud**. Development happens on
Windows; GitHub holds versioning/CI. See
[ADR-0002](../decisions/ADR-0002-environment-and-infrastructure.md).

## Connecting

Connection parameters are intentionally **not versioned**. They live in the local
SSH client configuration (`~/.ssh/config`) under the alias:

```bash
ssh ether-oracle
```

The private key is shared with the NEXO deployment and stored outside any
repository (`~/.ssh/nexo_deploy`). Never copy key material into this repository.

## Host baseline (verified)

| Item | Value |
| --- | --- |
| OS | Ubuntu, kernel `6.17.0-1020-oracle` |
| Docker Engine | `29.6.1` |
| Docker Compose | `v5.3.0` |

## Shared-host constraints

The host runs other workloads (e.g. `kallion-*` stacks). Before deploying:

- **PostgreSQL/Redis ports are free** on the host (`5432`/`6379`). Other stacks
  use `5433`/`6380`, so there is no clash — but this can change.
- **Host port `8080` is already occupied** by an existing listener. Set
  `API_HOST_PORT` to a free port when deploying here.
- Databases and Redis must **never** be exposed publicly. The compose file binds
  host ports to `127.0.0.1` by default; keep it that way.
- Expose public HTTP through a reverse proxy (ports 80/443 are already served on
  this host) rather than publishing app ports directly.

Port/bind overrides (all optional, see `.env.example`):

```
POSTGRES_HOST_BIND / POSTGRES_HOST_PORT
REDIS_HOST_BIND / REDIS_HOST_PORT
API_HOST_BIND / API_HOST_PORT
GAME_SERVER_HOST_BIND / GAME_SERVER_HOST_PORT
```

## Deploying the stack (outline)

Runtime validation (images, volumes, persistence, networking, restart,
healthchecks) is performed here, not on Windows.

1. `ssh ether-oracle`
2. Clone the repository to a dedicated directory (e.g. `~/project-ether`).
3. Create `.env` from `.env.example` and fill in real values — including a real
   `AUTHENTICATION_SIGNING_KEY`. `.env` is git-ignored; never commit it.
4. `docker compose up -d` (the compose project is named `project-ether`, which
   isolates networks/volumes from other stacks on the host).
5. Validate: `GET /health` = 200 and `GET /ready` = 200 (all configured
   dependencies reachable).

> With `ASPNETCORE_ENVIRONMENT=Production` the API, GameServer and Worker refuse
> to start without a valid signing key.

## Capacity snapshot

| Resource | Value |
| --- | --- |
| Disk `/` | ~11 GB free of 45 GB |
| Memory | ~15 GB free of 23 GB |

Disk is the main constraint — prune unused images before large builds.
