using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task Delete_StandaloneProjectWithManagedFile_BlocksAndPreservesFile()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var managedPath = $"/files/business-documents/basic-design/{Guid.NewGuid():N}.pdf";
        string fullPath;
        using (var scope = Factory.Services.CreateScope())
        {
            var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            fullPath = Path.Combine(environment.ContentRootPath, "wwwroot",
                managedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, "standalone design file");
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
                FilePath = managedPath,
                OriginalFileName = "standalone.pdf",
            });
            await db.SaveChangesAsync();
            return project.Id;
        });

        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/design-projects/{id}/deletion-impact"));

        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        var blocker = impact.GetProperty("items").EnumerateArray().Single(item =>
            item.GetProperty("key").GetString() == "design.filesPendingCleanup");
        blocker.GetProperty("action").GetString().Should().Be("Block");
        blocker.GetProperty("resolutionLinks").EnumerateArray().Should().Contain(link =>
            link.GetProperty("url").GetString()!.StartsWith(
                $"/admin/design-projects/{id}?tab=basic&documentId=", StringComparison.Ordinal));
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
        File.Exists(fullPath).Should().BeTrue();
    }

    [Fact]
    public async Task DeletionImpact_LinkedManagedFileWithoutSidecar_LinksToManualCleanup()
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
        var blocker = impact.GetProperty("items").EnumerateArray().Single(item =>
            item.GetProperty("key").GetString() == "design.filesPendingCleanup");
        blocker.GetProperty("action").GetString().Should().Be("Block");
        blocker.GetProperty("resolutionLinks").EnumerateArray().Should().Contain(link =>
            link.GetProperty("url").GetString()!.StartsWith(
                $"/admin/design-projects/{id}?tab=basic&documentId=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeletionImpact_DesignFiles_HaveBusinessLabelsAndExactSourceLinks()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Distinct cleanup links");
        var sourceIds = await WithDbAsync<(int BasicId, int ShopId)>(async db =>
        {
            var basic = new BasicDesignDoc
            {
                DesignProjectId = id,
                DocumentCode = "KT-BD-CLEANUP",
                Title = "Hồ sơ kiến trúc",
                DisciplineCode = "ARCH",
                FilePath = "/files/design/basic/cleanup.pdf",
            };
            var shop = new ShopDrawing
            {
                DesignProjectId = id,
                DrawingCode = "KT-SD-CLEANUP",
                Title = "Bản vẽ thi công",
                DisciplineCode = "ARCH",
                ConstructionItem = "Kiến trúc",
                FilePath = "/files/design/shop/cleanup.pdf",
            };
            db.BasicDesignDocs.Add(basic);
            db.ShopDrawings.Add(shop);
            await db.SaveChangesAsync();
            return (basic.Id, shop.Id);
        });

        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/design-projects/{id}/deletion-impact"));

        var links = impact.GetProperty("items").EnumerateArray().Single(item =>
                item.GetProperty("key").GetString() == "design.filesPendingCleanup")
            .GetProperty("resolutionLinks").EnumerateArray().ToList();
        links.Should().Contain(link =>
            link.GetProperty("label").GetString() == "KT-BD-CLEANUP · Hồ sơ kiến trúc" &&
            link.GetProperty("url").GetString() ==
                $"/admin/design-projects/{id}?tab=basic&documentId={sourceIds.BasicId}");
        links.Should().Contain(link =>
            link.GetProperty("label").GetString() == "KT-SD-CLEANUP · Bản vẽ thi công" &&
            link.GetProperty("url").GetString() ==
                $"/admin/design-projects/{id}?tab=shop&documentId={sourceIds.ShopId}");
        links.Select(link => link.GetProperty("url").GetString()).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task DeletionImpact_SyncedManagedFile_RequiresManualSourceCleanup()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var id = await CreateAsync(customerId, "Synced Drive file");
        var driveFileId = $"drive-{Guid.NewGuid():N}";
        var managedPath = $"/files/business-documents/basic-design/{Guid.NewGuid():N}.pdf";
        await WithDbAsync(async db =>
        {
            var driveSettings = await db.GoogleDriveCredentials.SingleOrDefaultAsync(item => item.Id == 1);
            if (driveSettings is null)
            {
                driveSettings = new GoogleDriveCredential
                {
                    Id = 1,
                    UpdatedByUserId = await db.Users.Select(item => item.Id).FirstAsync(),
                    UpdatedAt = DateTime.UtcNow,
                };
                db.GoogleDriveCredentials.Add(driveSettings);
            }
            driveSettings.InstanceId = "integration-tests";
            var project = await db.DesignProjects.FindAsync(id);
            project!.OperationalProjectId = operationalProjectId;
            var document = new BasicDesignDoc
            {
                DesignProjectId = id,
                DocumentCode = $"BD-{Guid.NewGuid():N}"[..24],
                Title = "Synced cleanup sidecar",
                DisciplineCode = "ARCH",
                FilePath = managedPath,
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
                LocalPath = managedPath,
                OriginalFileName = "synced-sidecar.pdf",
                Sha256 = new string('a', 64),
                Origin = ProjectDocumentOrigin.Nicon,
                DesiredOperation = ProjectDocumentDesiredOperation.None,
                SyncStatus = ProjectDocumentSyncStatus.Synced,
                Generation = 1,
                DriveFileId = driveFileId,
                DriveFolderId = "drive-folder",
            });
            await db.SaveChangesAsync();
        });

        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/design-projects/{id}/deletion-impact"));

        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        impact.GetProperty("items").EnumerateArray().Should().NotContain(item =>
            item.GetProperty("key").GetString() == "hardDelete.managedExternalItems");
        var blocker = impact.GetProperty("items").EnumerateArray().Single(item =>
            item.GetProperty("key").GetString() == "design.filesPendingCleanup");
        blocker.GetProperty("resolutionLinks").EnumerateArray().Should().Contain(link =>
            link.GetProperty("url").GetString()!.StartsWith(
                $"/admin/design-projects/{id}?tab=basic&documentId=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeletionImpact_OrphanedPendingDeleteSidecar_BlocksAndLinksToDocuments()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var id = await CreateAsync(customerId, "Orphaned pending delete");
        await WithDbAsync(async db =>
        {
            var project = await db.DesignProjects.FindAsync(id);
            project!.OperationalProjectId = operationalProjectId;
            db.ProjectDocuments.Add(new ProjectDocument
            {
                OperationalProjectId = operationalProjectId,
                SourceModule = ProjectDocumentSourceModule.Design,
                SourceType = ProjectDocumentSourceType.ExistingManagedFile,
                SourceEntityType = nameof(BasicDesignDoc),
                SourceSlot = "file",
                SourceRecordId = 987654321,
                LocalPath = "/files/business-documents/basic-design/orphaned-delete.pdf",
                OriginalFileName = "orphaned-delete.pdf",
                Sha256 = new string('a', 64),
                DesiredOperation = ProjectDocumentDesiredOperation.Delete,
                SyncStatus = ProjectDocumentSyncStatus.Failed,
                SyncAttemptCount = 3,
            });
            await db.SaveChangesAsync();
        });

        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/design-projects/{id}/deletion-impact"));

        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        var blocker = impact.GetProperty("items").EnumerateArray().Single(item =>
            item.GetProperty("key").GetString() == "design.filesPendingCleanup");
        blocker.GetProperty("resolutionLinks").EnumerateArray().Should().Contain(link =>
            link.GetProperty("url").GetString() ==
            $"/admin/operational-projects/{operationalProjectId}#project-documents");

        await WithDbAsync(async db =>
        {
            var sidecar = await db.ProjectDocuments.SingleAsync(item =>
                item.OperationalProjectId == operationalProjectId &&
                item.SourceRecordId == 987654321);
            sidecar.DesiredOperation = ProjectDocumentDesiredOperation.None;
            sidecar.SyncStatus = ProjectDocumentSyncStatus.Deleted;
            await db.SaveChangesAsync();
        });
        var clearedImpact = await ReadJsonAsync(
            await Client.GetAsync($"/api/design-projects/{id}/deletion-impact"));
        clearedImpact.GetProperty("canDelete").GetBoolean().Should().BeTrue();
        var delete = await Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/design-projects/{id}")
        {
            Content = JsonContent.Create(new
            {
                planToken = clearedImpact.GetProperty("planToken").GetString(),
                confirmation = clearedImpact.GetProperty("requiredConfirmation").GetString(),
            }),
        });

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await WithDbAsync(db => db.DesignProjects.AnyAsync(item => item.Id == id)))
            .Should().BeFalse();
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
    public async Task DeletionImpact_SyncedSidecarWithPendingDesiredOperation_BlocksDelete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var id = await CreateAsync(customerId, "Unstable desired operation");
        await WithDbAsync(async db =>
        {
            var project = await db.DesignProjects.FindAsync(id);
            project!.OperationalProjectId = operationalProjectId;
            var document = new BasicDesignDoc
            {
                DesignProjectId = id,
                DocumentCode = $"BD-{Guid.NewGuid():N}"[..24],
                Title = "Pending sidecar operation",
                DisciplineCode = "ARCH",
                FilePath = $"/files/business-documents/basic-design/{Guid.NewGuid():N}.pdf",
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
                OriginalFileName = "pending-operation.pdf",
                Sha256 = new string('a', 64),
                Origin = ProjectDocumentOrigin.Nicon,
                DesiredOperation = ProjectDocumentDesiredOperation.Upsert,
                SyncStatus = ProjectDocumentSyncStatus.Synced,
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

    [Theory]
    [InlineData("manual-upload")]
    [InlineData("imported")]
    [InlineData("wrong-operational-project")]
    [InlineData("missing-slot")]
    public async Task DeletionImpact_WithUnsafeNearMatchSidecar_BlocksDelete(string sidecarVariant)
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var otherOperationalProjectId = sidecarVariant == "wrong-operational-project"
            ? await CreateOperationalProjectAsync(customerId)
            : operationalProjectId;
        var id = await CreateAsync(customerId, $"Unsafe sidecar {sidecarVariant}");
        await WithDbAsync(async db =>
        {
            var project = await db.DesignProjects.FindAsync(id);
            project!.OperationalProjectId = operationalProjectId;
            var document = new BasicDesignDoc
            {
                DesignProjectId = id,
                DocumentCode = $"BD-{Guid.NewGuid():N}"[..24],
                Title = "Unsafe cleanup sidecar",
                DisciplineCode = "ARCH",
                FilePath = $"/files/business-documents/basic-design/{Guid.NewGuid():N}.pdf",
            };
            db.BasicDesignDocs.Add(document);
            await db.SaveChangesAsync();
            db.ProjectDocuments.Add(new ProjectDocument
            {
                OperationalProjectId = otherOperationalProjectId,
                SourceModule = ProjectDocumentSourceModule.Design,
                SourceType = sidecarVariant switch
                {
                    "manual-upload" => ProjectDocumentSourceType.ManualUpload,
                    "imported" => ProjectDocumentSourceType.GoogleDriveImport,
                    _ => ProjectDocumentSourceType.ExistingManagedFile,
                },
                SourceEntityType = nameof(BasicDesignDoc),
                SourceSlot = sidecarVariant == "missing-slot" ? null : "file",
                SourceRecordId = document.Id,
                LocalPath = document.FilePath,
                OriginalFileName = "unsafe-sidecar.pdf",
                Sha256 = new string('a', 64),
                Origin = sidecarVariant == "imported"
                    ? ProjectDocumentOrigin.GoogleDrive
                    : ProjectDocumentOrigin.Nicon,
                DesiredOperation = ProjectDocumentDesiredOperation.None,
                SyncStatus = ProjectDocumentSyncStatus.Synced,
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
    public async Task DeletionImpact_WhenAnotherProjectSharesManagedPath_BlocksDelete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var firstProjectId = await CreateAsync(customerId, "Shared file owner");
        var secondProjectId = await CreateAsync(customerId, "Shared file consumer");
        var managedPath = $"/files/business-documents/basic-design/{Guid.NewGuid():N}.pdf";
        await WithDbAsync(async db =>
        {
            db.BasicDesignDocs.AddRange(
                new BasicDesignDoc
                {
                    DesignProjectId = firstProjectId,
                    DocumentCode = $"BD-{Guid.NewGuid():N}"[..24],
                    Title = "Original shared file",
                    DisciplineCode = "ARCH",
                    FilePath = managedPath,
                },
                new BasicDesignDoc
                {
                    DesignProjectId = secondProjectId,
                    DocumentCode = $"BD-{Guid.NewGuid():N}"[..24],
                    Title = "Other shared file",
                    DisciplineCode = "ARCH",
                    FilePath = managedPath,
                });
            await db.SaveChangesAsync();
        });

        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/design-projects/{firstProjectId}/deletion-impact"));

        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "design.filesPendingCleanup" &&
            item.GetProperty("action").GetString() == "Block");
    }

    [Fact]
    public async Task Delete_WhenManagedFileAppearsAfterPreview_ReturnsConflictAndPreservesProject()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var firstProjectId = await CreateAsync(customerId, "Path owner before preview");
        var managedPath = $"/files/business-documents/basic-design/{Guid.NewGuid():N}.pdf";
        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/design-projects/{firstProjectId}/deletion-impact"));
        impact.GetProperty("canDelete").GetBoolean().Should().BeTrue();
        await WithDbAsync(async db =>
        {
            db.BasicDesignDocs.Add(new BasicDesignDoc
            {
                DesignProjectId = firstProjectId,
                DocumentCode = $"BD-{Guid.NewGuid():N}"[..24],
                Title = "New managed file",
                DisciplineCode = "ARCH",
                FilePath = managedPath,
            });
            await db.SaveChangesAsync();
        });

        var delete = await Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/design-projects/{firstProjectId}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
            }),
        });

        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.DesignProjects.AnyAsync(item => item.Id == firstProjectId)))
            .Should().BeTrue();
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
