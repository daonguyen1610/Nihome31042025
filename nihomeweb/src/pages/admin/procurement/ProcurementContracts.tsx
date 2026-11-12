import { Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { formatMoney } from "@/lib/displaySettings";
import type { ContractResponse } from "@/services/adminApi";

export default function ProcurementContracts({
  projectId,
  rows,
  canRead,
  available,
  onRetry,
}: {
  projectId: number;
  rows: ContractResponse[];
  canRead: boolean;
  available: boolean;
  onRetry: () => void;
}) {
  const { t } = useI18n();
  const { has } = usePermissions();
  const downstream = rows.filter((row) => row.direction === "Downstream");
  const registry = `/admin/contracts?direction=Downstream&operationalProjectId=${projectId}`;
  return (
    <section className="mb-6 space-y-3" data-testid="procurement-contract-registry">
      <h2 className="text-lg font-semibold">{t("procurement.contractRegistry.title")}</h2>
      <p className="text-sm text-muted-foreground">{t("procurement.contractRegistry.hint")}</p>
      <p className="max-w-3xl text-sm text-muted-foreground">
        {t("cmAreas.supplierContractGuide")}
      </p>
      {has(ADMIN_PERMS.procurementRfqs) && (
        <Button asChild variant="outline">
          <Link to={`/admin/procurement-control/rfqs?projectId=${projectId}`}>
            {t("cmAreas.openRfq")}
          </Link>
        </Button>
      )}
      {!canRead ? (
        <p>{t("procurement.contractRegistry.permission")}</p>
      ) : (
        <>
          <Button asChild variant="outline">
            <Link to={registry}>{t("nav.contracts")}</Link>
          </Button>
          {!available ? (
            <div role="alert">
              <p>{t("procurement.contractRegistry.loadError")}</p>
              <Button variant="outline" onClick={onRetry}>
                {t("common.retry")}
              </Button>
            </div>
          ) : downstream.length === 0 ? (
            <p>{t("procurement.contractRegistry.empty")}</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr>
                    <th className="p-2 text-left">{t("contracts.field.number")}</th>
                    <th className="p-2 text-left">{t("contracts.field.vendor")}</th>
                    <th className="p-2 text-left">{t("contracts.field.status")}</th>
                    <th className="p-2 text-right">{t("contracts.field.value")}</th>
                  </tr>
                </thead>
                <tbody>
                  {downstream.map((row) => (
                    <tr key={row.id} className="border-t">
                      <td className="p-2">
                        <Link className="text-primary underline" to={`/admin/contracts/${row.id}`}>
                          {row.contractNumber}
                        </Link>
                      </td>
                      <td className="p-2">{row.vendorName ?? row.vendorCode}</td>
                      <td className="p-2">{t(`contracts.status.${row.status}`)}</td>
                      <td className="p-2 text-right tabular-nums">
                        {formatMoney(row.currentValue, row.currency)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {has(ADMIN_PERMS.contractsManage) && (
            <p className="text-sm">{t("procurement.contractRegistry.createHint")}</p>
          )}
        </>
      )}
    </section>
  );
}
