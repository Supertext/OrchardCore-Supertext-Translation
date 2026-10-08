using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Supertext.OrchardCore.Translation.Tests;

/// <summary>
/// The module's UI strings in Localization/de.po, fr.po and it.po: every text the code asks
/// for (T["…"] in views, S["…"]/H["…"] in classes, SupertextException messages) is translated
/// in each language with the right msgctxt, no stale entries, and placeholders and links kept.
/// </summary>
public sealed partial class LocalizationTests
{
    private const string Module = "Supertext.OrchardCore.Translation";
    private const string MessagesContext = Module + ".Services.SupertextMessages";

    public static TheoryData<string> Languages => new() { "de", "fr", "it" };

    private static readonly string ModuleDir = FindModuleDir();

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_string_is_translated(string language)
    {
        var po = ReadPo(language);
        var missing = SourceStrings()
            .Where(s => !po.TryGetValue(s, out var translation) || translation.Length == 0)
            .Select(s => $"{s.Context} | {s.Id}")
            .ToList();
        Assert.True(missing.Count == 0, $"{language}.po misses:\n" + string.Join("\n", missing));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void No_stale_entries(string language)
    {
        var used = SourceStrings().ToHashSet();
        var stale = ReadPo(language).Keys.Where(k => !used.Contains(k)).Select(k => $"{k.Context} | {k.Id}").ToList();
        Assert.True(stale.Count == 0, $"{language}.po has entries no code uses:\n" + string.Join("\n", stale));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Placeholders_and_links_are_kept(string language)
    {
        var wrong = new List<string>();
        foreach (var (key, translation) in ReadPo(language))
        {
            if (!Signature(key.Id).SequenceEqual(Signature(translation)))
            {
                wrong.Add($"{key.Id}\n  → {translation}");
            }
        }
        Assert.True(wrong.Count == 0, $"{language}.po changes placeholders, tags or URLs:\n" + string.Join("\n", wrong));
    }

    /// <summary>Placeholders, HTML tags and URLs, sorted.</summary>
    private static IEnumerable<string> Signature(string text)
        => SignatureRegex().Matches(text).Select(m => m.Value).Order(StringComparer.Ordinal);

    private static IEnumerable<(string Context, string Id)> SourceStrings()
    {
        var result = new HashSet<(string, string)>();
        foreach (var file in Directory.EnumerateFiles(ModuleDir, "*.*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(ModuleDir, file).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal) || relative.StartsWith("bin/", StringComparison.Ordinal))
            {
                continue;
            }
            var text = File.ReadAllText(file);
            if (file.EndsWith(".cshtml", StringComparison.Ordinal))
            {
                var context = Module + "." + relative[..^".cshtml".Length].Replace('/', '.');
                foreach (Match m in ViewStringRegex().Matches(text))
                {
                    result.Add((context, Unescape(m.Groups[1].Value)));
                }
            }
            else if (file.EndsWith(".cs", StringComparison.Ordinal))
            {
                var ns = NamespaceRegex().Match(text).Groups[1].Value;
                var localizer = LocalizerRegex().Match(text);
                foreach (Match m in ClassStringRegex().Matches(text))
                {
                    Assert.True(localizer.Success, $"{relative} uses S[] or H[] without an IStringLocalizer<T>/IHtmlLocalizer<T>");
                    result.Add((ns + "." + localizer.Groups[1].Value, Unescape(m.Groups[1].Value)));
                }
                foreach (Match m in ExceptionRegex().Matches(text))
                {
                    result.Add((MessagesContext, Unescape(m.Groups[1].Value)));
                }
                if (relative == "Api/SupertextClient.cs")
                {
                    foreach (Match m in SwitchMessageRegex().Matches(text))
                    {
                        result.Add((MessagesContext, Unescape(m.Groups[1].Value)));
                    }
                }
            }
        }
        Assert.NotEmpty(result);
        return result;
    }

    private static Dictionary<(string Context, string Id), string> ReadPo(string language)
    {
        var entries = new Dictionary<(string, string), string>();
        string context = string.Empty, id = null;
        foreach (var line in File.ReadLines(Path.Combine(ModuleDir, "Localization", language + ".po")))
        {
            if (line.StartsWith("msgctxt ", StringComparison.Ordinal))
            {
                context = PoString(line["msgctxt ".Length..]);
            }
            else if (line.StartsWith("msgid ", StringComparison.Ordinal))
            {
                id = PoString(line["msgid ".Length..]);
            }
            else if (line.StartsWith("msgstr ", StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(id))
                {
                    Assert.True(entries.TryAdd((context, id), PoString(line["msgstr ".Length..])), $"{language}.po: duplicate {context} | {id}");
                }
                context = string.Empty;
                id = null;
            }
        }
        return entries;
    }

    private static string PoString(string quoted) => Unescape(quoted.Trim()[1..^1]);

    private static string Unescape(string literal)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < literal.Length; i++)
        {
            if (literal[i] == '\\' && i + 1 < literal.Length)
            {
                i++;
                sb.Append(literal[i] switch { 'n' => '\n', 't' => '\t', var c => c });
            }
            else
            {
                sb.Append(literal[i]);
            }
        }
        return sb.ToString();
    }

    private static string FindModuleDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", Module);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }
        throw new DirectoryNotFoundException("src/" + Module + " not found above " + AppContext.BaseDirectory);
    }

    private const string Literal = "\"((?:[^\"\\\\]|\\\\.)*)\"";

    [GeneratedRegex(@"\bT\[" + Literal)]
    private static partial Regex ViewStringRegex();

    [GeneratedRegex(@"\b[SH]\[" + Literal)]
    private static partial Regex ClassStringRegex();

    [GeneratedRegex(@"new SupertextException\(" + Literal)]
    private static partial Regex ExceptionRegex();

    [GeneratedRegex(@"=> " + Literal + ",")]
    private static partial Regex SwitchMessageRegex();

    [GeneratedRegex(@"^namespace ([\w.]+);", RegexOptions.Multiline)]
    private static partial Regex NamespaceRegex();

    [GeneratedRegex(@"I(?:String|Html)Localizer<(\w+)>")]
    private static partial Regex LocalizerRegex();

    [GeneratedRegex(@"\{\d+\}|</?\w+[^>]*>|https?://[^\s)<""]+")]
    private static partial Regex SignatureRegex();
}
