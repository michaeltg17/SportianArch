# Architecture

This document explains *how* the demo is wired and the decisions behind it. The repo
implements a single Todo vertical as a **Self-Contained System (SCS)**: a set of small,
independently deployable services plus one embeddable UI, integrated through narrow contracts.

## 1. The pieces

| Unit | Tech | Owns | Exposes |
| --- | --- | --- | --- |
| `scs-todos-api` | .NET 10 minimal API, EF Core + Npgsql | the `todos` table (`scs_todos` DB) | `/api/v1/todos` CRUD + `/complete`; publishes `todo.completed` |
| `scs-notifications-api` | .NET 10 minimal API, EF Core + Npgsql | the `notifications` table (`scs_notifications` DB) | `/api/v1/notifications`; consumes `todo.completed` |
| `scs-bff` | .NET 10 minimal API | nothing (stateless) | `/api/v1/dashboard` (aggregator), proxied todo writes — the **single origin** the UI talks to |
| `scs-web` | React 19 → **one ES module** (`scs-web.js`) | nothing | the `<scs-todo-app>` custom element (Shadow DOM) |
| `host` | React 19 SPA | identity (who is signed in) | the embed point + the `scs:*` event console |
| infra | PostgreSQL 16, RabbitMQ 3.13 | data, messaging | one DB per service, one topic exchange |

### Why three services for one small feature?

To make the pattern concrete:

- **Database-per-service.** `scs-todos-api` and `scs-notifications-api` never share tables or
  even databases. Each maps its own entities and runs its own migrations. If the notification
  schema changes, the todo service is unaffected.
- **Async by default.** Completing a todo *happens* in the todo context. "The user should be
  notified" is a *consequence* that belongs to the notification context. The two are connected
  by an event (`todo.completed` on the `scs.todos` topic exchange), not a synchronous call.
  The consumer can be down, slow, or scaled independently — the durable queue absorbs it.
- **A BFF, because UIs have different needs.** The widget wants one round-trip that returns
  "my todos + my notifications + any soft warnings". The BFF fans out to the two services
  *server-side* (service-to-service, internal network) and merges the results. The BFF is
  **thin on purpose**: it adds presentation-level aggregation, no business rules. It degrades
  gracefully — if an upstream is down it returns the other half plus a `warnings` entry instead
  of a 500.
- **Versioned contracts.** All endpoints live under `/api/v1`. The JSON shapes are the contract;
  `packages/shared` mirrors them for the TypeScript side so the two ecosystems can't drift
  silently.

## 2. The event flow (the heart of the SCS)

```
complete todo (todos-api)
  → UPDATE todos (Postgres)
  → BasicPublish  exchange=scs.todos  routingKey=todo.completed
                  body={ messageId, todoId, title, userId, completedAt }
  → 200 OK
        │  (durable queue: notifications.todo.completed)
        ▼
notifications-api consumer (BackgroundService)
  → deserialize + validate
  → dedupe (already stored for this source+type? → ack and skip)
  → INSERT notifications (Postgres)
  → BasicAck
        ▼
widget polls BFF /dashboard → notification rendered
```

Properties that matter:

- Exchange `scs.todos` is a **topic** exchange, durable; routing key `todo.completed`.
  More event types can join the same exchange (`todo.deleted`, `todo.created`, …) without
  touching existing consumers — each consumer binds the keys it cares about.
- Messages are **persistent** (`deliveryMode=2`), the queue is durable, consumers use
  `prefetch=10` and manual ack — a message is only acked after it is **stored**, so a consumer
  crash replays it (at-least-once delivery → the dedupe check).
- The consumer reconnects: on `ConnectionShutdown` initiated by the peer it tears down the
  channel and re-establishes it, re-declaring exchange/queue/bindings (idempotently).
- The event carries `userId` (the `sub` claim at publish time) so the consumer can store the
  notification in the right user's space without asking the todo service back.

## 3. Embedding the SCS UI — the three strategies, plainly

A microfrontend question is really: *how does a second team's UI get into my page?* There are
three standard answers. Each trades off isolation, coupling and complexity differently.

### Strategy A — Web component (what this repo uses)

The SCS UI is compiled to **one self-contained ES module** that registers a custom element:

```html
<script type="module" src="https://scs.example.com/scs-web.js"></script>

<scs-todo-app
  scs-web-url="https://bff.example.com"
  scs-token="eyJ..."
  scs-user="Ada Lovelace">
</scs-todo-app>
```

- **How it isolates.** The element renders inside a **Shadow DOM**: the host's CSS can't
  reach in, the SCS's CSS can't leak out. Styling is themed through CSS custom properties.
- **How it communicates.** Only through **composed custom events** (`scs:ready`,
  `scs:todo-added`, `scs:todo-completed`, `scs:error`) that bubble out of the shadow boundary,
  plus the REST calls it makes to its own BFF. No shared globals, no shared framework, no
  shared npm packages — React is bundled *inside* `scs-web.js`.
- **How the host integrates.** Add a `<script>` tag, add the element, set a few attributes,
  listen to a few events. That's the entire integration surface. The host doesn't need to know
  the SCS uses React, or TypeScript, or any particular version of anything.
- **Costs.** Slightly larger payload (the framework ships with the component), and you manage
  the config channel (attributes) and event contract yourself. No build-time coupling, though:
  the two sides can be released independently.
- **When to pick it.** Default choice for embedding a *feature* (a widget, a panel, a form)
  into a page you don't control. Maximum isolation, minimum ceremony.

