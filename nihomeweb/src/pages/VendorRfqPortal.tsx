import { useEffect, useState, type FormEvent } from "react";
import axios from "axios";
import { CheckCircle2, Download, FileText } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { PageError, PageLoading } from "@/components/PageState";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { calculateRfqCommercialPreview } from "@/lib/rfqCommercial";
import { rfqApi, saveRfqDownload, type VendorPortalRfq } from "@/services/rfqApi";

export default function VendorRfqPortal() {
  const token = new URLSearchParams(window.location.hash.slice(1)).get("token") ?? "";
  const { t, lang } = useI18n();
  const [rfq, setRfq] = useState<VendorPortalRfq | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [loadStatus, setLoadStatus] = useState<number | null>(null);
  const [submission, setSubmission] = useState<{ submittedAt: string; totalOriginal: number; totalVnd: number; currency: string; validUntil: string } | null>(null);
  const [prices, setPrices] = useState<Record<number, string>>({});
  const [currency, setCurrency] = useState("VND");
  const [exchangeRate, setExchangeRate] = useState("1");
  const [leadTime, setLeadTime] = useState("0");
  const [paymentTerms, setPaymentTerms] = useState("");
  const [validUntil, setValidUntil] = useState("");
  const [freight, setFreight] = useState("0");
  const [discountPercent, setDiscountPercent] = useState("0");
  const [discountAmount, setDiscountAmount] = useState("0");
  const [vatPercent, setVatPercent] = useState("0");
  const [note, setNote] = useState("");

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    rfqApi.portal(token).then(response => { if (!cancelled) setRfq(response.data); })
      .catch(reason => { if (!cancelled) { setLoadStatus(axios.isAxiosError(reason) ? reason.response?.status ?? 0 : 0); setError(extractApiError(reason)); } })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [token]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!rfq) return;
    const lines = Object.entries(prices).filter(([, value]) => value !== "")
      .map(([rfqLineId, unitPrice]) => ({ rfqLineId: Number(rfqLineId), unitPrice: Number(unitPrice) }));
    if (!lines.length || !paymentTerms.trim() || new Date(validUntil).getTime() < new Date(rfq.dueAt).getTime() ||
      !/^[A-Z]{3}$/.test(currency) || Number(exchangeRate) <= 0 || currency === "VND" && Number(exchangeRate) !== 1 ||
      Number(discountPercent) > 0 && Number(discountAmount) > 0) {
      setError(t("rfq.portal.validation")); return;
    }
    setBusy(true); setError("");
    const preview = calculateRfqCommercialPreview(Object.fromEntries(rfq.lines.map(line => [line.id, line.quantity])),
      prices, freight, discountPercent, discountAmount, vatPercent, exchangeRate);
    try {
      await rfqApi.portalBid(token, {
        leadTimeDays: Number(leadTime), paymentTerms: paymentTerms.trim(), validUntil: new Date(validUntil).toISOString(),
        note: note.trim(), lines, currency, exchangeRateToVnd: Number(exchangeRate), freightAmount: Number(freight),
        discountPercent: Number(discountPercent), discountAmount: Number(discountAmount), vatPercent: Number(vatPercent),
      });
      setSubmission({ submittedAt: new Date().toISOString(), totalOriginal: preview.totalOriginal,
        totalVnd: preview.totalVnd, currency, validUntil: new Date(validUntil).toISOString() });
    } catch (reason) { setError(extractApiError(reason)); }
    finally { setBusy(false); }
  }

  if (loading) return <main className="mx-auto min-h-screen max-w-4xl p-6"><PageLoading /></main>;
  if (error && !rfq) return <main className="mx-auto flex min-h-screen max-w-xl items-center p-6"><section className="w-full border-y py-10">
    <h1 className="text-2xl font-semibold">{loadStatus === 404 ? t("rfq.portal.expiredTitle") : t("rfq.portal.loadErrorTitle")}</h1>
    <p className="mt-3 text-muted-foreground">{loadStatus === 404 ? t("rfq.portal.expiredMessage") : t("rfq.portal.loadErrorMessage")}</p>
    <p className="mt-4 text-sm">{t("rfq.portal.contactProcurement")}</p>
  </section></main>;
  if (!rfq) return null;
  const number = new Intl.NumberFormat(lang, { maximumFractionDigits: 4 });
  const preview = calculateRfqCommercialPreview(Object.fromEntries(rfq.lines.map(line => [line.id, line.quantity])),
    prices, freight, discountPercent, discountAmount, vatPercent, exchangeRate);
  if (submission) return <main className="mx-auto flex min-h-screen max-w-xl items-center p-6"><section className="w-full border-y py-12 text-center"><CheckCircle2 className="mx-auto h-10 w-10 text-emerald-600" /><h1 className="mt-4 text-2xl font-semibold">{t("rfq.portal.success")}</h1><p className="mt-2 text-muted-foreground">{rfq.code} · {rfq.vendorName}</p>
    <dl className="mx-auto mt-6 grid max-w-sm gap-3 text-left text-sm sm:grid-cols-2"><div><dt className="text-muted-foreground">{t("rfq.portal.submittedAt")}</dt><dd className="font-medium">{new Date(submission.submittedAt).toLocaleString(lang)}</dd></div><div><dt className="text-muted-foreground">{t("rfq.field.validity")}</dt><dd className="font-medium">{new Date(submission.validUntil).toLocaleString(lang)}</dd></div><div><dt className="text-muted-foreground">{t("rfq.commercial.totalOriginal")}</dt><dd className="font-semibold">{number.format(submission.totalOriginal)} {submission.currency}</dd></div><div><dt className="text-muted-foreground">{t("rfq.commercial.totalVnd")}</dt><dd className="font-semibold">{number.format(submission.totalVnd)} VND</dd></div></dl>
    <p className="mt-6 text-sm text-muted-foreground">{t("rfq.portal.successNote")}</p></section></main>;

  return <main className="min-h-screen bg-[linear-gradient(135deg,#f8fafc_0%,#fff7ed_100%)] px-4 py-8 text-foreground sm:px-6">
    <div className="mx-auto max-w-5xl">
      <header className="border-b border-slate-300 pb-6"><div className="flex items-center gap-3 text-amber-700"><FileText className="h-7 w-7" /><span className="text-sm font-semibold uppercase">{rfq.code}</span></div>
        <h1 className="mt-3 text-3xl font-semibold">{rfq.title}</h1><p className="mt-2 text-muted-foreground">{rfq.vendorName} · {new Date(rfq.dueAt).toLocaleString(lang)}</p></header>
      <form onSubmit={submit} className="space-y-7 py-7">
        {error && <p role="alert" className="border-y border-red-300 py-3 text-sm text-red-700">{error}</p>}
        <fieldset disabled={busy || !rfq.canSubmit} className="space-y-6">
          {rfq.documents.length > 0 && <section className="space-y-3 border-b pb-5"><h2 className="text-lg font-semibold">{t("rfq.portal.documents")}</h2>{rfq.documents.map(document => <Button key={document.id} type="button" variant="outline" className="mr-2 max-w-full whitespace-normal" onClick={async () => { setBusy(true); setError(""); try { saveRfqDownload((await rfqApi.portalDocument(token, document.id)).data, document.name); } catch (reason) { setError(extractApiError(reason)); } finally { setBusy(false); } }}><Download className="mr-2 h-4 w-4" />{document.name}</Button>)}</section>}
          <section className="space-y-3"><h2 className="text-lg font-semibold">{t("rfq.portal.prices")}</h2>{rfq.lines.map(line => <label key={line.id} className="grid gap-2 border-b py-3 sm:grid-cols-[1fr_220px] sm:items-center"><span><strong>{line.itemCode}</strong> · {line.description}<small className="block text-muted-foreground">{line.quantity} {line.unit}</small></span><Input aria-label={`${t("rfq.field.unitPrice")} ${line.itemCode}`} type="number" min="0" step="0.0001" value={prices[line.id] ?? ""} onChange={event => setPrices(current => ({ ...current, [line.id]: event.target.value }))} /></label>)}</section>
          <section className="grid gap-4 border-y py-5 sm:grid-cols-2 lg:grid-cols-3">
            <Field label={t("rfq.field.currency")}><Input maxLength={3} value={currency} onChange={event => { const value = event.target.value.toUpperCase(); setCurrency(value); if (value === "VND") setExchangeRate("1"); }} /></Field>
            <Field label={t("rfq.field.exchangeRate")}><Input type="number" min="0.00000001" step="0.00000001" disabled={currency === "VND"} value={exchangeRate} onChange={event => setExchangeRate(event.target.value)} /></Field>
            <Field label={t("rfq.field.leadTime")}><Input type="number" min="0" max="3650" value={leadTime} onChange={event => setLeadTime(event.target.value)} /></Field>
            <Field label={t("rfq.field.freight")}><Input type="number" min="0" step="0.0001" value={freight} onChange={event => setFreight(event.target.value)} /></Field>
            <Field label={t("rfq.field.discountPercent")}><Input type="number" min="0" max="100" step="0.01" disabled={Number(discountAmount) > 0} value={discountPercent} onChange={event => { setDiscountPercent(event.target.value); if (Number(event.target.value) > 0) setDiscountAmount("0"); }} /></Field>
            <Field label={t("rfq.field.discountAmount")}><Input type="number" min="0" step="0.0001" disabled={Number(discountPercent) > 0} value={discountAmount} onChange={event => { setDiscountAmount(event.target.value); if (Number(event.target.value) > 0) setDiscountPercent("0"); }} /></Field>
            <Field label={t("rfq.field.vatPercent")}><Input type="number" min="0" max="100" step="0.01" value={vatPercent} onChange={event => setVatPercent(event.target.value)} /></Field>
            <Field label={t("rfq.field.validity")}><Input type="datetime-local" value={validUntil} onChange={event => setValidUntil(event.target.value)} /></Field>
          </section>
          <PortalCommercialPreview preview={preview} currency={currency} number={number.format} t={t} />
          <Field label={t("rfq.field.paymentTerms")}><Textarea required maxLength={1000} value={paymentTerms} onChange={event => setPaymentTerms(event.target.value)} /></Field>
          <Field label={t("rfq.field.note")}><Textarea maxLength={2000} value={note} onChange={event => setNote(event.target.value)} /></Field>
          <Button type="submit" disabled={busy || !rfq.canSubmit}>{t("rfq.portal.submit")}</Button>
        </fieldset>
      </form>
    </div>
  </main>;
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return <label className="grid gap-1.5 text-sm font-medium">{label}{children}</label>;
}

