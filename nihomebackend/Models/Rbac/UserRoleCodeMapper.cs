using System.Collections.Frozen;
using NihomeBackend.Models;

namespace NihomeBackend.Models.Rbac;

/// <summary>
/// Adapter between the legacy <see cref="UserRole"/> enum domain and the
/// string-based role-code domain used by the RBAC tables. Centralises the
/// mapping so callers never rely on <c>enum.ToString()</c> matching role
/// codes by accident — if the enum is ever renamed, only this file changes.
/// Backed by a <see cref="FrozenDictionary{TKey,TValue}"/> built once at
/// startup (effectively a singleton immutable map).
/// </summary>
public static class UserRoleCodeMapper
{
    private static readonly FrozenDictionary<UserRole, string> EnumToCode =
        new Dictionary<UserRole, string>
        {
            [UserRole.SUPER_ADMIN] = SystemRoleCodes.SuperAdmin,
            [UserRole.ADMIN] = SystemRoleCodes.Admin,
            [UserRole.USER] = SystemRoleCodes.User,
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, UserRole> CodeToEnum =
        EnumToCode.ToFrozenDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    public static string ToCode(UserRole role) => EnumToCode[role];

    public static bool TryFromCode(string? code, out UserRole role)
    {
        if (!string.IsNullOrWhiteSpace(code) && CodeToEnum.TryGetValue(code.Trim(), out role))
            return true;
        role = default;
        return false;
    }
}
