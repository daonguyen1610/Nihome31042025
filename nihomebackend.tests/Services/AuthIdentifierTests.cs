using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class AuthIdentifierTests : IDisposable
{
    private readonly NihomeBackend.Data.AppDbContext _db = DbContextFactory.Create();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task FindUserAsync_MatchesEmailCaseInsensitively()
    {
        _db.Users.Add(new ApplicationUser
        {
            PhoneNumber = "0900000001",
            FullName = "A",
            Email = "match@nihome.vn",
            PasswordHash = "x",
        });
        await _db.SaveChangesAsync();

        var user = await AuthIdentifier.FindUserAsync(_db, "  MATCH@Nihome.vn  ");
        Assert.NotNull(user);
        Assert.Equal("0900000001", user.PhoneNumber);
    }

    [Fact]
    public async Task FindUserAsync_MatchesTrimmedPhone()
    {
        _db.Users.Add(new ApplicationUser
        {
            PhoneNumber = "0900000002",
            FullName = "B",
            Email = "phone@nihome.vn",
            PasswordHash = "x",
        });
        await _db.SaveChangesAsync();

        var user = await AuthIdentifier.FindUserAsync(_db, "  0900000002  ");
        Assert.NotNull(user);
        Assert.Equal("phone@nihome.vn", user.Email);
    }
}
