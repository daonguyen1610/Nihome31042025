using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class DesignReviewServiceTests : IDisposable
{
    private readonly AppDbContext db = DbContextFactory.Create();
    private readonly DesignReviewService service;
    private readonly int managerId;
    private readonly int recipientId;
    private readonly int projectId;
    private readonly long documentId;
    private readonly long perspectiveDocumentId;
    private readonly long acceptanceDocumentId;
    private readonly int conceptRevisionId;

    public DesignReviewServiceTests()
    {
        var saleRole = new Role { Code = "SALE", Name = "Kinh doanh", IsActive = true };
        saleRole.RolePermissions.Add(new RolePermission
        {
            Permission = new Permission { Module = "design.reviews", Action = "receive", IsActive = true },
        });
        db.Add(saleRole);
        var manager = User("design.manager@example.test", "Chủ nhiệm thiết kế");
        var recipient = User("sales.reviewer@example.test", "Kinh doanh góp ý", saleRole);
        var lead = User("design.lead@example.test", "Design Lead");
        var customer = new Customer { Name = "NICON design review customer", Type = CustomerType.Company };
        db.Add(customer);
        db.SaveChanges();
        var operational = new OperationalProject { Code = "NICON-DR-01", Name = "NICON office design", CustomerId = customer.Id };
        db.Add(operational);
        db.SaveChanges();
        var project = new DesignProject
        {
            OperationalProjectId = operational.Id,
            ProjectCode = "DP-NICON-DR-01",
            Name = "Thiết kế văn phòng NICON",
            CustomerId = customer.Id,
            ProjectManagerUserId = manager.Id,
        };
        db.Add(project);
        db.SaveChanges();
        var concept = new ConceptOption
        {
            DesignProjectId = project.Id,
            Name = "Phương án kiến trúc NICON R01",
            Status = ConceptOptionStatus.Drafting,
        };
        var assignee = new OperationalProjectMember
        {
            OperationalProjectId = operational.Id,
            UserId = manager.Id,
            Position = "Kiến trúc sư",
            StartedAt = DateTime.UtcNow.AddDays(-1),
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
        };
        var approver = new OperationalProjectMember
        {
            OperationalProjectId = operational.Id,
            UserId = lead.Id,
            Position = "Design Lead",
            StartedAt = DateTime.UtcNow.AddDays(-1),
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
        };
        db.AddRange(concept, assignee, approver);
        db.SaveChanges();
        var phase = new DesignSchedulePhase
        {
            OperationalProjectId = operational.Id,
            DesignProjectId = project.Id,
            Code = DesignSchedulePhaseCode.Concept,
            PlannedStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            PlannedEnd = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = DesignScheduleStatus.Completed,
            ProgressPercent = 100,
            Weight = 100,
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
        };
        db.Add(phase);
        db.SaveChanges();
        db.Add(new DesignScheduleTask
        {
            OperationalProjectId = operational.Id,
            DesignProjectId = project.Id,
            PhaseId = phase.Id,
            Code = "CON-ARC-001",
            Name = "Hoàn thiện phương án kiến trúc",
            DepartmentCode = "DESIGN",
            AssigneeMemberId = assignee.Id,
            ApproverMemberId = approver.Id,
            PlannedStart = phase.PlannedStart,
            PlannedEnd = phase.PlannedEnd,
            ActualEnd = phase.PlannedEnd,
            Status = DesignScheduleStatus.Completed,
            ProgressPercent = 100,
            Weight = 100,
            ApprovalStatus = DesignTaskApprovalStatus.Approved,
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
        });
        var document = new ProjectDocument
        {
            OperationalProjectId = operational.Id,
            Category = ProjectDocumentCategory.DesignConcept,
            SourceModule = ProjectDocumentSourceModule.Design,
            SourceEntityType = nameof(ConceptOption),
            SourceRecordId = concept.Id,
            LocalPath = "/files/project-documents/concept.pdf",
            OriginalFileName = "PA-KT-Concept-R01.pdf",
            ContentType = "application/pdf",
            Sha256 = new string('a', 64),
            SyncStatus = ProjectDocumentSyncStatus.Synced,
        };
        db.Add(document);
        var perspective = new ProjectDocument
        {
            OperationalProjectId = operational.Id,
            Category = ProjectDocumentCategory.DesignConcept,
            SourceModule = ProjectDocumentSourceModule.Design,
            SourceEntityType = nameof(ConceptOption),
            SourceRecordId = concept.Id,
            LocalPath = "/files/project-documents/concept-perspective.jpg",
            OriginalFileName = "Phối cảnh văn phòng NICON.jpg",
            ContentType = "image/jpeg",
            Sha256 = new string('b', 64),
            SyncStatus = ProjectDocumentSyncStatus.Synced,
        };
        var acceptance = new ProjectDocument
        {
            OperationalProjectId = operational.Id,
            Category = ProjectDocumentCategory.DesignConcept,
            SourceModule = ProjectDocumentSourceModule.Design,
            SourceEntityType = nameof(ConceptOption),
            SourceRecordId = concept.Id,
            LocalPath = "/files/project-documents/TK-M03.pdf",
            OriginalFileName = "TK-M03-NICON-da-ky.pdf",
            ContentType = "application/pdf",
            Sha256 = new string('c', 64),
            SyncStatus = ProjectDocumentSyncStatus.Synced,
        };
        db.AddRange(perspective, acceptance);
        db.SaveChanges();
        var revision = new ConceptRevision
        {
            ConceptOptionId = concept.Id,
            Number = 0,
            CreatedByUserId = manager.Id,
            Artifacts =
            [
                new ConceptArtifact { ProjectDocumentId = document.Id, Kind = ConceptArtifactKind.Presentation, CreatedByUserId = manager.Id },
                new ConceptArtifact { ProjectDocumentId = perspective.Id, Kind = ConceptArtifactKind.Perspective, CreatedByUserId = manager.Id },
            ],
            Waivers =
            [
                new ConceptChecklistWaiver { Kind = ConceptArtifactKind.SiteRoofPlan, Reason = "Không áp dụng cho hồ sơ thử nghiệm", CreatedByUserId = manager.Id },
                new ConceptChecklistWaiver { Kind = ConceptArtifactKind.FloorPlan, Reason = "Không áp dụng cho hồ sơ thử nghiệm", CreatedByUserId = manager.Id },
                new ConceptChecklistWaiver { Kind = ConceptArtifactKind.Section, Reason = "Không áp dụng cho hồ sơ thử nghiệm", CreatedByUserId = manager.Id },
            ],
        };
        db.Add(revision);
        db.SaveChanges();
        managerId = manager.Id;
        recipientId = recipient.Id;
        projectId = project.Id;
        documentId = document.Id;
        perspectiveDocumentId = perspective.Id;
        acceptanceDocumentId = acceptance.Id;
        conceptRevisionId = revision.Id;
        var access = new Mock<IProjectAccessService>();
        access.Setup(item => item.CanViewDesignProjectAsync(It.IsAny<int>(), projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        access.Setup(item => item.HasAdministrativeBypassAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        service = new DesignReviewService(db, new ConceptDossierService(db), access.Object, new Mock<INotificationService>().Object,
            NullLogger<DesignReviewService>.Instance);
    }

    [Fact]
    public async Task Lifecycle_RestrictsRecipientToViewAndComment_ThenRecordsClientDecision()
    {
        var created = await service.CreateAsync(projectId, new CreateDesignReviewRequest
        {
            Stage = "Concept",
            Title = "Phương án Concept R01",
            RecipientUserIds = [recipientId],
            ProjectDocumentIds = [documentId, perspectiveDocumentId],
            ConceptRevisionId = conceptRevisionId,
        }, managerId, default);
        Assert.Equal("Draft", created.Status);

        var submitted = await service.SubmitInternalAsync(projectId, created.Id, "", managerId, default);
        Assert.Equal("InternalReview", submitted!.Status);
        var visible = await service.ListAsync(projectId, recipientId, default);
        Assert.Single(visible!);
        Assert.False(visible![0].CanManage);
        Assert.True(visible[0].CanComment);

        var commented = await service.AddCommentAsync(projectId, created.Id,
            new AddDesignReviewCommentRequest { Body = "Kinh doanh đề nghị làm rõ vật liệu hoàn thiện." }, recipientId, default);
        Assert.Single(commented!.Comments);

        var consolidated = await service.ConsolidateAsync(projectId, created.Id,
            new ConsolidateDesignReviewRequest { InternalSummary = "Đã cập nhật vật liệu theo góp ý.", RowVersion = "" }, managerId, default);
        Assert.Equal("ReadyForClient", consolidated!.Status);
        var clientSubmitted = await service.SubmitClientAsync(projectId, created.Id, "", managerId, default);
        Assert.Equal("SubmittedToClient", clientSubmitted!.Status);
        var approved = await service.RecordClientDecisionAsync(projectId, created.Id,
            new RecordDesignClientDecisionRequest
            {
                Decision = "ClientApproved",
                RowVersion = "",
                ClientAcceptanceDocumentId = acceptanceDocumentId,
                ClientAcceptanceSignedBy = "Đại diện chủ đầu tư",
                ClientAcceptanceSignedAt = DateTime.UtcNow,
            }, managerId, default);
        Assert.Equal("ClientApproved", approved!.Status);
    }

    [Fact]
    public async Task Create_RejectsWrongStageDocument()
    {
        await Assert.ThrowsAsync<DesignReviewOperationException>(() => service.CreateAsync(projectId,
            new CreateDesignReviewRequest
            {
                Stage = "BasicDesign",
                Title = "Wrong category",
                RecipientUserIds = [recipientId],
                ProjectDocumentIds = [documentId],
            }, managerId, default));
        Assert.Empty(db.DesignReviews);
    }

    [Fact]
    public async Task Create_RejectsRecipientOutsideApprovedBusinessRoles()
    {
        var observer = User("observer@example.test", "Người không thuộc luồng trình");

        await Assert.ThrowsAsync<DesignReviewOperationException>(() => service.CreateAsync(projectId,
            new CreateDesignReviewRequest
            {
                Stage = "Concept",
                Title = "Phương án không đúng người nhận",
                RecipientUserIds = [observer.Id],
                ProjectDocumentIds = [documentId],
            }, managerId, default));
    }

    [Fact]
    public async Task SubmitInternal_RejectsUntilEveryStageTaskIsApproved()
    {
        var created = await CreateReviewAsync();
        var task = db.DesignScheduleTasks.Single();
        task.ApprovalStatus = DesignTaskApprovalStatus.Pending;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DesignReviewOperationException>(() =>
            service.SubmitInternalAsync(projectId, created.Id, "", managerId, default));
    }

    [Fact]
    public async Task SubmitInternal_RejectsChangedEvidenceSnapshot()
    {
        var created = await CreateReviewAsync();
        var document = db.ProjectDocuments.Single(item => item.Id == documentId);
        document.Sha256 = new string('b', 64);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DesignReviewOperationException>(() =>
            service.SubmitInternalAsync(projectId, created.Id, "", managerId, default));
    }

    public void Dispose() => db.Dispose();

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task RecipientEligibility_UsesCapabilityAndActiveRoleInsteadOfRoleCode(bool granted, bool activeRole)
    {
        var recipient = await db.Users.Include(item => item.RoleEntity).ThenInclude(role => role!.RolePermissions)
            .SingleAsync(item => item.Id == recipientId);
        recipient.RoleEntity!.Code = "NICON_CM_REVIEWER";
        recipient.RoleEntity.IsActive = activeRole;
        if (!granted) db.RolePermissions.RemoveRange(recipient.RoleEntity.RolePermissions);
        await db.SaveChangesAsync();

        var candidates = await service.ListRecipientCandidatesAsync(projectId, managerId, default);
        Assert.Equal(granted && activeRole, candidates.Any(item => item.UserId == recipientId));
        if (granted && activeRole)
            Assert.Equal("Draft", (await CreateReviewAsync()).Status);
        else
        {
            await Assert.ThrowsAsync<DesignReviewOperationException>(CreateReviewAsync);
            Assert.Empty(db.DesignReviews);
        }
    }

    private Task<DesignReviewResponse> CreateReviewAsync() => service.CreateAsync(projectId,
        new CreateDesignReviewRequest
        {
            Stage = "Concept",
            Title = "Phương án Concept R01",
            RecipientUserIds = [recipientId],
            ProjectDocumentIds = [documentId, perspectiveDocumentId],
            ConceptRevisionId = conceptRevisionId,
        }, managerId, default);

    private ApplicationUser User(string email, string name, Role? role = null)
    {
        var user = new ApplicationUser
        {
            Email = email,
            PhoneNumber = $"09{db.Users.Count() + 1:00000000}",
            FullName = name,
            PasswordHash = "test",
            IsActive = true,
            RoleEntity = role,
        };
        db.Add(user);
        db.SaveChanges();
        return user;
    }
}
