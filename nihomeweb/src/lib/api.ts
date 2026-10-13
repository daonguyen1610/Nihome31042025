import axios from "axios";
import type { RootState } from "@/store";
import { createLogger, newCorrelationId } from "@/lib/logger";

const logger = createLogger("nihomeweb/src/lib/api.ts");

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || "/api",
  headers: { "Content-Type": "application/json" },
});

let storeRef: (() => RootState) | null = null;

export const setStoreRef = (getState: () => RootState) => {
  storeRef = getState;
};

api.interceptors.request.use((config) => {
  if (!config.headers["X-Correlation-Id"]) {
    config.headers["X-Correlation-Id"] = newCorrelationId();
  }

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

  const method = config.method?.toLowerCase();
  const isMutation = method === "post" || method === "put" || method === "patch" || method === "delete";
  const isMultipart = typeof FormData !== "undefined" && config.data instanceof FormData;
  if (isMutation && !isMultipart && !config.headers["Idempotency-Key"]) {
    config.headers["Idempotency-Key"] = newIdempotencyKey();
  }
  const rowVersion = !isMultipart && config.data && typeof config.data === "object"
    ? (config.data as { rowVersion?: string }).rowVersion
    : undefined;
  if (rowVersion && !config.headers["If-Match"]) {
    config.headers["If-Match"] = `"${rowVersion}"`;
  }
  return config;
});

const readHeader = (headers: unknown, name: string): string | undefined => {
  if (!headers || typeof headers !== "object") return undefined;
  const getter = (headers as { get?: (headerName: string) => unknown }).get;
  const value = typeof getter === "function"
    ? getter.call(headers, name)
    : (headers as Record<string, unknown>)[name]
      ?? (headers as Record<string, unknown>)[name.toLowerCase()];
  return typeof value === "string" && value.trim() ? value : undefined;
};

export const getApiCorrelationId = (error: unknown): string | undefined => {
  if (!axios.isAxiosError(error)) return undefined;
  return readHeader(error.response?.headers, "X-Correlation-Id")
    ?? readHeader(error.config?.headers, "X-Correlation-Id");
};

api.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    if (axios.isAxiosError(error)) {
      const method = error.config?.method?.toUpperCase() ?? "UNKNOWN";
      const path = error.config?.url?.split("?", 1)[0] ?? "unknown";
      logger.error(
        `API ${method} ${path} failed with status ${error.response?.status ?? "NETWORK"}`,
        {
          apiName: `${method} ${path}`,
          correlationId: getApiCorrelationId(error),
          messageType: "api_error",
          error,
        },
      );
    }
    return Promise.reject(error);
  },
);

/**
 * Build axios request config carrying an Idempotency-Key header so the backend
 * can short-circuit retries of the same logical mutation.
 */
export const withIdempotencyKey = (key?: string | null) =>
  key ? { headers: { "Idempotency-Key": key } } : {};

export const withIfMatch = (rowVersion?: string | null) =>
  rowVersion ? { headers: { "If-Match": `"${rowVersion}"` } } : {};

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
