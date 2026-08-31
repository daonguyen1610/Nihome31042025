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
    GoogleDriveOptions options) : IProjectDriveFolderService
{
    public async Task<ProjectDriveFolder> EnsureAsync(
        OperationalProject project,
        ProjectDocumentCategory category,
        int? userId = null,
        CancellationToken ct = default)
    {
        if (category == ProjectDocumentCategory.Unclassified)
            throw new ProjectDocumentValidationException("Tệp chưa phân loại không thể được lưu vào thư mục dự án.");
        var existing = await db.ProjectDriveFolders.AsNoTracking().FirstOrDefaultAsync(folder =>
            folder.OperationalProjectId == project.Id && folder.Category == category, ct);
        if (existing is not null) return existing;

        var projectFolder = SafeFolderName($"{project.Code}_{project.Name}");
        var segments = new List<DriveFolderSegment>
        {
            new(projectFolder, FolderIdentity("project", project.Id, null, 0)),
        };
        var categoryPath = new List<string>();
        foreach (var name in options.Folders.SegmentsFor(category))
        {
            categoryPath.Add(name);
            segments.Add(new DriveFolderSegment(name,
                PathIdentity(project.Id, string.Join('/', categoryPath))));
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

    private Dictionary<string, string> FolderIdentity(
        string kind,
        int projectId,
        ProjectDocumentCategory? category,
        int depth) => new()
        {
            ["niconInstance"] = options.InstanceId,
            ["niconFolderKind"] = kind,
            ["niconProjectId"] = projectId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["niconCategory"] = category?.ToString() ?? string.Empty,
            ["niconFolderDepth"] = depth.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

    private Dictionary<string, string> PathIdentity(int projectId, string path) => new()
    {
        ["niconInstance"] = options.InstanceId,
        ["niconFolderKind"] = "project-path",
        ["niconProjectId"] = projectId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["niconFolderPath"] = path,
    };

    private static string SafeFolderName(string value) => string.Join("-", value.Split(
        Path.GetInvalidFileNameChars().Concat(['/']).ToArray(),
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}