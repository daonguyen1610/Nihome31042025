import { extractDownloadError } from "@/lib/apiError";
import { useCallback, useEffect, useState } from "react";
import { adminApi, type ProcurementIfcPacket } from "@/services/adminApi";
import { useI18n } from "@/lib/i18n";
import { useUiError } from "@/hooks/useUiError";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { Button } from "@/components/ui/button";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import { ListPagination } from "@/components/admin/ListPagination";
import { formatDateTime } from "@/lib/displaySettings";

export function ProcurementIfcInbox({ projectId }: { projectId: number }) {
  const { t, lang } = useI18n();
  const uiError = useUiError();
  const { has } = usePermissions();
  const [rows, setRows] = useState<ProcurementIfcPacket[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    void adminApi
      .listProcurementIfc(projectId, page)
      .then(({ data }) => {
        if (active) {
          setRows(data.items);
          setTotal(data.total);
        }
      })
      .catch((cause) => {
        if (active) setError(uiError(cause));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [projectId, page, revision, uiError]);
  const refresh = useCallback(() => setRevision((value) => value + 1), []);
  async function acknowledge(row: ProcurementIfcPacket) {
    setBusy(row.recipientId);
    setError(null);
    try {
      const { data } = await adminApi.acknowledgeProcurementIfc(
        projectId,
        row.recipientId,
        row.rowVersion,
      );
      setRows((current) =>
        current.map((item) => (item.recipientId === data.recipientId ? data : item)),
      );
    } catch (cause) {
      setError(uiError(cause));
    } finally {
      setBusy(null);
    }
  }
  return (
    <section className="space-y-4" data-testid="procurement-ifc-inbox">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">{t("procurement.ifc.title")}</h2>
          <p className="text-sm text-muted-foreground">{t("procurement.ifc.description")}</p>
        </div>
        <Button variant="outline" onClick={refresh} disabled={loading || busy != null}>
          {t("common.refresh")}
        </Button>
      </div>
      {error && <PageError message={error} onRetry={refresh} />}
      {loading ? (
        <PageLoading />
      ) : rows.length === 0 ? (
        <PageEmpty message={t("procurement.ifc.empty")} />
      ) : (
        <ul className="space-y-4">
          {rows.map((row) => (
            <li
              key={row.recipientId}
              className="space-y-3 rounded-lg bg-card p-4"
              data-testid={`procurement-ifc-${row.recipientId}`}
            >
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                  <h3 className="break-words font-semibold">
                    {row.releaseNumber} · {row.title}
                  </h3>
                  <p className="text-sm text-muted-foreground">
                    {row.dossierCode} ·{" "}
                    {row.releaseDate ? formatDateTime(row.releaseDate, lang) : "—"}
                  </p>
                </div>
                <p className="text-sm">
                  {row.acknowledgedAt
                    ? t("procurement.ifc.received")
                    : t("procurement.ifc.pending")}
                </p>
              </div>
              <ul className="space-y-2">
                {row.drawings.map((drawing) => (
                  <li
                    key={drawing.itemId}
                    className="flex flex-wrap items-center justify-between gap-2"
                  >
                    <span className="min-w-0 break-words">
                      {drawing.code} · {drawing.title}
                    </span>
                    {drawing.hasFile && (
                      <AdminFilePreview
                        fileName={drawing.code}
                        fetchFile={async () => {
                          try {
                            return (
                              await adminApi.getProcurementIfcContent(
                                projectId,
                                row.recipientId,
                                drawing.itemId,
                              )
                            ).data;
                          } catch (cause) {
                            setError(
                              await extractDownloadError(cause, t("auth.err.unexpected"), t),
                            );
                            throw cause;
                          }
                        }}
                      />
                    )}
                  </li>
                ))}
              </ul>
              {row.acknowledgedAt ? (
                <p className="text-sm">
                  {t("procurement.ifc.receivedAt", {
                    date: formatDateTime(row.acknowledgedAt, lang),
                  })}
                </p>
              ) : (
                has(ADMIN_PERMS.procurementIfcAcknowledge) && (
                  <Button onClick={() => void acknowledge(row)} disabled={busy != null}>
                    {busy === row.recipientId
                      ? t("common.saving")
                      : t("procurement.ifc.acknowledge")}
                  </Button>
                )
              )}
            </li>
          ))}
        </ul>
      )}
      <ListPagination
        page={page}
        pageSize={20}
        total={total}
        onPageChange={setPage}
        disabled={loading || busy != null}
      />
    </section>
  );
}
