using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

public sealed class ConstructionProcurementModelTests
{
    [Fact]
    public void SupplyLinks_RetainIndependentRootsAndHistoryBelongsToRequest()
    {
        using var db = DbContextFactory.Create();
        var request = db.Model.FindEntityType(typeof(ConstructionResourceRequest))!;
        foreach (var name in new[] { nameof(ConstructionResourceRequest.ProcurementContractId), nameof(ConstructionResourceRequest.SupplyDocumentId) })
        {
            var property = request.FindProperty(name)!;
            Assert.True(property.IsNullable);
            var foreignKey = Assert.Single(request.GetForeignKeys().Where(x => x.Properties.Contains(property)));
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        }
        var history = db.Model.FindEntityType(typeof(ConstructionResourceHandoffEvent))!;
        var requestLink = Assert.Single(history.GetForeignKeys().Where(x => x.PrincipalEntityType == request));
        Assert.Equal(DeleteBehavior.Cascade, requestLink.DeleteBehavior);
    }
}
