using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace NihomeBackend.Services;

// Buffer before emitting any response bytes: remote files can change without
// their persisted catalog metadata being reconciled yet.
public sealed class VerifiedProjectDocumentDownload : IAsyncDisposable
{
    private readonly FileBufferingWriteStream buffer;

    private VerifiedProjectDocumentDownload(FileBufferingWriteStream buffer) => this.buffer = buffer;

    public static async Task<VerifiedProjectDocumentDownload> CreateAsync(
        ProjectDocumentDownload content, string expectedSha256, CancellationToken ct = default)
    {
        if (expectedSha256.Length != 64 || !expectedSha256.All(Uri.IsHexDigit))
            throw new ProjectDocumentConflictException("Hồ sơ chưa có mã kiểm tra hợp lệ của bản đã trình; không thể tải bằng chứng chưa xác minh.");

        var buffer = new FileBufferingWriteStream(memoryThreshold: 64 * 1024,
            bufferLimit: ProjectDocumentStorageService.MaxFileSize);
        try
        {
            using var hash = SHA256.Create();
            var hashing = new CryptoStream(buffer, hash, CryptoStreamMode.Write, leaveOpen: true);
            try
            {
                await content.WriteToAsync(hashing, ct);
                await hashing.FlushFinalBlockAsync(ct);
            }
            catch
            {
                // A size-limit failure can already have closed the file buffer.
                // Preserve that original error if crypto finalization also fails.
                try { await hashing.DisposeAsync(); }
                catch { }
                throw;
            }
            await hashing.DisposeAsync();
            if (buffer.Length == 0 || !Convert.ToHexString(hash.Hash!).Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new ProjectDocumentConflictException("Nội dung tệp hiện tại không khớp bản hồ sơ đã trình. Vui lòng liên hệ Chủ nhiệm thiết kế để kiểm tra phiên bản.");
            return new VerifiedProjectDocumentDownload(buffer);
        }
        catch (IOException)
        {
            await buffer.DisposeAsync();
            throw new ProjectDocumentConflictException("Không thể xác minh đầy đủ tệp hồ sơ hoặc tệp vượt quá 100 MiB. Vui lòng thử lại hoặc liên hệ Chủ nhiệm thiết kế.");
        }
        catch
        {
            await buffer.DisposeAsync();
            throw;
        }
    }

    public Task WriteToAsync(Stream destination, CancellationToken ct = default) => buffer.DrainBufferAsync(destination, ct);

    public ValueTask DisposeAsync() => buffer.DisposeAsync();
}
