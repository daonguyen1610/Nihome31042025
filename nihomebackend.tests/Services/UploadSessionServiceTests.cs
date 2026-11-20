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
    private const string Target = "/api/quotes/42/documents";
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
        _sut.CreateAsync(Owner, Target, "hop-dong-ky.pdf", size);

    private Task<UploadChunkResult> SendAsync(Guid id, long offset, byte[] bytes, int user = Owner) =>
        _sut.AppendAsync(id, user, offset, new MemoryStream(bytes), bytes.Length);

    private static byte[] Bytes(int length, byte value) => Enumerable.Repeat(value, length).ToArray();

    /// <summary>Uses the finished file like a successful upload request: reads it, then closes.</summary>
    private async Task<Tuple<byte[], string>?> CompleteAsync(Guid id, int user = Owner, string target = Target)
    {
        await using var held = await _sut.HoldCompletedAsync([id], user, target);
        if (held is null) return null;
        var file = held.Files.Single();
        var bytes = await File.ReadAllBytesAsync(file.StagingPath);
        await held.CloseAsync();
        return Tuple.Create(bytes, file.FileName);
    }

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
        // Bound to the upload request it was opened for.
        Assert.Null(await CompleteAsync(session.Id, target: "/api/customers/42/documents"));
        Assert.NotNull(await CompleteAsync(session.Id, target: Target.ToUpperInvariant()));
    }

    [Fact]
    public async Task FailedUploadRequest_KeepsTheSessionSoTheRequestCanBeSentAgain()
    {
        var session = await StartAsync(4);
        await SendAsync(session.Id, 0, Bytes(4, 1));

        // The module refused or failed the request: the held files are let go, not closed.
        await using (var held = await _sut.HoldCompletedAsync([session.Id], Owner, Target))
            Assert.NotNull(held);

        Assert.NotNull(await _sut.FindAsync(session.Id, Owner));
        Assert.Equal(Bytes(4, 1), (await CompleteAsync(session.Id))!.Item1);
        Assert.Null(await _sut.FindAsync(session.Id, Owner));
    }

    [Fact]
    public async Task DoubleSubmit_UsesTheFileOnce()
    {
        var session = await StartAsync(4);
        await SendAsync(session.Id, 0, Bytes(4, 1));

        var first = await _sut.HoldCompletedAsync([session.Id], Owner, Target);
        // The second request waits for the first one to finish with the file.
        var second = _sut.HoldCompletedAsync([session.Id], Owner, Target);
        await Task.Delay(50);
        Assert.False(second.IsCompleted);
        await first!.CloseAsync();
        await first.DisposeAsync();

        Assert.Null(await second);
    }

    [Fact]
    public async Task OneMissingFile_HoldsNothing()
    {
        var done = await StartAsync(4);
        await SendAsync(done.Id, 0, Bytes(4, 1));
        var unfinished = await StartAsync(10);

        Assert.Null(await _sut.HoldCompletedAsync([done.Id, unfinished.Id], Owner, Target));
        // The finished one was let go and is still usable.
        Assert.NotNull(await CompleteAsync(done.Id));
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
    public async Task Cancel_WaitsForTheRequestUsingTheFile()
    {
        var session = await StartAsync(4);
        await SendAsync(session.Id, 0, Bytes(4, 1));
        var held = await _sut.HoldCompletedAsync([session.Id], Owner, Target);

        var cancel = _sut.CancelAsync(session.Id, Owner);
        await Task.Delay(50);
        // The file is not pulled away while the upload request still reads it.
        Assert.False(cancel.IsCompleted);
        Assert.True(File.Exists(held!.Files.Single().StagingPath));
        await held.DisposeAsync();

        Assert.True(await cancel);
        Assert.False(File.Exists(_sut.StagingPath(session.Id)));
    }

    [Fact]
    public async Task ExpiryCleanup_SkipsASessionInUse_AndSweepsFilesWithoutASession()
    {
        var busy = await StartAsync(4);
        await SendAsync(busy.Id, 0, Bytes(4, 1));
        var orphan = Path.Combine(Path.GetDirectoryName(_sut.StagingPath(busy.Id))!, $"{Guid.NewGuid():N}.part");
        await File.WriteAllBytesAsync(orphan, Bytes(3, 9));
        var later = DateTime.UtcNow + UploadSessionService.Lifetime + TimeSpan.FromMinutes(1);
        File.SetLastWriteTimeUtc(orphan, DateTime.UtcNow - UploadSessionService.Lifetime - TimeSpan.FromMinutes(1));

        await using (var held = await _sut.HoldCompletedAsync([busy.Id], Owner, Target))
        {
            Assert.Equal(0, await _sut.DeleteExpiredAsync(later));
            Assert.True(File.Exists(_sut.StagingPath(busy.Id)));
        }
        Assert.False(File.Exists(orphan));

        Assert.Equal(1, await _sut.DeleteExpiredAsync(later));
        Assert.False(File.Exists(_sut.StagingPath(busy.Id)));
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
