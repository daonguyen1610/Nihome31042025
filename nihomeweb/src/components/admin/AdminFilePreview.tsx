import { useEffect, useRef, useState } from "react";
import { Download, ExternalLink, Eye, FileWarning, Loader2 } from "lucide-react";
import { Button, type ButtonProps } from "@/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { resolveSafeLinkUrl } from "@/lib/url";

type PreviewKind = "image" | "pdf" | "text" | "unsupported";

type AdminFilePreviewProps = {
  url?: string | null;
  fileName?: string | null;
  contentType?: string | null;
  label?: string;
  showLabel?: boolean;
  variant?: ButtonProps["variant"];
  size?: ButtonProps["size"];
  className?: string;
  testId?: string;
  fetchFile?: () => Promise<Blob>;
};

const IMAGE_EXTENSIONS = new Set(["bmp", "gif", "jpeg", "jpg", "png", "svg", "webp"]);
const TEXT_EXTENSIONS = new Set(["csv", "log", "md", "txt"]);

const getExtension = (value: string) => {
  const path = value.split(/[?#]/, 1)[0];
  const file = path.split("/").pop() ?? "";
  const dot = file.lastIndexOf(".");
  return dot >= 0 ? file.slice(dot + 1).toLowerCase() : "";
};

const getPreviewKind = (url: string, fileName?: string | null, contentType?: string | null): PreviewKind => {
  const normalizedType = contentType?.toLowerCase().split(";", 1)[0].trim();
  if (normalizedType?.startsWith("image/")) return "image";
  if (normalizedType === "application/pdf") return "pdf";
  if (normalizedType === "text/plain" || normalizedType === "text/csv") return "text";

  const extension = getExtension(fileName ?? "") || getExtension(url);
  if (IMAGE_EXTENSIONS.has(extension)) return "image";
  if (extension === "pdf") return "pdf";
  if (TEXT_EXTENSIONS.has(extension)) return "text";
  return "unsupported";
};

const getDisplayName = (url: string, fileName?: string | null) => {
  if (fileName?.trim()) return fileName.trim();
  const path = url.split(/[?#]/, 1)[0];
  return decodeURIComponent(path.split("/").pop() || url);
};

export default function AdminFilePreview({
  url,
  fileName,
  contentType,
  label,
  showLabel = false,
  variant = "outline",
  size = showLabel ? "sm" : "icon",
  className,
  testId,
  fetchFile,
}: AdminFilePreviewProps) {
  const { t } = useI18n();
  const href = resolveSafeLinkUrl(url ?? "");
  const [open, setOpen] = useState(false);
  const [blobHref, setBlobHref] = useState<string | null>(null);
  const [fileLoading, setFileLoading] = useState(false);
  const [fileLoadFailed, setFileLoadFailed] = useState(false);
  const blobHrefRef = useRef<string | null>(null);
  const displayName = getDisplayName(url ?? "", fileName);
  const previewKind = href ? getPreviewKind(href, fileName, contentType) : "unsupported";
  const triggerLabel = label ?? t("common.previewFile");
  const effectiveHref = fetchFile ? blobHref : href;

  useEffect(() => () => {
    if (blobHrefRef.current) URL.revokeObjectURL(blobHrefRef.current);
  }, []);

  const handleOpenChange = async (nextOpen: boolean) => {
    setOpen(nextOpen);
    if (!nextOpen || !fetchFile || blobHrefRef.current || fileLoading) return;

    setFileLoading(true);
    setFileLoadFailed(false);
    try {
      const blob = await fetchFile();
      const objectUrl = URL.createObjectURL(blob);
      blobHrefRef.current = objectUrl;
      setBlobHref(objectUrl);
    } catch {
      setFileLoadFailed(true);
    } finally {
      setFileLoading(false);
    }
  };

  if (!href && !fetchFile) {
    return (
      <Button
        type="button"
        variant={variant}
        size={size}
        className={className}
        disabled
        title={t("common.invalidFileLink")}
        aria-label={t("common.invalidFileLink")}
        data-testid={testId}
      >
        <FileWarning className={cn("h-4 w-4 shrink-0", showLabel && "mr-1.5")} />
        {showLabel ? triggerLabel : null}
      </Button>
    );
  }

  return (
    <Dialog open={open} onOpenChange={(nextOpen) => void handleOpenChange(nextOpen)}>
      <DialogTrigger asChild>
        <Button
          type="button"
          variant={variant}
          size={size}
          className={className}
          title={triggerLabel}
          aria-label={`${triggerLabel}: ${displayName}`}
          data-testid={testId}
        >
          <Eye className={cn("h-4 w-4 shrink-0", showLabel && "mr-1.5")} />
          {showLabel ? triggerLabel : null}
        </Button>
      </DialogTrigger>
      <DialogContent
        className="flex max-h-[92vh] w-[96vw] max-w-5xl flex-col overflow-hidden sm:w-full"
        data-testid={testId ? `${testId}-dialog` : undefined}
      >
        <DialogHeader>
          <DialogTitle>{t("common.filePreviewTitle")}</DialogTitle>
          <DialogDescription className="break-all">{displayName}</DialogDescription>
        </DialogHeader>

        <div className="flex min-h-[50vh] flex-1 items-center justify-center overflow-auto rounded-md border bg-muted/30">
          {fileLoading ? (
            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          ) : fileLoadFailed || !effectiveHref ? (
            <div className="max-w-md p-8 text-center text-sm text-muted-foreground">
              <FileWarning className="mx-auto mb-3 h-10 w-10" />
              <p>{t("common.previewUnavailable")}</p>
            </div>
          ) : previewKind === "image" ? (
            <img
              src={effectiveHref}
              alt={displayName}
              className="max-h-[70vh] max-w-full object-contain"
              data-testid={testId ? `${testId}-image` : undefined}
            />
          ) : previewKind === "pdf" || previewKind === "text" ? (
            <iframe
              src={effectiveHref}
              title={`${t("common.filePreviewTitle")}: ${displayName}`}
              className="h-[70vh] w-full bg-background"
              referrerPolicy="no-referrer"
              data-testid={testId ? `${testId}-frame` : undefined}
            />
          ) : (
            <div className="max-w-md p-8 text-center text-sm text-muted-foreground">
              <FileWarning className="mx-auto mb-3 h-10 w-10" />
              <p>{t("common.previewUnavailable")}</p>
            </div>
          )}
        </div>

        <DialogFooter className="gap-2 sm:gap-0">
          <DialogClose asChild>
            <Button
              type="button"
              variant="outline"
              data-testid={testId ? `${testId}-close` : undefined}
            >
              {t("common.close")}
            </Button>
          </DialogClose>
          <Button asChild variant="outline" disabled={!effectiveHref}>
            <a href={effectiveHref ?? undefined} target="_blank" rel="noopener noreferrer">
              <ExternalLink className="mr-1.5 h-4 w-4" />
              {t("common.openInNewTab")}
            </a>
          </Button>
          <Button asChild disabled={!effectiveHref}>
            <a href={effectiveHref ?? undefined} download={fileName || true}>
              <Download className="mr-1.5 h-4 w-4" />
              {t("common.downloadFile")}
            </a>
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
