using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NihomeBackend.Constants;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Controllers;

public class JobApplicationsControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly JobApplicationsController _sut;

    public JobApplicationsControllerTests()
    {
        _db = DbContextFactory.Create();
        SeedRecruitmentMetadata();
        _emailServiceMock = new Mock<IEmailService>();
        var translationService = new TranslationService(_db, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        var recruitmentMetadataService = new RecruitmentMetadataService(_db, translationService);
        var svc = new JobApplicationService(
            _db,
            recruitmentMetadataService,
            _emailServiceMock.Object,
            Mock.Of<ILogger<JobApplicationService>>());
        _sut = new JobApplicationsController(svc);
    }

    public void Dispose() => _db.Dispose();

    private void SeedRecruitmentMetadata()
    {
        _db.RecruitmentMetadataItems.AddRange(
            new RecruitmentMetadataItem
            {
                GroupKey = RecruitmentMetadataGroups.ApplicationStatus,
                Value = "new",
                Label = "Mới",
                IsActive = true,
                SortOrder = 1,
            },
            new RecruitmentMetadataItem
            {
                GroupKey = RecruitmentMetadataGroups.ApplicationStatus,
                Value = "interview",
                Label = "Phỏng vấn",
                IsActive = true,
                SortOrder = 2,
            },
            new RecruitmentMetadataItem
            {
                GroupKey = RecruitmentMetadataGroups.ApplicationStatus,
                Value = "hired",
                Label = "Đã tuyển",
                IsActive = true,
                SortOrder = 3,
            },
            new RecruitmentMetadataItem
            {
                GroupKey = RecruitmentMetadataGroups.ApplicationStatus,
                Value = "rejected",
                Label = "Từ chối",
                IsActive = true,
                SortOrder = 4,
            });
        _db.SaveChanges();
    }

    private async Task<JobPosition> SeedActivePosition()
    {
        var pos = new JobPosition
        {
            Title = "Dev",
            Department = "Eng",
            Location = "HCM",
            EmploymentType = "full-time",
            ExperienceLevel = "mid",
            IsActive = true,
            SortOrder = 0
        };
        _db.JobPositions.Add(pos);
        await _db.SaveChangesAsync();
        return pos;
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var result = await _sut.GetAll(null, null);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task Submit_Returns201_WhenValid()
    {
        var pos = await SeedActivePosition();

        var result = await _sut.Submit(new SubmitJobApplicationRequest
        {
            JobPositionId = pos.Id,
            CandidateName = "Test",
            Email = "test@test.com"
        });

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, obj.StatusCode);
    }

    [Fact]
    public async Task Submit_ReturnsBadRequest_WhenPositionInvalid()
    {
        var result = await _sut.Submit(new SubmitJobApplicationRequest
        {
            JobPositionId = 999,
            CandidateName = "X",
            Email = "x@x.com"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Submit_IncludesCvUrl_InResponse()
    {
        var pos = await SeedActivePosition();

        var result = await _sut.Submit(new SubmitJobApplicationRequest
        {
            JobPositionId = pos.Id,
            CandidateName = "A",
            Email = "a@a.com",
            CvUrl = "/files/cv/test.pdf"
        });

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, obj.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsOk_WhenValid()
    {
        var pos = await SeedActivePosition();
        var app = new JobApplication
        {
            JobPositionId = pos.Id,
            CandidateName = "A",
            Email = "a@t.com",
            Status = "new"
        };
        _db.JobApplications.Add(app);
        await _db.SaveChangesAsync();

        var result = await _sut.UpdateStatus(app.Id, new UpdateApplicationStatusRequest { Status = "interview" });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsNotFound_WhenMissing()
    {
        var result = await _sut.UpdateStatus(999, new UpdateApplicationStatusRequest { Status = "hired" });
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsBadRequest_WhenInvalidStatus()
    {
        var pos = await SeedActivePosition();
        var app = new JobApplication
        {
            JobPositionId = pos.Id,
            CandidateName = "A",
            Email = "a@t.com",
            Status = "new"
        };
        _db.JobApplications.Add(app);
        await _db.SaveChangesAsync();

        var result = await _sut.UpdateStatus(app.Id, new UpdateApplicationStatusRequest { Status = "bogus" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenFound()
    {
        var pos = await SeedActivePosition();
        var app = new JobApplication
        {
            JobPositionId = pos.Id,
            CandidateName = "A",
            Email = "a@t.com"
        };
        _db.JobApplications.Add(app);
        await _db.SaveChangesAsync();

        var result = await _sut.Delete(app.Id);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        var result = await _sut.Delete(999);
        Assert.IsType<NotFoundResult>(result);
    }
}
