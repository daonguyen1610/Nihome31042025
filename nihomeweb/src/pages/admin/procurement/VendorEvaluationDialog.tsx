import { useEffect, useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { Textarea } from "@/components/ui/textarea";
import { useI18n } from "@/lib/i18n";
import type { UpsertVendorEvaluationRequest, VendorEvaluationResponse, VendorProjectOptionResponse } from "@/services/vendorApi";

interface VendorEvaluationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  evaluation: VendorEvaluationResponse | null;
  projects: VendorProjectOptionResponse[];
  saving: boolean;
  apiError: string | null;
  onSave: (request: UpsertVendorEvaluationRequest) => Promise<void>;
}

const emptyEvaluation = (): UpsertVendorEvaluationRequest => ({
  projectId: 0,
  scoreQuality: 0,
  scoreSchedule: 0,
  scoreCost: 0,
  scoreSafety: 0,
  comment: "",
});

const VendorEvaluationDialog = ({ open, onOpenChange, evaluation, projects, saving, apiError, onSave }: VendorEvaluationDialogProps) => {
  const { t } = useI18n();
  const [form, setForm] = useState<UpsertVendorEvaluationRequest>(emptyEvaluation);
  const [validationError, setValidationError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setForm(evaluation ? {
      projectId: evaluation.projectId,
      scoreQuality: evaluation.scoreQuality,
      scoreSchedule: evaluation.scoreSchedule,
      scoreCost: evaluation.scoreCost,
      scoreSafety: evaluation.scoreSafety,
      comment: evaluation.comment ?? "",
    } : emptyEvaluation());
    setValidationError(null);
  }, [evaluation, open]);

  const projectOptions = useMemo(() => projects.map((project) => ({
    value: String(project.id),
    label: project.name,
    hint: project.projectCode,
    keywords: project.projectCode,
  })), [projects]);

  const scoreFields = ["scoreQuality", "scoreSchedule", "scoreCost", "scoreSafety"] as const;
  const average = scoreFields.reduce((sum, field) => sum + Number(form[field] || 0), 0) / scoreFields.length;

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!form.projectId) {
      setValidationError(t("vendors.evaluation.validation.projectRequired"));
      return;
    }
    if (scoreFields.some((field) => !Number.isInteger(Number(form[field])) || Number(form[field]) < 0 || Number(form[field]) > 10)) {
      setValidationError(t("vendors.evaluation.validation.scoreRange"));
      return;
    }
    setValidationError(null);
    await onSave({ ...form, comment: form.comment?.trim() || null });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <form onSubmit={submit}>
          <DialogHeader>
            <DialogTitle>{t(evaluation ? "vendors.evaluation.editTitle" : "vendors.evaluation.createTitle")}</DialogTitle>
            <DialogDescription>{t("vendors.evaluation.dialogDescription")}</DialogDescription>
          </DialogHeader>
          <div className="mt-4 space-y-4">
            <div className="space-y-1">
              <Label>{t("vendors.evaluation.project")} *</Label>
              <SearchableSelect value={form.projectId ? String(form.projectId) : null} onChange={(value) => setForm((current) => ({ ...current, projectId: Number(value) }))} options={projectOptions} disabled={Boolean(evaluation)} placeholder={t("vendors.evaluation.selectProject")} searchPlaceholder={t("vendors.evaluation.searchProject")} emptyText={t("vendors.evaluation.noProjects")} />
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              {scoreFields.map((field) => (
                <div key={field} className="space-y-1">
                  <Label htmlFor={`evaluation-${field}`}>{t(`vendors.evaluation.${field}`)}</Label>
                  <Input id={`evaluation-${field}`} type="number" min={0} max={10} step={1} value={form[field]} onChange={(event) => setForm((current) => ({ ...current, [field]: Number(event.target.value) }))} />
                </div>
              ))}
            </div>
            <div className="rounded-md border bg-slate-50 p-3 text-sm"><span className="text-slate-500">{t("vendors.evaluation.averagePreview")}</span><strong className="ml-2">{average.toFixed(2)}</strong></div>
            <div className="space-y-1"><Label htmlFor="evaluation-comment">{t("vendors.evaluation.comment")}</Label><Textarea id="evaluation-comment" maxLength={1000} value={form.comment ?? ""} onChange={(event) => setForm((current) => ({ ...current, comment: event.target.value }))} /></div>
            {validationError || apiError ? <p role="alert" className="text-sm text-destructive">{validationError ?? apiError}</p> : null}
          </div>
          <DialogFooter className="mt-6"><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button><Button type="submit" disabled={saving}>{t(saving ? "vendors.form.saving" : "common.save")}</Button></DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
};

export default VendorEvaluationDialog;
