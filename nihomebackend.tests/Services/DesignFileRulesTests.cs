using Microsoft.AspNetCore.Http;
using Moq;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public class DesignFileRulesTests
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
        Assert.Null(DesignFileRules.Problem(File("KT-01 Mặt bằng.PDF", 5 * 1024 * 1024)));

    [Fact]
    public void NamesTheFileAndItsSizeWhenTooLarge()
    {
        var message = DesignFileRules.Problem(File("KC-02 Kết cấu.pdf", 25L * 1024 * 1024 + 300 * 1024));

        Assert.NotNull(message);
        Assert.Contains("KC-02 Kết cấu.pdf", message);
        Assert.Contains("25,3 MB", message);
        Assert.Contains("20 MB", message);
    }

    [Fact]
    public void SaysWhenTheFileIsEmpty() =>
        Assert.Contains("trống", DesignFileRules.Problem(File("ghi-chu.docx", 0)));

    [Fact]
    public void ListsTheAcceptedTypesForAnUnsupportedFile()
    {
        var message = DesignFileRules.Problem(File("ban-ve.dwg", 1024));

        Assert.NotNull(message);
        Assert.Contains(".dwg", message);
        Assert.Contains("PDF", message);
        Assert.Contains("Excel", message);
    }
}
