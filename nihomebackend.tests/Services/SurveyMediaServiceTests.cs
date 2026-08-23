using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using NihomeBackend.Services.GoogleDrive;
using nihomebackend.tests.Helpers;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace nihomebackend.tests.Services;

public sealed class SurveyMediaServiceTests : IDisposable
{
    private readonly NihomeBackend.Data.AppDbContext db = DbContextFactory.Create();
    private readonly Mock<ISurveyMediaStorageService> storage = new();
    private readonly Mock<IGoogleDriveAdapter> drive = new();
    private readonly MemoryCache cache = new(new MemoryCacheOptions());
    private readonly SurveyMediaService service;

    public SurveyMediaServiceTests()
    {
        service = new SurveyMediaService(
            db, storage.Object, drive.Object, new TranslationService(db, cache));
    }

    [Fact]
    public async Task AddAsync_TotalWouldExceed2GiB_RejectsWithoutWritingFile()
    {
        var survey = await AddSurveyAsync();
        db.SurveyMedia.Add(Media(survey.Id, size: SurveyMediaStorageService.MaxSurveySize - 5));
        await db.SaveChangesAsync();
        var file = new FormFile(Stream.Null, 0, 10, "file", "photo.jpg");

        var exception = await Assert.ThrowsAsync<SurveyMediaValidationException>(() =>
            service.AddAsync(survey.Id, new SurveyMediaUploadRequest { File = file }, 7));

        Assert.Contains("2 GiB", exception.Message);
        storage.Verify(store => store.StoreAsync(It.IsAny<int>(), It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetryAsync_ThirdAttempt_RemainsTerminal()
    {
        var survey = await AddSurveyAsync();
        var media = Media(survey.Id);
        media.SyncStatus = SurveyMediaSyncStatus.Failed;
        media.SyncAttemptCount = 3;
        db.SurveyMedia.Add(media);
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<SurveyMediaValidationException>(() =>
            service.RetryAsync(survey.Id, media.Id, 7));

        Assert.Contains("3 lần", exception.Message);
        Assert.Equal(SurveyMediaSyncStatus.Failed, media.SyncStatus);
    }

    [Fact]
    public async Task RetryAsync_ProcessingMedia_PreservesActiveClaim()
    {
        var survey = await AddSurveyAsync();
        var claimToken = Guid.NewGuid();
        var claimExpiresAt = DateTime.UtcNow.AddMinutes(15);
        var media = Media(survey.Id);
        media.SyncStatus = SurveyMediaSyncStatus.Processing;
        media.SyncAttemptCount = 1;
        media.ClaimToken = claimToken;
        media.ClaimExpiresAt = claimExpiresAt;
        db.SurveyMedia.Add(media);
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<SurveyMediaValidationException>(() =>
            service.RetryAsync(survey.Id, media.Id, 7));

        Assert.Contains("đang được đồng bộ", exception.Message);
        Assert.Equal(SurveyMediaSyncStatus.Processing, media.SyncStatus);
        Assert.Equal(1, media.SyncAttemptCount);
        Assert.Equal(claimToken, media.ClaimToken);
        Assert.Equal(claimExpiresAt, media.ClaimExpiresAt);
    }

    [Fact]
    public async Task RecalculateAggregateAsync_NoMedia_PreservesLegacyStatus()
    {
        var survey = await AddSurveyAsync(SurveyDriveSyncStatus.Synced);

        await service.RecalculateAggregateAsync(survey.Id);
        await db.SaveChangesAsync();

        Assert.Equal(SurveyDriveSyncStatus.Synced, survey.DriveSyncStatus);
    }

    [Fact]
    public async Task RecalculateAggregateAsync_FailedMedia_SetsAggregateFailure()
    {
        var survey = await AddSurveyAsync();
        var media = Media(survey.Id);
        media.SyncStatus = SurveyMediaSyncStatus.Failed;
        media.SyncError = "Drive unavailable";
        db.SurveyMedia.Add(media);
        await db.SaveChangesAsync();

        await service.RecalculateAggregateAsync(survey.Id);
        await db.SaveChangesAsync();

        Assert.Equal(SurveyDriveSyncStatus.Failed, survey.DriveSyncStatus);
        Assert.Equal("Drive unavailable", survey.DriveSyncError);
    }

    [Fact]
    public async Task UpdateChecklistAsync_WrongSurvey_IsNotFoundAndUnchanged()
    {
        var first = await AddSurveyAsync();
        var second = await AddSurveyAsync();
        var result = new SurveyChecklistResult
        {
            SurveyId = first.Id,
            TemplateCode = "geology",
            TemplateTitle = "Địa chất",
        };
        db.SurveyChecklistResults.Add(result);
        await db.SaveChangesAsync();

        var response = await service.UpdateChecklistAsync(second.Id, result.Id,
            new UpdateSurveyChecklistResultRequest { Status = SurveyChecklistStatus.Ok }, 7);

        Assert.Null(response);
        Assert.Null(result.Status);
    }

    [Fact]
    public async Task DeleteAsync_RemoteDeleteFailure_RetainsSyncedRecord()
    {
        var survey = await AddSurveyAsync();
        var media = Media(survey.Id);
        media.SyncStatus = SurveyMediaSyncStatus.Synced;
        media.DriveFileId = "drive-file";
        db.SurveyMedia.Add(media);
        await db.SaveChangesAsync();
        drive.Setup(adapter => adapter.DeleteAsync("drive-file", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Drive down"));

        await Assert.ThrowsAsync<SurveyMediaValidationException>(() => service.DeleteAsync(survey.Id, media.Id));

        Assert.True(await db.SurveyMedia.AnyAsync(item => item.Id == media.Id));
        storage.Verify(store => store.Delete(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_LocalDeleteFailure_RetainsRecordAndReportsActionableError()
    {
        var survey = await AddSurveyAsync();
        var media = Media(survey.Id);
        db.SurveyMedia.Add(media);
        await db.SaveChangesAsync();
        storage.Setup(store => store.Delete(survey.Id, media.RelativePath))
            .Throws(new UnauthorizedAccessException("private path details"));

        var exception = await Assert.ThrowsAsync<SurveyMediaValidationException>(() =>
            service.DeleteAsync(survey.Id, media.Id));

        Assert.Contains("vùng lưu trữ riêng tư", exception.Message);
        Assert.DoesNotContain("private path details", exception.Message);
        Assert.True(await db.SurveyMedia.AnyAsync(item => item.Id == media.Id));
    }

    [Fact]
    public async Task DeleteAsync_ProcessingMedia_PreservesActiveClaimAndFiles()
    {
        var survey = await AddSurveyAsync();
        var claimToken = Guid.NewGuid();
        var claimExpiresAt = DateTime.UtcNow.AddMinutes(15);
        var media = Media(survey.Id);
        media.SyncStatus = SurveyMediaSyncStatus.Processing;
        media.SyncAttemptCount = 1;
        media.ClaimToken = claimToken;
        media.ClaimExpiresAt = claimExpiresAt;
        db.SurveyMedia.Add(media);
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<SurveyMediaConflictException>(() =>
            service.DeleteAsync(survey.Id, media.Id));

        Assert.Contains("đang được đồng bộ", exception.Message);
        Assert.True(await db.SurveyMedia.AnyAsync(item => item.Id == media.Id));
        Assert.Equal(SurveyMediaSyncStatus.Processing, media.SyncStatus);
        Assert.Equal(claimToken, media.ClaimToken);
        Assert.Equal(claimExpiresAt, media.ClaimExpiresAt);
        drive.VerifyNoOtherCalls();
        storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExportPdfAsync_MoreThanOnePage_PreservesFinalMediaRow()
    {
        var survey = await AddSurveyAsync();
        for (var index = 1; index <= 60; index++)
        {
            var media = Media(survey.Id);
            media.OriginalFileName = $"photo-{index:00}.jpg";
            db.SurveyMedia.Add(media);
        }
        await db.SaveChangesAsync();

        var pdf = await service.ExportPdfAsync(survey.Id, "en");
        var pages = ExtractPdfPages(pdf!);

        Assert.Equal(2, pages.Count);
        Assert.Contains("photo-60.jpg", pages[^1]);
    }

    [Fact]
    public async Task ExportPdfAsync_UnknownSurvey_ReturnsNull()
    {
        var pdf = await service.ExportPdfAsync(9999999, "en");

        Assert.Null(pdf);
    }

    [Theory]
    [InlineData("vi", "BÁO CÁO KHẢO SÁT", "Địa chất", "Cần chú ý")]
    [InlineData("en", "SURVEY REPORT", "Geology", "Needs attention")]
    [InlineData("zh", "勘察报告", "地质", "需要注意")]
    [InlineData("ja", "調査報告書", "地質", "要注意")]
    public async Task ExportPdfAsync_LocalizesAndPreservesUnicode(
        string language, string title, string checklistTitle, string checklistStatus)
    {
        var survey = await AddSurveyAsync();
        db.SurveyChecklistResults.Add(new SurveyChecklistResult
        {
            SurveyId = survey.Id,
            TemplateCode = "geology",
            TemplateTitle = "Địa chất",
            Status = SurveyChecklistStatus.NeedsAttention,
            Note = "Cao độ cần kiểm tra",
        });
        AddTranslation("surveys.pdf.title", language, title);
        AddTranslation("masterData.survey_checklist_default.geology.label", language, checklistTitle);
        AddTranslation("surveys.checklist.status.NeedsAttention", language, checklistStatus);
        await db.SaveChangesAsync();

        var pdf = await service.ExportPdfAsync(survey.Id, language);
        var document = string.Join('\n', ExtractPdfPages(pdf!));

        Assert.Contains(title, document);
        Assert.Contains(checklistTitle, document);
        Assert.Contains(checklistStatus, document);
        Assert.Contains("Cao độ cần kiểm tra", document);
    }

    [Theory]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("en-US")]
    public async Task ExportPdfAsync_UnsupportedLanguage_IsRejected(string language)
    {
        var survey = await AddSurveyAsync();

        var exception = await Assert.ThrowsAsync<SurveyMediaValidationException>(() =>
            service.ExportPdfAsync(survey.Id, language));

        Assert.Contains("vi, en, zh hoặc ja", exception.Message);
    }

    [Fact]
    public async Task ExportPdfAsync_LanguageIsCaseInsensitiveAndTrimmed()
    {
        var survey = await AddSurveyAsync();
        AddTranslation("surveys.pdf.title", "en", "SURVEY REPORT");
        await db.SaveChangesAsync();

        var pdf = await service.ExportPdfAsync(survey.Id, " EN ");
        var document = string.Join('\n', ExtractPdfPages(pdf!));

        Assert.Contains("SURVEY REPORT", document);
    }

    [Fact]
    public async Task GetDriveConnectionStatusAsync_WritableFolder_ReportsConnectedAccountAndFolder()
    {
        drive.Setup(adapter => adapter.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveConnection(
                "kudung053@gmail.com",
                "Nihome Surveys",
                "https://drive.google.com/drive/folders/root-folder",
                true,
                false,
                false,
                true));
        var status = await service.GetDriveConnectionStatusAsync();

        Assert.Equal("Connected", status.Status);
        Assert.Equal("kudung053@gmail.com", status.AccountEmail);
        Assert.Equal("MyDrive", status.StorageType);
        Assert.Equal("Nihome Surveys", status.RootFolderName);
        Assert.Equal("https://drive.google.com/drive/folders/root-folder", status.RootFolderLink);
    }

    [Fact]
    public async Task GetDriveConnectionStatusAsync_TrashedRoot_ReportsInvalidRoot()
    {
        drive.Setup(adapter => adapter.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveConnection(
                "kudung053@gmail.com",
                "Deleted Surveys",
                "https://drive.google.com/drive/folders/root-folder",
                true,
                true,
                false,
                true));
        var status = await service.GetDriveConnectionStatusAsync();

        Assert.Equal("InvalidRoot", status.Status);
    }

    [Fact]
    public async Task GetDriveConnectionStatusAsync_FolderWithoutAddCapability_ReportsReadOnly()
    {
        drive.Setup(adapter => adapter.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveConnection(
                "kudung053@gmail.com",
                "Nihome Surveys",
                "https://drive.google.com/drive/folders/root-folder",
                true,
                false,
                false,
                false));
        var status = await service.GetDriveConnectionStatusAsync();

        Assert.Equal("ReadOnly", status.Status);
    }

    [Fact]
    public async Task GetDriveConnectionStatusAsync_CredentialFailure_LogsWarningAndReportsUnavailable()
    {
        var logger = new Mock<ILogger<SurveyMediaService>>();
        drive.Setup(adapter => adapter.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("credential path must remain private"));
        var connectedService = new SurveyMediaService(
            db,
            storage.Object,
            drive.Object,
            new TranslationService(db, cache),
            logger.Object);

        var status = await connectedService.GetDriveConnectionStatusAsync();

        Assert.Equal("Unavailable", status.Status);
        Assert.DoesNotContain("credential path", status.Error, StringComparison.OrdinalIgnoreCase);
        logger.Verify(log => log.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("connection validation failed")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    public void Dispose()
    {
        cache.Dispose();
        db.Dispose();
    }

    private void AddTranslation(string key, string language, string value)
    {
        db.Translations.Add(new Translation
        {
            Key = key,
            LanguageCode = language,
            Value = value,
            Category = "surveys",
        });
    }

    private static List<string> ExtractPdfPages(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return document.GetPages().Select(page => ContentOrderTextExtractor.GetText(page)).ToList();
    }

    private async Task<Survey> AddSurveyAsync(SurveyDriveSyncStatus status = SurveyDriveSyncStatus.NotSynced)
    {
        var survey = new Survey
        {
            Code = $"SV-{Guid.NewGuid():N}",
            Location = "Test",
            SurveyDate = DateTime.UtcNow,
            DriveSyncStatus = status,
        };
        db.Surveys.Add(survey);
        await db.SaveChangesAsync();
        return survey;
    }

    private static SurveyMedia Media(int surveyId, long size = 1) => new()
    {
        SurveyId = surveyId,
        OriginalFileName = "photo.jpg",
        StoredFileName = "stored.jpg",
        ContentType = "image/jpeg",
        Extension = ".jpg",
        Size = size,
        RelativePath = $"/files/survey-media/{surveyId}/stored.jpg",
    };
}