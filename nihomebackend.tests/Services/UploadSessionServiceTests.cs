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

    /// <summary>Completes like a module that moves the file away: reads it, then removes it.</summary>
    private Task<Tuple<byte[], string>?> CompleteAsync(Guid id, int user = Owner,
        UploadSessionPurpose purpose = UploadSessionPurpose.ContractAttachment, int context = ContractId) =>
        _sut.CompleteAsync(id, user, purpose, context, async completed =>
        {
            var bytes = await File.ReadAllBytesAsync(completed.StagingPath);
            File.Delete(completed.StagingPath);
            return Tuple.Create(bytes, completed.FileName);
        });

    [Fact]
    public async Task Chunks_AppendInOrder_AndTheCompleteFileHasEveryByte()
    {
        var session = await StartAsync(10);

        Assert.Equal(UploadChunkStatus.Accepted, (await SendAsync(session.Id, 0, Bytes(6, 1))).Status);
        var last = await SendAsync(session.Id, 6, Bytes(4, 2));

        Assert.Equal(UploadChunkStatus.Accepted, last.Status);
        Assert.Equal(10, last.ReceivedBytes);
        var completed = await CompleteAsync(session.Id);
        Assert.NotNull(completed);
        Assert.Equal([.. Bytes(6, 1), .. Bytes(4, 2)], completed!.Item1);
        Assert.Equal("hop-dong-ky.pdf", completed.Item2);
        // Taken: the session is closed.
        Assert.Null(await _sut.FindAsync(session.Id, Owner));
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
        Assert.Null(await CompleteAsync(session.Id, user: Owner + 1));
        Assert.False(await _sut.CancelAsync(session.Id, Owner + 1));
        Assert.NotNull(await _sut.FindAsync(session.Id, Owner));
    }

    [Fact]
    public async Task IncompleteOrMismatchedSession_CannotBeCompleted()
    {
        var session = await StartAsync(10);
        await SendAsync(session.Id, 0, Bytes(4, 1));

        Assert.Null(await CompleteAsync(session.Id));
        await SendAsync(session.Id, 4, Bytes(6, 1));
        // Bound to the contract and purpose it was opened for.
        Assert.Null(await CompleteAsync(session.Id, purpose: UploadSessionPurpose.ContractAppendix));
        Assert.Null(await CompleteAsync(session.Id, context: ContractId + 1));
        Assert.NotNull(await CompleteAsync(session.Id));
    }

    [Fact]
    public async Task FailedRegistration_KeepsTheSessionSoTheCompletionCanBeRetried()
    {
        var session = await StartAsync(4);
        await SendAsync(session.Id, 0, Bytes(4, 1));

        // The module could not record the file (e.g. a database error) and threw.
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CompleteAsync<string>(session.Id, Owner,
            UploadSessionPurpose.ContractAttachment, ContractId, _ => throw new InvalidOperationException()));
        // Or it left the file where it was.
        Assert.Equal("kept", await _sut.CompleteAsync(session.Id, Owner,
            UploadSessionPurpose.ContractAttachment, ContractId, _ => Task.FromResult("kept")));

        Assert.NotNull(await _sut.FindAsync(session.Id, Owner));
        Assert.Equal(Bytes(4, 1), (await CompleteAsync(session.Id))!.Item1);
    }

    [Fact]
    public async Task DoubleSubmit_TakesTheFileOnce()
    {
        var session = await StartAsync(4);
        await SendAsync(session.Id, 0, Bytes(4, 1));
        var consumed = 0;
        var release = new TaskCompletionSource();

        Task<string?> Complete() => _sut.CompleteAsync(session.Id, Owner, UploadSessionPurpose.ContractAttachment,
            ContractId, async completed =>
            {
                Interlocked.Increment(ref consumed);
                await release.Task;
                File.Delete(completed.StagingPath);
                return "taken";
            });

        var first = Complete();
        var second = Complete();
        release.SetResult();

        Assert.Equal(["taken", null], (await Task.WhenAll(first, second)).OrderBy(item => item is null));
        Assert.Equal(1, consumed);
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
    public async Task LostStagedBytes_EndTheSessionInsteadOfPaddingTheFile()
    {
        var session = await StartAsync(10);
        await SendAsync(session.Id, 0, Bytes(4, 1));
        // App_Data was replaced (e.g. a redeploy) while the row survived.
        File.Delete(_sut.StagingPath(session.Id));

        var next = await SendAsync(session.Id, 4, Bytes(6, 2));

        Assert.Equal(UploadChunkStatus.NotFound, next.Status);
        Assert.False(File.Exists(_sut.StagingPath(session.Id)));
        Assert.Null(await _sut.FindAsync(session.Id, Owner));
    }

    [Fact]
    public async Task Release_DeletesTheRowAndAnyLeftoverFile()
    {
        var session = await StartAsync(4);
        await SendAsync(session.Id, 0, Bytes(4, 1));

        await _sut.ReleaseAsync(session.Id);

        Assert.False(File.Exists(_sut.StagingPath(session.Id)));
        Assert.Null(await _sut.FindAsync(session.Id, Owner));
    }
}
