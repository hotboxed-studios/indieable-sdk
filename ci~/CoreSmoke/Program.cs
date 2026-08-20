using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using IndieableSdk;
using IndieableSdk.Events;

internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            await RunAsync();
            Console.WriteLine("Indieable generic C# and event-bus smoke passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static async Task RunAsync()
    {
        GlobalEventBus.Clear();

        var secondSubscriberCalled = false;
        var subscriberFailureReported = false;
        GlobalEventBus.SubscriberException += _ =>
            subscriberFailureReported = true;

        using var failing = GlobalEventBus.SubscribeAll(
            _ => throw new InvalidOperationException("expected smoke failure"));
        using var succeeding = GlobalEventBus.SubscribeAll(
            _ => secondSubscriberCalled = true);

        GlobalEventBus.Publish(
            "smoke.local",
            new SmokePayload { value = 1 });

        Assert(secondSubscriberCalled,
            "one failing subscriber stopped another subscriber");
        Assert(subscriberFailureReported,
            "subscriber failure was not surfaced");

        failing.Dispose();
        succeeding.Dispose();

        var handler = new StubHandler();
        using var httpClient = new HttpClient(handler);
        await using var client = new IndieableClient(
            new IndieableClientOptions
            {
                BaseUrl = "https://example.test",
                PublicGameKey = "ind_pub_smoke_test",
                Environment = "development",
                BuildVersion = "smoke",
                HttpClient = httpClient,
                IdentityStorage =
                    new IndieableMemoryIdentityStorage()
            });

        var manifest =
            await client.GetPrivacyManifestAsync();
        Assert(manifest.Configured,
            "public privacy manifest was not mapped");
        Assert(!client.IsConnected,
            "manifest lookup created a session");

        var session = await client.ConnectAsync();
        Assert(client.IsConnected,
            "ConnectAsync did not establish a session");
        Assert(session.IdentityState == "EPHEMERAL_SESSION",
            "session identity state was not mapped");

        var preferences =
            await client.GetPrivacyPreferencesAsync();
        Assert(preferences.IsGranted(
                IndieableClient.GameplayTelemetryPurpose),
            "privacy preference was not mapped");

        var forwarded =
            new TaskCompletionSource<IndieableEventReceipt>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var failed =
            new TaskCompletionSource<Exception>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        await using var forwarder =
            new IndieableEventBusForwarder(
                client,
                new IndieableEventBusForwarderOptions
                {
                    Mode =
                        IndieableEventForwardingMode.AllowList,
                    TestByDefault = true,
                    Routes = new[]
                    {
                        new IndieableEventForwardingRoute
                        {
                            SourceEventName =
                                "game.door.opened",
                            IndieableEventKey =
                                "door_opened",
                            Purpose =
                                IndieableEventForwardingPurpose
                                    .GameplayTelemetry
                        }
                    }
                });

        forwarder.ApplyPrivacyPreferences(preferences);
        forwarder.EventForwarded += (_, receipt) =>
            forwarded.TrySetResult(receipt);
        forwarder.EventFailed += (_, exception) =>
            failed.TrySetResult(exception);

        GlobalEventBus.Publish(
            "game.door.opened",
            new SmokePayload { value = 7 },
            new IndieableEventContext
            {
                IdempotencyKey =
                    "smoke-door-opened-0001",
                Test = true
            });

        var winner = await Task.WhenAny(
            forwarded.Task,
            failed.Task,
            Task.Delay(TimeSpan.FromSeconds(5)));

        if (winner == failed.Task)
            throw new InvalidOperationException(
                "event-bus forwarding failed",
                await failed.Task);
        if (winner != forwarded.Task)
            throw new TimeoutException(
                "event-bus forwarding did not complete");

        var receipt = await forwarded.Task;
        Assert(receipt.Accepted,
            "forwarded event was not accepted");
        Assert(handler.EventRequestBody != null,
            "event request was not captured");

        using var document =
            JsonDocument.Parse(handler.EventRequestBody!);
        var root = document.RootElement;
        Assert(
            root.GetProperty("event_key").GetString() ==
                "door_opened",
            "event route did not rename the bus event");
        Assert(
            root.GetProperty("payload")
                .GetProperty("value")
                .GetInt32() == 7,
            "typed event payload was not serialized");
        Assert(root.GetProperty("test").GetBoolean(),
            "test default/context was not forwarded");

        GlobalEventBus.Clear();
    }

    private static void Assert(
        bool condition,
        string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class SmokePayload
    {
        public int value { get; set; }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public string? EventRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            if (path == "/api/connect/v1/events")
            {
                EventRequestBody = request.Content == null
                    ? null
                    : await request.Content.ReadAsStringAsync(
                        cancellationToken);
                return Json(
                    """
                    {
                      "accepted": true,
                      "duplicate": false,
                      "test": true,
                      "event_key": "door_opened",
                      "event_id": "evt_smoke",
                      "schema_version": 1,
                      "processing_purpose": "GAMEPLAY_TELEMETRY"
                    }
                    """);
            }

            if (path ==
                "/api/connect/v1/privacy/manifest")
            {
                return Json(
                    """
                    {
                      "configured": true,
                      "game_title": "Smoke Game",
                      "manifest_version": 1,
                      "notice_version": "smoke-1",
                      "audience_classification": "general_audience",
                      "published_at": "2026-08-20T00:00:00Z",
                      "controller": {
                        "name": "Smoke Studio",
                        "contact": "privacy@example.test",
                        "privacy_policy_url": "https://example.test/privacy"
                      },
                      "purposes": []
                    }
                    """);
            }

            if (path ==
                "/api/connect/v1/session")
            {
                return Json(
                    """
                    {
                      "session_token": "ind_sess_smoke_token",
                      "session_type": "anonymous",
                      "identity_state": "EPHEMERAL_SESSION",
                      "persistent_identity": false,
                      "public_player_ref": "",
                      "expires_at": "2026-08-21T00:00:00Z",
                      "steam_ticket_identity": "indieable",
                      "game": { "title": "Smoke Game" }
                    }
                    """);
            }

            if (path ==
                "/api/connect/v1/privacy/preferences")
            {
                return Json(
                    """
                    {
                      "persistent_identity": true,
                      "identity_state": "ANONYMOUS_INSTALL",
                      "public_player_ref": "gp_smoke",
                      "manifest": {
                        "version": 1,
                        "notice_version": "smoke-1",
                        "controller_name": "Smoke Studio",
                        "controller_contact": "privacy@example.test",
                        "privacy_policy_url": "https://example.test/privacy",
                        "audience_classification": "general_audience",
                        "published_at": "2026-08-20T00:00:00Z",
                        "purposes": []
                      },
                      "permissions": [
                        {
                          "purpose_key": "GAMEPLAY_TELEMETRY",
                          "state": "GRANTED",
                          "effective_at": "2026-08-20T00:00:00Z",
                          "requires_affirmative_permission": true
                        }
                      ]
                    }
                    """);
            }

            return new HttpResponseMessage(
                HttpStatusCode.NotFound)
            {
                Content = new StringContent(
                    "{\"error\":{\"code\":\"not_found\",\"message\":\"not found\"}}",
                    Encoding.UTF8,
                    "application/json")
            };
        }

        private static HttpResponseMessage Json(
            string value)
        {
            return new HttpResponseMessage(
                HttpStatusCode.OK)
            {
                Content = new StringContent(
                    value,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
