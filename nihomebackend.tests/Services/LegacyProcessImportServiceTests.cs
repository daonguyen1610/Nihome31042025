using NihomeBackend.Services;
using Xunit;

namespace nihomebackend.tests.Services;

public class LegacyProcessImportServiceTests
{
    [Fact]
    public void ParseLegacyPage_ReadsShowHideProcessSections()
    {
        const string html = """
            <div class="content">
              <a class="showhideTable">Quy trình kiểm soát tài liệu</a>
              <div class="showhide">
                <img src="/Themes/Nicon/Content/file/kstl0001.jpg" alt="Quy trình kiểm soát tài liệu">
                <table>
                  <tr>
                    <td>BLĐ-QT01.doc</td>
                    <td><a href="/Admin/GeneralProcess/DownloadFile?fileName=BL%C4%90-QT01.doc">Download</a></td>
                  </tr>
                </table>
              </div>
            </div>
            """;

        var result = LegacyProcessImportService.ParseLegacyPage("general", "GeneralProcess", html);

        var process = Assert.Single(result);
        Assert.Equal("Quy trình kiểm soát tài liệu", process.Title);
        Assert.Single(process.Images);
        Assert.Equal("/Themes/Nicon/Content/file/kstl0001.jpg", process.Images[0].Url);
        Assert.Single(process.Files);
        Assert.Equal("BLĐ-QT01.doc", process.Files[0].DisplayName);
    }

    [Fact]
    public void ParseLegacyPage_ReadsSingleHeadingProcessPage()
    {
        const string html = """
            <div class="content">
              <h2>Quy trình đấu thầu</h2>
              <div class="panel panel-default">
                <img src="/Themes/Nicon/Content/file/0005.jpg" alt="Quy trình đấu thầu">
              </div>
              <table>
                <tr>
                  <td>DT-M01-Báo giá thiết kế.xls</td>
                  <td><a href="/Admin/DTProcess/DownloadFile?fileName=DT-M01.xls">Download</a></td>
                </tr>
              </table>
            </div>
            <div class="main-footer"></div>
            """;

        var result = LegacyProcessImportService.ParseLegacyPage("dt", "DTProcess", html);

        var process = Assert.Single(result);
        Assert.Equal("Quy trình đấu thầu", process.Title);
        Assert.Single(process.Images);
        Assert.Single(process.Files);
    }

    [Fact]
    public void ParseLegacyPage_SkipsUnsupportedFileSchemeAssets()
    {
        const string html = """
            <div class="content">
              <a class="showhideTable">Quy trình kiểm soát tài liệu</a>
              <div class="showhide">
                <img src="file:///C:/Themes/Nicon/Content/file/kstl0001.jpg" alt="Unsupported image">
                <img src="/Themes/Nicon/Content/file/kstl0002.jpg" alt="Valid image">
                <table>
                  <tr>
                    <td>Local file.doc</td>
                    <td><a href="file:///C:/legacy/Local file.doc">Download</a></td>
                  </tr>
                  <tr>
                    <td>Valid file.doc</td>
                    <td><a href="/Admin/GeneralProcess/DownloadFile?fileName=Valid.doc">Download</a></td>
                  </tr>
                </table>
              </div>
            </div>
            """;

        var result = LegacyProcessImportService.ParseLegacyPage("general", "GeneralProcess", html);

        var process = Assert.Single(result);
        var image = Assert.Single(process.Images);
        var file = Assert.Single(process.Files);
        Assert.Equal("/Themes/Nicon/Content/file/kstl0002.jpg", image.Url);
        Assert.Equal("Valid file.doc", file.DisplayName);
    }
}
