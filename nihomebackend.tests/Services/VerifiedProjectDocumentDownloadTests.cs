using System.Security.Cryptography;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class VerifiedProjectDocumentDownloadTests
{
    [Theory]
    [InlineData(128)]
    [InlineData(96 * 1024)]
    public async Task MatchingSnapshot_ReturnsExactBytes_ForMemoryAndDiskBuffer(int length)
    {
        var bytes = Enumerable.Range(0, length).Select(value => (byte)(value % 251)).ToArray();
        var content = Content(bytes);
        await using var verified = await VerifiedProjectDocumentDownload.CreateAsync(content, Hash(bytes).ToUpperInvariant());
        await using var response = new MemoryStream();

        await verified.WriteToAsync(response);

        Assert.Equal(bytes, response.ToArray());
        Assert.True(response.CanWrite);
    }

    [Fact]
    public async Task ChangedRemoteBytes_AreRejectedEvenWhenCatalogSnapshotIsUnchanged()
    {
        var approved = "%PDF NICON approved Concept R00"u8.ToArray();
        var changed = "%PDF NICON unreviewed Concept R01"u8.ToArray();

        await Assert.ThrowsAsync<ProjectDocumentConflictException>(() =>
            VerifiedProjectDocumentDownload.CreateAsync(Content(changed), Hash(approved)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public async Task InvalidSnapshot_DoesNotReadRemoteContent(string snapshot)
    {
        var reads = 0;
        var content = new ProjectDocumentDownload("NICON.pdf", "application/pdf", (_, _) =>
        {
            reads++;
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<ProjectDocumentConflictException>(() =>
            VerifiedProjectDocumentDownload.CreateAsync(content, snapshot));

        Assert.Equal(0, reads);
    }

    [Fact]
    public async Task EmptyContent_IsRejectedEvenWithMatchingEmptyHash()
    {
        await Assert.ThrowsAsync<ProjectDocumentConflictException>(() =>
            VerifiedProjectDocumentDownload.CreateAsync(Content([]), Hash([])));
    }

    [Fact]
    public async Task OversizedRemoteContent_IsBoundedAndRejected()
    {
        var chunk = new byte[1024 * 1024];
        var chunksWritten = 0;
        var content = new ProjectDocumentDownload("NICON.pdf", "application/pdf", async (target, ct) =>
        {
            for (var index = 0; index < 102; index++)
            {
                await target.WriteAsync(chunk, ct);
                chunksWritten++;
            }
        });

        await Assert.ThrowsAsync<ProjectDocumentConflictException>(() =>
            VerifiedProjectDocumentDownload.CreateAsync(content, new string('a', 64)));

        Assert.Equal(100, chunksWritten);
    }

    [Fact]
    public async Task InterruptedDownload_DoesNotProduceVerifiedContent()
    {
        var content = new ProjectDocumentDownload("NICON.pdf", "application/pdf", async (target, ct) =>
        {
            await target.WriteAsync(new byte[96 * 1024], ct);
            throw new IOException("Drive connection interrupted");
        });

        await Assert.ThrowsAsync<ProjectDocumentConflictException>(() =>
            VerifiedProjectDocumentDownload.CreateAsync(content, new string('a', 64)));
    }

    [Fact]
    public async Task CancelledDownload_PreservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var content = new ProjectDocumentDownload("NICON.pdf", "application/pdf", (_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            VerifiedProjectDocumentDownload.CreateAsync(content, new string('a', 64), cancellation.Token));
    }

    private static ProjectDocumentDownload Content(byte[] bytes) => new("NICON.pdf", "application/pdf",
        (target, ct) => target.WriteAsync(bytes, ct).AsTask());

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
