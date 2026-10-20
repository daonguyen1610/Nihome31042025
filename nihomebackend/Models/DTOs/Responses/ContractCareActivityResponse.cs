namespace NihomeBackend.Models.DTOs.Responses;

public sealed class ContractCareActivityResponse : CustomerActivityResponse
{
    public int ContractId { get; set; }
}

public sealed record ContractCareActivityListResponse(
    int Total, int Page, int PageSize, List<ContractCareActivityResponse> Items);
