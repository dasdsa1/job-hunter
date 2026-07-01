using System.Net;
using JobHunterApp.Models;
using JobHunterApp.Services;
using PlatformSchemas;

namespace JobHunterApp.Tests;

public class HubClientServiceTests
{
    private static Curriculum SampleCurriculum() => new()
    {
        SchemaVersion = "1.0.0",
        Basics = new { name = "Jane Dev" },
        SourceText = "resume text",
    };

    [Fact]
    public async Task PostCurriculumAsync_NoHubBaseUrl_ReturnsFalseWithoutSendingRequest()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var client = new HubClientService(new HttpClient(handler));
        var config = new AppConfig { HubBaseUrl = "" };

        var result = await client.PostCurriculumAsync(config, SampleCurriculum());

        Assert.False(result);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task PostCurriculumAsync_Success_PostsToV1CurriculaWithAuthAndVersionHeaders()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created);
        var client = new HubClientService(new HttpClient(handler));
        var config = new AppConfig { HubBaseUrl = "http://localhost:5000/", HubToken = "cbif_pat_test" };

        var result = await client.PostCurriculumAsync(config, SampleCurriculum());

        Assert.True(result);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("http://localhost:5000/v1/curricula", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal("cbif_pat_test", handler.LastRequest.Headers.Authorization?.Parameter);
        Assert.Equal("1", handler.LastRequest.Headers.GetValues("X-CBIF-Version").First());
    }

    [Fact]
    public async Task PostCurriculumAsync_ServerError_ReturnsFalse()
    {
        var handler = new RecordingHandler(HttpStatusCode.InternalServerError);
        var client = new HubClientService(new HttpClient(handler));
        var config = new AppConfig { HubBaseUrl = "http://localhost:5000" };

        var result = await client.PostCurriculumAsync(config, SampleCurriculum());

        Assert.False(result);
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
