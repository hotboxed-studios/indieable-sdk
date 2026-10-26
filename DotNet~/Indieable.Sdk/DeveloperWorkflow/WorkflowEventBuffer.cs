namespace IndieableSdk.DeveloperWorkflow;

/// <summary>
/// Bounded, session-scoped optional delivery. Gameplay callbacks never wait for the network.
/// Retryable failure pauses until the host explicitly retries; the SDK creates no files or persistent identity.
/// </summary>
public sealed class WorkflowEventBuffer : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly Queue<Envelope> _pending = new();
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _worker;
    private readonly int _capacity;
    private readonly Func<IEnumerable<WorkflowEvent>, Guid, CancellationToken, Task<WorkflowDelivery<WorkflowEventReceipt>>> _send;
    private bool _paused, _invalidated, _inFlight, _disposed;
    private int _cleanup;
    private DateTimeOffset? _retryAt;

    /// <summary>Optional notification without logging. Subscriber failures cannot stop delivery.</summary>
    public event Action<WorkflowDelivery<WorkflowEventReceipt>>? DeliveryCompleted;
    public int PendingCount { get { lock (_sync) return _pending.Count; } }
    public bool IsPaused { get { lock (_sync) return _paused; } }
    public bool CollectionInvalidated { get { lock (_sync) return _invalidated; } }
    public DateTimeOffset? RetryAt { get { lock (_sync) return _retryAt; } }

    internal WorkflowEventBuffer(
        Func<IEnumerable<WorkflowEvent>, Guid, CancellationToken, Task<WorkflowDelivery<WorkflowEventReceipt>>> send,
        int capacity)
    {
        if (capacity is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be between one and 1024 events, including an in-flight event.");
        _send = send; _capacity = capacity;
        _worker = Task.Run(DeliverAsync);
    }

    /// <summary>Returns false when full, disposed, invalidated, or already pending. Invalid arguments remain programming errors.</summary>
    public bool TryEnqueue(WorkflowEvent observation)
    {
        WorkflowValidation.Events(new[] { observation });
        lock (_sync)
        {
            if (_disposed || _invalidated || _pending.Count >= _capacity || _pending.Any(item => item.Event.EventId == observation.EventId)) return false;
            _pending.Enqueue(new(observation, Guid.NewGuid()));
            if (!_paused) Wake();
            return true;
        }
    }

    /// <summary>Retry the unchanged pending operation explicitly. Returns false before the server's minimum delay or after invalidation.</summary>
    public bool RetryPending()
    {
        lock (_sync)
        {
            if (_disposed || _invalidated || _pending.Count == 0 || _inFlight || _retryAt > DateTimeOffset.UtcNow) return false;
            _paused = false; _retryAt = null; Wake(); return true;
        }
    }

    /// <summary>Call on logout, gameplay withdrawal or known access invalidation. Already committed requests cannot be recalled.</summary>
    public void InvalidateCollection()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _invalidated = true; _pending.Clear(); _paused = false; _retryAt = null;
            _shutdown.Cancel();
        }
    }

    private void Wake()
    {
        try { _wake.Release(); } catch (SemaphoreFullException) { }
    }

    private async Task DeliverAsync()
    {
        try
        {
            while (true)
            {
                await _wake.WaitAsync(_shutdown.Token).ConfigureAwait(false);
                while (true)
                {
                    Envelope envelope;
                    lock (_sync)
                    {
                        if (_disposed || _invalidated || _paused || _pending.Count == 0) break;
                        envelope = _pending.Peek(); _inFlight = true;
                    }
                    var result = await _send(new[] { envelope.Event }, envelope.RequestId, _shutdown.Token).ConfigureAwait(false);
                    lock (_sync)
                    {
                        _inFlight = false;
                        if (_invalidated || _disposed) break;
                        if (result.Error?.InvalidatesCollectionContext == true)
                        {
                            _invalidated = true; _pending.Clear(); _retryAt = null;
                        }
                        else if (result.Succeeded || result.Error?.Retryable != true)
                            _pending.Dequeue();
                        else
                        {
                            _paused = true;
                            _retryAt = result.Error.RetryAt ?? DateTimeOffset.UtcNow.AddSeconds(1);
                        }
                    }
                    if (DeliveryCompleted is { } callbacks)
                        foreach (Action<WorkflowDelivery<WorkflowEventReceipt>> callback in callbacks.GetInvocationList())
                            try { callback(result); } catch { /* Observers cannot disrupt delivery. */ }
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        finally { lock (_sync) _inFlight = false; }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (!_disposed) { _disposed = true; _pending.Clear(); _shutdown.Cancel(); }
        }
        await _worker.ConfigureAwait(false);
        if (Interlocked.Exchange(ref _cleanup, 1) == 0) { _shutdown.Dispose(); _wake.Dispose(); }
    }

    private sealed record Envelope(WorkflowEvent Event, Guid RequestId);
}
