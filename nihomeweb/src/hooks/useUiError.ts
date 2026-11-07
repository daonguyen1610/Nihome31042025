import { useCallback, useLayoutEffect, useRef } from "react";
import { getUiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";

/** Read the current language on failure without restarting data-loading effects. */
export function useUiError() {
  const { t } = useI18n();
  const translate = useRef(t);
  useLayoutEffect(() => {
    translate.current = t;
  }, [t]);
  return useCallback(
    (error: unknown, fallback?: string) => getUiError(error, translate.current, fallback),
    [],
  );
}
