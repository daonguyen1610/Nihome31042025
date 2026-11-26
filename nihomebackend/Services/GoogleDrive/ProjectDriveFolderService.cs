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
    Task EnsureTreeAsync(OperationalProject project, int? userId = null, CancellationToken ct = default);

    /// <summary>
    /// Administrator action: forgets bound folders that are gone from Drive, then completes the tree. Nothing
    /// on Drive is deleted, so a trashed folder can still be restored by hand.
    /// </summary>
    Task RepairTreeAsync(OperationalProject project, int? userId = null, CancellationToken ct = default);
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
        if (existing is not null)
        {
            await EnsureStillOnDriveAsync(existing, ct);
            return existing;
        }

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

    public async Task EnsureTreeAsync(OperationalProject project, int? userId = null, CancellationToken ct = default)
    {
        var options = await settingsStore.GetRuntimeAsync(ct);
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.RefreshToken) || string.IsNullOrWhiteSpace(options.RootFolderId))
            throw new ProjectDriveNotConnectedException();
        var bound = await db.ProjectDriveFolders.AsNoTracking()
            .Where(folder => folder.OperationalProjectId == project.Id).Select(folder => folder.Category).ToListAsync(ct);
        foreach (var category in Enum.GetValues<ProjectDocumentCategory>()
                     .Where(category => category != ProjectDocumentCategory.Unclassified).Except(bound))
            await EnsureAsync(project, category, userId, ct);
    }

    public async Task RepairTreeAsync(OperationalProject project, int? userId = null, CancellationToken ct = default)
    {
        var options = await settingsStore.GetRuntimeAsync(ct);
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.RefreshToken) || string.IsNullOrWhiteSpace(options.RootFolderId))
            throw new ProjectDriveNotConnectedException();
        // With the wrong account every folder looks gone, so no link is dropped before the root is writable.
        var root = await drive.CheckConnectionAsync(ct);
        if (!root.IsFolder || root.IsTrashed || !root.CanAddChildren) throw new ProjectDriveNotConnectedException();
        var bindings = await db.ProjectDriveFolders
            .Where(folder => folder.OperationalProjectId == project.Id).ToListAsync(ct);
        foreach (var binding in bindings)
        {
            if (await IsOnDriveAsync(binding.DriveFolderId, ct)) binding.MissingSince = null;
            else db.ProjectDriveFolders.Remove(binding);
        }
        await db.SaveChangesAsync(ct);
        await EnsureTreeAsync(project, userId, ct);
    }

    private async Task<bool> IsOnDriveAsync(string folderId, CancellationToken ct) =>
        await drive.GetMetadataAsync(folderId, ct) is { IsTrashed: false };

    // A file sent into a trashed folder lands in the trash while looking synced.
    private async Task EnsureStillOnDriveAsync(ProjectDriveFolder binding, CancellationToken ct)
    {
        var onDrive = await IsOnDriveAsync(binding.DriveFolderId, ct);
        if (onDrive != binding.MissingSince.HasValue)
        {
            if (!onDrive) throw new ProjectDriveFolderMissingException();
            return;
        }
        // A bulk update, so the shared context and the reconciliation lease are not disturbed.
        var missingSince = onDrive ? (DateTime?)null : DateTime.UtcNow;
        await db.ProjectDriveFolders.Where(folder => folder.Id == binding.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(folder => folder.MissingSince, missingSince), ct);
        binding.MissingSince = missingSince;
        if (!onDrive) throw new ProjectDriveFolderMissingException();
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

public sealed class ProjectDriveFolderMissingException()
    : InvalidOperationException("Thư mục đích trên Google Drive đã bị xoá. Hãy khôi phục thư mục từ thùng rác hoặc tạo lại thư mục của dự án.");
