using Microsoft.AspNetCore.Hosting;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class UploadSessionServiceTests : IDisposable
{
    private const int Owner = 7;
    private const int ContractId = 42;
    private readonly AppDbContext _db;
    private readonly string _contentRoot;
    private readonly UploadSessionService _sut;

    public UploadSessionServiceTests()
    {
        _db = DbContextFactory.Create();
        _contentRoot = Path.Combine(Path.GetTempPath(), $"nihome-upload-sessions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _sut = new UploadSessionService(_db,
            Mock.Of<IWebHostEnvironment>(environment => environment.ContentRootPath == _contentRoot));
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_contentRoot)) Directory.Delete(_contentRoot, recursive: true);
    }

    private Task<UploadSession> StartAsync(long size) =>
        _sut.CreateAsync(Owner, UploadSessionPurpose.ContractAttachment, ContractId, "hop-dong-ky.pdf", size);

    private Task<UploadChunkResult> SendAsync(Guid id, long offset, byte[] bytes, int user = Owner) =>
        _sut.AppendAsync(id, user, offset, new MemoryStream(bytes), bytes.Length);

    private static byte[] Bytes(int length, byte value) => Enumerable.Repeat(value, length).ToArray();

    [Fact]
    public async Task Chunks_AppendInOrder_AndTheCompleteFileHasEveryByte()
    {
        var session = await StartAsync(10);

        Assert.Equal(UploadChunkStatus.Accepted, (await SendAsync(session.Id, 0, Bytes(6, 1))).Status);
        var last = await SendAsync(session.Id, 6, Bytes(4, 2));

        Assert.Equal(UploadChunkStatus.Accepted, last.Status);
        Assert.Equal(10, last.ReceivedBytes);
        var completed = await _sut.TakeCompletedAsync(session.Id, Owner, UploadSessionPurpose.ContractAttachment, ContractId);
        Assert.NotNull(completed);
        Assert.Equal([.. Bytes(6, 1), .. Bytes(4, 2)], await File.ReadAllBytesAsync(completed!.StagingPath));
        Assert.Equal("hop-dong-ky.pdf", completed.FileName);
    }

    [Fact]
    public async Task ResentChunk_AfterALostResponse_IsNotWrittenTwice()
    {
        var session = await StartAsync(10);
        await SendAsync(session.Id, 0, Bytes(6, 1));

        // The client never saw the reply and sends the same chunk again.
        var retry = await SendAsync(session.Id, 0, Bytes(6, 1));

        Assert.Equal(UploadChunkStatus.Accepted, retry.Status);
        Assert.Equal(6, retry.ReceivedBytes);
        Assert.Equal(6, new FileInfo(_sut.StagingPath(session.Id)).Length);
    }

    [Fact]
    public async Task ChunkAtTheWrongOffset_ReportsWhereToResume()
    {
        var session = await StartAsync(10);
        await SendAsync(session.Id, 0, Bytes(4, 1));

        var gap = await SendAsync(session.Id, 6, Bytes(4, 2));

        Assert.Equal(UploadChunkStatus.OffsetMismatch, gap.Status);
        Assert.Equal(4, gap.ReceivedBytes);
    }

    [Fact]
    public async Task ChunkBeyondTheDeclaredSize_OrLargerThanAChunk_IsRefused()
    {
        var session = await StartAsync(10);

        Assert.Equal(UploadChunkStatus.Invalid, (await SendAsync(session.Id, 0, Bytes(11, 1))).Status);
        var big = await StartAsync(UploadSessionService.ChunkSize * 2L);
        Assert.Equal(UploadChunkStatus.Invalid,
            (await SendAsync(big.Id, 0, Bytes(UploadSessionService.ChunkSize + 1, 1))).Status);
    }

    [Fact]
    public async Task AnotherUser_CannotSeeAppendCompleteOrCancelTheSession()
    {
        var session = await StartAsync(4);

        Assert.Null(await _sut.FindAsync(session.Id, Owner + 1));
        Assert.Equal(UploadChunkStatus.NotFound, (await SendAsync(session.Id, 0, Bytes(4, 1), Owner + 1)).Status);
        await SendAsync(session.Id, 0, Bytes(4, 1));
        Assert.Null(await _sut.TakeCompletedAsync(session.Id, Owner + 1, UploadSessionPurpose.ContractAttachment, ContractId));
        Assert.False(await _sut.CancelAsync(session.Id, Owner + 1));
        Assert.NotNull(await _sut.FindAsync(session.Id, Owner));
    }

    [Fact]
    public async Task IncompleteOrMismatchedSession_CannotBeCompleted()
    {
        var session = await StartAsync(10);
        await SendAsync(session.Id, 0, Bytes(4, 1));

        Assert.Null(await _sut.TakeCompletedAsync(session.Id, Owner, UploadSessionPurpose.ContractAttachment, ContractId));
        await SendAsync(session.Id, 4, Bytes(6, 1));
        // Bound to the contract and purpose it was opened for.
        Assert.Null(await _sut.TakeCompletedAsync(session.Id, Owner, UploadSessionPurpose.ContractAppendix, ContractId));
        Assert.Null(await _sut.TakeCompletedAsync(session.Id, Owner, UploadSessionPurpose.ContractAttachment, ContractId + 1));
        Assert.NotNull(await _sut.TakeCompletedAsync(session.Id, Owner, UploadSessionPurpose.ContractAttachment, ContractId));
    }

    [Fact]
    public async Task CancelAndExpiry_RemoveTheRowAndTheStagedBytes()
    {
        var cancelled = await StartAsync(10);
        await SendAsync(cancelled.Id, 0, Bytes(4, 1));
        Assert.True(await _sut.CancelAsync(cancelled.Id, Owner));
        Assert.False(File.Exists(_sut.StagingPath(cancelled.Id)));
        Assert.Null(await _sut.FindAsync(cancelled.Id, Owner));

        var abandoned = await StartAsync(10);
        await SendAsync(abandoned.Id, 0, Bytes(4, 1));
        var kept = await StartAsync(10);

        var removed = await _sut.DeleteExpiredAsync(DateTime.UtcNow + UploadSessionService.Lifetime + TimeSpan.FromMinutes(1));

        Assert.Equal(2, removed);
        Assert.False(File.Exists(_sut.StagingPath(abandoned.Id)));
        Assert.Null(await _sut.FindAsync(kept.Id, Owner));
    }

    [Fact]
    public async Task Release_DeletesTheRowAndAnyLeftoverFile()
    {
        var session = await StartAsync(4);
        await SendAsync(session.Id, 0, Bytes(4, 1));
        var completed = await _sut.TakeCompletedAsync(session.Id, Owner, UploadSessionPurpose.ContractAttachment, ContractId);

        await _sut.ReleaseAsync(session.Id);

        Assert.False(File.Exists(completed!.StagingPath));
        Assert.Null(await _sut.FindAsync(session.Id, Owner));
    }
}
