using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services.GoogleDrive;

namespace NihomeBackend.Services.HardDelete;

public sealed class HardDeleteOperationService(
    AppDbContext db,
    IHardDeleteFileService files,
    IGoogleDriveAdapter drive,
    IHardDeleteResourceHandlerRegistry handlers,
    ILogger<HardDeleteOperationService> logger) : IHardDeleteOperationService
{
    private const int MaxAttempts = 8;
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(15);

    public async Task<HardDeleteOperationResult> CreateAsync(
        CreateHardDeleteOperationRequest request, CancellationToken ct = default)
    {
        Validate(request);
        var now = DateTime.UtcNow;
        var operation = new HardDeleteOperation
        {
            Id = Guid.NewGuid(),
            ResourceType = request.ResourceType.Trim(),
            ResourceId = request.ResourceId.Trim(),
            ResourceLabel = request.ResourceLabel.Trim(),
            PlanToken = request.PlanToken.Trim(),
            Confirmation = request.Confirmation.Trim(),
            RequestedBy = request.RequestedBy.Trim(),
            Status = HardDeleteOperationStatus.Preparing,
            CreatedAt = now,
            UpdatedAt = now,
            Items = request.Items.Select(item => new HardDeleteItem
            {
                Kind = item.Kind,
                Status = HardDeleteItemStatus.Pending,
                ActionIdentifier = item.ActionIdentifier.Trim(),
                Sequence = item.Sequence,
                ExpectedParentId = item.ExpectedParentId,
                ExpectedAppPropertiesJson = item.ExpectedAppProperties is null
                    ? null
                    : JsonSerializer.Serialize(item.ExpectedAppProperties),
            }).ToList(),
        };
        HardDeleteStateMachine.Transition(operation, HardDeleteOperationStatus.Ready, now);
        db.HardDeleteOperations.Add(operation);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
        {
            logger.LogInformation(exception, "A hard-delete operation already exists for {ResourceType}/{ResourceId}",
                operation.ResourceType, operation.ResourceId);
            throw new HardDeleteOperationConflictException(
                "Đã có một tác vụ xóa đang hoạt động cho tài nguyên này.");
        }
        return ToResult(operation);
    }

    public async Task<HardDeleteOperationResult> ProcessAsync(
        Guid operationId, CancellationToken ct = default, string? requestedBy = null)
    {
        var operation = await db.HardDeleteOperations.Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.Id == operationId &&
                (requestedBy == null || item.RequestedBy == requestedBy), ct)
            ?? throw new HardDeleteOperationException("operation_not_found", "Không tìm thấy tác vụ xóa.");
        if (operation.Status == HardDeleteOperationStatus.Completed) return ToResult(operation);
        var now = DateTime.UtcNow;
        if (operation.Status == HardDeleteOperationStatus.Processing &&
            operation.LastAttemptAt > now.Subtract(ProcessingLease))
            throw new HardDeleteOperationConflictException("Tác vụ xóa đang được xử lý.");

        var handler = handlers.Find(operation.ResourceType);
        if (handler is null)
        {
            return await MarkManualAsync(operation, "resource_handler_missing",
                "Chưa đăng ký bộ xử lý xóa cơ sở dữ liệu cho loại tài nguyên này.", ct);
        }

        if (operation.Status != HardDeleteOperationStatus.Processing)
            HardDeleteStateMachine.Transition(operation, HardDeleteOperationStatus.Processing, now);
        else
            operation.UpdatedAt = now;
        operation.AttemptCount++;
        operation.LastAttemptAt = now;
        operation.NextAttemptAt = null;
        operation.LastErrorCode = null;
        operation.LastErrorMessage = null;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new HardDeleteOperationConflictException("Tác vụ xóa đã được một tiến trình khác nhận xử lý.");
        }

        try
        {
            await handler.AuthorizeAsync(new HardDeleteResourceContext(
                operation.Id, operation.ResourceType, operation.ResourceId,
                operation.PlanToken, operation.RequestedBy, string.Empty,
                operation.HasIrreversibleStep), ct);
            await QuarantineLocalFilesAsync(operation, ct);
            await PermanentlyDeleteDriveItemsAsync(operation, ct);
            await FinalizeDatabaseAsync(operation, handler, ct);
            await PurgeLocalFilesAsync(operation, ct);
            HardDeleteStateMachine.Transition(operation, HardDeleteOperationStatus.Completed, DateTime.UtcNow);
            await db.SaveChangesAsync(ct);
            return ToResult(operation);
        }
        catch (DrivePermanentDeleteRejectedException exception)
        {
            if (!operation.HasIrreversibleStep) await RestoreLocalFilesAsync(operation, ct);
            return await MarkManualAsync(operation, exception.Code, exception.Message, ct);
        }
        catch (HardDeleteAuthorizationException exception)
        {
            if (!operation.HasIrreversibleStep) await RestoreLocalFilesAsync(operation, ct);
            return await MarkManualAsync(operation, exception.Code, exception.Message, ct);
        }
        catch (DeletionPlanChangedException exception)
        {
            var driveDeletionCompleted = operation.Items.Any(item =>
                item.Kind is HardDeleteItemKind.DriveFile or HardDeleteItemKind.DriveFolder &&
                item.Status == HardDeleteItemStatus.Completed);
            operation.HasIrreversibleStep = driveDeletionCompleted;
            if (!driveDeletionCompleted) await RestoreLocalFilesAsync(operation, ct);
            return await MarkManualAsync(operation, "deletion_plan_changed", exception.Message, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (!operation.HasIrreversibleStep) await RestoreLocalFilesAsync(operation, ct);
            var error = Sanitize(exception.Message);
            operation.LastErrorCode = "hard_delete_processing_failed";
            operation.LastErrorMessage = error;
            operation.NextAttemptAt = operation.AttemptCount >= MaxAttempts
                ? null
                : DateTime.UtcNow.Add(RetryDelay(operation.AttemptCount));
            HardDeleteStateMachine.Transition(operation,
                operation.AttemptCount >= MaxAttempts
                    ? HardDeleteOperationStatus.ManualActionRequired
                    : HardDeleteOperationStatus.Failed,
                DateTime.UtcNow);
            await db.SaveChangesAsync(ct);
            logger.LogWarning(exception, "Hard-delete operation {OperationId} failed on attempt {AttemptCount}",
                operation.Id, operation.AttemptCount);
            return ToResult(operation);
        }
    }

    public async Task<HardDeleteOperationResult?> GetAsync(
        Guid operationId, CancellationToken ct = default, string? requestedBy = null)
    {
        var operation = await db.HardDeleteOperations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == operationId &&
                (requestedBy == null || item.RequestedBy == requestedBy), ct);
        return operation is null ? null : ToResult(operation);
    }

    private async Task QuarantineLocalFilesAsync(HardDeleteOperation operation, CancellationToken ct)
    {
        foreach (var item in operation.Items.Where(item =>
            item.Kind == HardDeleteItemKind.LocalFile && item.Status == HardDeleteItemStatus.Pending)
            .OrderBy(item => item.Sequence))
        {
            Touch(item);
            var result = await files.QuarantineAsync(operation.Id, item.ActionIdentifier, ct);
            item.QuarantinePath = result.QuarantinePath;
            item.Status = result.WasMissing ? HardDeleteItemStatus.Completed : HardDeleteItemStatus.Quarantined;
            item.CompletedAt = result.WasMissing ? DateTime.UtcNow : null;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task PermanentlyDeleteDriveItemsAsync(HardDeleteOperation operation, CancellationToken ct)
    {
        foreach (var item in operation.Items.Where(item =>
            item.Kind is HardDeleteItemKind.DriveFile or HardDeleteItemKind.DriveFolder &&
            item.Status != HardDeleteItemStatus.Completed).OrderBy(item => item.Sequence))
        {
            Touch(item);
            var properties = string.IsNullOrWhiteSpace(item.ExpectedAppPropertiesJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(item.ExpectedAppPropertiesJson) ?? [];
            await drive.PermanentDeleteOwnedAsync(new DrivePermanentDeleteRequest(
                item.ActionIdentifier, properties, item.ExpectedParentId), ct);
            operation.HasIrreversibleStep = true;
            item.Status = HardDeleteItemStatus.Completed;
            item.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task FinalizeDatabaseAsync(
        HardDeleteOperation operation, IHardDeleteResourceHandler handler, CancellationToken ct)
    {
        var databaseItems = operation.Items.Where(item =>
            item.Kind == HardDeleteItemKind.DatabaseAggregate && item.Status != HardDeleteItemStatus.Completed).ToList();
        if (databaseItems.Count != 1)
            throw new HardDeleteOperationException("invalid_database_item_count",
                "Tác vụ xóa phải có đúng một bước kết thúc dữ liệu.");

        var item = databaseItems[0];
        operation.HasIrreversibleStep = true;
        await db.SaveChangesAsync(ct);
        Touch(item);
        await handler.FinalizeAsync(new HardDeleteResourceContext(
            operation.Id, operation.ResourceType, operation.ResourceId,
            operation.PlanToken, operation.RequestedBy, item.ActionIdentifier,
            operation.HasIrreversibleStep), ct);
        item.Status = HardDeleteItemStatus.Completed;
        item.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task PurgeLocalFilesAsync(HardDeleteOperation operation, CancellationToken ct)
    {
        foreach (var item in operation.Items.Where(item =>
            item.Kind == HardDeleteItemKind.LocalFile && item.Status == HardDeleteItemStatus.Quarantined))
        {
            await files.PurgeAsync(item.QuarantinePath, ct);
            item.Status = HardDeleteItemStatus.Completed;
            item.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task RestoreLocalFilesAsync(HardDeleteOperation operation, CancellationToken ct)
    {
        foreach (var item in operation.Items.Where(item =>
            item.Kind == HardDeleteItemKind.LocalFile && item.Status == HardDeleteItemStatus.Quarantined))
        {
            await files.RestoreAsync(item.ActionIdentifier, item.QuarantinePath, ct);
            item.QuarantinePath = null;
            item.Status = HardDeleteItemStatus.Pending;
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<HardDeleteOperationResult> MarkManualAsync(
        HardDeleteOperation operation, string code, string message, CancellationToken ct)
    {
        operation.LastErrorCode = code;
        operation.LastErrorMessage = Sanitize(message);
        operation.NextAttemptAt = null;
        if (operation.Status != HardDeleteOperationStatus.ManualActionRequired)
            HardDeleteStateMachine.Transition(operation, HardDeleteOperationStatus.ManualActionRequired, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        return ToResult(operation);
    }

    private static void Validate(CreateHardDeleteOperationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ResourceType) || string.IsNullOrWhiteSpace(request.ResourceId) ||
            string.IsNullOrWhiteSpace(request.ResourceLabel) || string.IsNullOrWhiteSpace(request.PlanToken) ||
            string.IsNullOrWhiteSpace(request.Confirmation) || string.IsNullOrWhiteSpace(request.RequestedBy) ||
            request.Items.Count == 0 || request.Items.Any(item => string.IsNullOrWhiteSpace(item.ActionIdentifier)) ||
            request.Items.Select(item => item.Sequence).Distinct().Count() != request.Items.Count)
        {
            throw new HardDeleteOperationException("invalid_operation_plan", "Kế hoạch tác vụ xóa không hợp lệ.");
        }
    }

    private static void Touch(HardDeleteItem item)
    {
        item.AttemptCount++;
        item.LastAttemptAt = DateTime.UtcNow;
        item.LastErrorCode = null;
        item.LastErrorMessage = null;
    }

    private static TimeSpan RetryDelay(int attempt) =>
        TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Clamp(attempt - 1, 0, 6))));

    private static string Sanitize(string message)
    {
        var sanitized = string.Join(' ', message.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return sanitized.Length <= 1000 ? sanitized : sanitized[..1000];
    }

    private static HardDeleteOperationResult ToResult(HardDeleteOperation operation) => new(
        operation.Id,
        operation.Status,
        operation.Status == HardDeleteOperationStatus.Completed,
        operation.Status == HardDeleteOperationStatus.ManualActionRequired,
        operation.LastErrorCode,
        operation.LastErrorMessage);
}