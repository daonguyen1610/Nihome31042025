import { Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";

// Switches between the investment-rate catalogs and the BOQ template library.
export function MaterialRatesNav({ active }: { active: "investment" | "boq" }) {
  const { t } = useI18n();
  return (
    <nav
      className="flex w-fit rounded-lg border bg-muted/30 p-1"
      aria-label={t("materialRates.catalogType.navigation")}
    >
      <Button variant={active === "investment" ? "secondary" : "ghost"} size="sm" asChild>
        <Link to="/admin/material-rates/investment">
          {t("materialRates.catalogType.investment")}
        </Link>
      </Button>
      <Button variant={active === "boq" ? "secondary" : "ghost"} size="sm" asChild>
        <Link to="/admin/material-rates/boq">{t("materialRates.catalogType.boq")}</Link>
      </Button>
    </nav>
  );
}
