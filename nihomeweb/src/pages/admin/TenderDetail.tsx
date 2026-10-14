import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  AlertTriangle,
  ArrowLeft,
  Calendar,
  CheckCircle2,
  ChevronDown,
  ClipboardList,
  Clock,
  FileSpreadsheet,
  History,
  Library,
  Loader2,
  Plus,
  Trash2,
  Trophy,
  Upload,
  UserRound,
  X,
  XCircle,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import TenderEstimatePanel from "./TenderEstimatePanel";
import { isTenderChecklistEvidenceReady } from "./tenderChecklist";
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
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
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
  type DepartmentWorkPackageResponse,
  type MasterDataOption,
  type OpportunityResponse,
  type TenderAssigneeOptionResponse,
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
  NotApplicable: "border-zinc-200 bg-zinc-100 text-zinc-600",
};

const CHECKLIST_STATUSES: TenderChecklistItemStatus[] = [
  "NotStarted",
  "Preparing",
  "Done",
  "Submitted",
  "NotApplicable",
];

const DEADLINE_ALERT_DAYS = 3;

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

const todayDateInputValue = () => {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, "0");
  const day = String(now.getDate()).padStart(2, "0");
  return `${now.getFullYear()}-${month}-${day}`;
};

/** Whole days until (or since, negative) the ISO deadline. */
const daysUntil = (iso: string): number => {
  const target = new Date(iso).getTime();
  const now = Date.now();
  return Math.ceil((target - now) / (1000 * 60 * 60 * 24));
};

// ---------------------------- header ----------------------------

interface HeaderProps {
  tender: TenderResponse;
  canRequestFinance: boolean;
}

