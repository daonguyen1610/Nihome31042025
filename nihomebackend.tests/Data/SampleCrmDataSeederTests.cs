using NihomeBackend.Data;
using NihomeBackend.Constants;
using NihomeBackend.Models;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

public class SampleCrmDataSeederTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();

    public SampleCrmDataSeederTests()
    {
        DbSeeder.Seed(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Seed_CompleteRerun_PreservesStableIdsAndCounts()
    {
        var before = CaptureFingerprint();

        SampleCrmDataSeeder.Seed(_db);

        Assert.Equal(before, CaptureFingerprint());
    }

    [Fact]
    public void Seed_HardDeletedSampleDesignProject_WithTombstone_DoesNotRecreateRoot()
    {
        var project = _db.DesignProjects.Single(item => item.ProjectCode == "DP-SAMPLE-001");
        _db.SeededRootDeletions.Add(new SeededRootDeletion
        {
            ResourceType = EntityTypes.DesignProject,
            ResourceKey = project.ProjectCode,
        });
        _db.DesignProjects.Remove(project);
        _db.SaveChanges();

        SampleCrmDataSeeder.Seed(_db);

        Assert.DoesNotContain(_db.DesignProjects, item => item.ProjectCode == "DP-SAMPLE-001");
    }

    [Fact]
    public void Seed_HardDeletedSampleOperationalProject_WithTombstones_DoesNotRecreateRoots()
    {
        var operationalProject = _db.OperationalProjects
            .First(item => item.Code.StartsWith("PJ-SAMPLE-"));
        var deletedCode = operationalProject.Code;
        _db.SeededRootDeletions.Add(new SeededRootDeletion
        {
            ResourceType = EntityTypes.OperationalProject,
            ResourceKey = deletedCode,
        });
        operationalProject.Code = $"REMOVED-{operationalProject.Id}";
        _db.SaveChanges();

        SampleCrmDataSeeder.Seed(_db);

        Assert.DoesNotContain(_db.OperationalProjects, item => item.Code == deletedCode);
    }

    [Fact]
    public void Seed_PartialDeletion_RestoresOnlyMissingCanonicalRowsAndChildren()
    {
        var contract = _db.Contracts
            .Where(item => item.Note != null && item.Note.StartsWith("[SAMPLE_CONTRACT]")
                && item.Status == ContractStatus.InProgress)
            .OrderBy(item => item.ContractNumber)
            .First();
        var preservedMilestone = _db.ContractPaymentMilestones.Single(item =>
            item.ContractId == contract.Id && item.Order == 1);
        var removedMilestone = _db.ContractPaymentMilestones.Single(item =>
            item.ContractId == contract.Id && item.Order == 2);
        var removedAppendix = _db.ContractAppendices.Single(item =>
            item.ContractId == contract.Id && item.VoNumber == 2);
        var project = _db.DesignProjects.Single(item => item.ProjectCode == "DP-SAMPLE-003");
        var preservedDrawing = _db.ShopDrawings.Single(item =>
            item.DesignProjectId == project.Id && item.DrawingCode == "KT-SD-001");
        var removedDrawing = _db.ShopDrawings.Single(item =>
            item.DesignProjectId == project.Id && item.DrawingCode == "KT-SD-002");
        var removedTask = _db.ConstructionTasks.Single(item =>
            item.DesignProjectId == project.Id && item.TaskCode == "T-004");
        var removedPunch = _db.PunchItems.Single(item =>
            item.DesignProjectId == project.Id && item.PunchCode == "P-003");
        var removedAcceptance = _db.AcceptanceRecords.Single(item =>
            item.DesignProjectId == project.Id && item.AcceptanceCode == "A-003");
        var removedAsBuilt = _db.AsBuiltDocuments.Single(item =>
            item.DesignProjectId == project.Id && item.DocumentCode == "AB-006");
        var removedVendor = _db.Vendors.Single(item => item.VendorCode == "TP-MEP-01");
        var removedSurvey = _db.Surveys.Single(item => item.Code == "SV-SAMPLE-005");
        var removedSurveyChecklist = _db.SurveyChecklistResults
            .Where(item => item.SurveyId == removedSurvey.Id)
            .ToList();
        var release = _db.IfcReleases
            .OrderBy(item => item.Id)
            .First(item => item.Note != null && item.Note.StartsWith("[SAMPLE_IFC]"));
        var removedReleaseItem = _db.IfcReleaseItems
            .Where(item => item.IfcReleaseId == release.Id)
            .OrderBy(item => item.Id)
            .First();
        var handover = _db.HandoverRecords.Single(item => item.HandoverCode == "HO-SAMPLE-001");
        var removedHandoverHistory = _db.HandoverStatusHistory.Single(item =>
            item.HandoverRecordId == handover.Id && item.FromStatus == null);
        var capability = _db.CapabilityDocuments.Single(item =>
            item.FilePath == "/files/capability/phap-nhan-erc.pdf");
        var removedCapabilityVersion = _db.CapabilityDocumentVersions.Single(item =>
            item.CapabilityDocumentId == capability.Id && item.VersionNumber == 1);
        var tender = _db.Tenders.Single(item => item.Code == "TD-SAMPLE-001");
        var removedChecklistItem = _db.TenderChecklistItems
            .Where(item => item.TenderId == tender.Id)
            .OrderBy(item => item.SortOrder)
            .First();
        var preservedIds = new
        {
            Contract = contract.Id,
            Milestone = preservedMilestone.Id,
            Drawing = preservedDrawing.Id,
            Project = project.Id,
            Tender = tender.Id,
        };

        _db.RemoveRange(removedMilestone, removedAppendix, removedDrawing, removedTask,
            removedPunch, removedAcceptance, removedAsBuilt, removedVendor, removedReleaseItem,
            removedHandoverHistory, removedCapabilityVersion, removedChecklistItem);
        _db.SurveyChecklistResults.RemoveRange(removedSurveyChecklist);
        _db.Surveys.Remove(removedSurvey);
        _db.SaveChanges();

        SampleCrmDataSeeder.Seed(_db);

        Assert.Equal(preservedIds.Contract, contract.Id);
        Assert.Equal(preservedIds.Milestone, preservedMilestone.Id);
        Assert.Equal(preservedIds.Drawing, preservedDrawing.Id);
        Assert.Equal(preservedIds.Project, project.Id);
        Assert.Equal(preservedIds.Tender, tender.Id);
        Assert.Equal(4, _db.ContractPaymentMilestones.Count(item => item.ContractId == contract.Id));
        Assert.Contains(_db.ContractAppendices, item => item.ContractId == contract.Id && item.VoNumber == 2);
        Assert.Contains(_db.ShopDrawings, item => item.DesignProjectId == project.Id && item.DrawingCode == "KT-SD-002");
        Assert.Contains(_db.ConstructionTasks, item => item.DesignProjectId == project.Id && item.TaskCode == "T-004");
        Assert.Contains(_db.PunchItems, item => item.DesignProjectId == project.Id && item.PunchCode == "P-003");
        Assert.Contains(_db.AcceptanceRecords, item => item.DesignProjectId == project.Id && item.AcceptanceCode == "A-003");
        Assert.Contains(_db.AsBuiltDocuments, item => item.DesignProjectId == project.Id && item.DocumentCode == "AB-006");
        Assert.Contains(_db.Vendors, item => item.VendorCode == "TP-MEP-01");
        var restoredSurvey = _db.Surveys.Single(item => item.Code == "SV-SAMPLE-005");
        Assert.NotEmpty(_db.SurveyChecklistResults.Where(item => item.SurveyId == restoredSurvey.Id));
        Assert.Contains(_db.IfcReleaseItems, item => item.IfcReleaseId == release.Id
            && item.ShopDrawingId == removedReleaseItem.ShopDrawingId);
        Assert.Contains(_db.HandoverStatusHistory, item => item.HandoverRecordId == handover.Id
            && item.FromStatus == null && item.ToStatus == HandoverStatus.Draft);
        Assert.Contains(_db.CapabilityDocumentVersions, item => item.CapabilityDocumentId == capability.Id
            && item.VersionNumber == 1);
        Assert.Contains(_db.TenderChecklistItems, item => item.TenderId == tender.Id
            && item.TemplateCode == removedChecklistItem.TemplateCode);
    }

    [Fact]
    public void Seed_CustomizedCanonicalRows_PreservesAdminValues()
    {
        var contract = _db.Contracts.Single(item => item.ContractNumber == "HD-SAMPLE-003");
        var drawing = _db.ShopDrawings.Single(item => item.DrawingCode == "KT-SD-001");
        var capability = _db.CapabilityDocuments.Single(item =>
            item.FilePath == "/files/capability/phap-nhan-erc.pdf");
        contract.Value = 987_654_321m;
        contract.Note = "[SAMPLE_CONTRACT] Nội dung quản trị tùy chỉnh";
        drawing.Title = "Bản vẽ quản trị đã đổi tên";
        drawing.Note = "Ghi chú quản trị không còn marker";
        capability.Name = "Hồ sơ năng lực quản trị tùy chỉnh";
        capability.Description = "Mô tả quản trị không còn marker";
        _db.SaveChanges();

        SampleCrmDataSeeder.Seed(_db);

        Assert.Equal(987_654_321m, contract.Value);
        Assert.Equal("[SAMPLE_CONTRACT] Nội dung quản trị tùy chỉnh", contract.Note);
        Assert.Equal("Bản vẽ quản trị đã đổi tên", drawing.Title);
        Assert.Equal("Ghi chú quản trị không còn marker", drawing.Note);
        Assert.Equal("Hồ sơ năng lực quản trị tùy chỉnh", capability.Name);
        Assert.Equal("Mô tả quản trị không còn marker", capability.Description);
    }

    [Fact]
    public void Seed_AdminManagedRelationshipsAndLeadLifecycle_PreservesChangesOnRerun()
    {
        var contract = _db.Contracts.Single(item => item.ContractNumber == "HD-SAMPLE-003");
        var alternateOpportunity = _db.Opportunities
            .Where(item => item.Id != contract.OpportunityId)
            .OrderBy(item => item.Id)
            .First(item => _db.Quotes.Any(quote => quote.OpportunityId == item.Id));
        var alternateQuote = _db.Quotes.First(item => item.OpportunityId == alternateOpportunity.Id);
        var alternateOperationalProject = _db.OperationalProjects
            .First(item => item.Id != contract.OperationalProjectId);
        contract.OpportunityId = alternateOpportunity.Id;
        contract.QuoteId = alternateQuote.Id;
        contract.OperationalProjectId = alternateOperationalProject.Id;

        var designProject = _db.DesignProjects.Single(item => item.ProjectCode == "DP-SAMPLE-003");
        var alternateContract = _db.Contracts.Single(item => item.ContractNumber == "HD-SAMPLE-006");
        var alternateCustomer = _db.Customers.First(item => item.Id != designProject.CustomerId);
        var alternateOwners = _db.Users
            .Where(item => item.Id != designProject.ProjectManagerUserId
                && item.Id != designProject.DesignLeadUserId)
            .OrderBy(item => item.Id)
            .Take(2)
            .ToList();
        designProject.ContractId = alternateContract.Id;
        designProject.CustomerId = alternateCustomer.Id;
        designProject.ProjectManagerUserId = alternateOwners[0].Id;
        designProject.DesignLeadUserId = alternateOwners[1].Id;

        var lead = _db.Leads.Where(item => item.Name.StartsWith("[SAMPLE]"))
            .OrderBy(item => item.Id).First();
        var convertedAt = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        lead.Status = LeadStatus.Junk;
        lead.ConvertedCustomerId = alternateCustomer.Id;
        lead.ConvertedOpportunityId = alternateOpportunity.Id;
        lead.ConvertedAt = convertedAt;
        _db.SaveChanges();

        SampleCrmDataSeeder.Seed(_db);

        Assert.Equal(alternateOpportunity.Id, contract.OpportunityId);
        Assert.Equal(alternateQuote.Id, contract.QuoteId);
        Assert.Equal(alternateOperationalProject.Id, contract.OperationalProjectId);
        Assert.Equal(alternateContract.Id, designProject.ContractId);
        Assert.Equal(alternateCustomer.Id, designProject.CustomerId);
        Assert.Equal(alternateOwners[0].Id, designProject.ProjectManagerUserId);
        Assert.Equal(alternateOwners[1].Id, designProject.DesignLeadUserId);
        Assert.Equal(LeadStatus.Junk, lead.Status);
        Assert.Equal(alternateCustomer.Id, lead.ConvertedCustomerId);
        Assert.Equal(alternateOpportunity.Id, lead.ConvertedOpportunityId);
        Assert.Equal(convertedAt, lead.ConvertedAt);
    }

    [Fact]
    public void Seed_PreexistingNaturalKeysWithoutSampleMarkers_DoesNotCollideOrOverwrite()
    {
        using var db = DbContextFactory.Create();
        db.Users.Add(new ApplicationUser
        {
            PhoneNumber = "0999000001",
            FullName = "Existing Administrator",
            Email = "existing.admin@example.com",
            Role = UserRole.SUPER_ADMIN,
            IsActive = true,
        });
        db.Vendors.Add(new Vendor
        {
            VendorCode = "NCC-ELECTRIC-01",
            CompanyName = "Nhà cung cấp hiện hữu",
            VendorType = VendorType.Supplier,
            IsActive = true,
        });
        db.AsBuiltDocumentCategories.Add(new AsBuiltDocumentCategory
        {
            Code = AsBuiltCategoryCodes.Drawing,
            Name = "Danh mục quản trị hiện hữu",
            NameVi = "Danh mục quản trị hiện hữu",
            NameEn = "Existing admin category",
            NameZh = "现有管理类别",
            NameJa = "既存管理カテゴリ",
            SortOrder = 99,
            IsActive = false,
        });
        db.SaveChanges();

        SampleCrmDataSeeder.Seed(db);

        var vendor = Assert.Single(db.Vendors.Where(item => item.VendorCode == "NCC-ELECTRIC-01"));
        Assert.Equal("Nhà cung cấp hiện hữu", vendor.CompanyName);
        var category = Assert.Single(db.AsBuiltDocumentCategories.Where(item =>
            item.Code == AsBuiltCategoryCodes.Drawing));
        Assert.Equal("Danh mục quản trị hiện hữu", category.Name);
        Assert.Equal(99, category.SortOrder);
        Assert.False(category.IsActive);
    }

    private string CaptureFingerprint()
    {
        static string Rows(IEnumerable<(string Code, int Id)> rows) =>
            string.Join(',', rows.Select(item => $"{item.Code}:{item.Id}"));

        var identities = new[]
        {
            Rows(_db.Quotes.OrderBy(item => item.Code).Select(item => new ValueTuple<string, int>(item.Code, item.Id))),
            Rows(_db.Contracts.OrderBy(item => item.ContractNumber).Select(item => new ValueTuple<string, int>(item.ContractNumber, item.Id))),
            Rows(_db.Tenders.OrderBy(item => item.Code).Select(item => new ValueTuple<string, int>(item.Code, item.Id))),
            Rows(_db.Surveys.OrderBy(item => item.Code).Select(item => new ValueTuple<string, int>(item.Code, item.Id))),
            Rows(_db.OperationalProjects.OrderBy(item => item.Code).Select(item => new ValueTuple<string, int>(item.Code, item.Id))),
            Rows(_db.DesignProjects.OrderBy(item => item.ProjectCode).Select(item => new ValueTuple<string, int>(item.ProjectCode, item.Id))),
        };
        var counts = new[]
        {
            _db.ContractPaymentMilestones.Count(), _db.ContractAppendices.Count(),
            _db.ContractAttachments.Count(), _db.TenderChecklistItems.Count(),
            _db.SurveyChecklistResults.Count(), _db.BasicDesignDocs.Count(),
            _db.ShopDrawings.Count(), _db.DrawingRevisions.Count(),
            _db.ConstructionTasks.Count(), _db.ConstructionTaskDependencies.Count(),
            _db.SiteDiaries.Count(), _db.PunchItems.Count(),
            _db.AcceptanceRecords.Count(), _db.AsBuiltDocuments.Count(),
        };
        return $"{string.Join('|', identities)}|{string.Join(',', counts)}";
    }
}
