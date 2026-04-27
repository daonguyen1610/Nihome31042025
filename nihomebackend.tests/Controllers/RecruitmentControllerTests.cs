using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using NihomeBackend.Constants;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Controllers;

public class RecruitmentControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly RecruitmentController _sut;

    public RecruitmentControllerTests()
    {
        _db = DbContextFactory.Create();
        _db.Translations.AddRange(
            new Translation { Key = "recruit.meta.employment.fullTime", LanguageCode = "vi", Value = "Toàn thời gian" },
            new Translation { Key = "recruit.meta.experience.mid", LanguageCode = "vi", Value = "Trung cấp (2-5 năm)" },
            new Translation { Key = "recruit.meta.status.interview", LanguageCode = "vi", Value = "Phỏng vấn" });
        _db.RecruitmentMetadataItems.AddRange(
            new RecruitmentMetadataItem
            {
                GroupKey = RecruitmentMetadataGroups.EmploymentType,
                Value = "full-time",
                Label = "Toàn thời gian",
                TranslationKey = "recruit.meta.employment.fullTime",
                IsActive = true,
                SortOrder = 1,
            },
            new RecruitmentMetadataItem
            {
                GroupKey = RecruitmentMetadataGroups.ExperienceLevel,
                Value = "mid",
                Label = "Trung cấp (2-5 năm)",
                TranslationKey = "recruit.meta.experience.mid",
                IsActive = true,
                SortOrder = 1,
            },
            new RecruitmentMetadataItem
            {
                GroupKey = RecruitmentMetadataGroups.ApplicationStatus,
                Value = "interview",
                Label = "Phỏng vấn",
                TranslationKey = "recruit.meta.status.interview",
                IsActive = true,
                SortOrder = 1,
            });
        _db.SaveChanges();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var translationService = new TranslationService(_db, cache);
        var metadataService = new RecruitmentMetadataService(_db, translationService);
        _sut = new RecruitmentController(metadataService);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetMetadata_ReturnsLocalizedRecruitmentOptions()
    {
        var result = await _sut.GetMetadata("vi");

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<RecruitmentMetadataResponse>(ok.Value);

        Assert.Contains(payload.EmploymentTypes, item => item.Value == "full-time" && item.Label == "Toàn thời gian");
        Assert.Contains(payload.ExperienceLevels, item => item.Value == "mid" && item.Label == "Trung cấp (2-5 năm)");
        Assert.Contains(payload.ApplicationStatuses, item => item.Value == "interview" && item.Label == "Phỏng vấn");
    }

    [Fact]
    public async Task CreateMetadataItem_ReturnsCreated_WhenValid()
    {
        var result = await _sut.CreateMetadataItem(new UpsertRecruitmentMetadataItemRequest
        {
            GroupKey = RecruitmentMetadataGroups.EmploymentType,
            Value = "contract",
            Label = "Hợp đồng",
            IsActive = true,
            SortOrder = 9,
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var payload = Assert.IsType<RecruitmentMetadataItemResponse>(created.Value);
        Assert.Equal("contract", payload.Value);
        Assert.Equal("Hợp đồng", payload.Label);
    }

    [Fact]
    public async Task DeleteMetadataItem_ReturnsBadRequest_WhenInUse()
    {
        var item = new RecruitmentMetadataItem
        {
            GroupKey = RecruitmentMetadataGroups.ApplicationStatus,
            Value = "new",
            Label = "Mới",
            IsActive = true,
            SortOrder = 2,
        };
        _db.RecruitmentMetadataItems.Add(item);

        var position = new JobPosition
        {
            Title = "Dev",
            Department = "Eng",
            Location = "HCM",
            EmploymentType = "full-time",
            ExperienceLevel = "mid",
            IsActive = true,
            SortOrder = 1,
        };
        _db.JobPositions.Add(position);
        await _db.SaveChangesAsync();

        _db.JobApplications.Add(new JobApplication
        {
            JobPositionId = position.Id,
            CandidateName = "A",
            Email = "a@test.com",
            Status = "new",
        });
        await _db.SaveChangesAsync();

        var result = await _sut.DeleteMetadataItem(item.Id);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
