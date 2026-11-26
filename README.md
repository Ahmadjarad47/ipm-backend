# IPM Backend — نظام إدارة المشاريع الداخلي

REST + real-time API for the **Internal Project Management (IPM)** system (SRS `IPM-SRS-001 v1.1`).
Built module by module following the [delivery pipeline](docs/MODULE-PIPELINE.md) and the [roadmap](docs/ROADMAP.md).

> Front end: [`ipm-web`](https://github.com/Ahmadjarad47/ipm-web) (Angular 22).

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white) ![PostgreSQL 17](https://img.shields.io/badge/PostgreSQL-17-4169E1?logo=postgresql&logoColor=white) ![Redis](https://img.shields.io/badge/Redis-7-DC382D?logo=redis&logoColor=white) ![SignalR](https://img.shields.io/badge/real--time-SignalR-512BD4) ![License: MIT](https://img.shields.io/badge/license-MIT-green)

## The idea

Organisations run their projects across chat threads, spreadsheets and tools that each force a fixed process. **IPM** is one **single source of truth** for projects, tasks, owners and **approval decisions**:

- **Nothing is hard-coded.** Admins define work-item types, custom fields, statuses, transitions, approval chains and roles, globally or per project.
- **Every decision is accountable.** Each change records who made it, when and who approved it, in an audit log that is hash-chained so tampering can be detected.
- **Governance built into the workflow.** Status transitions can wait for a multi-stage approval, sensitive edits go through approval, and nobody can approve their own request.
- **Connected to the code.** Git repositories (GitHub, GitLab, Gitea) link commits and PRs to work items automatically.

The result is an Arabic-first, self-hosted platform in the spirit of OpenProject and Jira, focused on governance and auditability. It exposes **130+ REST endpoints** and is covered by **400+ automated tests**.

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

## Configuration

### Telegram notifications (optional)

Users link their Telegram from their profile; work items assigned to them then arrive in a private chat with the bot ([spec 11](docs/modules/11-telegram-notifications.md)).
Off by default; without a token the system runs exactly as before.

1. Create a bot with [@BotFather](https://t.me/BotFather).
2. Copy `.env.example` to `.env` (git ignores it) and fill in `TELEGRAM_BOT_TOKEN` and `TELEGRAM_BOT_USERNAME`; `docker compose up -d --build` reads them.
3. Or set the `Telegram` section directly (environment variables use `__`):

| Setting | Meaning |
|---|---|
| `Telegram:Enabled` | `true` to turn the feature on. Enabled with an empty token behaves as disabled |
| `Telegram:BotToken` / `Telegram:BotUsername` | from BotFather (the token is a secret: environment or secret store only, never a committed file) |
| `Telegram:Mode` | `Polling` (default, no public address needed: development) or `Webhook` (production) |
| `Telegram:WebhookUrl` / `Telegram:WebhookSecret` | Webhook mode: the public https address of `/api/v1/telegram/webhook`, and a secret of 1-256 characters (`A-Za-z0-9_-`) that Telegram sends back in `X-Telegram-Bot-Api-Secret-Token` |
| `Telegram:AppBaseUrl` | where the web app lives; used for the link in each message (an inline button is added only for https) |
| `Telegram:MessagesPerSecond` | global send cap, default 25 (Telegram allows about 30); each chat gets at most one message per second |

Messages are queued in an outbox in the same transaction as the notification and sent by a background worker, with retries (5 s doubling to 10 min, 8 attempts) and `retry_after` handling.
The token is part of every Bot API URL, so that HTTP client has no logging.

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
| `Ipm.RealTime` | SignalR hubs, the notification pusher and the hub token rule — SignalR lives only here; depends on Application only |
| `Ipm.Api` | minimal-API endpoints (no business logic), auth, OpenAPI, composition root (wires Infrastructure and RealTime) |

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

| 11 | Telegram notifications | [11-telegram-notifications](docs/modules/11-telegram-notifications.md) |

Attachments are out of scope for the first release (owner decision): the `files.*` permissions and attachment settings exist and are stored, but upload/download is not implemented.

## Delivery process

Every module goes through fixed stages — spec (owner approval required) → domain → storage → application → API → delivery — and no code is written before its spec is approved. Details in [docs/MODULE-PIPELINE.md](docs/MODULE-PIPELINE.md); front-end integration notes in [docs/INTEGRATION.md](docs/INTEGRATION.md).
