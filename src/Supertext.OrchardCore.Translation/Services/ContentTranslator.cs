using System.Text.Json.Dynamic;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using Supertext.OrchardCore.Translation.Api;

namespace Supertext.OrchardCore.Translation.Services;

/// <param name="Segments">translatable texts found in the source</param>
/// <param name="Untranslated">texts that came back missing or damaged and kept the source text</param>
public sealed record TranslationResult(int Segments, int Untranslated);

/// <summary>The translated content of a source item, ready to be applied to a target item.</summary>
public sealed record PreparedTranslation(JsonObject Data, TranslationResult Result);

/// <summary>
/// Writes a translation of a source item's content into a target item of the same
/// localization set: the source content is copied over the target (except the parts that
/// belong to the target: culture, URL, alias, list membership), its texts are translated
/// with Supertext in one document per language, and the result is applied to the target.
/// Saving is up to the caller.
/// </summary>
public sealed class ContentTranslator(
    IContentDefinitionManager contentDefinitionManager,
    SupertextClient client,
    SupertextConfiguration configuration,
    ILogger<ContentTranslator> logger)
{
    /// <summary>Parts that identify the target item rather than describe its content.</summary>
    private static readonly string[] TargetOwnedParts = ["LocalizationPart", "AutoroutePart", "AliasPart", "ContainedPart"];

    private readonly Dictionary<string, IReadOnlyList<PartSchema>> _schemas = new(StringComparer.Ordinal);

    /// <summary>Translates the source and applies the result to the target (not saved).</summary>
    public async Task<TranslationResult> TranslateAsync(ContentItem source, ContentItem target, string sourceCulture, string targetCulture, CancellationToken ct = default)
    {
        var prepared = await PrepareAsync(source, sourceCulture, targetCulture, ct);
        Apply(prepared, target);
        return prepared.Result;
    }

    /// <summary>Translates the source's content without touching any item yet.</summary>
    public async Task<PreparedTranslation> PrepareAsync(ContentItem source, string sourceCulture, string targetCulture, CancellationToken ct = default)
    {
        var settings = await configuration.GetSettingsAsync();
        var connection = await configuration.GetConnectionAsync();
        if (connection.ApiKey == string.Empty)
        {
            throw new SupertextException("No Supertext API key configured. An administrator can add it under Settings → Supertext.");
        }

        var data = (JsonObject)Data(source).DeepClone();
        foreach (var owned in TargetOwnedParts)
        {
            data.Remove(owned);
        }

        var walker = new ContentWalker(ResolveTypeAsync, SupertextConfiguration.ExcludedFields(settings));
        var segments = await walker.CollectAsync(data, source.ContentType);
        var untranslated = 0;
        var targetCode = SupertextConfiguration.TargetLanguageCode(settings, targetCulture);

        foreach (var chunk in Chunk(segments))
        {
            var html = HtmlDocument.Build(chunk.Select(s => s.Segment).ToList());
            var translated = await client.TranslateDocumentAsync(connection, html, targetCode, sourceCulture, settings.Politeness, ct);
            var parsed = HtmlDocument.Parse(translated, chunk.Select(s => s.Segment).ToList());
            for (var i = 0; i < chunk.Count; i++)
            {
                if (parsed.TryGetValue(i, out var text) && !string.IsNullOrWhiteSpace(text))
                {
                    chunk[i].Apply(text);
                }
                else
                {
                    untranslated++;
                    logger.LogWarning("Supertext returned no usable translation for {Path} ({Culture}); the source text was kept.", chunk[i].Path, targetCulture);
                }
            }
        }

        return new PreparedTranslation(data, new TranslationResult(segments.Count, untranslated));
    }

    /// <summary>Copies translated content over the target, keeping the target's own parts.</summary>
    public static void Apply(PreparedTranslation prepared, ContentItem target)
    {
        // Merge through Apply(): it also clears the target's cache of typed parts, so
        // handlers that run on save (TitlePart sets DisplayText) see the new values.
        var replacement = new ContentItem();
        var replacementData = Data(replacement);
        foreach (var (key, value) in prepared.Data)
        {
            replacementData[key] = value?.DeepClone();
        }
        target.Apply(replacement);

        if (prepared.Data["TitlePart"]?["Title"] is JsonValue title && title.TryGetValue<string>(out var displayText))
        {
            target.DisplayText = displayText;
        }
    }

    private static JsonObject Data(ContentItem item) => (JsonDynamicObject)item.Content;

    private static List<List<PendingSegment>> Chunk(List<PendingSegment> segments)
    {
        var chunks = new List<List<PendingSegment>>();
        var current = new List<PendingSegment>();
        var size = 0;
        foreach (var segment in segments)
        {
            var length = HtmlDocument.MeasureSegment(segment.Segment);
            if (current.Count > 0 && size + length > SupertextClient.MaxDocumentCharacters)
            {
                chunks.Add(current);
                current = [];
                size = 0;
            }
            current.Add(segment);
            size += length;
        }
        if (current.Count > 0)
        {
            chunks.Add(current);
        }
        return chunks;
    }

    private async Task<IReadOnlyList<PartSchema>> ResolveTypeAsync(string contentType)
    {
        if (_schemas.TryGetValue(contentType, out var cached))
        {
            return cached;
        }
        var definition = await contentDefinitionManager.GetTypeDefinitionAsync(contentType);
        IReadOnlyList<PartSchema> parts = definition is null
            ? []
            : definition.Parts.Select(p => new PartSchema(
                p.Name,
                p.PartDefinition.Name,
                p.PartDefinition.Fields.Select(f => new FieldSchema(f.Name, f.FieldDefinition.Name, f.GetSettings<ContentPartFieldSettings>().Editor)).ToList()))
              .ToList();
        _schemas[contentType] = parts;
        return parts;
    }
}
