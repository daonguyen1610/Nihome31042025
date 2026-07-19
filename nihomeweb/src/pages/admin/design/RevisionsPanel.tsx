import { useCallback, useEffect, useMemo, useState } from "react";
import { CheckCircle2, GitCompareArrows, History, Loader2, Plus, Sparkles } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
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
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import {
  adminApi,
  type DrawingRevisionDiffResponse,
  type DrawingRevisionResponse,
  type DrawingRevisionTargetType,
  type MasterDataOption,
} from "@/services/adminApi";

const formatDate = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString(lang);
  } catch {
    return iso;
  }
};

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  targetType: DrawingRevisionTargetType;
  targetId: number;
  /** Display code shown in the header (KT-BD-001, KT-SD-001…). */
  targetCode: string;
  /** Display title shown in the header. */
  targetTitle?: string;
}

/**
 * NIH-117 Drawing Revisions panel. Shared component reused by both the
 * Basic Design tab and the Shop Drawing tab — polymorphic on
 * <c>targetType</c>. Lists R1..RN newest-first, exposes a
 * "Tạo revision mới" form when the user has manage permission, and
 * offers a metadata-only diff between any two revisions of the same
 * target.
 */
export const RevisionsPanel = ({
  open,
  onOpenChange,
  targetType,
  targetId,
  targetCode,
  targetTitle,
}: Props) => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.designRevisionsManage);

  const [rows, setRows] = useState<DrawingRevisionResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reasons, setReasons] = useState<MasterDataOption[]>([]);

  const fetchRows = useCallback(async () => {
    if (!open) return;
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.listDrawingRevisions({
        targetType,
        targetId,
        pageSize: 200,
      });
      setRows(data.items ?? []);
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setLoading(false);
    }
  }, [open, targetType, targetId]);

  useEffect(() => {
    void fetchRows();
  }, [fetchRows]);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    (async () => {
      try {
        const { data } = await adminApi.getMasterDataOptions("drawing_revision_reason");
        if (!cancelled) setReasons((data ?? []).filter((o) => o.isActive));
      } catch {
        // non-fatal — form validation will catch it server-side
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [open]);

  const reasonOptions = useMemo(
    () => reasons.map((r) => ({ value: r.code, label: r.name })),
    [reasons],
  );

  // ---------- create form ----------
  const [createOpen, setCreateOpen] = useState(false);
  const [reasonCode, setReasonCode] = useState("");
  const [note, setNote] = useState("");
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const resetForm = () => {
    setReasonCode("");
    setNote("");
    setFormError(null);
  };

  const submitCreate = async () => {
    setFormError(null);
    if (!reasonCode) {
      setFormError(t("drawingRevision.form.reasonRequired"));
      return;
    }
    if (!note.trim()) {
      setFormError(t("drawingRevision.form.noteRequired"));
      return;
    }
    setSaving(true);
    try {
      await adminApi.createDrawingRevision({
        targetType,
        targetId,
        reasonCode,
        note: note.trim(),
      });
      toast({ title: t("drawingRevision.created") });
      setCreateOpen(false);
      resetForm();
      await fetchRows();
    } catch (err) {
      setFormError(extractApiError(err));
    } finally {
      setSaving(false);
    }
  };

  // ---------- diff ----------
  const [diffFrom, setDiffFrom] = useState<number | null>(null);
  const [diffTo, setDiffTo] = useState<number | null>(null);
  const [diff, setDiff] = useState<DrawingRevisionDiffResponse | null>(null);
  const [diffLoading, setDiffLoading] = useState(false);

  const runDiff = async () => {
    if (diffFrom == null || diffTo == null || diffFrom === diffTo) return;
    setDiffLoading(true);
    setDiff(null);
    try {
      const { data } = await adminApi.diffDrawingRevisions(diffFrom, diffTo);
      setDiff(data);
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setDiffLoading(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl" data-testid="revisions-panel">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <History className="h-4 w-4" />
            {t("drawingRevision.title")}
          </DialogTitle>
          <DialogDescription>
            <span className="font-mono">{targetCode}</span>
            {targetTitle ? <span className="ml-2 text-slate-600">— {targetTitle}</span> : null}
          </DialogDescription>
        </DialogHeader>

        {loading ? (
          <div className="flex items-center justify-center py-6">
            <Loader2 className="h-5 w-5 animate-spin text-slate-400" />
          </div>
        ) : error ? (
          <p className="rounded-md border border-rose-200 bg-rose-50/60 p-3 text-sm text-rose-700">{error}</p>
        ) : (
          <div className="space-y-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <p className="text-xs text-slate-500">
                {rows.length === 0
                  ? t("drawingRevision.empty")
                  : `${rows.length} × ${t("drawingRevision.field.number")}`}
              </p>
              {canManage ? (
                <Button
                  size="sm"
                  onClick={() => {
                    resetForm();
                    setCreateOpen(true);
                  }}
                  data-testid="revisions-new"
                >
                  <Plus className="mr-1 h-3.5 w-3.5" />
                  {t("drawingRevision.new")}
                </Button>
              ) : null}
            </div>

            {rows.length > 0 ? (
              <ul className="space-y-2">
                {rows.map((row) => (
                  <li
                    key={row.id}
                    className={cn(
                      "rounded-md border p-3",
                      row.isCurrent
                        ? "border-emerald-200 bg-emerald-50/60"
                        : "border-slate-200 bg-white",
                    )}
                    data-testid={`revision-row-${row.id}`}
                  >
                    <div className="flex flex-wrap items-start justify-between gap-2">
                      <div className="min-w-0">
                        <p className="flex items-center gap-2 text-sm font-semibold text-slate-900">
                          <span className="font-mono">{row.revisionLabel}</span>
                          {row.isCurrent ? (
                            <Badge variant="outline" className="border-emerald-300 bg-emerald-100 text-emerald-800">
                              <CheckCircle2 className="mr-1 h-3 w-3" />
                              {t("drawingRevision.badge.current")}
                            </Badge>
                          ) : (
                            <Badge variant="outline" className="border-slate-200 bg-slate-50 text-slate-500">
                              {t("drawingRevision.badge.superseded")}
                            </Badge>
                          )}
                        </p>
                        <p className="mt-0.5 text-xs text-slate-600">
                          <Sparkles className="mr-1 inline h-3 w-3 text-slate-400" />
                          {row.reasonLabel ?? row.reasonCode}
                        </p>
                        <p className="mt-1 whitespace-pre-wrap text-sm text-slate-800">{row.note}</p>
                      </div>
                      <div className="text-right text-[11px] text-slate-500">
                        <p>{formatDate(row.createdAt, lang)}</p>
                        {row.createdByName ? <p>{row.createdByName}</p> : null}
                      </div>
                    </div>
                  </li>
                ))}
              </ul>
            ) : null}

            {/* Diff picker — only useful when there are ≥2 revisions */}
            {rows.length >= 2 ? (
              <section className="rounded-md border border-slate-200 bg-slate-50/40 p-3">
                <p className="mb-2 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-slate-600">
                  <GitCompareArrows className="h-3.5 w-3.5" />
                  Diff
                </p>
                <div className="flex flex-wrap items-center gap-2">
                  <Select value={diffFrom != null ? String(diffFrom) : ""} onValueChange={(v) => setDiffFrom(v ? Number(v) : null)}>
                    <SelectTrigger className="h-9 w-[140px]" data-testid="revisions-diff-from">
                      <SelectValue placeholder="From" />
                    </SelectTrigger>
                    <SelectContent>
                      {rows.map((r) => (
                        <SelectItem key={r.id} value={String(r.id)}>{r.revisionLabel}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <span className="text-xs text-slate-400">→</span>
                  <Select value={diffTo != null ? String(diffTo) : ""} onValueChange={(v) => setDiffTo(v ? Number(v) : null)}>
                    <SelectTrigger className="h-9 w-[140px]" data-testid="revisions-diff-to">
                      <SelectValue placeholder="To" />
                    </SelectTrigger>
                    <SelectContent>
                      {rows.map((r) => (
                        <SelectItem key={r.id} value={String(r.id)}>{r.revisionLabel}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => void runDiff()}
                    disabled={diffFrom == null || diffTo == null || diffFrom === diffTo || diffLoading}
                    data-testid="revisions-diff-run"
                  >
                    {diffLoading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : "Diff"}
                  </Button>
                </div>
                {diff ? (
                  <ul className="mt-2 list-disc space-y-0.5 pl-4 text-xs text-slate-700" data-testid="revisions-diff-output">
                    {diff.changes.map((line, i) => (
                      <li key={i}>{line}</li>
                    ))}
                  </ul>
                ) : null}
              </section>
            ) : null}
          </div>
        )}

        {/* create-revision dialog nested inside the panel */}
        <Dialog open={createOpen} onOpenChange={setCreateOpen}>
          <DialogContent className="max-w-lg">
            <DialogHeader>
              <DialogTitle>{t("drawingRevision.new")}</DialogTitle>
              <DialogDescription>{t("drawingRevision.form.hint")}</DialogDescription>
            </DialogHeader>
            <div className="grid gap-3">
              <div>
                <Label>{t("drawingRevision.field.reason")}</Label>
                <Select value={reasonCode} onValueChange={setReasonCode}>
                  <SelectTrigger className="mt-1 h-9" data-testid="revisions-form-reason">
                    <SelectValue placeholder={t("drawingRevision.form.reasonPlaceholder")} />
                  </SelectTrigger>
                  <SelectContent>
                    {reasonOptions.map((o) => (
                      <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label>{t("drawingRevision.field.note")}</Label>
                <Textarea
                  rows={4}
                  value={note}
                  onChange={(e) => setNote(e.target.value)}
                  placeholder={t("drawingRevision.form.notePlaceholder")}
                  data-testid="revisions-form-note"
                />
              </div>
            </div>
            {formError ? <p className="text-sm text-rose-600" data-testid="revisions-form-error">{formError}</p> : null}
            <DialogFooter>
              <Button variant="outline" onClick={() => setCreateOpen(false)} disabled={saving}>
                {t("common.cancel")}
              </Button>
              <Button onClick={() => void submitCreate()} disabled={saving} data-testid="revisions-form-save">
                {t("common.save")}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            {t("common.close")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};
