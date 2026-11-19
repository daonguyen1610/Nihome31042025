import { useCallback } from "react";
import { useI18n } from "@/lib/i18n";
import { formatNumber } from "@/lib/displaySettings";
import { BUSINESS_FILE_ACCEPT, businessFileProblem } from "@/lib/businessFileRules";
import { useUploadLimit } from "@/hooks/useUploadLimit";
import type { UploadLimitKind } from "@/services/adminApi";

/**
 * Checks a business document before it uploads: accepted types and the
 * module's size limit from Settings. `problemFor` returns the reason to show,
 * naming the file, or null when the file may be sent.
 */
export function useBusinessFileCheck(kind: UploadLimitKind) {
  const { t, lang } = useI18n();
  const limitMb = useUploadLimit(kind);
  const problemFor = useCallback(
    (file: File): string | null => {
      const problem = businessFileProblem(file, limitMb);
      if (!problem) return null;
      if (problem.code === "tooLarge")
        return t("designFiles.tooLarge", {
          name: file.name,
          size: formatNumber(problem.sizeMb, lang, { maximumFractionDigits: 1 }),
          max: limitMb,
        });
      if (problem.code === "empty") return t("designFiles.empty", { name: file.name });
      return t("designFiles.unsupported", { name: file.name });
    },
    [limitMb, lang, t],
  );
  const rule =
    limitMb == null ? t("designFiles.ruleTypes") : t("designFiles.rule", { max: limitMb });
  return { accept: BUSINESS_FILE_ACCEPT, limitMb, rule, problemFor };
}
