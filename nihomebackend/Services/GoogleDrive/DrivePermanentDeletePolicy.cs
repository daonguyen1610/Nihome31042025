namespace NihomeBackend.Services.GoogleDrive;

public sealed record DrivePermanentDeleteRequest(
    string FileId,
    IReadOnlyDictionary<string, string> ExpectedAppProperties,
    string? ExpectedParentId = null);

public sealed class DrivePermanentDeleteRejectedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

internal static class DrivePermanentDeletePolicy
{
    public static void EnsureOwned(
        DriveItem item,
        string instanceId,
        IReadOnlyDictionary<string, string> expectedAppProperties,
        string? expectedParentId)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || expectedAppProperties.Count == 0)
            throw Rejected("drive_ownership_unknown");
        if (item.IsOwnedByMe != true || item.CanDelete != true)
            throw Rejected("drive_not_exclusively_owned");

        var hasInstance = TryGetProperty(item.AppProperties, "niconInstance", out var actualInstance) ||
            TryGetProperty(item.AppProperties, "nihomeInstance", out actualInstance);
        if (!hasInstance || !string.Equals(actualInstance, instanceId, StringComparison.Ordinal))
            throw Rejected("drive_instance_mismatch");

        foreach (var expected in expectedAppProperties)
        {
            if (!TryGetProperty(item.AppProperties, expected.Key, out var actual) ||
                !string.Equals(actual, expected.Value, StringComparison.Ordinal))
            {
                throw Rejected("drive_properties_mismatch");
            }
        }

        if (!string.IsNullOrWhiteSpace(expectedParentId) &&
            item.Parents?.Contains(expectedParentId, StringComparer.Ordinal) != true)
        {
            throw Rejected("drive_parent_mismatch");
        }
    }

    private static bool TryGetProperty(
        IReadOnlyDictionary<string, string> properties, string key, out string value) =>
        properties.TryGetValue(key, out value!);

    private static DrivePermanentDeleteRejectedException Rejected(string code) => new(
        code,
        "Không thể xóa vĩnh viễn mục Google Drive vì quyền sở hữu hoặc nguồn gốc không được xác minh.");
}