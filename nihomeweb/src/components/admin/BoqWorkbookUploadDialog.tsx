import { useEffect, useState } from "react";
import { AlertTriangle, Download, FileCheck2, UploadCloud } from "lucide-react";
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
import { useI18n } from "@/lib/i18n";
import { formatVnd } from "@/lib/numberFormat";
import {
  adminApi,
  type BoqTemplateResponse,
  type ProjectBoqWorkbookPreviewResponse,
  type ContractResponse,
} from "@/services/adminApi";

const selectClass = "h-10 w-full min-w-0 rounded-md border bg-background px-3 text-sm";

function ProblemView({ problem }: { problem: BoqWorkbookProblem }) {
  const { t } = useI18n();
  if ("message" in problem)
    return (
      <p role="alert" className="text-sm text-destructive">
        {problem.message}
      </p>
    );
  return (
    <div
      role="alert"
      className="space-y-2 rounded-lg border border-destructive/40 bg-destructive/5 p-3"
      data-testid="boq-workbook-issues"
    >
      <p className="text-sm font-medium text-destructive">
        {t("boqWorkbook.issuesTitle", { count: problem.count })}
      </p>
      {problem.count > problem.issues.length && (
        <p className="text-xs text-muted-foreground">
          {t("boqWorkbook.issuesMore", { shown: problem.issues.length, count: problem.count })}
        </p>
      )}
      <ul className="max-h-60 space-y-2 overflow-auto">
        {problem.issues.map((issue, index) => (
          <li key={index} className="rounded-md border bg-background p-2 text-sm">
            {issue.sheet && (
              <p className="text-xs font-semibold text-muted-foreground">
                {issue.cell
                  ? t("boqWorkbook.issueCell", { sheet: issue.sheet, cell: issue.cell })
                  : t("boqWorkbook.issueSheet", { sheet: issue.sheet })}
              </p>
            )}
            <p>{t(issue.messageKey, { limit: issue.limit ?? "" })}</p>
          </li>
        ))}
      </ul>
    </div>
  );
}

// Where the BOQ is uploaded: the project page (a contract BOQ, so a signed contract is required)
// or an input-price work package (a tender BOQ), whose forms come from the BOQ template library.
export type BoqUploadTarget =
  | { kind: "project"; projectId: number; contracts: ContractResponse[] }
  | { kind: "workPackage"; workPackageId: number };

