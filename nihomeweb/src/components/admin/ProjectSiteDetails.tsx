import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useI18n } from "@/lib/i18n";
import { formatNumber } from "@/lib/displaySettings";
import { cn } from "@/lib/utils";
import { adminApi, type MasterDataOption } from "@/services/adminApi";
import type { ProjectConstructionKind } from "@/services/admin/types/operationalProjects";

export interface ProjectSite {
  siteAddress?: string | null;
  constructionKind?: ProjectConstructionKind | null;
  businessSectorCode?: string | null;
  siteAreaM2?: number | null;
}

/**
 * Where and what NICON builds (feedback rows 25 and 27): address, new build or renovation,
 * business sector and area, read from the project. Says which details are still missing and,
 * when given, links to the project to add them.
 */
export function ProjectSiteDetails({
  site,
  projectHref,
  className,
  compact = false,
  testId = "project-site",
}: {
  site: ProjectSite;
  projectHref?: string;
  className?: string;
  /** Two columns at most, for narrow places such as a dialog. */
  compact?: boolean;
  testId?: string;
}) {
  const { t, lang } = useI18n();
  const [sectors, setSectors] = useState<MasterDataOption[]>([]);
  useEffect(() => {
    let cancelled = false;
    adminApi
      .getMasterDataOptions("business_sector", lang)
      .then(({ data }) => {
        if (!cancelled) setSectors(data ?? []);
      })
      .catch(() => {
        // The code still shows when the sector names cannot load.
      });
    return () => {
      cancelled = true;
    };
  }, [lang]);

  const sector = site.businessSectorCode
    ? (sectors.find((item) => item.code === site.businessSectorCode)?.name ??
      site.businessSectorCode)
    : "";
  const fields = [
    { key: "siteAddress", value: site.siteAddress ?? "" },
    {
      key: "constructionKind",
      value: site.constructionKind
        ? t(`operationalProjects.constructionKind.${site.constructionKind}`)
        : "",
    },
    { key: "businessSector", value: sector },
    {
      key: "siteArea",
      value: site.siteAreaM2 != null ? `${formatNumber(site.siteAreaM2)} m²` : "",
    },
  ];
  const missing = fields.filter((field) => !field.value);
  return (
    <section className={cn("rounded-lg border bg-card p-4", className)} data-testid={testId}>
      <h2 className="mb-3 text-sm font-medium">{t("operationalProjects.site.title")}</h2>
      <dl className={cn("grid gap-3 text-sm sm:grid-cols-2", !compact && "lg:grid-cols-4")}>
        {fields.map((field) => (
          <div key={field.key} className="min-w-0">
            <dt className="text-xs text-muted-foreground">
              {t(`operationalProjects.field.${field.key}`)}
            </dt>
            <dd className="break-words">{field.value || "—"}</dd>
          </div>
        ))}
      </dl>
      {missing.length > 0 && (
        <p className="mt-3 text-sm text-amber-700 dark:text-amber-400">
          {projectHref ? (
            <>
              {t("operationalProjects.site.missingElsewhere", {
                fields: missing
                  .map((field) => t(`operationalProjects.field.${field.key}`))
                  .join(", "),
              })}{" "}
              <Link className="underline underline-offset-4" to={projectHref}>
                {t("operationalProjects.site.openProject")}
              </Link>
            </>
          ) : (
            t("operationalProjects.site.missing", {
              fields: missing
                .map((field) => t(`operationalProjects.field.${field.key}`))
                .join(", "),
            })
          )}
        </p>
      )}
    </section>
  );
}
