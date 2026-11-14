using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

public class ProjectBoqWorkbooksControllerTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string TemplateCode = "PCCC-CAN-HO";

    private async Task LoginAsync(string role = "SUPER_ADMIN")
    {
        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));
    }

    private async Task<int> CreateProjectAsync() => await WithDbAsync(async db =>
    {
        var manager = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
            .Select(user => user.Id).SingleAsync();
        var customer = new Customer { Type = CustomerType.Company, Name = "Chủ đầu tư căn hộ dịch vụ", SourceCode = "referral" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var project = new OperationalProject
        {
            Code = $"PJ-BOQ-{Guid.NewGuid():N}"[..28],
            Name = "Căn hộ dịch vụ Thảo Điền",
            CustomerId = customer.Id,
            ProjectManagerUserId = manager,
        };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    });

    private static string Base(int projectId) => $"/api/operational-projects/{projectId}/procurement";

    private async Task<byte[]> TemplateAsync(int projectId)
    {
        var response = await Client.GetAsync($"{Base(projectId)}/boq-templates/{TemplateCode}/file");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static byte[] Fill(byte[] template, bool priced, Action<IXLWorksheet>? change = null)
    {
        using var book = new XLWorkbook(new MemoryStream(template));
        var sheet = book.Worksheet("ptct");
        sheet.Cell("E17").Value = 50;
        sheet.Cell("F17").Value = 420000;
        sheet.Cell("E32").Value = 70;
        if (priced) sheet.Cell("F32").Value = 265000;
        change?.Invoke(sheet);
        book.RecalculateAllFormulas();
        using var output = new MemoryStream();
        book.SaveAs(output, new SaveOptions { EvaluateFormulasBeforeSaving = true });
        return output.ToArray();
    }

    private static MultipartFormDataContent Form(byte[] bytes, string templateCode = TemplateCode, string? hash = null, int? contractId = null)
    {
        var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(bytes), "file", "BOQ-PCCC-Thao-Dien.xlsx" },
            { new StringContent(templateCode), "templateCode" },
        };
        if (hash != null) form.Add(new StringContent(hash), "sourceHash");
        if (contractId != null) form.Add(new StringContent(contractId.Value.ToString()), "upstreamContractId");
        return form;
    }

    private async Task<HttpResponseMessage> PreviewAsync(int projectId, byte[] bytes, string templateCode = TemplateCode)
    {
        using var form = Form(bytes, templateCode);
        return await Client.PostAsync($"{Base(projectId)}/boq-workbooks/preview", form);
    }

    private async Task<HttpResponseMessage> ImportAsync(int projectId, byte[] bytes, string hash, int? contractId = null)
    {
        using var form = Form(bytes, hash: hash, contractId: contractId);
        return await Client.PostAsync($"{Base(projectId)}/boq-workbooks", form);
    }

    private async Task<JsonElement> ImportOkAsync(int projectId, byte[] bytes, int? contractId = null)
    {
        var preview = await PreviewAsync(projectId, bytes);
        preview.StatusCode.Should().Be(HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
        var hash = (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sourceHash").GetString()!;
        var created = await ImportAsync(projectId, bytes, hash, contractId);
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        return await created.Content.ReadFromJsonAsync<JsonElement>();
    }

    private Task<HttpResponseMessage> PostJsonAsync(string url, object body) => SendJsonAsync(HttpMethod.Post, url, body);

    private async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string url, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await Client.SendAsync(request);
    }

    private async Task<JsonElement> SubmitAndApproveAsync(int projectId, JsonElement revision)
    {
        var id = revision.GetProperty("id").GetInt32();
        var submitted = await PostJsonAsync($"{Base(projectId)}/boq-revisions/{id}/submit", new { rowVersion = revision.GetProperty("rowVersion").GetString() });
        submitted.StatusCode.Should().Be(HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());
        var body = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        var approved = await PostJsonAsync($"{Base(projectId)}/boq-revisions/{id}/decision", new { approved = true, rowVersion = body.GetProperty("rowVersion").GetString() });
        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        return await approved.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task TenderBoq_UploadKeepsFileAndSheetsAndNeverBecomesMaterialAllowance()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();

        var templates = await Client.GetFromJsonAsync<JsonElement>($"{Base(projectId)}/boq-templates");
        templates.EnumerateArray().Select(item => item.GetProperty("code").GetString()).Should().Contain(TemplateCode);
        var template = await TemplateAsync(projectId);
        template.Should().Equal(ProjectBoqTemplateCatalog.ReadFile(ProjectBoqTemplateCatalog.Find(TemplateCode)!), "the download must stay the approved customer layout");

        var unpriced = Fill(template, priced: false);
        var preview = await PreviewAsync(projectId, unpriced);
        preview.StatusCode.Should().Be(HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
        var summary = await preview.Content.ReadFromJsonAsync<JsonElement>();
        summary.GetProperty("itemCount").GetInt32().Should().Be(2);
        summary.GetProperty("missingPriceCount").GetInt32().Should().Be(1);
        summary.GetProperty("total").GetDecimal().Should().Be(21_000_000m);
        (await WithDbAsync(db => db.ProjectBoqRevisions.CountAsync(item => item.OperationalProjectId == projectId))).Should().Be(0, "preview must not save");

        var hash = summary.GetProperty("sourceHash").GetString()!;
        var created = await ImportAsync(projectId, unpriced, hash);
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetInt32();
        draft.GetProperty("status").GetString().Should().Be("Draft");
        draft.GetProperty("upstreamContractId").ValueKind.Should().Be(JsonValueKind.Null);
        draft.GetProperty("templateCode").GetString().Should().Be(TemplateCode);
        draft.GetProperty("sourceFileName").GetString().Should().Be("BOQ-PCCC-Thao-Dien.xlsx");
        var sheet = draft.GetProperty("sheets").EnumerateArray().Single();
        sheet.GetProperty("name").GetString().Should().Be("ptct");
        sheet.GetProperty("total").GetDecimal().Should().Be(21_000_000m);
        var line = draft.GetProperty("lines")[0];
        line.GetProperty("itemCode").GetString().Should().Be("S01-R0017");
        line.GetProperty("sheetName").GetString().Should().Be("ptct");
        line.GetProperty("sourceRow").GetInt32().Should().Be(17);
        line.GetProperty("approvedQuantity").GetDecimal().Should().Be(50m);
        line.GetProperty("budgetUnitPrice").GetDecimal().Should().Be(420000m);
        draft.GetProperty("lines")[1].GetProperty("priceMissing").GetBoolean().Should().BeTrue();

        // Confirming the same file twice must not create a second revision.
        var repeated = await ImportAsync(projectId, unpriced, hash);
        (await repeated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32().Should().Be(id);

        var stored = await Client.GetAsync($"{Base(projectId)}/boq-revisions/{id}/workbook");
        stored.StatusCode.Should().Be(HttpStatusCode.OK);
        (await stored.Content.ReadAsByteArrayAsync()).Should().Equal(unpriced);

        var blocked = await PostJsonAsync($"{Base(projectId)}/boq-revisions/{id}/submit", new { rowVersion = draft.GetProperty("rowVersion").GetString() });
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var reason = await blocked.Content.ReadFromJsonAsync<JsonElement>();
        reason.GetProperty("messageKey").GetString().Should().Be("boqWorkbook.error.missingPrices");
        reason.GetProperty("limit").GetString().Should().Be("1");
        (await WithDbAsync(db => db.ProjectBoqRevisions.AsNoTracking().SingleAsync(item => item.Id == id))).Status.Should().Be(ProjectBoqRevisionStatus.Draft);

        var edit = await SendJsonAsync(HttpMethod.Put, $"{Base(projectId)}/boq-revisions/{id}", new
        {
            upstreamContractId = await CreateSignedUpstreamContractAsync(projectId),
            currency = "VND",
            lines = new[] { new { itemCode = "X", description = "Sửa tay", unit = "m", approvedQuantity = 1m, budgetUnitPrice = 1m } },
            rowVersion = draft.GetProperty("rowVersion").GetString(),
        });
        edit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await edit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("messageKey").GetString().Should().Be("boqWorkbook.error.workbookReadOnly");

        var approved = await SubmitAndApproveAsync(projectId, await ImportOkAsync(projectId, Fill(template, priced: true)));
        approved.GetProperty("status").GetString().Should().Be("TenderApproved");
        (await WithDbAsync(db => db.ProjectBoqRevisions.AnyAsync(item => item.OperationalProjectId == projectId && item.Status == ProjectBoqRevisionStatus.Approved)))
            .Should().BeFalse("a tender BOQ must never act as the MR/warehouse allowance");
    }

    [Fact]
    public async Task ContractBoq_ApprovedLinesBecomeTheAllowanceOtherModulesLinkTo()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var contractId = await CreateSignedUpstreamContractAsync(projectId);
        var template = await TemplateAsync(projectId);

        var approved = await SubmitAndApproveAsync(projectId, await ImportOkAsync(projectId, Fill(template, priced: true), contractId));

        approved.GetProperty("status").GetString().Should().Be("Approved");
        approved.GetProperty("upstreamContractId").GetInt32().Should().Be(contractId);
        approved.GetProperty("costTotal").GetDecimal().Should().Be(21_000_000m + 70 * 265_000m);
        var lineId = approved.GetProperty("lines")[1].GetProperty("id").GetInt32();
        (await WithDbAsync(db => db.ProjectBoqLines.AsNoTracking().SingleAsync(item => item.Id == lineId)))
            .ApprovedQuantity.Should().Be(70m);
    }

    [Theory]
    [InlineData("text-quantity", "boqWorkbook.error.number", "E17")]
    [InlineData("broken-total", "boqWorkbook.error.totalMismatch", "G101")]
    [InlineData("extra-sheet", "boqWorkbook.error.unknownSheet", null)]
    public async Task InvalidWorkbook_ReturnsCellIssueAndSavesNothing(string scenario, string messageKey, string? cell)
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var bytes = Fill(await TemplateAsync(projectId), priced: true, sheet =>
        {
            if (scenario == "text-quantity") sheet.Cell("E17").Value = "50 cái";
            if (scenario == "broken-total") sheet.Cell("G30").FormulaA1 = "SUM(G16:G16)";
            if (scenario == "extra-sheet") sheet.Workbook.AddWorksheet("Ghi chú").Cell("A1").Value = "Ghi chú nội bộ";
        });

        var response = await PreviewAsync(projectId, bytes);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("messageKey").GetString().Should().Be("boqWorkbook.error.invalidRows");
        var issue = body.GetProperty("issues").EnumerateArray().First(item => item.GetProperty("messageKey").GetString() == messageKey);
        if (cell != null) issue.GetProperty("cell").GetString().Should().Be(cell);
        var imported = await ImportAsync(projectId, bytes, new string('0', 64));
        imported.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ProjectBoqRevisions.CountAsync(item => item.OperationalProjectId == projectId))).Should().Be(0);
    }

    [Fact]
    public async Task WrongTemplateContractOrChangedFile_AreRejectedWithoutWrites()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var otherProjectId = await CreateProjectAsync();
        var otherContractId = await CreateSignedUpstreamContractAsync(otherProjectId);
        var bytes = Fill(await TemplateAsync(projectId), priced: true);

        var wrongTemplate = await PreviewAsync(projectId, bytes, "KHONG-CO");
        (await wrongTemplate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("messageKey").GetString().Should().Be("boqWorkbook.error.template");
        var hash = (await (await PreviewAsync(projectId, bytes)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sourceHash").GetString()!;
        var wrongContract = await ImportAsync(projectId, bytes, hash, otherContractId);
        (await wrongContract.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("messageKey").GetString().Should().Be("boqWorkbook.error.contract");
        var changed = await ImportAsync(projectId, bytes, new string('0', 64));
        (await changed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("messageKey").GetString().Should().Be("boqWorkbook.error.changedFile");
        (await WithDbAsync(db => db.ProjectBoqRevisions.CountAsync(item => item.OperationalProjectId == projectId))).Should().Be(0);
    }

    [Fact]
    public async Task Upload_RequiresBoqManagePermission()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var bytes = Fill(await TemplateAsync(projectId), priced: true);

        Client.DefaultRequestHeaders.Authorization = null;
        (await Client.GetAsync($"{Base(projectId)}/boq-templates")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await LoginAsync("DESIGN");
        (await PreviewAsync(projectId, bytes)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ImportAsync(projectId, bytes, new string('0', 64))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // 31/10 meeting: QS enters the project's root BOQ; approval stays with PM/BGD.
    [Fact]
    public async Task QsOnTheProjectTeam_UploadsTheTenderBoqButCannotApproveIt()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var bytes = Fill(await TemplateAsync(projectId), priced: true);

        await LoginAsync("QS");
        (await PreviewAsync(projectId, bytes)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await WithDbAsync(async db =>
        {
            var qs = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["QS"])
                .Select(user => user.Id).SingleAsync();
            db.OperationalProjectMembers.Add(new OperationalProjectMember
            {
                OperationalProjectId = projectId,
                UserId = qs,
                Position = "QS",
                StartedAt = DateTime.UtcNow,
                CreatedByUserId = qs,
                UpdatedByUserId = qs,
            });
            await db.SaveChangesAsync();
        });
        var revision = await ImportOkAsync(projectId, bytes);
        var id = revision.GetProperty("id").GetInt32();
        var submitted = await PostJsonAsync($"{Base(projectId)}/boq-revisions/{id}/submit", new { rowVersion = revision.GetProperty("rowVersion").GetString() });
        submitted.StatusCode.Should().Be(HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());
        var body = await submitted.Content.ReadFromJsonAsync<JsonElement>();

        var decision = await PostJsonAsync($"{Base(projectId)}/boq-revisions/{id}/decision", new { approved = true, rowVersion = body.GetProperty("rowVersion").GetString() });

        decision.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.ProjectBoqRevisions.Where(item => item.Id == id).Select(item => item.Status).SingleAsync()))
            .Should().Be(ProjectBoqRevisionStatus.Submitted);
    }

    private Task<HttpResponseMessage> ConvertAsync(int projectId, JsonElement revision, int contractId) =>
        PostJsonAsync($"{Base(projectId)}/boq-revisions/{revision.GetProperty("id").GetInt32()}/contract",
            new { upstreamContractId = contractId, rowVersion = revision.GetProperty("rowVersion").GetString() });

    private static async Task<string?> ErrorKeyAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("messageKey").GetString();
    }

    // 31/10 meeting: a won tender BOQ becomes the contract BOQ without a new file.
    [Fact]
    public async Task WonTender_ThePmTurnsTheApprovedTenderBoqIntoTheContractBoq()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var contractId = await CreateSignedUpstreamContractAsync(projectId);
        var tender = await SubmitAndApproveAsync(projectId, await ImportOkAsync(projectId, Fill(await TemplateAsync(projectId), priced: true)));
        var id = tender.GetProperty("id").GetInt32();

        await LoginAsync("QS");
        (await ConvertAsync(projectId, tender, contractId)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await LoginAsync("PM");
        var converted = await ConvertAsync(projectId, tender, contractId);
        converted.StatusCode.Should().Be(HttpStatusCode.OK, await converted.Content.ReadAsStringAsync());
        var contract = await converted.Content.ReadFromJsonAsync<JsonElement>();
        contract.GetProperty("id").GetInt32().Should().Be(id);
        contract.GetProperty("status").GetString().Should().Be("Approved");
        contract.GetProperty("upstreamContractId").GetInt32().Should().Be(contractId);
        contract.GetProperty("revisionNumber").GetInt32().Should().Be(tender.GetProperty("revisionNumber").GetInt32());
        contract.GetProperty("sourceFileName").GetString().Should().Be(tender.GetProperty("sourceFileName").GetString());
        contract.GetProperty("approvedAt").GetDateTime().Should().Be(tender.GetProperty("approvedAt").GetDateTime(), "the tender approval stays on record");
        (await WithDbAsync(db => db.ProjectBoqRevisions.CountAsync(item => item.OperationalProjectId == projectId))).Should().Be(1);

        var workspace = await Client.GetFromJsonAsync<JsonElement>($"/api/operational-projects/{projectId}/procurement/material-requests");
        workspace.GetProperty("currentApprovedBoq").GetProperty("revisionId").GetInt32().Should().Be(id, "the contract BOQ is the allowance");

        (await ConvertAsync(projectId, contract, contractId)).StatusCode.Should().Be(HttpStatusCode.OK, "a repeated convert returns the same BOQ");
    }

    [Fact]
    public async Task ContractConversion_RejectsDraftsUnsignedContractsOlderTendersAndASecondContractBoq()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var signed = await CreateSignedUpstreamContractAsync(projectId);
        var unsigned = await WithDbAsync(async db =>
        {
            var contract = await db.Contracts.SingleAsync(item => item.Id == signed);
            var draft = new Contract
            {
                ContractNumber = UniqueSlug("HD-DRAFT").ToUpperInvariant(),
                CustomerId = contract.CustomerId,
                Direction = ContractDirection.Upstream,
                Type = ContractType.Construction,
                OperationalProjectId = projectId,
                Status = ContractStatus.Draft,
                Value = 1m,
            };
            db.Contracts.Add(draft);
            await db.SaveChangesAsync();
            return draft.Id;
        });
        var template = await TemplateAsync(projectId);
        var draft = await ImportOkAsync(projectId, Fill(template, priced: true));
        (await ErrorKeyAsync(await ConvertAsync(projectId, draft, signed))).Should().Be("boqWorkbook.error.notTender");

        var older = await SubmitAndApproveAsync(projectId, draft);
        (await ErrorKeyAsync(await ConvertAsync(projectId, older, unsigned))).Should().Be("boqWorkbook.error.contract");
        var newer = await SubmitAndApproveAsync(projectId, await ImportOkAsync(projectId, Fill(template, priced: true, sheet => sheet.Cell("E17").Value = 60)));
        (await ErrorKeyAsync(await ConvertAsync(projectId, older, signed))).Should().Be("boqWorkbook.error.notLatestTender");

        (await ConvertAsync(projectId, newer, signed)).StatusCode.Should().Be(HttpStatusCode.OK);
        var third = await SubmitAndApproveAsync(projectId, await ImportOkAsync(projectId, Fill(template, priced: true, sheet => sheet.Cell("E17").Value = 70)));
        (await ErrorKeyAsync(await ConvertAsync(projectId, third, signed))).Should().Be("boqWorkbook.error.contractBoqExists");
        (await WithDbAsync(db => db.ProjectBoqRevisions.CountAsync(item => item.OperationalProjectId == projectId && item.Status == ProjectBoqRevisionStatus.Approved)))
            .Should().Be(1);
    }
}
