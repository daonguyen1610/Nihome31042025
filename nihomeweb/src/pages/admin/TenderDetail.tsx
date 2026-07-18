import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  AlertTriangle,
  ArrowLeft,
  Calendar,
  CheckCircle2,
  ChevronDown,
  Clock,
  Download,
  History,
  Library,
  Loader2,
  Trophy,
  Upload,
  UserRound,
  X,
  XCircle,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { PageLoading, PageError } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Progress } from "@/components/ui/progress";
import { Textarea } from "@/components/ui/textarea";
import { Checkbox } from "@/components/ui/checkbox";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
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
  adminApi,
  type CapabilityDocumentResponse,
  type MasterDataOption,
  type OpportunityResponse,
  type TenderChecklistItemResponse,
  type TenderChecklistItemStatus,
  type TenderResponse,
  type TenderStatus,
  type TenderTimelineEvent,
} from "@/services/adminApi";

// ---------------------------- shared helpers ----------------------------

const STATUS_BADGE_STYLES: Record<TenderStatus, string> = {
  Preparing: "border-slate-200 bg-slate-50 text-slate-700",
  Submitted: "border-sky-200 bg-sky-50 text-sky-700",
  Won: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Lost: "border-rose-200 bg-rose-50 text-rose-700",
  Cancelled: "border-zinc-200 bg-zinc-100 text-zinc-600",
};

const CHECKLIST_STATUS_STYLES: Record<TenderChecklistItemStatus, string> = {
  NotStarted: "border-slate-200 bg-slate-50 text-slate-700",
  Preparing: "border-amber-200 bg-amber-50 text-amber-700",
  Done: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Submitted: "border-sky-200 bg-sky-50 text-sky-700",
};

const CHECKLIST_STATUSES: TenderChecklistItemStatus[] = [
  "NotStarted",
  "Preparing",
  "Done",
  "Submitted",
];

const DEADLINE_ALERT_DAYS = 3;

const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? "";
const FILE_BASE = API_BASE.replace(/\/api\/?$/, "");

/** Resolve host-relative `/files/...` paths to an absolute URL for downloads. */
const resolveFileUrl = (path?: string | null): string | null => {
  if (!path) return null;
  if (/^https?:\/\//i.test(path)) return path;
  return `${FILE_BASE}${path.startsWith("/") ? "" : "/"}${path}`;
};

const formatDate = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString(lang);
  } catch {
    return iso;
  }
};

const formatDateTime = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleString(lang);
  } catch {
    return iso;
  }
};

const toDateInputValue = (iso?: string | null) => (iso ? iso.slice(0, 10) : "");

/** Whole days until (or since, negative) the ISO deadline. */
const daysUntil = (iso: string): number => {
  const target = new Date(iso).getTime();
  const now = Date.now();
  return Math.ceil((target - now) / (1000 * 60 * 60 * 24));
};

// ---------------------------- header ----------------------------

interface HeaderProps {
  tender: TenderResponse;
}

