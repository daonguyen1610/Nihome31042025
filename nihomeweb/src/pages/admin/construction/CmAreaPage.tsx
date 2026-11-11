import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Label } from "@/components/ui/label";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { Tabs, TabsContent, TabsTrigger } from "@/components/ui/tabs";
import { ScrollableTabsList } from "@/components/ui/scrollable-tabs-list";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import { AcceptanceRecordsPanel } from "./AcceptanceRecordsPage";
import { AsBuiltDocumentsPanel } from "./AsBuiltDocumentsPage";
import { CmContractsPanel } from "./CmContractsPanel";
import { CmPreparationSection } from "./CmPreparationSection";
import { CmScheduleWorkspace } from "./CmScheduleWorkspace";
import { ConstructionTasksPanel } from "./ConstructionTasksPage";
import {
  ConstructionWorkspaceShell,
  type ConstructionProjectScope,
} from "./ConstructionWorkspaceShell";
import { HandoverRecordsPanel } from "./HandoverRecordsPage";
import { HseViolationsPanel } from "./HseViolationsPage";
import { PunchListPanel } from "./PunchListPage";
import { QaQcDossiersPanel } from "./QaQcWorkspacePage";
import { NoDesignProjectNotice } from "./NoDesignProjectNotice";
import { QaQcTemplatesSection } from "./QaQcTemplatesSection";
import { QsBatchesPanel } from "./QsWorkspacePage";
import { SiteDiaryPanel } from "./SiteDiaryPage";
import { ResourceRequestsPanel } from "./BchResourceRequestsPage";
import { selectDesignProjectId } from "./constructionProjectSelection";

export type CmArea = "schedule" | "commercial" | "quality" | "safety" | "contracts";

const AREA_LABEL: Record<CmArea, string> = {
  schedule: "construction.cm.area.schedule",
  commercial: "construction.cm.area.quantityCost",
  quality: "construction.cm.area.quality",
  safety: "construction.cm.area.safety",
  contracts: "construction.cm.area.contracts",
};

const AREA_GUIDE: Record<CmArea, string> = {
  schedule: "cmAreas.scheduleGuide",
  commercial: "cmAreas.commercialGuide",
  quality: "cmAreas.qualityGuide",
  safety: "cmAreas.safetyGuide",
  contracts: "cmAreas.contractsGuide",
};

export function CmAreaPage({ area }: { area: CmArea }) {
  const { t } = useI18n();
  const { has } = usePermissions();
  const siteOnly =
    area === "schedule" &&
    !has(ADMIN_PERMS.constructionCmPreparation) &&
    !has(ADMIN_PERMS.constructionTasksManage);

  return (
    <ConstructionWorkspaceShell
      title={t(AREA_LABEL[area])}
      description={t(siteOnly ? "cmAreas.scheduleGuideSite" : AREA_GUIDE[area])}
      testId={`cm-${area}-page`}
    >
      {(scope) => <CmAreaContent area={area} scope={scope} />}
    </ConstructionWorkspaceShell>
  );
}

