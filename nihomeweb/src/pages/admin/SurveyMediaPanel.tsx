import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  AlertTriangle,
  Camera,
  CheckCircle2,
  Cloud,
  Download,
  ExternalLink,
  File,
  FileText,
  Image as ImageIcon,
  Loader2,
  LocateFixed,
  MapPin,
  RefreshCw,
  RotateCcw,
  Trash2,
  Upload,
  XCircle,
} from "lucide-react";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
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
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import {
  adminApi,
  type SurveyChecklistResultResponse,
  type SurveyChecklistStatus,
  type SurveyDriveConnectionStatusResponse,
  type SurveyMediaResponse,
  type SurveyMediaSyncStatus,
  type SurveyResponse,
  type SurveySyncLogResponse,
} from "@/services/adminApi";

const MAX_FILE_SIZE = 100 * 1024 * 1024;
const ALLOWED_EXTENSIONS = new Set(["jpg", "jpeg", "png", "heic", "mp4", "mov", "pdf", "dwg", "rvt"]);
const ACTIVE_SYNC_STATUSES = new Set<SurveyMediaSyncStatus>(["Pending", "Processing"]);

const SYNC_BADGE: Record<SurveyMediaSyncStatus, string> = {
  Pending: "border-slate-200 bg-slate-50 text-slate-700",
  Processing: "border-amber-200 bg-amber-50 text-amber-700",
  Synced: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Failed: "border-rose-200 bg-rose-50 text-rose-700",
};

const CHECKLIST_BADGE: Record<SurveyChecklistStatus, string> = {
  Ok: "border-emerald-200 bg-emerald-50 text-emerald-700",
  NeedsAttention: "border-amber-200 bg-amber-50 text-amber-700",
  Failed: "border-rose-200 bg-rose-50 text-rose-700",
};

const CONNECTION_BADGE: Record<SurveyDriveConnectionStatusResponse["status"], string> = {
  Connected: "border-emerald-200 bg-emerald-50 text-emerald-700",
  ReadOnly: "border-amber-200 bg-amber-50 text-amber-700",
  InvalidRoot: "border-rose-200 bg-rose-50 text-rose-700",
  Unavailable: "border-rose-200 bg-rose-50 text-rose-700",
};

type Props = {
  survey: SurveyResponse;
  canManage: boolean;
  onRefresh: () => Promise<void>;
  formatDateTime: (value?: string | null) => string;
};

type UploadDraft = {
  file: File | null;
  note: string;
  latitude: string;
  longitude: string;
};

const emptyUpload = (): UploadDraft => ({ file: null, note: "", latitude: "", longitude: "" });

const extensionOf = (fileName: string) => fileName.split(".").pop()?.toLowerCase() ?? "";

function MediaIcon({ media }: { media: SurveyMediaResponse }) {
  if (media.contentType.startsWith("image/")) return <ImageIcon className="h-6 w-6" />;
  if (media.contentType.startsWith("video/")) return <Camera className="h-6 w-6" />;
  if (media.contentType === "application/pdf") return <FileText className="h-6 w-6" />;
  return <File className="h-6 w-6" />;
}

function SurveyMediaThumbnail({ surveyId, media }: { surveyId: number; media: SurveyMediaResponse }) {
  const [url, setUrl] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (!media.contentType.startsWith("image/")) return;
    let active = true;
    let objectUrl: string | null = null;
    void adminApi.getSurveyMediaContent(surveyId, media.id)
      .then((response) => {
        if (!active) return;
        objectUrl = URL.createObjectURL(response.data);
        setUrl(objectUrl);
      })
      .catch(() => {
        if (active) setFailed(true);
      });
    return () => {
      active = false;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [media.contentType, media.id, surveyId]);

  if (!url || failed) {
    return <MediaIcon media={media} />;
  }
  return (
    <img
      src={url}
      alt={media.originalFileName}
      className="h-full w-full object-cover"
      onError={() => setFailed(true)}
    />
  );
}

