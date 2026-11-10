import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { FieldScope } from "@/components/ui/field-scope";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { FormEvent, useEffect, useState } from "react";
import { FileCheck2, Pencil, Plus, Send } from "lucide-react";
import { ConstructionReviewActions } from "./ConstructionReviewActions";
import { ConstructionDeletionControl } from "./ConstructionDeletionControl";
import { type ScopeProps, useConstructionActions } from "./constructionScope";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { ConstructionStatusBadge } from "./constructionUi";
import { REVIEW_STATUS_TONE } from "./constructionStatus";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type IfcReleaseResponse,
  type ProjectDocumentResponse,
  type QaQcDossierResponse,
  type QaQcEvidenceType,
  type QaQcTemplateResponse,
} from "@/services/adminApi";
import {
  buildQaQcChecklistResults,
  parseQaQcChecklistResults,
  parseQaQcTemplateSchema,
  serializeQaQcChecklistResults,
  type QaQcChecklistResult,
  type QaQcChecklistResultValue,
  type QaQcTemplateItem,
} from "./qaQcChecklist";

const evidenceTypes: QaQcEvidenceType[] = [
  "General",
  "AcceptanceRecord",
  "Sample",
  "LaboratoryResult",
  "MaterialCertificate",
  "SiteDiary",
];

const checklistResultValues: QaQcChecklistResultValue[] = [
  "Pending",
  "Pass",
  "Fail",
  "NotApplicable",
];

