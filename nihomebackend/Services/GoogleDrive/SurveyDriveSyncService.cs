using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Services.GoogleDrive;

public interface ISurveyDriveSyncProcessor
{
    Task<bool> ProcessNextAsync(CancellationToken ct = default);
}

public sealed class SurveyDriveSyncProcessor(
    AppDbContext db,
    ISurveyMediaStorageService storage,
    IGoogleDriveAdapter drive,
    ISurveyMediaService mediaService,
    GoogleDriveOptions options,
    IAuditLogger audit,
    ILogger<SurveyDriveSyncProcessor> logger) : ISurveyDriveSyncProcessor
{
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Claims one due row with a conditional update before making any remote call. The persisted
    /// lease lets another worker recover an interrupted upload without exceeding three claims.
    /// </summary>
    public async Task<bool> ProcessNextAsync(CancellationToken ct = default)
    {
        var connection = await mediaService.GetDriveConnectionStatusAsync(ct);
        if (!string.Equals(connection.Status, "Connected", StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Survey Drive synchronization paused because the validated connection status is {Status}",
                connection.Status);
            return false;
        }

        var now = DateTime.UtcNow;
        var candidates = await db.SurveyMedia.AsNoTracking()
            .Where(IsDueForClaim(now))
            .OrderBy(m => m.NextSyncAttemptAt ?? m.CreatedAt)
            .Select(m => m.Id)
            .Take(10)
            .ToListAsync(ct);

        foreach (var id in candidates)
        {
            var claimToken = Guid.NewGuid();
            var claimed = await db.SurveyMedia
                .Where(m => m.Id == id)
                .Where(IsDueForClaim(now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.SyncStatus, SurveyMediaSyncStatus.Processing)
                    .SetProperty(m => m.SyncAttemptCount, m =>
                        m.SyncStatus == SurveyMediaSyncStatus.Pending
                            ? m.SyncAttemptCount + 1
                            : m.SyncAttemptCount)
                    .SetProperty(m => m.SyncStartedAt, now)
                    .SetProperty(m => m.LastSyncAttemptAt, now)
                    .SetProperty(m => m.NextSyncAttemptAt, (DateTime?)null)
                    .SetProperty(m => m.SyncError, (string?)null)
                    .SetProperty(m => m.ClaimToken, claimToken)
                    .SetProperty(m => m.ClaimExpiresAt, now.Add(ClaimLease))
                    .SetProperty(m => m.UpdatedAt, now), ct);
            if (claimed == 0) continue;

            await ProcessClaimAsync(id, claimToken, ct);
            return true;
        }
        return false;
    }

    internal static Expression<Func<SurveyMedia, bool>> IsDueForClaim(DateTime now) => media =>
        (media.SyncStatus == SurveyMediaSyncStatus.Pending &&
         media.SyncAttemptCount < SurveyMediaService.MaxSyncAttempts &&
         (!media.NextSyncAttemptAt.HasValue || media.NextSyncAttemptAt <= now)) ||
        (media.SyncStatus == SurveyMediaSyncStatus.Processing &&
         media.SyncAttemptCount <= SurveyMediaService.MaxSyncAttempts &&
         media.ClaimExpiresAt <= now);

    private async Task ProcessClaimAsync(long mediaId, Guid claimToken, CancellationToken ct)
    {
        var media = await db.SurveyMedia
            .Include(m => m.Survey).ThenInclude(s => s.LinkedProject)
            .Include(m => m.Survey).ThenInclude(s => s.LinkedOpportunity)
            .FirstAsync(m => m.Id == mediaId && m.ClaimToken == claimToken, ct);

        try
        {
            var businessCode = media.Survey.LinkedProject?.Slug;
            if (string.IsNullOrWhiteSpace(businessCode) && media.Survey.LinkedOpportunityId.HasValue)
            {
                businessCode = $"OPP-{media.Survey.LinkedOpportunityId.Value}";
            }
            businessCode ??= media.Survey.Code;

            var folder = await drive.EnsureFolderPathAsync(
                [options.Folders.SurveyMedia, SafeFolderName(businessCode), SafeFolderName(media.Survey.Code)], ct);
            await using var content = storage.OpenRead(media.SurveyId, media.RelativePath);
            var uploaded = await drive.UploadAsync(
                folder.Id, media.Id, media.OriginalFileName, media.ContentType, content, ct);

            var completedAt = DateTime.UtcNow;
            media.DriveFileId = uploaded.FileId;
            media.DriveFolderId = folder.Id;
            media.DriveFolderLink = folder.Link;
            media.SyncStatus = SurveyMediaSyncStatus.Synced;
            media.SyncError = null;
            media.SyncedAt = completedAt;
            media.ClaimToken = null;
            media.ClaimExpiresAt = null;
            media.UpdatedAt = completedAt;
            media.Survey.DriveFolderId = folder.Id;
            media.Survey.DriveFolderLink = folder.Link;
            await mediaService.RecalculateAggregateAsync(media.SurveyId, ct);
            await db.SaveChangesAsync(ct);
            audit.Log(new AuditEvent
            {
                Action = "survey.media.sync_success",
                ResourceType = EntityTypes.Survey,
                ResourceId = media.SurveyId.ToString(),
                Message = $"Media #{media.Id} synced to Google Drive on attempt {media.SyncAttemptCount}.",
                TargetSystem = "GoogleDrive",
            });
        }
        catch (Exception exception)
        {
            var failedAt = DateTime.UtcNow;
            media.SyncError = Limit(exception.Message, 2000);
            media.SyncStatus = media.SyncAttemptCount >= SurveyMediaService.MaxSyncAttempts
                ? SurveyMediaSyncStatus.Failed
                : SurveyMediaSyncStatus.Pending;
            media.NextSyncAttemptAt = media.SyncStatus == SurveyMediaSyncStatus.Pending
                ? failedAt.Add(Backoff(media.SyncAttemptCount))
                : null;
            media.ClaimToken = null;
            media.ClaimExpiresAt = null;
            media.UpdatedAt = failedAt;
            await mediaService.RecalculateAggregateAsync(media.SurveyId, ct);
            await db.SaveChangesAsync(ct);
            audit.Log(new AuditEvent
            {
                Action = "survey.media.sync_failure",
                ResourceType = EntityTypes.Survey,
                ResourceId = media.SurveyId.ToString(),
                Message = $"Media #{media.Id} Drive sync failed on attempt {media.SyncAttemptCount}: {media.SyncError}",
                Status = AuditStatus.Failure,
                FailureReason = media.SyncError,
                TargetSystem = "GoogleDrive",
            });
            logger.LogWarning(exception, "Survey media {MediaId} Drive sync attempt {Attempt} failed", media.Id, media.SyncAttemptCount);
        }
    }

    internal static TimeSpan Backoff(int attempt) => attempt switch
    {
        <= 1 => TimeSpan.FromMinutes(1),
        _ => TimeSpan.FromMinutes(5),
    };

    private static string SafeFolderName(string value) =>
        string.Join("-", value.Split(Path.GetInvalidFileNameChars().Concat(['/']).ToArray(),
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

/// <summary>
/// Polls for pending media. Every attempt is claimed and recorded in SQL before contacting Drive.
/// </summary>
public sealed class SurveyDriveSyncService(
    IServiceScopeFactory scopeFactory,
    GoogleDriveOptions options,
    ILogger<SurveyDriveSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Clamp(options.PollIntervalSeconds, 5, 300));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<ISurveyDriveSyncProcessor>();
                if (!await processor.ProcessNextAsync(stoppingToken))
                {
                    await Task.Delay(delay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Survey Drive sync polling failed; polling will resume.");
                await Task.Delay(delay, stoppingToken);
            }
        }
    }
}