const HeaderCard = ({ tender, canRequestFinance }: HeaderProps) => {
  const { t } = useI18n();
  const days = daysUntil(tender.submissionDeadline);
  const isTerminal =
    tender.status === "Won" || tender.status === "Lost" || tender.status === "Cancelled";

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm md:p-5">
      <div className="flex flex-col items-stretch gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0 flex-1">
          <div className="text-sm text-slate-500">
            <Link
              to="/admin/tenders"
              className="inline-flex items-center gap-1 hover:text-slate-800"
            >
              <ArrowLeft className="h-4 w-4" />
              {t("tenders.detail.back")}
            </Link>
          </div>
          <div className="mt-1 flex flex-wrap items-center gap-2">
            <h1 className="min-w-0 break-words text-xl font-bold text-slate-900 md:text-2xl xl:truncate">
              {tender.name}
            </h1>
            <Badge className={cn("border", STATUS_BADGE_STYLES[tender.status])} variant="outline">
              {t(`tenders.status.${tender.status}`)}
            </Badge>
          </div>
          <p className="mt-1 flex min-w-0 flex-wrap items-baseline gap-x-1.5 gap-y-0.5 break-words text-sm text-slate-600">
            <span className="font-mono text-slate-500">{tender.code}</span>
            <span>·</span>
            <span className="font-medium">{tender.customerName}</span>
            {tender.preparerName ? (
              <span className="text-xs text-slate-400">• {tender.preparerName}</span>
            ) : null}
          </p>
        </div>
        {canRequestFinance && tender.operationalProjectId && tender.status === "Preparing" ? (
          <Button
            asChild
            variant="outline"
            className="h-auto min-h-10 w-full whitespace-normal text-center sm:w-auto sm:shrink-0"
          >
            <Link
              to={`/admin/work-packages?create=1&purpose=FinanceTenderReview&projectId=${tender.operationalProjectId}&tenderId=${tender.id}`}
            >
              <CheckCircle2 className="mr-1 h-4 w-4 shrink-0" />
              {t("tenders.action.requestFinanceReview")}
            </Link>
          </Button>
        ) : null}
      </div>

      <div className="mt-4 grid gap-3 sm:grid-cols-3">
        <div
          className={cn(
            "rounded-lg p-3",
            isTerminal
              ? "bg-slate-50"
              : days < 0
                ? "bg-rose-50"
                : days <= DEADLINE_ALERT_DAYS
                  ? "bg-amber-50"
                  : "bg-slate-50",
          )}
        >
          <div className="text-xs text-slate-500">{t("tenders.field.deadline")}</div>
          <div className="mt-0.5 text-sm font-semibold text-slate-800">
            {formatDate(tender.submissionDeadline)}
          </div>
          {!isTerminal ? (
            <div
              className={cn(
                "mt-1 inline-flex items-center gap-1 text-xs font-medium",
                days < 0
                  ? "text-rose-700"
                  : days <= DEADLINE_ALERT_DAYS
                    ? "text-amber-700"
                    : "text-slate-500",
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
          <div className="text-xs text-slate-500">
            {t("tenders.detail.header.checklistProgress")}
          </div>
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

const InfoTab = ({
  tender,
  canManage,
  onUploadInvitation,
}: {
  tender: TenderResponse;
  canManage: boolean;
  onUploadInvitation: (file: File) => Promise<void>;
}) => {
  const { t, lang } = useI18n();
  const invitationFileRef = useRef<HTMLInputElement>(null);
  const rows: [string, string][] = [
    [t("tenders.field.code"), tender.code],
    [t("tenders.field.customer"), tender.customerName],
    [t("tenders.field.preparer"), tender.preparerName ?? "—"],
    [t("tenders.field.openingDate"), formatDate(tender.openingDate, lang)],
    [t("tenders.field.invitationReceivedAt"), formatDateTime(tender.invitationReceivedAt, lang)],
    [t("tenders.field.invitationReference"), tender.invitationReference ?? "—"],
    [t("tenders.field.invitationSource"), tender.invitationSource || "—"],
    [
      t("tenders.field.invitationReceivedBy"),
      tender.invitationReceivedByName || `#${tender.invitationReceivedByUserId}`,
    ],
    [t("tenders.field.invitationPortalUrl"), tender.invitationPortalUrl || "—"],
    [t("tenders.field.deadline"), formatDate(tender.submissionDeadline, lang)],
    [t("tenders.field.submittedAt"), formatDateTime(tender.submittedAt, lang)],
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
              <dt className="text-xs font-medium uppercase tracking-wide text-slate-500">
                {label}
              </dt>
              <dd className="mt-0.5 break-words text-sm text-slate-800">{value}</dd>
            </div>
          ))}
        </dl>
      </div>
      <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 className="text-sm font-semibold text-slate-800">
              {t("tenders.invitationDocuments.title")}
            </h3>
            <p className="text-xs text-slate-500">{t("tenders.invitationDocuments.hint")}</p>
          </div>
          {canManage ? (
            <>
              <input
                ref={invitationFileRef}
                type="file"
                className="hidden"
                accept=".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg"
                onChange={(event) => {
                  const file = event.target.files?.[0];
                  event.target.value = "";
                  if (file) void onUploadInvitation(file);
                }}
              />
              <Button
                size="sm"
                variant="outline"
                onClick={() => invitationFileRef.current?.click()}
              >
                <Upload className="mr-1 h-4 w-4" />
                {t("tenders.invitationDocuments.upload")}
              </Button>
            </>
          ) : null}
        </div>
        {tender.invitationDocuments.length === 0 ? (
          <p className="mt-3 text-sm text-slate-500">{t("tenders.invitationDocuments.empty")}</p>
        ) : (
          <ul className="mt-3 space-y-2">
            {tender.invitationDocuments.map((document) => (
              <li key={document.id} className="rounded border bg-slate-50 px-3 py-2 text-sm">
                <span className="break-all font-medium">
                  {document.originalFileName ?? document.externalUrl}
                </span>
                <span className="ml-2 text-xs text-slate-500">
                  {formatDateTime(document.createdAt, lang)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
      {tender.note ? (
        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <div className="text-xs font-medium uppercase tracking-wide text-slate-500">
            {t("tenders.field.note")}
          </div>
          <p className="mt-1 whitespace-pre-wrap break-words text-sm text-slate-800">
            {tender.note}
          </p>
        </div>
      ) : null}
    </div>
  );
};

// ---------------------------- checklist tab ----------------------------

interface ChecklistTabProps {
  tender: TenderResponse;
  planPackages: DepartmentWorkPackageResponse[];
  assignees: TenderAssigneeOptionResponse[] | null;
  canManage: boolean;
  onPatch: (
    itemId: number,
    body: Parameters<typeof adminApi.updateTenderChecklistItem>[2],
  ) => Promise<void>;
  onUpload: (itemId: number, file: File) => Promise<void>;
  onCreate: (body: Parameters<typeof adminApi.createTenderChecklistItem>[1]) => Promise<void>;
  onDelete: (itemId: number) => Promise<void>;
  onOpenLibrary: (itemId: number) => void;
  isTerminal: boolean;
}

const ChecklistTab = ({
  tender,
  planPackages,
  assignees,
  canManage,
  onPatch,
  onUpload,
  onCreate,
  onDelete,
  onOpenLibrary,
  isTerminal,
}: ChecklistTabProps) => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const disabled = !canManage || isTerminal;
  const [savingId, setSavingId] = useState<number | null>(null);
  const [fileInputTarget, setFileInputTarget] = useState<number | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [newTitle, setNewTitle] = useState("");
  const [newRequired, setNewRequired] = useState(true);
  const [newDocumentKind, setNewDocumentKind] = useState("Custom");
  const [newWorkTaskId, setNewWorkTaskId] = useState<string>("unlinked");
  const [notApplicableItem, setNotApplicableItem] = useState<TenderChecklistItemResponse | null>(
    null,
  );
  const [notApplicableReason, setNotApplicableReason] = useState("");
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleStatus = async (
    item: TenderChecklistItemResponse,
    next: TenderChecklistItemStatus,
  ) => {
    if (item.status === next) return;
    if (next === "NotApplicable") {
      setNotApplicableItem(item);
      setNotApplicableReason(item.notApplicableReason ?? "");
      return;
    }
    setSavingId(item.id);
    try {
      await onPatch(item.id, { status: next });
    } finally {
      setSavingId(null);
    }
  };

  const createItem = async () => {
    const title = newTitle.trim();
    if (!title) return;
    try {
      await onCreate({
        title,
        isRequired: newRequired,
        documentKind: newDocumentKind,
        workTaskId: newWorkTaskId === "unlinked" ? null : Number(newWorkTaskId),
      });
      setNewTitle("");
      setNewRequired(true);
      setNewDocumentKind("Custom");
      setNewWorkTaskId("unlinked");
      setCreateOpen(false);
    } catch {
      // Parent displays the API error; keep the dialog open for correction.
    }
  };

  const markNotApplicable = async () => {
    if (!notApplicableItem || !notApplicableReason.trim()) return;
    try {
      await onPatch(notApplicableItem.id, {
        status: "NotApplicable",
        isRequired: false,
        notApplicableReason: notApplicableReason.trim(),
      });
      setNotApplicableItem(null);
      setNotApplicableReason("");
    } catch {
      // Parent displays the API error; keep the dialog open for correction.
    }
  };

  const handleDeadline = async (item: TenderChecklistItemResponse, iso: string) => {
    const today = todayDateInputValue();
    if (iso && iso < today) {
      toast({
        title: t("common.error"),
        description: t("tenders.detail.checklist.deadlinePast"),
        variant: "destructive",
      });
      return;
    }
    if (iso && iso > toDateInputValue(tender.submissionDeadline)) {
      toast({
        title: t("common.error"),
        description: t("tenders.detail.checklist.deadlineAfterSubmission"),
        variant: "destructive",
      });
      return;
    }
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

  const handleOwner = async (item: TenderChecklistItemResponse, value: string) => {
    const nextOwnerId = value === "unassigned" ? null : Number(value);
    if (item.ownerUserId === nextOwnerId) return;
    setSavingId(item.id);
    try {
      await onPatch(
        item.id,
        nextOwnerId == null ? { clearOwner: true } : { ownerUserId: nextOwnerId },
      );
    } finally {
      setSavingId(null);
    }
  };

  const planTasks = planPackages.flatMap((workPackage) =>
    workPackage.tasks.map((task) => ({
      ...task,
      packageName: workPackage.name,
    })),
  );

  const handleWorkTask = async (item: TenderChecklistItemResponse, value: string) => {
    setSavingId(item.id);
    try {
      await onPatch(
        item.id,
        value === "unlinked" ? { clearWorkTask: true } : { workTaskId: Number(value) },
      );
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
      <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
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
        {!disabled && (
          <Button
            size="sm"
            variant="outline"
            onClick={() => setCreateOpen(true)}
            data-testid="tender-checklist-add"
          >
            <Plus className="mr-1 h-4 w-4" />
            {t("tenders.detail.checklist.add")}
          </Button>
        )}
      </div>

      <input
        ref={fileInputRef}
        type="file"
        className="hidden"
        onChange={onFileChange}
        accept=".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg"
      />

      {/* Desktop table */}
      <div className="hidden max-w-full overflow-x-auto rounded-lg border border-slate-200 bg-white shadow-sm xl:block">
        <table className="min-w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-3 py-2">{t("tenders.detail.checklist.header.title")}</th>
              <th className="px-3 py-2">{t("tenders.detail.checklist.header.status")}</th>
              <th className="px-3 py-2">{t("tenders.detail.checklist.header.owner")}</th>
              <th className="px-3 py-2">{t("tenders.detail.checklist.header.deadline")}</th>
              <th className="px-3 py-2">{t("tenders.detail.checklist.header.file")}</th>
              <th className="px-3 py-2 text-right">
                {t("tenders.detail.checklist.header.action")}
              </th>
            </tr>
          </thead>
          <tbody>
            {tender.checklistItems.map((item) => (
              <tr
                key={item.id}
                className="border-t align-top"
                data-testid={`tender-checklist-row-${item.templateCode ?? item.id}-desktop`}
              >
                <td className="px-3 py-2 font-medium text-slate-800">
                  <div>{item.title}</div>
                  <label className="mt-1 flex items-center gap-1 text-[11px] font-normal text-slate-500">
                    <input
                      type="checkbox"
                      checked={item.isRequired}
                      disabled={disabled || savingId === item.id}
                      onChange={(event) =>
                        void onPatch(item.id, { isRequired: event.target.checked })
                      }
                    />
                    {t("tenders.detail.checklist.required")}
                  </label>
                  <Badge variant="outline" className="mt-1 text-[10px] font-normal">
                    {t(
                      item.templateCode === "capability" && !item.capabilityDocumentId
                        ? "tenders.detail.checklist.sharedDocumentRequired"
                        : item.capabilityDocumentId
                          ? "tenders.detail.checklist.sharedDocument"
                          : "tenders.detail.checklist.packageDocument",
                    )}
                  </Badge>
                </td>
                <td className="px-3 py-2">
                  <Select
                    value={item.status}
                    disabled={disabled || savingId === item.id}
                    onValueChange={(v) => void handleStatus(item, v as TenderChecklistItemStatus)}
                  >
                    <SelectTrigger
                      className={cn("h-8 w-40 text-xs", CHECKLIST_STATUS_STYLES[item.status])}
                    >
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {CHECKLIST_STATUSES.filter(
                        (s) => s !== "NotApplicable" || !item.isRequired,
                      ).map((s) => (
                        <SelectItem key={s} value={s}>
                          {t(`tenders.detail.checklist.itemStatus.${s}`)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </td>
                <td className="px-3 py-2 text-xs text-slate-700">
                  {!disabled && assignees ? (
                    <Select
                      value={item.ownerUserId == null ? "unassigned" : String(item.ownerUserId)}
                      disabled={savingId === item.id}
                      onValueChange={(value) => void handleOwner(item, value)}
                    >
                      <SelectTrigger
                        className="h-8 w-44 text-xs"
                        aria-label={t("tenders.detail.checklist.assign")}
                        data-testid={`tender-checklist-owner-${item.templateCode ?? item.id}-desktop`}
                      >
                        <SelectValue placeholder={t("tenders.detail.checklist.assign")} />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="unassigned">
                          {t("tenders.detail.checklist.clearOwner")}
                        </SelectItem>
                        {assignees.map((assignee) => (
                          <SelectItem key={assignee.id} value={String(assignee.id)}>
                            {assignee.fullName}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  ) : (
                    (item.ownerName ?? <span className="text-slate-400">—</span>)
                  )}
                  <Select
                    value={item.workTaskId == null ? "unlinked" : String(item.workTaskId)}
                    disabled={disabled || savingId === item.id}
                    onValueChange={(value) => void handleWorkTask(item, value)}
                  >
                    <SelectTrigger
                      className="mt-1 h-8 w-44 text-xs"
                      aria-label={t("tenders.detail.checklist.workTask")}
                    >
                      <SelectValue placeholder={t("tenders.detail.checklist.workTask")} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="unlinked">
                        {t("tenders.detail.checklist.noWorkTask")}
                      </SelectItem>
                      {planTasks.map((task) => (
                        <SelectItem key={task.id} value={String(task.id)}>
                          {task.departmentCode} · {task.title}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </td>
                <td className="px-3 py-2">
                  <Input
                    type="date"
                    value={toDateInputValue(item.internalDeadline)}
                    min={todayDateInputValue()}
                    max={toDateInputValue(tender.submissionDeadline)}
                    aria-label={t("tenders.detail.checklist.setDeadline")}
                    className="h-8 w-40 text-xs"
                    disabled={disabled || savingId === item.id}
                    onChange={(e) => void handleDeadline(item, e.target.value)}
                  />
                </td>
                <td className="px-3 py-2">
                  {item.filePath ? (
                    <AdminFilePreview
                      url={item.filePath}
                      fileName={item.originalFileName}
                      showLabel
                      label={item.originalFileName ?? t("common.previewFile")}
                      variant="ghost"
                      testId={`tender-checklist-preview-${item.templateCode ?? item.id}-desktop`}
                      fetchFile={async () =>
                        (await adminApi.getTenderChecklistFileContent(tender.id, item.id)).data
                      }
                    />
                  ) : (
                    <span className="text-xs text-slate-400">
                      {t("tenders.detail.checklist.noFile")}
                    </span>
                  )}
                </td>
                <td className="px-3 py-2 text-right">
                  {!disabled ? (
                    <div className="flex justify-end gap-1">
                      {item.templateCode === "capability" ? (
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={savingId === item.id}
                          onClick={() => onOpenLibrary(item.id)}
                          data-testid={`tender-checklist-library-${item.templateCode ?? item.id}-desktop`}
                        >
                          <Library className="h-3.5 w-3.5" />
                          <span className="ml-1 text-xs">
                            {t("tenders.detail.checklist.pickFromLibrary")}
                          </span>
                        </Button>
                      ) : null}
                      {item.templateCode !== "capability" ? (
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={savingId === item.id}
                          onClick={() => triggerUpload(item.id)}
                          data-testid={`tender-checklist-upload-${item.templateCode ?? item.id}-desktop`}
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
                      {!item.templateCode && !item.filePath ? (
                        <Button
                          variant="ghost"
                          size="icon"
                          disabled={savingId === item.id}
                          aria-label={t("tenders.detail.checklist.delete")}
                          onClick={() => void onDelete(item.id).catch(() => undefined)}
                        >
                          <Trash2 className="h-3.5 w-3.5 text-rose-600" />
                        </Button>
                      ) : null}
                    </div>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Mobile cards */}
      <div className="grid min-w-0 gap-2 xl:hidden">
        {tender.checklistItems.map((item) => (
          <div
            key={item.id}
            className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm"
            data-testid={`tender-checklist-row-${item.templateCode ?? item.id}-mobile`}
          >
            <div className="flex items-start justify-between gap-2">
              <div className="min-w-0">
                <div className="break-words text-sm font-medium text-slate-800">{item.title}</div>
                <label className="mt-1 flex items-center gap-1 text-xs text-slate-500">
                  <input
                    type="checkbox"
                    checked={item.isRequired}
                    disabled={disabled || savingId === item.id}
                    onChange={(event) =>
                      void onPatch(item.id, { isRequired: event.target.checked })
                    }
                  />
                  {t("tenders.detail.checklist.required")}
                </label>
                <Badge variant="outline" className="mt-1 text-[10px] font-normal">
                  {t(
                    item.templateCode === "capability" && !item.capabilityDocumentId
                      ? "tenders.detail.checklist.sharedDocumentRequired"
                      : item.capabilityDocumentId
                        ? "tenders.detail.checklist.sharedDocument"
                        : "tenders.detail.checklist.packageDocument",
                  )}
                </Badge>
                {!disabled && assignees ? (
                  <Select
                    value={item.ownerUserId == null ? "unassigned" : String(item.ownerUserId)}
                    disabled={savingId === item.id}
                    onValueChange={(value) => void handleOwner(item, value)}
                  >
                    <SelectTrigger
                      className="mt-2 h-9 w-full text-xs"
                      aria-label={t("tenders.detail.checklist.assign")}
                      data-testid={`tender-checklist-owner-${item.templateCode ?? item.id}-mobile`}
                    >
                      <SelectValue placeholder={t("tenders.detail.checklist.assign")} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="unassigned">
                        {t("tenders.detail.checklist.clearOwner")}
                      </SelectItem>
                      {assignees.map((assignee) => (
                        <SelectItem key={assignee.id} value={String(assignee.id)}>
                          {assignee.fullName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : (
                  <div className="mt-0.5 text-xs text-slate-500">{item.ownerName ?? "—"}</div>
                )}
                <Select
                  value={item.workTaskId == null ? "unlinked" : String(item.workTaskId)}
                  disabled={disabled || savingId === item.id}
                  onValueChange={(value) => void handleWorkTask(item, value)}
                >
                  <SelectTrigger
                    className="mt-2 h-9 w-full text-xs"
                    aria-label={t("tenders.detail.checklist.workTask")}
                  >
                    <SelectValue placeholder={t("tenders.detail.checklist.workTask")} />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="unlinked">
                      {t("tenders.detail.checklist.noWorkTask")}
                    </SelectItem>
                    {planTasks.map((task) => (
                      <SelectItem key={task.id} value={String(task.id)}>
                        {task.departmentCode} · {task.title}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <Badge
                variant="outline"
                className={cn(
                  "max-w-[45%] shrink-0 whitespace-normal text-right text-xs leading-tight",
                  CHECKLIST_STATUS_STYLES[item.status],
                )}
              >
                {t(`tenders.detail.checklist.itemStatus.${item.status}`)}
              </Badge>
            </div>
            <div className="mt-2 grid min-w-0 grid-cols-1 gap-2 sm:grid-cols-2">
              <Select
                value={item.status}
                disabled={disabled || savingId === item.id}
                onValueChange={(v) => void handleStatus(item, v as TenderChecklistItemStatus)}
              >
                <SelectTrigger className="h-8 text-xs">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {CHECKLIST_STATUSES.filter((s) => s !== "NotApplicable" || !item.isRequired).map(
                    (s) => (
                      <SelectItem key={s} value={s}>
                        {t(`tenders.detail.checklist.itemStatus.${s}`)}
                      </SelectItem>
                    ),
                  )}
                </SelectContent>
              </Select>
              <Input
                type="date"
                value={toDateInputValue(item.internalDeadline)}
                min={todayDateInputValue()}
                max={toDateInputValue(tender.submissionDeadline)}
                aria-label={t("tenders.detail.checklist.setDeadline")}
                className="h-8 text-xs"
                disabled={disabled || savingId === item.id}
                onChange={(e) => void handleDeadline(item, e.target.value)}
              />
            </div>
            <div className="mt-2 text-xs">
              {item.filePath ? (
                <AdminFilePreview
                  url={item.filePath}
                  fileName={item.originalFileName}
                  showLabel
                  label={item.originalFileName ?? t("common.previewFile")}
                  variant="ghost"
                  testId={`tender-checklist-preview-${item.templateCode ?? item.id}-mobile`}
                  fetchFile={async () =>
                    (await adminApi.getTenderChecklistFileContent(tender.id, item.id)).data
                  }
                />
              ) : (
                <span className="text-slate-400">{t("tenders.detail.checklist.noFile")}</span>
              )}
              {!disabled ? (
                <div className="mt-2 grid grid-cols-1 gap-2">
                  {item.templateCode === "capability" ? (
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={savingId === item.id}
                      onClick={() => onOpenLibrary(item.id)}
                      data-testid={`tender-checklist-library-${item.templateCode ?? item.id}-mobile`}
                    >
                      <Library className="h-3.5 w-3.5" />
                      <span className="ml-1">{t("tenders.detail.checklist.pickFromLibrary")}</span>
                    </Button>
                  ) : null}
                  {item.templateCode !== "capability" ? (
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={savingId === item.id}
                      onClick={() => triggerUpload(item.id)}
                      data-testid={`tender-checklist-upload-${item.templateCode ?? item.id}-mobile`}
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
                  {!item.templateCode && !item.filePath ? (
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => void onDelete(item.id).catch(() => undefined)}
                    >
                      <Trash2 className="mr-1 h-3.5 w-3.5" />
                      {t("tenders.detail.checklist.delete")}
                    </Button>
                  ) : null}
                </div>
              ) : null}
            </div>
          </div>
        ))}
      </div>

      {tender.submissions.length > 0 ? (
        <section
          className="space-y-2 rounded-lg border border-sky-200 bg-sky-50/50 p-3 sm:p-4"
          data-testid="tender-submission-snapshots"
        >
          <div>
            <h3 className="text-sm font-semibold text-sky-950">
              {t("tenders.detail.checklist.snapshotTitle")}
            </h3>
            <p className="text-xs text-sky-800">{t("tenders.detail.checklist.snapshotHint")}</p>
          </div>
          {tender.submissions.map((submission) => (
            <div key={submission.id} className="rounded-md border border-sky-100 bg-white p-3">
              <div className="flex flex-col gap-1 text-xs text-slate-600 sm:flex-row sm:flex-wrap sm:items-center sm:gap-x-3">
                <span className="font-semibold text-slate-800">#{submission.sequence}</span>
                <span>{formatDateTime(submission.submittedAt, lang)}</span>
                <span className="break-words">
                  {submission.submittedByName ?? `#${submission.submittedByUserId}`}
                </span>
                <span>
                  {submission.itemCount} {t("tenders.detail.checklist.snapshotItems")}
                </span>
                <span>{submission.submissionChannel}</span>
                {submission.receiptReference ? <span>{submission.receiptReference}</span> : null}
              </div>
              <ul className="mt-2 grid gap-1 sm:grid-cols-2">
                {submission.items.map((item) => (
                  <li
                    key={item.checklistItemId}
                    className="min-w-0 rounded bg-slate-50 px-2 py-1.5 text-xs text-slate-700"
                  >
                    <div className="break-words font-medium">{item.title}</div>
                    <div className="mt-0.5 flex flex-wrap gap-x-2 text-[11px] text-slate-500">
                      <span>{t(`tenders.detail.checklist.itemStatus.${item.status}`)}</span>
                      <span>
                        {item.isRequired
                          ? t("tenders.detail.checklist.required")
                          : t("tenders.detail.checklist.optional")}
                      </span>
                      {item.originalFileName ? (
                        <span className="break-all">{item.originalFileName}</span>
                      ) : null}
                    </div>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </section>
      ) : null}

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="w-[calc(100vw-2rem)] max-w-md">
          <DialogHeader>
            <DialogTitle>{t("tenders.detail.checklist.add")}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label>{t("tenders.detail.checklist.customTitle")}</Label>
              <Input
                data-testid="tender-checklist-custom-title"
                value={newTitle}
                onChange={(event) => setNewTitle(event.target.value)}
                maxLength={300}
              />
            </div>
            <div className="space-y-1">
              <Label>{t("tenders.detail.checklist.documentKind")}</Label>
              <Select value={newDocumentKind} onValueChange={setNewDocumentKind}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {[
                    "Legal",
                    "BidSecurity",
                    "Boq",
                    "MethodStatement",
                    "Drawings",
                    "Schedule",
                    "Custom",
                  ].map((kind) => (
                    <SelectItem key={kind} value={kind}>
                      {t(`tenders.detail.checklist.documentKind.${kind}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>{t("tenders.detail.checklist.workTask")}</Label>
              <Select value={newWorkTaskId} onValueChange={setNewWorkTaskId}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="unlinked">
                    {t("tenders.detail.checklist.noWorkTask")}
                  </SelectItem>
                  {planTasks.map((task) => (
                    <SelectItem key={task.id} value={String(task.id)}>
                      {task.departmentCode} · {task.packageName} · {task.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <label className="flex items-center gap-2 text-sm">
              <input
                data-testid="tender-checklist-custom-required"
                type="checkbox"
                checked={newRequired}
                onChange={(event) => setNewRequired(event.target.checked)}
              />
              {t("tenders.detail.checklist.required")}
            </label>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button
              data-testid="tender-checklist-custom-save"
              disabled={!newTitle.trim()}
              onClick={() => void createItem()}
            >
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={notApplicableItem != null}
        onOpenChange={(open) => !open && setNotApplicableItem(null)}
      >
        <DialogContent className="w-[calc(100vw-2rem)] max-w-md">
          <DialogHeader>
            <DialogTitle>{t("tenders.detail.checklist.notApplicableTitle")}</DialogTitle>
            <DialogDescription>{notApplicableItem?.title}</DialogDescription>
          </DialogHeader>
          <div className="space-y-1">
            <Label>{t("tenders.detail.checklist.notApplicableReason")}</Label>
            <Textarea
              data-testid="tender-checklist-na-reason"
              value={notApplicableReason}
              onChange={(event) => setNotApplicableReason(event.target.value)}
              maxLength={500}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setNotApplicableItem(null)}>
              {t("common.cancel")}
            </Button>
            <Button
              data-testid="tender-checklist-na-save"
              disabled={!notApplicableReason.trim()}
              onClick={() => void markNotApplicable()}
            >
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
};

// ---------------------------- tender plan tab ----------------------------

const TenderPlanTab = ({
  tender,
  packages,
  financePackages,
  canManage,
  loadError,
  onRetry,
}: {
  tender: TenderResponse;
  packages: DepartmentWorkPackageResponse[];
  financePackages: DepartmentWorkPackageResponse[];
  canManage: boolean;
  loadError: string | null;
  onRetry: () => void;
}) => {
  const { t, lang } = useI18n();
  const active = packages.filter((item) => item.status !== "Cancelled");
  const cancelled = tender.status === "Cancelled";
  const hasPassedSubmission = ["Submitted", "Won", "Lost"].includes(tender.status);
  const complete =
    hasPassedSubmission ||
    (active.length > 0 && active.every((item) => item.status === "Completed"));
  const financeApproved =
    hasPassedSubmission ||
    financePackages.some(
      (item) =>
        item.operationalProjectId === tender.operationalProjectId &&
        item.financeReview?.status === "Approved",
    );
  const dossierReady = hasPassedSubmission || isTenderChecklistEvidenceReady(tender);
  const processStages = [
    { key: "received", done: true, active: false },
    { key: "planning", done: complete, active: !cancelled && !complete },
    { key: "dossier", done: dossierReady, active: !cancelled && complete && !dossierReady },
    {
      key: "review",
      done: financeApproved,
      active: !cancelled && dossierReady && !financeApproved,
    },
    {
      key: "submitted",
      done: hasPassedSubmission,
      active: !cancelled && financeApproved && tender.status === "Preparing",
    },
    {
      key: "result",
      done: ["Won", "Lost", "Cancelled"].includes(tender.status),
      active: tender.status === "Submitted",
    },
  ];
  return (
    <div className="space-y-3" data-testid="tender-plan-tab">
      <div className="pb-1" data-testid="tender-process-overview">
        <ol className="grid grid-cols-2 gap-2 sm:grid-cols-3 xl:grid-cols-6">
          {processStages.map((stage, index) => (
            <li
              key={stage.key}
              className={cn(
                "rounded-lg border px-3 py-2 text-xs",
                stage.done && "border-emerald-200 bg-emerald-50 text-emerald-800",
                stage.active && !stage.done && "border-primary/30 bg-primary/5 text-primary",
                !stage.done && !stage.active && "border-slate-200 bg-slate-50 text-slate-500",
              )}
            >
              <div className="mb-1 font-semibold">{index + 1}</div>
              <div>{t(`tenders.process.${stage.key}`)}</div>
            </li>
          ))}
        </ol>
      </div>
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0">
          <h2 className="text-base font-semibold text-slate-800">{t("tenders.plan.title")}</h2>
          <p className="text-sm text-muted-foreground">{t("tenders.plan.help")}</p>
        </div>
        {canManage && tender.status === "Preparing" && tender.operationalProjectId ? (
          <Button asChild className="w-full shrink-0 sm:w-auto">
            <Link
              to={`/admin/work-packages?create=1&purpose=TenderPlanning&projectId=${tender.operationalProjectId}&tenderId=${tender.id}`}
            >
              <ClipboardList className="mr-2 h-4 w-4" />
              {t("tenders.plan.assignDepartment")}
            </Link>
          </Button>
        ) : null}
      </div>
      {loadError ? (
        <div
          className="flex flex-col gap-3 rounded-lg border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800 sm:flex-row sm:items-center sm:justify-between"
          role="alert"
        >
          <span>{loadError}</span>
          <Button variant="outline" size="sm" className="shrink-0" onClick={onRetry}>
            {t("common.retry")}
          </Button>
        </div>
      ) : (
        <div
          className={cn(
            "rounded-lg border p-3 text-sm",
            complete
              ? "border-emerald-200 bg-emerald-50 text-emerald-800"
              : "border-amber-200 bg-amber-50 text-amber-900",
          )}
        >
          {active.length === 0
            ? t("tenders.plan.required")
            : complete
              ? t("tenders.plan.ready")
              : t("tenders.plan.inProgress")}
        </div>
      )}
      <div className="grid gap-3 lg:grid-cols-2">
        {packages.map((item) => (
          <Link
            key={item.id}
            to={`/admin/work-packages/${item.id}`}
            className="min-w-0 rounded-lg border bg-white p-4 shadow-sm transition hover:border-primary/40"
          >
            <div className="flex min-w-0 items-start justify-between gap-2">
              <div className="min-w-0">
                <div className="truncate font-medium">
                  {item.code} · {item.name}
                </div>
                <div className="text-sm text-muted-foreground">
                  {t(item.departmentLabelKey)} · {item.leadUserName}
                </div>
              </div>
              <Badge variant="outline" className="shrink-0">
                {t(`workPackages.status.${item.status}`)}
              </Badge>
            </div>
            <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
              <span>
                {formatDate(item.plannedStart, lang)} → {formatDate(item.plannedEnd, lang)}
              </span>
              <span>{item.progressPercent}%</span>
              <span>
                {item.tasks.length} {t("tenders.plan.tasks")}
              </span>
            </div>
            <Progress className="mt-2 h-2" value={item.progressPercent} />
          </Link>
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
  targetItemId: number | null;
  onSubmit: (
    assignments: { checklistItemId: number; capabilityDocumentId: number }[],
  ) => Promise<void>;
}

const LibraryPicker = ({ open, onClose, tender, targetItemId, onSubmit }: LibraryPickerProps) => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [docs, setDocs] = useState<CapabilityDocumentResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedDocId, setSelectedDocId] = useState<number | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const targetItem = tender.checklistItems.find((item) => item.id === targetItemId);

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
      setSelectedDocId(null);
    }
  }, [open, targetItemId]);

  const confirm = async () => {
    if (targetItemId == null) return;
    if (selectedDocId == null) return;
    setSubmitting(true);
    try {
      await onSubmit([{ checklistItemId: targetItemId, capabilityDocumentId: selectedDocId }]);
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
            <div
              className="mt-1 rounded-md border bg-muted/30 px-3 py-2 text-sm font-medium"
              data-testid="tender-library-target"
            >
              {targetItem?.title ?? "—"}
            </div>
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
            <RadioGroup
              className="grid max-h-80 gap-2 overflow-y-auto"
              value={selectedDocId != null ? String(selectedDocId) : ""}
              onValueChange={(value) => setSelectedDocId(Number(value))}
            >
              {docs.map((d) => (
                <label
                  key={d.id}
                  className={cn(
                    "flex cursor-pointer items-start gap-3 rounded-md border p-2.5 text-sm hover:bg-slate-50",
                    selectedDocId === d.id && "border-sky-200 bg-sky-50",
                  )}
                >
                  <RadioGroupItem value={String(d.id)} className="mt-0.5" />
                  <div className="min-w-0 flex-1">
                    <div className="break-words font-medium text-slate-800">{d.name}</div>
                    <div className="mt-0.5 flex flex-wrap gap-2 text-xs text-slate-500">
                      <span>{d.tagLabel ?? d.tagCode}</span>
                      {d.originalFileName ? <span>· {d.originalFileName}</span> : null}
                    </div>
                  </div>
                </label>
              ))}
            </RadioGroup>
          )}
        </div>

        <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
          <Button variant="outline" onClick={onClose} disabled={submitting}>
            {t("common.cancel")}
          </Button>
          <Button
            onClick={() => void confirm()}
            disabled={submitting || targetItemId == null || selectedDocId == null}
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

type WonContinuation = { opportunityId: number } | { createOpportunity: true };

interface MarkWonDialogProps {
  open: boolean;
  onClose: () => void;
  tender: TenderResponse;
  onSubmit: (continuation: WonContinuation, note: string | null) => Promise<void>;
}

const MarkWonDialog = ({ open, onClose, tender, onSubmit }: MarkWonDialogProps) => {
  const { t } = useI18n();
  const [opps, setOpps] = useState<OpportunityResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [oppId, setOppId] = useState<number | null>(null);
  // The won bid usually is the deal itself, so opening a new opportunity (with
  // its project) is the default; linking an existing one stays available.
  const [mode, setMode] = useState<"new" | "existing">("new");
  const [note, setNote] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    setOppId(null);
    setMode("new");
    setNote("");
    (async () => {
      try {
        const { data } = await adminApi.listOpportunities({
          customerId: tender.customerId,
          pageSize: 100,
        });
        if (!cancelled)
          setOpps(
            (data.items ?? []).filter(
              (item) =>
                item.operationalProjectId === tender.operationalProjectId &&
                item.stage !== "Won" &&
                item.stage !== "Lost",
            ),
          );
      } catch (err) {
        if (!cancelled) setError(extractApiError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [open, tender.customerId, tender.operationalProjectId]);

  const confirm = async () => {
    if (mode === "existing" && oppId == null) {
      setError(t("tenders.detail.result.opportunityRequired"));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await onSubmit(
        mode === "new" ? { createOpportunity: true } : { opportunityId: oppId! },
        note.trim() || null,
      );
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
          <DialogDescription>{t("tenders.detail.result.continueHint")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <div
            className="grid gap-2"
            role="radiogroup"
            aria-label={t("tenders.detail.result.continueHint")}
          >
            {(["new", "existing"] as const).map((option) => (
              <button
                key={option}
                type="button"
                role="radio"
                aria-checked={mode === option}
                data-testid={`tender-won-mode-${option}`}
                disabled={submitting || (option === "existing" && !loading && opps.length === 0)}
                onClick={() => {
                  setMode(option);
                  setError(null);
                }}
                className={cn(
                  "rounded-md border p-3 text-left text-sm transition-colors disabled:cursor-not-allowed disabled:opacity-50",
                  mode === option ? "border-primary bg-primary/5" : "hover:bg-muted/50",
                )}
              >
                <span className="font-medium">{t(`tenders.detail.result.mode.${option}`)}</span>
                <span className="mt-0.5 block text-xs text-muted-foreground">
                  {t(`tenders.detail.result.mode.${option}Hint`)}
                </span>
              </button>
            ))}
          </div>
          {mode === "existing" && (
            <div>
              <Label className="text-xs">{t("tenders.detail.result.pickOpportunity")}</Label>
              {loading ? (
                <div className="mt-1 flex items-center gap-2 text-sm text-muted-foreground">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  {t("common.loading")}
                </div>
              ) : opps.length === 0 ? (
                <p className="mt-1 text-xs text-rose-600">
                  {t("tenders.detail.result.noOpportunity")}
                </p>
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
          )}
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
            data-testid="tender-won-confirm"
            onClick={() => void confirm()}
            disabled={submitting || (mode === "existing" && opps.length === 0)}
          >
            {submitting ? (
              <Loader2 className="mr-1 h-4 w-4 animate-spin" />
            ) : (
              <Trophy className="mr-1 h-4 w-4" />
            )}
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
          <Button variant="destructive" onClick={() => void confirm()} disabled={submitting}>
            {submitting ? (
              <Loader2 className="mr-1 h-4 w-4 animate-spin" />
            ) : (
              <XCircle className="mr-1 h-4 w-4" />
            )}
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
                <dt className="text-xs text-emerald-700">
                  {t("tenders.detail.result.opportunity")}
                </dt>
                <dd className="break-words font-medium text-emerald-900">
                  {tender.wonOpportunityId != null ? (
                    <Link
                      to={`/admin/opportunities/${tender.wonOpportunityId}`}
                      className="underline underline-offset-2"
                    >
                      {tender.wonOpportunityName ?? `#${tender.wonOpportunityId}`}
                    </Link>
                  ) : (
                    "—"
                  )}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-emerald-700">{t("tenders.detail.result.closedAt")}</dt>
                <dd className="font-medium text-emerald-900">
                  {formatDateTime(tender.closedAt, lang)}
                </dd>
              </div>
            </dl>
            {tender.note ? (
              <div className="mt-2">
                <div className="text-xs text-emerald-700">{t("tenders.detail.result.note")}</div>
                <p className="text-sm text-emerald-900">{tender.note}</p>
              </div>
            ) : null}
            {tender.wonOpportunityId && tender.operationalProjectId ? (
              <Button asChild className="mt-3 w-full sm:w-auto">
                <Link
                  to={`/admin/contracts?opportunityId=${tender.wonOpportunityId}&operationalProjectId=${tender.operationalProjectId}&customerId=${tender.customerId}`}
                >
                  {t("tenders.detail.result.createContract")}
                </Link>
              </Button>
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
                <dd className="font-medium text-rose-900">
                  {formatDateTime(tender.closedAt, lang)}
                </dd>
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

        {!isTerminal && tender.status === "Submitted" && canMarkResult ? (
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
          <p className="mt-3 text-xs text-slate-500">
            {tender.status === "Preparing"
              ? t("tenders.lifecycle.resultRequiresSubmitted")
              : t("tenders.detail.markResultForbidden")}
          </p>
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
          <ChevronDown
            className={cn("h-4 w-4 transition-transform", historyOpen && "rotate-180")}
          />
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
                        <span className="rounded bg-slate-100 px-1.5 py-0.5 font-mono">
                          {ev.action}
                        </span>
                      </div>
                      {ev.message ? (
                        <div className="mt-0.5 break-words text-sm text-slate-700">
                          {ev.message}
                        </div>
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
  const canSubmit = has(ADMIN_PERMS.tendersSubmit);
  const canApproveEstimate = has(ADMIN_PERMS.tendersApproveEstimate);
  const navigate = useNavigate();

  const [tender, setTender] = useState<TenderResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [timeline, setTimeline] = useState<TenderTimelineEvent[] | null>(null);
  const [timelineLoading, setTimelineLoading] = useState(false);
  const [assignees, setAssignees] = useState<TenderAssigneeOptionResponse[] | null>(null);
  const [planPackages, setPlanPackages] = useState<DepartmentWorkPackageResponse[]>([]);
  const [financePackages, setFinancePackages] = useState<DepartmentWorkPackageResponse[]>([]);
  const [planError, setPlanError] = useState<string | null>(null);

  const [libraryTargetItemId, setLibraryTargetItemId] = useState<number | null>(null);
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

  const fetchPlan = useCallback(async () => {
    if (!Number.isFinite(tenderId)) return;
    setPlanError(null);
    try {
      const { data } = await adminApi.listWorkPackages({ sourceTenderId: tenderId, pageSize: 100 });
      setPlanPackages(
        data.items.filter(
          (item) => item.purpose === "TenderPlanning" || item.purpose === "General",
        ),
      );
      setFinancePackages(data.items.filter((item) => item.purpose === "FinanceTenderReview"));
    } catch (cause) {
      setPlanPackages([]);
      setFinancePackages([]);
      setPlanError(extractApiError(cause, t("tenders.plan.loadError")));
    }
  }, [t, tenderId]);

  useEffect(() => {
    void fetchTender();
    void fetchTimeline();
    void fetchPlan();
  }, [fetchPlan, fetchTender, fetchTimeline]);

  useEffect(() => {
    if (!canManage) return;
    let cancelled = false;
    adminApi
      .listTenderAssigneeOptions()
      .then(({ data }) => {
        if (!cancelled) setAssignees(data);
      })
      .catch(() => {
        if (!cancelled) {
          setAssignees(null);
          toast({
            title: t("common.error"),
            description: t("tenders.detail.checklist.assigneeLoadFailed"),
            variant: "destructive",
          });
        }
      });
    return () => {
      cancelled = true;
    };
  }, [canManage, t, toast]);

  const isTerminal = useMemo(
    () => tender?.status === "Won" || tender?.status === "Lost" || tender?.status === "Cancelled",
    [tender?.status],
  );
  const tenderPlanReady = useMemo(() => {
    const active = planPackages.filter((item) => item.status !== "Cancelled");
    return active.length > 0 && active.every((item) => item.status === "Completed");
  }, [planPackages]);
  const tenderFinanceReady = useMemo(
    () =>
      financePackages.some(
        (item) =>
          item.operationalProjectId === tender?.operationalProjectId &&
          item.financeReview?.status === "Approved",
      ),
    [financePackages, tender?.operationalProjectId],
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

  const handleInvitationUpload = async (file: File) => {
    try {
      const { data } = await adminApi.uploadTenderInvitationDocument(tenderId, file);
      setTender(data);
      toast({ title: t("tenders.detail.saved") });
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
      throw err;
    }
  };

  const handleChecklistCreate = async (
    body: Parameters<typeof adminApi.createTenderChecklistItem>[1],
  ) => {
    try {
      const { data } = await adminApi.createTenderChecklistItem(tenderId, body);
      setTender(data);
      toast({ title: t("tenders.detail.saved") });
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
      throw err;
    }
  };

  const handleChecklistDelete = async (itemId: number) => {
    try {
      const { data } = await adminApi.deleteTenderChecklistItem(tenderId, itemId);
      setTender(data);
      toast({ title: t("tenders.detail.saved") });
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
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

  const handleMarkWon = async (continuation: WonContinuation, note: string | null) => {
    const { data } = await adminApi.markTenderWon(tenderId, { ...continuation, note });
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
          <PageError
            message={t("tenders.detail.notFound")}
            onRetry={() => navigate("/admin/tenders")}
          />
        </div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout>
      <div className="min-w-0 space-y-4">
        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => void fetchTender()} />
        ) : !tender ? (
          <PageError message={t("tenders.detail.notFound")} onRetry={() => void fetchTender()} />
        ) : (
          <>
            <HeaderCard tender={tender} canRequestFinance={canManage} />

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

            <Tabs defaultValue="checklist" className="min-w-0 w-full">
              <TabsList
                data-testid="tender-detail-tabs"
                className="grid h-auto w-full grid-cols-2 gap-1 rounded-lg bg-slate-100 p-1 sm:grid-cols-3 xl:grid-cols-5"
              >
                <TabsTrigger
                  value="info"
                  className="h-full min-w-0 whitespace-normal px-2 py-2 text-center text-xs sm:text-sm"
                >
                  <CheckCircle2 className="mr-1 h-4 w-4 shrink-0" />
                  {t("tenders.detail.tab.info")}
                </TabsTrigger>
                <TabsTrigger
                  value="checklist"
                  className="h-full min-w-0 whitespace-normal px-2 py-2 text-center text-xs sm:text-sm"
                >
                  <Library className="mr-1 h-4 w-4 shrink-0" />
                  {t("tenders.detail.tab.checklist")}
                </TabsTrigger>
                <TabsTrigger
                  value="plan"
                  data-testid="tender-plan-tab-trigger"
                  className="h-full min-w-0 whitespace-normal px-2 py-2 text-center text-xs sm:text-sm"
                >
                  <Calendar className="mr-1 h-4 w-4 shrink-0" />
                  {t("tenders.plan.title")}
                </TabsTrigger>
                <TabsTrigger
                  value="estimate"
                  data-testid="tender-estimate-tab"
                  className="h-full min-w-0 whitespace-normal px-2 py-2 text-center text-xs sm:text-sm"
                >
                  <FileSpreadsheet className="mr-1 h-4 w-4 shrink-0" />
                  {t("tenders.estimate.title")}
                </TabsTrigger>
                <TabsTrigger
                  value="result"
                  className="col-span-2 h-full min-w-0 whitespace-normal px-2 py-2 text-center text-xs sm:col-span-1 sm:text-sm"
                >
                  <Trophy className="mr-1 h-4 w-4 shrink-0" />
                  {t("tenders.detail.tab.result")}
                </TabsTrigger>
              </TabsList>

              <TabsContent value="info" className="mt-3">
                <InfoTab
                  tender={tender}
                  canManage={canManage && !isTerminal}
                  onUploadInvitation={handleInvitationUpload}
                />
              </TabsContent>

              <TabsContent value="checklist" className="mt-3">
                <ChecklistTab
                  tender={tender}
                  planPackages={planPackages}
                  assignees={assignees}
                  canManage={canManage}
                  onPatch={handleChecklistPatch}
                  onUpload={handleUpload}
                  onCreate={handleChecklistCreate}
                  onDelete={handleChecklistDelete}
                  onOpenLibrary={setLibraryTargetItemId}
                  isTerminal={!!isTerminal}
                />
              </TabsContent>

              <TabsContent value="plan" className="mt-3">
                <TenderPlanTab
                  tender={tender}
                  packages={planPackages}
                  financePackages={financePackages}
                  canManage={canManage}
                  loadError={planError}
                  onRetry={() => void fetchPlan()}
                />
              </TabsContent>

              <TabsContent value="estimate" className="mt-3">
                <TenderEstimatePanel
                  tender={tender}
                  canManage={canManage}
                  canSubmit={canSubmit}
                  canMarkResult={canMarkResult}
                  canApprove={canApproveEstimate}
                  planReady={tenderPlanReady}
                  financeReady={tenderFinanceReady}
                  onTenderChanged={setTender}
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
              open={libraryTargetItemId != null}
              onClose={() => setLibraryTargetItemId(null)}
              tender={tender}
              targetItemId={libraryTargetItemId}
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
