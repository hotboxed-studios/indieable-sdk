using System;
using System.Collections;
using System.Text;
using IndieableSdk.Steam;
using UnityEngine;
using UnityEngine.Networking;

namespace IndieableSdk
{
    internal sealed class IndieableClient
    {
        private readonly IndieableOptions _options;
        private readonly IIndieableIdentityStorage _identityStorage;
        private readonly string _identityStorageKey;
        private IndieableStoredIdentity _storedIdentity;
        private string _sessionToken;
        private IndieableSessionInfo _session;

        internal bool IsConnected { get { return !string.IsNullOrEmpty(_sessionToken); } }
        internal IndieableSessionInfo Session { get { return _session; } }
        internal IndieableOptions Options { get { return _options; } }

        internal IndieableClient(IndieableOptions options)
        {
            _options = options;
            _identityStorage = options.IdentityStorage ?? new IndieableFileIdentityStorage();
            _identityStorageKey = string.Format("{0}|{1}|{2}",
                (options.BaseUrl ?? "https://indieable.com").TrimEnd('/'),
                options.PublicGameKey ?? "",
                options.Environment ?? "production");
            _storedIdentity = _identityStorage.Load(_identityStorageKey);
            if (_storedIdentity != null && !string.IsNullOrWhiteSpace(options.LocalProfileRef))
                _storedIdentity.LocalProfileRef = options.LocalProfileRef.Trim();
        }

        internal IEnumerator Connect(Action<IndieableSessionInfo> onSuccess, Action<IndieableError> onError)
        {
            IndieableError error = null;
            var connected = false;
            yield return ConnectAttempt(
                delegate(IndieableSessionInfo info) { connected = true; if (onSuccess != null) onSuccess(info); },
                delegate(IndieableError caught) { error = caught; });

            if (connected) yield break;
            if (_storedIdentity != null && _options.AutoClearInvalidIdentity && error != null &&
                (error.Code == "invalid_installation" || error.Code == "installation_expired"))
            {
                ClearStoredIdentity();
                error = null;
                yield return ConnectAttempt(
                    delegate(IndieableSessionInfo info) { connected = true; if (onSuccess != null) onSuccess(info); },
                    delegate(IndieableError caught) { error = caught; });
                if (connected) yield break;
            }
            if (error != null) Fail(onError, error);
        }

        private IEnumerator ConnectAttempt(Action<IndieableSessionInfo> onSuccess, Action<IndieableError> onError)
        {
            var request = new SessionRequest
            {
                public_game_key = _options.PublicGameKey,
                sdk_version = _options.SdkVersion,
                build_version = _options.BuildVersion,
                platform = string.IsNullOrWhiteSpace(_options.Platform) ? Application.platform.ToString() : _options.Platform,
                environment = _options.Environment,
                engine = string.IsNullOrWhiteSpace(_options.Engine) ? "Unity" : _options.Engine,
                engine_version = string.IsNullOrWhiteSpace(_options.EngineVersion) ? Application.unityVersion : _options.EngineVersion,
                installation_credential = _storedIdentity != null ? _storedIdentity.InstallationCredential : "",
                local_profile_ref = CurrentLocalProfileRef()
            };
            yield return SendRequest("/api/connect/v1/session", UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(request), null,
                delegate(string json)
                {
                    var response = JsonUtility.FromJson<SessionResponse>(json);
                    if (response == null || string.IsNullOrWhiteSpace(response.session_token))
                    {
                        if (onError != null) onError(new IndieableError("invalid_response", "Indieable returned an invalid session response."));
                        return;
                    }
                    _sessionToken = response.session_token;
                    _session = MapSession(response);
                    if (onSuccess != null) onSuccess(_session);
                }, onError, false);
        }

        internal IEnumerator GetPrivacyManifest(Action<IndieablePrivacyManifest> onSuccess,
            Action<IndieableError> onError)
        {
            var path = "/api/connect/v1/privacy/manifest?game_key=" + UnityWebRequest.EscapeURL(_options.PublicGameKey ?? "");
            yield return SendRequest(path, UnityWebRequest.kHttpVerbGET, null, null,
                delegate(string json)
                {
                    var response = JsonUtility.FromJson<PrivacyManifestResponse>(json);
                    if (response == null)
                    {
                        Fail(onError, new IndieableError("invalid_response", "Indieable returned an invalid privacy notice."));
                        return;
                    }
                    if (onSuccess != null) onSuccess(MapPrivacyManifest(response));
                }, onError);
        }

