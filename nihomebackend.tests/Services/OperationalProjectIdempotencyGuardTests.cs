using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Moq;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class OperationalProjectIdempotencyGuardTests
{
    [Theory]
    [InlineData(false, true, "Design", true)]
    [InlineData(false, true, "General", false)]
    [InlineData(false, false, "Design", false)]
    [InlineData(true, false, "General", true)]
    public async Task UploadReplay_RechecksCurrentPermissionAndSourceScope(
        bool operationsManager, bool designManager, string module, bool allowed)
    {
        var projects = new Mock<IOperationalProjectService>();
        var permissions = new Mock<IPermissionService>();
        var access = new Mock<IProjectAccessService>();
        permissions.Setup(item => item.HasAsync(7, "operations.projects.manage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(operationsManager);
        access.Setup(item => item.CanManageDesignDocumentsAsync(7, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(designManager);
        projects.Setup(item => item.GetAsync(42, 7, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationalProjectResponse { Id = 42 });
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "7")], "test")),
        };
        context.Request.RouteValues["id"] = 42;
        context.Request.ContentType = "multipart/form-data; boundary=nicon-upload";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues> { ["sourceModule"] = module });
        var guard = new OperationalProjectIdempotencyGuard(projects.Object, permissions.Object, access.Object);

        var rejection = await guard.ValidateAsync(context);

        if (allowed) Assert.Null(rejection);
        else
        {
            Assert.IsType<ForbidResult>(rejection);
            projects.Verify(item => item.GetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
