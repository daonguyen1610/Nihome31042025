using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class CapabilityDocumentServiceTests : IDisposable
{
    private const string ValidPath = "/files/capability/aaaa1111.pdf";
    private readonly AppDbContext _db;
    private readonly CapabilityDocumentService _sut;

    public CapabilityDocumentServiceTests()
    {
        _db = DbContextFactory.Create();
        SeedTagOptions();
        _sut = new CapabilityDocumentService(_db, NullLogger<CapabilityDocumentService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private void SeedTagOptions()
    {
        _db.MasterDataOptions.AddRange(
            new MasterDataOption { Category = "capability_document_tag", Code = "iso", Name = "ISO", IsActive = true, SortOrder = 1 },
            new MasterDataOption { Category = "capability_document_tag", Code = "kien-truc", Name = "Kiến trúc", IsActive = true, SortOrder = 2 },
            new MasterDataOption { Category = "capability_document_tag", Code = "inactive", Name = "Inactive", IsActive = false, SortOrder = 99 }
        );
        _db.SaveChanges();
    }

    private static UpsertCapabilityDocumentRequest NewCreateRequest(
        string tag = "iso",
        string? name = null,
        DateTime? issued = null,
        DateTime? expiry = null,
        string filePath = ValidPath,
        string originalFileName = "ISO 9001.pdf")
        => new()
        {
            Name = name ?? "Chứng nhận ISO 9001:2015",
            TagCode = tag,
            IssuedDate = issued,
            ExpiryDate = expiry,
            FilePath = filePath,
            OriginalFileName = originalFileName,
            FileSize = 1024,
            ContentType = "application/pdf",
        };

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateAsync_HappyPath_PersistsRowAndReturnsTagLabel()
    {
        var resp = await _sut.CreateAsync(NewCreateRequest(), callerUserId: 42);

        Assert.NotEqual(0, resp.Id);
        Assert.Equal("ISO", resp.TagLabel);
        Assert.Equal(1, resp.CurrentVersion);
        Assert.Equal(42, resp.UploadedByUserId);
        Assert.Equal(ValidPath, resp.FilePath);
    }

    [Fact]
    public async Task CreateAsync_WithInactiveTag_Throws()
    {
        var ex = await Assert.ThrowsAsync<CapabilityDocumentOperationException>(() =>
            _sut.CreateAsync(NewCreateRequest(tag: "inactive"), 1));
        Assert.Contains("không hợp lệ", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownTag_Throws()
    {
        await Assert.ThrowsAsync<CapabilityDocumentOperationException>(() =>
            _sut.CreateAsync(NewCreateRequest(tag: "does-not-exist"), 1));
    }

    [Fact]
    public async Task CreateAsync_WithoutFilePath_Throws()
    {
        var req = NewCreateRequest();
        req.FilePath = null;
        req.OriginalFileName = null;
        await Assert.ThrowsAsync<CapabilityDocumentOperationException>(() => _sut.CreateAsync(req, 1));
    }

    [Fact]
    public async Task CreateAsync_WithNonManagedPath_Throws()
    {
        var req = NewCreateRequest(filePath: "/images/upload/foo.pdf");
        await Assert.ThrowsAsync<CapabilityDocumentOperationException>(() => _sut.CreateAsync(req, 1));
    }

    [Fact]
    public async Task CreateAsync_ExpiryBeforeIssue_Throws()
    {
        var req = NewCreateRequest(
            issued: new DateTime(2026, 6, 1),
            expiry: new DateTime(2025, 6, 1));
        await Assert.ThrowsAsync<CapabilityDocumentOperationException>(() => _sut.CreateAsync(req, 1));
    }

    [Fact]
    public async Task CreateAsync_AbsoluteUrlWithinManagedPath_IsNormalisedToRelative()
    {
        var req = NewCreateRequest(filePath: "https://example.com/files/capability/abc.pdf");
        var resp = await _sut.CreateAsync(req, 1);
        Assert.Equal("/files/capability/abc.pdf", resp.FilePath);
    }

    // ---------------- Update ----------------

    [Fact]
    public async Task UpdateAsync_ChangesMetadataButKeepsFile()
    {
        var created = await _sut.CreateAsync(NewCreateRequest(), 1);

        var req = NewCreateRequest(tag: "kien-truc", name: "Portfolio 2026");
        req.FilePath = null;                // metadata-only update
        req.OriginalFileName = null;
        var updated = await _sut.UpdateAsync(created.Id, req, 2);

        Assert.NotNull(updated);
        Assert.Equal("Portfolio 2026", updated!.Name);
        Assert.Equal("kien-truc", updated.TagCode);
        Assert.Equal(created.FilePath, updated.FilePath);
        Assert.Equal(1, updated.CurrentVersion); // no version bump for metadata
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNull()
    {
        var updated = await _sut.UpdateAsync(999, NewCreateRequest(), 1);
        Assert.Null(updated);
    }

    // ---------------- Replace file ----------------

    [Fact]
    public async Task ReplaceFileAsync_BumpsVersionAndArchivesPreviousFile()
    {
        var created = await _sut.CreateAsync(NewCreateRequest(originalFileName: "v1.pdf"), 1);

        var replaced = await _sut.ReplaceFileAsync(created.Id, new ReplaceCapabilityDocumentFileRequest
        {
            FilePath = "/files/capability/bbbb2222.pdf",
            OriginalFileName = "v2.pdf",
            FileSize = 2048,
            ContentType = "application/pdf",
        }, callerUserId: 99);

        Assert.NotNull(replaced);
        Assert.Equal(2, replaced!.CurrentVersion);
        Assert.Equal("v2.pdf", replaced.OriginalFileName);
        Assert.Equal(1, replaced.PreviousVersionCount);

        var detail = await _sut.GetAsync(created.Id);
        Assert.NotNull(detail);
        Assert.Single(detail!.Versions);
        var snapshot = detail.Versions[0];
        Assert.Equal(1, snapshot.VersionNumber);
        Assert.Equal("v1.pdf", snapshot.OriginalFileName);
    }

    [Fact]
    public async Task ReplaceFileAsync_SamePathAsCurrent_Throws()
    {
        var created = await _sut.CreateAsync(NewCreateRequest(), 1);
        await Assert.ThrowsAsync<CapabilityDocumentOperationException>(() =>
            _sut.ReplaceFileAsync(created.Id, new ReplaceCapabilityDocumentFileRequest
            {
                FilePath = created.FilePath,
                OriginalFileName = "same.pdf",
                FileSize = 1,
                ContentType = "application/pdf",
            }, 1));
    }

    [Fact]
    public async Task ReplaceFileAsync_UnknownId_ReturnsNull()
    {
        var replaced = await _sut.ReplaceFileAsync(999, new ReplaceCapabilityDocumentFileRequest
        {
            FilePath = "/files/capability/x.pdf",
            OriginalFileName = "x.pdf",
            FileSize = 1,
            ContentType = "application/pdf",
        }, 1);
        Assert.Null(replaced);
    }

    // ---------------- Delete ----------------

    [Fact]
    public async Task DeleteAsync_RemovesRowAndReturnsTrue()
    {
        var created = await _sut.CreateAsync(NewCreateRequest(), 1);
        Assert.True(await _sut.DeleteAsync(created.Id));
        Assert.False(await _db.CapabilityDocuments.AnyAsync(d => d.Id == created.Id));
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    {
        Assert.False(await _sut.DeleteAsync(999));
    }

    // ---------------- List / expiry state ----------------

    [Fact]
    public void ComputeExpiryState_BandsMatchAcceptanceCriteria()
    {
        var now = new DateTime(2026, 7, 15);
        Assert.Equal("none", CapabilityDocumentService.ComputeExpiryState(null, now));
        Assert.Equal("expired", CapabilityDocumentService.ComputeExpiryState(now.AddDays(-1), now));
        Assert.Equal("critical", CapabilityDocumentService.ComputeExpiryState(now.AddDays(15), now));
        Assert.Equal("critical", CapabilityDocumentService.ComputeExpiryState(now.AddDays(30), now));
        Assert.Equal("warning", CapabilityDocumentService.ComputeExpiryState(now.AddDays(45), now));
        Assert.Equal("warning", CapabilityDocumentService.ComputeExpiryState(now.AddDays(60), now));
        Assert.Equal("ok", CapabilityDocumentService.ComputeExpiryState(now.AddDays(120), now));
    }

    [Fact]
    public async Task ListAsync_FiltersByTagAndSearchAndExpiryState()
    {
        // ISO expired
        var a = NewCreateRequest(name: "ISO Expired", tag: "iso");
        a.ExpiryDate = DateTime.UtcNow.AddDays(-10);
        a.FilePath = "/files/capability/a.pdf";
        await _sut.CreateAsync(a, 1);
        // ISO warning window
        var b = NewCreateRequest(name: "ISO Warning", tag: "iso");
        b.ExpiryDate = DateTime.UtcNow.AddDays(45);
        b.FilePath = "/files/capability/b.pdf";
        await _sut.CreateAsync(b, 1);
        // Kiến trúc no expiry
        var c = NewCreateRequest(name: "Kien Truc Portfolio", tag: "kien-truc");
        c.FilePath = "/files/capability/c.pdf";
        await _sut.CreateAsync(c, 1);

        var isoResult = await _sut.ListAsync(tagCode: "iso");
        Assert.Equal(2, isoResult.Total);

        var expired = await _sut.ListAsync(expiryState: "expired");
        Assert.Single(expired.Items);
        Assert.Equal("ISO Expired", expired.Items[0].Name);

        var searched = await _sut.ListAsync(search: "Portfolio");
        Assert.Single(searched.Items);
        Assert.Equal("Kien Truc Portfolio", searched.Items[0].Name);
    }

    [Fact]
    public void NormalizeManagedPath_RejectsTraversalAndForeignPaths()
    {
        Assert.Null(CapabilityDocumentService.NormalizeManagedPath(""));
        Assert.Null(CapabilityDocumentService.NormalizeManagedPath("/etc/passwd"));
        Assert.Null(CapabilityDocumentService.NormalizeManagedPath("/images/upload/foo.pdf"));
        Assert.Null(CapabilityDocumentService.NormalizeManagedPath("/files/capability/../secret"));
        Assert.Equal("/files/capability/x.pdf",
            CapabilityDocumentService.NormalizeManagedPath("/files/capability/x.pdf"));
        Assert.Equal("/files/capability/x.pdf",
            CapabilityDocumentService.NormalizeManagedPath("https://api.example.com/files/capability/x.pdf"));
    }
}