function CmAreaContent({ area, scope }: { area: CmArea; scope: ConstructionProjectScope }) {
  const { t } = useI18n();
  const { has } = usePermissions();
  const [searchParams, setSearchParams] = useSearchParams();
  const [qaQcTemplatesVersion, setQaQcTemplatesVersion] = useState(0);

  const tabs = areaTabs(area, has, t);
  const requested = searchParams.get("tab");
  const active =
    requested && tabs.some((tab) => tab.value === requested) ? requested : tabs[0]?.value;

  const requestedDesignProjectId = Number(searchParams.get("designProjectId"));
  const designProjectId = selectDesignProjectId(
    requestedDesignProjectId,
    scope.designProjectId,
    scope.designProjects,
  );

  const changeTab = (value: string) =>
    setSearchParams(
      (current) => {
        const next = new URLSearchParams(current);
        next.set("tab", value);
        return next;
      },
      { replace: true },
    );

  const changeDesignProject = (value: string) =>
    setSearchParams(
      (current) => {
        const next = new URLSearchParams(current);
        if (value) next.set("designProjectId", value);
        else next.delete("designProjectId");
        return next;
      },
      { replace: true },
    );

  if (area === "contracts") {
    return <CmContractsPanel projectId={scope.projectId} />;
  }
  if (area === "safety") {
    return <HseViolationsPanel projectId={scope.projectId} scope="review" />;
  }

  const showDesignProjectPicker =
    scope.designProjects.length > 1 && (area === "schedule" || area === "quality");

  return (
    <div className="space-y-4">
      {showDesignProjectPicker && (
        <div className="max-w-md space-y-1.5">
          <Label htmlFor="cm-design-project">{t("construction.common.designProject")}</Label>
          <SearchableSelect
            id="cm-design-project"
            value={designProjectId == null ? "" : String(designProjectId)}
            onChange={changeDesignProject}
            options={scope.designProjects.map((project) => ({
              value: String(project.id),
              label: `${project.projectCode} — ${project.name}`,
            }))}
            placeholder={t("construction.common.selectProject")}
            searchPlaceholder={t("common.search")}
          />
        </div>
      )}
      <Tabs value={active} onValueChange={changeTab} className="space-y-4">
        <ScrollableTabsList previousLabel={t("common.prev")} nextLabel={t("common.next")}>
          {tabs.map((tab) => (
            <TabsTrigger key={tab.value} value={tab.value}>
              {tab.label}
            </TabsTrigger>
          ))}
        </ScrollableTabsList>

        {area === "schedule" && (
          <>
            {tabs.some((tab) => tab.value === "plan") && (
              <TabsContent value="plan" className="space-y-5">
                <CmScheduleWorkspace key={scope.projectId} projectId={scope.projectId} standalone />
                {has(ADMIN_PERMS.constructionCmPreparation) && (
                  <CmPreparationSection projectId={scope.projectId} />
                )}
              </TabsContent>
            )}
            {tabs.some((tab) => tab.value === "tasks") && (
              <TabsContent value="tasks">
                <ConstructionTasksPanel
                  designProjectId={designProjectId}
                  designProjects={scope.designProjects}
                  scope="review"
                />
              </TabsContent>
            )}
            {tabs.some((tab) => tab.value === "diary") && (
              <TabsContent value="diary">
                {designProjectId == null ? (
                  <NoDesignProjectNotice />
                ) : (
                  <SiteDiaryPanel
                    designProjectId={designProjectId}
                    designProjects={scope.designProjects}
                    scope="review"
                  />
                )}
              </TabsContent>
            )}
          </>
        )}

        {area === "commercial" && (
          <>
            {tabs.some((tab) => tab.value === "resources") && (
              <TabsContent value="resources">
                <ResourceRequestsPanel projectId={scope.projectId} scope="review" />
              </TabsContent>
            )}
            {tabs.some((tab) => tab.value === "qs") && (
              <TabsContent value="qs">
                <QsBatchesPanel projectId={scope.projectId} scope="review" />
              </TabsContent>
            )}
          </>
        )}

        {area === "quality" && (
          <>
            {tabs.some((tab) => tab.value === "dossiers") && (
              <TabsContent value="dossiers" className="space-y-6">
                <QaQcDossiersPanel
                  projectId={scope.projectId}
                  scope="review"
                  templatesVersion={qaQcTemplatesVersion}
                />
                {has(ADMIN_PERMS.constructionQaQcTemplateManage) && (
                  <QaQcTemplatesSection
                    projectId={scope.projectId}
                    onChanged={() => setQaQcTemplatesVersion((value) => value + 1)}
                  />
                )}
              </TabsContent>
            )}
            {tabs.some((tab) => tab.value === "punchlist") && (
              <TabsContent value="punchlist">
                {designProjectId == null ? (
                  <NoDesignProjectNotice />
                ) : (
                  <PunchListPanel
                    designProjectId={designProjectId}
                    designProjects={scope.designProjects}
                    operationalProjectId={scope.projectId}
                    scope="review"
                  />
                )}
              </TabsContent>
            )}
            {tabs.some((tab) => tab.value === "acceptance") && (
              <TabsContent value="acceptance">
                {designProjectId == null ? (
                  <NoDesignProjectNotice />
                ) : (
                  <AcceptanceRecordsPanel
                    designProjectId={designProjectId}
                    designProjects={scope.designProjects}
                    scope="review"
                  />
                )}
              </TabsContent>
            )}
            {tabs.some((tab) => tab.value === "asbuilt") && (
              <TabsContent value="asbuilt">
                {designProjectId == null ? (
                  <NoDesignProjectNotice />
                ) : (
                  <AsBuiltDocumentsPanel designProjectId={designProjectId} scope="review" />
                )}
              </TabsContent>
            )}
            {tabs.some((tab) => tab.value === "handover") && (
              <TabsContent value="handover">
                {designProjectId == null ? (
                  <NoDesignProjectNotice />
                ) : (
                  <HandoverRecordsPanel designProjectId={designProjectId} scope="review" />
                )}
              </TabsContent>
            )}
          </>
        )}
      </Tabs>
    </div>
  );
}