export function QaQcDossiersPanel({
  projectId,
  scope = "execute",
  templatesVersion = 0,
}: ScopeProps & { templatesVersion?: number }) {
  const uiError = useUiError();
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManageTemplates = has(ADMIN_PERMS.constructionQaQcTemplateManage);
  const { canManage, canApprove, canEditRow, canDeleteRow } = useConstructionActions(scope, {
    manage: ADMIN_PERMS.constructionQaQcCreate,
    supervisor: ADMIN_PERMS.constructionQaQcReview,
    approve: ADMIN_PERMS.constructionQaQcReview,
  });
  const [templates, setTemplates] = useState<QaQcTemplateResponse[]>([]);
  const [documents, setDocuments] = useState<ProjectDocumentResponse[]>([]);
  const [ifcReleases, setIfcReleases] = useState<
    Array<{ release: IfcReleaseResponse; designProjectCode: string }>
  >([]);
  const [ifcLoading, setIfcLoading] = useState(false);
  const [rows, setRows] = useState<QaQcDossierResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<QaQcDossierResponse | null>(null);
  const [saving, setSaving] = useState(false);
  const [evidence, setEvidence] = useState<
    Array<{ projectDocumentId: string; evidenceType: QaQcEvidenceType }>
  >([]);
  const [checklistItems, setChecklistItems] = useState<QaQcTemplateItem[]>([]);
  const [checklistResults, setChecklistResults] = useState<QaQcChecklistResult[]>([]);
  const [advancedResultsJson, setAdvancedResultsJson] = useState("[]");
  const [form, setForm] = useState({
    templateId: "",
    wbsCode: "",
    title: "",
    ifcReleaseId: "",
  });

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    void Promise.all([
      adminApi.listQaQcTemplates(projectId),
      adminApi.listQaQcDossiers(projectId),
      adminApi.listProjectDocuments(projectId),
    ])
      .then(([templateResponse, dossierResponse, documentResponse]) => {
        if (!active) return;
        setTemplates(templateResponse.data);
        setRows(dossierResponse.data);
        setDocuments(documentResponse.data);
      })
      .catch((cause) => active && setError(uiError(cause, t("construction.qaQc.loadError"))))
      .finally(() => active && setLoading(false));
    return () => {
      active = false;
    };
  }, [projectId, reload, templatesVersion, t, uiError]);

  useEffect(() => {
    let active = true;
    setIfcLoading(true);
    void adminApi
      .getOperationalProject(projectId)
      .then(async ({ data }) => {
        const perProject = await Promise.all(
          data.designProjects.map(async (designProject) => {
            try {
              const { data: list } = await adminApi.listIfcReleases({
                designProjectId: designProject.id,
                status: "Released",
                pageSize: 100,
              });
              return list.items.map((release) => ({
                release,
                designProjectCode: designProject.projectCode,
              }));
            } catch {
              return [];
            }
          }),
        );
        if (active) setIfcReleases(perProject.flat());
      })
      .catch(() => active && setIfcReleases([]))
      .finally(() => active && setIfcLoading(false));
    return () => {
      active = false;
    };
  }, [projectId]);

  const ifcLabel = (releaseId: number) => {
    const match = ifcReleases.find((item) => item.release.id === releaseId);
    return match ? match.release.releaseNumber : `#${releaseId}`;
  };

  const templateName = (template: QaQcTemplateResponse) => {
    if (lang === "vi") return template.nameVi;
    if (lang === "zh") return template.nameZh;
    if (lang === "ja") return template.nameJa;
    return template.name || template.nameVi;
  };

  const emptyForm = () => ({
    templateId: "",
    wbsCode: "",
    title: "",
    ifcReleaseId: "",
  });

  const loadChecklist = (schemaJson: string, resultsJson = "[]") => {
    try {
      const items = parseQaQcTemplateSchema(schemaJson);
      const results = buildQaQcChecklistResults(items, resultsJson);
      setChecklistItems(items);
      setChecklistResults(results);
      setAdvancedResultsJson(serializeQaQcChecklistResults(results));
      return true;
    } catch {
      setChecklistItems([]);
      setChecklistResults([]);
      setAdvancedResultsJson("[]");
      return false;
    }
  };

  const openCreate = () => {
    setEditing(null);
    setForm(emptyForm());
    setChecklistItems([]);
    setChecklistResults([]);
    setAdvancedResultsJson("[]");
    setEvidence([]);
    setOpen(true);
  };

  const openEdit = (row: QaQcDossierResponse) => {
    setEditing(row);
    setForm({
      templateId: String(row.templateId),
      wbsCode: row.wbsCode,
      title: row.title,
      ifcReleaseId: row.ifcReleaseId ? String(row.ifcReleaseId) : "",
    });
    loadChecklist(row.templateSnapshotJson, row.checklistResultsJson);
    setEvidence(
      row.evidence.map((item) => ({
        projectDocumentId: String(item.projectDocumentId),
        evidenceType: item.evidenceType,
      })),
    );
    setOpen(true);
  };

  const save = async (event: FormEvent) => {
    event.preventDefault();
    if (!form.ifcReleaseId) {
      toast({ variant: "destructive", title: t("construction.qaQc.ifcRequired") });
      return;
    }
    if (checklistItems.length === 0 || checklistResults.length !== checklistItems.length) {
      toast({ variant: "destructive", title: t("construction.qaQc.invalidPublishedTemplate") });
      return;
    }
    const evidenceIds = evidence.map((item) => Number(item.projectDocumentId));
    if (
      evidenceIds.some((id) => !Number.isSafeInteger(id) || id <= 0) ||
      new Set(evidenceIds).size !== evidenceIds.length
    ) {
      toast({ variant: "destructive", title: t("construction.qaQc.invalidEvidence") });
      return;
    }
    const body = {
      templateId: Number(form.templateId),
      wbsCode: form.wbsCode.trim(),
      title: form.title.trim(),
      ifcReleaseId: form.ifcReleaseId ? Number(form.ifcReleaseId) : null,
      checklistResultsJson: serializeQaQcChecklistResults(checklistResults),
      evidence: evidence.map((item, index) => ({
        projectDocumentId: evidenceIds[index],
        evidenceType: item.evidenceType,
      })),
    };
    setSaving(true);
    try {
      if (editing) {
        await adminApi.updateQaQcDossier(projectId, editing.id, {
          ...body,
          rowVersion: editing.rowVersion,
        });
        toast({ title: t("construction.common.updated") });
      } else {
        await adminApi.createQaQcDossier(projectId, body);
        toast({ title: t("construction.qaQc.created") });
      }
      setOpen(false);
      setEditing(null);
      setForm(emptyForm());
      setChecklistItems([]);
      setChecklistResults([]);
      setAdvancedResultsJson("[]");
      setEvidence([]);
      setReload((value) => value + 1);
    } catch (cause) {
      toast({
        variant: "destructive",
        title: uiError(cause, t("construction.qaQc.saveError")),
      });
    } finally {
      setSaving(false);
    }
  };

  const submit = async (row: QaQcDossierResponse) => {
    try {
      await adminApi.submitQaQcDossier(projectId, row.id, row.rowVersion);
      setReload((value) => value + 1);
      toast({ title: t("construction.common.submitted") });
    } catch (cause) {
      toast({
        variant: "destructive",
        title: uiError(cause, t("construction.common.actionError")),
      });
    }
  };

  const decide = async (row: QaQcDossierResponse, approved: boolean, reason: string) => {
    try {
      await adminApi.decideQaQcDossier(projectId, row.id, approved, reason, row.rowVersion);
      setReload((value) => value + 1);
      toast({ title: t("construction.cm.decisionSaved") });
      return true;
    } catch (cause) {
      toast({
        variant: "destructive",
        title: uiError(cause, t("construction.common.actionError")),
      });
      return false;
    }
  };

  const publishedTemplates = templates.filter((template) => template.isPublished);
  const applyAdvancedResults = () => {
    try {
      const parsed = parseQaQcChecklistResults(advancedResultsJson);
      const templateCodes = new Set(checklistItems.map((item) => item.code.toLocaleUpperCase()));
      if (parsed.some((entry) => !templateCodes.has(entry.code.toLocaleUpperCase()))) {
        throw new Error("unknown-checklist-code");
      }
      const merged = buildQaQcChecklistResults(
        checklistItems,
        serializeQaQcChecklistResults(parsed),
      );
      setChecklistResults(merged);
      setAdvancedResultsJson(serializeQaQcChecklistResults(merged));
      toast({ title: t("construction.qaQc.advancedApplied") });
    } catch {
      toast({ variant: "destructive", title: t("construction.qaQc.invalidChecklistJson") });
    }
  };

  return (
    <>
      <section className="space-y-4">
        <div className="flex flex-col gap-2 rounded-lg border bg-muted/20 p-3 text-sm sm:flex-row sm:items-center sm:justify-between">
          <div className="space-y-1">
            <span>
              {t("construction.qaQc.templateSummary", { count: publishedTemplates.length })}
            </span>
            {publishedTemplates.length === 0 && (
              <p className="text-xs text-muted-foreground">
                {t("construction.qaQc.noTemplatesHint")}
              </p>
            )}
          </div>
          {canManage && (
            <Button
              onClick={openCreate}
              disabled={publishedTemplates.length === 0 && !editing}
              data-testid="qa-qc-new"
            >
              <Plus className="mr-2 h-4 w-4" />
              {t("construction.qaQc.new")}
            </Button>
          )}
        </div>
        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => setReload((value) => value + 1)} />
        ) : rows.length === 0 ? (
          <PageEmpty
            message={t("construction.qaQc.empty")}
            action={
              canManage && (
                <Button
                  onClick={openCreate}
                  disabled={publishedTemplates.length === 0}
                  data-testid="qa-qc-empty-new"
                >
                  <Plus className="mr-2 h-4 w-4" />
                  {t("construction.qaQc.new")}
                </Button>
              )
            }
          />
        ) : (
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {rows.map((row) => (
              <article
                key={row.id}
                id={`qa-qc-${row.id}`}
                ref={(element) => {
                  if (element && window.location.hash === `#qa-qc-${row.id}`)
                    element.scrollIntoView({ block: "center" });
                }}
                className="space-y-3 rounded-xl border bg-card p-4 shadow-sm"
                data-testid={`qa-qc-row-${row.id}`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="font-semibold">{row.code}</p>
                    <p className="text-sm text-muted-foreground">{row.title}</p>
                  </div>
                  <ConstructionStatusBadge tone={REVIEW_STATUS_TONE[row.status]}>
                    {t(`construction.status.${row.status}`)}
                  </ConstructionStatusBadge>
                </div>
                <dl className="grid grid-cols-2 gap-2 text-sm">
                  <div>
                    <dt className="text-muted-foreground">{t("construction.common.wbs")}</dt>
                    <dd>{row.wbsCode}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("construction.qaQc.template")}</dt>
                    <dd>
                      {templates.find((template) => template.id === row.templateId)?.code ??
                        `#${row.templateId}`}{" "}
                      · v{row.templateVersion}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("construction.qaQc.ifcRelease")}</dt>
                    <dd>{row.ifcReleaseId ? ifcLabel(row.ifcReleaseId) : "—"}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("construction.qaQc.evidence")}</dt>
                    <dd>{row.evidenceDocumentIds.length.toLocaleString(lang)}</dd>
                  </div>
                </dl>
                {row.evidence.length > 0 && (
                  <div className="space-y-1 text-sm" data-testid={`qa-qc-evidence-${row.id}`}>
                    <p className="text-muted-foreground">{t("construction.qaQc.evidence")}</p>
                    <ul className="space-y-1">
                      {row.evidence.map((item) => (
                        <li key={item.projectDocumentId} className="break-words">
                          {documents.find((document) => document.id === item.projectDocumentId)
                            ?.originalFileName ?? `#${item.projectDocumentId}`}{" "}
                          · {t(`construction.qaQc.evidenceType.${item.evidenceType}`)}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
                {row.status === "Draft" && canEditRow(row.createdByUserId) && (
                  <div className="flex flex-wrap items-center gap-2">
                    <Button
                      size="sm"
                      onClick={() => void submit(row)}
                      data-testid={`qa-qc-submit-${row.id}`}
                    >
                      <Send className="mr-2 h-4 w-4" />
                      {t("construction.common.submit")}
                    </Button>
                    <Button size="sm" variant="outline" onClick={() => openEdit(row)}>
                      <Pencil className="mr-2 h-4 w-4" />
                      {t("common.edit")}
                    </Button>
                  </div>
                )}
                {(row.status === "Draft" || row.status === "CmRejected") &&
                  canDeleteRow(row.createdByUserId) && (
                    <ConstructionDeletionControl
                      rowVersion={row.rowVersion}
                      getImpact={() => adminApi.getQaQcDossierDeletionImpact(projectId, row.id)}
                      deleteRecord={(request) =>
                        adminApi.deleteQaQcDossier(projectId, row.id, request)
                      }
                      onDeleted={() => setReload((value) => value + 1)}
                    />
                  )}
                <ConstructionReviewActions
                  allowed={canApprove}
                  status={row.status}
                  createdByUserId={row.createdByUserId}
                  lastEditedByUserId={row.lastEditedByUserId}
                  onDecide={(approved, reason) => decide(row, approved, reason)}
                />
              </article>
            ))}
          </div>
        )}
      </section>
      <Dialog open={open} onOpenChange={(nextOpen) => !saving && setOpen(nextOpen)}>
        <DialogContent
          className="max-h-[calc(100dvh-2rem)] overflow-hidden p-0 sm:max-w-2xl"
          onEscapeKeyDown={(event) => saving && event.preventDefault()}
          onInteractOutside={(event) => saving && event.preventDefault()}
        >
          <form
            onSubmit={save}
            className="flex max-h-[calc(100dvh-2rem)] min-h-0 flex-col"
            aria-busy={saving}
          >
            <div className="min-h-0 space-y-4 overflow-y-auto px-4 pb-6 pt-5 sm:px-6 sm:pt-6">
              <DialogHeader className="pr-8">
                <DialogTitle className="flex items-center gap-2">
                  <FileCheck2 className="h-5 w-5" />
                  {t(editing ? "construction.qaQc.editDossier" : "construction.qaQc.new")}
                </DialogTitle>
                <DialogDescription>{t("construction.qaQc.formIntro")}</DialogDescription>
              </DialogHeader>
              {!ifcLoading && ifcReleases.length === 0 && (
                <p role="status" className="rounded-lg chip-warning p-3 text-sm">
                  {t("construction.qaQc.noIfcRelease")}
                </p>
              )}
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5 sm:col-span-2">
                  <Label htmlFor="qa-title">
                    {t("construction.qaQc.dossierTitle")}
                    <span className="ml-0.5 text-destructive" aria-hidden="true">
                      *
                    </span>
                  </Label>
                  <Input
                    id="qa-title"
                    required
                    maxLength={300}
                    placeholder={t("construction.qaQc.titleExample")}
                    aria-describedby="qa-title-hint"
                    value={form.title}
                    onChange={(e) => setForm({ ...form, title: e.target.value })}
                  />
                  <p id="qa-title-hint" className="text-xs text-muted-foreground">
                    {t("construction.qaQc.titleHint")}
                  </p>
                </div>
                <FieldScope className="space-y-1.5 sm:col-span-2">
                  <Label>
                    {t("construction.qaQc.templateLabel")}
                    <span className="ml-0.5 text-destructive" aria-hidden="true">
                      *
                    </span>
                  </Label>
                  <Select
                    required
                    value={form.templateId}
                    onValueChange={(value) => {
                      setForm({ ...form, templateId: value });
                      const template = publishedTemplates.find((item) => item.id === Number(value));
                      if (template && !loadChecklist(template.schemaJson)) {
                        toast({
                          variant: "destructive",
                          title: t("construction.qaQc.invalidPublishedTemplate"),
                        });
                      }
                    }}
                  >
                    <SelectTrigger data-testid="qa-qc-template">
                      <SelectValue placeholder={t("construction.qaQc.selectTemplatePlaceholder")} />
                    </SelectTrigger>
                    <SelectContent>
                      {publishedTemplates.map((template) => (
                        <SelectItem key={template.id} value={String(template.id)}>
                          {templateName(template)} · v{template.version}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <p className="text-xs text-muted-foreground">
                    {t("construction.qaQc.selectTemplateHint")}
                  </p>
                </FieldScope>
                <div className="space-y-1.5">
                  <Label htmlFor="qa-wbs">
                    {t("construction.qaQc.wbsLabel")}
                    <span className="ml-0.5 text-destructive" aria-hidden="true">
                      *
                    </span>
                  </Label>
                  <Input
                    id="qa-wbs"
                    required
                    maxLength={100}
                    placeholder="WBS-3.1-FORM"
                    aria-describedby="qa-wbs-hint"
                    value={form.wbsCode}
                    onChange={(e) => setForm({ ...form, wbsCode: e.target.value })}
                  />
                  <p id="qa-wbs-hint" className="text-xs text-muted-foreground">
                    {t("construction.qaQc.wbsHint")}
                  </p>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="qa-ifc">
                    {t("construction.qaQc.ifcReleaseLabel")}
                    <span className="ml-0.5 text-destructive" aria-hidden="true">
                      *
                    </span>
                  </Label>
                  <SearchableSelect
                    id="qa-ifc"
                    required
                    data-testid="qa-qc-ifc-release"
                    ariaLabel={t("construction.qaQc.ifcRelease")}
                    value={form.ifcReleaseId}
                    onChange={(value) => setForm({ ...form, ifcReleaseId: value })}
                    placeholder={t("construction.qaQc.selectIfcRelease")}
                    searchPlaceholder={t("construction.qaQc.ifcRelease")}
                    emptyText={t("common.noResults")}
                    loading={ifcLoading}
                    disabled={ifcLoading || ifcReleases.length === 0}
                    options={ifcReleases.map(({ release, designProjectCode }) => ({
                      value: String(release.id),
                      label: `${release.releaseNumber} · ${release.title}`,
                      hint: `${designProjectCode}${
                        release.releaseDate
                          ? ` · ${new Date(release.releaseDate).toLocaleDateString(lang)}`
                          : ""
                      }`,
                    }))}
                  />
                  {ifcReleases.length > 0 && (
                    <p className="text-xs text-muted-foreground">
                      {t("construction.qaQc.ifcHint")}
                    </p>
                  )}
                </div>
                <fieldset className="space-y-3 rounded-lg border p-4 sm:col-span-2">
                  <legend className="px-1 text-sm font-semibold">
                    {t("construction.qaQc.inspectionResults")}
                  </legend>
                  <p className="text-sm text-muted-foreground">
                    {t("construction.qaQc.inspectionResultsHint")}
                  </p>
                  {!form.templateId ? (
                    <p className="rounded-lg bg-muted/50 p-3 text-sm text-muted-foreground">
                      {t("construction.qaQc.chooseTemplateForChecklist")}
                    </p>
                  ) : checklistItems.length === 0 ? (
                    <p className="rounded-lg chip-warning p-3 text-sm" role="alert">
                      {t("construction.qaQc.invalidPublishedTemplate")}
                    </p>
                  ) : (
                    <div className="space-y-3">
                      {checklistItems.map((item, index) => (
                        <div key={item.code} className="rounded-lg border bg-card p-3">
                          <div className="mb-3">
                            <p className="font-medium">
                              {index + 1}. {item.title}
                            </p>
                            {item.acceptanceCriteria && (
                              <p className="mt-1 text-sm text-muted-foreground">
                                {t("construction.qaQc.acceptanceCriteria")}:{" "}
                                {item.acceptanceCriteria}
                              </p>
                            )}
                          </div>
                          <div className="grid gap-3 sm:grid-cols-[12rem_1fr]">
                            <div className="space-y-1.5">
                              <Label htmlFor={`qa-result-${index}`}>
                                {t("construction.qaQc.result")}
                              </Label>
                              <Select
                                value={checklistResults[index]?.result ?? "Pending"}
                                onValueChange={(result: QaQcChecklistResultValue) => {
                                  const next = checklistResults.map((entry, resultIndex) =>
                                    resultIndex === index ? { ...entry, result } : entry,
                                  );
                                  setChecklistResults(next);
                                  setAdvancedResultsJson(serializeQaQcChecklistResults(next));
                                }}
                              >
                                <SelectTrigger id={`qa-result-${index}`}>
                                  <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                  {checklistResultValues.map((result) => (
                                    <SelectItem key={result} value={result}>
                                      {t(`construction.qaQc.resultValue.${result}`)}
                                    </SelectItem>
                                  ))}
                                </SelectContent>
                              </Select>
                            </div>
                            <div className="space-y-1.5">
                              <Label htmlFor={`qa-note-${index}`}>
                                {t("construction.qaQc.resultNote")}
                              </Label>
                              <Input
                                id={`qa-note-${index}`}
                                maxLength={1000}
                                placeholder={t("construction.qaQc.resultNotePlaceholder")}
                                value={checklistResults[index]?.note ?? ""}
                                onChange={(event) => {
                                  const next = checklistResults.map((entry, resultIndex) =>
                                    resultIndex === index
                                      ? { ...entry, note: event.target.value }
                                      : entry,
                                  );
                                  setChecklistResults(next);
                                  setAdvancedResultsJson(serializeQaQcChecklistResults(next));
                                }}
                              />
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </fieldset>
                {canManageTemplates && checklistItems.length > 0 && (
                  <details className="rounded-lg border bg-muted/30 px-4 py-3 sm:col-span-2">
                    <summary className="cursor-pointer text-sm font-medium">
                      {t("construction.qaQc.advancedJson")}
                    </summary>
                    <div className="mt-3 space-y-3">
                      <p className="text-xs text-muted-foreground">
                        {t("construction.qaQc.checklistHelpAdvanced")}
                      </p>
                      <Textarea
                        id="qa-checklist"
                        aria-label={t("construction.qaQc.checklistLabel")}
                        className="min-h-28 font-mono text-xs"
                        value={advancedResultsJson}
                        onChange={(event) => setAdvancedResultsJson(event.target.value)}
                      />
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={applyAdvancedResults}
                      >
                        {t("construction.qaQc.applyJson")}
                      </Button>
                    </div>
                  </details>
                )}
                <div className="space-y-1.5 sm:col-span-2">
                  <span className="text-sm font-medium">{t("construction.qaQc.evidence")}</span>
                  <p className="text-xs text-muted-foreground">
                    {t("construction.qaQc.evidenceHint")}
                  </p>
                  {documents.length === 0 && (
                    <p className="text-xs text-muted-foreground">
                      {t("construction.qaQc.noEvidenceDocuments")}
                    </p>
                  )}
                  {evidence.map((item, index) => (
                    <div key={index} className="grid gap-2 sm:grid-cols-[1fr_1fr_auto]">
                      <SearchableSelect
                        data-testid={`qa-evidence-document-${index}`}
                        ariaLabel={t("construction.qaQc.selectEvidenceDocument")}
                        value={item.projectDocumentId}
                        onChange={(value) =>
                          setEvidence((current) =>
                            current.map((entry, entryIndex) =>
                              entryIndex === index ? { ...entry, projectDocumentId: value } : entry,
                            ),
                          )
                        }
                        placeholder={t("construction.qaQc.selectEvidenceDocument")}
                        options={documents.map((document) => ({
                          value: String(document.id),
                          label: document.originalFileName,
                        }))}
                      />
                      <Select
                        value={item.evidenceType}
                        onValueChange={(value: QaQcEvidenceType) =>
                          setEvidence((current) =>
                            current.map((entry, entryIndex) =>
                              entryIndex === index ? { ...entry, evidenceType: value } : entry,
                            ),
                          )
                        }
                      >
                        <SelectTrigger
                          data-testid={`qa-evidence-type-${index}`}
                          aria-label={t("construction.qaQc.evidenceType")}
                        >
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {evidenceTypes.map((type) => (
                            <SelectItem key={type} value={type}>
                              {t(`construction.qaQc.evidenceType.${type}`)}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <Button
                        type="button"
                        variant="outline"
                        onClick={() =>
                          setEvidence((current) => current.filter((_, i) => i !== index))
                        }
                      >
                        {t("construction.qaQc.removeEvidence")}
                      </Button>
                    </div>
                  ))}
                  <Button
                    data-testid="qa-add-evidence"
                    type="button"
                    variant="outline"
                    disabled={documents.length === 0}
                    onClick={() =>
                      setEvidence((current) => [
                        ...current,
                        { projectDocumentId: "", evidenceType: "General" },
                      ])
                    }
                  >
                    <Plus className="mr-2 h-4 w-4" />
                    {t("construction.qaQc.addEvidence")}
                  </Button>
                </div>
              </div>
            </div>
            <DialogFooter className="shrink-0 gap-2 border-t bg-background px-4 py-3 sm:px-6">
              <Button
                type="button"
                variant="outline"
                disabled={saving}
                onClick={() => setOpen(false)}
              >
                {t("common.cancel")}
              </Button>
              <Button
                type="submit"
                disabled={saving || (!ifcLoading && ifcReleases.length === 0)}
                data-testid="qa-qc-save"
              >
                {t(editing ? "common.save" : "construction.qaQc.createSubmit")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  );
}
