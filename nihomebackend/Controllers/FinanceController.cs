using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/finance")]
[Route("api/v1/finance")]
[Authorize]
public sealed class FinanceController(IFinanceService service, IAuditLogger audit) : ControllerBase
{
    [HttpGet("payment-requests")]
    [RequirePermission("finance.payments", "view")]
    public async Task<ActionResult<IReadOnlyList<PaymentRequestResponse>>> ListPayments(CancellationToken ct) =>
        Ok(await service.ListPaymentRequestsAsync(ct));

    [HttpGet("periods")]
    [RequirePermission("finance.periods", "view")]
    public async Task<ActionResult<IReadOnlyList<AccountingPeriodResponse>>> ListPeriods(CancellationToken ct) =>
        Ok(await service.ListPeriodsAsync(ct));

    [HttpGet("corrections")]
    [RequirePermission("finance.corrections", "view")]
    public async Task<ActionResult<IReadOnlyList<AccountingCorrectionResponse>>> ListCorrections(CancellationToken ct) =>
        Ok(await service.ListCorrectionsAsync(ct));

    [HttpPost("payment-requests")]
    [RequirePermission("finance.payments", "manage")]
    [Idempotency("finance.payments.create", requireKey: true)]
    public Task<ActionResult<PaymentRequestResponse>> CreatePayment(
        [FromBody] PaymentRequestUpsertRequest request, CancellationToken ct) =>
        Execute(EntityTypes.PaymentRequest, "finance.payment.create", userId =>
            service.CreatePaymentRequestAsync(request, userId, ct), true);

    [HttpPut("payment-requests/{id:int}")]
    [RequirePermission("finance.payments", "manage")]
    [Idempotency("finance.payments.update", requireKey: true)]
    public Task<ActionResult<PaymentRequestResponse>> UpdatePayment(
        int id, [FromBody] PaymentRequestUpsertRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(id, EntityTypes.PaymentRequest, "finance.payment.update", _ =>
            service.UpdatePaymentRequestAsync(id, request, ct));
    }

    [HttpPost("payment-requests/{id:int}/submit")]
    [RequirePermission("finance.payments", "manage")]
    [Idempotency("finance.payments.submit", requireKey: true)]
    public Task<ActionResult<PaymentRequestResponse>> SubmitPayment(int id, [FromBody] FinanceTransitionRequest request, CancellationToken ct) =>
        PaymentTransition(id, request, "submit", userId => service.SubmitPaymentRequestAsync(id, request, userId, ct));

    [HttpPost("payment-requests/{id:int}/validate")]
    [RequirePermission("finance.payments", "manage")]
    [Idempotency("finance.payments.validate", requireKey: true)]
    public Task<ActionResult<PaymentRequestResponse>> ValidatePayment(int id, [FromBody] FinanceTransitionRequest request, CancellationToken ct) =>
        PaymentTransition(id, request, "validate", userId => service.ValidatePaymentRequestAsync(id, request, userId, ct));

    [HttpPost("payment-requests/{id:int}/decision")]
    [RequirePermission("finance.payments", "approve")]
    [Idempotency("finance.payments.decision", requireKey: true)]
    public Task<ActionResult<PaymentRequestResponse>> DecidePayment(
        int id, [FromBody] FinanceDecisionRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(id, EntityTypes.PaymentRequest, "finance.payment.decision", userId =>
            service.DecidePaymentRequestAsync(id, request, userId, ct));
    }

    [HttpPost("payment-requests/{id:int}/pay")]
    [RequirePermission("finance.payments", "pay")]
    [Idempotency("finance.payments.pay", requireKey: true)]
    public Task<ActionResult<PaymentRequestResponse>> PayPayment(int id, [FromBody] FinanceTransitionRequest request, CancellationToken ct) =>
        PaymentTransition(id, request, "pay", userId => service.PayPaymentRequestAsync(id, request, userId, ct));

    [HttpPost("payment-requests/{id:int}/cancel")]
    [RequirePermission("finance.payments", "approve")]
    [Idempotency("finance.payments.cancel", requireKey: true)]
    public Task<ActionResult<PaymentRequestResponse>> CancelPayment(int id, [FromBody] FinanceTransitionRequest request, CancellationToken ct) =>
        PaymentTransition(id, request, "cancel", userId => service.CancelPaymentRequestAsync(id, request, userId, ct));

    [HttpPost("periods")]
    [RequirePermission("finance.periods", "close")]
    [Idempotency("finance.periods.create", requireKey: true)]
    public Task<ActionResult<AccountingPeriodResponse>> CreatePeriod([FromBody] AccountingPeriodCreateRequest request, CancellationToken ct) =>
        Execute(EntityTypes.AccountingPeriod, "finance.period.create", _ => service.CreatePeriodAsync(request, ct), true);

