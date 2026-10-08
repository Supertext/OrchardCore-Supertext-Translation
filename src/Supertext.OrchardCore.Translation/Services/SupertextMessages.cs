using Microsoft.Extensions.Localization;
using Supertext.OrchardCore.Translation.Api;

namespace Supertext.OrchardCore.Translation.Services;

/// <summary>
/// Shows a <see cref="SupertextException"/> in the user's admin language: its English
/// template is the msgid (context <c>Supertext.OrchardCore.Translation.Services.SupertextMessages</c>
/// in <c>Localization/*.po</c>), the detail from Supertext is appended untranslated.
/// </summary>
public sealed class SupertextMessages(IStringLocalizer<SupertextMessages> stringLocalizer)
{
    private readonly IStringLocalizer S = stringLocalizer;

    public string Describe(SupertextException e)
    {
        var text = S[e.Template, e.Args].Value;
        return e.Detail is null ? text : $"{text} ({e.Detail})";
    }
}
