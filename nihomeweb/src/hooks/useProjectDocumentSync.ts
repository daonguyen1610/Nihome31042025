import { useEffect, useState } from "react";
import { adminApi, type ProjectDocumentResponse } from "@/services/adminApi";

/** Refresh asynchronous Drive state without resetting the surrounding form. */
export function useProjectDocumentSync(
  projectId: number | null | undefined,
  enabled: boolean,
  onUpdate: (documents: ProjectDocumentResponse[]) => void,
  designProjectId?: number,
) {
  const [error, setError] = useState<unknown>(null);

  useEffect(() => {
    setError(null);
    if (!projectId || !enabled) return;
    let disposed = false;
    let refreshing = false;
    const refresh = async () => {
      if (document.hidden || refreshing) return;
      refreshing = true;
      try {
        const response = await adminApi.listProjectDocuments(projectId, designProjectId);
        if (!disposed) {
          onUpdate(response.data);
          setError(null);
        }
      } catch (reason) {
        if (!disposed) setError(reason);
      } finally {
        refreshing = false;
      }
    };
    const interval = window.setInterval(() => void refresh(), 3_000);
    const resume = () => void refresh();
    document.addEventListener("visibilitychange", resume);
    return () => {
      disposed = true;
      window.clearInterval(interval);
      document.removeEventListener("visibilitychange", resume);
    };
  }, [enabled, onUpdate, projectId, designProjectId]);

  return error;
}
