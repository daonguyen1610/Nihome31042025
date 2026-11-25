import { useState } from "react";
import { Check, Loader2, X } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { personOption } from "@/lib/people";
import { useUiError } from "@/hooks/useUiError";
import { useToast } from "@/hooks/use-toast";
import { useConfirm } from "@/components/ui/confirmation";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { SearchableSelect } from "@/components/ui/searchable-select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  adminApi,
  type ContractResponse,
  type ContractReviewChain,
  type ContractStatus,
} from "@/services/adminApi";
import { formatDate } from "./contractDetailShared";

/** The usual contract flow (NICON M-04); On hold sits on In progress. */
const FLOW: ContractStatus[] = [
  "Draft",
  "UnderReview",
  "Approved",
  "Signed",
  "InProgress",
  "Completed",
];

interface Props {
  contract: ContractResponse;
  currentUserId?: number;
  /** May send the contract for review (crm.contracts.manage on this contract). */
  canSubmit: boolean;
  onChanged: (contract: ContractResponse) => void;
}

/** Contract steps, the internal review chain, and the action the viewer can take now. */
export function ContractReviewPanel({ contract, currentUserId, canSubmit, onChanged }: Props) {
  const { t } = useI18n();
  const uiError = useUiError();
  const { toast } = useToast();
  const confirmAction = useConfirm();
  const [chain, setChain] = useState<ContractReviewChain | null>(null);
  const [reviewers, setReviewers] = useState<number[]>([]);
  const [approvers, setApprovers] = useState<(number | null)[]>([]);
  const [rejecting, setRejecting] = useState(false);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const flowIndex = FLOW.indexOf(contract.status === "OnHold" ? "InProgress" : contract.status);
  const steps = contract.reviewSteps ?? [];
  const current = steps.find((step) => step.state === "Current");
  const myTurn = contract.status === "UnderReview" && current?.approverUserId === currentUserId;
  const stepLabel = (step: { roleCode: string; roleLabelKey?: string | null; name: string }) =>
    step.roleCode === ""
      ? t("contracts.review.taggedStep")
      : step.roleLabelKey
        ? t(step.roleLabelKey)
        : step.name;

  const run = async (action: () => Promise<{ data: ContractResponse }>, done: string) => {
    setBusy(true);
    setError("");
    try {
      const { data } = await action();
      onChanged(data);
      setChain(null);
      setRejecting(false);
      setReason("");
      toast({ title: t(done) });
    } catch (err) {
      setError(uiError(err));
    } finally {
      setBusy(false);
    }
  };

  const openSubmit = async () => {
    setError("");
    try {
      const { data } = await adminApi.getContractReviewChain(contract.id);
      setReviewers([]);
      // Auto-assign a step that only one person can decide; the sender picks otherwise.
      setApprovers(
        data.steps.map((step) =>
          !step.everyoneApproves && step.candidates.length === 1 ? step.candidates[0].id : null,
        ),
      );
      setChain(data);
    } catch (err) {
      toast({ variant: "destructive", title: uiError(err) });
    }
  };

  const nextText =
    contract.status === "Draft"
      ? t("contracts.review.next.draft")
      : contract.status === "UnderReview"
        ? t("contracts.review.next.underReview", {
            name: current?.approverName ?? "—",
            current: steps.findIndex((step) => step.state === "Current") + 1,
            total: steps.length,
          })
        : contract.status === "Approved"
          ? t("contracts.review.next.approved")
          : contract.status === "Cancelled"
            ? t("contracts.review.next.cancelled")
            : null;

  return (
    <section
      aria-label={t("contracts.review.flowTitle")}
      className="rounded-xl border border-border bg-card p-4 shadow-sm"
      data-testid="contract-review-panel"
    >
      <h2 className="text-sm font-semibold text-foreground">{t("contracts.review.flowTitle")}</h2>
      {flowIndex >= 0 && (
        <ol className="mt-3 flex flex-wrap gap-2">
          {FLOW.map((status, index) => {
            const done = index < flowIndex || (index === flowIndex && status === "Completed");
            const isCurrent = index === flowIndex && !done;
            return (
              <li
                key={status}
                aria-current={isCurrent ? "step" : undefined}
                className={cn(
                  "flex items-center gap-1.5 rounded-md border px-2 py-1 text-xs",
                  isCurrent && "border-primary bg-primary/5 font-medium",
                  done && "border-success-strong/30 bg-success-soft",
                )}
              >
                <span
                  aria-hidden
                  className={cn(
                    "flex h-5 w-5 items-center justify-center rounded-full border text-[11px]",
                    isCurrent && "border-primary bg-primary text-primary-foreground",
                    done && "border-success-strong bg-success-strong text-white",
                  )}
                >
                  {done ? <Check className="h-3 w-3" /> : index + 1}
                </span>
                {t(`contracts.status.${status}`)}
              </li>
            );
          })}
        </ol>
      )}

      {steps.length > 0 && (
        <ol className="mt-3 space-y-1 text-sm" data-testid="contract-review-steps">
          {steps.map((step, index) => (
            <li key={`${step.approverUserId}-${index}`} className="flex flex-wrap gap-x-2">
              <span className="font-medium">
                {index + 1}. {stepLabel(step)}
              </span>
              <span>{step.approverName}</span>
              <span className="text-muted-foreground">
                {step.state === "Approved"
                  ? t("contracts.review.state.approved", { date: formatDate(step.decidedAt) })
                  : step.state === "Current" && contract.status === "UnderReview"
                    ? t("contracts.review.state.current")
                    : t("contracts.review.state.waiting")}
              </span>
            </li>
          ))}
        </ol>
      )}

      {nextText && <p className="mt-3 text-sm text-foreground">{nextText}</p>}

      <div className="mt-3 flex flex-wrap gap-2">
        {contract.status === "Draft" && canSubmit && contract.type !== "Unclassified" && (
          <Button size="sm" onClick={() => void openSubmit()}>
            {t("contracts.review.submit")}
          </Button>
        )}
        {myTurn && (
          <>
            <Button
              size="sm"
              disabled={busy}
              onClick={async () => {
                if (!(await confirmAction(t("contracts.review.approveConfirm")))) return;
                await run(
                  () =>
                    adminApi.decideContractReview(contract.id, "approve", {
                      rowVersion: contract.rowVersion,
                    }),
                  "contracts.review.approved",
                );
              }}
            >
              {t("contracts.review.approve")}
            </Button>
            <Button
              size="sm"
              variant="outline"
              disabled={busy}
              onClick={() => {
                setError("");
                setRejecting(true);
              }}
            >
              {t("contracts.review.reject")}
            </Button>
          </>
        )}
      </div>
      {error && !chain && !rejecting && (
        <p role="alert" className="mt-2 text-sm text-destructive">
          {error}
        </p>
      )}

      <Dialog open={chain !== null} onOpenChange={(open) => !open && setChain(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("contracts.review.submit")}</DialogTitle>
            <DialogDescription>{t("contracts.review.submitHint")}</DialogDescription>
          </DialogHeader>
          {chain && (
            <div className="space-y-4">
              <div className="space-y-1.5">
                <Label htmlFor="contract-review-tag">{t("contracts.review.tagReviewers")}</Label>
                <SearchableSelect
                  id="contract-review-tag"
                  ariaLabel={t("contracts.review.tagReviewers")}
                  value={null}
                  onChange={(value) => setReviewers((prev) => [...prev, Number(value)])}
                  placeholder={t("contracts.review.tagPlaceholder")}
                  options={chain.reviewers
                    .filter((person) => !reviewers.includes(person.id))
                    .map((person) => personOption(person, t))}
                />
                {reviewers.length > 0 && (
                  <ul className="flex flex-wrap gap-2">
                    {reviewers.map((id) => {
                      const name = chain.reviewers.find((person) => person.id === id)?.name;
                      return (
                        <li
                          key={id}
                          className="flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs"
                        >
                          {name}
                          <button
                            type="button"
                            aria-label={t("contracts.review.untag", { name: name ?? "" })}
                            onClick={() => setReviewers((prev) => prev.filter((x) => x !== id))}
                          >
                            <X className="h-3 w-3" />
                          </button>
                        </li>
                      );
                    })}
                  </ul>
                )}
              </div>
              {chain.steps.map((step, index) => {
                const role = step.roleLabelKey ? t(step.roleLabelKey) : step.roleName;
                const title = t("contracts.review.stepTitle", { n: index + 1, role });
                return (
                  <div key={`${step.order}-${index}`} className="space-y-1.5">
                    {step.everyoneApproves ? (
                      <p className="text-sm font-medium">{title}</p>
                    ) : (
                      <Label htmlFor={`contract-review-step-${index}`}>{title}</Label>
                    )}
                    {step.everyoneApproves ? (
                      <p className="text-sm text-muted-foreground">
                        {t("contracts.review.everyoneInOrder", {
                          names: step.candidates.map((c) => c.name).join(", ") || "—",
                        })}
                      </p>
                    ) : (
                      <SearchableSelect
                        id={`contract-review-step-${index}`}
                        ariaLabel={title}
                        value={approvers[index]?.toString() ?? null}
                        onChange={(value) =>
                          setApprovers((prev) =>
                            prev.map((id, other) => (other === index ? Number(value) : id)),
                          )
                        }
                        placeholder={t("contracts.review.selectApprover")}
                        options={step.candidates.map((person) => personOption(person, t))}
                      />
                    )}
                    {step.candidates.length === 0 && (
                      <p role="alert" className="text-sm text-destructive">
                        {t("contracts.review.noCandidates", { role })}
                      </p>
                    )}
                  </div>
                );
              })}
              {error && (
                <p role="alert" className="text-sm text-destructive">
                  {error}
                </p>
              )}
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setChain(null)} disabled={busy}>
              {t("common.cancel")}
            </Button>
            <Button
              disabled={busy}
              onClick={() =>
                void run(
                  () =>
                    adminApi.submitContractReview(contract.id, {
                      reviewerUserIds: reviewers,
                      approverUserIds: approvers.filter((id): id is number => id != null),
                      rowVersion: contract.rowVersion,
                    }),
                  "contracts.review.submitted",
                )
              }
            >
              {busy && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}
              {t("contracts.review.submit")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={rejecting} onOpenChange={setRejecting}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("contracts.review.reject")}</DialogTitle>
            <DialogDescription>{t("contracts.review.rejectHint")}</DialogDescription>
          </DialogHeader>
          <Label htmlFor="contract-review-reason">{t("contracts.review.reason")}</Label>
          <Textarea
            id="contract-review-reason"
            value={reason}
            maxLength={1000}
            onChange={(event) => setReason(event.target.value)}
          />
          {error && (
            <p role="alert" className="text-sm text-destructive">
              {error}
            </p>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejecting(false)} disabled={busy}>
              {t("common.cancel")}
            </Button>
            <Button
              variant="destructive"
              disabled={busy || !reason.trim()}
              onClick={() =>
                void run(
                  () =>
                    adminApi.decideContractReview(contract.id, "reject", {
                      note: reason.trim(),
                      rowVersion: contract.rowVersion,
                    }),
                  "contracts.review.rejected",
                )
              }
            >
              {t("contracts.review.reject")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </section>
  );
}
