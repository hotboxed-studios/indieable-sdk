using System;
using IndieableSdk.Events;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieableSdk.Tests
{
    public sealed class IndieableEventContextTests
    {
        [Test]
        public void EventRequestBody_CarriesUnityCorrelationOptions()
        {
            var occurredAt = new DateTime(
                2026,
                8,
                23,
                12,
                34,
                56,
                DateTimeKind.Utc);
            var options = new IndieableEventOptions
            {
                Test = true,
                SchemaVersion = 1,
                OccurredAtUtc = occurredAt,
                TraceType = "multiplayer_run",
                TraceId = "trace-42",
                RunId = "run-42"
            };

            string body = IndieableClient.BuildEventRequestBody(
                "game.run.ended",
                "{\"reason\":\"completed\"}",
                "run-42-ended",
                options);

            Assert.That(body, Does.Contain("\"test\":true"));
            Assert.That(body, Does.Contain("\"schema_version\":1"));
            Assert.That(body, Does.Contain("\"occurred_at\":\"2026-08-23T12:34:56.0000000Z\""));
            Assert.That(body, Does.Contain("\"trace_type\":\"multiplayer_run\""));
            Assert.That(body, Does.Contain("\"trace_id\":\"trace-42\""));
            Assert.That(body, Does.Contain("\"run_id\":\"run-42\""));
        }

        [Test]
        public void EventContextClone_PreservesCorrelationWithoutAliasing()
        {
            var source = new IndieableEventContext
            {
                IdempotencyKey = "run-42-node-3",
                SchemaVersion = 1,
                TraceType = "multiplayer_run",
                TraceId = "trace-42",
                RunId = "run-42",
                Test = true
            };

            IndieableEventContext clone = source.Clone();

            Assert.That(clone, Is.Not.SameAs(source));
            Assert.That(clone.IdempotencyKey, Is.EqualTo(source.IdempotencyKey));
            Assert.That(clone.SchemaVersion, Is.EqualTo(1));
            Assert.That(clone.TraceType, Is.EqualTo("multiplayer_run"));
            Assert.That(clone.TraceId, Is.EqualTo("trace-42"));
            Assert.That(clone.RunId, Is.EqualTo("run-42"));
            Assert.That(clone.Test, Is.True);
        }

        [Test]
        public void ProjectSettings_DefaultToPreviewAndFailClosedWithoutKey()
        {
            IndieableProjectSettings settings =
                ScriptableObject.CreateInstance<IndieableProjectSettings>();
            try
            {
                Assert.That(
                    settings.BaseUrl,
                    Is.EqualTo("https://preview.indieable.com"));
                Assert.That(settings.Environment, Is.EqualTo("development"));
                Assert.That(settings.AutoInitialize, Is.True);
                Assert.That(settings.ShowStartupConsent, Is.True);
                Assert.That(settings.IsConfigured, Is.False);
                Assert.That(settings.CreateOptions().PublicGameKey, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void RequestHeader_ResolvesConfiguredEnvironmentVariable()
        {
            const string testValue = "local-header-test";
            const string variable = "INDIEABLE_TEST_HEADER";
            string previous = Environment.GetEnvironmentVariable(variable);
            try
            {
                Environment.SetEnvironmentVariable(variable, testValue);
                var header = new IndieableRequestHeader
                {
                    Name = "x-indieable-test",
                    ValueEnvironmentVariable = variable
                };

                Assert.That(header.Value, Is.Empty);
                Assert.That(
                    header.ValueEnvironmentVariable,
                    Is.EqualTo(variable));
                Assert.That(
                    header.TryResolve(out string name, out string value),
                    Is.True);
                Assert.That(
                    name,
                    Is.EqualTo("x-indieable-test"));
                Assert.That(value, Is.EqualTo(testValue));
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, previous);
            }
        }

        [Test]
        public void RequestHeader_RejectsSdkOwnedAndNewlineValues()
        {
            var owned = new IndieableRequestHeader
            {
                Name = "Authorization",
                Value = "not-allowed"
            };
            var newline = new IndieableRequestHeader
            {
                Name = "x-test",
                Value = "first\nsecond"
            };

            Assert.That(owned.TryValidate(out _), Is.False);
            Assert.That(newline.TryValidate(out _), Is.False);
        }

        [Test]
        public void StartupConsent_StoresExplicitDecisionByNoticeVersion()
        {
            IndieableProjectSettings settings =
                ScriptableObject.CreateInstance<IndieableProjectSettings>();
            const string notice = "test-notice-v1";
            try
            {
                var serialized = new SerializedObject(settings);
                serialized.FindProperty("publicGameKey").stringValue =
                    "ind_pub_test";
                serialized.FindProperty("environment").stringValue =
                    "development";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                string key = IndieableStartupConsent.BuildDecisionKey(
                    settings,
                    notice);
                PlayerPrefs.DeleteKey(key + ".saved");
                PlayerPrefs.DeleteKey(key + ".telemetry");
                PlayerPrefs.DeleteKey(key + ".diagnostics");

                Assert.That(
                    IndieableStartupConsent.TryGetDecision(
                        settings,
                        notice,
                        out _,
                        out _),
                    Is.False);

                IndieableStartupConsent.RecordDecision(
                    settings,
                    notice,
                    true,
                    false);

                Assert.That(
                    IndieableStartupConsent.TryGetDecision(
                        settings,
                        notice,
                        out bool telemetry,
                        out bool diagnostics),
                    Is.True);
                Assert.That(telemetry, Is.True);
                Assert.That(diagnostics, Is.False);
                Assert.That(
                    IndieableStartupConsent.TryGetDecision(
                        settings,
                        "test-notice-v2",
                        out _,
                        out _),
                    Is.False);
            }
            finally
            {
                string key = IndieableStartupConsent.BuildDecisionKey(
                    settings,
                    notice);
                PlayerPrefs.DeleteKey(key + ".saved");
                PlayerPrefs.DeleteKey(key + ".telemetry");
                PlayerPrefs.DeleteKey(key + ".diagnostics");
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void AutomaticConsent_IsSuppressedForNonInteractiveProcess()
        {
            const string variable = "UNITY_NON_INTERACTIVE";
            string previous = Environment.GetEnvironmentVariable(variable);
            try
            {
                Environment.SetEnvironmentVariable(variable, "1");
                Assert.That(
                    IndieableStartupConsent.ShouldSuppressAutomaticUi(),
                    Is.True);
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, previous);
            }
        }

        [Test]
        public void PrivacyUiToolkitResources_ArePackaged()
        {
            Assert.That(
                Resources.Load<VisualTreeAsset>(
                    "IndieablePrivacyPreferences"),
                Is.Not.Null);
            Assert.That(
                Resources.Load<StyleSheet>(
                    "IndieablePrivacyPreferences"),
                Is.Not.Null);
        }
    }
}
