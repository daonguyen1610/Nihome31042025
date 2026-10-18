import { ReactNode, useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageError, PageLoading } from "@/components/PageState";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
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
  const { t } = useI18n();
  const [searchParams] = useSearchParams();
  const requestedProjectId = Number(searchParams.get("projectId"));
  const [projects, setProjects] = useState<OperationalProjectListItemResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    void adminApi
      .listOperationalProjects({ pageSize: 200 })
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
        if (active) setError(extractApiError(cause, t("construction.common.projectsLoadError")));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [onProjectChange, projectId, reloadKey, requestedProjectId, t]);

  return (
    <AdminLayout>
      <main className="mx-auto w-full max-w-[1500px] space-y-5 p-4 sm:p-6" data-testid={testId}>
        <header className="flex flex-col gap-4 rounded-xl border bg-card p-4 shadow-sm sm:p-6 lg:flex-row lg:items-end lg:justify-between">
          <div className="space-y-1">
            <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
            <p className="max-w-3xl text-sm text-muted-foreground">{description}</p>
          </div>
          <div className="w-full space-y-1.5 lg:w-80">
            <Label htmlFor={`${testId}-project`}>{t("construction.common.project")}</Label>
            <Select
              value={projectId == null ? "" : String(projectId)}
              onValueChange={(value) => onProjectChange(Number(value))}
              disabled={loading || projects.length === 0}
            >
              <SelectTrigger id={`${testId}-project`} data-testid={`${testId}-project`}>
                <SelectValue placeholder={t("construction.common.selectProject")} />
              </SelectTrigger>
              <SelectContent>
                {projects.map((project) => (
                  <SelectItem key={project.id} value={String(project.id)}>
                    {project.code} · {project.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
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
