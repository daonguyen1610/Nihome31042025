import { useEffect, useState } from "react";
import { adminApi, type UploadLimitKind } from "@/services/adminApi";

// One request per module and page load; every upload control shares it.
const limitRequests = new Map<UploadLimitKind, Promise<number | null>>();

/**
 * A module's per-file upload limit from Settings, in MB, or null while loading
 * or when it could not be read (the server still enforces it on upload).
 */
export function useUploadLimit(kind: UploadLimitKind): number | null {
  const [limitMb, setLimitMb] = useState<number | null>(null);
  useEffect(() => {
    let request = limitRequests.get(kind);
    if (!request) {
      request = adminApi
        .getUploadLimit(kind)
        .then(({ data }) => data.maxFileSizeMb)
        .catch(() => {
          limitRequests.delete(kind);
          return null;
        });
      limitRequests.set(kind, request);
    }
    let active = true;
    void request.then((value) => {
      if (active) setLimitMb(value);
    });
    return () => {
      active = false;
    };
  }, [kind]);
  return limitMb;
}
