using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class ConceptFinalizationPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompetingFinalizations_RejectStaleWriterAndRollBackItsChanges(bool discardSibling)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var setup = new RelationalContext(options);
        await setup.Database.EnsureCreatedAsync();
        var first = new ConceptOption { DesignProjectId = 101, Name = "Phương án sân trong", Status = ConceptOptionStatus.PresentedToClient };
        var second = new ConceptOption { DesignProjectId = 101, Name = "Phương án thông tầng", Status = ConceptOptionStatus.PresentedToClient };
        var independent = new ConceptOption { DesignProjectId = 102, Name = "Dự án độc lập" };
        setup.AddRange(first, second, independent);
        await setup.SaveChangesAsync();

        await using var writerA = new RelationalContext(options);
        await using var writerB = new RelationalContext(options);
        // Both requests read before either commits: the service-level Any check
        // alone cannot protect this interleaving.
        (await writerA.ConceptOptions.AnyAsync(item => item.DesignProjectId == 101 && item.Status == ConceptOptionStatus.Finalized)).Should().BeFalse();
        (await writerB.ConceptOptions.AnyAsync(item => item.DesignProjectId == 101 && item.Status == ConceptOptionStatus.Finalized)).Should().BeFalse();
        var optionA = await writerA.ConceptOptions.SingleAsync(item => item.Id == first.Id);
        var optionB = await writerB.ConceptOptions.SingleAsync(item => item.Id == second.Id);
        var staleSibling = await writerB.ConceptOptions.SingleAsync(item => item.Id == first.Id);
        var unrelated = await writerB.ConceptOptions.SingleAsync(item => item.Id == independent.Id);
        optionA.Status = ConceptOptionStatus.Finalized;
        await writerA.SaveChangesAsync();
        optionB.Status = ConceptOptionStatus.Finalized;
        if (discardSibling) staleSibling.Status = ConceptOptionStatus.Discarded;
        unrelated.Name = "Must roll back";
        Func<Task> staleSave = () => writerB.SaveChangesAsync();
        await staleSave.Should().ThrowAsync<DbUpdateException>();

        setup.ChangeTracker.Clear();
        (await setup.ConceptOptions.CountAsync(item => item.DesignProjectId == 101 && item.Status == ConceptOptionStatus.Finalized)).Should().Be(1);
        (await setup.ConceptOptions.SingleAsync(item => item.Id == second.Id)).Status.Should().Be(ConceptOptionStatus.PresentedToClient);
        (await setup.ConceptOptions.SingleAsync(item => item.Id == independent.Id)).Name.Should().Be("Dự án độc lập");
    }

    private sealed class RelationalContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
            {
                property.SetColumnType(null);
                if (property.IsConcurrencyToken && property.ClrType == typeof(byte[]))
                    property.ValueGenerated = ValueGenerated.Never;
            }
        }
    }
}
