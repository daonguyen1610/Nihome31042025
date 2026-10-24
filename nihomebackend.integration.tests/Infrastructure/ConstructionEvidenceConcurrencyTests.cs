using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Infrastructure;

// SQL Server owns rowversion generation; InMemory cannot prove this invariant.
public sealed class SqlEvidenceFactAttribute : FactAttribute
{
    public SqlEvidenceFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NICON_SQL_TEST_CONNECTION")))
            Skip = "Set NICON_SQL_TEST_CONNECTION to run isolated SQL Server evidence concurrency tests.";
    }
}

public sealed class SqlEvidenceTheoryAttribute : TheoryAttribute
{
    public SqlEvidenceTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NICON_SQL_TEST_CONNECTION")))
            Skip = "Set NICON_SQL_TEST_CONNECTION to run isolated SQL Server evidence concurrency tests.";
    }
}

public sealed class ConstructionEvidenceConcurrencyTests
{
    [SqlEvidenceTheory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task ConstructionLinkWins_StaleDeleteCannotRemoveEvidence(bool quantitySurvey, bool managed, bool updating)
    {
        await using var database = await EvidenceDatabase.CreateAsync(managed);
        var barrier = new BeforeSave();
        await using var deleting = database.Context(barrier);
        var documents = new ProjectDocumentService(deleting, new MetadataDocumentStorage(), null!, null!, null!);
        async Task DeleteAsync()
        {
            if (!managed)
                await documents.DeleteAsync(database.ProjectId, database.DocumentId, database.UserId, true);
            else
            {
                var attachment = await deleting.ContractAttachments.SingleAsync();
                await documents.StageExistingManagedFileDeleteAsync(database.ProjectId, ProjectDocumentSourceModule.Crm,
                    nameof(ContractAttachment), "file", attachment.Id, attachment.FilePath, database.UserId);
                deleting.ContractAttachments.Remove(attachment);
                await deleting.SaveChangesAsync();
            }
        }
        var deletion = DeleteAsync();
        try
        {
            await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await using var linking = database.Context();
            var version = await linking.ProjectDocuments.Select(item => item.RowVersion).SingleAsync();
            await BindConstructionEvidenceAsync(linking, database, quantitySurvey, updating);
            (await linking.ProjectDocuments.AsNoTracking().Select(item => item.RowVersion).SingleAsync())
                .Should().NotEqual(version);
        }
        finally { barrier.Release.TrySetResult(); }
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => deletion);
        await using var verify = database.Context();
        (await verify.ProjectDocuments.SingleAsync()).DeletedAt.Should().BeNull();
        (await verify.ProjectDocuments.SingleAsync()).DesiredOperation.Should().NotBe(ProjectDocumentDesiredOperation.Delete);
        var evidenceCount = quantitySurvey ? await verify.ConstructionQsBatchEvidence.CountAsync()
            : await verify.QaQcDossierEvidence.CountAsync();
        evidenceCount.Should().Be(1);
        if (managed) (await verify.ContractAttachments.CountAsync()).Should().Be(1);
    }

