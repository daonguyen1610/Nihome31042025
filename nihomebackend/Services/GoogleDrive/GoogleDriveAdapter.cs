using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google;
using Google.Apis.Services;
using GoogleFile = Google.Apis.Drive.v3.Data.File;

namespace NihomeBackend.Services.GoogleDrive;

public sealed record DriveFolder(string Id, string Link);
public sealed record DriveFolderSegment(string Name, IReadOnlyDictionary<string, string> AppProperties);
public sealed record DriveUpload(string FileId, string? Version = null, DateTime? ModifiedAt = null, string? Link = null);
public sealed record DriveItem(
    string Id,
    string Name,
    string MimeType,
    long? Size,
    string? Version,
    DateTime? ModifiedAt,
    string? Link,
    IReadOnlyDictionary<string, string> AppProperties,
    bool IsTrashed,
    IReadOnlyList<string>? Parents = null,
    bool? IsOwnedByMe = null,
    bool? CanDelete = null);
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
    Task<DriveFolder> EnsureFolderPathAsync(IReadOnlyList<DriveFolderSegment> folders, CancellationToken ct = default);
    Task<DriveUpload> UploadAsync(
        string folderId,
        long surveyMediaId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default);
    Task<DriveUpload> UploadAsync(
        string folderId,
        string replicaKey,
        long generation,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default);
    Task<IReadOnlyList<DriveItem>> ListChildrenAsync(string folderId, CancellationToken ct = default);
    Task DownloadAsync(string fileId, Stream destination, CancellationToken ct = default);
    Task<DriveItem?> GetMetadataAsync(string fileId, CancellationToken ct = default);
    Task UpdateFileNameAsync(string fileId, string fileName, CancellationToken ct = default);
    Task MoveAsync(string fileId, string destinationFolderId, CancellationToken ct = default);
    Task DeleteAsync(string fileId, CancellationToken ct = default);
    Task PermanentDeleteOwnedAsync(DrivePermanentDeleteRequest request, CancellationToken ct = default);
}

