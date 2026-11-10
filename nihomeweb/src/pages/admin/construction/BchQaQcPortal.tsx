import { useSearchParams } from "react-router-dom";
import { Tabs, TabsContent, TabsTrigger } from "@/components/ui/tabs";
import { ScrollableTabsList } from "@/components/ui/scrollable-tabs-list";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import { AcceptanceRecordsPanel } from "./AcceptanceRecordsPage";
import { ConstructionWorkspaceShell } from "./ConstructionWorkspaceShell";
import { NoDesignProjectNotice } from "./NoDesignProjectNotice";
import { HseViolationsPanel } from "./HseViolationsPage";
import { PunchListPanel } from "./PunchListPage";
import { QaQcDossiersPanel } from "./QaQcWorkspacePage";

type Tab = "dossiers" | "punchlist" | "acceptance" | "hse";

export function BchQaQcPortal() {
  const { t } = useI18n();
  const { has } = usePermissions();
  const [searchParams, setSearchParams] = useSearchParams();

  const allowed: { value: Tab; label: string }[] = [];
  if (has(ADMIN_PERMS.constructionQaQc))
    allowed.push({ value: "dossiers", label: t("nav.constructionQaQc") });
  if (has(ADMIN_PERMS.constructionPunch))
    allowed.push({ value: "punchlist", label: t("nav.constructionPunch") });
  if (has(ADMIN_PERMS.constructionAcceptance))
    allowed.push({ value: "acceptance", label: t("nav.constructionAcceptance") });
  if (has(ADMIN_PERMS.constructionHse))
    allowed.push({ value: "hse", label: t("nav.constructionHse") });

  const requested = searchParams.get("tab") as Tab | null;
  const active =
    requested && allowed.some((tab) => tab.value === requested) ? requested : allowed[0]?.value;

  const changeTab = (value: string) =>
    setSearchParams(
      (current) => {
        const next = new URLSearchParams(current);
        next.set("tab", value);
        return next;
      },
      { replace: true },
    );

  return (
    <ConstructionWorkspaceShell
      title={t("construction.bch.qaQc.title")}
      description={t("construction.bch.qaQc.description")}
      testId="qa-qc-page"
      touchFriendly
    >
      {({ projectId: operationalProjectId, designProjectId, designProjects }) => {
        const effectiveDesignProjectId = designProjectId ?? designProjects[0]?.id ?? null;
        return (
          <Tabs value={active} onValueChange={changeTab} className="space-y-4">
            <ScrollableTabsList previousLabel={t("common.prev")} nextLabel={t("common.next")}>
              {allowed.map((tab) => (
                <TabsTrigger key={tab.value} value={tab.value}>
                  {tab.label}
                </TabsTrigger>
              ))}
            </ScrollableTabsList>
            {has(ADMIN_PERMS.constructionQaQc) && (
              <TabsContent value="dossiers">
                <QaQcDossiersPanel projectId={operationalProjectId} scope="execute" />
              </TabsContent>
            )}
            {has(ADMIN_PERMS.constructionPunch) && (
              <TabsContent value="punchlist">
                {effectiveDesignProjectId == null ? (
                  <NoDesignProjectNotice />
                ) : (
                  <PunchListPanel
                    designProjectId={effectiveDesignProjectId}
                    designProjects={designProjects}
                    operationalProjectId={operationalProjectId}
                    scope="execute"
                  />
                )}
              </TabsContent>
            )}
            {has(ADMIN_PERMS.constructionAcceptance) && (
              <TabsContent value="acceptance">
                {effectiveDesignProjectId == null ? (
                  <NoDesignProjectNotice />
                ) : (
                  <AcceptanceRecordsPanel
                    designProjectId={effectiveDesignProjectId}
                    designProjects={designProjects}
                    scope="execute"
                  />
                )}
              </TabsContent>
            )}
            {has(ADMIN_PERMS.constructionHse) && (
              <TabsContent value="hse">
                <HseViolationsPanel projectId={operationalProjectId} scope="execute" />
              </TabsContent>
            )}
          </Tabs>
        );
      }}
    </ConstructionWorkspaceShell>
  );
}
