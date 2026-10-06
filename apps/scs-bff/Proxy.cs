using System.Net;
using System.Text;
using System.Text.Json;

namespace Bff.Api;

/// <summary>
/// Forwards a request to an upstream SCS service, passing the validated
/// Authorization header through and returning the upstream payload verbatim.
/// </summary>
public static class Proxy
{
    public static async Task<ForwardResult> ForwardAsync(
        HttpClient client,
        HttpContext http,
        HttpMethod method,
        string path,
        object? body,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        var authorization = http.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authorization))
            request.Headers.Authorization = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(authorization);
        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        return new ForwardResult(response.StatusCode, text);
    }
}

public sealed record ForwardResult(HttpStatusCode Status, string Body)
{
    public int StatusCode => (int)Status;
}
