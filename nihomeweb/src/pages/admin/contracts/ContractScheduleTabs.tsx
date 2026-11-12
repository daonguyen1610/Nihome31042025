import { isOverdue } from "./contractDetailShared";
import {
  ArrowDown,
  ArrowUp,
  CheckCircle2,
  Loader2,
  Pencil,
  Plus,
  Send,
  Trash2,
} from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { formatMoney } from "@/lib/displaySettings";
import { cn } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  type ContractResponse,
  type ContractDirection,
  type PaymentMilestoneStatus,
} from "@/services/adminApi";
import { formatDate, MILESTONE_STATUS_STYLES, MilestoneDraft } from "./contractDetailShared";

export interface ScheduleTabProps {
  contract: ContractResponse;
  canManage: boolean;
  onMilestoneStatus: (milestoneId: number, next: PaymentMilestoneStatus) => Promise<void>;
  onEditActualPaymentDate: (milestoneId: number, actualPaymentDate?: string | null) => void;
  busyMilestoneId: number | null;
}

export const ScheduleTab = ({
  contract,
  canManage,
  onMilestoneStatus,
  onEditActualPaymentDate,
  busyMilestoneId,
}: ScheduleTabProps) => {
  const { t } = useI18n();
  const milestones = contract.paymentMilestones;
  const upstream = contract.direction === "Upstream";
  const statusLabel = (status: PaymentMilestoneStatus) =>
    t(upstream ? `contracts.collectionStatus.${status}` : `contracts.milestoneStatus.${status}`);

  if (milestones.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-input p-6 text-center text-sm text-muted-foreground">
        {t("contracts.milestonesEmpty")}
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {milestones.map((m) => {
        const overdue = isOverdue(m.dueDate, m.status);
        const busy = busyMilestoneId === m.id;
        return (
          <div
            key={m.id}
            className={cn(
              "rounded-lg border bg-card p-4 shadow-sm",
              overdue ? "border-rose-300 ring-1 ring-rose-100" : "border-border",
            )}
          >
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-semibold text-muted-foreground">#{m.order}</span>
                  <span className="truncate text-base font-semibold text-foreground">{m.name}</span>
                </div>
                <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                  <span>
                    {m.percentValue}% • {formatMoney(m.amount, contract.currency)}
                  </span>
                  <span>
                    • {t("contracts.milestone.dueDate")}: {formatDate(m.dueDate)}
                  </span>
                  {m.status === "Paid" ? (
                    <span>
                      •{" "}
                      {t(
                        upstream
                          ? "contracts.collection.actualDate"
                          : "contracts.milestone.actualPaymentDate",
                      )}
                      : {formatDate(m.actualPaymentDate)}
                    </span>
                  ) : null}
                  <Badge className={cn("border", MILESTONE_STATUS_STYLES[m.status])}>
                    {statusLabel(m.status)}
                  </Badge>
                  {overdue ? (
                    <Badge className="border border-rose-300 bg-rose-100 text-rose-700">
                      {t("contracts.schedule.overdue")}
                    </Badge>
                  ) : null}
                </div>
              </div>
              {canManage ? (
                <div className="flex flex-wrap gap-2">
                  {m.status === "Pending" ? (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={busy}
                      onClick={() => onMilestoneStatus(m.id, "Requested")}
                    >
                      {busy ? (
                        <Loader2 className="mr-1 h-3 w-3 animate-spin" />
                      ) : (
                        <Send className="mr-1 h-3 w-3" />
                      )}
                      {t(
                        upstream
                          ? "contracts.collection.markRequested"
                          : "contracts.schedule.markRequested",
                      )}
                    </Button>
                  ) : null}
                  {m.status === "Pending" || m.status === "Requested" ? (
                    <Button size="sm" disabled={busy} onClick={() => onEditActualPaymentDate(m.id)}>
                      {busy ? (
                        <Loader2 className="mr-1 h-3 w-3 animate-spin" />
                      ) : (
                        <CheckCircle2 className="mr-1 h-3 w-3" />
                      )}
                      {t(
                        upstream
                          ? "contracts.collection.markCollected"
                          : "contracts.schedule.markPaid",
                      )}
                    </Button>
                  ) : null}
                  {m.status === "Paid" ? (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={busy}
                      onClick={() => onEditActualPaymentDate(m.id, m.actualPaymentDate)}
                    >
                      <Pencil className="mr-1 h-3 w-3" />
                      {t(
                        upstream
                          ? "contracts.collection.editActualDate"
                          : "contracts.schedule.editActualPaymentDate",
                      )}
                    </Button>
                  ) : null}
                  {m.status !== "Pending" ? (
                    <Button
                      size="sm"
                      variant="ghost"
                      disabled={busy}
                      onClick={() => onMilestoneStatus(m.id, "Pending")}
                    >
                      {t("contracts.schedule.revertPending")}
                    </Button>
                  ) : null}
                </div>
              ) : null}
            </div>
          </div>
        );
      })}
    </div>
  );
};

export interface EditScheduleTabProps {
  value: number;
  currency: string;
  direction: ContractDirection;
  milestones: MilestoneDraft[];
  onChange: (milestones: MilestoneDraft[]) => void;
}

