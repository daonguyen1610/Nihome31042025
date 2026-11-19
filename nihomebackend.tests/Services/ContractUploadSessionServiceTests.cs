using Microsoft.AspNetCore.Hosting;
using Moq;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class ContractUploadSessionServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"contract-uploads-{Guid.NewGuid():N}");
    private readonly ContractUploadSessionService _sessions;

    public ContractUploadSessionServiceTests()
    {
        _sessions = new ContractUploadSessionService(
            Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == _root));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private ContractUploadSession Start(long size) =>
        _sessions.Start(contractId: 7, userId: 3, "HD-2026-0030 ban scan.pdf", size, "application/pdf");

    private static MemoryStream Bytes(int count, byte value = 1) => new(Enumerable.Repeat(value, count).ToArray());

    [Fact]
    public async Task ChunksAppendInOrder_AndTheFileMovesOutWhenComplete()
    {
        var session = Start(10);
        Assert.Equal(ChunkWriteStatus.Written, (await _sessions.AppendAsync(session, 0, 6, Bytes(6), default)).Status);
        var last = await _sessions.AppendAsync(session, 6, 4, Bytes(4, 2), default);
        Assert.Equal(new ChunkWriteResult(ChunkWriteStatus.Written, 10), last);

        var destination = Path.Combine(_root, "stored.pdf");
        _sessions.MoveCompletedFile(session, destination);
        Assert.Equal([1, 1, 1, 1, 1, 1, 2, 2, 2, 2], await File.ReadAllBytesAsync(destination));
        Assert.Null(_sessions.Find(session.Id, 7, 3));
    }

    [Fact]
    public async Task AChunkThatDoesNotStartAtTheReceivedBytes_IsRefusedWithTheCount()
    {
        var session = Start(10);
        await _sessions.AppendAsync(session, 0, 4, Bytes(4), default);
        var result = await _sessions.AppendAsync(session, 0, 4, Bytes(4), default);
        Assert.Equal(new ChunkWriteResult(ChunkWriteStatus.OffsetMismatch, 4), result);
        Assert.Equal(4, _sessions.ReceivedBytes(session));
    }

    [Fact]
    public async Task BytesThatArriveBeforeTheConnectionDrops_AreKeptForTheNextChunk()
    {
        var session = Start(10);
        var result = await _sessions.AppendAsync(session, 0, 8, new DroppingStream(Bytes(3)), default);
        Assert.Equal(new ChunkWriteResult(ChunkWriteStatus.Written, 3), result);
        Assert.Equal(ChunkWriteStatus.Written, (await _sessions.AppendAsync(session, 3, 7, Bytes(7), default)).Status);
        Assert.Equal(10, _sessions.ReceivedBytes(session));
    }

    [Fact]
    public async Task AChunkPastTheDeclaredSizeOrLargerThanAChunk_IsRefused()
    {
        var session = Start(10);
        Assert.Equal(ChunkWriteStatus.TooLarge, (await _sessions.AppendAsync(session, 0, 11, Bytes(11), default)).Status);
        var big = Start(ContractUploadSessionService.ChunkSize * 2L);
        Assert.Equal(ChunkWriteStatus.TooLarge,
            (await _sessions.AppendAsync(big, 0, ContractUploadSessionService.ChunkSize + 1L, Bytes(1), default)).Status);
        Assert.Equal(0, _sessions.ReceivedBytes(session));
    }

    [Fact]
    public void ASessionIsFoundOnlyByItsUserAndContract_AndIdsCannotLeaveTheFolder()
    {
        var session = Start(10);
        Assert.NotNull(_sessions.Find(session.Id, 7, 3));
        Assert.Null(_sessions.Find(session.Id, 7, 4));
        Assert.Null(_sessions.Find(session.Id, 8, 3));
        Assert.Null(_sessions.Find("../../etc", 7, 3));
    }

    [Fact]
    public void OnlyUploadsUntouchedSinceTheCutoffAreRemoved()
    {
        var old = Start(10);
        var recent = Start(10);
        var oldFolder = Path.Combine(_root, "App_Data", "contract-uploads", old.Id);
        foreach (var file in Directory.EnumerateFiles(oldFolder))
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(-30));

        Assert.Equal(1, _sessions.DeleteExpired(DateTime.UtcNow - ContractUploadSessionService.Lifetime));
        Assert.Null(_sessions.Find(old.Id, 7, 3));
        Assert.NotNull(_sessions.Find(recent.Id, 7, 3));
    }

    /// <summary>Yields its bytes, then fails as a dropped connection does.</summary>
    private sealed class DroppingStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            return read > 0 ? read : throw new IOException("The client disconnected.");
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