/// <summary>
/// Creates the Drive client lazily. Folder operations always anchor at the configured root and set
/// Shared Drive flags where Drive permits.
/// </summary>
public sealed class GoogleDriveAdapter(
    IGoogleDriveSettingsStore settingsStore) : IGoogleDriveAdapter, IDisposable
{
    private DriveService? service;
    private GoogleDriveOptions options = new();
    private readonly SemaphoreSlim serviceLock = new(1, 1);

    public async Task<DriveConnection> CheckConnectionAsync(CancellationToken ct = default)
    {
        var aboutRequest = (await GetServiceAsync(ct)).About.Get();
        aboutRequest.Fields = "user(emailAddress)";
        var about = await aboutRequest.ExecuteAsync(ct);

        var request = (await GetServiceAsync(ct)).Files.Get(options.RootFolderId);
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
        => await EnsureFolderPathAsync(folderNames.Select(name =>
            new DriveFolderSegment(name, new Dictionary<string, string>())).ToList(), ct);

    public async Task<DriveFolder> EnsureFolderPathAsync(
        IReadOnlyList<DriveFolderSegment> folders, CancellationToken ct = default)
    {
        await GetServiceAsync(ct);
        var parentId = options.RootFolderId;
        foreach (var folder in folders)
        {
            parentId = await FindOrCreateFolderAsync(parentId, folder, ct);
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
                ["niconSurveyMediaId"] = idempotencyValue,
            },
        };
        var request = (await GetServiceAsync(ct)).Files.Create(metadata, content, contentType);
        request.Fields = "id";
        request.SupportsAllDrives = options.SupportsAllDrives;
        var progress = await request.UploadAsync(ct);
        if (progress.Status != Google.Apis.Upload.UploadStatus.Completed || request.ResponseBody?.Id is null)
        {
            throw progress.Exception ?? new InvalidOperationException("Google Drive không trả về mã tệp sau khi tải lên.");
        }
        return new DriveUpload(request.ResponseBody.Id);
    }

    public async Task<DriveUpload> UploadAsync(
        string folderId,
        string replicaKey,
        long generation,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default)
    {
        var generationValue = generation.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var existing = await FindReplicaAsync(folderId, replicaKey, generationValue, ct);
        if (existing is not null) return ToUpload(existing);

        var metadata = new GoogleFile
        {
            Name = fileName,
            Parents = [folderId],
            AppProperties = new Dictionary<string, string>
            {
                ["niconInstance"] = options.InstanceId,
                ["niconReplicaKey"] = replicaKey,
                ["niconGeneration"] = generationValue,
            },
        };
        var request = (await GetServiceAsync(ct)).Files.Create(metadata, content, contentType);
        request.Fields = ItemFields;
        request.SupportsAllDrives = options.SupportsAllDrives;
        var progress = await request.UploadAsync(ct);
        if (progress.Status != Google.Apis.Upload.UploadStatus.Completed || request.ResponseBody?.Id is null)
            throw progress.Exception ?? new InvalidOperationException("Google Drive không trả về mã tệp sau khi tải lên.");
        return ToUpload(request.ResponseBody);
    }

    public async Task<IReadOnlyList<DriveItem>> ListChildrenAsync(string folderId, CancellationToken ct = default)
    {
        var items = new List<DriveItem>();
        string? pageToken = null;
        do
        {
            var request = (await GetServiceAsync(ct)).Files.List();
            request.Q = $"'{EscapeQueryValue(folderId)}' in parents and trashed = false";
            request.Fields = $"nextPageToken,files({ItemFields})";
            request.PageSize = 1000;
            request.PageToken = pageToken;
            request.SupportsAllDrives = options.SupportsAllDrives;
            request.IncludeItemsFromAllDrives = options.SupportsAllDrives;
            var response = await request.ExecuteAsync(ct);
            items.AddRange(response.Files?.Select(ToItem) ?? []);
            pageToken = response.NextPageToken;
        } while (!string.IsNullOrWhiteSpace(pageToken));
        return items;
    }

    public async Task DownloadAsync(string fileId, Stream destination, CancellationToken ct = default)
    {
        var request = (await GetServiceAsync(ct)).Files.Get(fileId);
        request.SupportsAllDrives = options.SupportsAllDrives;
        var progress = await request.DownloadAsync(destination, ct);
        if (progress.Status != Google.Apis.Download.DownloadStatus.Completed)
            throw progress.Exception ?? new InvalidOperationException("Không thể tải tệp từ Google Drive.");
    }

    public async Task<DriveItem?> GetMetadataAsync(string fileId, CancellationToken ct = default)
    {
        var request = (await GetServiceAsync(ct)).Files.Get(fileId);
        request.Fields = ItemFields;
        request.SupportsAllDrives = options.SupportsAllDrives;
        try
        {
            return ToItem(await request.ExecuteAsync(ct));
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task UpdateFileNameAsync(string fileId, string fileName, CancellationToken ct = default)
    {
        var request = (await GetServiceAsync(ct)).Files.Update(new GoogleFile { Name = fileName }, fileId);
        request.Fields = "id,name";
        request.SupportsAllDrives = options.SupportsAllDrives;
        await request.ExecuteAsync(ct);
    }

    public async Task MoveAsync(string fileId, string destinationFolderId, CancellationToken ct = default)
    {
        var metadataRequest = (await GetServiceAsync(ct)).Files.Get(fileId);
        metadataRequest.Fields = "parents";
        metadataRequest.SupportsAllDrives = options.SupportsAllDrives;
        var metadata = await metadataRequest.ExecuteAsync(ct);
        if (metadata.Parents?.Contains(destinationFolderId, StringComparer.Ordinal) == true) return;
        var request = (await GetServiceAsync(ct)).Files.Update(new GoogleFile(), fileId);
        request.AddParents = destinationFolderId;
        request.RemoveParents = string.Join(',', metadata.Parents ?? []);
        request.Fields = ItemFields;
        request.SupportsAllDrives = options.SupportsAllDrives;
        await request.ExecuteAsync(ct);
    }

    private async Task<GoogleFile?> FindReplicaAsync(
        string folderId, string replicaKey, string generation, CancellationToken ct)
    {
        return await FindReplicaAsync(folderId, replicaKey, generation, "nicon", ct) ??
            await FindReplicaAsync(folderId, replicaKey, generation, "nihome", ct);
    }

    private async Task<GoogleFile?> FindReplicaAsync(
        string folderId,
        string replicaKey,
        string generation,
        string propertyPrefix,
        CancellationToken ct)
    {
        var list = (await GetServiceAsync(ct)).Files.List();
        list.Q = $"'{EscapeQueryValue(folderId)}' in parents and " +
            $"appProperties has {{ key='{propertyPrefix}Instance' and value='{EscapeQueryValue(options.InstanceId)}' }} and " +
            $"appProperties has {{ key='{propertyPrefix}ReplicaKey' and value='{EscapeQueryValue(replicaKey)}' }} and " +
            $"appProperties has {{ key='{propertyPrefix}Generation' and value='{EscapeQueryValue(generation)}' }} and trashed = false";
        list.Fields = $"files({ItemFields})";
        list.PageSize = 1;
        list.SupportsAllDrives = options.SupportsAllDrives;
        list.IncludeItemsFromAllDrives = options.SupportsAllDrives;
        return (await list.ExecuteAsync(ct)).Files?.FirstOrDefault();
    }

    private async Task<string?> FindUploadedFileAsync(
        string folderId, string idempotencyValue, CancellationToken ct)
    {
        return await FindUploadedFileAsync(folderId, idempotencyValue, "niconSurveyMediaId", ct) ??
            await FindUploadedFileAsync(folderId, idempotencyValue, "nihomeSurveyMediaId", ct);
    }

    private async Task<string?> FindUploadedFileAsync(
        string folderId,
        string idempotencyValue,
        string propertyName,
        CancellationToken ct)
    {
        var list = (await GetServiceAsync(ct)).Files.List();
        list.Q = $"'{EscapeQueryValue(folderId)}' in parents and " +
            $"appProperties has {{ key='{propertyName}' and value='{EscapeQueryValue(idempotencyValue)}' }} and " +
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
        var request = (await GetServiceAsync(ct)).Files.Update(new GoogleFile { Trashed = true }, fileId);
        request.Fields = "id,trashed";
        request.SupportsAllDrives = options.SupportsAllDrives;
        try
        {
            await request.ExecuteAsync(ct);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
        }
    }

    public async Task PermanentDeleteOwnedAsync(
        DrivePermanentDeleteRequest request, CancellationToken ct = default)
    {
        var metadata = await GetMetadataAsync(request.FileId, ct);
        if (metadata is null) return;
        DrivePermanentDeletePolicy.EnsureOwned(
            metadata, options.InstanceId, request.ExpectedAppProperties, request.ExpectedParentId);

        var delete = (await GetServiceAsync(ct)).Files.Delete(request.FileId);
        delete.SupportsAllDrives = options.SupportsAllDrives;
        try
        {
            await delete.ExecuteAsync(ct);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return;
        }

        if (await GetMetadataAsync(request.FileId, ct) is not null)
            throw new InvalidOperationException("Google Drive chưa xác nhận mục đã được xóa vĩnh viễn.");
    }

    private async Task<string> FindOrCreateFolderAsync(string parentId, DriveFolderSegment folder, CancellationToken ct)
    {
        var identityQuery = string.Join(" and ", folder.AppProperties.OrderBy(property => property.Key).Select(property =>
            $"appProperties has {{ key='{EscapeQueryValue(property.Key)}' and value='{EscapeQueryValue(property.Value)}' }}"));
        var list = (await GetServiceAsync(ct)).Files.List();
        list.Q = $"'{EscapeQueryValue(parentId)}' in parents and mimeType = 'application/vnd.google-apps.folder' and trashed = false" +
            (identityQuery.Length == 0 ? $" and name = '{EscapeQueryValue(folder.Name)}'" : $" and {identityQuery}");
        list.Fields = "files(id,name)";
        list.PageSize = 2;
        list.SupportsAllDrives = options.SupportsAllDrives;
        list.IncludeItemsFromAllDrives = options.SupportsAllDrives;
        var found = await list.ExecuteAsync(ct);
        var existing = found.Files?.FirstOrDefault();
        if (existing?.Id is not null) return existing.Id;

        var create = (await GetServiceAsync(ct)).Files.Create(new GoogleFile
        {
            Name = folder.Name,
            MimeType = "application/vnd.google-apps.folder",
            Parents = [parentId],
            AppProperties = folder.AppProperties.Count == 0
                ? null
                : new Dictionary<string, string>(folder.AppProperties),
        });
        create.Fields = "id";
        create.SupportsAllDrives = options.SupportsAllDrives;
        return (await create.ExecuteAsync(ct)).Id;
    }

    private static string EscapeQueryValue(string value) =>
        value.Replace("\\", "\\\\").Replace("'", "\\'");

    private const string ItemFields = "id,name,mimeType,size,version,modifiedTime,webViewLink,appProperties,trashed,parents,ownedByMe,capabilities(canDelete)";

    private static DriveItem ToItem(GoogleFile file) => new(
        file.Id, file.Name ?? string.Empty, file.MimeType ?? "application/octet-stream", file.Size,
        file.Version?.ToString(System.Globalization.CultureInfo.InvariantCulture), file.ModifiedTimeDateTimeOffset?.UtcDateTime,
        file.WebViewLink, file.AppProperties is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(file.AppProperties), file.Trashed == true,
        file.Parents?.ToList() ?? [], file.OwnedByMe, file.Capabilities?.CanDelete);

    private static DriveUpload ToUpload(GoogleFile file) => new(
        file.Id, file.Version?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        file.ModifiedTimeDateTimeOffset?.UtcDateTime, file.WebViewLink);

    private async Task<DriveService> GetServiceAsync(CancellationToken ct)
    {
        if (service is not null) return service;
        await serviceLock.WaitAsync(ct);
        try
        {
            service ??= await CreateServiceAsync(ct);
            return service;
        }
        finally
        {
            serviceLock.Release();
        }
    }

    private async Task<DriveService> CreateServiceAsync(CancellationToken ct)
    {
        options = await settingsStore.GetRuntimeAsync(ct);
        var refreshToken = options.RefreshToken;
        if (string.IsNullOrWhiteSpace(options.ClientId) ||
            string.IsNullOrWhiteSpace(options.ClientSecret) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidOperationException(
                "Google Drive chưa có thông tin xác thực hợp lệ. Hãy kết nối lại trong Cài đặt.");
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
            refresh_token = refreshToken,
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

    public void Dispose()
    {
        service?.Dispose();
        serviceLock.Dispose();
    }
}