export const EditScheduleTab = ({
  value,
  currency,
  direction,
  milestones,
  onChange,
}: EditScheduleTabProps) => {
  const { t } = useI18n();
  const total = milestones.reduce((sum, milestone) => sum + milestone.percentValue, 0);
  const totalValid = milestones.length === 0 || Math.abs(total - 100) <= 0.01;
  const patch = (index: number, next: Partial<MilestoneDraft>) =>
    onChange(
      milestones.map((milestone, current) =>
        current === index ? { ...milestone, ...next } : milestone,
      ),
    );
  const move = (index: number, direction: -1 | 1) => {
    const target = index + direction;
    if (target < 0 || target >= milestones.length) return;
    const next = [...milestones];
    [next[index], next[target]] = [next[target], next[index]];
    onChange(next.map((milestone, current) => ({ ...milestone, order: current + 1 })));
  };

  return (
    <div className="space-y-3">
      <div className="flex justify-end">
        <Button
          type="button"
          size="sm"
          variant="outline"
          onClick={() =>
            onChange([
              ...milestones,
              {
                order: milestones.length + 1,
                name: "",
                percentValue: 0,
                dueDate: "",
                actualPaymentDate: "",
                status: "Pending",
                responsibleAccountantUserId: null,
                requestedAt: null,
                note: "",
              },
            ])
          }
        >
          <Plus className="mr-1 h-4 w-4" />
          {t("contracts.addMilestone")}
        </Button>
      </div>
      {milestones.length === 0 ? (
        <div className="rounded-lg border border-dashed border-input p-6 text-center text-sm text-muted-foreground">
          {t("contracts.milestonesEmpty")}
        </div>
      ) : (
        milestones.map((milestone, index) => {
          const amount = Math.round(value * milestone.percentValue) / 100;
          return (
            <div
              key={index}
              className="space-y-3 rounded-lg border border-border bg-card p-4 shadow-sm"
              data-testid={`contract-detail-milestone-${index}`}
            >
              <div className="flex items-center justify-between gap-2">
                <span className="text-sm font-semibold text-muted-foreground">#{index + 1}</span>
                <div className="flex gap-1">
                  <Button
                    type="button"
                    size="icon"
                    variant="ghost"
                    className="h-11 w-11 sm:h-8 sm:w-8"
                    disabled={index === 0}
                    onClick={() => move(index, -1)}
                    aria-label={t("workflow.moveUp")}
                  >
                    <ArrowUp className="h-4 w-4" />
                  </Button>
                  <Button
                    type="button"
                    size="icon"
                    variant="ghost"
                    className="h-11 w-11 sm:h-8 sm:w-8"
                    disabled={index === milestones.length - 1}
                    onClick={() => move(index, 1)}
                    aria-label={t("workflow.moveDown")}
                  >
                    <ArrowDown className="h-4 w-4" />
                  </Button>
                  <Button
                    type="button"
                    size="icon"
                    variant="ghost"
                    className="h-11 w-11 text-destructive hover:text-destructive sm:h-8 sm:w-8"
                    onClick={() =>
                      onChange(
                        milestones
                          .filter((_, current) => current !== index)
                          .map((item, current) => ({ ...item, order: current + 1 })),
                      )
                    }
                    aria-label={t("common.delete")}
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </div>
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor={`milestone-name-${index}`}>
                    {t("contracts.milestone.name")} *
                  </Label>
                  <Input
                    id={`milestone-name-${index}`}
                    value={milestone.name}
                    onChange={(event) => patch(index, { name: event.target.value })}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor={`milestone-percent-${index}`}>
                    {t("contracts.milestone.percent")} *
                  </Label>
                  <Input
                    id={`milestone-percent-${index}`}
                    type="number"
                    min={0}
                    max={100}
                    step="0.01"
                    value={milestone.percentValue}
                    onChange={(event) =>
                      patch(index, { percentValue: Number(event.target.value) || 0 })
                    }
                  />
                  <p className="text-xs text-muted-foreground">≈ {formatMoney(amount, currency)}</p>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor={`milestone-due-${index}`}>
                    {t("contracts.milestone.dueDate")}
                  </Label>
                  <Input
                    id={`milestone-due-${index}`}
                    type="date"
                    value={milestone.dueDate}
                    onChange={(event) => patch(index, { dueDate: event.target.value })}
                  />
                </div>
                <div className="space-y-1.5">
                  <span>{t("contracts.milestone.status")}</span>
                  <div className="flex h-10 items-center">
                    <Badge variant="outline">
                      {t(
                        direction === "Upstream"
                          ? `contracts.collectionStatus.${milestone.status}`
                          : `contracts.milestoneStatus.${milestone.status}`,
                      )}
                    </Badge>
                  </div>
                </div>
                {milestone.status === "Paid" ? (
                  <div className="space-y-1.5">
                    <Label htmlFor={`milestone-actual-${index}`}>
                      {t(
                        direction === "Upstream"
                          ? "contracts.collection.actualDate"
                          : "contracts.milestone.actualPaymentDate",
                      )}{" "}
                      *
                    </Label>
                    <Input
                      id={`milestone-actual-${index}`}
                      type="date"
                      value={milestone.actualPaymentDate}
                      readOnly
                    />
                  </div>
                ) : null}
              </div>
            </div>
          );
        })
      )}
      {milestones.length > 0 ? (
        <div
          className={cn(
            "flex items-center justify-between rounded px-3 py-2 text-sm",
            totalValid ? "bg-emerald-50 text-emerald-700" : "bg-rose-50 text-rose-700",
          )}
        >
          <span>{t("contracts.milestonesSum")}</span>
          <span className="font-semibold">{total.toFixed(2)}% / 100%</span>
        </div>
      ) : null}
    </div>
  );
};
