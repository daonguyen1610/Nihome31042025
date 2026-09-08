using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IFinanceService
{
    Task<PaymentReferencesResponse> GetPaymentReferencesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PaymentRequestResponse>> ListPaymentRequestsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AccountingPeriodResponse>> ListPeriodsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AccountingCorrectionResponse>> ListCorrectionsAsync(CancellationToken ct = default);
    Task<PaymentRequestResponse> CreatePaymentRequestAsync(PaymentRequestUpsertRequest request, int userId, CancellationToken ct = default);
    Task<PaymentRequestResponse?> UpdatePaymentRequestAsync(int id, PaymentRequestUpsertRequest request, CancellationToken ct = default);
    Task<PaymentRequestResponse?> SubmitPaymentRequestAsync(int id, FinanceTransitionRequest request, int userId, CancellationToken ct = default);
    Task<PaymentRequestResponse?> ValidatePaymentRequestAsync(int id, FinanceTransitionRequest request, int userId, CancellationToken ct = default);
    Task<PaymentRequestResponse?> DecidePaymentRequestAsync(int id, FinanceDecisionRequest request, int userId, CancellationToken ct = default);
    Task<PaymentRequestResponse?> PayPaymentRequestAsync(int id, FinanceTransitionRequest request, int userId, CancellationToken ct = default);
    Task<PaymentRequestResponse?> CancelPaymentRequestAsync(int id, FinanceTransitionRequest request, int userId, CancellationToken ct = default);
    Task<AccountingPeriodResponse> CreatePeriodAsync(AccountingPeriodCreateRequest request, CancellationToken ct = default);
    Task<AccountingPeriodResponse?> StartClosingPeriodAsync(int id, FinanceTransitionRequest request, int userId, CancellationToken ct = default);
    Task<AccountingPeriodResponse?> ClosePeriodAsync(int id, FinanceTransitionRequest request, int userId, CancellationToken ct = default);
    Task<AccountingCorrectionResponse> CreateCorrectionAsync(AccountingCorrectionUpsertRequest request, int userId, CancellationToken ct = default);
    Task<AccountingCorrectionResponse?> UpdateCorrectionAsync(int id, AccountingCorrectionUpsertRequest request, CancellationToken ct = default);
    Task<AccountingCorrectionResponse?> SubmitCorrectionAsync(int id, FinanceTransitionRequest request, int userId, CancellationToken ct = default);
    Task<AccountingCorrectionResponse?> DecideCorrectionAsync(int id, FinanceDecisionRequest request, int userId, CancellationToken ct = default);
    Task<AccountingCorrectionResponse?> ReverseCorrectionAsync(int id, AccountingCorrectionReversalRequest request, int userId, CancellationToken ct = default);
}

public sealed class FinanceOperationException(string message) : Exception(message);