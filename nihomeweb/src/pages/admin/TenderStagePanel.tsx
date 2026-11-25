import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { CheckCircle2, Circle, CircleDot } from "lucide-react";
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
import { useI18n } from "@/lib/i18n";
import { personOption } from "@/lib/people";
import { formatDateTime } from "@/lib/displaySettings";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { useUiError } from "@/hooks/useUiError";
import { useAppSelector } from "@/store";
import {
  adminApi,
  TENDER_STAGES,
  type TenderResponse,
  type TenderStageApproverOptionsResponse,
} from "@/services/adminApi";

interface Props {
  tender: TenderResponse;
  canManage: boolean;
  onChanged: (tender: TenderResponse) => void;
}

/** Steps that finish on an approval; Submission ends with the bid, Result with Won/Lost. */
const APPROVAL_STAGES = TENDER_STAGES.slice(0, TENDER_STAGES.indexOf("Submission"));

const TenderStagePanel = ({ tender, canManage, onChanged }: Props) => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const uiError = useUiError();
  const currentUserId = useAppSelector((state) => state.auth.user?.userId);
  const [options, setOptions] = useState<TenderStageApproverOptionsResponse | null>(null);
  const [approverId, setApproverId] = useState("");
  const [note, setNote] = useState("");
  const [returnOpen, setReturnOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);

  const currentIndex = TENDER_STAGES.indexOf(tender.stage);
  const isClosed =
    tender.status === "Won" || tender.status === "Lost" || tender.status === "Cancelled";
  const needsApproval = !isClosed && APPROVAL_STAGES.includes(tender.stage);
  const pending = tender.stageApproverUserId != null;
  const canRequest = needsApproval && !pending && canManage;
  const isApprover = needsApproval && pending && tender.stageApproverUserId === currentUserId;

  useEffect(() => {
    if (!canRequest) return;
    let cancelled = false;
    setOptions(null);
    setApproverId("");
    adminApi
      .listTenderStageApprovers(tender.id)
      .then(({ data }) => {
        if (!cancelled) setOptions(data);
      })
      .catch((err) => {
        if (!cancelled)
          toast({ title: t("common.error"), description: uiError(err), variant: "destructive" });
      });
    return () => {
      cancelled = true;
    };
  }, [canRequest, tender.id, tender.stage, t, toast, uiError]);

  const run = async (action: () => Promise<{ data: TenderResponse }>, done: string) => {
    setBusy(true);
    try {
      const { data } = await action();
      onChanged(data);
      toast({ title: done });
      return true;
    } catch (err) {
      toast({ title: t("common.error"), description: uiError(err), variant: "destructive" });
      return false;
    } finally {
      setBusy(false);
    }
  };

  const stageLabel = (stage: string) => t(`tenders.stage.${stage}`);
  const roleLabel = options?.roleLabelKey ? t(options.roleLabelKey) : (options?.roleName ?? "");

  return (
    <section
      aria-labelledby="tender-stage-title"
      data-testid="tender-stage-panel"
      className="rounded-xl border border-border bg-card p-4 shadow-sm md:p-5"
    >
      <h2 id="tender-stage-title" className="text-base font-semibold text-foreground">
        {t("tenders.stage.title")}
      </h2>
      <ol className="mt-3 grid gap-2 sm:grid-cols-3 xl:grid-cols-6">
        {TENDER_STAGES.map((stage, index) => {
          const state = index < currentIndex ? "done" : index === currentIndex ? "current" : "next";
          const Icon = state === "done" ? CheckCircle2 : state === "current" ? CircleDot : Circle;
          return (
            <li
              key={stage}
              aria-current={state === "current" ? "step" : undefined}
              className={cn(
                "flex items-start gap-2 rounded-lg border p-2 text-sm",
                state === "current"
                  ? "border-primary bg-primary/5 font-semibold text-foreground"
                  : "border-border text-muted-foreground",
              )}
            >
              <Icon
                aria-hidden
                className={cn("mt-0.5 h-4 w-4 shrink-0", state === "done" && "text-emerald-700")}
              />
              <span className="min-w-0">
                <span className="block break-words">
                  {index + 1}. {stageLabel(stage)}
                </span>
                <span className="block text-xs font-normal">
                  {t(`tenders.stage.state.${state}`)}
                </span>
              </span>
            </li>
          );
        })}
      </ol>

      <div className="mt-4 space-y-3 text-sm">
        {tender.status === "Cancelled" ? (
          <p className="text-muted-foreground">{t("tenders.stage.hint.cancelled")}</p>
        ) : pending && needsApproval ? (
          <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-amber-900">
            <p data-testid="tender-stage-pending">
              {t("tenders.stage.pending", {
                approver: tender.stageApproverName ?? "—",
                requester: tender.stageRequestedByName ?? "—",
                at: tender.stageRequestedAt ? formatDateTime(tender.stageRequestedAt, lang) : "—",
              })}
            </p>
            {tender.stageRequestNote ? (
              <p className="mt-1 break-words">
                {t("tenders.stage.noteLabel")}: {tender.stageRequestNote}
              </p>
            ) : null}
            {isApprover ? (
              <div className="mt-3 flex flex-col gap-2 sm:flex-row">
                <Button
                  disabled={busy}
                  data-testid="tender-stage-approve"
                  onClick={() =>
                    void run(
                      () =>
                        adminApi.decideTenderStage(tender.id, "approve", {
                          rowVersion: tender.rowVersion,
                        }),
                      t("tenders.stage.approved"),
                    )
                  }
                >
                  {t("tenders.stage.approve", {
                    next: stageLabel(TENDER_STAGES[currentIndex + 1]),
                  })}
                </Button>
                <Button variant="outline" disabled={busy} onClick={() => setReturnOpen(true)}>
                  {t("tenders.stage.return")}
                </Button>
              </div>
            ) : null}
          </div>
        ) : canRequest ? (
          options == null ? (
            <p className="text-muted-foreground">{t("common.loading")}</p>
          ) : !options.roleCode ? (
            <p className="text-amber-900">
              {t("tenders.stage.notConfigured", { stage: stageLabel(tender.stage) })}
            </p>
          ) : options.candidates.length === 0 ? (
            <p className="text-amber-900">{t("tenders.stage.noApprover", { role: roleLabel })}</p>
          ) : (
            <div className="grid gap-3 md:grid-cols-2">
              <p className="text-muted-foreground md:col-span-2">
                {t("tenders.stage.requestHint", {
                  stage: stageLabel(tender.stage),
                  next: stageLabel(TENDER_STAGES[currentIndex + 1]),
                  role: roleLabel,
                })}
              </p>
              <div className="space-y-1">
                <Label htmlFor="tender-stage-approver">{t("tenders.stage.approver")}</Label>
                <SearchableSelect
                  id="tender-stage-approver"
                  value={approverId}
                  onChange={setApproverId}
                  required
                  placeholder={t("tenders.stage.approverPlaceholder")}
                  data-testid="tender-stage-approver"
                  options={options.candidates.map((person) => personOption(person, t))}
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="tender-stage-note">{t("tenders.stage.noteLabel")}</Label>
                <Textarea
                  id="tender-stage-note"
                  value={note}
                  maxLength={1000}
                  onChange={(event) => setNote(event.target.value)}
                />
              </div>
              <div className="md:col-span-2">
                <Button
                  className="w-full sm:w-auto"
                  disabled={busy || !approverId}
                  data-testid="tender-stage-request"
                  onClick={async () => {
                    const ok = await run(
                      () =>
                        adminApi.requestTenderStageApproval(tender.id, {
                          approverUserId: Number(approverId),
                          note: note.trim() || null,
                          rowVersion: tender.rowVersion,
                        }),
                      t("tenders.stage.requested"),
                    );
                    if (ok) setNote("");
                  }}
                >
                  {t("tenders.stage.request")}
                </Button>
                {!approverId ? (
                  <p className="mt-1 text-xs text-muted-foreground">
                    {t("tenders.stage.chooseApproverFirst")}
                  </p>
                ) : null}
              </div>
            </div>
          )
        ) : needsApproval ? (
          <p className="text-muted-foreground">{t("tenders.stage.hint.waitingForPreparer")}</p>
        ) : tender.stage === "Submission" && tender.status === "Preparing" ? (
          <p className="text-muted-foreground">{t("tenders.stage.hint.submission")}</p>
        ) : tender.status === "Won" ? (
          <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
            <p className="text-emerald-800">{t("tenders.stage.hint.won")}</p>
            {tender.wonOpportunityId && tender.operationalProjectId ? (
              <Button asChild className="w-full sm:w-auto">
                <Link
                  to={`/admin/contracts?opportunityId=${tender.wonOpportunityId}&operationalProjectId=${tender.operationalProjectId}&customerId=${tender.customerId}`}
                >
                  {t("tenders.detail.result.createContract")}
                </Link>
              </Button>
            ) : null}
          </div>
        ) : tender.status === "Lost" ? (
          <p className="text-muted-foreground">{t("tenders.stage.hint.lost")}</p>
        ) : (
          <p className="text-muted-foreground">{t("tenders.stage.hint.result")}</p>
        )}
      </div>

      <Dialog open={returnOpen} onOpenChange={(open) => !busy && setReturnOpen(open)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("tenders.stage.returnTitle")}</DialogTitle>
            <DialogDescription>
              {t("tenders.stage.returnDescription", { stage: stageLabel(tender.stage) })}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-1">
            <Label htmlFor="tender-stage-return-reason">{t("tenders.stage.returnReason")} *</Label>
            <Textarea
              id="tender-stage-return-reason"
              value={reason}
              maxLength={1000}
              required
              placeholder={t("tenders.stage.returnReasonExample")}
              onChange={(event) => setReason(event.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" disabled={busy} onClick={() => setReturnOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button
              disabled={busy || !reason.trim()}
              onClick={async () => {
                const ok = await run(
                  () =>
                    adminApi.decideTenderStage(tender.id, "return", {
                      note: reason.trim(),
                      rowVersion: tender.rowVersion,
                    }),
                  t("tenders.stage.returned"),
                );
                if (ok) {
                  setReturnOpen(false);
                  setReason("");
                }
              }}
            >
              {t("tenders.stage.returnConfirm")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </section>
  );
};

export default TenderStagePanel;