        internal IEnumerator GetPrivacyPreferences(Action<IndieablePrivacyPreferences> onSuccess,
            Action<IndieableError> onError)
        {
            if (!RequireSession(onError)) yield break;
            yield return SendRequest("/api/connect/v1/privacy/preferences", UnityWebRequest.kHttpVerbGET,
                null, _sessionToken,
                delegate(string json)
                {
                    var response = JsonUtility.FromJson<PrivacyPreferencesResponse>(json);
                    if (response == null)
                    {
                        Fail(onError, new IndieableError("invalid_response", "Indieable returned invalid privacy preferences."));
                        return;
                    }
                    if (onSuccess != null) onSuccess(MapPreferences(response));
                }, onError);
        }

        internal IEnumerator SetPrivacyPreference(string purposeKey, bool enabled, string locale,
            bool customUi, Action<IndieablePrivacyPreferences> onSuccess, Action<IndieableError> onError)
        {
            if (!RequireSession(onError)) yield break;
            var normalizedPurpose = (purposeKey ?? "").Trim().ToUpperInvariant();
            if (normalizedPurpose != "GAMEPLAY_TELEMETRY" && normalizedPurpose != "DIAGNOSTICS")
            {
                Fail(onError, new IndieableError("invalid_preference", "Only gameplay telemetry and diagnostics are optional SDK preferences."));
                yield break;
            }
            var request = new PrivacyPreferenceRequest
            {
                purpose_key = normalizedPurpose,
                enabled = enabled,
                source = customUi ? "sdk_custom_ui" : "sdk_default_ui",
                locale = locale ?? "",
                local_profile_ref = CurrentLocalProfileRef()
            };
            yield return SendRequest("/api/connect/v1/privacy/preferences", UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(request), _sessionToken,
                delegate(string json)
                {
                    var response = JsonUtility.FromJson<PrivacyPreferenceUpdateResponse>(json);
                    if (response == null)
                    {
                        Fail(onError, new IndieableError("invalid_response", "Indieable returned an invalid privacy response."));
                        return;
                    }
                    if (!string.IsNullOrWhiteSpace(response.installation_credential))
                    {
                        _storedIdentity = new IndieableStoredIdentity
                        {
                            InstallationCredential = response.installation_credential,
                            LocalProfileRef = CurrentLocalProfileRef()
                        };
                        SaveStoredIdentity();
                    }
                    ApplyIdentityResponse(response.session_type, response.identity_state,
                        response.persistent_identity, response.public_player_ref);
                    IndieableRuntime.Instance.Run(GetPrivacyPreferences(onSuccess, onError));
                }, onError);
        }

        internal IEnumerator SetLocalProfile(string localProfileRef, Action<IndieableSessionInfo> onSuccess,
            Action<IndieableError> onError)
        {
            if (!RequireSession(onError)) yield break;
            if (_storedIdentity == null || string.IsNullOrWhiteSpace(_storedIdentity.InstallationCredential))
            {
                Fail(onError, new IndieableError("persistent_identity_required", "Enable a persistent Indieable feature before selecting a local profile."));
                yield break;
            }
            var value = (localProfileRef ?? "").Trim();
            yield return SendRequest("/api/connect/v1/identity/local-profile", UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(new LocalProfileRequest { local_profile_ref = value }), _sessionToken,
                delegate(string json)
                {
                    var response = JsonUtility.FromJson<LocalProfileResponse>(json);
                    if (response == null)
                    {
                        Fail(onError, new IndieableError("invalid_response", "Indieable returned an invalid local-profile response."));
                        return;
                    }
                    _storedIdentity.LocalProfileRef = value;
                    SaveStoredIdentity();
                    ApplyIdentityResponse(null, response.identity_state, response.persistent_identity, response.public_player_ref);
                    if (onSuccess != null) onSuccess(_session);
                }, onError);
        }

        internal IEnumerator ResetLocalIdentity(Action onSuccess, Action<IndieableError> onError)
        {
            if (!IsConnected)
            {
                ClearStoredIdentity();
                if (onSuccess != null) onSuccess();
                yield break;
            }
            yield return SendRequest("/api/connect/v1/privacy/reset", UnityWebRequest.kHttpVerbPOST,
                "{}", _sessionToken,
                delegate(string _)
                {
                    ClearStoredIdentity();
                    _sessionToken = null;
                    _session = null;
                    if (onSuccess != null) onSuccess();
                }, onError);
        }