type TabDef = { value: string; label: string };

function areaTabs(
  area: CmArea,
  has: (permission: string) => boolean,
  t: (key: string) => string,
): TabDef[] {
  if (area === "schedule") {
    return [
      ...(has(ADMIN_PERMS.constructionCmPreparation) || has(ADMIN_PERMS.constructionTasks)
        ? [{ value: "plan", label: t("cmAreas.tab.plan") }]
        : []),
      ...(has(ADMIN_PERMS.constructionTasks)
        ? [{ value: "tasks", label: t("nav.constructionTasks") }]
        : []),
      ...(has(ADMIN_PERMS.constructionDiary)
        ? [{ value: "diary", label: t("nav.constructionDiary") }]
        : []),
    ];
  }
  if (area === "commercial") {
    return [
      ...(has(ADMIN_PERMS.constructionResourceRequests) ||
      has(ADMIN_PERMS.constructionResourceRequestsReview)
        ? [{ value: "resources", label: t("nav.constructionResourceRequests") }]
        : []),
      ...(has(ADMIN_PERMS.constructionQs) || has(ADMIN_PERMS.constructionQsReview)
        ? [{ value: "qs", label: t("nav.constructionQs") }]
        : []),
    ];
  }
  if (area === "quality") {
    return [
      ...(has(ADMIN_PERMS.constructionQaQc) || has(ADMIN_PERMS.constructionQaQcReview)
        ? [{ value: "dossiers", label: t("nav.constructionQaQc") }]
        : []),
      ...(has(ADMIN_PERMS.constructionPunch) || has(ADMIN_PERMS.constructionPunchVerify)
        ? [{ value: "punchlist", label: t("nav.constructionPunch") }]
        : []),
      ...(has(ADMIN_PERMS.constructionAcceptance) || has(ADMIN_PERMS.constructionAcceptanceApprove)
        ? [{ value: "acceptance", label: t("nav.constructionAcceptance") }]
        : []),
      ...(has(ADMIN_PERMS.constructionAsBuilt) || has(ADMIN_PERMS.constructionAsBuiltApprove)
        ? [{ value: "asbuilt", label: t("nav.constructionAsBuilt") }]
        : []),
      ...(has(ADMIN_PERMS.constructionHandover) || has(ADMIN_PERMS.constructionHandoverComplete)
        ? [{ value: "handover", label: t("nav.constructionHandover") }]
        : []),
    ];
  }
  return [];
}
