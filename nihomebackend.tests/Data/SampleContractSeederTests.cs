using NihomeBackend.Data;
using NihomeBackend.Models;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

/// <summary>
/// Verifies the sample-contract branch of <see cref="SampleCrmDataSeeder"/>:
/// seeds sample customers first (required FK), runs the top-level seeder and
/// asserts every ContractStatus branch has coverage and re-runs are no-ops.
/// </summary>
public class SampleContractSeederTests : IDisposable
{
    private readonly AppDbContext _db;

    public SampleContractSeederTests()
    {
        _db = DbContextFactory.Create();
        // The seeder resolves an owner via the SALE test user's phone; when
        // absent it falls back to any SUPER_ADMIN row. Seed the minimum.
        _db.Users.Add(new ApplicationUser
        {
            PhoneNumber = "0335240370",
            FullName = "Super Admin",
            Email = "superadmin@example.com",
            Role = UserRole.SUPER_ADMIN,
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Seed_InsertsSampleContractsCoveringMultipleStatuses()
    {
        SampleCrmDataSeeder.Seed(_db);

        var contracts = _db.Contracts.ToList();
        Assert.NotEmpty(contracts);

        // The seed list drives one row per status entry we authored — assert
        // the flagship statuses are covered so filters + badge have data.
        var statuses = contracts.Select(c => c.Status).Distinct().ToList();
        Assert.Contains(ContractStatus.Draft, statuses);
        Assert.Contains(ContractStatus.Signed, statuses);
        Assert.Contains(ContractStatus.InProgress, statuses);
        Assert.Contains(ContractStatus.OnHold, statuses);
        Assert.Contains(ContractStatus.Completed, statuses);
        Assert.Equal(6, contracts.Count);

        Assert.All(contracts, contract =>
        {
            var opportunity = _db.Opportunities.Single(item => item.Id == contract.OpportunityId);
            Assert.Equal(opportunity.CustomerId, contract.CustomerId);
            if (contract.QuoteId.HasValue)
            {
                var quote = _db.Quotes.Single(item => item.Id == contract.QuoteId.Value);
                Assert.Equal(opportunity.Id, quote.OpportunityId);
            }
        });

        var wonOpportunity = _db.Opportunities.Single(item => item.Stage == OpportunityStage.Won);
        Assert.NotNull(wonOpportunity.WonQuoteId);
        var wonQuote = _db.Quotes.Single(item => item.Id == wonOpportunity.WonQuoteId);
        Assert.Equal(wonOpportunity.Id, wonQuote.OpportunityId);
        Assert.Equal(QuoteStatus.CustomerApproved, wonQuote.Status);
        var completed = Assert.Single(contracts, contract => contract.Status == ContractStatus.Completed);
        Assert.Equal(wonOpportunity.Id, completed.OpportunityId);
        Assert.Equal(wonQuote.Id, completed.QuoteId);

        var convertedLead = _db.Leads.Single(lead => lead.Status == LeadStatus.Converted);
        Assert.NotNull(convertedLead.ConvertedAt);
        Assert.Equal(convertedLead.ConvertedCustomerId,
            _db.Opportunities.Single(opportunity => opportunity.Id == convertedLead.ConvertedOpportunityId).CustomerId);
    }

    [Fact]
    public void Seed_GeneratesUniqueContractNumbers()
    {
        SampleCrmDataSeeder.Seed(_db);

        var numbers = _db.Contracts.Select(c => c.ContractNumber).ToList();
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
        Assert.All(numbers, n => Assert.StartsWith("HD-", n));
    }

    [Fact]
    public void Seed_IsIdempotent_AndPreservesAdminEdits()
    {
        SampleCrmDataSeeder.Seed(_db);
        var edited = _db.Contracts.First();
        var originalName = edited.ContractNumber;
        edited.Note = "[SAMPLE_CONTRACT] Custom admin note override";
        edited.Value = 999_999_999m;
        _db.SaveChanges();

        var countBefore = _db.Contracts.Count();
        SampleCrmDataSeeder.Seed(_db);
        var countAfter = _db.Contracts.Count();

        Assert.Equal(countBefore, countAfter);
        var reloaded = _db.Contracts.Single(c => c.ContractNumber == originalName);
        Assert.Equal("[SAMPLE_CONTRACT] Custom admin note override", reloaded.Note);
        Assert.Equal(999_999_999m, reloaded.Value);
    }

    [Fact]
    public void Seed_OperationalDataTargetsOnlySampleProjects_AndIsIdempotent()
    {
        var userCustomer = new Customer
        {
            Name = "Khách hàng do người dùng tạo",
            SourceCode = "manual",
        };
        _db.Customers.Add(userCustomer);
        _db.SaveChanges();
        var userProject = new DesignProject
        {
            ProjectCode = "DP-USER-0001",
            Name = "Dự án người dùng",
            CustomerId = userCustomer.Id,
            CurrentStage = DesignProjectStage.ShopDrawing,
            Status = DesignProjectStatus.Active,
            Note = "Ghi chú tùy chỉnh",
        };
        _db.DesignProjects.Add(userProject);
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        var sampleProjectIds = _db.DesignProjects
            .Where(project => project.Note != null && project.Note.StartsWith("[SAMPLE_DP]"))
            .Select(project => project.Id)
            .ToHashSet();
        Assert.NotEmpty(sampleProjectIds);
        Assert.DoesNotContain(_db.PermitChecklistItems, item => item.DesignProjectId == userProject.Id);
        Assert.All(_db.PermitChecklistItems, item => Assert.Contains(item.DesignProjectId, sampleProjectIds));

        var linkedProject = _db.DesignProjects.First(project => sampleProjectIds.Contains(project.Id)
            && project.ContractId.HasValue);
        var linkedContract = _db.Contracts.Single(contract => contract.Id == linkedProject.ContractId);
        Assert.Equal(linkedContract.CustomerId, linkedProject.CustomerId);
        var projectManagerRoleId = _db.Roles.Single(role => role.Code == "PM").Id;
        var designLeadRoleId = _db.Roles.Single(role => role.Code == "DESIGN_LEAD").Id;
        Assert.All(_db.DesignProjects.Where(project => sampleProjectIds.Contains(project.Id)), project =>
        {
            Assert.Equal(projectManagerRoleId,
                _db.Users.Single(user => user.Id == project.ProjectManagerUserId).RoleEntityId);
            Assert.Equal(designLeadRoleId,
                _db.Users.Single(user => user.Id == project.DesignLeadUserId).RoleEntityId);
        });

        Assert.All(_db.ConceptOptions.Where(item => item.Description != null && item.Description.StartsWith("[SAMPLE]")),
            item => Assert.Contains(item.DesignProjectId, sampleProjectIds));
        Assert.All(_db.BasicDesignDocs.Where(item => item.Note != null && item.Note.StartsWith("[SAMPLE_BD]")),
            item => Assert.Contains(item.DesignProjectId, sampleProjectIds));
        Assert.All(_db.ShopDrawings.Where(item => item.Note != null && item.Note.StartsWith("[SAMPLE_SD]")),
            item => Assert.Contains(item.DesignProjectId, sampleProjectIds));
        Assert.All(_db.IfcReleases.Where(item => item.Note != null && item.Note.StartsWith("[SAMPLE_IFC]")),
            item => Assert.Contains(item.DesignProjectId, sampleProjectIds));
        Assert.All(_db.ConstructionTasks.Where(item => item.Description != null && item.Description.StartsWith("[SAMPLE_CONSTR]")),
            item => Assert.Contains(item.DesignProjectId, sampleProjectIds));
        Assert.All(_db.SiteDiaries.Where(item => item.Note != null && item.Note.StartsWith("[SAMPLE_DIARY]")),
            item => Assert.Contains(item.DesignProjectId, sampleProjectIds));
        Assert.All(_db.PunchItems.Where(item => item.Note != null && item.Note.StartsWith("[SAMPLE_PUNCH]")),
            item => Assert.Contains(item.DesignProjectId, sampleProjectIds));
        Assert.All(_db.AcceptanceRecords.Where(item => item.Description != null && item.Description.StartsWith("[SAMPLE_ACCEPT]")),
            item => Assert.Contains(item.DesignProjectId, sampleProjectIds));
        Assert.All(_db.AsBuiltDocuments.Where(item => item.Note != null && item.Note.StartsWith("[SAMPLE_ASBUILT]")),
            item => Assert.Contains(item.DesignProjectId, sampleProjectIds));
        Assert.All(_db.HandoverRecords.Where(item => item.HandoverCode == "HO-SAMPLE-001"),
            item => Assert.Contains(item.DesignProjectId, sampleProjectIds));

        Assert.Contains(_db.CapabilityDocumentVersions, version => version.VersionNumber == 1);

        var showcaseProject = _db.DesignProjects.Single(project =>
            sampleProjectIds.Contains(project.Id)
            && project.CurrentStage == DesignProjectStage.ShopDrawing);
        Assert.Contains(_db.ConceptOptions, item => item.DesignProjectId == showcaseProject.Id
                                                && item.Status == ConceptOptionStatus.Finalized);
        Assert.Equal(3, _db.BasicDesignDocs.Count(item => item.DesignProjectId == showcaseProject.Id
                                                      && item.Note != null
                                                      && item.Note.StartsWith("[SAMPLE_BD_HISTORY]")));
        var showcaseDrawingIds = _db.ShopDrawings
            .Where(item => item.DesignProjectId == showcaseProject.Id)
            .Select(item => item.Id)
            .ToHashSet();
        Assert.NotEmpty(showcaseDrawingIds);
        Assert.Contains(_db.DrawingRevisions, revision =>
            revision.TargetType == DrawingRevisionTargetType.ShopDrawing
            && showcaseDrawingIds.Contains(revision.TargetId));
        Assert.Contains(_db.IfcReleases, release => release.DesignProjectId == showcaseProject.Id);

        var editedContract = _db.Contracts.First(contract =>
            contract.Note != null && contract.Note.StartsWith("[SAMPLE_CONTRACT]"));
        editedContract.Note = "[SAMPLE_CONTRACT] Nội dung quản trị đã chỉnh sửa";
        editedContract.Value = 123_456_789m;
        _db.SaveChanges();
        var countsBefore = RepresentativeCounts();

        DbSeeder.Seed(_db);

        Assert.Equal(countsBefore, RepresentativeCounts());
        Assert.Equal("[SAMPLE_CONTRACT] Nội dung quản trị đã chỉnh sửa", editedContract.Note);
        Assert.Equal(123_456_789m, editedContract.Value);
    }

    [Fact]
    public void Seed_DesignFilesAreValidSelfHealingAndPreserveCustomPaths()
    {
        var webRootPath = Path.Combine(Path.GetTempPath(), $"nihome-seed-{Guid.NewGuid():N}");
        try
        {
            SampleCrmDataSeeder.Seed(_db, webRootPath);

            var basicDocuments = _db.BasicDesignDocs
                .Where(item => item.Note != null && item.Note.StartsWith("[SAMPLE_BD]"))
                .OrderBy(item => item.Id)
                .ToList();
            var shopDrawings = _db.ShopDrawings
                .Where(item => item.Note != null && item.Note.StartsWith("[SAMPLE_SD]"))
                .OrderBy(item => item.Id)
                .ToList();
            Assert.NotEmpty(basicDocuments);
            Assert.NotEmpty(shopDrawings);

            foreach (var document in basicDocuments.Cast<object>().Concat(shopDrawings))
            {
                var metadata = document switch
                {
                    BasicDesignDoc basic => (basic.FilePath, basic.OriginalFileName, basic.FileSize, basic.ContentType),
                    ShopDrawing shop => (shop.FilePath, shop.OriginalFileName, shop.FileSize, shop.ContentType),
                    _ => throw new InvalidOperationException(),
                };
                Assert.StartsWith("/files/design/", metadata.FilePath);
                Assert.EndsWith(".pdf", metadata.OriginalFileName);
                Assert.True(metadata.FileSize > 0);
                Assert.Equal("application/pdf", metadata.ContentType);

                var fullPath = Path.Combine(webRootPath, metadata.FilePath!.TrimStart('/'));
                Assert.True(File.Exists(fullPath));
                Assert.StartsWith("%PDF", File.ReadAllText(fullPath));
            }

            var selfHealingDocument = basicDocuments[0];
            var selfHealingPath = Path.Combine(webRootPath, selfHealingDocument.FilePath!.TrimStart('/'));
            var expectedBytes = File.ReadAllBytes(selfHealingPath);
            File.Delete(selfHealingPath);

            var customizedDocument = basicDocuments[1];
            customizedDocument.FilePath = "/files/design/custom/admin-replacement.pdf";
            customizedDocument.OriginalFileName = "admin-replacement.pdf";
            customizedDocument.FileSize = 98765;
            customizedDocument.ContentType = "application/custom-pdf";
            _db.SaveChanges();

            SampleCrmDataSeeder.Seed(_db, webRootPath);

            Assert.Equal(expectedBytes, File.ReadAllBytes(selfHealingPath));
            Assert.Equal("/files/design/custom/admin-replacement.pdf", customizedDocument.FilePath);
            Assert.Equal("admin-replacement.pdf", customizedDocument.OriginalFileName);
            Assert.Equal(98765, customizedDocument.FileSize);
            Assert.Equal("application/custom-pdf", customizedDocument.ContentType);
        }
        finally
        {
            if (Directory.Exists(webRootPath)) Directory.Delete(webRootPath, recursive: true);
        }
    }

    private int[] RepresentativeCounts() =>
    [
        _db.Contracts.Count(),
        _db.DesignProjects.Count(),
        _db.PermitChecklistItems.Count(),
        _db.ConceptOptions.Count(),
        _db.BasicDesignDocs.Count(),
        _db.ShopDrawings.Count(),
        _db.ConstructionTasks.Count(),
        _db.AcceptanceRecords.Count(),
        _db.AsBuiltDocuments.Count(),
        _db.CapabilityDocumentVersions.Count(),
    ];
}
