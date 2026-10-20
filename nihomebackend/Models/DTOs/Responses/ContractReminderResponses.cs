namespace NihomeBackend.Models.DTOs.Responses;

public sealed record ContractReminderResponse(int Id, string Kind, DateTime ReminderDate,
    string ToEmail, string Subject, string Body, string Status, string? ErrorCode,
    DateTime CreatedAt, DateTime? AttemptedAt, DateTime? SentAt);

public sealed record ContractReminderPreviewResponse(string PreviewToken, int ContactId,
    string ToEmail, string Subject, string Body, string Kind, DateTime ReminderDate,
    IReadOnlyList<ContractReminderContact> Contacts);

public sealed record ContractReminderContact(int Id, string Name, string Email);

public sealed record ContractReminderTemplatesResponse(string RenewalSubject, string RenewalBody,
    string EndSubject, string EndBody);
