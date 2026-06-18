using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class EmailUniquenessTests : IDisposable
{
    private readonly NihomeBackend.Data.AppDbContext _db = DbContextFactory.Create();

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData("  Foo@BAR.com  ", "foo@bar.com")]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void Normalize_TrimsAndLowercases(string? input, string expected)
    {
        Assert.Equal(expected, EmailUniqueness.Normalize(input));
    }

    [Fact]
    public async Task IsTakenAsync_TrueForExistingEmail_CaseInsensitive()
    {
        _db.Users.Add(new ApplicationUser
        {
            PhoneNumber = "0900000001",
            FullName = "A",
            Email = "match@nihome.vn",
            PasswordHash = "x",
        });
        await _db.SaveChangesAsync();

        Assert.True(await EmailUniqueness.IsTakenAsync(_db, "MATCH@Nihome.vn"));
    }

    [Fact]
    public async Task IsTakenAsync_ExcludesGivenUserId()
    {
        var user = new ApplicationUser
        {
            PhoneNumber = "0900000002",
            FullName = "B",
            Email = "self@nihome.vn",
            PasswordHash = "x",
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        Assert.False(await EmailUniqueness.IsTakenAsync(_db, "self@nihome.vn", excludeUserId: user.Id));
    }
}
