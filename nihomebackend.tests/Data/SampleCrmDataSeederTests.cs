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
        SampleCrmDataSeeder.Seed(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Seed_UsesCompactLinkedBusinessScenarios()
    {
        Assert.Equal(2, _db.Leads.Count());
        Assert.Equal(5, _db.Customers.Count());
        Assert.Equal(6, _db.Opportunities.Count());
        Assert.Equal(8, _db.Quotes.Count());
        Assert.Equal(5, _db.OperationalProjects.Count());
        Assert.Equal(3, _db.DesignProjects.Count());
        Assert.Equal(2, _db.Surveys.Count());
        Assert.Equal(2, _db.Tenders.Count());
        Assert.DoesNotContain(_db.Leads, item => item.Name.Contains("[SAMPLE]"));
        Assert.DoesNotContain(_db.Customers, item => item.Name.Contains("[SAMPLE]"));
        Assert.DoesNotContain(_db.Opportunities, item => item.Name.Contains("[SAMPLE]"));
        Assert.DoesNotContain(_db.Vendors, item => item.CompanyName.Contains("[SAMPLE]"));
        Assert.DoesNotContain(_db.OperationalProjects, item => item.Name.Contains("[SAMPLE]"));
        Assert.DoesNotContain(_db.DesignProjects, item => item.Name.Contains("[SAMPLE]"));

        var projects = _db.OperationalProjects.ToDictionary(item => item.Id);
        foreach (var opportunity in _db.Opportunities)
        {
            var project = projects[Assert.IsType<int>(opportunity.OperationalProjectId)];
            Assert.Equal(opportunity.CustomerId, project.CustomerId);
        }
        foreach (var design in _db.DesignProjects)
        {
            var project = projects[Assert.IsType<int>(design.OperationalProjectId)];
            Assert.Equal(design.CustomerId, project.CustomerId);
            if (design.ContractId.HasValue)
                Assert.Equal(design.CustomerId, _db.Contracts.Single(item => item.Id == design.ContractId).CustomerId);
        }
        Assert.Null(_db.DesignProjects.Single(item => item.CurrentStage == DesignProjectStage.Concept).ContractId);
        foreach (var survey in _db.Surveys)
        {
            var opportunity = _db.Opportunities.Single(item => item.Id == survey.LinkedOpportunityId);
            Assert.Equal(opportunity.OperationalProjectId, survey.OperationalProjectId);
            Assert.Null(survey.LinkedProjectId);
        }
        foreach (var tender in _db.Tenders)
            Assert.Contains(_db.Customers, item => item.Id == tender.CustomerId);
        var wonTender = _db.Tenders.Single(item => item.Status == TenderStatus.Won);
        Assert.Equal(wonTender.CustomerId,
            _db.Opportunities.Single(item => item.Id == wonTender.WonOpportunityId).CustomerId);

        var anPhuDesign = _db.DesignProjects.Single(item => item.ProjectCode == "DP-SAMPLE-003");
        Assert.Contains("Nhà máy may An Phú", anPhuDesign.Name);
        Assert.Equal("Công ty Cổ phần May mặc An Phú",
            _db.Customers.Single(item => item.Id == anPhuDesign.CustomerId).Name);
        Assert.Contains("An Phú", projects[anPhuDesign.OperationalProjectId!.Value].Name);
        var anPhuContract = _db.Contracts.Single(item => item.Id == anPhuDesign.ContractId);
        Assert.Equal(ContractStatus.InProgress, anPhuContract.Status);
        var anPhuOpportunity = _db.Opportunities.Single(item => item.Id == anPhuContract.OpportunityId);
        Assert.Contains("Nhà máy may An Phú", anPhuOpportunity.Name);
        Assert.Equal(anPhuOpportunity.OperationalProjectId, anPhuDesign.OperationalProjectId);
        var anPhuSurvey = _db.Surveys.Single(item => item.LinkedOpportunityId == anPhuOpportunity.Id);
        Assert.True(projects[anPhuDesign.OperationalProjectId!.Value].CreatedAt < anPhuSurvey.SurveyDate);
        Assert.True(anPhuSurvey.SurveyDate < anPhuContract.SignedDate);
        Assert.Contains(_db.PermitChecklistItems, item => item.DesignProjectId == anPhuDesign.Id);
        var ifc = _db.IfcReleases.Single(item => item.DesignProjectId == anPhuDesign.Id);
        Assert.Contains("xưởng may", ifc.Title);
        Assert.Equal(IfcReleaseStatus.Released, ifc.Status);
        Assert.NotNull(ifc.ReleaseDate);
        var releasedDrawingIds = _db.IfcReleaseItems.Where(item => item.IfcReleaseId == ifc.Id)
            .Select(item => item.ShopDrawingId).ToHashSet();
        Assert.NotEmpty(releasedDrawingIds);
        Assert.All(_db.ShopDrawings.Where(item => releasedDrawingIds.Contains(item.Id)),
            item => Assert.Equal(ShopDrawingStatus.Released, item.Status));
        Assert.Contains(_db.ConstructionTasks, item => item.DesignProjectId == anPhuDesign.Id);
        Assert.True(DateOnly.FromDateTime(ifc.ReleaseDate.Value) < _db.ConstructionTasks
            .Where(item => item.DesignProjectId == anPhuDesign.Id)
            .Min(item => item.PlannedStart));
    }

    [Fact]
    public void Seed_CompleteRerun_PreservesStableIdsAndCounts()
    {
        var before = CaptureFingerprint();

        SampleCrmDataSeeder.Seed(_db);

        Assert.Equal(before, CaptureFingerprint());
    }

    [Fact]
    public void Seed_SameDisplayNames_DoNotClaimUnrelatedRecords()
    {
        var customer = new Customer
        {
            Name = "Nguyễn Văn An",
            SourceCode = "referral",
            Contacts = [new CustomerContact { FullName = "Nguyễn Văn An", Phone = "0912345678", IsPrimary = true }],
        };
        _db.Customers.Add(customer);
        _db.Leads.Add(new Lead { Name = "Nguyễn Văn An", Phone = "0912345678", SourceCode = "referral" });
        var opportunity = new Opportunity { Name = "Thiết kế concept nhà phố Nguyễn Văn An", Customer = customer };
        _db.Opportunities.Add(opportunity);
        _db.SaveChanges();

        SampleCrmDataSeeder.Seed(_db);

        Assert.DoesNotContain(_db.OpportunityActivities, item => item.OpportunityId == opportunity.Id);
        Assert.DoesNotContain(_db.CustomerActivities, item => item.CustomerId == customer.Id);
        Assert.Equal(2, _db.Leads.Count(item => item.Name == "Nguyễn Văn An"));
        Assert.Equal(2, _db.Customers.Count(item => item.Name == "Nguyễn Văn An"));
        Assert.Equal(2, _db.Opportunities.Count(item => item.Name == opportunity.Name));
    }

    [Fact]
    public void Seed_RfqScenario_UsesNamedProjectApprovedBoqAndCompetingBids()
    {
        RfqSampleDataSeeder.Seed(_db);
        RfqSampleDataSeeder.Seed(_db);

        var project = _db.OperationalProjects.Single(item => item.Code == "PJ-SAMPLE-RFQ");
        Assert.Equal("Nhà máy cơ khí Hòa Bình – KCN Quang Minh", project.Name);
        Assert.Equal("Công ty TNHH Cơ khí Hòa Bình",
            _db.Customers.Single(item => item.Id == project.CustomerId).Name);
        var revision = _db.ProjectBoqRevisions.Single(item => item.Id == project.FinalProjectBoqRevisionId);
        Assert.Equal(ProjectBoqRevisionStatus.Approved, revision.Status);

        var rfqs = _db.Rfqs.Where(item => item.OperationalProjectId == project.Id).OrderBy(item => item.Code).ToList();
        Assert.Equal(2, rfqs.Count);
        Assert.All(rfqs, item => Assert.DoesNotContain("[SAMPLE]", item.Title));
        Assert.Contains("cáp", rfqs[0].Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tủ phân phối", rfqs[1].Title, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RfqStatus.Draft, rfqs[0].Status);
        Assert.Equal(RfqStatus.Issued, rfqs[1].Status);
        Assert.All(rfqs, item => Assert.Equal(revision.Id, item.SourceBoqRevisionId));
        Assert.Equal(2, _db.RfqBids.Count(item => item.RfqId == rfqs[1].Id));

        var before = CaptureFingerprint();
        SampleCrmDataSeeder.Seed(_db);
        Assert.Equal(before, CaptureFingerprint());
        Assert.Equal(6, _db.Opportunities.Count());
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
    public void Seed_HardDeletedSampleCustomer_WithTombstone_DoesNotRecreateRoot()
    {
        var customer = _db.Customers.First(item => item.Name == "Nguyễn Văn An");
        var deletedName = customer.Name;
        _db.SeededRootDeletions.Add(new SeededRootDeletion
        {
            ResourceType = EntityTypes.Customer,
            ResourceKey = deletedName,
        });
        customer.Name = $"REMOVED-{customer.Id}";
        _db.SaveChanges();

        SampleCrmDataSeeder.Seed(_db);

        Assert.DoesNotContain(_db.Customers, item => item.Name == deletedName);
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
    public void Seed_HardDeletedBusinessRoots_WithTombstones_DoNotRecreateRootsOrCapabilityFile()
    {
        var opportunity = _db.Opportunities.First(item => item.Name == "Thiết kế concept nhà phố Nguyễn Văn An");
        var opportunityName = opportunity.Name;
        var contract = _db.Contracts.First(item => item.ContractNumber.StartsWith("HD-SAMPLE-"));
        var contractNumber = contract.ContractNumber;
        var survey = _db.Surveys.First(item => item.Code.StartsWith("SV-SAMPLE-"));
        var surveyCode = survey.Code;
        var tender = _db.Tenders.First(item => item.Code.StartsWith("TD-SAMPLE-"));
        var tenderCode = tender.Code;
        var tenderChecklistIds = _db.TenderChecklistItems.Where(item => item.TenderId == tender.Id)
            .Select(item => item.Id).ToList();
        var capability = _db.CapabilityDocuments.First(item =>
            item.Description != null && item.Description.StartsWith("[SAMPLE_CAP]"));
        var capabilityPath = capability.FilePath;
        _db.SeededRootDeletions.AddRange(
            new SeededRootDeletion { ResourceType = EntityTypes.Opportunity, ResourceKey = opportunityName },
            new SeededRootDeletion { ResourceType = EntityTypes.Contract, ResourceKey = contractNumber },
            new SeededRootDeletion { ResourceType = EntityTypes.Survey, ResourceKey = surveyCode },
            new SeededRootDeletion { ResourceType = EntityTypes.Tender, ResourceKey = tenderCode },
            new SeededRootDeletion { ResourceType = EntityTypes.CapabilityDocument, ResourceKey = capabilityPath });
        opportunity.Name = $"REMOVED-{opportunity.Id}";
        contract.ContractNumber = $"REMOVED-{contract.Id}";
        contract.Note = "Removed sample contract";
        survey.Code = $"REMOVED-{survey.Id}";
        survey.Note = "Removed sample survey";
        survey.Location = $"Removed location {survey.Id}";
        tender.Code = $"REMOVED-{tender.Id}";
        tender.Note = "Removed sample tender";
        _db.TenderChecklistItems.RemoveRange(_db.TenderChecklistItems.Where(item => item.TenderId == tender.Id));
        capability.FilePath = $"/files/capability/removed-{capability.Id}.pdf";
        capability.Description = "Removed sample capability";
        _db.SaveChanges();
        var webRootPath = Path.Combine(Path.GetTempPath(), $"nihome-tombstone-{Guid.NewGuid():N}");

        try
        {
            SampleCrmDataSeeder.Seed(_db, webRootPath);

            Assert.DoesNotContain(_db.Opportunities, item => item.Name == opportunityName);
            Assert.DoesNotContain(_db.Contracts, item => item.ContractNumber == contractNumber);
            Assert.DoesNotContain(_db.Surveys, item => item.Code == surveyCode);
            Assert.DoesNotContain(_db.Tenders, item => item.Code == tenderCode);
            Assert.DoesNotContain(_db.TenderChecklistItems, item => tenderChecklistIds.Contains(item.Id));
            Assert.DoesNotContain(_db.CapabilityDocuments, item => item.FilePath == capabilityPath);
            Assert.False(File.Exists(Path.Combine(webRootPath, capabilityPath.TrimStart('/'))));
        }
        finally
        {
            if (Directory.Exists(webRootPath)) Directory.Delete(webRootPath, recursive: true);
        }
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
        var removedSurvey = _db.Surveys.Single(item => item.Code == "SV-SAMPLE-002");
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
        var restoredSurvey = _db.Surveys.Single(item => item.Code == "SV-SAMPLE-002");
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

        var lead = _db.Leads.Where(item => item.Name == "Nguyễn Văn An")
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
