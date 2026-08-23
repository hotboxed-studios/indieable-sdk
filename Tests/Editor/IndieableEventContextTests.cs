using System;
using IndieableSdk.Events;
using NUnit.Framework;
using UnityEngine;

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
                Assert.That(settings.IsConfigured, Is.False);
                Assert.That(settings.CreateOptions().PublicGameKey, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    }
}
