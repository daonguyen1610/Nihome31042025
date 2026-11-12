import { useUiError } from "@/hooks/useUiError";
import { blankVoDraft } from "./contractDetailShared";
import { getUiError } from "@/lib/apiError";
import { useConfirm } from "@/components/ui/confirmation";
import { FieldScope } from "@/components/ui/field-scope";
import { MultiFilePicker } from "@/components/ui/multi-file-picker";
import { useMemo, useState } from "react";
import {
  FileText,
  Loader2,
  Pencil,
  Plus,
  Save,
  Send,
  ShieldCheck,
  ShieldX,
  Trash2,
  X,
} from "lucide-react";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import { useI18n } from "@/lib/i18n";
import { formatVndWithSymbol } from "@/lib/numberFormat";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import {
  adminApi,
  type ContractAppendixFileInfo,
  type ContractAppendixResponse,
  type ContractResponse,
  type UpsertContractAppendixRequest,
} from "@/services/adminApi";
import { VO_STATUS_STYLES, formatDate, formatBytes } from "./contractDetailShared";

export interface VoDraft {
  id?: number;
  title: string;
  reason: string;
  valueDelta: string;
  files: ContractAppendixFileInfo[];
  pendingFiles: File[];
}

export interface VoTabProps {
  contract: ContractResponse;
  rows: ContractAppendixResponse[];
  canManage: boolean;
  canDecide: boolean;
  refresh: () => Promise<void>;
}

const appendixFiles = (vo: ContractAppendixResponse): ContractAppendixFileInfo[] => {
  if (vo.files && vo.files.length > 0) return vo.files;
  if (!vo.filePath) return [];
  return [
    {
      filePath: vo.filePath,
      originalFileName: vo.originalFileName,
      fileSize: vo.fileSize,
      contentType: vo.contentType,
    },
  ];
};

