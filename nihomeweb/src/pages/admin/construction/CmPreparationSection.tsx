import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { Loader2, Play, RefreshCcw, Save, Upload } from "lucide-react";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { MultiFilePicker } from "@/components/ui/multi-file-picker";
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
import { newIdempotencyKey } from "@/lib/api";
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
  const canOpenDocuments = has(ADMIN_PERMS.operationalProjects);
  const canUploadDocuments = has(ADMIN_PERMS.operationalProjectsManage);
  const [preparation, setPreparation] = useState<ConstructionPreparationResponse | null>(null);
  const [projectDocuments, setProjectDocuments] = useState<ProjectDocumentResponse[]>([]);
  const [pendingFiles, setPendingFiles] = useState<File[]>([]);
  const [uploading, setUploading] = useState(false);
  const uploadKeysRef = useRef(new Map<File, string>());
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

  const selectUploadFiles = (files: File[]) => {
    for (const file of files) {
      if (!uploadKeysRef.current.has(file)) {
        uploadKeysRef.current.set(file, newIdempotencyKey());
      }
    }
    setPendingFiles(files);
  };

  const reloadProjectDocuments = async () => {
    const { data } = await adminApi.listProjectDocuments(projectId);
    setProjectDocuments(
      (data ?? []).filter(
        (document) =>
          document.category === "ConstructionAcceptance" &&
          document.desiredOperation !== "Delete" &&
          document.syncStatus !== "Deleted",
      ),
    );
  };

  const uploadDocuments = async () => {
    if (pendingFiles.length === 0) return;
    setUploading(true);
    let uploaded = 0;
    let firstError: string | null = null;
    for (const file of pendingFiles) {
      try {
        await adminApi.uploadProjectDocument(
          projectId,
          file,
          "ConstructionAcceptance",
          uploadKeysRef.current.get(file) ?? newIdempotencyKey(),
        );
        uploadKeysRef.current.delete(file);
        uploaded += 1;
      } catch (cause) {
        firstError ??= `${file.name}: ${uiError(cause)}`;
      }
    }
    if (uploaded > 0) {
      setPendingFiles(pendingFiles.filter((file) => uploadKeysRef.current.has(file)));
      await reloadProjectDocuments().catch(() => undefined);
      toast({ title: t("construction.cm.uploaded", { count: uploaded }) });
    }
    if (firstError) {
      toast({ variant: "destructive", title: t("common.error"), description: firstError });
    }
    setUploading(false);
  };

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
            <fieldset className="min-w-0 space-y-4 rounded-xl border bg-card p-4 sm:p-5">
              <legend className="px-2 font-semibold">
                {t("construction.cm.baselineEvidence")}
              </legend>
              {canUploadDocuments && (
                <div className="space-y-3">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <p className="text-sm font-medium">{t("construction.cm.uploadTitle")}</p>
                    {canOpenDocuments && (
                      <Link
                        to={`/admin/operational-projects/${projectId}#project-documents`}
                        className="text-sm font-medium text-primary underline underline-offset-4"
                      >
                        {t("construction.cm.uploadManage")}
                      </Link>
                    )}
                  </div>
                  <MultiFilePicker
                    id="preparation-document-upload"
                    files={pendingFiles}
                    onChange={selectUploadFiles}
                    maxFiles={20}
                    maxSizeMb={100}
                    disabled={uploading}
                  />
                  <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
                    <Button
                      type="button"
                      size="sm"
                      disabled={uploading || pendingFiles.length === 0}
                      onClick={() => void uploadDocuments()}
                    >
                      {uploading ? (
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      ) : (
                        <Upload className="mr-2 h-4 w-4" />
                      )}
                      {t("operationalProjects.documents.upload")}
                    </Button>
                    <p className="text-xs text-muted-foreground">
                      {projectDocuments.length > 0
                        ? t("construction.cm.uploadAvailable", { count: projectDocuments.length })
                        : t("construction.cm.docSelectEmpty")}
                    </p>
                  </div>
                </div>
              )}
              <div className="space-y-2">
                <p className="max-w-3xl text-sm text-muted-foreground">
                  {t("construction.cm.baselineGuide")}
                </p>
                <div>
                  <p className="text-sm font-medium">{t("construction.cm.assignTitle")}</p>
                  <p className="text-xs text-muted-foreground">
                    {t("construction.cm.assignGuide")}
                  </p>
                  <p className="max-w-2xl text-xs text-muted-foreground">
                    {t("construction.cm.docReferenceHelp")}
                  </p>
                </div>
                <p className="text-xs text-muted-foreground lg:hidden">
                  {t("construction.cm.scrollTableHint")}
                </p>
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[720px] border-collapse text-sm">
                    <thead>
                      <tr className="border-b text-left text-xs font-medium text-muted-foreground">
                        <th scope="col" className="pb-2 pr-4">
                          {t("construction.cm.baselineRequirement")}
                        </th>
                        <th scope="col" className="pb-2 pr-4">
                          {t("construction.cm.evidenceFileLabel")}
                        </th>
                        <th scope="col" className="pb-2">
                          <div className="flex items-center gap-2">
                            {t("construction.cm.documentNumberLabel")}
                            <Badge
                              variant="outline"
                              className="px-1.5 py-0 text-[10px] font-medium"
                            >
                              {t("construction.cm.requiredTag")}
                            </Badge>
                          </div>
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {baselineDocumentFields.map(([documentField, referenceField]) => (
                        <tr key={referenceField} className="border-b last:border-0">
                          <td className="py-3 pr-4 align-top">
                            <p className="font-medium">{t(`construction.cm.${referenceField}`)}</p>
                            <p className="max-w-[18rem] text-xs text-muted-foreground">
                              {t(`construction.cm.${referenceField}Help`)}
                            </p>
                          </td>
                          <td className="py-3 pr-4 align-top">
                            <DocumentSelect
                              id={documentField}
                              label={t("construction.cm.evidenceFileLabel")}
                              hideLabel
                              value={preparation[documentField]}
                              documents={projectDocuments}
                              disabled={!canManage}
                              onChange={(value) => {
                                const picked = projectDocuments.find((item) => item.id === value);
                                patchPreparation({
                                  [documentField]: value,
                                  ...(picked && !preparation[referenceField]
                                    ? {
                                        [referenceField]: picked.originalFileName.replace(
                                          /\.[^.]+$/,
                                          "",
                                        ),
                                      }
                                    : {}),
                                });
                              }}
                            />
                          </td>
                          <td className="py-3 align-top">
                            <Input
                              id={referenceField}
                              required
                              maxLength={500}
                              disabled={!canManage}
                              aria-label={`${t(`construction.cm.${referenceField}`)} · ${t("construction.cm.documentNumberLabel")}`}
                              placeholder={t(`construction.cm.${referenceField}Placeholder`)}
                              value={preparation[referenceField]}
                              onChange={(event) =>
                                patchPreparation({ [referenceField]: event.target.value })
                              }
                            />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </fieldset>
            <fieldset className="grid gap-4 rounded-xl border bg-card p-4 sm:p-5 md:grid-cols-2 xl:grid-cols-3">
              <legend className="px-2 font-semibold">
                {t("construction.cm.projectRequirements")}
              </legend>
              <p className="text-sm text-muted-foreground md:col-span-2 xl:col-span-3">
                {t("construction.cm.projectRequirementsGuide")}
              </p>
              {(
                ["siteHandoverRequired", "methodStatementRequired", "hsePlanRequired"] as const
              ).map((field) => (
                <div
                  key={field}
                  className="flex items-start justify-between gap-3 rounded-lg border p-3"
                >
                  <div className="space-y-1">
                    <Label htmlFor={field}>{t(`construction.cm.${field}`)}</Label>
                    <p className="text-xs text-muted-foreground">
                      {t(`construction.cm.${field}Help`)}
                    </p>
                  </div>
                  <Switch
                    id={field}
                    className="mt-0.5 shrink-0"
                    checked={preparation[field]}
                    disabled={!canManage}
                    onCheckedChange={(checked) => patchPreparation({ [field]: checked })}
                  />
                </div>
              ))}
              {preparation.siteHandoverRequired && (
                <DocumentSelect
                  id="siteHandoverDocumentId"
                  label={t("construction.cm.siteHandoverDocumentLabel")}
                  value={preparation.siteHandoverDocumentId}
                  documents={projectDocuments}
                  disabled={!canManage}
                  onChange={(value) => patchPreparation({ siteHandoverDocumentId: value })}
                />
              )}
              {preparation.methodStatementRequired && (
                <DocumentSelect
                  id="methodStatementDocumentId"
                  label={t("construction.cm.methodStatementDocumentLabel")}
                  value={preparation.methodStatementDocumentId}
                  documents={projectDocuments}
                  disabled={!canManage}
                  onChange={(value) => patchPreparation({ methodStatementDocumentId: value })}
                />
              )}
              {preparation.hsePlanRequired && (
                <DocumentSelect
                  id="hsePlanDocumentId"
                  label={t("construction.cm.hsePlanDocumentLabel")}
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
                        {t(
                          preparation[field]
                            ? "construction.cm.verifiedMet"
                            : "construction.cm.verifiedNotMet",
                        )}
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
  hideLabel = false,
  value,
  documents,
  disabled,
  onChange,
}: {
  id: string;
  label: string;
  hideLabel?: boolean;
  value?: number | null;
  documents: ProjectDocumentResponse[];
  disabled: boolean;
  onChange: (value: number | null) => void;
}) {
  const { t } = useI18n();

  return (
    <div className="space-y-1.5">
      <Label htmlFor={id} className={hideLabel ? "sr-only" : undefined}>
        {label}
      </Label>
      <Select
        value={value ? String(value) : "none"}
        onValueChange={(next) => onChange(next === "none" ? null : Number(next))}
        disabled={disabled}
      >
        <SelectTrigger id={id} data-testid={id}>
          <SelectValue placeholder={t("construction.cm.selectFile")} />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="none">{t("construction.cm.docSelectNone")}</SelectItem>
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