function PortalCommercialPreview({ preview, currency, number, t }: {
  preview: ReturnType<typeof calculateRfqCommercialPreview>;
  currency: string;
  number: (value: number) => string;
  t: (key: string) => string;
}) {
  return <section className="border-y py-4" aria-label={t("rfq.commercial.preview")}><h2 className="font-semibold">{t("rfq.commercial.preview")}</h2><dl className="mt-3 grid gap-2 text-sm sm:grid-cols-2 lg:grid-cols-3">
    <div><dt className="text-muted-foreground">{t("rfq.commercial.subtotal")}</dt><dd>{number(preview.subtotal)} {currency}</dd></div><div><dt className="text-muted-foreground">{t("rfq.field.freight")}</dt><dd>{number(preview.freight)} {currency}</dd></div><div><dt className="text-muted-foreground">{t("rfq.commercial.discount")}</dt><dd>-{number(preview.discount)} {currency}</dd></div><div><dt className="text-muted-foreground">{t("rfq.commercial.vat")}</dt><dd>{number(preview.vat)} {currency}</dd></div><div><dt className="text-muted-foreground">{t("rfq.commercial.totalOriginal")}</dt><dd className="font-semibold">{number(preview.totalOriginal)} {currency}</dd></div><div><dt className="text-muted-foreground">{t("rfq.commercial.totalVnd")}</dt><dd className="font-semibold">{number(preview.totalVnd)} VND</dd></div>
  </dl></section>;
}