    [SqlEvidenceTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DeletionWins_ConstructionCannotBindDeletedEvidence(bool quantitySurvey, bool updating)
    {
        await using var database = await EvidenceDatabase.CreateAsync(false);
        await using (var deleting = database.Context())
            await new ProjectDocumentService(deleting, new MetadataDocumentStorage(), null!, null!, null!)
                .DeleteAsync(database.ProjectId, database.DocumentId, database.UserId, true);
        await using var linking = database.Context();
        await Assert.ThrowsAsync<ConstructionOperationException>(() =>
            BindConstructionEvidenceAsync(linking, database, quantitySurvey, updating));
        await using var verify = database.Context();
        (await verify.ProjectDocuments.SingleAsync()).DeletedAt.Should().NotBeNull();
        (await verify.QaQcDossierEvidence.CountAsync()).Should().Be(0);
        (await verify.ConstructionQsBatchEvidence.CountAsync()).Should().Be(0);
    }

    private static async Task BindConstructionEvidenceAsync(AppDbContext db, EvidenceDatabase database,
        bool quantitySurvey, bool updating)
    {
        var operations = new ConstructionOperationsService(db, null!);
        if (quantitySurvey)
        {
            var request = new CreateConstructionQsBatchRequest
            {
                Direction = ConstructionQsDirection.Downstream,
                ContractId = await db.Contracts.Select(item => item.Id).SingleAsync(),
                WbsCode = "STRUCTURE-02",
                PeriodFrom = new DateTime(2026, 10, 1),
                PeriodTo = new DateTime(2026, 10, 15),
                CertifiedAmount = 120000000m,
                Description = "NICON structural steel erection acceptance",
                Lines = [new ConstructionQsBatchLineRequest
                {
                    ItemCode = "KC-THEP-01",
                    Description = "Structural steel erection",
                    Unit = "tonne",
                    Quantity = 20m,
                    UnitPrice = 6000000m,
                }],
            };
            var initial = updating ? await operations.CreateQsAsync(database.ProjectId, request, database.UserId, default) : null;
            request.EvidenceDocumentIds = [database.DocumentId];
            if (initial is not null)
                await operations.UpdateQsAsync(database.ProjectId, initial.Id, request, initial.RowVersion, database.UserId, default);
            else await operations.CreateQsAsync(database.ProjectId, request, database.UserId, default);
            return;
        }
        var project = await db.OperationalProjects.SingleAsync();
        var design = new DesignProject
        {
            OperationalProjectId = project.Id,
            ProjectCode = "DP-NICON-QUALITY",
            Name = "NICON structural quality evidence",
            CustomerId = project.CustomerId,
        };
        var template = new QaQcTemplate
        {
            Code = "NICON-STEEL-QUALITY",
            NameVi = "Kiểm tra kết cấu thép",
            Name = "Steel quality inspection",
            NameZh = "钢结构质量检查",
            NameJa = "鉄骨品質検査",
            IsPublished = true,
            CreatedByUserId = database.UserId,
        };
        db.AddRange(design, template);
        await db.SaveChangesAsync();
        var release = new IfcRelease
        {
            DesignProjectId = design.Id,
            ReleaseNumber = "IFC-KC-01",
            Title = "Structural steel erection",
            Status = IfcReleaseStatus.Released,
        };
        db.IfcReleases.Add(release);
        await db.SaveChangesAsync();
        var dossier = new CreateQaQcDossierRequest
        {
            TemplateId = template.Id,
            IfcReleaseId = release.Id,
            WbsCode = "STRUCTURE-02",
            Title = "Nghiệm thu kết cấu thép nhà xưởng NICON",
            ChecklistResultsJson = "[]",
        };
        var draft = updating ? await operations.CreateDossierAsync(database.ProjectId, dossier, database.UserId, default) : null;
        dossier.EvidenceDocumentIds = [database.DocumentId];
        if (draft is not null)
            await operations.UpdateDossierAsync(database.ProjectId, draft.Id, dossier, draft.RowVersion, database.UserId, default);
        else await operations.CreateDossierAsync(database.ProjectId, dossier, database.UserId, default);
    }
    [SqlEvidenceFact]
    public Task ConfirmationWins_StaleManualDeleteCannotDeleteEvidence() => RaceAsync(managed: false);

    [SqlEvidenceFact]
    public Task ConfirmationWins_StaleManagedDeleteRollsBackSourceRemoval() => RaceAsync(managed: true);

    private static async Task RaceAsync(bool managed)
    {
        await using var database = await EvidenceDatabase.CreateAsync(managed);
        var barrier = new BeforeSave();
        await using var deleting = database.Context(barrier);
        var documents = new ProjectDocumentService(deleting, null!, null!, null!, null!);
        async Task DeleteAsync()
        {
            if (!managed)
                await documents.DeleteAsync(database.ProjectId, database.DocumentId, database.UserId, true);
            else
            {
                var attachment = await deleting.ContractAttachments.SingleAsync();
                await documents.StageExistingManagedFileDeleteAsync(database.ProjectId, ProjectDocumentSourceModule.Crm,
                    nameof(ContractAttachment), "file", attachment.Id, attachment.FilePath, database.UserId);
                deleting.ContractAttachments.Remove(attachment);
                await deleting.SaveChangesAsync();
            }
        }
        var deletion = DeleteAsync();
        try
        {
            await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await using var confirming = database.Context();
            var request = await confirming.ConstructionResourceRequests.AsNoTracking().SingleAsync();
            var originalDocumentVersion = await confirming.ProjectDocuments.Select(x => x.RowVersion).SingleAsync();
            await new ConstructionProcurementService(confirming).ConfirmAsync(database.ProjectId, request.Id,
                new ConfirmConstructionSupplyRequest { DocumentId = database.DocumentId, RowVersion = Convert.ToBase64String(request.RowVersion) }, database.UserId, default);
            var currentVersion = await confirming.ProjectDocuments.AsNoTracking().Select(x => x.RowVersion).SingleAsync();
            currentVersion.Should().NotEqual(originalDocumentVersion);
        }
        finally { barrier.Release.TrySetResult(); }
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => deletion);
        await using var verify = database.Context();
        (await verify.ProjectDocuments.SingleAsync()).DeletedAt.Should().BeNull();
        (await verify.ProjectDocuments.SingleAsync()).DesiredOperation.Should().NotBe(ProjectDocumentDesiredOperation.Delete);
        (await verify.ConstructionResourceRequests.SingleAsync()).SupplyDocumentId.Should().Be(database.DocumentId);
        (await verify.Set<ConstructionResourceHandoffEvent>().CountAsync()).Should().Be(1);
        if (managed) (await verify.ContractAttachments.CountAsync()).Should().Be(1);
    }

