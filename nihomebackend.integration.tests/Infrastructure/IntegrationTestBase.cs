using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests sharing a single web host per test class.
/// Uses a fresh HttpClient per test so authentication headers don't leak between tests.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<NihomeWebApplicationFactory>
{
    protected readonly NihomeWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(NihomeWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected T WithDb<T>(Func<AppDbContext, T> fn)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return fn(db);
    }

    protected async Task WithDbAsync(Func<AppDbContext, Task> fn)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await fn(db);
    }

    protected async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> fn)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await fn(db);
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>
    /// Exercises the permanent-delete contract after obtaining the current
    /// impact plan and its typed confirmation value.
    /// </summary>
    protected async Task<HttpResponseMessage> DeleteWithImpactAsync(string path)
    {
        using var impactResponse = await Client.GetAsync($"{path}/deletion-impact");
        impactResponse.EnsureSuccessStatusCode();
        var impact = await ReadJsonAsync(impactResponse);
        return await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, path)
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
            }),
        });
    }

    /// <summary>
    /// Creates synced Project Document evidence and the client-approved review
    /// required by the design stage gates.
    /// </summary>
    protected Task CreateClientApprovedDesignReviewAsync(
        int projectId, DesignReviewStage stage, params int[] sourceRecordIds) =>
        WithDbAsync(async db =>
        {
            var sourceEntityType = stage switch
            {
                DesignReviewStage.Concept => nameof(ConceptOption),
                DesignReviewStage.BasicDesign => nameof(BasicDesignDoc),
                _ => nameof(ShopDrawing),
            };
            var category = stage switch
            {
                DesignReviewStage.Concept => ProjectDocumentCategory.DesignConcept,
                DesignReviewStage.BasicDesign => ProjectDocumentCategory.DesignBasic,
                _ => ProjectDocumentCategory.DesignShopDrawing,
            };
            var project = await db.DesignProjects.AsNoTracking()
                .SingleAsync(item => item.Id == projectId);
            var managerId = await db.Users.AsNoTracking()
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["DESIGN"])
                .Select(user => user.Id)
                .SingleAsync();
            var documents = sourceRecordIds.Select(id => new ProjectDocument
            {
                OperationalProjectId = project.OperationalProjectId!.Value,
                Category = category,
                SourceModule = ProjectDocumentSourceModule.Design,
                SourceEntityType = sourceEntityType,
                SourceRecordId = id,
                LocalPath = $"/files/project-documents/{sourceEntityType}-{id}.pdf",
                OriginalFileName = $"{sourceEntityType}-{id}.pdf",
                ContentType = "application/pdf",
                Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes($"{sourceEntityType}:{id}"))).ToLowerInvariant(),
                SyncStatus = ProjectDocumentSyncStatus.Synced,
                CreatedByUserId = managerId,
                UpdatedByUserId = managerId,
            }).ToList();
            db.ProjectDocuments.AddRange(documents);
            await db.SaveChangesAsync();

            ConceptRevision? conceptRevision = null;
            ProjectDocument? acceptance = null;
            if (stage == DesignReviewStage.Concept)
            {
                sourceRecordIds.Should().ContainSingle();
                var sourceId = sourceRecordIds.Single();
                var option = await db.ConceptOptions.SingleAsync(item => item.Id == sourceId);
                option.Status = ConceptOptionStatus.PresentedToClient;
                option.PresentedAt = DateTime.UtcNow;
                conceptRevision = await db.ConceptRevisions
                    .Where(item => item.ConceptOptionId == sourceId)
                    .OrderByDescending(item => item.Number).FirstOrDefaultAsync();
                if (conceptRevision is null)
                {
                    conceptRevision = new ConceptRevision
                    {
                        ConceptOptionId = sourceId,
                        Number = 0,
                        CreatedByUserId = managerId,
                    };
                    db.ConceptRevisions.Add(conceptRevision);
                }
                conceptRevision.FrozenAt = DateTime.UtcNow;
                acceptance = new ProjectDocument
                {
                    OperationalProjectId = project.OperationalProjectId!.Value,
                    Category = ProjectDocumentCategory.DesignConcept,
                    SourceModule = ProjectDocumentSourceModule.Design,
                    SourceEntityType = nameof(ConceptOption),
                    SourceRecordId = sourceId,
                    LocalPath = $"/files/project-documents/TK-M03-{sourceId}.pdf",
                    OriginalFileName = $"TK-M03-{sourceId}-signed.pdf",
                    ContentType = "application/pdf",
                    Sha256 = new string('d', 64),
                    SyncStatus = ProjectDocumentSyncStatus.Synced,
                    CreatedByUserId = managerId,
                    UpdatedByUserId = managerId,
                };
                db.ProjectDocuments.Add(acceptance);
                await db.SaveChangesAsync();
            }

            var revision = await db.DesignReviews
                .Where(review => review.DesignProjectId == projectId && review.Stage == stage)
                .Select(review => (int?)review.RevisionNumber)
                .MaxAsync() ?? 0;
            db.DesignReviews.Add(new DesignReview
            {
                DesignProjectId = projectId,
                Stage = stage,
                ConceptRevisionId = conceptRevision?.Id,
                ClientAcceptanceDocumentId = acceptance?.Id,
                ClientAcceptanceSignedBy = acceptance is null ? null : "Đại diện chủ đầu tư",
                ClientAcceptanceSignedAt = acceptance is null ? null : DateTime.UtcNow,
                RevisionNumber = revision + 1,
                Title = $"{stage} client approval",
                Status = DesignReviewStatus.ClientApproved,
                DesignManagerUserId = managerId,
                CreatedByUserId = managerId,
                UpdatedByUserId = managerId,
                Documents = documents.Select(document => new DesignReviewDocument
                {
                    ProjectDocumentId = document.Id,
                    SourceEntityTypeSnapshot = sourceEntityType,
                    SourceRecordIdSnapshot = document.SourceRecordId!.Value,
                    Sha256Snapshot = document.Sha256,
                }).ToList(),
            });
            await db.SaveChangesAsync();
        });

    /// <summary>
    /// Exercises the public tender submission contract with a fresh concurrency
    /// token and idempotency key. Negative-path tests may inspect the response.
    /// </summary>
    protected async Task<HttpResponseMessage> SubmitTenderAsync(int tenderId)
    {
        using var detailResponse = await Client.GetAsync($"/api/tenders/{tenderId}");
        detailResponse.EnsureSuccessStatusCode();
        var rowVersion = (await ReadJsonAsync(detailResponse)).GetProperty("rowVersion").GetString();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/tenders/{tenderId}/submit")
        {
            Content = JsonContent.Create(new
            {
                submissionChannel = "Cổng đấu thầu quốc gia",
                receiptReference = $"TEST-{Guid.NewGuid():N}"[..21].ToUpperInvariant(),
                rowVersion,
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await Client.SendAsync(request);
    }

    /// <summary>Builds a short unique slug suitable for column length limits.</summary>
    protected static string UniqueSlug(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}".Substring(0, Math.Min(40, prefix.Length + 9));

    /// <summary>
    /// Creates a signed, in-force upstream contract for the project so
    /// post-contract controls such as the execution BOQ can be exercised.
    /// </summary>
    protected async Task<int> CreateSignedUpstreamContractAsync(int operationalProjectId) =>
        await WithDbAsync(async db =>
        {
            var customerId = await db.OperationalProjects.AsNoTracking()
                .Where(project => project.Id == operationalProjectId)
                .Select(project => project.CustomerId).SingleAsync();
            var contract = new Contract
            {
                ContractNumber = UniqueSlug("HD-TEST").ToUpperInvariant(),
                CustomerId = customerId,
                Direction = ContractDirection.Upstream,
                Type = ContractType.Construction,
                OperationalProjectId = operationalProjectId,
                Status = ContractStatus.Signed,
                SignedDate = DateTime.UtcNow.AddDays(-1),
                Value = 1_000_000_000m,
            };
            db.Contracts.Add(contract);
            await db.SaveChangesAsync();
            return contract.Id;
        });

    /// <summary>
    /// Records an approved Finance – Accounting review for a quote
    /// (<see cref="WorkPackagePurpose.FinanceCommercialReview"/>) or a tender
    /// (<see cref="WorkPackagePurpose.FinanceTenderReview"/>) so the pre-contract
    /// control that blocks sending/submitting can be satisfied.
    /// </summary>
    protected async Task<int> CreateApprovedFinanceReviewAsync(
        int operationalProjectId, string purpose, int? quoteId = null, int? tenderId = null) =>
        await WithDbAsync(async db =>
        {
            var groupId = await db.RoleGroups.AsNoTracking()
                .Where(group => group.Code == "FINANCE" && group.IsActive)
                .Select(group => group.Id).SingleAsync();
            var financeUserId = await db.Users.AsNoTracking()
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["ACCOUNTANT"])
                .Select(user => user.Id).SingleAsync();
            var reviewedQuoteVersion = quoteId.HasValue
                ? await db.Quotes.AsNoTracking().Where(quote => quote.Id == quoteId.Value)
                    .Select(quote => (int?)quote.Version).SingleAsync()
                : null;
            var now = DateTime.UtcNow;
            var package = new DepartmentWorkPackage
            {
                Code = UniqueSlug("WP-TEST").ToUpperInvariant(),
                Kind = WorkPackageKind.Project,
                Purpose = Enum.Parse<WorkPackagePurpose>(purpose),
                Name = "Finance review fixture",
                OperationalProjectId = operationalProjectId,
                SourceQuoteId = quoteId,
                SourceTenderId = tenderId,
                DepartmentRoleGroupId = groupId,
                LeadUserId = financeUserId,
                Status = WorkPackageStatus.Active,
                CreatedByUserId = financeUserId,
                UpdatedByUserId = financeUserId,
                FinanceReview = new DepartmentFinanceReview
                {
                    Status = FinanceReviewStatus.Approved,
                    ContractConditionsReviewed = true,
                    GuaranteesReviewed = true,
                    PaymentTermsReviewed = true,
                    FinancialDossierReviewed = true,
                    ReviewedQuoteVersion = reviewedQuoteVersion,
                    ReviewedAt = now,
                    ReviewedByUserId = financeUserId,
                    CreatedByUserId = financeUserId,
                    UpdatedByUserId = financeUserId,
                    SubmittedAt = now.AddHours(-1),
                },
            };
            db.DepartmentWorkPackages.Add(package);
            await db.SaveChangesAsync();
            return package.Id;
        });

    /// <summary>
    /// Creates the completed departmental plan required before a tender may be
    /// submitted. Tests focused on downstream handoffs can use this fixture
    /// without bypassing the production submission gate.
    /// </summary>
    protected async Task<int> CreateCompletedTenderPlanAsync(
        int operationalProjectId, int tenderId) =>
        await WithDbAsync(async db =>
        {
            var groupId = await db.RoleGroups.AsNoTracking()
                .Where(group => group.Code == "FINANCE" && group.IsActive)
                .Select(group => group.Id).SingleAsync();
            var leadUserId = await db.Users.AsNoTracking()
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["ACCOUNTANT"])
                .Select(user => user.Id).SingleAsync();
            var now = DateTime.UtcNow;
            var package = new DepartmentWorkPackage
            {
                Code = UniqueSlug("WP-TENDER-PLAN").ToUpperInvariant(),
                Kind = WorkPackageKind.Project,
                Purpose = WorkPackagePurpose.TenderPlanning,
                Name = "Completed tender plan fixture",
                OperationalProjectId = operationalProjectId,
                SourceTenderId = tenderId,
                DepartmentRoleGroupId = groupId,
                LeadUserId = leadUserId,
                Status = WorkPackageStatus.Completed,
                PlannedStart = now.Date.AddDays(-7),
                PlannedEnd = now.Date,
                CompletedAt = now,
                CreatedByUserId = leadUserId,
                UpdatedByUserId = leadUserId,
            };
            db.DepartmentWorkPackages.Add(package);
            await db.SaveChangesAsync();
            return package.Id;
        });

    /// <summary>
    /// Completes the tender checklist with the evidence required by the NICON
    /// dossier contract: capability comes from the shared library, while every
    /// other row has a tender-specific uploaded file. Completion and independent
    /// verification are recorded by different seeded business users.
    /// </summary>
    protected async Task<int> CompleteTenderChecklistWithEvidenceAsync(int tenderId) =>
        await WithDbAsync(async db =>
        {
            var document = new CapabilityDocument
            {
                Name = "Hồ sơ năng lực NICON dùng chung",
                TagCode = "capability",
                FilePath = $"/files/capability/{UniqueSlug("shared")}.pdf",
                OriginalFileName = "ho-so-nang-luc-nicon.pdf",
                FileSize = 1024,
                ContentType = "application/pdf",
                CurrentVersion = 1,
            };
            db.CapabilityDocuments.Add(document);

            var preparerUserId = await db.Users.AsNoTracking()
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["SALE"])
                .Select(user => user.Id).SingleAsync();
            var verifierUserId = await db.Users.AsNoTracking()
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["ACCOUNTANT"])
                .Select(user => user.Id).SingleAsync();
            var now = DateTime.UtcNow;

            var items = await db.TenderChecklistItems
                .Where(item => item.TenderId == tenderId)
                .ToListAsync();
            foreach (var item in items)
            {
                item.Status = TenderChecklistItemStatus.Submitted;
                if (item.TemplateCode == "capability")
                {
                    item.CapabilityDocument = document;
                    item.FilePath = document.FilePath;
                    item.OriginalFileName = document.OriginalFileName;
                }
                else
                {
                    item.CapabilityDocumentId = null;
                    item.FilePath = $"/files/tenders/test-{tenderId}-{item.Id}.pdf";
                    item.OriginalFileName = $"{item.TemplateCode ?? $"item-{item.Id}"}.pdf";
                }

                item.CompletedAt = now.AddMinutes(-5);
                item.CompletedByUserId = preparerUserId;
                item.VerifiedAt = now;
                item.VerifiedByUserId = verifierUserId;
                var evidence = new TenderChecklistEvidence
                {
                    ChecklistItem = item,
                    Revision = 1,
                    Source = item.TemplateCode == "capability"
                        ? TenderEvidenceSource.CapabilityLibrary
                        : TenderEvidenceSource.DirectUpload,
                    FilePath = item.FilePath,
                    OriginalFileName = item.OriginalFileName,
                    FileSize = 1024,
                    ContentType = "application/pdf",
                    CapabilityDocument = item.TemplateCode == "capability" ? document : null,
                    CapabilityDocumentVersion = item.TemplateCode == "capability" ? document.CurrentVersion : null,
                    CreatedByUserId = preparerUserId,
                    CreatedAt = now.AddMinutes(-5),
                };
                db.TenderChecklistEvidence.Add(evidence);
                item.CurrentEvidence = evidence;
            }

            await db.SaveChangesAsync();
            return document.Id;
        });
}
