namespace NihomeBackend.Services;

public class HostedImageService(IWebHostEnvironment env)
{
    private const string ManagedImagePrefix = "/images/upload/";

    public string? NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return imageUrl;
        }

        if (imageUrl.StartsWith(ManagedImagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return imageUrl;
        }

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) &&
            uri.AbsolutePath.StartsWith(ManagedImagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsolutePath;
        }

        return imageUrl;
    }

    public bool IsManagedUpload(string? imageUrl)
    {
        var normalizedImageUrl = NormalizeImageUrl(imageUrl);
        return !string.IsNullOrWhiteSpace(normalizedImageUrl)
            && normalizedImageUrl.StartsWith(ManagedImagePrefix, StringComparison.OrdinalIgnoreCase);
    }

    public void DeleteIfManagedUpload(string? imageUrl)
    {
        imageUrl = NormalizeImageUrl(imageUrl);
        if (!IsManagedUpload(imageUrl))
        {
            return;
        }

        var uploadRoot = Path.GetFullPath(
            Path.Combine(env.ContentRootPath, "wwwroot", "images", "upload"));
        var relative = imageUrl![ManagedImagePrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(uploadRoot, relative));

        // Reject forged URLs that resolve outside the upload root.
        var rootWithSeparator = uploadRoot.EndsWith(Path.DirectorySeparatorChar)
            ? uploadRoot
            : uploadRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            return;
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}