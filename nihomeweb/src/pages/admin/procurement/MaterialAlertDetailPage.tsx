import { useCallback, useEffect, useMemo, useState } from "react";
import { ArrowLeft, CheckCircle2, ExternalLink, History, TriangleAlert } from "lucide-react";
import { Link, useParams } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import { adminApi, type MaterialAlertResponse, type MaterialAlertSeverity, type MaterialAlertStatus } from "@/services/adminApi";
import { useAppSelector } from "@/store";

const statusClass: Record<MaterialAlertStatus, string> = {
  Open: "border-rose-200 bg-rose-50 text-rose-800",
  Acknowledged: "border-amber-200 bg-amber-50 text-amber-800",
  Resolved: "border-emerald-200 bg-emerald-50 text-emerald-800",
};

const severityClass: Record<MaterialAlertSeverity, string> = {
  Critical: "border-rose-300 bg-rose-100 text-rose-900",
  Warning: "border-amber-300 bg-amber-100 text-amber-900",
};

const MaterialAlertDetailPage = () => {
  const { projectId: projectIdParam, alertId: alertIdParam } = useParams();
  const projectId = Number(projectIdParam);
  const alertId = Number(alertIdParam);
  const { t, lang } = useI18n();
  const { has } = usePermissions();
  const { toast } = useToast();
  const currentUser = useAppSelector((state) => state.auth.user);
  const canManage = has(ADMIN_PERMS.procurementMaterialAlertsManage);
  const [alert, setAlert] = useState<MaterialAlertResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [note, setNote] = useState("");
  const [formError, setFormError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!Number.isInteger(projectId) || projectId < 1 || !Number.isInteger(alertId) || alertId < 1) {
      setError(t("procurement.alerts.detail.invalidRoute"));
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      setAlert((await adminApi.getMaterialAlert(projectId, alertId)).data);
    } catch (reason) {
      setError(extractApiError(reason));
    } finally {
      setLoading(false);
    }
  }, [alertId, projectId, t]);

  useEffect(() => { void load(); }, [load]);

  const number = useMemo(() => new Intl.NumberFormat(lang, { maximumFractionDigits: 3 }), [lang]);
  const dateTime = (value?: string | null) => value
    ? new Intl.DateTimeFormat(lang, { dateStyle: "medium", timeStyle: "short" }).format(new Date(value))
    : t("procurement.alerts.notAvailable");

  const acknowledge = async () => {
    if (!alert) return;
    const normalized = note.trim();
    if (normalized.length < 3 || normalized.length > 2000) {
      setFormError(t("procurement.alerts.acknowledge.validation"));
      return;
    }
    setBusy(true);
    setFormError(null);
    try {
      const response = await adminApi.acknowledgeMaterialAlert(projectId, alert.id, normalized, alert.rowVersion);
      setAlert(response.data);
      setDialogOpen(false);
      setNote("");
      toast({ title: t("procurement.alerts.acknowledge.success") });
    } catch (reason) {
      const message = extractApiError(reason);
      setFormError(message);
      toast({ title: t("common.error"), description: message, variant: "destructive" });
    } finally {
      setBusy(false);
    }
  };

  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (error || !alert) return <AdminLayout><div className="p-4 sm:p-6"><PageError message={error ?? t("procurement.alerts.detail.notFound")} onRetry={() => void load()} /></div></AdminLayout>;

  const canAcknowledge = canManage && alert.status === "Open" &&
    (currentUser?.userId === alert.assignedToUserId || currentUser?.role === "PM");
  const sourceHref = alert.sourceEntityType === "WarehouseIssue"
    ? `/admin/procurement-control/projects/${projectId}/warehouse/issue/${alert.sourceEntityId}`
    : alert.sourceEntityType === "MaterialRequest"
      ? `/admin/procurement-control/projects/${projectId}/material-requests/${alert.sourceEntityId}`
      : null;
  const metrics = alert.type === "OverBoq"
    ? [
        ["procurement.alerts.boqAllowance", alert.boqAllowance],
        ["procurement.alerts.received", alert.receivedQuantity],
        ["procurement.alerts.issued", alert.issuedQuantity],
        ["procurement.alerts.onHand", alert.onHandQuantity],
      ] as const
    : [
        ["procurement.alerts.required", alert.requiredQuantity],
        ["procurement.alerts.received", alert.receivedQuantity],
        ["procurement.alerts.issued", alert.issuedQuantity],
        ["procurement.alerts.onHand", alert.onHandQuantity],
      ] as const;

  return (
    <AdminLayout>
      <main className="space-y-6 p-4 sm:p-6">
        <nav aria-label={t("procurement.alerts.detail.context")} className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <span>{alert.customerName}</span><span aria-hidden="true">/</span>
          <span>{alert.operationalProjectCode} · {alert.operationalProjectName}</span><span aria-hidden="true">/</span>
          <span className="font-medium text-foreground">{alert.code}</span>
        </nav>

        <header className="flex flex-col gap-4 border-b pb-5 lg:flex-row lg:items-end lg:justify-between">
          <div className="min-w-0">
            <Button asChild variant="ghost" size="sm" className="mb-2 -ml-3"><Link to={`/admin/procurement-control?projectId=${projectId}&tab=alerts`}><ArrowLeft className="mr-2 h-4 w-4" />{t("common.back")}</Link></Button>
            <div className="flex flex-wrap items-center gap-2">
              <TriangleAlert className="h-6 w-6 text-amber-600" aria-hidden="true" />
              <h1 className="text-2xl font-semibold">{alert.code} · {alert.itemCode}</h1>
              <Badge variant="outline" className={severityClass[alert.severity]}>{t(`procurement.alerts.severity.${alert.severity}`)}</Badge>
              <Badge variant="outline" className={statusClass[alert.status]}>{t(`procurement.alerts.status.${alert.status}`)}</Badge>
            </div>
            <p className="mt-2 max-w-3xl text-sm text-muted-foreground">{alert.description}</p>
          </div>
          {canAcknowledge && <Button onClick={() => { setFormError(null); setDialogOpen(true); }}><CheckCircle2 className="mr-2 h-4 w-4" />{t("procurement.alerts.acknowledge.action")}</Button>}
        </header>

        <section aria-labelledby="alert-risk-heading" className="border-y py-5">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <p className="text-sm font-medium text-muted-foreground">{t(`procurement.alerts.type.${alert.type}`)}</p>
              <h2 id="alert-risk-heading" className="mt-1 text-xl font-semibold">{t("procurement.alerts.detail.variance", { value: number.format(alert.varianceQuantity), unit: alert.unit })}</h2>
              <p className="mt-1 text-sm text-muted-foreground">{t(`procurement.alerts.detail.explanation.${alert.type}`)}</p>
            </div>
            {sourceHref && <Button asChild variant="outline"><Link to={sourceHref}>{t("procurement.alerts.openSource")}<ExternalLink className="ml-2 h-4 w-4" /></Link></Button>}
          </div>
          <dl className="mt-5 grid grid-cols-2 gap-x-5 gap-y-4 border-t pt-4 sm:grid-cols-4">
            {metrics.map(([label, value]) => <Metric key={label} label={t(label)} value={`${number.format(value)} ${alert.unit}`} />)}
          </dl>
        </section>

        <div className="grid gap-8 xl:grid-cols-[minmax(0,1fr)_minmax(320px,0.7fr)]">
          <section aria-labelledby="alert-accountability-heading" className="space-y-4">
            <div><h2 id="alert-accountability-heading" className="text-lg font-semibold">{t("procurement.alerts.accountability")}</h2><p className="text-sm text-muted-foreground">{t("procurement.alerts.accountabilityHint")}</p></div>
            <dl className="grid grid-cols-1 gap-4 border-y py-4 sm:grid-cols-2">
              <Datum label={t("procurement.alerts.assignee")} value={alert.assignedToUserName ?? t("procurement.alerts.unknownAssignee")} />
              <Datum label={t("procurement.alerts.detectedAt")} value={dateTime(alert.detectedAt)} />
              <Datum label={t("procurement.alerts.lastEvaluated")} value={dateTime(alert.lastEvaluatedAt)} />
              <Datum label={t("procurement.alerts.resolvedAt")} value={dateTime(alert.resolvedAt)} />
              {alert.acknowledgedAt && <Datum label={t("procurement.alerts.acknowledgedBy")} value={`${alert.acknowledgedByName ?? t("procurement.alerts.unknownAssignee")} · ${dateTime(alert.acknowledgedAt)}`} />}
              {alert.acknowledgementNote && <div className="sm:col-span-2"><Datum label={t("procurement.alerts.acknowledge.note")} value={alert.acknowledgementNote} /></div>}
            </dl>

            <div><h2 className="text-lg font-semibold">{t("procurement.alerts.contracts")}</h2><p className="text-sm text-muted-foreground">{t("procurement.alerts.contractsHint")}</p></div>
            {alert.contracts.length === 0 ? <p className="border-y py-4 text-sm text-muted-foreground">{t("procurement.alerts.noContracts")}</p> : <div className="divide-y border-y">{alert.contracts.map((contract) => <div key={contract.id} className="flex flex-col gap-1 py-3 sm:flex-row sm:items-center sm:justify-between"><div><p className="font-medium">{contract.contractNumber}</p><p className="text-sm text-muted-foreground">{contract.vendorName ?? t("procurement.alerts.customerContract")}</p></div><div className="flex flex-wrap gap-2"><Badge variant="outline">{t(`contracts.direction.${contract.direction}`)}</Badge><Badge variant="outline">{t(`contracts.status.${contract.status}`)}</Badge></div></div>)}</div>}
          </section>

          <section aria-labelledby="alert-history-heading" className="space-y-4">
            <div><h2 id="alert-history-heading" className="flex items-center gap-2 text-lg font-semibold"><History className="h-5 w-5" />{t("procurement.alerts.history")}</h2><p className="text-sm text-muted-foreground">{t("procurement.alerts.historyHint")}</p></div>
            <ol className="border-y">{alert.events.length === 0 ? <li className="py-4 text-sm text-muted-foreground">{t("procurement.alerts.noHistory")}</li> : alert.events.map((event) => <li key={event.id} className="border-b py-4 last:border-b-0"><div className="flex flex-wrap items-center justify-between gap-2"><p className="font-medium">{t(`procurement.alerts.event.${event.type}`)}</p><time className="text-xs text-muted-foreground">{dateTime(event.changedAt)}</time></div><p className="mt-1 text-sm text-muted-foreground">{event.changedByName ?? `#${event.changedByUserId}`}</p>{event.reason && <p className="mt-2 whitespace-pre-wrap text-sm">{event.reason}</p>}</li>)}</ol>
          </section>
        </div>
      </main>

      <Dialog open={dialogOpen} onOpenChange={(open) => { if (!busy) setDialogOpen(open); }}>
        <DialogContent className="max-h-[90vh] overflow-y-auto">
          <DialogHeader><DialogTitle>{t("procurement.alerts.acknowledge.title")}</DialogTitle><DialogDescription>{t("procurement.alerts.acknowledge.description")}</DialogDescription></DialogHeader>
          <div className="space-y-2"><Label htmlFor="alert-acknowledgement-note">{t("procurement.alerts.acknowledge.note")}</Label><Textarea id="alert-acknowledgement-note" value={note} onChange={(event) => setNote(event.target.value)} maxLength={2000} rows={5} aria-describedby="alert-note-hint alert-note-error" /><div id="alert-note-hint" className="flex justify-between gap-3 text-xs text-muted-foreground"><span>{t("procurement.alerts.acknowledge.noteHint")}</span><span>{note.length}/2000</span></div>{formError && <p id="alert-note-error" className="text-sm text-destructive" role="alert">{formError}</p>}</div>
          <DialogFooter><Button variant="outline" onClick={() => setDialogOpen(false)} disabled={busy}>{t("common.cancel")}</Button><Button onClick={() => void acknowledge()} disabled={busy}>{t("procurement.alerts.acknowledge.confirm")}</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

const Metric = ({ label, value }: { label: string; value: string }) => <div><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-1 text-lg font-semibold tabular-nums">{value}</dd></div>;
const Datum = ({ label, value }: { label: string; value: string }) => <div className="min-w-0"><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-1 break-words font-medium">{value}</dd></div>;

export default MaterialAlertDetailPage;