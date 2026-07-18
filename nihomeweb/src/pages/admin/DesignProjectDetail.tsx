import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft, PenTool } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { extractApiError } from "@/lib/apiError";
import { PageLoading, PageError } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from "@/components/ui/tabs";
import {
  adminApi,
  type DesignProjectResponse,
  type DesignProjectStage,
  type DesignProjectStatus,
} from "@/services/adminApi";
import { ConceptOptionsTab } from "./design/ConceptOptionsTab";
import { BasicDesignTab } from "./design/BasicDesignTab";

const STAGE_BADGE: Record<DesignProjectStage, string> = {
  Concept: "border-sky-200 bg-sky-50 text-sky-700",
  BasicDesign: "border-indigo-200 bg-indigo-50 text-indigo-700",
  ShopDrawing: "border-violet-200 bg-violet-50 text-violet-700",
  Completed: "border-emerald-200 bg-emerald-50 text-emerald-700",
};

const STATUS_BADGE: Record<DesignProjectStatus, string> = {
  Active: "border-emerald-200 bg-emerald-50 text-emerald-700",
  OnHold: "border-amber-200 bg-amber-50 text-amber-700",
  Completed: "border-slate-200 bg-slate-50 text-slate-700",
  Cancelled: "border-rose-200 bg-rose-50 text-rose-700",
};

const STAGE_ORDER: DesignProjectStage[] = ["Concept", "BasicDesign", "ShopDrawing", "Completed"];

const formatDate = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString(lang);
  } catch {
    return iso;
  }
};

const formatDateTime = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try {
    const d = new Date(iso);
    return `${d.toLocaleTimeString(lang, { hour: "2-digit", minute: "2-digit", second: "2-digit" })} ${d.toLocaleDateString(lang)}`;
  } catch {
    return iso;
  }
};

