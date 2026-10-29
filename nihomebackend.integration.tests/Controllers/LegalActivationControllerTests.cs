using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class LegalActivationControllerTests : IntegrationTestBase
{
    public LegalActivationControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SharedAndDesignMemberships_DoNotDuplicateLegalCandidatesOrBlockActivation(bool activateContract)
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var fixture = await SeedFixtureAsync();
        var designOnlyUserId = await AddDesignMembershipsAsync(fixture);
        var request = ActivationRequest(fixture, includePermit: true);
        using var response = activateContract
            ? await PostActivationAsync(fixture.ContractId, request, Guid.NewGuid().ToString("N"))
            : await PostSetupAsync(fixture.ProjectId, request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var preview = await ReadJsonAsync(await Client.GetAsync($"/api/operational-projects/{fixture.ProjectId}/legal-setup"));
        var memberIds = preview.GetProperty("projectMembers").EnumerateArray().Select(item => item.GetProperty("userId").GetInt32()).ToList();
        memberIds.Should().BeEquivalentTo(new[] { fixture.LegalLeadUserId, fixture.AssigneeUserId });
        memberIds.Should().NotContain(designOnlyUserId);
        (await CountsAsync(fixture.ProjectId)).Should().Be((1, 1, activateContract ? 1 : 0));
    }

    [Fact]
    public async Task PreviewIsReadOnly_DirectTransitionIsBlocked_ActivationIsAtomicAndIdempotent()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var fixture = await SeedFixtureAsync();
        var before = await CountsAsync(fixture.ProjectId);

        var previewResponse = await Client.GetAsync($"/api/contracts/{fixture.ContractId}/legal-activation-preview");
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
        var preview = await ReadJsonAsync(previewResponse);
        preview.GetProperty("eligible").GetBoolean().Should().BeTrue();
        preview.GetProperty("operationalProjectId").GetInt32().Should().Be(fixture.ProjectId);
        (await CountsAsync(fixture.ProjectId)).Should().Be(before);

        var direct = await Client.PostAsJsonAsync($"/api/contracts/{fixture.ContractId}/transition", new
        {
            newStatus = "InProgress",
            rowVersion = fixture.RowVersion,
        });
        direct.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(direct)).GetProperty("code").GetString().Should().Be("LEGAL_ACTIVATION_REQUIRED");

        var request = ActivationRequest(fixture, includePermit: true);
        var activated = await PostActivationAsync(fixture.ContractId, request, Guid.NewGuid().ToString("N"));
        activated.StatusCode.Should().Be(HttpStatusCode.OK, await activated.Content.ReadAsStringAsync());
        var body = await ReadJsonAsync(activated);
        var packageId = body.GetProperty("workPackageId").GetInt32();
        body.GetProperty("contract").GetProperty("status").GetString().Should().Be("InProgress");

        var retry = await PostActivationAsync(fixture.ContractId, request, Guid.NewGuid().ToString("N"));
        retry.StatusCode.Should().Be(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
        (await ReadJsonAsync(retry)).GetProperty("workPackageId").GetInt32().Should().Be(packageId);

        await WithDbAsync(async db =>
        {
            (await db.Contracts.FindAsync(fixture.ContractId))!.Status.Should().Be(ContractStatus.InProgress);
            (await db.DepartmentWorkPackages.CountAsync(item => item.OperationalProjectId == fixture.ProjectId &&
                item.Purpose == WorkPackagePurpose.LegalPermitting)).Should().Be(1);
            (await db.DepartmentWorkTasks.CountAsync(item => item.WorkPackageId == packageId)).Should().Be(1);
            (await db.PermitChecklistItems.CountAsync(item => item.OperationalProjectId == fixture.ProjectId)).Should().Be(1);
            (await db.ContractLegalActivations.CountAsync(item => item.ContractId == fixture.ContractId)).Should().Be(1);
        });
    }

    [Fact]
    public async Task InvalidPermitRollsBack_AndSecondContractReusesExistingPackage()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var fixture = await SeedFixtureAsync();
        var invalid = ActivationRequest(fixture, includePermit: true, selfApprove: true);

        var rejected = await PostActivationAsync(fixture.ContractId, invalid, Guid.NewGuid().ToString("N"));

        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(rejected)).GetProperty("code").GetString().Should().Be("LEGAL_SELF_APPROVAL");
        await WithDbAsync(async db =>
        {
            (await db.Contracts.FindAsync(fixture.ContractId))!.Status.Should().Be(ContractStatus.Signed);
            (await db.DepartmentWorkPackages.CountAsync(item => item.OperationalProjectId == fixture.ProjectId)).Should().Be(0);
            (await db.PermitChecklistItems.CountAsync(item => item.OperationalProjectId == fixture.ProjectId)).Should().Be(0);
        });

        var first = await PostActivationAsync(fixture.ContractId,
            ActivationRequest(fixture, includePermit: true), Guid.NewGuid().ToString("N"));
        first.EnsureSuccessStatusCode();
        var firstPackageId = (await ReadJsonAsync(first)).GetProperty("workPackageId").GetInt32();
        var second = await SeedContractAsync(fixture.ProjectId, fixture.CustomerId, "HD-LEGAL-SECOND");
        var reusePreview = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{second.ContractId}/legal-activation-preview"));

        var reused = await PostActivationAsync(second.ContractId,
            ActivationRequest(fixture with { ContractId = second.ContractId, RowVersion = second.RowVersion }, includePermit: false,
                workPackageRowVersion: reusePreview.GetProperty("workPackageRowVersion").GetString()),
            Guid.NewGuid().ToString("N"));

        reused.StatusCode.Should().Be(HttpStatusCode.OK, await reused.Content.ReadAsStringAsync());
        var reusedBody = await ReadJsonAsync(reused);
        reusedBody.GetProperty("workPackageId").GetInt32().Should().Be(firstPackageId);
        reusedBody.GetProperty("reusedExistingPackage").GetBoolean().Should().BeTrue();
        await WithDbAsync(async db =>
        {
            (await db.DepartmentWorkPackages.CountAsync(item => item.OperationalProjectId == fixture.ProjectId &&
                item.Purpose == WorkPackagePurpose.LegalPermitting)).Should().Be(1);
            (await db.PermitChecklistItems.CountAsync(item => item.OperationalProjectId == fixture.ProjectId)).Should().Be(1);
            (await db.ContractLegalActivations.CountAsync(item => item.OperationalProjectId == fixture.ProjectId)).Should().Be(2);
        });
    }

    [Fact]
    public async Task NonProjectManagerCannotPreviewOrActivate()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var fixture = await SeedFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));

        (await Client.GetAsync($"/api/contracts/{fixture.ContractId}/legal-activation-preview")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        var response = await PostActivationAsync(fixture.ContractId,
            ActivationRequest(fixture, includePermit: true), Guid.NewGuid().ToString("N"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StaleContractVersionRejectsActivationWithoutPartialWrites()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var fixture = await SeedFixtureAsync();
        await WithDbAsync(async db =>
        {
            var contract = await db.Contracts.SingleAsync(item => item.Id == fixture.ContractId);
            contract.Note = "Concurrent contract update";
            await db.SaveChangesAsync();
        });

        var response = await PostActivationAsync(fixture.ContractId,
            ActivationRequest(fixture, includePermit: true), Guid.NewGuid().ToString("N"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("CONCURRENCY_CONFLICT");
        await WithDbAsync(async db =>
        {
            (await db.Contracts.FindAsync(fixture.ContractId))!.Status.Should().Be(ContractStatus.Signed);
            (await db.DepartmentWorkPackages.CountAsync(item => item.OperationalProjectId == fixture.ProjectId))
                .Should().Be(0);
            (await db.PermitChecklistItems.CountAsync(item => item.OperationalProjectId == fixture.ProjectId))
                .Should().Be(0);
            (await db.ContractLegalActivations.CountAsync(item => item.ContractId == fixture.ContractId))
                .Should().Be(0);
        });
    }

    [Fact]
    public async Task LegacyProjectSetup_LinksExistingPermitWithoutLosingDesignProject()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var fixture = await SeedFixtureAsync();
        var legacy = await WithDbAsync(async db =>
        {
            var design = new DesignProject
            {
                Name = "Legacy legal design workspace",
                CustomerId = fixture.CustomerId,
                OperationalProjectId = fixture.ProjectId,
            };
            db.DesignProjects.Add(design);
            await db.SaveChangesAsync();
            var permit = new PermitChecklistItem
            {
                OperationalProjectId = fixture.ProjectId,
                DesignProjectId = design.Id,
                PermitTypeCode = "gpxd",
                Status = PermitStatus.Preparing,
            };
            db.PermitChecklistItems.Add(permit);
            await db.SaveChangesAsync();
            return (DesignProjectId: design.Id, PermitId: permit.Id);
        });
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/legal-setup")
        {
            Content = JsonContent.Create(new
            {
                legalLeadUserId = fixture.LegalLeadUserId,
                templateId = fixture.TemplateId,
                templateRowVersion = fixture.TemplateRowVersion,
                plannedStart = DateTime.UtcNow.Date,
                plannedEnd = DateTime.UtcNow.Date.AddMonths(6),
                permits = new[]
                {
                    new
                    {
                        permitTypeCode = "gpxd",
                        issuingAgency = "Sở Xây dựng",
                        assigneeUserId = fixture.AssigneeUserId,
                        approverUserId = fixture.LegalLeadUserId,
                        plannedStart = DateTime.UtcNow.Date,
                        plannedEnd = DateTime.UtcNow.Date.AddMonths(3),
                        predecessorPermitTypeCodes = Array.Empty<string>(),
                    },
                },
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        await WithDbAsync(async db =>
        {
            var permit = await db.PermitChecklistItems.SingleAsync(item => item.Id == legacy.PermitId);
            permit.DesignProjectId.Should().Be(legacy.DesignProjectId);
            permit.WorkTaskId.Should().NotBeNull();
            (await db.PermitChecklistItems.CountAsync(item =>
                item.OperationalProjectId == fixture.ProjectId && item.PermitTypeCode == "gpxd")).Should().Be(1);
            (await db.DepartmentWorkPackages.CountAsync(item =>
                item.OperationalProjectId == fixture.ProjectId && item.Purpose == WorkPackagePurpose.LegalPermitting))
                .Should().Be(1);
        });
    }

    [Fact]
    public async Task MissingRequiredPermitAndUnpublishedTemplateDoNotActivateContract()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var fixture = await SeedFixtureAsync();
        var before = await CountsAsync(fixture.ProjectId);
        var missing = await PostActivationAsync(fixture.ContractId, ActivationRequest(fixture, false), Guid.NewGuid().ToString("N"));
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(missing)).GetProperty("code").GetString().Should().Be("LEGAL_REQUIRED_PERMITS_MISSING");
        (await CountsAsync(fixture.ProjectId)).Should().Be(before);
        await WithDbAsync(async db =>
        {
            (await db.LegalChecklistTemplates.FindAsync(fixture.TemplateId))!.Status = LegalChecklistTemplateStatus.Draft;
            await db.SaveChangesAsync();
        });
        var draft = await PostActivationAsync(fixture.ContractId, ActivationRequest(fixture, true), Guid.NewGuid().ToString("N"));
        draft.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(draft)).GetProperty("code").GetString().Should().Be("LEGAL_TEMPLATE_NOT_PUBLISHED");
        (await CountsAsync(fixture.ProjectId)).Should().Be(before);
    }

    [Fact]
    public async Task ContractActivationCanApplyTemplateToExistingLegacyPackage()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var fixture = await SeedFixtureAsync();
        (await PostActivationAsync(fixture.ContractId, ActivationRequest(fixture, true), Guid.NewGuid().ToString("N"))).EnsureSuccessStatusCode();
        var original = await WithDbAsync(async db =>
        {
            var package = await db.DepartmentWorkPackages.SingleAsync(x => x.OperationalProjectId == fixture.ProjectId);
            package.LegalChecklistTemplateId = null; package.LegalChecklistSnapshotJson = null;
            var permit = await db.PermitChecklistItems.SingleAsync(x => x.OperationalProjectId == fixture.ProjectId);
            permit.IsRequired = false; permit.Note = "Legacy permit evidence retained";
            await db.SaveChangesAsync();
            return (package.Id, package.PlannedStart, package.PlannedEnd);
        });
        var second = await SeedContractAsync(fixture.ProjectId, fixture.CustomerId, "HD-LEGACY-TEMPLATE");
        var preview = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{second.ContractId}/legal-activation-preview"));
        var response = await PostActivationAsync(second.ContractId, new
        {
            templateId = fixture.TemplateId,
            templateRowVersion = fixture.TemplateRowVersion,
            workPackageRowVersion = preview.GetProperty("workPackageRowVersion").GetString(),
            rowVersion = second.RowVersion,
            legalLeadUserId = fixture.LegalLeadUserId,
            plannedStart = DateTime.UtcNow.Date,
            plannedEnd = DateTime.UtcNow.Date.AddMonths(9),
            permits = Array.Empty<object>(),
        }, Guid.NewGuid().ToString("N"));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        await WithDbAsync(async db =>
        {
            var package = await db.DepartmentWorkPackages.SingleAsync(x => x.Id == original.Id);
            package.LegalChecklistTemplateId.Should().Be(fixture.TemplateId);
            package.PlannedStart.Should().Be(original.PlannedStart); package.PlannedEnd.Should().Be(original.PlannedEnd);
            var permit = await db.PermitChecklistItems.SingleAsync(x => x.OperationalProjectId == fixture.ProjectId);
            permit.IsRequired.Should().BeTrue(); permit.Note.Should().Be("Legacy permit evidence retained");
            (await db.DepartmentWorkTasks.CountAsync(x => x.WorkPackageId == package.Id)).Should().Be(1);
        });
    }

    [Fact]
    public async Task GenericWorkPackageEndpointCannotBypassLegalTemplateSetup()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var fixture = await SeedFixtureAsync();
        var before = await CountsAsync(fixture.ProjectId);
        var groupId = await WithDbAsync(db => db.RoleGroups.Where(x => x.Code == "CONSTRUCTION").Select(x => x.Id).SingleAsync());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/work-packages")
        {
            Content = JsonContent.Create(new
            {
                kind = "Project",
                purpose = "LegalPermitting",
                name = "NICON legal template bypass attempt",
                operationalProjectId = fixture.ProjectId,
                departmentRoleGroupId = groupId,
                leadUserId = fixture.LegalLeadUserId,
                plannedStart = DateTime.UtcNow.Date,
                plannedEnd = DateTime.UtcNow.Date.AddMonths(6),
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CountsAsync(fixture.ProjectId)).Should().Be(before);
    }

    [Theory]
    [InlineData(WorkPackageStatus.Completed)]
    [InlineData(WorkPackageStatus.Cancelled)]
    public async Task ClosedLegalPackageRejectsSetupAndSecondContractWithoutWrites(WorkPackageStatus status)
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var fixture = await SeedFixtureAsync();
        (await PostActivationAsync(fixture.ContractId, ActivationRequest(fixture, true), Guid.NewGuid().ToString("N"))).EnsureSuccessStatusCode();
        await WithDbAsync(async db =>
        {
            var package = await db.DepartmentWorkPackages.SingleAsync(x => x.OperationalProjectId == fixture.ProjectId);
            package.Status = status;
            package.LegalChecklistTemplateId = null;
            package.LegalChecklistSnapshotJson = null;
            (await db.DepartmentWorkTasks.SingleAsync(x => x.WorkPackageId == package.Id)).Status = WorkTaskStatus.Verified;
            await db.SaveChangesAsync();
        });
        var second = await SeedContractAsync(fixture.ProjectId, fixture.CustomerId, "HD-CLOSED-LEGAL");
        var before = await LegalStateAsync(fixture.ProjectId);
        var body = await ExistingSetupBodyAsync(fixture);
        var extra = body["permits"]![0]!.DeepClone();
        extra["permitTypeCode"] = "pccc";
        body["permits"]!.AsArray().Add(extra);
        var response = await PostSetupAsync(fixture.ProjectId, body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("LEGAL_PACKAGE_CLOSED");
        body["rowVersion"] = second.RowVersion;
        response = await PostActivationAsync(second.ContractId, body, Guid.NewGuid().ToString("N"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("LEGAL_PACKAGE_CLOSED");
        (await LegalStateAsync(fixture.ProjectId)).Should().Be(before);
        await WithDbAsync(async db => (await db.Contracts.FindAsync(second.ContractId))!.Status.Should().Be(ContractStatus.Signed));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task StalePackageRejectsSetupAndActivationWithoutOverwritingHistory(bool legacy, bool activate)
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var fixture = await SeedFixtureAsync();
        (await PostActivationAsync(fixture.ContractId, ActivationRequest(fixture, true), Guid.NewGuid().ToString("N"))).EnsureSuccessStatusCode();
        if (legacy)
            await WithDbAsync(async db =>
            {
                var package = await db.DepartmentWorkPackages.SingleAsync(x => x.OperationalProjectId == fixture.ProjectId);
                package.LegalChecklistTemplateId = null;
                package.LegalChecklistSnapshotJson = null;
                await db.SaveChangesAsync();
            });
        var body = await ExistingSetupBodyAsync(fixture);
        await WithDbAsync(async db =>
        {
            var package = await db.DepartmentWorkPackages.SingleAsync(x => x.OperationalProjectId == fixture.ProjectId);
            package.PlannedEnd = package.PlannedEnd!.Value.AddMonths(2);
            await db.SaveChangesAsync();
        });
        var second = await SeedContractAsync(fixture.ProjectId, fixture.CustomerId, "HD-STALE-LEGAL");
        body["rowVersion"] = second.RowVersion;
        var before = await LegalStateAsync(fixture.ProjectId);
        var response = activate
            ? await PostActivationAsync(second.ContractId, body, Guid.NewGuid().ToString("N"))
            : await PostSetupAsync(fixture.ProjectId, body);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await ReadJsonAsync(response)).GetProperty("code").GetString().Should()
            .Be(activate ? "CONCURRENCY_CONFLICT" : "crm_concurrency_conflict");
        (await LegalStateAsync(fixture.ProjectId)).Should().Be(before);
        await WithDbAsync(async db => (await db.Contracts.FindAsync(second.ContractId))!.Status.Should().Be(ContractStatus.Signed));
    }

    [Fact]
    public async Task ExistingPackageRequiresTokenAndAuditsConfirmedScheduleChanges()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var fixture = await SeedFixtureAsync();
        (await PostActivationAsync(fixture.ContractId, ActivationRequest(fixture, true), Guid.NewGuid().ToString("N"))).EnsureSuccessStatusCode();
        var body = await ExistingSetupBodyAsync(fixture);
        var token = body["workPackageRowVersion"]!.DeepClone();
        body.Remove("workPackageRowVersion");
        var before = await LegalStateAsync(fixture.ProjectId);
        (await PostSetupAsync(fixture.ProjectId, body)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LegalStateAsync(fixture.ProjectId)).Should().Be(before);
        body["workPackageRowVersion"] = token;
        var newEnd = DateTime.UtcNow.Date.AddMonths(8);
        body["plannedEnd"] = JsonValue.Create(newEnd);
        var response = await PostSetupAsync(fixture.ProjectId, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        await WithDbAsync(async db =>
        {
            var package = await db.DepartmentWorkPackages.SingleAsync(x => x.OperationalProjectId == fixture.ProjectId);
            package.PlannedEnd.Should().Be(newEnd);
            var history = await db.DepartmentWorkPackageHistory.SingleAsync(x => x.WorkPackageId == package.Id && x.Action == "UpdatedByLegalSetup");
            var snapshot = JsonDocument.Parse(history.SnapshotJson).RootElement;
            snapshot.GetProperty("Before").GetProperty("PlannedEnd").GetDateTime().Should().Be(DateTime.UtcNow.Date.AddMonths(6));
            snapshot.GetProperty("After").GetProperty("PlannedEnd").GetDateTime().Should().Be(newEnd);
            history.ChangedByUserId.Should().BeGreaterThan(0);
        });
        // A fresh retry with unchanged configuration must not add a second change event.
        (await PostSetupAsync(fixture.ProjectId, await ExistingSetupBodyAsync(fixture, newEnd))).EnsureSuccessStatusCode();
        await WithDbAsync(async db =>
            (await db.DepartmentWorkPackageHistory.CountAsync(x => x.WorkPackage.OperationalProjectId == fixture.ProjectId && x.Action == "UpdatedByLegalSetup"))
                .Should().Be(1));
    }

    [Fact]
    public async Task NewPermitCanDependOnConfiguredPermitButNotUnconfiguredOrOtherProjectPermit()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var fixture = await SeedFixtureAsync();
        (await PostActivationAsync(fixture.ContractId, ActivationRequest(fixture, true), Guid.NewGuid().ToString("N"))).EnsureSuccessStatusCode();
        await WithDbAsync(async db =>
        {
            db.PermitChecklistItems.Add(new PermitChecklistItem
            {
                OperationalProjectId = fixture.ProjectId,
                PermitTypeCode = "unconfigured",
                CreatedByUserId = fixture.LegalLeadUserId,
                UpdatedByUserId = fixture.LegalLeadUserId,
            });
            var otherProject = new OperationalProject
            {
                Code = UniqueSlug("OP-OTHER-LEGAL").ToUpperInvariant(),
                Name = "NICON independent factory legal dossier",
                CustomerId = fixture.CustomerId,
            };
            var package = new DepartmentWorkPackage
            {
                Code = UniqueSlug("LEGAL-OTHER"),
                Name = "Separate factory permitting",
                OperationalProject = otherProject,
                Purpose = WorkPackagePurpose.LegalPermitting,
                DepartmentRoleGroupId = await db.RoleGroups.Where(x => x.Code == "CONSTRUCTION").Select(x => x.Id).SingleAsync(),
                LeadUserId = fixture.LegalLeadUserId,
            };
            var task = new DepartmentWorkTask { WorkPackage = package, WbsCode = "L-01", Title = "Other factory permit", AssigneeUserId = fixture.AssigneeUserId, ApproverUserId = fixture.LegalLeadUserId };
            db.PermitChecklistItems.Add(new PermitChecklistItem
            {
                OperationalProject = otherProject,
                PermitTypeCode = "other-project-permit",
                WorkTask = task,
                CreatedByUserId = fixture.LegalLeadUserId,
                UpdatedByUserId = fixture.LegalLeadUserId,
            });
            await db.SaveChangesAsync();
        });
        var body = await ExistingSetupBodyAsync(fixture);
        var added = body["permits"]![0]!;
        added["permitTypeCode"] = "pccc";
        var before = await LegalStateAsync(fixture.ProjectId);
        foreach (var code in new[] { "unknown-permit", "unconfigured", "other-project-permit" })
        {
            added["predecessorPermitTypeCodes"] = new JsonArray(code);
            var rejected = await PostSetupAsync(fixture.ProjectId, body);
            rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ReadJsonAsync(rejected)).GetProperty("code").GetString().Should().Be("LEGAL_PREDECESSOR_INVALID");
            (await LegalStateAsync(fixture.ProjectId)).Should().Be(before);
        }
        added["predecessorPermitTypeCodes"] = new JsonArray("gpxd");
        var response = await PostSetupAsync(fixture.ProjectId, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        await WithDbAsync(async db =>
        {
            var permits = await db.PermitChecklistItems.Where(x => x.OperationalProjectId == fixture.ProjectId).ToDictionaryAsync(x => x.PermitTypeCode);
            var link = await db.DepartmentWorkTaskDependencies.SingleAsync(x => x.WorkTaskId == permits["pccc"].WorkTaskId);
            link.PredecessorTaskId.Should().Be(permits["gpxd"].WorkTaskId);
            permits.Count.Should().Be(3);
        });
    }

    [Fact]
    public async Task SecondContractCanReuseCompletedLegalPackageWithoutMutatingItsDossier()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var fixture = await SeedFixtureAsync();
        (await PostActivationAsync(fixture.ContractId, ActivationRequest(fixture, true), Guid.NewGuid().ToString("N"))).EnsureSuccessStatusCode();
        await WithDbAsync(async db =>
        {
            var package = await db.DepartmentWorkPackages.SingleAsync(x => x.OperationalProjectId == fixture.ProjectId);
            package.Status = WorkPackageStatus.Completed;
            (await db.DepartmentWorkTasks.SingleAsync(x => x.WorkPackageId == package.Id)).Status = WorkTaskStatus.Verified;
            var permit = await db.PermitChecklistItems.SingleAsync(x => x.OperationalProjectId == fixture.ProjectId);
            permit.Status = PermitStatus.Issued;
            permit.IssuedAt = DateTime.UtcNow;
            permit.IssuedFilePath = "/files/business-documents/permits/nicon-factory-issued.pdf";
            await db.SaveChangesAsync();
        });
        var second = await SeedContractAsync(fixture.ProjectId, fixture.CustomerId, "HD-COMPLETED-LEGAL");
        var body = await ExistingSetupBodyAsync(fixture);
        body["permits"] = new JsonArray();
        body["rowVersion"] = second.RowVersion;
        var before = await LegalStateAsync(fixture.ProjectId, includeActivations: false);
        var response = await PostActivationAsync(second.ContractId, body, Guid.NewGuid().ToString("N"));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await LegalStateAsync(fixture.ProjectId, includeActivations: false)).Should().Be(before);
        await WithDbAsync(async db =>
        {
            (await db.Contracts.FindAsync(second.ContractId))!.Status.Should().Be(ContractStatus.InProgress);
            (await db.ContractLegalActivations.CountAsync(x => x.OperationalProjectId == fixture.ProjectId)).Should().Be(2);
        });
    }

    private async Task<JsonObject> ExistingSetupBodyAsync(LegalFixture fixture, DateTime? end = null)
    {
        var preview = await ReadJsonAsync(await Client.GetAsync($"/api/operational-projects/{fixture.ProjectId}/legal-setup"));
        var body = JsonSerializer.SerializeToNode(ActivationRequest(fixture, true,
            workPackageRowVersion: preview.GetProperty("workPackageRowVersion").GetString()))!.AsObject();
        if (end.HasValue) body["plannedEnd"] = JsonValue.Create(end.Value);
        return body;
    }

    private async Task<HttpResponseMessage> PostSetupAsync(int projectId, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/operational-projects/{projectId}/legal-setup") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await Client.SendAsync(request);
    }

    private Task<string> LegalStateAsync(int projectId, bool includeActivations = true) => WithDbAsync(async db =>
    {
        var package = await db.DepartmentWorkPackages.AsNoTracking().SingleAsync(x => x.OperationalProjectId == projectId);
        return JsonSerializer.Serialize(new
        {
            package,
            Tasks = await db.DepartmentWorkTasks.AsNoTracking().Where(x => x.WorkPackageId == package.Id).OrderBy(x => x.Id).ToListAsync(),
            Permits = await db.PermitChecklistItems.AsNoTracking().Where(x => x.OperationalProjectId == projectId).OrderBy(x => x.Id).ToListAsync(),
            History = await db.DepartmentWorkPackageHistory.AsNoTracking().Where(x => x.WorkPackageId == package.Id).OrderBy(x => x.Id).ToListAsync(),
            Activations = includeActivations ? await db.ContractLegalActivations.AsNoTracking().CountAsync(x => x.OperationalProjectId == projectId) : 0,
        });
    });

    private Task<int> AddDesignMembershipsAsync(LegalFixture fixture) => WithDbAsync(async db =>
    {
        var architect = await db.Users.SingleAsync(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["ARCHITECT"]);
        var flow = new DesignProject
        {
            OperationalProjectId = fixture.ProjectId,
            CustomerId = fixture.CustomerId,
            ProjectCode = UniqueSlug("DP-LEGAL"),
            Name = "NICON – Luồng thiết kế nhà máy",
            StartDate = DateTime.UtcNow.Date,
            Deadline = DateTime.UtcNow.Date.AddMonths(6),
        };
        db.DesignProjects.Add(flow);
        foreach (var userId in new[] { fixture.LegalLeadUserId, architect.Id })
            db.OperationalProjectMembers.Add(new OperationalProjectMember
            {
                OperationalProjectId = fixture.ProjectId,
                DesignProject = flow,
                UserId = userId,
                Position = "NICON – Nhóm thiết kế nhà máy",
                StartedAt = DateTime.UtcNow.AddDays(-1),
            });
        await db.SaveChangesAsync();
        return architect.Id;
    });

    private async Task<LegalFixture> SeedFixtureAsync()
    {
        var fixture = await WithDbAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(item => item.PhoneNumber == TestDataSeeder.SuperAdminPhone);
            var legal = await db.Users.SingleAsync(item => item.PhoneNumber ==
                TestDataSeeder.BusinessRolePhonesByCode["LEGAL_OFFICER"]);
            var assignee = await db.Users.SingleAsync(item => item.PhoneNumber ==
                TestDataSeeder.BusinessRolePhonesByCode["DESIGN"]);
            var customer = new Customer { Name = $"Legal customer {Guid.NewGuid():N}", Type = CustomerType.Company };
            var project = new OperationalProject
            {
                Code = UniqueSlug("OP-LEGAL").ToUpperInvariant(),
                Name = "Nhà máy công nghiệp NICON",
                Customer = customer,
                ProjectManagerUserId = admin.Id,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            foreach (var (user, role) in new[]
            {
                (legal, ProjectTeamRoleCode.LegalOfficer),
                (assignee, ProjectTeamRoleCode.Architect),
            })
                db.OperationalProjectMembers.Add(new OperationalProjectMember
                {
                    OperationalProjectId = project.Id,
                    UserId = user.Id,
                    Position = role.ToString(),
                    StartedAt = DateTime.UtcNow.AddDays(-10),
                    CreatedByUserId = admin.Id,
                    UpdatedByUserId = admin.Id,
                    Roles =
                    [
                        new OperationalProjectMemberRole
                        {
                            RoleCode = role,
                            StartedAt = DateTime.UtcNow.AddDays(-10),
                        },
                    ],
                });
            await db.SaveChangesAsync();
            var template = new LegalChecklistTemplate
            {
                Code = UniqueSlug("LEGAL-TEMPLATE"),
                NameVi = "Mẫu pháp lý nhà máy",
                Name = "Factory permits",
                NameZh = "工厂许可",
                NameJa = "工場許可",
                ScopeVi = "Mẫu kiểm thử",
                Scope = "Test fixture",
                ScopeZh = "测试",
                ScopeJa = "テスト",
                Status = LegalChecklistTemplateStatus.Published,
                ItemsJson = "[{\"PermitTypeCode\":\"gpxd\",\"IsRequired\":true}]",
            };
            db.LegalChecklistTemplates.Add(template);
            await db.SaveChangesAsync();
            return new LegalFixture(project.Id, customer.Id, 0, string.Empty, legal.Id, assignee.Id,
                template.Id, CrmConcurrency.Encode(template.RowVersion));
        });
        var contract = await SeedContractAsync(fixture.ProjectId, fixture.CustomerId, "HD-LEGAL-FIRST");
        return fixture with { ContractId = contract.ContractId, RowVersion = contract.RowVersion };
    }

    private Task<(int ContractId, string RowVersion)> SeedContractAsync(int projectId, int customerId, string prefix) =>
        WithDbAsync(async db =>
        {
            var contract = new Contract
            {
                ContractNumber = UniqueSlug(prefix).ToUpperInvariant(),
                CustomerId = customerId,
                OperationalProjectId = projectId,
                Direction = ContractDirection.Upstream,
                Type = ContractType.DesignAndBuild,
                Status = ContractStatus.Signed,
                SignedDate = DateTime.UtcNow.AddDays(-1),
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(6),
                Value = 2_000_000_000m,
            };
            db.Contracts.Add(contract);
            await db.SaveChangesAsync();
            db.ContractAttachments.Add(new ContractAttachment
            {
                ContractId = contract.Id,
                Kind = ContractAttachmentKind.SignedScan,
                FilePath = $"/files/contracts/{contract.ContractNumber}.pdf",
                OriginalFileName = $"{contract.ContractNumber}.pdf",
            });
            await db.SaveChangesAsync();
            return (contract.Id, CrmConcurrency.Encode(contract.RowVersion));
        });

    private static object ActivationRequest(LegalFixture fixture, bool includePermit, bool selfApprove = false, string? workPackageRowVersion = null) => new
    {
        templateId = fixture.TemplateId,
        templateRowVersion = fixture.TemplateRowVersion,
        workPackageRowVersion,
        legalLeadUserId = fixture.LegalLeadUserId,
        plannedStart = DateTime.UtcNow.Date,
        plannedEnd = DateTime.UtcNow.Date.AddMonths(6),
        rowVersion = fixture.RowVersion,
        permits = includePermit
            ? new object[]
            {
                new
                {
                    permitTypeCode = "gpxd",
                    issuingAgency = "Sở Xây dựng",
                    assigneeUserId = fixture.AssigneeUserId,
                    approverUserId = selfApprove ? fixture.AssigneeUserId : fixture.LegalLeadUserId,
                    plannedStart = DateTime.UtcNow.Date,
                    plannedEnd = DateTime.UtcNow.Date.AddMonths(3),
                    predecessorPermitTypeCodes = Array.Empty<string>(),
                },
            }
            : Array.Empty<object>(),
    };

    private async Task<HttpResponseMessage> PostActivationAsync(int contractId, object body, string idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/contracts/{contractId}/activate-with-legal")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await Client.SendAsync(request);
    }

    private Task<(int Packages, int Permits, int Activations)> CountsAsync(int projectId) => WithDbAsync(async db =>
        (await db.DepartmentWorkPackages.CountAsync(item => item.OperationalProjectId == projectId),
         await db.PermitChecklistItems.CountAsync(item => item.OperationalProjectId == projectId),
         await db.ContractLegalActivations.CountAsync(item => item.OperationalProjectId == projectId)));

    private sealed record LegalFixture(
        int ProjectId,
        int CustomerId,
        int ContractId,
        string RowVersion,
        int LegalLeadUserId,
        int AssigneeUserId,
        int TemplateId,
        string TemplateRowVersion);
}
