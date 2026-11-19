import { useRef } from "react";
import { FileUp, Loader2 } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useBusinessFileCheck } from "@/hooks/useBusinessFileCheck";
import { Button } from "@/components/ui/button";

/**
 * Upload button for basic design documents and shop drawings. A file of the
 * wrong type or size is refused here, with the reason, before it uploads.
 */
export function DesignFileUploadButton({
  label,
  busy,
  onFile,
}: {
  label: string;
  busy: boolean;
  onFile: (file: File) => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const inputRef = useRef<HTMLInputElement | null>(null);
  const { accept, rule, problemFor } = useBusinessFileCheck("design");

  const pick = (file: File) => {
    const problem = problemFor(file);
    if (problem) {
      toast({ variant: "destructive", title: t("designFiles.refusedTitle"), description: problem });
      return;
    }
    onFile(file);
  };

  return (
    <>
      <Button
        type="button"
        variant="outline"
        size="sm"
        className="gap-1 text-xs"
        disabled={busy}
        title={rule}
        onClick={() => inputRef.current?.click()}
      >
        {busy ? (
          <Loader2 className="h-3.5 w-3.5 animate-spin" />
        ) : (
          <FileUp className="h-3.5 w-3.5" />
        )}
        {label}
      </Button>
      <input
        ref={inputRef}
        type="file"
        accept={accept}
        className="hidden"
        onChange={(event) => {
          const file = event.target.files?.[0];
          if (file) pick(file);
          event.target.value = "";
        }}
      />
    </>
  );
}
