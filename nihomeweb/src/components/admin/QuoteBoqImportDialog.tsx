import { useRef, useState } from "react";
import { Download, FileSpreadsheet, Loader2, Upload } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { adminApi, type QuoteBoqImportPreviewResponse, type QuoteBoqSheetMapping } from "@/services/adminApi";

type Props = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  quoteId: number;
  rowVersion: string;
  onImported: () => Promise<void> | void;
};

export default function QuoteBoqImportDialog({ open, onOpenChange, quoteId, rowVersion, onImported }: Props) {
  const { t } = useI18n();
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<QuoteBoqImportPreviewResponse | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const reset = () => { setFile(null); setPreview(null); setError(null); if (inputRef.current) inputRef.current.value = ""; };
  const updateSheet = (index: number, patch: Partial<QuoteBoqSheetMapping>) => {
    if (!preview) return;
    const sheets = [...preview.sheets];
    sheets[index] = { ...sheets[index], ...patch };
    setPreview({ ...preview, sheets });
  };
  const downloadTemplate = async () => {
    setBusy(true); setError(null);
    try {
      const { data } = await adminApi.downloadQuoteBoqTemplate();
      const link = document.createElement("a");
      link.href = URL.createObjectURL(data); link.download = "NICON-quote-BOQ-template.xlsx"; link.click();
      URL.revokeObjectURL(link.href);
    } catch (err) { setError(extractApiError(err)); } finally { setBusy(false); }
  };
  const runPreview = async () => {
    if (!file) return;
    setBusy(true); setError(null); setPreview(null);
    try { setPreview((await adminApi.previewQuoteBoqImport(quoteId, file)).data); }
    catch (err) { setError(extractApiError(err)); } finally { setBusy(false); }
  };
  const confirm = async () => {
    if (!file || !preview) return;
    setBusy(true); setError(null);
    try {
      await adminApi.confirmQuoteBoqImport(quoteId, file, preview.sourceHash, preview.previewToken, rowVersion, preview.sheets);
      await onImported(); reset(); onOpenChange(false);
    } catch (err) { setError(extractApiError(err)); } finally { setBusy(false); }
  };
  const selectedCount = preview?.sheets.filter((sheet) => sheet.suggestedClassification === "Detail").length ?? 0;
  const mappingsValid = preview?.sheets.every((sheet) => {
    if (sheet.suggestedClassification !== "Detail") return true;
    const required = [sheet.headerRow, sheet.descriptionColumn, sheet.unitColumn, sheet.quantityColumn, sheet.unitPriceColumn];
    if (required.some((value) => value == null || !Number.isInteger(value) || value < 1)) return false;
    const columns = [sheet.itemCodeColumn, sheet.descriptionColumn, sheet.unitColumn, sheet.quantityColumn, sheet.unitPriceColumn]
      .filter((value): value is number => value != null);
    return sheet.headerRow! <= 1000 && columns.every((value) => value <= 500) && new Set(columns).size === columns.length;
  }) ?? false;

  return <Dialog open={open} onOpenChange={(value) => { if (!busy) { onOpenChange(value); if (!value) reset(); } }}>
    <DialogContent data-testid="quote-boq-import-dialog" className="max-h-[90vh] max-w-5xl overflow-y-auto">
      <DialogHeader>
        <DialogTitle>{t("quotes.boqImport.title")}</DialogTitle>
        <DialogDescription>{t("quotes.boqImport.description")}</DialogDescription>
      </DialogHeader>
      <div className="space-y-4">
        <div className="flex flex-wrap gap-2">
          <Button data-testid="quote-boq-template-download" type="button" variant="outline" onClick={() => void downloadTemplate()} disabled={busy}>
            <Download className="mr-1.5 h-4 w-4" />{t("quotes.boqImport.downloadTemplate")}
          </Button>
          <Input data-testid="quote-boq-import-file" ref={inputRef} className="max-w-md" type="file" accept=".xlsx" onChange={(event) => { setFile(event.target.files?.[0] ?? null); setPreview(null); setError(null); }} />
          <Button data-testid="quote-boq-import-preview" type="button" onClick={() => void runPreview()} disabled={!file || busy}>
            {busy ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : <FileSpreadsheet className="mr-1.5 h-4 w-4" />}{t("quotes.boqImport.preview")}
          </Button>
        </div>
        {error && <p role="alert" className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">{error}</p>}
        {preview && <>
          {preview.workbookWarnings.map((warning) => <p key={warning} className="rounded-md bg-amber-50 p-2 text-sm text-amber-900">{warning}</p>)}
          <div className="overflow-x-auto rounded-md border">
            <table className="w-full min-w-[900px] text-sm">
              <thead className="bg-muted/40 text-left"><tr>
                <th className="p-2">{t("quotes.boqImport.sheet")}</th><th className="p-2">{t("quotes.boqImport.classification")}</th>
                <th className="p-2">{t("quotes.boqImport.headerRow")}</th><th className="p-2">{t("quotes.boqImport.columns")}</th><th className="p-2">{t("quotes.boqImport.rows")}</th>
              </tr></thead>
              <tbody className="divide-y">{preview.sheets.map((sheet, index) => <tr data-testid={`quote-boq-sheet-${index}`} key={`${sheet.order}-${sheet.name}`}>
                <td className="p-2"><div className="font-medium">{sheet.order}. {sheet.name}</div>{sheet.isHidden && <div className="text-xs text-amber-700">{t("quotes.boqImport.hidden")}</div>}</td>
                <td className="p-2"><Select value={sheet.suggestedClassification} onValueChange={(value) => updateSheet(index, { suggestedClassification: value as QuoteBoqSheetMapping["suggestedClassification"] })}><SelectTrigger className="w-32"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Ignored">{t("quotes.boqImport.ignored")}</SelectItem><SelectItem value="Summary">{t("quotes.boqImport.summary")}</SelectItem><SelectItem value="Detail">{t("quotes.boqImport.detail")}</SelectItem></SelectContent></Select></td>
                <td className="p-2"><Input className="w-20" type="number" min={1} max={1000} step={1} value={sheet.headerRow ?? ""} onChange={(event) => updateSheet(index, { headerRow: event.target.value ? Number(event.target.value) : null })} /></td>
                <td className="p-2"><div className="grid grid-cols-5 gap-1">{(["descriptionColumn", "unitColumn", "quantityColumn", "unitPriceColumn", "itemCodeColumn"] as const).map((field) => <div key={field}><Label className="text-[10px]">{t(`quotes.boqImport.${field}`)}</Label><Input type="number" min={1} max={500} step={1} value={sheet[field] ?? ""} onChange={(event) => updateSheet(index, { [field]: event.target.value ? Number(event.target.value) : null })} /></div>)}</div></td>
                <td className="p-2 text-right">{sheet.suggestedItemCount}</td>
              </tr>)}</tbody>
            </table>
          </div>
          {!mappingsValid && <p role="alert" className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">{t("quotes.boqImport.invalidMapping")}</p>}
        </>}
      </div>
      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>{t("common.cancel")}</Button>
        <Button data-testid="quote-boq-import-confirm" onClick={() => void confirm()} disabled={!preview || selectedCount === 0 || !mappingsValid || busy}>
          {busy ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : <Upload className="mr-1.5 h-4 w-4" />}{t("quotes.boqImport.confirm")}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>;
}
