using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Notifications.Api.Domain;
using Notifications.Api.Endpoints;
using Notifications.Api.Infrastructure;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

var scs = new ScsOptions(builder.Configuration);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(scs.AllowedOrigins)
    .AllowAnyHeader()
    .WithMethods("GET", "POST", "PATCH", "DELETE", "OPTIONS")));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep claim names as issued: sub, name
        options.TokenValidationParameters = scs.BuildTokenValidationParameters();
    });
builder.Services.AddAuthorization();

var notificationsConnection = builder.Configuration.GetConnectionString("NotificationsDb")
    ?? throw new InvalidOperationException("ConnectionStrings:NotificationsDb is required.");
var rabbitUrl = builder.Configuration.GetConnectionString("RabbitMq")
    ?? throw new InvalidOperationException("ConnectionStrings:RabbitMq is required.");

builder.Services.AddDbContextFactory<NotificationsDbContext>(options => options.UseNpgsql(notificationsConnection));
builder.Services.AddScoped<INotificationStore, EfNotificationStore>();
builder.Services.AddSingleton<IConnectionFactory>(_ =>
{
    var factory = new ConnectionFactory();
    factory.Uri = new Uri(rabbitUrl);
    return factory;
});
builder.Services.AddHostedService<RabbitEventConsumer>();

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Demo bootstrap: create the schema on first run. A production SCS would ship
// versioned EF Core migrations applied by its own pipeline.
var dbFactory = app.Services.GetRequiredService<IDbContextFactory<NotificationsDbContext>>();
await using (var db = await dbFactory.CreateDbContextAsync())
{
    await db.Database.EnsureCreatedAsync();
}

app.MapGet("/healthz", () => Results.Ok(new
{
    service = "scs-notifications-api",
    version = "v1",
    status = "healthy",
    time = DateTime.UtcNow,
}));

app.MapNotificationsApi();

app.Run();
