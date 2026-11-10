import { useUiError } from "@/hooks/useUiError";
import { collectLookupPages } from "@/lib/pagination";
import { ReactNode, useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import { cn } from "@/lib/utils";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { Label } from "@/components/ui/label";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { useI18n } from "@/lib/i18n";
import { adminApi, type OperationalProjectListItemResponse } from "@/services/adminApi";
import { selectAccessibleProjectId } from "./constructionProjectSelection";

export interface ConstructionProjectScope {
  projectId: number;
  designProjectId: number | null;
  designProjects: { id: number; projectCode: string; name: string }[];
}

interface ConstructionWorkspaceShellProps {
  title: string;
  description: string;
  children: (scope: ConstructionProjectScope) => ReactNode;
  testId: string;
  touchFriendly?: boolean;
}

export function ConstructionWorkspaceShell({
  title,
  description,
  children,
  testId,
  touchFriendly,
}: ConstructionWorkspaceShellProps) {
  const uiError = useUiError();
  const { t } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedProjectId = Number(searchParams.get("projectId"));
  const [projects, setProjects] = useState<OperationalProjectListItemResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);
  const [scope, setScope] = useState<ConstructionProjectScope | null>(null);
  const [scopeError, setScopeError] = useState<string | null>(null);

  const selectedProjectId = selectAccessibleProjectId(requestedProjectId, projects);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    void collectLookupPages((page) => adminApi.listOperationalProjects({ pageSize: 200, page }))
      .then(({ data }) => {
        if (!active) return;
        const items = [...data.items];
        setProjects(items);
        const next = selectAccessibleProjectId(requestedProjectId, items);
        if (next != null && next !== requestedProjectId) {
          setSearchParams(
            (current) => {
              const params = new URLSearchParams(current);
              params.set("projectId", String(next));
              return params;
            },
            { replace: true },
          );
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
  }, [reloadKey, requestedProjectId, setSearchParams, t, uiError]);

  useEffect(() => {
    if (selectedProjectId == null) {
      setScope(null);
      return;
    }
    let active = true;
    setScope(null);
    setScopeError(null);
    void adminApi
      .getOperationalProject(selectedProjectId)
      .then(({ data }) => {
        if (!active) return;
        setScope({
          projectId: selectedProjectId,
          designProjectId: data.designProjectId ?? null,
          designProjects: (data.designProjects ?? []).map((project) => ({
            id: project.id,
            projectCode: project.projectCode,
            name: project.name,
          })),
        });
      })
      .catch((cause) => {
        if (active) setScopeError(uiError(cause, t("construction.common.projectsLoadError")));
      });
    return () => {
      active = false;
    };
  }, [reloadKey, selectedProjectId, t, uiError]);

  return (
    <AdminLayout>
      <div className={cn("space-y-5", touchFriendly && "construction-touch")} data-testid={testId}>
        <header className="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
          <div className="min-w-0 space-y-2">
            <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
            <p className="max-w-3xl text-sm text-muted-foreground">{description}</p>
          </div>
          <div className="w-full min-w-0 space-y-1.5 lg:w-[26rem] lg:shrink-0">
            <Label htmlFor={`${testId}-project`}>
              {t("construction.common.operationalProject")}
            </Label>
            <SearchableSelect
              id={`${testId}-project`}
              data-testid={`${testId}-project`}
              ariaLabel={t("construction.common.operationalProject")}
              value={selectedProjectId == null ? "" : String(selectedProjectId)}
              onChange={(value) =>
                setSearchParams(
                  (current) => {
                    const next = new URLSearchParams(current);
                    next.set("projectId", value);
                    next.delete("designProjectId");
                    return next;
                  },
                  { replace: true },
                )
              }
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
          <PageEmpty message={t("construction.common.noProjects")} />
        ) : selectedProjectId == null || (!scope && !scopeError) ? (
          <PageLoading />
        ) : scopeError ? (
          <PageError message={scopeError} onRetry={() => setReloadKey((value) => value + 1)} />
        ) : scope ? (
          children(scope)
        ) : null}
      </div>
    </AdminLayout>
  );
}
