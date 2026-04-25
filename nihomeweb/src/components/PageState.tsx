import { Loader2 } from "lucide-react";
import { useI18n } from "@/lib/i18n";

export function PageLoading() {
  return (
    <div className="flex items-center justify-center py-32">
      <Loader2 className="w-8 h-8 animate-spin text-primary" />
    </div>
  );
}

export function PageError({ message, onRetry }: { message: string; onRetry?: () => void }) {
  const { t } = useI18n();
  return (
    <div className="flex flex-col items-center justify-center py-32 gap-4 text-center px-4">
      <p className="text-destructive font-semibold">{message}</p>
      {onRetry && (
        <button onClick={onRetry} className="btn-pill btn-gradient text-white px-6 py-2.5 text-xs uppercase tracking-wider">
          {t("common.retry")}
        </button>
      )}
    </div>
  );
}

export function PageEmpty({ message }: { message: string }) {
  return (
    <p className="text-center py-20 text-muted-foreground">{message}</p>
  );
}
