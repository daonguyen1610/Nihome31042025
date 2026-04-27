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

export default api;
