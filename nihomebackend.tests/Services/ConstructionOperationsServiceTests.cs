using System.Text.Json;
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
    private static readonly Guid WbsNodeId = Guid.Parse("a4c18d9a-f7ab-4e6f-9ca2-019b71000041");

    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly Mock<IProcurementService> _procurement = new();
    private readonly ConstructionOperationsService _sut;

    public ConstructionOperationsServiceTests()
    {
        _db.ProjectScheduleNodes.Add(new ProjectScheduleNode
        {
            Id = WbsNodeId,
            OperationalProjectId = ProjectId,
        });
        _db.ProjectScheduleRevisions.Add(new ProjectScheduleRevision
        {
            OperationalProjectId = ProjectId,
            Version = 1,
            Status = ProjectScheduleRevisionStatus.Published,
            Items =
            [
                new ProjectScheduleItem
                {
                    NodeId = WbsNodeId,
                    WbsCode = "1.2.1",
                    Title = "Thi công móng",
                    PlannedStart = new DateOnly(2026, 1, 1),
                    PlannedEnd = new DateOnly(2027, 1, 1),
                },
            ],
        });
        _db.SaveChanges();
        _sut = new ConstructionOperationsService(_db, _procurement.Object);
    }

    [Fact]
    public async Task Readiness_requires_structured_published_cm_baseline()
    {
        var node = Guid.NewGuid();
        var task = new ConstructionTask { Id = 61, DesignProject = new DesignProject { Id = 51, OperationalProjectId = ProjectId }, ProjectScheduleNodeId = node, Wbs = "1.1", TaskCode = "TC-1", Name = "Thi công móng" };
        _db.ConstructionTasks.Add(task);
        _db.ConstructionPreparations.Add(new ConstructionPreparation { OperationalProjectId = ProjectId, Version = 1 });
        var crm = _db.ProjectScheduleRevisions.Single();
        await _db.SaveChangesAsync();
        var missing = await _sut.GetPreparationAsync(ProjectId, default);
        Assert.Contains("published CM schedule baseline", missing!.MissingConditions);
        _db.CmScheduleBaselines.Add(new CmScheduleBaseline { OperationalProjectId = ProjectId, CrmRevisionId = crm.Id, Version = 1, Status = "Draft", ItemsJson = JsonSerializer.Serialize(new[] { new CmScheduleLine(task.Id, task.DesignProjectId, node, "1.1", task.Name, new(2026, 11, 1), new(2026, 11, 10), new(2026, 11, 1), new(2026, 11, 10), 1, true, []) }) });
        await _db.SaveChangesAsync();
        Assert.Contains("published CM schedule baseline", (await _sut.GetPreparationAsync(ProjectId, default))!.MissingConditions);
        _db.CmScheduleBaselines.Single().Status = "Published";
        await _db.SaveChangesAsync();
        Assert.DoesNotContain("published CM schedule baseline", (await _sut.GetPreparationAsync(ProjectId, default))!.MissingConditions);
        task.ProjectScheduleNodeId = Guid.NewGuid();
        await _db.SaveChangesAsync();
        Assert.Contains("published CM schedule baseline", (await _sut.GetPreparationAsync(ProjectId, default))!.MissingConditions);
        task.ProjectScheduleNodeId = node;
        _db.ProjectScheduleRevisions.Add(new ProjectScheduleRevision { OperationalProjectId = ProjectId, Version = 2, Status = ProjectScheduleRevisionStatus.Published });
        await _db.SaveChangesAsync();
        Assert.Contains("published CM schedule baseline", (await _sut.GetPreparationAsync(ProjectId, default))!.MissingConditions);
    }

    [Fact]
    public async Task Optional_project_requirements_do_not_block_construction_readiness()
    {
        _db.ConstructionPreparations.Add(new ConstructionPreparation
        {
            OperationalProjectId = ProjectId,
            Version = 1,
            SiteHandoverRequired = false,
            MethodStatementRequired = false,
            HsePlanRequired = false,
        });
        await _db.SaveChangesAsync();

        var response = await _sut.GetPreparationAsync(ProjectId, default);

        Assert.NotNull(response);
        Assert.DoesNotContain("approved project ISO conditional-requirements profile", response.MissingConditions);
        Assert.DoesNotContain("site handover evidence", response.MissingConditions);
        Assert.DoesNotContain("approved method statement evidence", response.MissingConditions);
        Assert.DoesNotContain("approved HSE plan evidence", response.MissingConditions);
    }

    [Fact]
    public async Task Required_project_requirement_still_needs_its_evidence()
    {
        _db.ConstructionPreparations.Add(new ConstructionPreparation
        {
            OperationalProjectId = ProjectId,
            Version = 1,
            SiteHandoverRequired = true,
            MethodStatementRequired = false,
            HsePlanRequired = false,
        });
        await _db.SaveChangesAsync();

        var response = await _sut.GetPreparationAsync(ProjectId, default);

        Assert.NotNull(response);
        Assert.Contains("site handover evidence", response.MissingConditions);
        Assert.DoesNotContain("approved project ISO conditional-requirements profile", response.MissingConditions);
        Assert.DoesNotContain("approved method statement evidence", response.MissingConditions);
        Assert.DoesNotContain("approved HSE plan evidence", response.MissingConditions);
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
        Assert.Contains("không được tự thực hiện bước duyệt CM độc lập", error.Message, StringComparison.OrdinalIgnoreCase);

        var stored = await _db.ConstructionResourceRequests.FindAsync(created.Id);
        Assert.Equal(ConstructionReviewStatus.PendingCmReview, stored!.Status);
        Assert.Equal(ConstructionHandoffStatus.NotReady, stored.HandoffStatus);
    }

    [Fact]
    public async Task Resource_request_rejects_wbs_removed_from_latest_published_revision()
    {
        _db.ProjectScheduleRevisions.Add(new ProjectScheduleRevision
        {
            OperationalProjectId = ProjectId,
            Version = 2,
            Status = ProjectScheduleRevisionStatus.Published,
        });
        await _db.SaveChangesAsync();

        var action = () => _sut.CreateResourceAsync(ProjectId, EquipmentRequest(), BchUserId, default);

        var error = await Assert.ThrowsAsync<ConstructionOperationException>(action);
        Assert.Contains("đang có hiệu lực", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_db.ConstructionResourceRequests);
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
    public async Task Qa_qc_template_requires_business_friendly_checklist_rows()
    {
        var request = new CreateQaQcTemplateRequest
        {
            Code = "ISO-QA-UX",
            NameVi = "Kiểm tra cốp pha",
            Name = "Formwork inspection",
            NameZh = "模板检查",
            NameJa = "型枠検査",
            SchemaJson = """{"schemaVersion":1,"items":[{"code":"C1","title":"Cao độ cốp pha"}]}""",
        };

        var action = () => _sut.CreateTemplateAsync(request, CmUserId, default);

        var error = await Assert.ThrowsAsync<ConstructionOperationException>(action);
        Assert.Contains("tiêu chí đạt", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_db.QaQcTemplates);
    }

    [Fact]
    public async Task Qa_qc_template_uses_vietnamese_source_name_for_other_languages()
    {
        var request = new CreateQaQcTemplateRequest
        {
            Code = "ISO-QA-SRC",
            NameVi = "  Kiểm tra cốp pha  ",
            SchemaJson = """{"schemaVersion":1,"items":[{"code":"C1","title":"Cao độ cốp pha","acceptanceCriteria":"Sai số ±5 mm"}]}""",
        };

        var created = await _sut.CreateTemplateAsync(request, CmUserId, default);

        Assert.Equal("Kiểm tra cốp pha", created.NameVi);
        var stored = Assert.Single(_db.QaQcTemplates);
        Assert.Equal("Kiểm tra cốp pha", stored.Name);
        Assert.Equal("Kiểm tra cốp pha", stored.NameZh);
        Assert.Equal("Kiểm tra cốp pha", stored.NameJa);
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
            SchemaJson = """{"schemaVersion":1,"items":[{"code":"C1","title":"Cao độ móng","acceptanceCriteria":"Đúng bản vẽ IFC"}]}""",
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
                ProjectScheduleNodeId = WbsNodeId,
                WbsCode = "1.2.1",
                Title = "Foundation concrete inspection",
                ChecklistResultsJson = """[{"code":"C1","result":"Pending","note":""}]""",
            },
            BchUserId,
            default);

        var error = await Assert.ThrowsAsync<ConstructionOperationException>(action);
        Assert.Contains("IFC đã phát hành", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_db.QaQcDossiers);
    }

    [Fact]
    public async Task Qa_qc_dossier_rejects_result_not_defined_by_selected_template()
    {
        var templateId = await SeedQaQcReferencesAsync();
        var request = QaQcRequest(templateId);
        request.ChecklistResultsJson = """[{"code":"UNKNOWN","result":"Pass","note":""}]""";

        var action = () => _sut.CreateDossierAsync(ProjectId, request, BchUserId, default);

        var error = await Assert.ThrowsAsync<ConstructionOperationException>(action);
        Assert.Contains("không thuộc mẫu", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_db.QaQcDossiers);
    }

    [Fact]
    public async Task Qa_qc_dossier_preserves_each_evidence_type_and_legacy_ids()
    {
        var templateId = await SeedQaQcReferencesAsync();
        var typed = QaQcRequest(templateId);
        typed.Evidence =
        [
            new QaQcEvidenceRequest { ProjectDocumentId = 901, EvidenceType = QaQcEvidenceType.LaboratoryResult },
            new QaQcEvidenceRequest { ProjectDocumentId = 902, EvidenceType = QaQcEvidenceType.MaterialCertificate },
        ];

        var created = await _sut.CreateDossierAsync(ProjectId, typed, BchUserId, default);
        var loaded = await _sut.GetDossierAsync(ProjectId, created.Id, default);

        Assert.NotNull(loaded);
        Assert.Contains(loaded.Evidence, item => item.ProjectDocumentId == 901 &&
            item.EvidenceType == QaQcEvidenceType.LaboratoryResult);
        Assert.Contains(loaded.Evidence, item => item.ProjectDocumentId == 902 &&
            item.EvidenceType == QaQcEvidenceType.MaterialCertificate);
        Assert.Equal(2, loaded.EvidenceDocumentIds.Count);

        var legacyUpdate = QaQcRequest(templateId);
        legacyUpdate.EvidenceDocumentIds = [901, 902];
        var updated = await _sut.UpdateDossierAsync(ProjectId, created.Id, legacyUpdate,
            created.RowVersion, BchUserId, default);
        Assert.Contains(updated!.Evidence, item => item.ProjectDocumentId == 901 &&
            item.EvidenceType == QaQcEvidenceType.LaboratoryResult);
        Assert.Contains(updated.Evidence, item => item.ProjectDocumentId == 902 &&
            item.EvidenceType == QaQcEvidenceType.MaterialCertificate);

        var legacy = QaQcRequest(templateId);
        legacy.EvidenceDocumentIds = [903];
        var legacyCreated = await _sut.CreateDossierAsync(ProjectId, legacy, BchUserId, default);
        Assert.Equal(QaQcEvidenceType.General, Assert.Single(legacyCreated.Evidence).EvidenceType);
    }

    [Fact]
    public async Task Qa_qc_dossier_rejects_mixed_duplicate_and_unknown_evidence()
    {
        var templateId = await SeedQaQcReferencesAsync();
        var request = QaQcRequest(templateId);
        request.Evidence = [new QaQcEvidenceRequest { ProjectDocumentId = 901, EvidenceType = QaQcEvidenceType.Sample }];
        request.EvidenceDocumentIds = [902];
        await Assert.ThrowsAsync<ConstructionOperationException>(() =>
            _sut.CreateDossierAsync(ProjectId, request, BchUserId, default));

        request.EvidenceDocumentIds = [];
        request.Evidence.Add(new QaQcEvidenceRequest { ProjectDocumentId = 901, EvidenceType = QaQcEvidenceType.Sample });
        await Assert.ThrowsAsync<ConstructionOperationException>(() =>
            _sut.CreateDossierAsync(ProjectId, request, BchUserId, default));

        request.Evidence.RemoveAt(1);
        request.Evidence[0].EvidenceType = (QaQcEvidenceType)999;
        await Assert.ThrowsAsync<ConstructionOperationException>(() =>
            _sut.CreateDossierAsync(ProjectId, request, BchUserId, default));
        Assert.Empty(_db.QaQcDossiers);
    }

    [Fact]
    public async Task Preparation_get_recomputes_zero_permit_blocker_from_persisted_evidence()
    {
        var documents = Enumerable.Range(1, 9).Select(index => new ProjectDocument
        {
            Id = index,
            OperationalProjectId = ProjectId,
            Category = ProjectDocumentCategory.ConstructionAcceptance,
            LocalPath = $"/files/project-documents/readiness-{index}.pdf",
            OriginalFileName = $"readiness-{index}.pdf",
            Sha256 = new string('a', 64),
        }).ToList();
        _db.ProjectDocuments.AddRange(documents);
        _db.Contracts.Add(new Contract
        {
            Id = 601,
            OperationalProjectId = ProjectId,
            ContractNumber = "HD-READY-001",
            Direction = ContractDirection.Upstream,
            Type = ContractType.Construction,
            Status = ContractStatus.Signed,
            SignedDate = DateTime.UtcNow.Date,
        });
        _db.DesignProjects.Add(new DesignProject
        {
            Id = 701,
            OperationalProjectId = ProjectId,
            ProjectCode = "DP-READY-001",
            Name = "Readiness test project",
        });
        _db.IfcReleases.Add(new IfcRelease
        {
            Id = 801,
            DesignProjectId = 701,
            ReleaseNumber = "IFC-READY-001",
            Title = "Start package",
            Status = IfcReleaseStatus.Released,
        });
        _db.ConstructionPreparations.Add(new ConstructionPreparation
        {
            Id = 901,
            OperationalProjectId = ProjectId,
            WbsBaselineDocumentId = 1,
            ScheduleBaselineDocumentId = 2,
            SCurveBaselineDocumentId = 3,
            BchOrganizationDocumentId = 4,
            ResourcePlanDocumentId = 5,
            BudgetBaselineDocumentId = 6,
            SiteHandoverRequired = true,
            SiteHandoverDocumentId = 7,
            MethodStatementRequired = true,
            MethodStatementDocumentId = 8,
            HsePlanRequired = true,
            HsePlanDocumentId = 9,
            CreatedByUserId = CmUserId,
            UpdatedByUserId = CmUserId,
        });
        await _db.SaveChangesAsync();

        var response = await _sut.GetPreparationAsync(ProjectId, default);

        Assert.NotNull(response);
        Assert.Contains("valid required permits", response.MissingConditions);
        Assert.DoesNotContain("published WBS baseline evidence", response.MissingConditions);
    }

    [Fact]
    public async Task Qs_reversal_must_exactly_mirror_active_approved_predecessor()
    {
        await SeedDownstreamContractAsync();
        var predecessor = ApprovedQsBatch();
        _db.ConstructionQsBatches.Add(predecessor);
        await _db.SaveChangesAsync();

        var request = QsRequest();
        request.ReversesBatchId = predecessor.Id;
        request.CertifiedAmount = 900m;
        request.Lines[0].Quantity = 9m;

        var action = () => _sut.CreateQsAsync(ProjectId, request, BchUserId, default);

        var error = await Assert.ThrowsAsync<ConstructionOperationException>(action);
        Assert.Contains("phản chiếu chính xác", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Qs_predecessor_allows_only_one_active_successor()
    {
        await SeedDownstreamContractAsync();
        var predecessor = ApprovedQsBatch();
        _db.ConstructionQsBatches.Add(predecessor);
        await _db.SaveChangesAsync();
        _db.ConstructionQsBatches.Add(new ConstructionQsBatch
        {
            OperationalProjectId = ProjectId,
            Code = "QS-0041-0002",
            Direction = ConstructionQsDirection.Downstream,
            Status = ConstructionReviewStatus.PendingCmReview,
            ContractId = 501,
            WbsCode = "1.2.1",
            PeriodFrom = DateTime.UtcNow.Date,
            PeriodTo = DateTime.UtcNow.Date,
            CertifiedAmount = 1000m,
            Description = "First correction",
            CreatedByUserId = BchUserId,
            SupersedesBatchId = predecessor.Id,
            AdjustmentKind = ConstructionQsAdjustmentKind.SupersedingCorrection,
            IsActiveLineage = true,
        });
        await _db.SaveChangesAsync();
        var request = QsRequest();
        request.SupersedesBatchId = predecessor.Id;

        var error = await Assert.ThrowsAsync<ConstructionOperationException>(
            () => _sut.CreateQsAsync(ProjectId, request, BchUserId, default));

        Assert.Contains("lô kế nhiệm đang hiệu lực", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Qs_draft_correction_can_be_edited_before_approval()
    {
        await SeedDownstreamContractAsync();
        var predecessor = ApprovedQsBatch();
        _db.ConstructionQsBatches.Add(predecessor);
        await _db.SaveChangesAsync();
        var createRequest = QsRequest();
        createRequest.SupersedesBatchId = predecessor.Id;

        var created = await _sut.CreateQsAsync(ProjectId, createRequest, BchUserId, default);
        var updateRequest = QsRequest();
        updateRequest.SupersedesBatchId = predecessor.Id;
        updateRequest.Description = "Corrected draft quantities";

        var updated = await _sut.UpdateQsAsync(
            ProjectId,
            created.Id,
            updateRequest,
            created.RowVersion,
            BchUserId,
            default);

        Assert.NotNull(updated);
        Assert.Equal("Corrected draft quantities", updated.Description);
        Assert.Equal(ConstructionQsAdjustmentKind.SupersedingCorrection, updated.AdjustmentKind);
        Assert.Equal(ConstructionQsRecordState.Active, updated.RecordState);
        Assert.False((await _db.ConstructionQsBatches.FindAsync(created.Id))!.IsActiveLineage);
    }

    [Fact]
    public async Task Qs_reference_data_is_project_scoped_and_filters_approved_records()
    {
        await SeedDownstreamContractAsync();
        _db.ContractAppendices.AddRange(
            new ContractAppendix { ContractId = 501, VoNumber = 1, Title = "Approved VO", Reason = "Scope", Status = ContractAppendixStatus.Approved },
            new ContractAppendix { ContractId = 501, VoNumber = 2, Title = "Draft VO", Reason = "Draft", Status = ContractAppendixStatus.Draft });
        _db.ProjectBoqRevisions.AddRange(
            new ProjectBoqRevision { OperationalProjectId = ProjectId, RevisionNumber = 1, Status = ProjectBoqRevisionStatus.Approved, PreparedByUserId = CmUserId },
            new ProjectBoqRevision { OperationalProjectId = ProjectId, RevisionNumber = 2, Status = ProjectBoqRevisionStatus.Draft, PreparedByUserId = CmUserId },
            new ProjectBoqRevision { OperationalProjectId = 999, RevisionNumber = 1, Status = ProjectBoqRevisionStatus.Approved, PreparedByUserId = CmUserId });
        await _db.SaveChangesAsync();

        var result = await _sut.GetQsReferencesAsync(ProjectId, default);

        var contract = Assert.Single(result.Contracts);
        Assert.Equal(501, contract.Id);
        Assert.Single(contract.ApprovedAppendices);
        Assert.Single(result.ApprovedBoqRevisions);
        Assert.Equal(ProjectId, (await _db.ProjectBoqRevisions.FindAsync(result.ApprovedBoqRevisions[0].Id))!.OperationalProjectId);
    }

    [Fact]
    public async Task Qs_rejects_unapproved_contract_appendix_from_direct_api_request()
    {
        await SeedDownstreamContractAsync();
        var appendix = new ContractAppendix
        {
            ContractId = 501,
            VoNumber = 2,
            Title = "Unapproved scope change",
            Reason = "Awaiting CM decision",
            Status = ContractAppendixStatus.Draft,
        };
        _db.ContractAppendices.Add(appendix);
        await _db.SaveChangesAsync();
        var request = QsRequest();
        request.ContractAppendixId = appendix.Id;

        var error = await Assert.ThrowsAsync<ConstructionOperationException>(
            () => _sut.CreateQsAsync(ProjectId, request, BchUserId, default));

        Assert.Contains("VO/phụ lục phải được duyệt", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_db.ConstructionQsBatches);
    }

    [Fact]
    public async Task Material_resource_requires_handoff_assignees_before_submit()
    {
        var request = EquipmentRequest();
        request.Type = ConstructionResourceType.Material;
        request.ProjectBoqLineId = 77;

        var error = await Assert.ThrowsAsync<ConstructionOperationException>(
            () => _sut.CreateResourceAsync(ProjectId, request, BchUserId, default));

        Assert.Contains("người phụ trách tại công trường", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_db.ConstructionResourceRequests);
    }

    [Fact]
    public async Task Resource_reference_data_returns_project_users_and_current_boq_allowance()
    {
        var pmRole = new NihomeBackend.Models.Rbac.Role { Code = "PM", Name = "Project Manager" };
        var procurementRole = new NihomeBackend.Models.Rbac.Role { Code = "PROCUREMENT", Name = "Procurement" };
        _db.Roles.AddRange(pmRole, procurementRole);
        var siteUser = new ApplicationUser
        {
            Id = BchUserId,
            PhoneNumber = "0900000101",
            Email = "site@nicon.test",
            PasswordHash = "test",
            FullName = "NICON Site Engineer",
            RoleEntity = pmRole,
        };
        var procurementUser = new ApplicationUser
        {
            Id = 303,
            PhoneNumber = "0900000303",
            Email = "procurement@nicon.test",
            PasswordHash = "test",
            FullName = "NICON Procurement Owner",
            RoleEntity = procurementRole,
        };
        _db.Users.AddRange(siteUser, procurementUser);
        _db.OperationalProjects.Add(new OperationalProject
        {
            Id = ProjectId,
            Code = "NICON-M4-041",
            Name = "NICON Riverside Office",
            ProjectManagerUserId = BchUserId,
        });
        _db.OperationalProjectMembers.Add(new OperationalProjectMember
        {
            OperationalProjectId = ProjectId,
            UserId = procurementUser.Id,
            Position = "Procurement",
            StartedAt = DateTime.UtcNow.Date,
            CreatedByUserId = CmUserId,
            UpdatedByUserId = CmUserId,
        });
        var revision = new ProjectBoqRevision
        {
            OperationalProjectId = ProjectId,
            RevisionNumber = 2,
            Status = ProjectBoqRevisionStatus.Approved,
            ApprovedAt = DateTime.UtcNow,
            PreparedByUserId = CmUserId,
            Lines =
            [
                new ProjectBoqLine
                {
                    ItemCode = "MAT-STEEL-01",
                    Description = "Reinforcement steel",
                    Unit = "kg",
                    ApprovedQuantity = 1000m,
                    SortOrder = 0,
                },
            ],
        };
        _db.ProjectBoqRevisions.Add(revision);
        await _db.SaveChangesAsync();

        var result = await _sut.GetResourceReferencesAsync(ProjectId, default);

        var line = Assert.Single(result.BoqLines);
        Assert.Equal(1000m, line.RemainingQuantity);
        Assert.Contains(result.SiteUsers, user => user.UserId == siteUser.Id);
        Assert.Equal(procurementUser.Id, Assert.Single(result.ProcurementUsers).UserId);
    }

    private static UpsertConstructionResourceRequest EquipmentRequest() => new()
    {
        Type = ConstructionResourceType.Equipment,
        ProjectScheduleNodeId = WbsNodeId,
        WbsCode = "1.2.1",
        NeedByDate = DateTime.UtcNow.AddDays(7),
        Quantity = 2,
        Unit = "machine",
        Reason = "Two excavators are required for the approved earthwork sequence.",
    };

    private async Task<int> SeedQaQcReferencesAsync()
    {
        var template = new QaQcTemplate
        {
            Code = "ISO-QA-01",
            Version = 1,
            NameVi = "Hồ sơ chất lượng vật liệu",
            Name = "Material quality dossier",
            NameZh = "材料质量档案",
            NameJa = "材料品質記録",
            SchemaJson = """{"schemaVersion":1,"items":[{"code":"C1","title":"Chất lượng vật liệu","acceptanceCriteria":"Đúng hồ sơ được duyệt"}]}""",
            IsPublished = true,
            CreatedByUserId = CmUserId,
        };
        _db.QaQcTemplates.Add(template);
        _db.DesignProjects.Add(new DesignProject
        {
            Id = 701,
            OperationalProjectId = ProjectId,
            ProjectCode = "DP-QA-041",
            Name = "NICON quality control test project",
        });
        _db.IfcReleases.Add(new IfcRelease
        {
            Id = 801,
            DesignProjectId = 701,
            ReleaseNumber = "IFC-QA-041",
            Title = "Foundation works",
            Status = IfcReleaseStatus.Released,
        });
        _db.ProjectDocuments.AddRange(Enumerable.Range(901, 3).Select(id => new ProjectDocument
        {
            Id = id,
            OperationalProjectId = ProjectId,
            Category = ProjectDocumentCategory.ConstructionAcceptance,
            LocalPath = $"/files/project-documents/quality-{id}.pdf",
            OriginalFileName = $"quality-{id}.pdf",
            Sha256 = new string('a', 64),
        }));
        await _db.SaveChangesAsync();
        return template.Id;
    }

    private static CreateQaQcDossierRequest QaQcRequest(int templateId) => new()
    {
        TemplateId = templateId,
        ProjectScheduleNodeId = WbsNodeId,
        WbsCode = "1.2.1",
        Title = "Kiểm tra chất lượng bê tông móng",
        IfcReleaseId = 801,
        ChecklistResultsJson = """[{"code":"C1","result":"Pending","note":""}]""",
    };

    private async Task SeedDownstreamContractAsync()
    {
        _db.Contracts.Add(new Contract
        {
            Id = 501,
            OperationalProjectId = ProjectId,
            ContractNumber = "HD-DOWN-001",
            Direction = ContractDirection.Downstream,
            Type = ContractType.Subcontract,
            Status = ContractStatus.Signed,
            Value = 10_000m,
        });
        await _db.SaveChangesAsync();
    }

    private static ConstructionQsBatch ApprovedQsBatch() => new()
    {
        OperationalProjectId = ProjectId,
        Code = "QS-0041-0001",
        Direction = ConstructionQsDirection.Downstream,
        Status = ConstructionReviewStatus.CmApproved,
        ContractId = 501,
        WbsCode = "1.2.1",
        PeriodFrom = DateTime.UtcNow.Date,
        PeriodTo = DateTime.UtcNow.Date,
        CertifiedAmount = 1000m,
        Description = "Approved quantities",
        CreatedByUserId = BchUserId,
        RecordState = ConstructionQsRecordState.Active,
        Lines = [new ConstructionQsBatchLine { ItemCode = "QS-01", Description = "Work", Unit = "m2", Quantity = 10m, UnitPrice = 100m, Amount = 1000m }],
    };

    private static CreateConstructionQsBatchRequest QsRequest() => new()
    {
        Direction = ConstructionQsDirection.Downstream,
        ContractId = 501,
        ProjectScheduleNodeId = WbsNodeId,
        WbsCode = "1.2.1",
        PeriodFrom = DateTime.UtcNow.Date,
        PeriodTo = DateTime.UtcNow.Date,
        CertifiedAmount = 1000m,
        Description = "QS adjustment",
        Lines = [new ConstructionQsBatchLineRequest { ItemCode = "QS-01", Description = "Work", Unit = "m2", Quantity = 10m, UnitPrice = 100m }],
    };

    public void Dispose() => _db.Dispose();
}
