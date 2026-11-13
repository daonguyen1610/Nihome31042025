using Microsoft.EntityFrameworkCore;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class AggregateDeletionServiceTests : IDisposable
{
    private readonly AppDbContext db = DbContextFactory.Create();
    private readonly Mock<IProjectDocumentStagingService> projectDocuments = new();

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task DeleteOperationalProjectAsync_RemovesConstructionPreparations()
    {
        var project = new OperationalProject { Code = "OP-1", Name = "Project", CustomerId = 1 };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        var first = new ConstructionPreparation { OperationalProjectId = project.Id, Version = 1 };
        db.ConstructionPreparations.Add(first);
        await db.SaveChangesAsync();
        db.ConstructionPreparations.Add(new ConstructionPreparation
        {
            OperationalProjectId = project.Id,
            Version = 2,
            SupersedesPreparationId = first.Id,
        });
        await db.SaveChangesAsync();

        await AggregateDeletionService.DeleteOperationalProjectAsync(
            db, project, projectDocuments.Object, null, CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.False(await db.OperationalProjects.AnyAsync(item => item.Id == project.Id));
        Assert.False(await db.ConstructionPreparations.AnyAsync(
            item => item.OperationalProjectId == project.Id));
    }
}
