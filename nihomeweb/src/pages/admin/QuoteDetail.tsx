import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  ArrowLeft,
  Ban,
  Calendar,
  CheckCheck,
  Clipboard,
  History,
  ListChecks,
  Loader2,
  Pencil,
  Plus,
  Save,
  Send,
  ThumbsDown,
  ThumbsUp,
  Trash2,
  XCircle,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { formatVnd, parseVnd } from "@/lib/numberFormat";
import { PageLoading, PageError } from "@/components/PageState";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  adminApi,
  type QuoteItemInput,
  type QuoteResponse,
  type QuoteStatus,
  type QuoteVersionsResponse,
  type UpdateQuoteRequest,
} from "@/services/adminApi";

const STATUS_STYLES: Record<QuoteStatus, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  PendingApproval: "border-amber-200 bg-amber-50 text-amber-700",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  SentToCustomer: "border-sky-200 bg-sky-50 text-sky-700",
  CustomerApproved: "border-green-300 bg-green-100 text-green-800",
  Rejected: "border-rose-200 bg-rose-50 text-rose-700",
  Expired: "border-orange-200 bg-orange-50 text-orange-700",
  Cancelled: "border-zinc-200 bg-zinc-100 text-zinc-600",
};

type WorkflowKind =
  | "submit"
  | "approve"
  | "rejectInternal"
  | "send"
  | "customerApprove"
  | "customerReject"
  | "cancel";

/**
 * Set of workflow actions to expose given the current status. Delete lives
 * outside because it is destructive and only makes sense for Draft.
 */
const WORKFLOW_BY_STATUS: Record<QuoteStatus, WorkflowKind[]> = {
  Draft: ["submit"],
  PendingApproval: ["approve", "rejectInternal", "cancel"],
  Approved: ["send", "cancel"],
  SentToCustomer: ["customerApprove", "customerReject", "cancel"],
  CustomerApproved: [],
  Rejected: [],
  Expired: ["cancel"],
  Cancelled: [],
};

/**
 * Whether a Draft item edit should hit the API. When status is one of these
 * the update route spawns a new version server-side (spec NIH-84 AC #4).
 */
function toFormState(q: QuoteResponse): UpdateQuoteRequest {
  return {
    ownerUserId: q.ownerUserId ?? null,
    areaSqm: q.areaSqm ?? null,
    unitPricePerSqm: q.unitPricePerSqm ?? null,
    packageDescription: q.packageDescription ?? "",
    items: q.items.map((i) => ({
      itemCode: i.itemCode ?? null,
      name: i.name,
      unit: i.unit,
      quantity: i.quantity,
      unitPrice: i.unitPrice,
      sortOrder: i.sortOrder,
    })),
    discountPercent: q.discountPercent,
    vatPercent: q.vatPercent,
    validUntil: q.validUntil,
    note: q.note ?? "",
  };
}

/** Server-side rounding: match the QuoteService implementation. */
function computePreview(
  method: "UnitCost" | "Boq",
  form: UpdateQuoteRequest,
): { subtotal: number; grandTotal: number } {
  const subtotal =
    method === "Boq"
      ? (form.items ?? []).reduce(
          (s, i) => s + Math.round(i.quantity * i.unitPrice * 100) / 100,
          0,
        )
      : Math.round(((form.areaSqm ?? 0) * (form.unitPricePerSqm ?? 0)) * 100) /
        100;
  const afterDiscount = subtotal * (1 - form.discountPercent / 100);
  const vat = afterDiscount * (form.vatPercent / 100);
  const grandTotal = Math.round((afterDiscount + vat) * 100) / 100;
  return { subtotal: Math.round(subtotal * 100) / 100, grandTotal };
}

// -------- Component --------