export default function SurveyMediaPanel({ survey, canManage, onRefresh, formatDateTime }: Props) {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const cameraInputRef = useRef<HTMLInputElement>(null);
  const [upload, setUpload] = useState<UploadDraft>(emptyUpload);
  const [uploading, setUploading] = useState(false);
  const [locating, setLocating] = useState(false);
  const [deleting, setDeleting] = useState<SurveyMediaResponse | null>(null);
  const [busyMediaId, setBusyMediaId] = useState<number | null>(null);
  const [syncLog, setSyncLog] = useState<SurveySyncLogResponse[]>([]);
  const [syncLogLoading, setSyncLogLoading] = useState(false);
  const [driveConnection, setDriveConnection] = useState<SurveyDriveConnectionStatusResponse | null>(null);
  const [driveConnectionLoading, setDriveConnectionLoading] = useState(false);
  const [checklistSavingId, setChecklistSavingId] = useState<number | null>(null);
  const [checklistDrafts, setChecklistDrafts] = useState<Record<number, SurveyChecklistResultResponse>>({});
  const [exporting, setExporting] = useState(false);

  const hasActiveSync = survey.media.some((media) => ACTIVE_SYNC_STATUSES.has(media.syncStatus));

  const checkDriveConnection = useCallback(async () => {
    setDriveConnectionLoading(true);
    try {
      setDriveConnection((await adminApi.getSurveyDriveConnection()).data);
    } catch (error) {
      setDriveConnection({
        status: "Unavailable",
        syncMode: "PushOnly",
        error: extractApiError(error),
      });
    } finally {
      setDriveConnectionLoading(false);
    }
  }, []);

  useEffect(() => {
    void checkDriveConnection();
  }, [checkDriveConnection]);

  useEffect(() => {
    setChecklistDrafts(Object.fromEntries(survey.checklistResults.map((item) => [item.id, item])));
  }, [survey.checklistResults]);

  useEffect(() => {
    if (!hasActiveSync) return;
    const timer = window.setInterval(() => void onRefresh(), 10_000);
    return () => window.clearInterval(timer);
  }, [hasActiveSync, onRefresh]);

  const totalSize = useMemo(
    () => survey.media.reduce((total, media) => total + media.size, 0),
    [survey.media],
  );
  const formatSize = (bytes: number) => {
    if (bytes < 1024 * 1024) return `${new Intl.NumberFormat(lang, { maximumFractionDigits: 1 }).format(bytes / 1024)} KB`;
    return `${new Intl.NumberFormat(lang, { maximumFractionDigits: 1 }).format(bytes / 1024 / 1024)} MB`;
  };

  const selectFile = (file?: File) => {
    if (!file) return;
    if (!ALLOWED_EXTENSIONS.has(extensionOf(file.name))) {
      toast({ title: t("common.error"), description: t("surveys.media.validation.format"), variant: "destructive" });
      return;
    }
    if (file.size > MAX_FILE_SIZE) {
      toast({ title: t("common.error"), description: t("surveys.media.validation.size"), variant: "destructive" });
      return;
    }
    setUpload((current) => ({ ...current, file }));
  };

  const requestLocation = () => {
    if (!navigator.geolocation) {
      toast({ title: t("common.error"), description: t("surveys.media.locationUnavailable"), variant: "destructive" });
      return;
    }
    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      ({ coords }) => {
        setUpload((current) => ({
          ...current,
          latitude: coords.latitude.toFixed(6),
          longitude: coords.longitude.toFixed(6),
        }));
        setLocating(false);
      },
      () => {
        setLocating(false);
        toast({ title: t("common.error"), description: t("surveys.media.locationDenied"), variant: "destructive" });
      },
      { enableHighAccuracy: true, timeout: 10_000 },
    );
  };

  const submitUpload = async () => {
    if (!upload.file) {
      toast({ title: t("common.error"), description: t("surveys.media.validation.fileRequired"), variant: "destructive" });
      return;
    }
    const latitude = upload.latitude.trim() === "" ? undefined : Number(upload.latitude);
    const longitude = upload.longitude.trim() === "" ? undefined : Number(upload.longitude);
    if ((latitude == null) !== (longitude == null) || (latitude != null && (!Number.isFinite(latitude) || latitude < -90 || latitude > 90)) || (longitude != null && (!Number.isFinite(longitude) || longitude < -180 || longitude > 180))) {
      toast({ title: t("common.error"), description: t("surveys.media.validation.coordinates"), variant: "destructive" });
      return;
    }

    setUploading(true);
    try {
      await adminApi.uploadSurveyMedia(survey.id, {
        file: upload.file,
        note: upload.note.trim() || undefined,
        capturedAt: new Date(upload.file.lastModified).toISOString(),
        latitude,
        longitude,
      });
      setUpload(emptyUpload());
      if (fileInputRef.current) fileInputRef.current.value = "";
      if (cameraInputRef.current) cameraInputRef.current.value = "";
      toast({ title: t("surveys.media.uploaded") });
      await onRefresh();
    } catch (error) {
      toast({ title: t("common.error"), description: extractApiError(error), variant: "destructive" });
    } finally {
      setUploading(false);
    }
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    setBusyMediaId(deleting.id);
    try {
      await adminApi.deleteSurveyMedia(survey.id, deleting.id);
      toast({ title: t("surveys.media.deleted") });
      setDeleting(null);
      await onRefresh();
    } catch (error) {
      toast({ title: t("common.error"), description: extractApiError(error), variant: "destructive" });
    } finally {
      setBusyMediaId(null);
    }
  };

  const retrySync = async (media: SurveyMediaResponse) => {
    setBusyMediaId(media.id);
    try {
      await adminApi.retrySurveyMediaSync(survey.id, media.id);
      toast({ title: t("surveys.media.retryQueued") });
      await onRefresh();
    } catch (error) {
      toast({ title: t("common.error"), description: extractApiError(error), variant: "destructive" });
    } finally {
      setBusyMediaId(null);
    }
  };

  const loadSyncLog = async () => {
    setSyncLogLoading(true);
    try {
      setSyncLog((await adminApi.getSurveySyncLog(survey.id)).data);
    } catch (error) {
      toast({ title: t("common.error"), description: extractApiError(error), variant: "destructive" });
    } finally {
      setSyncLogLoading(false);
    }
  };

  const updateChecklistDraft = (id: number, update: Partial<SurveyChecklistResultResponse>) => {
    setChecklistDrafts((current) => ({ ...current, [id]: { ...current[id], ...update } }));
  };

  const saveChecklist = async (id: number) => {
    const draft = checklistDrafts[id];
    if (!draft?.status) {
      toast({ title: t("common.error"), description: t("surveys.checklist.validation.status"), variant: "destructive" });
      return;
    }
    setChecklistSavingId(id);
    try {
      await adminApi.updateSurveyChecklist(survey.id, id, {
        status: draft.status,
        note: draft.note?.trim() || null,
        sortOrder: draft.sortOrder,
      });
      toast({ title: t("surveys.checklist.saved") });
      await onRefresh();
    } catch (error) {
      toast({ title: t("common.error"), description: extractApiError(error), variant: "destructive" });
    } finally {
      setChecklistSavingId(null);
    }
  };

  const exportPdf = async () => {
    setExporting(true);
    try {
      const blob = (await adminApi.exportSurveyPdf(survey.id, lang)).data;
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `survey-${survey.code}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
      toast({ title: t("surveys.media.pdfExported") });
    } catch (error) {
      toast({ title: t("common.error"), description: extractApiError(error), variant: "destructive" });
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className="space-y-4" data-testid="survey-media-panel">
      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="font-semibold text-slate-900">{t("surveys.media.title")}</h2>
            <p className="mt-1 text-xs text-slate-500">
              {survey.media.length} · {formatSize(totalSize)} / 2 GB
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            {survey.driveFolderLink ? (
              <Button asChild size="sm" variant="outline">
                <a href={survey.driveFolderLink} target="_blank" rel="noreferrer">
                  <Cloud className="mr-1.5 h-4 w-4" />
                  {t("surveys.media.openDrive")}
                </a>
              </Button>
            ) : null}
            <Button size="sm" variant="outline" onClick={() => void exportPdf()} disabled={exporting}>
              {exporting ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : <Download className="mr-1.5 h-4 w-4" />}
              {t("surveys.media.exportPdf")}
            </Button>
          </div>
        </div>

        <div className="mt-4 rounded-lg border border-slate-200 bg-slate-50/70 p-3" data-testid="survey-drive-connection">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <p className="text-sm font-medium text-slate-900">{t("surveys.driveConnection.title")}</p>
                {driveConnection ? (
                  <Badge variant="outline" className={CONNECTION_BADGE[driveConnection.status]}>
                    {driveConnection.status === "Connected" ? <Cloud className="mr-1 h-3.5 w-3.5" /> : <AlertTriangle className="mr-1 h-3.5 w-3.5" />}
                    {t(`surveys.driveConnection.status.${driveConnection.status}`)}
                  </Badge>
                ) : null}
                <Badge variant="outline">{t("surveys.driveConnection.pushOnly")}</Badge>
              </div>
              <p className="mt-1 text-xs text-slate-600">
                {driveConnection ? t(`surveys.driveConnection.description.${driveConnection.status}`) : t("surveys.driveConnection.checking")}
              </p>
              {driveConnection?.accountEmail ? <p className="mt-1 break-all text-xs text-slate-500">{t("surveys.driveConnection.authenticatedAccount")}: {driveConnection.accountEmail}</p> : null}
              {driveConnection?.storageType ? <p className="mt-1 text-xs text-slate-500">{t("surveys.driveConnection.storageType")}: {t(`surveys.driveConnection.storage.${driveConnection.storageType}`)}</p> : null}
              {driveConnection?.rootFolderName ? <p className="mt-1 text-xs text-slate-500">{t("surveys.driveConnection.folder")}: {driveConnection.rootFolderName}</p> : null}
              {driveConnection?.error ? <p className="mt-2 rounded bg-rose-50 p-2 text-xs text-rose-700">{driveConnection.error}</p> : null}
            </div>
            <div className="flex flex-wrap gap-2">
              {driveConnection?.rootFolderLink ? (
                <Button asChild size="sm" variant="outline">
                  <a href={driveConnection.rootFolderLink} target="_blank" rel="noreferrer">
                    <ExternalLink className="mr-1.5 h-4 w-4" />{t("surveys.driveConnection.openRoot")}
                  </a>
                </Button>
              ) : null}
              <Button size="sm" variant="outline" onClick={() => void checkDriveConnection()} disabled={driveConnectionLoading}>
                {driveConnectionLoading ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-1.5 h-4 w-4" />}
                {t("surveys.driveConnection.check")}
              </Button>
            </div>
          </div>
        </div>

        {canManage ? (
          <div className="mt-4 rounded-lg border border-dashed border-slate-300 bg-slate-50/60 p-3">
            <div className="flex flex-wrap gap-2">
              <Button type="button" size="sm" variant="outline" onClick={() => fileInputRef.current?.click()} data-testid="survey-media-choose-file">
                <Upload className="mr-1.5 h-4 w-4" />
                {t("surveys.media.chooseFile")}
              </Button>
              <Button type="button" size="sm" variant="outline" onClick={() => cameraInputRef.current?.click()} data-testid="survey-media-camera">
                <Camera className="mr-1.5 h-4 w-4" />
                {t("surveys.media.takePhoto")}
              </Button>
              <Button type="button" size="sm" variant="outline" onClick={requestLocation} disabled={locating}>
                {locating ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : <LocateFixed className="mr-1.5 h-4 w-4" />}
                {t("surveys.media.useLocation")}
              </Button>
              <input ref={fileInputRef} type="file" className="hidden" accept=".jpg,.jpeg,.png,.heic,.mp4,.mov,.pdf,.dwg,.rvt" onChange={(event) => selectFile(event.target.files?.[0])} />
              <input ref={cameraInputRef} type="file" className="hidden" accept="image/*" capture="environment" onChange={(event) => selectFile(event.target.files?.[0])} />
            </div>
            {upload.file ? (
              <div className="mt-3 grid gap-3 lg:grid-cols-[minmax(0,1fr)_180px_180px_auto] lg:items-end">
                <div>
                  <p className="mb-1 truncate text-xs font-medium text-slate-700">{upload.file.name} · {formatSize(upload.file.size)}</p>
                  <Textarea value={upload.note} maxLength={2000} rows={2} placeholder={t("surveys.media.notePlaceholder")} onChange={(event) => setUpload((current) => ({ ...current, note: event.target.value }))} />
                </div>
                <Input inputMode="decimal" value={upload.latitude} placeholder={t("surveys.media.latitude")} aria-label={t("surveys.media.latitude")} onChange={(event) => setUpload((current) => ({ ...current, latitude: event.target.value }))} />
                <Input inputMode="decimal" value={upload.longitude} placeholder={t("surveys.media.longitude")} aria-label={t("surveys.media.longitude")} onChange={(event) => setUpload((current) => ({ ...current, longitude: event.target.value }))} />
                <Button onClick={() => void submitUpload()} disabled={uploading} data-testid="survey-media-upload">
                  {uploading ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : <Upload className="mr-1.5 h-4 w-4" />}
                  {t("surveys.media.upload")}
                </Button>
              </div>
            ) : (
              <p className="mt-2 text-xs text-slate-500">{t("surveys.media.uploadHint")}</p>
            )}
          </div>
        ) : null}

        {survey.media.length === 0 ? (
          <div className="mt-4 rounded-lg border border-dashed p-8 text-center text-sm text-slate-500">
            <ImageIcon className="mx-auto mb-2 h-7 w-7 text-slate-400" />
            {t("surveys.media.empty")}
          </div>
        ) : (
          <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {survey.media.map((media) => (
              <article key={media.id} className="overflow-hidden rounded-lg border border-slate-200 bg-white" data-testid="survey-media-card">
                <div className="flex h-36 items-center justify-center bg-slate-100 text-slate-400">
                  <SurveyMediaThumbnail surveyId={survey.id} media={media} />
                </div>
                <div className="p-3">
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium text-slate-900" title={media.originalFileName}>{media.originalFileName}</p>
                      <p className="text-xs text-slate-500">{formatSize(media.size)} · {formatDateTime(media.capturedAt ?? media.createdAt)}</p>
                    </div>
                    <Badge variant="outline" className={cn("shrink-0", SYNC_BADGE[media.syncStatus])}>
                      {media.syncStatus === "Processing" ? <Loader2 className="mr-1 h-3 w-3 animate-spin" /> : null}
                      {t(`surveys.media.syncStatus.${media.syncStatus}`)}
                    </Badge>
                  </div>
                  {media.note ? <p className="mt-2 line-clamp-2 text-xs text-slate-600">{media.note}</p> : null}
                  {media.latitude != null && media.longitude != null ? (
                    <a className="mt-2 inline-flex items-center gap-1 text-xs text-sky-700 hover:underline" href={`https://www.openstreetmap.org/?mlat=${media.latitude}&mlon=${media.longitude}#map=18/${media.latitude}/${media.longitude}`} target="_blank" rel="noreferrer">
                      <MapPin className="h-3 w-3" />
                      {t("surveys.media.viewMap")} <ExternalLink className="h-3 w-3" />
                    </a>
                  ) : null}
                  {media.syncError ? <p className="mt-2 rounded bg-rose-50 p-2 text-xs text-rose-700">{media.syncError}</p> : null}
                  <div className="mt-3 flex flex-wrap gap-1.5">
                    <AdminFilePreview url={media.contentUrl} fileName={media.originalFileName} contentType={media.contentType} fetchFile={async () => (await adminApi.getSurveyMediaContent(survey.id, media.id)).data} showLabel size="sm" label={t("surveys.media.preview")} />
                    {canManage && media.syncAttemptCount < media.maxSyncAttempts &&
                    ((media.syncStatus === "Pending" && media.syncAttemptCount > 0) || media.syncStatus === "Failed") ? (
                      <Button size="sm" variant="outline" onClick={() => void retrySync(media)} disabled={busyMediaId === media.id}>
                        <RotateCcw className="mr-1 h-3.5 w-3.5" />{t("surveys.media.retry")}
                      </Button>
                    ) : null}
                    {canManage ? (
                      <Button size="sm" variant="outline" className="text-rose-700 hover:text-rose-800" onClick={() => setDeleting(media)} disabled={busyMediaId === media.id}>
                        <Trash2 className="mr-1 h-3.5 w-3.5" />{t("surveys.media.delete")}
                      </Button>
                    ) : null}
                  </div>
                  <p className="mt-2 text-[11px] text-slate-400">{t("surveys.media.attempts")}: {media.syncAttemptCount}/{media.maxSyncAttempts}</p>
                </div>
              </article>
            ))}
          </div>
        )}
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm" data-testid="survey-checklist">
        <div className="flex items-center justify-between gap-2">
          <div>
            <h2 className="font-semibold text-slate-900">{t("surveys.checklist.title")}</h2>
            <p className="mt-1 text-xs text-slate-500">{t("surveys.checklist.hint")}</p>
          </div>
          <ClipboardStatus checklist={survey.checklistResults} />
        </div>
        {survey.checklistResults.length === 0 ? (
          <p className="mt-4 text-sm text-slate-500">{t("surveys.checklist.empty")}</p>
        ) : (
          <div className="mt-4 space-y-2">
            {survey.checklistResults.map((item) => {
              const draft = checklistDrafts[item.id] ?? item;
              return (
                <div key={item.id} className="grid gap-2 rounded-lg border border-slate-200 p-3 md:grid-cols-[minmax(160px,1fr)_180px_minmax(220px,1fr)_auto] md:items-center">
                  <p className="text-sm font-medium text-slate-800">
                    {localizedChecklistTitle(item, t)}
                  </p>
                  <Select value={draft.status ?? ""} disabled={!canManage} onValueChange={(value) => updateChecklistDraft(item.id, { status: value as SurveyChecklistStatus })}>
                    <SelectTrigger><SelectValue placeholder={t("surveys.checklist.selectStatus")} /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Ok">{t("surveys.checklist.status.Ok")}</SelectItem>
                      <SelectItem value="NeedsAttention">{t("surveys.checklist.status.NeedsAttention")}</SelectItem>
                      <SelectItem value="Failed">{t("surveys.checklist.status.Failed")}</SelectItem>
                    </SelectContent>
                  </Select>
                  <Input value={draft.note ?? ""} disabled={!canManage} maxLength={2000} placeholder={t("surveys.checklist.notePlaceholder")} onChange={(event) => updateChecklistDraft(item.id, { note: event.target.value })} />
                  {canManage ? (
                    <Button size="sm" onClick={() => void saveChecklist(item.id)} disabled={checklistSavingId === item.id}>
                      {checklistSavingId === item.id ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : null}{t("common.save")}
                    </Button>
                  ) : null}
                </div>
              );
            })}
          </div>
        )}
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm" data-testid="survey-sync-log">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h2 className="font-semibold text-slate-900">{t("surveys.syncLog.title")}</h2>
            <p className="mt-1 text-xs text-slate-500">{t("surveys.syncLog.hint")}</p>
          </div>
          <Button size="sm" variant="outline" onClick={() => void loadSyncLog()} disabled={syncLogLoading}>
            {syncLogLoading ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-1.5 h-4 w-4" />}
            {t("surveys.syncLog.refresh")}
          </Button>
        </div>
        {syncLog.length === 0 ? (
          <p className="mt-4 text-sm text-slate-500">{t("surveys.syncLog.empty")}</p>
        ) : (
          <div className="mt-3 overflow-x-auto">
            <table className="w-full min-w-[640px] text-left text-sm">
              <thead className="border-b text-xs uppercase text-slate-500"><tr><th className="py-2 pr-3">{t("surveys.syncLog.file")}</th><th className="py-2 pr-3">{t("surveys.syncLog.status")}</th><th className="py-2 pr-3">{t("surveys.syncLog.attempt")}</th><th className="py-2">{t("surveys.syncLog.lastAttempt")}</th></tr></thead>
              <tbody className="divide-y">
                {syncLog.map((entry) => <tr key={entry.mediaId}><td className="py-2 pr-3"><p className="max-w-xs truncate">{entry.fileName}</p>{entry.error ? <p className="max-w-md text-xs text-rose-600">{entry.error}</p> : null}</td><td className="py-2 pr-3">{t(`surveys.media.syncStatus.${entry.status}`)}</td><td className="py-2 pr-3">{entry.attemptCount}/{entry.maxAttempts}</td><td className="py-2">{formatDateTime(entry.lastAttemptAt)}</td></tr>)}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <AlertDialog open={Boolean(deleting)} onOpenChange={(open) => { if (!open) setDeleting(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("surveys.media.deleteTitle")}</AlertDialogTitle>
            <AlertDialogDescription>{t("surveys.media.deleteDescription")}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction className="bg-rose-600 hover:bg-rose-700" onClick={() => void confirmDelete()}>{t("surveys.media.delete")}</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

function ClipboardStatus({ checklist }: { checklist: SurveyChecklistResultResponse[] }) {
  const completed = checklist.filter((item) => item.status).length;
  const failed = checklist.filter((item) => item.status === "Failed").length;
  const needsAttention = checklist.filter((item) => item.status === "NeedsAttention").length;
  const status: SurveyChecklistStatus = failed > 0 ? "Failed" : needsAttention > 0 ? "NeedsAttention" : "Ok";
  const Icon = status === "Ok" ? CheckCircle2 : status === "Failed" ? XCircle : RefreshCw;
  return (
    <Badge variant="outline" className={cn("gap-1", CHECKLIST_BADGE[status])}>
      <Icon className="h-3.5 w-3.5" />{completed}/{checklist.length}
    </Badge>
  );
}

function localizedChecklistTitle(
  item: SurveyChecklistResultResponse,
  t: (key: string) => string,
) {
  const key = `masterData.survey_checklist_default.${item.templateCode}.label`;
  const translated = t(key);
  return translated === key ? item.templateTitle : translated;
}
