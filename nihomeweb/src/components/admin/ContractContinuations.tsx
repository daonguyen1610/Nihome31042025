import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { adminApi, type ContractResponse } from "@/services/adminApi";
import { useI18n } from "@/lib/i18n";
import { extractApiError } from "@/lib/apiError";
import { useContractContinuation } from "@/hooks/useContractContinuation";
import { usePermissions } from "@/hooks/usePermissions";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

export function ContinuationFields({
  context,
}: {
  context: ReturnType<typeof useContractContinuation>;
}) {
  const { t } = useI18n();
  if (context.error) return <p role="alert">{context.error}</p>;
  if (context.loading) return <p role="status">{t("common.loading")}</p>;
  if (!context.source) return null;
  return (
    <section className="space-y-2 rounded border p-3" aria-label={t("continuation.title")}>
      <p>
        {t("continuation.source")}: {context.source.contractNumber}
      </p>
      <Label htmlFor="continuation-kind">{t("continuation.kind")}</Label>
      <select
        id="continuation-kind"
        className="h-10 w-full rounded border bg-background px-2"
        value={context.kind}
        disabled={context.inherited}
        onChange={(e) => context.setKind(e.target.value)}
      >
        {context.kinds.map((kind) => (
          <option key={kind} value={kind}>
            {t(`continuation.${kind}`)}
          </option>
        ))}
      </select>
      <Label htmlFor="continuation-reason">{t("continuation.reason")}</Label>
      <Input
        id="continuation-reason"
        value={context.reason}
        maxLength={2000}
        readOnly={context.inherited}
        onChange={(e) => context.setReason(e.target.value)}
      />
      <p className="text-sm text-muted-foreground">{t("continuation.help")}</p>
    </section>
  );
}

export default function ContractContinuations({
  contract,
  canManage,
}: {
  contract: ContractResponse;
  canManage: boolean;
}) {
  const { t } = useI18n();
  const { has } = usePermissions();
  const [data, setData] = useState<
    Awaited<ReturnType<typeof adminApi.getContractContinuations>>["data"] | null
  >(null);
  const [error, setError] = useState("");
  const [reload, setReload] = useState(0);
  useEffect(() => {
    let active = true;
    setData(null);
    setError("");
    adminApi
      .getContractContinuations(contract.id)
      .then((r) => {
        if (active) setData(r.data);
      })
      .catch((e) => {
        if (active) setError(extractApiError(e));
      });
    return () => {
      active = false;
    };
  }, [contract.id, reload]);
  const canCreateOpportunity = has("crm.opportunities.manage");
  const eligible = !!contract.signedDate && !["Draft", "Cancelled"].includes(contract.status);
  return (
    <section className="space-y-3 rounded border p-4" aria-label={t("continuation.title")}>
      <h2 className="font-semibold">{t("continuation.title")}</h2>
      <p className="text-sm text-muted-foreground">{t("continuation.help")}</p>
      {error ? (
        <>
          <p role="alert">{error}</p>
          <Button variant="outline" onClick={() => setReload((x) => x + 1)}>
            {t("common.refresh")}
          </Button>
        </>
      ) : !data ? (
        <p role="status">{t("common.loading")}</p>
      ) : (
        <>
          {eligible && (canManage || canCreateOpportunity) && (
            <div className="flex flex-wrap gap-2">
              {canManage && (
                <Button asChild>
                  <Link to={`/admin/contracts?sourceContractId=${contract.id}`}>
                    {t("continuation.createContract")}
                  </Link>
                </Button>
              )}
              {canCreateOpportunity && (
                <Button asChild variant="outline">
                  <Link to={`/admin/opportunities?sourceContractId=${contract.id}`}>
                    {t("continuation.createOpportunity")}
                  </Link>
                </Button>
              )}
            </div>
          )}
          {!data.contracts.length && !data.opportunities.length && <p>{t("continuation.empty")}</p>}
          <ul className="space-y-2">
            {data.contracts.map((row) => (
              <li key={`c-${row.id}`} className="break-words rounded border p-2">
                <Link className="underline" to={`/admin/contracts/${row.id}`}>
                  {row.contractNumber}
                </Link>{" "}
                ·{" "}
                {row.isSource
                  ? t("continuation.source")
                  : t(`continuation.${row.continuationKind}`)}{" "}
                · {t(`contracts.status.${row.status}`)}
                {row.continuationReason && <p>{row.continuationReason}</p>}
              </li>
            ))}
            {data.opportunities.map((row) => (
              <li key={`o-${row.id}`} className="break-words rounded border p-2">
                <Link className="underline" to={`/admin/opportunities/${row.id}`}>
                  {row.name}
                </Link>{" "}
                · {t(`opportunities.stage.${row.stage}`)}
                <p>{row.continuationReason}</p>
                {row.lostNote && <p>{row.lostNote}</p>}
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}
