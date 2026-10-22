using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace IndieableSdk.DeveloperWorkflow;

internal sealed class WorkflowTransport : IDisposable
{
    internal const int MaxRequestBytes = 32768;
    private const int MaxResponseBytes = 65536;
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly Uri _baseUri;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _retryDelay;
    private readonly int _maxRetries;

    public WorkflowTransport(WorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/")
            throw new ArgumentException("BaseUrl must be an HTTPS origin, or a loopback HTTP origin.", nameof(options));
        if (options.RequestTimeout <= TimeSpan.Zero || options.RequestTimeout > TimeSpan.FromSeconds(60))
            throw new ArgumentException("RequestTimeout must be between zero and 60 seconds.", nameof(options));
        if (options.MaxRetries < 0 || options.MaxRetries > 3)
            throw new ArgumentException("MaxRetries must be between zero and three.", nameof(options));
        if (options.RetryBaseDelay < TimeSpan.Zero || options.RetryBaseDelay > TimeSpan.FromSeconds(2))
            throw new ArgumentException("RetryBaseDelay must be between zero and two seconds.", nameof(options));
        _baseUri = uri;
        _timeout = options.RequestTimeout;
        _maxRetries = options.MaxRetries;
        _retryDelay = options.RetryBaseDelay;
        _client = options.HttpClient ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        _ownsClient = options.HttpClient == null;
    }

    internal Task<T> PostAsync<T>(string path, object body, string bearer, CancellationToken cancellationToken)
        => SendAsync<T>(HttpMethod.Post, path, body, bearer, cancellationToken);

    internal Task<T> GetAsync<T>(string path, string bearer, CancellationToken cancellationToken)
        => SendAsync<T>(HttpMethod.Get, path, null, bearer, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, string bearer, CancellationToken cancellationToken)
    {
        var bytes = body == null ? Array.Empty<byte>() : JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions);
        if (bytes.Length > MaxRequestBytes)
            throw new ArgumentException($"The request exceeds the {MaxRequestBytes}-byte limit.", nameof(body));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_timeout);
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                using var request = new HttpRequestMessage(method, new Uri(_baseUri, path));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (body != null)
                {
                    request.Content = new ByteArrayContent(bytes);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                }
                HttpResponseMessage response;
                try
                {
                    response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
                }
                catch (HttpRequestException)
                {
                    if (attempt >= _maxRetries) throw new WorkflowException("network_error", 0, true);
                    await DelayAsync(attempt, null, deadline.Token).ConfigureAwait(false);
                    continue;
                }
                using (response)
                {
                    // Defense in depth for injected transports; their handlers must disable redirects themselves.
                    // The SDK-owned handler never follows redirects.
                    if (response.RequestMessage?.RequestUri is { } finalUri &&
                        (finalUri.Scheme != _baseUri.Scheme || finalUri.Authority != _baseUri.Authority))
                        throw new WorkflowException("unsafe_redirect", (int)response.StatusCode, false);
                    var json = await ReadBoundedAsync(response.Content, deadline.Token).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        var error = ParseError(response.StatusCode, json);
                        // A long Retry-After belongs to the host's later retry policy, not this short gameplay-safe deadline.
                        // Never shorten the server's requested backoff and retry early.
                        var serverDelay = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow);
                        if (!error.Retryable || attempt >= _maxRetries || serverDelay > TimeSpan.FromSeconds(2)) throw error;
                        await DelayAsync(attempt, response.Headers.RetryAfter, deadline.Token).ConfigureAwait(false);
                        continue;
                    }
                    try
                    {
                        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new WorkflowException("invalid_response", (int)response.StatusCode, false);
                    }
                    catch (JsonException) { throw new WorkflowException("invalid_response", (int)response.StatusCode, false); }
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WorkflowException("request_timeout", 0, true);
        }
        catch (HttpRequestException) { throw new WorkflowException("network_error", 0, true); }
        catch (IOException) { throw new WorkflowException("network_error", 0, true); }
    }

    private async Task DelayAsync(int attempt, RetryConditionHeaderValue? retryAfter, CancellationToken token)
    {
        var suggested = retryAfter?.Delta ?? (retryAfter?.Date - DateTimeOffset.UtcNow);
        var milliseconds = suggested?.TotalMilliseconds ?? _retryDelay.TotalMilliseconds * Math.Pow(2, attempt);
        await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp(milliseconds, 0, 2000)), token).ConfigureAwait(false);
    }

    private static async Task<string> ReadBoundedAsync(HttpContent content, CancellationToken token)
    {
        if (content.Headers.ContentLength > MaxResponseBytes) throw new WorkflowException("response_too_large", 0, false);
        await using var source = await content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var destination = new MemoryStream();
        var buffer = new byte[4096];
        int read;
        while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            if (destination.Length + read > MaxResponseBytes) throw new WorkflowException("response_too_large", 0, false);
            destination.Write(buffer, 0, read);
        }
        return Encoding.UTF8.GetString(destination.GetBuffer(), 0, (int)destination.Length);
    }

    private static WorkflowException ParseError(HttpStatusCode status, string json)
    {
        var code = "request_failed";
        var retryable = status == HttpStatusCode.TooManyRequests || (int)status >= 500;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                if (error.TryGetProperty("code", out var codeValue) && codeValue.ValueKind == JsonValueKind.String &&
                    Regex.IsMatch(codeValue.GetString() ?? "", "^[a-z][a-z0-9_]{0,63}$")) code = codeValue.GetString()!;
                if (error.TryGetProperty("retryable", out var retry) && retry.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    retryable = retry.GetBoolean() && retryable;
            }
        }
        catch (JsonException) { }
        return new WorkflowException(code, (int)status, retryable);
    }

    public void Dispose() { if (_ownsClient) _client.Dispose(); }
}

