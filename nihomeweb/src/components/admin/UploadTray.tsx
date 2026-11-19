import { useEffect, useSyncExternalStore } from "react";
import { CheckCircle2, CircleSlash, Loader2, TriangleAlert, X } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { formatNumber } from "@/lib/displaySettings";
import { getUiError } from "@/lib/apiError";
import { uploadTracker, type UploadItem } from "@/lib/uploadTracker";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";

const toMb = (bytes: number) => bytes / 1024 / 1024;

function UploadRow({ item }: { item: UploadItem }) {
  const { t, lang } = useI18n();
  const percent = item.size > 0 ? Math.round((item.loaded / item.size) * 100) : 0;
  const mb = (bytes: number) => formatNumber(toMb(bytes), lang, { maximumFractionDigits: 1 });
  // Every byte is sent and the server may already be saving the record, so a
  // cancel could no longer undo the upload: it is not offered from here on.
  const sent = item.status === "uploading" && item.size > 0 && item.loaded >= item.size;

  return (
    <li className="space-y-1.5 py-2">
      <div className="flex items-start gap-2">
        {item.status === "uploading" ? (
          <Loader2
            className="mt-0.5 h-4 w-4 shrink-0 animate-spin text-muted-foreground"
            aria-hidden
          />
        ) : item.status === "done" ? (
          <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" aria-hidden />
        ) : item.status === "failed" ? (
          <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0 text-destructive" aria-hidden />
        ) : (
          <CircleSlash className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" aria-hidden />
        )}
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-medium text-foreground" title={item.name}>
            {item.name}
          </p>
          <p
            className={
              item.status === "failed"
                ? "text-xs text-destructive"
                : "text-xs tabular-nums text-muted-foreground"
            }
          >
            {sent
              ? t("upload.tray.saving", { total: mb(item.size) })
              : item.status === "uploading"
                ? t("upload.tray.progress", {
                    loaded: mb(item.loaded),
                    total: mb(item.size),
                    percent,
                  })
                : item.status === "done"
                  ? t("upload.tray.done", { total: mb(item.size) })
                  : item.status === "cancelled"
                    ? t("upload.cancelled")
                    : getUiError(item.error, t)}
          </p>
        </div>
        {item.status === "uploading" && !sent ? (
          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="h-8 w-8 shrink-0"
            aria-label={t("upload.tray.cancel", { name: item.name })}
            onClick={() => uploadTracker.cancel(item.id)}
          >
            <X className="h-4 w-4" aria-hidden />
          </Button>
        ) : null}
      </div>
      {item.status === "uploading" ? (
        <Progress
          value={percent}
          className="h-1.5"
          aria-label={t("upload.tray.progressLabel", { name: item.name })}
        />
      ) : null}
    </li>
  );
}

/**
 * Every file being uploaded, with its progress and a cancel. Leaving the page
 * while a file is still uploading asks for confirmation.
 */
export function UploadTray() {
  const { t } = useI18n();
  const items = useSyncExternalStore(uploadTracker.subscribe, uploadTracker.getSnapshot);
  const active = items.filter((item) => item.status === "uploading").length;

  useEffect(() => {
    if (active === 0) return;
    const warn = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [active]);

  if (items.length === 0) return null;
  return (
    <section
      aria-label={t("upload.tray.title")}
      className="fixed inset-x-4 bottom-4 z-50 mx-auto max-w-md rounded-lg border border-border bg-card shadow-lg"
      data-testid="upload-tray"
    >
      <header className="flex items-center justify-between gap-2 border-b border-border px-4 py-2">
        <h2 className="text-sm font-semibold text-foreground" aria-live="polite">
          {active > 0 ? t("upload.tray.uploading", { count: active }) : t("upload.tray.finished")}
        </h2>
        {active === 0 ? (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="h-8"
            onClick={() => uploadTracker.clearFinished()}
          >
            {t("upload.tray.clear")}
          </Button>
        ) : null}
      </header>
      <ul className="max-h-64 divide-y divide-border overflow-y-auto px-4">
        {items.map((item) => (
          <UploadRow key={item.id} item={item} />
        ))}
      </ul>
    </section>
  );
}
