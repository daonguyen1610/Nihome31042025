import { useState, type FormEvent, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { useI18n } from "@/lib/i18n";
import type { RfqBidDraft, RfqDetail, RfqDraft, RfqReferences } from "@/services/rfqApi";

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
    setError("");
    onSave({ title: title.trim(), sourceBoqRevisionId: revisionId, ownerUserId: ownerId, dueAt: new Date(dueAt).toISOString(),
      note: note.trim(), vendorIds: vendors, lines, rowVersion: detail?.header.rowVersion });
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
      <div className="max-h-64 space-y-3 overflow-auto rounded-lg border p-3">
        {revision?.lines.map(line => <div key={line.id} className="grid items-center gap-2 sm:grid-cols-[1fr_140px]">
          <RfqField label={`${line.itemCode} · ${line.description} (${line.unit}, ≤ ${line.quantity})`}>
            <Input aria-label={`${t("rfq.field.quantity")} ${line.itemCode}`} type="number" step="0.000001" min="0.000001" max={line.quantity}
              value={quantities[line.id] ?? ""} onChange={e => setQuantities({ ...quantities, [line.id]: e.target.value })} />
          </RfqField>
        </div>)}
      </div>
      <fieldset className="space-y-2"><legend className="mb-2 text-sm font-medium">{t("rfq.field.vendors")}</legend>
        <div className="grid max-h-48 gap-2 overflow-auto rounded-lg border p-3 sm:grid-cols-2">
          {references.vendors.map(v => <label key={v.id} className="flex items-center gap-2 text-sm"><input type="checkbox" checked={vendors.includes(v.id)}
            onChange={e => setVendors(e.target.checked ? [...vendors, v.id] : vendors.filter(id => id !== v.id))} />{v.name}</label>)}
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
  const { t } = useI18n();
  const [vendorId, setVendorId] = useState(0);
  const [leadTimeDays, setLeadTimeDays] = useState("0");
  const [paymentTerms, setPaymentTerms] = useState("");
  const [validUntil, setValidUntil] = useState("");
  const [note, setNote] = useState("");
  const [prices, setPrices] = useState<Record<number, string>>({});
  const [documentIds, setDocumentIds] = useState<number[]>([]);
  const [error, setError] = useState("");
  function submit(event: FormEvent) {
    event.preventDefault();
    const lines = Object.entries(prices).filter(([, value]) => value !== "").map(([id, value]) => ({ rfqLineId: Number(id), unitPrice: Number(value) }));
    if (!vendorId || !lines.length || !paymentTerms.trim() || !Number.isInteger(Number(leadTimeDays)) || documentIds.length > 20) { setError(t("rfq.validation.bid")); return; }
    if (new Date(validUntil).getTime() < new Date(detail.header.dueAt).getTime() || new Date(validUntil).getTime() <= Date.now()) { setError(t("rfq.validation.validity")); return; }
    if (lines.some(x => !Number.isFinite(x.unitPrice) || x.unitPrice < 0 || x.unitPrice > 999999999999.9999)) { setError(t("rfq.validation.price")); return; }
    setError("");
    onSave({ vendorId, leadTimeDays: Number(leadTimeDays), paymentTerms: paymentTerms.trim(), validUntil: new Date(validUntil).toISOString(),
      note: note.trim(), lines, documentIds, rowVersion: detail.header.rowVersion });
  }
  return <form onSubmit={submit} className="space-y-5">
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    <fieldset disabled={busy} className="space-y-5">
      <p className="text-sm text-muted-foreground">{t("rfq.bidHelp")}</p>
      <RfqField label={t("rfq.field.vendor")}><select required value={vendorId || ""} onChange={e => setVendorId(Number(e.target.value))} className={rfqSelectClass}>
        <option value="">{t("rfq.choose")}</option>{detail.vendors.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}
      </select></RfqField>
      <div className="max-h-64 space-y-3 overflow-auto rounded-lg border p-3">
        {detail.lines.map(line => <RfqField key={line.id} label={`${line.itemCode} · ${line.description} · ${t("rfq.field.unitPrice")} (VND)`}>
          <Input type="number" min="0" max="999999999999.9999" step="0.0001" value={prices[line.id] ?? ""} onChange={e => setPrices({ ...prices, [line.id]: e.target.value })} />
        </RfqField>)}
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <RfqField label={t("rfq.field.leadTime")}><Input required type="number" min="0" max="3650" step="1" value={leadTimeDays} onChange={e => setLeadTimeDays(e.target.value)} /></RfqField>
        <RfqField label={t("rfq.field.validity")}><Input required type="datetime-local" value={validUntil} onChange={e => setValidUntil(e.target.value)} /></RfqField>
      </div>
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
