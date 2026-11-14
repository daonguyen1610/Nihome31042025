import { useEffect, useState } from "react";
import { AlertTriangle, Download, FileCheck2, UploadCloud } from "lucide-react";
import { BoqWorkbookProblemView } from "@/components/admin/BoqWorkbookUploadDialog";
import { InvestmentRateGroupList } from "@/components/admin/InvestmentRateGroupList";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { boqWorkbookProblem, saveBlob, type BoqWorkbookProblem } from "@/lib/boqWorkbook";
import { formatNumber } from "@/lib/displaySettings";
import { useI18n } from "@/lib/i18n";
import { formatVnd } from "@/lib/numberFormat";
import {
  adminApi,
  type InvestmentRateTemplate,
  type InvestmentRateWorkbookPreview,
  type WorkPackageInvestmentRate,
} from "@/services/adminApi";

const selectClass = "h-10 w-full min-w-0 rounded-md border bg-background px-3 text-sm";

// Procurement picks the form for the building type, checks the filled file, then saves it as a Draft.
export function InvestmentRateUploadDialog({
  workPackageId,
  open,
  onOpenChange,
  onCreated,
}: {
  workPackageId: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: (revision: WorkPackageInvestmentRate) => void;
}) {
  const { t } = useI18n();
  const [templates, setTemplates] = useState<InvestmentRateTemplate[]>([]);
  const [templateCode, setTemplateCode] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<InvestmentRateWorkbookPreview | null>(null);
  const [problem, setProblem] = useState<BoqWorkbookProblem | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!open) return;
    setFile(null);
    setPreview(null);
    setProblem(null);
    adminApi
      .listInvestmentRateTemplates()
      .then(({ data }) => {
        setTemplates(data);
        setTemplateCode((current) => current || (data[0]?.code ?? ""));
      })
      .catch(async (error) => setProblem(await boqWorkbookProblem(error, t)));
  }, [open, workPackageId, t]);

  const run = async (action: () => Promise<void>) => {
    setBusy(true);
    setProblem(null);
    try {
      await action();
    } catch (error) {
      setProblem(await boqWorkbookProblem(error, t));
    } finally {
      setBusy(false);
    }
  };
  const choose = (next: File | null) => {
    setFile(next);
    setPreview(null);
    setProblem(null);
  };
  const template = templates.find((item) => item.code === templateCode);

  return (
    <Dialog
      open={open}
      onOpenChange={(value) => {
        if (!busy) onOpenChange(value);
      }}
    >
      <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t("investmentRate.dialog.title")}</DialogTitle>
          <DialogDescription>{t("investmentRate.dialog.hint")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          <div className="grid gap-2">
            <Label htmlFor="investment-rate-template">{t("investmentRate.dialog.template")}</Label>
            <div className="flex flex-col gap-2 sm:flex-row">
              <select
                id="investment-rate-template"
                className={selectClass}
                value={templateCode}
                disabled={busy}
                onChange={(event) => {
                  setTemplateCode(event.target.value);
                  choose(null);
                }}
              >
                {templates.map((item) => (
                  <option key={item.code} value={item.code}>
                    {t(item.nameKey)}
                  </option>
                ))}
              </select>
              <Button
                type="button"
                variant="outline"
                className="shrink-0"
                disabled={busy || !template}
                data-testid="investment-rate-template-download"
                onClick={() =>
                  void run(async () => {
                    const { data } = await adminApi.downloadInvestmentRateTemplate(template!.code);
                    saveBlob(data, template!.file);
                  })
                }
              >
                <Download className="mr-2 h-4 w-4" />
                {t("investmentRate.dialog.downloadTemplate")}
              </Button>
            </div>
          </div>
          <Label
            className="flex min-h-28 cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed bg-background px-4 py-5 text-center transition-colors hover:border-primary/60 focus-within:ring-2 focus-within:ring-ring"
            onDragOver={(event) => event.preventDefault()}
            onDrop={(event) => {
              event.preventDefault();
              if (!busy) choose(event.dataTransfer.files?.[0] ?? null);
            }}
          >
            <UploadCloud className="h-7 w-7 text-primary" />
            <span className="mt-2 text-sm font-medium">{t("boqWorkbook.select")}</span>
            <span className="mt-1 text-xs text-muted-foreground">{t("boqWorkbook.fileLimit")}</span>
            <Input
              aria-label={t("boqWorkbook.file")}
              type="file"
              accept=".xlsx"
              className="sr-only"
              disabled={busy}
              onChange={(event) => {
                choose(event.target.files?.[0] ?? null);
                event.target.value = "";
              }}
            />
          </Label>
          {file && (
            <div className="flex flex-col gap-3 rounded-md border p-3 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex min-w-0 items-center gap-2">
                <FileCheck2 className="h-5 w-5 shrink-0 text-emerald-600" />
                <span className="break-all text-sm font-medium">{file.name}</span>
              </div>
              <Button
                type="button"
                variant="outline"
                disabled={busy || !templateCode}
                data-testid="investment-rate-validate"
                onClick={() =>
                  void run(async () => {
                    const { data } = await adminApi.previewWorkPackageInvestmentRate(
                      workPackageId,
                      templateCode,
                      file,
                    );
                    setPreview(data);
                  })
                }
              >
                {t("boqWorkbook.validate")}
              </Button>
            </div>
          )}
          {problem && <BoqWorkbookProblemView problem={problem} />}
          {preview && (
            <section
              className="space-y-3 rounded-lg border p-3"
              data-testid="investment-rate-preview"
            >
              <h3 className="text-sm font-semibold">{t("investmentRate.dialog.previewTitle")}</h3>
              <InvestmentRateTotals value={preview} />
              {(preview.convertedAreaSqm <= 0 || preview.ratePerSqm <= 0) && (
                <p className="flex gap-2 rounded-md border border-amber-300/70 bg-amber-50 p-2 text-xs text-amber-950 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-100">
                  <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                  {t("investmentRate.dialog.zeroHint")}
                </p>
              )}
              <InvestmentRateGroupList groups={preview.groups} />
            </section>
          )}
        </div>
        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            disabled={busy}
            onClick={() => onOpenChange(false)}
          >
            {t("common.cancel")}
          </Button>
          <Button
            type="button"
            disabled={busy || !preview || !file}
            data-testid="investment-rate-confirm"
            onClick={() =>
              void run(async () => {
                const { data } = await adminApi.importWorkPackageInvestmentRate(workPackageId, {
                  templateCode,
                  file: file!,
                  sourceHash: preview!.sourceHash,
                });
                onCreated(data);
              })
            }
          >
            {t("boqWorkbook.confirm")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Converted area, rate per m² and estimated value of an investment-rate form. */
export function InvestmentRateTotals({
  value,
}: {
  value: { convertedAreaSqm: number; ratePerSqm: number; total: number };
}) {
  const { t } = useI18n();
  return (
    <dl className="grid gap-2 text-sm sm:grid-cols-3">
      <div>
        <dt className="text-xs text-muted-foreground">{t("investmentRate.field.convertedArea")}</dt>
        <dd className="font-medium tabular-nums">
          {formatNumber(value.convertedAreaSqm, undefined, { maximumFractionDigits: 2 })} m²
        </dd>
      </div>
      <div>
        <dt className="text-xs text-muted-foreground">{t("investmentRate.field.rate")}</dt>
        <dd className="font-medium tabular-nums">{formatVnd(value.ratePerSqm)} ₫/m²</dd>
      </div>
      <div>
        <dt className="text-xs text-muted-foreground">{t("investmentRate.field.total")}</dt>
        <dd className="font-semibold tabular-nums">{formatVnd(value.total)} ₫</dd>
      </div>
    </dl>
  );
}
