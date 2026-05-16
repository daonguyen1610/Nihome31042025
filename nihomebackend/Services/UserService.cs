using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public enum UserServiceError
{
    DuplicatePhoneNumber,
    InvalidRole,
    SelfActionNotAllowed,
    LastSuperAdmin,
}

public class UserServiceException(UserServiceError error, string message) : InvalidOperationException(message)
{
    public UserServiceError Error { get; } = error;
}

public class UserService(AppDbContext db, PasswordService passwordService)
{
    private const int DefaultTake = 20;
    private const int MaxTake = 100;

    public async Task<UserListResponse> GetListAsync(int skip, int take, string? search, string? role)
    {
        skip = Math.Max(0, skip);
        take = take <= 0 ? DefaultTake : Math.Min(take, MaxTake);

        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == ParseRole(role));
        }

        var normalizedSearch = search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(u =>
                u.PhoneNumber.ToLower().Contains(normalizedSearch) ||
                (u.FullName != null && u.FullName.ToLower().Contains(normalizedSearch)) ||
                (u.Email != null && u.Email.ToLower().Contains(normalizedSearch)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(u => u.Id)
            .Skip(skip)
            .Take(take)
            .Select(u => new UserListItemResponse
            {
                Id = u.Id,
                PhoneNumber = u.PhoneNumber,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role.ToString(),
                IsActive = u.IsActive,
                AvatarUrl = u.AvatarUrl,
            })
            .ToListAsync();

        return new UserListResponse { Items = items, Total = total };
    }

    public async Task<UserDetailResponse?> GetByIdAsync(int id)
    {
        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == id);

        return user == null ? null : MapDetail(user);
    }

    public async Task<UserDetailResponse> CreateAsync(CreateUserRequest req)
    {
        var phoneNumber = req.PhoneNumber.Trim();
        if (await db.Users.AsNoTracking().AnyAsync(u => u.PhoneNumber == phoneNumber))
        {
            throw new UserServiceException(
                UserServiceError.DuplicatePhoneNumber,
                "Phone number already registered.");
        }

        var user = new ApplicationUser
        {
            PhoneNumber = phoneNumber,
            FullName = req.FullName.Trim(),
            Email = NormalizeOptional(req.Email),
            Role = ParseRole(req.Role),
            IsActive = true,
        };
        user.PasswordHash = passwordService.Hash(user, req.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return MapDetail(user);
    }

    public async Task<UserDetailResponse?> UpdateAsync(int id, UpdateUserRequest req, int currentUserId)
    {
        var user = await db.Users.Include(u => u.RefreshTokens).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return null;
        }

        var nextRole = string.IsNullOrWhiteSpace(req.Role) ? user.Role : ParseRole(req.Role);
        var nextIsActive = req.IsActive ?? user.IsActive;

        await EnsureRoleAndStatusChangeAllowedAsync(user, currentUserId, nextRole, nextIsActive);

        if (req.FullName != null)
        {
            user.FullName = req.FullName.Trim();
        }

        if (req.Email != null)
        {
            user.Email = NormalizeOptional(req.Email);
        }

        user.Role = nextRole;
        user.IsActive = nextIsActive;

        await db.SaveChangesAsync();
        return MapDetail(user);
    }

    public async Task<UserDetailResponse?> ToggleActiveAsync(int id, int currentUserId)
    {
        var user = await db.Users.Include(u => u.RefreshTokens).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return null;
        }

        var nextIsActive = !user.IsActive;
        await EnsureRoleAndStatusChangeAllowedAsync(user, currentUserId, user.Role, nextIsActive);

        user.IsActive = nextIsActive;
        await db.SaveChangesAsync();

        return MapDetail(user);
    }

    public async Task<bool> DeleteAsync(int id, int currentUserId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return false;
        }

        await EnsureRoleAndStatusChangeAllowedAsync(user, currentUserId, user.Role, nextIsActive: false);

        user.IsActive = false;
        await db.SaveChangesAsync();

        return true;
    }

    public async Task<RoleCatalogResponse> GetRoleCatalogAsync()
    {
        var counts = await db.Users
            .AsNoTracking()
            .GroupBy(u => u.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Role, x => x.Count);

        var roles = Enum.GetValues<UserRole>()
            .Select(role => new RoleMetadataResponse
            {
                Role = role.ToString(),
                LabelKey = $"adminUsers.roles.{role}.label",
                DescriptionKey = $"adminUsers.roles.{role}.description",
                UserCount = counts.GetValueOrDefault(role, 0),
                IsSystemRole = true,
            })
            .ToList();

        return new RoleCatalogResponse
        {
            Roles = roles,
            PermissionMatrix = BuildPermissionMatrix(),
        };
    }

    private async Task EnsureRoleAndStatusChangeAllowedAsync(
        ApplicationUser user,
        int currentUserId,
        UserRole nextRole,
        bool nextIsActive)
    {
        var isSelf = user.Id == currentUserId;
        if (isSelf && (!nextIsActive || nextRole != user.Role))
        {
            throw new UserServiceException(
                UserServiceError.SelfActionNotAllowed,
                "You cannot change your own role or deactivate your own account.");
        }

        if (user.Role == UserRole.SUPER_ADMIN && user.IsActive && (!nextIsActive || nextRole != UserRole.SUPER_ADMIN))
        {
            var otherActiveSuperAdmins = await db.Users
                .AsNoTracking()
                .CountAsync(u => u.Id != user.Id && u.Role == UserRole.SUPER_ADMIN && u.IsActive);

            if (otherActiveSuperAdmins == 0)
            {
                throw new UserServiceException(
                    UserServiceError.LastSuperAdmin,
                    "At least one active SUPER_ADMIN account is required.");
            }
        }
    }

    private static UserRole ParseRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role) ||
            !Enum.TryParse<UserRole>(role.Trim(), ignoreCase: true, out var parsed) ||
            !Enum.IsDefined(parsed))
        {
            throw new UserServiceException(UserServiceError.InvalidRole, "Invalid user role.");
        }

        return parsed;
    }

    private static UserDetailResponse MapDetail(ApplicationUser user) => new()
    {
        Id = user.Id,
        PhoneNumber = user.PhoneNumber,
        FullName = user.FullName,
        Email = user.Email,
        Role = user.Role.ToString(),
        IsActive = user.IsActive,
        AvatarUrl = user.AvatarUrl,
        RefreshTokenCount = user.RefreshTokens?.Count ?? 0,
    };

    private static List<PermissionMatrixRowResponse> BuildPermissionMatrix() =>
    [
        Permission("adminUsers.permissions.dashboard", superAdmin: true, admin: true, user: false),
        Permission("adminUsers.permissions.content", superAdmin: true, admin: true, user: false),
        Permission("adminUsers.permissions.recruitment", superAdmin: true, admin: true, user: false),
        Permission("adminUsers.permissions.contacts", superAdmin: true, admin: true, user: false),
        Permission("adminUsers.permissions.settings", superAdmin: true, admin: true, user: false),
        Permission("adminUsers.permissions.users", superAdmin: true, admin: false, user: false),
        Permission("adminUsers.permissions.publicProfile", superAdmin: true, admin: true, user: true),
    ];

    private static PermissionMatrixRowResponse Permission(
        string moduleKey,
        bool superAdmin,
        bool admin,
        bool user)
    {
        return new PermissionMatrixRowResponse
        {
            ModuleKey = moduleKey,
            AccessByRole = new Dictionary<string, bool>
            {
                [UserRole.SUPER_ADMIN.ToString()] = superAdmin,
                [UserRole.ADMIN.ToString()] = admin,
                [UserRole.USER.ToString()] = user,
            },
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
