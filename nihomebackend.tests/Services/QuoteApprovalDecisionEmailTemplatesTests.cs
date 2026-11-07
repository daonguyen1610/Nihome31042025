using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class QuoteApprovalDecisionEmailTemplatesTests
{
    [Fact]
    public void DefaultTemplate_IsValidAndEncodesDynamicContent()
    {
        var body = QuoteApprovalDecisionEmailTemplates.Validate(QuoteApprovalDecisionEmailTemplates.DefaultBody, true);
        var subject = QuoteApprovalDecisionEmailTemplates.Validate(
            QuoteApprovalDecisionEmailTemplates.DefaultSubject, false);
        var rendered = QuoteApprovalDecisionEmailTemplates.Render(new(subject, body),
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["recipientName"] = "<script>alert(1)</script>",
                ["approverName"] = "Trưởng phòng Cung ứng",
                ["quoteCode"] = "QT-2026-0001",
                ["quoteVersion"] = "3",
                ["grandTotal"] = "810.000.000",
                ["validUntil"] = "30/11/2026",
                ["decisionLabel"] = "Đã phê duyệt",
                ["decisionNote"] = "Approved by finance",
                ["quoteUrl"] = "https://nicon.example/admin/quotes/1",
            });

        Assert.Contains("QT-2026-0001", rendered.Subject);
        Assert.Contains("Đã phê duyệt", rendered.Subject);
        Assert.Contains("&lt;script&gt;", rendered.Body);
        Assert.DoesNotContain("<script>", rendered.Body);
        Assert.Contains("Approved by finance", rendered.Body);
        Assert.Contains("https://nicon.example/admin/quotes/1", rendered.Body);
    }

    [Theory]
    [InlineData("<div>{{quoteCode}}<script>bad</script><a href=\"{{quoteUrl}}\">Open</a></div>")]
    [InlineData("<div>{{quoteCode}}<a href=\"https://other.example\">Open</a></div>")]
    [InlineData("<div>{{decisionLabel}}<a href=\"{{quoteUrl}}\">Open</a></div>")]
    [InlineData("<div>{{quoteCode}}{{unsupported}}<a href=\"{{quoteUrl}}\">Open</a></div>")]
    public void UnsafeOrIncompleteBody_IsRejected(string body)
    {
        Assert.Throws<ArgumentException>(() => QuoteApprovalDecisionEmailTemplates.Validate(body, true));
    }

    [Fact]
    public void Get_UsesConfiguredTemplatesWhenPresent()
    {
        var settings = new NihomeBackend.Models.SiteSettings
        {
            QuoteApprovalDecisionEmailSubjectTemplate = "Ket qua {{quoteCode}}",
            QuoteApprovalDecisionEmailBodyTemplate =
                "<div>{{quoteCode}}<a href=\"{{quoteUrl}}\">Open</a></div>",
        };

        var template = QuoteApprovalDecisionEmailTemplates.Get(settings);

        Assert.Equal("Ket qua {{quoteCode}}", template.Subject);
        Assert.Contains("{{quoteUrl}}", template.Body);
    }
}
