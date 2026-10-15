import { FormEvent, type ReactNode, useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Check, ClipboardCheck, FilePlus2, Play, RefreshCcw, Save, X } from "lucide-react";
import { ConstructionWorkspaceShell } from "./ConstructionWorkspaceShell";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type ConstructionPreparationResponse,
  type ConstructionQsBatchResponse,
  type ConstructionResourceRequestResponse,
  type QaQcDossierResponse,
  type QaQcTemplateResponse,
  type ProjectDocumentResponse,
} from "@/services/adminApi";

type ReviewTarget =
  | { kind: "resource"; row: ConstructionResourceRequestResponse }
  | { kind: "qaQc"; row: QaQcDossierResponse }
  | { kind: "qs"; row: ConstructionQsBatchResponse };

const referenceFields = [
  "wbsBaselineReference",
  "scheduleBaselineReference",
  "sCurveBaselineReference",
  "bchOrganizationReference",
  "resourcePlanReference",
  "budgetBaselineReference",
] as const;

const baselineDocumentFields = [
  ["wbsBaselineDocumentId", "wbsBaselineReference"],
  ["scheduleBaselineDocumentId", "scheduleBaselineReference"],
  ["sCurveBaselineDocumentId", "sCurveBaselineReference"],
  ["bchOrganizationDocumentId", "bchOrganizationReference"],
  ["resourcePlanDocumentId", "resourcePlanReference"],
  ["budgetBaselineDocumentId", "budgetBaselineReference"],
] as const;

