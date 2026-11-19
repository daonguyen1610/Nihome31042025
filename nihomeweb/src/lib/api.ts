import axios, { type InternalAxiosRequestConfig } from "axios";
import { uploadTracker } from "@/lib/uploadTracker";
import type { RootState } from "@/store";
import { createLogger, newCorrelationId } from "@/lib/logger";
import { randomId } from "@/lib/randomId";

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
      (config.headers as { set: (name: string, value?: string) => void }).set(
        "Content-Type",
        undefined,
      );
    } else {
      delete (config.headers as Record<string, unknown>)["Content-Type"];
    }
  }

  const token = storeRef?.()?.auth.accessToken;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  const method = config.method?.toLowerCase();
  const isMutation =
    method === "post" || method === "put" || method === "patch" || method === "delete";
  const isMultipart = typeof FormData !== "undefined" && config.data instanceof FormData;
  if (isMutation && !isMultipart && !config.headers["Idempotency-Key"]) {
    config.headers["Idempotency-Key"] = newIdempotencyKey();
  }
  if (isMultipart) trackUpload(config);
  const rowVersion =
    !isMultipart && config.data && typeof config.data === "object"
      ? (config.data as { rowVersion?: string }).rowVersion
      : undefined;
  if (rowVersion && !config.headers["If-Match"]) {
    config.headers["If-Match"] = `"${rowVersion}"`;
  }
  return config;
});

type TrackedConfig = InternalAxiosRequestConfig & { uploadTrackerId?: number };

/**
 * Shows a file upload in the upload tray: its progress, and a cancel that
 * aborts the request. A caller's own progress callback and signal still work.
 */
function trackUpload(config: TrackedConfig) {
  const files = [...(config.data as FormData).values()].filter(
    (value): value is File => typeof File !== "undefined" && value instanceof File,
  );
  if (files.length === 0) return;
  const controller = new AbortController();
  const callerSignal = config.signal as AbortSignal | undefined;
  if (callerSignal?.aborted) controller.abort();
  else callerSignal?.addEventListener?.("abort", () => controller.abort(), { once: true });
  config.signal = controller.signal;
  const size = files.reduce((total, file) => total + file.size, 0);
  const id = uploadTracker.start({
    name: files.length === 1 ? files[0].name : `${files[0].name} +${files.length - 1}`,
    size,
    abort: () => controller.abort(),
  });
  const callerProgress = config.onUploadProgress;
  config.onUploadProgress = (event) => {
    // The request body also carries form fields and boundaries; scale to the files.
    const sent = event.total ? (event.loaded / event.total) * size : event.loaded;
    uploadTracker.progress(id, Math.min(size, Math.round(sent)));
    callerProgress?.(event);
  };
  config.uploadTrackerId = id;
}

api.interceptors.response.use(
  (response) => {
    const id = (response.config as TrackedConfig).uploadTrackerId;
    if (id) uploadTracker.finish(id);
    return response;
  },
  (error: unknown) => {
    const id = axios.isAxiosError(error)
      ? (error.config as TrackedConfig | undefined)?.uploadTrackerId
      : undefined;
    if (id) uploadTracker.fail(id, error);
    return Promise.reject(error);
  },
);

const readHeader = (headers: unknown, name: string): string | undefined => {
  if (!headers || typeof headers !== "object") return undefined;
  const getter = (headers as { get?: (headerName: string) => unknown }).get;
  const value =
    typeof getter === "function"
      ? getter.call(headers, name)
      : ((headers as Record<string, unknown>)[name] ??
        (headers as Record<string, unknown>)[name.toLowerCase()]);
  return typeof value === "string" && value.trim() ? value : undefined;
};

export const getApiCorrelationId = (error: unknown): string | undefined => {
  if (!axios.isAxiosError(error)) return undefined;
  return (
    readHeader(error.response?.headers, "X-Correlation-Id") ??
    readHeader(error.config?.headers, "X-Correlation-Id")
  );
};

api.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    // A cancel from the upload tray is the user's choice, not a failure to log.
    if (axios.isAxiosError(error) && error.code !== "ERR_CANCELED") {
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

export const newIdempotencyKey = randomId;

export default api;
