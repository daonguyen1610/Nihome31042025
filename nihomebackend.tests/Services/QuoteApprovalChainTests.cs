using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class QuoteApprovalChainTests
{
    [Fact]
    public async Task Configured_StepWithoutAName_StillReads()
    {
        using var db = DbContextFactory.Create();
        db.WorkflowConfigs.Add(new WorkflowConfig
        {
            Module = "quotes",
            Action = "approve",
            Name = "Duyệt báo giá",
            IsActive = true,
            StepsJson = """[{"order":1,"name":null,"approverRoleCode":"PROCUREMENT_MANAGER","slaHours":24,"requireAllApprovers":false}]""",
        });
        await db.SaveChangesAsync();

        var steps = await QuoteApprovalChain.ConfiguredAsync(db, CancellationToken.None);

        Assert.Equal("", Assert.Single(steps).Name);
        Assert.Equal("PROCUREMENT_MANAGER", steps[0].RoleCode);
    }

    [Fact]
    public async Task Configured_WithoutAnActiveWorkflow_IsTheChosenProcurementManager()
    {
        using var db = DbContextFactory.Create();

        var step = Assert.Single(await QuoteApprovalChain.ConfiguredAsync(db, CancellationToken.None));

        Assert.Equal("PROCUREMENT_MANAGER", step.RoleCode);
        Assert.False(step.EveryoneApproves);
    }

    [Fact]
    public async Task ChainMembers_ReadTheQuote_UntilTheChainEnds()
    {
        using var db = DbContextFactory.Create();
        var quote = new Quote { Code = "QT-2026-0101", OwnerUserId = 7 };
        db.Quotes.Add(quote);
        await db.SaveChangesAsync();
        QuoteApprovalStep Step(int order, int userId) => new(order, $"Bước {order}", "PROCUREMENT_MANAGER", null, 24,
            userId, $"Người {userId}", null, null);

        // Sent with a chain in which 12 approves twice (an "everyone approves" step) and 120 once.
        await QuoteApprovalChain.SetApproversAsync(db, quote, [Step(1, 12), Step(2, 120), Step(3, 12)], CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal([12, 120], db.QuoteChainApprovers.Where(item => item.QuoteId == quote.Id)
            .Select(item => item.UserId).OrderBy(id => id).ToList());
        Assert.True(await QuoteApprovalChain.CanReadAsync(db, quote.Id, 7, null, 120, CancellationToken.None));
        Assert.True(await QuoteApprovalChain.CanReadAsync(db, quote.Id, 7, null, 7, CancellationToken.None));
        Assert.False(await QuoteApprovalChain.CanReadAsync(db, quote.Id, 7, null, 20, CancellationToken.None));

        // Rejected or withdrawn: the chain ends, and so does their access.
        await QuoteApprovalChain.ClearApproversAsync(db, quote.Id, CancellationToken.None);
        await db.SaveChangesAsync();
        Assert.False(await QuoteApprovalChain.CanReadAsync(db, quote.Id, 7, null, 120, CancellationToken.None));
    }
}
