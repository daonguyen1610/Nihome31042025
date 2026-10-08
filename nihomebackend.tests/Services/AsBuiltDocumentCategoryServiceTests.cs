using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class AsBuiltDocumentCategoryServiceTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAllAsync_DoesNotSeedDefaults_WhenTableIsEmpty()
    {
        var service = new AsBuiltDocumentCategoryService(
            _db, NullLogger<AsBuiltDocumentCategoryService>.Instance);

        var result = await service.GetAllAsync(includeInactive: true);

        Assert.Empty(result);
        Assert.Empty(_db.AsBuiltDocumentCategories);
    }
}
