namespace Supertext.OrchardCore.Translation.Api;

/// <summary>
/// Orchard Core cultures → Supertext language codes, by the Supertext SDK's rules (<c>Languages</c>,
/// the same in every Supertext plugin) and without guessing: the <b>Language mapping</b> setting
/// (<c>culture=code</c> per line) wins; else a culture that equals a Supertext code apart from case
/// and <c>_</c>/<c>-</c> (<c>de-CH</c>, <c>es</c>) is used as it is; anything else is the error
/// <c>unsupported_language</c> and nothing is sent. There are no default regions: a culture <c>de</c>
/// must be mapped (to <c>de-DE</c>, <c>de-CH</c> or <c>de-AT</c>). One mapping serves both directions:
/// as the source, a mapped code is sent in its source form (<c>de-CH</c> → <c>de</c>).
/// No Orchard Core dependency (unit-tested).
/// </summary>
public static class LanguageMapping
{
    /// <summary>Target languages Supertext offers (code, English name, whether formal/informal is offered).</summary>
    public static IReadOnlyList<Sdk.V1.TargetLanguage> Targets => Sdk.V1.Languages.Targets;

    /// <summary>The stored text (<c>culture=code</c> per line) as a dictionary; blank and malformed lines are skipped, the first entry per culture wins.</summary>
    public static IReadOnlyDictionary<string, string> Parse(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in (text ?? string.Empty).Split('\n'))
        {
            var pair = line.Split('=', 2);
            if (pair.Length == 2 && pair[0].Trim() != string.Empty && pair[1].Trim() != string.Empty)
            {
                result.TryAdd(pair[0].Trim(), pair[1].Trim());
            }
        }
        return result;
    }

    /// <summary>The setting's text for a mapping: one <c>culture=code</c> per line, blank codes left out.</summary>
    public static string Serialize(IEnumerable<KeyValuePair<string, string>> mapping)
        => string.Join("\n", mapping
            .Where(p => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => p.Key.Trim() + "=" + p.Value.Trim()));

    /// <summary>The Supertext target code for a culture.</summary>
    /// <exception cref="SupertextException">Template for <c>unsupported_language</c> when the culture is neither mapped nor a Supertext code.</exception>
    public static string Target(string culture, IReadOnlyDictionary<string, string> mapping)
        => Resolve(() => Sdk.V1.Languages.Target(culture, mapping));

    /// <summary>The Supertext source code for a culture ("en-US" → "en"); empty stays empty (Supertext detects the language).</summary>
    /// <exception cref="SupertextException">Template for <c>unsupported_language</c>.</exception>
    public static string Source(string culture, IReadOnlyDictionary<string, string> mapping)
        => Resolve(() => Sdk.V1.Languages.Source(culture, mapping));

    /// <summary>The target code, or null when the culture has none yet (for the settings page).</summary>
    public static string TargetOrNull(string culture, IReadOnlyDictionary<string, string> mapping)
    {
        try
        {
            return Sdk.V1.Languages.Target(culture, mapping);
        }
        catch (Sdk.V1.SupertextException)
        {
            return null;
        }
    }

    /// <summary>The Supertext target that equals the culture apart from case and <c>_</c>/<c>-</c> ("de_ch" → "de-CH"; "de" → null).</summary>
    public static string MatchTarget(string culture) => Sdk.V1.Languages.MatchTarget(culture);

    /// <summary>Whether Supertext offers formal/informal for a target code; null for a code that isn't in the SDK's list.</summary>
    public static bool? SupportsPoliteness(string target) => Sdk.V1.Languages.SupportsPoliteness(target);

    /// <summary>
    /// The form of address to send for a target: the setting, except "default" for a target Supertext
    /// offers no formal/informal choice for (English, Swiss German, Romansh, Albanian, Serbian), which it
    /// would reject. The settings page says so next to those languages. Unknown (mapped) codes get the setting.
    /// </summary>
    public static string PolitenessFor(string target, string politeness)
        => politeness is "more" or "less" && SupportsPoliteness(target) != false ? politeness : "default";

    private static string Resolve(Func<string> resolve)
    {
        try
        {
            return resolve();
        }
        catch (Sdk.V1.SupertextException e)
        {
            throw SupertextClient.FromSdk(e);
        }
    }
}
