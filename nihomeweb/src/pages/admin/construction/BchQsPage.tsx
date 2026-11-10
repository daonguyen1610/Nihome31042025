import { useI18n } from "@/lib/i18n";
import { ConstructionWorkspaceShell } from "./ConstructionWorkspaceShell";
import { QsBatchesPanel } from "./QsWorkspacePage";

export function BchQsPage() {
  const { t } = useI18n();

  return (
    <ConstructionWorkspaceShell
      title={t("construction.bch.qs.title")}
      description={t("construction.bch.qs.description")}
      testId="qs-page"
      touchFriendly
    >
      {({ projectId: operationalProjectId }) => (
        <QsBatchesPanel projectId={operationalProjectId} scope="execute" />
      )}
    </ConstructionWorkspaceShell>
  );
}
