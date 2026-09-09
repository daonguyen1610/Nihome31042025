import { useState, type FormEvent, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { useI18n } from "@/lib/i18n";
import { calculateRfqCommercialPreview } from "@/lib/rfqCommercial";
import type { RfqBatchAwardDraft, RfqBidDraft, RfqDetail, RfqDraft, RfqReferences } from "@/services/rfqApi";

export const rfqSelectClass = "h-11 w-full min-w-0 rounded-md border border-input bg-background px-3 text-sm";
export function RfqField({ label, children }: { label: string; children: ReactNode }) {
  return <label className="grid min-w-0 gap-1.5 text-sm font-medium">{label}{children}</label>;
}
const rfqLocalDate = (value: string) => {
  const date = new Date(value);
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
  return date.toISOString().slice(0, 16);
};

export function RfqDraftForm({ detail, references, busy, onSave }: {
  detail: RfqDetail | null; references: RfqReferences; busy: boolean; onSave: (draft: RfqDraft) => void;
}) {
  const { t } = useI18n();
  const [title, setTitle] = useState(detail?.header.title ?? "");
  const [revisionId, setRevisionId] = useState(detail?.header.sourceBoqRevisionId ?? 0);
  const [ownerId, setOwnerId] = useState(detail?.header.ownerUserId ?? 0);
  const [dueAt, setDueAt] = useState(detail ? rfqLocalDate(detail.header.dueAt) : "");
  const [note, setNote] = useState(detail?.note ?? "");
  const [vendors, setVendors] = useState(detail?.vendors.map(x => x.id) ?? []);
  const [weights, setWeights] = useState({
    priceWeight: detail?.scoring?.priceWeight ?? 50,
    leadTimeWeight: detail?.scoring?.leadTimeWeight ?? 20,
    vendorRatingWeight: detail?.scoring?.vendorRatingWeight ?? 20,
    commercialWeight: detail?.scoring?.commercialWeight ?? 10,
  });
  const [quantities, setQuantities] = useState<Record<number, string>>(Object.fromEntries(detail?.lines.map(x => [x.projectBoqLineId, String(x.quantity)]) ?? []));
  const [error, setError] = useState("");
  const revision = references.revisions.find(x => x.id === revisionId);
  function submit(event: FormEvent) {
    event.preventDefault();
    const lines = Object.entries(quantities).filter(([, value]) => value !== "").map(([id, quantity]) => ({ projectBoqLineId: Number(id), quantity: Number(quantity) }));
    if (!title.trim() || !revision || !ownerId || !vendors.length || !lines.length || lines.length > 500 || vendors.length > 100) {
      setError(t("rfq.validation.scope")); return;
    }
    if (new Date(dueAt).getTime() <= Date.now()) { setError(t("rfq.validation.due")); return; }
    if (lines.some(x => !Number.isFinite(x.quantity) || x.quantity <= 0 ||
      x.quantity > (revision.lines.find(l => l.id === x.projectBoqLineId)?.quantity ?? 0))) {
      setError(t("rfq.validation.quantity")); return;
    }
    if (Object.values(weights).some(value => value < 0 || value > 100) || Object.values(weights).reduce((sum, value) => sum + value, 0) !== 100) {
      setError(t("rfq.validation.weights")); return;
    }
    setError("");
    onSave({ title: title.trim(), sourceBoqRevisionId: revisionId, ownerUserId: ownerId, dueAt: new Date(dueAt).toISOString(),
      note: note.trim(), vendorIds: vendors, lines, ...weights, rowVersion: detail?.header.rowVersion });
  }
  return <form onSubmit={submit} className="space-y-5">
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    <fieldset disabled={busy} className="space-y-5">
      <RfqField label={t("rfq.field.title")}><Input required maxLength={200} value={title} onChange={e => setTitle(e.target.value)} /></RfqField>
      <div className="grid gap-4 sm:grid-cols-2">
        <RfqField label={t("rfq.field.boq")}><select required className={rfqSelectClass} value={revisionId || ""} onChange={e => { setRevisionId(Number(e.target.value)); setQuantities({}); }}>
          <option value="">{t("rfq.choose")}</option>{references.revisions.map(x => <option key={x.id} value={x.id}>BOQ #{x.revision} · {x.currency}</option>)}
        </select></RfqField>
        <RfqField label={t("rfq.field.owner")}><select required className={rfqSelectClass} value={ownerId || ""} onChange={e => setOwnerId(Number(e.target.value))}>
          <option value="">{t("rfq.choose")}</option>{references.owners.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}
        </select></RfqField>
        <RfqField label={t("rfq.field.due")}><Input required type="datetime-local" value={dueAt} onChange={e => setDueAt(e.target.value)} /></RfqField>
      </div>
      <p className="text-sm text-muted-foreground">{t("rfq.scopeHelp")}</p>
      <div className="space-y-3 rounded-lg border p-3 sm:max-h-64 sm:overflow-auto">
        {revision?.lines.map(line => <div key={line.id} className="grid items-center gap-2 sm:grid-cols-[1fr_140px]">
          <RfqField label={`${line.itemCode} · ${line.description} (${line.unit}, ≤ ${line.quantity})`}>
            <Input aria-label={`${t("rfq.field.quantity")} ${line.itemCode}`} type="number" step="0.000001" min="0.000001" max={line.quantity}
              value={quantities[line.id] ?? ""} onChange={e => setQuantities({ ...quantities, [line.id]: e.target.value })} />
          </RfqField>
        </div>)}
      </div>
      <fieldset className="space-y-2"><legend className="mb-2 text-sm font-medium">{t("rfq.field.vendors")}</legend>
        <div className="grid gap-2 rounded-lg border p-3 sm:max-h-48 sm:grid-cols-2 sm:overflow-auto">
          {references.vendors.map(v => <label key={v.id} className="flex items-center gap-2 text-sm"><input type="checkbox" checked={vendors.includes(v.id)}
            onChange={e => setVendors(e.target.checked ? [...vendors, v.id] : vendors.filter(id => id !== v.id))} />{v.name}</label>)}
        </div>
      </fieldset>
      <fieldset className="space-y-2"><legend className="text-sm font-medium">{t("rfq.scoring.weights")}</legend>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {(["priceWeight", "leadTimeWeight", "vendorRatingWeight", "commercialWeight"] as const).map(key =>
            <RfqField key={key} label={t(`rfq.scoring.${key}`)}><Input type="number" min="0" max="100" step="0.01" value={weights[key]}
              onChange={event => setWeights(current => ({ ...current, [key]: Number(event.target.value) }))} /></RfqField>)}
        </div>
      </fieldset>
      <RfqField label={t("rfq.field.note")}><Textarea maxLength={2000} value={note} onChange={e => setNote(e.target.value)} /></RfqField>
      <Button type="submit" disabled={busy}>{t("rfq.save")}</Button>
    </fieldset>
  </form>;
}

export function RfqBidForm({ detail, references, busy, onSave, onUpload }: {
  detail: RfqDetail; references: RfqReferences; busy: boolean;
  onSave: (draft: RfqBidDraft) => void; onUpload: (file: File) => Promise<void>;
}) {
  const { t, lang } = useI18n();
  const [vendorId, setVendorId] = useState(0);
  const [leadTimeDays, setLeadTimeDays] = useState("0");
  const [paymentTerms, setPaymentTerms] = useState("");
  const [validUntil, setValidUntil] = useState("");
  const [note, setNote] = useState("");
  const [prices, setPrices] = useState<Record<number, string>>({});
  const [documentIds, setDocumentIds] = useState<number[]>([]);
  const [currency, setCurrency] = useState("VND");
  const [exchangeRateToVnd, setExchangeRateToVnd] = useState("1");
  const [freightAmount, setFreightAmount] = useState("0");
  const [discountPercent, setDiscountPercent] = useState("0");
  const [discountAmount, setDiscountAmount] = useState("0");
  const [vatPercent, setVatPercent] = useState("0");
  const [error, setError] = useState("");
  const number = new Intl.NumberFormat(lang, { maximumFractionDigits: 4 });
  const preview = calculateRfqCommercialPreview(
    Object.fromEntries(detail.lines.map(line => [line.id, line.quantity])), prices,
    freightAmount, discountPercent, discountAmount, vatPercent, exchangeRateToVnd);
  function submit(event: FormEvent) {
    event.preventDefault();
    const lines = Object.entries(prices).filter(([, value]) => value !== "").map(([id, value]) => ({ rfqLineId: Number(id), unitPrice: Number(value) }));
    if (!vendorId || !lines.length || !paymentTerms.trim() || !Number.isInteger(Number(leadTimeDays)) || documentIds.length > 20) { setError(t("rfq.validation.bid")); return; }
    if (new Date(validUntil).getTime() < new Date(detail.header.dueAt).getTime() || new Date(validUntil).getTime() <= Date.now()) { setError(t("rfq.validation.validity")); return; }
    if (lines.some(x => !Number.isFinite(x.unitPrice) || x.unitPrice < 0 || x.unitPrice > 999999999999.9999)) { setError(t("rfq.validation.price")); return; }
    if (!/^[A-Z]{3}$/.test(currency) || Number(exchangeRateToVnd) <= 0 || currency === "VND" && Number(exchangeRateToVnd) !== 1 ||
      Number(discountPercent) > 0 && Number(discountAmount) > 0) { setError(t("rfq.validation.commercial")); return; }
    setError("");
    onSave({ vendorId, leadTimeDays: Number(leadTimeDays), paymentTerms: paymentTerms.trim(), validUntil: new Date(validUntil).toISOString(),
      note: note.trim(), lines, documentIds, currency, exchangeRateToVnd: Number(exchangeRateToVnd),
      freightAmount: Number(freightAmount), discountPercent: Number(discountPercent),
      discountAmount: Number(discountAmount), vatPercent: Number(vatPercent), rowVersion: detail.header.rowVersion });
  }
  return <form onSubmit={submit} className="space-y-5">
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    <fieldset disabled={busy} className="space-y-5">
      <p className="text-sm text-muted-foreground">{t("rfq.bidHelp")}</p>
      <RfqField label={t("rfq.field.vendor")}><select required value={vendorId || ""} onChange={e => setVendorId(Number(e.target.value))} className={rfqSelectClass}>
        <option value="">{t("rfq.choose")}</option>{detail.vendors.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}
      </select></RfqField>
      <div className="space-y-3 rounded-lg border p-3 sm:max-h-64 sm:overflow-auto">
        {detail.lines.map(line => <RfqField key={line.id} label={`${line.itemCode} · ${line.description} · ${t("rfq.field.unitPrice")} (${currency})`}>
          <Input type="number" min="0" max="999999999999.9999" step="0.0001" value={prices[line.id] ?? ""} onChange={e => setPrices({ ...prices, [line.id]: e.target.value })} />
        </RfqField>)}
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <RfqField label={t("rfq.field.leadTime")}><Input required type="number" min="0" max="3650" step="1" value={leadTimeDays} onChange={e => setLeadTimeDays(e.target.value)} /></RfqField>
        <RfqField label={t("rfq.field.validity")}><Input required type="datetime-local" value={validUntil} onChange={e => setValidUntil(e.target.value)} /></RfqField>
      </div>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <RfqField label={t("rfq.field.currency")}><Input required maxLength={3} value={currency} onChange={e => { const value = e.target.value.toUpperCase(); setCurrency(value); if (value === "VND") setExchangeRateToVnd("1"); }} /></RfqField>
        <RfqField label={t("rfq.field.exchangeRate")}><Input required type="number" min="0.00000001" step="0.00000001" disabled={currency === "VND"} value={exchangeRateToVnd} onChange={e => setExchangeRateToVnd(e.target.value)} /></RfqField>
        <RfqField label={t("rfq.field.freight")}><Input type="number" min="0" step="0.0001" value={freightAmount} onChange={e => setFreightAmount(e.target.value)} /></RfqField>
        <RfqField label={t("rfq.field.discountPercent")}><Input type="number" min="0" max="100" step="0.01" disabled={Number(discountAmount) > 0} value={discountPercent} onChange={e => { setDiscountPercent(e.target.value); if (Number(e.target.value) > 0) setDiscountAmount("0"); }} /></RfqField>
        <RfqField label={t("rfq.field.discountAmount")}><Input type="number" min="0" step="0.0001" disabled={Number(discountPercent) > 0} value={discountAmount} onChange={e => { setDiscountAmount(e.target.value); if (Number(e.target.value) > 0) setDiscountPercent("0"); }} /></RfqField>
        <RfqField label={t("rfq.field.vatPercent")}><Input type="number" min="0" max="100" step="0.01" value={vatPercent} onChange={e => setVatPercent(e.target.value)} /></RfqField>
      </div>
      <CommercialPreview preview={preview} currency={currency} number={number.format} t={t} />
      <RfqField label={t("rfq.field.paymentTerms")}><Textarea required maxLength={1000} value={paymentTerms} onChange={e => setPaymentTerms(e.target.value)} /></RfqField>
      <RfqField label={t("rfq.field.note")}><Textarea maxLength={2000} value={note} onChange={e => setNote(e.target.value)} /></RfqField>
      <fieldset><legend className="mb-2 text-sm font-medium">{t("rfq.documents")}</legend>
        {references.documents.map(file => <label key={file.id} className="flex items-center gap-2 py-1 text-sm"><input type="checkbox" checked={documentIds.includes(file.id)}
          onChange={e => setDocumentIds(e.target.checked ? [...documentIds, file.id] : documentIds.filter(id => id !== file.id))} />{file.name}</label>)}
        <RfqField label={t("rfq.upload")}><Input type="file" onChange={e => { const file = e.target.files?.[0]; if (file) void onUpload(file); e.target.value = ""; }} /></RfqField>
        <p className="mt-2 text-xs text-muted-foreground">{t("rfq.uploadHelp")}</p>
      </fieldset>
      <Button type="submit" disabled={busy}>{t("rfq.submitBid")}</Button>
    </fieldset>
  </form>;
}

function CommercialPreview({ preview, currency, number, t }: {
  preview: ReturnType<typeof calculateRfqCommercialPreview>;
  currency: string;
  number: (value: number) => string;
  t: (key: string) => string;
}) {
  return <section className="border-y py-4" aria-label={t("rfq.commercial.preview")}>
    <h3 className="font-semibold">{t("rfq.commercial.preview")}</h3>
    <dl className="mt-3 grid gap-2 text-sm sm:grid-cols-2 lg:grid-cols-3">
      <div><dt className="text-muted-foreground">{t("rfq.commercial.subtotal")}</dt><dd className="font-medium">{number(preview.subtotal)} {currency}</dd></div>
      <div><dt className="text-muted-foreground">{t("rfq.field.freight")}</dt><dd className="font-medium">{number(preview.freight)} {currency}</dd></div>
      <div><dt className="text-muted-foreground">{t("rfq.commercial.discount")}</dt><dd className="font-medium">-{number(preview.discount)} {currency}</dd></div>
      <div><dt className="text-muted-foreground">{t("rfq.commercial.vat")}</dt><dd className="font-medium">{number(preview.vat)} {currency}</dd></div>
      <div><dt className="text-muted-foreground">{t("rfq.commercial.totalOriginal")}</dt><dd className="font-semibold">{number(preview.totalOriginal)} {currency}</dd></div>
      <div><dt className="text-muted-foreground">{t("rfq.commercial.totalVnd")}</dt><dd className="font-semibold">{number(preview.totalVnd)} VND</dd></div>
    </dl>
  </section>;
}

export function RfqEvaluationForm({ detail, busy, onSave }: {
  detail: RfqDetail; busy: boolean; onSave: (bidId: number, score: number, note: string) => void;
}) {
  const { t } = useI18n();
  const candidates = detail.bids.filter(bid => bid.isCurrent && !bid.withdrawnAt);
  const [bidId, setBidId] = useState(candidates[0]?.id ?? 0);
  const selected = candidates.find(bid => bid.id === bidId);
  const [score, setScore] = useState(String(selected?.commercialScore ?? 50));
  const [note, setNote] = useState(selected?.evaluationNote ?? "");
  const [error, setError] = useState("");
  const commercialScore = Number(score) || 0;
  const projected = selected && selected.priceScore !== null && selected.leadTimeScore !== null && selected.vendorRatingScore !== null
    ? ((selected.priceScore * detail.scoring.priceWeight) + (selected.leadTimeScore * detail.scoring.leadTimeWeight) +
      (selected.vendorRatingScore * detail.scoring.vendorRatingWeight) + (commercialScore * detail.scoring.commercialWeight)) / 100
    : null;
  return <form noValidate className="space-y-4" onSubmit={event => {
    event.preventDefault();
    const value = Number(score);
    if (!bidId || value < 0 || value > 100 || note.trim().length < 3) { setError(t("rfq.validation.evaluation")); return; }
    onSave(bidId, value, note.trim());
  }}>
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    <RfqField label={t("rfq.field.bid")}><select className={rfqSelectClass} value={bidId} onChange={event => {
      const next = candidates.find(bid => bid.id === Number(event.target.value));
      setBidId(Number(event.target.value)); setScore(String(next?.commercialScore ?? 50)); setNote(next?.evaluationNote ?? "");
    }}>{candidates.map(bid => <option key={bid.id} value={bid.id}>{detail.vendors.find(vendor => vendor.id === bid.vendorId)?.name} · #{bid.revision}</option>)}</select></RfqField>
    {selected && <section className="border-y py-4 text-sm" aria-label={t("rfq.scoring.breakdown")}><h3 className="font-semibold">{t("rfq.scoring.breakdown")}</h3>
      <dl className="mt-3 grid gap-2 sm:grid-cols-2"><div><dt className="text-muted-foreground">{t("rfq.scoring.priceWeight")} · {detail.scoring.priceWeight}%</dt><dd>{selected.priceScore ?? "—"}</dd></div><div><dt className="text-muted-foreground">{t("rfq.scoring.leadTimeWeight")} · {detail.scoring.leadTimeWeight}%</dt><dd>{selected.leadTimeScore ?? "—"}</dd></div><div><dt className="text-muted-foreground">{t("rfq.scoring.vendorRatingWeight")} · {detail.scoring.vendorRatingWeight}%</dt><dd>{selected.vendorRatingScore ?? "—"}</dd></div><div><dt className="text-muted-foreground">{t("rfq.scoring.commercialWeight")} · {detail.scoring.commercialWeight}%</dt><dd>{commercialScore}</dd></div></dl>
      <p className="mt-3 border-t pt-3 font-semibold">{t("rfq.scoring.projectedTotal")}: {projected === null ? "—" : projected.toFixed(2)}</p>
    </section>}
    <RfqField label={t("rfq.scoring.commercialScore")}><Input type="number" min="0" max="100" step="0.01" value={score} onChange={event => setScore(event.target.value)} /></RfqField>
    <RfqField label={t("rfq.scoring.note")}><Textarea minLength={3} maxLength={2000} value={note} onChange={event => setNote(event.target.value)} /></RfqField>
    <Button type="submit" disabled={busy}>{t("rfq.scoring.save")}</Button>
  </form>;
}

type AwardRow = { key: number; rfqLineId: number; bidId: number; quantity: string; materialRequestLineId: number; contractType: "Supply" | "Subcontract" };

export function RfqBatchAwardForm({ detail, busy, onSave }: {
  detail: RfqDetail; busy: boolean; onSave: (draft: RfqBatchAwardDraft) => void;
}) {
  const { t } = useI18n();
  const [nextKey, setNextKey] = useState(detail.lines.length + 1);
  const initialRows = detail.lines.map((line, index) => {
    const bid = detail.bids.find(item => item.isCurrent && !item.withdrawnAt &&
      (detail.scoring.commercialWeight === 0 || item.weightedScore !== null) &&
      item.lines.some(value => value.rfqLineId === line.id));
    const request = detail.materialRequests.find(item => item.projectBoqLineId === line.projectBoqLineId && item.remainingQuantity > 0);
    const vendor = detail.vendors.find(item => item.id === bid?.vendorId);
    return { key: index + 1, rfqLineId: line.id, bidId: bid?.id ?? 0, quantity: String(line.quantity),
      materialRequestLineId: request?.lineId ?? 0, contractType: vendor?.type === "SubContractor" ? "Subcontract" as const : "Supply" as const };
  });
  const [rows, setRows] = useState<AwardRow[]>(initialRows);
  const [reason, setReason] = useState("");
  const [overrideReason, setOverrideReason] = useState("");
  const [error, setError] = useState("");
  const update = (key: number, value: Partial<AwardRow>) => setRows(current => current.map(row => row.key === key ? { ...row, ...value } : row));
  const add = (lineId: number) => { setRows(current => [...current, { key: nextKey, rfqLineId: lineId, bidId: 0, quantity: "", materialRequestLineId: 0, contractType: "Supply" }]); setNextKey(value => value + 1); };
  const needsOverride = rows.some(row => {
    const selected = detail.bids.find(bid => bid.id === row.bidId)?.lines.find(line => line.rfqLineId === row.rfqLineId)?.weightedScore;
    const best = Math.max(...detail.bids.map(bid => bid.lines.find(line => line.rfqLineId === row.rfqLineId)?.weightedScore ?? -1));
    return selected !== null && selected !== undefined && selected < best;
  });
  return <form className="space-y-5" onSubmit={event => {
    event.preventDefault();
    const invalidCoverage = detail.lines.some(line => rows.filter(row => row.rfqLineId === line.id).reduce((sum, row) => sum + Number(row.quantity || 0), 0) !== line.quantity);
    if (reason.trim().length < 3) { setError(t("rfq.validation.reasonRequired")); return; }
    if (invalidCoverage) { setError(t("rfq.validation.coverage")); return; }
    if (rows.some(row => !row.bidId || row.contractType === "Supply" && !row.materialRequestLineId || Number(row.quantity) <= 0)) { setError(t("rfq.validation.allocation")); return; }
    if (needsOverride && overrideReason.trim().length < 3) { setError(t("rfq.validation.overrideReason")); return; }
    onSave({ reason: reason.trim(), overrideReason: overrideReason.trim() || undefined, rowVersion: detail.header.rowVersion,
      lines: rows.map(row => ({ bidId: row.bidId, rfqLineId: row.rfqLineId, contractType: row.contractType,
        quantity: Number(row.quantity), materialRequestAllocations: row.contractType === "Supply"
          ? [{ materialRequestLineId: row.materialRequestLineId, quantity: Number(row.quantity) }] : [] })) });
  }}>
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    {detail.lines.map(line => <section key={line.id} className="space-y-3 border-y py-4">
      <div className="flex items-center justify-between gap-3"><h3 className="font-medium">{line.itemCode} · {line.description} ({line.quantity} {line.unit})</h3>
        <Button type="button" variant="outline" size="sm" onClick={() => add(line.id)}>{t("rfq.award.addSplit")}</Button></div>
      {rows.filter(row => row.rfqLineId === line.id).map(row => {
        const bid = detail.bids.find(item => item.id === row.bidId);
        const vendor = detail.vendors.find(item => item.id === bid?.vendorId);
        const bids = detail.bids.filter(item => item.isCurrent && !item.withdrawnAt &&
          (detail.scoring.commercialWeight === 0 || item.weightedScore !== null) &&
          item.lines.some(value => value.rfqLineId === line.id));
        const requests = detail.materialRequests.filter(item => item.projectBoqLineId === line.projectBoqLineId && item.remainingQuantity > 0);
        return <div key={row.key} className="grid gap-3 rounded-md border p-3 lg:grid-cols-[1.4fr_120px_1fr_150px_auto]">
          <RfqField label={t("rfq.field.vendor")}><select className={rfqSelectClass} value={row.bidId || ""} onChange={event => {
            const selectedBid = bids.find(item => item.id === Number(event.target.value));
            const selectedVendor = detail.vendors.find(item => item.id === selectedBid?.vendorId);
            update(row.key, { bidId: Number(event.target.value), contractType: selectedVendor?.type === "SubContractor" ? "Subcontract" : "Supply" });
          }}><option value="">{t("rfq.choose")}</option>{bids.map(item => <option key={item.id} value={item.id}>{detail.vendors.find(v => v.id === item.vendorId)?.name} · #{item.revision}</option>)}</select></RfqField>
          <RfqField label={t("rfq.field.quantity")}><Input type="number" min="0.000001" step="0.000001" value={row.quantity} onChange={event => update(row.key, { quantity: event.target.value })} /></RfqField>
          <RfqField label={t("rfq.award.materialRequest")}><select disabled={row.contractType === "Subcontract"} className={rfqSelectClass} value={row.contractType === "Subcontract" ? "" : row.materialRequestLineId || ""} onChange={event => update(row.key, { materialRequestLineId: Number(event.target.value) })}><option value="">{row.contractType === "Subcontract" ? t("rfq.award.notApplicable") : t("rfq.choose")}</option>{requests.map(item => <option key={item.lineId} value={item.lineId}>{item.materialRequestCode} · {t("rfq.award.requestedShort")}: {item.requestedQuantity} · {t("rfq.award.allocatedShort")}: {item.alreadyAllocatedQuantity} · {t("rfq.award.remainingShort")}: {item.remainingQuantity}</option>)}</select></RfqField>
          <RfqField label={t("rfq.field.contractType")}><select className={rfqSelectClass} value={row.contractType} onChange={event => { const contractType = event.target.value as AwardRow["contractType"]; update(row.key, { contractType, materialRequestLineId: contractType === "Subcontract" ? 0 : row.materialRequestLineId }); }}><option value="Supply">{t("rfq.supply")}</option><option value="Subcontract">{t("rfq.subcontract")}</option></select></RfqField>
          <Button type="button" variant="outline" disabled={rows.filter(item => item.rfqLineId === line.id).length === 1} onClick={() => setRows(current => current.filter(item => item.key !== row.key))}>{t("common.delete")}</Button>
          {vendor && <p className="text-xs text-muted-foreground lg:col-span-5">{vendor.name} · {bid?.currency} · {t("rfq.scoring.total")}: {bid?.lines.find(value => value.rfqLineId === row.rfqLineId)?.weightedScore ?? "—"}</p>}
        </div>;
      })}
      {(() => { const allocated = rows.filter(row => row.rfqLineId === line.id).reduce((sum, row) => sum + Number(row.quantity || 0), 0); return <p className={allocated === line.quantity ? "text-sm text-emerald-700" : "text-sm text-amber-700"}>{t("rfq.award.lineSummary", { allocated, total: line.quantity, remaining: line.quantity - allocated })}</p>; })()}
    </section>)}
    <RfqField label={t("rfq.field.reason")}><Textarea minLength={3} maxLength={2000} value={reason} onChange={event => setReason(event.target.value)} /></RfqField>
    {needsOverride && <section className="border-l-4 border-amber-500 bg-amber-50 p-3"><p className="mb-2 text-sm font-medium text-amber-900">{t("rfq.award.overrideWarning")}</p><RfqField label={t("rfq.award.overrideReason")}><Textarea minLength={3} maxLength={2000} value={overrideReason} onChange={event => setOverrideReason(event.target.value)} /></RfqField></section>}
    <Button type="submit" disabled={busy}>{t("rfq.award")}</Button>
  </form>;
}
