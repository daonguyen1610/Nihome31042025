import { useI18n } from "@/lib/i18n";
import { formatVnd } from "@/lib/numberFormat";
import type { InvestmentRateGroupLine } from "@/services/adminApi";

/** The work groups of an investment-rate form, one card-like row each so it reads on phones too. */
export function InvestmentRateGroupList({ groups }: { groups: InvestmentRateGroupLine[] }) {
  const { t } = useI18n();
  return (
    <ul className="divide-y rounded-md border" data-testid="investment-rate-groups">
      {groups.map((group) => (
        <li
          key={group.code}
          className="flex flex-col gap-1 p-2 text-sm sm:flex-row sm:items-start sm:justify-between sm:gap-4"
        >
          <div className="min-w-0">
            <p className="break-words font-medium">
              <span className="text-muted-foreground">{group.code}</span> · {group.name}
            </p>
            <p className="text-xs text-muted-foreground">
              {t("investmentRate.field.material")}: {formatVnd(group.material)} ·{" "}
              {t("investmentRate.field.labour")}: {formatVnd(group.labour)} ·{" "}
              {t("investmentRate.field.equipment")}: {formatVnd(group.equipment)}
            </p>
          </div>
          <p className="shrink-0 font-medium tabular-nums sm:text-right">
            {formatVnd(group.ratePerSqm)} ₫/m²
          </p>
        </li>
      ))}
    </ul>
  );
}
