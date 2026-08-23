using Microsoft.Extensions.Configuration;
using NihomeBackend.Services.GoogleDrive;

namespace nihomebackend.tests.Services;

public sealed class GoogleDriveOptionsTests
{
    [Fact]
    public void Configuration_BindsModuleFolderRegistry()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleDrive:Folders:SurveyMedia"] = "survey-media-folder",
            })
            .Build();

        var options = configuration
            .GetSection(GoogleDriveOptions.SectionName)
            .Get<GoogleDriveOptions>();

        Assert.NotNull(options);
        Assert.Equal("survey-media-folder", options.Folders.SurveyMedia);
    }
}
