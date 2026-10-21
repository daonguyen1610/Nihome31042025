using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Infrastructure;

public sealed class ConceptDossierConcurrencyTests
{
    [SqlEvidenceFact]
    public Task SubmissionWins_ConcurrentDetachPreservesSubmittedFile() => RaceAsync(submissionFirst: true, deleteDocument: false);

    [SqlEvidenceFact]
    public Task SubmissionWins_ConcurrentDeletePreservesDocument() => RaceAsync(submissionFirst: true, deleteDocument: true);

    [SqlEvidenceFact]
    public Task DetachWins_SubmissionRejectsChangedFileSet() => RaceAsync(submissionFirst: false, deleteDocument: false);

    private static async Task RaceAsync(bool submissionFirst, bool deleteDocument)
    {
        await using var database = await DossierDatabase.CreateAsync();
        var beforeSave = new BeforeSave();
        var waitingForLock = new LockRequested();
        await using var firstContext = database.Context(beforeSave);
        await using var secondContext = database.Context(waitingForLock);
        Task Submit(AppDbContext db) => new DesignReviewService(db, new ConceptDossierService(db), null!, null!,
            NullLogger<DesignReviewService>.Instance).SubmitInternalAsync(database.ProjectId, database.ReviewId,
                database.ReviewVersion, database.UserId, default);
        Task Edit(AppDbContext db) => deleteDocument
            ? new ProjectDocumentService(db, null!, null!, null!, null!).DeleteAsync(
                database.OperationalId, database.DocumentId, database.UserId, true)
            : new ConceptDossierService(db).RemoveArtifactAsync(database.OptionId, database.RevisionId,
                database.ArtifactId, database.UserId, default);
        var first = submissionFirst ? Submit(firstContext) : Edit(firstContext);
        Task second = Task.CompletedTask;
        try
        {
            await beforeSave.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            second = submissionFirst ? Edit(secondContext) : Submit(secondContext);
            await waitingForLock.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            second.IsCompleted.Should().BeFalse("the checked dossier is protected until the first transaction commits");
        }
        finally { beforeSave.Release.TrySetResult(); }
        await first;
        if (!submissionFirst)
            await Assert.ThrowsAsync<DesignReviewOperationException>(() => second);
        else if (deleteDocument)
            await Assert.ThrowsAsync<ProjectDocumentValidationException>(() => second);
        else
            await Assert.ThrowsAsync<ConceptOptionOperationException>(() => second);
        await using var verify = database.Context();
        (await verify.ConceptArtifacts.AnyAsync(item => item.Id == database.ArtifactId)).Should().Be(submissionFirst);
        var revision = await verify.ConceptRevisions.SingleAsync();
        var review = await verify.DesignReviews.SingleAsync();
        revision.FrozenAt.HasValue.Should().Be(submissionFirst);
        review.Status.Should().Be(submissionFirst ? DesignReviewStatus.InternalReview : DesignReviewStatus.Draft);
        (await verify.ProjectDocuments.SingleAsync()).DeletedAt.Should().BeNull();
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

    private sealed class LockRequested : DbCommandInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("sp_getapplock", StringComparison.Ordinal)) Reached.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class DossierDatabase(string connection) : IAsyncDisposable
    {
        public int OperationalId { get; private set; }
        public int ProjectId { get; private set; }
        public int UserId { get; private set; }
        public int OptionId { get; private set; }
        public int RevisionId { get; private set; }
        public int ReviewId { get; private set; }
        public int ArtifactId { get; private set; }
        public long DocumentId { get; private set; }
        public string ReviewVersion { get; private set; } = string.Empty;

        public AppDbContext Context(IInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return new AppDbContext(options.Options);
        }

