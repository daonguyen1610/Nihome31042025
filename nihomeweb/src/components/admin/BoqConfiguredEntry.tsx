import { useCallback, useEffect, useState } from "react";
import { Download, FileSpreadsheet, UploadCloud, Plus, Info, FileCheck2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { useI18n } from "@/lib/i18n";
import api from "@/lib/api";
import { formatVnd, parseVndDecimal } from "@/lib/numberFormat";
import { extractApiError } from "@/lib/apiError";
import type {
  MaterialRateCatalogResponse,
  MaterialRateRevisionResponse,
} from "@/services/adminApi";

export interface BoqEntryField {
  key: string;
  nameVi: string;
  name: string;
  nameZh: string;
  nameJa: string;
  type: "Text" | "Number";
  binding: string;
  required: boolean;
  maxLength: number;
  decimalPlaces: number;
}
export interface BoqEntrySheet {
  key: string;
  nameVi: string;
  name: string;
  nameZh: string;
  nameJa: string;
  sourceRegion?: string;
  isPriced: boolean;
  fields: BoqEntryField[];
}
export interface BoqConfiguration {
  version: number;
  sourceFile?: string;
  sheets: BoqEntrySheet[];
}
export interface BoqEntryRow {
  sheetKey: string;
  values: Record<string, string>;
}
const nameKeys = ["nameVi", "name", "nameZh", "nameJa"] as const;
const languageLabels = ["VI", "EN", "ZH", "JA"];
const selectClass = "h-10 w-full min-w-0 rounded-md border bg-background px-3 text-sm";
const messageKey = (error: unknown) =>
  (error as { response?: { data?: { messageKey?: string } } }).response?.data?.messageKey;
const detail = (error: unknown) => {
  const context = (error as { response?: { data?: { context?: string } } }).response?.data?.context;
  return context ? " — " + context : "";
};
const download = (blob: Blob, filename: string) => {
  const href = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = href;
  anchor.download = filename;
  anchor.click();
  setTimeout(() => URL.revokeObjectURL(href), 1000);
};

function BoqTemplateDownload({ catalog }: { catalog: MaterialRateCatalogResponse }) {
  const { t, lang } = useI18n();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  return (
    <div className="w-full sm:w-auto sm:shrink-0">
      <Button
        variant="outline"
        className="w-full sm:w-auto"
        disabled={busy}
        data-testid="boq-template-download"
        onClick={async () => {
          setBusy(true);
          setError(null);
          try {
            const { data } = await api.get(
              `/material-rate-catalogs/${catalog.id}/configured-boq-template`,
              { params: { language: lang }, responseType: "blob" },
            );
            download(data, `${catalog.code}.xlsx`);
          } catch (error) {
            setError(extractApiError(error));
          } finally {
            setBusy(false);
          }
        }}
      >
        <Download className="mr-2 h-4 w-4 shrink-0" />
        {t("materialRates.package.download")}
      </Button>
      {error && (
        <p role="alert" className="mt-2 text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}

export function BoqCategorySetup({
  catalog,
  catalogs,
  canManage,
  onSaved,
}: {
  catalog: MaterialRateCatalogResponse;
  catalogs: MaterialRateCatalogResponse[];
  canManage: boolean;
  onSaved: () => void;
}) {
  const { t, lang } = useI18n();
  const [open, setOpen] = useState(false);
  const [activeSheet, setActiveSheet] = useState(0);
  const [configuration, setConfiguration] = useState<BoqConfiguration | null>(null);
  const [busy, setBusy] = useState(false);
  const [sources, setSources] = useState(catalogs);
  useEffect(() => {
    if (open)
      void api
        .get<MaterialRateCatalogResponse[]>("/material-rate-catalogs", {
          params: { catalogType: "Boq" },
        })
        .then(({ data }) => setSources(data))
        .catch((error) => setError(extractApiError(error)));
  }, [open]);
  const [error, setError] = useState<string | null>(null);
  const label = (item: BoqEntrySheet | BoqEntryField) =>
    item[lang === "vi" ? "nameVi" : lang === "zh" ? "nameZh" : lang === "ja" ? "nameJa" : "name"];
  const update = (sheetIndex: number, fieldIndex: number | null, key: string, value: unknown) =>
    setConfiguration(
      (current) =>
        current && {
          ...current,
          sheets: current.sheets.map((sheet, index) =>
            index !== sheetIndex
              ? sheet
              : fieldIndex == null
                ? { ...sheet, [key]: value }
                : {
                    ...sheet,
                    fields: sheet.fields.map((field, index) =>
                      index === fieldIndex ? { ...field, [key]: value } : field,
                    ),
                  },
          ),
        },
    );
  const save = async () => {
    if (!configuration) return;
    setBusy(true);
    setError(null);
    try {
      await api.put(`/material-rate-catalogs/${catalog.id}/boq-configuration`, {
        configuration,
        expectedUpdatedAt: catalog.updatedAt,
      });
      setOpen(false);
      onSaved();
    } catch (error) {
      setError(messageKey(error) ? t(messageKey(error)!) + detail(error) : extractApiError(error));
    } finally {
      setBusy(false);
    }
  };
  return (
    <section className="space-y-3 rounded-lg border bg-card p-4" data-testid="boq-category-setup">
      <h3 className="font-semibold">{t("boqConfigured.title")}</h3>
      <p className="text-sm text-muted-foreground">
        {t(catalog.boqConfiguration ? "boqConfigured.configuredHint" : "boqConfigured.setupHint")}
      </p>
      <div className="flex flex-wrap gap-2">
        {catalog.boqConfiguration && catalog.revisionCount === 0 && (
          <BoqTemplateDownload catalog={catalog} />
        )}
        {canManage && catalog.revisionCount === 0 && (
          <Button
            variant="outline"
            onClick={() => {
              setConfiguration(
                catalog.boqConfiguration ? structuredClone(catalog.boqConfiguration) : null,
              );
              setError(null);
              setActiveSheet(0);
              setOpen(true);
            }}
          >
            {t("boqConfigured.configure")}
          </Button>
        )}
      </div>
      {error && !open && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      <Dialog
        open={open}
        onOpenChange={(value) => {
          if (!busy) setOpen(value);
        }}
      >
        <DialogContent className="max-w-4xl">
          <DialogHeader>
            <DialogTitle>
              {t("boqConfigured.configure")} — {catalog.name}
            </DialogTitle>
          </DialogHeader>
          <form
            onSubmit={(event) => {
              event.preventDefault();
              void save();
            }}
            className="space-y-4"
          >
            <Label htmlFor="boq-copy-category">{t("boqConfigured.copy")}</Label>
            <select
              id="boq-copy-category"
              className={selectClass}
              defaultValue=""
              onChange={(event) => {
                const source = sources.find((item) => String(item.id) === event.target.value);
                if (source?.boqConfiguration)
                  setConfiguration(structuredClone(source.boqConfiguration));
              }}
            >
              <option value="" disabled>
                {t("boqConfigured.choose")}
              </option>
              {sources
                .filter((item) => item.boqConfiguration)
                .map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                  </option>
                ))}
            </select>
            {configuration && (
              <Label>
                {t("boqConfigured.sheet")}
                <select
                  className={selectClass}
                  value={Math.min(activeSheet, configuration.sheets.length - 1)}
                  onChange={(event) => setActiveSheet(Number(event.target.value))}
                >
                  {configuration.sheets.map((sheet, index) => (
                    <option key={index} value={index}>
                      {label(sheet)}
                    </option>
                  ))}
                </select>
              </Label>
            )}
            {configuration?.sheets.map((sheet, sheetIndex) =>
              sheetIndex !== Math.min(activeSheet, configuration.sheets.length - 1) ? null : (
                <fieldset key={sheetIndex} className="space-y-3 rounded-lg border p-3">
                  <legend className="px-1 text-sm font-semibold">
                    {label(sheet)} ·{" "}
                    {t(sheet.isPriced ? "boqConfigured.priced" : "boqConfigured.reference")}
                  </legend>
                  <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-5">
                    <Label>
                      {t("boqConfigured.sheetCode")}
                      <Input
                        required
                        maxLength={20}
                        pattern="[a-z][a-z0-9_]*"
                        value={sheet.key}
                        onChange={(e) => update(sheetIndex, null, "key", e.target.value)}
                      />
                    </Label>
                    {nameKeys.map((key, index) => (
                      <Label key={key}>
                        {languageLabels[index]}
                        <Input
                          required
                          maxLength={120}
                          value={sheet[key]}
                          onChange={(e) => update(sheetIndex, null, key, e.target.value)}
                        />
                      </Label>
                    ))}
                  </div>
                  {sheet.fields.map((field, fieldIndex) => (
                    <div key={fieldIndex} className="space-y-2 rounded-md bg-muted/30 p-3">
                      <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-5">
                        <Label>
                          {t("boqConfigured.fieldCode")}
                          <Input
                            required
                            maxLength={40}
                            pattern="[a-z][a-z0-9_]*"
                            value={field.key}
                            onChange={(e) => update(sheetIndex, fieldIndex, "key", e.target.value)}
                          />
                        </Label>
                        {nameKeys.map((key, index) => (
                          <Label key={key}>
                            {languageLabels[index]}
                            <Input
                              required
                              maxLength={120}
                              value={field[key]}
                              onChange={(e) => update(sheetIndex, fieldIndex, key, e.target.value)}
                            />
                          </Label>
                        ))}
                      </div>
                      <div className="flex flex-wrap items-end gap-3">
                        <Label>
                          {t("boqConfigured.type")}
                          <select
                            className={selectClass}
                            disabled={field.binding !== "None"}
                            value={field.type}
                            onChange={(e) => update(sheetIndex, fieldIndex, "type", e.target.value)}
                          >
                            <option value="Text">{t("boqConfigured.text")}</option>
                            <option value="Number">{t("boqConfigured.number")}</option>
                          </select>
                        </Label>
                        <Label className="flex items-center gap-2">
                          <input
                            type="checkbox"
                            disabled={field.binding !== "None"}
                            checked={field.required}
                            onChange={(e) =>
                              update(sheetIndex, fieldIndex, "required", e.target.checked)
                            }
                          />
                          {t("boqConfigured.required")}
                        </Label>
                        {field.type === "Number" ? (
                          <Label>
                            {t("boqConfigured.precision")}
                            <Input
                              type="number"
                              min={0}
                              max={field.binding === "UnitPrice" ? 2 : 4}
                              value={field.decimalPlaces}
                              onChange={(e) =>
                                update(
                                  sheetIndex,
                                  fieldIndex,
                                  "decimalPlaces",
                                  Number(e.target.value),
                                )
                              }
                            />
                          </Label>
                        ) : (
                          <Label>
                            {t("boqConfigured.maxLength")}
                            <Input
                              type="number"
                              min={1}
                              max={
                                field.binding === "Code"
                                  ? 38
                                  : field.binding === "Unit"
                                    ? 30
                                    : field.binding === "Name"
                                      ? 300
                                      : 2000
                              }
                              value={field.maxLength}
                              onChange={(e) =>
                                update(sheetIndex, fieldIndex, "maxLength", Number(e.target.value))
                              }
                            />
                          </Label>
                        )}
                        {field.binding === "None" && (
                          <Button
                            type="button"
                            variant="outline"
                            onClick={() =>
                              update(
                                sheetIndex,
                                null,
                                "fields",
                                sheet.fields.filter((_, index) => index !== fieldIndex),
                              )
                            }
                          >
                            {t("boqConfigured.removeField")}
                          </Button>
                        )}
                      </div>
                    </div>
                  ))}
                  <div className="flex flex-wrap gap-2">
                    <Button
                      type="button"
                      variant="outline"
                      disabled={sheet.fields.length >= 40}
                      onClick={() =>
                        update(sheetIndex, null, "fields", [
                          ...sheet.fields,
                          {
                            key: `field_${Date.now()}`,
                            nameVi: "",
                            name: "",
                            nameZh: "",
                            nameJa: "",
                            type: "Text",
                            binding: "None",
                            required: false,
                            maxLength: 300,
                            decimalPlaces: 2,
                          },
                        ])
                      }
                    >
                      {t("boqConfigured.addField")}
                    </Button>
                    <Button
                      type="button"
                      variant="outline"
                      disabled={configuration.sheets.length <= 1}
                      onClick={() =>
                        setConfiguration({
                          ...configuration,
                          sheets: configuration.sheets.filter((_, index) => index !== sheetIndex),
                        })
                      }
                    >
                      {t("boqConfigured.removeSheet")}
                    </Button>
                  </div>
                </fieldset>
              ),
            )}
            {configuration && (
              <Button
                type="button"
                variant="outline"
                disabled={configuration.sheets.length >= 40}
                onClick={() => {
                  let key = `sheet_${configuration.sheets.length + 1}`;
                  while (configuration.sheets.some((sheet) => sheet.key === key)) key += "_";
                  setConfiguration({
                    ...configuration,
                    sheets: [
                      ...configuration.sheets,
                      { ...structuredClone(configuration.sheets[0]), key },
                    ],
                  });
                }}
              >
                {t("boqConfigured.addSheet")}
              </Button>
            )}
            {error && (
              <p role="alert" className="text-sm text-destructive">
                {error}
              </p>
            )}
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setOpen(false)}
              >
                {t("common.cancel")}
              </Button>
              <Button type="submit" disabled={busy || !configuration}>
                {t("common.save")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </section>
  );
}

export function BoqConfiguredRows({
  catalog,
  revision,
  canManage,
  onSaved,
}: {
  catalog: MaterialRateCatalogResponse;
  revision: MaterialRateRevisionResponse;
  canManage: boolean;
  onSaved: (revision: MaterialRateRevisionResponse) => void;
}) {
  const { t, lang } = useI18n();
  const config = catalog.boqConfiguration!;
  const [sheetKey, setSheetKey] = useState(config.sheets[0].key);
  const [row, setRow] = useState<Record<string, string>>({});
  const [editing, setEditing] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<{ sourceHash: string; rows: BoqEntryRow[] } | null>(null);
  const [editingOpen, setEditingOpen] = useState(false);
  const sheet = config.sheets.find((sheet) => sheet.key === sheetKey)!;
  const label = (item: BoqEntrySheet | BoqEntryField) =>
    item[lang === "vi" ? "nameVi" : lang === "zh" ? "nameZh" : lang === "ja" ? "nameJa" : "name"];
  const editable = canManage && revision.status === "Draft";
  useEffect(() => {
    setRow({});
    setEditing(null);
    setPreview(null);
    setFile(null);
    setEditingOpen(false);
  }, [catalog.id, revision.id]);
  const run = async (action: () => Promise<void>) => {
    setBusy(true);
    setError(null);
    try {
      await action();
    } catch (error) {
      setError(messageKey(error) ? t(messageKey(error)!) + detail(error) : extractApiError(error));
    } finally {
      setBusy(false);
    }
  };
  const saveRows = (rows: BoqEntryRow[]) =>
    run(async () => {
      const { data } = await api.put<MaterialRateRevisionResponse>(
        `/material-rate-catalogs/${catalog.id}/revisions/${revision.id}/configured-boq-rows`,
        {
          rows,
          configurationHash: catalog.boqConfigurationHash,
          expectedUpdatedAt: revision.updatedAt,
        },
      );
      onSaved(data);
      setEditingOpen(false);
      setRow({});
      setEditing(null);
      setPreview(null);
    });
  const rows = revision.boqRows ?? [];
  const allowed =
    Object.keys(row).every((key) => sheet.fields.some((field) => field.key === key)) &&
    sheet.fields.every((field) => {
      const value = row[field.key] ?? "";
      if (field.required && !value.trim()) return false;
      if (field.binding === "Code" && value && !/^[A-Za-z0-9][A-Za-z0-9._-]{0,37}$/.test(value))
        return false;
      if (value.length > field.maxLength) return false;
      return (
        !value ||
        field.type !== "Number" ||
        (/^\d+(\.\d+)?$/.test(value) &&
          Number.isFinite(Number(value)) &&
          Number(value) <= 999999999999 &&
          (value.split(".")[1]?.length ?? 0) <= field.decimalPlaces &&
          (field.binding !== "Quantity" || Number(value) > 0))
      );
    });
  return (
    <section className="space-y-4" data-testid="configured-boq-rows">
      {editable && (
        <section className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-primary/20 bg-primary/[0.04] p-4">
          <div className="min-w-0 flex-[1_1_16rem]">
            <h4 className="text-sm font-semibold">{t("materialRates.line.manageTitle")}</h4>
            <p className="mt-1 max-w-2xl text-xs leading-relaxed text-muted-foreground">
              {t("materialRates.line.manageHint")}
            </p>
          </div>
          <Button
            className="shrink-0"
            disabled={busy}
            onClick={() => {
              setEditing(null);
              setRow({});
              setEditingOpen(true);
            }}
          >
            <Plus className="mr-2 h-4 w-4" />
            {t("boqConfigured.addRow")}
          </Button>
        </section>
      )}
      <section className="rounded-lg border bg-muted/20 p-4" data-testid="boq-excel-entry">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0 flex-[1_1_16rem]">
            <h4 className="flex items-center gap-2 text-sm font-semibold">
              <FileSpreadsheet className="h-4 w-4 shrink-0 text-primary" />
              {t("materialRates.import.title")}
            </h4>
            <p className="mt-1 max-w-2xl text-xs leading-relaxed text-muted-foreground">
              {t("boqConfigured.rowsHint")}
            </p>
          </div>
          <BoqTemplateDownload key={catalog.id} catalog={catalog} />
        </div>
        {editable && (
          <>
            <Label
              className="mt-4 flex min-h-32 cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed bg-background px-4 py-6 text-center transition-colors hover:border-primary/60 focus-within:ring-2 focus-within:ring-ring"
              onDragOver={(event) => event.preventDefault()}
              onDrop={(event) => {
                event.preventDefault();
                if (!busy) {
                  setFile(event.dataTransfer.files?.[0] ?? null);
                  setPreview(null);
                }
              }}
            >
              <UploadCloud className="h-8 w-8 text-primary" />
              <span className="mt-2 text-sm font-medium">{t("materialRates.import.select")}</span>
              <span className="mt-1 text-xs text-muted-foreground">Excel .xlsx · 20 MB</span>
              <Input
                aria-label={t("boqConfigured.file")}
                type="file"
                accept=".xlsx"
                className="sr-only"
                disabled={busy}
                onChange={(event) => {
                  setFile(event.target.files?.[0] ?? null);
                  setPreview(null);
                  event.target.value = "";
                }}
              />
            </Label>
            {file && (
              <div className="mt-3 flex flex-col gap-3 rounded-md border bg-background p-3 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex min-w-0 items-center gap-2">
                  <FileCheck2 className="h-5 w-5 shrink-0 text-emerald-600" />
                  <span className="break-all text-sm font-medium">{file.name}</span>
                </div>
                <Button
                  variant="outline"
                  disabled={busy || !file}
                  onClick={() =>
                    void run(async () => {
                      const form = new FormData();
                      form.append("file", file!);
                      const { data } = await api.post(
                        `/material-rate-catalogs/${catalog.id}/configured-boq-preview`,
                        form,
                      );
                      setPreview(data);
                    })
                  }
                >
                  {t("boqConfigured.preview")}
                </Button>
              </div>
            )}
            <div className="mt-3 flex gap-2 rounded-md border border-amber-300/70 bg-amber-50 p-3 text-xs leading-relaxed text-amber-950 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-100">
              <Info className="mt-0.5 h-4 w-4 shrink-0" />
              <span>{t("materialRates.import.replaceWarning")}</span>
            </div>
          </>
        )}
      </section>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      {preview && (
        <div className="space-y-3 rounded-lg border p-3">
          <p>{t("boqConfigured.replaceWarning", { count: preview.rows.length })}</p>
          <div className="max-h-80 overflow-auto">
            {preview.rows.map((item, index) => (
              <p key={index} className="border-b py-2 text-sm">
                {label(config.sheets.find((sheet) => sheet.key === item.sheetKey)!)}:{" "}
                {Object.values(item.values).join(" · ")}
              </p>
            ))}
          </div>
          <Button
            disabled={busy}
            onClick={() =>
              void run(async () => {
                const form = new FormData();
                form.append("file", file!);
                form.append("sourceHash", preview.sourceHash);
                form.append("expectedUpdatedAt", revision.updatedAt);
                const { data } = await api.post<MaterialRateRevisionResponse>(
                  `/material-rate-catalogs/${catalog.id}/revisions/${revision.id}/configured-boq-import`,
                  form,
                );
                onSaved(data);
                setPreview(null);
              })
            }
          >
            {t("boqConfigured.import")}
          </Button>
        </div>
      )}
      <Label htmlFor="boq-active-sheet">{t("boqConfigured.sheet")}</Label>
      <select
        id="boq-active-sheet"
        className={selectClass}
        value={sheetKey}
        disabled={editingOpen || busy}
        onChange={(e) => {
          setSheetKey(e.target.value);
          setRow({});
          setEditing(null);
        }}
      >
        {config.sheets.map((item) => (
          <option key={item.key} value={item.key}>
            {label(item)} — {t(item.isPriced ? "boqConfigured.priced" : "boqConfigured.reference")}
          </option>
        ))}
      </select>
      {rows.filter((item) => item.sheetKey === sheetKey).length === 0 && (
        <p className="rounded-lg border border-dashed p-5 text-sm text-muted-foreground">
          {t("boqConfigured.empty")}
        </p>
      )}
      {rows.map((item, index) =>
        item.sheetKey !== sheetKey ? null : (
          <article key={index} className="space-y-3 rounded-lg border p-4">
            <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {sheet.fields.map((field) => (
                <div key={field.key} className="min-w-0">
                  <dt className="text-xs text-muted-foreground">{label(field)}</dt>
                  <dd className="break-words text-sm">
                    {field.type === "Number" && item.values[field.key]
                      ? formatVnd(item.values[field.key])
                      : item.values[field.key] || "—"}
                  </dd>
                </div>
              ))}
            </dl>
            {editable && (
              <div className="flex flex-wrap gap-2">
                <Button
                  variant="outline"
                  disabled={busy}
                  onClick={() => {
                    setEditing(index);
                    setRow({ ...item.values });
                    setEditingOpen(true);
                  }}
                >
                  {t("common.edit")}
                </Button>
                <Button
                  variant="outline"
                  disabled={busy}
                  onClick={() => void saveRows(rows.filter((_, i) => i !== index))}
                >
                  {t("boqConfigured.removeRow")}
                </Button>
              </div>
            )}
          </article>
        ),
      )}
      <Dialog
        open={editingOpen}
        onOpenChange={(value) => {
          if (!busy) setEditingOpen(value);
        }}
      >
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>
              {t("boqConfigured.addRow")} — {label(sheet)}
            </DialogTitle>
          </DialogHeader>
          <form
            className="space-y-6"
            onSubmit={(event) => {
              event.preventDefault();
              if (allowed)
                void saveRows(
                  editing == null
                    ? [...rows, { sheetKey, values: row }]
                    : rows.map((item, index) =>
                        index === editing ? { sheetKey, values: row } : item,
                      ),
                );
            }}
          >
            <div className="grid gap-x-4 gap-y-5 sm:grid-cols-2">
              {sheet.fields.map((field) => (
                <div key={field.key} className="grid min-w-0 gap-2">
                  <Label htmlFor={`boq-entry-${field.key}`} className="leading-5">
                    {label(field)}
                    {field.required ? " *" : ""}
                  </Label>
                  {field.type === "Number" ? (
                    <BoqNumberEntry
                      id={`boq-entry-${field.key}`}
                      value={row[field.key] ?? ""}
                      precision={field.decimalPlaces}
                      onChange={(value) =>
                        setRow((current) => ({ ...current, [field.key]: value }))
                      }
                    />
                  ) : (
                    <Input
                      id={`boq-entry-${field.key}`}
                      required={field.required}
                      maxLength={field.maxLength}
                      value={row[field.key] ?? ""}
                      onChange={(event) =>
                        setRow((current) => ({ ...current, [field.key]: event.target.value }))
                      }
                    />
                  )}
                </div>
              ))}
            </div>
            {!allowed && (
              <p className="text-sm leading-relaxed text-muted-foreground">
                {t("boqConfigured.rules")}
              </p>
            )}
            {error && (
              <p role="alert" className="text-sm text-destructive">
                {error}
              </p>
            )}
            <DialogFooter className="border-t pt-4">
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setEditingOpen(false)}
              >
                {t("common.cancel")}
              </Button>
              <Button type="submit" disabled={busy || !allowed}>
                {t("common.save")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </section>
  );
}

function BoqNumberEntry({
  id,
  value,
  precision,
  onChange,
}: {
  id: string;
  value: string;
  precision: number;
  onChange: (value: string) => void;
}) {
  const display = useCallback(
    (value: string) =>
      value && value !== "invalid"
        ? new Intl.NumberFormat("vi-VN", { maximumFractionDigits: precision }).format(Number(value))
        : "",
    [precision],
  );
  const [text, setText] = useState(() => display(value));
  const [focused, setFocused] = useState(false);
  useEffect(() => {
    if (!focused && value !== "invalid") setText(display(value));
  }, [value, focused, display]);
  const change = (raw: string) => {
    setText(raw);
    if (!raw.trim()) {
      onChange("");
      return;
    }
    const numeric = parseVndDecimal(raw, precision);
    onChange(Number.isFinite(numeric) ? String(numeric) : "invalid");
  };
  return (
    <Input
      id={id}
      inputMode="decimal"
      value={text}
      onFocus={() => setFocused(true)}
      onChange={(event) => change(event.target.value)}
      onBlur={() => {
        setFocused(false);
        if (value !== "invalid") setText(display(value));
      }}
    />
  );
}
