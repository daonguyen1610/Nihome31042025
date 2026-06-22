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

            var result = await controller.UploadVideo(file, null, null, CancellationToken.None);

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

            var result = await controller.UploadVideo(file, null, null, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var mediaUrl = ok.Value?.GetType().GetProperty("mediaUrl")?.GetValue(ok.Value) as string;
            Assert.False(string.IsNullOrWhiteSpace(mediaUrl));
            Assert.StartsWith("/images/upload/misc/", mediaUrl!);

            var relative = mediaUrl!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(tempRoot, "wwwroot", relative);
            Assert.True(File.Exists(fullPath));
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Theory]
    [InlineData("activities")]
    [InlineData("news")]
    [InlineData("projects")]
    [InlineData("logos")]
    [InlineData("misc")]
    public async Task UploadImage_StoresFileInRequestedBucket(string bucket)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"nihome-system-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var controller = CreateController(tempRoot);
            await using var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
            var file = new FormFile(stream, 0, stream.Length, "file", "photo.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };

            var result = await controller.UploadImage(file, null, bucket, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var imageUrl = ok.Value?.GetType().GetProperty("imageUrl")?.GetValue(ok.Value) as string;
            Assert.False(string.IsNullOrWhiteSpace(imageUrl));
            Assert.StartsWith($"/images/upload/{bucket}/", imageUrl!);

            var relative = imageUrl!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(tempRoot, "wwwroot", relative);
            Assert.True(File.Exists(fullPath));
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("hackers")]
    [InlineData("../etc")]
    [InlineData("documents")]
    public async Task UploadImage_UnknownCategory_FallsBackToMisc(string? bucket)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"nihome-system-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var controller = CreateController(tempRoot);
            await using var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
            var file = new FormFile(stream, 0, stream.Length, "file", "photo.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };

            var result = await controller.UploadImage(file, null, bucket, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var imageUrl = ok.Value?.GetType().GetProperty("imageUrl")?.GetValue(ok.Value) as string;
            Assert.StartsWith("/images/upload/misc/", imageUrl!);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public async Task UploadImage_DeletesPreviousBucketedFile_OnReupload()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"nihome-system-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var controller = CreateController(tempRoot);

            // seed an existing managed file in the activities bucket
            var activitiesDir = Path.Combine(tempRoot, "wwwroot", "images", "upload", "activities");
            Directory.CreateDirectory(activitiesDir);
            var legacyName = $"{Guid.NewGuid():N}.jpg";
            var legacyPath = Path.Combine(activitiesDir, legacyName);
            await File.WriteAllBytesAsync(legacyPath, new byte[] { 1, 2, 3 });
            Assert.True(File.Exists(legacyPath));

            await using var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
            var file = new FormFile(stream, 0, stream.Length, "file", "photo.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };

            var result = await controller.UploadImage(
                file,
                $"/images/upload/activities/{legacyName}",
                "activities",
                CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);
            Assert.False(File.Exists(legacyPath));
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
