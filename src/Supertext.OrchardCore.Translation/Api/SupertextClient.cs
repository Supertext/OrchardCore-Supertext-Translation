using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Supertext.OrchardCore.Translation.Api;

public sealed class SupertextException(string message, Exception inner = null) : Exception(message, inner);

/// <summary>Where and how to reach Supertext (resolved from site settings and environment).</summary>
public sealed record SupertextConnection(string Endpoint, string ApiKey, int PollIntervalSeconds = 2, int PollTimeoutSeconds = 300);

/// <summary>
/// Supertext AI file translation (https://api.supertext.com/v1/), the same protocol as the
/// WordPress, TYPO3, Neos and Umbraco integrations: submit one HTML document, poll its
/// status, download the translation, delete the file.
/// </summary>
public sealed partial class SupertextClient(HttpClient http)
{
    public const string DefaultEndpoint = "https://api.supertext.com/v1/";

    /// <summary>Retries after HTTP 429 (requests per second are limited per key).</summary>
    private const int RateLimitRetries = 4;

    /// <summary>Documents above this size are split into several requests.</summary>
    public const int MaxDocumentCharacters = 900_000;

    /// <param name="targetLanguage">BCP-47 code, e.g. "de-CH"</param>
    /// <param name="sourceLanguage">culture of the source ("en-US"); sent as its primary subtag, empty for auto-detection</param>
    /// <param name="politeness">"default", "more" or "less"</param>
    public async Task<string> TranslateDocumentAsync(SupertextConnection connection, string html, string targetLanguage, string sourceLanguage, string politeness, CancellationToken ct = default)
    {
        var fileId = await SubmitAsync(connection, html, targetLanguage, sourceLanguage, politeness, ct);
        try
        {
            await WaitUntilDoneAsync(connection, fileId, ct);
            return await DownloadAsync(connection, fileId, ct);
        }
        finally
        {
            await DeleteQuietlyAsync(connection, fileId);
        }
    }

    /// <summary>Cost-free check of the API key.</summary>
    public async Task ValidateApiKeyAsync(SupertextConnection connection, CancellationToken ct = default)
    {
        using var _ = await SendAsync(connection, () => new HttpRequestMessage(HttpMethod.Get, "features"), ct);
    }

    /// <summary>Removes a pasted "Supertext-Auth-Key " prefix (Supertext shows the key with it).</summary>
    public static string NormalizeApiKey(string apiKey)
        => AuthPrefix().Replace(apiKey ?? string.Empty, string.Empty).Trim();

    /// <summary>Supertext expects the source as a primary subtag ("de", not "de-CH").</summary>
    public static string SourceLanguageCode(string culture)
        => string.IsNullOrWhiteSpace(culture) ? string.Empty : culture.Split('-', '_')[0].ToLowerInvariant();

