using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class ContactMessageService(
    AppDbContext db,
    IEmailService emailService,
    ILogger<ContactMessageService> logger)
{
    public async Task<List<ContactMessageResponse>> GetAllAsync(bool? replied = null)
    {
        var query = db.ContactMessages.AsNoTracking().AsQueryable();

        if (replied.HasValue)
            query = query.Where(c => c.IsReplied == replied.Value);

        var items = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        return items.Select(MapToResponse).ToList();
    }

    public async Task<ContactMessageResponse?> GetByIdAsync(int id)
    {
        var entity = await db.ContactMessages.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return entity == null ? null : MapToResponse(entity);
    }

    public async Task<ContactMessageResponse> SubmitAsync(SubmitContactRequest req)
    {
        var entity = new ContactMessage
        {
            Name = req.Name.Trim(),
            Email = req.Email.Trim().ToLowerInvariant(),
            Phone = req.Phone?.Trim(),
            Subject = req.Subject.Trim(),
            Message = req.Message.Trim(),
        };

        db.ContactMessages.Add(entity);
        await db.SaveChangesAsync();

        // Send notification email (best-effort)
        try
        {
            var settings = await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
            var notifyEmail = settings?.NotificationEmail ?? settings?.PrimaryEmail;
            if (!string.IsNullOrWhiteSpace(notifyEmail))
            {
                var subject = $"[{settings?.SiteName ?? "Nihome"}] Liên hệ mới: {entity.Name} – {entity.Subject}";
                var body = $"""
                    <div style='font-family:Segoe UI,Arial,sans-serif;max-width:600px;margin:0 auto;'>
                      <h2>Liên hệ mới từ website</h2>
                      <table style='width:100%;border-collapse:collapse;font-size:14px;'>
                        <tr><td style='padding:6px 0;font-weight:600;width:120px;'>Họ tên:</td><td>{System.Net.WebUtility.HtmlEncode(entity.Name)}</td></tr>
                        <tr><td style='padding:6px 0;font-weight:600;'>Email:</td><td>{System.Net.WebUtility.HtmlEncode(entity.Email)}</td></tr>
                        <tr><td style='padding:6px 0;font-weight:600;'>Điện thoại:</td><td>{System.Net.WebUtility.HtmlEncode(entity.Phone ?? "—")}</td></tr>
                        <tr><td style='padding:6px 0;font-weight:600;'>Chủ đề:</td><td>{System.Net.WebUtility.HtmlEncode(entity.Subject)}</td></tr>
                      </table>
                      <div style='margin:14px 0;padding:12px;background:#f8fafc;border:1px solid #e5e7eb;border-radius:6px;'>
                        <p style='margin:0;white-space:pre-wrap;'>{System.Net.WebUtility.HtmlEncode(entity.Message)}</p>
                      </div>
                    </div>
                    """;
                await emailService.SendEmailAsync(notifyEmail, subject, body);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send contact notification email for contact {Id}", entity.Id);
        }

        return MapToResponse(entity);
    }

    public async Task<ContactMessageResponse?> ReplyAsync(int id, ReplyContactRequest req)
    {
        var entity = await db.ContactMessages.FindAsync(id);
        if (entity == null) return null;

        entity.ReplyContent = req.ReplyContent.Trim();
        entity.IsReplied = true;
        entity.RepliedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Send reply email to customer (best-effort)
        try
        {
            var settings = await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
            var siteName = settings?.SiteName ?? "Nihome";
            var subject = $"Re: {entity.Subject} – {siteName}";
            var body = $"""
                <div style='font-family:Segoe UI,Arial,sans-serif;max-width:600px;margin:0 auto;'>
                  <h2>Phản hồi từ {System.Net.WebUtility.HtmlEncode(siteName)}</h2>
                  <div style='margin:14px 0;padding:12px;background:#f0fdf4;border:1px solid #bbf7d0;border-radius:6px;'>
                    <p style='margin:0;white-space:pre-wrap;'>{System.Net.WebUtility.HtmlEncode(entity.ReplyContent)}</p>
                  </div>
                  <hr style='border:none;border-top:1px solid #e5e7eb;margin:16px 0;'/>
                  <p style='font-size:12px;color:#6b7280;'>Tin nhắn gốc của bạn:</p>
                  <div style='padding:12px;background:#f8fafc;border:1px solid #e5e7eb;border-radius:6px;'>
                    <p style='margin:0;white-space:pre-wrap;font-size:13px;'>{System.Net.WebUtility.HtmlEncode(entity.Message)}</p>
                  </div>
                </div>
                """;
            await emailService.SendEmailAsync(entity.Email, subject, body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send reply email for contact {Id}", entity.Id);
        }

        return MapToResponse(entity);
    }

    public async Task<ContactMessageResponse?> MarkRepliedAsync(int id)
    {
        var entity = await db.ContactMessages.FindAsync(id);
        if (entity == null) return null;

        entity.IsReplied = true;
        entity.RepliedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.ContactMessages.FindAsync(id);
        if (entity == null) return false;
        db.ContactMessages.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    private static ContactMessageResponse MapToResponse(ContactMessage c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Email = c.Email,
        Phone = c.Phone,
        Subject = c.Subject,
        Message = c.Message,
        IsReplied = c.IsReplied,
        ReplyContent = c.ReplyContent,
        RepliedAt = c.RepliedAt,
        CreatedAt = c.CreatedAt,
    };
}
