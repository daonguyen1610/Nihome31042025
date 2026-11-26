import type { ReactNode } from "react";
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
    <div
      className="flex min-h-40 flex-col items-center justify-center gap-3 rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-10 text-center"
      role="alert"
    >
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

/** A list that is still loading keeps its shape, so the page does not jump when data lands. */
export function ListSkeleton() {
  const { t } = useI18n();
  return (
    <div className="space-y-2" role="status" aria-live="polite">
      <span className="sr-only">{t("common.loading")}</span>
      {[0, 1, 2, 3, 4].map((row) => (
        <div key={row} className="h-12 animate-pulse rounded-md bg-muted" aria-hidden />
      ))}
    </div>
  );
}

/** Non-blocking warning for data the page can live without, e.g. dropdown options. */
export function InlineWarning({ message, onRetry }: { message: string; onRetry?: () => void }) {
  const { t } = useI18n();
  return (
    <div
      className="flex flex-wrap items-center gap-2 rounded-md border border-warning/40 bg-warning-soft px-3 py-2 text-sm text-warning-strong"
      role="alert"
    >
      <AlertCircle className="h-4 w-4 shrink-0" aria-hidden />
      <span className="min-w-0 flex-1">{message}</span>
      {onRetry && (
        <Button type="button" variant="outline" size="sm" onClick={onRetry}>
          {t("common.retry")}
        </Button>
      )}
    </div>
  );
}

export function PageEmpty({ message, action }: { message: string; action?: ReactNode }) {
  return (
    <div className="flex min-h-40 flex-col items-center justify-center gap-3 rounded-lg border border-dashed bg-muted/20 px-4 py-10 text-center text-sm text-muted-foreground">
      <span className="rounded-full bg-muted p-2.5">
        <Inbox className="h-5 w-5" aria-hidden />
      </span>
      <p>{message}</p>
      {action}
    </div>
  );
}

/** Search or filters hide every record: never offer "create the first one" here. */
export function PageNoMatches({ onClear }: { onClear: () => void }) {
  const { t } = useI18n();
  return (
    <PageEmpty
      message={t("common.noResults")}
      action={
        <Button type="button" variant="outline" size="sm" onClick={onClear}>
          {t("common.clearFilters")}
        </Button>
      }
    />
  );
}

/**
 * A route that could not load. `stale` means its bundle file is gone after a
 * deploy, so reloading fetches the new version.
 */
export function PageLoadFailed({ stale }: { stale: boolean }) {
  const { t } = useI18n();
  return (
    <div className="p-4 sm:p-6">
      <PageError
        message={t(stale ? "app.newVersion.reload" : "app.pageLoadFailed")}
        onRetry={() => window.location.reload()}
      />
    </div>
  );
}
