using System.Security.Cryptography;
using System.Text;

namespace NihomeBackend.Services.HardDelete;

public sealed class HardDeleteFileOptions
{
    public IReadOnlyList<string> AllowedPrivateRoots { get; init; } =
    [
        "/files/quotes",
        "/files/customers",
        "/files/contracts",
        "/files/capability",
        "/files/tenders",
        "/files/business-documents",
        "/files/design",
        "/files/survey-media",
        "/files/project-documents",
    ];
}

public sealed record HardDeleteQuarantineResult(
    string ManagedPath,
    string? QuarantinePath,
    bool WasMissing);

public interface IHardDeleteFileService
{
    string ValidateManagedPath(string managedPath);
    Task<HardDeleteQuarantineResult> QuarantineAsync(
        Guid operationId, string managedPath, CancellationToken ct = default);
    Task RestoreAsync(string managedPath, string? quarantinePath, CancellationToken ct = default);
    Task PurgeAsync(string? quarantinePath, CancellationToken ct = default);
}

public sealed class HardDeleteFileException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class HardDeleteFileService(
    IWebHostEnvironment environment,
    HardDeleteFileOptions options) : IHardDeleteFileService
{
    private const string QuarantinePrefix = "/files/.hard-delete-quarantine";

    public string ValidateManagedPath(string managedPath)
    {
        if (string.IsNullOrWhiteSpace(managedPath) || managedPath[0] != '/' ||
            managedPath.Contains('\\') || managedPath.Contains('?') || managedPath.Contains('#') ||
            managedPath.Contains('%'))
        {
            throw InvalidPath();
        }

        var segments = managedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3 || segments.Any(segment => segment is "." or ".."))
            throw InvalidPath();

        var normalized = "/" + string.Join('/', segments);
        var allowed = options.AllowedPrivateRoots.Any(root =>
            normalized.StartsWith(root + "/", StringComparison.Ordinal));
        if (!allowed || normalized.StartsWith(QuarantinePrefix + "/", StringComparison.Ordinal))
            throw InvalidPath();

        var fullPath = ResolveUnderWebRoot(normalized);
        EnsureNoSymbolicLink(fullPath);
        return normalized;
    }

    public Task<HardDeleteQuarantineResult> QuarantineAsync(
        Guid operationId, string managedPath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var normalized = ValidateManagedPath(managedPath);
        var source = ResolveUnderWebRoot(normalized);
        var quarantineDirectory = ResolveUnderWebRoot($"{QuarantinePrefix}/{operationId:N}");
        var pathHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..24].ToLowerInvariant();
        var quarantineName = $"{pathHash}_{Path.GetFileName(source)}";
        var quarantineFullPath = Path.Combine(quarantineDirectory, quarantineName);
        var quarantinePath = $"{QuarantinePrefix}/{operationId:N}/{quarantineName}";
        if (File.Exists(quarantineFullPath))
        {
            if (File.Exists(source))
                throw new HardDeleteFileException("quarantine_source_conflict", "Tệp gốc và bản cách ly cùng tồn tại.");
            return Task.FromResult(new HardDeleteQuarantineResult(normalized, quarantinePath, false));
        }
        if (!File.Exists(source))
            return Task.FromResult(new HardDeleteQuarantineResult(normalized, null, true));

        Directory.CreateDirectory(quarantineDirectory);
        File.Move(source, quarantineFullPath);
        return Task.FromResult(new HardDeleteQuarantineResult(normalized, quarantinePath, false));
    }

    public Task RestoreAsync(string managedPath, string? quarantinePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var destination = ResolveUnderWebRoot(ValidateManagedPath(managedPath));
        if (string.IsNullOrWhiteSpace(quarantinePath)) return Task.CompletedTask;
        var source = ResolveQuarantinePath(quarantinePath);
        if (!File.Exists(source)) return Task.CompletedTask;
        if (File.Exists(destination))
            throw new HardDeleteFileException("restore_destination_exists", "Không thể khôi phục tệp vì đường dẫn gốc đã có dữ liệu.");

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Move(source, destination);
        return Task.CompletedTask;
    }

    public Task PurgeAsync(string? quarantinePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(quarantinePath)) return Task.CompletedTask;
        var fullPath = ResolveQuarantinePath(quarantinePath);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private string ResolveQuarantinePath(string quarantinePath)
    {
        if (!quarantinePath.StartsWith(QuarantinePrefix + "/", StringComparison.Ordinal) ||
            quarantinePath.Contains("..", StringComparison.Ordinal) || quarantinePath.Contains('\\'))
        {
            throw InvalidPath();
        }
        return ResolveUnderWebRoot(quarantinePath);
    }

    private string ResolveUnderWebRoot(string hostRelativePath)
    {
        var webRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot"));
        var fullPath = Path.GetFullPath(Path.Combine(
            webRoot, hostRelativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw InvalidPath();
        return fullPath;
    }

    private static void EnsureNoSymbolicLink(string fullPath)
    {
        var current = new FileInfo(fullPath);
        while (current.Directory is not null)
        {
            if (current.Exists && current.LinkTarget is not null) throw InvalidPath();
            current = new FileInfo(current.Directory.FullName);
        }
    }

    private static HardDeleteFileException InvalidPath() => new(
        "invalid_managed_path",
        "Đường dẫn tệp phải thuộc vùng lưu trữ riêng tư được quản lý và không được chứa thành phần duyệt thư mục.");
}