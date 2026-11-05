using NihomeBackend.Models;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class ProjectScheduleEmailTemplatesTests
{
    [Fact]
    public void DefaultTemplateRendersProjectAndChangeDetailsWithHtmlEncoding()
    {
        var template = ProjectScheduleEmailTemplates.Get(new SiteSettings());
        var rendered = ProjectScheduleEmailTemplates.Render(template, new Dictionary<string, string>
        {
            ["eventName"] = "Đã cập nhật",
            ["recipientName"] = "An <script>",
            ["projectCode"] = "PJ-2026-001",
            ["projectName"] = "Trung tâm & dịch vụ",
            ["version"] = "2",
            ["changeReason"] = "Khách điều chỉnh mốc bàn giao",
            ["changeSummary"] = "1.1 2026-01-01 → 2026-02-01",
        });

        Assert.Contains("An &lt;script&gt;", rendered.Body);
        Assert.Contains("Trung tâm &amp; dịch vụ", rendered.Body);
        Assert.Contains("1.1 2026-01-01 → 2026-02-01", rendered.Body);
        Assert.DoesNotContain("{{", rendered.Body);
    }

    [Fact]
    public void RenderKeepsSubjectPlainTextAndBodyHtmlEncoded()
    {
        var template = ProjectScheduleEmailTemplates.Get(null);
        var values = new Dictionary<string, string>
        {
            ["eventName"] = "Duyệt & phát hành",
            ["recipientName"] = "An <script>",
            ["projectCode"] = "PJ&A",
            ["projectName"] = "Trung tâm <mới>",
            ["version"] = "2",
            ["changeReason"] = "Điều chỉnh",
            ["changeSummary"] = "Mốc 1 → Mốc 2",
        };

        var rendered = ProjectScheduleEmailTemplates.Render(template, values);

        Assert.Contains("Duyệt & phát hành · PJ&A", rendered.Subject);
        Assert.DoesNotContain("&amp;", rendered.Subject);
        Assert.Contains("PJ&amp;A", rendered.Body);
        Assert.Contains("An &lt;script&gt;", rendered.Body);
    }

    [Fact]
    public void DefaultTemplatePassesServerValidation()
    {
        var template = ProjectScheduleEmailTemplates.Validate(
            ProjectScheduleEmailTemplates.DefaultSubject,
            ProjectScheduleEmailTemplates.DefaultBody);

        Assert.Equal(ProjectScheduleEmailTemplates.DefaultSubject, template.Subject);
        Assert.Equal(ProjectScheduleEmailTemplates.DefaultBody, template.Body);
    }

    [Fact]
    public void GetUpgradesOnlyThePreviousBuiltInBody()
    {
        var previousDefault = new SiteSettings
        {
            ProjectScheduleEmailBodyTemplate = ProjectScheduleEmailTemplates.LegacyDefaultBody,
        };
        var custom = new SiteSettings
        {
            ProjectScheduleEmailBodyTemplate = "<p>Custom {{eventName}}</p>",
        };

        Assert.Equal(ProjectScheduleEmailTemplates.DefaultBody,
            ProjectScheduleEmailTemplates.Get(previousDefault).Body);
        Assert.Equal(custom.ProjectScheduleEmailBodyTemplate,
            ProjectScheduleEmailTemplates.Get(custom).Body);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<div onclick=\"run()\">{{eventName}}</div>")]
    public void ValidateRejectsUnsafeMarkup(string body)
    {
        var template = ProjectScheduleEmailTemplates.Get(null);
        Assert.Throws<ArgumentException>(() => ProjectScheduleEmailTemplates.Validate(template.Subject, body));
    }

    [Fact]
    public void ValidateRejectsUnknownTokensAndMissingChangeSummary()
    {
        var template = ProjectScheduleEmailTemplates.Get(null);
        Assert.Throws<ArgumentException>(() => ProjectScheduleEmailTemplates.Validate(
            "{{notSupported}}", template.Body));
        Assert.Throws<ArgumentException>(() => ProjectScheduleEmailTemplates.Validate(
            template.Subject, template.Body.Replace("{{changeSummary}}", string.Empty, StringComparison.Ordinal)));
    }
}
