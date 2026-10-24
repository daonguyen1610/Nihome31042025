using System.IO;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class DesignReviewDownloadTests : IClassFixture<DesignReviewDownloadFactory>
{
    private readonly DesignReviewDownloadFactory factory;

    public DesignReviewDownloadTests(DesignReviewDownloadFactory factory) => this.factory = factory;

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task RecipientDownload_VerifiesActualBytesAgainstReviewSnapshot(
        bool changedBytes, bool updatedCatalog, bool versionedRoute)
    {
        var fixture = await SeedAsync(changedBytes, updatedCatalog);
        using var client = factory.CreateClient();
        await AuthTestHelper.AuthenticateAsync(client, api => AuthTestHelper.LoginAsRoleAsync(api, "SALE"));
        var prefix = versionedRoute ? "/api/v1" : "/api";

        using var response = await client.GetAsync(
            $"{prefix}/design-projects/{fixture.ProjectId}/reviews/{fixture.ReviewId}/documents/{fixture.DocumentId}/content");

        if (!changedBytes)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
            response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("NICON-Concept-R00.pdf");
            (await response.Content.ReadAsByteArrayAsync()).Should().Equal(fixture.ApprovedBytes);
        }
        else
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            response.Content.Headers.ContentDisposition.Should().BeNull();
            (await response.Content.ReadAsStringAsync()).Should().Contain("không khớp");
        }
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var review = await db.DesignReviews.AsNoTracking().Include(item => item.Documents)
            .SingleAsync(item => item.Id == fixture.ReviewId);
        review.Status.Should().Be(DesignReviewStatus.InternalReview);
        review.Documents.Single().Sha256Snapshot.Should().Be(fixture.ApprovedHash);
        var document = await db.ProjectDocuments.AsNoTracking().SingleAsync(item => item.Id == fixture.DocumentId);
        document.Sha256.Should().Be(fixture.CatalogHash);
        document.DeletedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("ACCOUNTANT")]
    [InlineData(null)]
    public async Task UnassignedOrAnonymousCaller_CannotReadEvidence(string? role)
    {
        var fixture = await SeedAsync(false, false);
        using var client = factory.CreateClient();
        if (role is not null)
            await AuthTestHelper.AuthenticateAsync(client, api => AuthTestHelper.LoginAsRoleAsync(api, role));
        var readsBefore = factory.Storage.ReadCount;

        using var response = await client.GetAsync(
            $"/api/design-projects/{fixture.ProjectId}/reviews/{fixture.ReviewId}/documents/{fixture.DocumentId}/content");

        response.StatusCode.Should().Be(role is null ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound);
        factory.Storage.ReadCount.Should().Be(readsBefore);
    }

    private async Task<DownloadFixture> SeedAsync(bool changedBytes, bool updatedCatalog)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var manager = await db.Users.Include(item => item.RoleEntity).FirstAsync(item => item.RoleEntity!.Code == "DESIGN");
        var recipient = await db.Users.Include(item => item.RoleEntity).FirstAsync(item => item.RoleEntity!.Code == "SALE");
        var customer = new Customer { Name = "Chủ đầu tư nhà xưởng NICON kiểm tra hồ sơ" };
        var project = new OperationalProject
        {
            Code = $"NIC-HASH-{Guid.NewGuid():N}"[..25],
            Name = "Nhà xưởng NICON - kiểm tra bản đã trình",
            Customer = customer,
            ProjectManagerUserId = manager.Id,
        };
        var design = new DesignProject
        {
            ProjectCode = $"DP-HASH-{Guid.NewGuid():N}"[..25],
            Name = project.Name,
            OperationalProject = project,
            Customer = customer,
            ProjectManagerUserId = manager.Id,
        };
        db.Add(design);
        await db.SaveChangesAsync();
        var approved = Enumerable.Repeat((byte)'A', 96 * 1024).ToArray();
        var actual = changedBytes ? Enumerable.Repeat((byte)'B', approved.Length).ToArray() : approved;
        var approvedHash = Convert.ToHexString(SHA256.HashData(approved)).ToLowerInvariant();
        var catalogHash = updatedCatalog ? Convert.ToHexString(SHA256.HashData(actual)).ToLowerInvariant() : approvedHash;
        var path = $"/files/project-documents/{Guid.NewGuid():N}.pdf";
        factory.Storage.Files[path] = actual;
        var document = new ProjectDocument
        {
            OperationalProjectId = project.Id,
            Category = ProjectDocumentCategory.DesignConcept,
            SourceModule = ProjectDocumentSourceModule.Design,
            OriginalFileName = "NICON-Concept-R00.pdf",
            ContentType = "application/pdf",
            LocalPath = path,
            Size = actual.Length,
            Sha256 = catalogHash,
            SyncStatus = ProjectDocumentSyncStatus.Synced,
        };
        db.Add(document);
        await db.SaveChangesAsync();
        var review = new DesignReview
        {
            DesignProjectId = design.Id,
            Stage = DesignReviewStage.Concept,
            Title = "Hồ sơ kiến trúc nhà xưởng R00 đã trình nội bộ",
            Status = DesignReviewStatus.InternalReview,
            DesignManagerUserId = manager.Id,
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
            Recipients = [new DesignReviewRecipient { UserId = recipient.Id }],
            Documents = [new DesignReviewDocument { ProjectDocumentId = document.Id, Sha256Snapshot = approvedHash }],
        };
        db.Add(review);
        await db.SaveChangesAsync();
        return new DownloadFixture(design.Id, review.Id, document.Id, approved, approvedHash, catalogHash);
    }

    private sealed record DownloadFixture(int ProjectId, int ReviewId, long DocumentId,
        byte[] ApprovedBytes, string ApprovedHash, string CatalogHash);
}

public sealed class DesignReviewDownloadFactory : NihomeWebApplicationFactory
{
    public SnapshotDocumentStorage Storage { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IProjectDocumentStorageService>();
            services.AddSingleton<IProjectDocumentStorageService>(Storage);
        });
    }

    public sealed class SnapshotDocumentStorage : IProjectDocumentStorageService
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public int ReadCount { get; private set; }
        public Stream OpenRead(int projectId, string localPath)
        {
            ReadCount++;
            return new MemoryStream(Files[localPath], writable: false);
        }
        public Task<StoredProjectDocument> InspectUploadAsync(IFormFile? file, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StoredProjectDocument> StoreAsync(int projectId, IFormFile? file, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StoredProjectDocument> StoreDriveImportAsync(int projectId, string fileName, string contentType, long? expectedLength, Func<Stream, CancellationToken, Task> writeContent, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StoredProjectDocument> InspectExistingAsync(ProjectDocumentSourceModule sourceModule, string localPath, string originalFileName, CancellationToken ct = default) => throw new NotSupportedException();
        public ManagedDocumentContent? GetContent(int projectId, string localPath, string originalFileName, string contentType) => throw new NotSupportedException();
        public void DeleteOwned(int projectId, string localPath) => throw new NotSupportedException();
    }
}
