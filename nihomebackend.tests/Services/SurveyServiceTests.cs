using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class SurveyServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SurveyService _sut;
    private readonly int _userId;

    public SurveyServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new SurveyService(_db, NullLogger<SurveyService>.Instance);

        var user = new ApplicationUser
        {
            PhoneNumber = "0900000010",
            FullName = "Surveyor Tester",
            Email = "surveyor.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);
        _db.MasterDataOptions.AddRange(
            new MasterDataOption { Category = "construction_type", Code = "residential", Name = "Nhà ở dân dụng", IsActive = true, SortOrder = 1 },
            new MasterDataOption { Category = "construction_type", Code = "commercial", Name = "Thương mại", IsActive = true, SortOrder = 2 },
            new MasterDataOption { Category = "construction_type", Code = "retired", Name = "Đã ẩn", IsActive = false, SortOrder = 9 }
        );
        _db.SaveChanges();
        _userId = user.Id;
    }

    public void Dispose() => _db.Dispose();

    private CreateSurveyRequest ValidCreate(string? location = null,
        string? typeCode = "residential",
        DateTime? date = null) => new()
        {
            Location = location ?? "Số 12 Nguyễn Trãi, Q. Thanh Xuân, Hà Nội",
            ConstructionTypeCode = typeCode,
            SurveyDate = date ?? DateTime.UtcNow.AddDays(-2),
            SurveyorUserId = _userId,
        };

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateAsync_HappyPath_AllocatesCodeAndLabel()
    {
        var resp = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.StartsWith($"SV-{DateTime.UtcNow.Year}-", resp.Code);
        Assert.EndsWith("-0001", resp.Code);
        Assert.Equal("Nhà ở dân dụng", resp.ConstructionTypeLabel);
        Assert.Equal("NotSynced", resp.DriveSyncStatus);
    }

    [Fact]
    public async Task CreateAsync_MissingLocation_Throws()
    {
        await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.CreateAsync(ValidCreate(location: "  "), _userId));
    }

    [Fact]
    public async Task CreateAsync_UnknownConstructionType_Throws()
    {
        await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.CreateAsync(ValidCreate(typeCode: "not-a-type"), _userId));
    }

    [Fact]
    public async Task CreateAsync_InactiveConstructionType_Throws()
    {
        // Master-data option exists but IsActive = false — should still 400.
        await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.CreateAsync(ValidCreate(typeCode: "retired"), _userId));
    }

    [Fact]
    public async Task CreateAsync_UnknownSurveyor_Throws()
    {
        var req = ValidCreate();
        req.SurveyorUserId = 99999;
        await Assert.ThrowsAsync<SurveyOperationException>(() => _sut.CreateAsync(req, _userId));
    }

    [Fact]
    public async Task CreateAsync_SequentialCodesPerYear()
    {
        var a = await _sut.CreateAsync(ValidCreate(), _userId);
        var b = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.EndsWith("-0001", a.Code);
        Assert.EndsWith("-0002", b.Code);
    }

    // ---------------- Get / List ----------------

    [Fact]
    public async Task GetAsync_ResolvesConstructionLabel()
    {
        var created = await _sut.CreateAsync(ValidCreate(typeCode: "commercial"), _userId);
        var got = await _sut.GetAsync(created.Id);
        Assert.NotNull(got);
        Assert.Equal("Thương mại", got!.ConstructionTypeLabel);
    }

    [Fact]
    public async Task GetAsync_UnknownReturnsNull()
    {
        Assert.Null(await _sut.GetAsync(99999));
    }

    [Fact]
    public async Task ListAsync_DefaultsToSurveyDateDescending()
    {
        var older = await _sut.CreateAsync(ValidCreate(date: DateTime.UtcNow.AddDays(-30)), _userId);
        var newer = await _sut.CreateAsync(ValidCreate(date: DateTime.UtcNow.AddDays(-1)), _userId);

        var list = await _sut.ListAsync(new SurveyListParams { PageSize = 50 });
        Assert.Equal(2, list.Total);
        Assert.Equal(newer.Id, list.Items[0].Id);
        Assert.Equal(older.Id, list.Items[1].Id);
    }

    [Fact]
    public async Task ListAsync_FiltersBySearchAndConstructionType()
    {
        await _sut.CreateAsync(ValidCreate(location: "Alpha site"), _userId);
        await _sut.CreateAsync(ValidCreate(location: "Beta site", typeCode: "commercial"), _userId);

        var searched = await _sut.ListAsync(new SurveyListParams { Search = "Alpha" });
        Assert.Single(searched.Items);
        Assert.Contains("Alpha", searched.Items[0].Location);

        var byType = await _sut.ListAsync(new SurveyListParams { ConstructionTypeCode = "commercial" });
        Assert.Single(byType.Items);
        Assert.Equal("commercial", byType.Items[0].ConstructionTypeCode);
    }

    [Fact]
    public async Task ListAsync_FiltersByDateRange()
    {
        await _sut.CreateAsync(ValidCreate(date: DateTime.UtcNow.AddDays(-60)), _userId);
        var inside = await _sut.CreateAsync(ValidCreate(date: DateTime.UtcNow.AddDays(-5)), _userId);

        var list = await _sut.ListAsync(new SurveyListParams
        {
            DateFrom = DateTime.UtcNow.AddDays(-10),
            DateTo = DateTime.UtcNow,
        });
        Assert.Single(list.Items);
        Assert.Equal(inside.Id, list.Items[0].Id);
    }

    [Fact]
    public async Task ListAsync_FiltersByDriveStatusCsv()
    {
        var a = await _sut.CreateAsync(ValidCreate(), _userId);
        var b = await _sut.CreateAsync(ValidCreate(), _userId);
        var rawA = await _db.Surveys.FirstAsync(s => s.Id == a.Id);
        rawA.DriveSyncStatus = SurveyDriveSyncStatus.Synced;
        var rawB = await _db.Surveys.FirstAsync(s => s.Id == b.Id);
        rawB.DriveSyncStatus = SurveyDriveSyncStatus.Failed;
        await _db.SaveChangesAsync();

        var list = await _sut.ListAsync(new SurveyListParams { DriveSyncStatus = "Synced,Failed" });
        Assert.Equal(2, list.Total);

        var syncedOnly = await _sut.ListAsync(new SurveyListParams { DriveSyncStatus = "Synced" });
        Assert.Single(syncedOnly.Items);
        Assert.Equal(a.Id, syncedOnly.Items[0].Id);
    }

    // ---------------- NIH-100 Update / Delete ----------------

    private UpdateSurveyRequest ValidUpdate(int _1, string? location = null,
        string? typeCode = "commercial", DateTime? date = null,
        int? surveyorId = null, string? note = null) => new()
        {
            Location = location ?? "Địa điểm cập nhật",
            ConstructionTypeCode = typeCode,
            SurveyDate = date ?? DateTime.UtcNow.AddDays(-1),
            SurveyorUserId = surveyorId ?? _userId,
            Note = note,
        };

    [Fact]
    public async Task UpdateAsync_HappyPath_AppliesEveryEditableField()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var newDate = DateTime.UtcNow.AddDays(-3);
        var updated = await _sut.UpdateAsync(created.Id, ValidUpdate(
            created.Id,
            location: "Địa điểm mới",
            typeCode: "commercial",
            date: newDate,
            note: "Ghi chú"), _userId);

        Assert.NotNull(updated);
        Assert.Equal("Địa điểm mới", updated!.Location);
        Assert.Equal("commercial", updated.ConstructionTypeCode);
        Assert.Equal("Thương mại", updated.ConstructionTypeLabel);
        Assert.Equal(newDate, updated.SurveyDate, TimeSpan.FromSeconds(1));
        Assert.Equal("Ghi chú", updated.Note);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNull()
    {
        var res = await _sut.UpdateAsync(99999, ValidUpdate(0), _userId);
        Assert.Null(res);
    }

    [Fact]
    public async Task UpdateAsync_MissingLocation_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.UpdateAsync(created.Id, ValidUpdate(created.Id, location: "  "), _userId));
    }

    [Fact]
    public async Task UpdateAsync_UnknownConstructionType_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.UpdateAsync(created.Id, ValidUpdate(created.Id, typeCode: "no-such-type"), _userId));
    }

    [Fact]
    public async Task UpdateAsync_UnknownSurveyor_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.UpdateAsync(created.Id, ValidUpdate(created.Id, surveyorId: 99999), _userId));
    }

    [Fact]
    public async Task DeleteAsync_NotSynced_Succeeds()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.True(await _sut.DeleteAsync(created.Id));
        Assert.False(await _db.Surveys.AnyAsync(s => s.Id == created.Id));
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    {
        Assert.False(await _sut.DeleteAsync(99999));
    }

    [Theory]
    [InlineData(SurveyDriveSyncStatus.Syncing)]
    [InlineData(SurveyDriveSyncStatus.Synced)]
    [InlineData(SurveyDriveSyncStatus.Failed)]
    public async Task DeleteAsync_AfterDriveTouched_Throws(SurveyDriveSyncStatus status)
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var raw = await _db.Surveys.FirstAsync(s => s.Id == created.Id);
        raw.DriveSyncStatus = status;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<SurveyOperationException>(() => _sut.DeleteAsync(created.Id));
        // Row must still exist so the audit trail is preserved.
        Assert.True(await _db.Surveys.AnyAsync(s => s.Id == created.Id));
    }
}
