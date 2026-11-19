import { useEffect, useState } from "react";
import { adminApi } from "@/services/adminApi";

// One request per page load; every upload button on the page shares it.
let limitRequest: Promise<number | null> | null = null;

/**
 * The design file size limit set in Settings, in MB, or null while loading or
 * when it could not be read (the server still enforces it on upload).
 */
export function useDesignUploadLimit(): number | null {
  const [limitMb, setLimitMb] = useState<number | null>(null);
  useEffect(() => {
    limitRequest ??= adminApi
      .getDesignUploadLimit()
      .then(({ data }) => data.maxFileSizeMb)
      .catch(() => {
        limitRequest = null;
        return null;
      });
    let active = true;
    void limitRequest.then((value) => {
      if (active) setLimitMb(value);
    });
    return () => {
      active = false;
    };
  }, []);
  return limitMb;
}
