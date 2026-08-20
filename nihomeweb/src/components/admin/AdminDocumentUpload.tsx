import { useRef, useState } from "react";
import { Loader2, Upload } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";

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

  const handleFiles = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const selected = Array.from(event.target.files ?? []);
    if (selected.length === 0) return;
    setError(null);
    if (selected.length > maxFiles) {
      setError(t("common.documentUpload.maxFiles").replace("{count}", String(maxFiles)));
      event.target.value = "";
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
          failedFiles.push(`${file.name}: ${extractApiError(uploadError) || t("common.documentUpload.error")}`);
        }
      }
      if (uploadedPaths.length > 0) await onUploaded(uploadedPaths);
      if (failedFiles.length > 0) setError(failedFiles.join("; "));
    } finally {
      setUploading(false);
      if (inputRef.current) inputRef.current.value = "";
    }
  };

  const inputId = `business-document-${testId ?? "upload"}`;
  return (
    <div className="space-y-2">
      <Label htmlFor={inputId} className="sr-only">{t("common.documentUpload.select")}</Label>
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
        <Input
          ref={inputRef}
          id={inputId}
          type="file"
          accept={BUSINESS_DOCUMENT_ACCEPT}
          multiple={multiple}
          disabled={disabled || uploading || maxFiles < 1}
          onChange={(event) => void handleFiles(event)}
          className="min-w-0 flex-1"
          data-testid={testId}
        />
        <Button
          type="button"
          variant="outline"
          disabled={disabled || uploading || maxFiles < 1}
          onClick={() => inputRef.current?.click()}
          className="shrink-0"
        >
          {uploading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Upload className="mr-2 h-4 w-4" />}
          {uploading ? t("common.documentUpload.uploading") : t("common.documentUpload.select")}
        </Button>
      </div>
      <p className="text-xs text-muted-foreground">{t("common.documentUpload.help")}</p>
      {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    </div>
  );
}