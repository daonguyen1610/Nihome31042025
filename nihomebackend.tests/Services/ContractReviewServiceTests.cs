using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class ContractReviewServiceTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly ContractService _contracts;
    private readonly ContractReviewService _sut;
    private readonly HashSet<int> _canViewContracts = [];
    private int _author, _tagged, _legal, _otherLegal, _contractId;

    public ContractReviewServiceTests()
    {
        _contracts = new ContractService(_db, NullLogger<ContractService>.Instance,
            Mock.Of<IProjectDocumentStagingService>(), new OpportunityClosureInvariantService(_db));
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(p => p.FilterByRolePermissionsAsync(It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, IReadOnlyCollection<string> _, CancellationToken _) =>
                ids.Where(_canViewContracts.Contains).ToHashSet());
        _sut = new ContractReviewService(_db, permissions.Object, _contracts, _notifications.Object,
            NullLogger<ContractReviewService>.Instance);

        var sales = new Role { Code = "SALES", Name = "Sales" };
        var legal = new Role { Code = "LEGAL_OFFICER", Name = "Legal Officer" };
        ApplicationUser User(string name, Role role) => new()
        {
            FullName = name,
            Email = $"{name.Replace(" ", "").ToLowerInvariant()}@nicon.vn",
            PhoneNumber = "0900000000",
            PasswordHash = "x",
            RoleEntity = role,
        };
        var author = User("Nguyen Soan", sales);
        var tagged = User("Tran Review", sales);
        var legalUser = User("Le Phap Che", legal);
        var otherLegal = User("Pham Phap Che", legal);
        var customer = new Customer { Name = "Công ty Minh Long", Type = CustomerType.Company };
        _db.AddRange(sales, legal, author, tagged, legalUser, otherLegal, customer);
        _db.WorkflowConfigs.Add(new WorkflowConfig
        {
            Module = "contracts",
            Action = "sign",
            Name = "Ký hợp đồng",
            IsActive = true,
            StepsJson = """[{"order":1,"name":"Pháp chế soát xét","approverRoleCode":"LEGAL_OFFICER","slaHours":24,"requireAllApprovers":false}]""",
        });
        _db.SaveChanges();
        var contract = new Contract
        {
            ContractNumber = "HD-2026-0801",
            CustomerId = customer.Id,
            Type = ContractType.Construction,
            OwnerUserId = author.Id,
            Value = 1_000_000_000m,
        };
        _db.Contracts.Add(contract);
        _db.SaveChanges();
        (_author, _tagged, _legal, _otherLegal, _contractId) =
            (author.Id, tagged.Id, legalUser.Id, otherLegal.Id, contract.Id);
        _canViewContracts.UnionWith([_author, _tagged, _legal, _otherLegal]);
    }

    public void Dispose() => _db.Dispose();

    private Task<NihomeBackend.Models.DTOs.Responses.ContractResponse?> Submit(params int[] tagged) =>
        _sut.SubmitAsync(_contractId, new SubmitContractReviewRequest
        {
            ReviewerUserIds = [.. tagged],
            ApproverUserIds = [_legal],
        }, _author, canSeeAll: false, CancellationToken.None);

    private Task<NihomeBackend.Models.DTOs.Responses.ContractResponse?> Approve(int caller) =>
        _sut.ApproveAsync(_contractId, new ContractReviewDecisionRequest(), caller, CancellationToken.None);

    private static async Task<string?> CodeOf(Func<Task> act) =>
        (await Assert.ThrowsAsync<ContractValidationException>(act)).Code;

    [Fact]
    public async Task Review_AdvancesOnlyWhenTheAssignedReviewerApproves_ThenApproves()
    {
        var sent = await Submit(_tagged);

        Assert.Equal(ContractStatus.UnderReview, sent!.Status);
        Assert.Equal([_tagged, _legal], sent.ReviewSteps.Select(s => s.ApproverUserId));
        Assert.Equal("Current", sent.ReviewSteps[0].State);
        _notifications.Verify(n => n.NotifyFromTemplateAsync(_tagged, "contract.review-requested",
            It.IsAny<IDictionary<string, string>>(), "Contract", _contractId, It.IsAny<string>(), "vi"));

        // The workflow approver cannot jump ahead of the tagged reviewer, and the author never decides.
        Assert.Equal("CONTRACT_REVIEW_NOT_YOUR_TURN", await CodeOf(() => Approve(_legal)));
        Assert.Null(await Approve(_author));

        var afterTagged = await Approve(_tagged);
        Assert.Equal(ContractStatus.UnderReview, afterTagged!.Status);
        Assert.Equal(1, afterTagged.ReviewStepIndex);
        _notifications.Verify(n => n.NotifyFromTemplateAsync(_legal, "contract.review-requested",
            It.IsAny<IDictionary<string, string>>(), "Contract", _contractId, It.IsAny<string>(), "vi"));

        var approved = await Approve(_legal);
        Assert.Equal(ContractStatus.Approved, approved!.Status);
        Assert.All(approved.ReviewSteps, s => Assert.Equal("Approved", s.State));
        _notifications.Verify(n => n.NotifyFromTemplateAsync(_author, "contract.review-approved",
            It.IsAny<IDictionary<string, string>>(), "Contract", _contractId, It.IsAny<string>(), "vi"));
    }

    [Fact]
    public async Task Submit_RefusesTheAuthorAsReviewer_AndAPersonTwice()
    {
        Assert.Equal("CONTRACT_REVIEW_INVALID_REVIEWER", await CodeOf(() => Submit(_author)));
        Assert.Equal("CONTRACT_REVIEW_PERSON_TWICE", await CodeOf(() => Submit(_legal)));
        Assert.Equal(ContractStatus.Draft, _db.Contracts.Single().Status);
    }

    [Fact]
    public async Task Approve_RefusesAReviewerWhoLostContractAccess()
    {
        await Submit();
        _canViewContracts.Remove(_legal);

        Assert.Equal("CONTRACT_REVIEW_NOT_ALLOWED", await CodeOf(() => Approve(_legal)));
    }

    [Fact]
    public async Task Reject_NeedsAReason_AndReturnsToDraftWithoutAChain()
    {
        await Submit();
        Assert.Equal("CONTRACT_REVIEW_REASON_REQUIRED", await CodeOf(() =>
            _sut.RejectAsync(_contractId, new ContractReviewDecisionRequest { Note = " " }, _legal, CancellationToken.None)));

        var rejected = await _sut.RejectAsync(_contractId,
            new ContractReviewDecisionRequest { Note = "Điều khoản thanh toán chưa khớp báo giá" }, _legal,
            CancellationToken.None);

        Assert.Equal(ContractStatus.Draft, rejected!.Status);
        Assert.Empty(rejected.ReviewSteps);
        _notifications.Verify(n => n.NotifyFromTemplateAsync(_author, "contract.review-rejected",
            It.Is<IDictionary<string, string>>(d => d["reason"] == "Điều khoản thanh toán chưa khớp báo giá"),
            "Contract", _contractId, It.IsAny<string>(), "vi"));
    }

    [Fact]
    public async Task UnderReview_IsReadOnly_CannotBeApprovedByTransition_AndWithdrawsToDraft()
    {
        await Submit();
        Assert.Contains("Illegal", (await Assert.ThrowsAsync<ContractValidationException>(() =>
            _contracts.TransitionStatusAsync(_contractId, ContractStatus.Approved, _author, canSeeAll: true))).Message);
        var contract = _db.Contracts.Single();
        Assert.Equal("CONTRACT_REVIEW_LOCKED", await CodeOf(() => _contracts.UpdateAsync(_contractId,
            new UpsertContractRequest
            {
                ContractNumber = contract.ContractNumber,
                CustomerId = contract.CustomerId,
                Type = contract.Type,
                Status = ContractStatus.UnderReview,
                Value = 2m,
            }, _author, canSeeAll: true, canReassignOwner: false)));

        var withdrawn = await _contracts.TransitionStatusAsync(_contractId, ContractStatus.Draft, _author, canSeeAll: true);

        Assert.Equal(ContractStatus.Draft, withdrawn!.Status);
        Assert.Empty(withdrawn.ReviewSteps);
        Assert.Null(_db.Contracts.Single().ReviewSubmittedByUserId);
    }
}
