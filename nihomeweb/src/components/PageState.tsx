import { AlertCircle, Inbox, Loader2 } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { Button } from "@/components/ui/button";

export function PageLoading({ label }: { label?: string } = {}) {
  const { t } = useI18n();
  return (
    <div
      className="flex min-h-40 flex-col items-center justify-center gap-3 py-12 text-center text-sm text-muted-foreground"
      role="status"
      aria-live="polite"
    >
      <Loader2 className="h-7 w-7 animate-spin text-primary" aria-hidden />
      <span>{label ?? t("common.loading")}</span>
    </div>
  );
}

export function PageError({ message, onRetry }: { message: string; onRetry?: () => void }) {
  const { t } = useI18n();
  return (
    <div className="flex min-h-40 flex-col items-center justify-center gap-3 rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-10 text-center" role="alert">
      <span className="rounded-full bg-destructive/10 p-2.5 text-destructive">
        <AlertCircle className="h-5 w-5" aria-hidden />
      </span>
      <p className="max-w-xl text-sm font-medium text-destructive">{message}</p>
      {onRetry && (
        <Button type="button" variant="outline" size="sm" onClick={onRetry}>
          {t("common.retry")}
        </Button>
      )}
    </div>
  );
}

export function PageEmpty({ message }: { message: string }) {
  return (
    <div className="flex min-h-40 flex-col items-center justify-center gap-3 rounded-lg border border-dashed bg-muted/20 px-4 py-10 text-center text-sm text-muted-foreground">
      <span className="rounded-full bg-muted p-2.5">
        <Inbox className="h-5 w-5" aria-hidden />
      </span>
      <p>{message}</p>
    </div>
  );
}
