using System.Text;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Delegated-auth configuration: the SCS does not own identity. The host
/// platform mints a JWT (issuer "sportian-host", audience "scs") and the SCS
/// services only validate it. Demo uses a shared HS256 secret; production
/// would validate an OIDC IdP token via JWKS (see docs/ARCHITECTURE.md).
/// </summary>
internal sealed class ScsOptions
{
    public string Secret { get; }
    public string Issuer { get; }
    public string Audience { get; }
    public string[] AllowedOrigins { get; }

    public ScsOptions(IConfiguration configuration)
    {
        Secret = configuration["SCS:JwtSecret"] ?? "scs-demo-shared-secret-0123456789-0123456789";
        Issuer = configuration["SCS:JwtIssuer"] ?? "sportian-host";
        Audience = configuration["SCS:JwtAudience"] ?? "scs";
        AllowedOrigins = (configuration["SCS:AllowedOrigins"]
            ?? "http://localhost:5173,http://127.0.0.1:5173,http://localhost:8080,http://127.0.0.1:8080")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public TokenValidationParameters BuildTokenValidationParameters() => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
        ClockSkew = TimeSpan.FromSeconds(10),
    };
}
