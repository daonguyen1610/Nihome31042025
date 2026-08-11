using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class VendorServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<INotificationService> _notifications = new();
    private readonly VendorService _sut;
    private readonly string _contentRoot;
    private readonly int _ownerId;
    private readonly int _otherOwnerId;
    private readonly int _inactiveOwnerId;
    private readonly int _projectId;

    public VendorServiceTests()
    {
        _db = DbContextFactory.Create();
        _contentRoot = Path.Combine(Path.GetTempPath(), $"vendor-service-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _sut = new VendorService(
            _db,
            Mock.Of<IWebHostEnvironment>(environment => environment.ContentRootPath == _contentRoot),
            _notifications.Object,
            NullLogger<VendorService>.Instance);

        var owner = User("0901000001", "Vendor Owner");
        var otherOwner = User("0901000002", "Other Owner");
        var inactiveOwner = User("0901000003", "Inactive Owner", false);
        var customer = new Customer { Name = "Vendor Test Customer", Type = CustomerType.Company };
        _db.AddRange(owner, otherOwner, inactiveOwner, customer);
        _db.MasterDataOptions.AddRange(
            new MasterDataOption
            {
                Category = VendorService.ServiceGroupCategory,
                Code = "mep",
                Name = "MEP",
                IsActive = true,
            },
            new MasterDataOption
            {
                Category = VendorService.ServiceGroupCategory,
                Code = "inactive-group",
                Name = "Inactive",
                IsActive = false,
            });
        _db.SaveChanges();

        var project = new DesignProject
        {
            ProjectCode = "DP-VENDOR-UNIT",
            Name = "Vendor Evaluation Project",
            CustomerId = customer.Id,
        };
        _db.DesignProjects.Add(project);
        _db.SaveChanges();

        _ownerId = owner.Id;
        _otherOwnerId = otherOwner.Id;
        _inactiveOwnerId = inactiveOwner.Id;
        _projectId = project.Id;
    }

    [Fact]
    public async Task CreateAsync_normalizes_values_and_maps_owner()
    {
        var created = await _sut.CreateAsync(ValidCreate(
            code: "  ven-001  ", company: "  Alpha   Engineering  ", serviceGroup: " MEP "),
            _ownerId, false);

        Assert.Equal("VEN-001", created.VendorCode);
        Assert.Equal("Alpha   Engineering", created.CompanyName);
        Assert.Equal("mep", created.ServiceGroupCode);
        Assert.Equal(_ownerId, created.OwnerUserId);
        Assert.Equal("Vendor Owner", created.OwnerName);
        Assert.Equal("ALPHA ENGINEERING", (await _db.Vendors.FindAsync(created.Id))!.NormalizedCompanyName);
    }

    [Theory]
    [InlineData("code")]
    [InlineData("company")]
    [InlineData("tax")]
    public async Task CreateAsync_rejects_duplicate_normalized_identity(string duplicateField)
    {
        await _sut.CreateAsync(ValidCreate(code: "VEN-DUP", company: "Alpha Company", taxCode: "TAX-01"), _ownerId, true);
        var request = ValidCreate(code: "VEN-NEW", company: "Beta Company", taxCode: "TAX-02");
        if (duplicateField == "code") request.VendorCode = " ven-dup ";
        if (duplicateField == "company") request.CompanyName = " alpha   company ";
        if (duplicateField == "tax") request.TaxCode = " tax-01 ";

        var exception = await Assert.ThrowsAsync<VendorOperationException>(
            () => _sut.CreateAsync(request, _ownerId, true));

        Assert.Contains("already exists", exception.Message);
    }

    [Theory]
    [InlineData("unknown-owner")]
    [InlineData("inactive-owner")]
    [InlineData("unknown-group")]
    [InlineData("inactive-group")]
    public async Task CreateAsync_rejects_invalid_owner_or_service_group(string scenario)
    {
        var request = ValidCreate();
        if (scenario == "unknown-owner") request.OwnerUserId = 999999;
        if (scenario == "inactive-owner") request.OwnerUserId = _inactiveOwnerId;
        if (scenario == "unknown-group") request.ServiceGroupCode = "unknown";
        if (scenario == "inactive-group") request.ServiceGroupCode = "inactive-group";

        await Assert.ThrowsAsync<VendorOperationException>(() => _sut.CreateAsync(request, _ownerId, true));
    }

    [Fact]
    public async Task CreateAsync_enforces_owner_assignment_by_scope()
    {
        var request = ValidCreate(ownerUserId: _otherOwnerId);

        await Assert.ThrowsAsync<VendorOperationException>(() => _sut.CreateAsync(request, _ownerId, false));
        var created = await _sut.CreateAsync(request, _ownerId, true);

        Assert.Equal(_otherOwnerId, created.OwnerUserId);
    }

    [Fact]
    public async Task ListAsync_applies_search_filters_sort_pagination_and_owner_scope()
    {
        AddVendor("VEN-003", "Zulu MEP", _ownerId, VendorType.Supplier, true, "mep", "needle@example.test");
        AddVendor("VEN-001", "Alpha MEP", _ownerId, VendorType.Supplier, true, "mep");
        AddVendor("VEN-002", "Beta Civil", _ownerId, VendorType.SubContractor, false, "civil-works");
        AddVendor("VEN-004", "Other Owner", _otherOwnerId, VendorType.Supplier, true, "mep", "needle-other@example.test");
        await _db.SaveChangesAsync();

        var scoped = await _sut.ListAsync(_ownerId, false, null, null, null, _otherOwnerId, null,
            "vendorCode", "asc", 1, 2);
        var filtered = await _sut.ListAsync(_ownerId, true, "needle", VendorType.Supplier, true, _ownerId, " MEP ",
            "vendorCode", "desc", 1, 10);

        Assert.Equal(3, scoped.Total);
        Assert.Equal(new[] { "VEN-001", "VEN-002" }, scoped.Items.Select(item => item.VendorCode));
        Assert.Equal(2, scoped.PageSize);
        Assert.Single(filtered.Items);
        Assert.Equal("VEN-003", filtered.Items[0].VendorCode);
    }

    [Fact]
    public async Task Scoped_get_and_update_hide_other_owner_and_prevent_reassignment()
    {
        var own = AddVendor("VEN-OWN", "Owned Vendor", _ownerId);
        var other = AddVendor("VEN-OTHER", "Other Vendor", _otherOwnerId);
        await _db.SaveChangesAsync();

        Assert.Null(await _sut.GetAsync(other.Id, _ownerId, false));
        Assert.Null(await _sut.UpdateAsync(other.Id, ValidUpdate(other, _otherOwnerId), _ownerId, false));
        await Assert.ThrowsAsync<VendorOperationException>(
            () => _sut.UpdateAsync(own.Id, ValidUpdate(own, _otherOwnerId), _ownerId, false));
        Assert.NotNull(await _sut.GetAsync(other.Id, _ownerId, true));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("extension")]
    [InlineData("oversize")]
    public void ValidateDocument_rejects_invalid_files(string scenario)
    {
        var file = scenario switch
        {
            "empty" => FormFile(Array.Empty<byte>(), "empty.pdf", 0),
            "extension" => FormFile(Encoding.UTF8.GetBytes("payload"), "payload.exe"),
            _ => FormFile(new byte[] { 1 }, "large.pdf", VendorService.MaxDocumentSizeBytes + 1),
        };

        Assert.Throws<VendorOperationException>(() => VendorService.ValidateDocument(file));
    }

    [Fact]
    public async Task Document_upload_download_and_delete_round_trip()
    {
        var vendor = AddVendor("VEN-DOC", "Document Vendor", _ownerId);
        await _db.SaveChangesAsync();
        var bytes = Encoding.UTF8.GetBytes("vendor document");

        var uploaded = await _sut.UploadDocumentAsync(
            vendor.Id, VendorDocumentType.Capability, FormFile(bytes, "capability.pdf"), _ownerId, false);
        await using var downloaded = (await _sut.DownloadDocumentAsync(
            vendor.Id, uploaded!.Id, _ownerId, false))!.Content;
        using var buffer = new MemoryStream();
        await downloaded.CopyToAsync(buffer);

        Assert.Equal(bytes, buffer.ToArray());
        Assert.Equal("capability.pdf", uploaded.OriginalFileName);
        Assert.True(await _sut.DeleteDocumentAsync(vendor.Id, uploaded.Id, _ownerId, false));
        Assert.False(await _db.VendorDocuments.AnyAsync(document => document.Id == uploaded.Id));
    }

    [Fact]
    public async Task UploadDocumentAsync_rejects_invalid_enum()
    {
        var vendor = AddVendor("VEN-ENUM", "Enum Vendor", _ownerId);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<VendorOperationException>(() => _sut.UploadDocumentAsync(
            vendor.Id, (VendorDocumentType)999, FormFile(new byte[] { 1 }, "file.pdf"), _ownerId, false));
    }

    [Fact]
    public void GetDocumentPath_rejects_path_traversal()
    {
        Assert.Throws<VendorOperationException>(() => VendorService.GetDocumentPath(_contentRoot, 1, "../secret.pdf"));
        Assert.Throws<VendorOperationException>(() => VendorService.GetDocumentPath(_contentRoot, 1, "/tmp/secret.pdf"));
    }

    [Fact]
    public async Task DownloadDocumentAsync_reports_missing_physical_file()
    {
        var vendor = AddVendor("VEN-MISSING", "Missing File Vendor", _ownerId);
        await _db.SaveChangesAsync();
        var document = new VendorDocument
        {
            VendorId = vendor.Id,
            DocumentType = VendorDocumentType.Other,
            OriginalFileName = "missing.pdf",
            StoredFileName = "missing.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 10,
            CreatedByUserId = _ownerId,
        };
        _db.VendorDocuments.Add(document);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<VendorDocumentMissingException>(
            () => _sut.DownloadDocumentAsync(vendor.Id, document.Id, _ownerId, false));
    }

    [Fact]
    public async Task Evaluation_validates_project_duplicate_average_and_owner_scope()
    {
        var own = AddVendor("VEN-EVAL", "Evaluation Vendor", _ownerId);
        var other = AddVendor("VEN-EVAL-OTHER", "Other Evaluation Vendor", _otherOwnerId);
        await _db.SaveChangesAsync();

        var invalid = Evaluation(projectId: 999999);
        await Assert.ThrowsAsync<VendorOperationException>(
            () => _sut.CreateEvaluationAsync(own.Id, invalid, _ownerId, false));
        Assert.Null(await _sut.CreateEvaluationAsync(other.Id, Evaluation(), _ownerId, false));

        var created = await _sut.CreateEvaluationAsync(own.Id, Evaluation(), _ownerId, false);
        Assert.Equal(7.5m, created!.AverageScore);
        await Assert.ThrowsAsync<VendorOperationException>(
            () => _sut.CreateEvaluationAsync(own.Id, Evaluation(), _ownerId, false));
    }

    [Fact]
    public async Task UpdateEvaluationAsync_preserves_provenance_and_changes_updater()
    {
        var vendor = AddVendor("VEN-PROV", "Provenance Vendor", _ownerId);
        await _db.SaveChangesAsync();
        var created = await _sut.CreateEvaluationAsync(vendor.Id, Evaluation(), _ownerId, true);
        var evaluatedAt = created!.EvaluatedAt;

        var updated = await _sut.UpdateEvaluationAsync(vendor.Id, created.Id,
            Evaluation(quality: 10, schedule: 10, cost: 10, safety: 10), _otherOwnerId, true);

        Assert.Equal(_ownerId, updated!.EvaluatedByUserId);
        Assert.Equal(evaluatedAt, updated.EvaluatedAt);
        Assert.Equal(_otherOwnerId, updated.UpdatedByUserId);
        Assert.Equal(10m, updated.AverageScore);
    }

    [Fact]
    public async Task GetHistoryAsync_returns_scoped_old_and_new_json()
    {
        var own = AddVendor("VEN-HISTORY", "History Vendor", _ownerId);
        var other = AddVendor("VEN-HISTORY-OTHER", "Other History Vendor", _otherOwnerId);
        await _db.SaveChangesAsync();
        _db.AuditLogs.Add(new AuditLog
        {
            AuditId = Guid.NewGuid().ToString("N"),
            ResourceType = EntityTypes.Vendor,
            ResourceId = own.Id.ToString(),
            Action = "vendor.update",
            Message = "Updated",
            ActorUserId = _ownerId,
            OldValueJson = "{\"companyName\":\"Old\"}",
            NewValueJson = "{\"companyName\":\"New\"}",
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var history = await _sut.GetHistoryAsync(own.Id, _ownerId, false);

        var entry = Assert.Single(history!);
        Assert.Contains("Old", entry.OldValueJson!);
        Assert.Contains("New", entry.NewValueJson!);
        Assert.Null(await _sut.GetHistoryAsync(other.Id, _ownerId, false));
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_contentRoot)) Directory.Delete(_contentRoot, recursive: true);
    }

    private CreateVendorRequest ValidCreate(
        string code = "VEN-001",
        string company = "Vendor Company",
        string? taxCode = null,
        string serviceGroup = "mep",
        int? ownerUserId = null) => new()
    {
        VendorCode = code,
        CompanyName = company,
        VendorType = VendorType.Supplier,
        TaxCode = taxCode,
        ServiceGroupCode = serviceGroup,
        OwnerUserId = ownerUserId ?? _ownerId,
        IsActive = true,
    };

    private static UpdateVendorRequest ValidUpdate(Vendor vendor, int ownerUserId) => new()
    {
        VendorCode = vendor.VendorCode,
        CompanyName = vendor.CompanyName,
        VendorType = vendor.VendorType,
        TaxCode = vendor.TaxCode,
        ServiceGroupCode = vendor.ServiceGroupCode,
        OwnerUserId = ownerUserId,
        IsActive = vendor.IsActive,
    };

    private UpsertVendorEvaluationRequest Evaluation(
        int? projectId = null, byte quality = 9, byte schedule = 8, byte cost = 7, byte safety = 6) => new()
    {
        ProjectId = projectId ?? _projectId,
        ScoreQuality = quality,
        ScoreSchedule = schedule,
        ScoreCost = cost,
        ScoreSafety = safety,
        Comment = " Evaluated ",
    };

    private Vendor AddVendor(
        string code,
        string company,
        int ownerUserId,
        VendorType type = VendorType.Supplier,
        bool isActive = true,
        string serviceGroup = "mep",
        string? email = null)
    {
        var vendor = new Vendor
        {
            VendorCode = code,
            CompanyName = company,
            NormalizedCompanyName = VendorService.NormalizeCompanyName(company),
            VendorType = type,
            Email = email,
            ServiceGroupCode = serviceGroup,
            OwnerUserId = ownerUserId,
            IsActive = isActive,
            CreatedByUserId = ownerUserId,
            UpdatedByUserId = ownerUserId,
        };
        _db.Vendors.Add(vendor);
        return vendor;
    }

    private static ApplicationUser User(string phone, string name, bool active = true) => new()
    {
        PhoneNumber = phone,
        FullName = name,
        Email = $"{phone}@example.test",
        PasswordHash = "x",
        Role = UserRole.USER,
        IsActive = active,
    };

    private static FormFile FormFile(byte[] bytes, string fileName, long? declaredLength = null)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, declaredLength ?? bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };
    }
}
