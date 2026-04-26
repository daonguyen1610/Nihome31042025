using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public class EmailTemplateFormatterTests
{
    [Fact]
    public void ReplaceTokens_ReplacesAllMatchingTokens()
    {
        var template = "Hello {{name}}, welcome to {{site}}!";
        var tokens = new Dictionary<string, string>
        {
            ["name"] = "Vy",
            ["site"] = "Nihome"
        };

        var result = EmailTemplateFormatter.ReplaceTokens(template, tokens);

        Assert.Equal("Hello Vy, welcome to Nihome!", result);
    }

    [Fact]
    public void ReplaceTokens_IsCaseInsensitive_WithOrdinalIgnoreCaseDict()
    {
        var template = "{{Name}} at {{SITE}}";
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "Vy",
            ["site"] = "Nihome"
        };

        var result = EmailTemplateFormatter.ReplaceTokens(template, tokens);

        Assert.Equal("Vy at Nihome", result);
    }

    [Fact]
    public void ReplaceTokens_PreservesUnknownTokens()
    {
        var template = "Hello {{name}}, your {{unknownToken}} is ready";
        var tokens = new Dictionary<string, string> { ["name"] = "Vy" };

        var result = EmailTemplateFormatter.ReplaceTokens(template, tokens);

        Assert.Equal("Hello Vy, your {{unknownToken}} is ready", result);
    }

    [Fact]
    public void ReplaceTokens_ReturnsOriginal_WhenTemplateIsEmpty()
    {
        var result = EmailTemplateFormatter.ReplaceTokens("", new Dictionary<string, string> { ["a"] = "b" });
        Assert.Equal("", result);
    }

    [Fact]
    public void ReplaceTokens_ReturnsOriginal_WhenTemplateIsNull()
    {
        var result = EmailTemplateFormatter.ReplaceTokens(null!, new Dictionary<string, string> { ["a"] = "b" });
        Assert.Null(result);
    }

    [Fact]
    public void ReplaceTokens_ReturnsOriginal_WhenNoTokensInTemplate()
    {
        var result = EmailTemplateFormatter.ReplaceTokens("plain text", new Dictionary<string, string> { ["a"] = "b" });
        Assert.Equal("plain text", result);
    }

    [Fact]
    public void BuildNewApplicationEmail_UsesDefaultTemplates_WhenNullProvided()
    {
        var appliedAt = new DateTime(2026, 4, 26, 10, 30, 0);

        var (subject, body) = EmailTemplateFormatter.BuildNewApplicationEmail(
            subjectTemplate: null,
            bodyTemplate: null,
            siteName: "Nihome",
            positionTitle: "Backend Dev",
            department: "Engineering",
            candidateName: "Nguyễn Văn A",
            email: "a@test.com",
            phone: "0901234567",
            experienceYears: 3,
            coverLetter: "I love coding",
            appliedAt: appliedAt);

        Assert.Contains("Nihome", subject);
        Assert.Contains("Nguyễn Văn A", subject); // Subject uses plain text
        Assert.Contains("Backend Dev", subject);
        Assert.Contains("a@test.com", body);
        Assert.Contains("0901234567", body);
        Assert.Contains("3", body);
        Assert.Contains("I love coding", body);
        Assert.Contains("Engineering", body);
    }

    [Fact]
    public void BuildNewApplicationEmail_HtmlEncodesBodyTokens()
    {
        var appliedAt = new DateTime(2026, 1, 1, 0, 0, 0);

        var (subject, body) = EmailTemplateFormatter.BuildNewApplicationEmail(
            subjectTemplate: "{{candidateName}}",
            bodyTemplate: "<p>{{candidateName}}</p>",
            siteName: "Test",
            positionTitle: "Pos",
            department: "Dep",
            candidateName: "<script>alert(1)</script>",
            email: "x@x.com",
            phone: null,
            experienceYears: null,
            coverLetter: null,
            appliedAt: appliedAt);

        // Subject should be plain text (no encoding)
        Assert.Equal("<script>alert(1)</script>", subject);
        // Body should be HTML-encoded
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", body);
        Assert.DoesNotContain("<script>", body);
    }

    [Fact]
    public void BuildNewApplicationEmail_UsesCustomTemplates_WhenProvided()
    {
        var appliedAt = new DateTime(2026, 4, 26, 10, 30, 0);

        var (subject, body) = EmailTemplateFormatter.BuildNewApplicationEmail(
            subjectTemplate: "New: {{candidateName}}",
            bodyTemplate: "<b>{{candidateName}}</b> applied for {{positionTitle}}",
            siteName: "Nihome",
            positionTitle: "Designer",
            department: "Creative",
            candidateName: "Trần B",
            email: "b@test.com",
            phone: null,
            experienceYears: null,
            coverLetter: null,
            appliedAt: appliedAt);

        Assert.Equal("New: Trần B", subject);
        Assert.Equal("<b>Trần B</b> applied for Designer", body);
    }

    [Fact]
    public void BuildNewApplicationEmail_HandlesNullOptionalFields()
    {
        var appliedAt = new DateTime(2026, 1, 1, 0, 0, 0);

        var (subject, body) = EmailTemplateFormatter.BuildNewApplicationEmail(
            subjectTemplate: "{{candidateName}} – {{phone}} – {{experienceYears}}",
            bodyTemplate: "Letter: {{coverLetter}}",
            siteName: "Test",
            positionTitle: "Pos",
            department: "Dep",
            candidateName: "X",
            email: "x@x.com",
            phone: null,
            experienceYears: null,
            coverLetter: null,
            appliedAt: appliedAt);

        // Null fields should render as "—"
        Assert.Equal("X – — – —", subject);
        Assert.Equal("Letter: —", body);
    }

    [Fact]
    public void DefaultNewApplicationSubject_ContainsExpectedTokens()
    {
        var subject = EmailTemplateFormatter.DefaultNewApplicationSubject;

        Assert.Contains("{{siteName}}", subject);
        Assert.Contains("{{candidateName}}", subject);
        Assert.Contains("{{positionTitle}}", subject);
    }

    [Fact]
    public void DefaultNewApplicationBody_ContainsExpectedTokens()
    {
        var body = EmailTemplateFormatter.DefaultNewApplicationBody;

        Assert.Contains("{{siteName}}", body);
        Assert.Contains("{{candidateName}}", body);
        Assert.Contains("{{email}}", body);
        Assert.Contains("{{phone}}", body);
        Assert.Contains("{{positionTitle}}", body);
        Assert.Contains("{{department}}", body);
        Assert.Contains("{{experienceYears}}", body);
        Assert.Contains("{{coverLetter}}", body);
        Assert.Contains("{{appliedAt}}", body);
    }
}
