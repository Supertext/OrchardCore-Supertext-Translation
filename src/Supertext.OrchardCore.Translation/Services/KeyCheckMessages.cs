using System.Globalization;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Modules;
using Supertext.OrchardCore.Translation.PluginHealth;

namespace Supertext.OrchardCore.Translation.Services;

/// <summary>
/// The API key check's result in the admin's language: the Supertext Commando Center's message
/// table (ok, invalid, missing, unreachable, error), with the account and API key links for
/// <c>invalid</c> and <c>missing</c> (new tab). Used by Settings → Supertext and its save notice.
/// </summary>
public sealed class KeyCheckMessages(IHtmlLocalizer<KeyCheckMessages> htmlLocalizer, ILocalClock localClock)
{
    public const string SignupUrl = "https://www.supertext.com/person/en/account/signin";
    public const string ApiKeyUrl = "https://www.supertext.com/en/integrations/api";

    private readonly IHtmlLocalizer H = htmlLocalizer;

    public LocalizedHtmlString Describe(KeyCheck check) => check.Status switch
    {
        KeyCheckStatus.Ok => H["API key checked: the connection to Supertext works."],
        KeyCheckStatus.Invalid => H["Supertext rejected the API key. Check that you copied the whole key, or generate a new one at <a href=\"{0}\" target=\"_blank\" rel=\"noopener\">supertext.com → Integrations → API</a> (requires the Admin role). No Supertext account yet? Create one at <a href=\"{1}\" target=\"_blank\" rel=\"noopener\">supertext.com</a>.", ApiKeyUrl, SignupUrl],
        KeyCheckStatus.Missing => H["No API key is set yet. No Supertext account yet? Create one at <a href=\"{1}\" target=\"_blank\" rel=\"noopener\">supertext.com</a>. Generate your API key at <a href=\"{0}\" target=\"_blank\" rel=\"noopener\">supertext.com → Integrations → API</a> (requires the Admin role).", ApiKeyUrl, SignupUrl],
        KeyCheckStatus.Unreachable => H["The API key couldn't be checked because Supertext can't be reached. The settings are saved; check again later."],
        _ => H["The API key couldn't be checked: Supertext answered with an error ({0}). The settings are saved; check again later.", check.HttpStatus?.ToString(CultureInfo.InvariantCulture) ?? check.ErrorCode ?? "?"],
    };

    /// <summary>"checked 14.10.2026 09:15" in the site's time zone and the admin's culture.</summary>
    public async Task<LocalizedHtmlString> CheckedAtAsync(KeyCheck check)
    {
        var time = check.CheckedAt;
        if (DateTimeOffset.TryParse(check.CheckedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            try
            {
                time = (await localClock.ConvertToLocalAsync(parsed)).ToString("g", CultureInfo.CurrentUICulture);
            }
            catch (Exception)
            {
                time = parsed.ToString("u", CultureInfo.InvariantCulture);
            }
        }
        return H["checked {0}", time];
    }
}
