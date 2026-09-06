import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  AlertTriangle,
  ArrowUpRight,
  CalendarClock,
  ChartNoAxesCombined,
  Download,
  FileSpreadsheet,
  HardHat,
  Loader2,
  Scale,
  WalletCards,
} from "lucide-react";
import { Link, useSearchParams } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from "@/components/ui/accordion";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Progress } from "@/components/ui/progress";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { useI18n, type Lang } from "@/lib/i18n";
import { formatVndWithSymbol } from "@/lib/numberFormat";
import { adminApi } from "@/services/adminApi";
import type {
  ProjectOperationalReportResponse,
  ProjectReportAvailability,
  ProjectReportDrillDownResponse,
  ProjectReportExportParams,
  ProjectReportFilters,
} from "@/types/projectReports";

const ALL_PROJECTS = "all";
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const LOCALES: Record<Lang, string> = { vi: "vi-VN", en: "en-US", zh: "zh-CN", ja: "ja-JP" };

const statusKey = (area: "project" | "construction" | "acceptance" | "permit" | "quote" | "milestone", status: string) => {
  switch (area) {
    case "project": return `operationalProjects.status.${status}`;
    case "construction": return `constructionTasks.status.${status}`;
    case "acceptance": return `acceptance.status.${status.toLowerCase()}`;
    case "permit": return `permits.status.${status}`;
    case "quote": return `quotes.status.${status}`;
    case "milestone": return `contracts.milestoneStatus.${status}`;
  }
};

const isValidDate = (value: string) => {
  if (!DATE_PATTERN.test(value)) return false;
  const parsed = new Date(`${value}T00:00:00Z`);
  return !Number.isNaN(parsed.getTime()) && parsed.toISOString().slice(0, 10) === value;
};

const safeDrillDown = ({ route, query }: ProjectReportDrillDownResponse) => {
  if (!route.startsWith("/admin/")) return null;
  if (!query) return route;
  return `${route}${query.startsWith("?") ? query : `?${query}`}`;
};

const saveBlob = (blob: Blob, fileName: string) => {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
};

function AvailabilityNotice({ availability, reasonCode }: { availability: ProjectReportAvailability; reasonCode?: string | null }) {
  const { t } = useI18n();
  if (availability === "Available") return null;
  return (
    <div className="rounded-md border border-dashed bg-muted/30 p-4 text-sm text-muted-foreground" role="status">
      <p className="font-medium text-foreground">{t("projectReports.unavailable")}</p>
      <p className="mt-1 break-all">{reasonCode ? t("projectReports.reasonWithCode", { code: reasonCode }) : t("projectReports.noSource")}</p>
    </div>
  );
}

function Metric({ label, value, tone = "default" }: { label: string; value: string | number; tone?: "default" | "danger" }) {
  return (
    <div className="rounded-lg border bg-background p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className={tone === "danger" ? "mt-1 text-xl font-semibold text-destructive" : "mt-1 text-xl font-semibold"}>{value}</p>
    </div>
  );
}

function DictionaryMetrics({ values, emptyLabel, valueFormatter, labelFormatter }: {
  values: Record<string, number>;
  emptyLabel: string;
  valueFormatter?: (value: number) => string;
  labelFormatter?: (key: string) => string;
}) {
  const entries = Object.entries(values);
  if (entries.length === 0) return <p className="text-sm text-muted-foreground">{emptyLabel}</p>;
  return (
    <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
      {entries.map(([key, value]) => (
        <div key={key} className="rounded-md bg-muted/40 p-2.5">
          <p className="truncate text-xs text-muted-foreground" title={labelFormatter ? labelFormatter(key) : key}>
            {labelFormatter ? labelFormatter(key) : key}
          </p>
          <p className="mt-1 font-semibold">{valueFormatter ? valueFormatter(value) : value}</p>
        </div>
      ))}
    </div>
  );
}

function DrillDownLink({ drillDown, children }: { drillDown: ProjectReportDrillDownResponse; children: React.ReactNode }) {
  const href = safeDrillDown(drillDown);
  if (!href) return <span>{children}</span>;
  return (
    <Link to={href} className="inline-flex items-center gap-1 font-medium text-primary hover:underline">
      {children}<ArrowUpRight className="h-3.5 w-3.5" aria-hidden />
    </Link>
  );
}

