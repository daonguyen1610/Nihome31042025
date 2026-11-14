import { useState } from "react";
import { Link } from "react-router-dom";
import {
  AlertTriangle,
  ChevronDown,
  ChevronUp,
  Download,
  FileSpreadsheet,
  FileUp,
} from "lucide-react";
import { InvestmentRateGroupList } from "@/components/admin/InvestmentRateGroupList";
import {
  InvestmentRateTotals,
  InvestmentRateUploadDialog,
} from "@/components/admin/InvestmentRateUploadDialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { saveBlob } from "@/lib/boqWorkbook";
import { useI18n } from "@/lib/i18n";
import { adminApi, type WorkPackageInvestmentRate } from "@/services/adminApi";

// The investment-rate part of the work package's input-pricing panel: Procurement uploads the filled
// form here; the package lead approves it and it prices the project's unit-cost quotes.
export function WorkPackageInvestmentRateSection({
  packageId,
  open,
  rates,
  canProgress,
  canVerify,
  currentUserId,
  onChanged,
  onSubmit,
  onDecide,
}: {
  packageId: number;
  open: boolean;
  rates: WorkPackageInvestmentRate[];
  canProgress: boolean;
  canVerify: boolean;
  currentUserId: number | null;
  onChanged: () => void;
  onSubmit: (rate: WorkPackageInvestmentRate) => void;
  onDecide: (rate: WorkPackageInvestmentRate, approved: boolean) => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [uploadOpen, setUploadOpen] = useState(false);
  const [expanded, setExpanded] = useState<number | null>(null);

  const download = async (rate: WorkPackageInvestmentRate) => {
    try {
      const { data } = await adminApi.downloadWorkPackageInvestmentRate(packageId, rate.id);
      saveBlob(data, rate.sourceFileName);
    } catch (reason) {
      toast({ variant: "destructive", title: extractApiError(reason) });
    }
  };

  return (
    <div data-testid="work-package-investment-rate">
      <div className="flex flex-wrap items-center justify-between gap-3 border-y bg-muted/20 px-4 py-3">
        <div className="min-w-0 flex-[1_1_18rem]">
          <h3 className="text-sm font-semibold">{t("workPackages.inputPricing.investmentRate")}</h3>
          <p className="text-sm text-muted-foreground">
            {t("investmentRate.hint")}{" "}
            <Link
              className="font-medium text-primary underline underline-offset-2"
              to="/admin/material-rates/investment"
            >
              {t("investmentRate.library")}
            </Link>
          </p>
        </div>
        {canProgress && open && (
          <Button
            size="sm"
            onClick={() => setUploadOpen(true)}
            data-testid="work-package-investment-rate-upload"
          >
            <FileUp className="mr-2 h-4 w-4" />
            {t("investmentRate.upload")}
          </Button>
        )}
      </div>
      {rates.length === 0 ? (
        <p className="p-6 text-center text-sm text-muted-foreground">{t("investmentRate.empty")}</p>
      ) : (
        <ul className="divide-y">
          {rates.map((rate) => {
            const incomplete = rate.convertedAreaSqm <= 0 || rate.ratePerSqm <= 0;
            const showGroups = expanded === rate.id;
            return (
              <li
                key={rate.id}
                className="space-y-3 p-4"
                data-testid={`work-package-investment-rate-${rate.id}`}
              >
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0 space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <FileSpreadsheet className="h-4 w-4 shrink-0 text-emerald-600" />
                      <strong>R{rate.revisionNumber}</strong>
                      <Badge variant="outline">{t(`procurement.status.${rate.status}`)}</Badge>
                    </div>
                    <p className="break-all text-sm text-muted-foreground">
                      {rate.sourceFileName} · {rate.preparedByName}
                    </p>
                    {rate.decisionReason && (
                      <p className="text-sm text-destructive">{rate.decisionReason}</p>
                    )}
                  </div>
                  <Button size="sm" variant="outline" onClick={() => void download(rate)}>
                    <Download className="mr-1.5 h-4 w-4" />
                    {t("investmentRate.download")}
                  </Button>
                </div>
                <InvestmentRateTotals value={rate} />
                {incomplete && (
                  <p className="flex gap-2 rounded-md bg-amber-50 p-2 text-xs text-amber-900">
                    <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                    {t("investmentRate.missingValues")}
                  </p>
                )}
                {rate.groups.length > 0 && (
                  <Button
                    size="sm"
                    variant="ghost"
                    className="px-2"
                    aria-expanded={showGroups}
                    onClick={() => setExpanded(showGroups ? null : rate.id)}
                  >
                    {showGroups ? (
                      <ChevronUp className="mr-1.5 h-4 w-4" />
                    ) : (
                      <ChevronDown className="mr-1.5 h-4 w-4" />
                    )}
                    {showGroups
                      ? t("investmentRate.hideGroups")
                      : t("investmentRate.showGroups", { count: rate.groups.length })}
                  </Button>
                )}
                {showGroups && <InvestmentRateGroupList groups={rate.groups} />}
                <div className="flex flex-wrap justify-end gap-2">
                  {canProgress && open && ["Draft", "Rejected"].includes(rate.status) && (
                    <Button
                      size="sm"
                      disabled={incomplete}
                      onClick={() => onSubmit(rate)}
                      data-testid="work-package-investment-rate-submit"
                    >
                      {t("workPackages.action.submit")}
                    </Button>
                  )}
                  {canVerify &&
                    open &&
                    rate.status === "Submitted" &&
                    rate.preparedByUserId !== currentUserId && (
                      <>
                        <Button size="sm" variant="outline" onClick={() => onDecide(rate, false)}>
                          {t("common.reject")}
                        </Button>
                        <Button
                          size="sm"
                          onClick={() => onDecide(rate, true)}
                          data-testid="work-package-investment-rate-approve"
                        >
                          {t("common.approve")}
                        </Button>
                      </>
                    )}
                </div>
              </li>
            );
          })}
        </ul>
      )}
      <InvestmentRateUploadDialog
        workPackageId={packageId}
        open={uploadOpen}
        onOpenChange={setUploadOpen}
        onCreated={() => {
          setUploadOpen(false);
          toast({ title: t("investmentRate.created") });
          onChanged();
        }}
      />
    </div>
  );
}
