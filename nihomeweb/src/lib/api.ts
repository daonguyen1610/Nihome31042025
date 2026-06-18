import axios from "axios";
import type { RootState } from "@/store";

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || "/api",
  headers: { "Content-Type": "application/json" },
});

let storeRef: (() => RootState) | null = null;

export const setStoreRef = (getState: () => RootState) => {
  storeRef = getState;
};

api.interceptors.request.use((config) => {
  if (typeof FormData !== "undefined" && config.data instanceof FormData && config.headers) {
    // Let the browser set multipart boundaries for FormData payloads.
    if (typeof (config.headers as { set?: unknown }).set === "function") {
      (config.headers as { set: (name: string, value?: string) => void }).set("Content-Type", undefined);
    } else {
      delete (config.headers as Record<string, unknown>)["Content-Type"];
    }
  }

  const token = storeRef?.()?.auth.accessToken;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

/**
 * Build axios request config carrying an Idempotency-Key header so the backend
 * can short-circuit retries of the same logical mutation.
 */
export const withIdempotencyKey = (key?: string | null) =>
  key ? { headers: { "Idempotency-Key": key } } : {};

/**
 * UUID v4 generator that prefers the browser-native crypto API and falls back
 * to a Math.random implementation for non-secure contexts (older browsers).
 */
export const newIdempotencyKey = (): string => {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
};

export default api;
