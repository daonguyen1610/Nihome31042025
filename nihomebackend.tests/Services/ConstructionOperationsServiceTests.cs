using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class ConstructionOperationsServiceTests : IDisposable
{
    private const int ProjectId = 41;
    private const int BchUserId = 101;
    private const int CmUserId = 202;

    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly Mock<IProcurementService> _procurement = new();
    private readonly ConstructionOperationsService _sut;

    public ConstructionOperationsServiceTests()
    {
        _sut = new ConstructionOperationsService(_db, _procurement.Object);
    }

    [Fact]
    public async Task Resource_request_requires_independent_cm_reviewer()
    {
        var created = await _sut.CreateResourceAsync(ProjectId, EquipmentRequest(), BchUserId, default);
        var submitted = await _sut.SubmitResourceAsync(
            ProjectId, created.Id, created.RowVersion, BchUserId, default);

        var action = () => _sut.DecideResourceAsync(
            ProjectId,
            created.Id,
            new CmDecisionRequest
            {
                Approved = true,
                Reason = "Approved against the current resource plan.",
                RowVersion = submitted!.RowVersion,
            },
            BchUserId,
            default);

        var error = await Assert.ThrowsAsync<ConstructionOperationException>(action);
        Assert.Contains("author cannot", error.Message, StringComparison.OrdinalIgnoreCase);

        var stored = await _db.ConstructionResourceRequests.FindAsync(created.Id);
        Assert.Equal(ConstructionReviewStatus.PendingCmReview, stored!.Status);
        Assert.Equal(ConstructionHandoffStatus.NotReady, stored.HandoffStatus);
    }

    [Fact]
    public async Task Approved_equipment_request_creates_stable_handoff_without_procurement_mr()
    {
        var created = await _sut.CreateResourceAsync(ProjectId, EquipmentRequest(), BchUserId, default);
        var submitted = await _sut.SubmitResourceAsync(
            ProjectId, created.Id, created.RowVersion, BchUserId, default);

        var approved = await _sut.DecideResourceAsync(
            ProjectId,
            created.Id,
            new CmDecisionRequest
            {
                Approved = true,
                Reason = "CM verified the need and delivery date.",
                RowVersion = submitted!.RowVersion,
            },
            CmUserId,
            default);

        Assert.Equal(ConstructionReviewStatus.CmApproved, approved!.Status);
        Assert.Equal(ConstructionHandoffStatus.ReadyForProcurementAndFinance, approved.HandoffStatus);
        Assert.Equal("ConstructionResourceRequest", approved.HandoffEntityType);
        Assert.Equal(created.Id, approved.HandoffEntityId);
        _procurement.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Rejected_request_preserves_downstream_state()
    {
        var created = await _sut.CreateResourceAsync(ProjectId, EquipmentRequest(), BchUserId, default);
        var submitted = await _sut.SubmitResourceAsync(
            ProjectId, created.Id, created.RowVersion, BchUserId, default);

        var rejected = await _sut.DecideResourceAsync(
            ProjectId,
            created.Id,
            new CmDecisionRequest
            {
                Approved = false,
                Reason = "The request is outside the approved resource plan.",
                RowVersion = submitted!.RowVersion,
            },
            CmUserId,
            default);

        Assert.Equal(ConstructionReviewStatus.CmRejected, rejected!.Status);
        Assert.Equal(ConstructionHandoffStatus.NotReady, rejected.HandoffStatus);
        Assert.Null(rejected.HandoffEntityId);
        _procurement.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Qa_qc_dossier_requires_released_ifc_reference()
    {
        var template = new QaQcTemplate
        {
            Code = "ISO-QA",
            Version = 1,
            NameVi = "Hồ sơ chất lượng",
            Name = "Quality dossier",
            NameZh = "质量档案",
            NameJa = "品質記録",
            SchemaJson = "{}",
            IsPublished = true,
            CreatedByUserId = CmUserId,
        };
        _db.QaQcTemplates.Add(template);
        await _db.SaveChangesAsync();

        var action = () => _sut.CreateDossierAsync(
            ProjectId,
            new CreateQaQcDossierRequest
            {
                TemplateId = template.Id,
                WbsCode = "1.2.1",
                Title = "Foundation concrete inspection",
                ChecklistResultsJson = "[]",
            },
            BchUserId,
            default);

        var error = await Assert.ThrowsAsync<ConstructionOperationException>(action);
        Assert.Contains("released IFC", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_db.QaQcDossiers);
    }

    private static UpsertConstructionResourceRequest EquipmentRequest() => new()
    {
        Type = ConstructionResourceType.Equipment,
        WbsCode = "2.1.3",
        NeedByDate = DateTime.UtcNow.AddDays(7),
        Quantity = 2,
        Unit = "machine",
        Reason = "Two excavators are required for the approved earthwork sequence.",
    };

    public void Dispose() => _db.Dispose();
}
