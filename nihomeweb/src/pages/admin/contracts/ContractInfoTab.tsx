import { Link } from "react-router-dom";
import { Link2 } from "lucide-react";
import ContractSourceLeads from "@/components/admin/ContractSourceLeads";
import { useI18n } from "@/lib/i18n";
import { formatMoney } from "@/lib/displaySettings";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { type ContractResponse } from "@/services/adminApi";
import { formatDate } from "./contractDetailShared";

export interface InfoTabProps {
  contract: ContractResponse;
  onEnsureDesignProject?: () => void;
  ensuringDesignProject: boolean;
  canViewDesignProject: boolean;
  canViewLeads: boolean;
  canViewTender: boolean;
  /** Set when the caller may attach an approved quote to this contract. */
  onLinkQuote?: () => void;
}

export const InfoTab = ({
  contract,
  onEnsureDesignProject,
  ensuringDesignProject,
  canViewDesignProject,
  canViewLeads,
  canViewTender,
  onLinkQuote,
}: InfoTabProps) => {
  const { t } = useI18n();

  const rows: [string, React.ReactNode][] = [
    [t("contracts.field.number"), contract.contractNumber],
    [t("contracts.field.customer"), contract.customerName ?? "—"],
    [
      t("contracts.field.operationalProject"),
      contract.operationalProjectId ? (
        <Link
          className="text-primary hover:underline"
          to={`/admin/operational-projects/${contract.operationalProjectId}`}
        >
          {contract.operationalProjectCode ?? `#${contract.operationalProjectId}`} ·{" "}
          {contract.operationalProjectName ?? ""}
        </Link>
      ) : (
        "—"
      ),
    ],
    [t("contracts.field.direction"), t(`contracts.direction.${contract.direction}`)],
    [t("contracts.field.type"), t(`contracts.type.${contract.type}`)],
    [t("contracts.field.counterparty"), contract.vendorName ?? contract.customerName ?? "—"],
    [t("contracts.field.owner"), contract.ownerName ?? "—"],
    [t("contracts.field.status"), t(`contracts.status.${contract.status}`)],
    [t("contracts.field.signedDate"), formatDate(contract.signedDate)],
    [t("contracts.field.startDate"), formatDate(contract.startDate)],
    [t("contracts.field.endDate"), formatDate(contract.endDate)],
    [t("contracts.field.value"), `${formatMoney(contract.value, contract.currency)}`],
  ];

  return (
    <div className="space-y-4">
      <div className="rounded-lg border border-border bg-card p-4 shadow-sm">
        <dl className="grid gap-3 sm:grid-cols-2">
          {rows.map(([label, value]) => (
            <div key={label}>
              <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                {label}
              </dt>
              <dd className="mt-0.5 text-sm text-foreground">{value}</dd>
            </div>
          ))}
        </dl>
      </div>

      {contract.direction === "Upstream" ||
      contract.quoteId != null ||
      contract.opportunityId != null ||
      onLinkQuote ? (
        <div className="rounded-lg border border-border bg-card p-4 shadow-sm">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
              {t("contracts.source.title")}
            </div>
            {onLinkQuote && contract.quoteId == null && (
              <Button
                size="sm"
                variant="outline"
                data-testid="contract-link-quote"
                onClick={onLinkQuote}
              >
                <Link2 className="mr-1.5 h-4 w-4" />
                {t("quoteLink.fromContract.action")}
              </Button>
            )}
          </div>
          <dl className="mt-2 grid gap-3 sm:grid-cols-2">
            {contract.quoteId != null ? (
              <div>
                <dt className="text-xs text-muted-foreground">
                  {t("contracts.field.sourceQuote")}
                </dt>
                <dd className="mt-0.5 text-sm">
                  <Link className="underline" to={`/admin/quotes/${contract.quoteId}`}>
                    {contract.quoteCode ?? `#${contract.quoteId}`}
                  </Link>
                </dd>
              </div>
            ) : null}
            {contract.opportunityId != null ? (
              <div>
                <dt className="text-xs text-muted-foreground">
                  {t("contracts.field.sourceOpportunity")}
                </dt>
                <dd className="mt-0.5 text-sm">
                  <Link className="underline" to={`/admin/opportunities/${contract.opportunityId}`}>
                    {contract.opportunityTitle ?? `#${contract.opportunityId}`}
                  </Link>
                </dd>
              </div>
            ) : null}
            {contract.sourceTenderId != null ? (
              <div>
                <dt className="text-xs text-muted-foreground">
                  {t("contracts.field.sourceTender")}
                </dt>
                <dd className="mt-0.5 break-words text-sm">
                  {canViewTender ? (
                    <Link className="underline" to={`/admin/tenders/${contract.sourceTenderId}`}>
                      {contract.sourceTenderCode ??
                        contract.sourceTenderName ??
                        `#${contract.sourceTenderId}`}
                    </Link>
                  ) : (
                    <span>
                      {contract.sourceTenderCode ??
                        contract.sourceTenderName ??
                        `#${contract.sourceTenderId}`}
                    </span>
                  )}
                  {contract.sourceTenderCode && contract.sourceTenderName ? (
                    <span className="ml-1 text-muted-foreground">
                      · {contract.sourceTenderName}
                    </span>
                  ) : null}
                </dd>
              </div>
            ) : null}
          </dl>
          {contract.direction === "Upstream" && canViewLeads && (
            <ContractSourceLeads contract={contract} />
          )}
        </div>
      ) : null}

      <div className="rounded-lg border border-border bg-card p-4 shadow-sm">
        <div className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
          {t("contracts.designProject.title")}
        </div>
        {contract.designProjectId != null ? (
          <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1">
            {canViewDesignProject ? (
              <Link
                className="text-sm font-medium text-foreground underline"
                to={`/admin/design-projects/${contract.designProjectId}`}
              >
                {contract.designProjectCode} — {contract.designProjectName}
              </Link>
            ) : (
              <span className="text-sm font-medium text-foreground">
                {contract.designProjectCode} — {contract.designProjectName}
              </span>
            )}
            {contract.designProjectCurrentStage ? (
              <Badge variant="outline">
                {t(`designProjects.stage.${contract.designProjectCurrentStage}`)}
              </Badge>
            ) : null}
          </div>
        ) : contract.status === "InProgress" ? (
          <div className="mt-2 space-y-2">
            <p className="text-sm text-muted-foreground">{t("contracts.designProject.missing")}</p>
            {onEnsureDesignProject ? (
              <Button size="sm" onClick={onEnsureDesignProject} disabled={ensuringDesignProject}>
                {ensuringDesignProject ? "…" : t("contracts.designProject.create")}
              </Button>
            ) : null}
          </div>
        ) : (
          <p className="mt-2 text-sm text-muted-foreground">
            {t("contracts.designProject.pending")}
          </p>
        )}
      </div>

      {contract.scopeOfWork ? (
        <div className="rounded-lg border border-border bg-card p-4 shadow-sm">
          <div className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            {t("contracts.field.scope")}
          </div>
          <p className="mt-2 whitespace-pre-wrap text-sm text-foreground">{contract.scopeOfWork}</p>
        </div>
      ) : null}
      {contract.note ? (
        <div className="rounded-lg border border-border bg-card p-4 shadow-sm">
          <div className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            {t("contracts.field.note")}
          </div>
          <p className="mt-2 whitespace-pre-wrap text-sm text-foreground">{contract.note}</p>
        </div>
      ) : null}
    </div>
  );
};