    [HttpPost("periods/{id:int}/start-closing")]
    [RequirePermission("finance.periods", "close")]
    [Idempotency("finance.periods.start-closing", requireKey: true)]
    public Task<ActionResult<AccountingPeriodResponse>> StartClosingPeriod(int id, [FromBody] FinanceTransitionRequest request, CancellationToken ct) =>
        PeriodTransition(id, request, "start-closing", userId => service.StartClosingPeriodAsync(id, request, userId, ct));

    [HttpPost("periods/{id:int}/close")]
    [RequirePermission("finance.periods", "close")]
    [Idempotency("finance.periods.close", requireKey: true)]
    public Task<ActionResult<AccountingPeriodResponse>> ClosePeriod(int id, [FromBody] FinanceTransitionRequest request, CancellationToken ct) =>
        PeriodTransition(id, request, "close", userId => service.ClosePeriodAsync(id, request, userId, ct));

    [HttpPost("corrections")]
    [RequirePermission("finance.corrections", "manage")]
    [Idempotency("finance.corrections.create", requireKey: true)]
    public Task<ActionResult<AccountingCorrectionResponse>> CreateCorrection(
        [FromBody] AccountingCorrectionUpsertRequest request, CancellationToken ct) =>
        Execute(EntityTypes.AccountingCorrection, "finance.correction.create", userId =>
            service.CreateCorrectionAsync(request, userId, ct), true);

    [HttpPut("corrections/{id:int}")]
    [RequirePermission("finance.corrections", "manage")]
    [Idempotency("finance.corrections.update", requireKey: true)]
    public Task<ActionResult<AccountingCorrectionResponse>> UpdateCorrection(
        int id, [FromBody] AccountingCorrectionUpsertRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(id, EntityTypes.AccountingCorrection, "finance.correction.update", _ =>
            service.UpdateCorrectionAsync(id, request, ct));
    }

    [HttpPost("corrections/{id:int}/submit")]
    [RequirePermission("finance.corrections", "manage")]
    [Idempotency("finance.corrections.submit", requireKey: true)]
    public Task<ActionResult<AccountingCorrectionResponse>> SubmitCorrection(int id, [FromBody] FinanceTransitionRequest request, CancellationToken ct) =>
        CorrectionTransition(id, request, "submit", userId => service.SubmitCorrectionAsync(id, request, userId, ct));

    [HttpPost("corrections/{id:int}/decision")]
    [RequirePermission("finance.corrections", "approve")]
    [Idempotency("finance.corrections.decision", requireKey: true)]
    public Task<ActionResult<AccountingCorrectionResponse>> DecideCorrection(
        int id, [FromBody] FinanceDecisionRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(id, EntityTypes.AccountingCorrection, "finance.correction.decision", userId =>
            service.DecideCorrectionAsync(id, request, userId, ct));
    }

    [HttpPost("corrections/{id:int}/reverse")]
    [RequirePermission("finance.corrections", "reverse")]
    [Idempotency("finance.corrections.reverse", requireKey: true)]
    public Task<ActionResult<AccountingCorrectionResponse>> ReverseCorrection(
        int id, [FromBody] AccountingCorrectionReversalRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(id, EntityTypes.AccountingCorrection, "finance.correction.reverse", userId =>
            service.ReverseCorrectionAsync(id, request, userId, ct));
    }

    private Task<ActionResult<PaymentRequestResponse>> PaymentTransition(
        int id, FinanceTransitionRequest request, string action, Func<int, Task<PaymentRequestResponse?>> operation)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(id, EntityTypes.PaymentRequest, $"finance.payment.{action}", operation);
    }

    private Task<ActionResult<AccountingPeriodResponse>> PeriodTransition(
        int id, FinanceTransitionRequest request, string action, Func<int, Task<AccountingPeriodResponse?>> operation)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(id, EntityTypes.AccountingPeriod, $"finance.period.{action}", operation);
    }

    private Task<ActionResult<AccountingCorrectionResponse>> CorrectionTransition(
        int id, FinanceTransitionRequest request, string action, Func<int, Task<AccountingCorrectionResponse?>> operation)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(id, EntityTypes.AccountingCorrection, $"finance.correction.{action}", operation);
    }

    private async Task<ActionResult<T>> Execute<T>(string resourceType, string action, Func<int, Task<T>> operation, bool created)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var result = await operation(userId.Value);
            Audit(action, resourceType, result!);
            return created ? StatusCode(StatusCodes.Status201Created, result) : Ok(result);
        }
        catch (FinanceOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    private async Task<ActionResult<T>> ExecuteNullable<T>(
        int id, string resourceType, string action, Func<int, Task<T?>> operation) where T : class
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var result = await operation(userId.Value);
            if (result is null) return NotFound();
            Audit(action, resourceType, result, id);
            return Ok(result);
        }
        catch (FinanceOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    private void Audit(string action, string resourceType, object value, int? id = null) => audit.Log(new AuditEvent
    {
        Action = action,
        ResourceType = resourceType,
        ResourceId = id?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Message = $"{resourceType} finance workflow changed.",
        NewValue = value,
    });

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }
}