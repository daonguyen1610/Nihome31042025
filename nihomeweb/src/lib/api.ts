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
  const token = storeRef?.()?.auth.accessToken;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export default api;
