import { FormEvent, useCallback, useEffect, useState } from "react";
import { FileCheck2, Plus, Send } from "lucide-react";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { adminApi, type QaQcDossierResponse, type QaQcTemplateResponse } from "@/services/adminApi";

export default function QaQcWorkspacePage() {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const [projectId, setProjectId] = useState<number | null>(null);
  const selectProject = useCallback((id: number) => setProjectId(id), []);
  const [templates, setTemplates] = useState<QaQcTemplateResponse[]>([]);
  const [rows, setRows] = useState<QaQcDossierResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState({
    templateId: "",
    wbsCode: "",
    title: "",
    ifcReleaseId: "",
    checklistResultsJson: "[]",
    evidenceDocumentIds: "",
  });

  useEffect(() => {
    if (projectId == null) return;
    let active = true;
    setLoading(true);
    setError(null);
    void Promise.all([adminApi.listQaQcTemplates(projectId), adminApi.listQaQcDossiers(projectId)])
      .then(([templateResponse, dossierResponse]) => {
        if (!active) return;
        setTemplates(templateResponse.data);
        setRows(dossierResponse.data);
      })
      .catch(
        (cause) => active && setError(extractApiError(cause, t("construction.qaQc.loadError"))),
      )
      .finally(() => active && setLoading(false));
    return () => {
      active = false;
    };
  }, [projectId, reload, t]);

  const save = async (event: FormEvent) => {
    event.preventDefault();
    if (projectId == null) return;
    let checklist: unknown;
    try {
      checklist = JSON.parse(form.checklistResultsJson);
    } catch {
      toast({ variant: "destructive", title: t("construction.qaQc.invalidChecklistJson") });
      return;
    }
    setSaving(true);
    try {
      await adminApi.createQaQcDossier(projectId, {
        templateId: Number(form.templateId),
        wbsCode: form.wbsCode.trim(),
        title: form.title.trim(),
        ifcReleaseId: form.ifcReleaseId ? Number(form.ifcReleaseId) : null,
        checklistResultsJson: JSON.stringify(checklist),
        evidenceDocumentIds: form.evidenceDocumentIds
          .split(",")
          .map((value) => value.trim())
          .filter(Boolean)
          .map(Number),
      });
      setOpen(false);
      setForm({
        templateId: "",
        wbsCode: "",
        title: "",
        ifcReleaseId: "",
        checklistResultsJson: "[]",
        evidenceDocumentIds: "",
      });
      setReload((value) => value + 1);
      toast({ title: t("construction.qaQc.created") });
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("construction.qaQc.saveError")),
      });
    } finally {
      setSaving(false);
    }
  };

  const submit = async (row: QaQcDossierResponse) => {
    if (projectId == null) return;
    try {
      await adminApi.submitQaQcDossier(projectId, row.id, row.rowVersion);
      setReload((value) => value + 1);
      toast({ title: t("construction.common.submitted") });
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("construction.common.actionError")),
      });
    }
  };

  const publishedTemplates = templates.filter((template) => template.isPublished);
  return (
    <ConstructionWorkspaceShell
      title={t("construction.qaQc.title")}
      description={t("construction.qaQc.description")}
      projectId={projectId}
      onProjectChange={selectProject}
      testId="qa-qc-page"
    >
      <section className="space-y-4">
        <div className="flex flex-col gap-2 rounded-lg border bg-muted/20 p-3 text-sm sm:flex-row sm:items-center sm:justify-between">
          <span>
            {t("construction.qaQc.templateSummary", { count: publishedTemplates.length })}
          </span>
          {has(ADMIN_PERMS.constructionQaQcCreate) && (
            <Button
              onClick={() => setOpen(true)}
              disabled={publishedTemplates.length === 0}
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
          <PageEmpty message={t("construction.qaQc.empty")} />
        ) : (
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {rows.map((row) => (
              <article
                key={row.id}
                className="space-y-3 rounded-xl border bg-card p-4 shadow-sm"
                data-testid={`qa-qc-row-${row.id}`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="font-semibold">{row.code}</p>
                    <p className="text-sm text-muted-foreground">{row.title}</p>
                  </div>
                  <Badge variant="outline">{t(`construction.status.${row.status}`)}</Badge>
                </div>
                <dl className="grid grid-cols-2 gap-2 text-sm">
                  <div>
                    <dt className="text-muted-foreground">{t("construction.common.wbs")}</dt>
                    <dd>{row.wbsCode}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("construction.qaQc.template")}</dt>
                    <dd>
                      #{row.templateId} · v{row.templateVersion}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("construction.qaQc.ifcRelease")}</dt>
                    <dd>{row.ifcReleaseId ?? "—"}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("construction.qaQc.evidence")}</dt>
                    <dd>{row.evidenceDocumentIds.length.toLocaleString(lang)}</dd>
                  </div>
                </dl>
                {row.status === "Draft" && (
                  <Button
                    size="sm"
                    onClick={() => void submit(row)}
                    data-testid={`qa-qc-submit-${row.id}`}
                  >
                    <Send className="mr-2 h-4 w-4" />
                    {t("construction.common.submit")}
                  </Button>
                )}
              </article>
            ))}
          </div>
        )}
      </section>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl">
          <form onSubmit={save} className="space-y-4">
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <FileCheck2 className="h-5 w-5" />
                {t("construction.qaQc.new")}
              </DialogTitle>
            </DialogHeader>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-1.5 sm:col-span-2">
                <Label>{t("construction.qaQc.template")}</Label>
                <Select
                  required
                  value={form.templateId}
                  onValueChange={(value) => setForm({ ...form, templateId: value })}
                >
                  <SelectTrigger data-testid="qa-qc-template">
                    <SelectValue placeholder={t("construction.qaQc.selectTemplate")} />
                  </SelectTrigger>
                  <SelectContent>
                    {publishedTemplates.map((template) => (
                      <SelectItem key={template.id} value={String(template.id)}>
                        {template.nameVi} · v{template.version}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="qa-wbs">{t("construction.common.wbs")}</Label>
                <Input
                  id="qa-wbs"
                  required
                  maxLength={100}
                  value={form.wbsCode}
                  onChange={(e) => setForm({ ...form, wbsCode: e.target.value })}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="qa-ifc">{t("construction.qaQc.ifcRelease")}</Label>
                <Input
                  id="qa-ifc"
                  type="number"
                  min="1"
                  value={form.ifcReleaseId}
                  onChange={(e) => setForm({ ...form, ifcReleaseId: e.target.value })}
                />
              </div>
              <div className="space-y-1.5 sm:col-span-2">
                <Label htmlFor="qa-title">{t("construction.qaQc.dossierTitle")}</Label>
                <Input
                  id="qa-title"
                  required
                  maxLength={300}
                  value={form.title}
                  onChange={(e) => setForm({ ...form, title: e.target.value })}
                />
              </div>
              <div className="space-y-1.5 sm:col-span-2">
                <Label htmlFor="qa-checklist">{t("construction.qaQc.checklistJson")}</Label>
                <Textarea
                  id="qa-checklist"
                  required
                  className="min-h-28 font-mono text-xs"
                  value={form.checklistResultsJson}
                  onChange={(e) => setForm({ ...form, checklistResultsJson: e.target.value })}
                />
              </div>
              <div className="space-y-1.5 sm:col-span-2">
                <Label htmlFor="qa-evidence">{t("construction.qaQc.evidenceIds")}</Label>
                <Input
                  id="qa-evidence"
                  value={form.evidenceDocumentIds}
                  onChange={(e) => setForm({ ...form, evidenceDocumentIds: e.target.value })}
                  placeholder="101, 102"
                />
              </div>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setOpen(false)}>
                {t("common.cancel")}
              </Button>
              <Button type="submit" disabled={saving} data-testid="qa-qc-save">
                {t("common.save")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </ConstructionWorkspaceShell>
  );
}
