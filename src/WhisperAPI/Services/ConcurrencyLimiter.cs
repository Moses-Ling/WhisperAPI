using System.Collections.Concurrent;

namespace WhisperAPI.Services;

public sealed class ConcurrencyLimiter
{
    private readonly SemaphoreSlim _semaphore;
    public int MaxConcurrent { get; }

    public ConcurrencyLimiter(int maxConcurrent)
    {
        MaxConcurrent = Math.Max(1, maxConcurrent);
        _semaphore = new SemaphoreSlim(MaxConcurrent, MaxConcurrent);
    }

    public async Task<bool> TryEnterAsync(TimeSpan timeout, CancellationToken ct)
    {
        return await _semaphore.WaitAsync(timeout, ct);
    }

    public void Release()
    {
        _semaphore.Release();
    }
}

