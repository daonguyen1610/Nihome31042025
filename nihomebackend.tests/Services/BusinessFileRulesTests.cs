using Microsoft.AspNetCore.Http;
using Moq;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public class BusinessFileRulesTests
{
    private static IFormFile File(string name, long length)
    {
        var file = new Mock<IFormFile>();
        file.SetupGet(item => item.FileName).Returns(name);
        file.SetupGet(item => item.Length).Returns(length);
        return file.Object;
    }

    [Fact]
    public void AcceptsAnAllowedFileWithinTheLimit() =>
        Assert.Null(BusinessFileRules.Problem(File("KT-01 Mặt bằng.PDF", 5 * 1024 * 1024), 20));

    [Fact]
    public void NamesTheFileAndItsSizeWhenTooLarge()
    {
        var message = BusinessFileRules.Problem(File("KC-02 Kết cấu.pdf", 25L * 1024 * 1024 + 300 * 1024), 20);

        Assert.NotNull(message);
        Assert.Contains("KC-02 Kết cấu.pdf", message);
        Assert.Contains("25,3 MB", message);
        Assert.Contains("20 MB", message);
    }

    [Fact]
    public void FollowsTheConfiguredLimit()
    {
        var file = File("ban-ve-50mb.pdf", 50L * 1024 * 1024);

        Assert.NotNull(BusinessFileRules.Problem(file, 20));
        Assert.Null(BusinessFileRules.Problem(file, 60));
    }

    [Fact]
    public void SaysWhenTheFileIsEmpty() =>
        Assert.Contains("trống", BusinessFileRules.Problem(File("ghi-chu.docx", 0), 20));

    [Fact]
    public void ListsTheAcceptedTypesForAnUnsupportedFile()
    {
        var message = BusinessFileRules.Problem(File("ban-ve.dwg", 1024), 20);

        Assert.NotNull(message);
        Assert.Contains(".dwg", message);
        Assert.Contains("PDF", message);
        Assert.Contains("Excel", message);
    }
}
