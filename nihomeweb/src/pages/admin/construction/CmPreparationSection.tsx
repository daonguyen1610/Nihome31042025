import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Play, RefreshCcw, Save } from "lucide-react";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
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
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { useUiError } from "@/hooks/useUiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type ConstructionPreparationResponse,
  type ProjectDocumentResponse,
} from "@/services/adminApi";

const baselineDocumentFields = [
  ["wbsBaselineDocumentId", "wbsBaselineReference"],
  ["scheduleBaselineDocumentId", "scheduleBaselineReference"],
  ["sCurveBaselineDocumentId", "sCurveBaselineReference"],
  ["bchOrganizationDocumentId", "bchOrganizationReference"],
  ["resourcePlanDocumentId", "resourcePlanReference"],
  ["budgetBaselineDocumentId", "budgetBaselineReference"],
] as const;

export function CmPreparationSection({ projectId }: { projectId: number }) {
  const { t } = useI18n();
  const uiError = useUiError();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.constructionCmPreparationManage);
  const canStart = has(ADMIN_PERMS.constructionCmPreparationStart);
  const [preparation, setPreparation] = useState<ConstructionPreparationResponse | null>(null);
  const [projectDocuments, setProjectDocuments] = useState<ProjectDocumentResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  const [acting, setActing] = useState(false);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    void Promise.all([
      adminApi.getConstructionPreparation(projectId).catch((cause) => {
        if ((cause as { response?: { status?: number } }).response?.status === 404) return null;
        throw cause;
      }),
      adminApi.listProjectDocuments(projectId),
    ])
      .then(([prep, documentsList]) => {
        if (!active) return;
        setPreparation(
          // 204 No Content arrives as an empty body; treat it as "not created yet".
          prep?.data && typeof prep.data === "object"
            ? prep.data
            : {
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
              },
        );
        setProjectDocuments(
          (documentsList.data ?? []).filter(
            (document) =>
              document.category === "ConstructionAcceptance" &&
              document.desiredOperation !== "Delete" &&
              document.syncStatus !== "Deleted",
          ),
        );
      })
      .catch((cause) => {
        if (active) setError(uiError(cause, t("construction.cm.loadError")));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [projectId, reload, t, uiError]);

  const patchPreparation = (patch: Partial<ConstructionPreparationResponse>) =>
    setPreparation((current) => (current ? { ...current, ...patch } : current));

  const savePreparation = async () => {
    if (preparation == null) return;
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
        title: uiError(cause, t("construction.common.actionError")),
      });
    } finally {
      setActing(false);
    }
  };

  const readinessAction = async (start: boolean) => {
    if (preparation == null) return;
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
        title: uiError(cause, t("construction.common.actionError")),
      });
    } finally {
      setActing(false);
    }
  };

  if (loading && preparation == null) return <PageLoading />;
  if (error) return <PageError message={error} onRetry={() => setReload((value) => value + 1)} />;

  return (
    <details className="border-t py-4">
      <summary className="cursor-pointer py-2 font-semibold">
        {t("construction.cm.preparationTab")}
      </summary>
      <div className="mt-4 space-y-5">
        <p className="max-w-3xl text-sm text-muted-foreground">
          {t("construction.cm.preparationGuide")}
        </p>
        {preparation == null ? (
          <PageEmpty message={t("construction.cm.noPreparation")} />
        ) : (
          <>
            <div className="flex flex-col gap-3 rounded-xl border bg-card p-4 sm:flex-row sm:items-center sm:justify-between">
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
                  {preparation.status === "ReadyToStart" || preparation.status === "InConstruction"
                    ? t("construction.cm.ready")
                    : preparation.readinessSnapshotJson
                      ? t("construction.cm.blockerCount", {
                          count: preparation.missingConditions.length,
                        })
                      : t("construction.cm.notEvaluated")}
                </p>
              </div>
            </div>
            <div className="z-10 space-y-3 rounded-xl border bg-background p-4 sm:sticky sm:top-20">
              <p className="max-w-3xl text-sm text-muted-foreground">
                {t("construction.cm.actionGuide")}
              </p>
              <div className="flex flex-wrap gap-2">
                <Button asChild variant="outline">
                  <Link to="/admin/construction/cm/schedule?tab=tasks">
                    {t("nav.constructionTasks")}
                  </Link>
                </Button>
                {canManage && (
                  <Button disabled={acting} onClick={() => void savePreparation()}>
                    <Save className="mr-2 h-4 w-4" />
                    {t("common.save")}
                  </Button>
                )}
                <Button
                  variant="outline"
                  disabled={acting}
                  onClick={() => void readinessAction(false)}
                >
                  <RefreshCcw className="mr-2 h-4 w-4" />
                  {t("construction.cm.evaluate")}
                </Button>
                {canStart && (
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
            <fieldset className="space-y-4 rounded-xl border bg-card p-4 sm:p-5">
              <legend className="px-2 font-semibold">
                {t("construction.cm.baselineEvidence")}
              </legend>
              <p className="max-w-3xl text-sm text-muted-foreground">
                {t("construction.cm.baselineGuide")}
              </p>
              <div className="divide-y">
                {baselineDocumentFields.map(([documentField, referenceField]) => (
                  <div
                    key={referenceField}
                    className="grid gap-3 py-4 first:pt-0 last:pb-0 md:grid-cols-2"
                  >
                    <div className="space-y-1.5">
                      <Label htmlFor={referenceField}>
                        {t(`construction.cm.${referenceField}`)}
                      </Label>
                      <Input
                        id={referenceField}
                        required
                        maxLength={500}
                        disabled={!canManage}
                        value={preparation[referenceField]}
                        onChange={(event) =>
                          patchPreparation({ [referenceField]: event.target.value })
                        }
                      />
                    </div>
                    <DocumentSelect
                      id={documentField}
                      label={`${t(`construction.cm.${referenceField}`)} · ${t("acceptance.field.documents")}`}
                      value={preparation[documentField]}
                      documents={projectDocuments}
                      disabled={!canManage}
                      onChange={(value) => patchPreparation({ [documentField]: value })}
                    />
                  </div>
                ))}
              </div>
            </fieldset>
            <fieldset className="grid gap-4 rounded-xl border bg-card p-4 sm:p-5 md:grid-cols-2 xl:grid-cols-3">
              <legend className="px-2 font-semibold">
                {t("construction.cm.projectRequirements")}
              </legend>
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
                    disabled={!canManage}
                    onCheckedChange={(checked) => patchPreparation({ [field]: checked })}
                  />
                </div>
              ))}
              {preparation.siteHandoverRequired && (
                <DocumentSelect
                  id="siteHandoverDocumentId"
                  label={`${t("construction.cm.siteHandoverComplete")} · ${t("acceptance.field.documents")}`}
                  value={preparation.siteHandoverDocumentId}
                  documents={projectDocuments}
                  disabled={!canManage}
                  onChange={(value) => patchPreparation({ siteHandoverDocumentId: value })}
                />
              )}
              {preparation.methodStatementRequired && (
                <DocumentSelect
                  id="methodStatementDocumentId"
                  label={`${t("construction.cm.methodStatementApproved")} · ${t("acceptance.field.documents")}`}
                  value={preparation.methodStatementDocumentId}
                  documents={projectDocuments}
                  disabled={!canManage}
                  onChange={(value) => patchPreparation({ methodStatementDocumentId: value })}
                />
              )}
              {preparation.hsePlanRequired && (
                <DocumentSelect
                  id="hsePlanDocumentId"
                  label={`${t("construction.cm.hsePlanApproved")} · ${t("acceptance.field.documents")}`}
                  value={preparation.hsePlanDocumentId}
                  documents={projectDocuments}
                  disabled={!canManage}
                  onChange={(value) => patchPreparation({ hsePlanDocumentId: value })}
                />
              )}
            </fieldset>
            <section className="space-y-3" aria-labelledby="cm-verified-status">
              <h3 id="cm-verified-status" className="font-semibold">
                {t("construction.cm.verifiedStatus")}
              </h3>
              <p className="max-w-3xl text-sm text-muted-foreground">
                {t("construction.cm.verifiedGuide")}
              </p>
              <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2">
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
                    className="flex items-center justify-between gap-4 border-b py-2"
                  >
                    <dt className="text-sm">{t(`construction.cm.${field}`)}</dt>
                    <dd>
                      <Badge variant="outline">
                        {t(preparation[field] ? "common.yes" : "common.no")}
                      </Badge>
                    </dd>
                  </div>
                ))}
              </dl>
            </section>
            {preparation.missingConditions.length > 0 && (
              <div className="rounded-lg border border-warning/40 bg-warning-soft p-4">
                <h3 className="font-medium text-warning-strong">{t("construction.cm.blockers")}</h3>
                <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-warning-strong">
                  {preparation.missingConditions.map((condition) => (
                    <li key={condition}>
                      {condition === "published CM schedule baseline"
                        ? t("cmSchedule.readiness")
                        : condition}
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </>
        )}
      </div>
    </details>
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
