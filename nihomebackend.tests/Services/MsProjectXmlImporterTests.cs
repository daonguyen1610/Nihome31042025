using System.Text;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

/// <summary>
/// NICON's two real MS Project schedules (MMV master schedule and BMA design schedule), saved as
/// XML the way NICON is asked to export them (PRD v2 answer 11).
/// </summary>
public sealed class MsProjectXmlImporterTests
{
    private static MsProjectImport Read(string name)
    {
        using var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestData", "MsProject", name));
        return MsProjectXmlImporter.Parse(file);
    }

    [Fact]
    public void MmvMasterSchedule_KeepsLevelsDatesAndLinks()
    {
        var import = Read("nicon-mmv-master-schedule.xml");

        // 41 tasks less the project summary row.
        Assert.Equal(40, import.Items.Count);
        var permit = import.Items.Single(item => item.WbsCode == "1");
        Assert.StartsWith("ADJUSTED INVESTMENT REGISTRATION CERTIFICATE", permit.Title);
        Assert.Null(permit.ParentNodeId);
        Assert.Equal(new DateOnly(2024, 8, 15), permit.PlannedStart);
        Assert.Equal(new DateOnly(2024, 10, 1), permit.PlannedEnd);
        var receive = import.Items.Single(item => item.WbsCode == "1.1");
        Assert.Equal(permit.NodeId, receive.ParentNodeId);

        // 1.2 follows 1.1 finish-to-start; 2.1 starts with 2.2 (start-to-start); 2.2 finishes with 1.3.
        var submit = import.Items.Single(item => item.WbsCode == "1.2");
        var link = Assert.Single(submit.Predecessors);
        Assert.Equal((receive.NodeId, "FS", 0), (link.PredecessorNodeId!, link.Type, link.LagDays));
        var drawings = import.Items.Single(item => item.WbsCode == "2.2");
        Assert.Equal("SS", Assert.Single(import.Items.Single(item => item.WbsCode == "2.1").Predecessors).Type);
        Assert.Equal("FF", Assert.Single(drawings.Predecessors).Type);
        Assert.Equal(40, import.Items.Sum(item => item.Predecessors.Count));

        // Every child sits inside its parent, as the master schedule requires.
        var byId = import.Items.ToDictionary(item => item.NodeId!.Value);
        Assert.All(import.Items.Where(item => item.ParentNodeId.HasValue), item =>
        {
            var parent = byId[item.ParentNodeId!.Value];
            Assert.InRange(item.PlannedStart!.Value, parent.PlannedStart!.Value, parent.PlannedEnd!.Value);
            Assert.InRange(item.PlannedEnd!.Value, parent.PlannedStart!.Value, parent.PlannedEnd!.Value);
        });
        Assert.Contains(import.Items, item => byId.ContainsKey(item.ParentNodeId ?? Guid.Empty) &&
            byId[item.ParentNodeId!.Value].ParentNodeId.HasValue);
    }

    [Fact]
    public void BmaDesignSchedule_ReadsWithVietnameseNames()
    {
        var import = Read("nicon-bma-design-schedule.xml");

        Assert.Equal(46, import.Items.Count);
        Assert.Contains(import.Items, item => item.WbsCode == "4.3" && item.Title == "Lập Quy hoạch rút gọn 1/500");
        Assert.All(import.Items, item => Assert.True(item.PlannedEnd >= item.PlannedStart));
        // This file has no saved baseline: none is invented.
        Assert.All(import.Items, item => Assert.Null(item.BaselineStart));
    }

    [Fact]
    public void BaselineMilestoneAndLag_AreRead()
    {
        const string xml = """
            <Project xmlns="http://schemas.microsoft.com/project"><Tasks>
              <Task><UID>1</UID><OutlineLevel>1</OutlineLevel><WBS>1</WBS><Name>Móng</Name>
                <Start>2026-06-01T08:00:00</Start><Finish>2026-06-10T17:00:00</Finish>
                <Baseline><Number>0</Number><Start>2026-05-25T08:00:00</Start><Finish>2026-06-05T17:00:00</Finish></Baseline></Task>
              <Task><UID>2</UID><OutlineLevel>1</OutlineLevel><WBS>2</WBS><Name>Bàn giao mặt bằng</Name><Milestone>1</Milestone>
                <Start>2026-06-13T08:00:00</Start><Finish>2026-06-13T08:00:00</Finish>
                <PredecessorLink><PredecessorUID>1</PredecessorUID><Type>1</Type><LinkLag>9600</LinkLag><LagFormat>7</LagFormat></PredecessorLink></Task>
            </Tasks></Project>
            """;

        var import = MsProjectXmlImporter.Parse(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        Assert.Equal((new DateOnly(2026, 5, 25), new DateOnly(2026, 6, 5)),
            (import.Items[0].BaselineStart!.Value, import.Items[0].BaselineEnd!.Value));
        Assert.True(import.Items[1].IsMilestone);
        Assert.Equal(2, Assert.Single(import.Items[1].Predecessors).LagDays);
    }

    [Theory]
    [InlineData("""<?xml version="1.0"?><!DOCTYPE p [<!ENTITY x SYSTEM "file:///etc/passwd">]><Project xmlns="http://schemas.microsoft.com/project"><Tasks><Task><UID>1</UID><Name>&x;</Name></Task></Tasks></Project>""")]
    [InlineData("""<workbook><sheet>not a schedule</sheet></workbook>""")]
    [InlineData("""PK binary .mpp content""")]
    public void FilesThatAreNotMsProjectXml_AreRefused(string content)
    {
        Assert.Throws<MsProjectImportException>(() =>
            MsProjectXmlImporter.Parse(new MemoryStream(Encoding.UTF8.GetBytes(content))));
    }
}
