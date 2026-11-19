import { useRef } from "react";
import { FileUp, Loader2 } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { formatNumber } from "@/lib/displaySettings";
import { useToast } from "@/hooks/use-toast";
import { Button } from "@/components/ui/button";
import { DESIGN_FILE_ACCEPT, DESIGN_FILE_MAX_MB, designFileProblem } from "@/lib/designFileRules";

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
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const inputRef = useRef<HTMLInputElement | null>(null);

  const pick = (file: File) => {
    const problem = designFileProblem(file);
    if (!problem) {
      onFile(file);
      return;
    }
    toast({
      variant: "destructive",
      title: t("designFiles.refusedTitle"),
      description:
        problem.code === "tooLarge"
          ? t("designFiles.tooLarge", {
              name: file.name,
              size: formatNumber(problem.sizeMb, lang, { maximumFractionDigits: 1 }),
              max: DESIGN_FILE_MAX_MB,
            })
          : problem.code === "empty"
            ? t("designFiles.empty", { name: file.name })
            : t("designFiles.unsupported", { name: file.name }),
    });
  };

  return (
    <>
      <Button
        type="button"
        variant="outline"
        size="sm"
        className="gap-1 text-xs"
        disabled={busy}
        title={t("designFiles.rule", { max: DESIGN_FILE_MAX_MB })}
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
        accept={DESIGN_FILE_ACCEPT}
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
