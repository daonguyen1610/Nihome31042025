import { useCallback, useEffect, useMemo, useState } from "react";
import { Check, Loader2, Pencil, Plus, Send, Sparkles, Trash2, Undo2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { SearchableSelect } from "@/components/ui/searchable-select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import {
  adminApi,
  type ConceptOptionResponse,
  type ConceptOptionStatus,
  type CreateConceptOptionRequest,
  type DesignProjectResponse,
} from "@/services/adminApi";

const STATUS_BADGE: Record<ConceptOptionStatus, string> = {
  Drafting: "border-sky-200 bg-sky-50 text-sky-700",
  PendingInternalReview: "border-indigo-200 bg-indigo-50 text-indigo-700",
  PresentedToClient: "border-violet-200 bg-violet-50 text-violet-700",
  ClientRequestedChanges: "border-amber-200 bg-amber-50 text-amber-700",
  Finalized: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Discarded: "border-slate-200 bg-slate-50 text-slate-500",
};

const formatDate = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString(lang);
  } catch {
    return iso;
  }
};

const toDateInputValue = (iso?: string | null) => (iso ? iso.slice(0, 10) : "");
const toUtcMidnight = (yyyyMmDd: string): string =>
  yyyyMmDd ? `${yyyyMmDd}T00:00:00.000Z` : "";

interface UserOption {
  id: number;
  fullName: string;
}

interface FormState {
  name: string;
  description: string;
  internalNote: string;
  ownerUserId: number | null;
  presentedAt: string;
}

const emptyForm = (): FormState => ({
  name: "",
  description: "",
  internalNote: "",
  ownerUserId: null,
  presentedAt: "",
});

interface Props {
  project: DesignProjectResponse;
  /** Called after every mutation so the parent can refresh its own cache
   * (stage may have shifted to BasicDesign after a finalize). */
  onProjectMayHaveChanged: () => void | Promise<void>;
}

/**
 * NIH-114 Concept options tab. Renders the option cards + supports the
 * full lifecycle: create · edit · status transitions · finalize (with
 * confirmation) · discard · delete. The parent detail page mounts this
 * inside its "Concept" tab.
 */
