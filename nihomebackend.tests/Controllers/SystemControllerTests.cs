using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using NihomeBackend.Controllers;
using NihomeBackend.Models;
using NihomeBackend.Services;
using Xunit;

namespace nihomebackend.tests.Controllers;

public class SystemControllerTests
{
    [Fact]
    public void GetHealth_ReturnsHealthResponse()
    {
        var timeService = new TimeService();
        var controller = new SystemController(timeService);

        var hostEnvMock = new Mock<IHostEnvironment>();
        hostEnvMock.Setup(h => h.EnvironmentName).Returns("Testing");

        var services = new ServiceCollection();
        services.AddSingleton(hostEnvMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = serviceProvider }
        };

        var result = controller.GetHealth();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<HealthResponse>(ok.Value);
        Assert.Equal("Nihome Backend", response.Name);
        Assert.Equal("Testing", response.Environment);
        Assert.Equal("Healthy", response.Status);
    }

    [Fact]
    public void GetHealth_TimestampUtcIsRecent()
    {
        var timeService = new TimeService();
        var controller = new SystemController(timeService);

        var hostEnvMock = new Mock<IHostEnvironment>();
        hostEnvMock.Setup(h => h.EnvironmentName).Returns("Test");

        var services = new ServiceCollection();
        services.AddSingleton(hostEnvMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() }
        };

        var before = DateTime.UtcNow;
        var result = controller.GetHealth();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<HealthResponse>(ok.Value);

        Assert.True(response.TimestampUtc >= before.AddSeconds(-1));
        Assert.True(response.TimestampUtc <= DateTime.UtcNow.AddSeconds(1));
    }
}