const HeaderCard = ({ tender }: HeaderProps) => {
  const { t } = useI18n();
  const days = daysUntil(tender.submissionDeadline);
  const isTerminal = tender.status === "Won" || tender.status === "Lost" || tender.status === "Cancelled";

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm md:p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="text-sm text-slate-500">
            <Link to="/admin/tenders" className="inline-flex items-center gap-1 hover:text-slate-800">
              <ArrowLeft className="h-4 w-4" />
              {t("tenders.detail.back")}
            </Link>
          </div>
          <div className="mt-1 flex flex-wrap items-center gap-2">
            <h1 className="truncate text-xl font-bold text-slate-900 md:text-2xl">{tender.name}</h1>
            <Badge className={cn("border", STATUS_BADGE_STYLES[tender.status])} variant="outline">
              {t(`tenders.status.${tender.status}`)}
            </Badge>
          </div>
          <p className="mt-1 text-sm text-slate-600">
            <span className="font-mono text-slate-500">{tender.code}</span>
            <span className="mx-1.5">·</span>
            <span className="font-medium">{tender.customerName}</span>
            {tender.preparerName ? (
              <span className="ml-2 text-xs text-slate-400">• {tender.preparerName}</span>
            ) : null}
          </p>
        </div>
      </div>

      <div className="mt-4 grid gap-3 sm:grid-cols-3">
        <div className={cn("rounded-lg p-3", isTerminal ? "bg-slate-50" : days < 0 ? "bg-rose-50" : days <= DEADLINE_ALERT_DAYS ? "bg-amber-50" : "bg-slate-50")}>
          <div className="text-xs text-slate-500">{t("tenders.field.deadline")}</div>
          <div className="mt-0.5 text-sm font-semibold text-slate-800">
            {formatDate(tender.submissionDeadline)}
          </div>
          {!isTerminal ? (
            <div
              className={cn(
                "mt-1 inline-flex items-center gap-1 text-xs font-medium",
                days < 0 ? "text-rose-700" : days <= DEADLINE_ALERT_DAYS ? "text-amber-700" : "text-slate-500",
              )}
            >
              <Clock className="h-3.5 w-3.5" />
              {days < 0
                ? t("tenders.detail.header.overdue").replace("{days}", String(-days))
                : t("tenders.detail.header.countdown").replace("{days}", String(days))}
            </div>
          ) : null}
        </div>
        <div className="rounded-lg bg-slate-50 p-3">
          <div className="text-xs text-slate-500">{t("tenders.detail.header.checklistProgress")}</div>
          <div className="mt-1 flex items-center gap-2">
            <Progress value={tender.checklistCompletionPercent} className="h-2 flex-1" />
            <span className="text-sm font-semibold tabular-nums text-slate-700">
              {tender.checklistCompletionPercent}%
            </span>
          </div>
          <div className="mt-1 text-xs text-slate-500">
            {t("tenders.detail.checklist.percentDone").replace(
              "{percent}",
              String(tender.checklistCompletionPercent),
            )}
          </div>
        </div>
        <div className="rounded-lg bg-slate-50 p-3">
          <div className="text-xs text-slate-500">{t("tenders.field.openingDate")}</div>
          <div className="mt-0.5 text-sm font-semibold text-slate-800">
            {formatDate(tender.openingDate)}
          </div>
          {tender.infoSource ? (
            <div className="mt-1 text-xs text-slate-500 break-words">
              {t("tenders.field.infoSource")}: {tender.infoSource}
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
};

// ---------------------------- info tab ----------------------------

const InfoTab = ({ tender }: { tender: TenderResponse }) => {
  const { t, lang } = useI18n();
  const rows: [string, string][] = [
    [t("tenders.field.code"), tender.code],
    [t("tenders.field.customer"), tender.customerName],
    [t("tenders.field.preparer"), tender.preparerName ?? "—"],
    [t("tenders.field.openingDate"), formatDate(tender.openingDate, lang)],
    [t("tenders.field.deadline"), formatDate(tender.submissionDeadline, lang)],
    [t("tenders.field.infoSource"), tender.infoSource ?? "—"],
    [t("tenders.field.status"), t(`tenders.status.${tender.status}`)],
    [t("tenders.field.updatedAt"), formatDateTime(tender.updatedAt, lang)],
  ];

  return (
    <div className="space-y-4">
      <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <dl className="grid gap-3 sm:grid-cols-2">
          {rows.map(([label, value]) => (
            <div key={label}>
              <dt className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</dt>
              <dd className="mt-0.5 break-words text-sm text-slate-800">{value}</dd>
            </div>
          ))}
        </dl>
      </div>
      {tender.note ? (
        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <div className="text-xs font-medium uppercase tracking-wide text-slate-500">
            {t("tenders.field.note")}
          </div>
          <p className="mt-1 whitespace-pre-wrap break-words text-sm text-slate-800">{tender.note}</p>
        </div>
      ) : null}
    </div>
  );
};

// ---------------------------- checklist tab ----------------------------

interface ChecklistTabProps {
  tender: TenderResponse;
  canManage: boolean;
  onPatch: (itemId: number, body: Parameters<typeof adminApi.updateTenderChecklistItem>[2]) => Promise<void>;
  onUpload: (itemId: number, file: File) => Promise<void>;
  onOpenLibrary: () => void;
  isTerminal: boolean;
}

const ChecklistTab = ({ tender, canManage, onPatch, onUpload, onOpenLibrary, isTerminal }: ChecklistTabProps) => {
  const { t } = useI18n();
  const disabled = !canManage || isTerminal;
  const [savingId, setSavingId] = useState<number | null>(null);
  const [fileInputTarget, setFileInputTarget] = useState<number | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleStatus = async (item: TenderChecklistItemResponse, next: TenderChecklistItemStatus) => {
    if (item.status === next) return;
    setSavingId(item.id);
    try {
      await onPatch(item.id, { status: next });
    } finally {
      setSavingId(null);
    }
  };

  const handleDeadline = async (item: TenderChecklistItemResponse, iso: string) => {
    setSavingId(item.id);
    try {
      if (!iso) {
        await onPatch(item.id, { clearInternalDeadline: true });
      } else {
        // Parse the date-input value as UTC midnight so the value
        // round-trips through .slice(0, 10) without a timezone-driven
        // off-by-one on positive UTC offsets (e.g. Asia/Ho_Chi_Minh).
        await onPatch(item.id, { internalDeadline: `${iso}T00:00:00.000Z` });
      }
    } finally {
      setSavingId(null);
    }
  };

  const triggerUpload = (itemId: number) => {
    setFileInputTarget(itemId);
    setTimeout(() => fileInputRef.current?.click(), 0);
  };

  const onFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    const target = fileInputTarget;
    e.target.value = "";
    if (!file || target == null) return;
    setSavingId(target);
    try {
      await onUpload(target, file);
    } finally {
      setSavingId(null);
      setFileInputTarget(null);
    }
  };

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="text-base font-semibold text-slate-800">
            {t("tenders.detail.checklist.title")}
          </h2>
          <div className="mt-0.5 text-xs text-slate-500">
            {t("tenders.detail.checklist.percentDone").replace(
              "{percent}",
              String(tender.checklistCompletionPercent),
            )}
          </div>
        </div>
        {!disabled ? (
          <Button variant="outline" size="sm" onClick={onOpenLibrary}>
            <Library className="mr-1 h-4 w-4" />
            {t("tenders.detail.checklist.pickFromLibrary")}
          </Button>
        ) : null}
      </div>

      <input
        ref={fileInputRef}
        type="file"
        className="hidden"
        onChange={onFileChange}
        accept=".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg"
      />

      {/* Desktop table */}
      <div className="hidden overflow-x-auto rounded-lg border border-slate-200 bg-white shadow-sm md:block">
        <table className="min-w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-3 py-2">{t("tenders.detail.checklist.header.title")}</th>
              <th className="px-3 py-2">{t("tenders.detail.checklist.header.status")}</th>
              <th className="px-3 py-2">{t("tenders.detail.checklist.header.owner")}</th>
              <th className="px-3 py-2">{t("tenders.detail.checklist.header.deadline")}</th>
              <th className="px-3 py-2">{t("tenders.detail.checklist.header.file")}</th>
              <th className="px-3 py-2 text-right">{t("tenders.detail.checklist.header.action")}</th>
            </tr>
          </thead>
          <tbody>
            {tender.checklistItems.map((item) => (
              <tr key={item.id} className="border-t align-top">
                <td className="px-3 py-2 font-medium text-slate-800">{item.title}</td>
                <td className="px-3 py-2">
                  <Select
                    value={item.status}
                    disabled={disabled || savingId === item.id}
                    onValueChange={(v) => void handleStatus(item, v as TenderChecklistItemStatus)}
                  >
                    <SelectTrigger className={cn("h-8 w-40 text-xs", CHECKLIST_STATUS_STYLES[item.status])}>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {CHECKLIST_STATUSES.map((s) => (
                        <SelectItem key={s} value={s}>
                          {t(`tenders.detail.checklist.itemStatus.${s}`)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </td>
                <td className="px-3 py-2 text-xs text-slate-700">
                  {item.ownerName ?? <span className="text-slate-400">—</span>}
                </td>
                <td className="px-3 py-2">
                  <Input
                    type="date"
                    value={toDateInputValue(item.internalDeadline)}
                    className="h-8 w-40 text-xs"
                    disabled={disabled || savingId === item.id}
                    onChange={(e) => void handleDeadline(item, e.target.value)}
                  />
                </td>
                <td className="px-3 py-2">
                  {item.filePath ? (
                    <a
                      href={resolveFileUrl(item.filePath) ?? "#"}
                      target="_blank"
                      rel="noreferrer"
                      className="inline-flex items-center gap-1 text-xs text-sky-700 hover:underline"
                    >
                      <Download className="h-3.5 w-3.5" />
                      <span className="max-w-[140px] truncate">{item.originalFileName ?? "file"}</span>
                    </a>
                  ) : (
                    <span className="text-xs text-slate-400">{t("tenders.detail.checklist.noFile")}</span>
                  )}
                </td>
                <td className="px-3 py-2 text-right">
                  {!disabled ? (
                    <Button
                      variant="ghost"
                      size="sm"
                      disabled={savingId === item.id}
                      onClick={() => triggerUpload(item.id)}
                    >
                      {savingId === item.id ? (
                        <Loader2 className="h-3.5 w-3.5 animate-spin" />
                      ) : (
                        <Upload className="h-3.5 w-3.5" />
                      )}
                      <span className="ml-1 text-xs">
                        {item.filePath
                          ? t("tenders.detail.checklist.replace")
                          : t("tenders.detail.checklist.upload")}
                      </span>
                    </Button>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Mobile cards */}
      <div className="grid gap-2 md:hidden">
        {tender.checklistItems.map((item) => (
          <div key={item.id} className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
            <div className="flex items-start justify-between gap-2">
              <div className="min-w-0">
                <div className="break-words text-sm font-medium text-slate-800">{item.title}</div>
                <div className="mt-0.5 text-xs text-slate-500">{item.ownerName ?? "—"}</div>
              </div>
              <Badge
                variant="outline"
                className={cn("shrink-0 text-xs", CHECKLIST_STATUS_STYLES[item.status])}
              >
                {t(`tenders.detail.checklist.itemStatus.${item.status}`)}
              </Badge>
            </div>
            <div className="mt-2 grid grid-cols-2 gap-2">
              <Select
                value={item.status}
                disabled={disabled || savingId === item.id}
                onValueChange={(v) => void handleStatus(item, v as TenderChecklistItemStatus)}
              >
                <SelectTrigger className="h-8 text-xs">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {CHECKLIST_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {t(`tenders.detail.checklist.itemStatus.${s}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Input
                type="date"
                value={toDateInputValue(item.internalDeadline)}
                className="h-8 text-xs"
                disabled={disabled || savingId === item.id}
                onChange={(e) => void handleDeadline(item, e.target.value)}
              />
            </div>
            <div className="mt-2 flex items-center justify-between gap-2 text-xs">
              {item.filePath ? (
                <a
                  href={resolveFileUrl(item.filePath) ?? "#"}
                  target="_blank"
                  rel="noreferrer"
                  className="inline-flex min-w-0 items-center gap-1 text-sky-700 hover:underline"
                >
                  <Download className="h-3.5 w-3.5" />
                  <span className="truncate">{item.originalFileName ?? "file"}</span>
                </a>
              ) : (
                <span className="text-slate-400">{t("tenders.detail.checklist.noFile")}</span>
              )}
              {!disabled ? (
                <Button
                  variant="ghost"
                  size="sm"
                  disabled={savingId === item.id}
                  onClick={() => triggerUpload(item.id)}
                >
                  {savingId === item.id ? (
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  ) : (
                    <Upload className="h-3.5 w-3.5" />
                  )}
                  <span className="ml-1">
                    {item.filePath
                      ? t("tenders.detail.checklist.replace")
                      : t("tenders.detail.checklist.upload")}
                  </span>
                </Button>
              ) : null}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

// ---------------------------- library picker ----------------------------

interface LibraryPickerProps {
  open: boolean;
  onClose: () => void;
  tender: TenderResponse;
  onSubmit: (assignments: { checklistItemId: number; capabilityDocumentId: number }[]) => Promise<void>;
}

const LibraryPicker = ({ open, onClose, tender, onSubmit }: LibraryPickerProps) => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [docs, setDocs] = useState<CapabilityDocumentResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [targetItemId, setTargetItemId] = useState<number | null>(
    tender.checklistItems[0]?.id ?? null,
  );
  const [selectedDocIds, setSelectedDocIds] = useState<Set<number>>(new Set());
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    (async () => {
      try {
        const { data } = await adminApi.listCapabilityDocuments({ pageSize: 100 });
        if (!cancelled) setDocs(data.items ?? []);
      } catch (err) {
        if (!cancelled) setError(extractApiError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [open]);

  useEffect(() => {
    if (open) {
      setSelectedDocIds(new Set());
      setTargetItemId(tender.checklistItems[0]?.id ?? null);
    }
  }, [open, tender.checklistItems]);

  const toggleDoc = (id: number) => {
    setSelectedDocIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const confirm = async () => {
    if (targetItemId == null) return;
    if (selectedDocIds.size === 0) return;
    const assignments = Array.from(selectedDocIds).map((docId) => ({
      checklistItemId: targetItemId,
      capabilityDocumentId: docId,
    }));
    setSubmitting(true);
    try {
      await onSubmit(assignments);
      onClose();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-h-[85vh] w-[95vw] max-w-3xl overflow-y-auto sm:w-full">
        <DialogHeader>
          <DialogTitle>{t("tenders.detail.library.title")}</DialogTitle>
          <DialogDescription>{t("tenders.detail.library.subtitle")}</DialogDescription>
        </DialogHeader>

        <div className="mt-2 space-y-3">
          <div>
            <Label className="text-xs">{t("tenders.detail.library.pickTarget")}</Label>
            <Select
              value={targetItemId != null ? String(targetItemId) : ""}
              onValueChange={(v) => setTargetItemId(Number(v))}
            >
              <SelectTrigger className="mt-1 h-9">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {tender.checklistItems.map((it) => (
                  <SelectItem key={it.id} value={String(it.id)}>
                    {it.title}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-8 text-muted-foreground">
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              {t("common.loading")}
            </div>
          ) : error ? (
            <p className="text-sm text-rose-600">{error}</p>
          ) : docs.length === 0 ? (
            <p className="text-sm text-slate-500">{t("tenders.detail.library.empty")}</p>
          ) : (
            <div className="grid max-h-80 gap-2 overflow-y-auto">
              {docs.map((d) => (
                <label
                  key={d.id}
                  className={cn(
                    "flex cursor-pointer items-start gap-3 rounded-md border p-2.5 text-sm hover:bg-slate-50",
                    selectedDocIds.has(d.id) && "border-sky-200 bg-sky-50",
                  )}
                >
                  <Checkbox
                    checked={selectedDocIds.has(d.id)}
                    onCheckedChange={() => toggleDoc(d.id)}
                    className="mt-0.5"
                  />
                  <div className="min-w-0 flex-1">
                    <div className="break-words font-medium text-slate-800">{d.name}</div>
                    <div className="mt-0.5 flex flex-wrap gap-2 text-xs text-slate-500">
                      <span>{d.tagLabel ?? d.tagCode}</span>
                      {d.originalFileName ? <span>· {d.originalFileName}</span> : null}
                    </div>
                  </div>
                </label>
              ))}
            </div>
          )}
        </div>

        <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
          <Button variant="outline" onClick={onClose} disabled={submitting}>
            {t("common.cancel")}
          </Button>
          <Button
            onClick={() => void confirm()}
            disabled={submitting || targetItemId == null || selectedDocIds.size === 0}
          >
            {submitting ? (
              <Loader2 className="mr-1 h-4 w-4 animate-spin" />
            ) : (
              <Library className="mr-1 h-4 w-4" />
            )}
            {t("tenders.detail.library.confirm")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

// ---------------------------- mark won / lost dialogs ----------------------------

interface MarkWonDialogProps {
  open: boolean;
  onClose: () => void;
  tender: TenderResponse;
  onSubmit: (opportunityId: number, note: string | null) => Promise<void>;
}

const MarkWonDialog = ({ open, onClose, tender, onSubmit }: MarkWonDialogProps) => {
  const { t } = useI18n();
  const [opps, setOpps] = useState<OpportunityResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [oppId, setOppId] = useState<number | null>(null);
  const [note, setNote] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    setOppId(null);
    setNote("");
    (async () => {
      try {
        const { data } = await adminApi.listOpportunities({
          customerId: tender.customerId,
          pageSize: 100,
        });
        if (!cancelled) setOpps(data.items ?? []);
      } catch (err) {
        if (!cancelled) setError(extractApiError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [open, tender.customerId]);

  const confirm = async () => {
    if (oppId == null) {
      setError(t("tenders.detail.result.opportunityRequired"));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await onSubmit(oppId, note.trim() || null);
      onClose();
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t("tenders.detail.result.markWonTitle")}</DialogTitle>
          <DialogDescription>{t("tenders.detail.result.pickOpportunity")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <div>
            <Label className="text-xs">{t("tenders.detail.result.pickOpportunity")}</Label>
            {loading ? (
              <div className="mt-1 flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                {t("common.loading")}
              </div>
            ) : opps.length === 0 ? (
              <p className="mt-1 text-xs text-rose-600">{t("tenders.detail.result.noOpportunity")}</p>
            ) : (
              <Select
                value={oppId != null ? String(oppId) : ""}
                onValueChange={(v) => setOppId(Number(v))}
              >
                <SelectTrigger className="mt-1 h-9">
                  <SelectValue placeholder={t("tenders.detail.result.opportunityPlaceholder")} />
                </SelectTrigger>
                <SelectContent>
                  {opps.map((o) => (
                    <SelectItem key={o.id} value={String(o.id)}>
                      {o.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </div>
          <div>
            <Label className="text-xs">{t("tenders.detail.result.note")}</Label>
            <Textarea
              className="mt-1"
              rows={3}
              value={note}
              onChange={(e) => setNote(e.target.value)}
              placeholder={t("tenders.detail.result.notePlaceholder")}
              disabled={submitting}
            />
          </div>
          {error ? <p className="text-sm text-rose-600">{error}</p> : null}
        </div>
        <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
          <Button variant="outline" onClick={onClose} disabled={submitting}>
            {t("common.cancel")}
          </Button>
          <Button onClick={() => void confirm()} disabled={submitting || opps.length === 0}>
            {submitting ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <Trophy className="mr-1 h-4 w-4" />}
            {t("tenders.detail.result.markWon")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

interface MarkLostDialogProps {
  open: boolean;
  onClose: () => void;
  onSubmit: (reasonCode: string, note: string | null) => Promise<void>;
}

const MarkLostDialog = ({ open, onClose, onSubmit }: MarkLostDialogProps) => {
  const { t } = useI18n();
  const [reasons, setReasons] = useState<MasterDataOption[]>([]);
  const [loading, setLoading] = useState(false);
  const [reasonCode, setReasonCode] = useState<string>("");
  const [note, setNote] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    setReasonCode("");
    setNote("");
    (async () => {
      try {
        const { data } = await adminApi.getMasterDataOptions("opportunity_lost_reason");
        if (!cancelled) setReasons((data ?? []).filter((r) => r.isActive));
      } catch (err) {
        if (!cancelled) setError(extractApiError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [open]);

  const confirm = async () => {
    if (!reasonCode) {
      setError(t("tenders.detail.result.reasonRequired"));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await onSubmit(reasonCode, note.trim() || null);
      onClose();
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t("tenders.detail.result.markLostTitle")}</DialogTitle>
          <DialogDescription>{t("tenders.detail.result.reason")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <div>
            <Label className="text-xs">{t("tenders.detail.result.reason")}</Label>
            {loading ? (
              <div className="mt-1 flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                {t("common.loading")}
              </div>
            ) : (
              <Select value={reasonCode} onValueChange={setReasonCode}>
                <SelectTrigger className="mt-1 h-9">
                  <SelectValue placeholder={t("tenders.detail.result.reasonPlaceholder")} />
                </SelectTrigger>
                <SelectContent>
                  {reasons.map((r) => (
                    <SelectItem key={r.code} value={r.code}>
                      {r.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </div>
          <div>
            <Label className="text-xs">{t("tenders.detail.result.note")}</Label>
            <Textarea
              className="mt-1"
              rows={3}
              value={note}
              onChange={(e) => setNote(e.target.value)}
              placeholder={t("tenders.detail.result.notePlaceholder")}
              disabled={submitting}
            />
          </div>
          {error ? <p className="text-sm text-rose-600">{error}</p> : null}
        </div>
        <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
          <Button variant="outline" onClick={onClose} disabled={submitting}>
            {t("common.cancel")}
          </Button>
          <Button
            variant="destructive"
            onClick={() => void confirm()}
            disabled={submitting}
          >
            {submitting ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <XCircle className="mr-1 h-4 w-4" />}
            {t("tenders.detail.result.markLost")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

// ---------------------------- result & history tab ----------------------------

interface ResultTabProps {
  tender: TenderResponse;
  timeline: TenderTimelineEvent[] | null;
  timelineLoading: boolean;
  canMarkResult: boolean;
  isTerminal: boolean;
  onMarkWonClick: () => void;
  onMarkLostClick: () => void;
}

const ResultTab = ({
  tender,
  timeline,
  timelineLoading,
  canMarkResult,
  isTerminal,
  onMarkWonClick,
  onMarkLostClick,
}: ResultTabProps) => {
  const { t, lang } = useI18n();
  const [historyOpen, setHistoryOpen] = useState(false);

  return (
    <div className="space-y-4">
      {/* Result card */}
      <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <h2 className="text-base font-semibold text-slate-800">{t("tenders.detail.tab.result")}</h2>

        {tender.status === "Won" ? (
          <div className="mt-3 rounded-md border border-emerald-200 bg-emerald-50 p-3">
            <div className="inline-flex items-center gap-2 text-sm font-semibold text-emerald-800">
              <Trophy className="h-4 w-4" />
              {t("tenders.detail.result.wonBadge")}
            </div>
            <dl className="mt-2 grid gap-2 text-sm sm:grid-cols-2">
              <div>
                <dt className="text-xs text-emerald-700">{t("tenders.detail.result.opportunity")}</dt>
                <dd className="break-words font-medium text-emerald-900">
                  {tender.wonOpportunityName ?? (tender.wonOpportunityId != null ? `#${tender.wonOpportunityId}` : "—")}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-emerald-700">{t("tenders.detail.result.closedAt")}</dt>
                <dd className="font-medium text-emerald-900">{formatDateTime(tender.closedAt, lang)}</dd>
              </div>
            </dl>
            {tender.note ? (
              <div className="mt-2">
                <div className="text-xs text-emerald-700">{t("tenders.detail.result.note")}</div>
                <p className="text-sm text-emerald-900">{tender.note}</p>
              </div>
            ) : null}
          </div>
        ) : tender.status === "Lost" ? (
          <div className="mt-3 rounded-md border border-rose-200 bg-rose-50 p-3">
            <div className="inline-flex items-center gap-2 text-sm font-semibold text-rose-800">
              <XCircle className="h-4 w-4" />
              {t("tenders.detail.result.lostBadge")}
            </div>
            <dl className="mt-2 grid gap-2 text-sm sm:grid-cols-2">
              <div>
                <dt className="text-xs text-rose-700">{t("tenders.detail.result.reason")}</dt>
                <dd className="font-medium text-rose-900">
                  {tender.lostReasonLabel ?? tender.lostReasonCode ?? "—"}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-rose-700">{t("tenders.detail.result.closedAt")}</dt>
                <dd className="font-medium text-rose-900">{formatDateTime(tender.closedAt, lang)}</dd>
              </div>
            </dl>
            {tender.lostNote ? (
              <div className="mt-2">
                <div className="text-xs text-rose-700">{t("tenders.detail.result.note")}</div>
                <p className="text-sm text-rose-900">{tender.lostNote}</p>
              </div>
            ) : null}
          </div>
        ) : tender.status === "Cancelled" ? (
          <div className="mt-3 rounded-md border border-zinc-200 bg-zinc-50 p-3">
            <div className="inline-flex items-center gap-2 text-sm font-semibold text-zinc-800">
              <X className="h-4 w-4" />
              {t("tenders.detail.result.cancelledBadge")}
            </div>
          </div>
        ) : (
          <p className="mt-2 text-sm text-slate-500">{t("tenders.detail.result.summaryOpen")}</p>
        )}

        {!isTerminal && canMarkResult ? (
          <div className="mt-4 flex flex-wrap gap-2">
            <Button onClick={onMarkWonClick} size="sm">
              <Trophy className="mr-1 h-4 w-4" />
              {t("tenders.detail.result.markWon")}
            </Button>
            <Button
              variant="outline"
              className="border-rose-200 text-rose-700 hover:bg-rose-50"
              size="sm"
              onClick={onMarkLostClick}
            >
              <XCircle className="mr-1 h-4 w-4" />
              {t("tenders.detail.result.markLost")}
            </Button>
          </div>
        ) : !isTerminal ? (
          <p className="mt-3 text-xs text-slate-500">{t("tenders.detail.markResultForbidden")}</p>
        ) : null}
      </div>

      {/* History (collapsed) */}
      <div className="rounded-lg border border-slate-200 bg-white shadow-sm">
        <button
          type="button"
          className="flex w-full items-center justify-between px-4 py-3 text-left"
          onClick={() => setHistoryOpen((v) => !v)}
        >
          <span className="inline-flex items-center gap-2 text-sm font-semibold text-slate-800">
            <History className="h-4 w-4" />
            {t("tenders.detail.history.title")}
          </span>
          <ChevronDown className={cn("h-4 w-4 transition-transform", historyOpen && "rotate-180")} />
        </button>
        {historyOpen ? (
          <div className="border-t border-slate-100 p-4">
            {timelineLoading ? (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                {t("common.loading")}
              </div>
            ) : !timeline || timeline.length === 0 ? (
              <p className="text-sm text-slate-500">{t("tenders.detail.history.empty")}</p>
            ) : (
              <ol className="space-y-2">
                {timeline.map((ev) => (
                  <li key={ev.id} className="flex items-start gap-2 text-sm">
                    <div className="mt-1 h-2 w-2 rounded-full bg-slate-300" />
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2 text-xs text-slate-500">
                        <Calendar className="h-3 w-3" />
                        <span>{formatDateTime(ev.occurredAt, lang)}</span>
                        {ev.userName ? (
                          <span className="inline-flex items-center gap-1">
                            <UserRound className="h-3 w-3" />
                            {ev.userName}
                          </span>
                        ) : null}
                        <span className="rounded bg-slate-100 px-1.5 py-0.5 font-mono">{ev.action}</span>
                      </div>
                      {ev.message ? (
                        <div className="mt-0.5 break-words text-sm text-slate-700">{ev.message}</div>
                      ) : null}
                    </div>
                  </li>
                ))}
              </ol>
            )}
          </div>
        ) : null}
      </div>
    </div>
  );
};

// ---------------------------- page shell ----------------------------

const AdminTenderDetail = () => {
  const { id } = useParams<{ id: string }>();
  const tenderId = Number(id);
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.tendersManage);
  const canMarkResult = has(ADMIN_PERMS.tendersMarkResult);
  const navigate = useNavigate();

  const [tender, setTender] = useState<TenderResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [timeline, setTimeline] = useState<TenderTimelineEvent[] | null>(null);
  const [timelineLoading, setTimelineLoading] = useState(false);

  const [libraryOpen, setLibraryOpen] = useState(false);
  const [markWonOpen, setMarkWonOpen] = useState(false);
  const [markLostOpen, setMarkLostOpen] = useState(false);

  const fetchTender = useCallback(async () => {
    if (!Number.isFinite(tenderId)) return;
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.getTender(tenderId);
      setTender(data);
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setLoading(false);
    }
  }, [tenderId]);

  const fetchTimeline = useCallback(async () => {
    if (!Number.isFinite(tenderId)) return;
    setTimelineLoading(true);
    try {
      const { data } = await adminApi.getTenderTimeline(tenderId, 100);
      setTimeline(data);
    } catch {
      setTimeline([]);
    } finally {
      setTimelineLoading(false);
    }
  }, [tenderId]);

  useEffect(() => {
    void fetchTender();
    void fetchTimeline();
  }, [fetchTender, fetchTimeline]);

  const isTerminal = useMemo(
    () => tender?.status === "Won" || tender?.status === "Lost" || tender?.status === "Cancelled",
    [tender?.status],
  );

  const showDeadlineAlert = useMemo(() => {
    if (!tender || isTerminal) return false;
    const days = daysUntil(tender.submissionDeadline);
    return days <= DEADLINE_ALERT_DAYS && tender.checklistCompletionPercent < 100;
  }, [tender, isTerminal]);

  // ----- action handlers -----

  const handleChecklistPatch = async (
    itemId: number,
    body: Parameters<typeof adminApi.updateTenderChecklistItem>[2],
  ) => {
    try {
      const { data } = await adminApi.updateTenderChecklistItem(tenderId, itemId, body);
      setTender(data);
      toast({ title: t("tenders.detail.saved") });
      void fetchTimeline();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
      throw err;
    }
  };

  const handleUpload = async (itemId: number, file: File) => {
    try {
      const { data } = await adminApi.uploadTenderChecklistFile(tenderId, itemId, file);
      setTender(data);
      toast({ title: t("tenders.detail.saved") });
      void fetchTimeline();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err) ?? t("tenders.detail.uploadFailed"),
        variant: "destructive",
      });
      throw err;
    }
  };

  const handleAttachFromLibrary = async (
    assignments: { checklistItemId: number; capabilityDocumentId: number }[],
  ) => {
    const { data } = await adminApi.attachTenderChecklistFromLibrary(tenderId, {
      items: assignments,
    });
    setTender(data);
    toast({ title: t("tenders.detail.saved") });
    void fetchTimeline();
  };

  const handleMarkWon = async (opportunityId: number, note: string | null) => {
    const { data } = await adminApi.markTenderWon(tenderId, {
      opportunityId,
      note,
    });
    setTender(data);
    toast({ title: t("tenders.detail.result.wonBadge") });
    void fetchTimeline();
  };

  const handleMarkLost = async (reasonCode: string, note: string | null) => {
    const { data } = await adminApi.markTenderLost(tenderId, { reasonCode, note });
    setTender(data);
    toast({ title: t("tenders.detail.result.lostBadge") });
    void fetchTimeline();
  };

  // ----- render -----

  if (!Number.isFinite(tenderId)) {
    return (
      <AdminLayout>
        <div className="p-4">
          <PageError message={t("tenders.detail.notFound")} onRetry={() => navigate("/admin/tenders")} />
        </div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout>
      <div className="space-y-4 p-3 md:p-4">
        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => void fetchTender()} />
        ) : !tender ? (
          <PageError message={t("tenders.detail.notFound")} onRetry={() => void fetchTender()} />
        ) : (
          <>
            <HeaderCard tender={tender} />

            {showDeadlineAlert ? (
              <div className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                <p>
                  {t("tenders.detail.alert.deadlineNear")
                    .replace("{days}", String(Math.max(0, daysUntil(tender.submissionDeadline))))
                    .replace("{percent}", String(tender.checklistCompletionPercent))}
                </p>
              </div>
            ) : null}

            <Tabs defaultValue="checklist" className="w-full">
              <TabsList className="w-full justify-start overflow-x-auto whitespace-nowrap rounded-lg bg-slate-100 p-1">
                <TabsTrigger value="info">
                  <CheckCircle2 className="mr-1 h-4 w-4" />
                  {t("tenders.detail.tab.info")}
                </TabsTrigger>
                <TabsTrigger value="checklist">
                  <Library className="mr-1 h-4 w-4" />
                  {t("tenders.detail.tab.checklist")}
                </TabsTrigger>
                <TabsTrigger value="result">
                  <Trophy className="mr-1 h-4 w-4" />
                  {t("tenders.detail.tab.result")}
                </TabsTrigger>
              </TabsList>

              <TabsContent value="info" className="mt-3">
                <InfoTab tender={tender} />
              </TabsContent>

              <TabsContent value="checklist" className="mt-3">
                <ChecklistTab
                  tender={tender}
                  canManage={canManage}
                  onPatch={handleChecklistPatch}
                  onUpload={handleUpload}
                  onOpenLibrary={() => setLibraryOpen(true)}
                  isTerminal={!!isTerminal}
                />
              </TabsContent>

              <TabsContent value="result" className="mt-3">
                <ResultTab
                  tender={tender}
                  timeline={timeline}
                  timelineLoading={timelineLoading}
                  canMarkResult={canMarkResult}
                  isTerminal={!!isTerminal}
                  onMarkWonClick={() => setMarkWonOpen(true)}
                  onMarkLostClick={() => setMarkLostOpen(true)}
                />
              </TabsContent>
            </Tabs>

            <LibraryPicker
              open={libraryOpen}
              onClose={() => setLibraryOpen(false)}
              tender={tender}
              onSubmit={handleAttachFromLibrary}
            />
            <MarkWonDialog
              open={markWonOpen}
              onClose={() => setMarkWonOpen(false)}
              tender={tender}
              onSubmit={handleMarkWon}
            />
            <MarkLostDialog
              open={markLostOpen}
              onClose={() => setMarkLostOpen(false)}
              onSubmit={handleMarkLost}
            />
          </>
        )}
      </div>
    </AdminLayout>
  );
};

export default AdminTenderDetail;