export const ConceptOptionsTab = ({ project, onProjectMayHaveChanged }: Props) => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.designConceptsManage);
  const canFinalize = has(ADMIN_PERMS.designConceptsFinalize);
  const canPickOwner = has(ADMIN_PERMS.users);

  const [rows, setRows] = useState<ConceptOptionResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [users, setUsers] = useState<UserOption[]>([]);

  const fetchRows = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.listConceptOptions({
        designProjectId: project.id,
        pageSize: 100,
      });
      setRows(data.items ?? []);
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setLoading(false);
    }
  }, [project.id]);

  useEffect(() => {
    void fetchRows();
  }, [fetchRows]);

  useEffect(() => {
    if (!canPickOwner) return;
    let cancelled = false;
    (async () => {
      try {
        const { data } = await adminApi.getUsers({ take: 200 });
        if (!cancelled) {
          setUsers(
            (data.items ?? []).map((u) => ({
              id: u.id,
              fullName: u.fullName ?? u.email ?? `#${u.id}`,
            })),
          );
        }
      } catch {
        // no permission → owner picker stays hidden
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [canPickOwner]);

  const userOptions = useMemo(
    () => users.map((u) => ({ value: String(u.id), label: u.fullName })),
    [users],
  );

  // Once the parent project is past Concept, options are read-only.
  const isLocked = project.currentStage !== "Concept";

  // -------- create / edit dialog --------
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<ConceptOptionResponse | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm());
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const isEdit = !!editing;

  const openCreate = () => {
    setEditing(null);
    setForm(emptyForm());
    setFormError(null);
    setDialogOpen(true);
  };

  const openEdit = (row: ConceptOptionResponse) => {
    setEditing(row);
    setForm({
      name: row.name,
      description: row.description ?? "",
      internalNote: row.internalNote ?? "",
      ownerUserId: row.ownerUserId ?? null,
      presentedAt: toDateInputValue(row.presentedAt),
    });
    setFormError(null);
    setDialogOpen(true);
  };

  const submitForm = async () => {
    setFormError(null);
    if (!form.name.trim()) {
      setFormError(t("concepts.form.nameRequired"));
      return;
    }
    setSaving(true);
    try {
      if (isEdit && editing) {
        await adminApi.updateConceptOption(editing.id, {
          name: form.name.trim(),
          description: form.description.trim() || null,
          internalNote: form.internalNote.trim() || null,
          ownerUserId: form.ownerUserId ?? null,
          presentedAt: form.presentedAt ? toUtcMidnight(form.presentedAt) : null,
        });
        toast({ title: t("concepts.updated") });
      } else {
        const payload: CreateConceptOptionRequest = {
          designProjectId: project.id,
          name: form.name.trim(),
          description: form.description.trim() || null,
          internalNote: form.internalNote.trim() || null,
          ownerUserId: form.ownerUserId ?? null,
          presentedAt: form.presentedAt ? toUtcMidnight(form.presentedAt) : null,
        };
        await adminApi.createConceptOption(payload);
        toast({ title: t("concepts.created") });
      }
      setDialogOpen(false);
      setEditing(null);
      await fetchRows();
    } catch (err) {
      setFormError(extractApiError(err));
    } finally {
      setSaving(false);
    }
  };

  // -------- delete confirmation --------
  const [deleting, setDeleting] = useState<ConceptOptionResponse | null>(null);
  const [busyDelete, setBusyDelete] = useState(false);
  const confirmDelete = async () => {
    if (!deleting) return;
    setBusyDelete(true);
    try {
      await adminApi.deleteConceptOption(deleting.id);
      toast({ title: t("concepts.deleted") });
      setDeleting(null);
      await fetchRows();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setBusyDelete(false);
    }
  };

  // -------- finalize confirmation --------
  const [finalizing, setFinalizing] = useState<ConceptOptionResponse | null>(null);
  const [busyFinalize, setBusyFinalize] = useState(false);
  const confirmFinalize = async () => {
    if (!finalizing) return;
    setBusyFinalize(true);
    try {
      await adminApi.transitionConceptOption(finalizing.id, { status: "Finalized" });
      toast({ title: t("concepts.finalized") });
      setFinalizing(null);
      await Promise.all([fetchRows(), onProjectMayHaveChanged()]);
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setBusyFinalize(false);
    }
  };

  // -------- generic status transition --------
  const [transitioning, setTransitioning] = useState<number | null>(null);
  const transition = async (row: ConceptOptionResponse, next: ConceptOptionStatus) => {
    setTransitioning(row.id);
    try {
      await adminApi.transitionConceptOption(row.id, { status: next });
      toast({ title: t("concepts.transitioned") });
      await fetchRows();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setTransitioning(null);
    }
  };

  if (loading) {
    return (
      <div className="rounded-lg border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">
        <Loader2 className="mx-auto mb-2 h-5 w-5 animate-spin" />
      </div>
    );
  }
  if (error) {
    return (
      <div className="rounded-lg border border-rose-200 bg-rose-50/60 p-4 text-sm text-rose-700">
        {error}
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {isLocked ? (
        <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
          {t("concepts.locked")}
        </p>
      ) : null}

      <header className="flex items-center justify-between gap-2">
        <h2 className="text-base font-semibold text-slate-900">
          {t("concepts.title")}
        </h2>
        {canManage && !isLocked ? (
          <Button size="sm" onClick={openCreate}>
            <Plus className="mr-1 h-4 w-4" />
            {t("concepts.new")}
          </Button>
        ) : null}
      </header>

      {rows.length === 0 ? (
        <div className="rounded-md border border-dashed p-10 text-center text-sm text-muted-foreground">
          {t("concepts.empty")}
        </div>
      ) : (
        <div className="grid gap-3 md:grid-cols-2">
          {rows.map((row) => (
            <article
              key={row.id}
              className={cn(
                "rounded-lg border bg-white p-3 shadow-sm",
                row.status === "Finalized" && "border-emerald-300 ring-1 ring-emerald-200",
                row.status === "Discarded" && "opacity-70",
              )}
            >
              <header className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <h3 className="break-words text-sm font-semibold leading-tight">{row.name}</h3>
                  {row.ownerName ? (
                    <p className="mt-0.5 text-xs text-muted-foreground">{row.ownerName}</p>
                  ) : null}
                </div>
                <Badge variant="outline" className={cn("whitespace-nowrap", STATUS_BADGE[row.status])}>
                  {t(`concepts.status.${row.status}`)}
                </Badge>
              </header>
              {row.description ? (
                <p className="mt-2 whitespace-pre-wrap break-words text-xs text-slate-700">
                  {row.description}
                </p>
              ) : null}
              <dl className="mt-2 grid grid-cols-2 gap-2 text-xs text-slate-600">
                <div>
                  <dt className="text-muted-foreground">{t("concepts.field.presentedAt")}</dt>
                  <dd className="font-medium">{formatDate(row.presentedAt, lang)}</dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">{t("concepts.field.updatedAt")}</dt>
                  <dd className="font-medium">{formatDate(row.updatedAt, lang)}</dd>
                </div>
              </dl>
              {canManage && !isLocked ? (
                <div className="mt-3 flex flex-wrap gap-1.5">
                  <RowActions
                    row={row}
                    t={t}
                    canFinalize={canFinalize}
                    busy={transitioning === row.id}
                    onEdit={() => openEdit(row)}
                    onDelete={() => setDeleting(row)}
                    onFinalize={() => setFinalizing(row)}
                    onTransition={(next) => void transition(row, next)}
                  />
                </div>
              ) : null}
            </article>
          ))}
        </div>
      )}

      {/* create / edit dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{isEdit ? t("concepts.edit") : t("concepts.new")}</DialogTitle>
            <DialogDescription>{t("concepts.form.hint")}</DialogDescription>
          </DialogHeader>
          <div className="grid gap-3">
            <div>
              <Label>{t("concepts.field.name")}</Label>
              <Input
                autoFocus
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
              />
            </div>
            <div>
              <Label>{t("concepts.field.description")}</Label>
              <Textarea
                rows={3}
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              />
            </div>
            <div>
              <Label>{t("concepts.field.internalNote")}</Label>
              <Textarea
                rows={2}
                value={form.internalNote}
                onChange={(e) => setForm((f) => ({ ...f, internalNote: e.target.value }))}
              />
            </div>
            {canPickOwner ? (
              <div>
                <Label>{t("concepts.field.owner")}</Label>
                <SearchableSelect
                  className="mt-1"
                  value={form.ownerUserId != null ? String(form.ownerUserId) : ""}
                  onChange={(v) =>
                    setForm((f) => ({ ...f, ownerUserId: v ? Number(v) : null }))
                  }
                  options={[{ value: "", label: t("concepts.form.ownerNone") }, ...userOptions]}
                  placeholder={t("concepts.form.ownerNone")}
                />
              </div>
            ) : null}
            <div>
              <Label>{t("concepts.field.presentedAt")}</Label>
              <Input
                type="date"
                value={form.presentedAt}
                onChange={(e) => setForm((f) => ({ ...f, presentedAt: e.target.value }))}
              />
            </div>
          </div>
          {formError ? <p className="text-sm text-rose-600">{formError}</p> : null}
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={saving}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submitForm()} disabled={saving}>
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* delete confirmation */}
      <AlertDialog open={!!deleting} onOpenChange={(o) => (!o ? setDeleting(null) : undefined)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("concepts.delete.confirmTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("concepts.delete.confirmBody").replace("{name}", deleting?.name ?? "")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busyDelete}>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              className="bg-rose-600 hover:bg-rose-700"
              onClick={(e) => {
                e.preventDefault();
                void confirmDelete();
              }}
              disabled={busyDelete}
            >
              {t("concepts.delete.confirm")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* finalize confirmation */}
      <AlertDialog open={!!finalizing} onOpenChange={(o) => (!o ? setFinalizing(null) : undefined)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("concepts.finalize.confirmTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("concepts.finalize.confirmBody").replace("{name}", finalizing?.name ?? "")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busyFinalize}>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              className="bg-emerald-600 hover:bg-emerald-700"
              onClick={(e) => {
                e.preventDefault();
                void confirmFinalize();
              }}
              disabled={busyFinalize}
            >
              {t("concepts.finalize.confirm")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
};

// ------------------------------ row actions ------------------------------

const RowActions = ({
  row,
  t,
  canFinalize,
  busy,
  onEdit,
  onDelete,
  onFinalize,
  onTransition,
}: {
  row: ConceptOptionResponse;
  t: (key: string) => string;
  canFinalize: boolean;
  busy: boolean;
  onEdit: () => void;
  onDelete: () => void;
  onFinalize: () => void;
  onTransition: (next: ConceptOptionStatus) => void;
}) => {
  const s = row.status;
  const btn = "h-8 text-xs";
  return (
    <>
      {s === "Drafting" && (
        <>
          <Button size="sm" variant="outline" className={btn} onClick={onEdit}>
            <Pencil className="mr-1 h-3.5 w-3.5" />
            {t("common.edit")}
          </Button>
          <Button
            size="sm"
            variant="secondary"
            className={btn}
            disabled={busy}
            onClick={() => onTransition("PendingInternalReview")}
          >
            <Send className="mr-1 h-3.5 w-3.5" />
            {t("concepts.action.sendReview")}
          </Button>
          <Button
            size="sm"
            variant="ghost"
            className={cn(btn, "text-rose-700 hover:bg-rose-50 hover:text-rose-800")}
            onClick={onDelete}
          >
            <Trash2 className="mr-1 h-3.5 w-3.5" />
            {t("concepts.action.delete")}
          </Button>
        </>
      )}
      {s === "PendingInternalReview" && (
        <>
          <Button size="sm" variant="outline" className={btn} onClick={onEdit}>
            <Pencil className="mr-1 h-3.5 w-3.5" />
            {t("common.edit")}
          </Button>
          <Button
            size="sm"
            variant="secondary"
            className={btn}
            disabled={busy}
            onClick={() => onTransition("PresentedToClient")}
          >
            <Send className="mr-1 h-3.5 w-3.5" />
            {t("concepts.action.markPresented")}
          </Button>
          <Button
            size="sm"
            variant="ghost"
            className={btn}
            disabled={busy}
            onClick={() => onTransition("Drafting")}
          >
            <Undo2 className="mr-1 h-3.5 w-3.5" />
            {t("concepts.action.backToDraft")}
          </Button>
        </>
      )}
      {s === "PresentedToClient" && (
        <>
          <Button size="sm" variant="outline" className={btn} onClick={onEdit}>
            <Pencil className="mr-1 h-3.5 w-3.5" />
            {t("common.edit")}
          </Button>
          {canFinalize ? (
            <Button size="sm" className={cn(btn, "bg-emerald-600 hover:bg-emerald-700")} onClick={onFinalize}>
              <Sparkles className="mr-1 h-3.5 w-3.5" />
              {t("concepts.action.finalize")}
            </Button>
          ) : null}
          <Button
            size="sm"
            variant="secondary"
            className={btn}
            disabled={busy}
            onClick={() => onTransition("ClientRequestedChanges")}
          >
            {t("concepts.action.markRevision")}
          </Button>
          <Button
            size="sm"
            variant="ghost"
            className={btn}
            disabled={busy}
            onClick={() => onTransition("Discarded")}
          >
            {t("concepts.action.discard")}
          </Button>
        </>
      )}
      {s === "ClientRequestedChanges" && (
        <>
          <Button size="sm" variant="outline" className={btn} onClick={onEdit}>
            <Pencil className="mr-1 h-3.5 w-3.5" />
            {t("common.edit")}
          </Button>
          <Button
            size="sm"
            variant="secondary"
            className={btn}
            disabled={busy}
            onClick={() => onTransition("Drafting")}
          >
            <Undo2 className="mr-1 h-3.5 w-3.5" />
            {t("concepts.action.backToDraft")}
          </Button>
        </>
      )}
      {s === "Finalized" && (
        <Badge variant="outline" className="border-emerald-200 bg-emerald-50 text-emerald-700">
          <Check className="mr-1 h-3.5 w-3.5" />
          {t("concepts.status.Finalized")}
        </Badge>
      )}
    </>
  );
};
