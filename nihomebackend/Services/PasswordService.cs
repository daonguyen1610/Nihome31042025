using Microsoft.AspNetCore.Identity;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public class PasswordService
{
    private readonly PasswordHasher<ApplicationUser> _hasher = new();

    public string Hash(ApplicationUser user, string password) =>
        _hasher.HashPassword(user, password);

    public bool Verify(ApplicationUser user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Success;
}