        internal IEnumerator BeginDeviceLink(bool pollUntilLinked, Action<IndieableDeviceLink> onCode,
            Action<IndieableSessionInfo> onLinked, Action<IndieableError> onError)
        {
            if (!RequireSession(onError)) yield break;
            DeviceResponse device = null;
            yield return SendJson("/api/connect/v1/device", "{}", _sessionToken,
                delegate(string json) { device = JsonUtility.FromJson<DeviceResponse>(json); }, onError);
            if (device == null || string.IsNullOrWhiteSpace(device.device_code)) yield break;
            var link = new IndieableDeviceLink
            {
                DeviceCode = device.device_code,
                UserCode = device.user_code,
                VerificationUrl = device.verification_uri,
                VerificationUrlComplete = device.verification_uri_complete,
                ExpiresAt = device.expires_at,
                PollIntervalSeconds = Math.Max(1, device.interval)
            };
            if (onCode != null) onCode(link);
            if (pollUntilLinked) yield return PollDeviceLink(link, onLinked, onError);
        }

        internal IEnumerator PollDeviceLink(IndieableDeviceLink link,
            Action<IndieableSessionInfo> onLinked, Action<IndieableError> onError)
        {
            if (!RequireSession(onError) || link == null || string.IsNullOrWhiteSpace(link.DeviceCode)) yield break;
            DateTime expiresAt;
            if (!DateTime.TryParse(link.ExpiresAt, out expiresAt)) expiresAt = DateTime.UtcNow.AddMinutes(10);
            while (DateTime.UtcNow < expiresAt.ToUniversalTime())
            {
                LinkResponse result = null;
                IndieableError pollError = null;
                yield return SendJson("/api/connect/v1/device/poll",
                    JsonUtility.ToJson(new DevicePollRequest { device_code = link.DeviceCode }), _sessionToken,
                    delegate(string json) { result = JsonUtility.FromJson<LinkResponse>(json); },
                    delegate(IndieableError error) { pollError = error; });
                if (result != null && result.status == "linked")
                {
                    ApplyIdentityResponse(result.session_type, result.identity_state, true, result.player_ref);
                    if (onLinked != null) onLinked(_session);
                    yield break;
                }
                if (pollError != null)
                {
                    if (pollError.Code == "slow_down") link.PollIntervalSeconds = Math.Min(30, link.PollIntervalSeconds + 5);
                    else { Fail(onError, pollError); yield break; }
                }
                yield return new WaitForSecondsRealtime(Math.Max(1, link.PollIntervalSeconds));
            }
            Fail(onError, new IndieableError("expired_device_code", "The Indieable link code expired."));
        }

        internal void ConnectWithSteam(IIndieableSteamTicketProvider provider,
            Action<IndieableSessionInfo> onSuccess, Action<IndieableError> onError)
        {
            if (!RequireSession(onError)) return;
            if (provider == null)
            {
                Fail(onError, new IndieableError("steam_provider_missing", "A Steam ticket provider is required."));
                return;
            }
            var identity = string.IsNullOrWhiteSpace(_session.SteamTicketIdentity) ? "indieable" : _session.SteamTicketIdentity;
            try
            {
                provider.GetTicketForWebApi(identity,
                    delegate(string ticketHex) { IndieableRuntime.Instance.Run(SendSteamTicket(ticketHex, onSuccess, onError)); },
                    delegate(string message) { Fail(onError, new IndieableError("steam_ticket_failed", message)); });
            }
            catch (Exception exception) { Fail(onError, new IndieableError("steam_ticket_failed", exception.Message)); }
        }

        private IEnumerator SendSteamTicket(string ticketHex, Action<IndieableSessionInfo> onSuccess,
            Action<IndieableError> onError)
        {
            LinkResponse result = null;
            yield return SendJson("/api/connect/v1/steam",
                JsonUtility.ToJson(new SteamRequest { ticket = ticketHex }), _sessionToken,
                delegate(string json) { result = JsonUtility.FromJson<LinkResponse>(json); }, onError);
            if (result == null || result.status != "linked") yield break;
            ApplyIdentityResponse(result.session_type, result.identity_state, true, result.player_ref);
            if (onSuccess != null) onSuccess(_session);
        }

