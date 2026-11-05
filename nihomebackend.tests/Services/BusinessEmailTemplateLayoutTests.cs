using System.Net;
using System.Xml.Linq;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class BusinessEmailTemplateLayoutTests
{
    [Fact]
    public void BusinessDefaults_UseTheSameNiconLayout()
    {
        var bodies = new[]
        {
            EmailTemplateFormatter.DefaultQuoteBody,
            QuoteApprovalEmailTemplates.DefaultBody,
            ContractReminderTemplates.RenewalBody,
            ContractReminderTemplates.EndBody,
            WorkPackageApprovalEmailTemplates.DefaultBody,
            ProjectScheduleEmailTemplates.DefaultBody
        };

        foreach (var body in bodies)
        {
            AssertLayout(body);
            Assert.True(EmailTemplateFormatter.IsSafeQuoteHtml(body.Replace("<a href=\"{{workPackageUrl}}\">", "<span>")
                .Replace("<a href=\"{{quoteUrl}}\">", "<span>").Replace("</a>", "</span>")));
        }
    }

    [Fact]
    public void SharedLayout_ChangesPresentationAndPreservesEventContent()
    {
        const string layout = "<div><p>NICON · {{heading}}</p><div>{{content}}</div><p>{{footer}}</p></div>";
        var settings = new SiteSettings { SharedBusinessEmailLayoutTemplate = layout };

        var schedule = ProjectScheduleEmailTemplates.Get(settings);
        var quoteApproval = QuoteApprovalEmailTemplates.Get(settings);
        var contract = ContractReminderTemplates.Get(settings);
        var workPackage = WorkPackageApprovalEmailTemplates.Get(settings);

        Assert.All(new[] { schedule.Body, quoteApproval.Body, contract.RenewalBody,
            contract.EndBody, workPackage.Body }, body => Assert.StartsWith("<div><p>NICON · ", body));
        Assert.Contains("{{changeSummary}}", schedule.Body);
        Assert.Contains("{{quoteUrl}}", quoteApproval.Body);
        Assert.Contains("{{contractNumber}}", contract.RenewalBody);
        Assert.Contains("{{workPackageUrl}}", workPackage.Body);

        var otp = EmailTemplateFormatter.BuildOtpEmail(null, null, "NICON", "123456", layoutTemplate: layout);
        var application = EmailTemplateFormatter.BuildNewApplicationEmail(null, null, "NICON",
            "Kỹ sư MEP", "Thiết kế", "Nguyễn Văn An", "an@example.test", null, null, null,
            new DateTime(2026, 11, 5), layout);
        Assert.StartsWith("<div><p>NICON · ", otp.body);
        Assert.Contains("123456", otp.body);
        Assert.StartsWith("<div><p>NICON · ", application.body);
        Assert.Contains("Kỹ sư MEP", application.body);

        var quote = EmailTemplateFormatter.BuildQuoteEmail(null, null,
            new Dictionary<string, string>
            {
                ["customerName"] = "Nhà máy An Phú",
                ["quoteCode"] = "BG-2026-01",
                ["opportunityName"] = "Thiết kế MEP",
                ["quoteLines"] = "Hạng mục MEP",
                ["discountPercent"] = "0",
                ["vatPercent"] = "8",
                ["grandTotal"] = "100.000.000",
                ["validUntil"] = "30/11/2026"
            }, layout);
        Assert.StartsWith("<div><p>NICON · ", quote.body);
        Assert.Contains("BG-2026-01", quote.body);
        Assert.Contains("Hạng mục MEP", quote.body);
    }

    [Theory]
    [InlineData("Renewal")]
    [InlineData("End")]
    public void ContractTemplates_RenderBrandedSafeHtmlWithEncodedCustomerAndContract(string kind)
    {
        var templates = ContractReminderTemplates.Get(null);
        var template = kind == "Renewal" ? templates.RenewalBody : templates.EndBody;
        Assert.Equal(template, ContractReminderTemplates.Validate(template, true));
        var contract = new Contract
        {
            ContractNumber = "HD-ANPHU-2026<&>",
            Customer = new Customer { Name = "Nhà máy An Phú <script>alert(1)</script> & NICON" },
            EndDate = new DateTime(2026, 11, 30)
        };

        var rendered = ContractReminderTemplates.Render(templates, kind, contract);

        AssertLayout(rendered.Body);
        Assert.Contains(WebUtility.HtmlEncode(contract.Customer.Name), rendered.Body);
        Assert.Contains(WebUtility.HtmlEncode(contract.ContractNumber), rendered.Body);
        Assert.Contains("30/11/2026", rendered.Body);
        Assert.Contains("Ngày kết thúc dự kiến", rendered.Body);
        Assert.DoesNotContain("<script>", rendered.Body);
        Assert.DoesNotContain("{{", rendered.Body);
        Assert.True(EmailTemplateFormatter.IsSafeQuoteHtml(rendered.Body));
    }

    [Fact]
    public void ApprovalTemplate_RendersAllBusinessFieldsAndRetainsInSystemApprovalNotice()
    {
        var template = WorkPackageApprovalEmailTemplates.Get(null);
        Assert.Equal(template.Body, WorkPackageApprovalEmailTemplates.Validate(template.Body, true));
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["recipientName"] = "Trần <An>",
            ["senderName"] = "Mai & Anh",
            ["workPackageCode"] = "WP-ANPHU-MEP-01",
            ["workPackageName"] = "Cung cấp thiết bị MEP <Nhà máy An Phú>",
            ["recordType"] = "Báo giá đầu vào",
            ["recordReference"] = "BG-MEP-2026-01",
            ["workPackageUrl"] = "https://nicon.example.test/admin/work-packages/42"
        };

        var rendered = WorkPackageApprovalEmailTemplates.Render(template, tokens);

        AssertLayout(rendered.Body);
        Assert.All(tokens.Values, value => Assert.Contains(WebUtility.HtmlEncode(value), rendered.Body));
        Assert.Contains("quyết định phê duyệt được thực hiện trong hệ thống", rendered.Body);
        Assert.DoesNotContain("{{", rendered.Body);
        Assert.True(WorkPackageApprovalEmailTemplates.IsSafeApprovalHtml(template.Body));
        Assert.Equal(tokens["workPackageUrl"], XDocument.Parse(rendered.Body).Descendants("a").Single().Attribute("href")!.Value);
    }

    private static void AssertLayout(string html)
    {
        var root = XDocument.Parse(html).Root!;
        Assert.Contains("font-family:Arial,sans-serif", root.Attribute("style")!.Value);
        var table = Assert.Single(root.Elements("table"));
        Assert.Equal("presentation", table.Attribute("role")!.Value);
        Assert.Equal("100%", table.Attribute("width")!.Value);
        Assert.Contains("max-width:600px", table.Attribute("style")!.Value);
        var rows = table.Elements("tr").ToList();
        Assert.Contains("background:#e5394a", rows[0].Element("td")!.Attribute("style")!.Value);
        Assert.Contains("NICON", rows[0].Value);
        Assert.Contains("NICON", rows[^1].Value);
    }
}
