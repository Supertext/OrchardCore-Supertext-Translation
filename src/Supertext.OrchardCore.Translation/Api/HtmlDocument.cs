using System.Text.RegularExpressions;
using SdkHtmlDocument = Supertext.OrchardCore.Translation.Sdk.V1.HtmlDocument;
using SdkSegment = Supertext.OrchardCore.Translation.Sdk.V1.Segment;

namespace Supertext.OrchardCore.Translation.Api;

/// <summary>
/// The module's view of the document sent to Supertext. Packing and reading it back is the
/// Supertext SDK's <c>HtmlDocument</c> (one &lt;div data-st-id="N"&gt; per segment, plain text
/// escaped with &lt;br&gt; for line breaks, HTML as is); this class adds what is specific to
/// Orchard Core: Liquid tags in HTML (<c>{{ … }}</c>, <c>{% … %}</c>) are replaced by
/// placeholder elements so they are not translated, and restored afterwards.
///
/// HTML segments (rich text) go as one segment per field, formatting and links as inline
/// tags inside it, never one segment per formatted run.
/// </summary>
public static partial class HtmlDocument
{
    public sealed record Segment(string Text, bool IsHtml);

    private const string KeepAttribute = "data-st-keep";

    /// <summary>The SDK segment to send (Liquid masked) and the Liquid tags to put back.</summary>
    public static (SdkSegment Segment, List<string> Liquid) ToSdk(Segment segment)
    {
        if (!segment.IsHtml)
        {
            return (new SdkSegment(segment.Text, false), []);
        }
        return (new SdkSegment(MaskLiquid(segment.Text, out var liquid), true), liquid);
    }

    /// <returns>the translation with Liquid restored, or null when a placeholder went missing (the field then keeps its source text)</returns>
    public static string FromSdk(string translated, List<string> liquid)
        => translated is null ? null : RestoreLiquid(translated, liquid);

    public static string Build(IReadOnlyList<Segment> segments)
        => SdkHtmlDocument.Build(segments.Select(s => ToSdk(s).Segment).ToList());

    /// <returns>segment index => translated text (segments that are missing or whose Liquid came back damaged are left out)</returns>
    public static Dictionary<int, string> Parse(string html, IReadOnlyList<Segment> segments)
    {
        var prepared = segments.Select(ToSdk).ToList();
        var parsed = SdkHtmlDocument.Parse(html, prepared.Select(p => p.Segment).ToList());
        var result = new Dictionary<int, string>();
        for (var i = 0; i < parsed.Count; i++)
        {
            var text = FromSdk(parsed[i], prepared[i].Liquid);
            if (text is not null)
            {
                result[i] = text;
            }
        }
        return result;
    }

    private static string MaskLiquid(string html, out List<string> liquid)
    {
        var found = new List<string>();
        var masked = Liquid().Replace(html, m =>
        {
            found.Add(m.Value);
            return $"<span {KeepAttribute}=\"{found.Count - 1}\"></span>";
        });
        liquid = found;
        return masked;
    }

    private static string RestoreLiquid(string html, List<string> liquid)
    {
        if (liquid.Count == 0)
        {
            return html;
        }
        var restored = new HashSet<int>();
        var result = KeepPlaceholder().Replace(html, m =>
        {
            var index = int.Parse(m.Groups[1].Value);
            if (index >= liquid.Count)
            {
                return m.Value;
            }
            restored.Add(index);
            return liquid[index];
        });
        return restored.Count == liquid.Count ? result : null;
    }

    [GeneratedRegex(@"\{\{[\s\S]*?\}\}|\{%[\s\S]*?%\}")]
    private static partial Regex Liquid();

    [GeneratedRegex(@"<span\s+data-st-keep=""(\d+)""\s*>\s*</span>")]
    private static partial Regex KeepPlaceholder();
}
