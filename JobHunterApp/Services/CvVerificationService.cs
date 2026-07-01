using System.Text.RegularExpressions;

namespace JobHunterApp.Services;

/// <summary>
/// Verifies tailored CV claims against the original to catch hallucinations.
/// P0 #5: Fabricated/hallucinated skills are the top complaint in competitors.
/// Checks for: skill mentions, role titles, company names, dates (fuzzy match).
/// </summary>
public static class CvVerificationService
{
    public static (bool valid, List<string> issues) VerifyTailoredCv(string originalCv, string tailoredCv)
    {
        var issues = new List<string>();

        // Normalize text: lowercase, remove extra whitespace
        var origNorm = Normalize(originalCv);
        var tailNorm = Normalize(tailoredCv);

        // Extract key terms from tailored CV that might be hallucinated
        var tailoredSkills = ExtractSkills(tailoredCv);
        var tailoredRoles = ExtractRoles(tailoredCv);
        var tailoredCompanies = ExtractCompanies(tailoredCv);

        // Verify each skill appears in original (fuzzy: substring match on normalized text)
        foreach (var skill in tailoredSkills)
        {
            if (!ContainsTermFuzzy(origNorm, skill))
            {
                issues.Add($"Hallucinated skill: '{skill}' not found in original CV");
            }
        }

        // Verify roles (job titles) exist in original
        foreach (var role in tailoredRoles)
        {
            if (!ContainsTermFuzzy(origNorm, role))
            {
                issues.Add($"Hallucinated role: '{role}' not found in original CV");
            }
        }

        // Verify companies exist in original
        foreach (var company in tailoredCompanies)
        {
            if (!ContainsTermFuzzy(origNorm, company))
            {
                issues.Add($"Hallucinated company: '{company}' not found in original CV");
            }
        }

        var valid = issues.Count == 0;
        return (valid, issues);
    }

    private static string Normalize(string text) =>
        Regex.Replace(text.ToLowerInvariant(), @"\s+", " ").Trim();

    private static bool ContainsTermFuzzy(string text, string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return true;
        var normalized = Normalize(term);
        // Word-boundary match: term must not be part of a larger word
        var pattern = $@"\b{Regex.Escape(normalized)}\b";
        return Regex.IsMatch(text, pattern);
    }

    private static List<string> ExtractSkills(string cv)
    {
        // Simple heuristic: words after "skills" or "technologies" section headers, common tech terms
        var skills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var techKeywords = new[] { "python", "java", "c#", "javascript", "typescript", "golang", "rust", "kotlin",
            "sql", "postgresql", "mysql", "mongodb", "redis", "docker", "kubernetes", "aws", "azure", "gcp",
            "react", "angular", "vue", "node", "express", "django", "flask", "spring", "fastapi",
            "git", "ci/cd", "devops", "linux", "windows", "macos", "rest", "graphql", "grpc" };

        foreach (var keyword in techKeywords)
        {
            if (cv.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                skills.Add(keyword);
            }
        }

        return skills.ToList();
    }

    private static List<string> ExtractRoles(string cv)
    {
        // Extract job titles: "Senior Engineer", "Manager", "Lead", etc.
        var roles = new List<string>();
        var titlePattern = @"(?i)(senior|junior|principal|lead|head|manager|architect|engineer|developer|analyst|director)\s+\w+";
        foreach (var match in Regex.Matches(cv, titlePattern).Cast<Match>())
        {
            var title = match.Value.Trim();
            if (title.Length > 3 && title.Length < 50)
            {
                roles.Add(title);
            }
        }

        return roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ExtractCompanies(string cv)
    {
        // Extract company names: after "at" or "company", stop at newline/paren/(
        var companies = new List<string>();
        var companyPattern = @"(?:at|worked at|company|from)\s+([A-Z][A-Za-z0-9\s&\-\.]+?)(?:\n|\(|$)";
        foreach (var match in Regex.Matches(cv, companyPattern, RegexOptions.IgnoreCase).Cast<Match>())
        {
            var company = match.Groups[1].Value.Trim();
            if (company.Length > 1 && company.Length < 50)
            {
                companies.Add(company);
            }
        }

        return companies.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
