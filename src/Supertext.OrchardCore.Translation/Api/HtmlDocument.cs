using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Supertext.OrchardCore.Translation.Api;

/// <summary>
/// Packs many strings into one HTML document and splits the translated document back
/// apart. Supertext keeps markup and attributes and translates text nodes, so every
/// segment travels inside &lt;div data-st-id="N"&gt;…&lt;/div&gt;.
///
/// Plain-text segments are escaped (line breaks sent as &lt;br&gt;) so they survive the round
/// trip unchanged. HTML segments (rich text) are sent as they are: one segment per field,
/// formatting and links as inline tags inside it, never one segment per formatted run.
/// Liquid tags in HTML (<c>{{ … }}</c>, <c>{% … %}</c>) are replaced by placeholder elements
/// so they are not translated, and restored afterwards.
/// </summary>
public static partial class HtmlDocument
{
    public sealed record Segment(string Text, bool IsHtml);

    private const string KeepAttribute = "data-st-keep";

    public static string Build(IReadOnlyList<Segment> segments)
    {
        var sb = new StringBuilder("<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\"></head><body>\n");
        for (var i = 0; i < segments.Count; i++)
        {
            var s = segments[i];
            var content = s.IsHtml ? MaskLiquid(s.Text, out _) : EncodeText(s.Text);
            sb.Append("<div data-st-id=\"").Append(i).Append("\">").Append(content).Append("</div>\n");
        }
        return sb.Append("</body></html>").ToString();
    }

    /// <summary>Approximate size of a segment inside the document (for chunking).</summary>
    public static int MeasureSegment(Segment s)
        => (s.IsHtml ? s.Text.Length : EncodeText(s.Text).Length) + 32;

    /// <returns>segment index => translated text (segments whose markup came back damaged are left out)</returns>
    public static Dictionary<int, string> Parse(string html, IReadOnlyList<Segment> segments)
    {
        var doc = new HtmlAgilityPack.HtmlDocument { OptionOutputOriginalCase = true };
        doc.LoadHtml(html);
        var result = new Dictionary<int, string>();
        var nodes = doc.DocumentNode.SelectNodes("//div[@data-st-id]");
        if (nodes is null)
        {
            return result;
        }
        foreach (var node in nodes)
        {
            if (!int.TryParse(node.GetAttributeValue("data-st-id", ""), out var id) || id < 0 || id >= segments.Count)
            {
                continue;
            }
            if (segments[id].IsHtml)
            {
                MaskLiquid(segments[id].Text, out var liquid);
                var restored = RestoreLiquid(node.InnerHtml.Trim(), liquid);
                if (restored is not null)
                {
                    result[id] = restored;
                }
                continue;
            }
            // Plain text: <br> are the real line breaks; other whitespace (incl. formatting
            // newlines Supertext may add) collapses to one space.
            foreach (var br in node.SelectNodes(".//br")?.ToList() ?? [])
            {
                br.ParentNode.ReplaceChild(doc.CreateTextNode("\u001E"), br);
            }
            var text = WebUtility.HtmlDecode(node.InnerText);
            text = Whitespace().Replace(text, " ");
            text = LineBreak().Replace(text, "\n");
            result[id] = text.Trim(' ');
        }
        return result;
    }

    private static string EncodeText(string text)
        => WebUtility.HtmlEncode(text.Replace("\r\n", "\n").Replace('\r', '\n')).Replace("\n", "<br>");

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

    /// <returns>null when a placeholder went missing (the field then keeps its source text)</returns>
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

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(" ?\u001E ?")]
    private static partial Regex LineBreak();
}
