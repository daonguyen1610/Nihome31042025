using NihomeBackend.Models;
using NihomeBackend.Services.GoogleDrive;

namespace NihomeBackend.Services.HardDelete;

// Only empty, application-owned folder trees are eligible. Document cleanup
// remains in the owning module, so independent CRM and unknown Drive files block.
internal static class ProjectDriveDeletionPlanner
{
    public static async Task<IReadOnlyList<HardDeleteItemDefinition>> BuildAsync(
        int projectId, IReadOnlyCollection<string> bindings, IGoogleDriveAdapter drive,
        GoogleDriveOptions options, CancellationToken ct)
    {
        if (bindings.Count == 0) return [];
        if (!options.Enabled) throw new InvalidOperationException("Kết nối Drive để xác minh thư mục trước khi xóa dự án.");
        var roots = new Dictionary<string, DriveItem>(StringComparer.Ordinal);
        var projectIdentity = ProjectDriveFolderService.CreateProjectIdentity(options.InstanceId, projectId);
        foreach (var binding in bindings.Distinct(StringComparer.Ordinal))
        {
            var id = binding;
            var found = false;
            for (var depth = 0; depth < 12; depth++)
            {
                var metadata = await drive.GetMetadataAsync(id, ct);
                if (metadata is null)
                {
                    found = true; // Missing items are an idempotent deletion success.
                    break;
                }
                if (metadata.AppProperties.TryGetValue("niconFolderKind", out var kind) && kind == "project")
                {
                    DrivePermanentDeletePolicy.EnsureOwned(metadata, options.InstanceId, projectIdentity, options.RootFolderId);
                    roots[metadata.Id] = metadata;
                    found = true;
                    break;
                }
                if (metadata.Parents?.Count != 1 || metadata.Parents[0] == options.RootFolderId) break;
                DrivePermanentDeletePolicy.EnsureOwned(metadata, options.InstanceId,
                    new Dictionary<string, string> { ["niconProjectId"] = projectId.ToString(System.Globalization.CultureInfo.InvariantCulture) }, null);
                id = metadata.Parents[0];
            }
            if (!found) throw new InvalidOperationException("Không xác minh được thư mục gốc Drive của dự án.");
        }
        // A category binding may have disappeared while its project root survived.
        // Discover owned roots directly rather than leaving an orphan folder behind.
        foreach (var candidate in await drive.ListDeletionChildrenAsync(options.RootFolderId, ct))
        {
            if (candidate.AppProperties.TryGetValue("niconInstance", out var instance) && instance == options.InstanceId &&
                candidate.AppProperties.TryGetValue("niconFolderKind", out var kind) && kind == "project" &&
                candidate.AppProperties.TryGetValue("niconProjectId", out var remoteId) &&
                remoteId == projectId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            {
                DrivePermanentDeletePolicy.EnsureOwned(candidate, options.InstanceId, projectIdentity, options.RootFolderId);
                roots[candidate.Id] = candidate;
            }
        }
        var definitions = new List<HardDeleteItemDefinition>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in roots.Values.OrderBy(item => item.Id, StringComparer.Ordinal))
            await VisitAsync(root, options.RootFolderId, 0);
        return definitions;

        async Task VisitAsync(DriveItem folder, string parent, int depth)
        {
            if (depth > 16 || visited.Count >= 2000 || !visited.Add(folder.Id))
                throw new InvalidOperationException("Cây thư mục Drive vượt giới hạn hoặc có liên kết trùng; cần kiểm tra thủ công.");
            var expected = depth == 0 ? projectIdentity : new Dictionary<string, string>
            {
                ["niconInstance"] = options.InstanceId,
                ["niconProjectId"] = projectId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["niconFolderKind"] = "project-path",
            };
            if (folder.MimeType != "application/vnd.google-apps.folder")
                throw new InvalidOperationException("Thư mục Drive còn tệp; hãy dọn tệp tại phân hệ nguồn trước khi xóa dự án.");
            DrivePermanentDeletePolicy.EnsureOwned(folder, options.InstanceId, expected, parent);
            foreach (var child in (await drive.ListDeletionChildrenAsync(folder.Id, ct)).OrderBy(item => item.Id, StringComparer.Ordinal))
                await VisitAsync(child, folder.Id, depth + 1);
            definitions.Add(new HardDeleteItemDefinition(HardDeleteItemKind.DriveFolder, folder.Id,
                definitions.Count, expected, parent));
        }
    }
}
