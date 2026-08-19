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

type PreviewKind = "docx" | "image" | "pdf" | "text" | "unsupported";

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
const DOCX_CONTENT_TYPE = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
const DOCX_RENDER_OPTIONS = {
  breakPages: true,
  ignoreHeight: false,
  ignoreWidth: false,
  renderAltChunks: false,
  renderFooters: true,
  renderHeaders: true,
};

const sanitizeDocxLinks = (container: ParentNode) => {
  container.querySelectorAll<HTMLAnchorElement>("a[href]").forEach((link) => {
    const rawHref = link.getAttribute("href")?.trim() ?? "";
    if (rawHref.startsWith("#")) return;

    let isAllowed = false;
    try {
      const protocol = new URL(rawHref, window.location.origin).protocol;
      isAllowed = protocol === "http:" || protocol === "https:" || protocol === "mailto:";
    } catch {
      isAllowed = false;
    }
    if (isAllowed) return;

    link.removeAttribute("href");
    link.removeAttribute("target");
    link.removeAttribute("rel");
  });
};

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
  if (normalizedType === DOCX_CONTENT_TYPE) return "docx";

  const extension = getExtension(fileName ?? "") || getExtension(url);
  if (IMAGE_EXTENSIONS.has(extension)) return "image";
  if (extension === "pdf") return "pdf";
  if (TEXT_EXTENSIONS.has(extension)) return "text";
  if (extension === "docx") return "docx";
  return "unsupported";
};

const getDisplayName = (url: string, fileName?: string | null) => {
  if (fileName?.trim()) return fileName.trim();
  const path = url.split(/[?#]/, 1)[0];
  return decodeURIComponent(path.split("/").pop() || url);
};

function DocxPreview({ blob, testId }: { blob: Blob; testId?: string }) {
  const { t } = useI18n();
  const containerRef = useRef<HTMLDivElement>(null);
  const [rendering, setRendering] = useState(true);
  const [renderFailed, setRenderFailed] = useState(false);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    let cancelled = false;
    container.replaceChildren();
    setRendering(true);
    setRenderFailed(false);

    void import("docx-preview")
      .then(({ renderAsync }) => renderAsync(blob, container, container, DOCX_RENDER_OPTIONS))
      .then(() => {
        if (!cancelled) {
          sanitizeDocxLinks(container);
          setRendering(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setRendering(false);
          setRenderFailed(true);
        }
      });

    return () => {
      cancelled = true;
      container.replaceChildren();
    };
  }, [blob]);

  return (
    <div className="relative h-[70vh] w-full overflow-auto bg-muted/50 p-3 sm:p-6">
      {rendering ? (
        <div className="absolute inset-0 z-10 flex items-center justify-center">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      ) : null}
      {renderFailed ? (
        <div className="flex h-full items-center justify-center p-8 text-center text-sm text-muted-foreground">
          <div>
            <FileWarning className="mx-auto mb-3 h-10 w-10" />
            <p>{t("common.previewUnavailable")}</p>
          </div>
        </div>
      ) : (
        <div ref={containerRef} data-testid={testId ? `${testId}-docx` : undefined} />
      )}
    </div>
  );
}

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
  const [fileBlob, setFileBlob] = useState<Blob | null>(null);
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
    const shouldFetchDirectDocx = previewKind === "docx" && href && !fetchFile;
    if (!nextOpen || (!fetchFile && !shouldFetchDirectDocx) || blobHrefRef.current || fileLoading) return;

    setFileLoading(true);
    setFileLoadFailed(false);
    try {
      const blob = fetchFile
        ? await fetchFile()
        : await fetch(href, { credentials: "include" }).then((response) => {
          if (!response.ok) throw new Error(`HTTP ${response.status}`);
          return response.blob();
        });
      const objectUrl = URL.createObjectURL(blob);
      blobHrefRef.current = objectUrl;
      setFileBlob(blob);
      setBlobHref(objectUrl);
    } catch {
      setFileLoadFailed(true);
    } finally {
      setFileLoading(false);
    }
  };

  const handleOpenDocxInNewTab = () => {
    if (!fileBlob) return;

    const previewWindow = window.open("", "_blank");
    if (!previewWindow) return;

    previewWindow.opener = null;
    previewWindow.document.title = displayName;
    previewWindow.document.documentElement.lang = document.documentElement.lang;
    previewWindow.document.body.style.margin = "0";
    previewWindow.document.body.style.minHeight = "100vh";
    previewWindow.document.body.style.overflow = "auto";
    previewWindow.document.body.style.background = "#e5e7eb";

    void import("docx-preview")
      .then(({ renderAsync }) => renderAsync(
        fileBlob,
        previewWindow.document.body,
        previewWindow.document.head,
        DOCX_RENDER_OPTIONS,
      ))
      .then(() => sanitizeDocxLinks(previewWindow.document))
      .catch(() => {
        previewWindow.document.body.replaceChildren();
        const message = previewWindow.document.createElement("p");
        message.textContent = t("common.previewUnavailable");
        message.style.padding = "2rem";
        message.style.textAlign = "center";
        previewWindow.document.body.append(message);
      });
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
          ) : fileLoadFailed || !effectiveHref || (previewKind === "docx" && !fileBlob) ? (
            <div className="max-w-md p-8 text-center text-sm text-muted-foreground">
              <FileWarning className="mx-auto mb-3 h-10 w-10" />
              <p>{t("common.previewUnavailable")}</p>
            </div>
          ) : previewKind === "docx" ? (
            <DocxPreview blob={fileBlob!} testId={testId} />
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
          {previewKind === "docx" ? (
            <Button
              type="button"
              variant="outline"
              disabled={!fileBlob}
              onClick={handleOpenDocxInNewTab}
            >
              <ExternalLink className="mr-1.5 h-4 w-4" />
              {t("common.openInNewTab")}
            </Button>
          ) : (
            <Button asChild variant="outline" disabled={!effectiveHref}>
              <a href={effectiveHref ?? undefined} target="_blank" rel="noopener noreferrer">
                <ExternalLink className="mr-1.5 h-4 w-4" />
                {t("common.openInNewTab")}
              </a>
            </Button>
          )}
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
