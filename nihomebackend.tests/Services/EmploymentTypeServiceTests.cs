using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace nihomebackend.tests.Services;

public class EmploymentTypeServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly EmploymentTypeService _sut;

    public EmploymentTypeServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new EmploymentTypeService(_db, NullLogger<EmploymentTypeService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAllAsync_SeedsDefaults_WhenTableIsEmpty()
    {
        var result = await _sut.GetAllAsync(includeInactive: true);

        Assert.Contains(result, item => item.Code == "full-time" && item.Name == "Toàn thời gian");
        Assert.Contains(result, item => item.Code == "part-time" && item.Name == "Bán thời gian");
        Assert.Contains(result, item => item.Code == "intern" && item.Name == "Thực tập sinh");
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenCodeAlreadyExistsIgnoringCase()
    {
        _db.EmploymentTypes.Add(new EmploymentType { Code = "full-time", Name = "Toàn thời gian", IsActive = true, SortOrder = 1 });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateAsync(new UpsertEmploymentTypeRequest
        {
            Code = " FULL-TIME ",
            Name = "Khác",
            IsActive = true,
            SortOrder = 2,
        }));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesLinkedJobPositions_WhenCodeChanges()
    {
        var type = new EmploymentType { Code = "full-time", Name = "Toàn thời gian", IsActive = true, SortOrder = 1 };
        _db.EmploymentTypes.Add(type);
        _db.JobPositions.Add(new JobPosition
        {
            Title = "Site Engineer",
            Department = "Engineering",
            Location = "HCM",
            EmploymentType = "full-time",
            ExperienceLevel = "mid",
            IsActive = true,
            SortOrder = 1,
        });
        await _db.SaveChangesAsync();

        var updated = await _sut.UpdateAsync(type.Id, new UpsertEmploymentTypeRequest
        {
            Code = "permanent",
            Name = "Toàn thời gian",
            IsActive = true,
            SortOrder = 1,
        });

        Assert.NotNull(updated);
        Assert.Equal("permanent", _db.JobPositions.Single().EmploymentType);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesLinkedJobPositions_WhenLegacyEmploymentTypeHasSpacesAndCase()
    {
        var type = new EmploymentType { Code = "full-time", Name = "Toàn thời gian", IsActive = true, SortOrder = 1 };
        _db.EmploymentTypes.Add(type);
        _db.JobPositions.Add(new JobPosition
        {
            Title = "Legacy",
            Department = "Engineering",
            Location = "HCM",
            EmploymentType = "  Full-Time  ",
            ExperienceLevel = "mid",
            IsActive = true,
            SortOrder = 1,
        });
        await _db.SaveChangesAsync();

        var updated = await _sut.UpdateAsync(type.Id, new UpsertEmploymentTypeRequest
        {
            Code = "permanent",
            Name = "Toàn thời gian",
            IsActive = true,
            SortOrder = 1,
        });

        Assert.NotNull(updated);
        Assert.Equal("permanent", _db.JobPositions.Single().EmploymentType);
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenEmploymentTypeInUse()
    {
        var type = new EmploymentType { Code = "intern", Name = "Thực tập sinh", IsActive = true, SortOrder = 1 };
        _db.EmploymentTypes.Add(type);
        _db.JobPositions.Add(new JobPosition
        {
            Title = "Intern",
            Department = "Engineering",
            Location = "HCM",
            EmploymentType = "intern",
            ExperienceLevel = "student",
            IsActive = true,
            SortOrder = 1,
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteAsync(type.Id));
        Assert.Equal("Hình thức làm việc đang được sử dụng trong vị trí tuyển dụng, không thể xóa.", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenLegacyEmploymentTypeHasSpacesAndCase()
    {
        var type = new EmploymentType { Code = "intern", Name = "Thực tập sinh", IsActive = true, SortOrder = 1 };
        _db.EmploymentTypes.Add(type);
        _db.JobPositions.Add(new JobPosition
        {
            Title = "Legacy intern",
            Department = "Engineering",
            Location = "HCM",
            EmploymentType = "  Intern  ",
            ExperienceLevel = "student",
            IsActive = true,
            SortOrder = 1,
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteAsync(type.Id));
        Assert.Equal("Hình thức làm việc đang được sử dụng trong vị trí tuyển dụng, không thể xóa.", ex.Message);
    }
}
