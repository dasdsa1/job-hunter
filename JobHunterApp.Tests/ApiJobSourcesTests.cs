using JobHunterApp.Models;
using JobHunterApp.Services.Sources;

namespace JobHunterApp.Tests;

public class ApiJobSourcesTests
{
    private static SearchConfig Cfg(string title = "engineer", int max = 20) =>
        new() { JobTitle = title, Keywords = "", Location = "Remote", MaxJobsPerSite = max };

    [Fact]
    public void Remotive_Parse_MapsFields()
    {
        const string json = """
        { "jobs": [
            { "id": 123, "url": "https://remotive.com/x", "title": "Senior Engineer",
              "company_name": "Acme", "candidate_required_location": "Worldwide",
              "description": "<p>Build <b>things</b></p>", "publication_date": "2024-01-01",
              "salary": "$100k" }
        ] }
        """;
        var jobs = RemotiveSource.Parse(json, Cfg());
        var j = Assert.Single(jobs);
        Assert.Equal("remotive-123", j.Id);
        Assert.Equal("Senior Engineer", j.Title);
        Assert.Equal("Acme", j.Company);
        Assert.Equal("remotive", j.Source);
        Assert.DoesNotContain("<", j.Description);          // HTML stripped
        Assert.Contains("Build things", j.Description);
    }

    [Fact]
    public void RemoteOk_Parse_SkipsMetadataAndFiltersKeywords()
    {
        const string json = """
        [
          { "legal": "notice element, no position" },
          { "id": "1", "position": "Backend Engineer", "company": "Foo",
            "location": "Remote", "description": "Go and Rust", "url": "https://x" },
          { "id": "2", "position": "Sales Rep", "company": "Bar",
            "location": "NY", "description": "cold calling", "url": "https://y" }
        ]
        """;
        // Keyword "engineer" should keep the first, drop the sales role.
        var jobs = RemoteOkSource.Parse(json, Cfg("engineer"));
        var j = Assert.Single(jobs);
        Assert.Equal("Backend Engineer", j.Title);
        Assert.Equal("remoteok-1", j.Id);
    }

    [Fact]
    public void Arbeitnow_Parse_ReadsDataArray()
    {
        const string json = """
        { "data": [
            { "slug": "abc", "title": "Platform Engineer", "company_name": "Globex",
              "location": "Berlin", "description": "<div>k8s</div>", "url": "https://z",
              "created_at": 1700000000 }
        ], "links": {}, "meta": {} }
        """;
        var j = Assert.Single(ArbeitnowSource.Parse(json, Cfg("engineer")));
        Assert.Equal("arbeitnow-abc", j.Id);
        Assert.Equal("Platform Engineer", j.Title);
        Assert.Equal("k8s", j.Description);
    }

    [Fact]
    public void Adzuna_Parse_FlattensNestedObjects()
    {
        const string json = """
        { "results": [
            { "id": "999", "title": "Data Engineer",
              "company": { "display_name": "Initech" },
              "location": { "display_name": "Remote, US" },
              "description": "ETL pipelines", "redirect_url": "https://a",
              "created": "2024-02-02", "salary_min": 90000, "salary_max": 120000 }
        ] }
        """;
        var j = Assert.Single(AdzunaSource.Parse(json, Cfg()));
        Assert.Equal("adzuna-999", j.Id);
        Assert.Equal("Initech", j.Company);
        Assert.Equal("Remote, US", j.Location);
        Assert.NotNull(j.Salary);
    }

    [Fact]
    public void Dedup_RemovesCrossSourceTitleCompanyRepost()
    {
        var jobs = new[]
        {
            new JobListing { Id = "remotive-1", Title = "DevOps Engineer", Company = "Acme" },
            new JobListing { Id = "remoteok-9", Title = "DevOps  Engineer!", Company = "ACME" }, // same after norm
            new JobListing { Id = "adzuna-3",   Title = "QA Tester",        Company = "Beta" },
        };
        var deduped = ApiJobSources.Dedup(jobs);
        Assert.Equal(2, deduped.Count);
    }

    [Fact]
    public void Parse_RespectsMaxJobsPerSite()
    {
        var items = string.Join(",", Enumerable.Range(0, 50).Select(i =>
            $$"""{ "id": {{i}}, "title": "Engineer {{i}}", "company_name": "C", "description": "d", "url": "u" }"""));
        var jobs = RemotiveSource.Parse($$"""{ "jobs": [ {{items}} ] }""", Cfg(max: 10));
        Assert.Equal(10, jobs.Count);
    }
}
