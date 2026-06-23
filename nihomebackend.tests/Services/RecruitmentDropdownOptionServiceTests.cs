using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace nihomebackend.tests.Services;

public class RecruitmentDropdownOptionServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly RecruitmentDropdownOptionService _sut;

    public RecruitmentDropdownOptionServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new RecruitmentDropdownOptionService(_db, NullLogger<RecruitmentDropdownOptionService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetByTypeAsync_SeedsExperienceLevelDefaults_WhenTableIsEmpty()
    {
        var result = await _sut.GetByTypeAsync(RecruitmentDropdownOptionService.TypeExperienceLevel, includeInactive: true);

        Assert.Contains(result, x => x.Code == "student");
        Assert.Contains(result, x => x.Code == "junior");
        Assert.Contains(result, x => x.Code == "mid");
        Assert.Contains(result, x => x.Code == "senior");
    }

    [Fact]
    public async Task GetByTypeAsync_SeedsBenefitDefaults_WhenTableIsEmpty()
    {
        var result = await _sut.GetByTypeAsync(RecruitmentDropdownOptionService.TypeBenefit, includeInactive: true);

        Assert.Contains(result, x => x.Code == "health-insurance");
        Assert.Contains(result, x => x.Code == "training");
        Assert.Contains(result, x => x.Code == "friendly-culture");
        Assert.Contains(result, x => x.Code == "project-bonus");
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenCodeAlreadyExistsInSameType()
    {
        _db.RecruitmentDropdownOptions.Add(new RecruitmentDropdownOption
        {
            Type = "experience-level",
            Code = "mid",
            Name = "1-3 years",
            IsActive = true,
            SortOrder = 1,
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateAsync(new UpsertRecruitmentDropdownOptionRequest
        {
            Type = "experience-level",
            Code = " MID ",
            Name = "Duplicate",
            IsActive = true,
            SortOrder = 2,
        }));
    }

    [Fact]
    public async Task CreateAsync_AllowsSameCode_WhenDifferentType()
    {
        _db.RecruitmentDropdownOptions.Add(new RecruitmentDropdownOption
        {
            Type = "experience-level",
            Code = "mid",
            Name = "Mid level",
            IsActive = true,
            SortOrder = 1,
        });
        await _db.SaveChangesAsync();

        var result = await _sut.CreateAsync(new UpsertRecruitmentDropdownOptionRequest
        {
            Type = "benefit",
            Code = "mid",
            Name = "Mid something",
            IsActive = true,
            SortOrder = 1,
        });

        Assert.Equal("mid", result.Code);
        Assert.Equal("benefit", result.Type);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenIdDoesNotExist()
    {
        var result = await _sut.UpdateAsync(9999, new UpsertRecruitmentDropdownOptionRequest
        {
            Type = "experience-level",
            Code = "senior",
            Name = "Updated",
            IsActive = true,
            SortOrder = 1,
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_Succeeds_WhenEntityExists()
    {
        var entity = new RecruitmentDropdownOption
        {
            Type = "benefit",
            Code = "health-insurance",
            Name = "Health",
            IsActive = true,
            SortOrder = 1,
        };
        _db.RecruitmentDropdownOptions.Add(entity);
        await _db.SaveChangesAsync();

        var result = await _sut.UpdateAsync(entity.Id, new UpsertRecruitmentDropdownOptionRequest
        {
            Type = "benefit",
            Code = "health-insurance",
            Name = "Full health insurance",
            IsActive = true,
            SortOrder = 1,
        });

        Assert.NotNull(result);
        Assert.Equal("Full health insurance", result.Name);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenIdDoesNotExist()
    {
        var result = await _sut.DeleteAsync(9999);
        Assert.False(result);
    }
}
