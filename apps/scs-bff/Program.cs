using Microsoft.AspNetCore.Authentication.JwtBearer;
using Bff.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var scs = new ScsOptions(builder.Configuration);
var todosApiUrl = builder.Configuration["SCS:TodosApiUrl"] ?? "http://localhost:8081";
var notificationsApiUrl = builder.Configuration["SCS:NotificationsApiUrl"] ?? "http://localhost:8082";

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

builder.Services.AddHttpClient("todos", client =>
{
    client.BaseAddress = new Uri(todosApiUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient("notifications", client =>
{
    client.BaseAddress = new Uri(notificationsApiUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new
{
    service = "scs-bff",
    version = "v1",
    status = "healthy",
    upstreams = new { todosApi = todosApiUrl, notificationsApi = notificationsApiUrl },
    time = DateTime.UtcNow,
}));

app.MapBffApi();

app.Run();
