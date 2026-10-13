using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class DesignRecordDeletionControllerTests(NihomeWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task ConceptDelete_RequiresTypedConfirmationAndLeavesStateUnchangedOnRejection()
    {
        var fixture = await CreateDiscardedConceptAsync();
        await AuthTestHelper.AuthenticateAsync(
            Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        using var impactResponse = await Client.GetAsync(
            $"/api/concept-options/{fixture.ConceptId}/deletion-impact");
        impactResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var impact = await ReadJsonAsync(impactResponse);

        using var rejected = await Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete, $"/api/concept-options/{fixture.ConceptId}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = "SAI-MA-HO-SO",
            }),
        });

        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ConceptOptions.AnyAsync(item => item.Id == fixture.ConceptId)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ConceptDelete_WhenPlanChanges_ReturnsConflictAndPreservesRecord()
    {
        var fixture = await CreateDiscardedConceptAsync();
        await AuthTestHelper.AuthenticateAsync(
            Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        using var impactResponse = await Client.GetAsync(
            $"/api/concept-options/{fixture.ConceptId}/deletion-impact");
        impactResponse.EnsureSuccessStatusCode();
        var impact = await ReadJsonAsync(impactResponse);

        await WithDbAsync(async db =>
        {
            db.ProjectDocuments.Add(new ProjectDocument
            {
                OperationalProjectId = fixture.OperationalProjectId,
                Category = ProjectDocumentCategory.DesignConcept,
                SourceModule = ProjectDocumentSourceModule.Design,
                SourceEntityType = nameof(ConceptOption),
                SourceRecordId = fixture.ConceptId,
                LocalPath = $"/files/project-documents/{Guid.NewGuid():N}.pdf",
                OriginalFileName = "concept-plan-changed.pdf",
                Sha256 = new string('c', 64),
                SyncStatus = ProjectDocumentSyncStatus.Synced,
            });
            await db.SaveChangesAsync();
        });

        using var rejected = await Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete, $"/api/concept-options/{fixture.ConceptId}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
            }),
        });

        rejected.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.ConceptOptions.AnyAsync(item => item.Id == fixture.ConceptId)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ConceptDelete_WithCurrentPlanPermanentlyRemovesDiscardedRecord()
    {
        var fixture = await CreateDiscardedConceptAsync();
        await AuthTestHelper.AuthenticateAsync(
            Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        using var impactResponse = await Client.GetAsync(
            $"/api/concept-options/{fixture.ConceptId}/deletion-impact");
        impactResponse.EnsureSuccessStatusCode();
        var impact = await ReadJsonAsync(impactResponse);

        using var deleted = await Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete, $"/api/concept-options/{fixture.ConceptId}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
            }),
        });

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent, await deleted.Content.ReadAsStringAsync());
        (await WithDbAsync(db => db.ConceptOptions.AnyAsync(item => item.Id == fixture.ConceptId)))
            .Should().BeFalse();
    }

    private async Task<Fixture> CreateDiscardedConceptAsync() => await WithDbAsync(async db =>
    {
        var customer = new Customer
        {
            Name = $"Khách hàng xóa hồ sơ thiết kế {Guid.NewGuid():N}",
            Type = CustomerType.Company,
            SourceCode = "referral",
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var operational = new OperationalProject
        {
            Code = UniqueSlug("OP-DEL").ToUpperInvariant(),
            Name = "Dự án NICON kiểm tra xóa hồ sơ",
            CustomerId = customer.Id,
        };
        db.OperationalProjects.Add(operational);
        await db.SaveChangesAsync();
        var project = new DesignProject
        {
            OperationalProjectId = operational.Id,
            ProjectCode = UniqueSlug("DP-DEL").ToUpperInvariant(),
            Name = "Thiết kế nhà máy NICON",
            CustomerId = customer.Id,
            CurrentStage = DesignProjectStage.Concept,
        };
        db.DesignProjects.Add(project);
        await db.SaveChangesAsync();
        var concept = new ConceptOption
        {
            DesignProjectId = project.Id,
            Name = UniqueSlug("PA-KT").ToUpperInvariant(),
            Status = ConceptOptionStatus.Discarded,
        };
        db.ConceptOptions.Add(concept);
        await db.SaveChangesAsync();
        return new Fixture(operational.Id, concept.Id);
    });

    private sealed record Fixture(int OperationalProjectId, int ConceptId);
}
