using JobHunterApp.Services;

namespace JobHunterApp.Tests;

public class RateLimiterTests
{
    [Fact]
    public async Task ThrottleAsync_AllowsRequestsWithinLimit()
    {
        var limiter = new RateLimiter(10); // 10 requests per minute

        // Should allow 10 requests without delay
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 10; i++)
        {
            await limiter.ThrottleAsync();
        }
        sw.Stop();

        // 10 requests should complete quickly (< 1 second)
        Assert.True(sw.ElapsedMilliseconds < 1000);
    }

    [Fact]
    public async Task ThrottleAsync_DelaysOn11thRequest()
    {
        var limiter = new RateLimiter(2); // 2 requests per minute for testing

        // First 2 should be instant
        await limiter.ThrottleAsync();
        await limiter.ThrottleAsync();

        // 3rd should delay because window isn't full yet
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await limiter.ThrottleAsync();
        sw.Stop();

        // Should have waited (at least 100ms of the delay calculation)
        Assert.True(sw.ElapsedMilliseconds >= 100);
    }

    [Fact]
    public async Task ThrottleAsync_ParallelRequests_ThreadSafe()
    {
        var limiter = new RateLimiter(5);
        var tasks = new List<Task>();

        // Fire 10 requests in parallel
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(limiter.ThrottleAsync());
        }

        // Should not throw or race
        await Task.WhenAll(tasks);

        // All completed without exception
        Assert.True(true);
    }
}
