import { useEffect, useState } from "react";
import type { SearchableSelectOption } from "@/components/ui/searchable-select";
import { extractApiError } from "@/lib/apiError";
import { adminApi, type DesignProjectResponse } from "@/services/adminApi";

export function useDesignTeamOwners(project: DesignProjectResponse, enabled: boolean) {
  const [options, setOptions] = useState<SearchableSelectOption[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loadedFor, setLoadedFor] = useState<number | null>(null);
  const [loading, setLoading] = useState(enabled);

  useEffect(() => {
    if (!enabled || !project.operationalProjectId) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    void adminApi
      .getOperationalProjectTeam(project.operationalProjectId, project.id)
      .then(({ data }) => {
        if (cancelled) return;
        setOptions(
          data.members
            .filter((member) => member.isActive && member.isUserActive !== false)
            .map((member) => ({
              value: String(member.userId),
              label: member.userName,
              hint: member.position || member.email,
              keywords: member.email,
            })),
        );
        setError(null);
        setLoadedFor(project.id);
      })
      .catch((reason: unknown) => {
        if (!cancelled) {
          setOptions([]);
          setError(extractApiError(reason));
          setLoadedFor(project.id);
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [enabled, project.id, project.operationalProjectId]);

  return {
    options: enabled && loadedFor === project.id ? options : [],
    error: enabled && loadedFor === project.id ? error : null,
    loading: enabled && (loadedFor !== project.id || loading),
  };
}
