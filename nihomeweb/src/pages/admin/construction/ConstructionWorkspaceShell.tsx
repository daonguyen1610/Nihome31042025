import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { collectLookupPages } from "@/lib/pagination";
import { ReactNode, useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageError, PageLoading } from "@/components/PageState";
import { Label } from "@/components/ui/label";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { adminApi, type OperationalProjectListItemResponse } from "@/services/adminApi";

interface ConstructionWorkspaceShellProps {
  title: string;
  description: string;
  projectId: number | null;
  onProjectChange: (projectId: number) => void;
  children: ReactNode;
  testId: string;
}

export function ConstructionWorkspaceShell({
  title,
  description,
  projectId,
  onProjectChange,
  children,
  testId,
}: ConstructionWorkspaceShellProps) {
  const uiError = useUiError();
  const { t } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedProjectId = Number(searchParams.get("projectId"));
  const [projects, setProjects] = useState<OperationalProjectListItemResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    void collectLookupPages((page) => adminApi.listOperationalProjects({ pageSize: 200, page }))
      .then(async ({ data }) => {
        if (!active) return;
        const items = [...data.items];
        if (requestedProjectId > 0 && !items.some((project) => project.id === requestedProjectId)) {
          const requested = await adminApi.getOperationalProject(requestedProjectId);
          if (!active) return;
          items.push(requested.data);
        }
        setProjects(items);
        if (projectId == null && items.length > 0) {
          const requested = items.find((project) => project.id === requestedProjectId);
          onProjectChange(requested?.id ?? items[0].id);
        }
      })
      .catch((cause) => {
        if (active) setError(uiError(cause, t("construction.common.projectsLoadError")));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [onProjectChange, projectId, reloadKey, requestedProjectId, t, uiError]);

  return (
    <AdminLayout>
      <main className="mx-auto w-full max-w-[1500px] space-y-5 p-4 sm:p-6" data-testid={testId}>
        <header className="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
          <div className="min-w-0 space-y-2">
            <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
            <p className="max-w-3xl text-sm text-muted-foreground">{description}</p>
          </div>
          <div className="w-full min-w-0 space-y-1.5 lg:w-[26rem] lg:shrink-0">
            <Label htmlFor={`${testId}-project`}>{t("construction.common.project")}</Label>
            <SearchableSelect
              id={`${testId}-project`}
              data-testid={`${testId}-project`}
              ariaLabel={t("construction.common.project")}
              value={projectId == null ? "" : String(projectId)}
              onChange={(value) => {
                onProjectChange(Number(value));
                setSearchParams(
                  (current) => {
                    const next = new URLSearchParams(current);
                    next.set("projectId", value);
                    return next;
                  },
                  { replace: true },
                );
              }}
              options={projects.map((project) => ({
                value: String(project.id),
                label: `${project.code} · ${project.name}`,
              }))}
              placeholder={t("construction.common.selectProject")}
              searchPlaceholder={t("common.search")}
              disabled={loading || projects.length === 0}
              className="h-auto min-h-11 whitespace-normal py-2 text-left [&>span>span]:whitespace-normal [&>span>span]:break-words [&>span>span]:overflow-visible"
            />
          </div>
        </header>

        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => setReloadKey((value) => value + 1)} />
        ) : projects.length === 0 ? (
          <PageError message={t("construction.common.noProjects")} />
        ) : (
          children
        )}
      </main>
    </AdminLayout>
  );
}
