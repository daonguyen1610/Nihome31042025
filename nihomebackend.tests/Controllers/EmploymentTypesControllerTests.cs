using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Controllers;

public class EmploymentTypesControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly EmploymentTypeService _service;
    private readonly EmploymentTypesController _sut;

    public EmploymentTypesControllerTests()
    {
        _db = DbContextFactory.Create();
        _service = new EmploymentTypeService(_db);
        _sut = new EmploymentTypesController(_service);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAll_ReturnsOk_WithEmploymentTypeList()
    {
        _db.EmploymentTypes.Add(new EmploymentType { Code = "full-time", Name = "Toàn thời gian", IsActive = true, SortOrder = 1 });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAll(includeInactive: true);

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsType<List<EmploymentTypeResponse>>(ok.Value);
        Assert.Single(items);
        Assert.Equal("full-time", items[0].Code);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction_WhenRequestIsValid()
    {
        var result = await _sut.Create(new UpsertEmploymentTypeRequest
        {
            Code = "contract",
            Name = "Hợp đồng",
            IsActive = true,
            SortOrder = 10,
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<EmploymentTypeResponse>(created.Value);
        Assert.Equal("contract", response.Code);
    }

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenEmploymentTypeIsInUse()
    {
        var type = new EmploymentType { Code = "intern", Name = "Thực tập", IsActive = true, SortOrder = 1 };
        _db.EmploymentTypes.Add(type);
        _db.JobPositions.Add(new JobPosition
        {
            Title = "Intern",
            Department = "Engineering",
            Location = "HCM",
            EmploymentType = "intern",
            ExperienceLevel = "student",
            IsActive = true,
            SortOrder = 1,
        });
        await _db.SaveChangesAsync();

        var result = await _sut.Delete(type.Id);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
