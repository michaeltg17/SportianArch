using Microsoft.Playwright;

namespace SportianArch.E2E;

/// <summary>
/// Shared browser fixture: one Chromium instance for the whole test class.
/// The stack under test is the FULL local dev environment:
///   host (:8080) -> scs-web component (:5174) -> BFF (:8083) -> APIs (:8081/:8082)
///   -> Postgres + RabbitMQ (Docker).
/// </summary>
public sealed class E2EFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = default!;
    public IBrowser Browser { get; private set; } = default!;
    public IPage Page { get; private set; } = default!;

    public string HostUrl { get; } =
        Environment.GetEnvironmentVariable("SCS_HOST_URL") ?? "http://localhost:8080";

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
        Page = await Browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await Browser.CloseAsync();
        Playwright?.Dispose();
    }
}
