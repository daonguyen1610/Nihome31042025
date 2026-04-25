namespace NihomeBackend.Services;

public class HostedImageService(IWebHostEnvironment env)
{
    private const string ManagedImagePrefix = "/images/upload/";

    public bool IsManagedUpload(string? imageUrl)
    {
        return !string.IsNullOrWhiteSpace(imageUrl)
            && imageUrl.StartsWith(ManagedImagePrefix, StringComparison.OrdinalIgnoreCase);
    }

    public void DeleteIfManagedUpload(string? imageUrl)
    {
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