import { useCallback, useEffect, useMemo, useState } from "react";
import { CheckCircle2, ChevronsRight, Circle, History, Loader2, Pencil, Plus, Send, ShieldCheck, Sparkles, Trash2, Undo2, XCircle } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { SearchableSelect } from "@/components/ui/searchable-select";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
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
  type BasicDesignDocResponse,
  type BasicDesignDocStatus,
  type BasicDesignReadiness,
  type CreateBasicDesignDocRequest,
  type DesignProjectResponse,
  type MasterDataOption,
} from "@/services/adminApi";
import { RevisionsPanel } from "./RevisionsPanel";

const STATUS_BADGE: Record<BasicDesignDocStatus, string> = {
  InProgress: "border-sky-200 bg-sky-50 text-sky-700",
  SubmittedForReview: "border-amber-200 bg-amber-50 text-amber-700",
  InternallyApproved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  SubmittedForPermit: "border-indigo-200 bg-indigo-50 text-indigo-700",
  PermitApproved: "border-emerald-300 bg-emerald-100 text-emerald-800",
  Rejected: "border-rose-200 bg-rose-50 text-rose-700",
};

const formatDate = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString(lang);
  } catch {
    return iso;
  }
};

interface UserOption {
  id: number;
  fullName: string;
}

interface FormState {
  disciplineCode: string;
  title: string;
  description: string;
  ownerUserId: number | null;
  note: string;
}

const emptyForm = (): FormState => ({
  disciplineCode: "",
  title: "",
  description: "",
  ownerUserId: null,
  note: "",
});

interface Props {
  project: DesignProjectResponse;
  onProjectMayHaveChanged: () => void | Promise<void>;
}

/**
 * NIH-115 Basic Design tab. Renders per-discipline docs, exposes the
 * status state-machine actions, and surfaces the 3-discipline readiness
 * gate with a one-click "Unlock Shop Drawing" button when green.
 */
