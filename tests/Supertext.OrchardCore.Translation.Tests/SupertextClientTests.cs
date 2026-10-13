using System.Net;
using System.Text;
using Supertext.OrchardCore.Translation.Api;
using Xunit;

namespace Supertext.OrchardCore.Translation.Tests;

/// <summary>The wrapper around the bundled Supertext SDK, against a fake HTTP handler.</summary>
public class SupertextClientTests
{
    private static readonly SupertextConnection Connection = new("https://api.test/v1/", "Supertext-Auth-Key test-key", PollIntervalSeconds: 1, PollTimeoutSeconds: 10);

    [Fact]
    public async Task Translates_segments_in_one_document_and_keeps_liquid()
    {
        var handler = new FakeApi(html => html
            .Replace(">Hello<", ">Hallo<")
            .Replace("<p>Hi ", "<p>Hoi ")
            .Replace("<div data-st-id=\"2\">Lost</div>", string.Empty));
        var client = new SupertextClient(new HttpClient(handler));

        var result = await client.TranslateSegmentsAsync(Connection,
        [
            new("Hello", false),
            new("<p>Hi {{ User.Name }}</p>", true),
            new("Lost", false),
            new("   ", false),
        ], "de", "en-GB", "more");

        Assert.Equal("Hallo", result[0]);
        Assert.Equal("<p>Hoi {{ User.Name }}</p>", result[1]);
        Assert.False(result.ContainsKey(2)); // missing in the answer: the source is kept
        Assert.False(result.ContainsKey(3)); // blank: never sent
        Assert.Equal(1, handler.Uploads);
        Assert.Contains("name=\"target_lang\"\r\n\r\nde-DE", handler.LastUpload);
        Assert.Contains("name=\"source_lang\"\r\n\r\nen", handler.LastUpload);
        Assert.DoesNotContain("{{", handler.LastUpload);
        Assert.Equal("Supertext-Auth-Key test-key", handler.Authorization);
    }

    [Fact]
    public async Task Unsupported_language_pair_names_the_codes()
    {
        var handler = new FakeApi(_ => "", rejectWith: (HttpStatusCode.NotFound, "{\"error_code\":\"INVALID_LANGUAGE_PAIR\",\"source_lang\":\"en\",\"target_lang\":\"xx-YY\"}"));
        var client = new SupertextClient(new HttpClient(handler));

        var error = await Assert.ThrowsAsync<SupertextException>(() => client.TranslateDocumentAsync(Connection, "<p>x</p>", "xx-YY", "en", "default"));

        Assert.Equal("Supertext does not support this language pair ({0} → {1}). Check the language codes under Settings → Supertext.", error.Template);
        Assert.Equal(new object[] { "en", "xx-YY" }, error.Args);
    }

    private sealed class FakeApi(Func<string, string> translate, (HttpStatusCode Status, string Body)? rejectWith = null) : HttpMessageHandler
    {
        private readonly Dictionary<string, string> files = [];

        public int Uploads { get; private set; }

        public string LastUpload { get; private set; } = string.Empty;

        public string Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Authorization = request.Headers.Authorization?.ToString() ?? string.Join(",", request.Headers.GetValues("Authorization"));
            var path = request.RequestUri!.AbsolutePath["/v1/".Length..];
            if (request.Method == HttpMethod.Post && path == "translate/ai/file")
            {
                if (rejectWith is { } reject)
                {
                    return Reply(reject.Status, reject.Body);
                }
                Uploads++;
                LastUpload = await request.Content!.ReadAsStringAsync(ct);
                var start = LastUpload.IndexOf("<!DOCTYPE", StringComparison.Ordinal);
                var end = LastUpload.LastIndexOf("</html>", StringComparison.Ordinal) + "</html>".Length;
                files["f1"] = translate(LastUpload[start..end]);
                return Reply(HttpStatusCode.OK, "{\"file_id\":\"f1\"}");
            }
            return path switch
            {
                "translate/ai/file/f1/status" => Reply(HttpStatusCode.OK, "{\"status\":\"done\"}"),
                "translate/ai/file/f1/translation" => Reply(HttpStatusCode.OK, files["f1"]),
                _ => Reply(HttpStatusCode.OK, "{}"),
            };
        }

        private static HttpResponseMessage Reply(HttpStatusCode status, string body)
            => new(status) { Content = new StringContent(body, Encoding.UTF8) };
    }
}
