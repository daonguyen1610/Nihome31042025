import { useState } from "react";
import { Link } from "react-router-dom";
import { AlertTriangle, Download, FileSpreadsheet, FileUp } from "lucide-react";
import { BoqWorkbookUploadDialog } from "@/components/admin/BoqWorkbookUploadDialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { saveBlob } from "@/lib/boqWorkbook";
import { useI18n } from "@/lib/i18n";
import { formatVnd } from "@/lib/numberFormat";
import { adminApi, type WorkPackageBoqWorkbook } from "@/services/adminApi";

// The BOQ half of the work package's input-pricing panel: Procurement uploads the filled BOQ form
// here; the package lead approves it and it becomes the project's tender BOQ that quotes apply.
export function WorkPackageBoqSection({
  packageId,
  open,
  boqs,
  canProgress,
  canVerify,
  currentUserId,
  onChanged,
  onSubmit,
  onDecide,
}: {
  packageId: number;
  open: boolean;
  boqs: WorkPackageBoqWorkbook[];
  canProgress: boolean;
  canVerify: boolean;
  currentUserId: number | null;
  onChanged: () => void;
  onSubmit: (boq: WorkPackageBoqWorkbook) => void;
  onDecide: (boq: WorkPackageBoqWorkbook, approved: boolean) => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [uploadOpen, setUploadOpen] = useState(false);

  const download = async (boq: WorkPackageBoqWorkbook) => {
    try {
      const { data } = await adminApi.downloadWorkPackageBoq(packageId, boq.id);
      saveBlob(data, boq.sourceFileName ?? `BOQ-R${boq.revisionNumber}.xlsx`);
    } catch (reason) {
      toast({ variant: "destructive", title: extractApiError(reason) });
    }
  };

  return (
    <div data-testid="work-package-boq">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b bg-muted/20 px-4 py-3">
        <div className="min-w-0 flex-[1_1_18rem]">
          <h3 className="text-sm font-semibold">{t("workPackages.inputPricing.boq")}</h3>
          <p className="text-sm text-muted-foreground">
            {t("workPackageBoq.hint")}{" "}
            <Link
              className="font-medium text-primary underline underline-offset-2"
              to="/admin/material-rates/boq"
            >
              {t("workPackageBoq.library")}
            </Link>
          </p>
        </div>
        {canProgress && open && (
          <Button
            size="sm"
            onClick={() => setUploadOpen(true)}
            data-testid="work-package-boq-upload"
          >
            <FileUp className="mr-2 h-4 w-4" />
            {t("boqWorkbook.upload")}
          </Button>
        )}
      </div>
      {boqs.length === 0 ? (
        <p className="p-6 text-center text-sm text-muted-foreground">{t("workPackageBoq.empty")}</p>
      ) : (
        <ul className="divide-y">
          {boqs.map((boq) => (
            <li key={boq.id} className="space-y-2 p-4" data-testid={`work-package-boq-${boq.id}`}>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0 space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <FileSpreadsheet className="h-4 w-4 shrink-0 text-emerald-600" />
                    <strong>R{boq.revisionNumber}</strong>
                    <Badge variant="outline">{t(`procurement.status.${boq.status}`)}</Badge>
                  </div>
                  <p className="break-all text-sm text-muted-foreground">
                    {boq.sourceFileName} · {boq.preparedByName}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {t("quotes.projectBoq.summary", {
                      lines: boq.lineCount,
                      total: `${formatVnd(boq.total)} ₫`,
                    })}
                  </p>
                  {boq.decisionReason && (
                    <p className="text-sm text-destructive">{boq.decisionReason}</p>
                  )}
                </div>
                <Button size="sm" variant="outline" onClick={() => void download(boq)}>
                  <Download className="mr-1.5 h-4 w-4" />
                  {t("boqWorkbook.downloadWorkbook")}
                </Button>
              </div>
              {boq.missingPriceCount > 0 && (
                <p className="flex gap-2 rounded-md bg-amber-50 p-2 text-xs text-amber-900">
                  <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                  {t("workPackageBoq.missingPrices", { count: boq.missingPriceCount })}
                </p>
              )}
              <div className="flex flex-wrap justify-end gap-2">
                {canProgress && open && ["Draft", "Rejected"].includes(boq.status) && (
                  <Button
                    size="sm"
                    disabled={boq.missingPriceCount > 0}
                    onClick={() => onSubmit(boq)}
                    data-testid="work-package-boq-submit"
                  >
                    {t("workPackages.action.submit")}
                  </Button>
                )}
                {canVerify &&
                  open &&
                  boq.status === "Submitted" &&
                  boq.preparedByUserId !== currentUserId && (
                    <>
                      <Button size="sm" variant="outline" onClick={() => onDecide(boq, false)}>
                        {t("common.reject")}
                      </Button>
                      <Button
                        size="sm"
                        onClick={() => onDecide(boq, true)}
                        data-testid="work-package-boq-approve"
                      >
                        {t("common.approve")}
                      </Button>
                    </>
                  )}
              </div>
            </li>
          ))}
        </ul>
      )}
      <BoqWorkbookUploadDialog
        target={{ kind: "workPackage", workPackageId: packageId }}
        open={uploadOpen}
        onOpenChange={setUploadOpen}
        onCreated={() => {
          setUploadOpen(false);
          toast({ title: t("boqWorkbook.created") });
          onChanged();
        }}
      />
    </div>
  );
}
