import { useSearchParams } from "react-router-dom";
import { Tabs, TabsContent, TabsTrigger } from "@/components/ui/tabs";
import { ScrollableTabsList } from "@/components/ui/scrollable-tabs-list";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import { ResourceRequestsPanel } from "./BchResourceRequestsPage";
import { ConstructionWorkspaceShell } from "./ConstructionWorkspaceShell";
import { NoDesignProjectNotice } from "./NoDesignProjectNotice";
import { SiteDiaryPanel } from "./SiteDiaryPage";

type Tab = "resources" | "diary";

export function BchOperationsPortal() {
  const { t } = useI18n();
  const { has } = usePermissions();
  const [searchParams, setSearchParams] = useSearchParams();

  const canResources = has(ADMIN_PERMS.constructionResourceRequests);
  const canDiary = has(ADMIN_PERMS.constructionDiary);
  const tabs: Tab[] = [
    ...(canResources ? (["resources"] as const) : []),
    ...(canDiary ? (["diary"] as const) : []),
  ];
  const requested = searchParams.get("tab") as Tab | null;
  const active = requested && tabs.includes(requested) ? requested : tabs[0];

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
      title={t("construction.bch.operations.title")}
      description={t("construction.bch.operations.description")}
      testId="bch-resource-page"
      touchFriendly
    >
      {({ projectId: operationalProjectId, designProjectId, designProjects }) => {
        const effectiveDesignProjectId = designProjectId ?? designProjects[0]?.id ?? null;
        return (
          <Tabs value={active} onValueChange={changeTab} className="space-y-4">
            <ScrollableTabsList previousLabel={t("common.prev")} nextLabel={t("common.next")}>
              {canResources && (
                <TabsTrigger value="resources">{t("nav.constructionResourceRequests")}</TabsTrigger>
              )}
              {canDiary && <TabsTrigger value="diary">{t("nav.constructionDiary")}</TabsTrigger>}
            </ScrollableTabsList>
            {canResources && (
              <TabsContent value="resources">
                <ResourceRequestsPanel projectId={operationalProjectId} scope="execute" />
              </TabsContent>
            )}
            {canDiary && (
              <TabsContent value="diary">
                {effectiveDesignProjectId == null ? (
                  <NoDesignProjectNotice />
                ) : (
                  <SiteDiaryPanel
                    designProjectId={effectiveDesignProjectId}
                    designProjects={designProjects}
                  />
                )}
              </TabsContent>
            )}
          </Tabs>
        );
      }}
    </ConstructionWorkspaceShell>
  );
}
