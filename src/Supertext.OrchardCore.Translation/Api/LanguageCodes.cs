namespace Supertext.OrchardCore.Translation.Api;

/// <summary>
/// CMS cultures → Supertext language codes, by the Supertext SDK's rules: the source as a
/// bare language ("en"), the target as a regional code ("de-DE", "en-US"; a bare target such
/// as "de" is rejected with INVALID_LANGUAGE_PAIR). This class adds the per-culture override
/// from the settings.
/// </summary>
public static class LanguageCodes
{
    /// <summary>The region Supertext uses when a culture has none (the SDK's table, the same in every Supertext plugin).</summary>
    public static IReadOnlyDictionary<string, string> DefaultRegion => Sdk.LanguageCodes.DefaultRegions;

    /// <summary>"de_ch" → "de-CH", "zh-hant" → "zh-Hant", "es-419" → "es-419", "DE" → "de".</summary>
    public static string Normalize(string code) => Sdk.LanguageCodes.Normalize(code);

    /// <summary>Target code: the override if set, else the culture; a code without region gets the default region. Unknown bare codes stay as they are.</summary>
    public static string Target(string culture, string overrideCode = null)
        => Sdk.LanguageCodes.Target(string.IsNullOrWhiteSpace(overrideCode) ? culture : overrideCode);

    /// <summary>Source code: the bare language of the override if set, else of the culture ("en-US" → "en").</summary>
    public static string Source(string culture, string overrideCode = null)
        => Sdk.LanguageCodes.Source(string.IsNullOrWhiteSpace(overrideCode) ? culture : overrideCode);
}