        internal IEnumerator SendEvent(
            string eventKey,
            string payloadJson,
            IndieableEventOptions options,
            Action onSuccess,
            Action<IndieableError> onError)
        {
            if (!RequireSession(onError)) yield break;
            if (string.IsNullOrWhiteSpace(eventKey))
            {
                Fail(onError, new IndieableError("invalid_event", "An Indieable event key is required."));
                yield break;
            }
            var payload = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson.Trim();
            if (!payload.StartsWith("{") || !payload.EndsWith("}"))
            {
                Fail(onError, new IndieableError("invalid_event", "Event payload JSON must be an object."));
                yield break;
            }
            options = options ?? new IndieableEventOptions();
            if (options.SchemaVersion.HasValue && options.SchemaVersion.Value <= 0)
            {
                Fail(onError, new IndieableError(
                    "invalid_event",
                    "Event schema version must be positive when supplied."));
                yield break;
            }
            var key = string.IsNullOrWhiteSpace(options.IdempotencyKey)
                ? "unity-" + Guid.NewGuid().ToString("N")
                : options.IdempotencyKey.Trim();
            var body = BuildEventRequestBody(
                eventKey.Trim(),
                payload,
                key,
                options);
            yield return SendJson("/api/connect/v1/events", body, _sessionToken,
                delegate(string _) { if (onSuccess != null) onSuccess(); }, onError);
        }

        internal static string BuildEventRequestBody(
            string eventKey,
            string payloadJson,
            string idempotencyKey,
            IndieableEventOptions options)
        {
            options = options ?? new IndieableEventOptions();
            var body = new StringBuilder()
                .Append("{\"event_key\":\"")
                .Append(EscapeJson(eventKey))
                .Append("\",\"idempotency_key\":\"")
                .Append(EscapeJson(idempotencyKey))
                .Append("\",\"payload\":")
                .Append(payloadJson)
                .Append(",\"test\":")
                .Append(options.Test ? "true" : "false");
            if (options.SchemaVersion.HasValue)
            {
                body.Append(",\"schema_version\":")
                    .Append(options.SchemaVersion.Value);
            }
            if (options.OccurredAtUtc.HasValue)
            {
                body.Append(",\"occurred_at\":\"")
                    .Append(EscapeJson(
                        options.OccurredAtUtc.Value
                            .ToUniversalTime()
                            .ToString("O")))
                    .Append('"');
            }
            AppendJsonString(body, "trace_type", options.TraceType);
            AppendJsonString(body, "trace_id", options.TraceId);
            AppendJsonString(body, "run_id", options.RunId);
            body.Append('}');
            return body.ToString();
        }

        internal IEnumerator GetFeedbackConfig(Action<IndieableFeedbackConfig> onSuccess,
            Action<IndieableError> onError)
        {
            if (!RequireSession(onError)) yield break;
            yield return SendJson("/api/connect/v1/playtest/config", "{}", _sessionToken,
                delegate(string json)
                {
                    var response = JsonUtility.FromJson<FeedbackConfigResponse>(json);
                    if (response == null)
                    {
                        Fail(onError, new IndieableError("invalid_response", "Indieable returned an invalid feedback configuration."));
                        return;
                    }
                    var config = new IndieableFeedbackConfig
                    {
                        Available = response.available,
                        Reason = response.reason,
                        CampaignId = response.campaign != null ? response.campaign.id : "",
                        Title = response.campaign != null ? response.campaign.title : "Playtest feedback",
                        Description = response.campaign != null ? response.campaign.description : "",
                        SurveyQuestions = response.survey_questions ?? new string[0],
                        Anonymous = response.anonymous,
                        AnonymousAllowed = response.anonymous_allowed,
                        Round = response.round == null ? null : new IndieablePlaytestRound
                        {
                            Id = response.round.id,
                            Number = response.round.number,
                            BuildLabel = response.round.build_label,
                            Focus = response.round.focus,
                            EndsAt = response.round.ends_at
                        }
                    };
                    if (onSuccess != null) onSuccess(config);
                }, onError);
        }

