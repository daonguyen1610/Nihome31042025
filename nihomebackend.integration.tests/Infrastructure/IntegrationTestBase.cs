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
    /// other row has a tender-specific uploaded file.
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

            var items = await db.TenderChecklistItems
                .Where(item => item.TenderId == tenderId)
                .ToListAsync();
            foreach (var item in items)
            {
                item.Status = TenderChecklistItemStatus.Done;
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
            }

            await db.SaveChangesAsync();
            return document.Id;
        });
}