internal static class WorkflowValidation
{
    internal static string Token(string value, string name, int limit = 200)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > limit || value.Any(char.IsWhiteSpace) || value.Any(char.IsControl))
            throw new ArgumentException("A bounded, nonempty credential is required.", name);
        return value;
    }
    internal static void RequestId(Guid requestId)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("A nonempty request ID is required.", nameof(requestId));
    }
    internal static string Text(string value, string name, int limit, bool allowEmpty = false)
    {
        if ((!allowEmpty && string.IsNullOrWhiteSpace(value)) || value.Length > limit || value.Any(char.IsControl))
            throw new ArgumentException($"{name} must contain at most {limit} characters without control characters.", name);
        return value;
    }
    internal static string Opaque(string value, string name, int limit)
    {
        Text(value, name, limit);
        if (value.Any(char.IsWhiteSpace) || value.Contains('@'))
            throw new ArgumentException($"{name} must be opaque and contain no whitespace or email address.", name);
        return value;
    }
    internal static WorkflowEvent[] Events(IEnumerable<WorkflowEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var copy = events.Take(51).ToArray();
        if (copy.Length is < 1 or > 50) throw new ArgumentException("Send between one and 50 events.", nameof(events));
        if (copy.Any(e => e == null)) throw new ArgumentException("Events cannot contain null.", nameof(events));
        foreach (var item in copy)
        {
            RequestId(item.EventId);
            if (!Regex.IsMatch(item.Key ?? "", "^(session\\.(started|ended)|milestone\\.[a-z][a-z0-9_.]{0,79})$"))
                throw new ArgumentException("Only session boundaries or registered milestone keys are supported.", nameof(events));
            if (item.Properties?.DurationSeconds is { } seconds && (item.Key != "session.ended" || seconds < 0 || seconds > 86400))
                throw new ArgumentException("Duration is supported only on session.ended and must be between zero and 86400 seconds.", nameof(events));
            if (item.Properties?.InstallationRef is { } installation) Opaque(installation, "InstallationRef", 100);
            if (item.Properties?.CharacterRef is { } character) Opaque(character, "CharacterRef", 100);
        }
        if (copy.Select(e => e.EventId).Distinct().Count() != copy.Length)
            throw new ArgumentException("Each event in a batch requires a distinct event ID.", nameof(events));
        return copy;
    }
}
