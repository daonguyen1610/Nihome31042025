using NihomeBackend.Models;

namespace NihomeBackend.Data;

public static class KpiSeeder
{
    private sealed record DefinitionSeed(
        string Code,
        string RoleCode,
        string SourceModule,
        string MetricCode,
        decimal Weight,
        KpiTargetDirection Direction = KpiTargetDirection.HigherIsBetter,
        decimal? TargetValue = null);

    private static readonly DefinitionSeed[] Definitions =
    [
        new("SALES_CONVERSION", "SALES", "M1", "SalesLeadConversionRate", 0.40m),
        new("SALES_REVENUE", "SALES", "M6", "SalesNewContractRevenue", 0.40m),
        new("SALES_FIRST_RESPONSE", "SALES", "M1", "SalesFirstInteractionHours", 0.20m, KpiTargetDirection.LowerIsBetter),
        new("TENDER_WIN_RATE", "TENDERING", "M1", "TenderWinRate", 0.40m),
        new("TENDER_ON_TIME", "TENDERING", "M1", "TenderOnTimePreparationRate", 0.30m),
        new("TENDER_ESTIMATE_ACCURACY", "TENDERING", "M5", "TenderEstimateAccuracy", 0.30m),
        new("DESIGN_ON_TIME", "DESIGN", "M2", "DesignReleaseOnTimeRate", 0.40m),
        new("DESIGN_FIRST_PASS", "DESIGN", "M2", "DesignFirstPassRate", 0.30m),
        new("DESIGN_SITE_ERRORS", "DESIGN", "M4", "DesignSiteErrorCount", 0.30m, KpiTargetDirection.LowerIsBetter),
        new("SITE_PROGRESS", "SITE", "M4", "SiteProgressVariance", 0.30m, KpiTargetDirection.LowerIsBetter),
        new("SITE_MATERIAL_WASTE", "SITE", "M5", "MaterialWasteRate", 0.30m, KpiTargetDirection.LowerIsBetter),
        new("SITE_FIRST_ACCEPTANCE", "SITE", "M4", "FirstAcceptanceRate", 0.20m),
        new("SITE_HSE", "SITE", "M4", "HseViolationCount", 0.20m, KpiTargetDirection.LowerIsBetter),
        new("PROCUREMENT_COST", "PROCUREMENT", "M5", "ProcurementCostOptimization", 0.40m),
        new("PROCUREMENT_ON_TIME", "PROCUREMENT", "M5", "ProcurementDeliveryHours", 0.30m, KpiTargetDirection.LowerIsBetter),
        new("PROCUREMENT_VENDOR_RATING", "PROCUREMENT", "M5", "VendorRating", 0.30m),
        new("ACCOUNTING_COLLECTION", "PROJECT_ACCOUNTING", "M6", "ReceivableOnTimeRate", 0.40m),
        new("ACCOUNTING_PAYMENT_SPEED", "PROJECT_ACCOUNTING", "M6", "PartnerPaymentHours", 0.30m, KpiTargetDirection.LowerIsBetter, 72m),
        new("ACCOUNTING_ACCURACY", "PROJECT_ACCOUNTING", "M6", "AccountingCorrectionCount", 0.30m, KpiTargetDirection.LowerIsBetter, 1m),
    ];

    public static void Seed(AppDbContext db)
    {
        var userId = db.Users.OrderBy(user => user.Id).Select(user => user.Id).FirstOrDefault();
        if (userId == 0) return;
        var now = DateTime.UtcNow;
        var existing = db.KpiDefinitions.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var seed in Definitions)
        {
            if (existing.TryGetValue(seed.Code, out var definition))
            {
                definition.RoleCode = seed.RoleCode;
                definition.NameKey = $"kpi.definition.{seed.Code}";
                definition.SourceModule = seed.SourceModule;
                definition.MetricCode = seed.MetricCode;
                definition.TargetValue ??= seed.TargetValue;
                continue;
            }
            db.KpiDefinitions.Add(new KpiDefinition
            {
                Code = seed.Code,
                RoleCode = seed.RoleCode,
                NameKey = $"kpi.definition.{seed.Code}",
                SourceModule = seed.SourceModule,
                MetricCode = seed.MetricCode,
                Weight = seed.Weight,
                TargetDirection = seed.Direction,
                TargetValue = seed.TargetValue,
                EffectiveFrom = now,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedByUserId = userId,
                UpdatedByUserId = userId,
            });
        }
        db.SaveChanges();
    }
}
