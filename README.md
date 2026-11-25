# IPM Backend — نظام إدارة المشاريع الداخلي

REST + real-time API for the **Internal Project Management (IPM)** system (SRS `IPM-SRS-001 v1.1`).
Built module by module following the [delivery pipeline](docs/MODULE-PIPELINE.md) and the [roadmap](docs/ROADMAP.md).

> Front end: [`ipm-web`](https://github.com/Ahmadjarad47/ipm-web) (Angular 22).

**Stack:** .NET 10 · Clean Architecture · CQRS (Mediator) · EF Core + PostgreSQL 17 · Redis 7 · SignalR · FluentValidation · Serilog + Seq · Scalar (OpenAPI).

## Capabilities

- **Identity & permissions** — JWT + refresh tokens, default-deny permission keys at system and project scope, explicit denies, lockout and rate limiting.
- **Work management** — projects, members, templates, hierarchical work items, relations (cycle-safe), comments, configurable types, custom fields and workflows.
- **Approvals engine** — templates and rules, sequential stages, self-approval ban, delegation, admin override, transition gating, supersede on protected-field change, sensitive-change requests applied atomically.
- **Git integration** — repositories, signed webhooks (HMAC, de-duplication, replay protection) for Gitea / GitHub / GitLab, automatic `PROJ-n` linking.
- **Activity, audit & notifications** — tamper-evident hash-chained audit log with verify/export, activity feeds, notifications pushed over SignalR (`/api/v1/hubs/notifications`).
- **Dashboard, search, settings & reports** — KPIs, timeline, global search, system settings, work report exported to Excel.

## Quick start

Prerequisites: .NET SDK 10 (`global.json`) and Docker.

```powershell
docker compose up -d --build     # Postgres :55432, Redis :6379, Seq :5341, API :8080
```

| Endpoint | URL |
|---|---|
| Health | http://localhost:8080/health |
| API docs (Scalar) | http://localhost:8080/docs (spec: `/openapi/v1.json`) |
| Logs (Seq) | http://localhost:5341 |
| Request samples | [docs/http](docs/http) |

Run the API from source against containerised dependencies:

```powershell
docker compose up -d postgres redis seq
dotnet run --project src/Ipm.Api        # http://localhost:5150
```

**Development accounts** (seed data, dev only): `admin` / `Admin12345`; `khaled`, `noura`, `fahad`, `reem`, `abdullah` / `Demo12345`.
Demo projects: `ERP`, `SSP`, `BI`, `MAIL` (archived). Production uses environment variables, never these credentials.

## Build & test

```powershell
dotnet build Ipm.slnx            # warnings are errors
dotnet test Ipm.slnx             # integration tests need Docker (Testcontainers)
```

| Suite | Scope |
|---|---|
| `Ipm.Domain.UnitTests` | domain rules and invariants |
| `Ipm.ArchitectureTests` | enforces layer dependencies and folder/type conventions |
| `Ipm.IntegrationTests` | full HTTP stack on real PostgreSQL + Redis, SignalR client tests |

## Architecture

```
Domain  ←  Application  ←  Infrastructure  ←  Api
```

Dependencies point inward only — enforced by `ArchitectureTests`.

| Layer | Responsibility |
|---|---|
| `Ipm.Domain` | entities, value objects, domain errors and events — no framework dependencies |
| `Ipm.Application` | commands/queries/handlers (one folder per operation, one type per file), validators, abstractions |
| `Ipm.Infrastructure` | EF Core, Identity, Redis, Git providers, report writer (ClosedXML), notifications |
| `Ipm.Api` | minimal-API endpoints (no business logic), auth, OpenAPI, SignalR hub, composition root |

Cross-cutting conventions:

- **Result/Error** pattern end to end; failures surface as Arabic `application/problem+json`.
- **Optimistic concurrency** through `ETag` / `If-Match` (409 / 412 / 428).
- **Audit log** for every state-changing operation, hash-chained for tamper evidence.
- **Permission-keyed endpoints** — each endpoint declares the permission it needs.
- Central package management (`Directory.Packages.props`).

## Modules

| # | Module | Spec |
|---|---|---|
| 00 | Foundation | [00-foundation](docs/modules/00-foundation.md) |
| 01 | Identity & permissions | [01-identity](docs/modules/01-identity.md) |
| 02 | Work item types | [02-work-item-types](docs/modules/02-work-item-types.md) |
| 03 | Workflows | [03-workflows](docs/modules/03-workflows.md) |
| 04 | Projects | [04-projects](docs/modules/04-projects.md) |
| 05 | Work items | [05-work-items](docs/modules/05-work-items.md) |
| 05b | Relations & comments | [05b-relations-comments](docs/modules/05b-relations-comments.md) |
| 06 | Approvals engine | [06-approvals](docs/modules/06-approvals.md) |
| 06b | Sensitive changes | [06b-sensitive-changes](docs/modules/06b-sensitive-changes.md) |
| 07 | Git integration | [07-git-integration](docs/modules/07-git-integration.md) |
| 08 | Activity, audit & notifications | [08-activity-audit-notifications](docs/modules/08-activity-audit-notifications.md) |
| 09 | Dashboard, search & settings | [09-dashboard-search-settings](docs/modules/09-dashboard-search-settings.md) |
| 10 | Reports | [10-reports](docs/modules/10-reports.md) |

Attachments are out of scope for the first release (owner decision): the `files.*` permissions and attachment settings exist and are stored, but upload/download is not implemented.

## Delivery process

Every module goes through fixed stages — spec (owner approval required) → domain → storage → application → API → delivery — and no code is written before its spec is approved. Details in [docs/MODULE-PIPELINE.md](docs/MODULE-PIPELINE.md); front-end integration notes in [docs/INTEGRATION.md](docs/INTEGRATION.md).
