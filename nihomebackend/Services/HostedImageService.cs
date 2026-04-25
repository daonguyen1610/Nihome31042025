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

        var relativePath = imageUrl!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(env.ContentRootPath, "wwwroot", relativePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}