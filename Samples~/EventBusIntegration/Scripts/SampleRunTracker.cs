using System;
using IndieableSdk.Events;
using UnityEngine;

namespace IndieableSdk.Samples.EventBus
{
    /// <summary>
    /// Tiny fake run system used by the imported sample scene.
    /// It knows only the local GlobalEventBus, not the Indieable network SDK.
    /// </summary>
    public sealed class SampleRunTracker : MonoBehaviour
    {
        [SerializeField] private int startingFloor = 1;
        [SerializeField] private int players = 1;

        private DateTime _startedAtUtc;
        private int _runNumber;
        private int _floor;
        private int _deaths;
        private string _runId = "";

        public int RunNumber { get { return _runNumber; } }
        public int Floor { get { return _floor; } }
        public int Deaths { get { return _deaths; } }
        public string RunId { get { return _runId; } }

        private void Awake()
        {
            BeginRun();
        }

        public void BeginRun()
        {
            _runNumber++;
            _floor = Math.Max(0, startingFloor);
            _deaths = 0;
            _startedAtUtc = DateTime.UtcNow;
            _runId = "sample-run-" + Guid.NewGuid().ToString("N");
        }

        public void AddFloor()
        {
            _floor++;
        }

        public void RecordDeath()
        {
            _deaths++;
        }

        public void CompleteRun()
        {
            var elapsed = DateTime.UtcNow - _startedAtUtc;
            var payload = new RunCompletedEvent
            {
                floor = Math.Max(0, _floor),
                time_ms = Math.Max(0, (int)elapsed.TotalMilliseconds),
                deaths = Math.Max(0, _deaths),
                players = Math.Max(1, players)
            };

            GlobalEventBus.Publish(
                SampleEventNames.RunCompleted,
                payload,
                NewContext("run-completed"));

            BeginRun();
        }

        public IndieableEventContext NewContext(string operation)
        {
            return new IndieableEventContext
            {
                OccurredAtUtc = DateTime.UtcNow,
                IdempotencyKey = string.Format(
                    "sample-{0}-{1}-{2}",
                    operation,
                    _runNumber,
                    Guid.NewGuid().ToString("N"))
            };
        }
    }
}
