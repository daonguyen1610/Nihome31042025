using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models.DTOs.Requests;

namespace NihomeBackend.Services;

public sealed record ContactLeadResult(int LeadId, bool Created);

/// <summary>
/// Turns a website contact message into a CRM lead through the normal lead creation rules
/// (validation, owner assignment, notifications), and links them so it happens only once.
/// </summary>
public sealed class ContactLeadService(AppDbContext db, ILeadService leads)
{
    private const string WebsiteSource = "website";
    private const int NoteLimit = 4000;

    public async Task<ContactLeadResult?> CreateLeadAsync(int contactId, int callerUserId, CancellationToken ct)
    {
        var contact = await db.ContactMessages.FirstOrDefaultAsync(item => item.Id == contactId, ct);
        if (contact is null) return null;
        if (contact.LeadId is int existing) return new ContactLeadResult(existing, Created: false);

        var note = $"Liên hệ từ website ngày {contact.CreatedAt:dd/MM/yyyy} – {contact.Subject}\n\n{contact.Message}";
        var lead = await leads.CreateAsync(new CreateLeadRequest
        {
            Name = contact.Name,
            Phone = string.IsNullOrWhiteSpace(contact.Phone) ? null : contact.Phone,
            Email = string.IsNullOrWhiteSpace(contact.Email) ? null : contact.Email,
            SourceCode = WebsiteSource,
            Note = note.Length <= NoteLimit ? note : note[..NoteLimit],
        }, callerUserId, canManage: true, ct: ct);

        contact.LeadId = lead.Id;
        contact.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new ContactLeadResult(lead.Id, Created: true);
    }
}
