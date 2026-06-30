namespace JobHunterApp.Services;

public class RateLimiter(int requestsPerMinute)
{
    private readonly List<long> _timestamps = [];
    private readonly int _max = requestsPerMinute;
    private const long WindowMs = 60_000;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task ThrottleAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            while (true)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _timestamps.RemoveAll(t => now - t >= WindowMs);

                if (_timestamps.Count < _max)
                {
                    _timestamps.Add(now);
                    break;
                }

                var oldest = _timestamps[0];
                var waitMs = (int)(WindowMs - (now - oldest) + 100);
                await Task.Delay(waitMs);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
