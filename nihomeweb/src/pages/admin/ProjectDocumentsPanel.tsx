import { useUiError } from "@/hooks/useUiError";
import { useUploadLimit } from "@/hooks/useUploadLimit";
import { getUiError } from "@/lib/apiError";
import { useConfirm } from "@/components/ui/confirmation";
import { Fragment, useCallback, useEffect, useRef, useState } from "react";
import {
  AlertTriangle,
  Download,
  ExternalLink,
  FileText,
  Loader2,
  RefreshCcw,
  RotateCcw,
  Trash2,
  Upload,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { MultiFilePicker } from "@/components/ui/multi-file-picker";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useToast } from "@/hooks/use-toast";
import { useProjectDocumentSync } from "@/hooks/useProjectDocumentSync";
import { extractApiError } from "@/lib/apiError";
import { newIdempotencyKey } from "@/lib/api";
import { useI18n } from "@/lib/i18n";
import { formatFileSize } from "@/lib/numberFormat";
import {
  adminApi,
  type ProjectDocumentCategoryResponse,
  type ProjectDocumentResponse,
  type ProjectDocumentSyncStatus,
} from "@/services/adminApi";
import { formatDateTime as formatDateTimeInZone } from "@/lib/displaySettings";

const MAX_FILES = 20;
const BLOCKED_EXTENSIONS = new Set([
  "exe",
  "dll",
  "com",
  "bat",
  "cmd",
  "sh",
  "ps1",
  "js",
  "html",
  "htm",
  "svg",
]);

const statusClass: Record<ProjectDocumentSyncStatus, string> = {
  Pending: "border-border bg-muted/30 text-foreground",
  Processing: "border-warning/40 bg-warning-soft text-warning-strong",
  Synced: "border-success/30 bg-success-soft text-success-strong",
  Failed: "border-destructive/20 bg-destructive/10 text-destructive",
  Deleted: "border-border bg-muted/30 text-muted-foreground",
  Conflict: "border-warning/40 bg-warning-soft text-warning-strong",
  Missing: "border-destructive/20 bg-destructive/10 text-destructive",
  Damaged: "border-warning/40 bg-warning-soft text-warning-strong",
};

const isLostOnDrive = (document: ProjectDocumentResponse) =>
  document.syncStatus === "Missing" || document.syncStatus === "Damaged";

type Props = {
  projectId: number;
  designProjectId?: number;
  canManage: boolean;
  onCountChange?: (count: number) => void;
  sourceOptions?: Array<{ entityType: string; recordId: number; label: string; category: string }>;
};

const extensionOf = (fileName: string) => fileName.split(".").pop()?.toLowerCase() ?? "";

