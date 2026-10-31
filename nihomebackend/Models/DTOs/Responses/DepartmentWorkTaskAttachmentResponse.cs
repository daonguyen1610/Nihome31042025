namespace NihomeBackend.Models.DTOs.Responses;

public sealed record DepartmentWorkTaskAttachmentResponse(
    long Id, int WorkTaskId, string OriginalFileName, long FileSize, DateTime CreatedAt);
