namespace NihomeBackend.Models.DTOs.Responses;

public sealed record OperationalProjectAssignmentAttachmentResponse(
    long Id, int AssignmentId, string OriginalFileName, long FileSize, DateTime CreatedAt);
