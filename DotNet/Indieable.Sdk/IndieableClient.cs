using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IndieableSdk
{
    /// <summary>
    /// Engine-agnostic Indieable HTTP client. Constructing the client is local.
    /// Network and persistent identity are created only by explicit method calls.
    /// </summary>
    public sealed class IndieableClient : IAsyncDisposable
    {
        public const string GameplayTelemetryPurpose = "GAMEPLAY_TELEMETRY";
        public const string DiagnosticsPurpose = "DIAGNOSTICS";

        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web)
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

        private readonly IndieableClientOptions _options;
        private readonly Uri _baseUri;
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly IIndieableIdentityStorage _identityStorage;
        private readonly string _identityStorageKey;
        private readonly SemaphoreSlim _identityGate = new(1, 1);

        private IndieableStoredIdentity? _storedIdentity;
        private bool _identityLoaded;
        private string _sessionToken = "";

        public bool IsConnected => !string.IsNullOrWhiteSpace(_sessionToken);
        public IndieableSessionInfo? Session { get; private set; }

        public IndieableClient(IndieableClientOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.PublicGameKey))
                throw new ArgumentException(
                    "PublicGameKey is required.",
                    nameof(options));

            _baseUri = ValidateBaseUrl(options.BaseUrl);
            _identityStorage = options.IdentityStorage ??
                new IndieableFileIdentityStorage();
            _identityStorageKey = string.Join(
                "|",
                _baseUri.ToString().TrimEnd('/'),
                options.PublicGameKey.Trim(),
                NormalizeEnvironment(options.Environment));

            if (options.HttpClient != null)
            {
                _httpClient = options.HttpClient;
                _ownsHttpClient = false;
            }
            else
            {
                _httpClient = new HttpClient();
                _ownsHttpClient = true;
            }
        }

        public async Task<IndieablePrivacyManifest> GetPrivacyManifestAsync(
            CancellationToken cancellationToken = default)
        {
            var path =
                "api/connect/v1/privacy/manifest?game_key=" +
                Uri.EscapeDataString(_options.PublicGameKey.Trim());
            return await SendAsync<IndieablePrivacyManifest>(
                    HttpMethod.Get,
                    path,
                    null,
                    false,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IndieableSessionInfo> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            await EnsureIdentityLoadedAsync(cancellationToken)
                .ConfigureAwait(false);

            try
            {
                return await ConnectAttemptAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (IndieableApiException exception)
                when (_storedIdentity != null &&
                      _options.AutoClearInvalidIdentity &&
                      (exception.Code == "invalid_installation" ||
                       exception.Code == "installation_expired"))
            {
                await ClearStoredIdentityAsync(cancellationToken)
                    .ConfigureAwait(false);
                return await ConnectAttemptAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public async Task<IndieablePrivacyPreferences>
            GetPrivacyPreferencesAsync(
                CancellationToken cancellationToken = default)
        {
            RequireSession();
            var response = await SendAsync<PreferencesResponse>(
                    HttpMethod.Get,
                    "api/connect/v1/privacy/preferences",
                    null,
                    true,
                    cancellationToken)
                .ConfigureAwait(false);

            return MapPreferences(response);
        }

        public async Task<IndieablePrivacyPreferences>
            SetPrivacyPreferenceAsync(
                string purposeKey,
                bool enabled,
                string? locale = null,
                bool customUi = true,
                CancellationToken cancellationToken = default)
        {
            RequireSession();
            var purpose = (purposeKey ?? "").Trim().ToUpperInvariant();
            if (purpose != GameplayTelemetryPurpose &&
                purpose != DiagnosticsPurpose)
            {
                throw new ArgumentException(
                    "Only gameplay telemetry and diagnostics are optional SDK preferences.",
                    nameof(purposeKey));
            }

            var response = await SendAsync<PreferenceUpdateResponse>(
                    HttpMethod.Post,
                    "api/connect/v1/privacy/preferences",
                    new
                    {
                        purpose_key = purpose,
                        enabled,
                        source = customUi
                            ? "sdk_custom_ui"
                            : "sdk_default_ui",
                        locale = locale ?? "",
                        local_profile_ref = CurrentLocalProfileRef()
                    },
                    true,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(
                    response.InstallationCredential))
            {
                _storedIdentity = new IndieableStoredIdentity
                {
                    InstallationCredential =
                        response.InstallationCredential,
                    LocalProfileRef = CurrentLocalProfileRef()
                };
                await _identityStorage.SaveAsync(
                        _identityStorageKey,
                        _storedIdentity,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            ApplyIdentityResponse(
                response.SessionType,
                response.IdentityState,
                response.PersistentIdentity,
                response.PublicPlayerRef);

            return await GetPrivacyPreferencesAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IndieableSessionInfo> SetLocalProfileAsync(
            string localProfileRef,
            CancellationToken cancellationToken = default)
        {
            RequireSession();
            await EnsureIdentityLoadedAsync(cancellationToken)
                .ConfigureAwait(false);

            if (_storedIdentity == null ||
                string.IsNullOrWhiteSpace(
                    _storedIdentity.InstallationCredential))
            {
                throw new IndieableApiException(
                    "persistent_identity_required",
                    "Enable a persistent Indieable feature before selecting a local profile.");
            }

            var value = (localProfileRef ?? "").Trim();
            var response = await SendAsync<LocalProfileResponse>(
                    HttpMethod.Post,
                    "api/connect/v1/identity/local-profile",
                    new { local_profile_ref = value },
                    true,
                    cancellationToken)
                .ConfigureAwait(false);

            _storedIdentity.LocalProfileRef = value;
            await _identityStorage.SaveAsync(
                    _identityStorageKey,
                    _storedIdentity,
                    cancellationToken)
                .ConfigureAwait(false);

            ApplyIdentityResponse(
                null,
                response.IdentityState,
                response.PersistentIdentity,
                response.PublicPlayerRef);

            return Session ??
                throw new IndieableApiException(
                    "invalid_response",
                    "Indieable returned no active session.");
        }

        public async Task ResetLocalIdentityAsync(
            CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                await SendAsync<JsonElement>(
                        HttpMethod.Post,
                        "api/connect/v1/privacy/reset",
                        new { },
                        true,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await ClearStoredIdentityAsync(cancellationToken)
                .ConfigureAwait(false);
            _sessionToken = "";
            Session = null;
        }

        public async Task<IndieableDeviceLink> BeginAccountLinkAsync(
            CancellationToken cancellationToken = default)
        {
            RequireSession();
            var response = await SendAsync<DeviceResponse>(
                    HttpMethod.Post,
                    "api/connect/v1/device",
                    new { },
                    true,
                    cancellationToken)
                .ConfigureAwait(false);

            return new IndieableDeviceLink
            {
                DeviceCode = response.DeviceCode,
                UserCode = response.UserCode,
                VerificationUrl = response.VerificationUri,
                VerificationUrlComplete =
                    response.VerificationUriComplete,
                ExpiresAt = response.ExpiresAt,
                PollIntervalSeconds = Math.Max(1, response.Interval)
            };
        }

        public async Task<IndieableSessionInfo?> PollAccountLinkAsync(
            IndieableDeviceLink link,
            CancellationToken cancellationToken = default)
        {
            RequireSession();
            ArgumentNullException.ThrowIfNull(link);
            if (string.IsNullOrWhiteSpace(link.DeviceCode))
                throw new ArgumentException(
                    "DeviceCode is required.",
                    nameof(link));

            var response = await SendAsync<LinkResponse>(
                    HttpMethod.Post,
                    "api/connect/v1/device/poll",
                    new { device_code = link.DeviceCode },
                    true,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!string.Equals(
                    response.Status,
                    "linked",
                    StringComparison.OrdinalIgnoreCase))
                return null;

            ApplyIdentityResponse(
                response.SessionType,
                response.IdentityState,
                true,
                response.PlayerRef);
            return Session;
        }

        public async Task<IndieableSessionInfo> ConnectWithSteamTicketAsync(
            string ticketHex,
            CancellationToken cancellationToken = default)
        {
            RequireSession();
            if (string.IsNullOrWhiteSpace(ticketHex))
                throw new ArgumentException(
                    "A Steam Web API ticket is required.",
                    nameof(ticketHex));

            var response = await SendAsync<LinkResponse>(
                    HttpMethod.Post,
                    "api/connect/v1/steam",
                    new { ticket = ticketHex.Trim() },
                    true,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!string.Equals(
                    response.Status,
                    "linked",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IndieableApiException(
                    "steam_link_failed",
                    "Indieable did not link the Steam identity.");
            }

            ApplyIdentityResponse(
                response.SessionType,
                response.IdentityState,
                true,
                response.PlayerRef);

            return Session ??
                throw new IndieableApiException(
                    "invalid_response",
                    "Indieable returned no active session.");
        }

        public Task<IndieableEventReceipt> SendEventAsync(
            string eventKey,
            object? payload = null,
            IndieableEventOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var payloadElement = payload switch
            {
                null => JsonSerializer.SerializeToElement(
                    new Dictionary<string, object?>(),
                    JsonOptions),
                string json => ParsePayloadObject(json),
                JsonElement element => CloneObjectElement(element),
                _ => JsonSerializer.SerializeToElement(
                    payload,
                    payload.GetType(),
                    JsonOptions)
            };

            return SendEventElementAsync(
                eventKey,
                payloadElement,
                options,
                cancellationToken);
        }

        public async Task<IndieableFeedbackConfig>
            GetFeedbackConfigAsync(
                CancellationToken cancellationToken = default)
        {
            RequireSession();
            var response = await SendAsync<FeedbackConfigResponse>(
                    HttpMethod.Post,
                    "api/connect/v1/playtest/config",
                    new { },
                    true,
                    cancellationToken)
                .ConfigureAwait(false);

            return new IndieableFeedbackConfig
            {
                Available = response.Available,
                Reason = response.Reason,
                CampaignId = response.Campaign?.Id ?? "",
                Title = response.Campaign?.Title ??
                    "Playtest feedback",
                Description = response.Campaign?.Description ?? "",
                SurveyQuestions = response.SurveyQuestions,
                Anonymous = response.Anonymous,
                AnonymousAllowed = response.AnonymousAllowed,
                Round = response.Round
            };
        }

        public Task<IndieableSubmissionReceipt> SubmitFeedbackAsync(
            IndieableFeedbackSubmission submission,
            CancellationToken cancellationToken = default)
        {
            RequireSession();
            ArgumentNullException.ThrowIfNull(submission);
            if (submission.Rating < 1 || submission.Rating > 5)
                throw new ArgumentOutOfRangeException(
                    nameof(submission),
                    "Choose a rating from 1 to 5.");

            return SendAsync<IndieableSubmissionReceipt>(
                HttpMethod.Post,
                "api/connect/v1/playtest/feedback",
                submission,
                true,
                cancellationToken);
        }

        public Task<IndieableSubmissionReceipt> SubmitBugReportAsync(
            IndieableBugReportSubmission submission,
            CancellationToken cancellationToken = default)
        {
            RequireSession();
            ArgumentNullException.ThrowIfNull(submission);
            if (string.IsNullOrWhiteSpace(submission.Title))
                throw new ArgumentException(
                    "Give the bug a one-line title.",
                    nameof(submission));

            if (string.IsNullOrWhiteSpace(submission.BuildVersion))
                submission.BuildVersion = _options.BuildVersion;

            return SendAsync<IndieableSubmissionReceipt>(
                HttpMethod.Post,
                "api/connect/v1/playtest/bug-report",
                submission,
                true,
                cancellationToken);
        }

        public Task<IndieableChallengeCollection> GetChallengesAsync(
            CancellationToken cancellationToken = default)
        {
            RequireSession();
            return SendAsync<IndieableChallengeCollection>(
                HttpMethod.Post,
                "api/connect/v1/challenges",
                new { },
                true,
                cancellationToken);
        }

        public async Task<string> JoinChallengeAsync(
            string slug,
            CancellationToken cancellationToken = default)
        {
            RequireSession();
            if (string.IsNullOrWhiteSpace(slug))
                throw new ArgumentException(
                    "Challenge slug is required.",
                    nameof(slug));

            var response = await SendAsync<ChallengeJoinResponse>(
                    HttpMethod.Post,
                    "api/connect/v1/challenges/join",
                    new { slug = slug.Trim() },
                    true,
                    cancellationToken)
                .ConfigureAwait(false);

            return response.Status;
        }

        public Task<IndieableChallengeLeaderboard>
            GetLeaderboardAsync(
                string slug,
                int limit = 50,
                int offset = 0,
                CancellationToken cancellationToken = default)
        {
            RequireSession();
            if (string.IsNullOrWhiteSpace(slug))
                throw new ArgumentException(
                    "Challenge slug is required.",
                    nameof(slug));

            return SendAsync<IndieableChallengeLeaderboard>(
                HttpMethod.Post,
                "api/connect/v1/challenges/leaderboard",
                new
                {
                    slug = slug.Trim(),
                    limit = Math.Clamp(limit, 1, 100),
                    offset = Math.Max(0, offset)
                },
                true,
                cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            _identityGate.Dispose();
            if (_ownsHttpClient) _httpClient.Dispose();
            return ValueTask.CompletedTask;
        }

        private async Task<IndieableEventReceipt>
            SendEventElementAsync(
                string eventKey,
                JsonElement payload,
                IndieableEventOptions? options,
                CancellationToken cancellationToken)
        {
            RequireSession();
            var normalizedEventKey = (eventKey ?? "").Trim();
            if (string.IsNullOrWhiteSpace(normalizedEventKey))
                throw new ArgumentException(
                    "An Indieable event key is required.",
                    nameof(eventKey));
            if (payload.ValueKind != JsonValueKind.Object)
                throw new ArgumentException(
                    "Event payload JSON must be an object.",
                    nameof(payload));

            options ??= new IndieableEventOptions();
            var body = new Dictionary<string, object?>
            {
                ["event_key"] = normalizedEventKey,
                ["idempotency_key"] =
                    string.IsNullOrWhiteSpace(options.IdempotencyKey)
                        ? "dotnet-" + Guid.NewGuid().ToString("N")
                        : options.IdempotencyKey.Trim(),
                ["payload"] = payload,
                ["test"] = options.Test
            };

            if (options.SchemaVersion.HasValue)
                body["schema_version"] =
                    options.SchemaVersion.Value;
            if (options.OccurredAtUtc.HasValue)
                body["occurred_at"] =
                    options.OccurredAtUtc.Value
                        .ToUniversalTime()
                        .ToString("O");
            if (!string.IsNullOrWhiteSpace(options.TraceType))
                body["trace_type"] = options.TraceType.Trim();
            if (!string.IsNullOrWhiteSpace(options.TraceId))
                body["trace_id"] = options.TraceId.Trim();
            if (!string.IsNullOrWhiteSpace(options.RunId))
                body["run_id"] = options.RunId.Trim();

            return await SendAsync<IndieableEventReceipt>(
                    HttpMethod.Post,
                    "api/connect/v1/events",
                    body,
                    true,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task<IndieableSessionInfo> ConnectAttemptAsync(
            CancellationToken cancellationToken)
        {
            var response = await SendAsync<SessionResponse>(
                    HttpMethod.Post,
                    "api/connect/v1/session",
                    new
                    {
                        public_game_key =
                            _options.PublicGameKey.Trim(),
                        sdk_version = _options.SdkVersion,
                        build_version = _options.BuildVersion,
                        platform =
                            string.IsNullOrWhiteSpace(_options.Platform)
                                ? RuntimeInformation.OSDescription
                                : _options.Platform,
                        environment =
                            NormalizeEnvironment(
                                _options.Environment),
                        engine =
                            string.IsNullOrWhiteSpace(_options.Engine)
                                ? ".NET"
                                : _options.Engine,
                        engine_version =
                            string.IsNullOrWhiteSpace(
                                _options.EngineVersion)
                                ? Environment.Version.ToString()
                                : _options.EngineVersion,
                        installation_credential =
                            _storedIdentity?
                                .InstallationCredential ?? "",
                        local_profile_ref =
                            CurrentLocalProfileRef()
                    },
                    false,
                    cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(response.SessionToken))
                throw new IndieableApiException(
                    "invalid_response",
                    "Indieable returned an invalid session response.");

            _sessionToken = response.SessionToken;
            Session = new IndieableSessionInfo
            {
                SessionType = response.SessionType,
                IdentityState = response.IdentityState,
                PersistentIdentity =
                    response.PersistentIdentity,
                PublicPlayerRef = response.PublicPlayerRef,
                ExpiresAt = response.ExpiresAt,
                GameTitle = response.Game?.Title ?? "",
                SteamTicketIdentity =
                    response.SteamTicketIdentity
            };
            return Session;
        }

        private async Task<T> SendAsync<T>(
            HttpMethod method,
            string path,
            object? body,
            bool authenticated,
            CancellationToken cancellationToken)
        {
            if (authenticated) RequireSession();

            using var request = new HttpRequestMessage(
                method,
                new Uri(_baseUri, path.TrimStart('/')));

            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue(
                    "application/json"));
            if (authenticated)
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer",
                        _sessionToken);
            }

            if (body != null)
            {
                var json = JsonSerializer.Serialize(
                    body,
                    body.GetType(),
                    JsonOptions);
                request.Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");
            }

            using var timeout = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);
            if (_options.RequestTimeout > TimeSpan.Zero)
                timeout.CancelAfter(_options.RequestTimeout);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseContentRead,
                        timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                throw new IndieableApiException(
                    "request_timeout",
                    "The Indieable request timed out.");
            }
            catch (HttpRequestException exception)
            {
                throw new IndieableApiException(
                    "network_error",
                    exception.Message,
                    0,
                    exception);
            }

            using (response)
            {
                var json = await response.Content
                    .ReadAsStringAsync(timeout.Token)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    throw ParseApiException(
                        response.StatusCode,
                        json);

                if (typeof(T) == typeof(JsonElement) &&
                    string.IsNullOrWhiteSpace(json))
                {
                    return (T)(object)default(JsonElement);
                }

                try
                {
                    var value = JsonSerializer.Deserialize<T>(
                        string.IsNullOrWhiteSpace(json)
                            ? "{}"
                            : json,
                        JsonOptions);
                    if (value is null)
                    {
                        throw new IndieableApiException(
                            "invalid_response",
                            "Indieable returned an empty response.",
                            (int)response.StatusCode);
                    }
                    return value;
                }
                catch (JsonException exception)
                {
                    throw new IndieableApiException(
                        "invalid_response",
                        "Indieable returned invalid JSON.",
                        (int)response.StatusCode,
                        exception);
                }
            }
        }

        private static IndieableApiException ParseApiException(
            HttpStatusCode status,
            string json)
        {
            try
            {
                var envelope =
                    JsonSerializer.Deserialize<ErrorEnvelope>(
                        json,
                        JsonOptions);
                if (envelope?.Error != null)
                {
                    return new IndieableApiException(
                        envelope.Error.Code,
                        envelope.Error.Message,
                        (int)status);
                }
            }
            catch (JsonException)
            {
                // Fall through to a safe generic provider error.
            }

            return new IndieableApiException(
                "request_failed",
                "Indieable returned HTTP " + (int)status + ".",
                (int)status);
        }

        private async Task EnsureIdentityLoadedAsync(
            CancellationToken cancellationToken)
        {
            if (_identityLoaded) return;

            await _identityGate.WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            try
            {
                if (_identityLoaded) return;
                _storedIdentity = await _identityStorage
                    .LoadAsync(
                        _identityStorageKey,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (_storedIdentity != null &&
                    !string.IsNullOrWhiteSpace(
                        _options.LocalProfileRef))
                {
                    _storedIdentity.LocalProfileRef =
                        _options.LocalProfileRef.Trim();
                }

                _identityLoaded = true;
            }
            finally
            {
                _identityGate.Release();
            }
        }

        private async Task ClearStoredIdentityAsync(
            CancellationToken cancellationToken)
        {
            _storedIdentity = null;
            _identityLoaded = true;
            await _identityStorage.ClearAsync(
                    _identityStorageKey,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private string CurrentLocalProfileRef()
        {
            if (!string.IsNullOrWhiteSpace(
                    _options.LocalProfileRef))
                return _options.LocalProfileRef.Trim();
            return _storedIdentity?.LocalProfileRef ?? "";
        }

        private void ApplyIdentityResponse(
            string? sessionType,
            string identityState,
            bool persistentIdentity,
            string publicPlayerRef)
        {
            if (Session == null) return;
            if (!string.IsNullOrWhiteSpace(sessionType))
                Session.SessionType = sessionType;
            Session.IdentityState = identityState;
            Session.PersistentIdentity = persistentIdentity;
            Session.PublicPlayerRef = publicPlayerRef;
        }

        private static IndieablePrivacyPreferences MapPreferences(
            PreferencesResponse response)
        {
            var manifest = response.Manifest == null
                ? null
                : new IndieablePrivacyManifest
                {
                    Configured = true,
                    ManifestVersion =
                        response.Manifest.Version,
                    NoticeVersion =
                        response.Manifest.NoticeVersion,
                    AudienceClassification =
                        response.Manifest.AudienceClassification,
                    PublishedAt =
                        response.Manifest.PublishedAt,
                    Controller =
                        new IndieablePrivacyController
                        {
                            Name =
                                response.Manifest.ControllerName,
                            Contact =
                                response.Manifest
                                    .ControllerContact,
                            PrivacyPolicyUrl =
                                response.Manifest
                                    .PrivacyPolicyUrl
                        },
                    Purposes = response.Manifest.Purposes
                };

            return new IndieablePrivacyPreferences
            {
                PersistentIdentity =
                    response.PersistentIdentity,
                IdentityState = response.IdentityState,
                PublicPlayerRef = response.PublicPlayerRef,
                Manifest = manifest,
                Permissions = response.Permissions
            };
        }

        private void RequireSession()
        {
            if (!IsConnected)
            {
                throw new IndieableApiException(
                    "not_connected",
                    "Call ConnectAsync before this operation.");
            }
        }

        private static Uri ValidateBaseUrl(string? baseUrl)
        {
            var value = string.IsNullOrWhiteSpace(baseUrl)
                ? "https://indieable.com"
                : baseUrl.Trim();

            if (!Uri.TryCreate(
                    value.TrimEnd('/') + "/",
                    UriKind.Absolute,
                    out var uri))
                throw new ArgumentException(
                    "BaseUrl must be an absolute URI.",
                    nameof(baseUrl));

            if (!string.IsNullOrEmpty(uri.UserInfo))
                throw new ArgumentException(
                    "BaseUrl cannot include user information.",
                    nameof(baseUrl));

            var allowed =
                string.Equals(
                    uri.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(
                     uri.Scheme,
                     Uri.UriSchemeHttp,
                     StringComparison.OrdinalIgnoreCase) &&
                 uri.IsLoopback);

            if (!allowed)
                throw new ArgumentException(
                    "BaseUrl must use HTTPS. HTTP is allowed only for loopback development.",
                    nameof(baseUrl));

            return uri;
        }

        private static string NormalizeEnvironment(
            string? environment)
        {
            var value = (environment ?? "production")
                .Trim()
                .ToLowerInvariant();
            return value switch
            {
                "development" => "development",
                "test" => "test",
                _ => "production"
            };
        }

        private static JsonElement ParsePayloadObject(
            string json)
        {
            try
            {
                using var document = JsonDocument.Parse(
                    string.IsNullOrWhiteSpace(json)
                        ? "{}"
                        : json);
                return CloneObjectElement(
                    document.RootElement);
            }
            catch (JsonException exception)
            {
                throw new ArgumentException(
                    "Event payload JSON is invalid.",
                    nameof(json),
                    exception);
            }
        }

        private static JsonElement CloneObjectElement(
            JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new ArgumentException(
                    "Event payload JSON must be an object.");
            return element.Clone();
        }

        private sealed class DeviceResponse
        {
            public string DeviceCode { get; set; } = "";
            public string UserCode { get; set; } = "";
            public string VerificationUri { get; set; } = "";
            public string VerificationUriComplete { get; set; } = "";
            public string ExpiresAt { get; set; } = "";
            public int Interval { get; set; }
        }

        private sealed class ChallengeJoinResponse
        {
            public string Status { get; set; } = "";
            public bool Duplicate { get; set; }
            public string Slug { get; set; } = "";
        }
    }
}
