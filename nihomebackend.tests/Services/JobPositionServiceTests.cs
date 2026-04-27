using Microsoft.Extensions.Caching.Memory;
using NihomeBackend.Constants;
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
        SeedRecruitmentMetadata();
        var translationService = new TranslationService(_db, new MemoryCache(new MemoryCacheOptions()));
        var metadataService = new RecruitmentMetadataService(_db, translationService);
        _sut = new JobPositionService(_db, metadataService);
    }

    public void Dispose() => _db.Dispose();

    private void SeedRecruitmentMetadata()
    {
        _db.RecruitmentMetadataItems.AddRange(
            new RecruitmentMetadataItem
            {
                GroupKey = RecruitmentMetadataGroups.EmploymentType,
                Value = "full-time",
                Label = "Toàn thời gian",
                IsActive = true,
                SortOrder = 1,
            },
            new RecruitmentMetadataItem
            {
                GroupKey = RecruitmentMetadataGroups.ExperienceLevel,
                Value = "mid",
                Label = "Trung cấp",
                IsActive = true,
                SortOrder = 1,
            });
        _db.SaveChanges();
    }

    [Fact]
    public async Task CreateAsync_CreatesPosition_WhenMetadataIsValid()
    {
        var result = await _sut.CreateAsync(new UpsertJobPositionRequest
        {
            Title = "Dev",
            Department = "Eng",
            Location = "HCM",
            EmploymentType = "full-time",
            ExperienceLevel = "mid",
            Requirements = [],
            IsActive = true,
            SortOrder = 1,
        });

        Assert.Equal("full-time", result.EmploymentType);
        Assert.Equal("mid", result.ExperienceLevel);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenEmploymentTypeMetadataIsInvalid()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateAsync(new UpsertJobPositionRequest
        {
            Title = "Dev",
            Department = "Eng",
            Location = "HCM",
            EmploymentType = "contract",
            ExperienceLevel = "mid",
            Requirements = [],
            IsActive = true,
            SortOrder = 1,
        }));
    }
}
