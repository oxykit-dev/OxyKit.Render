using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using OxyKit.Render.Contracts;

namespace OxyKit.Render.Client;

public sealed class RenderClientOptions
{
    /// <summary>Base URL including trailing slash preference — e.g. http://localhost:5066/ or https://api.example.com/v1/</summary>
    public required Uri BaseAddress { get; init; }

    /// <summary>Local uses X-OxyKit-Render-Key; Cloud uses Bearer. Set one.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Header name for Local API key auth. Default X-OxyKit-Render-Key.</summary>
    public string ApiKeyHeader { get; init; } = "X-OxyKit-Render-Key";

    /// <summary>When true, send Authorization: Bearer {ApiKey} instead of the Local header.</summary>
    public bool UseBearerAuth { get; init; }
}

public sealed class RenderApiException(int statusCode, string? error, string? responseBody = null)
    : Exception(error ?? $"OxyKit.Render API returned {statusCode}.")
{
    public int StatusCode { get; } = statusCode;
    public string? Error { get; } = error;
    public string? ResponseBody { get; } = responseBody;
}

public sealed class RenderFileResult
{
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
    public string? FileName { get; init; }

    /// <summary>OxyKit.Render.Core version from <see cref="RenderEngineHeaders.Version"/>.</summary>
    public string? EngineVersion { get; init; }

    /// <summary>Dependency fingerprint from <see cref="RenderEngineHeaders.Fingerprint"/>.</summary>
    public string? EngineFingerprint { get; init; }

    public string ComputeSha256()
    {
        var hash = SHA256.HashData(Content);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>Thin HttpClient SDK for OxyKit.Render Local (and later Cloud) APIs.</summary>
public sealed class RenderClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public RenderClient(RenderClientOptions options)
        : this(CreateHttpClient(options), ownsHttp: true) { }

    public RenderClient(HttpClient httpClient)
        : this(httpClient, ownsHttp: false) { }

    private RenderClient(HttpClient httpClient, bool ownsHttp)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttp = ownsHttp;
    }

