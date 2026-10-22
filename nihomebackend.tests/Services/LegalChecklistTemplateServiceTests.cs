using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class LegalChecklistTemplateServiceTests
{
    [Fact]
    public async Task IndependentReview_FreezesPublishedVersion_AndKeepsRetiredContents()
    {
        using var db = DbContextFactory.Create();
        db.MasterDataOptions.Add(new MasterDataOption { Category = "permit_type", Code = "gpxd", Name = "GPXD", IsActive = true });
        await db.SaveChangesAsync();
        var service = new LegalChecklistTemplateService(db);
        var input = Request(); input.Items[0].PermitTypeCode = "GPXD";
        var draft = (await service.SaveAsync(null, input, 11, default))!;
        Assert.Equal("gpxd", draft.Items[0].PermitTypeCode);
        var entity = await db.LegalChecklistTemplates.SingleAsync();
        entity.RowVersion = new byte[8];
        await db.SaveChangesAsync();
        var decision = new LegalChecklistTemplateDecisionRequest { RowVersion = Convert.ToBase64String(new byte[8]), ApprovalReference = "NICON test approval" };
        var self = await Assert.ThrowsAsync<LegalActivationException>(() => service.DecideAsync(draft.Id, decision, 11, false, default));
        Assert.Equal("LEGAL_TEMPLATE_SELF_APPROVAL", self.Code);
        var published = (await service.DecideAsync(draft.Id, decision, 22, false, default))!;
        Assert.Equal("Published", published.Status);
        Assert.Equal(22, published.PublishedByUserId);
        var edit = Request(); edit.RowVersion = decision.RowVersion;
        var immutable = await Assert.ThrowsAsync<LegalActivationException>(() => service.SaveAsync(draft.Id, edit, 11, default));
        Assert.Equal("LEGAL_TEMPLATE_IMMUTABLE", immutable.Code);
        var revision = (await service.NewVersionAsync(draft.Id, decision.RowVersion, 11, default))!;
        Assert.Equal(2, revision.Version);
        Assert.Equal("Draft", revision.Status);
        Assert.Equal(published.Items[0].PermitTypeCode, revision.Items[0].PermitTypeCode);
        var retired = (await service.DecideAsync(draft.Id, decision, 22, true, default))!;
        Assert.Equal("Retired", retired.Status);
        Assert.Equal(published.Items[0].IsRequired, retired.Items[0].IsRequired);
        Assert.Empty(await service.ListAsync(true, default));
        Assert.Equal(4, await db.AuditLogs.CountAsync());
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("optional-only")]
    [InlineData("missing-language")]
    public async Task InvalidDraftHasNoPersistenceOrAudit(string scenario)
    {
        using var db = DbContextFactory.Create();
        db.MasterDataOptions.Add(new MasterDataOption { Category = "permit_type", Code = "gpxd", Name = "GPXD", IsActive = true });
        await db.SaveChangesAsync();
        var request = Request();
        if (scenario == "duplicate") request.Items.Add(new() { PermitTypeCode = "GPXD", IsRequired = true });
        if (scenario == "unknown") request.Items[0].PermitTypeCode = "missing";
        if (scenario == "optional-only") request.Items[0].IsRequired = false;
        if (scenario == "missing-language") request.NameJa = " ";
        await Assert.ThrowsAsync<LegalActivationException>(() => new LegalChecklistTemplateService(db).SaveAsync(null, request, 11, default));
        Assert.Empty(db.LegalChecklistTemplates);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task EditingSomeoneElsesDraftDoesNotPermitSelfPublication()
    {
        using var db = DbContextFactory.Create();
        db.MasterDataOptions.Add(new MasterDataOption { Category = "permit_type", Code = "gpxd", Name = "GPXD", IsActive = true });
        await db.SaveChangesAsync();
        var service = new LegalChecklistTemplateService(db);
        var draft = (await service.SaveAsync(null, Request(), 11, default))!;
        (await db.LegalChecklistTemplates.SingleAsync()).RowVersion = new byte[8];
        await db.SaveChangesAsync();
        var request = Request(); request.RowVersion = Convert.ToBase64String(new byte[8]);
        await service.SaveAsync(draft.Id, request, 22, default);
        var exception = await Assert.ThrowsAsync<LegalActivationException>(() => service.DecideAsync(draft.Id,
            new() { RowVersion = request.RowVersion, ApprovalReference = "CM test" }, 22, false, default));
        Assert.Equal("LEGAL_TEMPLATE_SELF_APPROVAL", exception.Code);
    }

    [Fact]
    public async Task EarlierEditorCannotPublishAfterAuditRetention()
    {
        using var db = DbContextFactory.Create();
        db.MasterDataOptions.Add(new MasterDataOption { Category = "permit_type", Code = "gpxd", Name = "GPXD", IsActive = true });
        await db.SaveChangesAsync();
        var service = new LegalChecklistTemplateService(db);
        var draft = (await service.SaveAsync(null, Request(), 11, default))!;
        (await db.LegalChecklistTemplates.SingleAsync()).RowVersion = new byte[8];
        await db.SaveChangesAsync();
        var request = Request(); request.RowVersion = Convert.ToBase64String(new byte[8]);
        await service.SaveAsync(draft.Id, request, 22, default);
        await service.SaveAsync(draft.Id, request, 33, default);
        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<LegalActivationException>(() => service.DecideAsync(draft.Id,
            new() { RowVersion = request.RowVersion, ApprovalReference = "Independent CM test" }, 22, false, default));
        Assert.Equal("LEGAL_TEMPLATE_SELF_APPROVAL", error.Code);
    }

    private static SaveLegalChecklistTemplateRequest Request() => new()
    {
        Code = "LEGAL-FACTORY",
        NameVi = "Pháp lý nhà máy",
        Name = "Factory permits",
        NameZh = "工厂许可",
        NameJa = "工場許可",
        ScopeVi = "Mẫu kiểm thử",
        Scope = "Test fixture",
        ScopeZh = "测试模板",
        ScopeJa = "テスト用",
        Items = [new() { PermitTypeCode = "gpxd", IsRequired = true }],
    };
}
