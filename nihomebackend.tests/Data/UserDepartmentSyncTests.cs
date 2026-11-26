using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

/// <summary>AppDbContext keeps a person's department in step with a role that belongs to one.</summary>
public sealed class UserDepartmentSyncTests
{
    [Fact]
    public async Task Department_follows_a_department_role_and_stays_for_a_role_without_one()
    {
        await using var db = DbContextFactory.Create();
        var sales = new RoleGroup { Code = "CRM_T", LabelKey = "g.crm" };
        var design = new RoleGroup { Code = "DESIGN_T", LabelKey = "g.design" };
        db.RoleGroups.AddRange(sales, design);
        await db.SaveChangesAsync();
        var sale = new Role { Code = "SALE_T", Name = "Sale", RoleGroupId = sales.Id };
        var architect = new Role { Code = "ARCH_T", Name = "Architect", RoleGroupId = design.Id };
        var head = new Role { Code = "HEAD_T", Name = "Head" };
        db.Roles.AddRange(sale, architect, head);
        await db.SaveChangesAsync();

        var user = new ApplicationUser { PhoneNumber = "0900000001", Email = "a@nicon.test", RoleEntityId = sale.Id };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        Assert.Equal(sales.Id, user.DepartmentRoleGroupId);

        user.RoleEntityId = architect.Id;
        await db.SaveChangesAsync();
        Assert.Equal(design.Id, user.DepartmentRoleGroupId);

        // A role without a department (e.g. department head) keeps the department chosen for the person.
        user.RoleEntityId = head.Id;
        user.DepartmentRoleGroupId = sales.Id;
        await db.SaveChangesAsync();
        Assert.Equal(sales.Id, (await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).DepartmentRoleGroupId);
    }

    [Fact]
    public async Task Leaving_a_department_role_for_a_role_without_one_clears_the_department()
    {
        await using var db = DbContextFactory.Create();
        var sales = new RoleGroup { Code = "CRM_T", LabelKey = "g.crm" };
        var manager = new Role { Code = "SALES_MANAGER_T", Name = "Sales manager", RoleGroup = sales };
        var admin = new Role { Code = "ADMIN_T", Name = "Admin" };
        db.Roles.AddRange(manager, admin);
        var user = new ApplicationUser { PhoneNumber = "0900000003", Email = "c@nicon.test", RoleEntity = manager };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Keeping the old department would let this person keep heading Sales.
        user.RoleEntityId = admin.Id;
        await db.SaveChangesAsync();

        Assert.Null((await db.Users.AsNoTracking().SingleAsync()).DepartmentRoleGroupId);
    }

    [Fact]
    public async Task Taking_a_role_out_of_every_department_clears_its_holders()
    {
        await using var db = DbContextFactory.Create();
        var sales = new RoleGroup { Code = "CRM_T", LabelKey = "g.crm" };
        var role = new Role { Code = "SALE_T", Name = "Sale", RoleGroup = sales };
        db.Users.Add(new ApplicationUser { PhoneNumber = "0900000004", Email = "d@nicon.test", RoleEntity = role });
        await db.SaveChangesAsync();

        role.RoleGroupId = null;
        await db.SaveChangesAsync();

        Assert.Null((await db.Users.AsNoTracking().SingleAsync()).DepartmentRoleGroupId);
    }

    [Fact]
    public async Task Moving_a_role_to_another_department_moves_its_holders()
    {
        await using var db = DbContextFactory.Create();
        var sales = new RoleGroup { Code = "CRM_T", LabelKey = "g.crm" };
        var design = new RoleGroup { Code = "DESIGN_T", LabelKey = "g.design" };
        db.RoleGroups.AddRange(sales, design);
        var role = new Role { Code = "SALE_T", Name = "Sale", RoleGroup = sales };
        db.Roles.Add(role);
        db.Users.Add(new ApplicationUser { PhoneNumber = "0900000002", Email = "b@nicon.test", RoleEntity = role });
        await db.SaveChangesAsync();

        role.RoleGroupId = design.Id;
        await db.SaveChangesAsync();

        Assert.Equal(design.Id, (await db.Users.AsNoTracking().SingleAsync()).DepartmentRoleGroupId);
    }
}
