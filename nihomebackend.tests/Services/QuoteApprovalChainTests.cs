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
    public void Includes_FindsEachApproverOfTheChain_AndNoOneElse()
    {
        var json = QuoteApprovalChain.Write([
            new QuoteApprovalStep(1, "Trưởng phòng Cung ứng duyệt", "PROCUREMENT_MANAGER", null, 24, 12, "Nguyễn Thị Thu Trang",
                DateTime.UtcNow, DateTime.UtcNow),
            new QuoteApprovalStep(2, "Trưởng phòng Kinh doanh duyệt", "SALES_MANAGER", null, 24, 120, "Trần Quốc Huy",
                DateTime.UtcNow, null),
        ]);

        Assert.True(QuoteApprovalChain.Includes(json, 12));
        Assert.True(QuoteApprovalChain.Includes(json, 120));
        Assert.False(QuoteApprovalChain.Includes(json, 1));
        Assert.False(QuoteApprovalChain.Includes(json, 20));
        Assert.False(QuoteApprovalChain.Includes(null, 12));
        // The SQL pattern is built on the same token.
        Assert.Equal("%\"approverUserId\":12,%", QuoteApprovalChain.ApproverPattern(12));
    }
}
