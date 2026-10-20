using System.Data;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

// Controllers enforce Contract read/write scope before calling this service.
public sealed class ContractCareActivityService(AppDbContext db)
{
    public async Task<ContractCareActivityListResponse> ListAsync(
        int contractId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, int.MaxValue / 100);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.ContractCareActivities.AsNoTracking().Where(item => item.ContractId == contractId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new ContractCareActivityResponse
            {
                Id = item.Id,
                ContractId = item.ContractId,
                Type = item.Type,
                Content = item.Content,
                OccurredAt = item.OccurredAt,
                CreatedByUserId = item.CreatedByUserId,
                CreatedByName = item.CreatedBy.FullName,
                CreatedAt = item.CreatedAt,
            }).ToListAsync(ct);
        return new(total, page, pageSize, items);
    }

    public async Task<ContractCareActivityResponse?> AddAsync(
        int contractId, ContractCareActivityRequest request, int userId, bool canSeeAll, CancellationToken ct)
    {
        var content = request.Content?.Trim();
        if (string.IsNullOrEmpty(content) || content.Length > 4000)
            throw new ArgumentException("Content must contain 1–4000 characters after trimming.");
        if (request.Type is null or < CustomerActivityType.Call or > CustomerActivityType.Note)
            throw new ArgumentException("Activity type must be Call, Email, Meeting or Note.");

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var contract = await db.Contracts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == contractId, ct);
        if (contract is null || !canSeeAll && contract.OwnerUserId != userId) return null;
        if (contract.Direction != ContractDirection.Upstream)
            throw new ArgumentException("Customer care requires an Upstream contract.");

        var now = DateTime.UtcNow;
        var activity = new ContractCareActivity
        {
            ContractId = contractId,
            Type = request.Type.Value,
            Content = content,
            OccurredAt = request.OccurredAt?.UtcDateTime ?? now,
            CreatedByUserId = userId,
            CreatedAt = now,
        };
        db.ContractCareActivities.Add(activity);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new ContractCareActivityResponse
        {
            Id = activity.Id,
            ContractId = activity.ContractId,
            Type = activity.Type,
            Content = activity.Content,
            OccurredAt = activity.OccurredAt,
            CreatedByUserId = userId,
            CreatedByName = await db.Users.AsNoTracking().Where(user => user.Id == userId)
                .Select(user => user.FullName).SingleAsync(ct),
            CreatedAt = now,
        };
    }
}