    private async Task<string> SubmitAsync(SupertextConnection connection, string html, string targetLanguage, string sourceLanguage, string politeness, CancellationToken ct)
    {
        HttpRequestMessage Build()
        {
            var form = new MultipartFormDataContent();
            // Quoted part names (name="target_lang"), as browsers send them: .NET leaves them
            // unquoted by default, which stricter multipart parsers reject.
            void AddField(string name, string value)
            {
                var part = new StringContent(value);
                part.Headers.ContentType = null;
                part.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = $"\"{name}\"" };
                form.Add(part);
            }
            AddField("target_lang", targetLanguage);
            var source = SourceLanguageCode(sourceLanguage);
            if (source != string.Empty)
            {
                AddField("source_lang", source);
            }
            if (politeness is "more" or "less")
            {
                AddField("politeness", politeness);
            }
            // The part's Content-Type must be exactly "text/html" (no charset), otherwise 415.
            var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(html));
            file.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "\"file\"", FileName = "\"content.html\"" };
            form.Add(file);
            return new HttpRequestMessage(HttpMethod.Post, "translate/ai/file") { Content = form };
        }

        using var response = await SendAsync(connection, Build, ct);
        var json = await ReadJsonAsync(response, ct);
        var fileId = json.ValueKind == JsonValueKind.Object && json.TryGetProperty("file_id", out var id) ? id.ToString() : string.Empty;
        if (fileId == string.Empty)
        {
            throw new SupertextException("Supertext did not return a file id.");
        }
        return fileId;
    }

    private async Task WaitUntilDoneAsync(SupertextConnection connection, string fileId, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(10, connection.PollTimeoutSeconds));
        var interval = TimeSpan.FromSeconds(Math.Max(1, connection.PollIntervalSeconds));
        do
        {
            using var response = await SendAsync(connection, () => new HttpRequestMessage(HttpMethod.Get, $"translate/ai/file/{Uri.EscapeDataString(fileId)}/status"), ct);
            var json = await ReadJsonAsync(response, ct);
            var status = json.ValueKind == JsonValueKind.Object && json.TryGetProperty("status", out var s) ? s.GetString() : null;
            switch (status)
            {
                case "done": return;
                case "error": throw new SupertextException("Supertext failed to translate the document.");
                case "limit_exceeded": throw new SupertextException("Your Supertext translation limit is exceeded.");
                case "deleted": throw new SupertextException("The Supertext file was deleted before it could be downloaded.");
            }
            await Task.Delay(interval, ct);
        }
        while (DateTime.UtcNow < deadline);
        throw new SupertextException("Timed out waiting for the Supertext translation.");
    }

    private async Task<string> DownloadAsync(SupertextConnection connection, string fileId, CancellationToken ct)
    {
        using var response = await SendAsync(connection, () => new HttpRequestMessage(HttpMethod.Get, $"translate/ai/file/{Uri.EscapeDataString(fileId)}/translation"), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new SupertextException("The translated document was empty.");
        }
        return body;
    }

    private async Task DeleteQuietlyAsync(SupertextConnection connection, string fileId)
    {
        try
        {
            using var _ = await SendAsync(connection, () => new HttpRequestMessage(HttpMethod.Delete, $"translate/ai/file/{Uri.EscapeDataString(fileId)}"), CancellationToken.None);
        }
        catch
        {
            // Files expire after 24 h anyway.
        }
    }

    private async Task<HttpResponseMessage> SendAsync(SupertextConnection connection, Func<HttpRequestMessage> build, CancellationToken ct)
    {
        var apiKey = NormalizeApiKey(connection.ApiKey);
        if (apiKey == string.Empty)
        {
            throw new SupertextException("No Supertext API key configured (Settings → Supertext, or SUPERTEXT_API_KEY).");
        }
        var endpoint = string.IsNullOrWhiteSpace(connection.Endpoint) ? DefaultEndpoint : connection.Endpoint.Trim();
        var baseUri = new Uri(endpoint.EndsWith('/') ? endpoint : endpoint + "/");

        HttpResponseMessage response;
        for (var attempt = 0; ; attempt++)
        {
            var request = build();
            request.RequestUri = new Uri(baseUri, request.RequestUri!.ToString());
            // Exactly one prefix, header name "Authorization" ("Authentication" gets 403).
            request.Headers.TryAddWithoutValidation("Authorization", "Supertext-Auth-Key " + apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            try
            {
                response = await http.SendAsync(request, ct);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                throw new SupertextException("Could not reach Supertext: " + e.Message, e);
            }
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt >= RateLimitRetries)
            {
                break;
            }
            // Rate limited: wait (Retry-After, else 1, 2, 4, 8 s with jitter) and retry.
            var delay = response.Headers.RetryAfter?.Delta
                ?? TimeSpan.FromMilliseconds(1000 * Math.Pow(2, attempt) + Random.Shared.Next(0, 250));
            response.Dispose();
            await Task.Delay(delay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : delay, ct);
        }

        var code = (int)response.StatusCode;
        if (code is >= 200 and < 300)
        {
            return response;
        }
        var message = code switch
        {
            401 or 403 => "Authentication failed. Please check the Supertext API key.",
            404 => "The requested Supertext resource was not found.",
            413 => "The content is too large for Supertext to translate in one go.",
            429 => "Too many requests to Supertext. Please try again shortly.",
            >= 500 => "The Supertext service is currently unavailable.",
            _ => $"Supertext answered with HTTP {code}.",
        };
        var detail = Tags().Replace(await response.Content.ReadAsStringAsync(ct), string.Empty).Trim();
        response.Dispose();
        if (detail != string.Empty)
        {
            message += " (" + (detail.Length > 200 ? detail[..200] : detail) + ")";
        }
        throw new SupertextException(message);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return default;
        }
    }

    [GeneratedRegex(@"^\s*Supertext-Auth-Key\s+", RegexOptions.IgnoreCase)]
    private static partial Regex AuthPrefix();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();
}