export function BoqWorkbookUploadDialog({
  target,
  open,
  onOpenChange,
  onCreated,
}: {
  target: BoqUploadTarget;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: (revision: { id: number }) => void;
}) {
  const { t } = useI18n();
  const [templates, setTemplates] = useState<BoqTemplateResponse[]>([]);
  const [templateCode, setTemplateCode] = useState("");
  const [contractId, setContractId] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ProjectBoqWorkbookPreviewResponse | null>(null);
  const [problem, setProblem] = useState<BoqWorkbookProblem | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!open) return;
    setFile(null);
    setPreview(null);
    setProblem(null);
    setContractId(
      target.kind === "project" && target.contracts.length === 1
        ? String(target.contracts[0].id)
        : "",
    );
    (target.kind === "project"
      ? adminApi.getBoqTemplates(target.projectId)
      : adminApi.listBoqLibrary()
    )
      .then(({ data }) => {
        setTemplates(data);
        setTemplateCode((current) => current || (data[0]?.code ?? ""));
      })
      .catch(async (error) => setProblem(await boqWorkbookProblem(error, t)));
    // The target identity, not the object, decides when the template list reloads.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, target.kind === "project" ? target.projectId : target.workPackageId, t]);

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
          <DialogTitle>{t("boqWorkbook.title")}</DialogTitle>
          <DialogDescription>{t("boqWorkbook.hint")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          <div className="grid gap-2">
            <Label htmlFor="boq-workbook-template">{t("boqWorkbook.template")}</Label>
            <div className="flex flex-col gap-2 sm:flex-row">
              <select
                id="boq-workbook-template"
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
                data-testid="boq-workbook-template-download"
                onClick={() =>
                  void run(async () => {
                    const { data } =
                      target.kind === "project"
                        ? await adminApi.downloadBoqTemplate(target.projectId, template!.code)
                        : await adminApi.downloadBoqLibraryFile(template!.code);
                    saveBlob(data, template!.file);
                  })
                }
              >
                <Download className="mr-2 h-4 w-4" />
                {t("boqWorkbook.download")}
              </Button>
            </div>
          </div>
          {target.kind === "project" && (
            <div className="grid gap-2">
              <Label htmlFor="boq-workbook-contract">{t("boqWorkbook.contract")}</Label>
              <select
                id="boq-workbook-contract"
                className={selectClass}
                value={contractId}
                disabled={busy}
                onChange={(event) => setContractId(event.target.value)}
              >
                <option value="" disabled>
                  {t("procurement.boq.selectSignedContract")}
                </option>
                {target.contracts.map((contract) => (
                  <option key={contract.id} value={contract.id}>
                    {contract.contractNumber}
                  </option>
                ))}
              </select>
              <p className="text-xs text-muted-foreground">{t("boqWorkbook.contractRequired")}</p>
            </div>
          )}
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
                data-testid="boq-workbook-validate"
                onClick={() =>
                  void run(async () => {
                    const { data } =
                      target.kind === "project"
                        ? await adminApi.previewProjectBoqWorkbook(
                            target.projectId,
                            templateCode,
                            file,
                          )
                        : await adminApi.previewWorkPackageBoq(
                            target.workPackageId,
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
          {problem && <ProblemView problem={problem} />}
          {preview && (
            <section className="space-y-3 rounded-lg border p-3" data-testid="boq-workbook-preview">
              <h3 className="text-sm font-semibold">{t("boqWorkbook.previewTitle")}</h3>
              {preview.sheets.map((sheet) => (
                <dl
                  key={sheet.name}
                  className="grid grid-cols-2 gap-x-3 gap-y-1 text-sm sm:grid-cols-[1fr_auto]"
                >
                  <dt className="text-muted-foreground">{t("boqWorkbook.sheet")}</dt>
                  <dd className="text-right font-medium">{sheet.name}</dd>
                  <dt className="text-muted-foreground">{t("boqWorkbook.items")}</dt>
                  <dd className="text-right tabular-nums">{sheet.itemCount}</dd>
                  <dt className="text-muted-foreground">{t("boqWorkbook.skipped")}</dt>
                  <dd className="text-right tabular-nums">{sheet.skippedCount}</dd>
                  <dt className="text-muted-foreground">{t("boqWorkbook.missingPrices")}</dt>
                  <dd className="text-right tabular-nums">{sheet.missingPriceCount}</dd>
                  <dt className="text-muted-foreground">{t("boqWorkbook.missingUnits")}</dt>
                  <dd className="text-right tabular-nums">{sheet.missingUnitCount}</dd>
                  <dt className="text-muted-foreground">{t("boqWorkbook.total")}</dt>
                  <dd className="text-right font-semibold tabular-nums">
                    {formatVnd(sheet.total)}
                  </dd>
                </dl>
              ))}
              {preview.skippedCount > 0 && (
                <p className="text-xs text-muted-foreground">{t("boqWorkbook.skippedHint")}</p>
              )}
              {[
                preview.missingPriceCount > 0 && "boqWorkbook.missingPriceHint",
                preview.missingUnitCount > 0 && "boqWorkbook.missingUnitHint",
              ]
                .filter((key): key is string => Boolean(key))
                .map((key) => (
                  <p
                    key={key}
                    className="flex gap-2 rounded-md border border-amber-300/70 bg-amber-50 p-2 text-xs text-amber-950 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-100"
                  >
                    <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                    {t(key)}
                  </p>
                ))}
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
            disabled={busy || !preview || !file || (target.kind === "project" && !contractId)}
            data-testid="boq-workbook-confirm"
            onClick={() =>
              void run(async () => {
                const body = { templateCode, file: file!, sourceHash: preview!.sourceHash };
                const { data } =
                  target.kind === "project"
                    ? await adminApi.importProjectBoqWorkbook(target.projectId, {
                        ...body,
                        upstreamContractId: Number(contractId),
                      })
                    : await adminApi.importWorkPackageBoq(target.workPackageId, body);
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
