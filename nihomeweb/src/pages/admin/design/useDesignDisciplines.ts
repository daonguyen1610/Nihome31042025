import { useCallback, useEffect, useMemo, useState } from "react";
import { useI18n } from "@/lib/i18n";
import { useUiError } from "@/hooks/useUiError";
import { useRequestGuard } from "@/hooks/useRequestGuard";
import { adminApi, type MasterDataOption } from "@/services/adminApi";

/**
 * Active design disciplines (Kiến trúc, Kết cấu, MEP…) for the drawing forms.
 * A failed load is reported instead of leaving an empty picker unexplained.
 */
export function useDesignDisciplines() {
  const { lang } = useI18n();
  const uiError = useUiError();
  const startRequest = useRequestGuard();
  const [disciplines, setDisciplines] = useState<MasterDataOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    const isCurrent = startRequest();
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.getMasterDataOptions("design_discipline", lang);
      if (isCurrent()) setDisciplines((data ?? []).filter((option) => option.isActive));
    } catch (reason) {
      if (isCurrent()) setError(uiError(reason));
    } finally {
      if (isCurrent()) setLoading(false);
    }
  }, [lang, startRequest, uiError]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const options = useMemo(
    () => disciplines.map((discipline) => ({ value: discipline.code, label: discipline.name })),
    [disciplines],
  );
  const labelByCode = useMemo(
    () => new Map(disciplines.map((discipline) => [discipline.code, discipline.name])),
    [disciplines],
  );

  return { options, labelByCode, loading, error, reload };
}
