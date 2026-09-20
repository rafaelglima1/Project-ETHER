# ADR-0002 — Environments and infrastructure

- Status: Accepted
- Date: M0 remediation
- Context: Blueprint v5.0 (repository bootstrap) + ADR-0001
- Deciders: Project owner + engineering

## Context

Development currently happens on Windows, the repository is versioned on GitHub,
and the project must actually run somewhere reliable. The M0 audit flagged that
the documentation implied Docker Desktop was a project requirement, which is not
the intent. This ADR records the environment and infrastructure decision
explicitly. It does not change any decision made in Blueprint v5.0.

## Decisions

### D1 — Windows is the development environment

Development, build and tests run on a Windows workstation.

### D2 — Oracle Cloud is the official runtime

The project's official execution environment is **Oracle Cloud**.

### D3 — Docker Engine + Docker Compose are mandatory on the runtime

The Oracle Cloud runtime uses **Docker Engine + Docker Compose**. These are
required there, not optional.

### D4 — Docker Desktop is not a development requirement

Docker Desktop is an optional convenience for running the local compose stack on
Windows. It is **not** a requirement of the project and must not be described as
such.

### D5 — Missing Docker Desktop does not block a phase

The absence of Docker Desktop on a Windows development machine does not block a
milestone. When it is unavailable, container validation is deferred to the
Oracle environment and recorded as `BLOCKED_BY_LOCAL_ENVIRONMENT` — not as a
project failure.

### D6 — Runtime validation happens on Oracle

Real validation of images, containers, volumes, persistence, networking, restart
policies and healthchecks is performed on the Oracle Cloud runtime. Local
validation (when Docker is available) is best-effort and not authoritative.

## Notes

- PostgreSQL is served by the official `postgres:18-alpine` image. For
  PostgreSQL 18 the persistent volume targets `/var/lib/postgresql` (not the
  legacy `/var/lib/postgresql/data`).
- This ADR supersedes the earlier assumption that Docker Desktop was required
  for development. Blueprint v5.0 decisions remain unchanged.

## Consequences

- Documentation distinguishes development (Windows) from runtime (Oracle Cloud).
- CI runs on GitHub without depending on Docker.
- Container correctness is proven on the same environment that serves users.
