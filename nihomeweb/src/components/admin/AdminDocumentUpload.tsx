import { useId, useRef, useState } from "react";
import { Loader2, UploadCloud } from "lucide-react";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";

export const BUSINESS_DOCUMENT_ACCEPT = ".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg";

type AdminDocumentUploadProps = {
  uploadFile: (file: File) => Promise<string>;
  onUploaded: (paths: string[]) => void | Promise<void>;
  multiple?: boolean;
  maxFiles?: number;
  disabled?: boolean;
  testId?: string;
};

export default function AdminDocumentUpload({
  uploadFile,
  onUploaded,
  multiple = false,
  maxFiles = multiple ? 20 : 1,
  disabled = false,
  testId,
}: AdminDocumentUploadProps) {
  const { t } = useI18n();
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inputId = useId();
  const isDisabled = disabled || uploading || maxFiles < 1;

  const uploadFiles = async (selected: File[]) => {
    if (selected.length === 0) return;
    setError(null);
    if (selected.length > maxFiles) {
      setError(t("common.documentUpload.maxFiles").replace("{count}", String(maxFiles)));
      if (inputRef.current) inputRef.current.value = "";
      return;
    }

    setUploading(true);
    const uploadedPaths: string[] = [];
    const failedFiles: string[] = [];
    try {
      for (const file of selected) {
        try {
          uploadedPaths.push(await uploadFile(file));
        } catch (uploadError) {
          failedFiles.push(
            `${file.name}: ${extractApiError(uploadError) || t("common.documentUpload.error")}`,
          );
        }
      }
      if (uploadedPaths.length > 0) await onUploaded(uploadedPaths);
      if (failedFiles.length > 0) setError(failedFiles.join("; "));
    } finally {
      setUploading(false);
      if (inputRef.current) inputRef.current.value = "";
    }
  };

  return (
    <div className="space-y-2">
      <label
        htmlFor={inputId}
        onDragOver={(event) => event.preventDefault()}
        onDrop={(event) => {
          event.preventDefault();
          if (isDisabled || inputRef.current?.matches(":disabled")) return;
          const files = Array.from(event.dataTransfer.files ?? []);
          if (files.length > 0) void uploadFiles(files);
        }}
        className={cn(
          "flex cursor-pointer flex-col items-center justify-center gap-1 rounded-lg border border-dashed border-border bg-muted/30 px-4 py-5 text-center transition-colors focus-within:border-primary focus-within:ring-2 focus-within:ring-ring/40",
          isDisabled
            ? "cursor-not-allowed opacity-50"
            : "hover:border-primary/60 hover:bg-muted/60",
        )}
      >
        <input
          ref={inputRef}
          id={inputId}
          type="file"
          accept={BUSINESS_DOCUMENT_ACCEPT}
          multiple={multiple}
          disabled={isDisabled}
          onChange={(event) => {
            const files = Array.from(event.target.files ?? []);
            if (files.length > 0) void uploadFiles(files);
          }}
          className="sr-only"
          data-testid={testId}
        />
        {uploading ? (
          <Loader2 className="h-6 w-6 animate-spin text-primary" aria-hidden />
        ) : (
          <UploadCloud className="h-6 w-6 text-primary" aria-hidden />
        )}
        <span className="text-sm font-medium text-foreground">
          {uploading ? t("common.documentUpload.uploading") : t("common.documentUpload.select")}
        </span>
        <span className="text-xs text-muted-foreground">{t("common.documentUpload.help")}</span>
        <span className="text-xs text-muted-foreground">
          {t("common.documentUpload.maxFiles").replace("{count}", String(maxFiles))}
        </span>
      </label>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}
