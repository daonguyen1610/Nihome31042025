using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class ContractRemindersTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private Task<int> SeedAsync(string role = "SALE", ContractStatus status = ContractStatus.Signed,
        ContractDirection direction = ContractDirection.Upstream) => WithDbAsync(async db =>
    {
        var phone = TestDataSeeder.BusinessRolePhonesByCode[role];
        var owner = await db.Users.SingleAsync(item => item.PhoneNumber == phone);
        var contract = new Contract
        {
            ContractNumber = $"REMINDER-{Guid.NewGuid():N}",
            OwnerUserId = owner.Id,
            Customer = new Customer
            {
                Name = "Nhà máy An Phú",
                SourceCode = "marketing",
                OwnerUserId = owner.Id,
                Contacts = [new() { FullName = "Mai Anh", Email = "mai.anh@example.test", IsPrimary = true }]
            },
            Direction = direction,
            Type = ContractType.DesignAndBuild,
            Status = status,
            EndDate = ContractReminderService.Today.AddDays(30),
            SignedDate = ContractReminderService.Today.AddDays(-30),
        };
        db.Contracts.Add(contract); await db.SaveChangesAsync(); return contract.Id;
    });

    private async Task LoginAsync(string role = "SALE") =>
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));

    private Task<HttpResponseMessage> PreviewAsync(int id, string kind = "Renewal", int days = 1) =>
        Client.PostAsJsonAsync($"/api/contracts/{id}/reminders/preview", new
        { kind, reminderDate = DateOnly.FromDateTime(ContractReminderService.Today.AddDays(days)).ToString("yyyy-MM-dd") });

    private async Task<string> TokenAsync(int id, string kind = "Renewal", int days = 1)
    {
        var response = await PreviewAsync(id, kind, days);
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("previewToken").GetString()!;
    }

    [Theory]
    [InlineData("Renewal")]
    [InlineData("End")]
    public async Task Owner_PreviewsSchedulesAndCancels_WithReplayProtection(string kind)
    {
        var id = await SeedAsync(); await LoginAsync();
        var token = await TokenAsync(id, kind);
        var first = await Client.PostAsJsonAsync($"/api/contracts/{id}/reminders", new { previewToken = token });
        first.EnsureSuccessStatusCode();
        var reminder = await ReadJsonAsync(first);
        var repeated = await Client.PostAsJsonAsync($"/api/contracts/{id}/reminders", new { previewToken = token });
        repeated.EnsureSuccessStatusCode();
        (await ReadJsonAsync(repeated)).GetProperty("id").GetInt32().Should().Be(reminder.GetProperty("id").GetInt32());
        var secondToken = await TokenAsync(id, kind);
        (await Client.PostAsJsonAsync($"/api/contracts/{id}/reminders", new { previewToken = secondToken })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var list = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{id}/reminders"));
        list.GetArrayLength().Should().Be(1);
        (await Client.PostAsync($"/api/contracts/{id}/reminders/{reminder.GetProperty("id").GetInt32()}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await TokenAsync(id, kind)).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UnauthenticatedAndReadOnlyRoles_CannotSchedule()
    {
        var id = await SeedAsync();
        (await PreviewAsync(id)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await LoginAsync("ACCOUNTANT");
        (await PreviewAsync(id)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.PostAsJsonAsync($"/api/contracts/{id}/reminders", new { previewToken = "forged" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonOwnerCannotAccessScheduleOrUseAnotherOwnersPreview()
    {
        var id = await SeedAsync("SALES_MANAGER"); await LoginAsync("SALES_MANAGER");
        var token = await TokenAsync(id);
        await LoginAsync();
        (await Client.GetAsync($"/api/contracts/{id}/reminders")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PreviewAsync(id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.PostAsJsonAsync($"/api/contracts/{id}/reminders", new { previewToken = token })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(ContractStatus.Draft, ContractDirection.Upstream)]
    [InlineData(ContractStatus.Cancelled, ContractDirection.Upstream)]
    [InlineData(ContractStatus.Completed, ContractDirection.Upstream)]
    [InlineData(ContractStatus.Signed, ContractDirection.Downstream)]
    public async Task IneligibleContractsCannotPreview(ContractStatus status, ContractDirection direction)
    {
        var id = await SeedAsync(status: status, direction: direction); await LoginAsync();
        (await PreviewAsync(id)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Unknown", 1)]
    [InlineData("Renewal", -1)]
    public async Task InvalidKindOrPastDateRejected(string kind, int days)
    {
        var id = await SeedAsync(); await LoginAsync();
        (await PreviewAsync(id, kind, days)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangedContractOrTemplateInvalidatesPreview()
    {
        var id = await SeedAsync(); await LoginAsync();
        var token = await TokenAsync(id);
        await WithDbAsync(async db => { var contract = await db.Contracts.FindAsync(id); contract!.EndDate = contract.EndDate!.Value.AddDays(1); await db.SaveChangesAsync(); });
        (await Client.PostAsJsonAsync($"/api/contracts/{id}/reminders", new { previewToken = token })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var missing = await Client.PostAsJsonAsync($"/api/contracts/{id}/reminders", new { previewToken = "forged" });
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{id}/reminders"))).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task DueWorkerPersistsDeliveryAndHistory_AndDeleteDisclosesSchedules()
    {
        var id = await SeedAsync(); await LoginAsync();
        var token = await TokenAsync(id, days: 0);
        (await Client.PostAsJsonAsync($"/api/contracts/{id}/reminders", new { previewToken = token })).EnsureSuccessStatusCode();
        using (var scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ContractReminderService>().SendDueAsync();
        var list = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{id}/reminders"));
        list[0].GetProperty("status").GetString().Should().Be("Sent");
        await WithDbAsync(async db => (await db.ContractCareActivities.CountAsync(item => item.ContractId == id)).Should().Be(1));
        var impact = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{id}/deletion-impact"));
        impact.ToString().Should().Contain("contract.reminders");
        await LoginAsync("SUPER_ADMIN");
        var contract = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{id}"));
        var deleted = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/contracts/{id}")
        {
            Content = JsonContent.Create(new
            {
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
                planToken = impact.GetProperty("planToken").GetString(),
                rowVersion = contract.GetProperty("rowVersion").GetString()
            })
        });
        deleted.IsSuccessStatusCode.Should().BeTrue(await deleted.Content.ReadAsStringAsync());
        await WithDbAsync(async db => (await db.ContractReminders.CountAsync(item => item.ContractId == id)).Should().Be(0));
    }

    [Fact]
    public async Task AdminTemplatesValidateAndPreserveOtherEmailTypes()
    {
        await LoginAsync();
        (await Client.GetAsync("/api/site-settings/contract-reminder-templates")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await LoginAsync("SUPER_ADMIN");
        var original = await Client.GetStringAsync("/api/site-settings/email-templates");
        var data = new
        {
            renewalSubject = "Gia hạn {{contractNumber}}",
            renewalBody = "<p>{{customerName}} {{contractNumber}} {{endDate}}</p>",
            endSubject = "Kết thúc {{contractNumber}}",
            endBody = "<p>{{contractNumber}} {{endDate}}</p>"
        };
        (await Client.PutAsJsonAsync("/api/site-settings/contract-reminder-templates", data)).EnsureSuccessStatusCode();
        (await Client.GetStringAsync("/api/site-settings/email-templates")).Should().Be(original);
        (await Client.PutAsJsonAsync("/api/site-settings/contract-reminder-templates", new { data.renewalSubject, data.renewalBody, data.endSubject, endBody = "<script>{{contractNumber}}{{endDate}}</script>" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AlternateContactIsSelected_AndForeignOrInvalidContactsAreRejected()
    {
        var id = await SeedAsync(); var otherId = await SeedAsync(); await LoginAsync();
        var contacts = await WithDbAsync(async db =>
        {
            var customerId = await db.Contracts.Where(item => item.Id == id).Select(item => item.CustomerId).SingleAsync();
            var contact = new CustomerContact { CustomerId = customerId, FullName = "Đại diện tài chính An Phú", Email = "finance.anphu@example.test" };
            db.CustomerContacts.Add(contact); await db.SaveChangesAsync();
            var foreign = await db.CustomerContacts.Where(item => item.CustomerId == db.Contracts.Where(c => c.Id == otherId).Select(c => c.CustomerId).First()).Select(item => item.Id).FirstAsync();
            return (contact.Id, Foreign: foreign);
        });
        foreach (var contactId in new[] { contacts.Id, contacts.Foreign, 0 })
        {
            var response = await Client.PostAsJsonAsync($"/api/contracts/{id}/reminders/preview", new
            { kind = "End", reminderDate = DateOnly.FromDateTime(ContractReminderService.Today).ToString("yyyy-MM-dd"), contactId });
            response.StatusCode.Should().Be(contactId == contacts.Id ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
            if (response.IsSuccessStatusCode) (await ReadJsonAsync(response)).GetProperty("toEmail").GetString().Should().Be("finance.anphu@example.test");
        }
    }

    [Fact]
    public async Task SendingBlocksDeletionAndCancellation_AndScheduleInvalidatesDeletionPlan()
    {
        var id = await SeedAsync(); await LoginAsync("SUPER_ADMIN");
        var oldPlan = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{id}/deletion-impact"));
        var token = await TokenAsync(id);
        var scheduled = await ReadJsonAsync(await Client.PostAsJsonAsync($"/api/contracts/{id}/reminders", new { previewToken = token }));
        var reminderId = scheduled.GetProperty("id").GetInt32();
        var contract = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{id}"));
        var deleted = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/contracts/{id}")
        {
            Content = JsonContent.Create(new
            {
                confirmation = oldPlan.GetProperty("requiredConfirmation").GetString(),
                planToken = oldPlan.GetProperty("planToken").GetString(),
                rowVersion = contract.GetProperty("rowVersion").GetString()
            })
        });
        deleted.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await WithDbAsync(async db => { var reminder = await db.ContractReminders.FindAsync(reminderId); reminder!.Status = "Sending"; await db.SaveChangesAsync(); });
        (await Client.PostAsync($"/api/contracts/{id}/reminders/{reminderId}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var impact = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{id}/deletion-impact"));
        var group = impact.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("key").GetString() == "contract.remindersSending");
        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        group.GetProperty("action").GetString().Should().Be("Block");
        group.GetProperty("count").GetInt32().Should().Be(1);
        var blocked = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/contracts/{id}")
        {
            Content = JsonContent.Create(new
            {
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
                planToken = impact.GetProperty("planToken").GetString(),
                rowVersion = contract.GetProperty("rowVersion").GetString()
            })
        });
        blocked.IsSuccessStatusCode.Should().BeFalse();
        (await Client.GetAsync($"/api/contracts/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        await WithDbAsync(async db => (await db.ContractReminders.SingleAsync(item => item.Id == reminderId)).Status.Should().Be("Sending"));
    }
}
