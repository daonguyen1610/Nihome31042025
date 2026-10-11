import { useEffect, useMemo, useState } from "react";
import { FileSpreadsheet, Loader2, Save, UploadCloud } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { adminApi, type BoqImportTemplateResponse, type BoqWorkbookPreviewResponse, type BoqWorkbookSheetPreviewResponse, type MaterialRateRevisionResponse } from "@/services/adminApi";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  catalogId: number;
  revisionId: number;
  onImported: (revision: MaterialRateRevisionResponse) => void;
}

const fields = ["descriptionColumn", "unitColumn", "quantityColumn", "unitPriceColumn", "itemCodeColumn"] as const;

export default function BoqCatalogWorkbookDialog({ open, onOpenChange, catalogId, revisionId, onImported }: Props) {
  const { t } = useI18n();
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<BoqWorkbookPreviewResponse | null>(null);
  const [templates, setTemplates] = useState<BoqImportTemplateResponse[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [templateName, setTemplateName] = useState("");

  useEffect(() => { if (open) void adminApi.listBoqImportTemplates().then(({ data }) => setTemplates(data)); }, [open]);
  const selected = useMemo(() => preview?.sheets.filter(sheet => sheet.include) ?? [], [preview]);
  const mappingsValid = selected.length > 0 && selected.every(sheet => {
    const required = [sheet.headerRow, sheet.descriptionColumn, sheet.unitColumn, sheet.quantityColumn, sheet.unitPriceColumn];
    const columns = [sheet.itemCodeColumn, sheet.descriptionColumn, sheet.unitColumn, sheet.quantityColumn, sheet.unitPriceColumn].filter((value): value is number => value != null);
    return required.every(value => value != null && value >= 1) && new Set(columns).size === columns.length;
  });
  const selectedCount = useMemo(() => selected.reduce((sum, sheet) => sum + sheet.candidateCount, 0), [selected]);

  const update = (index: number, patch: Partial<BoqWorkbookSheetPreviewResponse>) => setPreview(current => current ? ({ ...current, sheets: current.sheets.map((sheet, i) => i === index ? { ...sheet, ...patch } : sheet) }) : current);
  const applyTemplate = (index: number, id: string) => {
    const template = templates.find(item => item.id === Number(id));
    if (template) update(index, { headerRow: template.headerRow, itemCodeColumn: template.itemCodeColumn, descriptionColumn: template.descriptionColumn, unitColumn: template.unitColumn, quantityColumn: template.quantityColumn, unitPriceColumn: template.unitPriceColumn });
  };
  const runPreview = async () => {
    if (!file) return;
    setBusy(true); setError(null);
    try {
      const { data } = await adminApi.previewBoqWorkbook(catalogId, revisionId, file);
      setPreview({ ...data, sheets: data.sheets.map(sheet => ({ ...sheet, include: Boolean(sheet.headerRow && sheet.descriptionColumn && sheet.unitColumn && sheet.quantityColumn && sheet.unitPriceColumn) })) });
    } catch (err) { setError(extractApiError(err)); } finally { setBusy(false); }
  };
  const saveTemplate = async () => {
    if (selected.length !== 1 || !templateName.trim() || !mappingsValid) return;
    setBusy(true); setError(null);
    try {
      const sheet = selected[0];
      const { data } = await adminApi.createBoqImportTemplate({ name: templateName.trim(), headerRow: sheet.headerRow!, itemCodeColumn: sheet.itemCodeColumn, descriptionColumn: sheet.descriptionColumn!, unitColumn: sheet.unitColumn!, quantityColumn: sheet.quantityColumn!, unitPriceColumn: sheet.unitPriceColumn! });
      setTemplates(items => [...items, data]); setTemplateName("");
    } catch (err) { setError(extractApiError(err)); } finally { setBusy(false); }
  };
  const confirm = async () => {
    if (!file || !preview || !mappingsValid) return;
    setBusy(true); setError(null);
    try {
      const { data } = await adminApi.importBoqWorkbook(catalogId, revisionId, file, preview.sourceHash, preview.sheets);
      onImported(data); onOpenChange(false);
    } catch (err) { setError(extractApiError(err)); } finally { setBusy(false); }
  };

  return <Dialog open={open} onOpenChange={value => !busy && onOpenChange(value)}>
    <DialogContent className="max-h-[92vh] max-w-6xl overflow-y-auto" data-testid="boq-catalog-workbook-dialog">
      <DialogHeader><DialogTitle>{t("materialRates.boqWorkbook.title")}</DialogTitle><DialogDescription>{t("materialRates.boqWorkbook.description")}</DialogDescription></DialogHeader>
      <div className="space-y-4">
        <section className="rounded-lg border bg-muted/20 p-4">
          <Label htmlFor="boq-workbook-file">{t("materialRates.boqWorkbook.step1")}</Label>
          <div className="mt-2 flex flex-col gap-2 sm:flex-row"><Input id="boq-workbook-file" type="file" accept=".xlsx" data-testid="boq-workbook-file" onChange={event => { setFile(event.target.files?.[0] ?? null); setPreview(null); setError(null); }} /><Button onClick={() => void runPreview()} disabled={!file || busy} data-testid="boq-workbook-preview">{busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <FileSpreadsheet className="mr-2 h-4 w-4" />}{t("materialRates.boqWorkbook.preview")}</Button></div>
        </section>
        {preview && <section className="space-y-3">
          <div><h3 className="font-semibold">{t("materialRates.boqWorkbook.step2")}</h3><p className="text-sm text-muted-foreground">{t("materialRates.boqWorkbook.mappingHint", { count: selectedCount })}</p></div>
          {preview.sheets.map((sheet, index) => <article key={sheet.name} className="rounded-lg border p-3" data-testid={`boq-workbook-sheet-${index}`}>
            <div className="flex flex-wrap items-center justify-between gap-2"><Label className="flex items-center gap-2"><Checkbox data-testid={`boq-workbook-include-${index}`} checked={sheet.include} onCheckedChange={value => update(index, { include: value === true })} />{sheet.order}. {sheet.name}</Label><div className="flex items-center gap-2"><span className="text-xs text-muted-foreground">{sheet.candidateCount} {t("materialRates.boqWorkbook.rows")}</span><Select onValueChange={value => applyTemplate(index, value)}><SelectTrigger className="w-52" data-testid={`boq-workbook-template-${index}`}><SelectValue placeholder={t("materialRates.boqWorkbook.chooseTemplate")} /></SelectTrigger><SelectContent>{templates.map(template => <SelectItem key={template.id} value={String(template.id)}>{template.name}</SelectItem>)}</SelectContent></Select></div></div>
            {sheet.warnings.map(warning => <p key={warning} className="mt-2 text-xs text-amber-700">{warning}</p>)}
            {sheet.include && <div className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6"><div><Label>{t("materialRates.boqWorkbook.headerRow")}</Label><Input data-testid={`boq-workbook-headerRow-${index}`} type="number" min={1} value={sheet.headerRow ?? ""} onChange={event => update(index, { headerRow: Number(event.target.value) || null })} /></div>{fields.map(field => <div key={field}><Label>{t(`materialRates.boqWorkbook.${field}`)}</Label><Input data-testid={`boq-workbook-${field}-${index}`} type="number" min={1} value={sheet[field] ?? ""} onChange={event => update(index, { [field]: Number(event.target.value) || null })} /></div>)}</div>}
          </article>)}
          <div className="rounded-lg border bg-muted/20 p-3"><Label>{t("materialRates.boqWorkbook.saveTemplate")}</Label><div className="mt-2 flex gap-2"><Input data-testid="boq-workbook-template-name" value={templateName} maxLength={120} placeholder={t("materialRates.boqWorkbook.templateName")} onChange={event => setTemplateName(event.target.value)} /><Button data-testid="boq-workbook-template-save" variant="outline" onClick={() => void saveTemplate()} disabled={selected.length !== 1 || !templateName.trim() || !mappingsValid || busy}><Save className="mr-2 h-4 w-4" />{t("common.save")}</Button></div><p className="mt-1 text-xs text-muted-foreground">{t("materialRates.boqWorkbook.saveTemplateHint")}</p></div>
        </section>}
        {error && <p role="alert" className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">{error}</p>}
      </div>
      <DialogFooter><Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>{t("common.cancel")}</Button><Button data-testid="boq-workbook-confirm" onClick={() => void confirm()} disabled={!preview || !mappingsValid || busy}><UploadCloud className="mr-2 h-4 w-4" />{t("materialRates.boqWorkbook.import", { count: selectedCount })}</Button></DialogFooter>
    </DialogContent>
  </Dialog>;
}
