namespace NihomeBackend.Models.DTOs.Responses;

public static class DeletionImpactActions
{
    public const string Delete = "Delete";
    public const string Unlink = "Unlink";
    public const string Block = "Block";
}

public sealed class DeletionImpactLinkResponse
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class DeletionImpactItemResponse
{
    public string Key { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<string> Examples { get; set; } = [];
    public List<DeletionImpactLinkResponse> ResolutionLinks { get; set; } = [];
    public string? ResolutionUrl { get; set; }
}

public sealed class DeletionImpactResponse
{
    public string ResourceType { get; set; } = string.Empty;
    public int ResourceId { get; set; }
    public string ResourceLabel { get; set; } = string.Empty;
    public string RequiredConfirmation { get; set; } = string.Empty;
    public string PlanToken { get; set; } = string.Empty;
    public bool CanDelete { get; set; }
    public int TotalAffected { get; set; }
    public List<DeletionImpactItemResponse> Items { get; set; } = [];
}
