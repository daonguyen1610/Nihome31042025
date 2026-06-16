using System.Collections.Frozen;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NihomeBackend.Authorization;
using NihomeBackend.Services;

namespace nihomebackend.tests.Authorization;

public class PermissionAuthorizationFilterTests
{
    [Fact]
    public async Task NoAttributes_DoesNothing()
    {
        var ctx = MakeContext(typeof(NoPermController), nameof(NoPermController.Open), authenticated: false);
        await new PermissionAuthorizationFilter().OnAuthorizationAsync(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task AllowAnonymous_BypassesClassAttribute()
    {
        var ctx = MakeContext(typeof(GuardedController), nameof(GuardedController.PublicRead), authenticated: false);
        await new PermissionAuthorizationFilter().OnAuthorizationAsync(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task Unauthenticated_WithRequirement_Returns401()
    {
        var ctx = MakeContext(typeof(GuardedController), nameof(GuardedController.Read), authenticated: false);
        await new PermissionAuthorizationFilter().OnAuthorizationAsync(ctx);
        Assert.IsType<UnauthorizedResult>(ctx.Result);
    }

    [Fact]
    public async Task Authenticated_NoUserIdClaim_Returns403()
    {
        var ctx = MakeContext(typeof(GuardedController), nameof(GuardedController.Read), authenticated: true, userId: null);
        await new PermissionAuthorizationFilter().OnAuthorizationAsync(ctx);
        Assert.IsType<ForbidResult>(ctx.Result);
    }

    [Fact]
    public async Task Authenticated_MissingPermission_Returns403()
    {
        var perms = new Mock<IPermissionService>();
        perms.Setup(p => p.GetForUserAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptySet);
        var ctx = MakeContext(typeof(GuardedController), nameof(GuardedController.Read),
            authenticated: true, userId: 42, perms: perms.Object);

        await new PermissionAuthorizationFilter().OnAuthorizationAsync(ctx);
        Assert.IsType<ForbidResult>(ctx.Result);
    }

    [Fact]
    public async Task Authenticated_HasPermission_Continues()
    {
        var perms = new Mock<IPermissionService>();
        perms.Setup(p => p.GetForUserAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SetOf("content.news.view"));
        var ctx = MakeContext(typeof(GuardedController), nameof(GuardedController.Read),
            authenticated: true, userId: 42, perms: perms.Object);

        await new PermissionAuthorizationFilter().OnAuthorizationAsync(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task ClassAndMethodAttributes_BothRequired_All()
    {
        var perms = new Mock<IPermissionService>();
        perms.Setup(p => p.GetForUserAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SetOf("content.news.view")); // missing manage
        var ctx = MakeContext(typeof(GuardedController), nameof(GuardedController.Write),
            authenticated: true, userId: 42, perms: perms.Object);

        await new PermissionAuthorizationFilter().OnAuthorizationAsync(ctx);
        Assert.IsType<ForbidResult>(ctx.Result);

        // Add the second permission → continues.
        perms.Setup(p => p.GetForUserAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SetOf("content.news.view", "content.news.manage"));
        var ctx2 = MakeContext(typeof(GuardedController), nameof(GuardedController.Write),
            authenticated: true, userId: 42, perms: perms.Object);
        await new PermissionAuthorizationFilter().OnAuthorizationAsync(ctx2);
        Assert.Null(ctx2.Result);
    }

    [Fact]
    public async Task UidClaimFallback_Honored()
    {
        var perms = new Mock<IPermissionService>();
        perms.Setup(p => p.GetForUserAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SetOf("content.news.view"));
        var ctx = MakeContext(typeof(GuardedController), nameof(GuardedController.Read),
            authenticated: true, userId: 7, claimType: "uid", perms: perms.Object);

        await new PermissionAuthorizationFilter().OnAuthorizationAsync(ctx);
        Assert.Null(ctx.Result);
    }

    // ---- helpers ----

    private static IReadOnlySet<string> EmptySet =
        FrozenSet<string>.Empty.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlySet<string> SetOf(params string[] codes) =>
        codes.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static AuthorizationFilterContext MakeContext(
        Type controllerType,
        string methodName,
        bool authenticated,
        int? userId = null,
        string claimType = ClaimTypes.NameIdentifier,
        IPermissionService? perms = null)
    {
        var method = controllerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException(methodName);

        var descriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = controllerType.GetTypeInfo(),
            MethodInfo = method,
            ControllerName = controllerType.Name,
            ActionName = methodName,
            DisplayName = $"{controllerType.Name}.{methodName}",
        };

        var services = new ServiceCollection();
        services.AddSingleton(perms ?? Mock.Of<IPermissionService>());
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        if (authenticated)
        {
            var claims = new List<Claim>();
            if (userId.HasValue) claims.Add(new Claim(claimType, userId.Value.ToString()));
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), descriptor);
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    public class NoPermController
    {
        public void Open() { }
    }

    [RequirePermission("content.news", "view")]
    public class GuardedController
    {
        [AllowAnonymous]
        public void PublicRead() { }

        public void Read() { }

        [RequirePermission("content.news", "manage")]
        public void Write() { }
    }
}
