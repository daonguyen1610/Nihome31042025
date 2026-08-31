using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using NihomeBackend.Services.GoogleDrive;
using nihomebackend.Migrations;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class OperationalProjectDocumentsControllerTests(
    OperationalProjectDocumentsWebApplicationFactory factory)
    : IntegrationTestBase(factory), IClassFixture<OperationalProjectDocumentsWebApplicationFactory>, IDisposable
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF");
    private readonly HashSet<int> projectIdsToClean = [];

    [Fact]
    public async Task DocumentCategories_RequiresAuthentication_AndReturnsOrderedSupportedCatalog()
    {
        var unauthorized = await Client.GetAsync("/api/operational-projects/document-categories");
        unauthorized.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AuthenticateAsAsync("SUPER_ADMIN");
        var response = await Client.GetAsync("/api/operational-projects/document-categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = (await ReadJsonAsync(response)).EnumerateArray().ToArray();
        categories.Select(category => category.GetProperty("value").GetString()).Should().Equal(
            "CrmPreDesign", "DesignConcept", "DesignBasic", "DesignShopDrawing",
            "LegalPermits", "ConstructionAcceptance", "Procurement", "FinanceContracts");
        categories.Should().OnlyContain(category =>
            !string.IsNullOrWhiteSpace(category.GetProperty("folderPath").GetString()) &&
            category.GetProperty("translationKey").GetString() ==
            $"operationalProjects.documents.category.{category.GetProperty("value").GetString()}");
    }

    [Fact]
    public async Task Documents_WithoutAuthentication_RejectsListAndUpload()
    {
        var projectId = await CreateProjectAsync();

        var list = await Client.GetAsync($"/api/operational-projects/{projectId}/documents");
        using var uploadContent = CreateUploadContent("DesignBasic", "document.pdf", PdfBytes);
        var upload = await Client.PostAsync($"/api/operational-projects/{projectId}/documents", uploadContent);

        list.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        upload.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CountDocumentsAsync(projectId)).Should().Be(0);
        ProjectFiles(projectId).Should().BeEmpty();
    }

    [Fact]
    public async Task SuperAdmin_UploadsListsAndDownloadsPdf_FromDriveWithoutHostCopy()
    {
        await AuthenticateAsAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        using var uploadContent = CreateUploadContent("DesignBasic", "private-plan.pdf", PdfBytes);

        var upload = await Client.PostAsync($"/api/operational-projects/{projectId}/documents", uploadContent);

        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(upload);
        body.GetProperty("operationalProjectId").GetInt32().Should().Be(projectId);
        body.GetProperty("category").GetString().Should().Be("DesignBasic");
        body.GetProperty("sourceType").GetString().Should().Be("ManualUpload");
        body.GetProperty("origin").GetString().Should().Be("Nicon");
        body.GetProperty("desiredOperation").GetString().Should().Be("None");
        body.GetProperty("syncStatus").GetString().Should().Be("Synced");
        body.GetProperty("isDownloadable").GetBoolean().Should().BeTrue();
        body.GetProperty("size").GetInt64().Should().Be(PdfBytes.Length);
        body.GetProperty("sha256").GetString().Should().Be(Convert.ToHexString(SHA256.HashData(PdfBytes)).ToLowerInvariant());
        upload.Headers.Location.Should().NotBeNull();

        var documentId = body.GetProperty("id").GetInt64();
        var persisted = await LoadDocumentAsync(documentId);
        persisted.LocalPath.Should().BeEmpty();
        persisted.SyncStatus.Should().Be(ProjectDocumentSyncStatus.Synced);
        persisted.DesiredOperation.Should().Be(ProjectDocumentDesiredOperation.None);
        persisted.DriveFileId.Should().NotBeNullOrWhiteSpace();
        ProjectFiles(projectId).Should().BeEmpty();

        var list = await Client.GetAsync($"/api/operational-projects/{projectId}/documents");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listed = (await ReadJsonAsync(list)).EnumerateArray().Should().ContainSingle().Subject;
        listed.GetProperty("id").GetInt64().Should().Be(documentId);
        listed.GetProperty("syncStatus").GetString().Should().Be("Synced");

        var download = await Client.GetAsync($"/api/operational-projects/{projectId}/documents/{documentId}/content");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        download.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("private-plan.pdf");
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(PdfBytes);
    }

    [Theory]
    [InlineData("DesignBasic", "payload.exe")]
    [InlineData("Unclassified", "payload.pdf")]
    [InlineData("999", "payload.pdf")]
    public async Task Upload_InvalidFileOrCategory_ReturnsBadRequestWithoutPersistence(
        string category, string fileName)
    {
        await AuthenticateAsAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        var filesBefore = ProjectFiles(projectId);
        using var uploadContent = CreateUploadContent(category, fileName, PdfBytes);

        var response = await Client.PostAsync($"/api/operational-projects/{projectId}/documents", uploadContent);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        if (category == "999")
        {
            body.GetProperty("status").GetInt32().Should().Be(400);
            body.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
            body.GetProperty("errors").TryGetProperty("Category", out var categoryErrors).Should().BeTrue();
            categoryErrors.EnumerateArray().Should().NotBeEmpty();
        }
        else
        {
            body.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        }
        (await CountDocumentsAsync(projectId)).Should().Be(0);
        ProjectFiles(projectId).Should().Equal(filesBefore);
    }

    [Fact]
    public async Task SaleOutsideProjectScope_AllDocumentEndpointsReturnNotFoundWithoutChanges()
    {
        var projectId = await CreateProjectAsync();
        var document = await AddDocumentAsync(projectId, ProjectDocumentCategory.Unclassified,
            ProjectDocumentOrigin.GoogleDrive, ProjectDocumentSyncStatus.Pending);
        var before = Snapshot(document);
        await AuthenticateAsAsync("SALE");

        var responses = new List<HttpResponseMessage>
        {
            await Client.GetAsync($"/api/operational-projects/{projectId}/documents"),
            await Client.GetAsync($"/api/operational-projects/{projectId}/documents/{document.Id}/content"),
            await Client.DeleteAsync($"/api/operational-projects/{projectId}/documents/{document.Id}"),
            await Client.PostAsync($"/api/operational-projects/{projectId}/documents/{document.Id}/retry", null),
            await Client.PostAsJsonAsync($"/api/operational-projects/{projectId}/documents/{document.Id}/classify",
                new { category = "DesignBasic" }),
            await Client.PostAsJsonAsync($"/api/operational-projects/{projectId}/documents/{document.Id}/resolve-conflict",
                new { confirmKeepBoth = true }),
        };
        using var uploadContent = CreateUploadContent("DesignBasic", "out-of-scope.pdf", PdfBytes);
        responses.Add(await Client.PostAsync($"/api/operational-projects/{projectId}/documents", uploadContent));

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.NotFound);
        foreach (var response in responses)
        {
            AssertProblemDetails(await ReadJsonAsync(response), HttpStatusCode.NotFound);
            response.Dispose();
        }
        Snapshot(await LoadDocumentAsync(document.Id)).Should().Be(before);
        (await CountDocumentsAsync(projectId)).Should().Be(1);
        ProjectFiles(projectId).Should().ContainSingle();
    }

    [Fact]
    public async Task AccountantWithViewAll_CanListAndDownloadButAllMutationsAreForbidden()
    {
        var projectId = await CreateProjectAsync();
        var document = await AddDocumentAsync(projectId, ProjectDocumentCategory.Unclassified);
        var before = Snapshot(document);
        await AuthenticateAsAsync("ACCOUNTANT");

        var list = await Client.GetAsync($"/api/operational-projects/{projectId}/documents");
        var download = await Client.GetAsync(
            $"/api/operational-projects/{projectId}/documents/{document.Id}/content");
        var mutations = new List<HttpResponseMessage>
        {
            await Client.DeleteAsync($"/api/operational-projects/{projectId}/documents/{document.Id}"),
            await Client.PostAsync($"/api/operational-projects/{projectId}/documents/{document.Id}/retry", null),
            await Client.PostAsJsonAsync($"/api/operational-projects/{projectId}/documents/{document.Id}/classify",
                new { category = "DesignBasic" }),
            await Client.PostAsJsonAsync($"/api/operational-projects/{projectId}/documents/{document.Id}/resolve-conflict",
                new { confirmKeepBoth = true }),
        };
        using var uploadContent = CreateUploadContent("DesignBasic", "forbidden.pdf", PdfBytes);
        mutations.Add(await Client.PostAsync(
            $"/api/operational-projects/{projectId}/documents", uploadContent));

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        mutations.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Forbidden);
        Snapshot(await LoadDocumentAsync(document.Id)).Should().Be(before);
        (await CountDocumentsAsync(projectId)).Should().Be(1);
    }

    [Fact]
    public async Task DocumentFromDifferentProject_ReturnsNotFoundAndLeavesBothRowsUnchanged()
    {
        await AuthenticateAsAsync("SUPER_ADMIN");
        var firstProjectId = await CreateProjectAsync();
        var secondProjectId = await CreateProjectAsync();
        var firstDocument = await AddDocumentAsync(firstProjectId, ProjectDocumentCategory.DesignBasic);
        var secondDocument = await AddDocumentAsync(secondProjectId, ProjectDocumentCategory.DesignBasic);
        var firstBefore = Snapshot(firstDocument);
        var secondBefore = Snapshot(secondDocument);

        var response = await Client.DeleteAsync(
            $"/api/operational-projects/{firstProjectId}/documents/{secondDocument.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        AssertProblemDetails(await ReadJsonAsync(response), HttpStatusCode.NotFound);
        Snapshot(await LoadDocumentAsync(firstDocument.Id)).Should().Be(firstBefore);
        Snapshot(await LoadDocumentAsync(secondDocument.Id)).Should().Be(secondBefore);
        File.Exists(FullPath(firstDocument.LocalPath)).Should().BeTrue();
        File.Exists(FullPath(secondDocument.LocalPath)).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_QueuesDeleteAndKeepsPendingRowAndManagedFileUntilDriveWorkerCompletes()
    {
        await AuthenticateAsAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        var document = await AddDocumentAsync(projectId, ProjectDocumentCategory.DesignBasic,
            syncStatus: ProjectDocumentSyncStatus.Synced);
        var fullPath = FullPath(document.LocalPath);

        var response = await Client.DeleteAsync($"/api/operational-projects/{projectId}/documents/{document.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var persisted = await LoadDocumentAsync(document.Id);
        persisted.DesiredOperation.Should().Be(ProjectDocumentDesiredOperation.Delete);
        persisted.SyncStatus.Should().Be(ProjectDocumentSyncStatus.Pending);
        persisted.SyncAttemptCount.Should().Be(0);
        persisted.DeletedAt.Should().NotBeNull();
        File.Exists(fullPath).Should().BeTrue();

        var list = await Client.GetAsync($"/api/operational-projects/{projectId}/documents");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listed = (await ReadJsonAsync(list)).EnumerateArray().Should().ContainSingle().Subject;
        listed.GetProperty("id").GetInt64().Should().Be(document.Id);
        listed.GetProperty("desiredOperation").GetString().Should().Be("Delete");
        listed.GetProperty("syncStatus").GetString().Should().Be("Pending");

        var content = await Client.GetAsync($"/api/operational-projects/{projectId}/documents/{document.Id}/content");
        content.StatusCode.Should().Be(HttpStatusCode.OK);
        (await content.Content.ReadAsByteArrayAsync()).Should().Equal(PdfBytes);
    }

    [Fact]
    public async Task Delete_SourceOwnedDocument_ReturnsBadRequestAndKeepsReplicaState()
    {
        await AuthenticateAsAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        var document = await AddDocumentAsync(projectId, ProjectDocumentCategory.DesignBasic,
            syncStatus: ProjectDocumentSyncStatus.Synced);
        await WithDbAsync(async db =>
        {
            var persisted = await db.ProjectDocuments.SingleAsync(item => item.Id == document.Id);
            persisted.SourceType = ProjectDocumentSourceType.ExistingManagedFile;
            persisted.SourceModule = ProjectDocumentSourceModule.Design;
            persisted.SourceEntityType = nameof(BasicDesignDoc);
            persisted.SourceSlot = "file";
            persisted.SourceRecordId = 42;
            await db.SaveChangesAsync();
        });
        var before = Snapshot(await LoadDocumentAsync(document.Id));

        var response = await Client.DeleteAsync(
            $"/api/operational-projects/{projectId}/documents/{document.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        Snapshot(await LoadDocumentAsync(document.Id)).Should().Be(before);
        File.Exists(FullPath(document.LocalPath)).Should().BeTrue();
    }

    [Fact]
    public async Task Retry_PendingBackoffQueuesImmediately_AndTerminalFailureIsUnchanged()
    {
        await AuthenticateAsAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        var pending = await AddDocumentAsync(projectId, ProjectDocumentCategory.DesignBasic,
            syncStatus: ProjectDocumentSyncStatus.Pending, syncAttemptCount: 2,
            nextSyncAttemptAt: DateTime.UtcNow.AddHours(2));
        var failed = await AddDocumentAsync(projectId, ProjectDocumentCategory.DesignConcept,
            syncStatus: ProjectDocumentSyncStatus.Failed,
            syncAttemptCount: ProjectDocumentService.MaxSyncAttempts,
            nextSyncAttemptAt: DateTime.UtcNow.AddHours(2));
        var failedBefore = Snapshot(failed);
        var requestStartedAt = DateTime.UtcNow;

        var pendingResponse = await Client.PostAsync(
            $"/api/operational-projects/{projectId}/documents/{pending.Id}/retry", null);
        var failedResponse = await Client.PostAsync(
            $"/api/operational-projects/{projectId}/documents/{failed.Id}/retry", null);

        pendingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var pendingBody = await ReadJsonAsync(pendingResponse);
        pendingBody.GetProperty("syncStatus").GetString().Should().Be("Pending");
        pendingBody.GetProperty("syncAttemptCount").GetInt32().Should().Be(2);
        pendingBody.GetProperty("nextSyncAttemptAt").GetDateTime().Should().BeOnOrAfter(requestStartedAt);
        pendingBody.GetProperty("nextSyncAttemptAt").GetDateTime().Should().BeOnOrBefore(DateTime.UtcNow);

        failedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(failedResponse)).GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        Snapshot(await LoadDocumentAsync(failed.Id)).Should().Be(failedBefore);
    }

    [Fact]
    public async Task Classify_ImportedUnclassifiedSucceeds_AndClassifiedDocumentIsUnchanged()
    {
        await AuthenticateAsAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        var imported = await AddDocumentAsync(projectId, ProjectDocumentCategory.Unclassified,
            ProjectDocumentOrigin.GoogleDrive, ProjectDocumentSyncStatus.Synced);
        var classified = await AddDocumentAsync(projectId, ProjectDocumentCategory.DesignBasic,
            ProjectDocumentOrigin.GoogleDrive, ProjectDocumentSyncStatus.Synced);
        var classifiedBefore = Snapshot(classified);

        var success = await Client.PostAsJsonAsync(
            $"/api/operational-projects/{projectId}/documents/{imported.Id}/classify",
            new { category = "ConstructionAcceptance" });
        var rejected = await Client.PostAsJsonAsync(
            $"/api/operational-projects/{projectId}/documents/{classified.Id}/classify",
            new { category = "ConstructionAcceptance" });

        success.StatusCode.Should().Be(HttpStatusCode.OK);
        var successBody = await ReadJsonAsync(success);
        successBody.GetProperty("category").GetString().Should().Be("ConstructionAcceptance");
        successBody.GetProperty("generation").GetInt64().Should().Be(1);
        successBody.GetProperty("desiredOperation").GetString().Should().Be("None");
        successBody.GetProperty("syncStatus").GetString().Should().Be("Synced");

        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(rejected)).GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        Snapshot(await LoadDocumentAsync(classified.Id)).Should().Be(classifiedBefore);
    }

    [Fact]
    public async Task ResolveConflict_RequiresPendingConflictAndConfirmation_ThenKeepsBoth()
    {
        await AuthenticateAsAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        var noConflict = await AddDocumentAsync(projectId, ProjectDocumentCategory.DesignBasic,
            syncStatus: ProjectDocumentSyncStatus.Synced);
        var noConflictBefore = Snapshot(noConflict);
        var authoritative = await AddDocumentAsync(projectId, ProjectDocumentCategory.DesignConcept,
            syncStatus: ProjectDocumentSyncStatus.Conflict,
            conflictState: ProjectDocumentConflictState.PendingConfirmation);
        var remote = await AddDocumentAsync(projectId, ProjectDocumentCategory.DesignConcept,
            ProjectDocumentOrigin.GoogleDrive, ProjectDocumentSyncStatus.Conflict,
            conflictState: ProjectDocumentConflictState.PendingConfirmation,
            conflictWithDocumentId: authoritative.Id);
        var authoritativeBeforeNoConfirmation = Snapshot(authoritative);
        var remoteBeforeNoConfirmation = Snapshot(remote);

        var missingConflict = await Client.PostAsJsonAsync(
            $"/api/operational-projects/{projectId}/documents/{noConflict.Id}/resolve-conflict",
            new { confirmKeepBoth = true });
        var missingConfirmation = await Client.PostAsJsonAsync(
            $"/api/operational-projects/{projectId}/documents/{remote.Id}/resolve-conflict",
            new { confirmKeepBoth = false });

        missingConflict.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        missingConfirmation.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(missingConflict)).GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        (await ReadJsonAsync(missingConfirmation)).GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        Snapshot(await LoadDocumentAsync(noConflict.Id)).Should().Be(noConflictBefore);
        Snapshot(await LoadDocumentAsync(authoritative.Id)).Should().Be(authoritativeBeforeNoConfirmation);
        Snapshot(await LoadDocumentAsync(remote.Id)).Should().Be(remoteBeforeNoConfirmation);

        var success = await Client.PostAsJsonAsync(
            $"/api/operational-projects/{projectId}/documents/{remote.Id}/resolve-conflict",
            new { confirmKeepBoth = true });

        success.StatusCode.Should().Be(HttpStatusCode.OK);
        var successBody = await ReadJsonAsync(success);
        successBody.GetProperty("conflictState").GetString().Should().Be("None");
        successBody.GetProperty("syncStatus").GetString().Should().Be("Synced");
        successBody.GetProperty("desiredOperation").GetString().Should().Be("None");
        var authoritativeAfter = await LoadDocumentAsync(authoritative.Id);
        var remoteAfter = await LoadDocumentAsync(remote.Id);
        authoritativeAfter.ConflictState.Should().Be(ProjectDocumentConflictState.None);
        authoritativeAfter.SyncStatus.Should().Be(ProjectDocumentSyncStatus.Pending);
        authoritativeAfter.DesiredOperation.Should().Be(ProjectDocumentDesiredOperation.Upsert);
        remoteAfter.ConflictState.Should().Be(ProjectDocumentConflictState.None);
        remoteAfter.SyncStatus.Should().Be(ProjectDocumentSyncStatus.Synced);
        remoteAfter.DesiredOperation.Should().Be(ProjectDocumentDesiredOperation.None);
    }

    [Fact]
    public async Task RelationalSchemaAndMigration_DefineRequiredIndexesAndRowVersions()
    {
        var sqliteIndexes = await WithDbAsync(async db =>
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA index_list('project_documents');";
            if (command.Connection!.State != System.Data.ConnectionState.Open)
                await command.Connection.OpenAsync();
            var names = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) names.Add(reader.GetString(1));
            return names;
        });
        sqliteIndexes.Should().Contain("IX_project_documents_OperationalProjectId_Category_UpdatedAt");
        sqliteIndexes.Should().Contain("IX_project_documents_SyncStatus_NextSyncAttemptAt");
        sqliteIndexes.Should().Contain("IX_project_documents_DriveFileId");
        sqliteIndexes.Should().Contain("IX_project_documents_OperationalProjectId_SourceModule_SourceEntityType_SourceSlot_SourceRecordId_LocalPath");
        sqliteIndexes.Should().Contain("IX_project_documents_ConflictWithDocumentId_ConflictObservedDriveFileId_ConflictObservedDriveVersion");

        var modelOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"project-document-model-{Guid.NewGuid():N}").Options;
        await using (var modelContext = new AppDbContext(modelOptions))
        {
            var document = modelContext.Model.FindEntityType(typeof(ProjectDocument))!;
            var folder = modelContext.Model.FindEntityType(typeof(ProjectDriveFolder))!;
            AssertRowVersion(document, nameof(ProjectDocument.RowVersion));
            AssertRowVersion(folder, nameof(ProjectDriveFolder.RowVersion));
        }

        var migration = new AddProjectDocumentDriveSync();
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(AddProjectDocumentDriveSync).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [migrationBuilder]);
        var operations = migrationBuilder.Operations;
        var documentTable = operations.OfType<CreateTableOperation>()
            .Single(operation => operation.Name == "project_documents");
        var folderTable = operations.OfType<CreateTableOperation>()
            .Single(operation => operation.Name == "project_drive_folders");
        AssertSqlServerRowVersion(documentTable, "RowVersion");
        AssertSqlServerRowVersion(folderTable, "RowVersion");
        var migrationIndexes = operations.OfType<CreateIndexOperation>().ToDictionary(index => index.Name);
        sqliteIndexes.Should().OnlyContain(name => migrationIndexes.ContainsKey(name));
        migrationIndexes["IX_project_documents_DriveFileId"].IsUnique.Should().BeTrue();
        migrationIndexes["IX_project_documents_OperationalProjectId_SourceModule_SourceEntityType_SourceSlot_SourceRecordId_LocalPath"].IsUnique.Should().BeTrue();
        migrationIndexes["IX_project_documents_ConflictWithDocumentId_ConflictObservedDriveFileId_ConflictObservedDriveVersion"].IsUnique.Should().BeTrue();
    }

    private async Task AuthenticateAsAsync(string role) =>
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));

    private async Task<int> CreateProjectAsync()
    {
        var projectId = await WithDbAsync(async db =>
        {
            var ownerId = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.SuperAdminPhone)
                .Select(user => user.Id).SingleAsync();
            var customer = new Customer
            {
                Name = $"Document customer {Guid.NewGuid():N}",
                Type = CustomerType.Company,
                SourceCode = "referral",
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-DOC-{Guid.NewGuid():N}"[..30],
                Name = "Project document integration",
                CustomerId = customer.Id,
                ProjectManagerUserId = ownerId,
                CreatedByUserId = ownerId,
                UpdatedByUserId = ownerId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
        projectIdsToClean.Add(projectId);
        return projectId;
    }

    private async Task<ProjectDocument> AddDocumentAsync(
        int projectId,
        ProjectDocumentCategory category,
        ProjectDocumentOrigin origin = ProjectDocumentOrigin.Nicon,
        ProjectDocumentSyncStatus syncStatus = ProjectDocumentSyncStatus.Pending,
        int syncAttemptCount = 0,
        DateTime? nextSyncAttemptAt = null,
        ProjectDocumentConflictState conflictState = ProjectDocumentConflictState.None,
        long? conflictWithDocumentId = null)
    {
        var fileName = $"{Guid.NewGuid():N}.pdf";
        var localPath = $"/files/project-documents/{projectId}/{fileName}";
        var fullPath = FullPath(localPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, PdfBytes);
        var document = await WithDbAsync(async db =>
        {
            var entity = new ProjectDocument
            {
                OperationalProjectId = projectId,
                Category = category,
                SourceModule = ProjectDocumentSourceModule.General,
                SourceType = origin == ProjectDocumentOrigin.GoogleDrive
                    ? ProjectDocumentSourceType.GoogleDriveImport
                    : ProjectDocumentSourceType.ManualUpload,
                LocalPath = localPath,
                OriginalFileName = "document.pdf",
                ContentType = "application/pdf",
                Size = PdfBytes.Length,
                Sha256 = Convert.ToHexString(SHA256.HashData(PdfBytes)).ToLowerInvariant(),
                Origin = origin,
                Generation = 1,
                DesiredOperation = syncStatus == ProjectDocumentSyncStatus.Synced
                    ? ProjectDocumentDesiredOperation.None
                    : ProjectDocumentDesiredOperation.Upsert,
                SyncStatus = syncStatus,
                SyncAttemptCount = syncAttemptCount,
                NextSyncAttemptAt = nextSyncAttemptAt ?? DateTime.UtcNow,
                ConflictState = conflictState,
                ConflictWithDocumentId = conflictWithDocumentId,
                DriveFileId = origin == ProjectDocumentOrigin.GoogleDrive ? $"drive-{Guid.NewGuid():N}" : null,
                DriveFolderId = origin == ProjectDocumentOrigin.GoogleDrive ? "folder-import" : null,
            };
            db.ProjectDocuments.Add(entity);
            await db.SaveChangesAsync();
            return entity;
        });
        return document;
    }

    private Task<ProjectDocument> LoadDocumentAsync(long id) => WithDbAsync(async db =>
        await db.ProjectDocuments.AsNoTracking().SingleAsync(document => document.Id == id));

    private Task<int> CountDocumentsAsync(int projectId) => WithDbAsync(async db =>
        await db.ProjectDocuments.CountAsync(document => document.OperationalProjectId == projectId));

    private static MultipartFormDataContent CreateUploadContent(
        string category, string fileName, byte[] content)
    {
        var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        multipart.Add(file, "file", fileName);
        multipart.Add(new StringContent(category), "category");
        multipart.Add(new StringContent("General"), "sourceModule");
        return multipart;
    }

    private string FullPath(string localPath)
    {
        var environment = Factory.Services.GetRequiredService<IWebHostEnvironment>();
        return Path.Combine(environment.ContentRootPath, "wwwroot",
            localPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    }

    private string[] ProjectFiles(int projectId)
    {
        var directory = Path.GetDirectoryName(FullPath($"/files/project-documents/{projectId}/placeholder"))!;
        return Directory.Exists(directory) ? Directory.GetFiles(directory) : [];
    }

    private static DocumentSnapshot Snapshot(ProjectDocument document) => new(
        document.OperationalProjectId,
        document.Category,
        document.Generation,
        document.DesiredOperation,
        document.SyncStatus,
        document.SyncAttemptCount,
        document.SyncError,
        document.NextSyncAttemptAt,
        document.ConflictState,
        document.ConflictWithDocumentId,
        document.DeletedAt,
        document.LocalPath);

    private static void AssertRowVersion(IEntityType entity, string propertyName)
    {
        var property = entity.FindProperty(propertyName)!;
        property.IsConcurrencyToken.Should().BeTrue();
        property.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
    }

    private static void AssertSqlServerRowVersion(CreateTableOperation table, string columnName)
    {
        var column = table.Columns.Single(item => item.Name == columnName);
        column.ColumnType.Should().Be("rowversion");
        column.IsRowVersion.Should().BeTrue();
        column.IsNullable.Should().BeFalse();
    }

    private static void AssertProblemDetails(JsonElement body, HttpStatusCode statusCode)
    {
        body.GetProperty("status").GetInt32().Should().Be((int)statusCode);
        body.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    public void Dispose()
    {
        foreach (var projectId in projectIdsToClean)
        {
            var directory = Path.GetDirectoryName(FullPath($"/files/project-documents/{projectId}/placeholder"))!;
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        Client.Dispose();
    }

    private sealed record DocumentSnapshot(
        int OperationalProjectId,
        ProjectDocumentCategory Category,
        long Generation,
        ProjectDocumentDesiredOperation DesiredOperation,
        ProjectDocumentSyncStatus SyncStatus,
        int SyncAttemptCount,
        string? SyncError,
        DateTime? NextSyncAttemptAt,
        ProjectDocumentConflictState ConflictState,
        long? ConflictWithDocumentId,
        DateTime? DeletedAt,
        string LocalPath);
}

public sealed class OperationalProjectDocumentsWebApplicationFactory : NihomeWebApplicationFactory
{
    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(), $"nihome-project-documents-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(ProjectDriveSyncService)).ToList())
                services.Remove(descriptor);
            services.RemoveAll<GoogleDriveOptions>();
            services.AddSingleton(new GoogleDriveOptions
            {
                Enabled = true,
                InstanceId = "integration-tests",
                RootFolderId = "root",
            });
            services.RemoveAll<IGoogleDriveAdapter>();
            services.AddSingleton<IGoogleDriveAdapter, InMemoryGoogleDriveAdapter>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
            }.ToString();
            services.AddScoped<AppDbContext>(_ => new SqliteAppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite(connectionString)
                    .AddInterceptors(new TestRowVersionInterceptor())
                    .Options));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            TestDataSeeder.Seed(db);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        if (File.Exists(databasePath)) File.Delete(databasePath);
        if (File.Exists(databasePath + "-shm")) File.Delete(databasePath + "-shm");
        if (File.Exists(databasePath + "-wal")) File.Delete(databasePath + "-wal");
    }

    private sealed class SqliteAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
            {
                property.SetColumnType(null);
                if (property.IsConcurrencyToken && property.ClrType == typeof(byte[]))
                    property.ValueGenerated = ValueGenerated.Never;
            }
        }
    }

    private sealed class InMemoryGoogleDriveAdapter : IGoogleDriveAdapter
    {
        private readonly ConcurrentDictionary<string, DriveEntry> files = new();

        public Task<DriveConnection> CheckConnectionAsync(CancellationToken ct = default) => Task.FromResult(
            new DriveConnection("integration@nicon.test", "root", "https://drive.test/root", true, false, false, true));

        public Task<DriveFolder> EnsureFolderPathAsync(IReadOnlyList<string> folderNames, CancellationToken ct = default) =>
            Task.FromResult(Folder(folderNames));

        public Task<DriveFolder> EnsureFolderPathAsync(IReadOnlyList<DriveFolderSegment> folders, CancellationToken ct = default) =>
            Task.FromResult(Folder(folders.Select(folder => folder.Name)));

        public async Task<DriveUpload> UploadAsync(string folderId, long surveyMediaId, string fileName,
            string contentType, Stream content, CancellationToken ct = default) =>
            await UploadAsync(folderId, $"survey:{surveyMediaId}", 1, fileName, contentType, content, ct);

        public async Task<DriveUpload> UploadAsync(string folderId, string replicaKey, long generation,
            string fileName, string contentType, Stream content, CancellationToken ct = default)
        {
            await using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            var id = $"file-{Guid.NewGuid():N}";
            var modifiedAt = DateTime.UtcNow;
            files[id] = new DriveEntry(folderId, fileName, contentType, buffer.ToArray(), modifiedAt, false);
            return new DriveUpload(id, "1", modifiedAt, $"https://drive.test/{id}");
        }

        public Task<IReadOnlyList<DriveItem>> ListChildrenAsync(string folderId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DriveItem>>(files
                .Where(entry => entry.Value.FolderId == folderId && !entry.Value.IsTrashed)
                .Select(entry => Item(entry.Key, entry.Value)).ToList());

        public async Task DownloadAsync(string fileId, Stream destination, CancellationToken ct = default)
        {
            if (!files.TryGetValue(fileId, out var entry) || entry.IsTrashed) throw new FileNotFoundException();
            await destination.WriteAsync(entry.Content, ct);
        }

        public Task<DriveItem?> GetMetadataAsync(string fileId, CancellationToken ct = default) =>
            Task.FromResult(files.TryGetValue(fileId, out var entry) ? Item(fileId, entry) : null);

        public Task UpdateFileNameAsync(string fileId, string fileName, CancellationToken ct = default)
        {
            files[fileId] = files[fileId] with { Name = fileName, ModifiedAt = DateTime.UtcNow };
            return Task.CompletedTask;
        }

        public Task MoveAsync(string fileId, string destinationFolderId, CancellationToken ct = default)
        {
            if (files.TryGetValue(fileId, out var entry)) files[fileId] = entry with { FolderId = destinationFolderId };
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string fileId, CancellationToken ct = default)
        {
            if (files.TryGetValue(fileId, out var entry)) files[fileId] = entry with { IsTrashed = true };
            return Task.CompletedTask;
        }

        private static DriveFolder Folder(IEnumerable<string> names)
        {
            var key = string.Join('/', names);
            var id = $"folder-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant()[..16]}";
            return new DriveFolder(id, $"https://drive.test/folders/{id}");
        }

        private static DriveItem Item(string id, DriveEntry entry) => new(
            id, entry.Name, entry.ContentType, entry.Content.LongLength, "1", entry.ModifiedAt,
            $"https://drive.test/{id}", new Dictionary<string, string>(), entry.IsTrashed);

        private sealed record DriveEntry(
            string FolderId,
            string Name,
            string ContentType,
            byte[] Content,
            DateTime ModifiedAt,
            bool IsTrashed);
    }
}
