using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

public class OperationalProjectsControllerTests : IntegrationTestBase
{
    public OperationalProjectsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuthentication_IsUnauthorized()
    {
        var response = await Client.GetAsync("/api/operational-projects");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletionImpact_WithoutAuthentication_IsUnauthorized()
    {
        var response = await Client.GetAsync("/api/operational-projects/1/deletion-impact");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SuperAdmin_CanCreateReadAndActivateProject()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();

        var create = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Operational {Guid.NewGuid():N}",
            customerId,
            startDate = "2026-08-01",
            endDate = "2026-12-31",
        });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(create);
        created.GetProperty("code").GetString().Should().StartWith("PJ-");
        created.GetProperty("status").GetString().Should().Be("Planning");

        var id = created.GetProperty("id").GetInt32();
        var update = await Client.PutAsJsonAsync($"/api/operational-projects/{id}", new
        {
            name = created.GetProperty("name").GetString(),
            customerId,
            projectManagerUserId = created.GetProperty("projectManagerUserId").GetInt32(),
            startDate = "2026-08-01",
            endDate = "2026-12-31",
            status = "Active",
            rowVersion = created.GetProperty("rowVersion").GetString(),
        });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(update)).GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task Opportunity_ProjectFromDifferentCustomer_IsRejected()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerIds = new[]
        {
            await CreateCustomerAsync("Project owner"),
            await CreateCustomerAsync("Different customer"),
        };
        var projectResponse = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Cross customer {Guid.NewGuid():N}",
            customerId = customerIds[0],
        });
        projectResponse.EnsureSuccessStatusCode();
        var projectId = (await ReadJsonAsync(projectResponse)).GetProperty("id").GetInt32();

        var opportunityResponse = await Client.PostAsJsonAsync("/api/opportunities", new
        {
            name = $"Wrong project {Guid.NewGuid():N}",
            customerId = customerIds[1],
            operationalProjectId = projectId,
            estimatedValue = 1000,
            winProbability = 10,
            stage = "Prospecting",
        });

        opportunityResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Contract_InheritsProjectFromOpportunity()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var projectResponse = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Inheritance {Guid.NewGuid():N}",
            customerId,
        });
        projectResponse.EnsureSuccessStatusCode();
        var projectId = (await ReadJsonAsync(projectResponse)).GetProperty("id").GetInt32();

        var opportunityResponse = await Client.PostAsJsonAsync("/api/opportunities", new
        {
            name = $"Inheritance opportunity {Guid.NewGuid():N}",
            customerId,
            operationalProjectId = projectId,
            estimatedValue = 1000,
            winProbability = 20,
            stage = "Prospecting",
        });
        opportunityResponse.EnsureSuccessStatusCode();
        var opportunityId = (await ReadJsonAsync(opportunityResponse)).GetProperty("id").GetInt32();

        var contractResponse = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            opportunityId,
            status = "Draft",
            value = 1000,
        });

        contractResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJsonAsync(contractResponse))
            .GetProperty("operationalProjectId").GetInt32().Should().Be(projectId);
    }

    [Fact]
    public async Task Opportunity_UpdateWithoutProjectField_PreservesLink()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var projectResponse = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Preserved link {Guid.NewGuid():N}",
            customerId,
        });
        projectResponse.EnsureSuccessStatusCode();
        var projectId = (await ReadJsonAsync(projectResponse)).GetProperty("id").GetInt32();
        var create = await Client.PostAsJsonAsync("/api/opportunities", new
        {
            name = "Linked opportunity",
            customerId,
            operationalProjectId = projectId,
            estimatedValue = 1000,
            winProbability = 25,
            stage = "Prospecting",
        });
        create.EnsureSuccessStatusCode();
        var opportunity = await ReadJsonAsync(create);

        var update = await Client.PutAsJsonAsync(
            $"/api/opportunities/{opportunity.GetProperty("id").GetInt32()}",
            new
            {
                name = "Updated linked opportunity",
                customerId,
                estimatedValue = 2000,
                winProbability = 30,
                rowVersion = opportunity.GetProperty("rowVersion").GetString(),
            });

        update.EnsureSuccessStatusCode();
        (await ReadJsonAsync(update)).GetProperty("operationalProjectId")
            .GetInt32().Should().Be(projectId);
    }

    [Fact]
    public async Task DesignProject_InheritsProjectFromContract()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var projectManagerId = await WithDbAsync(db => db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
            .Select(user => user.Id)
            .SingleAsync());
        var designLeadId = await WithDbAsync(db => db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["DESIGN_LEAD"])
            .Select(user => user.Id)
            .SingleAsync());
        var projectResponse = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Design inheritance {Guid.NewGuid():N}",
            customerId,
        });
        projectResponse.EnsureSuccessStatusCode();
        var projectId = (await ReadJsonAsync(projectResponse)).GetProperty("id").GetInt32();
        var contractResponse = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            operationalProjectId = projectId,
            status = "Draft",
            value = 1000,
        });
        contractResponse.EnsureSuccessStatusCode();
        var contractId = (await ReadJsonAsync(contractResponse)).GetProperty("id").GetInt32();

        var designResponse = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = "Inherited design workflow",
            customerId,
            contractId,
            projectManagerUserId = projectManagerId,
            designLeadUserId = designLeadId,
        });

        designResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJsonAsync(designResponse)).GetProperty("operationalProjectId")
            .GetInt32().Should().Be(projectId);
        var roles = await WithDbAsync(db => db.OperationalProjectMemberRoles
            .Where(role => role.Member.OperationalProjectId == projectId && role.EndedAt == null)
            .Select(role => new
            {
                role.Member.UserId,
                role.RoleCode,
                role.Scope,
                role.ScopeValue,
                role.Source,
            })
            .ToListAsync());
        roles.Should().Contain(role =>
            role.UserId == projectManagerId &&
            role.RoleCode == ProjectTeamRoleCode.ProjectManager &&
            role.Scope == ProjectRoleScope.Module &&
            role.ScopeValue == "Design" &&
            role.Source == LegacyProjectTeamSyncService.RuntimeSource);
        roles.Should().Contain(role =>
            role.UserId == designLeadId &&
            role.RoleCode == ProjectTeamRoleCode.DesignLead &&
            role.Scope == ProjectRoleScope.Module &&
            role.ScopeValue == "Design" &&
            role.Source == LegacyProjectTeamSyncService.RuntimeSource);
    }

    [Fact]
    public async Task Delete_ProjectWithIndependentContract_UnlinksContractAndDeletesProject()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var projectResponse = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Protected {Guid.NewGuid():N}",
            customerId,
        });
        projectResponse.EnsureSuccessStatusCode();
        var project = await ReadJsonAsync(projectResponse);
        var projectId = project.GetProperty("id").GetInt32();
        await WithDbAsync(async db =>
        {
            db.Contracts.Add(new Contract
            {
                ContractNumber = $"HD-OP-{Guid.NewGuid():N}",
                CustomerId = customerId,
                OperationalProjectId = projectId,
            });
            await db.SaveChangesAsync();
        });
        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/operational-projects/{projectId}/deletion-impact"));
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "operations.contracts" &&
            item.GetProperty("action").GetString() == "Unlink" &&
            item.GetProperty("count").GetInt32() == 1);
        var delete = await ConfirmDeleteAsync(projectId, project, impact);

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await WithDbAsync(db => db.OperationalProjects.AnyAsync(item => item.Id == projectId)))
            .Should().BeFalse();
        (await WithDbAsync(db => db.Contracts
            .SingleAsync(item => item.OperationalProjectId == null && item.CustomerId == customerId)))
            .OperationalProjectId.Should().BeNull();
    }

    [Fact]
    public async Task Delete_ProjectWithTeamHistoryOnly_DeletesOwnedHistory()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var seeded = await WithDbAsync(async db =>
        {
            var userId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.SuperAdminPhone)
                .Select(user => user.Id)
                .SingleAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-HIST-{Guid.NewGuid():N}"[..24],
                Name = "Team history only",
                CustomerId = customerId,
                ProjectManagerUserId = userId,
                CreatedByUserId = userId,
                UpdatedByUserId = userId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            db.OperationalProjectTeamHistory.Add(new OperationalProjectTeamHistory
            {
                OperationalProjectId = project.Id,
                EntityType = "Project",
                EntityId = project.Id,
                Action = "Created",
                SnapshotJson = "{}",
                ChangedByUserId = userId,
            });
            await db.SaveChangesAsync();
            return new { project.Id, RowVersion = CrmConcurrency.Encode(project.RowVersion) };
        });
        var projectId = seeded.Id;
        var historyCount = await WithDbAsync(db => db.OperationalProjectTeamHistory
            .CountAsync(item => item.OperationalProjectId == projectId));
        (await WithDbAsync(db => db.OperationalProjectMembers
            .CountAsync(item => item.OperationalProjectId == projectId))).Should().Be(0);
        (await WithDbAsync(db => db.OperationalProjectAssignments
            .CountAsync(item => item.OperationalProjectId == projectId))).Should().Be(0);
        var project = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            rowVersion = seeded.RowVersion,
        })).RootElement.Clone();
        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/operational-projects/{projectId}/deletion-impact"));
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "operations.teamHistory" &&
            item.GetProperty("count").GetInt32() == historyCount);
        var delete = await ConfirmDeleteAsync(projectId, project, impact);

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await WithDbAsync(db => db.OperationalProjects.AnyAsync(item => item.Id == projectId)))
            .Should().BeFalse();
        (await WithDbAsync(db => db.OperationalProjectTeamHistory
            .CountAsync(item => item.OperationalProjectId == projectId)))
            .Should().Be(0);
    }

    [Fact]
    public async Task ActiveMember_CanReadPortfolioDetailAndTimeline_ButCannotMutate()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var projectResponse = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Member visible {Guid.NewGuid():N}",
            customerId,
        });
        projectResponse.EnsureSuccessStatusCode();
        var project = await ReadJsonAsync(projectResponse);
        var projectId = project.GetProperty("id").GetInt32();
        var memberUserId = await WithDbAsync(db => db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
            .Select(user => user.Id)
            .SingleAsync());
        await WithDbAsync(async db =>
        {
            db.OperationalProjectMembers.Add(new OperationalProjectMember
            {
                OperationalProjectId = projectId,
                UserId = memberUserId,
                Position = "Observer",
                StartedAt = DateTime.UtcNow.AddDays(-1),
                CreatedByUserId = memberUserId,
                UpdatedByUserId = memberUserId,
                Roles =
                [
                    new OperationalProjectMemberRole
                    {
                        RoleCode = ProjectTeamRoleCode.Observer,
                        Scope = ProjectRoleScope.Project,
                        StartedAt = DateTime.UtcNow.AddDays(-1),
                    },
                ],
            });
            await db.SaveChangesAsync();
        });

        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));
        var list = await ReadJsonAsync(await Client.GetAsync("/api/operational-projects"));
        var detail = await Client.GetAsync($"/api/operational-projects/{projectId}");
        var timeline = await Client.GetAsync($"/api/operational-projects/{projectId}/timeline");
        var update = await Client.PutAsJsonAsync($"/api/operational-projects/{projectId}", new
        {
            name = project.GetProperty("name").GetString(),
            customerId,
            projectManagerUserId = project.GetProperty("projectManagerUserId").GetInt32(),
            status = "Planning",
            rowVersion = project.GetProperty("rowVersion").GetString(),
        });
        var impact = await Client.GetAsync($"/api/operational-projects/{projectId}/deletion-impact");
        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/operational-projects/{projectId}")
        {
            Content = JsonContent.Create(new
            {
                planToken = new string('a', 64),
                confirmation = project.GetProperty("code").GetString(),
                rowVersion = project.GetProperty("rowVersion").GetString(),
            }),
        };
        deleteRequest.Headers.IfMatch.ParseAdd($"\"{project.GetProperty("rowVersion").GetString()}\"");
        var delete = await Client.SendAsync(deleteRequest);

        list.GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetInt32() == projectId);
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        timeline.StatusCode.Should().Be(HttpStatusCode.OK);
        update.StatusCode.Should().Be(HttpStatusCode.NotFound);
        impact.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await WithDbAsync(db => db.OperationalProjects.AnyAsync(item => item.Id == projectId)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WhenImpactChanged_ReturnsConflictAndPreservesProject()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var create = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Stale plan {Guid.NewGuid():N}",
            customerId,
        });
        create.EnsureSuccessStatusCode();
        var project = await ReadJsonAsync(create);
        var projectId = project.GetProperty("id").GetInt32();
        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/operational-projects/{projectId}/deletion-impact"));
        await WithDbAsync(async db =>
        {
            db.Contracts.Add(new Contract
            {
                ContractNumber = $"HD-STALE-{Guid.NewGuid():N}",
                CustomerId = customerId,
                OperationalProjectId = projectId,
            });
            await db.SaveChangesAsync();
        });

        var delete = await ConfirmDeleteAsync(projectId, project, impact);

        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.OperationalProjects.AnyAsync(item => item.Id == projectId)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task DeletionImpact_WithPendingDocument_BlocksDelete()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var create = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"File blocker {Guid.NewGuid():N}",
            customerId,
        });
        create.EnsureSuccessStatusCode();
        var project = await ReadJsonAsync(create);
        var projectId = project.GetProperty("id").GetInt32();
        await WithDbAsync(async db =>
        {
            db.ProjectDocuments.Add(new ProjectDocument
            {
                OperationalProjectId = projectId,
                LocalPath = $"/files/{Guid.NewGuid():N}.pdf",
                OriginalFileName = "pending.pdf",
                ContentType = "application/pdf",
                Sha256 = new string('a', 64),
                SyncStatus = ProjectDocumentSyncStatus.Pending,
            });
            await db.SaveChangesAsync();
        });
        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/operational-projects/{projectId}/deletion-impact"));

        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "operations.pendingDocuments" &&
            item.GetProperty("action").GetString() == "Block");
        (await ConfirmDeleteAsync(projectId, project, impact)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.OperationalProjects.AnyAsync(item => item.Id == projectId)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithUnverifiedDriveFolder_BlocksAndPreservesBinding()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var create = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Drive folder unlink {Guid.NewGuid():N}",
            customerId,
        });
        create.EnsureSuccessStatusCode();
        var project = await ReadJsonAsync(create);
        var projectId = project.GetProperty("id").GetInt32();
        var externalFolderId = $"drive-folder-{Guid.NewGuid():N}";
        await WithDbAsync(async db =>
        {
            db.ProjectDriveFolders.Add(new ProjectDriveFolder
            {
                OperationalProjectId = projectId,
                Category = ProjectDocumentCategory.DesignConcept,
                DriveFolderId = externalFolderId,
            });
            await db.SaveChangesAsync();
        });

        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/operational-projects/{projectId}/deletion-impact"));

        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "operations.pendingDocuments" &&
            item.GetProperty("action").GetString() == "Block" &&
            item.GetProperty("count").GetInt32() == 1);
        (await ConfirmDeleteAsync(projectId, project, impact)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ProjectDriveFolders
            .AnyAsync(folder => folder.DriveFolderId == externalFolderId))).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithStaleRowVersion_ReturnsConflictAndPreservesProject()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var create = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Stale row version {Guid.NewGuid():N}",
            customerId,
        });
        create.EnsureSuccessStatusCode();
        var project = await ReadJsonAsync(create);
        var projectId = project.GetProperty("id").GetInt32();
        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/operational-projects/{projectId}/deletion-impact"));
        var update = await Client.PutAsJsonAsync($"/api/operational-projects/{projectId}", new
        {
            name = $"Updated stale row version {Guid.NewGuid():N}",
            customerId,
            projectManagerUserId = project.GetProperty("projectManagerUserId").GetInt32(),
            status = project.GetProperty("status").GetString(),
            rowVersion = project.GetProperty("rowVersion").GetString(),
        });
        update.EnsureSuccessStatusCode();

        var delete = await ConfirmDeleteAsync(projectId, project, impact);

        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.OperationalProjects.AnyAsync(item => item.Id == projectId)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithoutRowVersion_ReturnsBadRequestAndPreservesProject()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var create = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Missing row version {Guid.NewGuid():N}",
            customerId,
        });
        create.EnsureSuccessStatusCode();
        var project = await ReadJsonAsync(create);
        var projectId = project.GetProperty("id").GetInt32();
        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/operational-projects/{projectId}/deletion-impact"));
        var delete = await Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/operational-projects/{projectId}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
            }),
        });

        delete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.OperationalProjects.AnyAsync(item => item.Id == projectId)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Delete_SampleProject_WritesTombstone()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var project = await WithDbAsync<OperationalProject>(async db =>
        {
            var item = new OperationalProject
            {
                Code = $"PJ-SAMPLE-{Guid.NewGuid():N}"[..40],
                Name = "Sample operational deletion",
                CustomerId = customerId,
            };
            db.OperationalProjects.Add(item);
            await db.SaveChangesAsync();
            return item;
        });
        var response = await Client.GetAsync($"/api/operational-projects/{project.Id}");
        var detail = await ReadJsonAsync(response);
        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/operational-projects/{project.Id}/deletion-impact"));

        (await ConfirmDeleteAsync(project.Id, detail, impact)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await WithDbAsync(db => db.SeededRootDeletions.AnyAsync(item =>
            item.ResourceType == NihomeBackend.Constants.EntityTypes.OperationalProject &&
            item.ResourceKey == project.Code))).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithSurveyChildren_PreviewsAndDeletesNestedRecords()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var create = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Survey child deletion {Guid.NewGuid():N}",
            customerId,
        });
        create.EnsureSuccessStatusCode();
        var project = await ReadJsonAsync(create);
        var projectId = project.GetProperty("id").GetInt32();
        var surveyId = await WithDbAsync<int>(async db =>
        {
            var survey = new Survey
            {
                Code = $"SV-{Guid.NewGuid():N}"[..24],
                Location = "Nested deletion fixture",
                SurveyDate = DateTime.UtcNow,
                OperationalProjectId = projectId,
                DriveFolderId = $"survey-drive-{Guid.NewGuid():N}",
                DriveFolderLink = "https://drive.google.com/drive/folders/test",
            };
            db.Surveys.Add(survey);
            await db.SaveChangesAsync();
            db.SurveyChecklistResults.Add(new SurveyChecklistResult
            {
                SurveyId = survey.Id,
                TemplateCode = "access",
                TemplateTitle = "Site access",
            });
            db.SurveySiteConditions.Add(new SurveySiteCondition
            {
                SurveyId = survey.Id,
                Code = "right-of-way",
                Category = SurveySiteConditionCategory.RightOfWay,
                Status = SurveySiteConditionStatus.Available,
            });
            await db.SaveChangesAsync();
            return survey.Id;
        });
        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/operational-projects/{projectId}/deletion-impact"));

        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "operations.surveyChecklistResults" &&
            item.GetProperty("count").GetInt32() == 1);
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "operations.surveySiteConditions" &&
            item.GetProperty("count").GetInt32() == 1);
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "operations.surveyDriveFolders" &&
            item.GetProperty("action").GetString() == "Block" &&
            item.GetProperty("count").GetInt32() == 1);
        (await ConfirmDeleteAsync(projectId, project, impact)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.Surveys.AnyAsync(item => item.Id == surveyId))).Should().BeTrue();
        (await WithDbAsync(db => db.SurveyChecklistResults.AnyAsync(item => item.SurveyId == surveyId)))
            .Should().BeTrue();
        (await WithDbAsync(db => db.SurveySiteConditions.AnyAsync(item => item.SurveyId == surveyId)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Timeline_AggregatesContractsAndRepeatedReadIsStable()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var projectId = await WithDbAsync(async db =>
        {
            var managerId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.SuperAdminPhone)
                .Select(user => user.Id)
                .SingleAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-TL-{Guid.NewGuid():N}"[..20],
                Name = "Timeline aggregation",
                CustomerId = customerId,
                ProjectManagerUserId = managerId,
                CreatedByUserId = managerId,
                UpdatedByUserId = managerId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            var contracts = new[]
            {
                new Contract
                {
                    ContractNumber = $"HD-TL-A-{Guid.NewGuid():N}"[..24],
                    CustomerId = customerId,
                    OperationalProjectId = project.Id,
                    Value = 10_000,
                },
                new Contract
                {
                    ContractNumber = $"HD-TL-B-{Guid.NewGuid():N}"[..24],
                    CustomerId = customerId,
                    OperationalProjectId = project.Id,
                    Value = 20_000,
                },
            };
            db.Contracts.AddRange(contracts);
            await db.SaveChangesAsync();
            var milestones = new[]
            {
                new ContractPaymentMilestone
                {
                    ContractId = contracts[0].Id,
                    Order = 1,
                    Name = "Advance",
                    PercentValue = 20,
                    DueDate = new DateTime(2026, 9, 1),
                },
                new ContractPaymentMilestone
                {
                    ContractId = contracts[1].Id,
                    Order = 1,
                    Name = new string('M', 200),
                    PercentValue = 50,
                    DueDate = new DateTime(2026, 8, 1),
                    Status = PaymentMilestoneStatus.Paid,
                    ActualPaymentDate = new DateTime(2026, 8, 30),
                    Note = new string('N', 500),
                },
            };
            db.ContractPaymentMilestones.AddRange(milestones);
            await db.SaveChangesAsync();
            return project.Id;
        });

        var first = await Client.GetAsync($"/api/operational-projects/{projectId}/timeline");
        var second = await Client.GetAsync($"/api/operational-projects/{projectId}/timeline");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await ReadJsonAsync(first)).EnumerateArray().ToList();
        items.Should().HaveCount(2);
        items[0].GetProperty("status").GetString().Should().Be("Paid");
        items[0].GetProperty("contractNumber").GetString().Should().StartWith("HD-TL-B-");
        items[0].GetProperty("plannedDate").GetDateTime().Should().Be(new DateTime(2026, 8, 1));
        items[0].GetProperty("actualDate").GetDateTime().Should().Be(new DateTime(2026, 8, 30));
        items[1].GetProperty("actualDate").ValueKind.Should().Be(JsonValueKind.Null);
        items[0].GetProperty("source").GetString().Should().Be("ContractPaymentMilestone");
        items[0].GetProperty("name").GetString().Should().HaveLength(200);
        items[0].GetProperty("note").GetString().Should().HaveLength(500);
        (await second.Content.ReadAsStringAsync()).Should().Be(await first.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Timeline_ProjectOutsideCallerScope_IsNotFound()
    {
        var customerId = await CreateCustomerAsync();
        var projectId = await WithDbAsync(async db =>
        {
            var ownerId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.SuperAdminPhone)
                .Select(user => user.Id)
                .SingleAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-SCOPE-{Guid.NewGuid():N}"[..24],
                Name = "Restricted timeline",
                CustomerId = customerId,
                ProjectManagerUserId = ownerId,
                CreatedByUserId = ownerId,
                UpdatedByUserId = ownerId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SALE"));

        var response = await Client.GetAsync($"/api/operational-projects/{projectId}/timeline");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Timeline_UnknownProject_IsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));

        var response = await Client.GetAsync("/api/operational-projects/2147483647/timeline");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private Task<int> CreateCustomerAsync(string name = "Operational project customer")
    {
        return WithDbAsync(async db =>
        {
            var customer = new Customer
            {
                Name = $"{name} {Guid.NewGuid():N}",
                Type = CustomerType.Company,
                SourceCode = "referral",
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            return customer.Id;
        });
    }

    private async Task<HttpResponseMessage> ConfirmDeleteAsync(
        int projectId,
        JsonElement project,
        JsonElement impact)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/operational-projects/{projectId}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
                rowVersion = project.GetProperty("rowVersion").GetString(),
            }),
        };
        request.Headers.IfMatch.ParseAdd(
            $"\"{project.GetProperty("rowVersion").GetString()}\"");
        return await Client.SendAsync(request);
    }
}
