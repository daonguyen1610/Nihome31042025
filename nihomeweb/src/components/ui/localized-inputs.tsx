import * as React from "react";
import { Upload } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { Input } from "@/components/ui/input";

type LocalizedFileInputProps = Omit<React.ComponentPropsWithoutRef<"input">, "type"> & {
  selectedFileName?: string | null;
};

const LocalizedFileInput = React.forwardRef<HTMLInputElement, LocalizedFileInputProps>(
  ({ className, id, selectedFileName, disabled, ...props }, ref) => {
    const { t } = useI18n();
    const generatedId = React.useId();
    const inputId = id ?? generatedId;

    return (
      <div className={cn("flex min-w-0 items-stretch", className)}>
        <input
          {...props}
          ref={ref}
          id={inputId}
          type="file"
          disabled={disabled}
          className="sr-only"
        />
        <label
          htmlFor={inputId}
          aria-disabled={disabled || undefined}
          className={cn(
            "inline-flex min-h-11 shrink-0 cursor-pointer items-center rounded-l-md border border-input bg-background px-3 text-sm font-medium hover:bg-accent focus-within:ring-2 focus-within:ring-ring",
            disabled && "pointer-events-none opacity-50",
          )}
        >
          <Upload className="mr-2 h-4 w-4" />
          {t("common.file.choose")}
        </label>
        <span className="flex min-w-0 flex-1 items-center rounded-r-md border border-l-0 border-input bg-muted/30 px-3 text-sm text-muted-foreground">
          <span className="truncate">{selectedFileName || t("common.file.noneSelected")}</span>
        </span>
      </div>
    );
  },
);
LocalizedFileInput.displayName = "LocalizedFileInput";

const localeByLanguage = {
  vi: "vi-VN",
  en: "en-US",
  zh: "zh-CN",
  ja: "ja-JP",
} as const;

const LocalizedDateInput = React.forwardRef<
  HTMLInputElement,
  Omit<React.ComponentPropsWithoutRef<typeof Input>, "type" | "lang">
>(({ className, ...props }, ref) => {
  const { lang } = useI18n();
  return (
    <Input {...props} ref={ref} type="date" lang={localeByLanguage[lang]} className={className} />
  );
});
LocalizedDateInput.displayName = "LocalizedDateInput";

export { LocalizedDateInput, LocalizedFileInput };
