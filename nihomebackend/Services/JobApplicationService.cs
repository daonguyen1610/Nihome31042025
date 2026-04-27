using NihomeBackend.Constants;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class JobApplicationService(
    AppDbContext db,
    RecruitmentMetadataService recruitmentMetadataService,
    IEmailService emailService,
    ILogger<JobApplicationService> logger)
{
    public async Task<List<JobApplicationResponse>> GetAllAsync(int? positionId = null, string? status = null)
    {
        var query = db.JobApplications
            .AsNoTracking()
            .Include(a => a.JobPosition)
            .AsQueryable();

        if (positionId.HasValue)
            query = query.Where(a => a.JobPositionId == positionId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status);

        var items = await query.OrderByDescending(a => a.AppliedAt).ToListAsync();
        return items.Select(MapToResponse).ToList();
    }

    public async Task<JobApplicationResponse> SubmitAsync(SubmitJobApplicationRequest req)
    {
        var positionExists = await db.JobPositions.AnyAsync(j => j.Id == req.JobPositionId && j.IsActive);
        if (!positionExists)
            throw new InvalidOperationException("Vị trí tuyển dụng không tồn tại hoặc đã đóng.");

        var defaultStatus = await recruitmentMetadataService.GetDefaultOptionValueAsync(
            RecruitmentMetadataGroups.ApplicationStatus);

        var entity = new JobApplication
        {
            JobPositionId = req.JobPositionId,
            CandidateName = req.CandidateName.Trim(),
            Email = req.Email.Trim().ToLowerInvariant(),
            Phone = req.Phone?.Trim(),
            ExperienceYears = req.ExperienceYears,
            CoverLetter = req.CoverLetter?.Trim(),
            CvUrl = req.CvUrl?.Trim(),
            Status = defaultStatus,
        };

        db.JobApplications.Add(entity);
        await db.SaveChangesAsync();

        await db.Entry(entity).Reference(a => a.JobPosition).LoadAsync();

        // Send notification email (best-effort, don't fail the submission)
        try
        {
            var settings = await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
            var notifyEmail = settings?.NotificationEmail ?? settings?.PrimaryEmail;
            if (!string.IsNullOrWhiteSpace(notifyEmail))
            {
                var (subject, body) = EmailTemplateFormatter.BuildNewApplicationEmail(
                    settings?.NewApplicationEmailSubjectTemplate,
                    settings?.NewApplicationEmailBodyTemplate,
                    settings?.SiteName ?? "Nihome",
                    entity.JobPosition?.Title ?? "",
                    entity.JobPosition?.Department ?? "",
                    entity.CandidateName,
                    entity.Email,
                    entity.Phone,
                    entity.ExperienceYears,
                    entity.CoverLetter,
                    entity.AppliedAt);

                await emailService.SendEmailAsync(notifyEmail, subject, body);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send new application notification email for application {Id}", entity.Id);
        }

        return MapToResponse(entity);
    }

    public async Task<JobApplicationResponse?> UpdateStatusAsync(int id, string status)
    {
        await recruitmentMetadataService.EnsureOptionExistsAsync(
            RecruitmentMetadataGroups.ApplicationStatus,
            status);

        var entity = await db.JobApplications
            .Include(a => a.JobPosition)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (entity == null) return null;

        entity.Status = status;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.JobApplications.FindAsync(id);
        if (entity == null) return false;
        db.JobApplications.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    private static JobApplicationResponse MapToResponse(JobApplication a) => new()
    {
        Id = a.Id,
        JobPositionId = a.JobPositionId,
        PositionTitle = a.JobPosition?.Title ?? "",
        CandidateName = a.CandidateName,
        Email = a.Email,
        Phone = a.Phone,
        ExperienceYears = a.ExperienceYears,
        CoverLetter = a.CoverLetter,
        CvUrl = a.CvUrl,
        Status = a.Status,
        AppliedAt = a.AppliedAt,
    };
}
