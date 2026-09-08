import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import {
  Banknote,
  CalendarClock,
  Check,
  CircleDollarSign,
  FileClock,
  Landmark,
  Plus,
  RefreshCw,
  RotateCcw,
  Send,
  ShieldCheck,
  X,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type AccountingCorrectionResponse,
  type AccountingPeriodResponse,
  type ContractResponse,
  type OperationalProjectListItemResponse,
  type PaymentAttachmentRequest,
  type PaymentRequestResponse,
  type PaymentReferencesResponse,
  type UserListItemResponse,
} from "@/services/adminApi";
import { useAppSelector } from "@/store";

type Translate = (key: string, params?: Record<string, string | number>) => string;
type CreateDialog = "payment" | "period" | "correction" | null;
type ActionKind =
  | "paymentApprove"
  | "paymentReject"
  | "paymentPay"
  | "paymentCancel"
  | "periodClose"
  | "correctionApprove"
  | "correctionReject"
  | "correctionReverse";
type ActionTarget = { kind: ActionKind; id: number; rowVersion: string };
type UserOption = Pick<UserListItemResponse, "id" | "fullName" | "email">;

const currentLocalDateTime = () => {
  const now = new Date();
  now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
  return now.toISOString().slice(0, 16);
};

const emptyAttachment = (): PaymentAttachmentRequest => ({ fileName: "", filePath: "" });
const emptyPayment = () => ({
  contractId: 0,
  vendorId: 0,
  contractPaymentMilestoneId: 0,
  supplierInvoiceNumber: "",
  invoiceDate: new Date().toISOString().slice(0, 10),
  invoiceAmount: 0,
  currency: "VND",
  receivedAt: currentLocalDateTime(),
  assignedAccountantUserId: 0,
  attachments: [emptyAttachment()],
});
const emptyCorrection = () => ({
  operationalProjectId: 0,
  accountingPeriodId: 0,
  sourceEntityType: "PaymentRequest",
  sourceEntityId: 0,
  reasonCode: "",
  originalValue: 0,
  correctedValue: 0,
  currency: "VND",
  note: "",
  responsibleAccountantUserId: 0,
});

const statusClass: Record<string, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  UnderValidation: "border-sky-200 bg-sky-50 text-sky-700",
  ReadyForApproval: "border-amber-200 bg-amber-50 text-amber-800",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Paid: "border-emerald-300 bg-emerald-100 text-emerald-800",
  Rejected: "border-rose-200 bg-rose-50 text-rose-700",
  Cancelled: "border-slate-200 bg-slate-50 text-slate-500",
  Open: "border-sky-200 bg-sky-50 text-sky-700",
  Closing: "border-amber-200 bg-amber-50 text-amber-800",
  Closed: "border-slate-300 bg-slate-100 text-slate-800",
  Submitted: "border-sky-200 bg-sky-50 text-sky-700",
  Reversed: "border-rose-200 bg-rose-50 text-rose-700",
};

const hostRelativePath = (value: string) => value.startsWith("/")
  && !value.startsWith("//")
  && !value.includes("\\")
  && !value.includes("..")
  && !value.includes("://");

