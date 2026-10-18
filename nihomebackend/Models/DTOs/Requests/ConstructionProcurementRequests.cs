using System.ComponentModel.DataAnnotations;
using NihomeBackend.Models;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class LinkConstructionContractRequest : IConcurrencyRequest
{
    [Range(1, int.MaxValue)] public int ContractId { get; set; }
    [MaxLength(128)] public string? RowVersion { get; set; }
}

public sealed class ConfirmConstructionSupplyRequest : IConcurrencyRequest
{
    [Range(1, long.MaxValue)] public long DocumentId { get; set; }
    [MaxLength(128)] public string? RowVersion { get; set; }
}

public sealed class CreateConstructionProcurementContractRequest
{
    [Range(1, int.MaxValue)] public int VendorId { get; set; }
    [EnumDataType(typeof(ContractType))] public ContractType Type { get; set; }
    [Required, Range(typeof(decimal), "0", "9007199254740991")] public decimal? Value { get; set; }
    [MaxLength(20000)] public string? ScopeOfWork { get; set; }
}
