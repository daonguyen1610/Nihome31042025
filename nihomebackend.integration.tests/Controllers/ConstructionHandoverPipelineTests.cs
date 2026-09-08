using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// Customer Module 4: site execution, quality recovery, and client handover.
/// Only customer/project/role foundations are seeded. Every business record and
/// transition uses HTTP with ordinary roles. Diary and punch are project-linked;
/// only acceptance has a task FK. No automatic dependency is assumed between them.
/// </summary>
public class ConstructionHandoverPipelineTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SiteExecution_ToClientHandover_EnforcesReadinessAndRecoversQualityIssues(bool reviseAndReopen)
    {
        var (projectId, pmId) = await CreateFoundationAsync();
        await AuthenticateAsync("PM");
        var task = await PostAsync("/api/construction-tasks", new
        {
            designProjectId = projectId,
            name = "Install and pressure-test factory fire-water pipework",
            plannedStart = "2026-09-01",
            plannedEnd = "2026-09-10",
        }, HttpStatusCode.Created);
        var taskId = Id(task);
        var diary = await PostAsync("/api/site-diaries", new
        {
            designProjectId = projectId,
            diaryDate = "2026-09-09",
            weatherCode = "sunny",
            workPerformed = "Installed 100 m of fire-water pipework; pressure test found a leaking flange.",
            headcountLabor = 8,
            headcountEngineers = 2,
            headcountSupervisors = 1,
            headcountSubcontractors = 3,
            incidents = "Flange at grid A3 requires seal replacement before handover.",
        }, HttpStatusCode.Created);
        var diaryId = Id(diary);
        diary.GetProperty("headcountTotal").GetInt32().Should().Be(14);
        await RejectUnchangedAsync($"/api/site-diaries/{diaryId}", "confirm", new { }, HttpStatusCode.BadRequest);
        await PostAsync($"/api/site-diaries/{diaryId}/submit", new { });
        await PostAsync($"/api/site-diaries/{diaryId}/confirm", new { });
        (await GetAsync($"/api/site-diaries/{diaryId}")).GetProperty("confirmedByUserId").GetInt32().Should().Be(pmId);

        var taskComplete = await Client.PutAsJsonAsync($"/api/construction-tasks/{taskId}", new
        {
            name = "Install and pressure-test factory fire-water pipework",
            plannedStart = "2026-09-01",
            plannedEnd = "2026-09-10",
            actualStart = "2026-09-01",
            actualEnd = "2026-09-09",
            progressPercent = 100,
            status = "InProgress",
        });
        taskComplete.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(taskComplete)).GetProperty("status").GetString().Should().Be("Completed");

        var handover = await PostAsync("/api/handover-records", HandoverPayload(projectId, pmId, false), HttpStatusCode.Created);
        var handoverId = Id(handover);
        var handoverPath = $"/api/handover-records/{handoverId}";
        await AssertReadinessAsync(handoverPath, accepted: 0, openPunch: 0, approvedCategories: 0, ready: false);
        await RejectUnchangedAsync(handoverPath, "status", new { status = "ReadyForHandover" }, HttpStatusCode.BadRequest);
        (await Client.PutAsJsonAsync(handoverPath, HandoverPayload(projectId, pmId, true))).EnsureSuccessStatusCode();
        await RejectUnchangedAsync(handoverPath, "status", new { status = "ReadyForHandover" }, HttpStatusCode.BadRequest);

        // A completed task alone cannot satisfy the approved acceptance gate.
        var acceptance = await PostAsync("/api/acceptance-records", new
        {
            designProjectId = projectId,
            constructionTaskId = taskId,
            title = "Factory fire-water pipework partial acceptance",
            acceptanceDate = "2026-09-10",
            location = "Factory grid A3",
            participants = "Site engineer; project manager; client representative",
            findings = "Installed length confirmed; flange repair recorded in project punch register.",
        }, HttpStatusCode.Created);
        var acceptancePath = $"/api/acceptance-records/{Id(acceptance)}";
        await RejectUnchangedAsync(acceptancePath, "approve", new { status = "Approved" }, HttpStatusCode.BadRequest);
        await PostAsync(acceptancePath + "/status", new { status = "Submitted" });
        await AuthenticateAsync("DESIGN");
        await RejectUnchangedAsync(acceptancePath, "approve", new { status = "Approved" }, HttpStatusCode.Forbidden);
        await AuthenticateAsync("PM");
        if (reviseAndReopen)
        {
            await PostAsync(acceptancePath + "/status", new { status = "Rejected", resolutionNote = "Add measured pressure and test duration." });
            await RejectUnchangedAsync(acceptancePath, "approve", new { status = "Approved" }, HttpStatusCode.BadRequest);
            await PostAsync(acceptancePath + "/status", new { status = "Draft" });
            (await Client.PutAsJsonAsync(acceptancePath, new
            {
                title = "Factory fire-water pipework partial acceptance",
                constructionTaskId = taskId,
                acceptanceDate = "2026-09-10",
                findings = "Retested at 12 bar for 2 hours after replacing flange seal; no leakage.",
            })).EnsureSuccessStatusCode();
            await PostAsync(acceptancePath + "/status", new { status = "Submitted" });
        }
        await PostAsync(acceptancePath + "/approve", new { status = "Approved", resolutionNote = "Partial works accepted; punch closure required for handover." });
        await AssertReadinessAsync(handoverPath, accepted: 1, openPunch: 0, approvedCategories: 0, ready: false);

        var punch = await PostAsync("/api/punch-items", new
        {
            designProjectId = projectId,
            title = "Replace leaking flange seal",
            severity = "High",
            assigneeUserId = pmId,
            location = "Factory grid A3",
            deadline = "2026-09-12",
        }, HttpStatusCode.Created);
        var punchPath = $"/api/punch-items/{Id(punch)}";
        await AssertReadinessAsync(handoverPath, accepted: 1, openPunch: 1, approvedCategories: 0, ready: false);
        await RejectUnchangedAsync(punchPath, "verify", new { status = "Verified", rootCause = "Construction" }, HttpStatusCode.BadRequest);
        await PostAsync(punchPath + "/status", new { status = "InProgress" });
        await PostAsync(punchPath + "/status", new { status = "Fixed", resolutionNote = "Replaced seal and repeated pressure test." });
        // Fixed is not verified: client handover remains blocked.
        await RejectUnchangedAsync(handoverPath, "status", new { status = "ReadyForHandover" }, HttpStatusCode.BadRequest);
        await RejectUnchangedAsync(punchPath, "verify", new { status = "Verified" }, HttpStatusCode.BadRequest);
        await PostAsync(punchPath + "/verify", new { status = "Verified", rootCause = "Construction" });
        await AssertReadinessAsync(handoverPath, accepted: 1, openPunch: 0, approvedCategories: 0, ready: false);
        await RejectUnchangedAsync(handoverPath, "status", new { status = "ReadyForHandover" }, HttpStatusCode.BadRequest);

        // Resolve required categories from master data instead of fixing their count in the test.
        var categories = (await GetAsync("/api/asbuilt-categories")).EnumerateArray()
            .Where(category => category.GetProperty("isRequired").GetBoolean() && category.GetProperty("isActive").GetBoolean())
            .Select(category => category.GetProperty("code").GetString()!).ToArray();
        categories.Should().NotBeEmpty();
        var documentIds = new List<int>();
        foreach (var category in categories)
        {
            await AuthenticateAsync("DESIGN");
            var evidence = $"Approved factory fire-water evidence: {category}";
            var filePath = await UploadAsync(evidence, $"{category}.pdf");
            var document = await PostAsync("/api/as-built-documents", new
            {
                designProjectId = projectId,
                title = $"Factory fire-water {category}",
                category,
                fileUrl = filePath,
            }, HttpStatusCode.Created);
            var documentId = Id(document);
            documentIds.Add(documentId);
            var documentPath = $"/api/as-built-documents/{documentId}";
            await PostAsync(documentPath + "/status", new { status = "Submitted" });
            await RejectUnchangedAsync(documentPath, "approve", new { status = "Approved" }, HttpStatusCode.Forbidden);
            await AuthenticateAsync("PM");
            await AssertReadinessAsync(handoverPath, accepted: 1, openPunch: 0, approvedCategories: documentIds.Count - 1, ready: false);
            await PostAsync(documentPath + "/approve", new { status = "Approved", note = "Reviewed against installed pipework and test evidence." });
            var content = await Client.GetAsync(documentPath + "/content");
            content.StatusCode.Should().Be(HttpStatusCode.OK);
            (await content.Content.ReadAsStringAsync()).Should().Be(evidence);
        }
        await AssertReadinessAsync(handoverPath, accepted: 1, openPunch: 0, approvedCategories: categories.Length, ready: true);
        await PostAsync(handoverPath + "/status", new { status = "ReadyForHandover" });

        if (reviseAndReopen)
        {
            // A newly reopened defect invalidates previously calculated readiness at completion.
            await PostAsync(punchPath + "/status", new { status = "Open", resolutionNote = "Leak recurred during final commissioning." });
            await RejectUnchangedAsync(handoverPath, "complete", new { status = "HandedOver" }, HttpStatusCode.BadRequest);
            await PostAsync(punchPath + "/status", new { status = "InProgress" });
            await PostAsync(punchPath + "/status", new { status = "Fixed", resolutionNote = "Replaced flange and retested." });
            await PostAsync(punchPath + "/verify", new { status = "Verified", rootCause = "Construction" });
            (await GetAsync(punchPath)).GetProperty("reopenCount").GetInt32().Should().Be(1);
        }
        await AuthenticateAsync("DESIGN");
        await RejectUnchangedAsync(handoverPath, "complete", new { status = "HandedOver" }, HttpStatusCode.Forbidden);
        await AuthenticateAsync("PM");
        await PostAsync(handoverPath + "/complete", new { status = "HandedOver", note = "Client accepted commissioned fire-water installation and complete dossier." });
        await RejectUnchangedAsync(handoverPath, "complete", new { status = "HandedOver" }, HttpStatusCode.BadRequest);
        var completed = await GetAsync(handoverPath);
        completed.GetProperty("status").GetString().Should().Be("HandedOver");
        completed.GetProperty("actualHandoverDate").GetString().Should().NotBeNullOrWhiteSpace();
        completed.GetProperty("statusHistory").EnumerateArray().Select(item => item.GetProperty("toStatus").GetString())
            .Should().Equal("HandedOver", "ReadyForHandover", "Draft");
        completed.GetProperty("statusHistory").EnumerateArray().Select(item => item.GetProperty("changedByUserId").GetInt32())
            .Should().OnlyContain(actor => actor == pmId);

        // Persisted identities and terminal states prove the same job survived every handoff.
        await WithDbAsync(async db =>
        {
            (await db.ConstructionTasks.SingleAsync(item => item.Id == taskId)).DesignProjectId.Should().Be(projectId);
            (await db.SiteDiaries.SingleAsync(item => item.Id == diaryId)).Status.Should().Be(SiteDiaryStatus.Confirmed);
            var persistedAcceptance = await db.AcceptanceRecords.SingleAsync(item => item.Id == Id(acceptance));
            persistedAcceptance.ConstructionTaskId.Should().Be(taskId);
            persistedAcceptance.ApprovedByUserId.Should().Be(pmId);
            (await db.PunchItems.SingleAsync(item => item.Id == Id(punch))).VerifiedByUserId.Should().Be(pmId);
            (await db.AsBuiltDocuments.CountAsync(item => documentIds.Contains(item.Id) && item.DesignProjectId == projectId && item.Status == AsBuiltStatus.Approved))
                .Should().Be(categories.Length);
            (await db.HandoverRecords.SingleAsync(item => item.Id == handoverId)).Status.Should().Be(HandoverStatus.HandedOver);
        });

        if (reviseAndReopen)
        {
            // Client follow-up reopens the completed record instead of overwriting
            // the original handover decision or creating a second project record.
            await PostAsync(handoverPath + "/status", new { status = "Reopened", note = "Client requested a follow-up commissioning walk." });
            await RejectUnchangedAsync(handoverPath, "complete", new { status = "HandedOver" }, HttpStatusCode.BadRequest);
            await PostAsync(handoverPath + "/status", new { status = "ReadyForHandover", note = "Follow-up walk completed; readiness rechecked." });
            await PostAsync(handoverPath + "/complete", new { status = "HandedOver", note = "Client accepted the follow-up commissioning." });
            var handedOverAgain = await GetAsync(handoverPath);
            handedOverAgain.GetProperty("reopenCount").GetInt32().Should().Be(1);
            handedOverAgain.GetProperty("statusHistory").EnumerateArray().Select(item => item.GetProperty("toStatus").GetString())
                .Should().Equal("HandedOver", "ReadyForHandover", "Reopened", "HandedOver", "ReadyForHandover", "Draft");
            (await WithDbAsync(db => db.HandoverRecords.CountAsync(item => item.DesignProjectId == projectId))).Should().Be(1);
            (await WithDbAsync(db => db.HandoverStatusHistory.CountAsync(item => item.HandoverRecordId == handoverId))).Should().Be(6);
        }
    }

    private async Task AssertReadinessAsync(string path, int accepted, int openPunch, int approvedCategories, bool ready)
    {
        var readiness = (await GetAsync(path)).GetProperty("readiness");
        readiness.GetProperty("approvedAcceptanceRecords").GetInt32().Should().Be(accepted);
        readiness.GetProperty("unresolvedPunchItems").GetInt32().Should().Be(openPunch);
        readiness.GetProperty("approvedRequiredAsBuiltCategories").GetInt32().Should().Be(approvedCategories);
        readiness.GetProperty("isReady").GetBoolean().Should().Be(ready);
    }

    private async Task RejectUnchangedAsync(string path, string action, object payload, HttpStatusCode expected)
    {
        var before = (await GetAsync(path)).GetRawText();
        var response = await Client.PostAsJsonAsync($"{path}/{action}", payload);
        response.StatusCode.Should().Be(expected, "{0}", await response.Content.ReadAsStringAsync());
        (await GetAsync(path)).GetRawText().Should().Be(before, "a rejected transition must preserve persisted state and audit stamps");
    }

    private async Task<JsonElement> GetAsync(string path)
    {
        var response = await Client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "{0}", await response.Content.ReadAsStringAsync());
        return await ReadJsonAsync(response);
    }

    private async Task<JsonElement> PostAsync(string path, object payload, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await Client.PostAsJsonAsync(path, payload);
        response.StatusCode.Should().Be(expected, "{0}", await response.Content.ReadAsStringAsync());
        return await ReadJsonAsync(response);
    }

    private async Task<string> UploadAsync(string evidence, string fileName)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(evidence));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        var response = await Client.PostAsync("/api/business-documents/as-built", form);
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("path").GetString()!;
    }

    private Task AuthenticateAsync(string role) => AuthTestHelper.AuthenticateAsync(Client,
        client => AuthTestHelper.LoginAsRoleAsync(client, role));

    private static int Id(JsonElement body) => body.GetProperty("id").GetInt32();

    private static object HandoverPayload(int projectId, int pmId, bool commissioned) => new
    {
        designProjectId = projectId,
        title = "Factory fire-water system client handover",
        plannedHandoverDate = "2026-09-15",
        responsibleUserId = pmId,
        commissioningCompleted = commissioned,
        commissioningNotes = commissioned ? "Pressure test and pump commissioning completed." : null,
        checklistItems = new[] { new { name = "Walk site with client and verify equipment", isCompleted = commissioned } },
        documents = Array.Empty<string>(),
        signatories = new[] { "Client facilities representative", "NICON project manager" },
    };

    private Task<(int ProjectId, int PmId)> CreateFoundationAsync() => WithDbAsync(async db =>
    {
        var pm = await db.Users.SingleAsync(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"]);
        var design = await db.Users.SingleAsync(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["DESIGN"]);
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var customer = new Customer
        {
            Name = $"Factory owner {suffix}",
            SourceCode = "referral",
            RelationshipStatus = CustomerRelationshipStatus.InProgress,
            Type = CustomerType.Company,
        };
        var operational = new OperationalProject
        {
            Code = $"PJ-CON-{suffix}",
            Name = "Factory fire-water installation",
            Customer = customer,
            ProjectManagerUserId = pm.Id,
        };
        var project = new DesignProject
        {
            ProjectCode = $"DP-CON-{suffix}",
            Name = operational.Name,
            Customer = customer,
            OperationalProject = operational,
            ProjectManagerUserId = pm.Id,
            DesignLeadUserId = design.Id,
        };
        db.DesignProjects.Add(project);
        await db.SaveChangesAsync();
        return (project.Id, pm.Id);
    });
}
