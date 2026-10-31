import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Check,
  ChevronDown,
  ChevronUp,
  Loader2,
  Pencil,
  Plus,
  Sparkles,
  Trash2,
} from "lucide-react";
import { ConceptDossierPanel } from "./ConceptDossierPanel";
import { useDesignTeamOwners } from "./useDesignTeamOwners";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { DeletionImpactDialog } from "@/components/admin/DeletionImpactDialog";
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
import { translateError, useI18n } from "@/lib/i18n";
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
  type DeletionImpactResponse,
  type DesignReviewResponse,
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

interface FormState {
  name: string;
  description: string;
  internalNote: string;
  ownerUserId: number | null;
}

const emptyForm = (): FormState => ({
  name: "",
  description: "",
  internalNote: "",
  ownerUserId: null,
});

interface Props {
  project: DesignProjectResponse;
  onOpenReviews: () => void;
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
export const ConceptOptionsTab = ({ project, onProjectMayHaveChanged, onOpenReviews }: Props) => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.designConceptsManage);
  const canFinalize = has(ADMIN_PERMS.designConceptsFinalize);
  const canHardDelete = has(ADMIN_PERMS.designRecordsHardDelete);
  const canPickOwner = canManage;
  const {
    options: userOptions,
    error: ownersError,
    loading: ownersLoading,
  } = useDesignTeamOwners(project, canPickOwner);

  const [rows, setRows] = useState<ConceptOptionResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [reviews, setReviews] = useState<DesignReviewResponse[]>([]);
  const confirmationTrigger = useRef<HTMLButtonElement | null>(null);
  const restoreConfirmationFocus = (event: Event) => {
    if (confirmationTrigger.current?.isConnected) {
      event.preventDefault();
      confirmationTrigger.current.focus();
    }
  };

  const fetchRows = useCallback(
    async (refresh = false) => {
      if (!refresh) setLoading(true);
      setError(null);
      try {
        const [options, reviewResult] = await Promise.allSettled([
          adminApi.listConceptOptions({ designProjectId: project.id, pageSize: 100 }),
          adminApi.listDesignReviews(project.id),
        ]);
        if (options.status === "fulfilled") setRows(options.value.data.items ?? []);
        if (reviewResult.status === "fulfilled") setReviews(reviewResult.value.data);
        else setReviews([]);
        if (options.status === "rejected") setError(extractApiError(options.reason));
        else if (reviewResult.status === "rejected") setError(extractApiError(reviewResult.reason));
      } catch (err) {
        setError(extractApiError(err));
      } finally {
        setLoading(false);
      }
    },
    [project.id],
  );

  const refreshRows = useCallback(() => fetchRows(true), [fetchRows]);
  const latestReviews = useMemo(() => {
    const latest = new Map<number, DesignReviewResponse>();
    for (const review of reviews) {
      if (
        review.conceptOptionId &&
        review.status !== "Draft" &&
        review.revisionNumber > (latest.get(review.conceptOptionId)?.revisionNumber ?? -1)
      ) {
        latest.set(review.conceptOptionId, review);
      }
    }
    return latest;
  }, [reviews]);
  const expandedRow = rows.find((row) => row.id === expandedId);

  useEffect(() => {
    void fetchRows();
  }, [fetchRows]);

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
        });
        toast({ title: t("concepts.updated") });
      } else {
        const payload: CreateConceptOptionRequest = {
          designProjectId: project.id,
          name: form.name.trim(),
          description: form.description.trim() || null,
          internalNote: form.internalNote.trim() || null,
          ownerUserId: form.ownerUserId ?? null,
        };
        await adminApi.createConceptOption(payload);
        toast({ title: t("concepts.created") });
      }
      setDialogOpen(false);
      setEditing(null);
      await fetchRows();
    } catch (err) {
      setFormError(translateError(t, extractApiError(err)));
    } finally {
      setSaving(false);
    }
  };

  // -------- delete confirmation --------
  const [deleting, setDeleting] = useState<ConceptOptionResponse | null>(null);
  const [busyDelete, setBusyDelete] = useState(false);
  const [deleteImpact, setDeleteImpact] = useState<DeletionImpactResponse | null>(null);
  const [loadingDeleteImpact, setLoadingDeleteImpact] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  useEffect(() => {
    if (!deleting) {
      setDeleteImpact(null);
      setDeleteError(null);
      return;
    }
    let active = true;
    setLoadingDeleteImpact(true);
    setDeleteError(null);
    void adminApi
      .getConceptOptionDeletionImpact(deleting.id)
      .then(({ data }) => {
        if (active) setDeleteImpact(data);
      })
      .catch((err) => {
        if (active) setDeleteError(extractApiError(err));
      })
      .finally(() => {
        if (active) setLoadingDeleteImpact(false);
      });
    return () => {
      active = false;
    };
  }, [deleting]);

  const confirmDelete = async (confirmation: string) => {
    if (!deleting || !deleteImpact) return null;
    setBusyDelete(true);
    setDeleteError(null);
    try {
      await adminApi.deleteConceptOption(deleting.id, {
        planToken: deleteImpact.planToken,
        confirmation,
      });
      toast({ title: t("concepts.deleted") });
      return null;
    } catch (err) {
      setDeleteError(extractApiError(err));
      throw err;
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
  const [discarding, setDiscarding] = useState<ConceptOptionResponse | null>(null);
  const transition = async (row: ConceptOptionResponse, next: ConceptOptionStatus) => {
    setTransitioning(row.id);
    try {
      await adminApi.transitionConceptOption(row.id, { status: next });
      toast({ title: t("concepts.transitioned") });
      await refreshRows();
      return true;
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
      return false;
    } finally {
      setTransitioning(null);
    }
  };

  if (loading) {
    return (
      <div
        role="status"
        className="rounded-lg border border-slate-200 bg-white p-8 text-center text-sm text-slate-500"
      >
        <Loader2 aria-hidden="true" className="mx-auto mb-2 h-5 w-5 animate-spin" />
        {t("common.loading")}
      </div>
    );
  }
  return (
    <div className="space-y-3">
      {error ? (
        <div
          role="alert"
          className="rounded-lg border border-rose-200 bg-rose-50/60 p-4 text-sm text-rose-700"
        >
          <p>{error}</p>
          <Button className="mt-2 min-h-11" variant="outline" onClick={() => void refreshRows()}>
            {t("common.retry")}
          </Button>
        </div>
      ) : null}
      {isLocked ? (
        <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
          {t("concepts.locked")}
        </p>
      ) : null}

      <header className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-base font-semibold text-slate-900">{t("concepts.title")}</h2>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" className="min-h-11" onClick={onOpenReviews}>
            {t("designProjects.detail.tab.reviews")}
          </Button>
          {canManage && !isLocked ? (
            <Button className="min-h-11" onClick={openCreate}>
              <Plus className="mr-1 h-4 w-4" />
              {t("concepts.new")}
            </Button>
          ) : null}
        </div>
      </header>

      {rows.length === 0 && !error ? (
        <div className="rounded-md border border-dashed p-10 text-center text-sm text-muted-foreground">
          {t("concepts.empty")}
        </div>
      ) : null}
      {rows.length > 0 ? (
        <div className="grid gap-3 md:grid-cols-2">
          {rows.map((row) => (
            <article
              key={row.id}
              className={cn(
                "min-w-0 rounded-lg border bg-white p-4 shadow-sm",
                row.status === "Finalized" && "border-emerald-300 ring-1 ring-emerald-200",
                row.status === "Discarded" && "border-slate-200 bg-slate-50/40",
              )}
            >
              <header className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <h3 className="break-words text-sm font-semibold leading-tight">{row.name}</h3>
                  {row.ownerName ? (
                    <p className="mt-0.5 text-xs text-muted-foreground">{row.ownerName}</p>
                  ) : null}
                </div>
                <Badge
                  variant="outline"
                  className={cn("whitespace-nowrap", STATUS_BADGE[row.status])}
                >
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
                    finalizeReady={
                      latestReviews.get(row.id)?.status === "ClientApproved" &&
                      !!latestReviews.get(row.id)?.clientAcceptanceDocumentId &&
                      !!latestReviews.get(row.id)?.clientAcceptanceSignedBy &&
                      !!latestReviews.get(row.id)?.clientAcceptanceSignedAt
                    }
                    canHardDelete={canHardDelete}
                    busy={transitioning === row.id}
                    onEdit={() => openEdit(row)}
                    onDelete={() => setDeleting(row)}
                    onFinalize={(trigger) => {
                      confirmationTrigger.current = trigger;
                      setFinalizing(row);
                    }}
                    onDiscard={(trigger) => {
                      confirmationTrigger.current = trigger;
                      setDiscarding(row);
                    }}
                  />
                </div>
              ) : null}
              {canHardDelete && row.status === "Discarded" && (!canManage || isLocked) ? (
                <Button
                  size="sm"
                  variant="ghost"
                  className="mt-3 h-8 text-xs text-rose-700"
                  onClick={() => setDeleting(row)}
                >
                  <Trash2 className="mr-1 h-3.5 w-3.5" />
                  {t("concepts.action.delete")}
                </Button>
              ) : null}
              {project.operationalProjectId ? (
                <Button
                  size="sm"
                  variant="outline"
                  className="mt-3 min-h-11"
                  aria-expanded={expandedId === row.id}
                  aria-controls={`concept-dossier-${row.id}`}
                  onClick={() => setExpandedId(expandedId === row.id ? null : row.id)}
                >
                  {t("concepts.dossier.open")}
                  {expandedId === row.id ? (
                    <ChevronUp className="ml-2 h-4 w-4" />
                  ) : (
                    <ChevronDown className="ml-2 h-4 w-4" />
                  )}
                </Button>
              ) : null}
            </article>
          ))}
        </div>
      ) : null}

      {expandedRow && project.operationalProjectId ? (
        <div className="min-w-0 rounded-lg border bg-white p-4 sm:p-6">
          <div className="flex flex-wrap items-start justify-between gap-3 border-b pb-4">
            <div>
              <h3 className="text-base font-semibold">{expandedRow.name}</h3>
              <p className="text-sm text-muted-foreground">{t("concepts.dossier.open")}</p>
            </div>
            <Button variant="outline" size="sm" onClick={() => setExpandedId(null)}>
              {t("common.close")}
            </Button>
          </div>
          <ConceptDossierPanel
            projectId={project.operationalProjectId}
            optionId={expandedRow.id}
            reviews={reviews.filter((review) => review.conceptOptionId === expandedId)}
            canManage={canManage}
            locked={
              isLocked || expandedRow.status === "Finalized" || expandedRow.status === "Discarded"
            }
            onChanged={refreshRows}
          />
        </div>
      ) : null}

      {/* create / edit dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{isEdit ? t("concepts.edit") : t("concepts.new")}</DialogTitle>
            <DialogDescription>{t("concepts.form.hint")}</DialogDescription>
          </DialogHeader>
          <div className="grid gap-3">
            <div>
              <Label htmlFor="concept-name">{t("concepts.field.name")}</Label>
              <Input
                id="concept-name"
                maxLength={200}
                autoFocus
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
              />
            </div>
            <div>
              <Label htmlFor="concept-description">{t("concepts.field.description")}</Label>
              <Textarea
                id="concept-description"
                maxLength={4000}
                rows={3}
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              />
            </div>
            <div>
              <Label htmlFor="concept-note">{t("concepts.field.internalNote")}</Label>
              <Textarea
                id="concept-note"
                maxLength={4000}
                rows={2}
                value={form.internalNote}
                onChange={(e) => setForm((f) => ({ ...f, internalNote: e.target.value }))}
              />
            </div>
            {canPickOwner ? (
              <div>
                <Label>{t("concepts.field.owner")}</Label>
                <SearchableSelect
                  ariaLabel={t("concepts.field.owner")}
                  className="mt-1"
                  value={form.ownerUserId != null ? String(form.ownerUserId) : ""}
                  onChange={(v) => setForm((f) => ({ ...f, ownerUserId: v ? Number(v) : null }))}
                  options={[{ value: "", label: t("concepts.form.ownerNone") }, ...userOptions]}
                  placeholder={t("concepts.form.ownerNone")}
                  disabled={!!ownersError || ownersLoading}
                />
                <p className="mt-1 text-xs text-muted-foreground">
                  {t(
                    ownersError
                      ? "designProjects.team.ownerPickerError"
                      : ownersLoading
                        ? "common.loading"
                        : userOptions.length
                          ? "designProjects.team.ownerPickerHint"
                          : "designProjects.team.ownerPickerEmpty",
                  )}
                </p>
              </div>
            ) : null}
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

      <DeletionImpactDialog
        open={deleting !== null}
        impact={deleteImpact}
        loading={loadingDeleteImpact}
        deleting={busyDelete}
        error={deleteError}
        onOpenChange={(open) => {
          if (!open && !busyDelete) setDeleting(null);
        }}
        onConfirm={confirmDelete}
        onCompleted={async () => {
          setDeleting(null);
          await fetchRows();
        }}
      />

      {/* finalize confirmation */}
      <AlertDialog
        open={!!discarding}
        onOpenChange={(open) => {
          if (!open && transitioning === null) setDiscarding(null);
        }}
      >
        <AlertDialogContent onCloseAutoFocus={restoreConfirmationFocus}>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("concepts.discard.confirmTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("concepts.discard.confirmBody").replace("{name}", discarding?.name ?? "")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={transitioning !== null}>
              {t("common.cancel")}
            </AlertDialogCancel>
            <AlertDialogAction
              disabled={transitioning !== null}
              onClick={(event) => {
                event.preventDefault();
                if (discarding)
                  void transition(discarding, "Discarded").then((success) => {
                    if (success) setDiscarding(null);
                  });
              }}
            >
              {t("concepts.discard.confirm")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
      <AlertDialog open={!!finalizing} onOpenChange={(o) => (!o ? setFinalizing(null) : undefined)}>
        <AlertDialogContent onCloseAutoFocus={restoreConfirmationFocus}>
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
  finalizeReady,
  canHardDelete,
  busy,
  onEdit,
  onDelete,
  onFinalize,
  onDiscard,
}: {
  row: ConceptOptionResponse;
  t: (key: string) => string;
  canFinalize: boolean;
  finalizeReady: boolean;
  canHardDelete: boolean;
  busy: boolean;
  onEdit: () => void;
  onDelete: () => void;
  onFinalize: (trigger: HTMLButtonElement) => void;
  onDiscard: (trigger: HTMLButtonElement) => void;
}) => {
  const s = row.status;
  const btn = "min-h-11 text-sm";
  return (
    <>
      {s === "Drafting" && (
        <>
          <Button size="sm" variant="outline" className={btn} onClick={onEdit}>
            <Pencil className="mr-1 h-3.5 w-3.5" />
            {t("common.edit")}
          </Button>
        </>
      )}
      {s === "PendingInternalReview" && (
        <>
          <span className="text-xs text-slate-500">{t("concepts.dossier.frozen")}</span>
        </>
      )}
      {s === "PresentedToClient" && (
        <>
          {canFinalize ? (
            <Button
              size="sm"
              className={cn(btn, "bg-emerald-600 hover:bg-emerald-700")}
              onClick={(event) => onFinalize(event.currentTarget)}
              disabled={busy || !finalizeReady}
              aria-describedby={!finalizeReady ? `concept-finalize-hint-${row.id}` : undefined}
            >
              <Sparkles className="mr-1 h-3.5 w-3.5" />
              {t("concepts.action.finalize")}
            </Button>
          ) : null}
          {canFinalize && !finalizeReady ? (
            <p
              id={`concept-finalize-hint-${row.id}`}
              className="w-full text-sm text-muted-foreground"
            >
              {t("concepts.dossier.finalizeHint")}
            </p>
          ) : null}
          <Button
            size="sm"
            variant="ghost"
            className={btn}
            disabled={busy}
            onClick={(event) => onDiscard(event.currentTarget)}
          >
            {t("concepts.action.discard")}
          </Button>
        </>
      )}
      {s === "ClientRequestedChanges" && (
        <span className="text-xs text-amber-700">{t("concepts.dossier.newRevision")}</span>
      )}
      {s === "Finalized" && (
        <Badge variant="outline" className="border-emerald-200 bg-emerald-50 text-emerald-700">
          <Check className="mr-1 h-3.5 w-3.5" />
          {t("concepts.status.Finalized")}
        </Badge>
      )}
      {canHardDelete && s === "Discarded" ? (
        <Button
          size="sm"
          variant="ghost"
          className={cn(btn, "text-rose-700 hover:bg-rose-50 hover:text-rose-800")}
          onClick={onDelete}
        >
          <Trash2 className="mr-1 h-3.5 w-3.5" />
          {t("concepts.action.delete")}
        </Button>
      ) : null}
    </>
  );
};
