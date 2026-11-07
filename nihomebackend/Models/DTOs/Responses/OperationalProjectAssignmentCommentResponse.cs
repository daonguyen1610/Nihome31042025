namespace NihomeBackend.Models.DTOs.Responses;

public sealed record OperationalProjectAssignmentCommentResponse(
    long Id,
    int AssignmentId,
    string Body,
    int CreatedByUserId,
    string CreatedByName,
    DateTime CreatedAt);