export default function ProjectDocumentsPanel({
  projectId,
  designProjectId,
  canManage,
  onCountChange,
  sourceOptions,
}: Props) {
  const uiError = useUiError();
  const confirmAction = useConfirm();
  const { t, lang } = useI18n();
  const { toast } = useToast();
  // Per-file limit from Settings; null until loaded (the server still checks).
  const uploadLimitMb = useUploadLimit("projectDocuments");
  const uploadIdempotencyKeysRef = useRef(new Map<File, string>());
  const [documents, setDocuments] = useState<ProjectDocumentResponse[]>([]);
  const [categories, setCategories] = useState<ProjectDocumentCategoryResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [files, setFiles] = useState<File[]>([]);
  const [category, setCategory] = useState("");
  const [sourceKey, setSourceKey] = useState("");
  const [uploading, setUploading] = useState(false);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [classifications, setClassifications] = useState<Record<number, string>>({});
  const updateDocuments = useCallback(
    (nextDocuments: ProjectDocumentResponse[]) => {
      setDocuments(nextDocuments);
      onCountChange?.(nextDocuments.length);
    },
    [onCountChange],
  );
  const syncError = useProjectDocumentSync(
    projectId,
    documents.some((item) => item.syncStatus === "Pending" || item.syncStatus === "Processing"),
    updateDocuments,
    designProjectId,
  );

  const formatDateTime = (value?: string | null) =>
    value ? formatDateTimeInZone(value, lang, { dateStyle: "medium", timeStyle: "short" }) : "—";

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [documentResponse, categoryResponse] = await Promise.all([
        adminApi.listProjectDocuments(projectId, designProjectId),
        adminApi.listProjectDocumentCategories(),
      ]);
      const nextDocuments = documentResponse.data ?? [];
      setDocuments(nextDocuments);
      setCategories(categoryResponse.data ?? []);
      onCountChange?.(nextDocuments.length);
    } catch (reason) {
      setError(uiError(reason));
    } finally {
      setLoading(false);
    }
  }, [projectId, designProjectId, onCountChange, uiError]);

  useEffect(() => {
    void load();
  }, [load]);
  useEffect(() => {
    uploadIdempotencyKeysRef.current.clear();
    setFiles([]);
  }, [projectId]);

  const selectFiles = (nextFiles: File[]) => {
    const validationMessage =
      nextFiles.length > MAX_FILES
        ? t("operationalProjects.documents.validation.maxFiles", { count: MAX_FILES })
        : nextFiles.some((file) => !file.name.trim() || file.size === 0)
          ? t("operationalProjects.documents.validation.fileRequired")
          : nextFiles.some((file) => BLOCKED_EXTENSIONS.has(extensionOf(file.name)))
            ? t("operationalProjects.documents.validation.extension")
            : uploadLimitMb != null &&
                nextFiles.some((file) => file.size > uploadLimitMb * 1024 * 1024)
              ? t("operationalProjects.documents.validation.sizeLimit", { max: uploadLimitMb })
              : null;
    if (validationMessage) {
      toast({
        title: t("common.error"),
        description: validationMessage,
        variant: "destructive",
      });
      return;
    }
    setFiles(nextFiles);
    uploadIdempotencyKeysRef.current.clear();
  };

  const selectCategory = (value: string) => {
    setCategory(value);
    uploadIdempotencyKeysRef.current.clear();
  };

  const upload = async () => {
    if (files.length === 0) {
      toast({
        title: t("common.error"),
        description: t("operationalProjects.documents.validation.fileRequired"),
        variant: "destructive",
      });
      return;
    }
    if (!category || !categories.some((item) => item.value === category)) {
      toast({
        title: t("common.error"),
        description: t("operationalProjects.documents.validation.categoryRequired"),
        variant: "destructive",
      });
      return;
    }
    const source = sourceOptions?.find(
      (item) => `${item.entityType}:${item.recordId}` === sourceKey,
    );
    if (sourceOptions?.length && !source) {
      toast({
        title: t("common.error"),
        description: t("designProjects.documents.validation.sourceRequired"),
        variant: "destructive",
      });
      return;
    }
    setUploading(true);
    const failed: File[] = [];
    let firstError: string | null = null;
    let uploaded = 0;
    for (const file of files) {
      const idempotencyKey = uploadIdempotencyKeysRef.current.get(file) ?? newIdempotencyKey();
      uploadIdempotencyKeysRef.current.set(file, idempotencyKey);
      try {
        await adminApi.uploadProjectDocument(
          projectId,
          file,
          category,
          idempotencyKey,
          source
            ? { module: "Design", entityType: source.entityType, recordId: source.recordId }
            : undefined,
        );
        uploadIdempotencyKeysRef.current.delete(file);
        uploaded += 1;
      } catch (reason) {
        failed.push(file);
        firstError ??= `${file.name}: ${uiError(reason)}`;
      }
    }
    setFiles(failed);
    if (uploaded > 0) {
      toast({ title: t("operationalProjects.documents.uploaded") });
      await load();
    }
    if (firstError) {
      toast({ title: t("common.error"), description: firstError, variant: "destructive" });
    }
    if (failed.length === 0) {
      setCategory("");
      setSourceKey("");
    }
    setUploading(false);
  };

  const mutate = async (
    document: ProjectDocumentResponse,
    action: "retry" | "classify" | "resolve" | "delete" | "verify",
  ) => {
    if (
      action === "delete" &&
      !(await confirmAction(
        t("operationalProjects.documents.deleteConfirm", { name: document.originalFileName }),
      ))
    )
      return;
    if (
      action === "resolve" &&
      !(await confirmAction(
        t("operationalProjects.documents.keepBothConfirm", { name: document.originalFileName }),
        { destructive: false },
      ))
    )
      return;
    const selectedCategory = classifications[document.id];
    if (action === "classify" && !categories.some((item) => item.value === selectedCategory)) {
      toast({
        title: t("common.error"),
        description: t("operationalProjects.documents.validation.categoryRequired"),
        variant: "destructive",
      });
      return;
    }
    setBusyId(document.id);
    try {
      if (action === "retry") await adminApi.retryProjectDocument(projectId, document.id);
      if (action === "verify") await adminApi.verifyProjectDocument(projectId, document.id);
      if (action === "classify")
        await adminApi.classifyProjectDocument(projectId, document.id, selectedCategory);
      if (action === "resolve")
        await adminApi.resolveProjectDocumentKeepBoth(projectId, document.id);
      if (action === "delete") await adminApi.deleteProjectDocument(projectId, document.id);
      toast({ title: t(`operationalProjects.documents.${action}Success`) });
      if (action === "delete") {
        const nextDocuments = documents.filter((item) => item.id !== document.id);
        setDocuments(nextDocuments);
        onCountChange?.(nextDocuments.length);
      } else {
        await load();
      }
    } catch (reason) {
      toast({
        title: t("common.error"),
        description: uiError(reason),
        variant: "destructive",
      });
    } finally {
      setBusyId(null);
    }
  };

  const download = async (document: ProjectDocumentResponse) => {
    setBusyId(document.id);
    try {
      const response = await adminApi.downloadProjectDocument(projectId, document.id);
      const url = URL.createObjectURL(response.data);
      const link = window.document.createElement("a");
      link.href = url;
      link.download = document.originalFileName;
      link.click();
      URL.revokeObjectURL(url);
    } catch (reason) {
      toast({
        title: t("common.error"),
        description: uiError(reason),
        variant: "destructive",
      });
    } finally {
      setBusyId(null);
    }
  };

  const canRetry = (document: ProjectDocumentResponse) =>
    (document.syncStatus === "Missing" && Boolean(document.hasServerCopy)) ||
    document.syncStatus === "Failed" ||
    (document.syncStatus === "Pending" && Boolean(document.nextSyncAttemptAt));

  const categoryLabel = (value: string) => {
    if (value === "Unclassified") return t("operationalProjects.documents.category.Unclassified");
    const item = categories.find((option) => option.value === value);
    return item ? t(item.translationKey) : value;
  };

  const statusBadge = (document: ProjectDocumentResponse) => (
    <Badge variant="outline" className={cn("whitespace-nowrap", statusClass[document.syncStatus])}>
      {t(`operationalProjects.documents.syncStatus.${document.syncStatus}`)}
    </Badge>
  );

  // Reference data sits quietly under the file name so name, category, status and actions stay scannable.
  const fileMeta = (document: ProjectDocumentResponse) => (
    <div className="mt-0.5 text-xs font-normal text-muted-foreground">
      <p>
        {formatFileSize(document.size)} · {t("operationalProjects.documents.field.sourceModule")}:{" "}
        {t(`operationalProjects.documents.sourceModule.${document.sourceModule}`)} ·{" "}
        {t("operationalProjects.documents.field.origin")}:{" "}
        {t(`operationalProjects.documents.origin.${document.origin}`)}
      </p>
      <p className="tabular-nums">
        {t("operationalProjects.documents.field.modifiedAt")}:{" "}
        {formatDateTime(document.driveModifiedAt ?? document.updatedAt)}
      </p>
    </div>
  );

  const hasNotice = (document: ProjectDocumentResponse) =>
    Boolean(document.syncError) || isLostOnDrive(document);

  const recoveryActions = (document: ProjectDocumentResponse) => (
    <>
      {canManage && isLostOnDrive(document) && (
        <Button
          size="sm"
          variant="outline"
          onClick={() => void mutate(document, "verify")}
          disabled={busyId === document.id}
          aria-label={t("operationalProjects.documents.verifyAria", {
            name: document.originalFileName,
          })}
        >
          <RefreshCcw className="mr-1.5 h-4 w-4" />
          {t("operationalProjects.documents.verify")}
        </Button>
      )}
      {canManage && canRetry(document) && (
        <Button
          size="sm"
          variant="outline"
          onClick={() => void mutate(document, "retry")}
          disabled={busyId === document.id}
          aria-label={t("operationalProjects.documents.retryAria", {
            name: document.originalFileName,
          })}
        >
          <RotateCcw className="mr-1.5 h-4 w-4" />
          {t("operationalProjects.documents.retry")}
        </Button>
      )}
    </>
  );

  // The reason and what to do about it, on their own full-width line.
  const notice = (document: ProjectDocumentResponse) => {
    const isError = document.syncStatus === "Missing" || document.syncStatus === "Failed";
    return (
      <>
        {hasNotice(document) && (
          <div
            className={cn(
              "flex flex-col gap-3 rounded-md p-3 sm:flex-row sm:items-center sm:justify-between",
              isError ? "bg-destructive/5" : "bg-warning-soft",
            )}
            data-testid={`project-document-notice-${document.id}`}
          >
            <div className="flex min-w-0 items-start gap-2">
              <AlertTriangle
                aria-hidden="true"
                className={cn(
                  "mt-0.5 h-4 w-4 shrink-0",
                  isError ? "text-destructive" : "text-warning-strong",
                )}
              />
              <p className="max-w-[75ch] break-words text-sm font-normal text-foreground">
                {document.syncError
                  ? t(document.syncError)
                  : t(`operationalProjects.documents.syncStatus.${document.syncStatus}`)}
              </p>
            </div>
            <div className="flex shrink-0 flex-wrap gap-2 pl-6 sm:pl-0">
              {recoveryActions(document)}
            </div>
          </div>
        )}
        {document.unsupportedReason && (
          <p className="max-w-[75ch] text-xs font-normal text-muted-foreground">
            {document.unsupportedReason}
          </p>
        )}
      </>
    );
  };

  const actions = (document: ProjectDocumentResponse, variant: "ghost" | "outline") => (
    <div
      className={cn(
        "flex items-center gap-1",
        variant === "ghost" ? "flex-nowrap [&_a]:px-2 [&_button]:px-2" : "flex-wrap",
      )}
      data-testid={`project-document-actions-${document.id}`}
    >
      {document.isDownloadable && (document.syncStatus !== "Missing" || document.hasServerCopy) && (
        <Button
          size="sm"
          variant={variant}
          onClick={() => void download(document)}
          disabled={busyId === document.id}
          aria-label={t("operationalProjects.documents.downloadAria", {
            name: document.originalFileName,
          })}
        >
          <Download className="mr-1.5 h-4 w-4" />
          {t("operationalProjects.documents.download")}
        </Button>
      )}
      {document.driveWebViewLink && (
        <Button size="sm" variant={variant} asChild>
          <a
            href={document.driveWebViewLink}
            target="_blank"
            rel="noreferrer noopener"
            aria-label={t("operationalProjects.documents.openDriveAria", {
              name: document.originalFileName,
            })}
          >
            <ExternalLink className="mr-1.5 h-4 w-4" />
            {t("operationalProjects.documents.openDrive")}
          </a>
        </Button>
      )}
      {!hasNotice(document) && recoveryActions(document)}
      {canManage && document.conflictState === "PendingConfirmation" && (
        <Button
          size="sm"
          variant="outline"
          onClick={() => void mutate(document, "resolve")}
          disabled={busyId === document.id}
        >
          {t("operationalProjects.documents.keepBoth")}
        </Button>
      )}
      {canManage && document.sourceType !== "ExistingManagedFile" && (
        <Button
          size="sm"
          variant={variant}
          className="text-destructive hover:bg-destructive/10 hover:text-destructive"
          onClick={() => void mutate(document, "delete")}
          disabled={busyId === document.id}
          aria-label={t("operationalProjects.documents.deleteAria", {
            name: document.originalFileName,
          })}
        >
          <Trash2 className="mr-1.5 h-4 w-4" />
          {t("common.delete")}
        </Button>
      )}
    </div>
  );

  return (
    <div className="min-w-0 space-y-4" data-testid="project-documents-section">
      <p className="text-sm text-muted-foreground">
        {t("operationalProjects.documents.description")}
      </p>
      {canManage && (
        <div
          className="grid min-w-0 grid-cols-1 gap-3 rounded-md border bg-muted/30 p-4 md:grid-cols-2 xl:grid-cols-[minmax(0,1fr)_minmax(0,0.65fr)_minmax(0,0.65fr)_auto] xl:items-end [&>div]:min-w-0"
          data-testid="project-documents-upload"
        >
          <div>
            <Label htmlFor="project-document-file">{t("operationalProjects.documents.file")}</Label>
            <MultiFilePicker
              id="project-document-file"
              files={files}
              onChange={selectFiles}
              maxFiles={MAX_FILES}
              maxSizeMb={uploadLimitMb}
              disabled={uploading}
              testId="project-document-file"
            />
          </div>
          {sourceOptions?.length ? (
            <div>
              <Label htmlFor="project-document-source">
                {t("designProjects.documents.sourceRecord")}
              </Label>
              <Select
                value={sourceKey}
                disabled={uploading}
                onValueChange={(value) => {
                  const source = sourceOptions.find(
                    (item) => `${item.entityType}:${item.recordId}` === value,
                  );
                  setSourceKey(value);
                  uploadIdempotencyKeysRef.current.clear();
                  if (source) selectCategory(source.category);
                }}
              >
                <SelectTrigger
                  id="project-document-source"
                  className="mt-1 min-w-0 [&>span]:truncate"
                >
                  <SelectValue placeholder={t("designProjects.documents.selectSourceRecord")} />
                </SelectTrigger>
                <SelectContent>
                  {sourceOptions.map((item) => (
                    <SelectItem
                      key={`${item.entityType}:${item.recordId}`}
                      value={`${item.entityType}:${item.recordId}`}
                    >
                      {item.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          ) : null}
          <div>
            <Label htmlFor="project-document-category">
              {t("operationalProjects.documents.field.category")}
            </Label>
            <Select
              value={category}
              onValueChange={selectCategory}
              disabled={uploading || Boolean(sourceOptions?.length)}
            >
              <SelectTrigger
                id="project-document-category"
                className="mt-1 min-w-0 [&>span]:truncate"
              >
                <SelectValue placeholder={t("operationalProjects.documents.selectCategory")} />
              </SelectTrigger>
              <SelectContent>
                {categories.map((item) => (
                  <SelectItem key={item.value} value={item.value}>
                    {t(item.translationKey)} · {item.folderPath}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <Button onClick={() => void upload()} disabled={uploading || files.length === 0}>
            {uploading ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Upload className="mr-2 h-4 w-4" />
            )}
            {t("operationalProjects.documents.upload")}
          </Button>
        </div>
      )}

      {syncError ? (
        <p role="status" className="break-words text-sm text-destructive">
          {t("operationalProjects.documents.loadError")}: {uiError(syncError)}
        </p>
      ) : null}
      {loading ? (
        <div className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          {t("operationalProjects.documents.loading")}
        </div>
      ) : error ? (
        <div className="rounded-md border border-destructive/30 bg-destructive/5 p-4">
          <p className="text-sm text-destructive">
            {t("operationalProjects.documents.loadError")}: {error}
          </p>
          <Button className="mt-3" size="sm" variant="outline" onClick={() => void load()}>
            <RefreshCcw className="mr-2 h-4 w-4" />
            {t("common.retry")}
          </Button>
        </div>
      ) : documents.length === 0 ? (
        <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
          {t("operationalProjects.documents.empty")}
        </div>
      ) : (
        <div data-testid="project-documents-list">
          {documents.some(isLostOnDrive) && (
            <div
              role="alert"
              className="mb-3 flex items-start gap-2 rounded-md bg-destructive/5 p-3"
            >
              <AlertTriangle
                aria-hidden="true"
                className="mt-0.5 h-4 w-4 shrink-0 text-destructive"
              />
              <p className="text-sm font-medium text-foreground">
                {t("operationalProjects.documents.driveProblem.summary", {
                  count: documents.filter(isLostOnDrive).length,
                })}
              </p>
            </div>
          )}
          <div className="space-y-3 xl:hidden">
            {documents.map((document) => (
              <article key={document.id} className="space-y-3 rounded-md border p-4">
                <div className="flex items-start gap-2">
                  <FileText className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
                  <div className="min-w-0">
                    <h3 className="font-medium [overflow-wrap:anywhere]">
                      {document.originalFileName}
                    </h3>
                    {fileMeta(document)}
                  </div>
                </div>
                <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                  {statusBadge(document)}
                  <span>{categoryLabel(document.category)}</span>
                </div>
                {notice(document)}
                {document.category === "Unclassified" && canManage && (
                  <div className="flex flex-col gap-2 sm:flex-row">
                    <Select
                      value={classifications[document.id] ?? ""}
                      onValueChange={(value) =>
                        setClassifications((current) => ({ ...current, [document.id]: value }))
                      }
                    >
                      <SelectTrigger
                        aria-label={t("operationalProjects.documents.classifyAria", {
                          name: document.originalFileName,
                        })}
                      >
                        <SelectValue
                          placeholder={t("operationalProjects.documents.selectCategory")}
                        />
                      </SelectTrigger>
                      <SelectContent>
                        {categories.map((item) => (
                          <SelectItem key={item.value} value={item.value}>
                            {t(item.translationKey)}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <Button
                      variant="outline"
                      onClick={() => void mutate(document, "classify")}
                      disabled={busyId === document.id}
                    >
                      {t("operationalProjects.documents.classify")}
                    </Button>
                  </div>
                )}
                {actions(document, "outline")}
              </article>
            ))}
          </div>
          <div className="hidden overflow-x-auto xl:block">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-muted-foreground">
                  <th className="p-3">{t("operationalProjects.documents.field.fileName")}</th>
                  <th className="p-3">{t("operationalProjects.documents.field.category")}</th>
                  <th className="p-3">{t("operationalProjects.documents.field.status")}</th>
                  <th className="p-3">{t("operationalProjects.documents.field.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {documents.map((document) => (
                  <Fragment key={document.id}>
                    <tr
                      className={cn(
                        "align-top",
                        !hasNotice(document) && !document.unsupportedReason && "border-b",
                      )}
                    >
                      <td className="min-w-56 p-3 font-medium [overflow-wrap:anywhere]">
                        {document.originalFileName}
                        {fileMeta(document)}
                      </td>
                      <td className="p-3">
                        {document.category === "Unclassified" && canManage ? (
                          <div className="min-w-48 space-y-2">
                            <Select
                              value={classifications[document.id] ?? ""}
                              onValueChange={(value) =>
                                setClassifications((current) => ({
                                  ...current,
                                  [document.id]: value,
                                }))
                              }
                            >
                              <SelectTrigger
                                aria-label={t("operationalProjects.documents.classifyAria", {
                                  name: document.originalFileName,
                                })}
                              >
                                <SelectValue
                                  placeholder={t("operationalProjects.documents.selectCategory")}
                                />
                              </SelectTrigger>
                              <SelectContent>
                                {categories.map((item) => (
                                  <SelectItem key={item.value} value={item.value}>
                                    {t(item.translationKey)}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => void mutate(document, "classify")}
                              disabled={busyId === document.id}
                            >
                              {t("operationalProjects.documents.classify")}
                            </Button>
                          </div>
                        ) : (
                          categoryLabel(document.category)
                        )}
                      </td>
                      <td className="p-3">{statusBadge(document)}</td>
                      <td className="w-px whitespace-nowrap p-2">{actions(document, "ghost")}</td>
                    </tr>
                    {(hasNotice(document) || document.unsupportedReason) && (
                      <tr className="border-b">
                        <td colSpan={4} className="space-y-2 px-3 pb-3">
                          {notice(document)}
                        </td>
                      </tr>
                    )}
                  </Fragment>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
