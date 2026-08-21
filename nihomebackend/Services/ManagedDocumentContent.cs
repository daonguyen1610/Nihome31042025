namespace NihomeBackend.Services;

public sealed record ManagedDocumentContent(
    string FullPath,
    string OriginalFileName,
    string ContentType);
