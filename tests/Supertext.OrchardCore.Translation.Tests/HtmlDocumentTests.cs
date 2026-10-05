using Supertext.OrchardCore.Translation.Api;
using Xunit;

namespace Supertext.OrchardCore.Translation.Tests;

public class HtmlDocumentTests
{
    [Fact]
    public void Round_trip_keeps_text_and_html()
    {
        var segments = new List<HtmlDocument.Segment>
        {
            new("Tom & Jerry <3\nsecond line", false),
            new("<p>Hello <strong>world</strong> and <a href=\"https://example.com\">a link</a>.</p>", true),
        };
        var parsed = HtmlDocument.Parse(HtmlDocument.Build(segments), segments);
        Assert.Equal("Tom & Jerry <3\nsecond line", parsed[0]);
        Assert.Equal(segments[1].Text, parsed[1]);
    }

    [Fact]
    public void Whole_rich_text_field_is_one_segment()
    {
        var segments = new List<HtmlDocument.Segment> { new("<p>One <b>bold</b> word.</p><ul><li>Item</li></ul>", true) };
        var html = HtmlDocument.Build(segments);
        Assert.Equal(1, html.Split("data-st-id").Length - 1);
    }

    [Fact]
    public void Liquid_is_masked_and_restored()
    {
        var segments = new List<HtmlDocument.Segment> { new("<p>Hello {{ User.Identity.Name }}, {% if x %}yes{% endif %}</p>", true) };
        var html = HtmlDocument.Build(segments);
        Assert.DoesNotContain("{{", html);
        Assert.DoesNotContain("{%", html);
        // Simulate a translation that keeps the placeholders.
        var translated = html.Replace("Hello", "Hallo");
        Assert.Equal("<p>Hallo {{ User.Identity.Name }}, {% if x %}yes{% endif %}</p>", HtmlDocument.Parse(translated, segments)[0]);
    }

    [Fact]
    public void Lost_liquid_placeholder_keeps_the_source()
    {
        var segments = new List<HtmlDocument.Segment> { new("<p>Hello {{ Name }}</p>", true) };
        var translated = "<div data-st-id=\"0\"><p>Hallo</p></div>";
        Assert.False(HtmlDocument.Parse(translated, segments).ContainsKey(0));
    }

    [Fact]
    public void Formatting_newlines_from_supertext_collapse_in_plain_text()
    {
        var segments = new List<HtmlDocument.Segment> { new("a b", false) };
        Assert.Equal("a b", HtmlDocument.Parse("<div data-st-id=\"0\">\n  a\n  b\n</div>", segments)[0]);
    }

    [Theory]
    [InlineData("Supertext-Auth-Key abc123", "abc123")]
    [InlineData("  supertext-auth-key   abc123 ", "abc123")]
    [InlineData("abc123", "abc123")]
    public void Api_key_prefix_is_stripped(string input, string expected)
        => Assert.Equal(expected, SupertextClient.NormalizeApiKey(input));

    [Theory]
    [InlineData("de-CH", "de")]
    [InlineData("en", "en")]
    [InlineData("zh_Hant_TW", "zh")]
    [InlineData("", "")]
    public void Source_language_is_the_primary_subtag(string culture, string expected)
        => Assert.Equal(expected, SupertextClient.SourceLanguageCode(culture));
}
