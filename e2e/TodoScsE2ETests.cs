namespace SportianArch.E2E;

/// <summary>
/// End-to-end scenarios for the Todo SCS, executed through the host platform
/// exactly like a real user would: browser -> host page -> <scs-todo-app>
/// (Shadow DOM) -> BFF -> .NET APIs -> Postgres/RabbitMQ.
///
/// Playwright CSS/text selectors pierce the (open) shadow root automatically,
/// so tests use the same data-testid hooks the widget exposes.
/// </summary>
public class TodoScsE2ETests : IClassFixture<E2EFixture>
{
    private readonly E2EFixture _fixture;
    private readonly Microsoft.Playwright.IPage Page;

    public TodoScsE2ETests(E2EFixture fixture)
    {
        _fixture = fixture;
        Page = fixture.Page;
    }

    private static string UniqueTitle(string prefix)
    {
        var title = $"{prefix} {Guid.NewGuid():N}";
        return title.Length <= 48 ? title : title[..48];
    }

    private const long DefaultTimeoutMs = 30_000;

    private static Microsoft.Playwright.LocatorWaitForOptions Visible(long timeout = DefaultTimeoutMs) =>
        new() { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = timeout };

    private static Microsoft.Playwright.LocatorWaitForOptions Hidden(long timeout = DefaultTimeoutMs) =>
        new() { State = Microsoft.Playwright.WaitForSelectorState.Hidden, Timeout = timeout };

    private async Task OpenHostAsync()
    {
        await Page.GotoAsync(_fixture.HostUrl);
        // The widget renders its card once the SCS bundle is loaded and mounted.
        await Page.Locator(".scs-header-title", new() { HasText = "SCS Todo Module" })
            .WaitForAsync(Visible());
    }

    private async Task<string> AddTodoAsync(string title)
    {
        await Page.GetByTestId("scs-todo-input").FillAsync(title);
        await Page.Locator(".scs-add-btn").ClickAsync();

        var item = Page.Locator(".scs-todo-item", new() { HasText = title });
        await item.WaitForAsync(Visible());
        return title;
    }

    private async Task CompleteTodoAsync(string title)
    {
        var item = Page.Locator(".scs-todo-item", new() { HasText = title });
        await item.Locator(".scs-complete-btn").ClickAsync();
        // Once completed the "Done" button is replaced by the check mark span.
        await item.Locator(".scs-complete-done").WaitForAsync(Visible());
    }

    [Fact]
    public async Task Host_Renders_And_Scs_Component_Becomes_Ready()
    {
        await OpenHostAsync();

        // The host console must have captured the scs:ready handshake,
        // announcing the component's name and version.
        var readyEntry = Page.Locator(".host-log-entry", new() { HasText = "scs:ready" });
        await readyEntry.WaitForAsync(Visible(timeout: 15_000));
        Assert.Contains("scs-todo-app", await readyEntry.InnerTextAsync());
    }

    [Fact]
    public async Task User_Can_Add_Todo_And_Host_Sees_The_Event()
    {
        await OpenHostAsync();
        var title = UniqueTitle("e2e add");
        await AddTodoAsync(title);

        // The todo is visible inside the shadow DOM list…
        var item = Page.Locator(".scs-todo-item", new() { HasText = title });
        await item.WaitForAsync(Visible());

        // …and the host platform observed the scs:todo-added event.
        await Page.Locator(".host-log-entry", new() { HasText = "scs:todo-added" })
            .WaitForAsync(Visible(timeout: 15_000));

        // Clean up so the scenario stays repeatable.
        await item.Locator(".scs-delete-btn").ClickAsync();
        await item.WaitForAsync(Hidden());
    }

    [Fact]
    public async Task Completed_Todo_Produces_Async_Notification_Via_RabbitMq()
    {
        await OpenHostAsync();
        var title = UniqueTitle("e2e complete");
        await AddTodoAsync(title);
        await CompleteTodoAsync(title);

        // The host must have observed scs:todo-completed…
        await Page.Locator(".host-log-entry", new() { HasText = "scs:todo-completed" })
            .WaitForAsync(Visible(timeout: 15_000));

        // …and the notification (todos-api -> RabbitMQ -> notifications-api ->
        // BFF dashboard -> widget poll) must show up in the widget.
        // (Class is .scs-notification; the data-testid is scs-notification-item.)
        var notification = Page.GetByTestId("scs-notification-item").Filter(new() { HasText = title });
        await notification.WaitForAsync(Visible(timeout: 45_000));
        Assert.Contains("was completed", await notification.InnerTextAsync());
    }

    [Fact]
    public async Task User_Switch_Isolates_Todo_Data()
    {
        await OpenHostAsync();
        var title = UniqueTitle("e2e isolation");
        await AddTodoAsync(title);
        var item = Page.Locator(".scs-todo-item", new() { HasText = title });
        await item.WaitForAsync(Visible());

        // Switch the signed-in host user; the widget remounts with a new token.
        await Page.SelectOptionAsync("#host-user", "grace");
        await Page.GetByTestId("scs-empty").WaitForAsync(Visible());

        // Switch back: the original todo is still there (scoped to Ada).
        await Page.SelectOptionAsync("#host-user", "ada");
        await item.WaitForAsync(Visible());

        await item.Locator(".scs-delete-btn").ClickAsync();
        await item.WaitForAsync(Hidden());
    }
}
