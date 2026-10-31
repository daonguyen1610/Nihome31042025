using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;

namespace NihomeBackend.Services;

internal static class DesignTeamMembership
{
    internal static Task<bool> ContainsActiveUserAsync(
        AppDbContext db, int designProjectId, int userId, CancellationToken ct) =>
        db.OperationalProjectMembers.AsNoTracking().AnyAsync(member =>
            member.DesignProjectId == designProjectId && member.UserId == userId &&
            member.EndedAt == null && member.User.IsActive, ct);
}
