using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Supertext.OrchardCore.Translation.Api;
using Supertext.OrchardCore.Translation.Services;

namespace Supertext.OrchardCore.Translation.Controllers;

/// <summary>
/// <c>GET /supertext/ping?challenge=…</c>: public (no login, no session, no antiforgery token), so the
/// Supertext Commando Center can check that the site is reachable. Answers only with the SDK's
/// <c>PingResponse</c> (the challenge echoed, nothing else) and only while version reports are on (404 otherwise).
/// </summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public sealed class PingController(SupertextConfiguration configuration) : Controller
{
    [HttpGet("supertext/ping")]
    public async Task<IActionResult> Ping(string challenge = null)
    {
        var enabled = SupertextReporting.Enabled(await configuration.GetSettingsAsync());
        var answer = VersionReports.Ping(challenge, enabled);
        foreach (var (name, value) in answer.Headers)
        {
            if (!string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                Response.Headers[name] = value;
            }
        }
        return new ContentResult
        {
            StatusCode = answer.Status,
            ContentType = answer.Headers.TryGetValue("Content-Type", out var type) ? type : "application/json",
            Content = answer.Body,
        };
    }
}
