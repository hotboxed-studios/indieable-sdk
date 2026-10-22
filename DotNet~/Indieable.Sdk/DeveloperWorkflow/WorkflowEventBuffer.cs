using System.Threading.Channels;

namespace IndieableSdk.DeveloperWorkflow;

/// <summary>
/// Optional process-local event delivery with a bounded queue. TryEnqueue never waits for the network.
/// This buffer creates no identity, persists nothing, and drops pending events on disposal.
/// </summary>
public sealed class WorkflowEventBuffer : IAsyncDisposable
{
    private readonly Channel<Envelope> _queue;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _worker;
    private readonly Func<IEnumerable<WorkflowEvent>, Guid, CancellationToken, Task<WorkflowDelivery<WorkflowEventReceipt>>> _send;
    private int _disposed;
    private int _cleanup;

    /// <summary>Optional notification with no automatic logging. Subscriber failures cannot stop delivery.</summary>
    public event Action<WorkflowDelivery<WorkflowEventReceipt>>? DeliveryCompleted;

    internal WorkflowEventBuffer(
        Func<IEnumerable<WorkflowEvent>, Guid, CancellationToken, Task<WorkflowDelivery<WorkflowEventReceipt>>> send,
        int capacity)
    {
        if (capacity is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be between one and 1024 events.");
        _send = send;
        _queue = Channel.CreateBounded<Envelope>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _worker = Task.Run(DeliverAsync);
    }

    /// <summary>Returns false when the queue is full or disposed. Invalid event arguments still report a programming error.</summary>
    public bool TryEnqueue(WorkflowEvent observation)
    {
        if (Volatile.Read(ref _disposed) != 0) return false;
        WorkflowValidation.Events(new[] { observation });
        return _queue.Writer.TryWrite(new(observation, Guid.NewGuid()));
    }

    private async Task DeliverAsync()
    {
        try
        {
            await foreach (var envelope in _queue.Reader.ReadAllAsync(_shutdown.Token).ConfigureAwait(false))
            {
                var result = await _send(new[] { envelope.Event }, envelope.RequestId, _shutdown.Token).ConfigureAwait(false);
                if (DeliveryCompleted is { } callbacks)
                    foreach (Action<WorkflowDelivery<WorkflowEventReceipt>> callback in callbacks.GetInvocationList())
                        try { callback(result); } catch { /* Analytics observers cannot disrupt the host game. */ }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _queue.Writer.TryComplete();
            _shutdown.Cancel();
        }
        await _worker.ConfigureAwait(false);
        if (Interlocked.Exchange(ref _cleanup, 1) == 0) _shutdown.Dispose();
    }

    private sealed record Envelope(WorkflowEvent Event, Guid RequestId);
}
