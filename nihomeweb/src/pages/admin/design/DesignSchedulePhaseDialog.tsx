import { useEffect, useRef, useState } from "react";
import { Loader2, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { newIdempotencyKey } from "@/lib/api";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { adminApi, type DesignSchedulePhaseResponse, type DesignScheduleStatus, type UpsertDesignSchedulePhaseRequest } from "@/services/adminApi";

interface Props {
  operationalProjectId: number;
  phase: DesignSchedulePhaseResponse | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => Promise<void> | void;
  onReload: () => Promise<DesignSchedulePhaseResponse | undefined>;
}

const transitions: Record<DesignScheduleStatus, DesignScheduleStatus[]> = {
  NotStarted: ["NotStarted", "InProgress", "OnHold", "WaitingForDepartment"],
  InProgress: ["InProgress", "Completed", "OnHold", "WaitingForDepartment"],
  OnHold: ["OnHold", "InProgress", "WaitingForDepartment"],
  WaitingForDepartment: ["WaitingForDepartment", "InProgress", "OnHold"],
  Completed: ["Completed"],
};

const toForm = (phase: DesignSchedulePhaseResponse): UpsertDesignSchedulePhaseRequest => ({
  plannedStart: phase.plannedStart.slice(0, 10),
  plannedEnd: phase.plannedEnd.slice(0, 10),
  actualStart: phase.actualStart?.slice(0, 10) ?? null,
  actualEnd: phase.actualEnd?.slice(0, 10) ?? null,
  status: phase.status,
  progressPercent: phase.progressPercent,
  weight: phase.weight,
  rowVersion: phase.rowVersion,
});

export const DesignSchedulePhaseDialog = ({ operationalProjectId, phase, open, onOpenChange, onSaved, onReload }: Props) => {
  const { t } = useI18n();
  const [form, setForm] = useState<UpsertDesignSchedulePhaseRequest | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [baseStatus, setBaseStatus] = useState<DesignScheduleStatus>("NotStarted");
  const idempotencyKey = useRef("");

  useEffect(() => {
    if (open && phase) {
      setForm(toForm(phase));
      setError(null);
      setConflict(false);
      setBaseStatus(phase.status);
      idempotencyKey.current = newIdempotencyKey();
    }
  }, [open, phase]);

  const validate = () => {
    if (!form?.plannedStart || !form.plannedEnd) return t("designProjects.schedule.validation.plannedRequired");
    if (form.plannedEnd < form.plannedStart) return t("designProjects.schedule.validation.plannedOrder");
    if (form.actualEnd && !form.actualStart) return t("designProjects.schedule.validation.actualStartRequired");
    if (form.actualStart && form.actualEnd && form.actualEnd < form.actualStart) return t("designProjects.schedule.validation.actualOrder");
    if (form.progressPercent < 0 || form.progressPercent > 100) return t("designProjects.schedule.validation.progressRange");
    if (form.weight < 1 || form.weight > 100) return t("designProjects.schedule.validation.weightRange");
    if (form.status === "NotStarted" && (form.actualStart || form.actualEnd || form.progressPercent !== 0)) return t("designProjects.schedule.validation.notStarted");
    if (form.status === "InProgress" && !form.actualStart) return t("designProjects.schedule.validation.inProgress");
    if (form.status === "Completed" && (!form.actualStart || !form.actualEnd || form.progressPercent !== 100)) return t("designProjects.schedule.validation.completed");
    if (form.status !== "Completed" && form.actualEnd) return t("designProjects.schedule.validation.actualEndCompleted");
    return null;
  };

  const save = async () => {
    if (!phase || !form) return;
    const validationError = validate();
    if (validationError) { setError(validationError); return; }
    setSaving(true);
    setError(null);
    setConflict(false);
    try {
      await adminApi.updateDesignSchedulePhase(operationalProjectId, phase.id, form, idempotencyKey.current);
      idempotencyKey.current = newIdempotencyKey();
      onOpenChange(false);
      await onSaved();
    } catch (saveError) {
      const status = (saveError as { response?: { status?: number } }).response?.status;
      if (status === 409) {
        setConflict(true);
        setError(t("designProjects.schedule.conflict"));
      } else setError(extractApiError(saveError));
    } finally { setSaving(false); }
  };

  const reloadVersion = async () => {
    const latest = await onReload();
    if (latest) {
      setForm(toForm(latest));
      setBaseStatus(latest.status);
      setConflict(false);
      setError(t("designProjects.schedule.conflictReloaded"));
    }
  };

  if (!form || !phase) return null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t("designProjects.schedule.phaseDialog.title", { phase: t(`designProjects.stage.${phase.code}`) })}</DialogTitle>
          <DialogDescription>{t("designProjects.schedule.phaseDialog.description")}</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 sm:grid-cols-2">
          <div><Label htmlFor="phase-planned-start">{t("designProjects.schedule.field.plannedStart")}</Label><Input id="phase-planned-start" className="mt-1 min-h-11" type="date" value={form.plannedStart} onChange={(event) => setForm({ ...form, plannedStart: event.target.value })} /></div>
          <div><Label htmlFor="phase-planned-end">{t("designProjects.schedule.field.plannedEnd")}</Label><Input id="phase-planned-end" className="mt-1 min-h-11" type="date" min={form.plannedStart} value={form.plannedEnd} onChange={(event) => setForm({ ...form, plannedEnd: event.target.value })} /></div>
          <div><Label htmlFor="phase-actual-start">{t("designProjects.schedule.field.actualStart")}</Label><Input id="phase-actual-start" className="mt-1 min-h-11" type="date" value={form.actualStart ?? ""} onChange={(event) => setForm({ ...form, actualStart: event.target.value || null })} /></div>
          <div><Label htmlFor="phase-actual-end">{t("designProjects.schedule.field.actualEnd")}</Label><Input id="phase-actual-end" className="mt-1 min-h-11" type="date" min={form.actualStart ?? undefined} value={form.actualEnd ?? ""} onChange={(event) => setForm({ ...form, actualEnd: event.target.value || null })} /></div>
          <div><Label>{t("designProjects.schedule.field.status")}</Label><Select value={form.status} onValueChange={(value) => setForm({ ...form, status: value as DesignScheduleStatus })}><SelectTrigger className="mt-1 min-h-11"><SelectValue /></SelectTrigger><SelectContent>{transitions[baseStatus].map((status) => <SelectItem key={status} value={status}>{t(`designProjects.schedule.status.${status}`)}</SelectItem>)}</SelectContent></Select></div>
          <div><Label htmlFor="phase-progress">{t("designProjects.schedule.field.progress")}</Label><Input id="phase-progress" className="mt-1 min-h-11" type="number" min={0} max={100} value={form.progressPercent} onChange={(event) => setForm({ ...form, progressPercent: Number(event.target.value) })} /></div>
          <div><Label htmlFor="phase-weight">{t("designProjects.schedule.field.weight")}</Label><Input id="phase-weight" className="mt-1 min-h-11" type="number" value={form.weight} readOnly aria-describedby="phase-weight-help" /><p id="phase-weight-help" className="mt-1 text-xs text-slate-500">{t("designProjects.schedule.phaseDialog.weightReadOnly")}</p></div>
        </div>
        {error ? <div className="rounded-md border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700" role="alert"><p>{error}</p>{conflict ? <Button className="mt-2 min-h-10" type="button" size="sm" variant="outline" onClick={() => void reloadVersion()}><RefreshCw className="mr-1.5 h-4 w-4" />{t("designProjects.schedule.action.reload")}</Button> : null}</div> : null}
        <DialogFooter><Button className="min-h-11" variant="outline" disabled={saving} onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button><Button className="min-h-11" disabled={saving} onClick={() => void save()}>{saving ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : null}{t("common.save")}</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  );
};
