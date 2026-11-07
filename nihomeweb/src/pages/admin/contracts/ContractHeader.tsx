import { Link } from "react-router-dom";
import { ArrowLeft, Loader2, Pencil, Save } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { formatVndWithSymbol } from "@/lib/numberFormat";
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

  return (
    <div className="rounded-xl border border-border bg-card p-4 shadow-sm md:p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Link to={returnTo} className="inline-flex items-center gap-1 hover:text-foreground">
              <ArrowLeft className="h-4 w-4" />
              {t("contracts.detail.back")}
            </Link>
          </div>
          <div className="mt-1 flex flex-wrap items-center gap-2">
            <h1 className="truncate text-xl font-bold text-foreground md:text-2xl">
              {contract.contractNumber}
            </h1>
            <Badge className={cn("border capitalize", STATUS_STYLES[contract.status])}>
              {t(`contracts.status.${contract.status}`)}
            </Badge>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            <span className="font-medium">{contract.customerName ?? "—"}</span>
            {contract.ownerName ? (
              <span className="ml-2 text-xs text-muted-foreground">• {contract.ownerName}</span>
            ) : null}
          </p>
        </div>
        <div className="flex shrink-0 flex-wrap gap-2">
          {canEdit && !editing ? (
            <Button variant="outline" size="sm" onClick={onEditInfo}>
              <Pencil className="mr-1 h-4 w-4" />
              {t("common.edit")}
            </Button>
          ) : null}
          {editing ? (
            <>
              <Button variant="outline" size="sm" onClick={onCancelEdit} disabled={saving}>
                {t("common.cancel")}
              </Button>
              <Button size="sm" onClick={onSave} disabled={saving}>
                {saving ? (
                  <Loader2 className="mr-1 h-4 w-4 animate-spin" />
                ) : (
                  <Save className="mr-1 h-4 w-4" />
                )}
                {t("common.save")}
              </Button>
            </>
          ) : null}
        </div>
      </div>

      <div className="mt-4 grid gap-3 sm:grid-cols-3">
        <div className="rounded-lg bg-muted/30 p-3">
          <div className="text-xs text-muted-foreground">
            {t("contracts.detail.header.baseValue")}
          </div>
          <div className="mt-1 text-lg font-semibold text-foreground">
            {formatVndWithSymbol(contract.value)}
          </div>
        </div>
        <div className="rounded-lg bg-muted/30 p-3">
          <div className="text-xs text-muted-foreground">
            {t("contracts.detail.header.voTotal")}
          </div>
          <div
            className={cn(
              "mt-1 text-lg font-semibold",
              contract.approvedVoTotal > 0
                ? "text-emerald-700"
                : contract.approvedVoTotal < 0
                  ? "text-rose-700"
                  : "text-foreground",
            )}
          >
            {contract.approvedVoTotal > 0 ? "+" : ""}
            {formatVndWithSymbol(contract.approvedVoTotal)}
          </div>
        </div>
        <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-3">
          <div className="text-xs text-emerald-700">
            {t("contracts.detail.header.currentValue")}
          </div>
          <div className="mt-1 text-lg font-bold text-emerald-900">
            {formatVndWithSymbol(contract.currentValue)}
          </div>
        </div>
      </div>

      {!editing && canTransition && transitions.length > 0 ? (
        <div className="mt-4 flex flex-wrap gap-2">
          {transitions.map((target) => (
            <Button
              key={target}
              variant={target === "Cancelled" ? "outline" : "default"}
              size="sm"
              disabled={transitionBusy !== null}
              onClick={() => onTransition(target)}
              className={cn(
                target === "Cancelled" && "border-rose-200 text-rose-700 hover:bg-rose-50",
              )}
            >
              {transitionBusy === target ? (
                <Loader2 className="mr-1 h-4 w-4 animate-spin" />
              ) : (
                <span className="mr-1">{TRANSITION_ICON[target]}</span>
              )}
              {label(target)}
            </Button>
          ))}
        </div>
      ) : null}
      {!editing && contract.type === "Unclassified" ? (
        <p className="mt-4 rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
          {t("contracts.validation.classifyBeforeTransition")}
        </p>
      ) : null}
    </div>
  );
};
