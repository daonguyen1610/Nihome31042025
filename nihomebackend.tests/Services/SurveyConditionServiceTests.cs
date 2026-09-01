using System.Text;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class SurveyConditionServiceTests : IDisposable
{
    private readonly NihomeBackend.Data.AppDbContext db = DbContextFactory.Create();
    private readonly SurveyConditionService service;
    private readonly Survey survey;

    public SurveyConditionServiceTests()
    {
        service = new SurveyConditionService(db, new Utf8CsvParser());
        var customer = new Customer { Name = "Condition customer", Type = CustomerType.Company };
        db.Customers.Add(customer);
        db.SaveChanges();
        var project = new OperationalProject
        {
            Code = "OP-CONDITIONS",
            Name = "Condition project",
            CustomerId = customer.Id,
        };
        db.OperationalProjects.Add(project);
        db.MasterDataOptions.AddRange(
            Infrastructure("electricity", 1),
            Infrastructure("water-supply", 2),
            Infrastructure("drainage", 3),
            Infrastructure("telecom", 4),
            Infrastructure("road-access", 5));
        db.SaveChanges();
        survey = new Survey
        {
            Code = "SV-CONDITIONS",
            Location = "Condition site",
            SurveyDate = DateTime.UtcNow,
            OperationalProjectId = project.Id,
        };
        db.Surveys.Add(survey);
        db.SaveChanges();
    }

    [Fact]
    public async Task ImportAsync_InvalidRow_PreservesExistingConditionsAtomically()
    {
        db.SurveySiteConditions.Add(new SurveySiteCondition
        {
            SurveyId = survey.Id,
            Category = SurveySiteConditionCategory.RightOfWay,
            Code = "access-width",
            Status = SurveySiteConditionStatus.Available,
            NumericValue = 4,
            UnitCode = "m",
        });
        await db.SaveChangesAsync();
        var csv = Csv(
            "RightOfWay,access-width,Available,6,yard,,,",
            "Elevation,site-elevation,Unknown,,m,,,");

        var result = await service.ImportAsync(survey.Id, csv, 7);

        Assert.NotNull(result);
        Assert.NotEmpty(result!.Errors);
        var persisted = Assert.Single(await db.SurveySiteConditions.AsNoTracking().ToListAsync());
        Assert.Equal(4, persisted.NumericValue);
    }

    [Fact]
    public async Task ImportAsync_ValidCsv_ReplacesAndReturnsStructuredConditions()
    {
        var csv = Csv(
            "RightOfWay,access-width,Available,6.5,m,,Truck access,",
            "Elevation,site-elevation,NeedsInvestigation,,m,,Survey benchmark required,",
            "Infrastructure,electricity,Available,,,electricity,Grid at boundary,");

        var result = await service.ImportAsync(survey.Id, csv, 7);

        Assert.NotNull(result);
        Assert.Empty(result!.Errors);
        Assert.Equal(3, result.Conditions.Count);
        Assert.Contains(result.Conditions, condition =>
            condition.Code == "access-width" && condition.NumericValue == 6.5m && condition.UnitCode == "m");
        Assert.Equal(3, await db.SurveySiteConditions.CountAsync());
    }

    [Fact]
    public async Task ReplaceAsync_ValidJsonShape_ReplacesExistingRows()
    {
        var conditions = await service.ReplaceAsync(survey.Id,
        [
            RequiredMeasurement("RightOfWay", "access-width", 5),
            RequiredMeasurement("Elevation", "site-elevation", 1.25m),
            new SurveySiteConditionRequest
            {
                Category = "Infrastructure",
                Code = "road-access",
                StatusCode = "Unavailable",
                ReferenceCode = "road-access",
                Description = "Chưa có đường xe tải vào công trường",
            },
        ], 8);

        Assert.NotNull(conditions);
        Assert.Equal(3, conditions!.Count);
        Assert.Contains(conditions, condition => condition.StatusCode == "Unavailable");
    }

    [Fact]
    public async Task ReplaceAsync_ValueExceedingStoragePrecision_PreservesExistingRows()
    {
        db.SurveySiteConditions.Add(new SurveySiteCondition
        {
            SurveyId = survey.Id,
            Category = SurveySiteConditionCategory.RightOfWay,
            Code = "access-width",
            Status = SurveySiteConditionStatus.Available,
            NumericValue = 4,
            UnitCode = "m",
        });
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<SurveyOperationException>(() => service.ReplaceAsync(survey.Id,
        [
            RequiredMeasurement("RightOfWay", "access-width", 1.1234567m),
            RequiredMeasurement("Elevation", "site-elevation", 1.25m),
        ], 8));

        Assert.Contains("6 chữ số thập phân", exception.Message);
        var persisted = Assert.Single(await db.SurveySiteConditions.AsNoTracking().ToListAsync());
        Assert.Equal(4, persisted.NumericValue);
    }

    [Fact]
    public async Task ImportAsync_ZeroOperationalProject_IsBlocked()
    {
        survey.OperationalProjectId = 0;
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<SurveyOperationException>(() =>
            service.ImportAsync(survey.Id, Csv(
                "RightOfWay,access-width,Unknown,,m,, ,",
                "Elevation,site-elevation,Unknown,,m,, ,"), 7));

        Assert.Contains("mã số lớn hơn 0", exception.Message);
        Assert.Empty(await db.SurveySiteConditions.ToListAsync());
    }

    public void Dispose() => db.Dispose();

    private static MasterDataOption Infrastructure(string code, int sortOrder) => new()
    {
        Category = SurveyConditionService.InfrastructureTypeCategory,
        Code = code,
        Name = code,
        IsActive = true,
        SortOrder = sortOrder,
    };

    private static SurveySiteConditionRequest RequiredMeasurement(string category, string code, decimal value) => new()
    {
        Category = category,
        Code = code,
        StatusCode = "Available",
        NumericValue = value,
        UnitCode = "m",
    };

    private static MemoryStream Csv(params string[] rows)
    {
        var content = string.Join(',', SurveyConditionService.CsvHeaders) + "\r\n" + string.Join("\r\n", rows) + "\r\n";
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }
}
