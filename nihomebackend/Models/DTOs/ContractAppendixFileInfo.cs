using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs;

/// <summary>One file attached to a contract appendix / variation order.</summary>
public class ContractAppendixFileInfo
{
    [StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [StringLength(300)]
    public string? OriginalFileName { get; set; }

    public long? FileSize { get; set; }

    [StringLength(150)]
    public string? ContentType { get; set; }
}
