using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Models.Rbac;

namespace NihomeBackend.Services;

public enum UserServiceError
{
    DuplicatePhoneNumber,
    DuplicateEmail,
    InvalidRole,
    SelfActionNotAllowed,
    LastSuperAdmin,
}

public class UserServiceException(UserServiceError error, string message) : InvalidOperationException(message)
{
    public UserServiceError Error { get; } = error;
}

public class UserService(AppDbContext db, PasswordService passwordService, INotificationService notifications)
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
            var trimmed = role.Trim();
            var roleId = await db.Roles.AsNoTracking()
                .Where(r => r.Code == trimmed)
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync();
            var hasLegacyEnum = UserRoleCodeMapper.TryFromCode(trimmed, out var legacyEnum);

            query = (roleId, hasLegacyEnum) switch
            {
                // System role: match users by RBAC FK OR legacy enum (covers users
                // not yet backfilled by RbacSeeder).
                (int rid, true) => query.Where(u => u.RoleEntityId == rid || (u.RoleEntityId == null && u.Role == legacyEnum)),
                // Custom role: must match by RBAC FK exactly.
                (int rid, false) => query.Where(u => u.RoleEntityId == rid),
                // RBAC row missing but code is a known system enum (in-memory/test path).
                (null, true) => query.Where(u => u.Role == legacyEnum),
                // Unknown code -> zero matches.
                (null, false) => query.Where(u => false),
            };
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
                Role = u.RoleEntity != null ? u.RoleEntity.Code : u.Role.ToString(),
                RoleId = u.RoleEntityId,
                RoleName = u.RoleEntity != null ? u.RoleEntity.Name : null,
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
            .Include(u => u.RoleEntity)
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

        var email = EmailUniqueness.Normalize(req.Email);
        if (string.IsNullOrEmpty(email))
        {
            throw new UserServiceException(
                UserServiceError.DuplicateEmail,
                "Email is required.");
        }

        if (await EmailUniqueness.IsTakenAsync(db, email))
        {
            throw new UserServiceException(
                UserServiceError.DuplicateEmail,
                "Email already registered.");
        }

        var resolved = await ResolveRoleAsync(req.Role);

        var user = new ApplicationUser
        {
            PhoneNumber = phoneNumber,
            FullName = req.FullName.Trim(),
            Email = email,
            Role = resolved.MirrorEnum,
            RoleEntityId = resolved.RoleEntity?.Id,
            IsActive = true,
        };
        user.PasswordHash = passwordService.Hash(user, req.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Attach the resolved entity so MapDetail can read RoleEntity.Code/.Name
        // without a re-query. EF tracker will accept this since RoleEntityId was
        // just persisted to the matching row.
        if (resolved.RoleEntity != null)
        {
            user.RoleEntity = resolved.RoleEntity;
        }

        try
        {
            await notifications.CreateForAdminsAsync(
                "User",
                $"Người dùng mới được tạo: {user.FullName ?? user.PhoneNumber}",
                null,
                "/admin/users");
        }
        catch { /* best-effort — do not fail the create */ }

        return MapDetail(user);
    }

    public async Task<UserDetailResponse?> UpdateAsync(int id, UpdateUserRequest req, int currentUserId)
    {
        var user = await db.Users
            .Include(u => u.RoleEntity)
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return null;
        }

        var nextRoleEntity = user.RoleEntity;
        var nextRoleEntityId = user.RoleEntityId;
        var nextMirrorEnum = user.Role;

        if (!string.IsNullOrWhiteSpace(req.Role))
        {
            var resolved = await ResolveRoleAsync(req.Role);
            nextRoleEntity = resolved.RoleEntity;
            nextRoleEntityId = resolved.RoleEntity?.Id;
            nextMirrorEnum = resolved.MirrorEnum;
        }

        var nextIsActive = req.IsActive ?? user.IsActive;

        await EnsureRoleAndStatusChangeAllowedAsync(
            user, currentUserId, nextRoleEntityId, nextMirrorEnum, nextIsActive);

        if (req.FullName != null)
        {
            user.FullName = req.FullName.Trim();
        }

        if (req.Email != null)
        {
            var email = EmailUniqueness.Normalize(req.Email);
            if (string.IsNullOrEmpty(email))
            {
                throw new UserServiceException(
                    UserServiceError.DuplicateEmail,
                    "Email is required.");
            }

            if (!email.Equals(user.Email, StringComparison.Ordinal) &&
                await EmailUniqueness.IsTakenAsync(db, email, excludeUserId: user.Id))
            {
                throw new UserServiceException(
                    UserServiceError.DuplicateEmail,
                    "Email already registered.");
            }

            user.Email = email;
        }

        user.Role = nextMirrorEnum;
        user.RoleEntityId = nextRoleEntityId;
        user.RoleEntity = nextRoleEntity;
        user.IsActive = nextIsActive;

        await db.SaveChangesAsync();
        return MapDetail(user);
    }

    public async Task<UserDetailResponse?> ToggleActiveAsync(int id, int currentUserId)
    {
        var user = await db.Users
            .Include(u => u.RoleEntity)
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return null;
        }

        var nextIsActive = !user.IsActive;
        await EnsureRoleAndStatusChangeAllowedAsync(
            user, currentUserId, user.RoleEntityId, user.Role, nextIsActive);

        user.IsActive = nextIsActive;
        await db.SaveChangesAsync();

        try
        {
            var title = nextIsActive ? "Tài khoản đã được kích hoạt" : "Tài khoản đã bị vô hiệu hóa";
            var body = nextIsActive
                ? "Tài khoản của bạn đã được kích hoạt trở lại."
                : "Tài khoản của bạn đã bị vô hiệu hóa. Liên hệ quản trị viên nếu cần hỗ trợ.";
            await notifications.CreateAsync(user.Id, "User", title, body, null);
        }
        catch { /* best-effort — do not fail the toggle */ }

        return MapDetail(user);
    }

    public async Task<bool> DeleteAsync(int id, int currentUserId)
    {
        var user = await db.Users
            .Include(u => u.RoleEntity)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return false;
        }

        await EnsureRoleAndStatusChangeAllowedAsync(
            user, currentUserId, user.RoleEntityId, user.Role, nextIsActive: false);

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
        int? nextRoleEntityId,
        UserRole nextMirrorEnum,
        bool nextIsActive)
    {
        var isSelf = user.Id == currentUserId;
        var roleChanged = nextRoleEntityId != user.RoleEntityId || nextMirrorEnum != user.Role;
        if (isSelf && (!nextIsActive || roleChanged))
        {
            throw new UserServiceException(
                UserServiceError.SelfActionNotAllowed,
                "You cannot change your own role or deactivate your own account.");
        }

        var superAdminRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.Code == SystemRoleCodes.SuperAdmin)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync();

        var isCurrentlySuperAdmin = IsSuperAdmin(user.RoleEntityId, user.Role, superAdminRoleId);
        var willBeSuperAdmin = IsSuperAdmin(nextRoleEntityId, nextMirrorEnum, superAdminRoleId);

        if (isCurrentlySuperAdmin && user.IsActive && (!nextIsActive || !willBeSuperAdmin))
        {
            // Count *other* active super admins. Matches either the RBAC FK or the
            // legacy enum fallback so users not yet backfilled by RbacSeeder still
            // count toward the safety quorum.
            var baseQuery = db.Users.AsNoTracking().Where(u => u.Id != user.Id && u.IsActive);
            var otherActiveSuperAdmins = superAdminRoleId.HasValue
                ? await baseQuery.CountAsync(u =>
                    u.RoleEntityId == superAdminRoleId.Value ||
                    (u.RoleEntityId == null && u.Role == UserRole.SUPER_ADMIN))
                : await baseQuery.CountAsync(u =>
                    u.RoleEntityId == null && u.Role == UserRole.SUPER_ADMIN);

            if (otherActiveSuperAdmins == 0)
            {
                throw new UserServiceException(
                    UserServiceError.LastSuperAdmin,
                    "At least one active SUPER_ADMIN account is required.");
            }
        }
    }

    private static bool IsSuperAdmin(int? roleEntityId, UserRole mirrorEnum, int? superAdminRoleId)
    {
        // Canonical source is the RBAC FK; fall back to the legacy enum when the
        // user has not been linked to a role row yet (test envs without RBAC seed).
        if (roleEntityId.HasValue && superAdminRoleId.HasValue)
        {
            return roleEntityId.Value == superAdminRoleId.Value;
        }
        return !roleEntityId.HasValue && mirrorEnum == UserRole.SUPER_ADMIN;
    }

    /// <summary>
    /// Resolves a role code (system or custom) against the RBAC <c>roles</c>
    /// table. Returns the matched entity and the legacy enum mirror to keep on
    /// <see cref="ApplicationUser.Role"/>. Custom roles always mirror as
    /// <see cref="UserRole.USER"/> so legacy queries (JWT role claim,
    /// NotificationService admin filter) don't implicitly grant elevated access
    /// to a custom-role user.
    /// </summary>
    private async Task<(Role? RoleEntity, UserRole MirrorEnum)> ResolveRoleAsync(string? roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode))
        {
            throw new UserServiceException(UserServiceError.InvalidRole, "Invalid user role.");
        }

        var trimmed = roleCode.Trim();

        // Intentionally tracked (no AsNoTracking): callers assign the returned
        // entity to a tracked User.RoleEntity navigation; an untracked instance
        // with the same key as a previously Included one would cause EF's
        // identity-map conflict on SaveChanges. The Roles table is tiny and
        // looked up by indexed Code, so the tracking cost is negligible.
        var roleEntity = await db.Roles
            .FirstOrDefaultAsync(r => r.Code == trimmed && r.IsActive);

        if (roleEntity != null)
        {
            var mirror = UserRoleCodeMapper.TryFromCode(trimmed, out var enumValue)
                ? enumValue
                : UserRole.USER;
            return (roleEntity, mirror);
        }

        // Backward-compatible fallback: in-memory test envs (and any flow before
        // the RBAC seeder has run) can still pass a known system code without a
        // matching Roles row. Persisting only the enum keeps the legacy path
        // working until RbacSeeder.BackfillUserRoleEntityIds runs at next boot.
        if (UserRoleCodeMapper.TryFromCode(trimmed, out var legacyEnum))
        {
            return (null, legacyEnum);
        }

        throw new UserServiceException(UserServiceError.InvalidRole, "Invalid user role.");
    }

    private static UserDetailResponse MapDetail(ApplicationUser user) => new()
    {
        Id = user.Id,
        PhoneNumber = user.PhoneNumber,
        FullName = user.FullName,
        Email = user.Email,
        Role = user.RoleEntity != null ? user.RoleEntity.Code : user.Role.ToString(),
        RoleId = user.RoleEntityId,
        RoleName = user.RoleEntity?.Name,
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
