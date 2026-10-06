# SportianArch — Self-Contained Systems (SCS) demo

A complete, end-to-end demonstration of the **Self-Contained System** pattern: a small
**Todo** vertical built as three independently deployable .NET 10 minimal APIs, wired
asynchronously with RabbitMQ, each owning its own Postgres database, with a React UI that
is **embeddable in any host platform** as a self-contained web component.

```
┌────────────────────────────────────────────────────────────────────────────┐
│  HOST PLATFORM (React SPA, :8080)                                         │
│  owns identity → mints a delegated JWT → embeds the SCS                   │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │  <scs-todo-app>  (web component, Shadow DOM, :5174 bundle)          │  │
│  │  talks to ONE origin only: the BFF · listens for scs:* events       │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────┬─────────────────────────────────────────┘
                                   │ HTTPS + Bearer JWT
                                   ▼
┌────────────────────────────────────────────────────────────────────────────┐
│  SCS-BFF (:8083)  thin aggregator/proxy — the SCS's single public face    │
└──────────────┬───────────────────────────────────────┬─────────────────────┘
               │ service-to-service (internal)         │
               ▼                                       ▼
┌──────────────────────────────┐        ┌──────────────────────────────────┐
│  SCS-TODOS-API (:8081)       │  ────► │  SCS-NOTIFICATIONS-API (:8082)   │
│  owns "todos" (Postgres)     │  async │  owns "notifications" (Postgres) │
│  publishes todo.completed    │ Rabbit │  consumes todo.completed         │
└──────────────────────────────┘  MQ    └──────────────────────────────────┘
```

## Why "self-contained"?

Each vertical is a small, bounded unit that a team can build, deploy and scale on its own:

- **Own database** (`scs_todos`, `scs_notifications`) — no cross-service JOINs, no shared schema.
- **Versioned, narrow API** (`/api/v1/...`) — the only synchronous contract.
- **Async integration events** (RabbitMQ, `todo.completed`) — services never call each other
  across bounded contexts for data that can be derived later.
- **Own UI** — the widget ships as one JS file and is embedded by the host with zero coupling
  (custom events in, nothing else).

## Repository layout

| Path | What it is |
| --- | --- |
| `apps/scs-todos-api` | .NET 10 minimal API — Todo CRUD, publishes `todo.completed` to RabbitMQ |
| `apps/scs-notifications-api` | .NET 10 minimal API — consumes `todo.completed`, stores notifications |
| `apps/scs-bff` | .NET 10 minimal API — thin BFF: `/api/v1/dashboard` aggregator + proxied writes |
| `apps/scs-web` | React UI compiled to a **`<scs-todo-app>` web component** (Shadow DOM) |
| `apps/host` | Demo host platform (React SPA) that embeds the SCS and shows the `scs:*` event console |
| `packages/shared` | TypeScript single source of truth: API DTOs, event payloads, host↔SCS event contract |
| `e2e` | .NET Playwright (xUnit) end-to-end tests — run against the full stack |
| `infra/postgres/init.sql` | Creates the two databases (one per service) |
| `docs/ARCHITECTURE.md` | Deep dive: embedding strategies, auth (JWT/OIDC), event flow |

## Prerequisites

- Docker (+ Docker Compose)
- Node.js ≥ 20.19 (npm workspaces)
- .NET SDK 10 (only for the e2e project and running APIs outside Docker)

## Quick start — full stack in Docker

```bash
docker compose up -d --build
```

That starts **everything**: Postgres, RabbitMQ (management UI on `:15672`, guest/guest),
the three .NET APIs, the SCS web component server and the host platform.

| URL | What |
| --- | --- |
| http://localhost:8080 | **Host platform** — embeds the SCS, user switcher, `scs:*` event console |
| http://localhost:5174 | SCS component bundle (`/scs-web.js`, served with CORS) |
| http://localhost:8083 | SCS BFF (`/api/v1/dashboard`, proxied todo writes) |
| http://localhost:8081 | scs-todos-api |
| http://localhost:8082 | scs-notifications-api |
| http://localhost:15672 | RabbitMQ management UI |
| `:8080..:8083/healthz` | Health probes (BFF also reports upstream status) |

Open **http://localhost:8080**, sign in as *Ada Lovelace* or *Grace Hopper*, add and complete a
todo, and watch:

1. the host platform receive `scs:todo-added` / `scs:todo-completed` custom events,
2. the `todo.completed` event travel through RabbitMQ (watch the queue in the management UI),
3. the notification appear in the widget — published by one service, stored by another.

## Local development (hot reload)

The .NET services stay in Docker; the frontends run locally with hot reload:

```bash
docker compose up -d --build postgres rabbitmq scs-todos-api scs-notifications-api scs-bff

npm install
npm run build -w @scs/shared

# two terminals:
npm run dev -w @scs/web     # vite build --watch + static server on :5174
npm run dev -w @scs/host    # host SPA with hot reload on :8080
```

## Authentication (demo)

The host platform owns identity. On load it mints a **delegated JWT** (HS256, demo shared
secret) with `iss=sportian-host`, `aud=scs`, `sub=<user-id>` and hands it to the widget via an
attribute. Every request the widget makes carries it as a bearer token; the .NET services
validate `iss`/`aud`/signature and scope all data by the `sub` claim.

> In production the host (or its OIDC identity provider) would sign the token with an
> **asymmetric key** and the SCS APIs would validate it via **JWKS** — the SCS never needs the
> host's identity internals. See `docs/ARCHITECTURE.md` § Authentication.

## End-to-end tests

```bash
docker compose up -d --build          # full stack
dotnet test e2e/SportianArch.E2E.csproj
```

Four browser-driven scenarios (Playwright + Chromium): host renders and the SCS reports ready,
add a todo (host sees the event), complete a todo (async RabbitMQ notification appears), and
user switching isolates data. See `e2e/README.md`.

## CI

Five **independent** pipelines (`.github/workflows/`): one per deployable unit
(`scs-todos-api`, `scs-notifications-api`, `scs-bff`, `frontend`) plus the `e2e` pipeline that
spins the whole stack in Docker and runs the Playwright suite. No pipeline depends on another —
each proves its own unit builds (and pushes its image to GHCR on `main`).
