using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        var webHostEnvMock = new Mock<IWebHostEnvironment>();
        webHostEnvMock.Setup(h => h.ContentRootPath).Returns("/tmp");
        var loggerMock = new Mock<ILogger<SystemController>>();
        var controller = new SystemController(timeService, webHostEnvMock.Object, loggerMock.Object);

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
        var webHostEnvMock = new Mock<IWebHostEnvironment>();
        webHostEnvMock.Setup(h => h.ContentRootPath).Returns("/tmp");
        var loggerMock = new Mock<ILogger<SystemController>>();
        var controller = new SystemController(timeService, webHostEnvMock.Object, loggerMock.Object);

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

    [Fact]
    public async Task UploadVideo_ReturnsBadRequest_ForInvalidExtension()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"nihome-system-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var controller = CreateController(tempRoot);
            await using var stream = new MemoryStream([1, 2, 3, 4]);
            var file = new FormFile(stream, 0, stream.Length, "file", "clip.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };

            var result = await controller.UploadVideo(file, null, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public async Task UploadVideo_ReturnsOk_AndStoresFile_ForValidVideo()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"nihome-system-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var controller = CreateController(tempRoot);
            await using var stream = new MemoryStream([0, 0, 0, 0, 0, 0, 0, 0]);
            var file = new FormFile(stream, 0, stream.Length, "file", "clip.mp4")
            {
                Headers = new HeaderDictionary(),
                ContentType = "video/mp4"
            };

            var result = await controller.UploadVideo(file, null, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var mediaUrl = ok.Value?.GetType().GetProperty("mediaUrl")?.GetValue(ok.Value) as string;
            Assert.False(string.IsNullOrWhiteSpace(mediaUrl));
            Assert.StartsWith("/images/upload/", mediaUrl!);

            var fileName = Path.GetFileName(mediaUrl!);
            var fullPath = Path.Combine(tempRoot, "wwwroot", "images", "upload", fileName);
            Assert.True(File.Exists(fullPath));
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    private static SystemController CreateController(string contentRootPath)
    {
        var timeService = new TimeService();
        var webHostEnvMock = new Mock<IWebHostEnvironment>();
        webHostEnvMock.Setup(h => h.ContentRootPath).Returns(contentRootPath);
        var loggerMock = new Mock<ILogger<SystemController>>();
        return new SystemController(timeService, webHostEnvMock.Object, loggerMock.Object);
    }
}
