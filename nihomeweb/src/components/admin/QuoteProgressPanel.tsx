import { Check } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import {
  getQuoteProgress,
  QUOTE_PROGRESS_STEPS,
  type QuoteProgressInput,
} from "@/lib/quoteProgress";

/** Who decides the internal approval now: the current step of the quote's approval chain. */
export interface QuoteCurrentApproval {
  name: string;
  role: string;
  current: number;
  total: number;
  /** False when that person can no longer approve, so Sales takes the quote back. */
  available: boolean;
}

/** Shows which step of the quote flow the quotation is at, who acts next and what to press. */
export function QuoteProgressPanel({
  currentApproval,
  ...props
}: QuoteProgressInput & { currentApproval?: QuoteCurrentApproval }) {
  const { t } = useI18n();
  const progress = getQuoteProgress(props);
  // The internal approval follows the chain in Cấu hình → Luồng duyệt, so its owner is
  // the current step's role once sent, and "the workflow's approvers" before.
  const ownerOf = (step: string) =>
    step === "internal"
      ? (currentApproval?.role ?? t("quotes.progress.owner.internalChain"))
      : t(`quotes.progress.owner.${step}`);
  const currentIndex =
    progress.kind === "active"
      ? progress.index
      : progress.kind === "done"
        ? QUOTE_PROGRESS_STEPS.length
        : -1;
  const nextText =
    progress.kind === "active"
      ? progress.step === "internal" && currentApproval?.available === false
        ? t("quotes.progress.next.internalBlocked", { name: currentApproval.name })
        : progress.step === "internal"
          ? t("quotes.progress.next.internalStep", {
              name: currentApproval?.name ?? "—",
              role: ownerOf("internal"),
              current: currentApproval?.current ?? 1,
              total: currentApproval?.total ?? 1,
            })
          : t(
              progress.ready
                ? `quotes.progress.next.${progress.step}Ready`
                : `quotes.progress.next.${progress.step}`,
            )
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
            owner: ownerOf(progress.step),
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
                    "mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full border text-xs font-semibold",
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
                  <span className="block text-muted-foreground">{ownerOf(step)}</span>
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
