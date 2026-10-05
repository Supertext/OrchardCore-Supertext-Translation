using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Supertext.OrchardCore.Translation.Api;

namespace Supertext.OrchardCore.Translation.Services;

/// <summary>A field of a content part, as defined in the content definition.</summary>
public sealed record FieldSchema(string Name, string FieldType, string Editor);

/// <summary>A part of a content type: <paramref name="Name"/> is the key in the item's JSON.</summary>
public sealed record PartSchema(string Name, string PartType, IReadOnlyList<FieldSchema> Fields);

/// <summary>A translatable string found in a content item, and how to write its translation back.</summary>
public sealed record PendingSegment(HtmlDocument.Segment Segment, string Path, Action<string> Apply);

/// <summary>
/// Finds the translatable text in a content item's JSON (the parts and fields Orchard Core
/// ships with, including widgets nested in Bag, Flow and Widgets List parts).
/// The walker works on plain JSON so it can be tested without a running site.
/// </summary>
public sealed partial class ContentWalker(Func<string, Task<IReadOnlyList<PartSchema>>> resolveType, IEnumerable<string> excluded)
{
    private const int MaxDepth = 10;

    /// <summary>Text field editors whose value is prose. Url, Email, Tel, Color, IconPicker, PredefinedList, CodeMirror and Monaco hold data or code.</summary>
    private static readonly HashSet<string> TranslatableTextEditors = new(StringComparer.OrdinalIgnoreCase) { "", "Standard", "TextArea", "Header" };

    private readonly HashSet<string> _excluded = new(excluded.Select(e => e.Trim()).Where(e => e != string.Empty), StringComparer.OrdinalIgnoreCase);

    public async Task<List<PendingSegment>> CollectAsync(JsonObject item, string contentType)
    {
        var result = new List<PendingSegment>();
        await CollectItemAsync(item, contentType, contentType, result, 0);
        return result;
    }

    private async Task CollectItemAsync(JsonObject item, string contentType, string path, List<PendingSegment> result, int depth)
    {
        if (depth > MaxDepth || string.IsNullOrEmpty(contentType))
        {
            return;
        }
        foreach (var part in await resolveType(contentType))
        {
            if (item[part.Name] is not JsonObject partData || IsExcluded(contentType, part.Name, null))
            {
                continue;
            }
            var partPath = $"{path}/{part.Name}";
            switch (part.PartType)
            {
                case "TitlePart":
                    AddString(partData, "Title", false, partPath, result);
                    break;
                case "HtmlBodyPart":
                    AddString(partData, "Html", true, partPath, result);
                    break;
                case "MarkdownBodyPart":
                    AddString(partData, "Markdown", false, partPath, result);
                    break;
                case "BagPart":
                    if (partData["ContentItems"] is JsonArray bagItems)
                    {
                        await CollectItemsAsync(bagItems, partPath, result, depth);
                    }
                    break;
                case "FlowPart":
                    if (partData["Widgets"] is JsonArray flowWidgets)
                    {
                        await CollectItemsAsync(flowWidgets, partPath, result, depth);
                    }
                    break;
                case "WidgetsListPart":
                    if (partData["Widgets"] is JsonObject zones)
                    {
                        foreach (var (zone, widgets) in zones)
                        {
                            if (widgets is JsonArray zoneItems)
                            {
                                await CollectItemsAsync(zoneItems, $"{partPath}/{zone}", result, depth);
                            }
                        }
                    }
                    break;
            }

            foreach (var field in part.Fields)
            {
                if (partData[field.Name] is not JsonObject fieldData || IsExcluded(contentType, part.Name, field.Name))
                {
                    continue;
                }
                var fieldPath = $"{partPath}/{field.Name}";
                var editor = field.Editor ?? string.Empty;
                switch (field.FieldType)
                {
                    case "TextField" when TranslatableTextEditors.Contains(editor):
                        AddString(fieldData, "Text", false, fieldPath, result);
                        break;
                    case "HtmlField" when !editor.Equals("Monaco", StringComparison.OrdinalIgnoreCase):
                        AddString(fieldData, "Html", true, fieldPath, result);
                        break;
                    case "MarkdownField":
                        AddString(fieldData, "Markdown", false, fieldPath, result);
                        break;
                    case "LinkField":
                        AddString(fieldData, "Text", false, fieldPath, result);
                        break;
                    case "MediaField":
                        if (fieldData["MediaTexts"] is JsonArray texts)
                        {
                            for (var i = 0; i < texts.Count; i++)
                            {
                                var index = i;
                                if (texts[i] is JsonValue v && v.TryGetValue<string>(out var alt) && ShouldTranslate(alt))
                                {
                                    result.Add(new PendingSegment(new HtmlDocument.Segment(alt, false), $"{fieldPath}/MediaTexts[{i}]", t => texts[index] = JsonValue.Create(t)));
                                }
                            }
                        }
                        break;
                }
            }
        }
    }

    private async Task CollectItemsAsync(JsonArray items, string path, List<PendingSegment> result, int depth)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is JsonObject nested && nested["ContentType"] is JsonValue type && type.TryGetValue<string>(out var nestedType))
            {
                await CollectItemAsync(nested, nestedType, $"{path}[{i}]:{nestedType}", result, depth + 1);
            }
        }
    }

    private bool IsExcluded(string contentType, string part, string field)
    {
        var name = field is null ? part : $"{part}.{field}";
        return _excluded.Contains(name) || _excluded.Contains($"{contentType}.{name}");
    }

    private static void AddString(JsonObject owner, string property, bool isHtml, string path, List<PendingSegment> result)
    {
        if (owner[property] is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
        {
            return;
        }
        var text = value.GetValue<string>();
        if (!ShouldTranslate(isHtml ? Tags().Replace(text, " ") : text))
        {
            return;
        }
        result.Add(new PendingSegment(new HtmlDocument.Segment(text, isHtml), $"{path}/{property}", t => owner[property] = JsonValue.Create(t)));
    }

    /// <summary>Skips empty values and values without words: URLs, e-mail addresses, paths, numbers.</summary>
    public static bool ShouldTranslate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        var trimmed = System.Net.WebUtility.HtmlDecode(text).Trim();
        if (NotProse().IsMatch(trimmed))
        {
            return false;
        }
        return trimmed.Any(char.IsLetter);
    }

    [GeneratedRegex(@"^(?:[a-z][a-z0-9+.-]*://\S+|mailto:\S+|[^\s@]+@[^\s@]+\.[^\s@]+|~?/[^\s]*|#[\w-]+|[\d\s.,:%+\-/()]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex NotProse();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();
}
