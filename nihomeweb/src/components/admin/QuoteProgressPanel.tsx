import { Check } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import {
  getQuoteProgress,
  QUOTE_PROGRESS_STEPS,
  type QuoteProgressInput,
} from "@/lib/quoteProgress";

/** Shows which step of the quote flow the quotation is at, who acts next and what to press. */
export function QuoteProgressPanel(props: QuoteProgressInput) {
  const { t } = useI18n();
  const progress = getQuoteProgress(props);
  const currentIndex =
    progress.kind === "active"
      ? progress.index
      : progress.kind === "done"
        ? QUOTE_PROGRESS_STEPS.length
        : -1;
  const nextText =
    progress.kind === "active"
      ? t(`quotes.progress.next.${progress.step}`)
      : progress.kind === "done"
        ? t("quotes.progress.done")
        : t("quotes.progress.stopped", { status: t(`quotes.status.${props.status}`) });

  return (
    <section
      aria-label={t("quotes.progress.title")}
      className="mb-3 rounded-lg border bg-card p-3"
      data-testid="quote-progress"
    >
      {progress.kind === "active" && (
        <p className="mb-1 text-sm font-medium text-foreground sm:hidden">
          {t("quotes.progress.stepOf", {
            current: progress.index + 1,
            total: QUOTE_PROGRESS_STEPS.length,
            step: t(`quotes.progress.step.${progress.step}`),
            owner: t(`quotes.progress.owner.${progress.step}`),
          })}
        </p>
      )}
      {progress.kind !== "stopped" && (
        <ol className="hidden gap-2 overflow-x-auto pb-1 sm:flex">
          {QUOTE_PROGRESS_STEPS.map((step, index) => {
            const done = index < currentIndex;
            const current = index === currentIndex;
            return (
              <li
                key={step}
                aria-current={current ? "step" : undefined}
                className={cn(
                  "flex min-w-[8.5rem] flex-1 items-start gap-2 rounded-md border px-2 py-1.5 text-xs",
                  current && "border-primary bg-primary/5",
                  done && "border-success/40 bg-success-soft",
                )}
              >
                <span
                  className={cn(
                    "mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full border text-[11px] font-semibold",
                    current && "border-primary bg-primary text-primary-foreground",
                    done && "border-success-strong bg-success-strong text-white",
                  )}
                  aria-hidden
                >
                  {done ? <Check className="h-3 w-3" /> : index + 1}
                </span>
                <span className="min-w-0">
                  <span className="block font-medium text-foreground">
                    {t(`quotes.progress.step.${step}`)}
                  </span>
                  <span className="block text-muted-foreground">
                    {t(`quotes.progress.owner.${step}`)}
                  </span>
                </span>
              </li>
            );
          })}
        </ol>
      )}
      <p className={cn("text-sm text-foreground", progress.kind !== "stopped" && "mt-2")}>
        {nextText}
      </p>
    </section>
  );
}
