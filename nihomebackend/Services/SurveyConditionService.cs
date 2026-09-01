using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface ISurveyConditionService
{
    Task<SurveySiteConditionImportResponse?> ImportAsync(
        int surveyId, Stream stream, int userId, CancellationToken ct = default);
    Task<List<SurveySiteConditionResponse>?> ReplaceAsync(
        int surveyId, IReadOnlyCollection<SurveySiteConditionRequest> conditions,
        int userId, CancellationToken ct = default);
}

public sealed partial class SurveyConditionService(AppDbContext db, IUtf8CsvParser csvParser)
    : ISurveyConditionService
{
    public const string InfrastructureTypeCategory = "survey_infrastructure_type";
    public static readonly IReadOnlyList<string> CsvHeaders =
    [
        "Category", "Code", "StatusCode", "NumericValue", "UnitCode",
        "ReferenceCode", "Description", "Note",
    ];

    private static readonly HashSet<string> UnitCodes =
        new(["m", "mm", "cm", "km", "m2", "m3", "percent"], StringComparer.Ordinal);
    private const decimal MaximumNumericValue = 999_999_999_999.999999m;

    public static byte[] CreateTemplate()
    {
        const string body =
            "Category,Code,StatusCode,NumericValue,UnitCode,ReferenceCode,Description,Note\r\n" +
            "RightOfWay,access-width,Unknown,,m,,,\r\n" +
            "Elevation,site-elevation,Unknown,,m,,,\r\n" +
            "Infrastructure,electricity,Unknown,,,electricity,,\r\n" +
            "Infrastructure,water-supply,Unknown,,,water-supply,,\r\n" +
            "Infrastructure,drainage,Unknown,,,drainage,,\r\n" +
            "Infrastructure,telecom,Unknown,,,telecom,,\r\n" +
            "Infrastructure,road-access,Unknown,,,road-access,,\r\n";
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(body)).ToArray();
    }

    public async Task<SurveySiteConditionImportResponse?> ImportAsync(
        int surveyId, Stream stream, int userId, CancellationToken ct = default)
    {
        var projectState = await GetProjectStateAsync(surveyId, ct);
        if (!projectState.Exists) return null;
        EnsureProjectAssigned(projectState.OperationalProjectId);

        var parsed = await csvParser.ParseAsync(stream, CsvHeaders, maxRows: 200, ct: ct);
        if (!parsed.IsValid)
        {
            return new SurveySiteConditionImportResponse { Errors = parsed.Errors };
        }

        var requests = new List<SurveySiteConditionRequest>();
        var errors = new List<CsvImportError>();
        for (var index = 0; index < parsed.Rows.Count; index++)
        {
            var row = parsed.Rows[index];
            decimal? numericValue = null;
            var rawValue = row["NumericValue"].Trim();
            var parsedValue = 0m;
            if (rawValue.Length > 0 &&
                !decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedValue))
            {
                errors.Add(new CsvImportError
                {
                    Row = index + 2,
                    Column = 4,
                    Message = $"NumericValue ở dòng {index + 2} phải là số theo định dạng dấu chấm, ví dụ 3.5.",
                });
            }
            else if (rawValue.Length > 0)
            {
                numericValue = parsedValue;
            }

            requests.Add(new SurveySiteConditionRequest
            {
                Category = row["Category"],
                Code = row["Code"],
                StatusCode = row["StatusCode"],
                NumericValue = numericValue,
                UnitCode = row["UnitCode"],
                ReferenceCode = row["ReferenceCode"],
                Description = row["Description"],
                Note = row["Note"],
            });
        }

        var validated = await ValidateAsync(requests, errors, includeRows: true, ct);
        if (errors.Count > 0)
        {
            return new SurveySiteConditionImportResponse { Errors = errors };
        }

        var conditions = await ReplaceValidatedAsync(surveyId, validated, userId, ct);
        return new SurveySiteConditionImportResponse { Conditions = conditions };
    }

    public async Task<List<SurveySiteConditionResponse>?> ReplaceAsync(
        int surveyId,
        IReadOnlyCollection<SurveySiteConditionRequest> conditions,
        int userId,
        CancellationToken ct = default)
    {
        var projectState = await GetProjectStateAsync(surveyId, ct);
        if (!projectState.Exists) return null;
        EnsureProjectAssigned(projectState.OperationalProjectId);
        var errors = new List<CsvImportError>();
        var validated = await ValidateAsync(conditions, errors, includeRows: false, ct);
        if (errors.Count > 0)
        {
            throw new SurveyOperationException(string.Join(" ", errors.Select(error => error.Message)));
        }
        return await ReplaceValidatedAsync(surveyId, validated, userId, ct);
    }

    private async Task<List<ValidatedCondition>> ValidateAsync(
        IReadOnlyCollection<SurveySiteConditionRequest> requests,
        List<CsvImportError> errors,
        bool includeRows,
        CancellationToken ct)
    {
        if (requests.Count == 0)
        {
            errors.Add(new CsvImportError { Message = "Danh sách điều kiện khảo sát phải có ít nhất một dòng." });
            return [];
        }

        var infrastructureCodeList = await db.MasterDataOptions.AsNoTracking()
            .Where(option => option.Category == InfrastructureTypeCategory && option.IsActive)
            .Select(option => option.Code)
            .ToListAsync(ct);
        var infrastructureCodes = new HashSet<string>(infrastructureCodeList, StringComparer.Ordinal);
        var result = new List<ValidatedCondition>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var request in requests)
        {
            var row = includeRows ? index + 2 : (int?)null;
            index++;
            var categoryText = request.Category.Trim();
            var code = request.Code.Trim();
            var statusText = request.StatusCode.Trim();
            var unitCode = Clean(request.UnitCode);
            var referenceCode = Clean(request.ReferenceCode);
            var description = Clean(request.Description);
            var note = Clean(request.Note);

            if (!Enum.TryParse<SurveySiteConditionCategory>(categoryText, true, out var category) ||
                !Enum.IsDefined(category))
            {
                AddError(errors, row, "Category chỉ nhận RightOfWay, Elevation hoặc Infrastructure.");
                continue;
            }
            if (code.Length is < 1 or > 80 || !StableCodeRegex().IsMatch(code))
            {
                AddError(errors, row, "Code phải gồm chữ thường, số và dấu gạch ngang, ví dụ access-width.");
            }
            if (!Enum.TryParse<SurveySiteConditionStatus>(statusText, true, out var status) ||
                !Enum.IsDefined(status))
            {
                AddError(errors, row, "StatusCode chỉ nhận Unknown, Available, Unavailable hoặc NeedsInvestigation.");
            }
            if (unitCode is not null && !UnitCodes.Contains(unitCode))
            {
                AddError(errors, row, $"UnitCode '{unitCode}' không hợp lệ. Dùng m, mm, cm, km, m2, m3 hoặc percent.");
            }
            if (referenceCode?.Length > 80)
            {
                AddError(errors, row, "ReferenceCode không được vượt quá 80 ký tự.");
            }
            if (description?.Length > 1000)
            {
                AddError(errors, row, "Description không được vượt quá 1000 ký tự.");
            }
            if (note?.Length > 2000)
            {
                AddError(errors, row, "Note không được vượt quá 2000 ký tự.");
            }
            if (request.NumericValue is < -MaximumNumericValue or > MaximumNumericValue ||
                request.NumericValue.HasValue && decimal.Round(request.NumericValue.Value, 6) != request.NumericValue.Value)
            {
                AddError(errors, row,
                    "NumericValue chỉ được có tối đa 12 chữ số phần nguyên và 6 chữ số thập phân, ví dụ 123.456789.");
            }
            if (request.NumericValue.HasValue && unitCode is null)
            {
                AddError(errors, row, "UnitCode là bắt buộc khi có NumericValue, ví dụ m.");
            }
            if (status is SurveySiteConditionStatus.Unavailable or SurveySiteConditionStatus.NeedsInvestigation &&
                description is null)
            {
                AddError(errors, row, "Description là bắt buộc khi trạng thái là Unavailable hoặc NeedsInvestigation.");
            }
            if (category == SurveySiteConditionCategory.Infrastructure &&
                (referenceCode is null || !infrastructureCodes.Contains(referenceCode)))
            {
                AddError(errors, row,
                    "ReferenceCode của hạ tầng phải là mã survey_infrastructure_type đang hoạt động.");
            }
            if (!keys.Add($"{category}:{code}"))
            {
                AddError(errors, row, $"Điều kiện {category}/{code} bị trùng lặp.");
            }

            result.Add(new ValidatedCondition(
                category, code, status, request.NumericValue, unitCode,
                referenceCode, description, note));
        }

        RequireMeasurement(result, errors, SurveySiteConditionCategory.RightOfWay, "access-width");
        RequireMeasurement(result, errors, SurveySiteConditionCategory.Elevation, "site-elevation");
        return result;
    }

    private async Task<List<SurveySiteConditionResponse>> ReplaceValidatedAsync(
        int surveyId,
        IReadOnlyCollection<ValidatedCondition> conditions,
        int userId,
        CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var existing = await db.SurveySiteConditions
            .Where(condition => condition.SurveyId == surveyId)
            .ToListAsync(ct);
        db.SurveySiteConditions.RemoveRange(existing);
        var now = DateTime.UtcNow;
        var entities = conditions.Select(condition => new SurveySiteCondition
        {
            SurveyId = surveyId,
            Category = condition.Category,
            Code = condition.Code,
            Status = condition.Status,
            NumericValue = condition.NumericValue,
            UnitCode = condition.UnitCode,
            ReferenceCode = condition.ReferenceCode,
            Description = condition.Description,
            Note = condition.Note,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId,
        }).ToList();
        db.SurveySiteConditions.AddRange(entities);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return entities.OrderBy(condition => condition.Category).ThenBy(condition => condition.Code).Select(Map).ToList();
    }

    private async Task<SurveyProjectState> GetProjectStateAsync(int surveyId, CancellationToken ct)
    {
        var state = await db.Surveys.AsNoTracking()
            .Where(survey => survey.Id == surveyId)
            .Select(survey => new SurveyProjectState(true, survey.OperationalProjectId))
            .FirstOrDefaultAsync(ct);
        return state ?? new SurveyProjectState(false, null);
    }

    private static void EnsureProjectAssigned(int? projectId)
    {
        if (!projectId.HasValue || projectId.Value <= 0)
        {
            throw new SurveyOperationException(
                "Dự án vận hành của phiếu khảo sát không hợp lệ. Hãy chọn một dự án có mã số lớn hơn 0, ví dụ 1, trước khi nhập điều kiện khảo sát.");
        }
    }

    private static void RequireMeasurement(
        IReadOnlyCollection<ValidatedCondition> conditions,
        List<CsvImportError> errors,
        SurveySiteConditionCategory category,
        string code)
    {
        var condition = conditions.FirstOrDefault(item => item.Category == category && item.Code == code);
        if (condition is null)
        {
            errors.Add(new CsvImportError { Message = $"Thiếu dòng bắt buộc {category}/{code} với UnitCode m." });
        }
        else if (condition.UnitCode != "m")
        {
            errors.Add(new CsvImportError { Message = $"Dòng {category}/{code} bắt buộc dùng UnitCode m." });
        }
    }

    private static void AddError(List<CsvImportError> errors, int? row, string message) =>
        errors.Add(new CsvImportError { Row = row, Message = row.HasValue ? $"Dòng {row}: {message}" : message });

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static SurveySiteConditionResponse Map(SurveySiteCondition condition) => new()
    {
        Id = condition.Id,
        Category = condition.Category.ToString(),
        Code = condition.Code,
        StatusCode = condition.Status.ToString(),
        NumericValue = condition.NumericValue,
        UnitCode = condition.UnitCode,
        ReferenceCode = condition.ReferenceCode,
        Description = condition.Description,
        Note = condition.Note,
        CreatedAt = condition.CreatedAt,
        UpdatedAt = condition.UpdatedAt,
    };

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex StableCodeRegex();

    private sealed record SurveyProjectState(bool Exists, int? OperationalProjectId);
    private sealed record ValidatedCondition(
        SurveySiteConditionCategory Category,
        string Code,
        SurveySiteConditionStatus Status,
        decimal? NumericValue,
        string? UnitCode,
        string? ReferenceCode,
        string? Description,
        string? Note);
}