export const VoTab = ({ contract, rows, canManage, canDecide, refresh }: VoTabProps) => {
  const uiError = useUiError();
  const confirmAction = useConfirm();
  const { t } = useI18n();
  const { toast } = useToast();
  const [draft, setDraft] = useState<VoDraft | null>(null);
  const [saving, setSaving] = useState(false);
  const [pendingActionId, setPendingActionId] = useState<number | null>(null);
  const [rejectingVo, setRejectingVo] = useState<ContractAppendixResponse | null>(null);
  const [rejectNote, setRejectNote] = useState("");

  const deletableIds = useMemo(
    () => (canManage ? rows.map((row) => row.id) : []),
    [canManage, rows],
  );
  const bulk = useBulkSelection<number>({
    visibleIds: deletableIds,
    deleteOne: (id) => adminApi.deleteContractAppendix(contract.id, id),
    onAfter: () => refresh(),
  });

  const openCreate = () => setDraft(blankVoDraft());
  const openEdit = (vo: ContractAppendixResponse) =>
    setDraft({
      id: vo.id,
      title: vo.title,
      reason: vo.reason,
      valueDelta: String(vo.valueDelta),
      files: appendixFiles(vo),
      pendingFiles: [],
    });

  const submitDraft = async () => {
    if (!draft) return;
    const delta = Number(draft.valueDelta);
    if (!Number.isFinite(delta) || delta === 0) {
      toast({ variant: "destructive", title: t("contracts.appendix.mustBeNonZero") });
      return;
    }
    if (!draft.title.trim() || !draft.reason.trim()) {
      toast({ variant: "destructive", title: t("contracts.documents.missingFields") });
      return;
    }
    setSaving(true);
    const uploaded: ContractAppendixFileInfo[] = [];
    try {
      for (const file of draft.pendingFiles) {
        const { data } = await adminApi.uploadContractAppendixFile(contract.id, file);
        uploaded.push(data);
      }
      const files = [...draft.files, ...uploaded];
      const primary = files[0];
      const body: UpsertContractAppendixRequest = {
        title: draft.title.trim(),
        reason: draft.reason.trim(),
        valueDelta: delta,
        files,
        filePath: primary?.filePath ?? null,
        originalFileName: primary?.originalFileName ?? null,
        fileSize: primary?.fileSize ?? null,
        contentType: primary?.contentType ?? null,
      };
      if (draft.id) {
        await adminApi.updateContractAppendix(contract.id, draft.id, body);
      } else {
        await adminApi.createContractAppendix(contract.id, body);
      }
      setDraft(null);
      await refresh();
    } catch (err) {
      if (uploaded.length > 0) {
        setDraft((current) =>
          current
            ? {
                ...current,
                files: [...current.files, ...uploaded],
                pendingFiles: current.pendingFiles.slice(uploaded.length),
              }
            : current,
        );
      }
      toast({ variant: "destructive", title: uiError(err) });
    } finally {
      setSaving(false);
    }
  };

  const runAction = async (vo: ContractAppendixResponse, kind: "submit" | "approve" | "delete") => {
    setPendingActionId(vo.id);
    try {
      if (kind === "submit") await adminApi.submitContractAppendix(contract.id, vo.id);
      else if (kind === "approve") await adminApi.approveContractAppendix(contract.id, vo.id);
      else {
        if (!(await confirmAction(t("contracts.appendix.confirmDelete")))) return;
        await adminApi.deleteContractAppendix(contract.id, vo.id);
      }
      await refresh();
    } catch (err) {
      toast({ variant: "destructive", title: uiError(err) });
    } finally {
      setPendingActionId(null);
    }
  };

  const openReject = (vo: ContractAppendixResponse) => {
    setRejectingVo(vo);
    setRejectNote("");
  };

  const confirmReject = async () => {
    if (!rejectingVo) return;
    if (!rejectNote.trim()) {
      toast({ variant: "destructive", title: t("contracts.appendix.rejectReasonRequired") });
      return;
    }
    setPendingActionId(rejectingVo.id);
    try {
      await adminApi.rejectContractAppendix(contract.id, rejectingVo.id, rejectNote.trim());
      setRejectingVo(null);
      setRejectNote("");
      await refresh();
    } catch (err) {
      toast({ variant: "destructive", title: uiError(err) });
    } finally {
      setPendingActionId(null);
    }
  };

  return (
    <div className="space-y-4">
      <div className="rounded-lg border border-sky-200 bg-sky-50 p-3 text-sm text-sky-950">
        <div className="flex items-start gap-2">
          <FileText className="mt-0.5 h-4 w-4 shrink-0" />
          <div>
            <p className="font-semibold">{t("contracts.appendix.guide.title")}</p>
            <p className="mt-1 text-sky-900">{t("contracts.appendix.guide.purpose")}</p>
            <p className="mt-1 text-sky-900">{t("contracts.appendix.guide.workflow")}</p>
            <p className="mt-1 text-sky-900">{t("contracts.appendix.guide.impact")}</p>
            <p className="mt-1 text-sky-900">{t("contracts.appendix.guide.delete")}</p>
          </div>
        </div>
      </div>
      <div className="flex flex-wrap items-center justify-between gap-2">
        {deletableIds.length > 0 ? (
          <label className="inline-flex items-center gap-2 text-xs text-muted-foreground">
            <Checkbox
              checked={
                bulk.allVisibleSelected ? true : bulk.someVisibleSelected ? "indeterminate" : false
              }
              onCheckedChange={(v) => bulk.toggleAllVisible(v === true)}
              aria-label={t("common.selectAll")}
            />
            {t("common.selectAll")}
          </label>
        ) : (
          <span />
        )}
        {canManage ? (
          <Button size="sm" onClick={openCreate}>
            <Plus className="mr-1 h-4 w-4" />
            {t("contracts.appendix.new")}
          </Button>
        ) : null}
      </div>

      <BulkActionBar
        selectedCount={bulk.selectedIds.size}
        bulkDeleting={bulk.bulkDeleting}
        onClear={bulk.clearSelection}
        onBulkDelete={bulk.handleBulkDelete}
      />

      {rows.length === 0 ? (
        <div className="rounded-lg border border-dashed border-input p-6 text-center text-sm text-muted-foreground">
          {t("contracts.appendix.empty")}
        </div>
      ) : (
        <div className="space-y-3">
          {rows.map((vo) => {
            const editable = canManage && (vo.status === "Draft" || vo.status === "Rejected");
            const canSubmit = canManage && vo.status === "Draft";
            const canReview = canDecide && vo.status === "Submitted";
            const canDelete = canManage;
            return (
              <div key={vo.id} className="rounded-lg border border-border bg-card p-4 shadow-sm">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div className="flex min-w-0 gap-3">
                    {canDelete ? (
                      <div className="pt-1" onClick={(e) => e.stopPropagation()}>
                        <Checkbox
                          checked={bulk.selectedIds.has(vo.id)}
                          onCheckedChange={(v) => bulk.toggleOne(vo.id, v === true)}
                          aria-label={`${t("common.selectItem")} · VO#${vo.voNumber}`}
                        />
                      </div>
                    ) : null}
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-sm font-semibold text-muted-foreground">
                          {t("contracts.appendix.number")} #{vo.voNumber}
                        </span>
                        <span className="truncate text-base font-semibold text-foreground">
                          {vo.title}
                        </span>
                        <Badge className={cn("border", VO_STATUS_STYLES[vo.status])}>
                          {t(`contracts.appendix.status.${vo.status}`)}
                        </Badge>
                      </div>
                      <p className="mt-1 whitespace-pre-wrap text-sm text-muted-foreground">
                        {vo.reason}
                      </p>
                      <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
                        <span
                          className={cn(
                            "font-semibold",
                            vo.valueDelta > 0 ? "text-emerald-700" : "text-rose-700",
                          )}
                        >
                          {vo.valueDelta > 0 ? "+" : ""}
                          {formatVndWithSymbol(vo.valueDelta)} (
                          {t(
                            vo.valueDelta > 0
                              ? "contracts.appendix.deltaPositive"
                              : "contracts.appendix.deltaNegative",
                          )}
                          )
                        </span>
                        {vo.submittedByName ? (
                          <span>
                            {t("contracts.appendix.submittedBy")}: {vo.submittedByName} •{" "}
                            {formatDate(vo.submittedAt)}
                          </span>
                        ) : null}
                        {vo.decidedByName ? (
                          <span>
                            {t("contracts.appendix.decidedBy")}: {vo.decidedByName} •{" "}
                            {formatDate(vo.decidedAt)}
                          </span>
                        ) : null}
                      </div>
                      {vo.decisionNote ? (
                        <p className="mt-1 text-xs italic text-muted-foreground">
                          {t("contracts.appendix.decisionNote")}: {vo.decisionNote}
                        </p>
                      ) : null}
                      {appendixFiles(vo).length > 0 ? (
                        <ul className="mt-2 space-y-1">
                          {appendixFiles(vo).map((file) => (
                            <li key={file.filePath}>
                              <AdminFilePreview
                                url={file.filePath}
                                fileName={file.originalFileName}
                                showLabel
                                label={file.originalFileName ?? t("common.previewFile")}
                                variant="ghost"
                                fetchFile={async () =>
                                  (
                                    await adminApi.getContractAppendixContent(
                                      contract.id,
                                      file.filePath,
                                    )
                                  ).data
                                }
                              />
                            </li>
                          ))}
                        </ul>
                      ) : null}
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {editable ? (
                      <Button size="sm" variant="outline" onClick={() => openEdit(vo)}>
                        <Pencil className="mr-1 h-3 w-3" />
                        {t("common.edit")}
                      </Button>
                    ) : null}
                    {canSubmit ? (
                      <Button
                        size="sm"
                        disabled={pendingActionId === vo.id}
                        onClick={() => runAction(vo, "submit")}
                      >
                        {pendingActionId === vo.id ? (
                          <Loader2 className="mr-1 h-3 w-3 animate-spin" />
                        ) : (
                          <Send className="mr-1 h-3 w-3" />
                        )}
                        {t("contracts.appendix.submit")}
                      </Button>
                    ) : null}
                    {canReview ? (
                      <>
                        <Button
                          size="sm"
                          disabled={pendingActionId === vo.id}
                          onClick={() => runAction(vo, "approve")}
                          className="bg-emerald-600 hover:bg-emerald-700"
                        >
                          <ShieldCheck className="mr-1 h-3 w-3" />
                          {t("contracts.appendix.approve")}
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={pendingActionId === vo.id}
                          onClick={() => openReject(vo)}
                          className="border-rose-200 text-rose-700 hover:bg-rose-50"
                        >
                          <ShieldX className="mr-1 h-3 w-3" />
                          {t("contracts.appendix.reject")}
                        </Button>
                      </>
                    ) : null}
                    {canDelete ? (
                      <Button
                        aria-label={t("common.delete")}
                        size="sm"
                        variant="ghost"
                        disabled={pendingActionId === vo.id}
                        onClick={() => runAction(vo, "delete")}
                        className="text-rose-700 hover:bg-rose-50"
                      >
                        <Trash2 className="h-3 w-3" />
                      </Button>
                    ) : null}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      <Dialog open={draft !== null} onOpenChange={(open) => (!open ? setDraft(null) : null)}>
        <DialogContent className="w-[95vw] max-w-lg">
          <DialogHeader>
            <DialogTitle>{draft?.id ? t("common.edit") : t("contracts.appendix.new")}</DialogTitle>
            <DialogDescription>{t("contracts.appendix.mustBeNonZero")}</DialogDescription>
          </DialogHeader>
          {draft ? (
            <div className="space-y-3">
              <FieldScope>
                <Label>{t("contracts.appendix.title")} *</Label>
                <Input
                  value={draft.title}
                  onChange={(e) => setDraft({ ...draft, title: e.target.value })}
                />
              </FieldScope>
              <FieldScope>
                <Label>{t("contracts.appendix.reason")} *</Label>
                <Textarea
                  rows={3}
                  value={draft.reason}
                  onChange={(e) => setDraft({ ...draft, reason: e.target.value })}
                />
              </FieldScope>
              <FieldScope>
                <Label>{t("contracts.appendix.valueDelta")} (₫) *</Label>
                <Input
                  type="number"
                  step="1"
                  value={draft.valueDelta}
                  onChange={(e) => setDraft({ ...draft, valueDelta: e.target.value })}
                  placeholder="45000000"
                />
              </FieldScope>
              <FieldScope>
                <Label>{t("contracts.appendix.file")}</Label>
                <MultiFilePicker
                  files={draft.pendingFiles}
                  onChange={(pendingFiles) =>
                    setDraft((current) => (current ? { ...current, pendingFiles } : current))
                  }
                  accept=".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg"
                  disabled={saving}
                  testId="contract-appendix-files"
                />
              </FieldScope>
              {draft.files.length > 0 ? (
                <ul className="space-y-1.5">
                  {draft.files.map((file, index) => (
                    <li
                      key={file.filePath}
                      className="flex items-center gap-2 rounded-md border bg-background px-2.5 py-1.5 text-sm"
                    >
                      <FileText className="h-4 w-4 shrink-0 text-muted-foreground" aria-hidden />
                      <span className="min-w-0 flex-1 break-all">
                        {file.originalFileName ?? file.filePath}
                      </span>
                      <span className="shrink-0 text-xs tabular-nums text-muted-foreground">
                        {formatBytes(file.fileSize)}
                      </span>
                      <button
                        type="button"
                        disabled={saving}
                        onClick={() =>
                          setDraft((current) =>
                            current
                              ? {
                                  ...current,
                                  files: current.files.filter((_, i) => i !== index),
                                }
                              : current,
                          )
                        }
                        aria-label={`${t("common.filePicker.remove")}: ${file.originalFileName ?? file.filePath}`}
                        className="flex h-8 w-8 shrink-0 items-center justify-center rounded text-muted-foreground transition-colors hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:opacity-50"
                      >
                        <X className="h-4 w-4" />
                      </button>
                    </li>
                  ))}
                </ul>
              ) : null}
            </div>
          ) : null}
          <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
            <Button variant="outline" onClick={() => setDraft(null)}>
              {t("common.cancel")}
            </Button>
            <Button onClick={submitDraft} disabled={saving}>
              {saving ? (
                <Loader2 className="mr-1 h-4 w-4 animate-spin" />
              ) : (
                <Save className="mr-1 h-4 w-4" />
              )}
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={rejectingVo !== null}
        onOpenChange={(open) => (!open ? setRejectingVo(null) : null)}
      >
        <DialogContent className="w-[95vw] max-w-md">
          <DialogHeader>
            <DialogTitle>{t("contracts.appendix.reject")}</DialogTitle>
            <DialogDescription>{t("contracts.appendix.rejectReasonRequired")}</DialogDescription>
          </DialogHeader>
          <Textarea
            aria-label={t("contracts.appendix.reason")}
            rows={3}
            value={rejectNote}
            onChange={(e) => setRejectNote(e.target.value)}
            placeholder={t("contracts.appendix.rejectReasonRequired")}
          />
          <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
            <Button variant="outline" onClick={() => setRejectingVo(null)}>
              {t("common.cancel")}
            </Button>
            <Button
              onClick={confirmReject}
              className="bg-rose-600 hover:bg-rose-700"
              disabled={pendingActionId !== null}
            >
              {t("contracts.appendix.reject")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
};
