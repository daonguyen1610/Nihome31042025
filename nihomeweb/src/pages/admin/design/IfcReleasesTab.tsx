import { useCallback, useEffect, useMemo, useState } from "react";
import {
  CheckCircle2,
  Loader2,
  Plus,
  Rocket,
  ShieldCheck,
  Trash2,
  XCircle,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
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
  type DesignProjectResponse,
  type IfcReleaseResponse,
  type IfcReleaseStatus,
  type MasterDataOption,
  type ShopDrawingResponse,
} from "@/services/adminApi";

const STATUS_BADGE: Record<IfcReleaseStatus, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  Released: "border-emerald-200 bg-emerald-50 text-emerald-800",
  Cancelled: "border-rose-200 bg-rose-50 text-rose-700",
};

const formatDate = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString(lang);
  } catch {
    return iso;
  }
};

interface Props {
  project: DesignProjectResponse;
}

/**
 * NIH-118 IFC Release tab. Full aggregate management for the phiếu
 * phát hành IFC: list header rows for the project, open a detail
 * sub-panel to add/remove bundled shop drawings + recipients, and
 * fire the atomic release action that flips every bundled drawing to
 * <c>Released</c>.
 */
export const IfcReleasesTab = ({ project }: Props) => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.designIfcManage);
  const canRelease = has(ADMIN_PERMS.designIfcRelease);

  const isShopStage = project.currentStage === "ShopDrawing" || project.currentStage === "Completed";

  const [rows, setRows] = useState<IfcReleaseResponse[]>([]);
  const [statusCounts, setStatusCounts] = useState<Partial<Record<IfcReleaseStatus, number>>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [recipientTypes, setRecipientTypes] = useState<MasterDataOption[]>([]);
  const [approvedDrawings, setApprovedDrawings] = useState<ShopDrawingResponse[]>([]);

  const fetchRows = useCallback(async () => {
    setError(null);
    try {
      const { data } = await adminApi.listIfcReleases({
        designProjectId: project.id,
        pageSize: 200,
      });
      setRows(data.items ?? []);
      setStatusCounts(data.statusCounts ?? {});
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
        const { data } = await adminApi.getMasterDataOptions("ifc_recipient_type");
        if (!cancelled) setRecipientTypes((data ?? []).filter((o) => o.isActive));
      } catch {
        // non-fatal — form validation will catch it server-side
      }
      // Prefetch approved/pending shop drawings for the "add drawings" picker.
      if (isShopStage) {
        try {
          const { data } = await adminApi.listShopDrawings({
            designProjectId: project.id,
            status: "Approved,PendingIfc",
            pageSize: 200,
          });
          if (!cancelled) setApprovedDrawings(data.items ?? []);
        } catch {
          /* non-fatal */
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [project.id, isShopStage]);

  // -------- create dialog --------
  const [createOpen, setCreateOpen] = useState(false);
  const [formTitle, setFormTitle] = useState("");
  const [formNote, setFormNote] = useState("");
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const openCreate = () => {
    setFormTitle("");
    setFormNote("");
    setFormError(null);
    setCreateOpen(true);
  };
  const submitCreate = async () => {
    if (!formTitle.trim()) {
      setFormError(t("ifcRelease.form.titleRequired"));
      return;
    }
    setSaving(true);
    try {
      const { data } = await adminApi.createIfcRelease({
        designProjectId: project.id,
        title: formTitle.trim(),
        note: formNote.trim() || null,
      });
      toast({ title: t("ifcRelease.created") });
      setCreateOpen(false);
      await fetchRows();
      setSelected(data);
    } catch (err) {
      setFormError(extractApiError(err));
    } finally {
      setSaving(false);
    }
  };

  // -------- selected release (detail panel) --------
  const [selected, setSelected] = useState<IfcReleaseResponse | null>(null);
  const refreshSelected = useCallback(async () => {
    if (!selected) return;
    try {
      const { data } = await adminApi.getIfcRelease(selected.id);
      setSelected(data);
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    }
  }, [selected, t, toast]);

  const isDraft = selected?.status === "Draft";
  const eligibleDrawings = useMemo(() => {
    if (!selected) return [] as ShopDrawingResponse[];
    const already = new Set(selected.items.map((i) => i.shopDrawingId));
    return approvedDrawings.filter((d) => !already.has(d.id));
  }, [approvedDrawings, selected]);

  // -------- add items --------
  const [pickerDrawingIds, setPickerDrawingIds] = useState<Set<number>>(new Set());
  const addItems = async () => {
    if (!selected || pickerDrawingIds.size === 0) return;
    try {
      const { data } = await adminApi.addIfcReleaseItems(selected.id, {
        shopDrawingIds: Array.from(pickerDrawingIds),
      });
      setSelected(data);
      setPickerDrawingIds(new Set());
      await fetchRows();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    }
  };

  const removeItem = async (itemId: number) => {
    if (!selected) return;
    try {
      const { data } = await adminApi.removeIfcReleaseItem(selected.id, itemId);
      setSelected(data);
      await fetchRows();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    }
  };

  // -------- add recipient --------
  const [recipientName, setRecipientName] = useState("");
  const [recipientType, setRecipientType] = useState("");
  const addRecipient = async () => {
    if (!selected || !recipientName.trim() || !recipientType) return;
    try {
      const { data } = await adminApi.addIfcReleaseRecipient(selected.id, {
        name: recipientName.trim(),
        recipientTypeCode: recipientType,
      });
      setSelected(data);
      setRecipientName("");
      setRecipientType("");
      await fetchRows();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    }
  };
  const removeRecipient = async (recipientId: number) => {
    if (!selected) return;
    try {
      const { data } = await adminApi.removeIfcReleaseRecipient(selected.id, recipientId);
      setSelected(data);
      await fetchRows();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    }
  };
  const acknowledge = async (recipientId: number) => {
    if (!selected) return;
    try {
      const { data } = await adminApi.acknowledgeIfcReleaseRecipient(selected.id, recipientId, {});
      setSelected(data);
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    }
  };

  // -------- release / cancel --------
  const [releaseConfirmOpen, setReleaseConfirmOpen] = useState(false);
  const [cancelConfirmOpen, setCancelConfirmOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  const doRelease = async () => {
    if (!selected) return;
    setBusy(true);
    try {
      const { data } = await adminApi.releaseIfcRelease(selected.id);
      setSelected(data);
      toast({ title: t("ifcRelease.released") });
      setReleaseConfirmOpen(false);
      await fetchRows();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setBusy(false);
    }
  };

  const doCancel = async () => {
    if (!selected) return;
    setBusy(true);
    try {
      const { data } = await adminApi.cancelIfcRelease(selected.id);
      setSelected(data);
      toast({ title: t("ifcRelease.cancelled") });
      setCancelConfirmOpen(false);
      await fetchRows();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setBusy(false);
    }
  };

  const doDelete = async () => {
    if (!selected) return;
    setBusy(true);
    try {
      await adminApi.deleteIfcRelease(selected.id);
      toast({ title: t("ifcRelease.deleted") });
      setSelected(null);
      await fetchRows();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setBusy(false);
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
    return <div className="rounded-lg border border-rose-200 bg-rose-50/60 p-4 text-sm text-rose-700">{error}</div>;
  }

  return (
    <div className="space-y-4" data-testid="ifc-releases-tab">
      {!isShopStage ? (
        <p
          className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800"
          data-testid="ifc-locked-before"
        >
          {t("ifcRelease.lockedBefore")}
        </p>
      ) : null}

      {/* Status pills */}
      <section className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        <StatPill label={t("ifcRelease.status.Draft")} value={statusCounts.Draft ?? 0} tone="slate" icon={<ShieldCheck className="h-3.5 w-3.5" />} />
        <StatPill label={t("ifcRelease.status.Released")} value={statusCounts.Released ?? 0} tone="emerald" />
        <StatPill label={t("ifcRelease.status.Cancelled")} value={statusCounts.Cancelled ?? 0} tone="rose" />
        <StatPill label={"Total"} value={rows.length} tone="slate" />
      </section>

      <header className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-base font-semibold text-slate-900">{t("ifcRelease.title")}</h2>
        {canManage && isShopStage ? (
          <Button size="sm" onClick={openCreate} data-testid="ifc-new">
            <Plus className="mr-1 h-4 w-4" />
            {t("ifcRelease.new")}
          </Button>
        ) : null}
      </header>

      {rows.length === 0 ? (
        <div className="rounded-md border border-dashed p-10 text-center text-sm text-muted-foreground" data-testid="ifc-empty">
          {t("ifcRelease.empty")}
        </div>
      ) : (
        <ul className="space-y-2">
          {rows.map((row) => (
            <li key={row.id}>
              <button
                type="button"
                className={cn(
                  "w-full rounded-md border p-3 text-left transition-colors",
                  selected?.id === row.id ? "border-slate-400 bg-slate-50/60" : "border-slate-200 bg-white hover:bg-slate-50/50",
                )}
                onClick={() => setSelected(row)}
                data-testid={`ifc-row-${row.id}`}
              >
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="font-mono text-xs text-slate-500">{row.releaseNumber}</p>
                    <p className="mt-0.5 break-words text-sm font-medium text-slate-900">{row.title}</p>
                    <p className="mt-0.5 text-xs text-slate-500">
                      {row.items.length} × {t("ifcRelease.field.items")} · {row.recipients.length} × {t("ifcRelease.field.recipients")}
                    </p>
                  </div>
                  <div className="flex flex-col items-end gap-1">
                    <Badge variant="outline" className={cn("whitespace-nowrap", STATUS_BADGE[row.status])}>
                      {t(`ifcRelease.status.${row.status}`)}
                    </Badge>
                    <span className="text-[10px] text-muted-foreground">
                      {row.releaseDate ? formatDate(row.releaseDate, lang) : formatDate(row.createdAt, lang)}
                    </span>
                  </div>
                </div>
              </button>
            </li>
          ))}
        </ul>
      )}

      {/* Detail dialog for the selected release */}
      <Dialog open={!!selected} onOpenChange={(v) => (!v ? setSelected(null) : undefined)}>
        <DialogContent className="max-w-3xl" data-testid="ifc-detail">
          {selected ? (
            <>
              <DialogHeader>
                <DialogTitle className="flex items-center gap-2">
                  <span className="font-mono">{selected.releaseNumber}</span>
                  <Badge variant="outline" className={cn(STATUS_BADGE[selected.status])}>
                    {t(`ifcRelease.status.${selected.status}`)}
                  </Badge>
                </DialogTitle>
                <DialogDescription>{selected.title}</DialogDescription>
              </DialogHeader>

              <div className="space-y-4">
                {selected.releaseDate ? (
                  <p className="text-xs text-slate-500">
                    {t("ifcRelease.field.releaseDate")}: {formatDate(selected.releaseDate, lang)}
                    {selected.issuedByName ? ` · ${selected.issuedByName}` : null}
                  </p>
                ) : null}
                {selected.note ? (
                  <p className="whitespace-pre-wrap rounded-md border border-slate-200 bg-slate-50/40 p-2 text-xs text-slate-700">{selected.note}</p>
                ) : null}

                {/* Items */}
                <section>
                  <p className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-slate-600">
                    {t("ifcRelease.field.items")}
                  </p>
                  {selected.items.length === 0 ? (
                    <p className="rounded-md border border-dashed p-3 text-center text-xs text-muted-foreground">—</p>
                  ) : (
                    <ul className="divide-y divide-slate-100 rounded-md border border-slate-200">
                      {selected.items.map((item) => (
                        <li key={item.id} className="flex items-center justify-between gap-2 p-2" data-testid={`ifc-item-${item.id}`}>
                          <div className="min-w-0">
                            <p className="font-mono text-xs text-slate-500">{item.drawingCode}</p>
                            <p className="text-sm text-slate-900">{item.title}</p>
                            <p className="text-[10px] text-muted-foreground">{item.disciplineLabel ?? item.disciplineCode} · {item.status}</p>
                          </div>
                          {canManage && isDraft ? (
                            <Button
                              variant="ghost"
                              size="sm"
                              className="h-7 text-rose-700 hover:bg-rose-50"
                              onClick={() => void removeItem(item.id)}
                              data-testid={`ifc-item-remove-${item.id}`}
                            >
                              <Trash2 className="h-3.5 w-3.5" />
                            </Button>
                          ) : null}
                        </li>
                      ))}
                    </ul>
                  )}
                  {canManage && isDraft && eligibleDrawings.length > 0 ? (
                    <details className="mt-2 rounded-md border border-slate-200 bg-slate-50/40 p-2 text-xs">
                      <summary className="cursor-pointer font-medium text-slate-700">
                        {t("ifcRelease.action.addDrawing")} ({eligibleDrawings.length})
                      </summary>
                      <div className="mt-2 max-h-40 space-y-1 overflow-auto">
                        {eligibleDrawings.map((d) => (
                          <label key={d.id} className="flex items-center gap-2 text-slate-700">
                            <Checkbox
                              checked={pickerDrawingIds.has(d.id)}
                              onCheckedChange={(v) => {
                                setPickerDrawingIds((prev) => {
                                  const next = new Set(prev);
                                  if (v === true) next.add(d.id);
                                  else next.delete(d.id);
                                  return next;
                                });
                              }}
                              data-testid={`ifc-picker-${d.id}`}
                            />
                            <span className="font-mono text-[10px] text-slate-500">{d.drawingCode}</span>
                            <span className="min-w-0 flex-1 truncate">{d.title}</span>
                            <Badge variant="outline" className="border-slate-200 bg-white text-[10px] text-slate-500">
                              {d.status}
                            </Badge>
                          </label>
                        ))}
                      </div>
                      <div className="mt-2 flex justify-end">
                        <Button size="sm" onClick={() => void addItems()} disabled={pickerDrawingIds.size === 0} data-testid="ifc-picker-add">
                          {t("ifcRelease.action.addDrawing")}
                        </Button>
                      </div>
                    </details>
                  ) : null}
                </section>

                {/* Recipients */}
                <section>
                  <p className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-slate-600">
                    {t("ifcRelease.field.recipients")}
                  </p>
                  {selected.recipients.length === 0 ? (
                    <p className="rounded-md border border-dashed p-3 text-center text-xs text-muted-foreground">—</p>
                  ) : (
                    <ul className="divide-y divide-slate-100 rounded-md border border-slate-200">
                      {selected.recipients.map((r) => (
                        <li key={r.id} className="flex items-center justify-between gap-2 p-2" data-testid={`ifc-recipient-${r.id}`}>
                          <div className="min-w-0">
                            <p className="text-sm text-slate-900">{r.name}</p>
                            <p className="text-[10px] text-muted-foreground">
                              {r.recipientTypeLabel ?? r.recipientTypeCode}
                              {r.acknowledgedAt ? ` · ${t("ifcRelease.action.acknowledged")} ${formatDate(r.acknowledgedAt, lang)}` : ""}
                            </p>
                          </div>
                          <div className="flex items-center gap-1">
                            {selected.status === "Released" && !r.isAcknowledged && canManage ? (
                              <Button
                                variant="outline"
                                size="sm"
                                className="h-7 text-xs"
                                onClick={() => void acknowledge(r.id)}
                                data-testid={`ifc-recipient-ack-${r.id}`}
                              >
                                <CheckCircle2 className="mr-1 h-3.5 w-3.5" />
                                {t("ifcRelease.action.acknowledge")}
                              </Button>
                            ) : null}
                            {canManage && isDraft ? (
                              <Button
                                variant="ghost"
                                size="sm"
                                className="h-7 text-rose-700 hover:bg-rose-50"
                                onClick={() => void removeRecipient(r.id)}
                              >
                                <Trash2 className="h-3.5 w-3.5" />
                              </Button>
                            ) : null}
                          </div>
                        </li>
                      ))}
                    </ul>
                  )}
                  {canManage && isDraft ? (
                    <div className="mt-2 flex flex-wrap gap-2">
                      <Input
                        value={recipientName}
                        onChange={(e) => setRecipientName(e.target.value)}
                        placeholder={t("ifcRelease.recipient.namePlaceholder")}
                        className="h-9 flex-1 min-w-[180px]"
                        data-testid="ifc-recipient-name"
                      />
                      <Select value={recipientType} onValueChange={setRecipientType}>
                        <SelectTrigger className="h-9 w-[180px]" data-testid="ifc-recipient-type">
                          <SelectValue placeholder={t("ifcRelease.recipient.typePlaceholder")} />
                        </SelectTrigger>
                        <SelectContent>
                          {recipientTypes.map((o) => (
                            <SelectItem key={o.code} value={o.code}>{o.name}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <Button
                        size="sm"
                        onClick={() => void addRecipient()}
                        disabled={!recipientName.trim() || !recipientType}
                        data-testid="ifc-recipient-add"
                      >
                        <Plus className="mr-1 h-3.5 w-3.5" />
                        {t("ifcRelease.action.addRecipient")}
                      </Button>
                    </div>
                  ) : null}
                </section>
              </div>

              <DialogFooter className="flex-wrap gap-2">
                {isDraft && canManage ? (
                  <Button variant="ghost" onClick={() => void doDelete()} disabled={busy} className="text-rose-700 hover:bg-rose-50">
                    <Trash2 className="mr-1 h-4 w-4" />
                    {t("ifcRelease.action.deleteRelease")}
                  </Button>
                ) : null}
                {isDraft && canManage ? (
                  <Button variant="outline" onClick={() => setCancelConfirmOpen(true)} disabled={busy}>
                    <XCircle className="mr-1 h-4 w-4" />
                    {t("ifcRelease.action.cancel")}
                  </Button>
                ) : null}
                {isDraft && canRelease ? (
                  <Button
                    className="bg-emerald-600 hover:bg-emerald-700"
                    onClick={() => setReleaseConfirmOpen(true)}
                    disabled={busy || selected.items.length === 0 || selected.recipients.length === 0}
                    data-testid="ifc-release"
                  >
                    <Rocket className="mr-1 h-4 w-4" />
                    {t("ifcRelease.action.release")}
                  </Button>
                ) : null}
                <Button variant="outline" onClick={() => setSelected(null)}>
                  {t("common.close")}
                </Button>
              </DialogFooter>
            </>
          ) : null}
        </DialogContent>
      </Dialog>

      {/* create dialog */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{t("ifcRelease.new")}</DialogTitle>
            <DialogDescription>{t("ifcRelease.form.hint")}</DialogDescription>
          </DialogHeader>
          <div className="grid gap-3">
            <div>
              <Label>{t("ifcRelease.field.title")}</Label>
              <Input
                autoFocus
                value={formTitle}
                onChange={(e) => setFormTitle(e.target.value)}
                data-testid="ifc-form-title"
              />
            </div>
            <div>
              <Label>{t("ifcRelease.field.note")}</Label>
              <Textarea
                rows={3}
                value={formNote}
                onChange={(e) => setFormNote(e.target.value)}
              />
            </div>
          </div>
          {formError ? <p className="text-sm text-rose-600">{formError}</p> : null}
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)} disabled={saving}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submitCreate()} disabled={saving} data-testid="ifc-form-save">
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* release confirmation */}
      <AlertDialog open={releaseConfirmOpen} onOpenChange={setReleaseConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("ifcRelease.action.release")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("ifcRelease.confirmRelease").replace("{code}", selected?.releaseNumber ?? "")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busy}>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              className="bg-emerald-600 hover:bg-emerald-700"
              onClick={(e) => {
                e.preventDefault();
                void doRelease();
              }}
              disabled={busy}
              data-testid="ifc-release-confirm"
            >
              {t("ifcRelease.action.release")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* cancel confirmation */}
      <AlertDialog open={cancelConfirmOpen} onOpenChange={setCancelConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("ifcRelease.action.cancel")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("ifcRelease.confirmCancel").replace("{code}", selected?.releaseNumber ?? "")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busy}>{t("common.close")}</AlertDialogCancel>
            <AlertDialogAction
              onClick={(e) => {
                e.preventDefault();
                void doCancel();
              }}
              disabled={busy}
            >
              {t("ifcRelease.action.cancel")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
};

// ------------------------------ stat pill ------------------------------

const TONE_BG: Record<string, string> = {
  slate: "border-slate-200 bg-slate-50 text-slate-700",
  emerald: "border-emerald-200 bg-emerald-50 text-emerald-700",
  rose: "border-rose-200 bg-rose-50 text-rose-700",
};

const StatPill = ({
  label,
  value,
  tone,
  icon,
}: {
  label: string;
  value: number;
  tone: keyof typeof TONE_BG | string;
  icon?: React.ReactNode;
}) => (
  <div className={cn("rounded-md border px-2.5 py-1.5", TONE_BG[tone] ?? TONE_BG.slate)}>
    <p className="flex items-center gap-1 text-[11px] font-medium uppercase tracking-wide opacity-80">
      {icon}
      {label}
    </p>
    <p className="mt-0.5 text-lg font-semibold leading-none">{value}</p>
  </div>
);
