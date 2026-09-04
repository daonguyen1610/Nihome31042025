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

        var projectFolder = SafeFolderName($"{project.Code}_{project.Name}");
        var segments = new List<DriveFolderSegment>
        {
            new(projectFolder, CreateProjectIdentity(options.InstanceId, project.Id)),
        };
        var categoryPath = new List<string>();
        foreach (var name in options.Folders.SegmentsFor(category))
        {
            categoryPath.Add(name);
            segments.Add(new DriveFolderSegment(name,
                CreatePathIdentity(options.InstanceId, project.Id, string.Join('/', categoryPath))));
        }
        var remote = await drive.EnsureFolderPathAsync(segments, ct);
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

    private static string SafeFolderName(string value) => string.Join("-", value.Split(
        Path.GetInvalidFileNameChars().Concat(['/']).ToArray(),
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}