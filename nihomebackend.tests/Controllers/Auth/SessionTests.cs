using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models.DTOs.Requests.Auth;
using NihomeBackend.Models.DTOs.Responses;
using nihomebackend.tests.Infrastructure;

namespace nihomebackend.tests.Controllers.Auth;

public sealed class SessionTests
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAuthResponse()
    {
        using var host = AuthTestHost.Create();
        var user = host.CreateUser("0900000020", "pass1234");

        var result = await host.Controller.Login(new LoginRequest
        {
            PhoneNumber = user.PhoneNumber,
            Password = "pass1234"
        });

        var response = ActionResultAssert.Ok<AuthResponse>(result);

        Assert.Equal(user.PhoneNumber, response.PhoneNumber);
        Assert.NotEmpty(response.AccessToken);
        Assert.NotEmpty(response.RefreshToken);
    }

    [Fact]
    public async Task Login_WithInactiveUser_ReturnsUnauthorized()
    {
        using var host = AuthTestHost.Create();
        var user = host.CreateUser("0900000021", "pass1234", isActive: false);

        var result = await host.Controller.Login(new LoginRequest
        {
            PhoneNumber = user.PhoneNumber,
            Password = "pass1234"
        });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Account is inactive.", ActionResultAssert.Message(unauthorized.Value));
    }

    [Fact]
    public async Task Refresh_WithValidRefreshToken_RotatesTokenAndRevokesPreviousToken()
    {
        using var host = AuthTestHost.Create();
        var user = host.CreateUser("0900000022", "pass1234");
        var refreshToken = await host.RefreshTokens.IssueAsync(user);

        var result = await host.Controller.Refresh(new RefreshRequest
        {
            RefreshToken = refreshToken.Token
        });

        var response = ActionResultAssert.Ok<AuthResponse>(result);

        Assert.True(refreshToken.IsRevoked);
        Assert.NotEqual(refreshToken.Token, response.RefreshToken);
        Assert.NotEmpty(response.AccessToken);
    }

    [Fact]
    public async Task Logout_WithValidRefreshToken_RevokesRefreshToken()
    {
        using var host = AuthTestHost.Create();
        var user = host.CreateUser("0900000023", "pass1234");
        var refreshToken = await host.RefreshTokens.IssueAsync(user);

        var result = await host.Controller.Logout(new RefreshRequest
        {
            RefreshToken = refreshToken.Token
        });

        Assert.Equal("Logout successful.", ActionResultAssert.OkMessage(result));
        Assert.True(refreshToken.IsRevoked);
    }
}
