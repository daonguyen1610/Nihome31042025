using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public class ContactValidationTests
{
    [Theory]
    [InlineData("0987654321")]
    [InlineData("0987 654 321")]
    [InlineData("0987.654.321")]
    [InlineData("+84987654321")]
    [InlineData("84987654321")]
    [InlineData("(098) 765-4321")]
    public void IsValidPhone_AcceptsTheWaysPeopleWriteVietnameseNumbers(string phone)
    {
        Assert.True(ContactValidation.IsValidPhone(phone));
    }

    [Theory]
    [InlineData("ewrt")]            // the value that started this
    [InlineData("abc123")]
    [InlineData("0")]
    [InlineData("012")]             // too short
    [InlineData("0987654321098765")] // too long
    [InlineData("+1555123456")]     // not a Vietnamese prefix
    public void IsValidPhone_RejectsMalformedNumbers(string phone)
    {
        Assert.False(ContactValidation.IsValidPhone(phone));
    }

    [Theory]
    [InlineData("a@b.vn")]
    [InlineData("nguyen.van.a@nihome.com.vn")]
    [InlineData("sale+crm@nihome.vn")]
    public void IsValidEmail_AcceptsRealAddresses(string email)
    {
        Assert.True(ContactValidation.IsValidEmail(email));
    }

    [Theory]
    [InlineData("345@434")]   // the value that started this — [EmailAddress] lets it through
    [InlineData("noatsign")]
    [InlineData("@nihome.vn")]
    [InlineData("a@")]
    [InlineData("a@b")]
    [InlineData("a b@nihome.vn")]
    public void IsValidEmail_RejectsMalformedAddresses(string email)
    {
        Assert.False(ContactValidation.IsValidEmail(email));
    }

    [Fact]
    public void BlankIsShapeValid_BecausePresenceIsASeparateRule()
    {
        Assert.True(ContactValidation.IsValidPhone(null));
        Assert.True(ContactValidation.IsValidPhone("   "));
        Assert.True(ContactValidation.IsValidEmail(null));
    }

    [Fact]
    public void Validate_RequiresAtLeastOneChannel()
    {
        Assert.NotNull(ContactValidation.Validate(null, null));
        Assert.Null(ContactValidation.Validate("0987654321", null));
        Assert.Null(ContactValidation.Validate(null, "a@b.vn"));
    }

    [Fact]
    public void Validate_RejectsAMalformedValueEvenWhenTheOtherIsFine()
    {
        Assert.NotNull(ContactValidation.Validate("ewrt", "a@b.vn"));
        Assert.NotNull(ContactValidation.Validate("0987654321", "345@434"));
    }
}
