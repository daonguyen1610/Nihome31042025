namespace NihomeBackend.Services.GoogleDrive;

/// <summary>
/// Google Drive OAuth settings. Production secrets must be supplied through protected deployment
/// configuration and must never be committed to source control or included in publish output.
/// </summary>
public sealed class GoogleDriveOptions
{
    public const string SectionName = "GoogleDrive";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string RootFolderId { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = "Nihome Survey Media";
    public string SurveyFolderName { get; set; } = "01_Khao_sat";
    public bool SupportsAllDrives { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 15;
}