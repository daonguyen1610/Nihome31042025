using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Mappings;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;
using Xunit;

namespace nihomebackend.tests.Mappings;

public class AutoMapperProfileTests
{
    private readonly IMapper _mapper;

    public AutoMapperProfileTests()
    {
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<AutoMapperProfile>(),
            NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();
    }

    [Fact]
    public void Configuration_IsValid()
    {
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<AutoMapperProfile>(),
            NullLoggerFactory.Instance);

        config.AssertConfigurationIsValid();
    }

    [Fact]
    public void Map_ApplicationUser_ToAuthResponse_MapsBasicFields()
    {
        var user = new ApplicationUser
        {
            Id = 42,
            PhoneNumber = "0123456789",
            FullName = "Test User",
            Email = "test@test.com",
            Role = UserRole.ADMIN,
            IsActive = true,
            AvatarUrl = "https://example.com/avatar.png",
            PasswordHash = "hashed"
        };

        var response = _mapper.Map<AuthResponse>(user);

        Assert.Equal(42, response.UserId);
        Assert.Equal("0123456789", response.PhoneNumber);
        Assert.Equal("Test User", response.FullName);
        Assert.Equal("test@test.com", response.Email);
        Assert.Equal("ADMIN", response.Role);
        Assert.True(response.IsActive);
        Assert.Equal("https://example.com/avatar.png", response.AvatarUrl);
    }

    [Fact]
    public void Map_ApplicationUser_ToAuthResponse_IgnoresTokenFields()
    {
        var user = new ApplicationUser
        {
            Id = 1,
            PhoneNumber = "0123456789",
            PasswordHash = "hashed",
            Role = UserRole.USER
        };

        var response = _mapper.Map<AuthResponse>(user);

        Assert.Equal(string.Empty, response.AccessToken);
        Assert.Equal(string.Empty, response.RefreshToken);
        Assert.False(response.OtpRequired);
    }
}