function ReportSection({ title, icon: Icon, children }: {
  title: string;
  icon: typeof ChartNoAxesCombined;
  children: React.ReactNode;
}) {
  return (
    <section className="rounded-xl border bg-card p-4 shadow-sm sm:p-5">
      <h3 className="mb-4 flex items-center gap-2 font-semibold">
        <Icon className="h-4 w-4 text-primary" aria-hidden />{title}
      </h3>
      {children}
    </section>
  );
}

function ProjectReportCard({ report }: { report: ProjectOperationalReportResponse }) {
  const { t } = useI18n();
  const projectLink = safeDrillDown(report.project.drillDown);
  const projectValue = String(report.project.operationalProjectId);
  const partial = [report.design, report.construction, report.acceptance, report.permits]
    .some((section) => section.availability !== "Available") || report.unavailableMetrics.length > 0;

  return (
    <AccordionItem
      value={projectValue}
      className="rounded-xl border bg-muted/10 px-3 shadow-sm sm:px-5"
      data-testid={`project-report-${projectValue}`}
    >
      <AccordionTrigger
        className="items-start gap-3 py-4 text-left hover:no-underline sm:py-5"
        data-testid={`project-report-trigger-${projectValue}`}
      >
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant="outline">{report.project.code}</Badge>
            <Badge variant="secondary">{t(statusKey("project", report.project.status))}</Badge>
            {partial && <Badge variant="outline" className="border-amber-300 text-amber-700">{t("projectReports.partialData")}</Badge>}
          </div>
          <h2 className="mt-2 break-words text-lg font-semibold sm:text-xl">{report.project.name}</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            {t("projectReports.customer")}: {report.project.customerName} · {t("projectReports.manager")}: {report.project.projectManagerName || t("projectReports.notAssigned")}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            {t("projectReports.projectDates", { start: report.project.startDate || "—", end: report.project.endDate || "—" })}
          </p>
        </div>
      </AccordionTrigger>

      <AccordionContent className="space-y-4 pb-4 sm:pb-5" data-testid={`project-report-content-${projectValue}`}>
        {projectLink && (
          <Link to={projectLink} className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline">
            {report.project.name}<ArrowUpRight className="h-3.5 w-3.5" aria-hidden />
          </Link>
        )}

        <div className="grid gap-4 xl:grid-cols-2">
        <ReportSection title={t("projectReports.design.title")} icon={ChartNoAxesCombined}>
          <AvailabilityNotice availability={report.design.availability} reasonCode={report.design.reasonCode} />
          {report.design.availability === "Available" && report.design.weightedProgressPercent != null && (
            <div className="space-y-3">
              <div className="flex items-end justify-between gap-3">
                <span className="text-sm text-muted-foreground">{t("projectReports.design.weightedProgress")}</span>
                <strong className="text-2xl">{report.design.weightedProgressPercent}%</strong>
              </div>
              <Progress value={report.design.weightedProgressPercent} aria-label={t("projectReports.design.weightedProgress")} />
              <p className="text-xs text-muted-foreground">{t("projectReports.design.policy")}: {report.design.rollupPolicyVersion}</p>
            </div>
          )}
        </ReportSection>

        <ReportSection title={t("projectReports.construction.title")} icon={HardHat}>
          <AvailabilityNotice availability={report.construction.availability} reasonCode={report.construction.reasonCode} />
          {report.construction.availability === "Available" && (
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-2">
                <Metric label={t("projectReports.totalCount")} value={report.construction.totalCount} />
                <Metric label={t("projectReports.overdue")} value={report.construction.overdueCount} tone="danger" />
              </div>
              <DictionaryMetrics
                values={report.construction.countsByStatus}
                emptyLabel={t("projectReports.noStatusData")}
                labelFormatter={(status) => t(statusKey("construction", status))}
              />
              {report.construction.overdueItems.length > 0 && (
                <div>
                  <p className="mb-2 text-sm font-medium">{t("projectReports.construction.overdueItems")}</p>
                  <ul className="space-y-2">
                    {report.construction.overdueItems.map((item) => (
                      <li key={item.id} className="flex flex-col gap-1 rounded-md border p-2.5 text-sm sm:flex-row sm:items-center sm:justify-between">
                        <DrillDownLink drillDown={item.drillDown}>{item.taskCode} · {item.name}</DrillDownLink>
                        <span className="text-xs text-muted-foreground">{t(statusKey("construction", item.status))} · {item.plannedEnd}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )}
        </ReportSection>

        <ReportSection title={t("projectReports.acceptance.title")} icon={CalendarClock}>
          <AvailabilityNotice availability={report.acceptance.availability} reasonCode={report.acceptance.reasonCode} />
          {report.acceptance.availability === "Available" && (
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-2">
                <Metric label={t("projectReports.totalCount")} value={report.acceptance.totalCount} />
                <Metric label={t("projectReports.overdue")} value={report.acceptance.overdueCount} tone="danger" />
              </div>
              <div>
                <p className="mb-2 text-sm font-medium">{t("projectReports.acceptance.byStatus")}</p>
                <DictionaryMetrics
                  values={report.acceptance.countsByStatus}
                  emptyLabel={t("projectReports.noStatusData")}
                  labelFormatter={(status) => t(statusKey("acceptance", status))}
                />
              </div>
              <div>
                <p className="mb-2 text-sm font-medium">{t("projectReports.acceptance.byRevision")}</p>
                <DictionaryMetrics values={report.acceptance.countsByRevision} emptyLabel={t("projectReports.noRevisionData")} />
              </div>
            </div>
          )}
        </ReportSection>

        <ReportSection title={t("projectReports.permits.title")} icon={Scale}>
          <AvailabilityNotice availability={report.permits.availability} reasonCode={report.permits.reasonCode} />
          {report.permits.availability === "Available" && (
            <div className="space-y-4">
              <div className="grid grid-cols-3 gap-2">
                <Metric label={t("projectReports.overdue")} value={report.permits.overdueCount} tone="danger" />
                <Metric label={t("projectReports.dueSoon")} value={report.permits.dueSoonCount} />
                <Metric label={t("projectReports.expiring")} value={report.permits.expiringCount} />
              </div>
              <p className="text-xs text-muted-foreground">{t("projectReports.permits.window", { days: report.permits.dueSoonWindowDays })}</p>
              {report.permits.items.length > 0 ? (
                <ul className="space-y-2">
                  {report.permits.items.map((item) => (
                    <li key={item.id} className="flex flex-col gap-1 rounded-md border p-2.5 text-sm sm:flex-row sm:items-center sm:justify-between">
                      <DrillDownLink drillDown={item.drillDown}>{item.permitTypeCode}</DrillDownLink>
                      <span className="text-xs text-muted-foreground">
                        {t(statusKey("permit", item.status))} · {item.targetDeadline || item.expiresAt || "—"}
                      </span>
                    </li>
                  ))}
                </ul>
              ) : <p className="text-sm text-muted-foreground">{t("projectReports.permits.noItems")}</p>}
            </div>
          )}
        </ReportSection>
        </div>

        <ReportSection title={t("projectReports.finance.title")} icon={WalletCards}>
        <AvailabilityNotice availability={report.contractualFinance.availability} />
        {report.contractualFinance.availability === "Available" && (
          <div className="space-y-4">
            <div className="rounded-md border border-blue-200 bg-blue-50 p-3 text-sm text-blue-900 dark:border-blue-900 dark:bg-blue-950/30 dark:text-blue-100">
              <p className="font-medium">{t("projectReports.finance.contractualOnly")}</p>
              <p className="mt-1">{t("projectReports.finance.notCashRevenue")}</p>
            </div>
            <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
              <Metric label={t("projectReports.finance.quoteTotal")} value={formatVndWithSymbol(report.contractualFinance.quoteGrandTotal)} />
              <Metric label={t("projectReports.finance.baseContract")} value={formatVndWithSymbol(report.contractualFinance.contractBaseValue)} />
              <Metric label={t("projectReports.finance.approvedVo")} value={formatVndWithSymbol(report.contractualFinance.approvedVariationOrderDelta)} />
              <Metric label={t("projectReports.finance.currentContract")} value={formatVndWithSymbol(report.contractualFinance.contractCurrentValue)} />
            </div>
            <div className="grid gap-4 lg:grid-cols-2">
              <div>
                <p className="mb-2 text-sm font-medium">{t("projectReports.finance.quotesByStatus")}</p>
                <DictionaryMetrics
                  values={report.contractualFinance.quoteTotalsByStatus}
                  emptyLabel={t("projectReports.noStatusData")}
                  valueFormatter={formatVndWithSymbol}
                  labelFormatter={(status) => t(statusKey("quote", status))}
                />
              </div>
              <div>
                <p className="mb-2 text-sm font-medium">{t("projectReports.finance.milestoneSchedule")}</p>
                <p className="mb-2 text-xs text-muted-foreground">{t("projectReports.finance.milestoneExplanation")}</p>
                <DictionaryMetrics
                  values={report.contractualFinance.milestoneScheduledValuesByStatus}
                  emptyLabel={t("projectReports.finance.noMilestones")}
                  valueFormatter={formatVndWithSymbol}
                  labelFormatter={(status) => t(statusKey("milestone", status))}
                />
              </div>
            </div>
          </div>
        )}
        </ReportSection>

        {report.unavailableMetrics.length > 0 && (
          <Accordion type="single" collapsible className="rounded-xl border bg-card px-4">
            <AccordionItem value="unavailable" className="border-0">
              <AccordionTrigger className="text-sm">
                <span className="flex items-center gap-2"><AlertTriangle className="h-4 w-4 text-amber-600" aria-hidden />{t("projectReports.unavailableMetrics", { count: report.unavailableMetrics.length })}</span>
              </AccordionTrigger>
              <AccordionContent>
                <div className="grid gap-2 sm:grid-cols-2">
                  {report.unavailableMetrics.map((metric) => (
                    <div key={`${metric.metricCode}-${metric.reasonCode}`} className="rounded-md bg-muted/40 p-2.5 text-xs">
                      <p className="font-medium break-all">{metric.metricCode}</p>
                      <p className="mt-1 break-all text-muted-foreground">{metric.reasonCode}</p>
                    </div>
                  ))}
                </div>
              </AccordionContent>
            </AccordionItem>
          </Accordion>
        )}
      </AccordionContent>
    </AccordionItem>
  );
}

export default function ProjectReports() {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const [searchParams, setSearchParams] = useSearchParams();
  const [exporting, setExporting] = useState<ProjectReportExportParams["format"] | null>(null);

  const projectValue = searchParams.get("project") ?? "";
  const parsedProjectId = Number(projectValue);
  const projectId = Number.isInteger(parsedProjectId) && parsedProjectId > 0 ? parsedProjectId : undefined;
  const from = searchParams.get("from") ?? "";
  const to = searchParams.get("to") ?? "";
  const datesValid = (!from || isValidDate(from)) && (!to || isValidDate(to));
  const rangeValid = datesValid && (!from || !to || from <= to);
  const validationMessage = !datesValid
    ? t("projectReports.filters.invalidDate")
    : !rangeValid
      ? t("projectReports.filters.invalidRange")
      : null;

  const baseFilters: ProjectReportFilters = {
    from: from || undefined,
    to: to || undefined,
  };
  const portfolioQuery = useQuery({
    queryKey: ["project-reports", "portfolio", baseFilters.from, baseFilters.to],
    queryFn: async () => (await adminApi.getProjectReports(baseFilters)).data,
    enabled: rangeValid,
  });
  const selectedQuery = useQuery({
    queryKey: ["project-reports", "project", projectId, baseFilters.from, baseFilters.to],
    queryFn: async () => (await adminApi.getProjectReports({ ...baseFilters, projectId })).data,
    enabled: rangeValid && projectId != null,
  });
  const activeQuery = projectId ? selectedQuery : portfolioQuery;
  const projects = activeQuery.data?.projects ?? [];
  const projectOptions = portfolioQuery.data?.projects ?? activeQuery.data?.projects ?? [];
  const canExport = has(ADMIN_PERMS.projectReportsExport);

  const generatedAt = useMemo(() => {
    const value = activeQuery.data?.generatedAtUtc;
    return value ? new Intl.DateTimeFormat(LOCALES[lang], { dateStyle: "medium", timeStyle: "short" }).format(new Date(value)) : null;
  }, [activeQuery.data?.generatedAtUtc, lang]);

  const updateFilter = (key: "project" | "from" | "to", value: string) => {
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      if (value && value !== ALL_PROJECTS) next.set(key, value);
      else next.delete(key);
      return next;
    }, { replace: true });
  };

  const handleExport = async (format: ProjectReportExportParams["format"]) => {
    if (!canExport || !rangeValid) return;
    setExporting(format);
    try {
      const response = await adminApi.exportProjectReports({ ...baseFilters, projectId, format, language: lang });
      saveBlob(response.data, `project-operational-report-${new Date().toISOString().slice(0, 10)}.${format}`);
      toast({ title: t("projectReports.export.success") });
    } catch (error) {
      toast({ title: t("common.error"), description: extractApiError(error), variant: "destructive" });
    } finally {
      setExporting(null);
    }
  };

  return (
    <AdminLayout>
      <div className="space-y-5" data-testid="project-reports-page">
        <header className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-primary">{t("projectReports.eyebrow")}</p>
            <h1 className="mt-1 text-2xl font-bold tracking-tight sm:text-3xl">{t("projectReports.title")}</h1>
            <p className="mt-2 max-w-3xl text-sm text-muted-foreground">{t("projectReports.description")}</p>
            {generatedAt && <p className="mt-2 text-xs text-muted-foreground">{t("projectReports.generatedAt", { value: generatedAt })}</p>}
          </div>
          {canExport && (
            <div className="flex flex-wrap gap-2">
              <Button type="button" variant="outline" data-testid="project-reports-export-xlsx" onClick={() => void handleExport("xlsx")} disabled={!rangeValid || exporting != null}>
                {exporting === "xlsx" ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <FileSpreadsheet className="mr-2 h-4 w-4" />}
                {t("projectReports.export.xlsx")}
              </Button>
              <Button type="button" variant="outline" data-testid="project-reports-export-pdf" onClick={() => void handleExport("pdf")} disabled={!rangeValid || exporting != null}>
                {exporting === "pdf" ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Download className="mr-2 h-4 w-4" />}
                {t("projectReports.export.pdf")}
              </Button>
            </div>
          )}
        </header>

        <section className="rounded-xl border bg-card p-4 shadow-sm" aria-labelledby="project-report-filters">
          <h2 id="project-report-filters" className="mb-3 font-semibold">{t("projectReports.filters.title")}</h2>
          <div className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <Label htmlFor="report-project">{t("projectReports.filters.project")}</Label>
              <Select value={projectId ? String(projectId) : ALL_PROJECTS} onValueChange={(value) => updateFilter("project", value)}>
                <SelectTrigger id="report-project"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL_PROJECTS}>{t("projectReports.filters.allProjects")}</SelectItem>
                  {projectOptions.map(({ project }) => (
                    <SelectItem key={project.operationalProjectId} value={String(project.operationalProjectId)}>{project.code} · {project.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="report-from">{t("projectReports.filters.from")}</Label>
              <Input id="report-from" type="date" value={from} onChange={(event) => updateFilter("from", event.target.value)} aria-invalid={Boolean(validationMessage)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="report-to">{t("projectReports.filters.to")}</Label>
              <Input id="report-to" type="date" value={to} onChange={(event) => updateFilter("to", event.target.value)} aria-invalid={Boolean(validationMessage)} />
            </div>
          </div>
          {validationMessage && <p className="mt-3 text-sm font-medium text-destructive" role="alert">{validationMessage}</p>}
        </section>

        {!rangeValid ? null : activeQuery.isLoading ? (
          <PageLoading label={t("projectReports.loading")} />
        ) : activeQuery.isError ? (
          <PageError message={extractApiError(activeQuery.error)} onRetry={() => void activeQuery.refetch()} />
        ) : projects.length === 0 ? (
          <PageEmpty message={projectId ? t("projectReports.emptyProject") : t("projectReports.emptyPortfolio")} />
        ) : (
          <Accordion
            key={`${projectId ?? ALL_PROJECTS}-${from}-${to}`}
            type="multiple"
            defaultValue={projectId ? [String(projectId)] : []}
            className="space-y-3"
          >
            {projects.map((project) => <ProjectReportCard key={project.project.operationalProjectId} report={project} />)}
          </Accordion>
        )}
      </div>
    </AdminLayout>
  );
}
