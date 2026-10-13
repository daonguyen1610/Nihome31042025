import { defineConfig } from "vite";
import react from "@vitejs/plugin-react-swc";
import fs from "node:fs";
import path from "path";

type AppSettings = {
  Logging?: {
    Frontend?: {
      AppName?: string;
      ApiName?: string;
      MessageType?: string;
      ThreadName?: string;
    };
  };
};

const readAppSettings = (filename: string): AppSettings => {
  const candidates = [
    path.resolve(import.meta.dirname, `../nihomebackend/${filename}`),
    path.resolve("/app", filename),
  ];
  const settingsPath = candidates.find((candidate) => fs.existsSync(candidate));
  if (!settingsPath) throw new Error(`Cannot find backend ${filename} for frontend logging configuration.`);
  return JSON.parse(fs.readFileSync(settingsPath, "utf8")) as AppSettings;
};

const loadFrontendLogging = (mode: string) => {
  const base = readAppSettings("appsettings.json").Logging?.Frontend ?? {};
  const environment = mode === "development"
    ? readAppSettings("appsettings.Development.json").Logging?.Frontend ?? {}
    : {};
  return {
    AppName: "NihomeWeb",
    ApiName: "Nihome SPA",
    MessageType: "browser_log",
    ThreadName: "browser-main",
    ...base,
    ...environment,
  };
};

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => ({
  define: {
    __NIHOME_LOGGING_CONFIG__: JSON.stringify(loadFrontendLogging(mode)),
  },
  server: {
    host: "::",
    port: 8080,
    hmr: {
      overlay: false,
    },
  },
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(import.meta.dirname, "./src"),
    },
    dedupe: ["react", "react-dom", "react/jsx-runtime", "react/jsx-dev-runtime", "@tanstack/react-query", "@tanstack/query-core"],
  },
}));
