namespace JobHunterApp.Services;

public class RateLimiter(int requestsPerMinute)
{
    private readonly List<long> _timestamps = [];
    private readonly int _max = requestsPerMinute;
    private const long WindowMs = 60_000;

    public async Task ThrottleAsync()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _timestamps.RemoveAll(t => now - t >= WindowMs);

        if (_timestamps.Count >= _max)
        {
            var oldest = _timestamps[0];
            var waitMs = (int)(WindowMs - (now - oldest) + 100);
            await Task.Delay(waitMs);
            await ThrottleAsync();
            return;
        }

        _timestamps.Add(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }
}