        internal IEnumerator SubmitFeedback(IndieableFeedbackSubmission submission,
            Action<string> onSuccess, Action<IndieableError> onError)
        {
            if (!RequireSession(onError)) yield break;
            if (submission == null || submission.Rating < 1 || submission.Rating > 5)
            {
                Fail(onError, new IndieableError("invalid_feedback", "Choose a rating from 1 to 5."));
                yield break;
            }
            yield return SendJson("/api/connect/v1/playtest/feedback", BuildFeedbackJson(submission), _sessionToken,
                delegate(string json)
                {
                    var response = JsonUtility.FromJson<SubmissionResponse>(json);
                    if (response == null || !response.accepted)
                    {
                        Fail(onError, new IndieableError("invalid_response", "Indieable returned an invalid feedback response."));
                        return;
                    }
                    if (onSuccess != null) onSuccess(response.submission_id);
                }, onError);
        }

        internal IEnumerator SubmitBugReport(IndieableBugReportSubmission submission,
            Action<string> onSuccess, Action<IndieableError> onError)
        {
            if (!RequireSession(onError)) yield break;
            if (submission == null || string.IsNullOrWhiteSpace(submission.Title))
            {
                Fail(onError, new IndieableError("invalid_bug_report", "Give the bug a one-line title."));
                yield break;
            }
            var request = new BugReportRequest
            {
                title = submission.Title,
                description = submission.Description,
                severity = submission.Severity,
                build_version = string.IsNullOrWhiteSpace(submission.BuildVersion) ? _options.BuildVersion : submission.BuildVersion
            };
            yield return SendJson("/api/connect/v1/playtest/bug-report", JsonUtility.ToJson(request), _sessionToken,
                delegate(string json)
                {
                    var response = JsonUtility.FromJson<SubmissionResponse>(json);
                    if (response == null || !response.accepted)
                    {
                        Fail(onError, new IndieableError("invalid_response", "Indieable returned an invalid bug-report response."));
                        return;
                    }
                    if (onSuccess != null) onSuccess(response.report_id);
                }, onError);
        }

        internal IEnumerator GetChallenges(Action<IndieableChallengeCollection> onSuccess,
            Action<IndieableError> onError)
        {
            if (!RequireSession(onError)) yield break;
            yield return SendJson("/api/connect/v1/challenges", "{}", _sessionToken,
                delegate(string json)
                {
                    var response = JsonUtility.FromJson<ChallengeListResponse>(json);
                    if (response == null)
                    {
                        Fail(onError, new IndieableError("invalid_response", "Indieable returned an invalid Challenge list."));
                        return;
                    }
                    if (onSuccess != null) onSuccess(new IndieableChallengeCollection
                    {
                        Linked = response.linked,
                        Joined = MapChallenges(response.joined),
                        Joinable = MapChallenges(response.joinable)
                    });
                }, onError);
        }

        internal IEnumerator JoinChallenge(string slug, Action<string> onSuccess,
            Action<IndieableError> onError)
        {
            if (!RequireSession(onError)) yield break;
            if (string.IsNullOrWhiteSpace(slug))
            {
                Fail(onError, new IndieableError("invalid_challenge", "Challenge slug is required."));
                yield break;
            }
            yield return SendJson("/api/connect/v1/challenges/join",
                JsonUtility.ToJson(new ChallengeSlugRequest { slug = slug.Trim() }), _sessionToken,
                delegate(string json)
                {
                    var response = JsonUtility.FromJson<ChallengeJoinResponse>(json);
                    if (response == null || string.IsNullOrWhiteSpace(response.status))
                    {
                        Fail(onError, new IndieableError("invalid_response", "Indieable returned an invalid join response."));
                        return;
                    }
                    if (onSuccess != null) onSuccess(response.status);
                }, onError);
        }