export default function CmConstructionWorkspacePage() {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canViewPreparation = has(ADMIN_PERMS.constructionCmPreparation);
  const canViewResources = has(ADMIN_PERMS.constructionResourceRequests);
  const canViewQaQc = has(ADMIN_PERMS.constructionQaQc);
  const canViewQs = has(ADMIN_PERMS.constructionQs);
  const [projectId, setProjectId] = useState<number | null>(null);
  const selectProject = useCallback((id: number) => setProjectId(id), []);
  const [preparation, setPreparation] = useState<ConstructionPreparationResponse | null>(null);
  const [resources, setResources] = useState<ConstructionResourceRequestResponse[]>([]);
  const [dossiers, setDossiers] = useState<QaQcDossierResponse[]>([]);
  const [batches, setBatches] = useState<ConstructionQsBatchResponse[]>([]);
  const [templates, setTemplates] = useState<QaQcTemplateResponse[]>([]);
  const [projectDocuments, setProjectDocuments] = useState<ProjectDocumentResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  const [acting, setActing] = useState(false);
  const [reviewTarget, setReviewTarget] = useState<ReviewTarget | null>(null);
  const [approved, setApproved] = useState(true);
  const [reviewReason, setReviewReason] = useState("");
  const [templateOpen, setTemplateOpen] = useState(false);
  const [templateForm, setTemplateForm] = useState({
    code: "",
    nameVi: "",
    name: "",
    nameZh: "",
    nameJa: "",
    schemaJson: "{}",
  });

  useEffect(() => {
    if (projectId == null) return;
    let active = true;
    setLoading(true);
    setError(null);
    void Promise.all([
      canViewPreparation
        ? adminApi.getConstructionPreparation(projectId).catch((cause) => {
            if ((cause as { response?: { status?: number } }).response?.status === 404) return null;
            throw cause;
          })
        : Promise.resolve(null),
      canViewResources
        ? adminApi.listConstructionResourceRequests(projectId)
        : Promise.resolve(null),
      canViewQaQc ? adminApi.listQaQcDossiers(projectId) : Promise.resolve(null),
      canViewQs ? adminApi.listConstructionQsBatches(projectId) : Promise.resolve(null),
      canViewQaQc ? adminApi.listQaQcTemplates(projectId) : Promise.resolve(null),
      canViewPreparation ? adminApi.listProjectDocuments(projectId) : Promise.resolve(null),
    ])
      .then(([prep, resourceList, dossierList, batchList, templateList, documentsList]) => {
        if (!active) return;
        setPreparation(
          prep?.data ??
            (canViewPreparation
              ? {
                  id: 0,
                  operationalProjectId: projectId,
                  version: 1,
                  status: "Draft",
                  wbsBaselineReference: "",
                  scheduleBaselineReference: "",
                  sCurveBaselineReference: "",
                  bchOrganizationReference: "",
                  resourcePlanReference: "",
                  budgetBaselineReference: "",
                  wbsBaselineDocumentId: null,
                  scheduleBaselineDocumentId: null,
                  sCurveBaselineDocumentId: null,
                  bchOrganizationDocumentId: null,
                  resourcePlanDocumentId: null,
                  budgetBaselineDocumentId: null,
                  budgetBaselineLocked: false,
                  siteHandoverRequired: false,
                  siteHandoverComplete: false,
                  methodStatementRequired: false,
                  methodStatementApproved: false,
                  hsePlanRequired: false,
                  hsePlanApproved: false,
                  siteHandoverDocumentId: null,
                  methodStatementDocumentId: null,
                  hsePlanDocumentId: null,
                  missingConditions: [],
                  readinessSnapshotJson: null,
                  rowVersion: "",
                }
              : null),
        );
        setResources(resourceList?.data ?? []);
        setDossiers(dossierList?.data ?? []);
        setBatches(batchList?.data ?? []);
        setTemplates(templateList?.data ?? []);
        setProjectDocuments(
          (documentsList?.data ?? []).filter(
            (document) =>
              document.category === "ConstructionAcceptance" &&
              document.desiredOperation !== "Delete" &&
              document.syncStatus !== "Deleted",
          ),
        );
      })
      .catch((cause) => active && setError(extractApiError(cause, t("construction.cm.loadError"))))
      .finally(() => active && setLoading(false));
    return () => {
      active = false;
    };
  }, [canViewPreparation, canViewQaQc, canViewQs, canViewResources, projectId, reload, t]);

  const patchPreparation = (patch: Partial<ConstructionPreparationResponse>) =>
    setPreparation((current) => (current ? { ...current, ...patch } : current));
  const savePreparation = async () => {
    if (projectId == null || preparation == null) return;
    setActing(true);
    try {
      const { data } = await adminApi.updateConstructionPreparation(projectId, {
        wbsBaselineReference: preparation.wbsBaselineReference,
        scheduleBaselineReference: preparation.scheduleBaselineReference,
        sCurveBaselineReference: preparation.sCurveBaselineReference,
        bchOrganizationReference: preparation.bchOrganizationReference,
        resourcePlanReference: preparation.resourcePlanReference,
        budgetBaselineReference: preparation.budgetBaselineReference,
        wbsBaselineDocumentId: preparation.wbsBaselineDocumentId,
        scheduleBaselineDocumentId: preparation.scheduleBaselineDocumentId,
        sCurveBaselineDocumentId: preparation.sCurveBaselineDocumentId,
        bchOrganizationDocumentId: preparation.bchOrganizationDocumentId,
        resourcePlanDocumentId: preparation.resourcePlanDocumentId,
        budgetBaselineDocumentId: preparation.budgetBaselineDocumentId,
        budgetBaselineLocked: preparation.budgetBaselineLocked,
        siteHandoverRequired: preparation.siteHandoverRequired,
        siteHandoverComplete: preparation.siteHandoverComplete,
        methodStatementRequired: preparation.methodStatementRequired,
        methodStatementApproved: preparation.methodStatementApproved,
        hsePlanRequired: preparation.hsePlanRequired,
        hsePlanApproved: preparation.hsePlanApproved,
        siteHandoverDocumentId: preparation.siteHandoverDocumentId,
        methodStatementDocumentId: preparation.methodStatementDocumentId,
        hsePlanDocumentId: preparation.hsePlanDocumentId,
        rowVersion: preparation.rowVersion,
      });
      setPreparation(data);
      toast({ title: t("construction.cm.saved") });
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("construction.common.actionError")),
      });
    } finally {
      setActing(false);
    }
  };
  const readinessAction = async (start: boolean) => {
    if (projectId == null || preparation == null) return;
    setActing(true);
    try {
      const { data } = start
        ? await adminApi.startConstruction(projectId, preparation.rowVersion)
        : await adminApi.evaluateConstructionReadiness(projectId, preparation.rowVersion);
      setPreparation(data);
      toast({ title: t(start ? "construction.cm.started" : "construction.cm.evaluated") });
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("construction.common.actionError")),
      });
    } finally {
      setActing(false);
    }
  };
  const decide = async () => {
    if (projectId == null || reviewTarget == null) return;
    setActing(true);
    try {
      if (reviewTarget.kind === "resource")
        await adminApi.decideConstructionResourceRequest(
          projectId,
          reviewTarget.row.id,
          approved,
          reviewReason,
          reviewTarget.row.rowVersion,
        );
      else if (reviewTarget.kind === "qaQc")
        await adminApi.decideQaQcDossier(
          projectId,
          reviewTarget.row.id,
          approved,
          reviewReason,
          reviewTarget.row.rowVersion,
        );
      else
        await adminApi.decideConstructionQsBatch(
          projectId,
          reviewTarget.row.id,
          approved,
          reviewReason,
          reviewTarget.row.rowVersion,
        );
      setReviewTarget(null);
      setReviewReason("");
      setReload((value) => value + 1);
      toast({ title: t("construction.cm.decisionSaved") });
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("construction.common.actionError")),
      });
    } finally {
      setActing(false);
    }
  };
  const createTemplate = async (event: FormEvent) => {
    event.preventDefault();
    if (projectId == null) return;
    try {
      JSON.parse(templateForm.schemaJson);
    } catch {
      toast({ variant: "destructive", title: t("construction.qaQc.invalidSchemaJson") });
      return;
    }
    setActing(true);
    try {
      await adminApi.createQaQcTemplate(projectId, templateForm);
      setTemplateOpen(false);
      setReload((value) => value + 1);
      toast({ title: t("construction.qaQc.templateCreated") });
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("construction.common.actionError")),
      });
    } finally {
      setActing(false);
    }
  };
  const publishTemplate = async (template: QaQcTemplateResponse) => {
    if (projectId == null) return;
    try {
      await adminApi.publishQaQcTemplate(projectId, template.id, template.rowVersion);
      setReload((value) => value + 1);
      toast({ title: t("construction.qaQc.templatePublished") });
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("construction.common.actionError")),
      });
    }
  };
  const openReview = (target: ReviewTarget, isApproved: boolean) => {
    setReviewTarget(target);
    setApproved(isApproved);
    setReviewReason("");
  };

  const ReviewButtons = ({ target, allowed }: { target: ReviewTarget; allowed: boolean }) =>
    allowed && target.row.status === "PendingCmReview" ? (
      <div className="flex gap-2">
        <Button size="sm" onClick={() => openReview(target, true)}>
          <Check className="mr-2 h-4 w-4" />
          {t("construction.common.approve")}
        </Button>
        <Button size="sm" variant="destructive" onClick={() => openReview(target, false)}>
          <X className="mr-2 h-4 w-4" />
          {t("construction.common.reject")}
        </Button>
      </div>
    ) : null;

  return (
    <ConstructionWorkspaceShell
      title={t("construction.cm.title")}
      description={t("construction.cm.description")}
      projectId={projectId}
      onProjectChange={selectProject}
      testId="cm-construction-page"
    >
      {loading ? (
        <PageLoading />
      ) : error ? (
        <PageError message={error} onRetry={() => setReload((value) => value + 1)} />
      ) : (
        <Tabs defaultValue="preparation" className="space-y-4">
          <TabsList className="h-auto w-full justify-start overflow-x-auto">
            <TabsTrigger value="preparation">{t("construction.cm.preparation")}</TabsTrigger>
            <TabsTrigger value="resources">{t("construction.resource.title")}</TabsTrigger>
            <TabsTrigger value="qa-qc">{t("construction.qaQc.title")}</TabsTrigger>
            <TabsTrigger value="qs">{t("construction.qs.title")}</TabsTrigger>
            {has(ADMIN_PERMS.constructionQaQcTemplateManage) && (
              <TabsTrigger value="templates">{t("construction.qaQc.templates")}</TabsTrigger>
            )}
          </TabsList>
          <TabsContent value="preparation" className="space-y-4">
            {preparation == null ? (
              <PageEmpty message={t("construction.cm.noPreparation")} />
            ) : (
              <>
                <div className="flex flex-col gap-3 rounded-xl border bg-card p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <div className="flex items-center gap-2">
                      <h2 className="font-semibold">
                        {t("construction.cm.version", { version: preparation.version })}
                      </h2>
                      <Badge variant="outline">
                        {t(`construction.preparationStatus.${preparation.status}`)}
                      </Badge>
                    </div>
                    <p className="text-sm text-muted-foreground">
                      {preparation.status === "ReadyToStart" ||
                      preparation.status === "InConstruction"
                        ? t("construction.cm.ready")
                        : preparation.readinessSnapshotJson
                          ? t("construction.cm.blockerCount", {
                              count: preparation.missingConditions.length,
                            })
                          : t("construction.cm.notEvaluated")}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button asChild variant="outline">
                      <Link to="/admin/construction/tasks">{t("nav.constructionTasks")}</Link>
                    </Button>
                    {has(ADMIN_PERMS.constructionCmPreparationManage) && (
                      <Button
                        variant="outline"
                        disabled={acting}
                        onClick={() => void savePreparation()}
                      >
                        <Save className="mr-2 h-4 w-4" />
                        {t("common.save")}
                      </Button>
                    )}
                    <Button disabled={acting} onClick={() => void readinessAction(false)}>
                      <RefreshCcw className="mr-2 h-4 w-4" />
                      {t("construction.cm.evaluate")}
                    </Button>
                    {has(ADMIN_PERMS.constructionCmPreparationStart) && (
                      <Button
                        disabled={
                          acting ||
                          preparation.status !== "ReadyToStart" ||
                          !preparation.readinessSnapshotJson
                        }
                        onClick={() => void readinessAction(true)}
                      >
                        <Play className="mr-2 h-4 w-4" />
                        {t("construction.cm.start")}
                      </Button>
                    )}
                  </div>
                </div>
                <div className="grid gap-4 rounded-xl border bg-card p-4 shadow-sm md:grid-cols-2 xl:grid-cols-3">
                  {referenceFields.map((field) => (
                    <div key={field} className="space-y-1.5">
                      <Label htmlFor={field}>{t(`construction.cm.${field}`)}</Label>
                      <Input
                        id={field}
                        required
                        maxLength={500}
                        disabled={!has(ADMIN_PERMS.constructionCmPreparationManage)}
                        value={preparation[field]}
                        onChange={(event) => patchPreparation({ [field]: event.target.value })}
                      />
                    </div>
                  ))}
                  {baselineDocumentFields.map(([field, labelField]) => (
                    <DocumentSelect
                      key={field}
                      id={field}
                      label={`${t(`construction.cm.${labelField}`)} · ${t("acceptance.field.documents")}`}
                      value={preparation[field]}
                      documents={projectDocuments}
                      disabled={!has(ADMIN_PERMS.constructionCmPreparationManage)}
                      onChange={(value) => patchPreparation({ [field]: value })}
                    />
                  ))}
                  {(
                    ["siteHandoverRequired", "methodStatementRequired", "hsePlanRequired"] as const
                  ).map((field) => (
                    <div
                      key={field}
                      className="flex items-center justify-between gap-3 rounded-lg border p-3"
                    >
                      <Label htmlFor={field}>{t(`construction.cm.${field}`)}</Label>
                      <Switch
                        id={field}
                        checked={preparation[field]}
                        disabled={!has(ADMIN_PERMS.constructionCmPreparationManage)}
                        onCheckedChange={(checked) => patchPreparation({ [field]: checked })}
                      />
                    </div>
                  ))}
                  {(
                    [
                      "budgetBaselineLocked",
                      "siteHandoverComplete",
                      "methodStatementApproved",
                      "hsePlanApproved",
                    ] as const
                  ).map((field) => (
                    <div
                      key={field}
                      className="flex items-center justify-between gap-3 rounded-lg border bg-muted/30 p-3"
                    >
                      <Label htmlFor={field}>{t(`construction.cm.${field}`)}</Label>
                      <Switch id={field} checked={preparation[field]} disabled />
                    </div>
                  ))}
                  {preparation.siteHandoverRequired && (
                    <DocumentSelect
                      id="siteHandoverDocumentId"
                      label={`${t("construction.cm.siteHandoverComplete")} · ${t("acceptance.field.documents")}`}
                      value={preparation.siteHandoverDocumentId}
                      documents={projectDocuments}
                      disabled={!has(ADMIN_PERMS.constructionCmPreparationManage)}
                      onChange={(value) => patchPreparation({ siteHandoverDocumentId: value })}
                    />
                  )}
                  {preparation.methodStatementRequired && (
                    <DocumentSelect
                      id="methodStatementDocumentId"
                      label={`${t("construction.cm.methodStatementApproved")} · ${t("acceptance.field.documents")}`}
                      value={preparation.methodStatementDocumentId}
                      documents={projectDocuments}
                      disabled={!has(ADMIN_PERMS.constructionCmPreparationManage)}
                      onChange={(value) => patchPreparation({ methodStatementDocumentId: value })}
                    />
                  )}
                  {preparation.hsePlanRequired && (
                    <DocumentSelect
                      id="hsePlanDocumentId"
                      label={`${t("construction.cm.hsePlanApproved")} · ${t("acceptance.field.documents")}`}
                      value={preparation.hsePlanDocumentId}
                      documents={projectDocuments}
                      disabled={!has(ADMIN_PERMS.constructionCmPreparationManage)}
                      onChange={(value) => patchPreparation({ hsePlanDocumentId: value })}
                    />
                  )}
                </div>
                {preparation.missingConditions.length > 0 && (
                  <div className="rounded-xl border border-amber-200 bg-amber-50 p-4">
                    <h3 className="font-medium text-amber-900">{t("construction.cm.blockers")}</h3>
                    <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-amber-900">
                      {preparation.missingConditions.map((condition) => (
                        <li key={condition}>{condition}</li>
                      ))}
                    </ul>
                  </div>
                )}
              </>
            )}
          </TabsContent>
          <TabsContent value="resources">
            <ReviewList empty={t("construction.cm.noPendingResources")}>
              {resources.map((row) => (
                <ReviewCard
                  key={row.id}
                  code={row.code}
                  title={`${t(`construction.resource.type.${row.type}`)} · ${row.quantity.toLocaleString(lang)} ${row.unit}`}
                  status={row.status}
                  detail={`${row.wbsCode} · ${row.reason}`}
                >
                  <ReviewButtons
                    target={{ kind: "resource", row }}
                    allowed={has(ADMIN_PERMS.constructionResourceRequestsReview)}
                  />
                </ReviewCard>
              ))}
            </ReviewList>
          </TabsContent>
          <TabsContent value="qa-qc">
            <ReviewList empty={t("construction.cm.noPendingQaQc")}>
              {dossiers.map((row) => (
                <ReviewCard
                  key={row.id}
                  code={row.code}
                  title={row.title}
                  status={row.status}
                  detail={`${row.wbsCode} · IFC #${row.ifcReleaseId ?? "—"}`}
                >
                  <ReviewButtons
                    target={{ kind: "qaQc", row }}
                    allowed={has(ADMIN_PERMS.constructionQaQcReview)}
                  />
                </ReviewCard>
              ))}
            </ReviewList>
          </TabsContent>
          <TabsContent value="qs">
            <ReviewList empty={t("construction.cm.noPendingQs")}>
              {batches.map((row) => (
                <ReviewCard
                  key={row.id}
                  code={row.code}
                  title={`${t(`construction.qs.direction.${row.direction}`)} · ${row.certifiedAmount.toLocaleString(lang)}`}
                  status={row.status}
                  detail={`${row.wbsCode} · #${row.contractId}`}
                >
                  <ReviewButtons
                    target={{ kind: "qs", row }}
                    allowed={has(ADMIN_PERMS.constructionQsReview)}
                  />
                </ReviewCard>
              ))}
            </ReviewList>
          </TabsContent>
          <TabsContent value="templates" className="space-y-3">
            <div className="flex justify-end">
              <Button onClick={() => setTemplateOpen(true)}>
                <FilePlus2 className="mr-2 h-4 w-4" />
                {t("construction.qaQc.newTemplate")}
              </Button>
            </div>
            <ReviewList empty={t("construction.qaQc.noTemplates")}>
              {templates.map((template) => (
                <ReviewCard
                  key={template.id}
                  code={template.code}
                  title={template.nameVi}
                  status={template.isPublished ? "Published" : "Draft"}
                  detail={`v${template.version}`}
                >
                  {!template.isPublished && (
                    <Button size="sm" onClick={() => void publishTemplate(template)}>
                      <ClipboardCheck className="mr-2 h-4 w-4" />
                      {t("construction.qaQc.publish")}
                    </Button>
                  )}
                </ReviewCard>
              ))}
            </ReviewList>
          </TabsContent>
        </Tabs>
      )}
      <Dialog open={reviewTarget != null} onOpenChange={(value) => !value && setReviewTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {t(approved ? "construction.cm.approveTitle" : "construction.cm.rejectTitle")}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-1.5">
            <Label htmlFor="review-reason">{t("construction.cm.decisionReason")}</Label>
            <Textarea
              id="review-reason"
              required
              maxLength={2000}
              value={reviewReason}
              onChange={(event) => setReviewReason(event.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setReviewTarget(null)}>
              {t("common.cancel")}
            </Button>
            <Button
              variant={approved ? "default" : "destructive"}
              disabled={acting || !reviewReason.trim()}
              onClick={() => void decide()}
              data-testid="cm-decision-confirm"
            >
              {t("common.confirm")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog open={templateOpen} onOpenChange={setTemplateOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <form onSubmit={createTemplate} className="space-y-4">
            <DialogHeader>
              <DialogTitle>{t("construction.qaQc.newTemplate")}</DialogTitle>
            </DialogHeader>
            <div className="grid gap-3 sm:grid-cols-2">
              <Input
                required
                maxLength={60}
                placeholder={t("construction.qaQc.templateCode")}
                value={templateForm.code}
                onChange={(e) => setTemplateForm({ ...templateForm, code: e.target.value })}
              />
              <Input
                required
                maxLength={200}
                placeholder={t("construction.qaQc.nameVi")}
                value={templateForm.nameVi}
                onChange={(e) => setTemplateForm({ ...templateForm, nameVi: e.target.value })}
              />
              <Input
                required
                maxLength={200}
                placeholder={t("construction.qaQc.nameEn")}
                value={templateForm.name}
                onChange={(e) => setTemplateForm({ ...templateForm, name: e.target.value })}
              />
              <Input
                required
                maxLength={200}
                placeholder={t("construction.qaQc.nameZh")}
                value={templateForm.nameZh}
                onChange={(e) => setTemplateForm({ ...templateForm, nameZh: e.target.value })}
              />
              <Input
                required
                maxLength={200}
                placeholder={t("construction.qaQc.nameJa")}
                value={templateForm.nameJa}
                onChange={(e) => setTemplateForm({ ...templateForm, nameJa: e.target.value })}
              />
              <Textarea
                required
                maxLength={20000}
                className="min-h-36 font-mono text-xs sm:col-span-2"
                value={templateForm.schemaJson}
                onChange={(e) => setTemplateForm({ ...templateForm, schemaJson: e.target.value })}
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setTemplateOpen(false)}>
                {t("common.cancel")}
              </Button>
              <Button type="submit" disabled={acting}>
                {t("common.save")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </ConstructionWorkspaceShell>
  );
}

function ReviewList({ children, empty }: { children: ReactNode; empty: string }) {
  const items = Array.isArray(children) ? children : [children];
  return items.length === 0 ? (
    <PageEmpty message={empty} />
  ) : (
    <div className="grid gap-3 lg:grid-cols-2">{children}</div>
  );
}

function DocumentSelect({
  id,
  label,
  value,
  documents,
  disabled,
  onChange,
}: {
  id: string;
  label: string;
  value?: number | null;
  documents: ProjectDocumentResponse[];
  disabled: boolean;
  onChange: (value: number | null) => void;
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Select
        value={value ? String(value) : "none"}
        onValueChange={(next) => onChange(next === "none" ? null : Number(next))}
        disabled={disabled}
      >
        <SelectTrigger id={id} data-testid={id}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="none">—</SelectItem>
          {documents.map((document) => (
            <SelectItem key={document.id} value={String(document.id)}>
              {document.originalFileName}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}

function ReviewCard({
  code,
  title,
  status,
  detail,
  children,
}: {
  code: string;
  title: string;
  status: string;
  detail: string;
  children?: ReactNode;
}) {
  const { t } = useI18n();
  return (
    <article className="space-y-3 rounded-xl border bg-card p-4 shadow-sm">
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="font-semibold">{code}</p>
          <p className="text-sm text-muted-foreground">{title}</p>
        </div>
        <Badge variant="outline">{t(`construction.status.${status}`)}</Badge>
      </div>
      <p className="text-sm">{detail}</p>
      {children}
    </article>
  );
}
