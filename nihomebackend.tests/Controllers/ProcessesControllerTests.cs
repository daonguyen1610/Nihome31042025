using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Controllers;

public class ProcessesControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ProcessesController _sut;

    public ProcessesControllerTests()
    {
        _db = DbContextFactory.Create();
        var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == "/tmp");
        var service = new ProcessService(_db, env);
        _sut = new ProcessesController(service, env, NullLogger<ProcessesController>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAll_ReturnsEmptyList_WhenNoProcesses()
    {
        var result = await _sut.GetAll();
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task GetAll_ReturnsProcesses_WhenDataExists()
    {
        var process = new ProcessDocument
        {
            Title = "Design Phase",
            GroupKey = "development",
            Code = "P001",
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.ProcessDocuments.Add(process);
        await _db.SaveChangesAsync();

        var result = await _sut.GetAll();
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task Create_ReturnsCreated_WithValidRequest()
    {
        var request = new UpsertProcessRequest
        {
            Title = "New Process",
            GroupKey = "process",
            Code = "P002",
            SortOrder = 1
        };

        var result = await _sut.Create(request);
        var createdResult = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<ProcessResponse>(createdResult.Value);
        Assert.Equal("New Process", response.Title);
    }

    [Fact]
    public async Task Create_SavesProcessToDatabase()
    {
        var request = new UpsertProcessRequest
        {
            Title = "DB Process",
            GroupKey = "test",
            Code = "P003",
            SortOrder = 1
        };

        await _sut.Create(request);
        var saved = _db.ProcessDocuments.FirstOrDefault(p => p.Title == "DB Process");
        Assert.NotNull(saved);
        Assert.Equal("test", saved.GroupKey);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenProcessExists()
    {
        var process = new ProcessDocument
        {
            Title = "Old Process",
            GroupKey = "old",
            Code = "OLD001",
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.ProcessDocuments.Add(process);
        await _db.SaveChangesAsync();

        var updateRequest = new UpsertProcessRequest
        {
            Title = "Updated Process",
            GroupKey = "new",
            Code = "NEW001",
            SortOrder = 2
        };

        var result = await _sut.Update(process.Id, updateRequest);
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ProcessResponse>(ok.Value);
        Assert.Equal("Updated Process", response.Title);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenProcessDoesNotExist()
    {
        var updateRequest = new UpsertProcessRequest
        {
            Title = "Test",
            GroupKey = "test",
            Code = "T001",
            SortOrder = 1
        };

        var result = await _sut.Update(999, updateRequest);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenProcessExists()
    {
        var process = new ProcessDocument
        {
            Title = "Delete Process",
            GroupKey = "delete",
            Code = "D001",
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.ProcessDocuments.Add(process);
        await _db.SaveChangesAsync();
        int id = process.Id;

        var result = await _sut.Delete(id);
        Assert.IsType<NoContentResult>(result);
        var deleted = _db.ProcessDocuments.FirstOrDefault(p => p.Id == id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenProcessDoesNotExist()
    {
        var result = await _sut.Delete(999);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Create_MultipleProcesses_InCorrectOrder()
    {
        var process1 = new UpsertProcessRequest
        {
            Title = "Step 1",
            GroupKey = "steps",
            Code = "S001",
            SortOrder = 1
        };

        var process2 = new UpsertProcessRequest
        {
            Title = "Step 2",
            GroupKey = "steps",
            Code = "S002",
            SortOrder = 2
        };

        await _sut.Create(process1);
        await _sut.Create(process2);

        var allProcesses = _db.ProcessDocuments.OrderBy(p => p.SortOrder).ToList();
        Assert.Equal(2, allProcesses.Count);
        Assert.Equal("Step 1", allProcesses[0].Title);
        Assert.Equal("Step 2", allProcesses[1].Title);
    }
}
