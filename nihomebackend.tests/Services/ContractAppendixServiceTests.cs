using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class ContractAppendixServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ContractAppendixService _sut;
    private readonly Mock<IProjectDocumentStagingService> _projectDocuments = new();
    private readonly Contract _contract;
    private readonly int _projectId;
    private readonly string _contentRoot;

    public ContractAppendixServiceTests()
    {
        _db = DbContextFactory.Create();
        _db.Customers.Add(new Customer { Name = "C", Type = CustomerType.Company });
        _db.SaveChanges();
        var customerId = _db.Customers.Single().Id;
        var operationalProject = new OperationalProject
        {
            Code = "OP-CONTRACT-VO",
            Name = "Contract appendix project",
            CustomerId = customerId,
        };
        _db.OperationalProjects.Add(operationalProject);
        _db.SaveChanges();

        _contract = new Contract
        {
            ContractNumber = "HD-TEST-0001",
            CustomerId = customerId,
            OwnerUserId = 100,
            Value = 1_000_000m,
            OperationalProjectId = operationalProject.Id,
        };
        _db.Contracts.Add(_contract);
        _db.SaveChanges();
        _projectId = operationalProject.Id;

        _contentRoot = Path.Combine(Path.GetTempPath(), $"nihome-contract-appendix-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_contentRoot, "wwwroot", "files", "contracts"));
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(item => item.ContentRootPath).Returns(_contentRoot);
        _sut = new ContractAppendixService(
            _db,
            NullLogger<ContractAppendixService>.Instance,
            _projectDocuments.Object,
            environment.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_contentRoot)) Directory.Delete(_contentRoot, recursive: true);
    }

    private UpsertContractAppendixRequest Req(decimal delta = 50_000m, string title = "T", string reason = "R") =>
        new() { Title = title, Reason = reason, ValueDelta = delta };

    [Fact]
    public async Task Create_AllocatesVoNumberStartingAtOne()
    {
        var vo = await _sut.CreateAsync(_contract.Id, Req(), callerUserId: 100, canSeeAll: true);
        Assert.NotNull(vo);
        Assert.Equal(1, vo!.VoNumber);
        Assert.Equal(ContractAppendixStatus.Draft, vo.Status);
    }

    [Fact]
    public async Task Create_AllocatesSequenceWithinOwningContract()
    {
        var first = await _sut.CreateAsync(_contract.Id, Req(), callerUserId: 100, canSeeAll: true);
        var second = await _sut.CreateAsync(_contract.Id, Req(title: "T2"), callerUserId: 100, canSeeAll: true);

        Assert.Equal(1, first!.VoNumber);
        Assert.Equal(2, second!.VoNumber);
        Assert.All(_db.ContractAppendices, appendix => Assert.Equal(_contract.Id, appendix.ContractId));
    }

    [Fact]
    public async Task Create_ThrowsOnZeroDelta()
    {
        await Assert.ThrowsAsync<ContractValidationException>(
            () => _sut.CreateAsync(_contract.Id, Req(delta: 0m), 100, true));
    }

    [Fact]
    public async Task SubmitApprove_Workflow_Advances()
    {
        var vo = await _sut.CreateAsync(_contract.Id, Req(delta: 25_000m), 100, true);
        var submitted = await _sut.SubmitAsync(_contract.Id, vo!.Id, 100, true);
        Assert.Equal(ContractAppendixStatus.Submitted, submitted!.Status);
        Assert.NotNull(submitted.SubmittedAt);

        var approved = await _sut.ApproveAsync(_contract.Id, vo.Id, "ok", callerUserId: 200, canSeeAll: true);
        Assert.Equal(ContractAppendixStatus.Approved, approved!.Status);
        Assert.NotNull(approved.DecidedAt);
        Assert.Equal("ok", approved.DecisionNote);
    }

    [Fact]
    public async Task Reject_RequiresNote()
    {
        var vo = await _sut.CreateAsync(_contract.Id, Req(), 100, true);
        await _sut.SubmitAsync(_contract.Id, vo!.Id, 100, true);
        await Assert.ThrowsAsync<ContractValidationException>(
            () => _sut.RejectAsync(_contract.Id, vo.Id, note: null, 200, true));
        var rejected = await _sut.RejectAsync(_contract.Id, vo.Id, note: "bad", 200, true);
        Assert.Equal(ContractAppendixStatus.Rejected, rejected!.Status);
    }

    [Fact]
    public async Task Update_LockedOnceSubmitted()
    {
        var vo = await _sut.CreateAsync(_contract.Id, Req(), 100, true);
        await _sut.SubmitAsync(_contract.Id, vo!.Id, 100, true);
        await Assert.ThrowsAsync<ContractValidationException>(
            () => _sut.UpdateAsync(_contract.Id, vo.Id, Req(delta: 99_000m), 100, true));
    }

    [Fact]
    public async Task Update_RejectedRow_ResetsToDraft()
    {
        var vo = await _sut.CreateAsync(_contract.Id, Req(), 100, true);
        await _sut.SubmitAsync(_contract.Id, vo!.Id, 100, true);
        await _sut.RejectAsync(_contract.Id, vo.Id, "no", 200, true);
        var edited = await _sut.UpdateAsync(_contract.Id, vo.Id, Req(delta: 88_000m, title: "T2"), 100, true);
        Assert.NotNull(edited);
        Assert.Equal(ContractAppendixStatus.Draft, edited!.Status);
        Assert.Null(edited.SubmittedAt);
        Assert.Null(edited.DecidedAt);
    }

    [Fact]
    public async Task Update_ReplacingManagedFile_DeletesPreviousFile()
    {
        var previousPath = CreateManagedFile("previous.pdf");
        var request = Req();
        request.FilePath = previousPath;
        var vo = await _sut.CreateAsync(_contract.Id, request, 100, true);

        var replacement = Req(title: "Updated");
        replacement.FilePath = CreateManagedFile("replacement.pdf");
        await _sut.UpdateAsync(_contract.Id, vo!.Id, replacement, 100, true);

        Assert.False(File.Exists(FullPath(previousPath)));
        Assert.True(File.Exists(FullPath(replacement.FilePath)));
        _projectDocuments.Verify(staging => staging.StageExistingManagedFileDeleteAsync(
            _projectId, ProjectDocumentSourceModule.Crm, nameof(ContractAppendix), "file",
            vo.Id, previousPath, 100, It.IsAny<CancellationToken>()), Times.Once);
        _projectDocuments.Verify(staging => staging.StageExistingManagedFileAsync(
            _projectId, ProjectDocumentCategory.FinanceContracts, ProjectDocumentSourceModule.Crm,
            nameof(ContractAppendix), "file", vo.Id, replacement.FilePath!,
            Path.GetFileName(replacement.FilePath), _contract.CustomerId, _contract.Id, 100,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ContractAppendixStatus.Draft)]
    [InlineData(ContractAppendixStatus.Submitted)]
    [InlineData(ContractAppendixStatus.Approved)]
    [InlineData(ContractAppendixStatus.Rejected)]
    public async Task Delete_AnyStatus_RemovesAppendixAndPreservesContract(ContractAppendixStatus status)
    {
        var request = Req();
        request.FilePath = CreateManagedFile($"delete-{status}.pdf");
        var vo = await _sut.CreateAsync(_contract.Id, request, 100, true);
        var row = _db.ContractAppendices.Single(v => v.Id == vo!.Id);
        row.Status = status;
        await _db.SaveChangesAsync();
        var customerId = _contract.CustomerId;

        Assert.True(await _sut.DeleteAsync(_contract.Id, vo!.Id, 100, true));
        Assert.Empty(_db.ContractAppendices);
        Assert.True(_db.Contracts.Any(c => c.Id == _contract.Id));
        Assert.True(_db.Customers.Any(c => c.Id == customerId));
        Assert.False(File.Exists(FullPath(request.FilePath)));
        _projectDocuments.Verify(staging => staging.StageExistingManagedFileDeleteAsync(
            _projectId, ProjectDocumentSourceModule.Crm, nameof(ContractAppendix), "file",
            vo.Id, request.FilePath!, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_MissingAppendix_ReturnsFalse()
    {
        Assert.False(await _sut.DeleteAsync(_contract.Id, 99999, 100, true));
    }

    [Fact]
    public async Task Delete_OtherOwnersContract_ReturnsFalse()
    {
        var vo = await _sut.CreateAsync(_contract.Id, Req(), 100, true);

        Assert.False(await _sut.DeleteAsync(_contract.Id, vo!.Id, 999, canSeeAll: false));
        Assert.Single(_db.ContractAppendices);
    }

    [Fact]
    public async Task List_ReturnsNullWhenSalesDoesNotOwn()
    {
        var rows = await _sut.ListAsync(_contract.Id, callerUserId: 999, canSeeAll: false);
        Assert.Null(rows);
    }

    [Fact]
    public async Task Update_CannotAddressAppendixThroughAnotherContract()
    {
        var vo = await _sut.CreateAsync(_contract.Id, Req(), 100, true);
        var otherContract = new Contract
        {
            ContractNumber = "HD-TEST-0002",
            CustomerId = _contract.CustomerId,
            OwnerUserId = 100,
            Value = 2_000_000m,
        };
        _db.Contracts.Add(otherContract);
        await _db.SaveChangesAsync();

        var updated = await _sut.UpdateAsync(otherContract.Id, vo!.Id, Req(title: "Cross contract"), 100, true);

        Assert.Null(updated);
        Assert.Equal("T", _db.ContractAppendices.Single().Title);
    }

    private string CreateManagedFile(string fileName)
    {
        var relativePath = $"/files/contracts/{fileName}";
        File.WriteAllText(FullPath(relativePath), "contract appendix");
        return relativePath;
    }

    private string FullPath(string? relativePath) =>
        Path.Combine(_contentRoot, "wwwroot", relativePath!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
}
