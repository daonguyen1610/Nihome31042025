using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class RfqsControllerTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task Lifecycle_PreservesRevisions_AwardsOnce_AndCreatesCorrectDownstreamContract()
    {
        var fixture = await SeedAsync();
        await LoginAsync("PROCUREMENT");
        var key = Guid.NewGuid().ToString();
        var draft = Draft(fixture);
        var created = await SendAsync(fixture.ProjectId, "", draft, key);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var detail = await DetailAsync(created);
        var replay = await SendAsync(fixture.ProjectId, "", draft, key);
        replay.Headers.GetValues("Idempotency-Replayed").Should().Contain("true");
        (await DetailAsync(replay)).Header.Id.Should().Be(detail.Header.Id);
        draft.Title = "Different payload";
        (await SendAsync(fixture.ProjectId, "", draft, key)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        detail = await TransitionAsync(detail, "issue");
        var frozenLines = detail.Lines.Select(x => (x.ItemCode, x.Quantity)).ToList();
        detail = await SubmitAsync(detail, fixture.VendorId, 12.3456m, 0m);
        var firstBidId = detail.Bids.Single().Id;
        detail = await SubmitAsync(detail, fixture.OtherVendorId, 1m, null);
        detail.Bids.Single(x => x.VendorId == fixture.OtherVendorId).IsComplete.Should().BeFalse();
        detail.Bids.Single(x => x.VendorId == fixture.OtherVendorId).IsEligible.Should().BeFalse();
        detail = await SubmitAsync(detail, fixture.VendorId, 8.1234m, 2m);
        detail.Bids.Single(x => x.Id == firstBidId).IsCurrent.Should().BeFalse();
        detail.Bids.Single(x => x.Id == firstBidId).Lines.First().UnitPrice.Should().Be(12.3456m);
        detail.Lines.Select(x => (x.ItemCode, x.Quantity)).Should().Equal(frozenLines);
        detail.Lines[0].LowestUnitPrice.Should().Be(1m);
        var winningBid = detail.Bids.Single(x => x.VendorId == fixture.VendorId && x.IsCurrent);
        winningBid.Total.Should().Be(14.1543m); // 1.25 × 8.1234 rounded to 4dp, plus 2 × 2.
        winningBid.IsLowest.Should().BeTrue();
        detail = await TransitionAsync(detail, "evaluate");
        detail = await DetailAsync(await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/bids/evaluate",
            new RfqBidEvaluationRequest
            {
                BidId = winningBid.Id,
                CommercialScore = 90m,
                Note = "Commercial terms and delivery evidence reviewed.",
                RowVersion = detail.Header.RowVersion
            }));
        var materialRequestLineIds = await CreateApprovedMaterialRequestLinesAsync(fixture);
        var award = BatchAward(detail, winningBid.Id, materialRequestLineIds,
            "Complete scope and delivery confirmed");
        (await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/award-batch", award)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await LoginAsync("BGD");
        (await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/award",
            new RfqAwardRequest { BidId = winningBid.Id, Reason = award.Reason, RowVersion = detail.Header.RowVersion }))
            .StatusCode.Should().Be(HttpStatusCode.Gone);
        var awardKey = Guid.NewGuid().ToString();
        var awarded = await DetailAsync(await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/award-batch", award, awardKey));
        awarded.Header.Status.Should().Be(RfqStatus.Awarded);
        awarded.SelectedBidId.Should().Be(winningBid.Id);
        JsonDocument.Parse(awarded.AwardSnapshotJson!).RootElement.GetProperty("comparison").GetProperty("Header").GetProperty("Status").GetInt32().Should().Be(2);
        var contract = await WithDbAsync(db => db.Contracts.AsNoTracking().SingleAsync(x => x.Id == awarded.ContractId));
        contract.Direction.Should().Be(ContractDirection.Downstream);
        contract.Type.Should().Be(ContractType.Supply);
        contract.CustomerId.Should().Be(fixture.CustomerId);
        contract.OperationalProjectId.Should().Be(fixture.ProjectId);
        contract.VendorId.Should().Be(fixture.VendorId);
        contract.Value.Should().Be(14.15m);
        contract.Status.Should().Be(ContractStatus.Draft);
        (await WithDbAsync(db => db.ContractLines.CountAsync(x => x.ContractId == contract.Id))).Should().Be(2);
        var awardReplay = await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/award-batch", award, awardKey);
        (await DetailAsync(awardReplay)).ContractId.Should().Be(contract.Id);
        (await WithDbAsync(db => db.Contracts.CountAsync(x => x.OperationalProjectId == fixture.ProjectId))).Should().Be(1);
        (await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/award-batch", award)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var notified = await WithDbAsync(db => db.Notifications.Where(x => x.RefEntityType == "Rfq" && x.RefEntityId == detail.Header.Id)
            .Select(x => new { x.TemplateCode, x.UserId }).ToListAsync());
        notified.Count(x => x.TemplateCode == "procurement.rfq.issued").Should().Be(2);
        notified.Count(x => x.TemplateCode == "procurement.rfq.awarded").Should().Be(2);
        await LoginAsync("SUPER_ADMIN");
        var contractImpact = await Client.GetStringAsync($"/api/contracts/{contract.Id}/deletion-impact");
        contractImpact.Should().Contain("contract.rfqs").And.Contain("Block");
        using var signRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/contracts/{contract.Id}/transition")
        {
            Content = JsonContent.Create(new { newStatus = "Signed", rowVersion = CrmConcurrency.Encode(contract.RowVersion) }),
        };
        signRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var signed = await Client.SendAsync(signRequest);
        signed.IsSuccessStatusCode.Should().BeTrue(await signed.Content.ReadAsStringAsync());
        (await WithDbAsync(db => db.Contracts.SingleAsync(x => x.Id == contract.Id))).Status.Should().Be(ContractStatus.Signed);
        await LoginAsync("PROCUREMENT");
        var closed = await TransitionAsync(awarded, "close");
        closed.Header.Status.Should().Be(RfqStatus.Closed);
        (await SendAsync(fixture.ProjectId, $"/{closed.Header.Id}/cancel", new ProcurementTransitionRequest
        { RowVersion = closed.Header.RowVersion, Reason = "No longer needed" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadAsync(closed)).Events.Should().HaveCount(closed.Events.Count);
    }

    [Fact]
    public async Task Comparison_ReportsInactiveVendor_WithoutLosingPartialZeroPriceMinimum()
    {
        var fixture = await SeedAsync();
        await LoginAsync("PROCUREMENT");
        var detail = await TransitionAsync(await CreateAsync(fixture), "issue");
        detail = await SubmitAsync(detail, fixture.VendorId, 0m, 20m);
        detail = await SubmitAsync(detail, fixture.OtherVendorId, 0m, null);
        detail.Vendors.Should().OnlyContain(vendor => vendor.IsActive);
        detail.Lines[0].LowestUnitPrice.Should().Be(0m);

        await WithDbAsync(async db =>
        {
            (await db.Vendors.FindAsync(fixture.VendorId))!.IsActive = false;
            await db.SaveChangesAsync();
        });
        detail = await ReadAsync(detail);
        detail.Vendors.Single(vendor => vendor.Id == fixture.VendorId).IsActive.Should().BeFalse();
        detail.Vendors.Single(vendor => vendor.Id == fixture.OtherVendorId).IsActive.Should().BeTrue();
        detail.Lines[0].LowestUnitPrice.Should().Be(0m);
        detail.Lines[1].LowestUnitPrice.Should().BeNull();
        detail.Bids.Should().OnlyContain(bid => !bid.IsEligible && !bid.IsLowest);
        detail.Bids.Single(bid => bid.VendorId == fixture.OtherVendorId).IsComplete.Should().BeFalse();
        var references = await Client.GetAsync(Base(fixture.ProjectId) + "/references");
        references.EnsureSuccessStatusCode();
        var vendors = (await ReadJsonAsync(references)).GetProperty("vendors").EnumerateArray().ToList();
        vendors.Should().NotContain(vendor => vendor.GetProperty("id").GetInt32() == fixture.VendorId);
        vendors.Should().OnlyContain(vendor => vendor.GetProperty("isActive").GetBoolean());
    }

    [Theory]
    [InlineData("title")]
    [InlineData("long-title")]
    [InlineData("past-deadline")]
    [InlineData("owner")]
    [InlineData("empty-vendors")]
    [InlineData("duplicate-vendor")]
    [InlineData("inactive-vendor")]
    [InlineData("cross-project-boq")]
    [InlineData("unapproved-boq")]
    [InlineData("currency")]
    [InlineData("empty-lines")]
    [InlineData("duplicate-line")]
    [InlineData("cross-project-line")]
    [InlineData("zero-quantity")]
    [InlineData("over-limit")]
    [InlineData("quantity-precision")]
    [InlineData("note-length")]
    public async Task InvalidDraft_IsRejectedWithoutCreatingRfq(string partition)
    {
        var fixture = await SeedAsync();
        var draft = Draft(fixture);
        switch (partition)
        {
            case "title": draft.Title = "   "; break;
            case "long-title": draft.Title = new string('x', 201); break;
            case "past-deadline": draft.DueAt = DateTime.UtcNow.AddMinutes(-1); break;
            case "owner": draft.OwnerUserId = fixture.PmId; break;
            case "empty-vendors": draft.VendorIds.Clear(); break;
            case "duplicate-vendor": draft.VendorIds.Add(fixture.VendorId); break;
            case "inactive-vendor": await WithDbAsync(async db => { (await db.Vendors.FindAsync(fixture.VendorId))!.IsActive = false; await db.SaveChangesAsync(); }); break;
            case "cross-project-boq": draft.SourceBoqRevisionId = (await SeedAsync()).RevisionId; break;
            case "unapproved-boq": await WithDbAsync(async db => { (await db.ProjectBoqRevisions.FindAsync(fixture.RevisionId))!.Status = ProjectBoqRevisionStatus.Draft; await db.SaveChangesAsync(); }); break;
            case "currency": await WithDbAsync(async db => { (await db.ProjectBoqRevisions.FindAsync(fixture.RevisionId))!.Currency = "USD"; await db.SaveChangesAsync(); }); break;
            case "empty-lines": draft.Lines.Clear(); break;
            case "duplicate-line": draft.Lines.Add(draft.Lines[0]); break;
            case "cross-project-line": draft.Lines[0].ProjectBoqLineId = (await SeedAsync()).LineIds[0]; break;
            case "zero-quantity": draft.Lines[0].Quantity = 0; break;
            case "over-limit": draft.Lines[0].Quantity = 1.250001m; break;
            case "quantity-precision": draft.Lines[0].Quantity = 0.0000001m; break;
            case "note-length": draft.Note = new string('x', 2001); break;
        }
        await LoginAsync("PROCUREMENT");
        (await SendAsync(fixture.ProjectId, "", draft)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.Rfqs.CountAsync(x => x.OperationalProjectId == fixture.ProjectId))).Should().Be(0);
    }

    [Fact]
    public async Task DraftUpdates_RequireConcurrency_AndIssuedScopeCannotChange()
    {
        var fixture = await SeedAsync();
        await LoginAsync("PROCUREMENT");
        var detail = await CreateAsync(fixture);
        var draft = Draft(fixture);
        var url = $"/{detail.Header.Id}";
        (await SendAsync(fixture.ProjectId, url, draft, method: HttpMethod.Put)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        draft.RowVersion = detail.Header.RowVersion;
        draft.Title = "Revised package";
        var updated = await DetailAsync(await SendAsync(fixture.ProjectId, url, draft, method: HttpMethod.Put));
        updated.Header.Title.Should().Be("Revised package");
        updated.Lines.Select(x => x.Id).Should().Equal(detail.Lines.Select(x => x.Id));
        draft.Title = "Stale overwrite";
        (await SendAsync(fixture.ProjectId, url, draft, method: HttpMethod.Put)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadAsync(updated)).Header.Title.Should().Be("Revised package");
        updated = await TransitionAsync(updated, "issue");
        draft.RowVersion = updated.Header.RowVersion;
        (await SendAsync(fixture.ProjectId, url, draft, method: HttpMethod.Put)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadAsync(updated)).Header.RowVersion.Should().Be(updated.Header.RowVersion);
    }

    [Fact]
    public async Task ProjectScope_IsEnforcedOnReadWriteExportReferencesAndCachedReplay()
    {
        var fixture = await SeedAsync();
        var other = await SeedAsync();
        await LoginAsync("PROCUREMENT");
        var key = Guid.NewGuid().ToString();
        var draft = Draft(fixture);
        var detail = await DetailAsync(await SendAsync(fixture.ProjectId, "", draft, key));
        var unrelated = await CreateAsync(other);
        (await Client.GetAsync(Base(other.ProjectId) + $"/{detail.Header.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(other.ProjectId, $"/{detail.Header.Id}/issue", new ProcurementTransitionRequest { RowVersion = detail.Header.RowVersion }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        var list = await Client.GetStringAsync(Base(fixture.ProjectId));
        list.Should().Contain(detail.Header.Code).And.NotContain(unrelated.Header.Code);
        var export = await Client.GetStringAsync(Base(fixture.ProjectId) + "/export");
        export.Should().Contain(detail.Header.Code).And.NotContain(unrelated.Header.Code);
        await WithDbAsync(async db =>
        {
            var member = await db.OperationalProjectMembers.SingleAsync(x => x.OperationalProjectId == fixture.ProjectId && x.UserId == fixture.OwnerId);
            member.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });
        foreach (var suffix in new[] { "", "/export", "/references", $"/{detail.Header.Id}", $"/{detail.Header.Id}/export", $"/{detail.Header.Id}/documents/1/download" })
            (await Client.GetAsync(Base(fixture.ProjectId) + suffix)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(fixture.ProjectId, "", draft, key)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        Client.DefaultRequestHeaders.Authorization = null;
        (await Client.GetAsync(Base(other.ProjectId))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await LoginAsync("SALE");
        (await Client.GetAsync(Base(other.ProjectId))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_FiltersSortsPaginates_AndExportUsesSameSelection()
    {
        var fixture = await SeedAsync();
        await LoginAsync("PROCUREMENT");
        var first = await CreateAsync(fixture);
        var secondDraft = Draft(fixture);
        secondDraft.Title = "=SUM(1,1)";
        secondDraft.DueAt = first.Header.DueAt.AddDays(1);
        var second = await DetailAsync(await SendAsync(fixture.ProjectId, "", secondDraft));
        var list = await Client.GetFromJsonAsync<RfqListResponse>(Base(fixture.ProjectId) + "?sortBy=dueAt&sortDirection=asc&pageSize=1", JsonOptions);
        list!.Total.Should().Be(2);
        list.Items.Single().Id.Should().Be(first.Header.Id);
        var page = await Client.GetFromJsonAsync<RfqListResponse>(Base(fixture.ProjectId) + "?sortBy=dueAt&sortDirection=asc&pageSize=1&page=2", JsonOptions);
        page!.Items.Single().Id.Should().Be(second.Header.Id);
        var filter = $"?search=SUM&status=Draft&ownerUserId={fixture.OwnerId}&dueFrom={Uri.EscapeDataString(first.Header.DueAt.ToString("O"))}";
        (await Client.GetFromJsonAsync<RfqListResponse>(Base(fixture.ProjectId) + filter, JsonOptions))!.Items.Single().Id.Should().Be(second.Header.Id);
        var export = await Client.GetStringAsync(Base(fixture.ProjectId) + "/export" + filter);
        export.Should().Contain("'=SUM(1,1)").And.NotContain(first.Header.Code);
        (await Client.GetAsync(Base(fixture.ProjectId) + "?page=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.GetAsync(Base(fixture.ProjectId) + "?status=999")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.GetAsync(Base(fixture.ProjectId) + "?dueFrom=2030-01-02&dueTo=2030-01-01")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("cross-rfq-line")]
    [InlineData("uninvited-vendor")]
    [InlineData("negative-price")]
    [InlineData("precision")]
    [InlineData("empty-lines")]
    [InlineData("duplicate-lines")]
    [InlineData("payment-terms")]
    [InlineData("lead-time")]
    [InlineData("validity")]
    [InlineData("overflow")]
    [InlineData("wrong-document")]
    public async Task InvalidBid_PreservesRfqAndHistory(string partition)
    {
        var fixture = await SeedAsync();
        await LoginAsync("PROCUREMENT");
        var detail = await TransitionAsync(await CreateAsync(fixture), "issue");
        var bid = Bid(detail, fixture.VendorId, 2m, 3m);
        switch (partition)
        {
            case "cross-rfq-line": bid.Lines[0].RfqLineId = int.MaxValue; break;
            case "uninvited-vendor": bid.VendorId = (await SeedAsync()).VendorId; break;
            case "negative-price": bid.Lines[0].UnitPrice = -1; break;
            case "precision": bid.Lines[0].UnitPrice = 1.00001m; break;
            case "empty-lines": bid.Lines.Clear(); break;
            case "duplicate-lines": bid.Lines.Add(bid.Lines[0]); break;
            case "payment-terms": bid.PaymentTerms = "  "; break;
            case "lead-time": bid.LeadTimeDays = -1; break;
            case "validity": bid.ValidUntil = detail.Header.DueAt.AddSeconds(-1); break;
            case "overflow": bid.Lines[0].UnitPrice = 1000000000000m; break;
            case "wrong-document": bid.DocumentIds.Add(long.MaxValue); break;
        }
        (await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/bids", bid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var unchanged = await ReadAsync(detail);
        unchanged.Header.RowVersion.Should().Be(detail.Header.RowVersion);
        unchanged.Bids.Should().BeEmpty();
        unchanged.Events.Should().HaveCount(detail.Events.Count);
    }

    [Fact]
    public async Task WithdrawalAndExpiredBid_CannotBeAwarded_AndZeroPricesRemainExplicit()
    {
        var fixture = await SeedAsync();
        await LoginAsync("PROCUREMENT");
        var detail = await TransitionAsync(await CreateAsync(fixture), "issue");
        detail = await SubmitAsync(detail, fixture.VendorId, 0m, 0m);
        detail.Bids.Single().Total.Should().Be(0);
        detail.Lines.Should().OnlyContain(x => x.LowestUnitPrice == 0);
        var bidId = detail.Bids.Single().Id;
        detail = await DetailAsync(await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/bids/{bidId}/withdraw", new ProcurementTransitionRequest
        { RowVersion = detail.Header.RowVersion, Reason = new string('R', 2000) }));
        detail.Events.Single(x => x.Action == "bid-withdrawn").Reason.Should().HaveLength(2000);
        detail.Bids.Single().IsEligible.Should().BeFalse();
        detail.Lines.Should().OnlyContain(x => x.LowestUnitPrice == null);
        detail = await SubmitAsync(detail, fixture.OtherVendorId, 10m, 20m);
        detail = await TransitionAsync(detail, "evaluate");
        var currentId = detail.Bids.Single(x => x.VendorId == fixture.OtherVendorId).Id;
        await WithDbAsync(async db => { (await db.RfqBids.FindAsync(currentId))!.ValidUntil = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync(); });
        await LoginAsync("BGD");
        foreach (var rejectedId in new[] { bidId, currentId })
            (await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/award-batch", new RfqBatchAwardRequest
            {
                RowVersion = detail.Header.RowVersion,
                Reason = "Attempt invalid award",
                Lines = detail.Lines.Select(line => new RfqBatchAwardLineRequest
                {
                    BidId = rejectedId,
                    RfqLineId = line.Id,
                    Quantity = line.Quantity,
                    ContractType = ContractType.Supply,
                    MaterialRequestAllocations = [new() { MaterialRequestLineId = int.MaxValue, Quantity = line.Quantity }],
                }).ToList(),
            })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.Contracts.CountAsync(x => x.OperationalProjectId == fixture.ProjectId))).Should().Be(0);
        (await ReadAsync(detail)).Header.RowVersion.Should().Be(detail.Header.RowVersion);
    }

    [Fact]
    public async Task Files_AreProjectScoped_AndLinkedEvidenceBlocksDeletion()
    {
        var fixture = await SeedAsync();
        var other = await SeedAsync();
        var fileId = await AddDocumentAsync(fixture.ProjectId);
        var wrongFileId = await AddDocumentAsync(other.ProjectId);
        await LoginAsync("PROCUREMENT");
        var detail = await CreateAsync(fixture);
        (await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/documents", new RfqAttachDocumentRequest
        { DocumentId = wrongFileId, RowVersion = detail.Header.RowVersion })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        detail = await DetailAsync(await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/documents", new RfqAttachDocumentRequest
        { DocumentId = fileId, RowVersion = detail.Header.RowVersion }));
        detail.Documents.Single().Id.Should().Be(fileId);
        (await WithDbAsync(db => db.ProjectDocuments.SingleAsync(x => x.Id == fileId))).SourceRecordId.Should().Be(detail.Header.Id);
        var references = await Client.GetFromJsonAsync<RfqReferenceResponse>(Base(fixture.ProjectId) + "/references", JsonOptions);
        references!.Documents.Should().NotContain(x => x.Id == fileId || x.Id == wrongFileId);
        (await Client.GetAsync(Base(other.ProjectId) + $"/{detail.Header.Id}/documents/{fileId}/download")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var scope = Factory.Services.CreateScope();
        var documentService = scope.ServiceProvider.GetRequiredService<IProjectDocumentService>();
        var deletion = () => documentService.DeleteAsync(fixture.ProjectId, fileId, fixture.OwnerId, true);
        await deletion.Should().ThrowAsync<ProjectDocumentValidationException>();
        await LoginAsync("SUPER_ADMIN");
        var impact = await Client.GetStringAsync($"/api/vendors/{fixture.VendorId}/deletion-impact");
        impact.Should().Contain("vendor.rfqs").And.Contain("Block");
        (await WithDbAsync(db => db.ProjectDocuments.SingleAsync(x => x.Id == fileId))).DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Overdue_NotificationsAreOncePerRfq_AndClosedProjectsRejectWrites()
    {
        var fixture = await SeedAsync();
        await LoginAsync("PROCUREMENT");
        var detail = await TransitionAsync(await CreateAsync(fixture), "issue");
        await WithDbAsync(async db => { (await db.Rfqs.FindAsync(detail.Header.Id))!.DueAt = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(); });
        for (var run = 0; run < 2; run++)
        {
            using var scope = Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<RfqService>().NotifyOverdueAsync(default);
        }
        (await WithDbAsync(db => db.Notifications.CountAsync(x => x.RefEntityType == "Rfq" && x.RefEntityId == detail.Header.Id &&
            x.TemplateCode == "procurement.rfq.overdue"))).Should().Be(2);
        var list = await Client.GetFromJsonAsync<RfqListResponse>(Base(fixture.ProjectId) + "?overdue=true", JsonOptions);
        list!.Items.Single().Overdue.Should().BeTrue();
        detail = await ReadAsync(detail);
        (await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/bids", Bid(detail, fixture.VendorId, 1, 2))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await WithDbAsync(async db => { (await db.OperationalProjects.FindAsync(fixture.ProjectId))!.Status = OperationalProjectStatus.Completed; await db.SaveChangesAsync(); });
        (await SendAsync(fixture.ProjectId, "", Draft(fixture))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("owner-left")]
    [InlineData("owner-inactive")]
    [InlineData("boq-replaced")]
    [InlineData("boq-replaced-other-currency")]
    public async Task Award_RevalidatesDependencies_AndPreservesStateAfterRejection(string change)
    {
        var fixture = await SeedAsync();
        await LoginAsync("PROCUREMENT");
        var detail = await SubmitAsync(await TransitionAsync(await CreateAsync(fixture), "issue"), fixture.VendorId, 10, 20);
        detail = await TransitionAsync(detail, "evaluate");
        detail = await DetailAsync(await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/bids/evaluate",
            new RfqBidEvaluationRequest
            {
                BidId = detail.Bids.Single().Id,
                CommercialScore = 80m,
                Note = "Commercial evidence reviewed before award.",
                RowVersion = detail.Header.RowVersion
            }));
        var materialRequestLineIds = await CreateApprovedMaterialRequestLinesAsync(fixture);
        var award = BatchAward(detail, detail.Bids.Single().Id, materialRequestLineIds,
            "Selected after evaluation");
        await WithDbAsync(async db =>
        {
            if (change == "owner-left")
                (await db.OperationalProjectMembers.SingleAsync(x => x.OperationalProjectId == fixture.ProjectId && x.UserId == fixture.OwnerId)).EndedAt = DateTime.UtcNow;
            if (change == "owner-inactive") (await db.Users.FindAsync(fixture.OwnerId))!.IsActive = false;
            if (change.StartsWith("boq-replaced", StringComparison.Ordinal)) db.ProjectBoqRevisions.Add(new ProjectBoqRevision
            {
                OperationalProjectId = fixture.ProjectId,
                Currency = change == "boq-replaced-other-currency" ? "USD" : "VND",
                Status = ProjectBoqRevisionStatus.Approved,
                RevisionNumber = 2,
                ApprovedAt = DateTime.UtcNow.AddMinutes(1),
                PreparedByUserId = fixture.PmId,
                Lines = [new() { ItemCode = "REVISED", Description = "Revised scope", Unit = "m3", ApprovedQuantity = 1, BudgetUnitPrice = 5 }],
            });
            await db.SaveChangesAsync();
        });
        try
        {
            await LoginAsync("BGD");
            (await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/award-batch", award))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var unchanged = await ReadAsync(detail);
            unchanged.Header.RowVersion.Should().Be(detail.Header.RowVersion);
            unchanged.ContractId.Should().BeNull();
            unchanged.AwardSnapshotJson.Should().BeNull();
            (await WithDbAsync(db => db.Contracts.CountAsync(x => x.OperationalProjectId == fixture.ProjectId))).Should().Be(0);
        }
        finally
        {
            if (change == "owner-inactive") await WithDbAsync(async db => { (await db.Users.FindAsync(fixture.OwnerId))!.IsActive = true; await db.SaveChangesAsync(); });
        }
    }

    [Fact]
    public async Task VendorPortal_TokenScopesReadAndCreatesImmutableForeignCurrencyRevision()
    {
        var fixture = await SeedAsync();
        var fileId = await AddDocumentAsync(fixture.ProjectId);
        var wrongFileId = await AddDocumentAsync(fixture.ProjectId);
        await LoginAsync("PROCUREMENT");
        var detail = await CreateAsync(fixture);
        detail = await DetailAsync(await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/documents",
            new RfqAttachDocumentRequest { DocumentId = fileId, RowVersion = detail.Header.RowVersion }));
        detail = await TransitionAsync(detail, "issue");
        const string token = "rfq-portal-test-token";
        await WithDbAsync(async db =>
        {
            var invitation = await db.RfqInvitations.SingleAsync(x =>
                x.RfqId == detail.Header.Id && x.VendorId == fixture.OtherVendorId);
            invitation.PortalTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
            invitation.PortalTokenExpiresAt = DateTime.UtcNow.AddDays(2);
            await db.SaveChangesAsync();
        });

        Client.DefaultRequestHeaders.Authorization = null;
        using var portalRequest = new HttpRequestMessage(HttpMethod.Get, "/api/vendor-rfqs");
        portalRequest.Headers.Add("X-RFQ-Portal-Token", token);
        var portalResponse = await Client.SendAsync(portalRequest);
        portalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var portalJson = await portalResponse.Content.ReadAsStringAsync();
        portalJson.Should().Contain(detail.Header.Code)
            .And.Contain("supplier-quotation.pdf")
            .And.NotContain("budgetUnitPrice")
            .And.NotContain("customerName")
            .And.NotContain("bids");
        using var wrongDownload = new HttpRequestMessage(HttpMethod.Get,
            $"/api/vendor-rfqs/documents/{wrongFileId}/download");
        wrongDownload.Headers.Add("X-RFQ-Portal-Token", token);
        (await Client.SendAsync(wrongDownload)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var portalKey = Guid.NewGuid().ToString();
        using var submit = new HttpRequestMessage(HttpMethod.Post, "/api/vendor-rfqs/bids")
        {
            Content = JsonContent.Create(new VendorPortalBidRequest
            {
                LeadTimeDays = 5,
                PaymentTerms = "Net 30 after delivery",
                ValidUntil = detail.Header.DueAt.AddDays(2),
                Currency = "USD",
                ExchangeRateToVnd = 25_000m,
                FreightAmount = 10m,
                DiscountPercent = 5m,
                VatPercent = 10m,
                Lines = detail.Lines.Select(x => new RfqBidLineRequest { RfqLineId = x.Id, UnitPrice = 2m }).ToList(),
            })
        };
        submit.Headers.Add("X-RFQ-Portal-Token", token);
        submit.Headers.Add("Idempotency-Key", portalKey);
        var submitted = await Client.SendAsync(submit);
        submitted.StatusCode.Should().Be(HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());
        var stored = await WithDbAsync(db => db.RfqBids.AsNoTracking().SingleAsync(x =>
            x.RfqId == detail.Header.Id && x.VendorId == fixture.OtherVendorId));
        stored.SubmittedViaPortal.Should().BeTrue();
        stored.Currency.Should().Be("USD");
        stored.ExchangeRateToVnd.Should().Be(25_000m);
        stored.TotalOriginal.Should().BeGreaterThan(stored.Subtotal);
        stored.Total.Should().Be(stored.TotalOriginal * 25_000m);
        (await WithDbAsync(db => db.Set<RfqEvent>().AsNoTracking().CountAsync(x =>
            x.RfqId == detail.Header.Id && x.Action == "portal-bid-submitted"))).Should().Be(1);
        (await WithDbAsync(db => db.Set<RfqEvent>().AsNoTracking().CountAsync(x =>
            x.RfqId == detail.Header.Id && x.Action == "bid-submitted"))).Should().Be(0);
        await LoginAsync("PROCUREMENT");
        var internalDetail = await ReadAsync(detail);
        var portalBid = internalDetail.Bids.Single(x => x.VendorId == fixture.OtherVendorId);
        portalBid.SubmittedViaPortal.Should().BeTrue();
        portalBid.SubmittedBy.Should().Contain("Alternative supplier").And.Contain("vendor portal");
        internalDetail.Events.Single(x => x.Action == "portal-bid-submitted").Actor
            .Should().StartWith("Alternative supplier").And.EndWith("(vendor portal)");
        Client.DefaultRequestHeaders.Authorization = null;

        const string otherToken = "other-rfq-portal-token";
        await WithDbAsync(async db =>
        {
            var invitation = await db.RfqInvitations.SingleAsync(x =>
                x.RfqId == detail.Header.Id && x.VendorId == fixture.VendorId);
            invitation.PortalTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(otherToken)));
            invitation.PortalTokenExpiresAt = DateTime.UtcNow.AddDays(2);
            await db.SaveChangesAsync();
        });
        using var crossTokenReplay = new HttpRequestMessage(HttpMethod.Post, "/api/vendor-rfqs/bids")
        {
            Content = JsonContent.Create(new VendorPortalBidRequest
            {
                LeadTimeDays = 5,
                PaymentTerms = "Net 30 after delivery",
                ValidUntil = detail.Header.DueAt.AddDays(2),
                Currency = "USD",
                ExchangeRateToVnd = 25_000m,
                FreightAmount = 10m,
                DiscountPercent = 5m,
                VatPercent = 10m,
                Lines = detail.Lines.Select(x => new RfqBidLineRequest { RfqLineId = x.Id, UnitPrice = 2m }).ToList(),
            })
        };
        crossTokenReplay.Headers.Add("X-RFQ-Portal-Token", otherToken);
        crossTokenReplay.Headers.Add("Idempotency-Key", portalKey);
        (await Client.SendAsync(crossTokenReplay)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var invalidRequest = new HttpRequestMessage(HttpMethod.Get, "/api/vendor-rfqs");
        invalidRequest.Headers.Add("X-RFQ-Portal-Token", "invalid-token");
        (await Client.SendAsync(invalidRequest)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BatchAward_SplitsLinesCreatesContractsAndBlocksMaterialRequestOverAllocation()
    {
        var fixture = await SeedAsync();
        var materialRequestLineIds = await WithDbAsync(async db =>
        {
            var request = new MaterialRequest
            {
                OperationalProjectId = fixture.ProjectId,
                Code = UniqueSlug("MR-RFQ-AWARD"),
                Status = MaterialRequestStatus.Approved,
                SiteRequesterUserId = fixture.PmId,
                ResponsibleSiteUserId = fixture.PmId,
                AssignedProcurementUserId = fixture.OwnerId,
                RequiredAt = DateTime.UtcNow.AddDays(5),
                ApprovedAt = DateTime.UtcNow,
                Lines =
                [
                    new() { ProjectBoqLineId = fixture.LineIds[0], RequestedQuantity = 1.25m },
                    new() { ProjectBoqLineId = fixture.LineIds[1], RequestedQuantity = 2m },
                ],
            };
            db.MaterialRequests.Add(request);
            await db.SaveChangesAsync();
            return request.Lines.Select(x => x.Id).ToArray();
        });

        await LoginAsync("PROCUREMENT");
        var detail = await TransitionAsync(await CreateAsync(fixture), "issue");
        detail = await SubmitAsync(detail, fixture.VendorId, 10m, 20m);
        detail = await SubmitAsync(detail, fixture.OtherVendorId, 8m, null);
        detail = await TransitionAsync(detail, "evaluate");
        var unevaluatedAward = new RfqBatchAwardRequest
        {
            Reason = "Must not award before commercial evaluation.",
            RowVersion = detail.Header.RowVersion,
            Lines =
            [
                new() { BidId = detail.Bids.Single(x => x.VendorId == fixture.VendorId).Id, RfqLineId = detail.Lines[0].Id, Quantity = 1.25m,
                    ContractType = ContractType.Supply, MaterialRequestAllocations = [new() { MaterialRequestLineId = materialRequestLineIds[0], Quantity = 1.25m }] },
                new() { BidId = detail.Bids.Single(x => x.VendorId == fixture.VendorId).Id, RfqLineId = detail.Lines[1].Id, Quantity = 2m,
                    ContractType = ContractType.Supply, MaterialRequestAllocations = [new() { MaterialRequestLineId = materialRequestLineIds[1], Quantity = 2m }] },
            ],
        };
        await LoginAsync("BGD");
        (await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/award-batch", unevaluatedAward)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.Contracts.CountAsync(x => x.OperationalProjectId == fixture.ProjectId))).Should().Be(0);
        await LoginAsync("PROCUREMENT");
        foreach (var bid in detail.Bids.Where(x => x.IsCurrent))
            detail = await DetailAsync(await SendAsync(fixture.ProjectId, $"/{detail.Header.Id}/bids/evaluate",
                new RfqBidEvaluationRequest
                {
                    BidId = bid.Id,
                    CommercialScore = bid.VendorId == fixture.VendorId ? 90m : 70m,
                    Note = "Commercial terms reviewed by Procurement.",
                    RowVersion = detail.Header.RowVersion
                }));

        var vendorBid = detail.Bids.Single(x => x.VendorId == fixture.VendorId && x.IsCurrent);
        var otherBid = detail.Bids.Single(x => x.VendorId == fixture.OtherVendorId && x.IsCurrent);
        otherBid.IsComplete.Should().BeFalse();
        otherBid.WeightedScore.Should().NotBeNull("a partial quotation must be scoreable for its quoted split-award line");
        var request = new RfqBatchAwardRequest
        {
            Reason = "Split delivery capacity and commercial risk.",
            OverrideReason = "Two suppliers are required to meet the delivery window.",
            RowVersion = detail.Header.RowVersion,
            Lines =
            [
                new() { BidId = vendorBid.Id, RfqLineId = detail.Lines[0].Id, Quantity = .5m,
                    ContractType = ContractType.Supply, MaterialRequestAllocations = [new() { MaterialRequestLineId = materialRequestLineIds[0], Quantity = .5m }] },
                new() { BidId = otherBid.Id, RfqLineId = detail.Lines[0].Id, Quantity = .75m,
                    ContractType = ContractType.Supply, MaterialRequestAllocations = [new() { MaterialRequestLineId = materialRequestLineIds[0], Quantity = .75m }] },
                new() { BidId = vendorBid.Id, RfqLineId = detail.Lines[1].Id, Quantity = 2m,
                    ContractType = ContractType.Supply, MaterialRequestAllocations = [new() { MaterialRequestLineId = materialRequestLineIds[1], Quantity = 2m }] },
            ],
        };

        await LoginAsync("BGD");
        var awarded = await DetailAsync(await SendAsync(fixture.ProjectId,
            $"/{detail.Header.Id}/award-batch", request));
        awarded.Header.Status.Should().Be(RfqStatus.Awarded);
        awarded.Awards.Should().HaveCount(2);
        var awards = awarded.Awards!;
        awards.Select(x => x.ContractId).Should().OnlyHaveUniqueItems();
        awards.SelectMany(x => x.Lines).Sum(x => x.MaterialRequestAllocations.Sum(a => a.Quantity)).Should().Be(3.25m);
        (await WithDbAsync(db => db.Contracts.AsNoTracking().CountAsync(x => x.OperationalProjectId == fixture.ProjectId))).Should().Be(2);
        (await WithDbAsync(db => db.RfqAwardMaterialRequestAllocations.AsNoTracking()
            .Where(x => materialRequestLineIds.Contains(x.MaterialRequestLineId)).SumAsync(x => x.Quantity))).Should().Be(3.25m);

        var secondFixture = await SeedAsync();
        var secondDetail = await LoginCreateIssueBidEvaluateAsync(secondFixture);
        var invalid = new RfqBatchAwardRequest
        {
            Reason = "Invalid over-allocation attempt.",
            RowVersion = secondDetail.Header.RowVersion,
            Lines = secondDetail.Lines.Select((line, index) => new RfqBatchAwardLineRequest
            {
                BidId = secondDetail.Bids.Single().Id,
                RfqLineId = line.Id,
                Quantity = line.Quantity,
                ContractType = ContractType.Supply,
                MaterialRequestAllocations = [new() { MaterialRequestLineId = materialRequestLineIds[Math.Min(index, 1)], Quantity = line.Quantity }],
            }).ToList(),
        };
        await LoginAsync("BGD");
        (await SendAsync(secondFixture.ProjectId, $"/{secondDetail.Header.Id}/award-batch", invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.Contracts.CountAsync(x => x.OperationalProjectId == secondFixture.ProjectId))).Should().Be(0);
    }

    private async Task<RfqDetailResponse> LoginCreateIssueBidEvaluateAsync(Fixture fixture)
    {
        await LoginAsync("PROCUREMENT");
        var detail = await TransitionAsync(await CreateAsync(fixture), "issue");
        detail = await SubmitAsync(detail, fixture.VendorId, 10m, 20m);
        return await TransitionAsync(detail, "evaluate");
    }

    private sealed record Fixture(int ProjectId, int CustomerId, int OwnerId, int PmId, int RevisionId, int[] LineIds, int VendorId, int OtherVendorId);

    private Task<Fixture> SeedAsync() => WithDbAsync(async db =>
    {
        var owner = await db.Users.SingleAsync(x => x.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PROCUREMENT"]);
        var pm = await db.Users.SingleAsync(x => x.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"]);
        var customer = new Customer
        {
            Name = "RFQ customer",
            Type = CustomerType.Individual,
            SourceCode = "referral",
            Contacts = [new() { FullName = "RFQ contact", Phone = "0912345678", IsPrimary = true }]
        };
        var project = new OperationalProject { Code = UniqueSlug("PJ-RFQ"), Name = "Factory procurement", Customer = customer, ProjectManagerUserId = pm.Id };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        db.OperationalProjectMembers.Add(new OperationalProjectMember
        {
            OperationalProjectId = project.Id,
            UserId = owner.Id,
            Position = "Procurement",
            StartedAt = DateTime.UtcNow,
            CreatedByUserId = pm.Id,
            UpdatedByUserId = pm.Id
        });
        var revision = new ProjectBoqRevision
        {
            OperationalProjectId = project.Id,
            RevisionNumber = 1,
            Currency = "VND",
            Status = ProjectBoqRevisionStatus.Approved,
            PreparedByUserId = pm.Id,
            ApprovedByUserId = pm.Id,
            ApprovedAt = DateTime.UtcNow,
            Lines = [new() { ItemCode = "CONCRETE", Description = "Ready mixed concrete", Unit = "m3", ApprovedQuantity = 1.25m, BudgetUnitPrice = 30m },
                new() { ItemCode = "STEEL", Description = "Reinforcement steel", Unit = "ton", ApprovedQuantity = 2m, BudgetUnitPrice = 50m }]
        };
        db.ProjectBoqRevisions.Add(revision);
        var vendor = new Vendor
        {
            VendorCode = UniqueSlug("NCC"),
            CompanyName = UniqueSlug("Concrete supplier"),
            VendorType = VendorType.Supplier,
            IsActive = true,
            Phone = "0912345678",
            CreatedByUserId = owner.Id
        };
        var other = new Vendor
        {
            VendorCode = UniqueSlug("NCC"),
            CompanyName = UniqueSlug("Alternative supplier"),
            VendorType = VendorType.Both,
            IsActive = true,
            Email = "quotes@example.test",
            CreatedByUserId = owner.Id
        };
        db.Vendors.AddRange(vendor, other);
        await db.SaveChangesAsync();
        return new Fixture(project.Id, customer.Id, owner.Id, pm.Id, revision.Id, revision.Lines.Select(x => x.Id).ToArray(), vendor.Id, other.Id);
    });

    private Task<long> AddDocumentAsync(int projectId) => WithDbAsync(async db =>
    {
        var document = new ProjectDocument
        {
            OperationalProjectId = projectId,
            Category = ProjectDocumentCategory.Procurement,
            OriginalFileName = "supplier-quotation.pdf",
            ContentType = "application/pdf",
            LocalPath = $"/files/projects/{projectId}/quotation.pdf"
        };
        db.ProjectDocuments.Add(document);
        await db.SaveChangesAsync();
        return document.Id;
    });

    private Task<int[]> CreateApprovedMaterialRequestLinesAsync(Fixture fixture) => WithDbAsync(async db =>
    {
        var request = new MaterialRequest
        {
            OperationalProjectId = fixture.ProjectId,
            Code = UniqueSlug("MR-RFQ-COVERAGE"),
            Status = MaterialRequestStatus.Approved,
            SiteRequesterUserId = fixture.PmId,
            ResponsibleSiteUserId = fixture.PmId,
            AssignedProcurementUserId = fixture.OwnerId,
            RequiredAt = DateTime.UtcNow.AddDays(5),
            ApprovedAt = DateTime.UtcNow,
            Lines =
            [
                new() { ProjectBoqLineId = fixture.LineIds[0], RequestedQuantity = 1.25m },
                new() { ProjectBoqLineId = fixture.LineIds[1], RequestedQuantity = 2m },
            ],
        };
        db.MaterialRequests.Add(request);
        await db.SaveChangesAsync();
        return request.Lines.Select(x => x.Id).ToArray();
    });

    private static RfqBatchAwardRequest BatchAward(RfqDetailResponse detail, int bidId,
        IReadOnlyList<int> materialRequestLineIds, string reason) => new()
        {
            Reason = reason,
            RowVersion = detail.Header.RowVersion,
            Lines = detail.Lines.Select((line, index) => new RfqBatchAwardLineRequest
            {
                BidId = bidId,
                RfqLineId = line.Id,
                ContractType = ContractType.Supply,
                Quantity = line.Quantity,
                MaterialRequestAllocations =
                [
                    new() { MaterialRequestLineId = materialRequestLineIds[index], Quantity = line.Quantity },
            ],
            }).ToList(),
        };

    private static RfqUpsertRequest Draft(Fixture fixture) => new()
    {
        Title = "Concrete and steel package",
        SourceBoqRevisionId = fixture.RevisionId,
        OwnerUserId = fixture.OwnerId,
        DueAt = DateTime.UtcNow.AddDays(10),
        VendorIds = [fixture.VendorId, fixture.OtherVendorId],
        Lines = [new() { ProjectBoqLineId = fixture.LineIds[0], Quantity = 1.25m }, new() { ProjectBoqLineId = fixture.LineIds[1], Quantity = 2m }],
    };
    private static RfqBidRequest Bid(RfqDetailResponse detail, int vendorId, decimal first, decimal? second) => new()
    {
        VendorId = vendorId,
        RowVersion = detail.Header.RowVersion,
        PaymentTerms = "30% advance, 70% after delivery",
        LeadTimeDays = 7,
        ValidUntil = detail.Header.DueAt.AddDays(10),
        Lines = new[] { new RfqBidLineRequest { RfqLineId = detail.Lines[0].Id, UnitPrice = first },
            second.HasValue ? new RfqBidLineRequest { RfqLineId = detail.Lines[1].Id, UnitPrice = second.Value } : null }.Where(x => x is not null).Select(x => x!).ToList(),
    };
    private async Task LoginAsync(string role)
    {
        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));
    }
    private static string Base(int projectId) => $"/api/operational-projects/{projectId}/procurement/rfqs";
    private async Task<HttpResponseMessage> SendAsync(int projectId, string suffix, object body, string? key = null, HttpMethod? method = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, Base(projectId) + suffix) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await Client.SendAsync(request);
    }
    private static async Task<RfqDetailResponse> DetailAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);
        return JsonSerializer.Deserialize<RfqDetailResponse>(body, JsonOptions)!;
    }
    private async Task<RfqDetailResponse> CreateAsync(Fixture fixture) => await DetailAsync(await SendAsync(fixture.ProjectId, "", Draft(fixture)));
    private async Task<RfqDetailResponse> ReadAsync(RfqDetailResponse detail) => await DetailAsync(await Client.GetAsync(Base(detail.Header.OperationalProjectId) + $"/{detail.Header.Id}"));
    private async Task<RfqDetailResponse> TransitionAsync(RfqDetailResponse detail, string action) => await DetailAsync(await SendAsync(detail.Header.OperationalProjectId,
        $"/{detail.Header.Id}/{action}", new ProcurementTransitionRequest { RowVersion = detail.Header.RowVersion }));
    private async Task<RfqDetailResponse> SubmitAsync(RfqDetailResponse detail, int vendorId, decimal first, decimal? second) =>
        await DetailAsync(await SendAsync(detail.Header.OperationalProjectId, $"/{detail.Header.Id}/bids", Bid(detail, vendorId, first, second)));
}