    [SqlEvidenceFact]
    public async Task DeletionWins_ConfirmationDoesNotMutateRequestOrHistory()
    {
        await using var database = await EvidenceDatabase.CreateAsync(false);
        await using (var deleting = database.Context())
            await new ProjectDocumentService(deleting, new MetadataDocumentStorage(), null!, null!, null!)
                .DeleteAsync(database.ProjectId, database.DocumentId, database.UserId, true);
        await using var confirming = database.Context();
        var request = await confirming.ConstructionResourceRequests.AsNoTracking().SingleAsync();
        await Assert.ThrowsAsync<ConstructionOperationException>(() => new ConstructionProcurementService(confirming)
            .ConfirmAsync(database.ProjectId, request.Id, new ConfirmConstructionSupplyRequest
            { DocumentId = database.DocumentId, RowVersion = Convert.ToBase64String(request.RowVersion) }, database.UserId, default));
        await using var verify = database.Context();
        (await verify.ConstructionResourceRequests.SingleAsync()).SuppliedAt.Should().BeNull();
        (await verify.Set<ConstructionResourceHandoffEvent>().CountAsync()).Should().Be(0);
        (await verify.ProjectDocuments.SingleAsync()).DeletedAt.Should().NotBeNull();
    }

    private sealed class BeforeSave : SaveChangesInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Reached.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return result;
        }
    }

    private sealed class MetadataDocumentStorage : IProjectDocumentStorageService
    {
        public Task<StoredProjectDocument> InspectUploadAsync(Microsoft.AspNetCore.Http.IFormFile? file, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StoredProjectDocument> StoreAsync(int projectId, Microsoft.AspNetCore.Http.IFormFile? file, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StoredProjectDocument> StoreDriveImportAsync(int projectId, string fileName, string contentType, long? expectedLength, Func<Stream, CancellationToken, Task> writeContent, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StoredProjectDocument> InspectExistingAsync(ProjectDocumentSourceModule sourceModule, string localPath, string originalFileName, CancellationToken ct = default) => throw new NotSupportedException();
        public ManagedDocumentContent? GetContent(int projectId, string localPath, string originalFileName, string contentType) => throw new NotSupportedException();
        public Stream OpenRead(int projectId, string localPath) => throw new NotSupportedException();
        public void DeleteOwned(int projectId, string localPath) { }
    }

    private sealed class EvidenceDatabase(string connection) : IAsyncDisposable
    {
        public int ProjectId { get; private set; }
        public int UserId { get; private set; }
        public long DocumentId { get; private set; }
        public AppDbContext Context(SaveChangesInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return new AppDbContext(options.Options);
        }
        public static async Task<EvidenceDatabase> CreateAsync(bool managed)
        {
            var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NICON_SQL_TEST_CONNECTION"))
            { InitialCatalog = $"NiconEvidenceTest_{Guid.NewGuid():N}", Pooling = false };
            var database = new EvidenceDatabase(builder.ConnectionString);
            try
            {
                await using var db = database.Context();
                await db.Database.EnsureCreatedAsync();
                var user = new ApplicationUser { PhoneNumber = "0911000016", FullName = "NICON site procurement", IsActive = true };
                var customer = new Customer { Name = "NICON industrial project client" };
                db.Users.Add(user); db.Customers.Add(customer); await db.SaveChangesAsync();
                database.UserId = user.Id;
                var project = new OperationalProject { Code = "PJ-EVIDENCE-SAFETY", Name = "An Phu structural works", CustomerId = customer.Id, CreatedByUserId = user.Id };
                var vendor = new Vendor { VendorCode = "NICON-CRANE", CompanyName = "NICON lifting equipment partner", CreatedByUserId = user.Id };
                db.OperationalProjects.Add(project); db.Vendors.Add(vendor); await db.SaveChangesAsync();
                database.ProjectId = project.Id;
                var contract = new Contract
                {
                    ContractNumber = "HD-CRANE-01",
                    CustomerId = customer.Id,
                    OperationalProjectId = project.Id,
                    Direction = ContractDirection.Downstream,
                    Type = ContractType.Subcontract,
                    VendorId = vendor.Id,
                    Status = ContractStatus.Signed,
                    OwnerUserId = user.Id
                };
                db.Contracts.Add(contract); await db.SaveChangesAsync();
                var document = new ProjectDocument { OperationalProjectId = project.Id, OriginalFileName = "Crane-delivery.pdf", LocalPath = "/files/contracts/crane-delivery.pdf" };
                if (managed)
                {
                    var attachment = new ContractAttachment { ContractId = contract.Id, FilePath = document.LocalPath, OriginalFileName = document.OriginalFileName };
                    db.ContractAttachments.Add(attachment); await db.SaveChangesAsync();
                    document.SourceType = ProjectDocumentSourceType.ExistingManagedFile; document.SourceModule = ProjectDocumentSourceModule.Crm;
                    document.SourceEntityType = nameof(ContractAttachment); document.SourceSlot = "file"; document.SourceRecordId = attachment.Id;
                }
                db.ProjectDocuments.Add(document);
                db.ConstructionResourceRequests.Add(new ConstructionResourceRequest
                {
                    OperationalProjectId = project.Id,
                    Code = "RR-CRANE-01",
                    Type = ConstructionResourceType.Equipment,
                    Status = ConstructionReviewStatus.CmApproved,
                    ProcurementContractId = contract.Id,
                    CreatedByUserId = user.Id,
                    WbsCode = "STRUCTURE-02",
                    Quantity = 2,
                    Unit = "set",
                    Reason = "Cranes delivered for structural assembly"
                });
                await db.SaveChangesAsync(); database.DocumentId = document.Id;
                return database;
            }
            catch { await database.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            // The catalog is always generated above; never target the supplied catalog.
            var catalog = new SqlConnectionStringBuilder(connection).InitialCatalog;
            const string prefix = "NiconEvidenceTest_";
            if (!catalog.StartsWith(prefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(catalog[prefix.Length..], "N", out _))
                throw new InvalidOperationException("Refusing cleanup outside the generated evidence test database.");
            await using var db = Context();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
