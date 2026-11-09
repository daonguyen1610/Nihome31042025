using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class CmScheduleControllerTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Baseline_IndependentApprovalActualsRecoveryAndHistory()
    {
        var fixture = await FixtureAsync();
        await Login("SUPER_ADMIN");
        var created = await Post(fixture.Project, "baselines", fixture.Payload);
        created.GetProperty("status").GetString().Should().Be("Draft");
        var id = created.GetProperty("id").GetInt32();
        var pending = await Post(fixture.Project, $"baselines/{id}/submit", await SubmitToken(fixture.Project, created));
        await Error(fixture.Project, $"baselines/{id}/decision", new { rowVersion = TokenValue(pending), approve = true }, "independentReview");
        await Login("PM");
        var published = await Post(fixture.Project, $"baselines/{id}/decision", new { rowVersion = TokenValue(pending), approve = true });
        published.GetProperty("status").GetString().Should().Be("Published");
        published.GetProperty("curve").GetArrayLength().Should().BeGreaterThan(1);
        await Login("SITE_COMMANDER");
        await ReportAllLeaves(fixture.Project);
        var snapshot = await Post(fixture.Project, "snapshots", new { });
        snapshot.GetProperty("planPercent").GetDecimal().Should().Be(100m);
        snapshot.GetProperty("actualPercent").GetDecimal().Should().Be(0m, "parent progress must not count twice");
        snapshot.GetProperty("isDelayed").GetBoolean().Should().BeTrue();
        foreach (var period in new[] { "month", "year" })
        {
            var report = await ReadJsonAsync(await Client.GetAsync(Root(fixture.Project) + $"/baselines/{id}/report?period={period}&throughDate={snapshot.GetProperty("asOfDate").GetString()}"));
            report.GetProperty("points").EnumerateArray().Last().GetProperty("actualPercent").GetDecimal().Should().Be(0m);
        }
        using (var invalidReport = await Client.GetAsync(Root(fixture.Project) + $"/baselines/{id}/report?period=week"))
        {
            invalidReport.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ReadJsonAsync(invalidReport)).GetProperty("code").GetString().Should().Be("CM_SCHEDULE_periodInvalid");
        }
        var retry = await Post(fixture.Project, "snapshots", new { });
        retry.GetProperty("id").GetInt32().Should().Be(snapshot.GetProperty("id").GetInt32());
        var snapshotId = snapshot.GetProperty("id").GetInt32();
        var recovered = await Post(fixture.Project, $"snapshots/{snapshotId}/recovery", new
        {
            rowVersion = TokenValue(snapshot),
            explanation = "Thiếu thiết bị ép cọc",
            recoveryPlan = "Điều phối thêm máy từ công trường Bình Dương",
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5),
        });
        await Login("PM");
        var reviewed = await Post(fixture.Project, $"snapshots/{snapshotId}/review", Token(recovered));
        reviewed.GetProperty("reviewedAt").GetString().Should().EndWith("Z");
        (await WithDbAsync(db => db.Notifications.CountAsync(x => x.RefEntityId == fixture.Project))).Should().Be(4);
        await Login("SUPER_ADMIN");
        var next = await Post(fixture.Project, "baselines", fixture.Payload);
        next.GetProperty("version").GetInt32().Should().Be(2);
        (await WithDbAsync(db => db.CmScheduleBaselines.SingleAsync(x => x.Id == id))).Status.Should().Be("Published");
    }

    [Theory]
    [InlineData("outside", "outsideCrm")]
    [InlineData("parent", "outsideCrm")]
    [InlineData("missing", "taskSetInvalid")]
    [InlineData("unlinked", "unlinkedTasks")]
    [InlineData("badweight", "weightTotalInvalid")]
    [InlineData("unsigned", "signedContractRequired")]
    public async Task Draft_InvalidInputs_DoNotPersist(string scenario, string code)
    {
        var fixture = await FixtureAsync(scenario);
        await Login("SUPER_ADMIN");
        await Error(fixture.Project, "baselines", fixture.Payload, code);
        (await WithDbAsync(db => db.CmScheduleBaselines.CountAsync(x => x.OperationalProjectId == fixture.Project))).Should().Be(0);
    }

    [Fact]
    public async Task CrmChanges_BlockApprovalButAuthorCanWithdrawPending()
    {
        var fixture = await FixtureAsync();
        await Login("SUPER_ADMIN");
        var created = await Post(fixture.Project, "baselines", fixture.Payload);
        var id = created.GetProperty("id").GetInt32();
        var pending = await Post(fixture.Project, $"baselines/{id}/submit", await SubmitToken(fixture.Project, created));
        await WithDbAsync(async db =>
        {
            db.ProjectScheduleRevisions.Add(new ProjectScheduleRevision { OperationalProjectId = fixture.Project, Version = 2, Status = ProjectScheduleRevisionStatus.Published });
            await db.SaveChangesAsync();
        });
        await Login("PM");
        await Error(fixture.Project, $"baselines/{id}/decision", new { rowVersion = TokenValue(pending), approve = true }, "crmChanged");
        await Login("SUPER_ADMIN");
        using var stale = await Send(fixture.Project, $"baselines/{id}/cancel", Token(created));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var cancelled = await Post(fixture.Project, $"baselines/{id}/cancel", Token(pending));
        cancelled.GetProperty("status").GetString().Should().Be("Cancelled");
    }

    [Fact]
    public async Task Policy_RequiresPermissionAndPresenceAndChangesActualComparison()
    {
        var fixture = await FixtureAsync();
        await Login("SUPER_ADMIN");
        var created = await Post(fixture.Project, "baselines", fixture.Payload);
        var id = created.GetProperty("id").GetInt32();
        var pending = await Post(fixture.Project, $"baselines/{id}/submit", await SubmitToken(fixture.Project, created));
        await Login("PM");
        await Post(fixture.Project, $"baselines/{id}/decision", new { rowVersion = TokenValue(pending), approve = true });
        await Login("SUPER_ADMIN");
        await Login("SITE_COMMANDER");
        await ReportAllLeaves(fixture.Project);
        await Login("SUPER_ADMIN");
        foreach (var invalid in new object[] { new { }, new { thresholdPercent = -1 }, new { thresholdPercent = 101 } })
        {
            using var response = await Client.PutAsJsonAsync("/api/site-settings/cm-schedule", invalid);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        using var updated = await Client.PutAsJsonAsync("/api/site-settings/cm-schedule", new { thresholdPercent = 100 });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        try
        {
            await Login("SITE_COMMANDER");
            using var denied = await Client.PutAsJsonAsync("/api/site-settings/cm-schedule", new { thresholdPercent = 0 });
            denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var snapshot = await Post(fixture.Project, "snapshots", new { });
            snapshot.GetProperty("thresholdPercent").GetDecimal().Should().Be(100m);
            snapshot.GetProperty("isDelayed").GetBoolean().Should().BeFalse();
            await WithDbAsync(async db => { var task = await db.ConstructionTasks.SingleAsync(x => x.Id == fixture.Leaf); task.Status = ConstructionTaskStatus.Cancelled; await db.SaveChangesAsync(); });
            await Error(fixture.Project, "snapshots", new { }, "actualIncomplete");
        }
        finally
        {
            await Login("SUPER_ADMIN");
            (await Client.PutAsJsonAsync("/api/site-settings/cm-schedule", new { thresholdPercent = 5 })).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task ProjectBoundary_HidesUnassignedProjectAndBlocksUnauthorizedWrites()
    {
        var fixture = await FixtureAsync();
        await Login("SITE_COMMANDER");
        using var write = await Send(fixture.Project, "baselines", fixture.Payload);
        write.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var unrelated = await FixtureAsync();
        await WithDbAsync(async db => { db.OperationalProjectMembers.RemoveRange(db.OperationalProjectMembers.Where(x => x.OperationalProjectId == unrelated.Project)); await db.SaveChangesAsync(); });
        (await Client.GetAsync(Root(unrelated.Project))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Actual_OnlyAssignedSiteManagerCanWriteAndStaleRetryPreservesProgress()
    {
        var fixture = await FixtureAsync();
        await Login("SUPER_ADMIN");
        var created = await Post(fixture.Project, "baselines", fixture.Payload);
        var id = created.GetProperty("id").GetInt32();
        var pending = await Post(fixture.Project, $"baselines/{id}/submit", await SubmitToken(fixture.Project, created));
        await Login("PM");
        await Post(fixture.Project, $"baselines/{id}/decision", new { rowVersion = TokenValue(pending), approve = true });
        var taskBefore = await WithDbAsync(db => db.ConstructionTasks.SingleAsync(x => x.Id == fixture.Leaf));
        await Login("SUPER_ADMIN");
        using (var directProgress = await Client.PutAsJsonAsync($"/api/construction-tasks/{fixture.Leaf}", new
        {
            wbs = taskBefore.Wbs,
            projectScheduleNodeId = taskBefore.ProjectScheduleNodeId,
            name = taskBefore.Name,
            plannedStart = taskBefore.PlannedStart,
            plannedEnd = taskBefore.PlannedEnd,
            progressPercent = 30,
            status = nameof(ConstructionTaskStatus.InProgress),
        }))
        {
            directProgress.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await directProgress.Content.ReadAsStringAsync()).Should().Contain("Chỉ Chỉ huy trưởng");
        }
        (await WithDbAsync(db => db.ConstructionTasks.SingleAsync(x => x.Id == fixture.Leaf))).ProgressPercent.Should().Be(0);
        await Login("PM");
        var emptyReport = await ReadJsonAsync(await Client.GetAsync(Root(fixture.Project) + $"/baselines/{id}/report?period=day&throughDate={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}"));
        emptyReport.GetProperty("points").EnumerateArray().Last().GetProperty("actualPercent").ValueKind.Should().Be(JsonValueKind.Null);
        await Login("SITE_COMMANDER");
        await ReportAllLeaves(fixture.Project);
        var workspace = await ReadJsonAsync(await Client.GetAsync(Root(fixture.Project)));
        var input = workspace.GetProperty("actualInputs").EnumerateArray().Single(x => x.GetProperty("taskId").GetInt32() == fixture.Leaf);
        var body = new { expectedUpdatedAt = input.GetProperty("updatedAt").GetString(), progressPercent = 100 };
        await Login("PM");
        await Error(fixture.Project, $"tasks/{fixture.Leaf}/actual", body, "siteManagerRequired");
        await Login("SITE_COMMANDER");
        foreach (var progress in new[] { -1, 101 })
        {
            using var invalid = await Send(fixture.Project, $"tasks/{fixture.Leaf}/actual", new { expectedUpdatedAt = body.expectedUpdatedAt, progressPercent = progress });
            invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        var updated = await Post(fixture.Project, $"tasks/{fixture.Leaf}/actual", body);
        updated.GetProperty("snapshots")[0].GetProperty("actualPercent").GetDecimal().Should().Be(60m);
        using var stale = await Send(fixture.Project, $"tasks/{fixture.Leaf}/actual", new { body.expectedUpdatedAt, progressPercent = 50 });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.ConstructionTasks.SingleAsync(x => x.Id == fixture.Leaf))).ProgressPercent.Should().Be(100);
        var current = updated.GetProperty("actualInputs").EnumerateArray().Single(x => x.GetProperty("taskId").GetInt32() == fixture.Leaf);
        var corrected = await Post(fixture.Project, $"tasks/{fixture.Leaf}/actual", new { expectedUpdatedAt = current.GetProperty("updatedAt").GetString(), progressPercent = 50 });
        corrected.GetProperty("snapshots")[0].GetProperty("actualPercent").GetDecimal().Should().Be(30m);
        var task = await WithDbAsync(db => db.ConstructionTasks.SingleAsync(x => x.Id == fixture.Leaf));
        task.Status.Should().Be(ConstructionTaskStatus.InProgress);
        task.ActualEnd.Should().BeNull();
        task.ProgressReportedAt.Should().NotBeNull();
        task.ProgressReportedByUserId.Should().Be(await User("SITE_COMMANDER"));
        task.UpdatedByUserId.Should().Be(await User("SITE_COMMANDER"));
    }

    [Fact]
    public async Task LegacyActual_RejectsEndedSiteManagerWhoStillHasProjectAccess()
    {
        var fixture = await FixtureAsync();
        var reporter = await User("SUPER_ADMIN");
        await WithDbAsync(async db =>
        {
            db.OperationalProjectMembers.Add(new OperationalProjectMember
            {
                OperationalProjectId = fixture.Project,
                UserId = reporter,
                Position = "Chỉ huy trưởng",
                StartedAt = DateTime.UtcNow.AddDays(-1),
            });
            await db.SaveChangesAsync();
        });
        await Login("SUPER_ADMIN");
        var payload = JsonSerializer.SerializeToElement(fixture.Payload);
        var draft = await Post(fixture.Project, "baselines", new
        {
            changeReason = "Phân công chỉ huy trưởng nhà máy Long An",
            weightBasisNote = "Tỷ trọng theo phạm vi khối lượng công tác",
            workingDaysOfWeek = new[] { 1, 2, 3, 4, 5, 6 },
            nonWorkingDates = Array.Empty<DateOnly>(),
            siteManagerUserId = reporter,
            items = payload.GetProperty("items"),
        });
        var id = draft.GetProperty("id").GetInt32();
        var pending = await Post(fixture.Project, $"baselines/{id}/submit", await SubmitToken(fixture.Project, draft));
        await Login("PM");
        await Post(fixture.Project, $"baselines/{id}/decision", new { rowVersion = TokenValue(pending), approve = true });
        await WithDbAsync(async db =>
        {
            // Administrative access must not restore an
            // ended BCH assignment's authority to report site actuals.
            (await db.OperationalProjectMembers.SingleAsync(x => x.OperationalProjectId == fixture.Project && x.UserId == reporter)).EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });
        await Login("SUPER_ADMIN");
        (await Client.GetAsync(Root(fixture.Project))).StatusCode.Should().Be(HttpStatusCode.OK);
        using var response = await Client.PatchAsJsonAsync($"/api/construction-tasks/{fixture.Leaf}/progress", new { progressPercent = 30 });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Chỉ Chỉ huy trưởng");
        var task = await WithDbAsync(db => db.ConstructionTasks.SingleAsync(x => x.Id == fixture.Leaf));
        task.ProgressPercent.Should().Be(0);
        task.ProgressReportedAt.Should().BeNull();
    }

    [Fact]
    public async Task ApprovalEmail_RequiresFreshPreviewAndKeepsExactRenderedContent()
    {
        var fixture = await FixtureAsync();
        await Login("SUPER_ADMIN");
        var created = await Post(fixture.Project, "baselines", fixture.Payload);
        var id = created.GetProperty("id").GetInt32();
        using var missing = await Send(fixture.Project, $"baselines/{id}/submit", Token(created));
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var previewResponse = await Client.GetAsync(Root(fixture.Project) + $"/baselines/{id}/email-preview");
        var preview = await ReadJsonAsync(previewResponse);
        await WithDbAsync(async db => { var task = await db.CmScheduleBaselines.SingleAsync(x => x.Id == id); task.ChangeReason = "Khách điều chỉnh phạm vi"; await db.SaveChangesAsync(); });
        await Error(fixture.Project, $"baselines/{id}/submit", new { rowVersion = TokenValue(created), previewToken = preview.GetProperty("previewToken").GetString() }, "emailPreviewRequired");
        var refreshed = await ReadJsonAsync(await Client.GetAsync(Root(fixture.Project)));
        var draft = refreshed.GetProperty("baselines")[0];
        var freshPreview = await ReadJsonAsync(await Client.GetAsync(Root(fixture.Project) + $"/baselines/{id}/email-preview"));
        var pending = await Post(fixture.Project, $"baselines/{id}/submit", new { rowVersion = TokenValue(draft), previewToken = freshPreview.GetProperty("previewToken").GetString() });
        var email = await WithDbAsync(db => db.ProjectScheduleEmails.SingleAsync(x => x.CmScheduleBaselineId == id));
        email.Body.Should().Be(freshPreview.GetProperty("emails")[0].GetProperty("body").GetString());
        email.Subject.Should().Be(freshPreview.GetProperty("emails")[0].GetProperty("subject").GetString());
        await Post(fixture.Project, $"baselines/{id}/cancel", Token(pending));
        (await WithDbAsync(db => db.ProjectScheduleEmails.SingleAsync(x => x.CmScheduleBaselineId == id))).Status.Should().Be(ProjectScheduleEmail.CancelledStatus);
    }

    [Fact]
    public async Task EmailTemplate_AdminChangeAffectsPreviewAndUnsafeHtmlIsRejected()
    {
        var fixture = await FixtureAsync();
        await Login("SUPER_ADMIN");
        var created = await Post(fixture.Project, "baselines", fixture.Payload);
        var path = "/api/site-settings/cm-schedule/email-template";
        var original = await ReadJsonAsync(await Client.GetAsync(path));
        var body = original.GetProperty("body").GetString();
        using var unsafeResponse = await Client.PutAsJsonAsync(path, new { subject = "CM", body = body + "<script>alert(1)</script>" });
        unsafeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        try
        {
            using var saved = await Client.PutAsJsonAsync(path, new { subject = "CM UPDATED · {{projectCode}} · {{version}}", body });
            saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
            var preview = await ReadJsonAsync(await Client.GetAsync(Root(fixture.Project) + $"/baselines/{created.GetProperty("id").GetInt32()}/email-preview"));
            preview.GetProperty("emails")[0].GetProperty("subject").GetString().Should().StartWith("CM UPDATED");
            await Login("SITE_COMMANDER");
            (await Client.PutAsJsonAsync(path, new { subject = "FORBIDDEN", body })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            await Login("SUPER_ADMIN");
            (await Client.PutAsJsonAsync(path, original)).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task IneligibleSiteManager_IsNotSelectableAndCannotReceiveBaseline()
    {
        var fixture = await FixtureAsync();
        var hr = await User("HR");
        await WithDbAsync(async db => { db.OperationalProjectMembers.Add(new OperationalProjectMember { OperationalProjectId = fixture.Project, UserId = hr, StartedAt = DateTime.UtcNow.AddDays(-1), Position = "Nhân sự" }); await db.SaveChangesAsync(); });
        await Login("SUPER_ADMIN");
        var workspace = await ReadJsonAsync(await Client.GetAsync(Root(fixture.Project)));
        workspace.GetProperty("members").EnumerateArray().Select(x => x.GetProperty("userId").GetInt32()).Should().NotContain(hr);
        var payload = JsonSerializer.SerializeToElement(fixture.Payload);
        await Error(fixture.Project, "baselines", new
        {
            changeReason = "Phân công không phù hợp",
            weightBasisNote = "Tỷ trọng theo phạm vi khối lượng công tác",
            workingDaysOfWeek = new[] { 1, 2, 3, 4, 5, 6 },
            nonWorkingDates = Array.Empty<DateOnly>(),
            siteManagerUserId = hr,
            items = payload.GetProperty("items")
        }, "siteManagerRequired");
    }

    [Fact]
    public async Task RootDeletion_DisclosesOwnedScheduleDataAndBlocksInFlightEmail()
    {
        var fixture = await FixtureAsync();
        await Login("SUPER_ADMIN");
        var created = await Post(fixture.Project, "baselines", fixture.Payload);
        var id = created.GetProperty("id").GetInt32();
        await Post(fixture.Project, $"baselines/{id}/submit", await SubmitToken(fixture.Project, created));
        var path = $"/api/operational-projects/{fixture.Project}";
        var impact = await ReadJsonAsync(await Client.GetAsync(path + "/deletion-impact"));
        impact.GetProperty("items").EnumerateArray().Should().Contain(x => x.GetProperty("key").GetString() == "operations.cmScheduleBaselines" && x.GetProperty("count").GetInt32() == 1);
        await WithDbAsync(async db => { var email = await db.ProjectScheduleEmails.SingleAsync(x => x.CmScheduleBaselineId == id); email.Status = ProjectScheduleEmail.SendingStatus; await db.SaveChangesAsync(); });
        var blocked = await ReadJsonAsync(await Client.GetAsync(path + "/deletion-impact"));
        blocked.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        using var stale = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, path) { Content = JsonContent.Create(new { planToken = impact.GetProperty("planToken").GetString(), confirmation = impact.GetProperty("requiredConfirmation").GetString() }) });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.CmScheduleBaselines.AnyAsync(x => x.Id == id))).Should().BeTrue();
        await WithDbAsync(async db => { var email = await db.ProjectScheduleEmails.SingleAsync(x => x.CmScheduleBaselineId == id); email.Status = ProjectScheduleEmail.SentStatus; await db.SaveChangesAsync(); });
        var finalImpact = await ReadJsonAsync(await Client.GetAsync(path + "/deletion-impact"));
        var rowVersion = await WithDbAsync(db => db.OperationalProjects.Where(x => x.Id == fixture.Project).Select(x => Convert.ToBase64String(x.RowVersion)).SingleAsync());
        using var deleted = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, path)
        {
            Content = JsonContent.Create(new
            {
                planToken = finalImpact.GetProperty("planToken").GetString(),
                confirmation = finalImpact.GetProperty("requiredConfirmation").GetString(),
                rowVersion,
            })
        });
        if (!deleted.IsSuccessStatusCode) throw new InvalidOperationException(await deleted.Content.ReadAsStringAsync());
        deleted.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.Accepted);
        (await WithDbAsync(db => db.CmScheduleBaselines.AnyAsync(x => x.OperationalProjectId == fixture.Project))).Should().BeFalse();
        (await WithDbAsync(db => db.ProjectScheduleEmails.AnyAsync(x => x.OperationalProjectId == fixture.Project))).Should().BeFalse();
    }

    private async Task<(int Project, int Leaf, object Payload)> FixtureAsync(string scenario = "")
    {
        var pm = await User("PM"); var site = await User("SITE_COMMANDER"); var author = await User("SUPER_ADMIN");
        // Upstream and CRM are established prerequisites. This suite exercises
        // the real CM HTTP lifecycle rather than retesting their own workflows.
        return await WithDbAsync(async db =>
        {
            if (!await db.SiteSettings.AnyAsync()) db.SiteSettings.Add(new SiteSettings());
            await db.SaveChangesAsync();
            var settings = await db.SiteSettings.FirstAsync();
            settings.CmScheduleEmailSubjectTemplate ??= CmScheduleMailService.DefaultSubject;
            settings.CmScheduleEmailBodyTemplate ??= CmScheduleMailService.DefaultBody;
            foreach (var key in new[] { "cmSchedule.approvalNotification", "cmSchedule.delayNotification" })
                if (!await db.Translations.AnyAsync(x => x.Key == key && x.LanguageCode == "vi")) db.Translations.Add(new Translation { Key = key, LanguageCode = "vi", Value = "Tiến độ CM phiên bản {version}" });
            var customer = new Customer { Name = "Chủ đầu tư nhà máy Long An", Type = CustomerType.Company, SourceCode = "referral" };
            var project = new OperationalProject { Code = $"CM-{Guid.NewGuid():N}"[..30], Name = "Mở rộng nhà máy Long An", Customer = customer, ProjectManagerUserId = pm, CreatedByUserId = author };
            db.OperationalProjects.Add(project);
            var design = new DesignProject { OperationalProject = project, Customer = customer, ProjectCode = $"DP-{Guid.NewGuid():N}"[..30], Name = "Hồ sơ kết cấu nhà máy" };
            db.DesignProjects.Add(design);
            db.Contracts.Add(new Contract { OperationalProject = project, Customer = customer, ContractNumber = $"HD-{Guid.NewGuid():N}"[..30], Direction = ContractDirection.Upstream, Type = ContractType.Construction, Status = scenario == "unsigned" ? ContractStatus.Draft : ContractStatus.Signed });
            db.OperationalProjectMembers.Add(new OperationalProjectMember { OperationalProject = project, UserId = site, Position = "Chỉ huy trưởng", StartedAt = DateTime.UtcNow.AddDays(-1) });
            var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10); var end = start.AddDays(8);
            var crm = new ProjectScheduleRevision { OperationalProject = project, Version = 1, Status = ProjectScheduleRevisionStatus.Published, CreatedByUserId = author };
            var inputs = new List<object>(); int leafId = 0;
            db.ProjectScheduleRevisions.Add(crm);
            var tasks = new List<ConstructionTask>();
            foreach (var wbs in new[] { "1", "1.1", "1.2" })
            {
                var node = new ProjectScheduleNode { Id = Guid.NewGuid(), OperationalProject = project };
                db.ProjectScheduleNodes.Add(node);
                crm.Items.Add(new ProjectScheduleItem { Node = node, NodeId = node.Id, WbsCode = wbs, Title = "Thi công móng " + wbs, PlannedStart = start, PlannedEnd = end });
                var task = new ConstructionTask { DesignProject = design, ProjectScheduleNodeId = scenario == "unlinked" && wbs == "1.2" ? null : node.Id, TaskCode = "TC-" + wbs, Wbs = wbs, Name = "Thi công móng " + wbs, PlannedStart = start, PlannedEnd = end, ProgressPercent = wbs == "1" ? 99 : 0 };
                db.ConstructionTasks.Add(task); tasks.Add(task);
            }
            await db.SaveChangesAsync();
            foreach (var task in tasks)
            {
                leafId = task.Id;
                if (scenario == "missing" && task.Wbs == "1.2") continue;
                inputs.Add(new { taskId = task.Id, plannedStart = scenario == "outside" ? start.AddDays(-1) : scenario == "parent" && task.Wbs == "1" ? start.AddDays(1) : start, plannedEnd = end, weight = task.Wbs == "1" ? 0 : task.Wbs == "1.1" ? scenario == "badweight" ? 30 : 40 : 60, isMilestone = false });
            }
            return (project.Id, leafId, (object)new { changeReason = "Triển khai kế hoạch thi công phần móng", weightBasisNote = "Tỷ trọng theo phạm vi khối lượng công tác móng", workingDaysOfWeek = new[] { 1, 2, 3, 4, 5, 6 }, nonWorkingDates = Array.Empty<DateOnly>(), siteManagerUserId = site, items = inputs });
        });
    }
    private async Task ReportAllLeaves(int project)
    {
        var workspace = await ReadJsonAsync(await Client.GetAsync(Root(project)));
        foreach (var item in workspace.GetProperty("baselines")[0].GetProperty("items").EnumerateArray().Where(x => x.GetProperty("isLeaf").GetBoolean() && !x.GetProperty("isMilestone").GetBoolean()))
        {
            var id = item.GetProperty("taskId").GetInt32();
            var input = workspace.GetProperty("actualInputs").EnumerateArray().Single(x => x.GetProperty("taskId").GetInt32() == id);
            await Post(project, $"tasks/{id}/actual", new { expectedUpdatedAt = input.GetProperty("updatedAt").GetString(), progressPercent = 0 });
            workspace = await ReadJsonAsync(await Client.GetAsync(Root(project)));
        }
    }
    private Task Login(string role) => AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));
    private Task<int> User(string role) => WithDbAsync(db => db.Users.Where(x => x.PhoneNumber == (role == "SUPER_ADMIN" ? TestDataSeeder.SuperAdminPhone : TestDataSeeder.BusinessRolePhonesByCode[role])).Select(x => x.Id).SingleAsync());
    private async Task<object> SubmitToken(int project, JsonElement row)
    {
        using var response = await Client.GetAsync(Root(project) + $"/baselines/{row.GetProperty("id").GetInt32()}/email-preview");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var preview = await ReadJsonAsync(response);
        return new { rowVersion = TokenValue(row), previewToken = preview.GetProperty("previewToken").GetString() };
    }
    private static string Root(int project) => $"/api/operational-projects/{project}/construction/schedule";
    private static string TokenValue(JsonElement row) => row.GetProperty("rowVersion").GetString()!;
    private static object Token(JsonElement row) => new { rowVersion = TokenValue(row) };
    private async Task<HttpResponseMessage> Send(int project, string path, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Root(project) + "/" + path) { Content = JsonContent.Create(payload) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await Client.SendAsync(request);
    }
    private async Task<JsonElement> Post(int project, string path, object payload)
    {
        using var response = await Send(project, path, payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await ReadJsonAsync(response);
    }
    private async Task Error(int project, string path, object payload, string code)
    {
        using var response = await Send(project, path, payload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("CM_SCHEDULE_" + code);
    }
}