        internal IEnumerator GetLeaderboard(string slug, int limit, int offset,
            Action<IndieableChallengeLeaderboard> onSuccess, Action<IndieableError> onError)
        {
            if (!RequireSession(onError)) yield break;
            if (string.IsNullOrWhiteSpace(slug))
            {
                Fail(onError, new IndieableError("invalid_challenge", "Challenge slug is required."));
                yield break;
            }
            var request = new ChallengeLeaderboardRequest
            {
                slug = slug.Trim(),
                limit = Math.Max(1, Math.Min(100, limit)),
                offset = Math.Max(0, offset)
            };
            yield return SendJson("/api/connect/v1/challenges/leaderboard", JsonUtility.ToJson(request), _sessionToken,
                delegate(string json)
                {
                    var response = JsonUtility.FromJson<ChallengeLeaderboardResponse>(json);
                    if (response == null || response.metric == null)
                    {
                        Fail(onError, new IndieableError("invalid_response", "Indieable returned an invalid leaderboard."));
                        return;
                    }
                    var rows = response.items ?? new ChallengeLeaderboardItemResponse[0];
                    var mapped = new IndieableLeaderboardItem[rows.Length];
                    for (var i = 0; i < rows.Length; i++)
                    {
                        mapped[i] = new IndieableLeaderboardItem
                        {
                            Rank = rows[i].rank,
                            DisplayName = rows[i].display_name,
                            BestScore = rows[i].best_value,
                            AchievedAt = rows[i].achieved_at,
                            IsCurrentPlayer = rows[i].is_current_player,
                            PersonalRecordCount = rows[i].pr_count
                        };
                    }
                    if (onSuccess != null) onSuccess(new IndieableChallengeLeaderboard
                    {
                        ChallengeSlug = response.slug,
                        MetricName = response.metric.display_name,
                        ValueType = response.metric.value_type,
                        SortDirection = response.metric.sort_direction,
                        Total = response.total,
                        Items = mapped
                    });
                }, onError);
        }

        internal void NotifyFeedbackVisibility(bool visible)
        {
            if (_options.FeedbackVisibilityChanged == null) return;
            try { _options.FeedbackVisibilityChanged(visible); }
            catch (Exception exception)
            {
                if (_options.LogErrors) Debug.LogWarning("[Indieable] FeedbackVisibilityChanged failed: " + exception.Message);
            }
        }

        internal void NotifyPrivacyVisibility(bool visible)
        {
            if (_options.PrivacyVisibilityChanged == null) return;
            try { _options.PrivacyVisibilityChanged(visible); }
            catch (Exception exception)
            {
                if (_options.LogErrors) Debug.LogWarning("[Indieable] PrivacyVisibilityChanged failed: " + exception.Message);
            }
        }

        private IndieableSessionInfo MapSession(SessionResponse response)
        {
            return new IndieableSessionInfo
            {
                SessionType = response.session_type,
                IdentityState = string.IsNullOrWhiteSpace(response.identity_state) ? "EPHEMERAL_SESSION" : response.identity_state,
                PersistentIdentity = response.persistent_identity,
                PublicPlayerRef = response.public_player_ref,
                ExpiresAt = response.expires_at,
                GameTitle = response.game != null ? response.game.title : "",
                SteamTicketIdentity = response.steam_ticket_identity
            };
        }

        private void ApplyIdentityResponse(string sessionType, string identityState,
            bool persistentIdentity, string publicPlayerRef)
        {
            if (_session == null) return;
            if (!string.IsNullOrWhiteSpace(sessionType)) _session.SessionType = sessionType;
            if (!string.IsNullOrWhiteSpace(identityState)) _session.IdentityState = identityState;
            _session.PersistentIdentity = persistentIdentity;
            if (!string.IsNullOrWhiteSpace(publicPlayerRef)) _session.PublicPlayerRef = publicPlayerRef;
        }

        private static IndieablePrivacyManifest MapPrivacyManifest(PrivacyManifestResponse response)
        {
            var rows = response.purposes ?? new PrivacyPurposeResponse[0];
            var purposes = new IndieablePrivacyPurpose[rows.Length];
            for (var i = 0; i < rows.Length; i++) purposes[i] = MapPrivacyPurpose(rows[i]);
            return new IndieablePrivacyManifest
            {
                Configured = response.configured,
                GameTitle = response.game_title,
                ManifestVersion = response.manifest_version,
                NoticeVersion = response.notice_version,
                AudienceClassification = response.audience_classification,
                PublishedAt = response.published_at,
                Controller = response.controller == null ? null : new IndieablePrivacyController
                {
                    Name = response.controller.name,
                    Contact = response.controller.contact,
                    PrivacyPolicyUrl = response.controller.privacy_policy_url
                },
                Purposes = purposes
            };
        }

