using JobHunterApp.Services;

namespace JobHunterApp.Tests;

public class AppliedJobsServiceTests
{
    [Fact]
    public void IsApplied_ReturnsFalseForNewJob()
    {
        var store = new AppliedJobsStore();
        Assert.False(AppliedJobsService.IsApplied("job-123", store));
    }

    [Fact]
    public void MarkApplied_AddsJobToStore()
    {
        var store = new AppliedJobsStore();
        AppliedJobsService.MarkApplied("job-123", "Engineer", "TechCorp", "linkedin", store);

        Assert.True(AppliedJobsService.IsApplied("job-123", store));
        Assert.Single(store.Jobs);
    }

    [Fact]
    public void MarkApplied_DoesNotDuplicate()
    {
        var store = new AppliedJobsStore();
        AppliedJobsService.MarkApplied("job-123", "Engineer", "TechCorp", "linkedin", store);
        AppliedJobsService.MarkApplied("job-123", "Engineer", "TechCorp", "linkedin", store);

        Assert.True(AppliedJobsService.IsApplied("job-123", store));
        Assert.Single(store.Jobs); // Should still be just one
    }

    [Fact]
    public void MarkApplied_StoresJobDetails()
    {
        var store = new AppliedJobsStore();
        AppliedJobsService.MarkApplied("job-456", "Designer", "CreativeCo", "indeed", store);

        var job = store.Jobs[0];
        Assert.Equal("job-456", job.Id);
        Assert.Equal("Designer", job.Title);
        Assert.Equal("CreativeCo", job.Company);
        Assert.Equal("indeed", job.Source);
    }

    [Fact]
    public void MarkApplied_MultipleDifferentJobs()
    {
        var store = new AppliedJobsStore();
        AppliedJobsService.MarkApplied("job-1", "Engineer", "Corp1", "linkedin", store);
        AppliedJobsService.MarkApplied("job-2", "Manager", "Corp2", "indeed", store);
        AppliedJobsService.MarkApplied("job-3", "Designer", "Corp3", "linkedin", store);

        Assert.True(AppliedJobsService.IsApplied("job-1", store));
        Assert.True(AppliedJobsService.IsApplied("job-2", store));
        Assert.True(AppliedJobsService.IsApplied("job-3", store));
        Assert.Equal(3, store.Jobs.Count);
    }
}
