using NihomeBackend.Models;

namespace NihomeBackend.Services;

// NICON BOQ thresholds per line (PRD v2, M5): yellow 85%, orange 95%, red 100%.
public static class BoqLimitRules
{
    public static BoqUsageLevel Classify(decimal approvedQuantity, decimal usedQuantity) =>
        approvedQuantity <= 0m || usedQuantity >= approvedQuantity ? BoqUsageLevel.Red
        : usedQuantity >= approvedQuantity * 0.95m ? BoqUsageLevel.Orange
        : usedQuantity >= approvedQuantity * 0.85m ? BoqUsageLevel.Yellow
        : BoqUsageLevel.Normal;
}
