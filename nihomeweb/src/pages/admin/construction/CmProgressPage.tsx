import { useCallback, useState } from "react";
import { useI18n } from "@/lib/i18n";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { PageError } from "@/components/PageState";
import { ConstructionWorkspaceShell } from "./ConstructionWorkspaceShell";
import { CmScheduleWorkspace } from "./CmScheduleWorkspace";

export default function CmProgressPage() {
  const { t } = useI18n();
  const { has } = usePermissions();
  const [projectId, setProjectId] = useState<number | null>(null);
  const selectProject = useCallback((id: number) => setProjectId(id), []);
  return (
    <ConstructionWorkspaceShell
      title={t("cmSchedule.navigation")}
      description={t("cmSchedule.pageDescription")}
      projectId={projectId}
      onProjectChange={selectProject}
      testId="cm-progress-page"
    >
      {has(ADMIN_PERMS.constructionTasks) ? (
        projectId !== null && (
          <CmScheduleWorkspace key={projectId} projectId={projectId} standalone />
        )
      ) : (
        <PageError message={t("forbidden.description")} />
      )}
    </ConstructionWorkspaceShell>
  );
}
