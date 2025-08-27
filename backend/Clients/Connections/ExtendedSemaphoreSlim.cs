using System.Threading;
using System.Threading.Tasks;

namespace NzbWebDAV.Clients.Connections;

/// <summary>
/// A semaphore that supports "reserved" slots.  A waiter is only granted a
/// permit when, after the acquisition, at least <c>reserved</c> slots would
/// remain available.  This allows callers to hold back a number of slots for
/// other operations.
/// </summary>
internal sealed class ExtendedSemaphoreSlim : IDisposable
{
    private readonly object _lock = new();
    private readonly int _max;
    private int _current;
    private TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ExtendedSemaphoreSlim(int initialCount, int maxCount)
    {
        if (initialCount < 0 || maxCount <= 0 || initialCount > maxCount)
            throw new ArgumentOutOfRangeException(nameof(initialCount));
        _current = initialCount;
        _max = maxCount;
    }

    /// <summary>Waits asynchronously until a permit is available while ensuring
    /// that the specified number of slots remain free after acquisition.</summary>
    public async Task WaitAsync(int reserved, CancellationToken cancellationToken = default)
    {
        if (reserved < 0)
            throw new ArgumentOutOfRangeException(nameof(reserved));

        while (true)
        {
            Task waitTask;
            lock (_lock)
            {
                if (_current > reserved)
                {
                    _current--;
                    return;
                }
                waitTask = _tcs.Task;
            }
            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Releases a permit, waking any waiters.</summary>
    public void Release()
    {
        TaskCompletionSource<bool>? toRelease;
        lock (_lock)
        {
            if (_current == _max)
                throw new SemaphoreFullException();
            _current++;
            toRelease = _tcs;
            _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        toRelease.TrySetResult(true);
    }

    public void Dispose()
    {
        // Nothing to dispose except for the TaskCompletionSource's task which
        // does not hold unmanaged resources. Provided for API symmetry.
    }
}
