import { useEffect, useState } from "react";
import { adminApi, type MasterDataOption } from "@/services/adminApi";

// One request per language for the whole session: every screen that names a sector reads the
// same localized list, so a project page, its list and a contract all agree.
const byLanguage = new Map<string, Promise<MasterDataOption[]>>();

/**
 * NICON's business sectors in the user's language, switched-off ones included so old records
 * keep their names. Empty while loading, and when the user may not read master data.
 */
export function useBusinessSectors(lang: string): MasterDataOption[] {
  const [sectors, setSectors] = useState<MasterDataOption[]>([]);
  useEffect(() => {
    let request = byLanguage.get(lang);
    if (!request) {
      request = adminApi
        .getMasterDataOptions("business_sector", lang, true)
        .then(({ data }) => data ?? [])
        .catch(() => {
          byLanguage.delete(lang);
          return [];
        });
      byLanguage.set(lang, request);
    }
    let cancelled = false;
    void request.then((items) => {
      if (!cancelled) setSectors(items);
    });
    return () => {
      cancelled = true;
    };
  }, [lang]);
  return sectors;
}
