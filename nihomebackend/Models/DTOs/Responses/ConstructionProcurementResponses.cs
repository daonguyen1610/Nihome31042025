using NihomeBackend.Models;

namespace NihomeBackend.Models.DTOs.Responses;

public sealed record ConstructionProcurementEventResponse(
    long Id, int? PreviousContractId, int ContractId, long? SupplyDocumentId,
    int ActorUserId, DateTime CreatedAt);

public sealed record ConstructionProcurementResponse(
    int Id, int OperationalProjectId, string Code, ConstructionResourceType Type,
    string WbsCode, DateTime NeedByDate, decimal Quantity, string Unit, string Reason,
    int? ContractId, string? ContractNumber, bool ContractValid, DateTime? SuppliedAt,
    int? SuppliedByUserId, long? SupplyDocumentId, string RowVersion,
    IReadOnlyList<ConstructionProcurementEventResponse> History);

public sealed record ConstructionProcurementContractResponse(int Id, string ContractNumber, ContractStatus Status);


public sealed record ConstructionSupplyDocumentResponse(long Id, string OriginalFileName);