        public static async Task<DossierDatabase> CreateAsync()
        {
            var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NICON_SQL_TEST_CONNECTION"))
            { InitialCatalog = $"NiconConceptTest_{Guid.NewGuid():N}", Pooling = false };
            var database = new DossierDatabase(builder.ConnectionString);
            try
            {
                await using var db = database.Context();
                await db.Database.EnsureCreatedAsync();
                var manager = new ApplicationUser { PhoneNumber = "0911000004", Email = "design.manager@nihome.test", FullName = "Chủ nhiệm thiết kế sân trong", IsActive = true };
                var lead = new ApplicationUser { PhoneNumber = "0911000011", Email = "design.lead@nihome.test", FullName = "Design Lead kiến trúc", IsActive = true };
                var customer = new Customer { Name = "Chủ đầu tư nhà phố sân trong" };
                db.AddRange(manager, lead, customer);
                await db.SaveChangesAsync();
                database.UserId = manager.Id;
                var operational = new OperationalProject { Code = "PJ-CONCEPT-LOCK", Name = "Nhà phố sân trong", CustomerId = customer.Id };
                db.Add(operational);
                await db.SaveChangesAsync();
                database.OperationalId = operational.Id;
                var project = new DesignProject
                {
                    OperationalProjectId = operational.Id,
                    ProjectCode = "DP-CONCEPT-LOCK",
                    Name = "Thiết kế nhà phố sân trong",
                    CustomerId = customer.Id,
                    ProjectManagerUserId = manager.Id,
                    DesignLeadUserId = lead.Id,
                    CreatedByUserId = manager.Id,
                    UpdatedByUserId = manager.Id,
                };
                var assignee = new OperationalProjectMember { OperationalProjectId = operational.Id, UserId = manager.Id, Position = "Kiến trúc sư", CreatedByUserId = manager.Id, UpdatedByUserId = manager.Id };
                var approver = new OperationalProjectMember { OperationalProjectId = operational.Id, UserId = lead.Id, Position = "Design Lead", CreatedByUserId = manager.Id, UpdatedByUserId = manager.Id };
                db.AddRange(project, assignee, approver);
                await db.SaveChangesAsync();
                database.ProjectId = project.Id;
                var option = new ConceptOption { DesignProjectId = project.Id, Name = "Phương án lấy sáng sân trong", CreatedByUserId = manager.Id, UpdatedByUserId = manager.Id };
                var phase = new DesignSchedulePhase
                {
                    OperationalProjectId = operational.Id,
                    DesignProjectId = project.Id,
                    Code = DesignSchedulePhaseCode.Concept,
                    CreatedByUserId = manager.Id,
                    UpdatedByUserId = manager.Id,
                    PlannedStart = new DateOnly(2026, 10, 1),
                    PlannedEnd = new DateOnly(2026, 10, 20),
                    Weight = 100,
                };
                db.AddRange(option, phase);
                await db.SaveChangesAsync();
                database.OptionId = option.Id;
                db.Add(new DesignScheduleTask
                {
                    OperationalProjectId = operational.Id,
                    DesignProjectId = project.Id,
                    PhaseId = phase.Id,
                    CreatedByUserId = manager.Id,
                    UpdatedByUserId = manager.Id,
                    Code = "CON-ARC-001",
                    Name = "Hoàn thiện phối cảnh sân trong",
                    DepartmentCode = "DESIGN",
                    AssigneeMemberId = assignee.Id,
                    ApproverMemberId = approver.Id,
                    Status = DesignScheduleStatus.Completed,
                    ApprovalStatus = DesignTaskApprovalStatus.Approved,
                    PlannedStart = phase.PlannedStart,
                    PlannedEnd = phase.PlannedEnd,
                    Weight = 100,
                });
                var document = new ProjectDocument
                {
                    OperationalProjectId = operational.Id,
                    Category = ProjectDocumentCategory.DesignConcept,
                    SourceModule = ProjectDocumentSourceModule.Design,
                    SourceEntityType = nameof(ConceptOption),
                    SourceRecordId = option.Id,
                    OriginalFileName = "Phối cảnh sân trong.png",
                    ContentType = "image/png",
                    Sha256 = new string('a', 64),
                    SyncStatus = ProjectDocumentSyncStatus.Synced,
                };
                db.Add(document);
                var revision = new ConceptRevision { ConceptOptionId = option.Id, Number = 0, CreatedByUserId = manager.Id };
                db.Add(revision);
                await db.SaveChangesAsync();
                database.DocumentId = document.Id;
                database.RevisionId = revision.Id;
                var artifact = new ConceptArtifact { ConceptRevisionId = revision.Id, ProjectDocumentId = document.Id, Kind = ConceptArtifactKind.Perspective };
                db.Add(artifact);
                foreach (var kind in new[] { ConceptArtifactKind.SiteRoofPlan, ConceptArtifactKind.FloorPlan, ConceptArtifactKind.Section, ConceptArtifactKind.Presentation })
                    db.Add(new ConceptChecklistWaiver { ConceptRevisionId = revision.Id, Kind = kind, Reason = "Không áp dụng trong bộ hồ sơ kiểm chứng khóa giao dịch." });
                var review = new DesignReview
                {
                    DesignProjectId = project.Id,
                    ConceptRevisionId = revision.Id,
                    Stage = DesignReviewStage.Concept,
                    CreatedByUserId = manager.Id,
                    UpdatedByUserId = manager.Id,
                    RevisionNumber = 1,
                    Title = "Trình phối cảnh sân trong",
                    DesignManagerUserId = manager.Id,
                    Recipients = [new DesignReviewRecipient { UserId = lead.Id }],
                    Documents = [new DesignReviewDocument { ProjectDocumentId = document.Id, SourceEntityTypeSnapshot = nameof(ConceptOption), SourceRecordIdSnapshot = option.Id, Sha256Snapshot = document.Sha256 }],
                };
                db.Add(review);
                await db.SaveChangesAsync();
                database.ReviewId = review.Id;
                database.ReviewVersion = Convert.ToBase64String(review.RowVersion);
                database.ArtifactId = artifact.Id;
                return database;
            }
            catch { await database.DisposeAsync(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            var catalog = new SqlConnectionStringBuilder(connection).InitialCatalog;
            const string prefix = "NiconConceptTest_";
            if (!catalog.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(catalog[prefix.Length..], "N", out _))
                throw new InvalidOperationException("Refusing cleanup outside the generated Concept test database.");
            await using var db = Context();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
