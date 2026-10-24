using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class DesignReviewsControllerTests : IntegrationTestBase
{
    public DesignReviewsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ConceptDraft_CanCorrectFileAndWaiver_ButFrozenRevisionCannotChange()
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN"));
        using var revisions = await Client.GetAsync($"/api/concept-options/{fixture.ConceptOptionId}/revisions");
        revisions.StatusCode.Should().Be(HttpStatusCode.OK);
        var artifactId = (await ReadJsonAsync(revisions))[0].GetProperty("artifacts")
            .EnumerateArray().Single(item => item.GetProperty("projectDocumentId").GetInt64() == fixture.DocumentId)
            .GetProperty("id").GetInt32();
        using var removed = await Client.DeleteAsync(
            $"/api/concept-options/{fixture.ConceptOptionId}/revisions/{fixture.ConceptRevisionId}/artifacts/{artifactId}");
        removed.StatusCode.Should().Be(HttpStatusCode.OK, await removed.Content.ReadAsStringAsync());
        (await ReadJsonAsync(removed)).GetProperty("artifacts").GetArrayLength().Should().Be(1);

        using var invalidFloor = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/concept-options/{fixture.ConceptOptionId}/revisions/{fixture.ConceptRevisionId}/artifacts", new
            {
                projectDocumentId = fixture.DocumentId,
                kind = "FloorPlan",
            });
        invalidFloor.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var floor = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/concept-options/{fixture.ConceptOptionId}/revisions/{fixture.ConceptRevisionId}/artifacts", new
            {
                projectDocumentId = fixture.DocumentId,
                kind = "FloorPlan",
                floorLabel = "Tầng trệt",
            });
        floor.StatusCode.Should().Be(HttpStatusCode.BadRequest); // waiver must be removed first
        using var removeWaiver = await Client.DeleteAsync(
            $"/api/concept-options/{fixture.ConceptOptionId}/revisions/{fixture.ConceptRevisionId}/waivers/FloorPlan");
        removeWaiver.StatusCode.Should().Be(HttpStatusCode.OK);
        using var addFloor = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/concept-options/{fixture.ConceptOptionId}/revisions/{fixture.ConceptRevisionId}/artifacts", new
            {
                projectDocumentId = fixture.DocumentId,
                kind = "FloorPlan",
                floorLabel = "Tầng trệt",
            });
        addFloor.StatusCode.Should().Be(HttpStatusCode.OK, await addFloor.Content.ReadAsStringAsync());
        (await ReadJsonAsync(addFloor)).GetProperty("artifacts").EnumerateArray()
            .Should().ContainSingle(item => item.GetProperty("floorLabel").GetString() == "Tầng trệt");

        await WithDbAsync(async db =>
        {
            var revision = await db.ConceptRevisions.SingleAsync(item => item.Id == fixture.ConceptRevisionId);
            revision.FrozenAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });
        using var afterFreeze = await Client.DeleteAsync(
            $"/api/concept-options/{fixture.ConceptOptionId}/revisions/{fixture.ConceptRevisionId}/waivers/Section");
        afterFreeze.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ConceptChecklistWaivers.CountAsync(item =>
            item.ConceptRevisionId == fixture.ConceptRevisionId && item.Kind == ConceptArtifactKind.Section)))
            .Should().Be(1);
    }

    [Fact]
    public async Task Lifecycle_EnforcesApprovedTaskAndRecipientScopedCommenting()
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN"));

        using var create = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews", new
            {
                stage = "Concept",
                title = "Phương án Concept nhà máy NICON R01",
                recipientUserIds = new[] { fixture.RecipientUserId },
                projectDocumentIds = new[] { fixture.DocumentId, fixture.PerspectiveDocumentId },
                conceptRevisionId = fixture.ConceptRevisionId,
            });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var created = await ReadJsonAsync(create);
        var reviewId = created.GetProperty("id").GetInt32();

        using var submit = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews/{reviewId}/submit-internal", new
            {
                rowVersion = created.GetProperty("rowVersion").GetString(),
            });
        submit.StatusCode.Should().Be(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
        (await WithDbAsync(db => db.Notifications.Where(item => item.UserId == fixture.RecipientUserId &&
            item.RefEntityType == "DesignReview" && item.RefEntityId == reviewId &&
            item.TemplateCode == "design-review.internal").Select(item => item.LinkUrl).SingleAsync()))
            .Should().Be($"/admin/design-reviews/{fixture.DesignProjectId}?reviewId={reviewId}");

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALE"));
        (await Client.GetAsync($"/api/design-projects/{fixture.DesignProjectId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var list = await Client.GetAsync($"/api/design-projects/{fixture.DesignProjectId}/reviews");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var recipientReview = (await ReadJsonAsync(list)).EnumerateArray().Single();
        recipientReview.GetProperty("canManage").GetBoolean().Should().BeFalse();
        recipientReview.GetProperty("canComment").GetBoolean().Should().BeTrue();

        using var comment = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews/{reviewId}/comments", new
            {
                body = "Kinh doanh đề nghị xác nhận vật liệu hoàn thiện theo brief khách hàng.",
                projectDocumentId = fixture.DocumentId,
            });
        comment.StatusCode.Should().Be(HttpStatusCode.OK, await comment.Content.ReadAsStringAsync());
        var commented = await ReadJsonAsync(comment);
        commented.GetProperty("comments")[0].GetProperty("projectDocumentId").GetInt64()
            .Should().Be(fixture.DocumentId);

        using var invalidDocument = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews/{reviewId}/comments", new
            {
                body = "Không được gắn sang tài liệu ngoài đợt trình.",
                projectDocumentId = fixture.AcceptanceDocumentId,
            });
        invalidDocument.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DesignReviewComments.CountAsync(item =>
            item.DesignReviewId == reviewId))).Should().Be(1);

        using var generalComment = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews/{reviewId}/comments", new
            {
                body = "Lưu ý chung cho bộ hồ sơ.",
            });
        generalComment.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(generalComment)).GetProperty("comments")[1]
            .GetProperty("projectDocumentId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        using var unrelated = await Client.GetAsync($"/api/design-projects/{fixture.DesignProjectId}/reviews");
        unrelated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(unrelated)).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task SubmitInternal_WhenEvidenceHashChanged_ReturnsBadRequestAndKeepsDraft()
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN"));
        using var create = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews", new
            {
                stage = "Concept",
                title = "Concept R01 kiểm tra tính bất biến",
                recipientUserIds = new[] { fixture.RecipientUserId },
                projectDocumentIds = new[] { fixture.DocumentId, fixture.PerspectiveDocumentId },
                conceptRevisionId = fixture.ConceptRevisionId,
            });
        create.EnsureSuccessStatusCode();
        var created = await ReadJsonAsync(create);
        var reviewId = created.GetProperty("id").GetInt32();
        await WithDbAsync(async db =>
        {
            var document = await db.ProjectDocuments.SingleAsync(item => item.Id == fixture.DocumentId);
            document.Sha256 = new string('f', 64);
            await db.SaveChangesAsync();
        });

        using var submit = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews/{reviewId}/submit-internal", new
            {
                rowVersion = created.GetProperty("rowVersion").GetString(),
            });

        submit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DesignReviews.Where(item => item.Id == reviewId)
            .Select(item => item.Status).SingleAsync())).Should().Be(DesignReviewStatus.Draft);
    }

    [Fact]
    public async Task ConceptDecision_RequiresSignedAcceptanceAndPreservesRejectedState()
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN"));
        using var create = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews", new
            {
                stage = "Concept",
                title = "Phương án kiến trúc NICON trình chủ đầu tư",
                recipientUserIds = new[] { fixture.RecipientUserId },
                projectDocumentIds = new[] { fixture.DocumentId, fixture.PerspectiveDocumentId },
                conceptRevisionId = fixture.ConceptRevisionId,
            });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var review = await ReadJsonAsync(create);
        var reviewId = review.GetProperty("id").GetInt32();
        foreach (var action in new[] { "submit-internal", "consolidate", "submit-client" })
        {
            object body = action == "consolidate"
                ? new { internalSummary = "Đã tổng hợp góp ý Kinh doanh.", rowVersion = review.GetProperty("rowVersion").GetString() }
                : new { rowVersion = review.GetProperty("rowVersion").GetString() };
            using var response = await SendWithKeyAsync(HttpMethod.Post,
                $"/api/design-projects/{fixture.DesignProjectId}/reviews/{reviewId}/{action}", body);
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            review = await ReadJsonAsync(response);
        }

        using var missing = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews/{reviewId}/client-decision", new
            {
                decision = "ClientApproved",
                rowVersion = review.GetProperty("rowVersion").GetString(),
            });
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DesignReviews.Where(item => item.Id == reviewId)
            .Select(item => item.Status).SingleAsync())).Should().Be(DesignReviewStatus.SubmittedToClient);

        using var missingSource = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/concept-options/{fixture.ConceptOptionId}/revisions/{fixture.ConceptRevisionId}/feedback", new
            {
                customerName = "Ông Trần Minh",
                channel = "Họp",
                occurredAt = DateTime.UtcNow,
                comment = "Cần làm rõ lối tiếp cận sảnh chính.",
            });
        missingSource.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ConceptFeedback.CountAsync(item =>
            item.ConceptRevisionId == fixture.ConceptRevisionId))).Should().Be(0);

        using var feedback = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/concept-options/{fixture.ConceptOptionId}/revisions/{fixture.ConceptRevisionId}/feedback", new
            {
                customerName = "Ông Trần Minh",
                channel = "Họp trực tiếp với chủ đầu tư",
                evidenceSource = "Biên bản họp Concept ngày 21/10, mục 3",
                occurredAt = DateTime.UtcNow,
                comment = "Cần làm rõ lối tiếp cận sảnh chính.",
                projectDocumentId = fixture.PerspectiveDocumentId,
            });
        feedback.StatusCode.Should().Be(HttpStatusCode.OK, await feedback.Content.ReadAsStringAsync());
        var feedbackId = (await ReadJsonAsync(feedback)).GetProperty("feedback")[0].GetProperty("id").GetInt32();
        using var missingOutcome = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/concept-options/{fixture.ConceptOptionId}/revisions/{fixture.ConceptRevisionId}/feedback/{feedbackId}/response", new
            {
                response = "Đã bổ sung lối tiếp cận.",
            });
        missingOutcome.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ConceptFeedback.Where(item => item.Id == feedbackId)
            .Select(item => item.Response).SingleAsync())).Should().BeNull();
        using var responseToCustomer = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/concept-options/{fixture.ConceptOptionId}/revisions/{fixture.ConceptRevisionId}/feedback/{feedbackId}/response", new
            {
                response = "Đã bổ sung lối tiếp cận và gửi bản phối cảnh cập nhật để xác nhận.",
                outcome = "Chủ đầu tư xác nhận lối tiếp cận phù hợp.",
            });
        responseToCustomer.StatusCode.Should().Be(HttpStatusCode.OK);
        var recordedFeedback = (await ReadJsonAsync(responseToCustomer)).GetProperty("feedback")[0];
        recordedFeedback.GetProperty("evidenceSource").GetString().Should().Be("Biên bản họp Concept ngày 21/10, mục 3");
        recordedFeedback.GetProperty("outcome").GetString().Should().Be("Chủ đầu tư xác nhận lối tiếp cận phù hợp.");
        recordedFeedback.GetProperty("occurredAt").GetString().Should().EndWith("Z");
        recordedFeedback.GetProperty("respondedAt").GetString().Should().EndWith("Z");
        using var duplicateResponse = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/concept-options/{fixture.ConceptOptionId}/revisions/{fixture.ConceptRevisionId}/feedback/{feedbackId}/response", new
            {
                response = "Không được ghi đè lịch sử phản hồi.",
                outcome = "Không thay đổi.",
            });
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var approved = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews/{reviewId}/client-decision", new
            {
                decision = "ClientApproved",
                clientAcceptanceDocumentId = fixture.AcceptanceDocumentId,
                clientAcceptanceSignedBy = "Ông Trần Minh, đại diện chủ đầu tư",
                clientAcceptanceSignedAt = DateTime.UtcNow,
                rowVersion = review.GetProperty("rowVersion").GetString(),
            });
        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        var approval = await ReadJsonAsync(approved);
        approval.GetProperty("clientAcceptanceDocumentId").GetInt64().Should().Be(fixture.AcceptanceDocumentId);
        approval.GetProperty("clientAcceptanceSignedAt").GetString().Should().EndWith("Z");
    }

    [Fact]
    public async Task ConceptReview_RejectsUnselectedRevisionAndKeepsState()
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN"));
        using var create = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews", new
            {
                stage = "Concept",
                title = "Không xác định phương án được duyệt",
                recipientUserIds = new[] { fixture.RecipientUserId },
                projectDocumentIds = new[] { fixture.DocumentId, fixture.PerspectiveDocumentId },
            });
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DesignReviews.CountAsync(item =>
            item.DesignProjectId == fixture.DesignProjectId))).Should().Be(0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ClientChanges_CreateR01_PreservesR00AndRejectsIncompleteSubmission(bool newerDraft, bool legacyShared)
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN"));
        using var create = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews", new
            {
                stage = "Concept",
                title = "Phương án sân trong trình khách",
                recipientUserIds = new[] { fixture.RecipientUserId },
                projectDocumentIds = new[] { fixture.DocumentId, fixture.PerspectiveDocumentId },
                conceptRevisionId = fixture.ConceptRevisionId,
            });
        create.EnsureSuccessStatusCode();
        var review = await ReadJsonAsync(create);
        var reviewId = review.GetProperty("id").GetInt32();
        if (newerDraft)
        {
            using var extraDraft = await SendWithKeyAsync(HttpMethod.Post,
                $"/api/design-projects/{fixture.DesignProjectId}/reviews", new
                {
                    stage = "Concept",
                    title = "Nháp tham khảo chưa trình",
                    recipientUserIds = new[] { fixture.RecipientUserId },
                    projectDocumentIds = new[] { fixture.DocumentId, fixture.PerspectiveDocumentId },
                    conceptRevisionId = fixture.ConceptRevisionId,
                });
            extraDraft.EnsureSuccessStatusCode();
        }
        foreach (var action in new[] { "submit-internal", "consolidate", "submit-client" })
        {
            using var step = await SendWithKeyAsync(HttpMethod.Post,
                $"/api/design-projects/{fixture.DesignProjectId}/reviews/{reviewId}/{action}", new
                {
                    internalSummary = "Kinh doanh tổng hợp để trình chủ đầu tư.",
                    rowVersion = review.GetProperty("rowVersion").GetString(),
                });
            step.EnsureSuccessStatusCode();
            review = await ReadJsonAsync(step);
        }
        using var decision = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews/{reviewId}/client-decision", new
            {
                decision = "ClientChangesRequested",
                note = "Chủ đầu tư yêu cầu điều chỉnh sảnh.",
                rowVersion = review.GetProperty("rowVersion").GetString(),
            });
        decision.EnsureSuccessStatusCode();
        if (legacyShared)
        {
            await WithDbAsync(async db =>
            {
                var legacy = await db.DesignReviews.Include(item => item.Documents).SingleAsync(item => item.Id == reviewId);
                legacy.ConceptRevisionId = null;
                var other = new ConceptOption { DesignProjectId = fixture.DesignProjectId, Name = "Phương án mặt đứng phụ trong hồ sơ cũ" };
                db.ConceptOptions.Add(other);
                await db.SaveChangesAsync();
                // A historical shared submission links more than one option.
                legacy.Documents.Single(item => item.ProjectDocumentId == fixture.PerspectiveDocumentId)
                    .SourceRecordIdSnapshot = other.Id;
                await db.SaveChangesAsync();
            });
        }
        using var before = await Client.GetAsync($"/api/concept-options/{fixture.ConceptOptionId}/revisions");
        (await ReadJsonAsync(before))[0].GetProperty("canCreateRevision").GetBoolean().Should().BeTrue();
        using var next = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/concept-options/{fixture.ConceptOptionId}/revisions", new { });
        next.StatusCode.Should().Be(HttpStatusCode.OK, await next.Content.ReadAsStringAsync());
        var nextRevision = await ReadJsonAsync(next);
        nextRevision.GetProperty("number").GetInt32().Should().Be(1);
        nextRevision.GetProperty("artifacts").GetArrayLength().Should().Be(0);
        using var duplicate = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/concept-options/{fixture.ConceptOptionId}/revisions", new { });
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var incomplete = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews", new
            {
                stage = "Concept",
                title = "R01 chưa đủ hồ sơ",
                recipientUserIds = new[] { fixture.RecipientUserId },
                projectDocumentIds = new[] { fixture.DocumentId, fixture.PerspectiveDocumentId },
                conceptRevisionId = nextRevision.GetProperty("id").GetInt32(),
            });
        incomplete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var history = await Client.GetAsync($"/api/concept-options/{fixture.ConceptOptionId}/revisions");
        var revisions = await ReadJsonAsync(history);
        revisions.GetArrayLength().Should().Be(2);
        var original = revisions.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == fixture.ConceptRevisionId);
        original.GetProperty("frozenAt").ValueKind.Should().Be(System.Text.Json.JsonValueKind.String);
        original.GetProperty("artifacts").GetArrayLength().Should().Be(2);
        (await WithDbAsync(db => db.DesignReviews.CountAsync(item => item.DesignProjectId == fixture.DesignProjectId)))
            .Should().Be(newerDraft ? 2 : 1);
    }

    [Theory]
    [InlineData(DesignTaskApprovalStatus.Pending)]
    [InlineData(DesignTaskApprovalStatus.Approved)]
    public async Task UpdateTask_SubmittedScopeIsImmutable(DesignTaskApprovalStatus approvalStatus)
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN"));

        await WithDbAsync(async db =>
        {
            var task = await db.DesignScheduleTasks.SingleAsync(item => item.DesignProjectId == fixture.DesignProjectId);
            task.ApprovalStatus = approvalStatus;
            await db.SaveChangesAsync();
        });
        var original = await WithDbAsync(db => db.DesignScheduleTasks.AsNoTracking()
            .SingleAsync(task => task.DesignProjectId == fixture.DesignProjectId));
        original.Status.Should().Be(DesignScheduleStatus.Completed);
        original.ApprovalStatus.Should().Be(approvalStatus);
        var originalHistoryCount = await WithDbAsync(db => db.DesignScheduleHistory.CountAsync(item =>
            item.OperationalProjectId == original.OperationalProjectId));
        var replacementScope = "Revised scope: coordinate structural design for an additional production floor";

        using var updatedResponse = await SendWithKeyAsync(HttpMethod.Put,
            $"/api/operational-projects/{original.OperationalProjectId}/design-schedule/tasks/{original.Id}", new
            {
                code = original.Code,
                name = replacementScope,
                description = "Scope changed after Design Lead approval; the previous approval cannot cover this work.",
                departmentCode = "design",
                assigneeMemberId = original.AssigneeMemberId,
                approverMemberId = original.ApproverMemberId,
                parentTaskId = original.ParentTaskId,
                resourceSummary = original.ResourceSummary,
                vendorId = original.VendorId,
                downstreamContractId = original.DownstreamContractId,
                isCritical = original.IsCritical,
                isMilestone = original.IsMilestone,
                plannedStart = original.PlannedStart,
                plannedEnd = original.PlannedEnd,
                actualStart = original.PlannedStart,
                actualEnd = original.PlannedEnd,
                status = "Completed",
                progressPercent = 100,
                weight = original.Weight,
                predecessorTaskIds = Array.Empty<int>(),
                ccMemberIds = Array.Empty<int>(),
                rowVersion = Convert.ToBase64String(original.RowVersion),
            });
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            await updatedResponse.Content.ReadAsStringAsync());

        var persisted = await WithDbAsync(db => db.DesignScheduleTasks.AsNoTracking()
            .SingleAsync(task => task.Id == original.Id));
        persisted.Name.Should().Be(original.Name);
        persisted.ApprovalStatus.Should().Be(approvalStatus);
        persisted.RowVersion.Should().Equal(original.RowVersion);
        (await WithDbAsync(db => db.DesignScheduleHistory.CountAsync(item =>
            item.OperationalProjectId == original.OperationalProjectId))).Should().Be(originalHistoryCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task TaskApproval_EndedAssignmentCannotSubmitOrReview(bool reviewing, bool endApprover)
    {
        var fixture = await CreateFixtureAsync();
        var task = await WithDbAsync(async db =>
        {
            var item = await db.DesignScheduleTasks.Include(x => x.AssigneeMember)
                .Include(x => x.ApproverMember).SingleAsync(x => x.DesignProjectId == fixture.DesignProjectId);
            item.Status = reviewing ? DesignScheduleStatus.Completed : DesignScheduleStatus.InProgress;
            item.ApprovalStatus = reviewing ? DesignTaskApprovalStatus.Pending : DesignTaskApprovalStatus.NotSubmitted;
            item.ProgressPercent = reviewing ? 100 : 90;
            if (endApprover) item.ApproverMember.EndedAt = DateTime.UtcNow.AddMinutes(-1);
            else item.AssigneeMember.EndedAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
            return item;
        });
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client,
            reviewing ? "DESIGN_LEAD" : "DESIGN"));
        var uri = $"/api/operational-projects/{task.OperationalProjectId}/design-schedule/tasks/{task.Id}";
        using var response = await SendWithKeyAsync(HttpMethod.Post, uri + (reviewing ? "/review" : "/complete"),
            new { decision = "Approve", rowVersion = Convert.ToBase64String(task.RowVersion) });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        var persisted = await WithDbAsync(db => db.DesignScheduleTasks.AsNoTracking().SingleAsync(x => x.Id == task.Id));
        persisted.Status.Should().Be(task.Status);
        persisted.ApprovalStatus.Should().Be(task.ApprovalStatus);
        persisted.RowVersion.Should().Equal(task.RowVersion);
    }

    [Fact]
    public async Task TaskApproval_RevokedDesignLeadCannotReviewWhileProjectMembershipRemains()
    {
        var fixture = await CreateFixtureAsync();
        var task = await WithDbAsync(async db =>
        {
            var item = await db.DesignScheduleTasks.Include(x => x.ApproverMember).ThenInclude(x => x.Roles)
                .SingleAsync(x => x.DesignProjectId == fixture.DesignProjectId);
            item.ApprovalStatus = DesignTaskApprovalStatus.Pending;
            foreach (var role in item.ApproverMember.Roles) role.EndedAt = DateTime.UtcNow.AddMinutes(-1);
            (await db.DesignProjects.SingleAsync(x => x.Id == fixture.DesignProjectId)).DesignLeadUserId = null;
            await db.SaveChangesAsync();
            return item;
        });
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN_LEAD"));
        using var response = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{task.OperationalProjectId}/design-schedule/tasks/{task.Id}/review",
            new { decision = "Approve", rowVersion = Convert.ToBase64String(task.RowVersion) });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await WithDbAsync(db => db.DesignScheduleTasks.AsNoTracking().SingleAsync(x => x.Id == task.Id)))
            .ApprovalStatus.Should().Be(DesignTaskApprovalStatus.Pending);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DesignManagerUpload_RequiresCurrentProjectAssignment(bool assigned)
    {
        var fixture = await CreateFixtureAsync();
        var project = await WithDbAsync(db => db.DesignProjects.AsNoTracking()
            .SingleAsync(item => item.Id == fixture.DesignProjectId));
        var manager = await WithDbAsync(db => db.Users.AsNoTracking()
            .Where(item => item.Id == project.ProjectManagerUserId)
            .Select(item => new { item.Id, item.IsActive, RoleCode = item.RoleEntity!.Code })
            .SingleAsync());
        manager.IsActive.Should().BeTrue();
        manager.RoleCode.Should().Be("DESIGN");
        (await WithDbAsync(db => db.OperationalProjectMembers.AnyAsync(member =>
            member.OperationalProjectId == project.OperationalProjectId &&
            member.UserId == manager.Id && member.EndedAt == null))).Should().BeTrue();
        var originalCount = await WithDbAsync(db => db.ProjectDocuments.CountAsync(item =>
            item.OperationalProjectId == project.OperationalProjectId));
        if (!assigned)
        {
            await WithDbAsync(async db =>
            {
                (await db.DesignProjects.SingleAsync(item => item.Id == project.Id)).ProjectManagerUserId = null;
                (await db.OperationalProjects.SingleAsync(item => item.Id == project.OperationalProjectId)).ProjectManagerUserId = null;
                foreach (var member in await db.OperationalProjectMembers.Where(item =>
                    item.OperationalProjectId == project.OperationalProjectId && item.UserId == manager.Id).ToListAsync())
                    member.EndedAt = DateTime.UtcNow.AddMinutes(-1);
                await db.SaveChangesAsync();
            });
        }

        await AuthTestHelper.AuthenticateAsync(Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN"));

        using var content = new MultipartFormDataContent();
        var pdfBytes = System.Text.Encoding.ASCII.GetBytes(
            "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\n%%EOF\n");
        var file = new ByteArrayContent(pdfBytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "NICON-Concept-Architecture-Review.pdf");
        content.Add(new StringContent("DesignConcept"), "category");
        content.Add(new StringContent("Design"), "sourceModule");
        content.Add(new StringContent("ConceptOption"), "sourceEntityType");
        content.Add(new StringContent(fixture.ConceptOptionId.ToString(
            System.Globalization.CultureInfo.InvariantCulture)), "sourceRecordId");

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/operational-projects/{project.OperationalProjectId}/documents")
        {
            Content = content,
        };
        request.Headers.Add("Idempotency-Key", $"design-upload-audit-{Guid.NewGuid():N}");
        using var response = await Client.SendAsync(request);

        if (!assigned)
        {
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
            (await WithDbAsync(db => db.ProjectDocuments.CountAsync(item =>
                item.OperationalProjectId == project.OperationalProjectId))).Should().Be(originalCount);
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "the active assigned Design Manager with design.projects.manage must reach the design upload fallback; " +
            await response.Content.ReadAsStringAsync());
        var uploaded = await ReadJsonAsync(response);
        uploaded.GetProperty("sourceModule").GetString().Should().Be("Design");
        uploaded.GetProperty("sourceEntityType").GetString().Should().Be("ConceptOption");
        uploaded.GetProperty("sourceRecordId").GetInt64().Should().Be(fixture.ConceptOptionId);
        uploaded.GetProperty("category").GetString().Should().Be("DesignConcept");
    }

    private async Task<Fixture> CreateFixtureAsync() => await WithDbAsync(async db =>
    {
        var manager = await UserAsync(db, "DESIGN");
        var lead = await UserAsync(db, "DESIGN_LEAD");
        var recipient = await UserAsync(db, "SALE");
        var customer = new Customer
        {
            Name = $"Khách hàng hồ sơ thiết kế {Guid.NewGuid():N}",
            Type = CustomerType.Company,
            SourceCode = "referral",
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var operational = new OperationalProject
        {
            Code = UniqueSlug("OP-DR").ToUpperInvariant(),
            Name = "Nhà máy NICON - trình duyệt Concept",
            CustomerId = customer.Id,
            ProjectManagerUserId = manager.Id,
        };
        db.OperationalProjects.Add(operational);
        await db.SaveChangesAsync();
        var designProject = new DesignProject
        {
            OperationalProjectId = operational.Id,
            ProjectCode = UniqueSlug("DP-DR").ToUpperInvariant(),
            Name = "Thiết kế nhà máy NICON",
            CustomerId = customer.Id,
            ProjectManagerUserId = manager.Id,
            DesignLeadUserId = lead.Id,
            CurrentStage = DesignProjectStage.Concept,
        };
        db.DesignProjects.Add(designProject);
        var assignee = Member(operational.Id, manager.Id, manager.Id, "Kiến trúc sư");
        assignee.Roles.Add(new OperationalProjectMemberRole
        {
            RoleCode = ProjectTeamRoleCode.Architect,
            Scope = ProjectRoleScope.Project,
            StartedAt = assignee.StartedAt,
        });
        var approver = Member(operational.Id, lead.Id, manager.Id, "Design Lead");
        approver.Roles.Add(new OperationalProjectMemberRole
        {
            RoleCode = ProjectTeamRoleCode.DesignLead,
            Scope = ProjectRoleScope.Project,
            StartedAt = approver.StartedAt,
        });
        db.OperationalProjectMembers.AddRange(assignee, approver);
        await db.SaveChangesAsync();
        var phase = new DesignSchedulePhase
        {
            OperationalProjectId = operational.Id,
            DesignProjectId = designProject.Id,
            Code = DesignSchedulePhaseCode.Concept,
            PlannedStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
            PlannedEnd = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = DesignScheduleStatus.Completed,
            ProgressPercent = 100,
            Weight = 100,
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
        };
        var concept = new ConceptOption
        {
            DesignProjectId = designProject.Id,
            Name = "Phương án Concept R01",
            Status = ConceptOptionStatus.Drafting,
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
        };
        db.AddRange(phase, concept);
        await db.SaveChangesAsync();
        db.DesignScheduleTasks.Add(new DesignScheduleTask
        {
            OperationalProjectId = operational.Id,
            DesignProjectId = designProject.Id,
            PhaseId = phase.Id,
            Code = "CON-ARC-001",
            Name = "Hoàn thiện Concept kiến trúc",
            DepartmentCode = "DESIGN",
            AssigneeMemberId = assignee.Id,
            ApproverMemberId = approver.Id,
            PlannedStart = phase.PlannedStart,
            PlannedEnd = phase.PlannedEnd,
            ActualEnd = phase.PlannedEnd,
            Status = DesignScheduleStatus.Completed,
            ApprovalStatus = DesignTaskApprovalStatus.Approved,
            ProgressPercent = 100,
            Weight = 100,
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
        });
        var document = new ProjectDocument
        {
            OperationalProjectId = operational.Id,
            Category = ProjectDocumentCategory.DesignConcept,
            SourceModule = ProjectDocumentSourceModule.Design,
            SourceEntityType = nameof(ConceptOption),
            SourceRecordId = concept.Id,
            LocalPath = $"/files/project-documents/concept-{Guid.NewGuid():N}.pdf",
            OriginalFileName = "NICON-Concept-R01.pdf",
            ContentType = "application/pdf",
            Sha256 = new string('a', 64),
            SyncStatus = ProjectDocumentSyncStatus.Synced,
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
        };
        db.ProjectDocuments.Add(document);
        await db.SaveChangesAsync();
        var perspective = new ProjectDocument
        {
            OperationalProjectId = operational.Id,
            Category = ProjectDocumentCategory.DesignConcept,
            SourceModule = ProjectDocumentSourceModule.Design,
            SourceEntityType = nameof(ConceptOption),
            SourceRecordId = concept.Id,
            LocalPath = $"/files/project-documents/concept-perspective-{Guid.NewGuid():N}.jpg",
            OriginalFileName = "NICON-Concept-phoi-canh.jpg",
            ContentType = "image/jpeg",
            Sha256 = new string('b', 64),
            SyncStatus = ProjectDocumentSyncStatus.Synced,
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
        };
        db.ProjectDocuments.Add(perspective);
        await db.SaveChangesAsync();
        var acceptance = new ProjectDocument
        {
            OperationalProjectId = operational.Id,
            Category = ProjectDocumentCategory.DesignConcept,
            SourceModule = ProjectDocumentSourceModule.Design,
            SourceEntityType = nameof(ConceptOption),
            SourceRecordId = concept.Id,
            LocalPath = $"/files/project-documents/TK-M03-{Guid.NewGuid():N}.pdf",
            OriginalFileName = "TK-M03-Concept-NICON-da-ky.pdf",
            ContentType = "application/pdf",
            Sha256 = new string('c', 64),
            SyncStatus = ProjectDocumentSyncStatus.Synced,
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
        };
        db.ProjectDocuments.Add(acceptance);
        await db.SaveChangesAsync();
        var revision = new ConceptRevision
        {
            ConceptOptionId = concept.Id,
            Number = 0,
            CreatedByUserId = manager.Id,
            Artifacts =
            [
                new ConceptArtifact { ProjectDocumentId = document.Id, Kind = ConceptArtifactKind.Presentation, CreatedByUserId = manager.Id },
                new ConceptArtifact { ProjectDocumentId = perspective.Id, Kind = ConceptArtifactKind.Perspective, CreatedByUserId = manager.Id },
            ],
            Waivers =
            [
                new ConceptChecklistWaiver { Kind = ConceptArtifactKind.SiteRoofPlan, Reason = "Không áp dụng trong fixture review", CreatedByUserId = manager.Id },
                new ConceptChecklistWaiver { Kind = ConceptArtifactKind.FloorPlan, Reason = "Không áp dụng trong fixture review", CreatedByUserId = manager.Id },
                new ConceptChecklistWaiver { Kind = ConceptArtifactKind.Section, Reason = "Không áp dụng trong fixture review", CreatedByUserId = manager.Id },
            ],
        };
        db.ConceptRevisions.Add(revision);
        await db.SaveChangesAsync();
        return new Fixture(designProject.Id, concept.Id, recipient.Id, document.Id, perspective.Id,
            acceptance.Id, revision.Id);
    });

    private static OperationalProjectMember Member(int projectId, int userId, int actorId, string position) => new()
    {
        OperationalProjectId = projectId,
        UserId = userId,
        Position = position,
        StartedAt = DateTime.UtcNow.AddDays(-1),
        CreatedByUserId = actorId,
        UpdatedByUserId = actorId,
    };

    private static Task<ApplicationUser> UserAsync(NihomeBackend.Data.AppDbContext db, string roleCode) =>
        db.Users.SingleAsync(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode[roleCode]);

    private async Task<HttpResponseMessage> SendWithKeyAsync(HttpMethod method, string uri, object body)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await Client.SendAsync(request);
    }

    private sealed record Fixture(int DesignProjectId, int ConceptOptionId, int RecipientUserId, long DocumentId,
        long PerspectiveDocumentId, long AcceptanceDocumentId, int ConceptRevisionId);
}
