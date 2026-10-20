using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class FinanceControllerTests : IntegrationTestBase
{
    public FinanceControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ScopedLists_WithoutAuthentication_ReturnUnauthorized()
    {
        (await Client.GetAsync("/api/finance/payment-requests")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync("/api/finance/periods")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync("/api/finance/corrections")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePayment_AsSaleWithoutPermission_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALE"));
        using var response = await SendAsync(HttpMethod.Post, "/api/finance/payment-requests", ValidPayment(1, 1, 1));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PaymentWorkflow_EnforcesOperationalProjectScope()
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        var existingPayment = await CreatePaymentAsync(fixture);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var phone = $"07{Random.Shared.Next(10000000, 99999999)}";
        var scopedUser = await WithDbAsync(async db =>
        {
            var role = new Role
            {
                Code = $"FIN_SCOPE_{suffix}",
                Name = $"Scoped finance {suffix}",
                IsActive = true,
                InitialPermissionsSeeded = true,
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            var permissionIds = await db.Permissions
                .Where(permission =>
                    (permission.Module == "finance.payments" &&
                     (permission.Action == "view" || permission.Action == "manage")) ||
                    (permission.Module == "crm.contracts" && permission.Action == "view"))
                .Select(permission => permission.Id)
                .ToListAsync();
            db.RolePermissions.AddRange(permissionIds.Select(permissionId => new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permissionId,
            }));
            var user = new ApplicationUser
            {
                PhoneNumber = phone,
                FullName = "Scoped finance tester",
                Email = $"finance-scope-{suffix}@nihome.test",
                Role = UserRole.USER,
                RoleEntityId = role.Id,
                IsActive = true,
            };
            user.PasswordHash = new PasswordService().Hash(user, TestDataSeeder.DefaultPassword);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return (RoleId: role.Id, UserId: user.Id);
        });
        var milestoneId = await WithDbAsync(async db =>
        {
            var contract = await db.Contracts.SingleAsync(item => item.Id == fixture.ContractId);
            contract.OwnerUserId = scopedUser.UserId;
            var milestone = new ContractPaymentMilestone
            {
                ContractId = fixture.ContractId,
                Order = 1,
                Name = "Scoped supplier payment",
                PercentValue = 100m,
                Status = PaymentMilestoneStatus.Pending,
            };
            db.ContractPaymentMilestones.Add(milestone);
            await db.SaveChangesAsync();
            return milestone.Id;
        });

        try
        {
            await AuthTestHelper.AuthenticateAsync(
                Client,
                client => AuthTestHelper.LoginAsync(client, phone, TestDataSeeder.DefaultPassword));

            var hiddenReferences = await ReadJsonAsync(await Client.GetAsync("/api/finance/payment-references"));
            hiddenReferences.GetProperty("contracts").EnumerateArray()
                .Should().NotContain(item => item.GetProperty("id").GetInt32() == fixture.ContractId);
            var hiddenList = await ReadJsonAsync(await Client.GetAsync("/api/finance/payment-requests"));
            hiddenList.EnumerateArray().Should().NotContain(item =>
                item.GetProperty("id").GetInt32() == existingPayment.GetProperty("id").GetInt32());
            using var rejectedCreate = await SendAsync(HttpMethod.Post, "/api/finance/payment-requests",
                ValidPayment(fixture.ContractId, fixture.VendorId, fixture.AccountantId));
            rejectedCreate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            using var rejectedTransition = await SendAsync(
                HttpMethod.Post,
                $"/api/finance/payment-requests/{existingPayment.GetProperty("id").GetInt32()}/submit",
                new { rowVersion = existingPayment.GetProperty("rowVersion").GetString() });
            rejectedTransition.StatusCode.Should().Be(HttpStatusCode.NotFound);
            using var rejectedRevision = await SendAsync(
                HttpMethod.Post,
                $"/api/finance/payment-requests/{existingPayment.GetProperty("id").GetInt32()}/revise",
                new { reason = "Project scope check", rowVersion = existingPayment.GetProperty("rowVersion").GetString() });
            rejectedRevision.StatusCode.Should().Be(HttpStatusCode.NotFound);
            var ownedContract = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{fixture.ContractId}"));
            using var rejectedSchedule = await Client.PutAsJsonAsync(
                $"/api/contracts/{fixture.ContractId}/milestones",
                new
                {
                    rowVersion = ownedContract.GetProperty("rowVersion").GetString(),
                    paymentMilestones = new[]
                    {
                        new
                        {
                            id = milestoneId,
                            order = 1,
                            name = "Unauthorized schedule change",
                            percentValue = 100m,
                            status = "Pending",
                        },
                    },
                });
            rejectedSchedule.StatusCode.Should().Be(HttpStatusCode.NotFound);
            using var rejectedMilestoneStatus = await Client.PatchAsJsonAsync(
                $"/api/contracts/{fixture.ContractId}/milestones/{milestoneId}/status",
                new
                {
                    status = "Requested",
                    responsibleAccountantUserId = fixture.AccountantId,
                    rowVersion = ownedContract.GetProperty("rowVersion").GetString(),
                });
            rejectedMilestoneStatus.StatusCode.Should().Be(HttpStatusCode.NotFound);
            var unchangedMilestone = await WithDbAsync(db => db.ContractPaymentMilestones.AsNoTracking()
                .SingleAsync(item => item.Id == milestoneId));
            unchangedMilestone.Name.Should().Be("Scoped supplier payment");
            unchangedMilestone.Status.Should().Be(PaymentMilestoneStatus.Pending);

            await WithDbAsync(async db =>
            {
                db.OperationalProjectMembers.Add(new OperationalProjectMember
                {
                    OperationalProjectId = fixture.ProjectId,
                    UserId = scopedUser.UserId,
                    Position = "Project accountant",
                    StartedAt = DateTime.UtcNow,
                    CreatedByUserId = scopedUser.UserId,
                    UpdatedByUserId = scopedUser.UserId,
                });
                await db.SaveChangesAsync();
            });

            var visibleReferences = await ReadJsonAsync(await Client.GetAsync("/api/finance/payment-references"));
            visibleReferences.GetProperty("contracts").EnumerateArray()
                .Should().Contain(item => item.GetProperty("id").GetInt32() == fixture.ContractId);
            var visibleList = await ReadJsonAsync(await Client.GetAsync("/api/finance/payment-requests"));
            visibleList.EnumerateArray().Should().Contain(item =>
                item.GetProperty("id").GetInt32() == existingPayment.GetProperty("id").GetInt32());
            using var allowedUpdate = await SendAsync(
                HttpMethod.Put,
                $"/api/finance/payment-requests/{existingPayment.GetProperty("id").GetInt32()}",
                new
                {
                    contractId = fixture.ContractId,
                    vendorId = fixture.VendorId,
                    supplierInvoiceNumber = "INV-2026-002",
                    invoiceDate = "2026-08-01",
                    invoiceAmount = 1000m,
                    currency = "VND",
                    receivedAt = new DateTime(2026, 8, 2, 1, 0, 0, DateTimeKind.Utc),
                    assignedAccountantUserId = fixture.AccountantId,
                    attachments = new[] { new { fileName = "invoice.pdf", filePath = "/files/finance/invoice.pdf" } },
                    rowVersion = existingPayment.GetProperty("rowVersion").GetString(),
                });
            allowedUpdate.StatusCode.Should().Be(HttpStatusCode.OK, await allowedUpdate.Content.ReadAsStringAsync());
        }
        finally
        {
            await WithDbAsync(async db =>
            {
                db.OperationalProjectMembers.RemoveRange(db.OperationalProjectMembers
                    .Where(member => member.UserId == scopedUser.UserId));
                db.Users.RemoveRange(db.Users.Where(user => user.Id == scopedUser.UserId));
                db.RolePermissions.RemoveRange(db.RolePermissions.Where(permission => permission.RoleId == scopedUser.RoleId));
                db.Roles.RemoveRange(db.Roles.Where(role => role.Id == scopedUser.RoleId));
                await db.SaveChangesAsync();
            });
        }
    }

    [Fact]
    public async Task PaymentLifecycle_EnforcesSelfApprovalAndPaidImmutability()
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        var draft = await CreatePaymentAsync(fixture);
        draft.GetProperty("status").GetString().Should().Be("Draft");

        var underValidation = await TransitionAsync("payment-requests", draft, "submit");
        underValidation.GetProperty("submittedByUserId").GetInt32().Should().Be(fixture.AccountantId);
        var ready = await TransitionAsync("payment-requests", underValidation, "validate");
        ready.GetProperty("validatedAt").ValueKind.Should().Be(JsonValueKind.String);

        using var selfApproval = await SendAsync(HttpMethod.Post,
            $"/api/finance/payment-requests/{ready.GetProperty("id").GetInt32()}/decision",
            new { approved = true, rowVersion = ready.GetProperty("rowVersion").GetString() });
        selfApproval.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.PaymentRequests.AsNoTracking().SingleAsync(item => item.Id == ready.GetProperty("id").GetInt32())))
            .Status.Should().Be(PaymentRequestStatus.ReadyForApproval);

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "BGD"));
        var approved = await TransitionAsync("payment-requests", ready, "decision", new
        {
            approved = true,
            rowVersion = ready.GetProperty("rowVersion").GetString(),
        });
        approved.GetProperty("status").GetString().Should().Be("Approved");

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        var paid = await TransitionAsync("payment-requests", approved, "pay");
        paid.GetProperty("status").GetString().Should().Be("Paid");
        paid.GetProperty("paidByUserId").GetInt32().Should().Be(fixture.AccountantId);
        paid.GetProperty("events").EnumerateArray()
            .Select(item => item.GetProperty("toStatus").GetString())
            .Should().Equal("Draft", "UnderValidation", "ReadyForApproval", "Approved", "Paid");

        using var updatePaid = await SendAsync(HttpMethod.Put,
            $"/api/finance/payment-requests/{paid.GetProperty("id").GetInt32()}",
            ValidPayment(fixture.ContractId, fixture.VendorId, fixture.AccountantId, paid.GetProperty("rowVersion").GetString()));
        updatePaid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var cancelPaid = await SendAsync(HttpMethod.Post,
            $"/api/finance/payment-requests/{paid.GetProperty("id").GetInt32()}/cancel",
            new { reason = "Cannot cancel a paid request", rowVersion = paid.GetProperty("rowVersion").GetString() });
        cancelPaid.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.PaymentRequests.AsNoTracking().SingleAsync(item => item.Id == paid.GetProperty("id").GetInt32())))
            .Status.Should().Be(PaymentRequestStatus.Paid);
    }

    [Fact]
    public async Task PaymentCreator_WithApprovalPermission_CannotApproveOwnRequest()
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ADMIN"));
        var draft = await CreatePaymentAsync(fixture);

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        var submitted = await TransitionAsync("payment-requests", draft, "submit");
        var ready = await TransitionAsync("payment-requests", submitted, "validate");

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ADMIN"));
        using var response = await SendAsync(HttpMethod.Post,
            $"/api/finance/payment-requests/{ready.GetProperty("id").GetInt32()}/decision",
            new { approved = true, rowVersion = ready.GetProperty("rowVersion").GetString() });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.PaymentRequests.Include(item => item.Events)
            .SingleAsync(item => item.Id == ready.GetProperty("id").GetInt32())))
            .Should().Match<PaymentRequest>(item => item.Status == PaymentRequestStatus.ReadyForApproval &&
                item.ApprovedByUserId == null && item.Events.Count == 3);

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "BGD"));
        var approved = await TransitionAsync("payment-requests", ready, "decision", new
        {
            approved = true,
            rowVersion = ready.GetProperty("rowVersion").GetString(),
        });
        approved.GetProperty("status").GetString().Should().Be("Approved");
    }

    [Fact]
    public async Task DownstreamQsLink_RoundTripsAndRejectsInvalidOrSupersededEvidence()
    {
        var fixture = await CreateFixtureAsync();
        var qsId = await WithDbAsync(async db =>
        {
            var siteQsUserId = await db.Users.Where(user => user.PhoneNumber ==
                TestDataSeeder.BusinessRolePhonesByCode["QS"]).Select(user => user.Id).SingleAsync();
            var qs = new ConstructionQsBatch
            {
                OperationalProjectId = fixture.ProjectId,
                ContractId = fixture.ContractId,
                Code = $"QS-PAY-{Guid.NewGuid():N}"[..30],
                Direction = ConstructionQsDirection.Downstream,
                Status = ConstructionReviewStatus.CmApproved,
                RecordState = ConstructionQsRecordState.Active,
                IsActiveLineage = true,
                WbsCode = "STRUCTURE-01",
                PeriodFrom = new DateTime(2026, 8, 1),
                PeriodTo = new DateTime(2026, 8, 31),
                CertifiedAmount = 1_000m,
                Description = "Accepted structural subcontract quantity for August",
                CreatedByUserId = siteQsUserId,
            };
            db.ConstructionQsBatches.Add(qs);
            await db.SaveChangesAsync();
            return qs.Id;
        });
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));

        var references = await ReadJsonAsync(await Client.GetAsync("/api/finance/payment-references"));
        references.GetProperty("qsBatches").EnumerateArray().Should().Contain(item =>
            item.GetProperty("id").GetInt32() == qsId &&
            item.GetProperty("contractId").GetInt32() == fixture.ContractId);
        using var createdResponse = await SendAsync(HttpMethod.Post, "/api/finance/payment-requests",
            ValidPayment(fixture.ContractId, fixture.VendorId, fixture.AccountantId, qsBatchId: qsId));
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(createdResponse);
        created.GetProperty("constructionQsBatchId").GetInt32().Should().Be(qsId);
        created.GetProperty("constructionQsCode").GetString().Should().StartWith("QS-PAY-");
        (await WithDbAsync(db => db.PaymentRequests.SingleAsync(item => item.Id == created.GetProperty("id").GetInt32())))
            .ConstructionQsBatchId.Should().Be(qsId);

        await WithDbAsync(async db =>
        {
            var qs = await db.ConstructionQsBatches.FindAsync(qsId);
            qs!.IsActiveLineage = false;
            qs.RecordState = ConstructionQsRecordState.Superseded;
            await db.SaveChangesAsync();
        });
        var submitted = await TransitionAsync("payment-requests", created, "submit");
        using var rejected = await SendAsync(HttpMethod.Post,
            $"/api/finance/payment-requests/{created.GetProperty("id").GetInt32()}/validate",
            new { rowVersion = submitted.GetProperty("rowVersion").GetString() });
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.PaymentRequests.SingleAsync(item => item.Id == created.GetProperty("id").GetInt32())))
            .Status.Should().Be(PaymentRequestStatus.UnderValidation);

        await WithDbAsync(async db =>
        {
            var qs = await db.ConstructionQsBatches.FindAsync(qsId);
            qs!.IsActiveLineage = true;
            qs.RecordState = ConstructionQsRecordState.Active;
            await db.SaveChangesAsync();
        });
        var ready = await TransitionAsync("payment-requests", submitted, "validate");
        await WithDbAsync(async db =>
        {
            var qs = await db.ConstructionQsBatches.FindAsync(qsId);
            qs!.IsActiveLineage = false;
            qs.RecordState = ConstructionQsRecordState.Superseded;
            await db.SaveChangesAsync();
        });
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "BGD"));
        using var blockedApproval = await SendAsync(HttpMethod.Post,
            $"/api/finance/payment-requests/{created.GetProperty("id").GetInt32()}/decision",
            new { approved = true, rowVersion = ready.GetProperty("rowVersion").GetString() });
        blockedApproval.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.PaymentRequests.SingleAsync(item => item.Id == created.GetProperty("id").GetInt32())))
            .Status.Should().Be(PaymentRequestStatus.ReadyForApproval);

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALE"));
        using var forbiddenRevision = await SendAsync(HttpMethod.Post,
            $"/api/finance/payment-requests/{created.GetProperty("id").GetInt32()}/revise",
            new { reason = "QS correction", rowVersion = ready.GetProperty("rowVersion").GetString() });
        forbiddenRevision.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        using var revisedResponse = await SendAsync(HttpMethod.Post,
            $"/api/finance/payment-requests/{created.GetProperty("id").GetInt32()}/revise",
            new { reason = "QS correction", rowVersion = ready.GetProperty("rowVersion").GetString() });
        revisedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var revised = await ReadJsonAsync(revisedResponse);
        revised.GetProperty("status").GetString().Should().Be("Draft");
        revised.GetProperty("revisionNumber").GetInt32().Should().Be(2);
        revised.GetProperty("revisions").EnumerateArray().Single()
            .GetProperty("constructionQsBatchId").GetInt32().Should().Be(qsId);
    }

    [Fact]
    public async Task DraftPayment_CanReplaceSupersededQsWithoutDuplicatingInvoice()
    {
        var fixture = await CreateFixtureAsync();
        var originalId = await WithDbAsync(async db =>
        {
            var siteQsUserId = await db.Users.Where(user => user.PhoneNumber ==
                TestDataSeeder.BusinessRolePhonesByCode["QS"]).Select(user => user.Id).SingleAsync();
            var original = new ConstructionQsBatch
            {
                OperationalProjectId = fixture.ProjectId,
                ContractId = fixture.ContractId,
                Code = $"QS-PAY-{Guid.NewGuid():N}"[..30],
                Direction = ConstructionQsDirection.Downstream,
                Status = ConstructionReviewStatus.CmApproved,
                RecordState = ConstructionQsRecordState.Active,
                IsActiveLineage = true,
                WbsCode = "STRUCTURE-01",
                PeriodFrom = new DateTime(2026, 8, 1),
                PeriodTo = new DateTime(2026, 8, 31),
                CertifiedAmount = 1_000m,
                Description = "Accepted subcontract works before correction",
                CreatedByUserId = siteQsUserId,
                Lines = [new ConstructionQsBatchLine
                {
                    ItemCode = "STRUCTURE-CONCRETE", Description = "Concrete works", Unit = "m3",
                    Quantity = 1m, UnitPrice = 1_000m, Amount = 1_000m,
                }],
            };
            db.ConstructionQsBatches.Add(original);
            await db.SaveChangesAsync();
            return original.Id;
        });
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        using var createdResponse = await SendAsync(HttpMethod.Post, "/api/finance/payment-requests",
            ValidPayment(fixture.ContractId, fixture.VendorId, fixture.AccountantId, qsBatchId: originalId));
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(createdResponse);

        var replacementId = await WithDbAsync(async db =>
        {
            var siteQsUserId = await db.Users.Where(user => user.PhoneNumber ==
                TestDataSeeder.BusinessRolePhonesByCode["QS"]).Select(user => user.Id).SingleAsync();
            var original = await db.ConstructionQsBatches.SingleAsync(item => item.Id == originalId);
            original.RecordState = ConstructionQsRecordState.Superseded;
            original.IsActiveLineage = false;
            var replacement = new ConstructionQsBatch
            {
                OperationalProjectId = fixture.ProjectId,
                ContractId = fixture.ContractId,
                Code = $"QS-PAY-{Guid.NewGuid():N}"[..30],
                Direction = ConstructionQsDirection.Downstream,
                Status = ConstructionReviewStatus.CmApproved,
                RecordState = ConstructionQsRecordState.Active,
                IsActiveLineage = true,
                SupersedesBatchId = original.Id,
                AdjustmentKind = ConstructionQsAdjustmentKind.SupersedingCorrection,
                WbsCode = "STRUCTURE-01",
                PeriodFrom = new DateTime(2026, 8, 1),
                PeriodTo = new DateTime(2026, 8, 31),
                CertifiedAmount = 1_100m,
                Description = "Corrected accepted subcontract works",
                CreatedByUserId = siteQsUserId,
                Lines = [new ConstructionQsBatchLine
                {
                    ItemCode = "STRUCTURE-CONCRETE", Description = "Concrete works", Unit = "m3",
                    Quantity = 1m, UnitPrice = 1_100m, Amount = 1_100m,
                }],
            };
            db.ConstructionQsBatches.Add(replacement);
            await db.SaveChangesAsync();
            return replacement.Id;
        });
        using var updatedResponse = await SendAsync(HttpMethod.Put,
            $"/api/finance/payment-requests/{created.GetProperty("id").GetInt32()}",
            ValidPayment(fixture.ContractId, fixture.VendorId, fixture.AccountantId,
                created.GetProperty("rowVersion").GetString(), qsBatchId: replacementId));
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK, await updatedResponse.Content.ReadAsStringAsync());
        var updated = await ReadJsonAsync(updatedResponse);
        updated.GetProperty("constructionQsBatchId").GetInt32().Should().Be(replacementId);
        updated.GetProperty("supplierInvoiceNumber").GetString().Should().Be("INV-2026-001");
        (await WithDbAsync(db => db.PaymentRequests.AsNoTracking().SingleAsync(item =>
            item.Id == created.GetProperty("id").GetInt32()))).ConstructionQsBatchId.Should().Be(replacementId);
    }

    [Fact]
    public async Task ApprovedPayment_StaleQsBlocksPayUntilSameRequestRevisionIsApproved()
    {
        var fixture = await CreateFixtureAsync();
        var originalQsId = await CreateApprovedDownstreamQsAsync(fixture);
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        using var createResponse = await SendAsync(HttpMethod.Post, "/api/finance/payment-requests",
            ValidPayment(fixture.ContractId, fixture.VendorId, fixture.AccountantId, qsBatchId: originalQsId));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = await ReadJsonAsync(createResponse);
        var submitted = await TransitionAsync("payment-requests", draft, "submit");
        var ready = await TransitionAsync("payment-requests", submitted, "validate");
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "BGD"));
        var approved = await TransitionAsync("payment-requests", ready, "decision", new
        {
            approved = true,
            rowVersion = ready.GetProperty("rowVersion").GetString(),
        });
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        using var prematureRevision = await SendAsync(HttpMethod.Post,
            $"/api/finance/payment-requests/{draft.GetProperty("id").GetInt32()}/revise",
            new { reason = "No QS correction", rowVersion = approved.GetProperty("rowVersion").GetString() });
        prematureRevision.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await WithDbAsync(async db =>
        {
            var qs = await db.ConstructionQsBatches.SingleAsync(item => item.Id == originalQsId);
            qs.IsActiveLineage = false;
            qs.RecordState = ConstructionQsRecordState.Superseded;
            await db.SaveChangesAsync();
        });

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        using var blockedPay = await SendAsync(HttpMethod.Post,
            $"/api/finance/payment-requests/{draft.GetProperty("id").GetInt32()}/pay",
            new { rowVersion = approved.GetProperty("rowVersion").GetString() });
        blockedPay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var unchanged = await WithDbAsync(db => db.PaymentRequests.AsNoTracking().SingleAsync(item =>
            item.Id == draft.GetProperty("id").GetInt32()));
        unchanged.Status.Should().Be(PaymentRequestStatus.Approved);
        unchanged.PaidAt.Should().BeNull();
        (await WithDbAsync(db => db.PaymentRequestEvents.CountAsync(item =>
            item.PaymentRequestId == draft.GetProperty("id").GetInt32()))).Should().Be(4);

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ADMIN"));
        using var revisedResponse = await SendAsync(HttpMethod.Post,
            $"/api/finance/payment-requests/{draft.GetProperty("id").GetInt32()}/revise",
            new { reason = "QS corrected by CM", rowVersion = approved.GetProperty("rowVersion").GetString() });
        revisedResponse.StatusCode.Should().Be(HttpStatusCode.OK, await revisedResponse.Content.ReadAsStringAsync());
        var revised = await ReadJsonAsync(revisedResponse);
        revised.GetProperty("id").GetInt32().Should().Be(draft.GetProperty("id").GetInt32());
        revised.GetProperty("supplierInvoiceNumber").GetString().Should().Be("INV-2026-001");
        revised.GetProperty("revisionNumber").GetInt32().Should().Be(2);
        revised.GetProperty("status").GetString().Should().Be("Draft");
        revised.GetProperty("approvedAt").ValueKind.Should().Be(JsonValueKind.Null);
        var prior = revised.GetProperty("revisions").EnumerateArray().Single();
        prior.GetProperty("revisionNumber").GetInt32().Should().Be(1);
        prior.GetProperty("status").GetString().Should().Be("Approved");
        prior.GetProperty("constructionQsBatchId").GetInt32().Should().Be(originalQsId);
        prior.GetProperty("supplierInvoiceNumber").GetString().Should().Be("INV-2026-001");
        prior.GetProperty("attachments").GetArrayLength().Should().Be(1);

        using var staleRevision = await SendAsync(HttpMethod.Post,
            $"/api/finance/payment-requests/{draft.GetProperty("id").GetInt32()}/revise",
            new { reason = "Duplicate revision", rowVersion = approved.GetProperty("rowVersion").GetString() });
        staleRevision.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var replacementQsId = await CreateApprovedDownstreamQsAsync(fixture, originalQsId);
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        using var updateResponse = await SendAsync(HttpMethod.Put,
            $"/api/finance/payment-requests/{draft.GetProperty("id").GetInt32()}",
            ValidPayment(fixture.ContractId, fixture.VendorId, fixture.AccountantId,
                revised.GetProperty("rowVersion").GetString(), qsBatchId: replacementQsId));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, await updateResponse.Content.ReadAsStringAsync());
        var updated = await ReadJsonAsync(updateResponse);
        var resubmitted = await TransitionAsync("payment-requests", updated, "submit");
        var revalidated = await TransitionAsync("payment-requests", resubmitted, "validate");
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ADMIN"));
        using var selfApproval = await SendAsync(HttpMethod.Post,
            $"/api/finance/payment-requests/{draft.GetProperty("id").GetInt32()}/decision",
            new { approved = true, rowVersion = revalidated.GetProperty("rowVersion").GetString() });
        selfApproval.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "BGD"));
        var reapproved = await TransitionAsync("payment-requests", revalidated, "decision", new
        {
            approved = true,
            rowVersion = revalidated.GetProperty("rowVersion").GetString(),
        });
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        var paid = await TransitionAsync("payment-requests", reapproved, "pay");
        paid.GetProperty("status").GetString().Should().Be("Paid");
        paid.GetProperty("revisionNumber").GetInt32().Should().Be(2);
        paid.GetProperty("revisions").EnumerateArray().Single()
            .GetProperty("constructionQsBatchId").GetInt32().Should().Be(originalQsId);
    }

    [Fact]
    public async Task RejectedPayment_CanReviseWithoutReusingInvoiceInAnotherRequest()
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        var draft = await CreatePaymentAsync(fixture);
        var submitted = await TransitionAsync("payment-requests", draft, "submit");
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "BGD"));
        var rejected = await TransitionAsync("payment-requests", submitted, "decision", new
        {
            approved = false,
            reason = "Invoice attachment needs correction",
            rowVersion = submitted.GetProperty("rowVersion").GetString(),
        });
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        using var duplicate = await SendAsync(HttpMethod.Post, "/api/finance/payment-requests",
            ValidPayment(fixture.ContractId, fixture.VendorId, fixture.AccountantId));
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var revisedResponse = await SendAsync(HttpMethod.Post,
            $"/api/finance/payment-requests/{draft.GetProperty("id").GetInt32()}/revise",
            new { reason = "Correct rejected attachment", rowVersion = rejected.GetProperty("rowVersion").GetString() });
        revisedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var revised = await ReadJsonAsync(revisedResponse);
        revised.GetProperty("supplierInvoiceNumber").GetString().Should().Be("INV-2026-001");
        revised.GetProperty("revisions").EnumerateArray().Single()
            .GetProperty("status").GetString().Should().Be("Rejected");
        revised.GetProperty("rejectedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("Upstream")]
    [InlineData("Pending")]
    [InlineData("OtherContract")]
    public async Task InvalidDownstreamQs_CannotBeLinkedToPayment(string scenario)
    {
        var fixture = await CreateFixtureAsync();
        var qsId = await WithDbAsync(async db =>
        {
            var siteQsUserId = await db.Users.Where(user => user.PhoneNumber ==
                TestDataSeeder.BusinessRolePhonesByCode["QS"]).Select(user => user.Id).SingleAsync();
            var qs = new ConstructionQsBatch
            {
                OperationalProjectId = fixture.ProjectId,
                ContractId = fixture.ContractId,
                Code = $"QS-BAD-{Guid.NewGuid():N}"[..30],
                Direction = scenario == "Upstream" ? ConstructionQsDirection.Upstream : ConstructionQsDirection.Downstream,
                Status = scenario == "Pending" ? ConstructionReviewStatus.PendingCmReview : ConstructionReviewStatus.CmApproved,
                RecordState = ConstructionQsRecordState.Active,
                IsActiveLineage = true,
                WbsCode = "STRUCTURE-01",
                PeriodFrom = new DateTime(2026, 8, 1),
                PeriodTo = new DateTime(2026, 8, 31),
                CertifiedAmount = 1_000m,
                Description = "Subcontract quantity awaiting validation",
                CreatedByUserId = siteQsUserId,
            };
            db.ConstructionQsBatches.Add(qs);
            await db.SaveChangesAsync();
            return qs.Id;
        });
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        var contractId = scenario == "OtherContract"
            ? await WithDbAsync(async db =>
            {
                var original = await db.Contracts.SingleAsync(item => item.Id == fixture.ContractId);
                var other = new Contract
                {
                    ContractNumber = $"HD-QS-{Guid.NewGuid():N}"[..35],
                    CustomerId = original.CustomerId,
                    OperationalProjectId = fixture.ProjectId,
                    VendorId = fixture.VendorId,
                    Direction = ContractDirection.Downstream,
                    Type = ContractType.Subcontract,
                    Status = ContractStatus.Signed,
                    SignedDate = new DateTime(2026, 7, 1),
                    Value = 100_000m,
                };
                db.Contracts.Add(other);
                await db.SaveChangesAsync();
                return other.Id;
            })
            : fixture.ContractId;
        using var response = await SendAsync(HttpMethod.Post, "/api/finance/payment-requests",
            ValidPayment(contractId, fixture.VendorId, fixture.AccountantId, qsBatchId: qsId));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.PaymentRequests.CountAsync(item => item.VendorId == fixture.VendorId)))
            .Should().Be(0);
    }

    [Fact]
    public async Task DuplicateSupplierInvoice_PerVendor_IsRejectedWithoutSecondRow()
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        await CreatePaymentAsync(fixture);
        using var duplicate = await SendAsync(HttpMethod.Post, "/api/finance/payment-requests",
            ValidPayment(fixture.ContractId, fixture.VendorId, fixture.AccountantId));
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.PaymentRequests.CountAsync(item => item.VendorId == fixture.VendorId &&
            item.SupplierInvoiceNumber == "INV-2026-001"))).Should().Be(1);
    }

    [Fact]
    public async Task CreatePayment_WithUpstreamContract_IsRejectedWithoutMutation()
    {
        var fixture = await CreateFixtureAsync();
        var upstreamContractId = await WithDbAsync(async db =>
        {
            var project = await db.OperationalProjects.SingleAsync(item => item.Id == fixture.ProjectId);
            var contract = new Contract
            {
                ContractNumber = $"HD-UP-{Guid.NewGuid():N}"[..20],
                CustomerId = project.CustomerId,
                OperationalProjectId = project.Id,
                Direction = ContractDirection.Upstream,
                Type = ContractType.DesignAndBuild,
                Status = ContractStatus.Signed,
                SignedDate = DateTime.UtcNow.Date,
                Value = 500_000m,
            };
            db.Contracts.Add(contract);
            await db.SaveChangesAsync();
            return contract.Id;
        });
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));

        using var response = await SendAsync(HttpMethod.Post, "/api/finance/payment-requests",
            ValidPayment(upstreamContractId, fixture.VendorId, fixture.AccountantId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.PaymentRequests.CountAsync(item => item.ContractId == upstreamContractId)))
            .Should().Be(0);
    }

    [Fact]
    public async Task Contract_WithPaymentRequest_CannotBeChangedToUpstream()
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        await CreatePaymentAsync(fixture);
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        using var detailResponse = await Client.GetAsync($"/api/contracts/{fixture.ContractId}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await ReadJsonAsync(detailResponse);

        using var update = await Client.PutAsJsonAsync($"/api/contracts/{fixture.ContractId}", new
        {
            contractNumber = detail.GetProperty("contractNumber").GetString(),
            customerId = detail.GetProperty("customerId").GetInt32(),
            operationalProjectId = fixture.ProjectId,
            direction = "Upstream",
            type = "DesignAndBuild",
            status = detail.GetProperty("status").GetString(),
            signedDate = detail.GetProperty("signedDate").GetDateTime(),
            value = detail.GetProperty("value").GetDecimal(),
            rowVersion = detail.GetProperty("rowVersion").GetString(),
        });

        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var persisted = await WithDbAsync(db => db.Contracts.AsNoTracking()
            .SingleAsync(item => item.Id == fixture.ContractId));
        persisted.Direction.Should().Be(ContractDirection.Downstream);
        persisted.VendorId.Should().Be(fixture.VendorId);
        persisted.OperationalProjectId.Should().Be(fixture.ProjectId);
        (await WithDbAsync(db => db.PaymentRequests.CountAsync(item => item.ContractId == fixture.ContractId)))
            .Should().Be(1);
    }

    [Fact]
    public async Task PeriodClose_UsesVietnamBoundaries_RequiresSequence_AndCannotReopen()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        using var createdResponse = await SendAsync(HttpMethod.Post, "/api/finance/periods", new { year = 2030, month = 2 });
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var period = await ReadJsonAsync(createdResponse);
        period.GetProperty("periodStartUtc").GetDateTime().Should().Be(new DateTime(2030, 1, 31, 17, 0, 0, DateTimeKind.Utc));
        period.GetProperty("periodEndUtc").GetDateTime().Should().Be(new DateTime(2030, 2, 28, 17, 0, 0, DateTimeKind.Utc));

        using var prematureClose = await SendAsync(HttpMethod.Post,
            $"/api/finance/periods/{period.GetProperty("id").GetInt32()}/close",
            new { reason = "Month-end close", rowVersion = period.GetProperty("rowVersion").GetString() });
        prematureClose.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.AccountingPeriods.AsNoTracking().SingleAsync(item => item.Id == period.GetProperty("id").GetInt32())))
            .Status.Should().Be(AccountingPeriodStatus.Open);

        var closing = await TransitionAsync("periods", period, "start-closing");
        var closed = await TransitionAsync("periods", closing, "close", new
        {
            reason = "Month-end close completed",
            rowVersion = closing.GetProperty("rowVersion").GetString(),
        });
        closed.GetProperty("status").GetString().Should().Be("Closed");
        using var noReopenRoute = await SendAsync(HttpMethod.Post,
            $"/api/finance/periods/{closed.GetProperty("id").GetInt32()}/reopen",
            new { reason = "Not allowed", rowVersion = closed.GetProperty("rowVersion").GetString() });
        noReopenRoute.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task CorrectionWorkflow_RejectsInvalidTransition_ApprovesAndReversesOnce()
    {
        var fixture = await CreateFixtureAsync();
        var sourcePaymentId = await SeedPaidPaymentAsync(fixture);
        var periodId = await SeedClosedPeriodAsync(2026, 8);
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        using var createdResponse = await SendAsync(HttpMethod.Post, "/api/finance/corrections", new
        {
            operationalProjectId = fixture.ProjectId,
            accountingPeriodId = periodId,
            sourceEntityType = "PaymentRequest",
            sourceEntityId = sourcePaymentId,
            reasonCode = "AMOUNT_ERROR",
            originalValue = 1000m,
            correctedValue = 900m,
            currency = "VND",
            note = "Supplier credit note received",
            responsibleAccountantUserId = fixture.AccountantId,
        });
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = await ReadJsonAsync(createdResponse);

        using var invalidDecision = await SendAsync(HttpMethod.Post,
            $"/api/finance/corrections/{draft.GetProperty("id").GetInt32()}/decision",
            new { approved = true, rowVersion = draft.GetProperty("rowVersion").GetString() });
        invalidDecision.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.AccountingCorrections.AsNoTracking().SingleAsync(item => item.Id == draft.GetProperty("id").GetInt32())))
            .Status.Should().Be(AccountingCorrectionStatus.Draft);

        var submitted = await TransitionAsync("corrections", draft, "submit");
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "BGD"));
        var approved = await TransitionAsync("corrections", submitted, "decision", new
        {
            approved = true,
            rowVersion = submitted.GetProperty("rowVersion").GetString(),
        });
        var reversal = await TransitionAsync("corrections", approved, "reverse", new
        {
            reason = "Reverse after approved replacement entry",
            rowVersion = approved.GetProperty("rowVersion").GetString(),
        });
        reversal.GetProperty("reversalOfCorrectionId").GetInt32().Should().Be(approved.GetProperty("id").GetInt32());
        reversal.GetProperty("originalValue").GetDecimal().Should().Be(900m);
        reversal.GetProperty("correctedValue").GetDecimal().Should().Be(1000m);
        (await WithDbAsync(db => db.AccountingCorrections.CountAsync(item => item.ReversalOfCorrectionId == approved.GetProperty("id").GetInt32())))
            .Should().Be(1);
    }

    private async Task<FinanceFixture> CreateFixtureAsync() => await WithDbAsync(async db =>
    {
        var accountantId = await UserIdAsync(db, "ACCOUNTANT");
        var pmId = await UserIdAsync(db, "PM");
        var vendor = new Vendor
        {
            VendorCode = $"V-{Guid.NewGuid():N}"[..16],
            CompanyName = "Finance Test Vendor",
            VendorType = VendorType.Supplier,
        };
        var customer = new Customer { Type = CustomerType.Company, Name = "Finance Test Customer", SourceCode = "referral" };
        db.AddRange(vendor, customer);
        await db.SaveChangesAsync();
        var project = new OperationalProject
        {
            Code = $"PJ-FIN-{Guid.NewGuid():N}"[..30],
            Name = "Finance Test Project",
            CustomerId = customer.Id,
            ProjectManagerUserId = pmId,
        };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        var contract = new Contract
        {
            ContractNumber = $"HD-FIN-{Guid.NewGuid():N}"[..30],
            CustomerId = customer.Id,
            VendorId = vendor.Id,
            OperationalProjectId = project.Id,
            Direction = ContractDirection.Downstream,
            Type = ContractType.Supply,
            Status = ContractStatus.InProgress,
            SignedDate = new DateTime(2026, 7, 1),
            Value = 100_000m,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        return new FinanceFixture(project.Id, contract.Id, vendor.Id, accountantId);
    });

    private async Task<JsonElement> CreatePaymentAsync(FinanceFixture fixture)
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/finance/payment-requests",
            ValidPayment(fixture.ContractId, fixture.VendorId, fixture.AccountantId));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadJsonAsync(response);
    }

    private static object ValidPayment(int contractId, int vendorId, int accountantId,
        string? rowVersion = null, int? qsBatchId = null) => new
        {
            contractId,
            vendorId,
            supplierInvoiceNumber = "INV-2026-001",
            invoiceDate = "2026-08-01",
            invoiceAmount = 1000m,
            currency = "VND",
            receivedAt = new DateTime(2026, 8, 2, 1, 0, 0, DateTimeKind.Utc),
            assignedAccountantUserId = accountantId,
            constructionQsBatchId = qsBatchId,
            attachments = new[] { new { fileName = "invoice.pdf", filePath = "/files/finance/invoice.pdf" } },
            rowVersion,
        };

    private async Task<int> SeedPaidPaymentAsync(FinanceFixture fixture) => await WithDbAsync(async db =>
    {
        var payment = new PaymentRequest
        {
            Code = $"PAY-{Guid.NewGuid():N}"[..20],
            ContractId = fixture.ContractId,
            VendorId = fixture.VendorId,
            SupplierInvoiceNumber = $"INV-{Guid.NewGuid():N}"[..25],
            InvoiceDate = new DateOnly(2026, 7, 1),
            InvoiceAmount = 1000m,
            Status = PaymentRequestStatus.Paid,
            ReceivedAt = new DateTime(2026, 7, 1),
            ValidatedAt = new DateTime(2026, 7, 2),
            AssignedAccountantUserId = fixture.AccountantId,
            PaidAt = new DateTime(2026, 7, 5),
            PaidByUserId = fixture.AccountantId,
            CreatedByUserId = fixture.AccountantId,
        };
        db.PaymentRequests.Add(payment);
        await db.SaveChangesAsync();
        return payment.Id;
    });

    private async Task<int> SeedClosedPeriodAsync(int year, int month) => await WithDbAsync(async db =>
    {
        var existing = await db.AccountingPeriods.SingleOrDefaultAsync(item => item.Year == year && item.Month == month);
        if (existing is not null) return existing.Id;
        var period = new AccountingPeriod
        {
            Year = year,
            Month = month,
            PeriodStartUtc = new DateTime(year, month, 1).AddHours(-7),
            PeriodEndUtc = new DateTime(year, month, 1).AddMonths(1).AddHours(-7),
            Status = AccountingPeriodStatus.Closed,
            ClosedAt = DateTime.UtcNow,
            CloseReason = "Test close",
        };
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
        return period.Id;
    });

    private async Task<JsonElement> TransitionAsync(string root, JsonElement current, string action, object? payload = null)
    {
        payload ??= new { rowVersion = current.GetProperty("rowVersion").GetString() };
        using var response = await SendAsync(HttpMethod.Post,
            $"/api/finance/{root}/{current.GetProperty("id").GetInt32()}/{action}", payload);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await Client.SendAsync(request);
    }

    private static Task<int> UserIdAsync(NihomeBackend.Data.AppDbContext db, string roleCode) => db.Users
        .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode[roleCode])
        .Select(user => user.Id).SingleAsync();

    private Task<int> CreateApprovedDownstreamQsAsync(FinanceFixture fixture, int? supersedesId = null) =>
        WithDbAsync(async db =>
        {
            var qsUserId = await UserIdAsync(db, "QS");
            var qs = new ConstructionQsBatch
            {
                OperationalProjectId = fixture.ProjectId,
                ContractId = fixture.ContractId,
                Code = $"QS-PAY-{Guid.NewGuid():N}"[..30],
                Direction = ConstructionQsDirection.Downstream,
                Status = ConstructionReviewStatus.CmApproved,
                RecordState = ConstructionQsRecordState.Active,
                IsActiveLineage = true,
                SupersedesBatchId = supersedesId,
                AdjustmentKind = supersedesId.HasValue
                    ? ConstructionQsAdjustmentKind.SupersedingCorrection : ConstructionQsAdjustmentKind.Original,
                WbsCode = "STRUCTURE-01",
                PeriodFrom = new DateTime(2026, 8, 1),
                PeriodTo = new DateTime(2026, 8, 31),
                CertifiedAmount = 1_000m,
                Description = "CM-approved downstream structural works",
                CreatedByUserId = qsUserId,
                Lines = [new ConstructionQsBatchLine
                {
                    ItemCode = "STRUCTURE-CONCRETE", Description = "Concrete works", Unit = "m3",
                    Quantity = 1m, UnitPrice = 1_000m, Amount = 1_000m,
                }],
            };
            db.ConstructionQsBatches.Add(qs);
            await db.SaveChangesAsync();
            return qs.Id;
        });

    private sealed record FinanceFixture(int ProjectId, int ContractId, int VendorId, int AccountantId);
}
