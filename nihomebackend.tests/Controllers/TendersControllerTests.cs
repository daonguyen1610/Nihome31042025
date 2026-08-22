using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NihomeBackend.Controllers;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace nihomebackend.tests.Controllers;

public class TendersControllerTests
{
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task UploadChecklistFile_WhenAttachmentOutcomeIsAmbiguous_CleansOnlyUnattachedFile(
        bool metadataCommitted, int expectedStoredFiles)
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"tender-upload-{Guid.NewGuid():N}");
        var service = new Mock<ITenderService>();
        service.Setup(item => item.AttachChecklistFileAsync(
                10, 20, It.IsAny<string>(), "checklist.pdf", 30, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Injected attachment failure"));
        service.Setup(item => item.IsChecklistFileAttachedAsync(
                10, 20, It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync(metadataCommitted);
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(item => item.ContentRootPath).Returns(contentRoot);
        var controller = new TendersController(
            service.Object,
            environment.Object,
            Mock.Of<IAuditLogger>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "30")],
                        "Test")),
                },
            },
        };
        await using var content = new MemoryStream("test content"u8.ToArray());
        var file = new FormFile(content, 0, content.Length, "file", "checklist.pdf")
        {
            Headers = new HeaderDictionary
            {
                ["Content-Type"] = "application/pdf",
            },
        };

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                controller.UploadChecklistFile(10, 20, file, CancellationToken.None));

            var uploadDirectory = Path.Combine(contentRoot, "wwwroot", "files", "tenders");
            var storedFiles = Directory.Exists(uploadDirectory)
                ? Directory.GetFiles(uploadDirectory)
                : [];
            Assert.Equal(expectedStoredFiles, storedFiles.Length);
        }
        finally
        {
            if (Directory.Exists(contentRoot)) Directory.Delete(contentRoot, recursive: true);
        }
    }
}