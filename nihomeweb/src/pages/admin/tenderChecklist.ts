import type { TenderResponse } from "@/services/adminApi";

export const isTenderChecklistEvidenceReady = (tender: Pick<TenderResponse, "checklistItems">) => {
  const requiredItems = tender.checklistItems.filter((item) => item.isRequired);
  return (
    requiredItems.length > 0 &&
    requiredItems.every((item) => {
      const complete =
        item.status === "Submitted" && item.verifiedAt != null && item.verifiedByUserId != null;
      const hasFile = Boolean(item.filePath && item.originalFileName);
      return (
        complete &&
        hasFile &&
        (item.templateCode === "capability"
          ? item.capabilityDocumentId != null
          : item.capabilityDocumentId == null)
      );
    })
  );
};
