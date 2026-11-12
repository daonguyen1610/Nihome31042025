namespace NihomeBackend.Models;

public class SiteSettings
{
    public const int DefaultContractMaxFileSizeMb = 100;
    public const string DefaultTimeZoneId = "Asia/Ho_Chi_Minh";
    public const string DefaultCurrencyCode = "VND";
    public const decimal DefaultUsdVndRate = 25_000m;

    public int Id { get; set; }

    /// <summary>IANA time zone used for every displayed date and time.</summary>
    public string DisplayTimeZoneId { get; set; } = DefaultTimeZoneId;

    /// <summary>ISO 4217 code applied to every money value in the system.</summary>
    public string CurrencyCode { get; set; } = DefaultCurrencyCode;

    /// <summary>VND per 1 USD, used to convert recorded amounts for display.</summary>
    public decimal UsdVndRate { get; set; } = DefaultUsdVndRate;

    public decimal CmScheduleDelayThresholdPercent { get; set; } = 5m;

    public string? CmScheduleEmailSubjectTemplate { get; set; }
    public string? CmScheduleEmailBodyTemplate { get; set; }

    public string SiteName { get; set; } = "Nihome";

    public string? SiteDescription { get; set; }

    public string? PrimaryEmail { get; set; }

    public string? SecondaryEmail { get; set; }

    public string? PrimaryPhone { get; set; }

    public string? SecondaryPhone { get; set; }

    public string? Address { get; set; }

    public string? MapEmbedUrl { get; set; }

    public bool EnableOtpForRegistration { get; set; } = true;

    public bool EnableOtpForForgotPassword { get; set; } = true;

    public string? OtpEmailSubjectTemplate { get; set; }

    public string? OtpEmailBodyTemplate { get; set; }

    public string? NewApplicationEmailSubjectTemplate { get; set; }

    public string? NewApplicationEmailBodyTemplate { get; set; }

    public string? QuoteEmailSubjectTemplate { get; set; }

    public string? QuoteEmailBodyTemplate { get; set; }
    public string? QuoteApprovalEmailSubjectTemplate { get; set; }
    public string? QuoteApprovalEmailBodyTemplate { get; set; }
    public string? QuoteApprovalDecisionEmailSubjectTemplate { get; set; }
    public string? QuoteApprovalDecisionEmailBodyTemplate { get; set; }

    public string? ContractRenewalEmailSubjectTemplate { get; set; }
    public string? ContractRenewalEmailBodyTemplate { get; set; }
    public string? ContractEndEmailSubjectTemplate { get; set; }
    public string? ContractEndEmailBodyTemplate { get; set; }
    public string? WorkPackageApprovalEmailSubjectTemplate { get; set; }
    public string? WorkPackageApprovalEmailBodyTemplate { get; set; }
    public string? ProjectScheduleEmailSubjectTemplate { get; set; }
    public string? ProjectScheduleEmailBodyTemplate { get; set; }

    public string? NotificationEmail { get; set; }

    public int AuditLogRetentionMinutes { get; set; } = 43200;

    public int ContractMaxFileSizeMb { get; set; } = DefaultContractMaxFileSizeMb;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