const FinanceControlPage = () => {
  const { t, lang } = useI18n();
  const { has } = usePermissions();
  const { toast } = useToast();
  const currentUserId = useAppSelector((state) => state.auth.user?.userId);

  const canViewPayments = has(ADMIN_PERMS.financePayments);
  const canManagePayments = has(ADMIN_PERMS.financePaymentsManage);
  const canApprovePayments = has(ADMIN_PERMS.financePaymentsApprove);
  const canPayPayments = has(ADMIN_PERMS.financePaymentsPay);
  const canViewPeriods = has(ADMIN_PERMS.financePeriods);
  const canClosePeriods = has(ADMIN_PERMS.financePeriodsClose);
  const canViewCorrections = has(ADMIN_PERMS.financeCorrections);
  const canManageCorrections = has(ADMIN_PERMS.financeCorrectionsManage);
  const canApproveCorrections = has(ADMIN_PERMS.financeCorrectionsApprove);
  const canReverseCorrections = has(ADMIN_PERMS.financeCorrectionsReverse);

  const [payments, setPayments] = useState<PaymentRequestResponse[]>([]);
  const [periods, setPeriods] = useState<AccountingPeriodResponse[]>([]);
  const [corrections, setCorrections] = useState<AccountingCorrectionResponse[]>([]);
  const [contracts, setContracts] = useState<ContractResponse[]>([]);
  const [paymentReferences, setPaymentReferences] = useState<PaymentReferencesResponse | null>(null);
  const [paymentReferencesError, setPaymentReferencesError] = useState("");
  const [paymentReferencesRetry, setPaymentReferencesRetry] = useState(0);
  const [projects, setProjects] = useState<OperationalProjectListItemResponse[]>([]);
  const [accountants, setAccountants] = useState<UserOption[]>([]);
  const [lookupAvailable, setLookupAvailable] = useState({ contracts: true, projects: true, users: true });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [dialog, setDialog] = useState<CreateDialog>(null);
  const [action, setAction] = useState<ActionTarget | null>(null);
  const [reason, setReason] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [paymentForm, setPaymentForm] = useState(emptyPayment);
  const [periodForm, setPeriodForm] = useState({ year: new Date().getFullYear(), month: new Date().getMonth() + 1 });
  const [correctionForm, setCorrectionForm] = useState(emptyCorrection);

  const dateTime = useMemo(() => new Intl.DateTimeFormat(lang, { dateStyle: "medium", timeStyle: "short" }), [lang]);
  const money = useMemo(() => new Intl.NumberFormat(lang, { maximumFractionDigits: 2 }), [lang]);
  const defaultTab = canViewPayments ? "payments" : canViewPeriods ? "periods" : "corrections";

  const loadFinance = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [paymentResult, periodResult, correctionResult] = await Promise.all([
        canViewPayments ? adminApi.listPaymentRequests() : Promise.resolve({ data: [] as PaymentRequestResponse[] }),
        canViewPeriods ? adminApi.listAccountingPeriods() : Promise.resolve({ data: [] as AccountingPeriodResponse[] }),
        canViewCorrections ? adminApi.listAccountingCorrections() : Promise.resolve({ data: [] as AccountingCorrectionResponse[] }),
      ]);
      setPayments(paymentResult.data);
      setPeriods(periodResult.data);
      setCorrections(correctionResult.data);
    } catch (reasonValue) {
      setError(extractApiError(reasonValue));
    } finally {
      setLoading(false);
    }
  }, [canViewCorrections, canViewPayments, canViewPeriods]);

  useEffect(() => { void loadFinance(); }, [loadFinance]);

  useEffect(() => {
    let cancelled = false;
    Promise.all([
      adminApi.listContracts({ page: 1, pageSize: 200 }).then((response) => response.data.items).catch(() => null),
      adminApi.listOperationalProjects({ page: 1, pageSize: 200 }).then((response) => response.data.items).catch(() => null),
      adminApi.getUsers({ skip: 0, take: 200, role: "ACCOUNTANT" }).then((response) => response.data.items.filter((user) => user.isActive)).catch(() => null),
    ]).then(([contractRows, projectRows, userRows]) => {
      if (cancelled) return;
      setLookupAvailable({ contracts: contractRows != null, projects: projectRows != null, users: userRows != null });
      setContracts((contractRows ?? []).filter((contract) => contract.direction === "Downstream" && !["Draft", "Cancelled"].includes(contract.status)));
      setProjects(projectRows ?? []);
      setAccountants(userRows ?? []);
    });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (dialog !== "payment" || !canManagePayments) return;
    let cancelled = false;
    setPaymentReferences(null);
    setPaymentReferencesError("");
    adminApi.getPaymentReferences().then(response => {
      if (!cancelled) setPaymentReferences(response.data);
    }).catch(reasonValue => {
      if (!cancelled) setPaymentReferencesError(extractApiError(reasonValue));
    });
    return () => { cancelled = true; };
  }, [dialog, canManagePayments, paymentReferencesRetry]);

  const status = (value: string) => <Badge variant="outline" className={statusClass[value] ?? ""}>{t(`finance.status.${value}`)}</Badge>;
  const formatDate = (value?: string | null) => value ? dateTime.format(new Date(value)) : "-";

  const runMutation = async (operation: () => Promise<unknown>, successKey: string, closeDialog = true) => {
    setBusy(true);
    setFormError(null);
    try {
      await operation();
      if (closeDialog) setDialog(null);
      setAction(null);
      setReason("");
      toast({ title: t(successKey) });
      await loadFinance();
    } catch (reasonValue) {
      const message = extractApiError(reasonValue);
      setFormError(message);
      toast({ title: t("common.error"), description: message, variant: "destructive" });
    } finally {
      setBusy(false);
    }
  };

  const openCreate = (kind: Exclude<CreateDialog, null>) => {
    setFormError(null);
    if (kind === "payment") setPaymentForm(emptyPayment());
    if (kind === "period") setPeriodForm({ year: new Date().getFullYear(), month: new Date().getMonth() + 1 });
    if (kind === "correction") setCorrectionForm(emptyCorrection());
    setDialog(kind);
  };

  const createPayment = () => {
    const attachments = paymentForm.attachments.filter((item) => item.fileName.trim() || item.filePath.trim());
    const valid = paymentForm.contractId > 0
      && paymentForm.vendorId > 0
      && paymentForm.supplierInvoiceNumber.trim().length > 0
      && paymentForm.invoiceDate
      && paymentForm.invoiceAmount > 0
      && /^[A-Z]{3}$/.test(paymentForm.currency)
      && paymentForm.receivedAt
      && paymentForm.assignedAccountantUserId > 0
      && attachments.every((item) => item.fileName.trim().length > 0 && hostRelativePath(item.filePath.trim()));
    if (!valid) { setFormError(t("finance.validation.payment")); return; }
    void runMutation(() => adminApi.createPaymentRequest({
      ...paymentForm,
      supplierInvoiceNumber: paymentForm.supplierInvoiceNumber.trim(),
      receivedAt: new Date(paymentForm.receivedAt).toISOString(),
      contractPaymentMilestoneId: paymentForm.contractPaymentMilestoneId || null,
      attachments: attachments.map((item) => ({ fileName: item.fileName.trim(), filePath: item.filePath.trim() })),
    }), "finance.success.paymentCreated");
  };

  const createPeriod = () => {
    if (periodForm.year < 2000 || periodForm.year > 9999 || periodForm.month < 1 || periodForm.month > 12) {
      setFormError(t("finance.validation.period")); return;
    }
    void runMutation(() => adminApi.createAccountingPeriod(periodForm.year, periodForm.month), "finance.success.periodCreated");
  };

  const createCorrection = () => {
    const valid = correctionForm.operationalProjectId > 0
      && correctionForm.accountingPeriodId > 0
      && /^[A-Za-z][A-Za-z0-9]*$/.test(correctionForm.sourceEntityType)
      && correctionForm.sourceEntityId > 0
      && /^[A-Z][A-Z0-9_]*$/.test(correctionForm.reasonCode)
      && correctionForm.originalValue !== correctionForm.correctedValue
      && /^[A-Z]{3}$/.test(correctionForm.currency)
      && correctionForm.responsibleAccountantUserId > 0;
    if (!valid) { setFormError(t("finance.validation.correction")); return; }
    void runMutation(() => adminApi.createAccountingCorrection({
      ...correctionForm,
      reasonCode: correctionForm.reasonCode.trim(),
      note: correctionForm.note.trim() || null,
    }), "finance.success.correctionCreated");
  };

  const openAction = (target: ActionTarget) => { setFormError(null); setReason(""); setAction(target); };
  const executeAction = () => {
    if (!action) return;
    const reasonValue = reason.trim() || undefined;
    const reasonRequired = ["paymentReject", "paymentCancel", "periodClose", "correctionReject", "correctionReverse"].includes(action.kind);
    if (reasonRequired && (reasonValue?.length ?? 0) < 3) { setFormError(t("finance.validation.reason")); return; }
    const operations: Record<ActionKind, () => Promise<unknown>> = {
      paymentApprove: () => adminApi.decidePaymentRequest(action.id, true, action.rowVersion, reasonValue),
      paymentReject: () => adminApi.decidePaymentRequest(action.id, false, action.rowVersion, reasonValue),
      paymentPay: () => adminApi.payPaymentRequest(action.id, action.rowVersion, reasonValue),
      paymentCancel: () => adminApi.cancelPaymentRequest(action.id, action.rowVersion, reasonValue),
      periodClose: () => adminApi.closeAccountingPeriod(action.id, action.rowVersion, reasonValue),
      correctionApprove: () => adminApi.decideAccountingCorrection(action.id, true, action.rowVersion, reasonValue),
      correctionReject: () => adminApi.decideAccountingCorrection(action.id, false, action.rowVersion, reasonValue),
      correctionReverse: () => adminApi.reverseAccountingCorrection(action.id, action.rowVersion, reasonValue ?? ""),
    };
    void runMutation(operations[action.kind], `finance.success.${action.kind}`, false);
  };

  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;

  return (
    <AdminLayout>
      <div className="space-y-5 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <div className="flex items-center gap-2 text-primary"><Landmark className="h-5 w-5" /><span className="text-xs font-semibold uppercase">{t("nav.finance")}</span></div>
            <h1 className="mt-1 text-2xl font-semibold">{t("finance.title")}</h1>
            <p className="mt-1 text-sm text-muted-foreground">{t("finance.subtitle")}</p>
          </div>
          <Button variant="outline" size="icon" title={t("finance.refresh")} aria-label={t("finance.refresh")} onClick={() => void loadFinance()} disabled={busy}>
            <RefreshCw className={busy ? "h-4 w-4 animate-spin" : "h-4 w-4"} />
          </Button>
        </header>

        {error ? <PageError message={error} onRetry={() => void loadFinance()} /> : (
          <Tabs defaultValue={defaultTab} className="space-y-4">
            <div className="overflow-x-auto pb-1">
              <TabsList className="h-auto w-max min-w-full justify-start">
                {canViewPayments && <TabsTrigger value="payments"><CircleDollarSign className="mr-2 h-4 w-4" />{t("finance.tabs.payments")} ({payments.length})</TabsTrigger>}
                {canViewPeriods && <TabsTrigger value="periods"><CalendarClock className="mr-2 h-4 w-4" />{t("finance.tabs.periods")} ({periods.length})</TabsTrigger>}
                {canViewCorrections && <TabsTrigger value="corrections"><FileClock className="mr-2 h-4 w-4" />{t("finance.tabs.corrections")} ({corrections.length})</TabsTrigger>}
              </TabsList>
            </div>
            {canViewPayments && (
              <TabsContent value="payments">
                <PaymentsPanel rows={payments} canManage={canManagePayments} canApprove={canApprovePayments} canPay={canPayPayments} currentUserId={currentUserId} busy={busy} status={status} money={money} formatDate={formatDate} t={t} onCreate={() => openCreate("payment")} onSubmit={(row) => void runMutation(() => adminApi.submitPaymentRequest(row.id, row.rowVersion), "finance.success.paymentSubmitted", false)} onValidate={(row) => void runMutation(() => adminApi.validatePaymentRequest(row.id, row.rowVersion), "finance.success.paymentValidated", false)} onAction={openAction} />
              </TabsContent>
            )}
            {canViewPeriods && (
              <TabsContent value="periods">
                <PeriodsPanel rows={periods} canClose={canClosePeriods} busy={busy} status={status} formatDate={formatDate} t={t} onCreate={() => openCreate("period")} onStart={(row) => void runMutation(() => adminApi.startClosingAccountingPeriod(row.id, row.rowVersion), "finance.success.periodClosing", false)} onClose={(row) => openAction({ kind: "periodClose", id: row.id, rowVersion: row.rowVersion })} />
              </TabsContent>
            )}
            {canViewCorrections && (
              <TabsContent value="corrections">
                <CorrectionsPanel rows={corrections} canManage={canManageCorrections} canApprove={canApproveCorrections} canReverse={canReverseCorrections} currentUserId={currentUserId} busy={busy} status={status} money={money} formatDate={formatDate} t={t} onCreate={() => openCreate("correction")} onSubmit={(row) => void runMutation(() => adminApi.submitAccountingCorrection(row.id, row.rowVersion), "finance.success.correctionSubmitted", false)} onAction={openAction} />
              </TabsContent>
            )}
          </Tabs>
        )}
      </div>

      <Dialog open={dialog != null} onOpenChange={(open) => { if (!open && !busy) setDialog(null); }}>
        <DialogContent className="max-h-[92vh] w-[95vw] max-w-3xl overflow-y-auto">
          <DialogHeader><DialogTitle>{dialog ? t(`finance.dialog.${dialog}.title`) : ""}</DialogTitle><DialogDescription>{dialog ? t(`finance.dialog.${dialog}.description`) : ""}</DialogDescription></DialogHeader>
          {dialog === "payment" && (paymentReferencesError
            ? <PageError message={paymentReferencesError} onRetry={() => setPaymentReferencesRetry(value => value + 1)} />
            : !paymentReferences ? <PageLoading />
            : !paymentReferences.contracts.length || !paymentReferences.accountants.length ? <PageEmpty message={t("finance.payments.referencesEmpty")} />
            : <PaymentForm form={paymentForm} setForm={setPaymentForm} contracts={paymentReferences.contracts} vendors={paymentReferences.vendors} accountants={paymentReferences.accountants} t={t} />)}
          {dialog === "period" && <PeriodForm form={periodForm} setForm={setPeriodForm} t={t} />}
          {dialog === "correction" && <CorrectionForm form={correctionForm} setForm={setCorrectionForm} periods={periods} projects={projects} contracts={contracts} payments={payments} accountants={accountants} lookupAvailable={lookupAvailable} t={t} />}
          {formError && <p className="text-sm text-destructive" role="alert">{formError}</p>}
          <DialogFooter><Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>{t("common.cancel")}</Button><Button onClick={dialog === "payment" ? createPayment : dialog === "period" ? createPeriod : createCorrection} disabled={busy || (dialog === "payment" && (!paymentReferences?.contracts.length || !paymentReferences.accountants.length))}>{busy ? t("common.saving") : t("finance.action.create")}</Button></DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={action != null} onOpenChange={(open) => { if (!open && !busy) setAction(null); }}>
        <DialogContent>
          <DialogHeader><DialogTitle>{action ? t(`finance.actionDialog.${action.kind}.title`) : ""}</DialogTitle><DialogDescription>{t("finance.actionDialog.description")}</DialogDescription></DialogHeader>
          <div className="space-y-2"><Label htmlFor="finance-action-reason">{t("finance.field.reason")}</Label><Textarea id="finance-action-reason" maxLength={2000} value={reason} onChange={(event) => setReason(event.target.value)} placeholder={t("finance.actionDialog.reasonPlaceholder")} /></div>
          {formError && <p className="text-sm text-destructive" role="alert">{formError}</p>}
          <DialogFooter><Button variant="outline" onClick={() => setAction(null)} disabled={busy}>{t("common.cancel")}</Button><Button variant={action && ["paymentReject", "paymentCancel", "correctionReject", "correctionReverse"].includes(action.kind) ? "destructive" : "default"} onClick={executeAction} disabled={busy}>{action ? t(`finance.action.${action.kind}`) : ""}</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

const PanelHeader = ({ title, description, action }: { title: string; description: string; action?: ReactNode }) => <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><h2 className="text-lg font-semibold">{title}</h2><p className="text-sm text-muted-foreground">{description}</p></div>{action}</div>;
const Data = ({ label, children }: { label: string; children: ReactNode }) => <div className="min-w-0"><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-0.5 break-words text-sm">{children}</dd></div>;
const ActionButton = ({ icon, children, ...props }: React.ComponentProps<typeof Button> & { icon: ReactNode }) => <Button size="sm" {...props}>{icon}{children}</Button>;

const PaymentsPanel = ({ rows, canManage, canApprove, canPay, currentUserId, busy, status, money, formatDate, t, onCreate, onSubmit, onValidate, onAction }: { rows: PaymentRequestResponse[]; canManage: boolean; canApprove: boolean; canPay: boolean; currentUserId?: number; busy: boolean; status: (value: string) => ReactNode; money: Intl.NumberFormat; formatDate: (value?: string | null) => string; t: Translate; onCreate: () => void; onSubmit: (row: PaymentRequestResponse) => void; onValidate: (row: PaymentRequestResponse) => void; onAction: (target: ActionTarget) => void }) => <section><PanelHeader title={t("finance.payments.title")} description={t("finance.payments.description")} action={canManage && <Button onClick={onCreate}><Plus className="mr-2 h-4 w-4" />{t("finance.payments.create")}</Button>} />{rows.length === 0 ? <PageEmpty message={t("finance.payments.empty")} /> : <div className="grid gap-4 xl:grid-cols-2">{rows.map((row) => <article className="rounded-md border p-4" key={row.id}><div className="flex items-start justify-between gap-3"><div><h3 className="font-semibold">{row.code}</h3><p className="text-sm text-muted-foreground">{row.supplierInvoiceNumber}</p></div>{status(row.status)}</div><dl className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3"><Data label={t("finance.field.contract")}>{row.contractNumber}</Data><Data label={t("finance.field.vendor")}>{row.vendorName}</Data><Data label={t("finance.field.amount")}>{money.format(row.invoiceAmount)} {row.currency}</Data><Data label={t("finance.field.invoiceDate")}>{formatDate(row.invoiceDate)}</Data><Data label={t("finance.field.receivedAt")}>{formatDate(row.receivedAt)}</Data><Data label={t("finance.field.accountant")}>{row.assignedAccountantName ?? `#${row.assignedAccountantUserId}`}</Data></dl>{row.attachments.length > 0 && <div className="mt-4 border-t pt-3"><p className="text-xs font-medium uppercase text-muted-foreground">{t("finance.field.attachments")}</p><div className="mt-2 flex flex-wrap gap-2">{row.attachments.map((item) => <a className="text-sm text-primary underline-offset-4 hover:underline" href={item.filePath} target="_blank" rel="noreferrer" key={item.id}>{item.fileName}</a>)}</div></div>}<details className="mt-4 border-t pt-3"><summary className="cursor-pointer text-sm font-medium">{t("finance.field.timeline")} ({row.events.length})</summary><ol className="mt-3 space-y-3 border-l pl-4">{row.events.map((event) => <li key={event.id}><div className="flex flex-wrap items-center gap-2">{event.fromStatus && <span className="text-xs text-muted-foreground">{t(`finance.status.${event.fromStatus}`)} {"->"}</span>}{status(event.toStatus)}</div><p className="mt-1 text-xs text-muted-foreground">{event.changedByName ?? `#${event.changedByUserId}`} · {formatDate(event.changedAt)}</p>{event.reason && <p className="mt-1 text-sm">{event.reason}</p>}</li>)}</ol></details>{row.decisionReason && <p className="mt-3 border-l-2 border-amber-400 pl-3 text-sm">{row.decisionReason}</p>}<div className="mt-4 flex flex-wrap gap-2 border-t pt-3">{canManage && row.status === "Draft" && <ActionButton variant="outline" disabled={busy} onClick={() => onSubmit(row)} icon={<Send className="mr-2 h-4 w-4" />}>{t("finance.action.submit")}</ActionButton>}{canManage && row.status === "UnderValidation" && row.assignedAccountantUserId === currentUserId && <ActionButton variant="outline" disabled={busy || row.attachments.length === 0} onClick={() => onValidate(row)} icon={<ShieldCheck className="mr-2 h-4 w-4" />}>{t("finance.action.validate")}</ActionButton>}{canApprove && row.status === "ReadyForApproval" && row.assignedAccountantUserId !== currentUserId && <ActionButton disabled={busy} onClick={() => onAction({ kind: "paymentApprove", id: row.id, rowVersion: row.rowVersion })} icon={<Check className="mr-2 h-4 w-4" />}>{t("finance.action.approve")}</ActionButton>}{canApprove && ["UnderValidation", "ReadyForApproval"].includes(row.status) && <ActionButton variant="destructive" disabled={busy} onClick={() => onAction({ kind: "paymentReject", id: row.id, rowVersion: row.rowVersion })} icon={<X className="mr-2 h-4 w-4" />}>{t("finance.action.reject")}</ActionButton>}{canPay && row.status === "Approved" && <ActionButton disabled={busy} onClick={() => onAction({ kind: "paymentPay", id: row.id, rowVersion: row.rowVersion })} icon={<Banknote className="mr-2 h-4 w-4" />}>{t("finance.action.pay")}</ActionButton>}{canApprove && row.status === "Approved" && <ActionButton variant="outline" disabled={busy} onClick={() => onAction({ kind: "paymentCancel", id: row.id, rowVersion: row.rowVersion })} icon={<X className="mr-2 h-4 w-4" />}>{t("finance.action.cancelPayment")}</ActionButton>}</div></article>)}</div>}</section>;

const PeriodsPanel = ({ rows, canClose, busy, status, formatDate, t, onCreate, onStart, onClose }: { rows: AccountingPeriodResponse[]; canClose: boolean; busy: boolean; status: (value: string) => ReactNode; formatDate: (value?: string | null) => string; t: Translate; onCreate: () => void; onStart: (row: AccountingPeriodResponse) => void; onClose: (row: AccountingPeriodResponse) => void }) => <section><PanelHeader title={t("finance.periods.title")} description={t("finance.periods.description")} action={canClose && <Button onClick={onCreate}><Plus className="mr-2 h-4 w-4" />{t("finance.periods.create")}</Button>} />{rows.length === 0 ? <PageEmpty message={t("finance.periods.empty")} /> : <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{rows.map((row) => <article className="rounded-md border p-4" key={row.id}><div className="flex items-start justify-between gap-3"><h3 className="font-semibold">{String(row.month).padStart(2, "0")}/{row.year}</h3>{status(row.status)}</div><dl className="mt-4 grid grid-cols-2 gap-3"><Data label={t("finance.field.periodStart")}>{formatDate(row.periodStartUtc)}</Data><Data label={t("finance.field.periodEnd")}>{formatDate(row.periodEndUtc)}</Data><Data label={t("finance.field.closingAt")}>{formatDate(row.closingAt)}</Data><Data label={t("finance.field.closedAt")}>{formatDate(row.closedAt)}</Data></dl>{row.closeReason && <p className="mt-3 border-l-2 border-slate-400 pl-3 text-sm">{row.closeReason}</p>}<div className="mt-4 flex flex-wrap gap-2 border-t pt-3">{canClose && row.status === "Open" && <ActionButton variant="outline" disabled={busy} onClick={() => onStart(row)} icon={<CalendarClock className="mr-2 h-4 w-4" />}>{t("finance.action.startClosing")}</ActionButton>}{canClose && row.status === "Closing" && <ActionButton disabled={busy} onClick={() => onClose(row)} icon={<ShieldCheck className="mr-2 h-4 w-4" />}>{t("finance.action.closePeriod")}</ActionButton>}</div></article>)}</div>}</section>;

const CorrectionsPanel = ({ rows, canManage, canApprove, canReverse, currentUserId, busy, status, money, formatDate, t, onCreate, onSubmit, onAction }: { rows: AccountingCorrectionResponse[]; canManage: boolean; canApprove: boolean; canReverse: boolean; currentUserId?: number; busy: boolean; status: (value: string) => ReactNode; money: Intl.NumberFormat; formatDate: (value?: string | null) => string; t: Translate; onCreate: () => void; onSubmit: (row: AccountingCorrectionResponse) => void; onAction: (target: ActionTarget) => void }) => <section><PanelHeader title={t("finance.corrections.title")} description={t("finance.corrections.description")} action={canManage && <Button onClick={onCreate}><Plus className="mr-2 h-4 w-4" />{t("finance.corrections.create")}</Button>} />{rows.length === 0 ? <PageEmpty message={t("finance.corrections.empty")} /> : <div className="grid gap-4 xl:grid-cols-2">{rows.map((row) => <article className="rounded-md border p-4" key={row.id}><div className="flex items-start justify-between gap-3"><div><h3 className="font-semibold">{row.code}</h3><p className="text-sm text-muted-foreground">{row.projectName} · {row.periodLabel}</p></div>{status(row.status)}</div><dl className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3"><Data label={t("finance.field.source")}>{row.sourceEntityType} #{row.sourceEntityId}</Data><Data label={t("finance.field.reasonCode")}>{row.reasonCode}</Data><Data label={t("finance.field.accountant")}>{row.responsibleAccountantName ?? `#${row.responsibleAccountantUserId}`}</Data><Data label={t("finance.field.originalValue")}>{money.format(row.originalValue)} {row.currency}</Data><Data label={t("finance.field.correctedValue")}>{money.format(row.correctedValue)} {row.currency}</Data><Data label={t("finance.field.delta")}>{money.format(row.correctedValue - row.originalValue)} {row.currency}</Data></dl>{row.note && <p className="mt-3 text-sm">{row.note}</p>}<details className="mt-4 border-t pt-3"><summary className="cursor-pointer text-sm font-medium">{t("finance.field.timeline")}</summary><ol className="mt-3 space-y-2 border-l pl-4 text-sm"><li>{t("finance.status.Draft")}</li>{row.submittedAt && <li>{t("finance.status.Submitted")} · {formatDate(row.submittedAt)}</li>}{row.approvedAt && <li>{t("finance.status.Approved")} · {formatDate(row.approvedAt)}</li>}{row.rejectedAt && <li>{t("finance.status.Rejected")} · {formatDate(row.rejectedAt)}</li>}{row.reversedAt && <li>{t("finance.status.Reversed")} · {formatDate(row.reversedAt)}</li>}</ol></details>{row.decisionReason && <p className="mt-3 border-l-2 border-amber-400 pl-3 text-sm">{row.decisionReason}</p>}<div className="mt-4 flex flex-wrap gap-2 border-t pt-3">{canManage && row.status === "Draft" && !row.reversalOfCorrectionId && <ActionButton variant="outline" disabled={busy} onClick={() => onSubmit(row)} icon={<Send className="mr-2 h-4 w-4" />}>{t("finance.action.submit")}</ActionButton>}{canApprove && row.status === "Submitted" && row.responsibleAccountantUserId !== currentUserId && <><ActionButton disabled={busy} onClick={() => onAction({ kind: "correctionApprove", id: row.id, rowVersion: row.rowVersion })} icon={<Check className="mr-2 h-4 w-4" />}>{t("finance.action.approve")}</ActionButton><ActionButton variant="destructive" disabled={busy} onClick={() => onAction({ kind: "correctionReject", id: row.id, rowVersion: row.rowVersion })} icon={<X className="mr-2 h-4 w-4" />}>{t("finance.action.reject")}</ActionButton></>}{canReverse && row.status === "Approved" && !row.reversalOfCorrectionId && <ActionButton variant="destructive" disabled={busy} onClick={() => onAction({ kind: "correctionReverse", id: row.id, rowVersion: row.rowVersion })} icon={<RotateCcw className="mr-2 h-4 w-4" />}>{t("finance.action.reverse")}</ActionButton>}</div></article>)}</div>}</section>;

const IdChoice = ({ id, label, value, onChange, available, options, placeholder, fallbackPlaceholder }: { id: string; label: string; value: number; onChange: (value: number) => void; available: boolean; options: Array<{ id: number; label: string }>; placeholder: string; fallbackPlaceholder: string }) => <div className="space-y-2"><Label htmlFor={id}>{label}</Label>{available ? <Select value={value ? String(value) : undefined} onValueChange={(next) => onChange(Number(next))}><SelectTrigger id={id}><SelectValue placeholder={placeholder} /></SelectTrigger><SelectContent>{options.map((option) => <SelectItem key={option.id} value={String(option.id)}>{option.label}</SelectItem>)}</SelectContent></Select> : <Input id={id} type="number" min={1} value={value || ""} onChange={(event) => onChange(Number(event.target.value))} placeholder={fallbackPlaceholder} />}</div>;

const PaymentForm = ({ form, setForm, contracts, vendors, accountants, t }: { form: ReturnType<typeof emptyPayment>; setForm: React.Dispatch<React.SetStateAction<ReturnType<typeof emptyPayment>>>; contracts: PaymentReferencesResponse["contracts"]; vendors: PaymentReferencesResponse["vendors"]; accountants: PaymentReferencesResponse["accountants"]; t: Translate }) => {
  const selectedContract = contracts.find((contract) => contract.id === form.contractId);
  const vendorOptions = selectedContract?.vendorId ? vendors.filter((vendor) => vendor.id === selectedContract.vendorId) : vendors;
  return <div className="space-y-5"><div className="grid gap-4 sm:grid-cols-2"><IdChoice id="finance-contract" label={t("finance.field.contract")} value={form.contractId} available={true} options={contracts.map((contract) => ({ id: contract.id, label: `${contract.contractNumber}${contract.vendorName ? ` · ${contract.vendorName}` : ""}` }))} placeholder={t("finance.lookup.contract")} fallbackPlaceholder={t("finance.lookup.contractId")} onChange={(value) => { const contract = contracts.find((item) => item.id === value); setForm((current) => ({ ...current, contractId: value, vendorId: contract?.vendorId ?? 0, contractPaymentMilestoneId: 0 })); }} /><IdChoice id="finance-vendor" label={t("finance.field.vendor")} value={form.vendorId} available={true} options={vendorOptions.map((vendor) => ({ id: vendor.id, label: `${vendor.vendorCode} · ${vendor.companyName}` }))} placeholder={t("finance.lookup.vendor")} fallbackPlaceholder={t("finance.lookup.vendorId")} onChange={(value) => setForm((current) => ({ ...current, vendorId: value }))} />{selectedContract?.paymentMilestones.length ? <div className="space-y-2"><Label htmlFor="finance-milestone">{t("finance.field.milestone")}</Label><Select value={form.contractPaymentMilestoneId ? String(form.contractPaymentMilestoneId) : "none"} onValueChange={(value) => setForm((current) => ({ ...current, contractPaymentMilestoneId: value === "none" ? 0 : Number(value) }))}><SelectTrigger id="finance-milestone"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">{t("finance.lookup.noMilestone")}</SelectItem>{selectedContract.paymentMilestones.map((item) => <SelectItem key={item.id} value={String(item.id)}>{item.order}. {item.name}</SelectItem>)}</SelectContent></Select></div> : null}<div className="space-y-2"><Label htmlFor="finance-invoice-number">{t("finance.field.invoiceNumber")}</Label><Input id="finance-invoice-number" maxLength={120} value={form.supplierInvoiceNumber} onChange={(event) => setForm((current) => ({ ...current, supplierInvoiceNumber: event.target.value }))} /></div><div className="space-y-2"><Label htmlFor="finance-invoice-date">{t("finance.field.invoiceDate")}</Label><Input id="finance-invoice-date" type="date" value={form.invoiceDate} onChange={(event) => setForm((current) => ({ ...current, invoiceDate: event.target.value }))} /></div><div className="space-y-2"><Label htmlFor="finance-invoice-amount">{t("finance.field.amount")}</Label><Input id="finance-invoice-amount" type="number" min={0.01} step="any" value={form.invoiceAmount || ""} onChange={(event) => setForm((current) => ({ ...current, invoiceAmount: Number(event.target.value) }))} /></div><div className="space-y-2"><Label htmlFor="finance-currency">{t("finance.field.currency")}</Label><Input id="finance-currency" maxLength={3} value={form.currency} onChange={(event) => setForm((current) => ({ ...current, currency: event.target.value.toUpperCase() }))} /></div><div className="space-y-2"><Label htmlFor="finance-received">{t("finance.field.receivedAt")}</Label><Input id="finance-received" type="datetime-local" value={form.receivedAt} onChange={(event) => setForm((current) => ({ ...current, receivedAt: event.target.value }))} /></div><IdChoice id="finance-accountant" label={t("finance.field.accountant")} value={form.assignedAccountantUserId} available={true} options={accountants.map((user) => ({ id: user.id, label: user.fullName ?? `#${user.id}` }))} placeholder={t("finance.lookup.accountant")} fallbackPlaceholder={t("finance.lookup.userId")} onChange={(value) => setForm((current) => ({ ...current, assignedAccountantUserId: value }))} /></div><div className="space-y-3"><div className="flex items-center justify-between gap-3"><div><Label>{t("finance.field.attachments")}</Label><p className="text-xs text-muted-foreground">{t("finance.attachment.help")}</p></div><Button type="button" variant="outline" size="sm" disabled={form.attachments.length >= 20} onClick={() => setForm((current) => ({ ...current, attachments: [...current.attachments, emptyAttachment()] }))}><Plus className="mr-2 h-4 w-4" />{t("finance.action.addAttachment")}</Button></div>{form.attachments.map((attachment, index) => <div className="grid gap-3 border-t pt-3 sm:grid-cols-[1fr_2fr_auto]" key={index}><div className="space-y-2"><Label>{t("finance.field.fileName")}</Label><Input maxLength={260} value={attachment.fileName} onChange={(event) => setForm((current) => ({ ...current, attachments: current.attachments.map((item, itemIndex) => itemIndex === index ? { ...item, fileName: event.target.value } : item) }))} /></div><div className="space-y-2"><Label>{t("finance.field.filePath")}</Label><Input maxLength={500} value={attachment.filePath} placeholder="/documents/invoice.pdf" onChange={(event) => setForm((current) => ({ ...current, attachments: current.attachments.map((item, itemIndex) => itemIndex === index ? { ...item, filePath: event.target.value } : item) }))} /></div><Button className="self-end" type="button" variant="ghost" size="icon" aria-label={t("finance.action.removeAttachment")} onClick={() => setForm((current) => ({ ...current, attachments: current.attachments.filter((_, itemIndex) => itemIndex !== index) }))}><X className="h-4 w-4" /></Button></div>)}</div></div>;
};

const PeriodForm = ({ form, setForm, t }: { form: { year: number; month: number }; setForm: React.Dispatch<React.SetStateAction<{ year: number; month: number }>>; t: Translate }) => <div className="grid gap-4 sm:grid-cols-2"><div className="space-y-2"><Label htmlFor="finance-period-year">{t("finance.field.year")}</Label><Input id="finance-period-year" type="number" min={2000} max={9999} value={form.year} onChange={(event) => setForm((current) => ({ ...current, year: Number(event.target.value) }))} /></div><div className="space-y-2"><Label htmlFor="finance-period-month">{t("finance.field.month")}</Label><Input id="finance-period-month" type="number" min={1} max={12} value={form.month} onChange={(event) => setForm((current) => ({ ...current, month: Number(event.target.value) }))} /></div></div>;

const CorrectionForm = ({ form, setForm, periods, projects, contracts, payments, accountants, lookupAvailable, t }: { form: ReturnType<typeof emptyCorrection>; setForm: React.Dispatch<React.SetStateAction<ReturnType<typeof emptyCorrection>>>; periods: AccountingPeriodResponse[]; projects: OperationalProjectListItemResponse[]; contracts: ContractResponse[]; payments: PaymentRequestResponse[]; accountants: UserOption[]; lookupAvailable: { projects: boolean; users: boolean }; t: Translate }) => {
  const projectContracts = contracts.filter((contract) => contract.operationalProjectId === form.operationalProjectId);
  const sourceOptions = form.sourceEntityType === "Contract"
    ? projectContracts.map((contract) => ({ id: contract.id, label: contract.contractNumber }))
    : form.sourceEntityType === "ContractPaymentMilestone"
      ? projectContracts.flatMap((contract) => contract.paymentMilestones.map((milestone) => ({ id: milestone.id, label: `${contract.contractNumber} · ${milestone.name}` })))
      : payments.filter((payment) => payment.status === "Paid" && projectContracts.some((contract) => contract.id === payment.contractId)).map((payment) => ({ id: payment.id, label: `${payment.code} · ${payment.supplierInvoiceNumber}` }));
  return <div className="grid gap-4 sm:grid-cols-2"><IdChoice id="finance-project" label={t("finance.field.project")} value={form.operationalProjectId} available={lookupAvailable.projects} options={projects.map((project) => ({ id: project.id, label: `${project.code} · ${project.name}` }))} placeholder={t("finance.lookup.project")} fallbackPlaceholder={t("finance.lookup.projectId")} onChange={(value) => setForm((current) => ({ ...current, operationalProjectId: value, sourceEntityId: 0 }))} /><IdChoice id="finance-closed-period" label={t("finance.field.period")} value={form.accountingPeriodId} available={periods.length > 0} options={periods.filter((period) => period.status === "Closed").map((period) => ({ id: period.id, label: `${String(period.month).padStart(2, "0")}/${period.year}` }))} placeholder={t("finance.lookup.closedPeriod")} fallbackPlaceholder={t("finance.lookup.periodId")} onChange={(value) => setForm((current) => ({ ...current, accountingPeriodId: value }))} /><div className="space-y-2"><Label htmlFor="finance-source-type">{t("finance.field.sourceType")}</Label><Select value={form.sourceEntityType} onValueChange={(value) => setForm((current) => ({ ...current, sourceEntityType: value, sourceEntityId: 0 }))}><SelectTrigger id="finance-source-type"><SelectValue /></SelectTrigger><SelectContent>{["PaymentRequest", "ContractPaymentMilestone", "Contract"].map((value) => <SelectItem key={value} value={value}>{t(`finance.source.${value}`)}</SelectItem>)}</SelectContent></Select></div><IdChoice id="finance-source-id" label={t("finance.field.source")} value={form.sourceEntityId} available={sourceOptions.length > 0} options={sourceOptions} placeholder={t("finance.lookup.source")} fallbackPlaceholder={t("finance.lookup.sourceId")} onChange={(value) => setForm((current) => ({ ...current, sourceEntityId: value }))} /><div className="space-y-2"><Label htmlFor="finance-reason-code">{t("finance.field.reasonCode")}</Label><Input id="finance-reason-code" maxLength={80} value={form.reasonCode} onChange={(event) => setForm((current) => ({ ...current, reasonCode: event.target.value.toUpperCase() }))} placeholder="AMOUNT_ERROR" /></div><IdChoice id="finance-responsible-accountant" label={t("finance.field.accountant")} value={form.responsibleAccountantUserId} available={lookupAvailable.users} options={accountants.map((user) => ({ id: user.id, label: user.fullName ?? user.email ?? `#${user.id}` }))} placeholder={t("finance.lookup.accountant")} fallbackPlaceholder={t("finance.lookup.userId")} onChange={(value) => setForm((current) => ({ ...current, responsibleAccountantUserId: value }))} /><div className="space-y-2"><Label htmlFor="finance-original-value">{t("finance.field.originalValue")}</Label><Input id="finance-original-value" type="number" step="any" value={form.originalValue} onChange={(event) => setForm((current) => ({ ...current, originalValue: Number(event.target.value) }))} /></div><div className="space-y-2"><Label htmlFor="finance-corrected-value">{t("finance.field.correctedValue")}</Label><Input id="finance-corrected-value" type="number" step="any" value={form.correctedValue} onChange={(event) => setForm((current) => ({ ...current, correctedValue: Number(event.target.value) }))} /></div><div className="space-y-2"><Label htmlFor="finance-correction-currency">{t("finance.field.currency")}</Label><Input id="finance-correction-currency" maxLength={3} value={form.currency} onChange={(event) => setForm((current) => ({ ...current, currency: event.target.value.toUpperCase() }))} /></div><div className="space-y-2 sm:col-span-2"><Label htmlFor="finance-correction-note">{t("finance.field.note")}</Label><Textarea id="finance-correction-note" maxLength={4000} value={form.note} onChange={(event) => setForm((current) => ({ ...current, note: event.target.value }))} /></div></div>;
};

export default FinanceControlPage;