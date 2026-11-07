import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { useConfirm } from "@/components/ui/confirmation";
import { FieldScope } from "@/components/ui/field-scope";
import { MultiFilePicker } from "@/components/ui/multi-file-picker";
import { useMemo, useState } from "react";
import { Loader2, Trash2, Upload } from "lucide-react";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import {
  adminApi,
  type ContractAttachmentKind,
  type ContractAttachmentResponse,
  type ContractResponse,
} from "@/services/adminApi";
import { formatBytes, formatDate } from "./contractDetailShared";

export interface DocumentsTabProps {
  contract: ContractResponse;
  rows: ContractAttachmentResponse[];
  canManage: boolean;
  error: string | null;
  refresh: () => Promise<void>;
}

export const DocumentsTab = ({ contract, rows, canManage, error, refresh }: DocumentsTabProps) => {
  const uiError = useUiError();
  const confirmAction = useConfirm();
  const { t } = useI18n();
  const { toast } = useToast();
  const [uploadKind, setUploadKind] = useState<ContractAttachmentKind>("Supporting");
  const [label, setLabel] = useState("");
  const [uploading, setUploading] = useState(false);
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [pendingFiles, setPendingFiles] = useState<File[]>([]);

  const visibleIds = useMemo(() => rows.map((a) => a.id), [rows]);
  const bulk = useBulkSelection<number>({
    visibleIds,
    deleteOne: (id) => adminApi.deleteContractAttachment(contract.id, id),
    onAfter: () => refresh(),
  });

  const handleFiles = async (files: File[]) => {
    if (files.length === 0) return;

    setUploading(true);
    let uploadedCount = 0;
    for (const file of files) {
      try {
        await adminApi.uploadContractAttachment(
          contract.id,
          file,
          uploadKind,
          label.trim() || undefined,
        );
        uploadedCount += 1;
      } catch (err) {
        toast({
          variant: "destructive",
          title: file.name,
          description: uiError(err),
        });
      }
    }

    try {
      if (uploadedCount > 0) await refresh();
    } catch (err) {
      toast({ variant: "destructive", title: uiError(err) });
    } finally {
      if (uploadedCount === files.length) setLabel("");
      setPendingFiles([]);
      setUploading(false);
    }
  };

  const handleDelete = async (id: number) => {
    if (!(await confirmAction(t("contracts.documents.confirmDelete")))) return;
    setDeletingId(id);
    try {
      await adminApi.deleteContractAttachment(contract.id, id);
      await refresh();
    } catch (err) {
      toast({ variant: "destructive", title: uiError(err) });
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <div className="space-y-4">
      {canManage ? (
        <div className="rounded-lg border border-border bg-card p-4 shadow-sm">
          <div className="grid gap-3 md:grid-cols-3">
            <FieldScope>
              <Label>{t("contracts.documents.typeLabel")}</Label>
              <Select
                value={uploadKind}
                onValueChange={(v) => setUploadKind(v as ContractAttachmentKind)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="SignedScan">
                    {t("contracts.documents.kind.SignedScan")}
                  </SelectItem>
                  <SelectItem value="Supporting">
                    {t("contracts.documents.kind.Supporting")}
                  </SelectItem>
                </SelectContent>
              </Select>
            </FieldScope>
            <FieldScope>
              <Label>{t("contracts.documents.label")}</Label>
              <Input value={label} onChange={(e) => setLabel(e.target.value)} placeholder="..." />
            </FieldScope>
            <div className="space-y-2 sm:col-span-2">
              <MultiFilePicker
                files={pendingFiles}
                onChange={setPendingFiles}
                accept=".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg"
                hint={t("contracts.documents.uploadHint")}
                disabled={uploading}
                testId="contract-attachment-files"
              />
              <Button
                className="w-full sm:w-auto"
                disabled={uploading || pendingFiles.length === 0}
                onClick={() => void handleFiles(pendingFiles)}
              >
                {uploading ? (
                  <Loader2 className="mr-1 h-4 w-4 animate-spin" />
                ) : (
                  <Upload className="mr-1 h-4 w-4" />
                )}
                {t("contracts.documents.upload")}
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      {error ? (
        <div className="flex flex-col items-center gap-3 rounded-lg border border-destructive/40 p-6 text-center text-sm text-destructive">
          <p>{error}</p>
          <Button type="button" variant="outline" size="sm" onClick={() => void refresh()}>
            {t("common.retry")}
          </Button>
        </div>
      ) : rows.length === 0 ? (
        <div className="rounded-lg border border-dashed border-input p-6 text-center text-sm text-muted-foreground">
          {t("contracts.documents.empty")}
        </div>
      ) : (
        <>
          {canManage ? (
            <div className="flex items-center gap-3">
              <label className="inline-flex items-center gap-2 text-xs text-muted-foreground">
                <Checkbox
                  checked={
                    bulk.allVisibleSelected
                      ? true
                      : bulk.someVisibleSelected
                        ? "indeterminate"
                        : false
                  }
                  onCheckedChange={(v) => bulk.toggleAllVisible(v === true)}
                  aria-label={t("common.selectAll")}
                />
                {t("common.selectAll")}
              </label>
            </div>
          ) : null}
          {canManage ? (
            <BulkActionBar
              selectedCount={bulk.selectedIds.size}
              bulkDeleting={bulk.bulkDeleting}
              onClear={bulk.clearSelection}
              onBulkDelete={bulk.handleBulkDelete}
            />
          ) : null}
          <div className="space-y-2">
            {rows.map((att) => (
              <div
                key={att.id}
                className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-border bg-card p-3 shadow-sm"
              >
                <div className="flex min-w-0 gap-3">
                  {canManage ? (
                    <div className="pt-1" onClick={(e) => e.stopPropagation()}>
                      <Checkbox
                        checked={bulk.selectedIds.has(att.id)}
                        onCheckedChange={(v) => bulk.toggleOne(att.id, v === true)}
                        aria-label={`${t("common.selectAll")} · ${att.originalFileName}`}
                      />
                    </div>
                  ) : null}
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge
                        className={cn(
                          "border",
                          att.kind === "SignedScan"
                            ? "border-emerald-200 bg-emerald-50 text-emerald-700"
                            : "border-border bg-muted/30 text-foreground",
                        )}
                      >
                        {t(`contracts.documents.kind.${att.kind}`)}
                      </Badge>
                      <span className="truncate text-sm font-medium text-foreground">
                        {att.originalFileName}
                      </span>
                    </div>
                    <div className="mt-1 flex flex-wrap gap-x-3 text-xs text-muted-foreground">
                      <span>{formatBytes(att.fileSize)}</span>
                      <span>{formatDate(att.createdAt)}</span>
                      {att.uploadedByName ? (
                        <span>
                          {t("contracts.documents.uploadedBy")}: {att.uploadedByName}
                        </span>
                      ) : null}
                      {att.label ? <span>“{att.label}”</span> : null}
                    </div>
                  </div>
                </div>
                <div className="flex gap-2">
                  <AdminFilePreview
                    url={att.filePath}
                    fileName={att.originalFileName}
                    contentType={att.contentType}
                    showLabel
                    fetchFile={async () =>
                      (await adminApi.getContractAttachmentContent(contract.id, att.id)).data
                    }
                  />
                  {canManage ? (
                    <Button
                      size="sm"
                      variant="ghost"
                      disabled={deletingId === att.id}
                      onClick={() => handleDelete(att.id)}
                      className="text-rose-700 hover:bg-rose-50"
                      aria-label={`${t("common.delete")} · ${att.originalFileName}`}
                    >
                      {deletingId === att.id ? (
                        <Loader2 className="h-3 w-3 animate-spin" />
                      ) : (
                        <Trash2 className="h-3 w-3" />
                      )}
                    </Button>
                  ) : null}
                </div>
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  );
};
