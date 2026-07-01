using JobHunterApp.Services;
using Xunit;

namespace JobHunterApp.Tests;

public class CvVerificationServiceTests
{
    [Fact]
    public void VerifyTailoredCv_ValidCv_ReturnsTrue()
    {
        var original = """
            Senior Software Engineer at Acme Corp (2020-2022)
            - Built REST APIs using Python and Django
            - Led migration to Kubernetes
            Skills: Python, Django, Kubernetes, AWS, PostgreSQL
            """;

        var tailored = """
            Senior Software Engineer at Acme Corp
            - Architected REST APIs with Python and Django
            - Led containerized infrastructure migration using Kubernetes
            Skills: Python, Django, Kubernetes, AWS
            """;

        var (valid, issues) = CvVerificationService.VerifyTailoredCv(original, tailored);

        Assert.True(valid);
        Assert.Empty(issues);
    }

    [Fact]
    public void VerifyTailoredCv_HallucinatedSkill_ReturnsFalseWithIssues()
    {
        var original = """
            Python Developer
            Skills: Python, SQL, Linux
            """;

        var tailored = """
            Python Developer
            - Developed microservices in Rust
            Skills: Python, SQL, Linux, Rust
            """;

        var (valid, issues) = CvVerificationService.VerifyTailoredCv(original, tailored);

        Assert.False(valid);
        Assert.Single(issues);
        Assert.Contains("Hallucinated skill: 'rust'", issues[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyTailoredCv_HallucinatedRole_ReturnsFalseWithIssues()
    {
        var original = """
            Software Engineer at TechCorp (2021-2023)
            Responsibilities: maintenance and bug fixes
            """;

        var tailored = """
            Principal Architect at TechCorp
            - Led architectural decisions for microservices
            """;

        var (valid, issues) = CvVerificationService.VerifyTailoredCv(original, tailored);

        Assert.False(valid);
        Assert.Single(issues);
        Assert.Contains("Principal", issues[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyTailoredCv_HallucinatedCompany_ReturnsFalseWithIssues()
    {
        var original = """
            Worked at StartupXYZ (2019-2020)
            Worked at TechCorp (2021-2023)
            """;

        var tailored = """
            Worked at StartupXYZ
            Worked at BigFAANG (2020-2021)
            Worked at TechCorp
            """;

        var (valid, issues) = CvVerificationService.VerifyTailoredCv(original, tailored);

        Assert.False(valid);
        Assert.Single(issues);
        Assert.Contains("BigFAANG", issues[0]);
    }

    [Fact]
    public void VerifyTailoredCv_MultipleHallucinations_ReturnsAllIssues()
    {
        var original = """
            Java Developer at StartupA
            Skills: Java, SQL
            """;

        var tailored = """
            Go Architect at StartupA
            - Designed systems in Rust
            Skills: Java, SQL, Go, Rust, Kubernetes
            """;

        var (valid, issues) = CvVerificationService.VerifyTailoredCv(original, tailored);

        Assert.False(valid);
        Assert.True(issues.Count >= 2, $"Expected at least 2 issues, got {issues.Count}");
    }
}
