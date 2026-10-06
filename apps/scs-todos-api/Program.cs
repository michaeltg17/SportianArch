using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.Client;
using Todos.Api.Domain;
using Todos.Api.Endpoints;
using Todos.Api.Infrastructure;

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

var todosConnection = builder.Configuration.GetConnectionString("TodosDb")
    ?? throw new InvalidOperationException("ConnectionStrings:TodosDb is required.");
var rabbitUrl = builder.Configuration.GetConnectionString("RabbitMq")
    ?? throw new InvalidOperationException("ConnectionStrings:RabbitMq is required.");

builder.Services.AddDbContextFactory<TodosDbContext>(options => options.UseNpgsql(todosConnection));
builder.Services.AddScoped<ITodoStore, EfTodoStore>();
builder.Services.AddSingleton<IConnectionFactory>(_ =>
{
    var factory = new ConnectionFactory();
    factory.Uri = new Uri(rabbitUrl);
    return factory;
});
builder.Services.AddSingleton<IEventPublisher, RabbitEventPublisher>();

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Map domain rule violations to proper HTTP errors.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (ArgumentException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

// Demo bootstrap: create the schema on first run. A production SCS would ship
// versioned EF Core migrations applied by its own pipeline.
var dbFactory = app.Services.GetRequiredService<IDbContextFactory<TodosDbContext>>();
await using (var db = await dbFactory.CreateDbContextAsync())
{
    await db.Database.EnsureCreatedAsync();
}

app.MapGet("/healthz", () => Results.Ok(new
{
    service = "scs-todos-api",
    version = "v1",
    status = "healthy",
    time = DateTime.UtcNow,
}));

app.MapTodosApi();

app.Run();
