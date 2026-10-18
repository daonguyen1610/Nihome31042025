using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class ConstructionProcurementControllerTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task MultipleRequests_CanUseSameContractAndMissingTokenDoesNotMutate()
    {
        var f = await SeedAsync();
        var other = await WithDbAsync(async db =>
        {
            var original = await db.ConstructionResourceRequests.FindAsync(f.RequestId);
            var row = new ConstructionResourceRequest
            {
                OperationalProjectId = f.ProjectId, Code = $"RR-{f.ProjectId}-CREW",
                Type = ConstructionResourceType.Labour, Status = ConstructionReviewStatus.CmApproved,
                CreatedByUserId = original!.CreatedByUserId, WbsCode = "STRUCTURE-03", Quantity = 12,
                Unit = "person", Reason = "Structural assembly crew for crane lifting operations", NeedByDate = original.NeedByDate,
            };
            db.ConstructionResourceRequests.Add(row); await db.SaveChangesAsync(); return row.Id;
        });
        await Login("PROCUREMENT");
        using var missing = await Post($"{Path(f.ProjectId)}/{other}/contract", new { contractId = f.ContractId });
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        foreach (var id in new[] { f.RequestId, other })
        {
            using var linked = await Post($"{Path(f.ProjectId)}/{id}/contract", new { contractId = f.ContractId, rowVersion = await Version(id) });
            linked.EnsureSuccessStatusCode();
        }
        (await WithDbAsync(db => db.ConstructionResourceRequests.CountAsync(x => x.ProcurementContractId == f.ContractId))).Should().Be(2);
        (await WithDbAsync(db => db.Set<ConstructionResourceHandoffEvent>().CountAsync(x => x.ConstructionResourceRequestId == other))).Should().Be(1);
    }

    [Fact]
    public async Task Procurement_CreatesOnlyDownstreamDraftWithReplayWithoutGlobalContractManagement()
    {
        var f = await SeedAsync();
        var vendor = await WithDbAsync(db => db.Contracts.Where(x => x.Id == f.ContractId).Select(x => x.VendorId).SingleAsync());
        await Login("PROCUREMENT");
        var payload = new { vendorId = vendor, type = "Subcontract", value = 120_000_000m, scopeOfWork = "Tower crane mobilisation for structural works" };
        var key = Guid.NewGuid().ToString("N");
        using var first = await Post($"{Path(f.ProjectId)}/contracts", payload, key);
        first.EnsureSuccessStatusCode();
        var created = await ReadJsonAsync(first);
        created.GetProperty("direction").GetString().Should().Be("Downstream");
        created.GetProperty("status").GetString().Should().Be("Draft");
        created.GetProperty("ownerUserId").GetInt32().Should().Be(f.ProcurementId);
        using var replay = await Post($"{Path(f.ProjectId)}/contracts", payload, key);
        replay.EnsureSuccessStatusCode();
        (await ReadJsonAsync(replay)).GetProperty("id").GetInt32().Should().Be(created.GetProperty("id").GetInt32());
        using var invalidType = await Post($"{Path(f.ProjectId)}/contracts", new { vendorId = vendor, type = "Design", value = 100 });
        invalidType.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var missingValue = await Post($"{Path(f.ProjectId)}/contracts", new { vendorId = vendor, type = "Subcontract" });
        missingValue.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var upstream = await Post("/api/contracts", new { type = "Design", direction = "Upstream", value = 100 });
        upstream.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.Contracts.CountAsync(x => x.OperationalProjectId == f.ProjectId))).Should().Be(2);
    }

    [Fact]
    public async Task LinkedContract_CannotChangeBusinessTypeOrDestroyResourceHistory()
    {
        var f = await SeedAsync();
        await Login("PROCUREMENT");
        using var linked = await Post($"{Path(f.ProjectId)}/{f.RequestId}/contract", new { contractId = f.ContractId, rowVersion = await Version(f.RequestId) });
        linked.EnsureSuccessStatusCode();
        var payload = await WithDbAsync(async db =>
        {
            var contract = await db.Contracts.FindAsync(f.ContractId);
            return new { customerId = contract!.CustomerId, direction = "Downstream", type = "Supply", vendorId = contract.VendorId,
                operationalProjectId = f.ProjectId, status = "Signed", signedDate = contract.SignedDate, value = contract.Value, rowVersion = Convert.ToBase64String(contract.RowVersion) };
        });
        await Login("SUPER_ADMIN");
        using var message = new HttpRequestMessage(HttpMethod.Put, $"/api/contracts/{f.ContractId}") { Content = JsonContent.Create(payload) };
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var rejected = await Client.SendAsync(message);
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await rejected.Content.ReadAsStringAsync()).Should().Contain("linked to construction resource requests");
        (await WithDbAsync(db => db.Contracts.Where(x => x.Id == f.ContractId).Select(x => x.Type).SingleAsync())).Should().Be(ContractType.Subcontract);
        (await WithDbAsync(db => db.Set<ConstructionResourceHandoffEvent>().CountAsync(x => x.ConstructionResourceRequestId == f.RequestId))).Should().Be(1);
    }

    [Fact]
    public async Task CmApproval_ProcurementLinkReplayAndSupply_PreserveAuditAndLockContract()
    {
        var f = await SeedAsync(ConstructionReviewStatus.PendingCmReview);
        await Login("PM");
        using var decision = await Post($"/api/operational-projects/{f.ProjectId}/construction/resource-requests/{f.RequestId}/cm-decision",
            new { approved = true, reason = "Tower crane allocation checked against structural programme", rowVersion = await Version(f.RequestId) });
        decision.EnsureSuccessStatusCode();
        await Login("PROCUREMENT");
        using var list = await Client.GetAsync(Path(f.ProjectId));
        list.EnsureSuccessStatusCode();
        (await ReadJsonAsync(list)).EnumerateArray().Should().Contain(x => x.GetProperty("id").GetInt32() == f.RequestId);
        var payload = new { contractId = f.ContractId, rowVersion = await Version(f.RequestId) };
        var key = Guid.NewGuid().ToString("N");
        using var linked = await Post($"{Path(f.ProjectId)}/{f.RequestId}/contract", payload, key);
        linked.EnsureSuccessStatusCode();
        using var replay = await Post($"{Path(f.ProjectId)}/{f.RequestId}/contract", payload, key);
        replay.EnsureSuccessStatusCode();
        replay.Headers.GetValues("Idempotency-Replayed").Should().Contain("true");
        var beforeSupply = await Version(f.RequestId);
        var supplyPayload = new { documentId = f.DocumentId, rowVersion = beforeSupply };
        var supplyKey = Guid.NewGuid().ToString("N");
        using var supplied = await Post($"{Path(f.ProjectId)}/{f.RequestId}/supply", supplyPayload, supplyKey);
        supplied.EnsureSuccessStatusCode();
        using var supplyReplay = await Post($"{Path(f.ProjectId)}/{f.RequestId}/supply", supplyPayload, supplyKey);
        supplyReplay.EnsureSuccessStatusCode();
        supplyReplay.Headers.GetValues("Idempotency-Replayed").Should().Contain("true");
        var body = await ReadJsonAsync(supplied);
        body.GetProperty("suppliedAt").ValueKind.Should().Be(JsonValueKind.String);
        body.GetProperty("history").GetArrayLength().Should().Be(2);
        body.GetProperty("suppliedByUserId").GetInt32().Should().Be(f.ProcurementId);
        using var change = await Post($"{Path(f.ProjectId)}/{f.RequestId}/contract", new { contractId = f.ContractId, rowVersion = await Version(f.RequestId) });
        change.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.Set<ConstructionResourceHandoffEvent>().CountAsync(x => x.ConstructionResourceRequestId == f.RequestId))).Should().Be(2);
        (await WithDbAsync(db => db.PaymentRequests.AnyAsync(x => x.ContractId == f.ContractId))).Should().BeFalse();
        await Login("SUPER_ADMIN");
        using var impact = await Client.GetAsync($"/api/contracts/{f.ContractId}/deletion-impact");
        impact.EnsureSuccessStatusCode();
        (await ReadJsonAsync(impact)).GetProperty("canDelete").GetBoolean().Should().BeFalse();
        using var deleteDocument = await Client.DeleteAsync($"/api/operational-projects/{f.ProjectId}/documents/{f.DocumentId}");
        deleteDocument.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ProjectDocuments.Where(x => x.Id == f.DocumentId).Select(x => x.DeletedAt).SingleAsync())).Should().BeNull();
        // Retaining supply evidence must not prevent recovery of a failed Drive upload.
        await WithDbAsync(async db =>
        {
            var document = await db.ProjectDocuments.FindAsync(f.DocumentId);
            document!.SyncStatus = ProjectDocumentSyncStatus.Failed;
            document.DesiredOperation = ProjectDocumentDesiredOperation.Upsert;
            await db.SaveChangesAsync();
        });
        using var retry = await Post($"/api/operational-projects/{f.ProjectId}/documents/{f.DocumentId}/retry", new { });
        retry.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RelinkBeforeSupply_RecordsPreviousContractAndRejectsStaleToken()
    {
        var f = await SeedAsync();
        await Login("PROCUREMENT");
        var stale = await Version(f.RequestId);
        using var linked = await Post($"{Path(f.ProjectId)}/{f.RequestId}/contract", new { contractId = f.ContractId, rowVersion = stale });
        linked.EnsureSuccessStatusCode();
        using var conflict = await Post($"{Path(f.ProjectId)}/{f.RequestId}/contract", new { contractId = f.ContractId, rowVersion = stale });
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var second = await WithDbAsync(async db =>
        {
            var original = await db.Contracts.FindAsync(f.ContractId);
            var next = new Contract { CustomerId = original!.CustomerId, OperationalProjectId = f.ProjectId,
                ContractNumber = $"HD-MACHINE-{Guid.NewGuid():N}"[..35], Direction = ContractDirection.Downstream,
                Type = ContractType.Subcontract, VendorId = original.VendorId, Status = ContractStatus.Draft };
            db.Contracts.Add(next); await db.SaveChangesAsync(); return next.Id;
        });
        using var relink = await Post($"{Path(f.ProjectId)}/{f.RequestId}/contract", new { contractId = second, rowVersion = await Version(f.RequestId) });
        relink.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(relink);
        body.GetProperty("history")[1].GetProperty("previousContractId").GetInt32().Should().Be(f.ContractId);
        using var unsigned = await Post($"{Path(f.ProjectId)}/{f.RequestId}/supply", new { documentId = f.DocumentId, rowVersion = await Version(f.RequestId) });
        unsigned.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ConstructionResourceRequests.Where(x => x.Id == f.RequestId).Select(x => x.SuppliedAt).SingleAsync())).Should().BeNull();
    }

    [Theory]
    [InlineData("Upstream")]
    [InlineData("OtherProject")]
    [InlineData("Cancelled")]
    public async Task InvalidContract_IsRejectedWithoutLinkOrHistory(string invalid)
    {
        var f = await SeedAsync();
        await WithDbAsync(async db =>
        {
            var contract = await db.Contracts.FindAsync(f.ContractId);
            if (invalid == "Upstream") contract!.Direction = ContractDirection.Upstream;
            if (invalid == "OtherProject") contract!.OperationalProjectId = null;
            if (invalid == "Cancelled") contract!.Status = ContractStatus.Cancelled;
            await db.SaveChangesAsync();
        });
        await Login("PROCUREMENT");
        using var rejected = await Post($"{Path(f.ProjectId)}/{f.RequestId}/contract", new { contractId = f.ContractId, rowVersion = await Version(f.RequestId) });
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ConstructionResourceRequests.Where(x => x.Id == f.RequestId).Select(x => x.ProcurementContractId).SingleAsync())).Should().BeNull();
        (await WithDbAsync(db => db.Set<ConstructionResourceHandoffEvent>().CountAsync(x => x.ConstructionResourceRequestId == f.RequestId))).Should().Be(0);
    }

    [Theory]
    [InlineData("OtherProject")]
    [InlineData("PendingDelete")]
    public async Task InvalidEvidence_LeavesSupplyAndHistoryUnchanged(string invalid)
    {
        var f = await SeedAsync();
        await Login("PROCUREMENT");
        using var linked = await Post($"{Path(f.ProjectId)}/{f.RequestId}/contract", new { contractId = f.ContractId, rowVersion = await Version(f.RequestId) });
        linked.EnsureSuccessStatusCode();
        await WithDbAsync(async db =>
        {
            var document = await db.ProjectDocuments.FindAsync(f.DocumentId);
            if (invalid == "OtherProject") document!.OperationalProjectId = -123;
            else document!.DesiredOperation = ProjectDocumentDesiredOperation.Delete;
            await db.SaveChangesAsync();
        });
        using var rejected = await Post($"{Path(f.ProjectId)}/{f.RequestId}/supply", new { documentId = f.DocumentId, rowVersion = await Version(f.RequestId) });
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ConstructionResourceRequests.Where(x => x.Id == f.RequestId).Select(x => x.SuppliedAt).SingleAsync())).Should().BeNull();
        (await WithDbAsync(db => db.Set<ConstructionResourceHandoffEvent>().CountAsync(x => x.ConstructionResourceRequestId == f.RequestId))).Should().Be(1);
    }

    [Fact]
    public async Task AccessAndFilters_RequirePermissionMembershipAndApprovedNonmaterialRequests()
    {
        var f = await SeedAsync();
        (await Client.GetAsync(Path(f.ProjectId))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await Login("QS");
        (await Client.GetAsync(Path(f.ProjectId))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var noPermission = await Post($"{Path(f.ProjectId)}/{f.RequestId}/contract", new { contractId = f.ContractId, rowVersion = await Version(f.RequestId) });
        noPermission.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await Login("PROCUREMENT");
        (await Client.GetAsync($"{Path(f.ProjectId)}?type=Material")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var labour = await Client.GetAsync($"{Path(f.ProjectId)}?type=Labour");
        (await ReadJsonAsync(labour)).GetArrayLength().Should().Be(0);
        await WithDbAsync(async db =>
        {
            var member = await db.OperationalProjectMembers.SingleAsync(x => x.OperationalProjectId == f.ProjectId && x.UserId == f.ProcurementId);
            member.EndedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        });
        (await Client.GetAsync(Path(f.ProjectId))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var crossScope = await Post($"{Path(f.ProjectId)}/{f.RequestId}/contract", new { contractId = f.ContractId, rowVersion = await Version(f.RequestId) });
        crossScope.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await WithDbAsync(db => db.ConstructionResourceRequests.Where(x => x.Id == f.RequestId).Select(x => x.ProcurementContractId).SingleAsync())).Should().BeNull();
        (await WithDbAsync(db => db.Set<ConstructionResourceHandoffEvent>().CountAsync(x => x.ConstructionResourceRequestId == f.RequestId))).Should().Be(0);
    }

    private Task Login(string role) => AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, role));
    private static string Path(int projectId) => $"/api/operational-projects/{projectId}/procurement/construction-resources";
    private Task<string> Version(int id) => WithDbAsync(db => db.ConstructionResourceRequests.Where(x => x.Id == id).Select(x => Convert.ToBase64String(x.RowVersion)).SingleAsync());
    private Task<HttpResponseMessage> Post(string path, object payload, string? key = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload) };
        message.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        return Client.SendAsync(message);
    }

    private Task<Fixture> SeedAsync(ConstructionReviewStatus status = ConstructionReviewStatus.CmApproved) => WithDbAsync(async db =>
    {
        async Task<int> User(string role) => (await db.Users.SingleAsync(x => x.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode[role])).Id;
        var pm = await User("PM"); var procurement = await User("PROCUREMENT"); var qs = await User("QS");
        var customerNumber = await db.Customers.CountAsync() + 1;
        var project = new OperationalProject { Code = $"PJ-CRANE-{Guid.NewGuid():N}"[..30], Name = "NICON tower crane and structural workforce", ProjectManagerUserId = pm, CreatedByUserId = pm,
            Customer = new Customer { Name = "NICON industrial construction client", Type = CustomerType.Company, SourceCode = "referral", TaxId = $"010{customerNumber:D7}",
                Contacts = [new CustomerContact { FullName = "An Phu legal representative", Position = "Director", Phone = "0901234567", Email = "representative@example.com", IsPrimary = true, IsLegalRepresentative = true, LegalRepresentativeSince = DateTime.UtcNow.AddYears(-1) }] } };
        db.OperationalProjects.Add(project); await db.SaveChangesAsync();
        db.OperationalProjectMembers.Add(new OperationalProjectMember { OperationalProjectId = project.Id, UserId = procurement, Position = "Site procurement coordinator", StartedAt = DateTime.UtcNow, CreatedByUserId = pm, UpdatedByUserId = pm });
        var vendor = new Vendor { VendorCode = $"V-{Guid.NewGuid():N}"[..25], CompanyName = "NICON structural equipment and labour partner", VendorType = VendorType.Both, CreatedByUserId = procurement };
        db.Vendors.Add(vendor); await db.SaveChangesAsync();
        var contract = new Contract { ContractNumber = $"HD-CRANE-{Guid.NewGuid():N}"[..35], CustomerId = project.CustomerId, OperationalProjectId = project.Id, Direction = ContractDirection.Downstream, Type = ContractType.Subcontract, VendorId = vendor.Id, Status = ContractStatus.Signed, SignedDate = DateTime.UtcNow.AddDays(-10), Value = 120_000_000m, OwnerUserId = procurement };
        var resource = new ConstructionResourceRequest { OperationalProjectId = project.Id, Code = $"RR-{project.Id}-CRANE", Type = ConstructionResourceType.Equipment, Status = status, WbsCode = "STRUCTURE-01", Quantity = 2, Unit = "set", Reason = "Tower cranes for reinforced concrete structure", NeedByDate = DateTime.UtcNow.AddDays(14), CreatedByUserId = qs };
        var document = new ProjectDocument { OperationalProjectId = project.Id, OriginalFileName = "Tower-crane-site-supply.pdf", Category = ProjectDocumentCategory.ConstructionAcceptance, LocalPath = $"/files/projects/{project.Id}/tower-crane.pdf", SyncStatus = ProjectDocumentSyncStatus.Pending };
        db.Contracts.Add(contract); db.ConstructionResourceRequests.Add(resource); db.ProjectDocuments.Add(document);
        await db.SaveChangesAsync(); return new Fixture(project.Id, resource.Id, contract.Id, document.Id, procurement);
    });
    private sealed record Fixture(int ProjectId, int RequestId, int ContractId, long DocumentId, int ProcurementId);
}