const AdminDesignProjectDetail = () => {
  const { t, lang } = useI18n();
  const { id: idParam } = useParams<{ id: string }>();
  const numericId = Number(idParam);
  const isValidId = Number.isFinite(numericId) && numericId > 0;

  const [project, setProject] = useState<DesignProjectResponse | null>(null);
  const [loading, setLoading] = useState(isValidId);
  const [error, setError] = useState<string | null>(null);

  const fetchProject = useCallback(async () => {
    if (!isValidId) return;
    // Only show the full-page spinner on the first load. Later refetches
    // (e.g. after finalizing a concept option) must keep the current
    // content on screen so the Tabs component doesn't remount and reset
    // the selected tab back to Overview.
    setError(null);
    try {
      const { data } = await adminApi.getDesignProject(numericId);
      setProject(data);
    } catch (err) {
      const status = (err as { response?: { status?: number } })?.response?.status;
      if (status === 404) {
        setProject(null);
      } else {
        setError(extractApiError(err));
      }
    } finally {
      setLoading(false);
    }
  }, [isValidId, numericId]);

  useEffect(() => {
    void fetchProject();
  }, [fetchProject]);

  const infoRows = useMemo(() => {
    if (!project) return [] as { label: string; value: string }[];
    return [
      { label: t("designProjects.field.customer"), value: project.customerName ?? `#${project.customerId}` },
      {
        label: t("designProjects.field.contract"),
        value: project.contractNumber ?? (project.contractId ? `#${project.contractId}` : "—"),
      },
      { label: t("designProjects.field.pm"), value: project.projectManagerName ?? "—" },
      { label: t("designProjects.field.designLead"), value: project.designLeadName ?? "—" },
      { label: t("designProjects.field.startDate"), value: formatDate(project.startDate, lang) },
      { label: t("designProjects.field.deadline"), value: formatDate(project.deadline, lang) },
      { label: t("designProjects.field.createdAt"), value: formatDateTime(project.createdAt, lang) },
      { label: t("designProjects.field.updatedAt"), value: formatDateTime(project.updatedAt, lang) },
    ];
  }, [project, t, lang]);

  if (!isValidId) {
    return (
      <AdminLayout>
        <div className="p-4">
          <PageError message={t("common.notFound")} />
        </div>
      </AdminLayout>
    );
  }

  if (loading) {
    return (
      <AdminLayout>
        <div className="p-4">
          <PageLoading />
        </div>
      </AdminLayout>
    );
  }

  if (error) {
    return (
      <AdminLayout>
        <div className="p-4">
          <PageError message={error} onRetry={() => void fetchProject()} />
        </div>
      </AdminLayout>
    );
  }

  if (!project) {
    return (
      <AdminLayout>
        <div className="p-4">
          <PageError message={t("common.notFound")} />
        </div>
      </AdminLayout>
    );
  }

  const stageIndex = STAGE_ORDER.indexOf(project.currentStage);

  return (
    <AdminLayout>
      <div className="space-y-4 p-3 md:p-4">
        <div>
          <Link
            to="/admin/design-projects"
            className="inline-flex items-center gap-1 text-sm text-slate-500 hover:text-slate-800"
          >
            <ArrowLeft className="h-4 w-4" />
            {t("designProjects.detail.back")}
          </Link>
        </div>

        <header className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="flex min-w-0 items-center gap-3">
              <PenTool className="h-6 w-6 text-slate-500" />
              <div className="min-w-0">
                <h1 className="text-lg font-bold text-slate-900 md:text-2xl">
                  {project.projectCode}
                </h1>
                <p className="text-sm text-slate-600">{project.name}</p>
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="outline" className={cn("whitespace-nowrap", STAGE_BADGE[project.currentStage])}>
                {t(`designProjects.stage.${project.currentStage}`)}
              </Badge>
              <Badge variant="outline" className={cn("whitespace-nowrap", STATUS_BADGE[project.status])}>
                {t(`designProjects.status.${project.status}`)}
              </Badge>
            </div>
          </div>

          {/* 3-stage progress strip */}
          <div className="mt-4">
            <p className="mb-2 text-xs uppercase tracking-wide text-slate-500">
              {t("designProjects.detail.progress")}
            </p>
            <div className="grid grid-cols-3 gap-2">
              {STAGE_ORDER.slice(0, 3).map((s, i) => {
                const active = i <= stageIndex;
                return (
                  <div
                    key={s}
                    className={cn(
                      "rounded-md border px-2 py-1.5 text-center text-xs font-medium",
                      active
                        ? "border-slate-800 bg-slate-800 text-white"
                        : "border-slate-200 bg-slate-50 text-slate-500",
                    )}
                  >
                    {t(`designProjects.stage.${s}`)}
                  </div>
                );
              })}
            </div>
          </div>
        </header>

        <Tabs defaultValue="overview">
          <TabsList>
            <TabsTrigger value="overview">{t("designProjects.detail.tab.overview")}</TabsTrigger>
            <TabsTrigger value="concept">{t("designProjects.detail.tab.concept")}</TabsTrigger>
            <TabsTrigger value="basic">{t("designProjects.detail.tab.basic")}</TabsTrigger>
            <TabsTrigger value="shop">{t("designProjects.detail.tab.shop")}</TabsTrigger>
            <TabsTrigger value="team">{t("designProjects.detail.tab.team")}</TabsTrigger>
            <TabsTrigger value="docs">{t("designProjects.detail.tab.docs")}</TabsTrigger>
          </TabsList>

          <TabsContent value="overview">
            <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
              <dl className="grid grid-cols-1 gap-4 md:grid-cols-2">
                {infoRows.map((r) => (
                  <div key={r.label}>
                    <dt className="text-xs uppercase tracking-wide text-slate-500">{r.label}</dt>
                    <dd className="mt-0.5 text-sm text-slate-900">{r.value}</dd>
                  </div>
                ))}
              </dl>
              {project.note ? (
                <div className="mt-4">
                  <p className="text-xs uppercase tracking-wide text-slate-500">
                    {t("designProjects.field.note")}
                  </p>
                  <p className="mt-1 whitespace-pre-wrap text-sm text-slate-800">{project.note}</p>
                </div>
              ) : null}
            </div>
          </TabsContent>

          {(["concept", "basic", "shop", "team", "docs"] as const).map((tab) => (
            <TabsContent key={tab} value={tab}>
              {tab === "concept" ? (
                <ConceptOptionsTab project={project} onProjectMayHaveChanged={fetchProject} />
              ) : tab === "basic" ? (
                <BasicDesignTab project={project} onProjectMayHaveChanged={fetchProject} />
              ) : (
                <div className="rounded-lg border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-500">
                  {t("designProjects.detail.stageComingSoon")}
                </div>
              )}
            </TabsContent>
          ))}
        </Tabs>
      </div>
    </AdminLayout>
  );
};

export default AdminDesignProjectDetail;
