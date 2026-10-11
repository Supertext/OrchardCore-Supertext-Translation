using Supertext.OrchardCore.Translation.Api;
using Xunit;

namespace Supertext.OrchardCore.Translation.Tests;

public class LanguageCodesTests
{
    [Theory]
    [InlineData("en", "en")]
    [InlineData("en-US", "en")]
    [InlineData("en_gb", "en")]
    [InlineData("de-CH", "de")]
    public void Source_is_the_bare_language(string culture, string expected) =>
        Assert.Equal(expected, LanguageCodes.Source(culture));

    [Theory]
    [InlineData("de", "de-DE")]
    [InlineData("fr", "fr-FR")]
    [InlineData("en", "en-US")]
    [InlineData("DE", "de-DE")]
    public void Bare_target_gets_the_default_region(string culture, string expected) =>
        Assert.Equal(expected, LanguageCodes.Target(culture));

    [Theory]
    [InlineData("de-CH", "de-CH")]
    [InlineData("de_ch", "de-CH")]
    [InlineData("fr-ch", "fr-CH")]
    [InlineData("zh-hant", "zh-Hant")]
    [InlineData("es-419", "es-419")]
    public void Regional_target_is_kept_and_normalized(string culture, string expected) =>
        Assert.Equal(expected, LanguageCodes.Target(culture));

    [Fact]
    public void Override_wins()
    {
        Assert.Equal("de-CH", LanguageCodes.Target("de", "de_ch"));
        Assert.Equal("de-DE", LanguageCodes.Target("de", " "));
        Assert.Equal("pt", LanguageCodes.Source("en-US", "pt-BR"));
    }

    [Fact]
    public void Unknown_bare_code_is_sent_unchanged() =>
        Assert.Equal("xx", LanguageCodes.Target("xx"));
}