### Strategy B — Module Federation (shared runtime)

Webpack/Vite **Module Federation** exposes named *remote entries* that the host loads at
**runtime** and consumes as first-class modules:

```js
// scs-web exposes:  { "./TodoApp": "./src/TodoApp.tsx" }
// host consumes:    const { TodoApp } = await import("scs_web/TodoApp");
```

- **How it differs.** The remote is a *module*, not an element: the host renders it inside its
  own React tree (`<Suspense>` and all), and both sides can **share** framework instances
  (one React for both) via shared-scope configuration.
- **Benefits.** No DOM/Shadow-DOM boundary to think about; the host styles and composes the
  remote like its own component; smaller payloads when frameworks are shared.
- **Costs.** A real **build-time contract**: shared dependency versions must line up
  (two Reacts = bugs), the host must understand the remote's export shape, and releases are
  more coupled. It's a *runtime* boundary, not a *process* boundary.
- **When to pick it.** Multiple teams build one cohesive app (shared design system, shared
  state) and you want component-level reuse *inside* a single React world.

### Strategy C — iframe + postMessage

The SCS UI is a full page served by the SCS, embedded in an `<iframe>`; the host and the SCS
talk with `window.postMessage` (origin-checked, typed messages).

- **How it differs.** The strongest isolation available: separate document, separate origin,
  separate CSP, separate JS heap. The SCS's page is just… served where it already is.
- **Benefits.** Zero build coupling (it's just a URL), hard security isolation, trivial to
  adopt anything (even a legacy app).
- **Costs.** UX friction (scrolling, focus, sizing, popups), `postMessage` is a manual
  protocol you have to design and version, and shared layout/styling with the host page is
  awkward.
- **When to pick it.** Embedding **third-party or untrusted** UIs, or UIs that must stay
  completely independent (different vendor, different security domain).

### Summary table

| | A: Web component | B: Module Federation | C: iframe |
| --- | --- | --- | --- |
| Isolation | DOM + styles (shadow) | Runtime module boundary | Full document/origin |
| Host needs to know | a tag + a few events | the remote's exports + versions | a URL + a message protocol |
| Shared framework | none (bundled in) | yes (single React) | none |
| Release coupling | none | medium (versions) | none |
| Best for | features embedded in pages you don't own | one app, many teams | untrusted / fully separate UIs |

This demo uses **A**: the Todo module is a feature that the host embeds without owning — the
textbook case for a self-contained web component.

## 4. Authentication

### Demo: delegated JWT, HS256

The host platform is the identity provider for the demo:

1. The host "signs the user in" (the user switcher) and **mints a JWT client-side**
   (`WebCrypto` HMAC-SHA256) with a shared demo secret.
2. Claims: `iss=sportian-host`, `aud=scs`, `sub=<user-id>`, `name`, `iat`, `exp` (1 h).
3. The token is passed to the widget as an attribute; the widget attaches it as
   `Authorization: Bearer …` to every BFF call.
4. The BFF and both APIs validate issuer, audience, signature and expiry
   (`MapInboundClaims=false` so `sub` stays `sub`), and **all queries are scoped by `sub`** —
   users never see each other's data, even though everything runs on the same machine.

The BFF forwards the bearer token downstream, so the whole chain authenticates with the same
delegated identity (there is no separate service identity in the demo; in a larger system the
BFF would use its own service credential for internal calls).

### Production shape: OIDC + JWKS

In a real deployment the host is itself a client of an identity provider (Keycloak, Entra ID,
Auth0, …):

1. The host completes a normal **OIDC** login (authorization code + PKCE) and obtains the
   user's identity token.
2. The IdP issues the token with an **asymmetric key** (RS256/ES256); the host never sees a
   signing secret.
3. The SCS APIs (and BFF) configure `JwtBearer` with the IdP's **JWKS** endpoint
   (`Authority` + `TokenValidationParameters`). Key rotation is handled for them; no shared
   secret exists anywhere.
4. The SCS should validate `iss`, `aud` (its own audience), `sub`, and the minimum claims it
   needs — and ignore the rest. That's the whole integration: the SCS knows *nothing* about
   the host's identity internals, by design.

The demo keeps HS256 + shared secret only because it lets the entire stack run with zero
external infrastructure; the code path (token in → claims out → `sub` scoping) is identical.

## 5. Cross-cutting decisions

- **CORS.** Each .NET service allows exactly the host origins (`:8080` dev/Docker, `:5173`
  legacy dev port). The BFF is the only origin the browser ever talks to for SCS data; the
  other APIs are reachable cross-origin too, defensively, but the widget is written against
  the BFF alone.
- **Rate limiting & error shape.** APIs map domain errors to clean status codes
  (401/404/409) with `{ "error": "..." }` bodies; the widget turns any failure into an
  `scs:error` event *and* an inline message, so the host always has a machine-readable signal.
- **Config via environment variables** (`SCS__*`), 12-factor style; the compose file is the
  single source of defaults, overridable per environment.
- **Health.** Every service exposes `/healthz`; the BFF's health includes an upstream report,
  so a single probe tells you whether the SCS as a whole is functional.
- **Schema provisioning.** The demo uses `Database.EnsureCreatedAsync()` at startup (dev
  ergonomics); in production each service would own **EF migrations** for its own database —
  still database-per-service, still no cross-DB coordination.
- **Monorepo, independent pipelines.** Everything lives in one repo (contracts next to their
  implementations), but CI treats each deployable unit independently — the SCS boundary is
  organizational, not just technical.