export const BasicDesignTab = ({ project, onProjectMayHaveChanged }: Props) => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.designBasicManage);
  const canApprove = has(ADMIN_PERMS.designBasicApprove);
  const canUnlock = has(ADMIN_PERMS.designProjectsManage);
  const canViewRevisions = has(ADMIN_PERMS.designRevisions);
  const canPickOwner = has(ADMIN_PERMS.users);

  const isConceptStage = project.currentStage === "Concept";
  const isBasicStage = project.currentStage === "BasicDesign";
  const isLocked = !isBasicStage; // read-only when Concept (locked before) or Shop/Completed (locked after)

  const [rows, setRows] = useState<BasicDesignDocResponse[]>([]);
  const [readiness, setReadiness] = useState<BasicDesignReadiness>({
    requiredDisciplineCodes: [],
    internallyApprovedDisciplineCodes: [],
    readyForShopDrawing: false,
  });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [disciplines, setDisciplines] = useState<MasterDataOption[]>([]);
  const [users, setUsers] = useState<UserOption[]>([]);

  const fetchRows = useCallback(async () => {
    setError(null);
    try {
      const { data } = await adminApi.listBasicDesignDocs({
        designProjectId: project.id,
        pageSize: 200,
      });
      setRows(data.items ?? []);
      setReadiness(data.readiness);
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
    let cancelled = false;
    (async () => {
      try {
        const { data } = await adminApi.getMasterDataOptions("design_discipline");
        if (!cancelled) setDisciplines((data ?? []).filter((o) => o.isActive));
      } catch {
        // non-fatal
      }
      if (canPickOwner) {
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
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [canPickOwner]);

  const disciplineOptions = useMemo(
    () => disciplines.map((d) => ({ value: d.code, label: d.name })),
    [disciplines],
  );
  const userOptions = useMemo(
    () => users.map((u) => ({ value: String(u.id), label: u.fullName })),
    [users],
  );

  const disciplineLabelByCode = useMemo(() => {
    const m = new Map<string, string>();
    for (const d of disciplines) m.set(d.code, d.name);
    return m;
  }, [disciplines]);

  const rowsByDiscipline = useMemo(() => {
    const groups = new Map<string, BasicDesignDocResponse[]>();
    for (const r of rows) {
      const bucket = groups.get(r.disciplineCode) ?? [];
      bucket.push(r);
      groups.set(r.disciplineCode, bucket);
    }
    return groups;
  }, [rows]);

  // -------- revisions panel --------
  const [revisionsFor, setRevisionsFor] = useState<BasicDesignDocResponse | null>(null);

  // -------- create / edit dialog --------
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<BasicDesignDocResponse | null>(null);
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
  const openEdit = (row: BasicDesignDocResponse) => {
    setEditing(row);
    setForm({
      disciplineCode: row.disciplineCode,
      title: row.title,
      description: row.description ?? "",
      ownerUserId: row.ownerUserId ?? null,
      note: row.note ?? "",
    });
    setFormError(null);
    setDialogOpen(true);
  };

  const submitForm = async () => {
    setFormError(null);
    if (!form.title.trim()) {
      setFormError(t("basicDesign.form.titleRequired"));
      return;
    }
    if (!form.disciplineCode) {
      setFormError(t("basicDesign.form.disciplineRequired"));
      return;
    }
    setSaving(true);
    try {
      if (isEdit && editing) {
        await adminApi.updateBasicDesignDoc(editing.id, {
          disciplineCode: form.disciplineCode,
          title: form.title.trim(),
          description: form.description.trim() || null,
          ownerUserId: form.ownerUserId ?? null,
          note: form.note.trim() || null,
        });
        toast({ title: t("basicDesign.updated") });
      } else {
        const payload: CreateBasicDesignDocRequest = {
          designProjectId: project.id,
          disciplineCode: form.disciplineCode,
          title: form.title.trim(),
          description: form.description.trim() || null,
          ownerUserId: form.ownerUserId ?? null,
          note: form.note.trim() || null,
        };
        await adminApi.createBasicDesignDoc(payload);
        toast({ title: t("basicDesign.created") });
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
  const [deleting, setDeleting] = useState<BasicDesignDocResponse | null>(null);
  const [busyDelete, setBusyDelete] = useState(false);
  const confirmDelete = async () => {
    if (!deleting) return;
    setBusyDelete(true);
    try {
      await adminApi.deleteBasicDesignDoc(deleting.id);
      toast({ title: t("basicDesign.deleted") });
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

  // -------- transitions --------
  const [transitioning, setTransitioning] = useState<number | null>(null);
  const transition = async (row: BasicDesignDocResponse, next: BasicDesignDocStatus) => {
    setTransitioning(row.id);
    try {
      await adminApi.transitionBasicDesignDoc(row.id, { status: next });
      toast({ title: t("basicDesign.transitioned") });
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

  // -------- unlock shop drawing --------
  const [busyUnlock, setBusyUnlock] = useState(false);
  const doUnlock = async () => {
    setBusyUnlock(true);
    try {
      await adminApi.unlockShopDrawing(project.id);
      toast({ title: t("basicDesign.readiness.unlocked") });
      await Promise.all([fetchRows(), onProjectMayHaveChanged()]);
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setBusyUnlock(false);
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
    <div className="space-y-4">
      {isConceptStage ? (
        <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
          {t("basicDesign.locked")}
        </p>
      ) : project.currentStage !== "BasicDesign" ? (
        <p className="rounded-md border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-700">
          {t("basicDesign.lockedShop")}
        </p>
      ) : null}

      {/* Readiness gate */}
      {isBasicStage || readiness.requiredDisciplineCodes.length > 0 ? (
        <section
          className={cn(
            "rounded-lg border p-3 shadow-sm",
            readiness.readyForShopDrawing
              ? "border-emerald-200 bg-emerald-50/60"
              : "border-slate-200 bg-slate-50",
          )}
        >
          <div className="flex flex-wrap items-start justify-between gap-2">
            <div>
              <p className="flex items-center gap-1.5 text-sm font-semibold text-slate-900">
                <ShieldCheck className="h-4 w-4" />
                {t("basicDesign.readiness.title")}
              </p>
              <p className="mt-0.5 text-xs text-slate-600">{t("basicDesign.readiness.hint")}</p>
              <div className="mt-2 flex flex-wrap gap-1.5">
                {readiness.requiredDisciplineCodes.map((code) => {
                  const ok = readiness.internallyApprovedDisciplineCodes
                    .map((c) => c.toLowerCase())
                    .includes(code.toLowerCase());
                  return (
                    <Badge
                      key={code}
                      variant="outline"
                      className={cn(
                        "gap-1 whitespace-nowrap",
                        ok
                          ? "border-emerald-200 bg-emerald-50 text-emerald-700"
                          : "border-slate-300 bg-white text-slate-500",
                      )}
                    >
                      {ok ? <CheckCircle2 className="h-3 w-3" /> : <Circle className="h-3 w-3" />}
                      {disciplineLabelByCode.get(code) ?? code}
                      <span className="text-[10px] opacity-80">
                        · {ok ? t("basicDesign.readiness.approved") : t("basicDesign.readiness.missing")}
                      </span>
                    </Badge>
                  );
                })}
              </div>
            </div>
            {isBasicStage && canUnlock ? (
              <Button
                size="sm"
                className={cn(
                  "gap-1.5",
                  readiness.readyForShopDrawing
                    ? "bg-emerald-600 hover:bg-emerald-700"
                    : "",
                )}
                disabled={!readiness.readyForShopDrawing || busyUnlock}
                onClick={() => void doUnlock()}
              >
                <ChevronsRight className="h-4 w-4" />
                {t("basicDesign.readiness.unlock")}
              </Button>
            ) : null}
          </div>
        </section>
      ) : null}

      <header className="flex items-center justify-between gap-2">
        <h2 className="text-base font-semibold text-slate-900">{t("basicDesign.title")}</h2>
        {canManage && isBasicStage ? (
          <Button size="sm" onClick={openCreate}>
            <Plus className="mr-1 h-4 w-4" />
            {t("basicDesign.new")}
          </Button>
        ) : null}
      </header>

      {rows.length === 0 ? (
        <div className="rounded-md border border-dashed p-10 text-center text-sm text-muted-foreground">
          {t("basicDesign.empty")}
        </div>
      ) : (
        <div className="space-y-4">
          {Array.from(rowsByDiscipline.entries()).map(([code, docs]) => (
            <section key={code} className="rounded-lg border border-slate-200 bg-white shadow-sm">
              <header className="flex items-center justify-between border-b border-slate-100 px-3 py-2">
                <p className="text-sm font-semibold text-slate-800">
                  {disciplineLabelByCode.get(code) ?? code}
                </p>
                <span className="text-xs text-slate-500">{docs.length}</span>
              </header>
              <ul className="divide-y divide-slate-100">
                {docs.map((row) => (
                  <li key={row.id} className="p-3">
                    <div className="flex flex-wrap items-start justify-between gap-2">
                      <div className="min-w-0">
                        <p className="font-mono text-xs text-slate-500">{row.documentCode}</p>
                        <p className="mt-0.5 break-words text-sm font-medium text-slate-900">
                          {row.title}
                        </p>
                        {row.ownerName ? (
                          <p className="mt-0.5 text-xs text-muted-foreground">{row.ownerName}</p>
                        ) : null}
                      </div>
                      <div className="flex flex-col items-end gap-1">
                        <Badge variant="outline" className={cn("whitespace-nowrap", STATUS_BADGE[row.status])}>
                          {t(`basicDesign.status.${row.status}`)}
                        </Badge>
                        <span className="text-[10px] text-muted-foreground">
                          {formatDate(row.updatedAt, lang)}
                        </span>
                        {canViewRevisions ? (
                          <Button
                            variant="ghost"
                            size="sm"
                            className="h-6 gap-1 px-1.5 text-[11px] text-slate-600 hover:text-slate-900"
                            onClick={() => setRevisionsFor(row)}
                            data-testid={`basic-design-revisions-${row.id}`}
                          >
                            <History className="h-3 w-3" />
                            {t("drawingRevision.button")}
                          </Button>
                        ) : null}
                      </div>
                    </div>
                    {canManage && isBasicStage ? (
                      <div className="mt-2 flex flex-wrap gap-1.5">
                        <RowActions
                          row={row}
                          t={t}
                          canApprove={canApprove}
                          busy={transitioning === row.id}
                          onEdit={() => openEdit(row)}
                          onDelete={() => setDeleting(row)}
                          onTransition={(next) => void transition(row, next)}
                        />
                      </div>
                    ) : null}
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </div>
      )}

      {/* create / edit dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{isEdit ? t("basicDesign.edit") : t("basicDesign.new")}</DialogTitle>
            <DialogDescription>{t("basicDesign.form.hint")}</DialogDescription>
          </DialogHeader>
          <div className="grid gap-3">
            <div>
              <Label>{t("basicDesign.field.discipline")}</Label>
              <Select
                value={form.disciplineCode}
                onValueChange={(v) => setForm((f) => ({ ...f, disciplineCode: v }))}
                disabled={isEdit && editing?.status !== "InProgress"}
              >
                <SelectTrigger className="mt-1 h-9">
                  <SelectValue placeholder={t("basicDesign.form.disciplinePlaceholder")} />
                </SelectTrigger>
                <SelectContent>
                  {disciplineOptions.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>{t("basicDesign.field.title")}</Label>
              <Input
                autoFocus
                value={form.title}
                onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
              />
            </div>
            <div>
              <Label>{t("basicDesign.field.description")}</Label>
              <Textarea
                rows={3}
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              />
            </div>
            {canPickOwner ? (
              <div>
                <Label>{t("basicDesign.field.owner")}</Label>
                <SearchableSelect
                  className="mt-1"
                  value={form.ownerUserId != null ? String(form.ownerUserId) : ""}
                  onChange={(v) =>
                    setForm((f) => ({ ...f, ownerUserId: v ? Number(v) : null }))
                  }
                  options={[{ value: "", label: t("basicDesign.form.ownerNone") }, ...userOptions]}
                  placeholder={t("basicDesign.form.ownerNone")}
                />
              </div>
            ) : null}
            <div>
              <Label>{t("basicDesign.field.note")}</Label>
              <Textarea
                rows={2}
                value={form.note}
                onChange={(e) => setForm((f) => ({ ...f, note: e.target.value }))}
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
            <AlertDialogTitle>{t("basicDesign.delete.confirmTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("basicDesign.delete.confirmBody").replace("{code}", deleting?.documentCode ?? "")}
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
              {t("basicDesign.delete.confirm")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {revisionsFor ? (
        <RevisionsPanel
          open={!!revisionsFor}
          onOpenChange={(v) => (!v ? setRevisionsFor(null) : undefined)}
          targetType="BasicDesignDoc"
          targetId={revisionsFor.id}
          targetCode={revisionsFor.documentCode}
          targetTitle={revisionsFor.title}
        />
      ) : null}
    </div>
  );
};

// ------------------------------ row actions ------------------------------

const RowActions = ({
  row,
  t,
  canApprove,
  busy,
  onEdit,
  onDelete,
  onTransition,
}: {
  row: BasicDesignDocResponse;
  t: (key: string) => string;
  canApprove: boolean;
  busy: boolean;
  onEdit: () => void;
  onDelete: () => void;
  onTransition: (next: BasicDesignDocStatus) => void;
}) => {
  const s = row.status;
  const btn = "h-8 text-xs";
  return (
    <>
      {s === "InProgress" && (
        <>
          <Button size="sm" variant="outline" className={btn} onClick={onEdit}>
            <Pencil className="mr-1 h-3.5 w-3.5" />
            {t("common.edit")}
          </Button>
          <Button size="sm" variant="secondary" className={btn} disabled={busy}
            onClick={() => onTransition("SubmittedForReview")}>
            <Send className="mr-1 h-3.5 w-3.5" />
            {t("basicDesign.action.sendReview")}
          </Button>
          <Button size="sm" variant="ghost" className={cn(btn, "text-rose-700 hover:bg-rose-50 hover:text-rose-800")}
            onClick={onDelete}>
            <Trash2 className="mr-1 h-3.5 w-3.5" />
            {t("basicDesign.action.delete")}
          </Button>
        </>
      )}
      {s === "SubmittedForReview" && (
        <>
          <Button size="sm" variant="outline" className={btn} onClick={onEdit}>
            <Pencil className="mr-1 h-3.5 w-3.5" />
            {t("common.edit")}
          </Button>
          {canApprove ? (
            <Button size="sm" className={cn(btn, "bg-emerald-600 hover:bg-emerald-700")} disabled={busy}
              onClick={() => onTransition("InternallyApproved")}>
              <Sparkles className="mr-1 h-3.5 w-3.5" />
              {t("basicDesign.action.approve")}
            </Button>
          ) : null}
          <Button size="sm" variant="ghost" className={btn} disabled={busy}
            onClick={() => onTransition("InProgress")}>
            <Undo2 className="mr-1 h-3.5 w-3.5" />
            {t("basicDesign.action.backToDraft")}
          </Button>
          <Button size="sm" variant="ghost" className={cn(btn, "text-rose-700 hover:bg-rose-50")} disabled={busy}
            onClick={() => onTransition("Rejected")}>
            <XCircle className="mr-1 h-3.5 w-3.5" />
            {t("basicDesign.action.reject")}
          </Button>
        </>
      )}
      {s === "InternallyApproved" && (
        <>
          <Button size="sm" variant="outline" className={btn} onClick={onEdit}>
            <Pencil className="mr-1 h-3.5 w-3.5" />
            {t("common.edit")}
          </Button>
          <Button size="sm" variant="secondary" className={btn} disabled={busy}
            onClick={() => onTransition("SubmittedForPermit")}>
            {t("basicDesign.action.submitPermit")}
          </Button>
        </>
      )}
      {s === "SubmittedForPermit" && (
        <>
          <Button size="sm" className={cn(btn, "bg-emerald-600 hover:bg-emerald-700")} disabled={busy}
            onClick={() => onTransition("PermitApproved")}>
            <CheckCircle2 className="mr-1 h-3.5 w-3.5" />
            {t("basicDesign.action.markPermitApproved")}
          </Button>
          <Button size="sm" variant="ghost" className={cn(btn, "text-rose-700 hover:bg-rose-50")} disabled={busy}
            onClick={() => onTransition("Rejected")}>
            <XCircle className="mr-1 h-3.5 w-3.5" />
            {t("basicDesign.action.reject")}
          </Button>
        </>
      )}
      {s === "PermitApproved" && (
        <Badge variant="outline" className="border-emerald-300 bg-emerald-100 text-emerald-800">
          <CheckCircle2 className="mr-1 h-3.5 w-3.5" />
          {t("basicDesign.status.PermitApproved")}
        </Badge>
      )}
      {s === "Rejected" && (
        <Badge variant="outline" className="border-rose-200 bg-rose-50 text-rose-700">
          <XCircle className="mr-1 h-3.5 w-3.5" />
          {t("basicDesign.status.Rejected")}
        </Badge>
      )}
    </>
  );
};
