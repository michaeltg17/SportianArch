# e2e — .NET Playwright tests

Browser-driven end-to-end tests for the Todo SCS, written in **xUnit + Microsoft.Playwright
(.NET 10)**. They exercise the *whole* stack exactly like a user would:

```
Chromium → host page (:8080) → <scs-todo-app> (Shadow DOM, :5174) → BFF (:8083)
        → scs-todos-api (:8081) / scs-notifications-api (:8082) → Postgres + RabbitMQ
```

Playwright's CSS and text selectors pierce the (open) Shadow DOM automatically, so the tests
target the same `data-testid` hooks the widget exposes — no test-only backdoors in the UI.

## Scenarios

| Test | Proves |
| --- | --- |
| `Host_Renders_And_Scs_Component_Becomes_Ready` | host loads the component; the `scs:ready` handshake reaches the host console |
| `User_Can_Add_Todo_And_Host_Sees_The_Event` | create a todo through the widget; the host observes `scs:todo-added` |
| `Completed_Todo_Produces_Async_Notification_Via_RabbitMq` | complete a todo → `todo.completed` travels RabbitMQ → the notification renders in the widget (async chain) |
| `User_Switch_Isolates_Todo_Data` | switching the signed-in host user remounts the widget with a new JWT; data is scoped per user |

## Running locally

1. Start the full stack (all-in-Docker is the simplest):

   ```bash
   docker compose up -d --build
   ```

2. Install the Playwright browser (once, per machine):

   ```powershell
   dotnet build e2e/SportianArch.E2E.csproj
   pwsh .\e2e\bin\Debug\net10.0\Playwright.ps1 install chromium
   ```

3. Run the suite:

   ```powershell
   dotnet test e2e/SportianArch.E2E.csproj
   ```

The host URL defaults to `http://localhost:8080`; override with the `SCS_HOST_URL`
environment variable (e.g. to point at a remote environment).

## In CI

`.github/workflows/e2e.yml` (an independent pipeline) does exactly the same on
`ubuntu-latest`: `docker compose up -d --build`, waits for the five `/healthz` endpoints,
installs Chromium **with system dependencies** (`Playwright.ps1 install --with-deps chromium`),
and runs `dotnet test`. On failure it dumps `docker compose logs` for diagnosis.

## Notes

- Tests share one browser and one page (xUnit `IClassFixture`), running sequentially; each
  scenario uses a unique todo title (guid suffix) so reruns don't collide.
- The async-notification scenario allows up to 45 s: it covers publish → consume → store →
  widget poll (the widget refreshes its dashboard on a 10 s interval by design — no push
  channel in the demo).
- No unit tests exist on purpose for this demo: the SCS contract is the *behavior of the
  deployed system*, which is what this suite verifies.