        private static IndieablePrivacyManifest MapOwnerManifest(PrivacyManifestOwnerResponse response)
        {
            if (response == null) return null;
            var rows = response.purposes ?? new PrivacyPurposeResponse[0];
            var purposes = new IndieablePrivacyPurpose[rows.Length];
            for (var i = 0; i < rows.Length; i++) purposes[i] = MapPrivacyPurpose(rows[i]);
            return new IndieablePrivacyManifest
            {
                Configured = true,
                ManifestVersion = response.version,
                NoticeVersion = response.notice_version,
                AudienceClassification = response.audience_classification,
                PublishedAt = response.published_at,
                Controller = new IndieablePrivacyController
                {
                    Name = response.controller_name,
                    Contact = response.controller_contact,
                    PrivacyPolicyUrl = response.privacy_policy_url
                },
                Purposes = purposes
            };
        }

        private static IndieablePrivacyPurpose MapPrivacyPurpose(PrivacyPurposeResponse row)
        {
            return new IndieablePrivacyPurpose
            {
                PurposeKey = row.purpose_key,
                DisplayName = row.display_name,
                Description = row.description,
                Enabled = row.enabled,
                RequiresAffirmativePermission = row.requires_affirmative_permission,
                AllowsPersistentIdentifier = row.allows_persistent_identifier,
                AuthorityModel = row.authority_model,
                TerminalStorageAuthority = row.terminal_storage_authority,
                RetentionDays = row.retention_days,
                HasRetentionDays = row.retention_days > 0
            };
        }

        private static IndieablePrivacyPreferences MapPreferences(PrivacyPreferencesResponse response)
        {
            var rows = response.permissions ?? new PrivacyPreferenceResponse[0];
            var permissions = new IndieablePrivacyPreference[rows.Length];
            for (var i = 0; i < rows.Length; i++)
            {
                permissions[i] = new IndieablePrivacyPreference
                {
                    PurposeKey = rows[i].purpose_key,
                    State = rows[i].state,
                    EffectiveAt = rows[i].effective_at,
                    RequiresAffirmativePermission = rows[i].requires_affirmative_permission
                };
            }
            return new IndieablePrivacyPreferences
            {
                PersistentIdentity = response.persistent_identity,
                IdentityState = response.identity_state,
                PublicPlayerRef = response.public_player_ref,
                Manifest = MapOwnerManifest(response.manifest),
                Permissions = permissions
            };
        }

        private static IndieableChallengeSummary[] MapChallenges(ChallengeSummaryResponse[] rows)
        {
            rows = rows ?? new ChallengeSummaryResponse[0];
            var mapped = new IndieableChallengeSummary[rows.Length];
            for (var i = 0; i < rows.Length; i++)
            {
                mapped[i] = new IndieableChallengeSummary
                {
                    Slug = rows[i].slug,
                    Name = rows[i].name,
                    Visibility = rows[i].visibility,
                    MetricName = rows[i].metric_name,
                    SortDirection = rows[i].sort_direction,
                    MembershipStatus = rows[i].membership_status,
                    StartsAt = rows[i].starts_at,
                    EndsAt = rows[i].ends_at,
                    HasCurrentScore = rows[i].has_current_score,
                    CurrentScore = rows[i].current_score
                };
            }
            return mapped;
        }

        private string BuildFeedbackJson(IndieableFeedbackSubmission submission)
        {
            var builder = new StringBuilder();
            builder.Append("{\"rating\":").Append(submission.Rating);
            AppendJsonString(builder, "liked", submission.Liked);
            AppendJsonString(builder, "confused", submission.Confused);
            if (submission.IncludeWouldWishlist)
                builder.Append(",\"would_wishlist\":").Append(submission.WouldWishlist ? "true" : "false");
            AppendJsonString(builder, "play_length", submission.PlayLength);
            AppendJsonString(builder, "pitch", submission.Pitch);
            AppendJsonString(builder, "build_version",
                string.IsNullOrWhiteSpace(submission.BuildVersion) ? _options.BuildVersion : submission.BuildVersion);
            builder.Append(",\"answers\":[");
            var answers = submission.Answers ?? new IndieableSurveyAnswer[0];
            var first = true;
            foreach (var answer in answers)
            {
                if (answer == null || string.IsNullOrWhiteSpace(answer.Question) || string.IsNullOrWhiteSpace(answer.Answer)) continue;
                if (!first) builder.Append(',');
                first = false;
                builder.Append("{\"question\":\"").Append(EscapeJson(answer.Question)).Append("\",\"answer\":\"")
                    .Append(EscapeJson(answer.Answer)).Append("\"}");
            }
            builder.Append("]}");
            return builder.ToString();
        }

