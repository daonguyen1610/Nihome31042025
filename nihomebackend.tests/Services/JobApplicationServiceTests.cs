using Microsoft.Extensions.Logging;
using Moq;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class JobApplicationServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly JobApplicationService _sut;

    public JobApplicationServiceTests()
    {
        _db = DbContextFactory.Create();
        SeedRecruitmentMetadata();
        _emailServiceMock = new Mock<IEmailService>();
        var translationService = new TranslationService(_db, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        var recruitmentMetadataService = new RecruitmentMetadataService(_db, translationService);
        _sut = new JobApplicationService(
            _db,
            recruitmentMetadataService,
            _emailServiceMock.Object,
            Mock.Of<ILogger<JobApplicationService>>());
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

    private async Task<JobPosition> SeedActivePosition(string title = "Backend Dev")
    {
        var position = new JobPosition
        {
            Title = title,
            Department = "Engineering",
            Location = "HCM",
            EmploymentType = "full-time",
            ExperienceLevel = "mid",
            IsActive = true,
            SortOrder = 0
        };
        _db.JobPositions.Add(position);
        await _db.SaveChangesAsync();
        return position;
    }

    private void SeedSiteSettings(string? notificationEmail = "hr@nihome.vn")
    {
        _db.SiteSettings.Add(new SiteSettings
        {
            SiteName = "Nihome",
            PrimaryEmail = "nihome@nihome.vn",
            NotificationEmail = notificationEmail,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();
    }

    // ── GetAllAsync ──

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenNoApplications()
    {
        var result = await _sut.GetAllAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAll_WhenNoFilters()
    {
        var pos = await SeedActivePosition();
        _db.JobApplications.AddRange(
            new JobApplication { JobPositionId = pos.Id, CandidateName = "A", Email = "a@t.com", Status = "new" },
            new JobApplication { JobPositionId = pos.Id, CandidateName = "B", Email = "b@t.com", Status = "interview" }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByPositionId()
    {
        var pos1 = await SeedActivePosition("Pos1");
        var pos2 = await SeedActivePosition("Pos2");
        _db.JobApplications.AddRange(
            new JobApplication { JobPositionId = pos1.Id, CandidateName = "A", Email = "a@t.com" },
            new JobApplication { JobPositionId = pos2.Id, CandidateName = "B", Email = "b@t.com" }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync(positionId: pos1.Id);

        Assert.Single(result);
        Assert.Equal("A", result[0].CandidateName);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByStatus()
    {
        var pos = await SeedActivePosition();
        _db.JobApplications.AddRange(
            new JobApplication { JobPositionId = pos.Id, CandidateName = "A", Email = "a@t.com", Status = "new" },
            new JobApplication { JobPositionId = pos.Id, CandidateName = "B", Email = "b@t.com", Status = "hired" }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync(status: "hired");

        Assert.Single(result);
        Assert.Equal("B", result[0].CandidateName);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsCvUrl()
    {
        var pos = await SeedActivePosition();
        _db.JobApplications.Add(new JobApplication
        {
            JobPositionId = pos.Id,
            CandidateName = "A",
            Email = "a@t.com",
            CvUrl = "/files/cv/test.pdf"
        });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync();

        Assert.Equal("/files/cv/test.pdf", result[0].CvUrl);
    }

    // ── SubmitAsync ──

    [Fact]
    public async Task SubmitAsync_CreatesApplication_WithAllFields()
    {
        var pos = await SeedActivePosition();
        SeedSiteSettings(notificationEmail: null); // no email → skip notification

        var req = new SubmitJobApplicationRequest
        {
            JobPositionId = pos.Id,
            CandidateName = "  Nguyễn Văn A  ",
            Email = "  A@TEST.COM  ",
            Phone = " 0901234567 ",
            ExperienceYears = 3,
            CoverLetter = " Great dev ",
            CvUrl = "/files/cv/abc.pdf"
        };

        var result = await _sut.SubmitAsync(req);

        Assert.Equal("Nguyễn Văn A", result.CandidateName);
        Assert.Equal("a@test.com", result.Email); // lowercased + trimmed
        Assert.Equal("0901234567", result.Phone);
        Assert.Equal(3, result.ExperienceYears);
        Assert.Equal("Great dev", result.CoverLetter);
        Assert.Equal("/files/cv/abc.pdf", result.CvUrl);
        Assert.Equal("new", result.Status);
        Assert.Equal(pos.Title, result.PositionTitle);
    }

    [Fact]
    public async Task SubmitAsync_ThrowsWhenPositionNotFound()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.SubmitAsync(new SubmitJobApplicationRequest
            {
                JobPositionId = 999,
                CandidateName = "X",
                Email = "x@x.com"
            }));

        Assert.Contains("không tồn tại", ex.Message);
    }

    [Fact]
    public async Task SubmitAsync_ThrowsWhenPositionInactive()
    {
        var pos = new JobPosition
        {
            Title = "Closed",
            Department = "HR",
            Location = "HN",
            EmploymentType = "full-time",
            ExperienceLevel = "junior",
            IsActive = false,
            SortOrder = 0
        };
        _db.JobPositions.Add(pos);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.SubmitAsync(new SubmitJobApplicationRequest
            {
                JobPositionId = pos.Id,
                CandidateName = "X",
                Email = "x@x.com"
            }));
    }

    [Fact]
    public async Task SubmitAsync_SendsNotificationEmail_WhenConfigured()
    {
        var pos = await SeedActivePosition();
        SeedSiteSettings("hr@nihome.vn");

        await _sut.SubmitAsync(new SubmitJobApplicationRequest
        {
            JobPositionId = pos.Id,
            CandidateName = "Vy",
            Email = "vy@test.com"
        });

        _emailServiceMock.Verify(
            e => e.SendEmailAsync("hr@nihome.vn", It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_SkipsEmail_WhenNoNotificationEmail()
    {
        var pos = await SeedActivePosition();
        // SiteSettings with all emails null
        _db.SiteSettings.Add(new SiteSettings
        {
            SiteName = "Test",
            NotificationEmail = null,
            PrimaryEmail = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _sut.SubmitAsync(new SubmitJobApplicationRequest
        {
            JobPositionId = pos.Id,
            CandidateName = "X",
            Email = "x@x.com"
        });

        _emailServiceMock.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);

        // Application still created successfully
        Assert.NotEqual(0, result.Id);
    }

    [Fact]
    public async Task SubmitAsync_StillSucceeds_WhenEmailSendFails()
    {
        var pos = await SeedActivePosition();
        SeedSiteSettings("hr@nihome.vn");

        _emailServiceMock
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        var result = await _sut.SubmitAsync(new SubmitJobApplicationRequest
        {
            JobPositionId = pos.Id,
            CandidateName = "Y",
            Email = "y@y.com"
        });

        // Application still created despite email failure
        Assert.NotEqual(0, result.Id);
        Assert.Equal("Y", result.CandidateName);
    }

    [Fact]
    public async Task SubmitAsync_FallsToPrimaryEmail_WhenNotificationEmailNull()
    {
        var pos = await SeedActivePosition();
        _db.SiteSettings.Add(new SiteSettings
        {
            SiteName = "Test",
            NotificationEmail = null,
            PrimaryEmail = "primary@nihome.vn",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await _sut.SubmitAsync(new SubmitJobApplicationRequest
        {
            JobPositionId = pos.Id,
            CandidateName = "Z",
            Email = "z@z.com"
        });

        _emailServiceMock.Verify(
            e => e.SendEmailAsync("primary@nihome.vn", It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    // ── UpdateStatusAsync ──

    [Fact]
    public async Task UpdateStatusAsync_UpdatesStatus()
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

        var result = await _sut.UpdateStatusAsync(app.Id, "interview");

        Assert.NotNull(result);
        Assert.Equal("interview", result!.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.UpdateStatusAsync(999, "hired");
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateStatusAsync_ThrowsForInvalidStatus()
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

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync(app.Id, "invalid_status"));

        Assert.Contains("không hợp lệ", ex.Message);
    }

    // ── DeleteAsync ──

    [Fact]
    public async Task DeleteAsync_RemovesApplication()
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

        var result = await _sut.DeleteAsync(app.Id);

        Assert.True(result);
        Assert.Empty(_db.JobApplications);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
    {
        var result = await _sut.DeleteAsync(999);
        Assert.False(result);
    }
}
