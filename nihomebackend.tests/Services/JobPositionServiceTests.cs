using Microsoft.Extensions.Caching.Memory;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class JobPositionServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly JobPositionService _sut;

    public JobPositionServiceTests()
    {
        _db = DbContextFactory.Create();
        var employmentTypeService = new EmploymentTypeService(_db);
        var translationSvc = new EntityTranslationService(_db, Mock.Of<IMemoryCache>());
        _sut = new JobPositionService(_db, employmentTypeService, translationSvc);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateAsync_Throws_WhenEmploymentTypeDoesNotExist()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateAsync(new UpsertJobPositionRequest
        {
            Title = "Site Engineer",
            Department = "Construction",
            Location = "HCM",
            EmploymentType = "not-exist",
            ExperienceLevel = "mid",
            Requirements = [],
            IsActive = true,
            SortOrder = 1,
        }));

        Assert.Equal("Hình thức làm việc không hợp lệ.", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_Succeeds_WhenEmploymentTypeExists()
    {
        _db.EmploymentTypes.Add(new EmploymentType { Code = "full-time", Name = "Toàn thời gian", IsActive = true, SortOrder = 1 });
        await _db.SaveChangesAsync();

        var result = await _sut.CreateAsync(new UpsertJobPositionRequest
        {
            Title = "Site Engineer",
            Department = "Construction",
            Location = "HCM",
            EmploymentType = "full-time",
            ExperienceLevel = "mid",
            Requirements = ["A"],
            IsActive = true,
            SortOrder = 1,
        });

        Assert.Equal("full-time", result.EmploymentType);
    }
}
