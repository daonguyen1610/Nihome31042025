using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;
using NihomeBackend.Services.GoogleDrive;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class SurveyDriveSyncProcessorTests : IDisposable
{
    private readonly NihomeBackend.Data.AppDbContext db = DbContextFactory.Create();

    [Fact]
    public void IsDueForClaim_ExpiredThirdProcessingAttempt_IsRecoverableWithoutAllowingFourthPendingAttempt()
    {
        var now = DateTime.UtcNow;
        var isDue = SurveyDriveSyncProcessor.IsDueForClaim(now).Compile();

        Assert.True(isDue(new SurveyMedia
        {
            SyncStatus = SurveyMediaSyncStatus.Processing,
            SyncAttemptCount = SurveyMediaService.MaxSyncAttempts,
            ClaimExpiresAt = now.AddSeconds(-1),
        }));
        Assert.False(isDue(new SurveyMedia
        {
            SyncStatus = SurveyMediaSyncStatus.Processing,
            SyncAttemptCount = SurveyMediaService.MaxSyncAttempts,
            ClaimExpiresAt = now.AddSeconds(1),
        }));
        Assert.False(isDue(new SurveyMedia
        {
            SyncStatus = SurveyMediaSyncStatus.Pending,
            SyncAttemptCount = SurveyMediaService.MaxSyncAttempts,
            NextSyncAttemptAt = now.AddSeconds(-1),
        }));
    }

    [Theory]
    [InlineData("ReadOnly")]
    [InlineData("InvalidRoot")]
    [InlineData("Unavailable")]
    public async Task ProcessNextAsync_NonConnectedDrive_DoesNotClaimOrUpload(string status)
    {
        var survey = new Survey
        {
            Code = $"SV-{Guid.NewGuid():N}",
            Location = "Drive gate test",
            SurveyDate = DateTime.UtcNow,
        };
        db.Surveys.Add(survey);
        await db.SaveChangesAsync();
        var media = new SurveyMedia
        {
            SurveyId = survey.Id,
            OriginalFileName = "private.jpg",
            StoredFileName = "stored.jpg",
            ContentType = "image/jpeg",
            Extension = ".jpg",
            Size = 10,
            RelativePath = $"/files/survey-media/{survey.Id}/stored.jpg",
        };
        db.SurveyMedia.Add(media);
        await db.SaveChangesAsync();

        var storage = new Mock<ISurveyMediaStorageService>();
        var drive = new Mock<IGoogleDriveAdapter>();
        var mediaService = new Mock<ISurveyMediaService>();
        mediaService.Setup(service => service.GetDriveConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SurveyDriveConnectionStatusResponse { Status = status });
        var processor = new SurveyDriveSyncProcessor(
            db,
            storage.Object,
            drive.Object,
            mediaService.Object,
            new GoogleDriveOptions(),
            Mock.Of<IAuditLogger>(),
            NullLogger<SurveyDriveSyncProcessor>.Instance);

        var processed = await processor.ProcessNextAsync();

        Assert.False(processed);
        Assert.Equal(SurveyMediaSyncStatus.Pending, media.SyncStatus);
        Assert.Equal(0, media.SyncAttemptCount);
        drive.VerifyNoOtherCalls();
        storage.VerifyNoOtherCalls();
    }

    public void Dispose() => db.Dispose();
}