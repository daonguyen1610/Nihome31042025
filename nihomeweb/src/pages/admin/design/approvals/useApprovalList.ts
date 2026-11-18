import { useCallback, useEffect, useState } from "react";
import type { AxiosResponse } from "axios";
import { useUiError } from "@/hooks/useUiError";
import { useRequestGuard } from "@/hooks/useRequestGuard";
import type { DesignApprovalInboxPage } from "@/services/adminApi";

/**
 * Loads one server-paged inbox list. Re-runs when `load` changes (callers
 * memoize it on their filters) or `refreshKey` changes, and ignores responses
 * from superseded requests.
 */
export function useApprovalList<T>(
  load: () => Promise<AxiosResponse<DesignApprovalInboxPage<T>>>,
  refreshKey: number,
) {
  const uiError = useUiError();
  const startRequest = useRequestGuard();
  const [data, setData] = useState<DesignApprovalInboxPage<T> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    const isCurrent = startRequest();
    setLoading(true);
    setError(null);
    try {
      const response = await load();
      if (isCurrent()) setData(response.data);
    } catch (reason) {
      if (isCurrent()) setError(uiError(reason));
    } finally {
      if (isCurrent()) setLoading(false);
    }
  }, [load, startRequest, uiError]);

  useEffect(() => {
    void reload();
  }, [reload, refreshKey]);

  return { data, loading, error, reload };
}
