import { Link } from "react-router-dom";
import { ArrowLeft, Loader2, Pencil, Save } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { formatMoney } from "@/lib/displaySettings";
import { cn } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { type ContractResponse, type ContractStatus } from "@/services/adminApi";
import {
  AVAILABLE_TRANSITIONS,
  TRANSITION_LABEL_KEY,
  STATUS_STYLES,
  TRANSITION_ICON,
} from "./contractDetailShared";

export interface HeaderProps {
  contract: ContractResponse;
  returnTo: string;
  canEdit: boolean;
  canTransition: boolean;
  editing: boolean;
  saving: boolean;
  onEditInfo: () => void;
  onCancelEdit: () => void;
  onSave: () => void;
  onTransition: (next: ContractStatus) => void;
  transitionBusy: ContractStatus | null;
}

export const ContractHeader = ({
  contract,
  returnTo,
  canEdit,
  canTransition,
  editing,
  saving,
  onEditInfo,
  onCancelEdit,
  onSave,
  onTransition,
  transitionBusy,
}: HeaderProps) => {
  const { t } = useI18n();

  const transitions =
    contract.type === "Unclassified" ? [] : (AVAILABLE_TRANSITIONS[contract.status] ?? []);
  const label = (target: ContractStatus): string => {
    // Signed -> InProgress on the Signed row = "Move to In progress",
    // OnHold -> InProgress = "Resume". Same icon, clearer wording.
    if (target === "InProgress" && contract.status === "OnHold") {
      return t("contracts.detail.transition.markResume");
    }
    return t(TRANSITION_LABEL_KEY[target]);
  };

  const showTransitions = !editing && canTransition && transitions.length > 0;
  const facts = [
    {
      label: t("contracts.detail.header.currentValue"),
      value: formatMoney(contract.currentValue, contract.currency),
      className: "text-xl",
    },
    {
      label: t("contracts.detail.header.baseValue"),
      value: formatMoney(contract.value, contract.currency),
    },
    {
      label: t("contracts.detail.header.voTotal"),
      value: `${contract.approvedVoTotal > 0 ? "+" : ""}${formatMoney(contract.approvedVoTotal, contract.currency)}`,
      className:
        contract.approvedVoTotal > 0
          ? "text-success-strong"
          : contract.approvedVoTotal < 0
            ? "text-destructive"
            : undefined,
    },
  ];

  return (
    <div className="space-y-4">
      <Link
        to={returnTo}
        className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="h-4 w-4" />
        {t("contracts.detail.back")}
      </Link>
      <header className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="break-words text-2xl font-semibold text-foreground">
              {contract.contractNumber}
            </h1>
            <Badge className={cn("border capitalize", STATUS_STYLES[contract.status])}>
              {t(`contracts.status.${contract.status}`)}
            </Badge>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            <span className="font-medium text-foreground">{contract.customerName ?? "—"}</span>
            {contract.ownerName ? <span className="ml-2">• {contract.ownerName}</span> : null}
          </p>
        </div>
        <div className="flex shrink-0 flex-wrap gap-2">
          {showTransitions &&
            // The forward step comes first and is the only filled button; cancelling stays quiet.
            [...transitions]
              .sort((x, y) => Number(x === "Cancelled") - Number(y === "Cancelled"))
              .map((target, index) => (
                <Button
                  key={target}
                  variant={
                    target === "Cancelled"
                      ? "destructiveOutline"
                      : index === 0
                        ? "default"
                        : "outline"
                  }
                  disabled={transitionBusy !== null}
                  onClick={() => onTransition(target)}
                >
                  {transitionBusy === target ? (
                    <Loader2 className="animate-spin" />
                  ) : (
                    TRANSITION_ICON[target]
                  )}
                  {label(target)}
                </Button>
              ))}
          {canEdit && !editing ? (
            <Button variant="outline" onClick={onEditInfo}>
              <Pencil />
              {t("common.edit")}
            </Button>
          ) : null}
          {editing ? (
            <>
              <Button variant="outline" onClick={onCancelEdit} disabled={saving}>
                {t("common.cancel")}
              </Button>
              <Button onClick={onSave} disabled={saving}>
                {saving ? <Loader2 className="animate-spin" /> : <Save />}
                {t("common.save")}
              </Button>
            </>
          ) : null}
        </div>
      </header>

      <dl className="grid divide-y rounded-lg border bg-card sm:grid-cols-3 sm:divide-x sm:divide-y-0">
        {facts.map((fact) => (
          <div key={fact.label} className="flex flex-col justify-between gap-1 p-4">
            <dt className="text-xs text-muted-foreground">{fact.label}</dt>
            <dd className={cn("text-lg font-semibold tabular-nums", fact.className)}>
              {fact.value}
            </dd>
          </div>
        ))}
      </dl>

      {!editing && contract.type === "Unclassified" ? (
        <p className="rounded-md border border-warning/40 bg-warning-soft p-3 text-sm text-warning-strong">
          {t("contracts.validation.classifyBeforeTransition")}
        </p>
      ) : null}
    </div>
  );
};
