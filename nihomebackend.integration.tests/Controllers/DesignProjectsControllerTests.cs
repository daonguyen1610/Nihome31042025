using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>DesignProjectsController</c> (NIH-113):
/// RBAC gating, list + get, CRUD happy paths + the auto-create hook
/// fired by <c>ContractsController</c> transitions.
/// </summary>
public class DesignProjectsControllerTests : IntegrationTestBase
{
    public DesignProjectsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/design-projects")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletionImpact_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/design-projects/1/deletion-impact"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsWarehouse_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/design-projects")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsPm_ReturnsOk()
    {
        // PM has design.projects.view (read-only).
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));
        var res = await Client.GetAsync("/api/design-projects");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("items").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public async Task Pm_CannotCreate()
    {
        // PM has view but not manage.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));
        var customerId = await FirstCustomerIdAsync();
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = "PM blocked create",
            customerId,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Pm_CannotPreviewDeletionImpact()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Restricted deletion preview");
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));

        var response = await Client.GetAsync($"/api/design-projects/{id}/deletion-impact");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.DesignProjects.AnyAsync(project => project.Id == id)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Create_HappyPath_AsSuperAdmin_ReturnsAutoCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"DP integ {Guid.NewGuid():N}",
            customerId,
            operationalProjectId,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("projectCode").GetString().Should().StartWith("DP-");
        body.GetProperty("currentStage").GetString().Should().Be("Concept");
        body.GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task Create_UnknownCustomer_IsNotFoundAfterAccessCheck()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = "Bad customer",
            customerId = 9999999,
        });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_UnknownId_Is404()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        (await Client.GetAsync("/api/design-projects/9999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ExtraCurrentStage_DoesNotChangeServerOwnedStage()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Update round-trip");
        var operationalProjectId = await WithDbAsync<int>(db => db.DesignProjects
            .Where(project => project.Id == id)
            .Select(project => project.OperationalProjectId!.Value)
            .SingleAsync());
        var res = await Client.PutAsJsonAsync($"/api/design-projects/{id}", new
        {
            name = "Update round-trip",
            customerId,
            operationalProjectId,
            currentStage = "BasicDesign",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("currentStage").GetString().Should().Be("Concept");
    }

    [Fact]
    public async Task Delete_BeyondConcept_PreviewsAndDeletesOwnedHistory()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Delete aggregate after stage");
        await WithDbAsync(async db =>
        {
            var project = await db.DesignProjects.FindAsync(id);
            project!.CurrentStage = DesignProjectStage.BasicDesign;
            await db.SaveChangesAsync();
        });
        var userId = await WithDbAsync<int>(db => db.Users.Select(user => user.Id).FirstAsync());
        var taskId = await WithDbAsync<int>(async db =>
        {
            var task = new ConstructionTask
            {
                DesignProjectId = id,
                TaskCode = $"T-{Guid.NewGuid():N}",
                Name = "Aggregate task",
                PlannedStart = new DateOnly(2026, 8, 1),
                PlannedEnd = new DateOnly(2026, 8, 2),
            };
            db.ConstructionTasks.Add(task);
            await db.SaveChangesAsync();
            db.ConstructionTaskDependencies.Add(new ConstructionTaskDependency
            {
                TaskId = task.Id,
                PredecessorTaskId = task.Id,
            });
            db.AcceptanceRecords.Add(new AcceptanceRecord
            {
                DesignProjectId = id,
                ConstructionTaskId = task.Id,
                AcceptanceCode = $"A-{Guid.NewGuid():N}",
                Title = "Acceptance blocker",
                AcceptanceDate = new DateOnly(2026, 8, 3),
            });
            db.HandoverRecords.Add(new HandoverRecord
            {
                DesignProjectId = id,
                HandoverCode = $"H-{Guid.NewGuid():N}",
                Title = "Handover blocker",
                PlannedHandoverDate = new DateOnly(2026, 8, 4),
                ResponsibleUserId = userId,
                CreatedByUserId = userId,
                UpdatedByUserId = userId,
            });
            await db.SaveChangesAsync();
            var handover = await db.HandoverRecords.SingleAsync(record => record.DesignProjectId == id);
            db.HandoverStatusHistory.Add(new HandoverStatusHistory
            {
                HandoverRecordId = handover.Id,
                ToStatus = HandoverStatus.ReadyForHandover,
                Note = "Ready before aggregate deletion",
                ChangedByUserId = userId,
            });
            await db.SaveChangesAsync();
            return task.Id;
        });

        var impactResponse = await Client.GetAsync($"/api/design-projects/{id}/deletion-impact");
        impactResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var impact = await ReadJsonAsync(impactResponse);
        impact.GetProperty("canDelete").GetBoolean().Should().BeTrue();
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "design.constructionTasks" &&
            item.GetProperty("count").GetInt32() == 1);
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "design.handoverHistory" &&
            item.GetProperty("count").GetInt32() == 1);
        var delete = await Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/design-projects/{id}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
            }),
        });
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await WithDbAsync(async db =>
        {
            (await db.DesignProjects.AnyAsync(project => project.Id == id)).Should().BeFalse();
            (await db.ConstructionTaskDependencies.AnyAsync(dependency => dependency.TaskId == taskId
                || dependency.PredecessorTaskId == taskId)).Should().BeFalse();
            (await db.AcceptanceRecords.AnyAsync(record => record.DesignProjectId == id)).Should().BeFalse();
            (await db.HandoverRecords.AnyAsync(record => record.DesignProjectId == id)).Should().BeFalse();
            (await db.HandoverStatusHistory.AnyAsync()).Should().BeFalse();
            (await db.Customers.AnyAsync(customer => customer.Id == customerId)).Should().BeTrue();
            (await db.Users.AnyAsync(user => user.Id == userId)).Should().BeTrue();
        });
    }

    [Fact]
    public async Task Delete_Concept_Succeeds()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Delete concept");
        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/design-projects/{id}/deletion-impact"));
        var delete = await Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/design-projects/{id}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
            }),
        });
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/design-projects/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithInvalidConfirmation_ReturnsBadRequestAndPreservesProject()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Invalid confirmation");
        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/design-projects/{id}/deletion-impact"));

        var delete = await Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/design-projects/{id}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = "WRONG-CODE",
            }),
        });

        delete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DesignProjects.AnyAsync(project => project.Id == id)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithoutConfirmationBody_ReturnsClientErrorAndPreservesProject()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Missing confirmation body");

        var delete = await Client.DeleteAsync($"/api/design-projects/{id}");

        ((int)delete.StatusCode).Should().BeInRange(400, 499);
        (await WithDbAsync(db => db.DesignProjects.AnyAsync(project => project.Id == id)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WhenImpactChanged_ReturnsConflictAndPreservesProject()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Stale deletion plan");
        var userId = await WithDbAsync(db => db.Users.Select(user => user.Id).FirstAsync());
        var handoverId = await WithDbAsync<int>(async db =>
        {
            var handover = new HandoverRecord
            {
                DesignProjectId = id,
                HandoverCode = $"H-{Guid.NewGuid():N}",
                Title = "Nested stale-plan fixture",
                PlannedHandoverDate = new DateOnly(2026, 8, 4),
                ResponsibleUserId = userId,
                CreatedByUserId = userId,
                UpdatedByUserId = userId,
            };
            db.HandoverRecords.Add(handover);
            await db.SaveChangesAsync();
            return handover.Id;
        });
        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/design-projects/{id}/deletion-impact"));
        await WithDbAsync(async db =>
        {
            db.HandoverStatusHistory.Add(new HandoverStatusHistory
            {
                HandoverRecordId = handoverId,
                ToStatus = HandoverStatus.ReadyForHandover,
                Note = "Nested record added after preview",
                ChangedByUserId = userId,
            });
            await db.SaveChangesAsync();
        });

        var delete = await Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/design-projects/{id}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
            }),
        });

        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.DesignProjects.AnyAsync(project => project.Id == id)))
            .Should().BeTrue();
        (await WithDbAsync(db => db.HandoverStatusHistory.AnyAsync(history =>
            history.HandoverRecordId == handoverId))).Should().BeTrue();
    }

    [Fact]
    public async Task DeletionImpact_StandaloneProjectWithManagedFile_BlocksDelete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await WithDbAsync<int>(async db =>
        {
            var project = new DesignProject
            {
                ProjectCode = $"DP-FILE-{Guid.NewGuid():N}"[..24],
                Name = "Standalone managed-file fixture",
                CustomerId = customerId,
            };
            db.DesignProjects.Add(project);
            await db.SaveChangesAsync();
            db.BasicDesignDocs.Add(new BasicDesignDoc
            {
                DesignProjectId = project.Id,
                DocumentCode = $"BD-{Guid.NewGuid():N}"[..24],
                Title = "Managed standalone file",
                DisciplineCode = "ARCH",
                FilePath = "/files/business-documents/basic-design/standalone.pdf",
                OriginalFileName = "standalone.pdf",
            });
            await db.SaveChangesAsync();
            return project.Id;
        });

        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/design-projects/{id}/deletion-impact"));

        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "design.filesPendingCleanup" &&
            item.GetProperty("action").GetString() == "Block" &&
            item.GetProperty("count").GetInt32() == 1);
        var delete = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/design-projects/{id}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
            }),
        });
        delete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DesignProjects.AnyAsync(project => project.Id == id)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task DeletionImpact_LinkedManagedFileWithoutSidecar_BlocksDelete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var id = await CreateAsync(customerId, "Linked file without sidecar");
        await WithDbAsync(async db =>
        {
            var project = await db.DesignProjects.FindAsync(id);
            project!.OperationalProjectId = operationalProjectId;
            db.BasicDesignDocs.Add(new BasicDesignDoc
            {
                DesignProjectId = id,
                DocumentCode = $"BD-{Guid.NewGuid():N}"[..24],
                Title = "Missing cleanup sidecar",
                DisciplineCode = "ARCH",
                FilePath = "/files/business-documents/basic-design/missing-sidecar.pdf",
                OriginalFileName = "missing-sidecar.pdf",
            });
            await db.SaveChangesAsync();
        });

        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/design-projects/{id}/deletion-impact"));

        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "design.filesPendingCleanup" &&
            item.GetProperty("action").GetString() == "Block");
    }

    [Fact]
    public async Task DeletionImpact_LinkedManagedFileWithProcessingSidecar_BlocksDelete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var id = await CreateAsync(customerId, "Processing file sidecar");
        await WithDbAsync(async db =>
        {
            var project = await db.DesignProjects.FindAsync(id);
            project!.OperationalProjectId = operationalProjectId;
            var document = new BasicDesignDoc
            {
                DesignProjectId = id,
                DocumentCode = $"BD-{Guid.NewGuid():N}"[..24],
                Title = "Processing cleanup sidecar",
                DisciplineCode = "ARCH",
                FilePath = "/files/business-documents/basic-design/processing-sidecar.pdf",
            };
            db.BasicDesignDocs.Add(document);
            await db.SaveChangesAsync();
            db.ProjectDocuments.Add(new ProjectDocument
            {
                OperationalProjectId = operationalProjectId,
                SourceModule = ProjectDocumentSourceModule.Design,
                SourceType = ProjectDocumentSourceType.ExistingManagedFile,
                SourceEntityType = nameof(BasicDesignDoc),
                SourceSlot = "file",
                SourceRecordId = document.Id,
                LocalPath = document.FilePath,
                OriginalFileName = "processing-sidecar.pdf",
                Sha256 = new string('a', 64),
                SyncStatus = ProjectDocumentSyncStatus.Processing,
            });
            await db.SaveChangesAsync();
        });

        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/design-projects/{id}/deletion-impact"));

        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "design.filesPendingCleanup" &&
            item.GetProperty("action").GetString() == "Block");
    }

    [Fact]
    public async Task Delete_SampleProject_WritesTombstone()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var project = await WithDbAsync<DesignProject>(async db =>
        {
            var item = new DesignProject
            {
                ProjectCode = $"DP-SAMPLE-{Guid.NewGuid():N}"[..40],
                Name = "Sample deletion tombstone",
                CustomerId = customerId,
            };
            db.DesignProjects.Add(item);
            await db.SaveChangesAsync();
            return item;
        });
        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/design-projects/{project.Id}/deletion-impact"));

        var delete = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/design-projects/{project.Id}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
            }),
        });

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await WithDbAsync(db => db.SeededRootDeletions.AnyAsync(item =>
            item.ResourceType == EntityTypes.DesignProject && item.ResourceKey == project.ProjectCode)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WhenTranslationAddedAfterPreview_ReturnsConflictAndPreservesProject()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Translation stale plan");
        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/design-projects/{id}/deletion-impact"));
        await WithDbAsync(async db =>
        {
            db.EntityTranslations.Add(new EntityTranslation
            {
                EntityType = EntityTypes.DesignProject,
                EntityId = id,
                FieldName = "Name",
                LanguageCode = "en",
                Value = "Translation added after preview",
            });
            await db.SaveChangesAsync();
        });

        var delete = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/design-projects/{id}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
            }),
        });

        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.DesignProjects.AnyAsync(project => project.Id == id)))
            .Should().BeTrue();
        (await WithDbAsync(db => db.EntityTranslations.AnyAsync(item =>
            item.EntityType == EntityTypes.DesignProject && item.EntityId == id))).Should().BeTrue();
    }

    // -------- helpers --------

    private async Task<int> CreateAsync(int customerId, string name)
    {
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name,
            customerId,
            operationalProjectId,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateOperationalProjectAsync(int customerId)
    {
        return await WithDbAsync<int>(async db =>
        {
            var project = new OperationalProject
            {
                Code = $"PJ-TEST-{Guid.NewGuid():N}",
                Name = "Design project integration fixture",
                CustomerId = customerId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
    }

    private async Task<int> FirstCustomerIdAsync()
    {
        return await WithDbAsync<int>(async db =>
        {
            var existing = await db.Customers.OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (existing != null) return existing.Id;

            var customer = new Customer
            {
                Name = "DP Test Customer " + Guid.NewGuid().ToString("N")[..6],
                SourceCode = "referral",
                RelationshipStatus = CustomerRelationshipStatus.InProgress,
                Type = CustomerType.Company,
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            return customer.Id;
        });
    }
}
