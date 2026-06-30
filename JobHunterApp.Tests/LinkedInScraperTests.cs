using JobHunterApp.Models;
using JobHunterApp.Services.Scrapers;
using System.Reflection;

namespace JobHunterApp.Tests;

public class LinkedInScraperTests
{
    [Fact]
    public void BuildUrl_IncludesJobTitleAndLocation()
    {
        var config = new SearchConfig
        {
            JobTitle = "Software Engineer",
            Location = "New York",
            Keywords = "",
            LinkedInEasyApplyOnly = false
        };

        var url = InvokeBuildUrl(config);

        Assert.Contains("keywords=Software+Engineer", url);
        Assert.Contains("location=New+York", url);
    }

    [Fact]
    public void BuildUrl_IncludesKeywords()
    {
        var config = new SearchConfig
        {
            JobTitle = "Engineer",
            Location = "Remote",
            Keywords = "React TypeScript",
            LinkedInEasyApplyOnly = false
        };

        var url = InvokeBuildUrl(config);

        Assert.Contains("Engineer", url);
        Assert.Contains("React", url);
        Assert.Contains("TypeScript", url);
    }

    [Fact]
    public void BuildUrl_EasyApplyOnlyAddsFilter()
    {
        var configWith = new SearchConfig
        {
            JobTitle = "Engineer",
            Location = "Remote",
            LinkedInEasyApplyOnly = true
        };

        var configWithout = new SearchConfig
        {
            JobTitle = "Engineer",
            Location = "Remote",
            LinkedInEasyApplyOnly = false
        };

        var urlWith = InvokeBuildUrl(configWith);
        var urlWithout = InvokeBuildUrl(configWithout);

        Assert.Contains("f_LF=f_AL", urlWith);
        Assert.DoesNotContain("f_LF=f_AL", urlWithout);
    }

    [Fact]
    public void BuildUrl_ReturnsValidLinkedInJobsUrl()
    {
        var config = new SearchConfig
        {
            JobTitle = "Product Manager",
            Location = "San Francisco",
            Keywords = ""
        };

        var url = InvokeBuildUrl(config);

        Assert.StartsWith("https://www.linkedin.com/jobs/search/?", url);
    }

    // Use reflection to invoke private BuildUrl method
    private static string InvokeBuildUrl(SearchConfig config)
    {
        var method = typeof(LinkedInScraper).GetMethod("BuildUrl",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null)
            throw new InvalidOperationException("BuildUrl method not found");

        return (string)(method.Invoke(null, new object[] { config }) ?? "");
    }
}
