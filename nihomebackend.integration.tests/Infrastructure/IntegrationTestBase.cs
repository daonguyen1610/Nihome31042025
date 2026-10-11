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
}
