import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Download, ExternalLink, FileText, Loader2, RefreshCcw, RotateCcw, Trash2, Upload } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { useToast } from "@/hooks/use-toast";
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

const MAX_FILE_SIZE = 100 * 1024 * 1024;
const BLOCKED_EXTENSIONS = new Set(["exe", "dll", "com", "bat", "cmd", "sh", "ps1", "js", "html", "htm", "svg"]);

const statusClass: Record<ProjectDocumentSyncStatus, string> = {
  Pending: "border-slate-200 bg-slate-50 text-slate-700",
  Processing: "border-amber-200 bg-amber-50 text-amber-700",
  Synced: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Failed: "border-rose-200 bg-rose-50 text-rose-700",
  Deleted: "border-slate-200 bg-slate-50 text-slate-500",
  Conflict: "border-orange-200 bg-orange-50 text-orange-700",
};

type Props = {
  projectId: number;
  canManage: boolean;
  onCountChange?: (count: number) => void;
};

const extensionOf = (fileName: string) => fileName.split(".").pop()?.toLowerCase() ?? "";

export default function ProjectDocumentsPanel({ projectId, canManage, onCountChange }: Props) {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const uploadIdempotencyKeyRef = useRef<string | null>(null);
  const [documents, setDocuments] = useState<ProjectDocumentResponse[]>([]);
  const [categories, setCategories] = useState<ProjectDocumentCategoryResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [category, setCategory] = useState("");
  const [uploading, setUploading] = useState(false);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [classifications, setClassifications] = useState<Record<number, string>>({});

  const dateTimeFormat = useMemo(() => new Intl.DateTimeFormat(lang, {
    dateStyle: "medium",
    timeStyle: "short",
  }), [lang]);
  const formatDateTime = (value?: string | null) => value ? dateTimeFormat.format(new Date(value)) : "—";

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [documentResponse, categoryResponse] = await Promise.all([
        adminApi.listProjectDocuments(projectId),
        adminApi.listProjectDocumentCategories(),
      ]);
      const nextDocuments = documentResponse.data ?? [];
      setDocuments(nextDocuments);
      setCategories(categoryResponse.data ?? []);
      onCountChange?.(nextDocuments.length);
    } catch (reason) {
      setError(extractApiError(reason));
    } finally {
      setLoading(false);
    }
  }, [onCountChange, projectId]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => { uploadIdempotencyKeyRef.current = null; }, [projectId]);

  const selectFile = (selected?: File) => {
    if (!selected) {
      setFile(null);
      uploadIdempotencyKeyRef.current = null;
      return;
    }
    if (!selected.name.trim() || selected.size === 0) {
      toast({ title: t("common.error"), description: t("operationalProjects.documents.validation.fileRequired"), variant: "destructive" });
      return;
    }
    if (BLOCKED_EXTENSIONS.has(extensionOf(selected.name))) {
      toast({ title: t("common.error"), description: t("operationalProjects.documents.validation.extension"), variant: "destructive" });
      return;
    }
    if (selected.size > MAX_FILE_SIZE) {
      toast({ title: t("common.error"), description: t("operationalProjects.documents.validation.size"), variant: "destructive" });
      return;
    }
    setFile(selected);
    uploadIdempotencyKeyRef.current = null;
  };

  const selectCategory = (value: string) => {
    setCategory(value);
    uploadIdempotencyKeyRef.current = null;
  };

  const upload = async () => {
    if (!file) {
      toast({ title: t("common.error"), description: t("operationalProjects.documents.validation.fileRequired"), variant: "destructive" });
      return;
    }
    if (!category || !categories.some(item => item.value === category)) {
      toast({ title: t("common.error"), description: t("operationalProjects.documents.validation.categoryRequired"), variant: "destructive" });
      return;
    }
    setUploading(true);
    try {
      const idempotencyKey = uploadIdempotencyKeyRef.current ?? newIdempotencyKey();
      uploadIdempotencyKeyRef.current = idempotencyKey;
      await adminApi.uploadProjectDocument(projectId, file, category, idempotencyKey);
      setFile(null);
      setCategory("");
      uploadIdempotencyKeyRef.current = null;
      if (fileInputRef.current) fileInputRef.current.value = "";
      toast({ title: t("operationalProjects.documents.uploaded") });
      await load();
    } catch (reason) {
      toast({ title: t("common.error"), description: extractApiError(reason), variant: "destructive" });
    } finally {
      setUploading(false);
    }
  };

  const mutate = async (document: ProjectDocumentResponse, action: "retry" | "classify" | "resolve" | "delete") => {
    if (action === "delete" && !window.confirm(t("operationalProjects.documents.deleteConfirm", { name: document.originalFileName }))) return;
    if (action === "resolve" && !window.confirm(t("operationalProjects.documents.keepBothConfirm", { name: document.originalFileName }))) return;
    const selectedCategory = classifications[document.id];
    if (action === "classify" && !categories.some(item => item.value === selectedCategory)) {
      toast({ title: t("common.error"), description: t("operationalProjects.documents.validation.categoryRequired"), variant: "destructive" });
      return;
    }
    setBusyId(document.id);
    try {
      if (action === "retry") await adminApi.retryProjectDocument(projectId, document.id);
      if (action === "classify") await adminApi.classifyProjectDocument(projectId, document.id, selectedCategory);
      if (action === "resolve") await adminApi.resolveProjectDocumentKeepBoth(projectId, document.id);
      if (action === "delete") await adminApi.deleteProjectDocument(projectId, document.id);
      toast({ title: t(`operationalProjects.documents.${action}Success`) });
      await load();
    } catch (reason) {
      toast({ title: t("common.error"), description: extractApiError(reason), variant: "destructive" });
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
      toast({ title: t("common.error"), description: extractApiError(reason), variant: "destructive" });
    } finally {
      setBusyId(null);
    }
  };

  const canRetry = (document: ProjectDocumentResponse) =>
    document.syncAttemptCount < document.maxSyncAttempts &&
    (document.syncStatus === "Failed" ||
      document.syncStatus === "Pending" && Boolean(document.nextSyncAttemptAt));

  const categoryLabel = (value: string) => {
    if (value === "Unclassified") return t("operationalProjects.documents.category.Unclassified");
    const item = categories.find(option => option.value === value);
    return item ? t(item.translationKey) : value;
  };

  const documentDetails = (document: ProjectDocumentResponse) => (
    <dl className="grid gap-x-4 gap-y-2 text-sm sm:grid-cols-2">
      <div><dt className="text-xs text-muted-foreground">{t("operationalProjects.documents.field.category")}</dt><dd>{categoryLabel(document.category)}</dd></div>
      <div><dt className="text-xs text-muted-foreground">{t("operationalProjects.documents.field.sourceModule")}</dt><dd>{t(`operationalProjects.documents.sourceModule.${document.sourceModule}`)}</dd></div>
      <div><dt className="text-xs text-muted-foreground">{t("operationalProjects.documents.field.size")}</dt><dd>{formatFileSize(document.size)}</dd></div>
      <div><dt className="text-xs text-muted-foreground">{t("operationalProjects.documents.field.origin")}</dt><dd>{t(`operationalProjects.documents.origin.${document.origin}`)}</dd></div>
      <div><dt className="text-xs text-muted-foreground">{t("operationalProjects.documents.field.modifiedAt")}</dt><dd>{formatDateTime(document.driveModifiedAt ?? document.updatedAt)}</dd></div>
      <div><dt className="text-xs text-muted-foreground">{t("operationalProjects.documents.field.status")}</dt><dd><Badge variant="outline" className={statusClass[document.syncStatus]}>{t(`operationalProjects.documents.syncStatus.${document.syncStatus}`)}</Badge></dd></div>
    </dl>
  );

  const actions = (document: ProjectDocumentResponse) => (
    <div className="flex flex-wrap items-center gap-2" data-testid={`project-document-actions-${document.id}`}>
      {document.isDownloadable && <Button size="sm" variant="outline" onClick={() => void download(document)} disabled={busyId === document.id} aria-label={t("operationalProjects.documents.downloadAria", { name: document.originalFileName })}><Download className="mr-1.5 h-4 w-4" />{t("operationalProjects.documents.download")}</Button>}
      {document.driveWebViewLink && <Button size="sm" variant="outline" asChild><a href={document.driveWebViewLink} target="_blank" rel="noreferrer" aria-label={t("operationalProjects.documents.openDriveAria", { name: document.originalFileName })}><ExternalLink className="mr-1.5 h-4 w-4" />{t("operationalProjects.documents.openDrive")}</a></Button>}
      {canManage && canRetry(document) && <Button size="sm" variant="outline" onClick={() => void mutate(document, "retry")} disabled={busyId === document.id} aria-label={t("operationalProjects.documents.retryAria", { name: document.originalFileName })}><RotateCcw className="mr-1.5 h-4 w-4" />{t("operationalProjects.documents.retry")}</Button>}
      {canManage && document.conflictState === "PendingConfirmation" && <Button size="sm" variant="outline" onClick={() => void mutate(document, "resolve")} disabled={busyId === document.id}>{t("operationalProjects.documents.keepBoth")}</Button>}
      {canManage && document.sourceType !== "ExistingManagedFile" && <Button size="sm" variant="destructive" onClick={() => void mutate(document, "delete")} disabled={busyId === document.id} aria-label={t("operationalProjects.documents.deleteAria", { name: document.originalFileName })}><Trash2 className="mr-1.5 h-4 w-4" />{t("common.delete")}</Button>}
    </div>
  );

  return (
    <div className="space-y-4" data-testid="project-documents-section">
      <p className="text-sm text-muted-foreground">{t("operationalProjects.documents.description")}</p>
      {canManage && <div className="grid gap-3 rounded-md border bg-muted/30 p-4 md:grid-cols-[minmax(0,1fr)_minmax(13rem,0.65fr)_auto] md:items-end" data-testid="project-documents-upload">
        <div><Label htmlFor="project-document-file">{t("operationalProjects.documents.file")}</Label><Input ref={fileInputRef} id="project-document-file" type="file" className="mt-1" onChange={event => selectFile(event.target.files?.[0])} /></div>
        <div><Label htmlFor="project-document-category">{t("operationalProjects.documents.field.category")}</Label><Select value={category} onValueChange={selectCategory}><SelectTrigger id="project-document-category" className="mt-1"><SelectValue placeholder={t("operationalProjects.documents.selectCategory")} /></SelectTrigger><SelectContent>{categories.map(item => <SelectItem key={item.value} value={item.value}>{t(item.translationKey)} · {item.folderPath}</SelectItem>)}</SelectContent></Select></div>
        <Button onClick={() => void upload()} disabled={uploading}>{uploading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Upload className="mr-2 h-4 w-4" />}{t("operationalProjects.documents.upload")}</Button>
      </div>}

      {loading ? <div className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" />{t("operationalProjects.documents.loading")}</div> : error ? <div className="rounded-md border border-destructive/30 bg-destructive/5 p-4"><p className="text-sm text-destructive">{t("operationalProjects.documents.loadError")}: {error}</p><Button className="mt-3" size="sm" variant="outline" onClick={() => void load()}><RefreshCcw className="mr-2 h-4 w-4" />{t("common.retry")}</Button></div> : documents.length === 0 ? <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">{t("operationalProjects.documents.empty")}</div> : <div data-testid="project-documents-list">
        <div className="space-y-3 lg:hidden">{documents.map(document => <article key={document.id} className="space-y-3 rounded-md border p-4"><div className="flex items-start gap-2"><FileText className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" /><h3 className="min-w-0 break-all font-medium">{document.originalFileName}</h3></div>{documentDetails(document)}{document.syncError && <p className="break-words rounded bg-destructive/5 p-2 text-xs text-destructive">{document.syncError}</p>}{document.unsupportedReason && <p className="text-xs text-muted-foreground">{document.unsupportedReason}</p>}{document.category === "Unclassified" && canManage && <div className="flex flex-col gap-2 sm:flex-row"><Select value={classifications[document.id] ?? ""} onValueChange={value => setClassifications(current => ({ ...current, [document.id]: value }))}><SelectTrigger aria-label={t("operationalProjects.documents.classifyAria", { name: document.originalFileName })}><SelectValue placeholder={t("operationalProjects.documents.selectCategory")} /></SelectTrigger><SelectContent>{categories.map(item => <SelectItem key={item.value} value={item.value}>{t(item.translationKey)}</SelectItem>)}</SelectContent></Select><Button variant="outline" onClick={() => void mutate(document, "classify")} disabled={busyId === document.id}>{t("operationalProjects.documents.classify")}</Button></div>}{actions(document)}</article>)}</div>
        <div className="hidden overflow-x-auto lg:block"><table className="w-full text-sm"><thead><tr className="border-b text-left text-muted-foreground"><th className="p-3">{t("operationalProjects.documents.field.fileName")}</th><th className="p-3">{t("operationalProjects.documents.field.category")}</th><th className="p-3">{t("operationalProjects.documents.field.sourceModule")}</th><th className="p-3">{t("operationalProjects.documents.field.size")}</th><th className="p-3">{t("operationalProjects.documents.field.status")}</th><th className="p-3">{t("operationalProjects.documents.field.origin")}</th><th className="p-3">{t("operationalProjects.documents.field.modifiedAt")}</th><th className="p-3">{t("operationalProjects.documents.field.actions")}</th></tr></thead><tbody>{documents.map(document => <tr key={document.id} className="border-b align-top"><td className="max-w-64 break-all p-3 font-medium">{document.originalFileName}{document.syncError && <p className="mt-1 break-words text-xs font-normal text-destructive">{document.syncError}</p>}{document.unsupportedReason && <p className="mt-1 text-xs font-normal text-muted-foreground">{document.unsupportedReason}</p>}</td><td className="p-3">{document.category === "Unclassified" && canManage ? <div className="min-w-48 space-y-2"><Select value={classifications[document.id] ?? ""} onValueChange={value => setClassifications(current => ({ ...current, [document.id]: value }))}><SelectTrigger aria-label={t("operationalProjects.documents.classifyAria", { name: document.originalFileName })}><SelectValue placeholder={t("operationalProjects.documents.selectCategory")} /></SelectTrigger><SelectContent>{categories.map(item => <SelectItem key={item.value} value={item.value}>{t(item.translationKey)}</SelectItem>)}</SelectContent></Select><Button size="sm" variant="outline" onClick={() => void mutate(document, "classify")} disabled={busyId === document.id}>{t("operationalProjects.documents.classify")}</Button></div> : categoryLabel(document.category)}</td><td className="p-3">{t(`operationalProjects.documents.sourceModule.${document.sourceModule}`)}</td><td className="p-3 whitespace-nowrap">{formatFileSize(document.size)}</td><td className="p-3"><Badge variant="outline" className={statusClass[document.syncStatus]}>{t(`operationalProjects.documents.syncStatus.${document.syncStatus}`)}</Badge></td><td className="p-3">{t(`operationalProjects.documents.origin.${document.origin}`)}</td><td className="p-3 whitespace-nowrap">{formatDateTime(document.driveModifiedAt ?? document.updatedAt)}</td><td className="min-w-72 p-3">{actions(document)}</td></tr>)}</tbody></table></div>
      </div>}
    </div>
  );
}
