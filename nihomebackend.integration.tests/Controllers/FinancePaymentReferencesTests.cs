using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class FinancePaymentReferencesTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Accountant_DiscoversEligibleProcurementContract_WithoutGainingCrmAccessOrSensitiveFields()
    {
        var fixture = await SeedAsync();
        await LoginAsync("ACCOUNTANT");
        using var response = await Client.GetAsync("/api/finance/payment-references");
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        var references = await ReadJsonAsync(response);
        Fields(references).Should().BeEquivalentTo("contracts", "vendors", "accountants");
        var contracts = references.GetProperty("contracts").EnumerateArray().ToArray();
        var contract = contracts.Single(x => Id(x) == fixture.SignedId);
        Fields(contract).Should().BeEquivalentTo("id", "contractNumber", "vendorId", "vendorName", "paymentMilestones");
        contract.GetProperty("contractNumber").GetString().Should().Be(fixture.Number);
        contract.GetProperty("vendorId").GetInt32().Should().Be(fixture.VendorId);
        contract.GetProperty("vendorName").GetString().Should().Be("Supplier with private details");
        contracts.Select(Id).Should().Contain(fixture.OtherEligibleIds);
        contracts.Select(Id).Should().NotIntersectWith(fixture.ExcludedIds);
        var milestones = contract.GetProperty("paymentMilestones").EnumerateArray().ToArray();
        milestones.Select(x => x.GetProperty("order").GetInt32()).Should().Equal(1, 2);
        milestones.Select(x => x.GetProperty("name").GetString()).Should().Equal("Advance", "Inspected delivery");
        foreach (var milestone in milestones) Fields(milestone).Should().BeEquivalentTo("id", "order", "name");

        var vendors = references.GetProperty("vendors").EnumerateArray().ToArray();
        vendors.Select(Id).Should().BeEquivalentTo(contracts.Select(x => x.GetProperty("vendorId").GetInt32()).Distinct());
        vendors.Select(Id).Should().NotContain(fixture.UnrelatedVendorId);
        foreach (var vendor in vendors) Fields(vendor).Should().BeEquivalentTo("id", "vendorCode", "companyName");
        var accountants = references.GetProperty("accountants").EnumerateArray().ToArray();
        accountants.Select(Id).Should().Contain(fixture.AccountantId).And.NotContain(fixture.InactiveAccountantId).And.NotContain(fixture.ProcurementId);
        foreach (var accountant in accountants) Fields(accountant).Should().BeEquivalentTo("id", "fullName");
        var allowedAccountants = await WithDbAsync(db => db.Users.Where(x => x.IsActive && x.RoleEntity != null && x.RoleEntity.Code == "ACCOUNTANT").Select(x => x.Id).ToListAsync());
        accountants.Select(Id).Should().BeEquivalentTo(allowedAccountants);

        // This narrow financial lookup must not grant general contract access,
        // customer data, negotiated values, or vendor/user contact details.
        using var generalList = await Client.GetAsync($"/api/contracts?search={Uri.EscapeDataString(fixture.Number)}");
        generalList.EnsureSuccessStatusCode();
        (await ReadJsonAsync(generalList)).GetProperty("items").EnumerateArray().Should().BeEmpty();
        using var generalDetail = await Client.GetAsync($"/api/contracts/{fixture.SignedId}");
        generalDetail.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("SALE", HttpStatusCode.Forbidden)]
    [InlineData("BGD", HttpStatusCode.Forbidden)]
    [InlineData("PROCUREMENT", HttpStatusCode.Forbidden)]
    public async Task References_RequirePaymentManagementPermission(string? role, HttpStatusCode expected)
    {
        if (role is not null) await LoginAsync(role);
        using var response = await Client.GetAsync("/api/finance/payment-references");
        response.StatusCode.Should().Be(expected);
    }

    private sealed record Fixture(int SignedId, string Number, int VendorId, int UnrelatedVendorId, int AccountantId, int InactiveAccountantId, int ProcurementId, int[] ExcludedIds, int[] OtherEligibleIds);

    private Task<Fixture> SeedAsync() => WithDbAsync(async db =>
    {
        var procurement = await db.Users.SingleAsync(x => x.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PROCUREMENT"]);
        var accountant = await db.Users.SingleAsync(x => x.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["ACCOUNTANT"]);
        var inactive = new ApplicationUser
        {
            FullName = "Inactive accountant",
            PhoneNumber = $"091{Random.Shared.Next(1000000, 9999999)}",
            Email = $"inactive-{Guid.NewGuid():N}@example.test",
            PasswordHash = "not-used-for-login",
            RoleEntityId = accountant.RoleEntityId,
            IsActive = false,
        };
        var customer = new Customer { Name = "Private customer information", Type = CustomerType.Individual, SourceCode = "referral" };
        var vendor = new Vendor
        {
            VendorCode = UniqueSlug("NCC-REF"),
            CompanyName = "Supplier with private details",
            VendorType = VendorType.Supplier,
            Email = "private.supplier@example.test",
            Phone = "0912345678",
            IsActive = true,
        };
        var unrelatedVendor = new Vendor { VendorCode = UniqueSlug("NCC-EXCLUDE"), CompanyName = "Only draft contracts", VendorType = VendorType.Supplier, IsActive = true };
        db.AddRange(customer, vendor, unrelatedVendor, inactive);
        await db.SaveChangesAsync();
        var signed = new Contract
        {
            ContractNumber = UniqueSlug("PO-REF"),
            CustomerId = customer.Id,
            VendorId = vendor.Id,
            OwnerUserId = procurement.Id,
            Direction = ContractDirection.Downstream,
            Type = ContractType.Supply,
            Status = ContractStatus.Signed,
            SignedDate = DateTime.UtcNow,
            Value = 123456m,
            Note = "Private commercial notes",
        };
        var excluded = new[]
        {
            new Contract { ContractNumber = UniqueSlug("PO-DRAFT"), CustomerId = customer.Id, VendorId = unrelatedVendor.Id, Direction = ContractDirection.Downstream, Status = ContractStatus.Draft },
            new Contract { ContractNumber = UniqueSlug("PO-CANCEL"), CustomerId = customer.Id, VendorId = unrelatedVendor.Id, Direction = ContractDirection.Downstream, Status = ContractStatus.Cancelled },
            new Contract { ContractNumber = UniqueSlug("PO-UPSTREAM"), CustomerId = customer.Id, VendorId = unrelatedVendor.Id, Direction = ContractDirection.Upstream, Status = ContractStatus.Signed },
            new Contract { ContractNumber = UniqueSlug("PO-NOVENDOR"), CustomerId = customer.Id, Direction = ContractDirection.Downstream, Status = ContractStatus.Signed },
        };
        var otherEligible = new[] { ContractStatus.InProgress, ContractStatus.OnHold, ContractStatus.Completed }.Select(status => new Contract
        {
            ContractNumber = UniqueSlug("PO-ELIGIBLE"),
            CustomerId = customer.Id,
            VendorId = vendor.Id,
            OwnerUserId = procurement.Id,
            Direction = ContractDirection.Downstream,
            Type = ContractType.Supply,
            Status = status,
        }).ToArray();
        db.Contracts.Add(signed);
        db.Contracts.AddRange(excluded);
        db.Contracts.AddRange(otherEligible);
        db.ContractPaymentMilestones.AddRange(
            new ContractPaymentMilestone { Contract = signed, Order = 2, Name = "Inspected delivery", PercentValue = 70 },
            new ContractPaymentMilestone { Contract = signed, Order = 1, Name = "Advance", PercentValue = 30 });
        await db.SaveChangesAsync();
        return new Fixture(signed.Id, signed.ContractNumber, vendor.Id, unrelatedVendor.Id, accountant.Id, inactive.Id, procurement.Id,
            excluded.Select(x => x.Id).ToArray(), otherEligible.Select(x => x.Id).ToArray());
    });

    private static int Id(JsonElement element) => element.GetProperty("id").GetInt32();
    private static IEnumerable<string> Fields(JsonElement element) => element.EnumerateObject().Select(x => x.Name);
    private async Task LoginAsync(string role)
    {
        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));
    }
}
