using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services.GoogleDrive;

public interface IProjectDriveFolderService
{
    Task<ProjectDriveFolder> EnsureAsync(
        OperationalProject project,
        ProjectDocumentCategory category,
        int? userId = null,
        CancellationToken ct = default);

    /// <summary>Creates the whole project folder tree and binds every category; safe to repeat.</summary>
    /// <param name="requireEnabled">False only for the tree migration, which runs while sync is off.</param>
    Task EnsureTreeAsync(OperationalProject project, int? userId = null, CancellationToken ct = default,
        bool requireEnabled = true);
}

public sealed class ProjectDriveFolderService(
    AppDbContext db,
    IGoogleDriveAdapter drive,
    IGoogleDriveSettingsStore settingsStore) : IProjectDriveFolderService
{
    public async Task<ProjectDriveFolder> EnsureAsync(
        OperationalProject project,
        ProjectDocumentCategory category,
        int? userId = null,
        CancellationToken ct = default)
    {
        var options = await settingsStore.GetRuntimeAsync(ct);
        if (category == ProjectDocumentCategory.Unclassified)
            throw new ProjectDocumentValidationException("Tệp chưa phân loại không thể được lưu vào thư mục dự án.");
        var existing = await db.ProjectDriveFolders.AsNoTracking().FirstOrDefaultAsync(folder =>
            folder.OperationalProjectId == project.Id && folder.Category == category, ct);
        if (existing is not null) return existing;

        var remote = await drive.EnsureFolderPathAsync(
            Segments(options, project, options.Folders.SegmentsFor(category)), ct);
        var now = DateTime.UtcNow;
        var binding = new ProjectDriveFolder
        {
            OperationalProjectId = project.Id,
            Category = category,
            DriveFolderId = remote.Id,
            DriveWebViewLink = remote.Link,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId,
        };
        db.ProjectDriveFolders.Add(binding);
        try
        {
            await db.SaveChangesAsync(ct);
            return binding;
        }
        catch (DbUpdateException)
        {
            db.Entry(binding).State = EntityState.Detached;
            return await db.ProjectDriveFolders.AsNoTracking().SingleAsync(folder =>
                folder.OperationalProjectId == project.Id && folder.Category == category, ct);
        }
    }

    public async Task EnsureTreeAsync(OperationalProject project, int? userId = null, CancellationToken ct = default,
        bool requireEnabled = true)
    {
        var options = await settingsStore.GetRuntimeAsync(ct);
        if ((requireEnabled && !options.Enabled) || string.IsNullOrWhiteSpace(options.RefreshToken) || string.IsNullOrWhiteSpace(options.RootFolderId))
            throw new ProjectDriveNotConnectedException();
        foreach (var category in Enum.GetValues<ProjectDocumentCategory>()
                     .Where(category => category != ProjectDocumentCategory.Unclassified))
            await EnsureAsync(project, category, userId, ct);
        // Folders no category uses yet are found again by their identity, so they are not stored.
        foreach (var path in options.Folders.ExtraTreePaths())
            await drive.EnsureFolderPathAsync(Segments(options, project, path.Split('/')), ct);
    }

    private static List<DriveFolderSegment> Segments(
        GoogleDriveOptions options, OperationalProject project, IEnumerable<string> names)
    {
        var segments = new List<DriveFolderSegment>
        {
            new(SafeFolderName($"{project.Code}_{project.Name}"), CreateProjectIdentity(options.InstanceId, project.Id)),
        };
        var path = new List<string>();
        foreach (var name in names)
        {
            path.Add(name);
            segments.Add(new DriveFolderSegment(name,
                CreatePathIdentity(options.InstanceId, project.Id, string.Join('/', path))));
        }
        return segments;
    }

    public static IReadOnlyDictionary<string, string> CreateProjectIdentity(
        string instanceId,
        int projectId) => new Dictionary<string, string>
        {
            ["niconInstance"] = instanceId,
            ["niconFolderKind"] = "project",
            ["niconProjectId"] = projectId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["niconCategory"] = string.Empty,
            ["niconFolderDepth"] = "0",
        };

    public static IReadOnlyDictionary<string, string> CreatePathIdentity(
        string instanceId,
        int projectId,
        string path) => new Dictionary<string, string>
        {
            ["niconInstance"] = instanceId,
            ["niconFolderKind"] = "project-path",
            ["niconProjectId"] = projectId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["niconFolderPath"] = path,
        };

    internal static string SafeFolderName(string value) => string.Join("-", value.Split(
        Path.GetInvalidFileNameChars().Concat(['/']).ToArray(),
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

public sealed class ProjectDriveNotConnectedException()
    : InvalidOperationException("Google Drive chưa được bật hoặc chưa kết nối tài khoản.");
