using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
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
        _db.SaveChanges();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var translationService = new TranslationService(_db, cache);
        var metadataService = new RecruitmentMetadataService(translationService);
        _sut = new RecruitmentController(metadataService);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetMetadata_ReturnsLocalizedRecruitmentOptions()
    {
        var result = await _sut.GetMetadata("vi");

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<NihomeBackend.Models.DTOs.Responses.RecruitmentMetadataResponse>(ok.Value);

        Assert.Contains(payload.EmploymentTypes, item => item.Value == "full-time" && item.Label == "Toàn thời gian");
        Assert.Contains(payload.ExperienceLevels, item => item.Value == "mid" && item.Label == "Trung cấp (2-5 năm)");
        Assert.Contains(payload.ApplicationStatuses, item => item.Value == "interview" && item.Label == "Phỏng vấn");
    }
}