const AdminQuoteDetail = () => {
  const { id } = useParams();
  const quoteId = Number(id);
  const navigate = useNavigate();
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();

  const canManage = has(ADMIN_PERMS.quotesManage);
  const canApprove = has(ADMIN_PERMS.quotesApprove);
  const canSend = has(ADMIN_PERMS.quotesSend);

  const [quote, setQuote] = useState<QuoteResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState<UpdateQuoteRequest | null>(null);
  const [saving, setSaving] = useState(false);

  const [versions, setVersions] = useState<QuoteVersionsResponse | null>(null);
  const [versionsLoading, setVersionsLoading] = useState(false);

  const [workflow, setWorkflow] = useState<WorkflowKind | null>(null);
  const [workflowNote, setWorkflowNote] = useState("");
  const [workflowBusy, setWorkflowBusy] = useState(false);

  // ---------- data load ----------

  const load = useCallback(async () => {
    if (!Number.isFinite(quoteId)) return;
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.getQuote(quoteId);
      setQuote(data);
      setForm(toFormState(data));
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setLoading(false);
    }
  }, [quoteId]);

  useEffect(() => {
    void load();
  }, [load]);

  const loadVersions = useCallback(async () => {
    if (!Number.isFinite(quoteId)) return;
    setVersionsLoading(true);
    try {
      const { data } = await adminApi.getQuoteVersions(quoteId);
      setVersions(data);
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setVersionsLoading(false);
    }
  }, [quoteId, toast, t]);

  // ---------- BOQ helpers ----------

  const addBoqRow = () => {
    if (!form) return;
    const items = [...(form.items ?? [])];
    items.push({
      itemCode: null,
      name: "",
      unit: "",
      quantity: 0,
      unitPrice: 0,
      sortOrder: items.length + 1,
    });
    setForm({ ...form, items });
  };
  const removeBoqRow = (idx: number) => {
    if (!form) return;
    const items = [...(form.items ?? [])];
    items.splice(idx, 1);
    setForm({ ...form, items });
  };
  const updateBoqRow = (idx: number, patch: Partial<QuoteItemInput>) => {
    if (!form) return;
    const items = [...(form.items ?? [])];
    items[idx] = { ...items[idx], ...patch };
    setForm({ ...form, items });
  };
  const pasteBoqFromClipboard = async () => {
    if (!form) return;
    try {
      const text = await navigator.clipboard.readText();
      const parsed = parseTsvBoq(text);
      if (parsed.length === 0) {
        toast({
          title: t("quotes.paste.empty"),
          variant: "destructive",
        });
        return;
      }
      const items = [...(form.items ?? []), ...parsed];
      setForm({ ...form, items });
      toast({
        title: t("quotes.paste.ok"),
        description: `+${parsed.length}`,
      });
    } catch {
      toast({
        title: t("common.error"),
        description: t("quotes.paste.needsPermission"),
        variant: "destructive",
      });
    }
  };

  // ---------- save + workflow ----------

  const handleSave = async () => {
    if (!quote || !form) return;
    setSaving(true);
    try {
      const { data } = await adminApi.updateQuote(quote.id, {
        ...form,
        packageDescription: form.packageDescription?.trim() || undefined,
        note: form.note?.trim() || undefined,
        validUntil: form.validUntil || null,
      });
      setQuote(data);
      setForm(toFormState(data));
      setEditing(false);
      toast({ title: t("quotes.updated") });
      // If we're viewing the Versions tab, refresh it too.
      if (versions) void loadVersions();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setSaving(false);
    }
  };

  const runWorkflow = async () => {
    if (!workflow || !quote) return;
    setWorkflowBusy(true);
    try {
      const body = { note: workflowNote.trim() || undefined };
      const fn: Record<WorkflowKind, () => Promise<{ data: QuoteResponse }>> = {
        submit: () => adminApi.submitQuote(quote.id, body),
        approve: () => adminApi.approveQuote(quote.id, body),
        rejectInternal: () => adminApi.rejectQuoteInternal(quote.id, body),
        send: () => adminApi.sendQuoteToCustomer(quote.id, body),
        customerApprove: () => adminApi.markQuoteCustomerApproved(quote.id, body),
        customerReject: () => adminApi.markQuoteCustomerRejected(quote.id, body),
        cancel: () => adminApi.cancelQuote(quote.id, body),
      };
      const { data } = await fn[workflow]();
      setQuote(data);
      setForm(toFormState(data));
      setWorkflow(null);
      setWorkflowNote("");
      toast({ title: t(`quotes.action.${workflow}`) });
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setWorkflowBusy(false);
    }
  };

  const handleDelete = async () => {
    if (!quote) return;
    if (!window.confirm(t("form.confirmDelete"))) return;
    try {
      await adminApi.deleteQuote(quote.id);
      toast({ title: t("quotes.updated") });
      navigate("/admin/quotes");
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    }
  };

  const preview = useMemo(() => {
    if (!quote || !form) return null;
    return computePreview(quote.method, form);
  }, [quote, form]);

  // -------- render --------

  if (!Number.isFinite(quoteId)) return <AdminLayout><PageError message="Invalid id" /></AdminLayout>;
  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (error) return <AdminLayout><PageError message={error} onRetry={() => void load()} /></AdminLayout>;
  if (!quote || !form) return null;

  const workflowKinds = WORKFLOW_BY_STATUS[quote.status];
  const showEditToggle =
    canManage && !editing && quote.status !== "Cancelled" && quote.status !== "Rejected";
  const canDelete = canManage && quote.status === "Draft";

  return (
    <AdminLayout>
      {/* ---------- Header ---------- */}
      <div className="mb-3 flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="mb-1 flex items-center gap-2 text-sm text-muted-foreground">
            <Button variant="ghost" size="sm" asChild className="h-7 px-2">
              <Link to="/admin/quotes">
                <ArrowLeft className="h-4 w-4" />
              </Link>
            </Button>
            <span>
              {t("quotes.field.opportunity")}: {quote.opportunityName ?? "—"} · {quote.customerName ?? "—"}
            </span>
          </div>
          <h1 className="flex flex-wrap items-center gap-3 text-2xl font-semibold tracking-tight">
            {quote.code}
            <Badge variant="outline" className={cn("whitespace-nowrap text-xs", STATUS_STYLES[quote.status])}>
              {t(`quotes.status.${quote.status}`)}
            </Badge>
            <span className="text-sm font-normal text-muted-foreground">V{quote.version}</span>
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            <Calendar className="mr-1 inline h-3.5 w-3.5" />
            {t("quotes.field.validUntil")}: {new Date(quote.validUntil).toLocaleDateString()}
          </p>
        </div>

        <div className="flex flex-wrap gap-1.5">
          {showEditToggle && (
            <Button variant="outline" onClick={() => setEditing(true)}>
              <Pencil className="mr-1.5 h-4 w-4" />
              {t("common.edit")}
            </Button>
          )}
          {editing && (
            <>
              <Button variant="outline" onClick={() => { setForm(toFormState(quote)); setEditing(false); }}>
                {t("common.cancel")}
              </Button>
              <Button onClick={() => void handleSave()} disabled={saving}>
                {saving && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
                <Save className="mr-1.5 h-4 w-4" />
                {t("common.save")}
              </Button>
            </>
          )}
          {!editing &&
            workflowKinds.map((k) => (
              <Button
                key={k}
                variant={k === "rejectInternal" || k === "customerReject" || k === "cancel" ? "outline" : "default"}
                onClick={() => { setWorkflowNote(""); setWorkflow(k); }}
                disabled={
                  (k === "approve" || k === "rejectInternal") ? !canApprove
                  : k === "send" ? !canSend
                  : !canManage
                }
              >
                {workflowIcon(k)}
                {t(`quotes.action.${k}`)}
              </Button>
            ))}
          {!editing && canDelete && (
            <Button variant="outline" className="text-destructive hover:text-destructive" onClick={() => void handleDelete()}>
              <Trash2 className="mr-1.5 h-4 w-4" />
              {t("quotes.action.delete")}
            </Button>
          )}
        </div>
      </div>

      {/* ---------- Body ---------- */}
      <Tabs
        defaultValue="content"
        className="w-full"
        onValueChange={(v) => {
          if (v === "versions" && !versions) void loadVersions();
        }}
      >
        <TabsList>
          <TabsTrigger value="content">
            <ListChecks className="mr-1.5 h-4 w-4" />
            {t("quotes.tab.content")}
          </TabsTrigger>
          <TabsTrigger value="versions">
            <History className="mr-1.5 h-4 w-4" />
            {t("quotes.tab.versions")}
          </TabsTrigger>
          <TabsTrigger value="workflow">
            {t("quotes.tab.workflow")}
          </TabsTrigger>
        </TabsList>

        {/* ---------- CONTENT ---------- */}
        <TabsContent value="content" className="mt-4">
          <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_280px] lg:gap-6 xl:grid-cols-[minmax(0,1fr)_320px]">
            <div className="min-w-0 space-y-4">
              {quote.method === "UnitCost" ? (
                <div className="grid grid-cols-2 gap-3 rounded-lg border bg-card p-4">
                  <FormField label={t("quotes.field.areaSqm")}>
                    <Input
                      inputMode="decimal"
                      value={form.areaSqm ?? ""}
                      disabled={!editing}
                      onChange={(e) => setForm({ ...form, areaSqm: e.target.value ? Number(e.target.value) : null })}
                    />
                  </FormField>
                  <FormField label={t("quotes.field.unitPricePerSqm")}>
                    <Input
                      inputMode="numeric"
                      value={form.unitPricePerSqm ? formatVnd(form.unitPricePerSqm) : ""}
                      disabled={!editing}
                      onChange={(e) => setForm({ ...form, unitPricePerSqm: parseVnd(e.target.value) || null })}
                    />
                  </FormField>
                  <div className="col-span-2">
                    <FormField label={t("quotes.field.packageDescription")}>
                      <Textarea
                        rows={3}
                        value={form.packageDescription ?? ""}
                        disabled={!editing}
                        onChange={(e) => setForm({ ...form, packageDescription: e.target.value })}
                      />
                    </FormField>
                  </div>
                </div>
              ) : (
                <BoqTable
                  form={form}
                  editing={editing}
                  onAdd={addBoqRow}
                  onRemove={removeBoqRow}
                  onChange={updateBoqRow}
                  onPaste={() => void pasteBoqFromClipboard()}
                  t={t}
                />
              )}

              <div className="grid grid-cols-2 gap-3 rounded-lg border bg-card p-4 sm:grid-cols-4">
                <FormField label={t("quotes.field.discountPercent")}>
                  <Input
                    type="number" min={0} max={100}
                    value={form.discountPercent}
                    disabled={!editing}
                    onChange={(e) => setForm({ ...form, discountPercent: Number(e.target.value) })}
                  />
                </FormField>
                <FormField label={t("quotes.field.vatPercent")}>
                  <Input
                    type="number" min={0} max={100}
                    value={form.vatPercent}
                    disabled={!editing}
                    onChange={(e) => setForm({ ...form, vatPercent: Number(e.target.value) })}
                  />
                </FormField>
                <FormField label={t("quotes.field.validUntil")}>
                  <Input
                    type="date"
                    value={form.validUntil?.slice(0, 10) ?? ""}
                    disabled={!editing}
                    onChange={(e) => setForm({ ...form, validUntil: e.target.value ? `${e.target.value}T23:59:59Z` : null })}
                  />
                </FormField>
                <FormField label={t("quotes.field.note")}>
                  <Input
                    value={form.note ?? ""}
                    disabled={!editing}
                    onChange={(e) => setForm({ ...form, note: e.target.value })}
                  />
                </FormField>
              </div>
            </div>

            {/* ---------- Summary side panel ---------- */}
            <aside className="space-y-2 rounded-lg border bg-card p-4 text-sm">
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">{t("quotes.field.subtotal")}</span>
                <span className="font-medium">{formatVnd(preview?.subtotal ?? quote.subtotal)} ₫</span>
              </div>
              <div className="flex items-center justify-between text-muted-foreground">
                <span>{t("quotes.field.discountPercent")}</span>
                <span>{form.discountPercent}%</span>
              </div>
              <div className="flex items-center justify-between text-muted-foreground">
                <span>{t("quotes.field.vatPercent")}</span>
                <span>{form.vatPercent}%</span>
              </div>
              <div className="my-2 border-t" />
              <div className="flex items-center justify-between">
                <span className="font-semibold">{t("quotes.field.grandTotal")}</span>
                <span className="text-lg font-bold text-primary">{formatVnd(preview?.grandTotal ?? quote.grandTotal)} ₫</span>
              </div>
              <p className="break-words text-xs italic text-muted-foreground">
                <span className="mr-1 font-medium not-italic">{t("quotes.field.grandTotalInWords")}:</span>
                {quote.grandTotalInWords}
              </p>
              {editing && preview && preview.grandTotal !== quote.grandTotal && (
                <p className="mt-2 rounded bg-amber-50 p-2 text-[11px] text-amber-800">
                  {t("quotes.preview.unsaved")}
                </p>
              )}
            </aside>
          </div>
        </TabsContent>

        {/* ---------- VERSIONS ---------- */}
        <TabsContent value="versions" className="mt-4">
          {versionsLoading ? (
            <PageLoading />
          ) : versions === null ? (
            <div className="rounded-lg border p-6 text-center text-sm text-muted-foreground">
              {t("common.loading")}...
            </div>
          ) : (
            <div className="space-y-2">
              {versions.versions
                .slice()
                .sort((a, b) => b.version - a.version)
                .map((v) => (
                  <div
                    key={v.version}
                    className={cn(
                      "rounded-lg border p-3 text-sm",
                      v.isCurrent ? "border-primary bg-primary/5" : "bg-card",
                    )}
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2 font-medium">
                        V{v.version}
                        {v.isCurrent && (
                          <Badge variant="outline" className="border-primary text-primary">
                            {t("quotes.version.current")}
                          </Badge>
                        )}
                      </div>
                      <div className="text-right">
                        <div className="font-medium">{formatVnd(v.grandTotal)} ₫</div>
                        <div className="text-xs text-muted-foreground">
                          {new Date(v.capturedAt).toLocaleString()}
                        </div>
                      </div>
                    </div>
                    <div className="mt-1 text-xs text-muted-foreground">
                      {t(`quotes.method.${v.method}`)} · {t("quotes.field.subtotal")} {formatVnd(v.subtotal)} · VAT {v.vatPercent}%
                    </div>
                  </div>
                ))}
            </div>
          )}
        </TabsContent>

        {/* ---------- WORKFLOW LOG ---------- */}
        <TabsContent value="workflow" className="mt-4">
          <div className="rounded-lg border bg-card p-4">
            <ul className="space-y-2 text-sm">
              {quote.approvalLogs.map((l) => (
                <li key={l.id} className="border-b pb-2 last:border-b-0 last:pb-0">
                  <div className="flex items-center justify-between">
                    <span className="font-medium">{l.action}</span>
                    <span className="text-xs text-muted-foreground">
                      {new Date(l.createdAt).toLocaleString()}
                    </span>
                  </div>
                  <div className="text-xs text-muted-foreground">
                    {l.fromStatus ? `${l.fromStatus} → ` : ""}
                    {l.toStatus}
                    {l.byUserName ? ` · ${l.byUserName}` : ""}
                  </div>
                  {l.note && <div className="mt-1 text-xs italic">"{l.note}"</div>}
                </li>
              ))}
              {quote.approvalLogs.length === 0 && (
                <li className="text-center text-muted-foreground">—</li>
              )}
            </ul>
          </div>
        </TabsContent>
      </Tabs>

      {/* ---------- Workflow note dialog ---------- */}
      <Dialog open={!!workflow} onOpenChange={(o) => !o && setWorkflow(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{workflow && t(`quotes.action.${workflow}`)}</DialogTitle>
            <DialogDescription>{t("quotes.action.noteOptional")}</DialogDescription>
          </DialogHeader>
          <Textarea
            rows={4}
            value={workflowNote}
            onChange={(e) => setWorkflowNote(e.target.value)}
            placeholder={t("quotes.field.note")}
          />
          <DialogFooter>
            <Button variant="outline" onClick={() => setWorkflow(null)}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void runWorkflow()} disabled={workflowBusy}>
              {workflowBusy && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

// -------- helpers --------

function workflowIcon(k: WorkflowKind) {
  const cls = "mr-1.5 h-4 w-4";
  switch (k) {
    case "submit": return <ThumbsUp className={cls} />;
    case "approve": return <CheckCheck className={cls} />;
    case "rejectInternal": return <ThumbsDown className={cls} />;
    case "send": return <Send className={cls} />;
    case "customerApprove": return <CheckCheck className={cls} />;
    case "customerReject": return <XCircle className={cls} />;
    case "cancel": return <Ban className={cls} />;
  }
}

function parseTsvBoq(text: string): QuoteItemInput[] {
  // Accept tab- or comma-separated rows in the order
  // (name, unit, quantity, unit_price) — optional 5th col is item_code.
  const rows: QuoteItemInput[] = [];
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trim();
    if (!line) continue;
    const cells = line.split(/\t|,(?![^"]*"[^"]*")/).map((c) => c.trim());
    if (cells.length < 4) continue;
    const [name, unit, qtyStr, priceStr, code] = cells;
    const quantity = parseNumberLoose(qtyStr);
    const unitPrice = parseNumberLoose(priceStr);
    if (!name || !unit || Number.isNaN(quantity) || Number.isNaN(unitPrice)) continue;
    rows.push({
      itemCode: code || null,
      name,
      unit,
      quantity,
      unitPrice,
      sortOrder: rows.length + 1,
    });
  }
  return rows;
}

function parseNumberLoose(s: string): number {
  // Accepts "1.234,56" (vi) or "1,234.56" (en). Strip thousands, keep last comma/dot as decimal.
  if (!s) return NaN;
  const cleaned = s.replace(/[^\d.,-]/g, "");
  // If both separators present, treat the LAST-occurring as the decimal.
  const lastDot = cleaned.lastIndexOf(".");
  const lastComma = cleaned.lastIndexOf(",");
  let normalised = cleaned;
  if (lastDot !== -1 && lastComma !== -1) {
    if (lastDot > lastComma) normalised = cleaned.replace(/,/g, "");
    else normalised = cleaned.replace(/\./g, "").replace(",", ".");
  } else if (lastComma !== -1) {
    normalised = cleaned.replace(",", ".");
  }
  const n = Number(normalised);
  return Number.isFinite(n) ? n : NaN;
}

const FormField = ({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) => (
  <div>
    <Label className="text-xs uppercase tracking-wide text-muted-foreground">{label}</Label>
    {children}
  </div>
);

const BoqTable = ({
  form,
  editing,
  onAdd,
  onRemove,
  onChange,
  onPaste,
  t,
}: {
  form: UpdateQuoteRequest;
  editing: boolean;
  onAdd: () => void;
  onRemove: (idx: number) => void;
  onChange: (idx: number, patch: Partial<QuoteItemInput>) => void;
  onPaste: () => void;
  t: (k: string) => string;
}) => {
  const items = form.items ?? [];
  return (
    <div className="rounded-lg border bg-card">
      <div className="flex items-center justify-between border-b bg-muted/30 p-2 text-xs uppercase tracking-wide text-muted-foreground">
        <span>{t("quotes.boq.title")}</span>
        {editing && (
          <div className="flex gap-1.5">
            <Button size="sm" variant="ghost" className="h-7 px-2" onClick={onPaste}>
              <Clipboard className="mr-1 h-3.5 w-3.5" />
              {t("quotes.boq.paste")}
            </Button>
            <Button size="sm" variant="outline" className="h-7 px-2" onClick={onAdd}>
              <Plus className="mr-1 h-3.5 w-3.5" />
              {t("quotes.boq.addRow")}
            </Button>
          </div>
        )}
      </div>

      {/* Desktop table (md+). */}
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full min-w-[720px] divide-y text-sm">
          <thead className="bg-muted/20 text-xs uppercase text-muted-foreground">
            <tr>
              <th className="w-24 px-2 py-1.5 text-left font-medium">{t("quotes.boq.code")}</th>
              <th className="px-2 py-1.5 text-left font-medium">{t("quotes.boq.name")}</th>
              <th className="w-24 px-2 py-1.5 text-left font-medium">{t("quotes.boq.unit")}</th>
              <th className="w-28 px-2 py-1.5 text-right font-medium">{t("quotes.boq.qty")}</th>
              <th className="w-36 px-2 py-1.5 text-right font-medium">{t("quotes.boq.unitPrice")}</th>
              <th className="w-36 px-2 py-1.5 text-right font-medium">{t("quotes.boq.amount")}</th>
              {editing && <th className="w-10 px-2 py-1.5" />}
            </tr>
          </thead>
          <tbody className="divide-y">
            {items.map((row, idx) => {
              const amount = Math.round(row.quantity * row.unitPrice * 100) / 100;
              return (
                <tr key={idx}>
                  <td className="px-2 py-1">
                    <Input
                      className="h-8"
                      value={row.itemCode ?? ""}
                      disabled={!editing}
                      onChange={(e) => onChange(idx, { itemCode: e.target.value || null })}
                    />
                  </td>
                  <td className="px-2 py-1">
                    <Input
                      className="h-8"
                      value={row.name}
                      disabled={!editing}
                      onChange={(e) => onChange(idx, { name: e.target.value })}
                    />
                  </td>
                  <td className="px-2 py-1">
                    <Input
                      className="h-8"
                      value={row.unit}
                      disabled={!editing}
                      onChange={(e) => onChange(idx, { unit: e.target.value })}
                    />
                  </td>
                  <td className="px-2 py-1">
                    <Input
                      className="h-8 text-right"
                      inputMode="decimal"
                      value={row.quantity}
                      disabled={!editing}
                      onChange={(e) => onChange(idx, { quantity: Number(e.target.value) || 0 })}
                    />
                  </td>
                  <td className="px-2 py-1">
                    <Input
                      className="h-8 text-right"
                      inputMode="numeric"
                      value={row.unitPrice ? formatVnd(row.unitPrice) : ""}
                      disabled={!editing}
                      onChange={(e) => onChange(idx, { unitPrice: parseVnd(e.target.value) || 0 })}
                    />
                  </td>
                  <td className="px-2 py-1 text-right font-medium">{formatVnd(amount)} ₫</td>
                  {editing && (
                    <td className="px-2 py-1 text-right">
                      <Button
                        variant="ghost"
                        size="sm"
                        className="h-7 px-1 text-destructive hover:text-destructive"
                        onClick={() => onRemove(idx)}
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    </td>
                  )}
                </tr>
              );
            })}
            {items.length === 0 && (
              <tr>
                <td colSpan={editing ? 7 : 6} className="px-2 py-6 text-center text-muted-foreground">
                  {t("quotes.validation.boqRequired")}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Mobile card list (<md). Every row becomes a stacked card so no
          horizontal scroll is required on narrow screens. Fields stay
          editable when the parent is in edit mode. */}
      <ul className="divide-y md:hidden">
        {items.length === 0 && (
          <li className="p-4 text-center text-sm text-muted-foreground">
            {t("quotes.validation.boqRequired")}
          </li>
        )}
        {items.map((row, idx) => {
          const amount = Math.round(row.quantity * row.unitPrice * 100) / 100;
          return (
            <li key={idx} className="space-y-2 p-3">
              {editing ? (
                <>
                  <div className="grid grid-cols-[1fr,auto] items-start gap-2">
                    <Input
                      className="h-9 text-sm font-medium"
                      placeholder={t("quotes.boq.name")}
                      value={row.name}
                      onChange={(e) => onChange(idx, { name: e.target.value })}
                    />
                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-9 px-2 text-destructive hover:text-destructive"
                      onClick={() => onRemove(idx)}
                      aria-label={t("quotes.action.delete")}
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                  <div className="grid grid-cols-2 gap-2">
                    <Input
                      className="h-9"
                      placeholder={t("quotes.boq.code")}
                      value={row.itemCode ?? ""}
                      onChange={(e) => onChange(idx, { itemCode: e.target.value || null })}
                    />
                    <Input
                      className="h-9"
                      placeholder={t("quotes.boq.unit")}
                      value={row.unit}
                      onChange={(e) => onChange(idx, { unit: e.target.value })}
                    />
                    <Input
                      className="h-9 text-right"
                      inputMode="decimal"
                      placeholder={t("quotes.boq.qty")}
                      value={row.quantity}
                      onChange={(e) => onChange(idx, { quantity: Number(e.target.value) || 0 })}
                    />
                    <Input
                      className="h-9 text-right"
                      inputMode="numeric"
                      placeholder={t("quotes.boq.unitPrice")}
                      value={row.unitPrice ? formatVnd(row.unitPrice) : ""}
                      onChange={(e) => onChange(idx, { unitPrice: parseVnd(e.target.value) || 0 })}
                    />
                  </div>
                  <div className="flex items-center justify-between rounded bg-muted/30 px-2 py-1 text-sm">
                    <span className="text-muted-foreground">{t("quotes.boq.amount")}</span>
                    <span className="font-semibold">{formatVnd(amount)} ₫</span>
                  </div>
                </>
              ) : (
                <>
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0 flex-1">
                      <div className="truncate text-sm font-medium">{row.name}</div>
                      {row.itemCode && (
                        <div className="text-xs text-muted-foreground">
                          {t("quotes.boq.code")}: {row.itemCode}
                        </div>
                      )}
                    </div>
                    <div className="whitespace-nowrap text-sm font-semibold">
                      {formatVnd(amount)} ₫
                    </div>
                  </div>
                  <div className="text-xs text-muted-foreground">
                    {row.quantity} {row.unit} × {formatVnd(row.unitPrice)} ₫
                  </div>
                </>
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
};

export default AdminQuoteDetail;
