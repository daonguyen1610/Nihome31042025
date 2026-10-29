using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class QuoteApprovalEmailTemplatesTests
{
    [Fact]
    public void DefaultTemplate_IsValidAndEncodesDynamicContent()
    {
        var body = QuoteApprovalEmailTemplates.Validate(QuoteApprovalEmailTemplates.DefaultBody, true);
        var subject = QuoteApprovalEmailTemplates.Validate(QuoteApprovalEmailTemplates.DefaultSubject, false);
        var rendered = QuoteApprovalEmailTemplates.Render(new(subject, body), new(StringComparer.OrdinalIgnoreCase)
        {
            ["recipientName"] = "<script>alert(1)</script>",
            ["senderName"] = "Kinh doanh",
            ["quoteCode"] = "QT-2026-0001",
            ["quoteVersion"] = "1",
            ["grandTotal"] = "810.000.000",
            ["validUntil"] = "30/11/2026",
            ["quoteUrl"] = "https://nicon.example/admin/quotes/1",
        });

        Assert.Contains("QT-2026-0001", rendered.Subject);
        Assert.Contains("&lt;script&gt;", rendered.Body);
        Assert.DoesNotContain("<script>", rendered.Body);
        Assert.Contains("810.000.000", rendered.Body);
        Assert.Contains("https://nicon.example/admin/quotes/1", rendered.Body);
    }

    [Theory]
    [InlineData("<div>{{quoteCode}}<script>bad</script><a href=\"{{quoteUrl}}\">Open</a></div>")]
    [InlineData("<div>{{quoteCode}}<a href=\"https://other.example\">Open</a></div>")]
    [InlineData("<div>{{unsupported}}<a href=\"{{quoteUrl}}\">Open</a></div>")]
    public void UnsafeOrIncompleteBody_IsRejected(string body)
    {
        Assert.Throws<ArgumentException>(() => QuoteApprovalEmailTemplates.Validate(body, true));
    }
}
