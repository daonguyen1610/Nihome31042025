using System.Net;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class PeopleDirectoryControllerTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData("create")]
    [InlineData("update-profile")]
    [InlineData("change-role")]
    [InlineData("deactivate")]
    [InlineData("reactivate")]
    public async Task UserWriteResponse_RetainsRoleAndDepartmentIdentity(string operation)
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var phone = $"098{Random.Shared.Next(1_000_000, 9_999_999)}";
        var email = $"nicon-finance-{Guid.NewGuid():N}@example.com";
        var id = operation == "create" ? 0 : await WithDbAsync(async db =>
        {
            var role = await db.Roles.SingleAsync(item => item.Code == "ACCOUNTANT");
            var user = new ApplicationUser
            {
                PhoneNumber = phone,
                Email = email,
                FullName = "NICON Finance – Nguyễn Minh Anh",
                RoleEntityId = role.Id,
                Role = UserRole.USER,
                IsActive = operation != "reactivate",
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        });
        using var response = operation switch
        {
            "create" => await Client.PostAsJsonAsync("/api/users", new
            {
                phoneNumber = phone,
                email,
                fullName = "NICON Finance – Nguyễn Minh Anh",
                password = "P@ssword1",
                role = "ACCOUNTANT",
            }),
            "update-profile" => await Client.PutAsJsonAsync($"/api/users/{id}", new { fullName = "NICON Finance – Nguyễn Minh Anh cập nhật" }),
            "change-role" => await Client.PutAsJsonAsync($"/api/users/{id}", new { role = "PROCUREMENT" }),
            _ => await Client.PatchAsync($"/api/users/{id}/toggle-active", null),
        };
        response.StatusCode.Should().Be(operation == "create" ? HttpStatusCode.Created : HttpStatusCode.OK);
        var written = await ReadJsonAsync(response);
        var expectedRole = operation == "change-role" ? "PROCUREMENT" : "ACCOUNTANT";
        var expected = await WithDbAsync(db => db.Roles.Where(role => role.Code == expectedRole)
            .Select(role => new { role.RoleGroupId, role.RoleGroup!.LabelKey }).SingleAsync());
        expected.RoleGroupId.Should().NotBeNull();
        written.GetProperty("role").GetString().Should().Be(expectedRole);
        written.GetProperty("departmentId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Number);
        written.GetProperty("departmentId").GetInt32().Should().Be(expected.RoleGroupId!.Value);
        written.GetProperty("departmentLabelKey").GetString().Should().Be(expected.LabelKey);
        written.GetProperty("isActive").GetBoolean().Should().Be(operation != "deactivate");
        var persisted = await ReadJsonAsync(await Client.GetAsync($"/api/users/{written.GetProperty("id").GetInt32()}"));
        foreach (var field in new[] { "role", "roleName", "roleLabelKey", "departmentId", "departmentLabelKey", "isActive" })
            written.GetProperty(field).GetRawText().Should().Be(persisted.GetProperty(field).GetRawText(), $"write and GET must agree on {field}");
    }

    [Fact]
    public async Task OpportunityOwners_ExposeRoleAndDepartmentWithinExistingSalesScope()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));
        var response = await Client.GetAsync("/api/opportunities/owner-options");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await ReadJsonAsync(response)).EnumerateArray().ToList();
        items.Should().NotBeEmpty();
        foreach (var item in items)
        {
            item.GetProperty("roleName").GetString().Should().NotBeNullOrWhiteSpace();
            var userId = item.GetProperty("id").GetInt32();
            var expectedDepartment = await WithDbAsync(db => db.Users.Where(user => user.Id == userId)
                .Select(user => user.RoleEntity != null && user.RoleEntity.RoleGroup != null && user.RoleEntity.RoleGroup.IsActive
                    ? user.RoleEntity.RoleGroup.LabelKey : null).SingleAsync());
            item.GetProperty("departmentLabelKey").GetString().Should().Be(expectedDepartment);
        }
        items.Should().Contain(item => item.GetProperty("departmentId").ValueKind == System.Text.Json.JsonValueKind.Number);
    }

    [Fact]
    public async Task DepartmentFilter_ReturnsOnlyMatchingPeopleWithRoleIdentity()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var group = await WithDbAsync(db => db.RoleGroups.SingleAsync(item => item.Code == "PROCUREMENT"));
        var expected = await WithDbAsync(db => db.Users.Where(user => user.RoleEntity != null &&
            user.RoleEntity.RoleGroupId == group.Id).Select(user => user.Id).ToListAsync());
        expected.Should().NotBeEmpty();
        var response = await Client.GetAsync($"/api/users?departmentId={group.Id}&take=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("total").GetInt32().Should().Be(expected.Count);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("id").GetInt32()).Should().BeEquivalentTo(expected);
        foreach (var item in items)
        {
            item.GetProperty("departmentId").GetInt32().Should().Be(group.Id);
            item.GetProperty("departmentLabelKey").GetString().Should().Be(group.LabelKey);
            item.GetProperty("roleName").GetString().Should().NotBeNullOrWhiteSpace();
        }
        var invalid = await Client.GetAsync("/api/users?departmentId=-1");
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var invalidBody = await ReadJsonAsync(invalid);
        invalidBody.GetProperty("code").GetString().Should().Be("PEOPLE_DEPARTMENT_FILTER_INVALID");
        invalidBody.GetProperty("field").GetString().Should().Be("departmentId");
        invalidBody.GetProperty("message").GetString().Should().Contain("Chọn một nhóm");
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("SALES_MANAGER", HttpStatusCode.Forbidden)]
    public async Task DepartmentFilter_PreservesDirectoryAuthorization(string? role, HttpStatusCode expected)
    {
        if (role != null)
            await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));
        (await Client.GetAsync("/api/users?departmentId=1")).StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task DepartmentFilter_IntersectsRoleSearchAndPagination()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var groupId = await WithDbAsync(db => db.RoleGroups.Where(group => group.Code == "PROCUREMENT").Select(group => group.Id).SingleAsync());
        var expected = await WithDbAsync(db => db.Users.Where(user => user.RoleEntity != null && user.RoleEntity.RoleGroupId == groupId)
            .OrderByDescending(user => user.Id).Select(user => new { user.Id, user.PhoneNumber, Role = user.RoleEntity!.Code }).ToListAsync());
        expected.Count.Should().BeGreaterThan(1);
        var page = await ReadJsonAsync(await Client.GetAsync($"/api/users?departmentId={groupId}&take=1&skip=1"));
        page.GetProperty("total").GetInt32().Should().Be(expected.Count);
        page.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetInt32().Should().Be(expected[1].Id);
        var person = expected[0];
        var search = await ReadJsonAsync(await Client.GetAsync($"/api/users?departmentId={groupId}&role={person.Role}&search={person.PhoneNumber}"));
        search.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetInt32().Should().Be(person.Id);
        var empty = await ReadJsonAsync(await Client.GetAsync("/api/users?departmentId=2147483647"));
        empty.GetProperty("total").GetInt32().Should().Be(0);
        empty.GetProperty("items").GetArrayLength().Should().Be(0);
        var malformed = await Client.GetAsync("/api/users?departmentId=invalid");
        malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(malformed)).GetProperty("errors").TryGetProperty("departmentId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task InactiveDepartment_IsUnassignedWithoutChangingStoredRole()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var groupId = await WithDbAsync(db => db.RoleGroups.Where(group => group.Code == "PROCUREMENT").Select(group => group.Id).SingleAsync());
        try
        {
            await WithDbAsync(async db => { (await db.RoleGroups.FindAsync(groupId))!.IsActive = false; await db.SaveChangesAsync(); });
            var expectedIds = await WithDbAsync(db => db.Users.Where(user => user.RoleEntity != null && user.RoleEntity.RoleGroupId == groupId).Select(user => user.Id).ToListAsync());
            expectedIds.Should().NotBeEmpty();
            var body = await ReadJsonAsync(await Client.GetAsync("/api/users?departmentId=0&take=100"));
            var items = body.GetProperty("items").EnumerateArray().Where(item => expectedIds.Contains(item.GetProperty("id").GetInt32())).ToList();
            items.Select(item => item.GetProperty("id").GetInt32()).Should().BeEquivalentTo(expectedIds);
            foreach (var item in items)
            {
                item.GetProperty("departmentId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
                item.GetProperty("departmentLabelKey").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
                item.GetProperty("roleName").GetString().Should().NotBeNullOrWhiteSpace();
            }
            var groups = (await ReadJsonAsync(await Client.GetAsync("/api/admin/rbac/role-groups"))).EnumerateArray();
            groups.Should().NotContain(item => item.GetProperty("id").GetInt32() == groupId);
            var excluded = await ReadJsonAsync(await Client.GetAsync($"/api/users?departmentId={groupId}"));
            excluded.GetProperty("total").GetInt32().Should().Be(0);
        }
        finally
        {
            await WithDbAsync(async db => { (await db.RoleGroups.FindAsync(groupId))!.IsActive = true; await db.SaveChangesAsync(); });
        }
    }
}
