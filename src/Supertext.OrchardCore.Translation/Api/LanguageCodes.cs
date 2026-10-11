namespace Supertext.OrchardCore.Translation.Api;

/// <summary>
/// CMS cultures → Supertext language codes. Supertext wants the source as a bare language
/// ("en") and the target as a regional code ("de-DE", "en-US"); a bare target such as "de"
/// is rejected with INVALID_LANGUAGE_PAIR. Same table as the Shopify app.
/// </summary>
public static class LanguageCodes
{
    /// <summary>The region Supertext uses when a culture has none.</summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultRegion = new Dictionary<string, string>
    {
        ["ar"] = "ar-SA", ["bg"] = "bg-BG", ["cs"] = "cs-CZ", ["da"] = "da-DK", ["de"] = "de-DE", ["el"] = "el-GR",
        ["en"] = "en-US", ["es"] = "es-ES", ["et"] = "et-EE", ["fi"] = "fi-FI", ["fr"] = "fr-FR", ["hr"] = "hr-HR",
        ["hu"] = "hu-HU", ["it"] = "it-IT", ["ja"] = "ja-JP", ["ko"] = "ko-KR", ["lt"] = "lt-LT", ["lv"] = "lv-LV",
        ["nb"] = "nb-NO", ["nl"] = "nl-NL", ["pl"] = "pl-PL", ["pt"] = "pt-PT", ["ro"] = "ro-RO", ["ru"] = "ru-RU",
        ["sk"] = "sk-SK", ["sl"] = "sl-SI", ["sr"] = "sr-RS", ["sv"] = "sv-SE", ["th"] = "th-TH", ["tr"] = "tr-TR",
        ["uk"] = "uk-UA", ["vi"] = "vi-VN", ["zh"] = "zh-CN",
    };

    /// <summary>"de_ch" → "de-CH", "zh-hant" → "zh-Hant", "es-419" → "es-419", "DE" → "de".</summary>
    public static string Normalize(string code)
    {
        var parts = code.Trim().Replace('_', '-').Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return string.Empty;
        }
        var result = new List<string> { parts[0].ToLowerInvariant() };
        foreach (var part in parts.Skip(1))
        {
            result.Add(part.Length == 2 ? part.ToUpperInvariant()
                : part.Length == 4 ? char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()
                : part);
        }
        return string.Join('-', result);
    }

    /// <summary>Target code: the override if set, else the culture; a code without region gets the default region. Unknown bare codes stay as they are.</summary>
    public static string Target(string culture, string overrideCode = null)
    {
        var code = Normalize(string.IsNullOrWhiteSpace(overrideCode) ? culture : overrideCode);
        return code.Contains('-') ? code : DefaultRegion.TryGetValue(code, out var regional) ? regional : code;
    }

    /// <summary>Source code: the bare language of the override if set, else of the culture ("en-US" → "en").</summary>
    public static string Source(string culture, string overrideCode = null) =>
        Normalize(string.IsNullOrWhiteSpace(overrideCode) ? culture : overrideCode).Split('-')[0];
}