    private static HttpClient CreateHttpClient(RenderClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var http = new HttpClient { BaseAddress = options.BaseAddress };
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            if (options.UseBearerAuth)
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    options.ApiKey
                );
            else
                http.DefaultRequestHeaders.TryAddWithoutValidation(
                    options.ApiKeyHeader,
                    options.ApiKey
                );
        }

        return http;
    }

    public async Task<FormatsResponse> GetFormatsAsync(
        CancellationToken cancellationToken = default
    )
    {
        using var response = await _http
            .GetAsync("formats", cancellationToken)
            .ConfigureAwait(false);
        return await ReadJsonAsync<FormatsResponse>(response, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TemplatesResponse> GetTemplatesAsync(
        CancellationToken cancellationToken = default
    )
    {
        using var response = await _http
            .GetAsync("templates", cancellationToken)
            .ConfigureAwait(false);
        return await ReadJsonAsync<TemplatesResponse>(response, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<HealthResponse>("health", cancellationToken);

    public Task<RenderFileResult> RenderHtmlPdfAsync(
        HtmlPdfRenderRequest request,
        CancellationToken cancellationToken = default
    ) => PostRenderAsync("render/html/pdf", request, cancellationToken);

    public Task<RenderFileResult> RenderCsvPdfAsync(
        CsvPdfRenderRequest request,
        CancellationToken cancellationToken = default
    ) => PostRenderAsync("render/csv/pdf", request, cancellationToken);

    public Task<RenderFileResult> RenderCsvXlsxAsync(
        CsvXlsxRenderRequest request,
        CancellationToken cancellationToken = default
    ) => PostRenderAsync("render/csv/xlsx", request, cancellationToken);

    public Task<RenderFileResult> RenderTablePdfAsync(
        TableRenderRequest request,
        CancellationToken cancellationToken = default
    ) => PostRenderAsync("render/table/pdf", request, cancellationToken);

    public Task<RenderFileResult> RenderTableHtmlAsync(
        TableRenderRequest request,
        CancellationToken cancellationToken = default
    ) => PostRenderAsync("render/table/html", request, cancellationToken);

    public Task<RenderFileResult> RenderTableXlsxAsync(
        TableRenderRequest request,
        CancellationToken cancellationToken = default
    ) => PostRenderAsync("render/table/xlsx", request, cancellationToken);

    public Task<RenderFileResult> RenderTableCsvAsync(
        TableRenderRequest request,
        CancellationToken cancellationToken = default
    ) => PostRenderAsync("render/table/csv", request, cancellationToken);

    public Task<RenderFileResult> RenderTemplateHtmlAsync(
        TemplateHtmlRenderRequest request,
        CancellationToken cancellationToken = default
    ) => PostRenderAsync("render/template/html", request, cancellationToken);

    public Task<RenderFileResult> RenderTemplatePdfAsync(
        TemplatePdfRenderRequest request,
        CancellationToken cancellationToken = default
    ) => PostRenderAsync("render/template/pdf", request, cancellationToken);

    /// <summary>
    /// Generic render: <c>POST /render/{input}/{output}</c> with an arbitrary JSON body
    /// (e.g. from a persisted job). Prefer typed helpers when the contract is known at compile time.
    /// </summary>
    public Task<RenderFileResult> RenderAsync(
        string input,
        string output,
        JsonElement body,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(output);
        var path =
            $"render/{Uri.EscapeDataString(input.Trim())}/{Uri.EscapeDataString(output.Trim())}";
        return PostRenderAsync(path, body, cancellationToken);
    }

    /// <summary>Cloud: fetch document provenance metadata.</summary>
    public Task<DocumentRecord> GetDocumentAsync(
        string documentId,
        CancellationToken cancellationToken = default
    ) =>
        GetJsonAsync<DocumentRecord>(
            $"documents/{Uri.EscapeDataString(documentId)}",
            cancellationToken
        );

    /// <summary>Cloud: verify bytes against the recorded SHA-256.</summary>
    public async Task<VerifyDocumentResponse> VerifyDocumentAsync(
        string documentId,
        byte[] content,
        CancellationToken cancellationToken = default
    )
    {
        var sha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        using var response = await _http
            .PostAsJsonAsync(
                $"documents/{Uri.EscapeDataString(documentId)}/verify",
                new { sha256 = sha },
                JsonOptions,
                cancellationToken
            )
            .ConfigureAwait(false);
        return await ReadJsonAsync<VerifyDocumentResponse>(response, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Cloud: register an external file (upload). Retention of bytes is opt-in.</summary>
    public async Task<DocumentRecord> RegisterDocumentAsync(
        Stream content,
        string contentType,
        RetentionOptions? retention = null,
        CancellationToken cancellationToken = default
    )
    {
        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(streamContent, "file", "document");
        if (retention is not null)
        {
            form.Add(
                new StringContent(retention.RetainArtifact ? "true" : "false"),
                "retainArtifact"
            );
            form.Add(
                new StringContent(retention.RetainTemplate ? "true" : "false"),
                "retainTemplate"
            );
            form.Add(
                new StringContent(retention.RetainPayload ? "true" : "false"),
                "retainPayload"
            );
        }

        using var response = await _http
            .PostAsync("documents/register", form, cancellationToken)
            .ConfigureAwait(false);
        return await ReadJsonAsync<DocumentRecord>(response, cancellationToken)
            .ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }

    private async Task<T> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(path, cancellationToken).ConfigureAwait(false);
        return await ReadJsonAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RenderFileResult> PostRenderAsync<T>(
        string path,
        T body,
        CancellationToken cancellationToken
    )
    {
        using var response = await _http
            .PostAsJsonAsync(path, body, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, cancellationToken).ConfigureAwait(false);

        var bytes = await response
            .Content.ReadAsByteArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var contentType =
            response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        response.Headers.TryGetValues(RenderEngineHeaders.Version, out var versions);
        response.Headers.TryGetValues(RenderEngineHeaders.Fingerprint, out var fingerprints);
        return new RenderFileResult
        {
            Content = bytes,
            ContentType = contentType,
            FileName = fileName,
            EngineVersion = versions?.FirstOrDefault(),
            EngineFingerprint = fingerprints?.FirstOrDefault(),
        };
    }

    private static async Task<T> ReadJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, cancellationToken).ConfigureAwait(false);

        var value = await response
            .Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return value
            ?? throw new RenderApiException((int)response.StatusCode, "Empty response body.", null);
    }

    private static async Task ThrowApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        var body = await response
            .Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
        string? error = null;
        try
        {
            var payload = JsonSerializer.Deserialize<ErrorResponse>(body, JsonOptions);
            error = payload?.Error;
        }
        catch (JsonException)
        {
            // keep raw body
        }

        throw new RenderApiException((int)response.StatusCode, error, body);
    }
}
