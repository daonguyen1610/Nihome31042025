import { useEffect, useMemo, useState } from "react";
import { CheckCircle2, FileSpreadsheet, Loader2, Save, Settings2, UploadCloud } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  adminApi,
  type BoqImportTemplateResponse,
  type BoqWorkbookPreviewResponse,
  type BoqWorkbookSheetPreviewResponse,
  type MaterialRateRevisionResponse,
} from "@/services/adminApi";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  catalogId: number;
  revisionId: number;
  onImported: (revision: MaterialRateRevisionResponse) => void;
}
const mappingFields = [
  "descriptionColumn",
  "unitColumn",
  "quantityColumn",
  "unitPriceColumn",
  "itemCodeColumn",
] as const;

export default function BoqCatalogWorkbookDialog({
  open,
  onOpenChange,
  catalogId,
  revisionId,
  onImported,
}: Props) {
  const { t } = useI18n();
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<BoqWorkbookPreviewResponse | null>(null);
  const [templates, setTemplates] = useState<BoqImportTemplateResponse[]>([]);
  const [activeIndex, setActiveIndex] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [templateName, setTemplateName] = useState("");

  useEffect(() => {
    if (open) void adminApi.listBoqImportTemplates().then(({ data }) => setTemplates(data));
  }, [open]);
  const selected = useMemo(() => preview?.sheets.filter((sheet) => sheet.include) ?? [], [preview]);
  const mappingsValid =
    selected.length > 0 &&
    selected.every((sheet) => {
      const required = [
        sheet.headerRow,
        sheet.descriptionColumn,
        sheet.unitColumn,
        sheet.quantityColumn,
        sheet.unitPriceColumn,
      ];
      const columns = [
        sheet.itemCodeColumn,
        sheet.descriptionColumn,
        sheet.unitColumn,
        sheet.quantityColumn,
        sheet.unitPriceColumn,
      ].filter((value): value is number => value != null);
      return (
        required.every((value) => value != null && value >= 1) &&
        new Set(columns).size === columns.length
      );
    });
  const selectedCount = useMemo(
    () => selected.reduce((sum, sheet) => sum + sheet.candidateCount, 0),
    [selected],
  );
  const activeSheet = activeIndex == null ? null : (preview?.sheets[activeIndex] ?? null);
  const update = (index: number, patch: Partial<BoqWorkbookSheetPreviewResponse>) =>
    setPreview((current) =>
      current
        ? {
            ...current,
            sheets: current.sheets.map((sheet, i) =>
              i === index ? { ...sheet, ...patch } : sheet,
            ),
          }
        : current,
    );
  const applyTemplate = (index: number, id: string) => {
    const template = templates.find((item) => item.id === Number(id));
    if (template)
      update(index, {
        headerRow: template.headerRow,
        itemCodeColumn: template.itemCodeColumn,
        descriptionColumn: template.descriptionColumn,
        unitColumn: template.unitColumn,
        quantityColumn: template.quantityColumn,
        unitPriceColumn: template.unitPriceColumn,
      });
  };
  const runPreview = async () => {
    if (!file) return;
    setBusy(true);
    setError(null);
    try {
      const { data } = await adminApi.previewBoqWorkbook(catalogId, revisionId, file);
      const sheets = data.sheets.map((sheet) => ({
        ...sheet,
        include:
          sheet.candidateCount > 0 &&
          Boolean(
            sheet.headerRow &&
            sheet.descriptionColumn &&
            sheet.unitColumn &&
            sheet.quantityColumn &&
            sheet.unitPriceColumn,
          ),
      }));
      setPreview({ ...data, sheets });
      const firstDetail = sheets.findIndex((sheet) => sheet.include);
      setActiveIndex(firstDetail >= 0 ? firstDetail : 0);
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setBusy(false);
    }
  };
  const saveTemplate = async () => {
    if (!activeSheet || !templateName.trim() || !mappingsValid) return;
    setBusy(true);
    setError(null);
    try {
      const { data } = await adminApi.createBoqImportTemplate({
        name: templateName.trim(),
        headerRow: activeSheet.headerRow!,
        itemCodeColumn: activeSheet.itemCodeColumn,
        descriptionColumn: activeSheet.descriptionColumn!,
        unitColumn: activeSheet.unitColumn!,
        quantityColumn: activeSheet.quantityColumn!,
        unitPriceColumn: activeSheet.unitPriceColumn!,
      });
      setTemplates((items) => [...items, data]);
      setTemplateName("");
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setBusy(false);
    }
  };
  const confirm = async () => {
    if (!file || !preview || !mappingsValid) return;
    setBusy(true);
    setError(null);
    try {
      const { data } = await adminApi.importBoqWorkbook(
        catalogId,
        revisionId,
        file,
        preview.sourceHash,
        preview.sheets,
      );
      onImported(data);
      onOpenChange(false);
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(value) => !busy && onOpenChange(value)}>
      <DialogContent
        className="max-h-[calc(100dvh-1rem)] min-w-0 max-w-7xl grid-rows-[auto_minmax(0,1fr)_auto] overflow-hidden"
        data-testid="boq-catalog-workbook-dialog"
      >
        <DialogHeader className="min-w-0 pr-6">
          <DialogTitle className="break-words leading-snug">
            {t("materialRates.boqWorkbook.title")}
          </DialogTitle>
          <DialogDescription>{t("materialRates.boqWorkbook.description")}</DialogDescription>
        </DialogHeader>
        <div className="min-h-0 space-y-4 overflow-y-auto pr-1">
          <section className="rounded-lg border bg-muted/20 p-3 sm:p-4">
            <Label htmlFor="boq-workbook-file">{t("materialRates.boqWorkbook.step1")}</Label>
            <div className="mt-2 flex min-w-0 flex-col gap-2 sm:flex-row">
              <Input
                className="min-w-0"
                id="boq-workbook-file"
                type="file"
                accept=".xlsx"
                data-testid="boq-workbook-file"
                onChange={(event) => {
                  setFile(event.target.files?.[0] ?? null);
                  setPreview(null);
                  setActiveIndex(null);
                  setError(null);
                }}
              />
              <Button
                className="w-full shrink-0 sm:w-auto"
                onClick={() => void runPreview()}
                disabled={!file || busy}
                data-testid="boq-workbook-preview"
              >
                {busy ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <FileSpreadsheet className="mr-2 h-4 w-4" />
                )}
                {t("materialRates.boqWorkbook.preview")}
              </Button>
            </div>
          </section>
          {preview && (
            <section className="space-y-3">
              <div>
                <h3 className="font-semibold">{t("materialRates.boqWorkbook.step2")}</h3>
                <p className="text-sm text-muted-foreground">
                  {t("materialRates.boqWorkbook.mappingHint", { count: selectedCount })}
                </p>
              </div>
              <div className="grid gap-4 lg:grid-cols-[minmax(240px,0.8fr)_minmax(0,2.2fr)]">
                <div
                  className="max-h-48 space-y-1 overflow-y-auto rounded-lg border p-2 lg:max-h-[58vh]"
                  data-testid="boq-workbook-sheet-list"
                >
                  {preview.sheets.map((sheet, index) => (
                    <div
                      key={sheet.name}
                      className={cn(
                        "flex items-center gap-2 rounded-md px-2 hover:bg-muted",
                        activeIndex === index && "bg-muted",
                      )}
                    >
                      <Checkbox
                        data-testid={`boq-workbook-include-${index}`}
                        checked={sheet.include}
                        aria-label={`${t("materialRates.boqWorkbook.includeSheet")} ${sheet.name}`}
                        onCheckedChange={(value) => update(index, { include: value === true })}
                      />
                      <button
                        type="button"
                        onClick={() => setActiveIndex(index)}
                        className="flex min-w-0 flex-1 items-center gap-3 py-2 text-left"
                      >
                        <span className="min-w-0 flex-1">
                          <span className="block truncate text-sm font-medium">
                            {sheet.order}. {sheet.name}
                          </span>
                          <span className="text-xs text-muted-foreground">
                            {sheet.candidateCount} {t("materialRates.boqWorkbook.rows")}
                          </span>
                        </span>
                        {sheet.include && sheet.candidateCount > 0 && (
                          <CheckCircle2 className="h-4 w-4 shrink-0 text-emerald-600" />
                        )}
                      </button>
                    </div>
                  ))}
                </div>
                {activeSheet && activeIndex != null && (
                  <div
                    className="min-w-0 space-y-4 rounded-lg border p-3 sm:p-4"
                    data-testid={`boq-workbook-sheet-${activeIndex}`}
                  >
                    <div className="flex min-w-0 flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                      <div className="min-w-0">
                        <h4 className="break-words font-semibold">{activeSheet.name}</h4>
                        <p className="text-xs text-muted-foreground">
                          {t("materialRates.boqWorkbook.visualMappingHint")}
                        </p>
                      </div>
                      <Select onValueChange={(value) => applyTemplate(activeIndex, value)}>
                        <SelectTrigger
                          className="w-full shrink-0 sm:w-56"
                          data-testid={`boq-workbook-template-${activeIndex}`}
                        >
                          <SelectValue
                            placeholder={t("materialRates.boqWorkbook.chooseTemplate")}
                          />
                        </SelectTrigger>
                        <SelectContent>
                          {templates.map((template) => (
                            <SelectItem key={template.id} value={String(template.id)}>
                              {template.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    {activeSheet.warnings.map((warning) => (
                      <p
                        key={warning}
                        className="rounded-md bg-amber-50 p-2 text-xs text-amber-800"
                      >
                        {warning}
                      </p>
                    ))}
                    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
                      {mappingFields.map((field) => (
                        <div key={field}>
                          <Label>{t(`materialRates.boqWorkbook.${field}`)}</Label>
                          <Select
                            value={activeSheet[field] == null ? "none" : String(activeSheet[field])}
                            onValueChange={(value) =>
                              update(activeIndex, {
                                [field]: value === "none" ? null : Number(value),
                              })
                            }
                          >
                            <SelectTrigger data-testid={`boq-workbook-${field}-${activeIndex}`}>
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {field === "itemCodeColumn" && (
                                <SelectItem value="none">
                                  {t("materialRates.boqWorkbook.noCode")}
                                </SelectItem>
                              )}
                              {activeSheet.columns.map((column) => (
                                <SelectItem key={column.number} value={String(column.number)}>
                                  {column.label}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                      ))}
                    </div>
                    <div
                      className="overflow-x-auto rounded-md border"
                      data-testid="boq-workbook-data-preview"
                    >
                      <table className="w-full min-w-[760px] text-sm">
                        <thead className="bg-muted/50">
                          <tr>
                            <th className="p-2 text-left">#</th>
                            {mappingFields.map((field) => (
                              <th key={field} className="p-2 text-left">
                                {t(`materialRates.boqWorkbook.${field}`)}
                              </th>
                            ))}
                          </tr>
                        </thead>
                        <tbody className="divide-y">
                          {activeSheet.previewRows.map((row) => (
                            <tr key={row.rowNumber}>
                              <td className="p-2 text-muted-foreground">{row.rowNumber}</td>
                              {mappingFields.map((field) => (
                                <td
                                  key={field}
                                  className="max-w-72 truncate p-2"
                                  title={activeSheet[field] ? row.cells[activeSheet[field]!] : ""}
                                >
                                  {activeSheet[field] ? row.cells[activeSheet[field]!] || "—" : "—"}
                                </td>
                              ))}
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                    <details className="rounded-md border p-3">
                      <summary className="flex cursor-pointer items-center gap-2 text-sm font-medium">
                        <Settings2 className="h-4 w-4" />
                        {t("materialRates.boqWorkbook.advanced")}
                      </summary>
                      <div className="mt-3 space-y-3">
                        <div className="max-w-48">
                          <Label>{t("materialRates.boqWorkbook.headerRow")}</Label>
                          <Input
                            data-testid={`boq-workbook-headerRow-${activeIndex}`}
                            type="number"
                            min={1}
                            value={activeSheet.headerRow ?? ""}
                            onChange={(event) =>
                              update(activeIndex, { headerRow: Number(event.target.value) || null })
                            }
                          />
                        </div>
                        <div>
                          <Label>{t("materialRates.boqWorkbook.saveTemplate")}</Label>
                          <div className="mt-2 flex flex-col gap-2 sm:flex-row">
                            <Input
                              data-testid="boq-workbook-template-name"
                              value={templateName}
                              maxLength={120}
                              placeholder={t("materialRates.boqWorkbook.templateName")}
                              onChange={(event) => setTemplateName(event.target.value)}
                            />
                            <Button
                              data-testid="boq-workbook-template-save"
                              variant="outline"
                              onClick={() => void saveTemplate()}
                              disabled={
                                !activeSheet.include ||
                                !templateName.trim() ||
                                !mappingsValid ||
                                busy
                              }
                            >
                              <Save className="mr-2 h-4 w-4" />
                              {t("common.save")}
                            </Button>
                          </div>
                          <p className="mt-1 text-xs text-muted-foreground">
                            {t("materialRates.boqWorkbook.saveTemplateHint")}
                          </p>
                        </div>
                      </div>
                    </details>
                  </div>
                )}
              </div>
            </section>
          )}
          {error && (
            <p role="alert" className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">
              {error}
            </p>
          )}
        </div>
        <DialogFooter className="gap-2 sm:space-x-0">
          <Button
            className="w-full sm:w-auto"
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={busy}
          >
            {t("common.cancel")}
          </Button>
          <Button
            className="w-full sm:w-auto"
            data-testid="boq-workbook-confirm"
            onClick={() => void confirm()}
            disabled={!preview || !mappingsValid || busy}
          >
            <UploadCloud className="mr-2 h-4 w-4" />
            {t("materialRates.boqWorkbook.import", { count: selectedCount })}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
