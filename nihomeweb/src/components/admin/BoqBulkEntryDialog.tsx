import { useState } from "react";
import { ListPlus, Plus, Trash2 } from "lucide-react";
import type { QuoteItemInput } from "@/services/adminApi";
import { validateQuoteValues } from "@/lib/quoteTotals";
import { useI18n } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { MoneyInput } from "@/components/ui/money-input";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (items: QuoteItemInput[]) => void;
}

const emptyRow = (): QuoteItemInput => ({ itemCode: null, name: "", unit: "", quantity: 0, unitPrice: 0 });

export default function BoqBulkEntryDialog({ open, onOpenChange, onConfirm }: Props) {
  const { t } = useI18n();
  const [rows, setRows] = useState<QuoteItemInput[]>([emptyRow()]);
  const issue = validateQuoteValues("Boq", { items: rows, discountPercent: 0, vatPercent: 0 });

  const close = () => {
    setRows([emptyRow()]);
    onOpenChange(false);
  };
  const update = (index: number, patch: Partial<QuoteItemInput>) =>
    setRows(current => current.map((row, rowIndex) => rowIndex === index ? { ...row, ...patch } : row));
  const remove = (index: number) =>
    setRows(current => current.length === 1 ? current : current.filter((_, rowIndex) => rowIndex !== index));

  return <Dialog open={open} onOpenChange={next => { if (!next) close(); }}>
    <DialogContent className="max-h-[92vh] max-w-6xl overflow-y-auto" data-testid="boq-bulk-entry-dialog">
      <DialogHeader>
        <DialogTitle className="flex items-center gap-2"><ListPlus className="h-5 w-5" />{t("quotes.bulk.title")}</DialogTitle>
        <DialogDescription>{t("quotes.bulk.description")}</DialogDescription>
      </DialogHeader>
      <div className="overflow-x-auto rounded-md border" data-testid="boq-bulk-table-scroll">
        <table className="w-full min-w-[850px] text-sm">
          <thead className="bg-muted/40 text-left text-xs uppercase text-muted-foreground">
            <tr>
              <th className="w-12 px-2 py-2 text-center">#</th>
              <th className="w-36 px-2 py-2">{t("quotes.boq.code")}</th>
              <th className="min-w-64 px-2 py-2">{t("quotes.boq.name")}</th>
              <th className="w-28 px-2 py-2">{t("quotes.boq.unit")}</th>
              <th className="w-36 px-2 py-2 text-right">{t("quotes.boq.quantity")}</th>
              <th className="w-44 px-2 py-2 text-right">{t("quotes.boq.unitPrice")}</th>
              <th className="w-12"><span className="sr-only">{t("common.delete")}</span></th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {rows.map((row, index) => <tr key={index} data-testid={`boq-bulk-row-${index}`}>
              <td className="px-2 py-2 text-center text-muted-foreground">{index + 1}</td>
              <td className="p-1.5"><Input data-testid={`boq-bulk-code-${index}`} maxLength={60} value={row.itemCode ?? ""} onChange={event => update(index, { itemCode: event.target.value || null })} /></td>
              <td className="p-1.5"><Input data-testid={`boq-bulk-name-${index}`} maxLength={300} value={row.name} onChange={event => update(index, { name: event.target.value })} /></td>
              <td className="p-1.5"><Input data-testid={`boq-bulk-unit-${index}`} maxLength={30} value={row.unit} onChange={event => update(index, { unit: event.target.value })} /></td>
              <td className="p-1.5"><Input data-testid={`boq-bulk-quantity-${index}`} className="text-right tabular-nums" type="number" inputMode="decimal" min="0.0001" step="0.0001" value={row.quantity || ""} onChange={event => update(index, { quantity: Number(event.target.value) })} /></td>
              <td className="p-1.5"><MoneyInput data-testid={`boq-bulk-price-${index}`} decimalScale={2} value={Number(row.unitPrice)} onChange={unitPrice => update(index, { unitPrice })} /></td>
              <td className="p-1.5"><Button data-testid={`boq-bulk-remove-${index}`} type="button" size="icon" variant="ghost" disabled={rows.length === 1} onClick={() => remove(index)}><Trash2 className="h-4 w-4" /></Button></td>
            </tr>)}
          </tbody>
        </table>
      </div>
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <Button data-testid="boq-bulk-add-row" type="button" variant="outline" onClick={() => setRows(current => [...current, emptyRow()])}><Plus className="mr-2 h-4 w-4" />{t("quotes.bulk.addRow")}</Button>
        {issue && <p role="alert" data-testid="boq-bulk-error" className="text-sm text-destructive">{t(`quotes.validation.${issue}`)}</p>}
      </div>
      <DialogFooter>
        <Button type="button" variant="outline" onClick={close}>{t("common.cancel")}</Button>
        <Button data-testid="boq-bulk-confirm" type="button" disabled={Boolean(issue)} onClick={() => { onConfirm(rows); close(); }}>{t("quotes.bulk.confirm", { count: rows.length })}</Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>;
}
