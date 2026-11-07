import { useFieldId } from "@/components/ui/field-scope";
import { useId, useMemo, useRef, useState } from "react";
import { FileText, UploadCloud, X } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";

const DEFAULT_MAX_FILES = 20;
const DEFAULT_MAX_SIZE_MB = 20;

const formatBytes = (bytes: number) => {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
};

const matchesAccept = (file: File, accept?: string) => {
  const rules = accept
    ?.split(",")
    .map((rule) => rule.trim().toLowerCase())
    .filter(Boolean);
  if (!rules?.length) return true;
  const name = file.name.toLowerCase();
  const type = file.type.toLowerCase();
  return rules.some((rule) => {
    if (rule.startsWith(".")) return name.endsWith(rule);
    if (rule.endsWith("/*")) return type.startsWith(rule.slice(0, -1));
    return type === rule;
  });
};

type MultiFilePickerProps = {
  files: File[];
  onChange: (files: File[]) => void;
  accept?: string;
  multiple?: boolean;
  maxFiles?: number;
  maxSizeMb?: number;
  disabled?: boolean;
  id?: string;
  testId?: string;
  hint?: string;
};

export function MultiFilePicker({
  files,
  onChange,
  accept,
  multiple = true,
  maxFiles = multiple ? DEFAULT_MAX_FILES : 1,
  maxSizeMb = DEFAULT_MAX_SIZE_MB,
  disabled = false,
  id,
  testId,
  hint,
}: MultiFilePickerProps) {
  const { t } = useI18n();
  const generatedId = useId();
  const inputId = useFieldId(id) ?? generatedId;
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const limitsHint = useMemo(() => {
    return t("common.filePicker.hint")
      .replace("{count}", String(maxFiles))
      .replace("{size}", String(maxSizeMb));
  }, [maxFiles, maxSizeMb, t]);

  const addFiles = (selected: File[]) => {
    if (selected.length === 0) return;
    setError(null);
    const failures: string[] = [];
    const valid: File[] = [];
    for (const file of selected) {
      if (!matchesAccept(file, accept)) {
        failures.push(t("common.filePicker.unsupported").replace("{name}", file.name));
      } else if (file.size > maxSizeMb * 1024 * 1024) {
        failures.push(
          t("common.filePicker.tooLarge")
            .replace("{name}", file.name)
            .replace("{size}", String(maxSizeMb)),
        );
      } else {
        valid.push(file);
      }
    }

    const merged = multiple ? [...files, ...valid] : valid.slice(0, 1);
    const unique = merged.filter(
      (file, index) =>
        merged.findIndex(
          (candidate) =>
            candidate.name === file.name &&
            candidate.size === file.size &&
            candidate.lastModified === file.lastModified,
        ) === index,
    );
    if (unique.length > maxFiles) {
      failures.push(t("common.filePicker.tooMany").replace("{count}", String(maxFiles)));
    }
    onChange(unique.slice(0, maxFiles));
    if (failures.length > 0) setError(failures.join(" "));
    if (inputRef.current) inputRef.current.value = "";
  };

  const removeAt = (index: number) => {
    setError(null);
    onChange(files.filter((_, current) => current !== index));
  };

  return (
    <div className="space-y-2">
      <label
        htmlFor={inputId}
        onDragOver={(event) => {
          event.preventDefault();
          if (!disabled && !inputRef.current?.matches(":disabled")) setDragOver(true);
        }}
        onDragLeave={() => setDragOver(false)}
        onDrop={(event) => {
          event.preventDefault();
          setDragOver(false);
          if (disabled || inputRef.current?.matches(":disabled")) return;
          addFiles(Array.from(event.dataTransfer.files ?? []));
        }}
        className={cn(
          "flex cursor-pointer flex-col items-center justify-center gap-1 rounded-lg border border-dashed px-4 py-5 text-center transition-colors focus-within:border-primary focus-within:ring-2 focus-within:ring-ring/40",
          dragOver
            ? "border-primary bg-primary/5"
            : "border-border bg-muted/30 hover:border-primary/60 hover:bg-muted/60",
          disabled && "cursor-not-allowed opacity-50",
        )}
      >
        <input
          ref={inputRef}
          id={inputId}
          type="file"
          accept={accept}
          multiple={multiple}
          disabled={disabled}
          className="sr-only"
          data-testid={testId}
          onChange={(event) => addFiles(Array.from(event.target.files ?? []))}
        />
        <UploadCloud className="h-6 w-6 text-primary" aria-hidden />
        <span className="text-sm font-medium text-foreground">
          {t("common.filePicker.dropHere")}
        </span>
        {hint ? <span className="text-xs text-muted-foreground">{hint}</span> : null}
        <span className="text-xs text-muted-foreground">{limitsHint}</span>
      </label>
      {files.length > 0 ? (
        <ul className="space-y-1.5">
          {files.map((file, index) => (
            <li
              key={`${file.name}-${file.lastModified}-${index}`}
              className="flex items-center gap-2 rounded-md border bg-background px-2.5 py-1.5 text-sm"
            >
              <FileText className="h-4 w-4 shrink-0 text-muted-foreground" aria-hidden />
              <span className="min-w-0 flex-1 break-all">{file.name}</span>
              <span className="shrink-0 text-xs tabular-nums text-muted-foreground">
                {formatBytes(file.size)}
              </span>
              <button
                type="button"
                disabled={disabled}
                onClick={() => removeAt(index)}
                aria-label={`${t("common.filePicker.remove")}: ${file.name}`}
                className="flex h-8 w-8 shrink-0 items-center justify-center rounded text-muted-foreground transition-colors hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:opacity-50"
              >
                <X className="h-4 w-4" />
              </button>
            </li>
          ))}
        </ul>
      ) : null}
      {error ? (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      ) : null}
    </div>
  );
}
