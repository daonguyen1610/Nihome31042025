import { useCallback, useEffect, useState } from "react";
import { adminApi, type ProjectScheduleItemResponse } from "@/services/adminApi";

export function usePublishedProjectWbs(projectId: number) {
  const [items, setItems] = useState<ProjectScheduleItemResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [failed, setFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setFailed(false);
    setItems([]);
    void adminApi
      .getProjectSchedule(projectId)
      .then(({ data }) => {
        if (!active) return;
        const published = data.revisions.find(
          (revision) => revision.id === data.publishedRevisionId,
        );
        setItems(published?.items ?? []);
      })
      .catch(() => active && setFailed(true))
      .finally(() => active && setLoading(false));

    return () => {
      active = false;
    };
  }, [projectId, attempt]);

  const retry = useCallback(() => setAttempt((value) => value + 1), []);

  return { items, loading, failed, retry };
}