        private string CurrentLocalProfileRef()
        {
            if (!string.IsNullOrWhiteSpace(_options.LocalProfileRef)) return _options.LocalProfileRef.Trim();
            return _storedIdentity != null ? (_storedIdentity.LocalProfileRef ?? "") : "";
        }

        private void SaveStoredIdentity()
        {
            try { _identityStorage.Save(_identityStorageKey, _storedIdentity); }
            catch (Exception exception)
            {
                if (_options.LogErrors) Debug.LogWarning("[Indieable] Could not save the local identity: " + exception.Message);
            }
        }

        private void ClearStoredIdentity()
        {
            _storedIdentity = null;
            try { _identityStorage.Clear(_identityStorageKey); }
            catch { }
        }

        private static void AppendJsonString(StringBuilder builder, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            builder.Append(",\"").Append(key).Append("\":\"").Append(EscapeJson(value)).Append("\"");
        }

        private bool RequireSession(Action<IndieableError> onError)
        {
            if (IsConnected) return true;
            Fail(onError, new IndieableError("not_connected", "Call Indieable.Connect before using this API."));
            return false;
        }

        private IEnumerator SendJson(string path, string body, string bearer,
            Action<string> onSuccess, Action<IndieableError> onError)
        {
            return SendRequest(path, UnityWebRequest.kHttpVerbPOST, body, bearer, onSuccess, onError);
        }

        private IEnumerator SendRequest(string path, string method, string body, string bearer,
            Action<string> onSuccess, Action<IndieableError> onError, bool logErrors = true)
        {
            var attempts = Math.Max(0, _options.MaxTransientRetries) + 1;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                using (var request = new UnityWebRequest(BuildUrl(path), method))
                {
                    if (method != UnityWebRequest.kHttpVerbGET)
                        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body ?? "{}"));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Accept", "application/json");
                    if (method != UnityWebRequest.kHttpVerbGET)
                        request.SetRequestHeader("Content-Type", "application/json");
                    if (!string.IsNullOrWhiteSpace(bearer)) request.SetRequestHeader("Authorization", "Bearer " + bearer);
                    ApplyRequestHeaders(request);
                    request.timeout = Math.Max(5, _options.RequestTimeoutSeconds);
                    yield return request.SendWebRequest();
                    var transient = request.result == UnityWebRequest.Result.ConnectionError ||
                                    request.responseCode == 408 || request.responseCode >= 500;
                    if (request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300)
                    {
                        if (onSuccess != null) onSuccess(request.downloadHandler.text);
                        yield break;
                    }
                    if (transient && attempt + 1 < attempts)
                    {
                        yield return new WaitForSecondsRealtime(0.5f * (attempt + 1));
                        continue;
                    }
                    var parsed = ParseError(request);
                    if (logErrors) Fail(onError, parsed);
                    else if (onError != null) onError(parsed);
                    yield break;
                }
            }
        }

        private IndieableError ParseError(UnityWebRequest request)
        {
            try
            {
                var envelope = JsonUtility.FromJson<ErrorEnvelope>(request.downloadHandler.text);
                if (envelope != null && envelope.error != null)
                    return new IndieableError(envelope.error.code, envelope.error.message, request.responseCode);
            }
            catch { }
            var message = request.result == UnityWebRequest.Result.ConnectionError
                ? "Indieable is unreachable. The host game can continue normally."
                : "Indieable rejected the request.";
            return new IndieableError("request_failed", message, request.responseCode);
        }

        private void ApplyRequestHeaders(UnityWebRequest request)
        {
            var headers = _options.RequestHeaders ??
                new IndieableRequestHeader[0];
            for (var index = 0; index < headers.Length; index++)
            {
                var header = headers[index];
                if (header == null ||
                    !header.TryResolve(out var name, out var value))
                    continue;
                request.SetRequestHeader(name, value);
            }
        }

        private string BuildUrl(string path) { return (_options.BaseUrl ?? "https://indieable.com").TrimEnd('/') + path; }

        private void Fail(Action<IndieableError> onError, IndieableError error)
        {
            if (_options.LogErrors) Debug.LogWarning("[Indieable] " + error);
            if (onError != null) onError(error);
        }

        private static string EscapeJson(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }
    }
}
