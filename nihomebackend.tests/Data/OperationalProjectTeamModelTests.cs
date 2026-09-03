using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

public sealed class OperationalProjectTeamModelTests
{
    [Theory]
    [InlineData(typeof(OperationalProjectMember))]
    [InlineData(typeof(OperationalProjectAssignment))]
    [InlineData(typeof(OperationalProjectTeamHistory))]
    public void OperationalProjectForeignKey_RestrictsParentDeletion(Type entityType)
    {
        using var db = DbContextFactory.Create();

        var foreignKey = db.Model.FindEntityType(entityType)!
            .GetForeignKeys()
            .Single(key => key.PrincipalEntityType.ClrType == typeof(OperationalProject));

        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }
}