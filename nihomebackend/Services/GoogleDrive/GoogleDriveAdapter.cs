using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google;
using Google.Apis.Services;
using GoogleFile = Google.Apis.Drive.v3.Data.File;

namespace NihomeBackend.Services.GoogleDrive;

public sealed record DriveFolder(string Id, string Link);
public sealed record DriveUpload(string FileId);
public sealed record DriveConnection(
    string? AccountEmail,
    string FolderName,
    string FolderLink,
    bool IsFolder,
    bool IsTrashed,
    bool IsSharedDrive,
    bool CanAddChildren);

public interface IGoogleDriveAdapter
{
    Task<DriveConnection> CheckConnectionAsync(CancellationToken ct = default);
    Task<DriveFolder> EnsureFolderPathAsync(IReadOnlyList<string> folderNames, CancellationToken ct = default);
    Task<DriveUpload> UploadAsync(
        string folderId,
        long surveyMediaId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default);
    Task DeleteAsync(string fileId, CancellationToken ct = default);
}

/// <summary>
/// Creates the Drive client lazily. Folder operations always anchor at the configured root and set
/// Shared Drive flags where Drive permits.
/// </summary>
public sealed class GoogleDriveAdapter(GoogleDriveOptions options) : IGoogleDriveAdapter, IDisposable
{
    private DriveService? service;

    public async Task<DriveConnection> CheckConnectionAsync(CancellationToken ct = default)
    {
        var aboutRequest = Service.About.Get();
        aboutRequest.Fields = "user(emailAddress)";
        var about = await aboutRequest.ExecuteAsync(ct);

        var request = Service.Files.Get(options.RootFolderId);
        request.Fields = "id,name,mimeType,trashed,driveId,webViewLink,capabilities(canAddChildren)";
        request.SupportsAllDrives = options.SupportsAllDrives;
        var folder = await request.ExecuteAsync(ct);
        return new DriveConnection(
            about.User?.EmailAddress,
            folder.Name ?? options.RootFolderId,
            folder.WebViewLink ?? $"https://drive.google.com/drive/folders/{folder.Id}",
            string.Equals(folder.MimeType, "application/vnd.google-apps.folder", StringComparison.Ordinal),
            folder.Trashed == true,
            !string.IsNullOrWhiteSpace(folder.DriveId),
            folder.Capabilities?.CanAddChildren == true);
    }

    public async Task<DriveFolder> EnsureFolderPathAsync(
        IReadOnlyList<string> folderNames, CancellationToken ct = default)
    {
        var parentId = options.RootFolderId;
        foreach (var folderName in folderNames)
        {
            parentId = await FindOrCreateFolderAsync(parentId, folderName, ct);
        }
        return new DriveFolder(parentId, $"https://drive.google.com/drive/folders/{parentId}");
    }

    public async Task<DriveUpload> UploadAsync(
        string folderId,
        long surveyMediaId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default)
    {
        var idempotencyValue = surveyMediaId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var existingFileId = await FindUploadedFileAsync(folderId, idempotencyValue, ct);
        if (existingFileId is not null) return new DriveUpload(existingFileId);

        var metadata = new GoogleFile
        {
            Name = fileName,
            Parents = [folderId],
            AppProperties = new Dictionary<string, string>
            {
                ["nihomeSurveyMediaId"] = idempotencyValue,
            },
        };
        var request = Service.Files.Create(metadata, content, contentType);
        request.Fields = "id";
        request.SupportsAllDrives = options.SupportsAllDrives;
        var progress = await request.UploadAsync(ct);
        if (progress.Status != Google.Apis.Upload.UploadStatus.Completed || request.ResponseBody?.Id is null)
        {
            throw progress.Exception ?? new InvalidOperationException("Google Drive không trả về mã tệp sau khi tải lên.");
        }
        return new DriveUpload(request.ResponseBody.Id);
    }

    private async Task<string?> FindUploadedFileAsync(
        string folderId, string idempotencyValue, CancellationToken ct)
    {
        var list = Service.Files.List();
        list.Q = $"'{EscapeQueryValue(folderId)}' in parents and " +
            $"appProperties has {{ key='nihomeSurveyMediaId' and value='{EscapeQueryValue(idempotencyValue)}' }} and " +
            "trashed = false";
        list.Fields = "files(id)";
        list.PageSize = 1;
        list.SupportsAllDrives = options.SupportsAllDrives;
        list.IncludeItemsFromAllDrives = options.SupportsAllDrives;
        var found = await list.ExecuteAsync(ct);
        return found.Files?.FirstOrDefault()?.Id;
    }

    public async Task DeleteAsync(string fileId, CancellationToken ct = default)
    {
        var request = Service.Files.Delete(fileId);
        request.SupportsAllDrives = options.SupportsAllDrives;
        try
        {
            await request.ExecuteAsync(ct);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
        }
    }

    private async Task<string> FindOrCreateFolderAsync(string parentId, string folderName, CancellationToken ct)
    {
        var escapedName = EscapeQueryValue(folderName);
        var list = Service.Files.List();
        list.Q = $"name = '{escapedName}' and '{EscapeQueryValue(parentId)}' in parents and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        list.Fields = "files(id,name)";
        list.PageSize = 2;
        list.SupportsAllDrives = options.SupportsAllDrives;
        list.IncludeItemsFromAllDrives = options.SupportsAllDrives;
        var found = await list.ExecuteAsync(ct);
        var existing = found.Files?.FirstOrDefault();
        if (existing?.Id is not null) return existing.Id;

        var create = Service.Files.Create(new GoogleFile
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = [parentId],
        });
        create.Fields = "id";
        create.SupportsAllDrives = options.SupportsAllDrives;
        return (await create.ExecuteAsync(ct)).Id;
    }

    private static string EscapeQueryValue(string value) =>
        value.Replace("\\", "\\\\").Replace("'", "\\'");

    private DriveService Service => service ??= CreateService();

    private DriveService CreateService()
    {
        if (string.IsNullOrWhiteSpace(options.ClientId) ||
            string.IsNullOrWhiteSpace(options.ClientSecret) ||
            string.IsNullOrWhiteSpace(options.RefreshToken))
        {
            throw new InvalidOperationException(
                "GoogleDrive:ClientId, ClientSecret và RefreshToken phải được cấu hình đầy đủ.");
        }
        if (string.IsNullOrWhiteSpace(options.RootFolderId))
        {
            throw new InvalidOperationException("GoogleDrive:RootFolderId chưa được cấu hình.");
        }

        var credentialJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "authorized_user",
            client_id = options.ClientId,
            client_secret = options.ClientSecret,
            refresh_token = options.RefreshToken,
        });
        var credential = GoogleCredential.FromJson(credentialJson)
            .CreateScoped(DriveService.Scope.Drive);
        if (credential.UnderlyingCredential is not UserCredential)
        {
            throw new InvalidOperationException(
                "Google Drive không thể khởi tạo thông tin xác thực OAuth của người dùng.");
        }
        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = options.ApplicationName,
        });
    }

    public void Dispose() => service?.Dispose();
}