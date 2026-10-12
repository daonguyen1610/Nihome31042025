import type { TenderResponse } from "@/services/adminApi";

export const isTenderChecklistEvidenceReady = (
  tender: Pick<TenderResponse, "checklistItems">,
) => tender.checklistItems.length > 0 && tender.checklistItems.every((item) => {
  const complete = item.status === "Done" || item.status === "Submitted";
  const hasFile = Boolean(item.filePath && item.originalFileName);
  return complete && hasFile && (item.templateCode === "capability"
    ? item.capabilityDocumentId != null
    : item.capabilityDocumentId == null);
});
