import { ArrowRight, Database } from "lucide-react";
import { Link, useLocation } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import { buildCatalogSetupLink, quoteReturnTarget, type QuoteCatalogType } from "@/lib/quoteCatalogSetup";

interface QuoteCatalogEmptyStateProps {
  catalogType: QuoteCatalogType;
}

/**
 * Shown in the quote form when no active catalog exists yet. Instead of an
 * empty dropdown it explains the three setup steps and links straight to the
 * catalog screen, which offers a way back to this quote once a revision is
 * approved.
 */
const QuoteCatalogEmptyState = ({ catalogType }: QuoteCatalogEmptyStateProps) => {
  const { t } = useI18n();
  const { has } = usePermissions();
  const location = useLocation();
  const canManageCatalogs = has(ADMIN_PERMS.materialRatesManage);
  const prefix = catalogType === "Boq" ? "quotes.catalogSetup.boq" : "quotes.catalogSetup.investment";

  return (
    <div
      className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900"
      data-testid={`quote-catalog-empty-${catalogType}`}
    >
      <div className="flex items-start gap-2">
        <Database className="mt-0.5 h-4 w-4 shrink-0" />
        <div className="min-w-0 space-y-2">
          <p className="font-medium">{t(`${prefix}.title`)}</p>
          <p className="text-xs leading-relaxed">{t(`${prefix}.description`)}</p>
          <ol className="list-decimal space-y-0.5 pl-4 text-xs">
            <li>{t("quotes.catalogSetup.step.create")}</li>
            <li>{t("quotes.catalogSetup.step.lines")}</li>
            <li>{t("quotes.catalogSetup.step.approve")}</li>
          </ol>
          {canManageCatalogs ? (
            <Button size="sm" asChild data-testid={`quote-catalog-setup-${catalogType}`}>
              <Link to={buildCatalogSetupLink(catalogType, quoteReturnTarget(location.pathname, location.search))}>
                {t(`${prefix}.action`)}
                <ArrowRight className="ml-1.5 h-4 w-4" />
              </Link>
            </Button>
          ) : (
            <p className="text-xs font-medium">{t("quotes.catalogSetup.askManager")}</p>
          )}
        </div>
      </div>
    </div>
  );
};

export default QuoteCatalogEmptyState;
