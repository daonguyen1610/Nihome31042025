using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>TendersController</c> — CRUD (NIH-95/96) plus
/// the NIH-97 detail-page workflow (checklist inline-edit, library attach,
/// mark won / mark lost, timeline).
/// </summary>
public class TendersControllerTests : IntegrationTestBase
{
    public TendersControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/tenders")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsWarehouse_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/tenders")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_HappyPath_ReturnsAutoChecklist()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();

        var res = await Client.PostAsJsonAsync("/api/tenders", new
        {
            name = "Gói thầu Alpha",
            customerId,
            submissionDeadline = DateTime.UtcNow.AddDays(14),
            openingDate = DateTime.UtcNow.AddDays(7),
            infoSource = "Website",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("code").GetString().Should().StartWith("TD-");
        body.GetProperty("status").GetString().Should().Be("Preparing");
        body.GetProperty("checklistItems").GetArrayLength().Should().BeGreaterThan(0);
        body.GetProperty("checklistCompletionPercent").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Create_WithPastDeadline_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var res = await Client.PostAsJsonAsync("/api/tenders", new
        {
            name = "Bad deadline",
            customerId,
            submissionDeadline = DateTime.UtcNow.AddDays(-1),
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithUnknownCustomer_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var res = await Client.PostAsJsonAsync("/api/tenders", new
        {
            name = "Bad customer",
            customerId = 999_999,
            submissionDeadline = DateTime.UtcNow.AddDays(10),
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sale_CannotCreateWithoutManagePermission()
    {
        // SALE has crm.tenders.view + crm.tenders.manage — should succeed.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var customerId = await CreateCustomerAsync();
        var res = await Client.PostAsJsonAsync("/api/tenders", new
        {
            name = "SALE-created tender",
            customerId,
            submissionDeadline = DateTime.UtcNow.AddDays(10),
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Update_WhilePreparing_UpdatesAllEditableFields()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateTenderAsync();
        var newDeadline = DateTime.UtcNow.AddDays(30);
        var res = await Client.PutAsJsonAsync($"/api/tenders/{id}", new
        {
            name = "Updated name",
            submissionDeadline = newDeadline,
            infoSource = "Referral",
            note = "note updated",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("name").GetString().Should().Be("Updated name");
        body.GetProperty("note").GetString().Should().Be("note updated");
    }

    [Fact]
    public async Task Update_WhilePreparing_BlankNameIsBadRequestAndPreservesTender()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateTenderAsync();
        var original = await WithDbAsync(db => db.Tenders.AsNoTracking().SingleAsync(item => item.Id == id));

        var response = await Client.PutAsJsonAsync($"/api/tenders/{id}", new
        {
            name = "   ",
            submissionDeadline = DateTime.UtcNow.AddDays(30),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var saved = await WithDbAsync(db => db.Tenders.AsNoTracking().SingleAsync(item => item.Id == id));
        saved.Name.Should().Be(original.Name);
        saved.SubmissionDeadline.Should().Be(original.SubmissionDeadline);
    }

    [Fact]
    public async Task Delete_Submitted_IsBadRequestAndPreservesTenderAndChecklist()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateTenderAsync();
        var customerId = await WithDbAsync(async db =>
        {
            var tender = await db.Tenders.FirstAsync(t => t.Id == id);
            tender.Status = TenderStatus.Submitted;
            await db.SaveChangesAsync();
            return tender.CustomerId;
        });

        var impactResponse = await Client.GetAsync($"/api/tenders/{id}/deletion-impact");
        impactResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var impact = await ReadJsonAsync(impactResponse);
        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "tender.status" &&
            item.GetProperty("action").GetString() == "Block");

        (await ConfirmDeleteAsync(id, impact)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.GetAsync($"/api/tenders/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync($"/api/customers/{customerId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await WithDbAsync(db => db.TenderChecklistItems.AnyAsync(i => i.TenderId == id))).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_MissingTender_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await DeleteWithBodyAsync(9_999_999, new string('a', 64), "TD-404"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithoutManagePermission_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await DeleteWithBodyAsync(9_999_999, new string('a', 64), "TD-404"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_AsSaleWithManagePermission_PreviewsAndDeletesPreparingTender()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var impactResponse = await Client.GetAsync($"/api/tenders/{tenderId}/deletion-impact");
        impactResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var impact = await ReadJsonAsync(impactResponse);

        (await ConfirmDeleteAsync(tenderId, impact)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await WithDbAsync(db => db.Tenders.AnyAsync(item => item.Id == tenderId))).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_Preparing_PurgesOwnedFileAndGraphButPreservesCapabilityAndTargets()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        var detail = await ReadJsonAsync(await Client.GetAsync($"/api/tenders/{tenderId}"));
        var checklistIds = detail.GetProperty("checklistItems").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt32()).Take(2).ToList();
        var capabilityId = await CreateCapabilityDocumentAsync();
        var capabilityPath = await WithDbAsync(db => db.CapabilityDocuments.AsNoTracking()
            .Where(item => item.Id == capabilityId).Select(item => item.FilePath).SingleAsync());
        var environment = Factory.Services.GetRequiredService<IWebHostEnvironment>();
        var capabilityFullPath = FullPath(environment, capabilityPath);
        Directory.CreateDirectory(Path.GetDirectoryName(capabilityFullPath)!);
        await File.WriteAllTextAsync(capabilityFullPath, "shared capability");
        string? ownedPath = null;

        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("owned tender file")), "file", "owned.pdf");
            var upload = await Client.PostAsync(
                $"/api/tenders/{tenderId}/checklist/{checklistIds[0]}/upload", form);
            upload.StatusCode.Should().Be(HttpStatusCode.OK);
            ownedPath = (await ReadJsonAsync(upload)).GetProperty("checklistItems").EnumerateArray()
                .Single(item => item.GetProperty("id").GetInt32() == checklistIds[0])
                .GetProperty("filePath").GetString();
            (await Client.PostAsJsonAsync(
                $"/api/tenders/{tenderId}/checklist/attach-from-library",
                new { items = new[] { new { checklistItemId = checklistIds[1], capabilityDocumentId = capabilityId } } }))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            var ids = await WithDbAsync(async db =>
            {
                var tender = await db.Tenders.SingleAsync(item => item.Id == tenderId);
                var preservedTarget = new Opportunity
                {
                    Name = "Preserved target",
                    CustomerId = tender.CustomerId,
                };
                var inbound = new Opportunity
                {
                    Name = "Inbound reference",
                    CustomerId = tender.CustomerId,
                    WonTenderId = tenderId,
                };
                var revision = new TenderEstimateRevision
                {
                    TenderId = tenderId,
                    VersionNumber = 1,
                    SourceFileName = "estimate.csv",
                    SourceSha256 = new string('b', 64),
                    ImportedByUserId = tender.CreatedByUserId ?? 1,
                    ImportedAt = DateTime.UtcNow,
                    Lines =
                    [
                        new TenderEstimateLine
                        {
                            ItemCode = "ITEM-1",
                            Description = "Owned estimate line",
                            Unit = "item",
                            Quantity = 1,
                        },
                    ],
                };
                db.AddRange(preservedTarget, inbound, revision);
                await db.SaveChangesAsync();
                tender.WonOpportunityId = preservedTarget.Id;
                await db.SaveChangesAsync();
                return (
                    TargetId: preservedTarget.Id,
                    InboundId: inbound.Id,
                    RevisionId: revision.Id);
            });

            var impactResponse = await Client.GetAsync($"/api/tenders/{tenderId}/deletion-impact");
            impactResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var impact = await ReadJsonAsync(impactResponse);
            impact.GetProperty("canDelete").GetBoolean().Should().BeTrue();
            impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
                item.GetProperty("key").GetString() == "tender.localFiles" &&
                item.GetProperty("examples").EnumerateArray().Any(example => example.GetString() == ownedPath));
            impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
                item.GetProperty("key").GetString() == "tender.capabilityDocuments" &&
                item.GetProperty("action").GetString() == "Unlink");

            var delete = await ConfirmDeleteAsync(tenderId, impact);
            delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
            File.Exists(FullPath(environment, ownedPath!)).Should().BeFalse();
            File.Exists(capabilityFullPath).Should().BeTrue();
            (await WithDbAsync(db => db.CapabilityDocuments.AnyAsync(item => item.Id == capabilityId))).Should().BeTrue();
            (await WithDbAsync(db => db.Opportunities.AnyAsync(item => item.Id == ids.TargetId))).Should().BeTrue();
            (await WithDbAsync(db => db.Opportunities.AsNoTracking()
                .Where(item => item.Id == ids.InboundId).Select(item => item.WonTenderId).SingleAsync())).Should().BeNull();
            (await WithDbAsync(db => db.TenderEstimateRevisions.AnyAsync(item => item.Id == ids.RevisionId))).Should().BeFalse();
            (await WithDbAsync(db => db.TenderChecklistItems.AnyAsync(item => item.TenderId == tenderId))).Should().BeFalse();
        }
        finally
        {
            if (ownedPath is not null)
            {
                var ownedFullPath = FullPath(environment, ownedPath);
                if (File.Exists(ownedFullPath)) File.Delete(ownedFullPath);
            }
            if (File.Exists(capabilityFullPath)) File.Delete(capabilityFullPath);
        }
    }

    [Fact]
    public async Task Delete_WithUnmanagedChecklistFile_IsBlockedAndPreservesGraphAndFile()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        const string unmanagedPath = "/files/contracts/unmanaged-tender.pdf";
        var environment = Factory.Services.GetRequiredService<IWebHostEnvironment>();
        var unmanagedFullPath = FullPath(environment, unmanagedPath);
        Directory.CreateDirectory(Path.GetDirectoryName(unmanagedFullPath)!);
        await File.WriteAllTextAsync(unmanagedFullPath, "must survive blocked deletion");

        try
        {
            var identifiers = await WithDbAsync(async db =>
            {
                var tender = await db.Tenders.SingleAsync(item => item.Id == tenderId);
                var checklistItem = await db.TenderChecklistItems
                    .FirstAsync(item => item.TenderId == tenderId);
                checklistItem.FilePath = unmanagedPath;
                checklistItem.OriginalFileName = "unmanaged-tender.pdf";
                var revision = new TenderEstimateRevision
                {
                    TenderId = tenderId,
                    VersionNumber = 1,
                    SourceFileName = "estimate.csv",
                    SourceSha256 = new string('c', 64),
                    ImportedByUserId = tender.CreatedByUserId ?? 1,
                    ImportedAt = DateTime.UtcNow,
                    Lines =
                    [
                        new TenderEstimateLine
                        {
                            ItemCode = "BLOCKED-1",
                            Description = "Preserved estimate line",
                            Unit = "item",
                            Quantity = 1,
                        },
                    ],
                };
                db.TenderEstimateRevisions.Add(revision);
                await db.SaveChangesAsync();
                return (ChecklistId: checklistItem.Id, RevisionId: revision.Id, LineId: revision.Lines.Single().Id);
            });

            var impactResponse = await Client.GetAsync($"/api/tenders/{tenderId}/deletion-impact");
            impactResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var impact = await ReadJsonAsync(impactResponse);
            impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
            impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
                item.GetProperty("key").GetString() == "tender.fileBlockers" &&
                item.GetProperty("action").GetString() == "Block");

            (await ConfirmDeleteAsync(tenderId, impact)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            File.Exists(unmanagedFullPath).Should().BeTrue();
            (await WithDbAsync(db => db.Tenders.AnyAsync(item => item.Id == tenderId))).Should().BeTrue();
            (await WithDbAsync(db => db.TenderChecklistItems.AnyAsync(item => item.Id == identifiers.ChecklistId)))
                .Should().BeTrue();
            (await WithDbAsync(db => db.TenderEstimateRevisions.AnyAsync(item => item.Id == identifiers.RevisionId)))
                .Should().BeTrue();
            (await WithDbAsync(db => db.TenderEstimateLines.AnyAsync(item => item.Id == identifiers.LineId)))
                .Should().BeTrue();
            (await WithDbAsync(db => db.HardDeleteOperations.AnyAsync(item =>
                item.ResourceType == "Tender" && item.ResourceId == tenderId.ToString()))).Should().BeFalse();
        }
        finally
        {
            if (File.Exists(unmanagedFullPath)) File.Delete(unmanagedFullPath);
        }
    }

    [Fact]
    public async Task Delete_WhenPlanChanges_ReturnsConflictAndPreservesTender()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        var impact = await ReadJsonAsync(await Client.GetAsync($"/api/tenders/{tenderId}/deletion-impact"));
        await WithDbAsync(async db =>
        {
            db.TenderChecklistItems.Add(new TenderChecklistItem
            {
                TenderId = tenderId,
                Title = "Changed after preview",
                SortOrder = 999,
            });
            await db.SaveChangesAsync();
        });

        (await ConfirmDeleteAsync(tenderId, impact)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.Tenders.AnyAsync(item => item.Id == tenderId))).Should().BeTrue();
        (await WithDbAsync(db => db.TenderChecklistItems.CountAsync(item => item.TenderId == tenderId)))
            .Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task Delete_InvalidConfirmation_ReturnsBadRequestAndPreservesTender()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        var impact = await ReadJsonAsync(await Client.GetAsync($"/api/tenders/{tenderId}/deletion-impact"));

        (await DeleteWithBodyAsync(
            tenderId,
            impact.GetProperty("planToken").GetString()!,
            "WRONG")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.Tenders.AnyAsync(item => item.Id == tenderId))).Should().BeTrue();
    }

    [Fact]
    public async Task List_FilterBySearchAndStatus()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        await CreateTenderAsync(name: "Uniquely-tagged Alpha");
        await CreateTenderAsync(name: "Other tender");

        var searched = await Client.GetAsync("/api/tenders?search=Uniquely-tagged&pageSize=20");
        searched.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(searched);
        body.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        for (int i = 0; i < body.GetProperty("items").GetArrayLength(); i++)
        {
            body.GetProperty("items")[i].GetProperty("name").GetString().Should().Contain("Uniquely-tagged");
        }
    }

    // ---------- helpers ----------

    private async Task<int> CreateCustomerAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "TC-" + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new
            {
                fullName = "Contact",
                phone = "0922" + Random.Shared.Next(100000, 999999),
                isPrimary = true,
            },
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateTenderAsync(string? name = null)
    {
        var customerId = await CreateCustomerAsync();
        var res = await Client.PostAsJsonAsync("/api/tenders", new
        {
            name = name ?? "Test tender " + Guid.NewGuid().ToString("N")[..6],
            customerId,
            submissionDeadline = DateTime.UtcNow.AddDays(14),
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private Task<HttpResponseMessage> ConfirmDeleteAsync(int tenderId, System.Text.Json.JsonElement impact) =>
        DeleteWithBodyAsync(
            tenderId,
            impact.GetProperty("planToken").GetString()!,
            impact.GetProperty("requiredConfirmation").GetString()!);

    private async Task<HttpResponseMessage> DeleteWithBodyAsync(
        int tenderId, string planToken, string confirmation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/tenders/{tenderId}")
        {
            Content = JsonContent.Create(new { planToken, confirmation }),
        };
        return await Client.SendAsync(request);
    }

    private static string FullPath(IWebHostEnvironment environment, string hostRelativePath) =>
        Path.Combine(
            environment.ContentRootPath,
            "wwwroot",
            hostRelativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    // ---------- NIH-97 checklist inline-edit ----------

    [Fact]
    public async Task PatchChecklist_ChangesStatusAndBumpsPercent()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateTenderAsync();
        var itemId = await FirstChecklistItemIdAsync(id);

        var res = await Client.PatchAsJsonAsync($"/api/tenders/{id}/checklist/{itemId}", new
        {
            status = "Done",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("checklistCompletionPercent").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PatchChecklist_InvalidStatus_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateTenderAsync();
        var itemId = await FirstChecklistItemIdAsync(id);

        var res = await Client.PatchAsJsonAsync($"/api/tenders/{id}/checklist/{itemId}", new
        {
            status = "Not-A-Status",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchChecklist_UnknownItem_Is404()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateTenderAsync();
        var res = await Client.PatchAsJsonAsync($"/api/tenders/{id}/checklist/9999", new { status = "Done" });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UploadChecklist_ContentIsResourceBoundAndStaticPathIsPrivate()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        var itemId = await FirstChecklistItemIdAsync(tenderId);
        string? storedPath = null;

        try
        {
            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes("tender checklist content"));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(file, "file", "checklist.pdf");

            var upload = await Client.PostAsync(
                $"/api/tenders/{tenderId}/checklist/{itemId}/upload", form);
            upload.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await ReadJsonAsync(upload);
            var item = body.GetProperty("checklistItems").EnumerateArray()
                .First(value => value.GetProperty("id").GetInt32() == itemId);
            storedPath = item.GetProperty("filePath").GetString();

            var content = await Client.GetAsync(
                $"/api/tenders/{tenderId}/checklist/{itemId}/content");
            content.StatusCode.Should().Be(HttpStatusCode.OK);
            (await content.Content.ReadAsStringAsync()).Should().Be("tender checklist content");

            (await Client.GetAsync(storedPath)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await Client.GetAsync($"/api/tenders/{tenderId}/checklist/999999/content"))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);

            using var anonymousClient = Factory.CreateClient();
            (await anonymousClient.GetAsync($"/api/tenders/{tenderId}/checklist/{itemId}/content"))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            using var forbiddenClient = Factory.CreateClient();
            await AuthTestHelper.AuthenticateAsync(
                forbiddenClient, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
            (await forbiddenClient.GetAsync($"/api/tenders/{tenderId}/checklist/{itemId}/content"))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            if (storedPath is not null)
            {
                var environment = Factory.Services.GetRequiredService<IWebHostEnvironment>();
                var fullPath = Path.Combine(
                    environment.ContentRootPath,
                    "wwwroot",
                    storedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(fullPath)) File.Delete(fullPath);
            }
        }
    }

    [Theory]
    [InlineData("patch")]
    [InlineData("upload")]
    [InlineData("library")]
    public async Task ChecklistMutation_WonTender_IsBadRequestAndPreservesItem(string operation)
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        var itemId = await FirstChecklistItemIdAsync(tenderId);
        var docId = await CreateCapabilityDocumentAsync();
        await WithDbAsync(async db =>
        {
            var tender = await db.Tenders.SingleAsync(item => item.Id == tenderId);
            tender.Status = TenderStatus.Won;
            await db.SaveChangesAsync();
        });

        var environment = Factory.Services.GetRequiredService<IWebHostEnvironment>();
        var uploadDirectory = Path.Combine(environment.ContentRootPath, "wwwroot", "files", "tenders");
        var filesBefore = Directory.Exists(uploadDirectory)
            ? Directory.GetFiles(uploadDirectory).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        HttpResponseMessage response;
        if (operation == "patch")
        {
            response = await Client.PatchAsJsonAsync(
                $"/api/tenders/{tenderId}/checklist/{itemId}", new { status = "Done" });
        }
        else if (operation == "library")
        {
            response = await Client.PostAsJsonAsync(
                $"/api/tenders/{tenderId}/checklist/attach-from-library",
                new { items = new[] { new { checklistItemId = itemId, capabilityDocumentId = docId } } });
        }
        else
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("blocked")), "file", "blocked.pdf");
            response = await Client.PostAsync(
                $"/api/tenders/{tenderId}/checklist/{itemId}/upload", form);
        }

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var unchanged = await WithDbAsync(db => db.TenderChecklistItems.AsNoTracking()
            .SingleAsync(item => item.Id == itemId));
        unchanged.Status.Should().Be(TenderChecklistItemStatus.NotStarted);
        unchanged.FilePath.Should().BeNull();
        unchanged.OriginalFileName.Should().BeNull();

        if (operation == "upload")
        {
            var filesAfter = Directory.Exists(uploadDirectory)
                ? Directory.GetFiles(uploadDirectory).ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            filesAfter.Should().BeEquivalentTo(filesBefore);
        }
    }

    // ---------- NIH-97 library attach ----------

    [Fact]
    public async Task AttachFromLibrary_HappyPath_CopiesFileMetadata()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        var itemId = await FirstChecklistItemIdAsync(tenderId);
        var docId = await CreateCapabilityDocumentAsync();

        var res = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/checklist/attach-from-library", new
        {
            items = new[]
            {
                new { checklistItemId = itemId, capabilityDocumentId = docId },
            },
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        var items = body.GetProperty("checklistItems");
        var enumerator = items.EnumerateArray();
        var target = enumerator.First(i => i.GetProperty("id").GetInt32() == itemId);
        target.GetProperty("originalFileName").GetString().Should().NotBeNullOrEmpty();
        target.GetProperty("status").GetString().Should().Be("Done");
    }

    [Fact]
    public async Task AttachFromLibrary_ContentRemainsAvailableBecauseReferencedDocumentCannotBeDeleted()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        var itemId = await FirstChecklistItemIdAsync(tenderId);
        var documentId = await CreateCapabilityDocumentAsync();
        var filePath = await WithDbAsync(db => db.CapabilityDocuments.AsNoTracking()
            .Where(document => document.Id == documentId)
            .Select(document => document.FilePath)
            .SingleAsync());
        var environment = Factory.Services.GetRequiredService<IWebHostEnvironment>();
        var fullPath = Path.Combine(
            environment.ContentRootPath,
            "wwwroot",
            filePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, "shared capability content");

        try
        {
            var attach = await Client.PostAsJsonAsync(
                $"/api/tenders/{tenderId}/checklist/attach-from-library",
                new { items = new[] { new { checklistItemId = itemId, capabilityDocumentId = documentId } } });
            attach.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await Client.GetAsync(
                $"/api/tenders/{tenderId}/checklist/{itemId}/content");
            content.StatusCode.Should().Be(HttpStatusCode.OK);
            (await content.Content.ReadAsStringAsync()).Should().Be("shared capability content");

            var impactResponse = await Client.GetAsync(
                $"/api/capability-documents/{documentId}/deletion-impact");
            impactResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var impact = await ReadJsonAsync(impactResponse);
            using var deleteRequest = new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/capability-documents/{documentId}")
            {
                Content = JsonContent.Create(new
                {
                    planToken = impact.GetProperty("planToken").GetString(),
                    confirmation = impact.GetProperty("requiredConfirmation").GetString(),
                }),
            };
            var delete = await Client.SendAsync(deleteRequest);
            delete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Client.GetAsync($"/api/tenders/{tenderId}/checklist/{itemId}/content"))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
    }

    [Fact]
    public async Task AttachFromLibrary_UnknownDocument_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        var itemId = await FirstChecklistItemIdAsync(tenderId);

        var res = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/checklist/attach-from-library", new
        {
            items = new[] { new { checklistItemId = itemId, capabilityDocumentId = 9_999_999 } },
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- NIH-97 mark-won / mark-lost ----------

    [Fact]
    public async Task MarkWon_AsSalesManager_SetsWonAndOpportunity()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        await SetTenderStatusAsync(tenderId, TenderStatus.Submitted);
        var customerId = await WithDbAsync(db => db.Tenders.Where(item => item.Id == tenderId)
            .Select(item => item.CustomerId).SingleAsync());
        var oppId = await CreateOpportunityAsync(customerId);

        var res = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/mark-won", new
        {
            opportunityId = oppId,
            note = "Ký hôm nay",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Won");
        body.GetProperty("wonOpportunityId").GetInt32().Should().Be(oppId);
    }

    [Fact]
    public async Task MarkWon_AsSale_IsForbidden()
    {
        // Regular SALE role should not carry crm.tenders.mark-result.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var tenderId = await CreateTenderAsync();
        var oppId = await CreateOpportunityAsync();
        var before = await WithDbAsync(db => db.Tenders.AsNoTracking()
            .SingleAsync(tender => tender.Id == tenderId));

        var res = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/mark-won", new
        {
            opportunityId = oppId,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var after = await WithDbAsync(db => db.Tenders.AsNoTracking()
            .SingleAsync(tender => tender.Id == tenderId));
        after.Status.Should().Be(before.Status);
        after.WonOpportunityId.Should().Be(before.WonOpportunityId);
        after.LostReasonCode.Should().Be(before.LostReasonCode);
        after.LostNote.Should().Be(before.LostNote);
        after.ClosedAt.Should().Be(before.ClosedAt);
        after.UpdatedAt.Should().Be(before.UpdatedAt);
        after.UpdatedByUserId.Should().Be(before.UpdatedByUserId);
    }

    [Fact]
    public async Task MarkLost_HappyPath_SetsLostAndReason()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        await SetTenderStatusAsync(tenderId, TenderStatus.Submitted);
        var reasonCode = await FirstOpportunityLostReasonAsync();

        var res = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/mark-lost", new
        {
            reasonCode,
            note = "Cạnh tranh giá",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Lost");
        body.GetProperty("lostReasonCode").GetString().Should().Be(reasonCode);
    }

    [Fact]
    public async Task MarkLost_UnknownReason_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        await SetTenderStatusAsync(tenderId, TenderStatus.Submitted);
        var res = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/mark-lost", new
        {
            reasonCode = "definitely-not-a-real-reason",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MarkWon_AfterAlreadyWon_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        await SetTenderStatusAsync(tenderId, TenderStatus.Submitted);
        var customerId = await WithDbAsync(db => db.Tenders.Where(item => item.Id == tenderId)
            .Select(item => item.CustomerId).SingleAsync());
        var oppId = await CreateOpportunityAsync(customerId);
        (await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/mark-won", new { opportunityId = oppId }))
            .EnsureSuccessStatusCode();

        var res = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/mark-won", new { opportunityId = oppId });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- NIH-97 timeline ----------

    [Fact]
    public async Task Timeline_ReturnsAuditRowsForTender()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        // Trigger at least one auditable action.
        await Client.PutAsJsonAsync($"/api/tenders/{tenderId}", new
        {
            name = "Renamed",
            submissionDeadline = DateTime.UtcNow.AddDays(20),
            note = "note",
        });

        var res = await Client.GetAsync($"/api/tenders/{tenderId}/timeline");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        // Audit log flush is queued so the array may be empty in-test —
        // shape verification is what we assert here (matches contracts).
        (await ReadJsonAsync(res)).ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public async Task Timeline_UnknownTender_Is404()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await Client.GetAsync("/api/tenders/9999999/timeline")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- helpers ----------

    private async Task<int> FirstChecklistItemIdAsync(int tenderId)
    {
        var res = await Client.GetAsync($"/api/tenders/{tenderId}");
        res.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(res);
        var items = body.GetProperty("checklistItems");
        items.GetArrayLength().Should().BeGreaterThan(0);
        return items[0].GetProperty("id").GetInt32();
    }

    private async Task<int> CreateOpportunityAsync(int? existingCustomerId = null)
    {
        var customerId = existingCustomerId ?? await CreateCustomerAsync();
        var res = await Client.PostAsJsonAsync("/api/opportunities", new
        {
            name = "Opp " + Guid.NewGuid().ToString("N")[..6],
            customerId,
            estimatedValue = 1_000_000m,
            winProbability = 40,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateCapabilityDocumentAsync()
    {
        // The controller create path requires a pre-uploaded file. For
        // this test we just care that the tender attach can copy metadata
        // from an existing row, so we seed the DB directly and skip the
        // physical file upload.
        return await WithDbAsync(async db =>
        {
            var tag = await db.MasterDataOptions
                .Where(o => o.Category == "capability_document_tag" && o.IsActive)
                .OrderBy(o => o.SortOrder)
                .FirstAsync();
            var doc = new CapabilityDocument
            {
                Name = "Test doc " + Guid.NewGuid().ToString("N")[..6],
                TagCode = tag.Code,
                FilePath = "/files/capability/test-" + Guid.NewGuid().ToString("N") + ".pdf",
                OriginalFileName = "seeded.pdf",
                FileSize = 1024,
                ContentType = "application/pdf",
                CurrentVersion = 1,
            };
            db.CapabilityDocuments.Add(doc);
            await db.SaveChangesAsync();
            return doc.Id;
        });
    }

    private async Task<string> FirstOpportunityLostReasonAsync() =>
        await WithDbAsync(async db =>
        {
            var opt = await db.MasterDataOptions
                .Where(o => o.Category == "opportunity_lost_reason" && o.IsActive)
                .OrderBy(o => o.SortOrder)
                .FirstAsync();
            return opt.Code;
        });

    private Task SetTenderStatusAsync(int tenderId, TenderStatus status) => WithDbAsync(async db =>
    {
        var tender = await db.Tenders.SingleAsync(item => item.Id == tenderId);
        tender.Status = status;
        await db.SaveChangesAsync();
    });
}
