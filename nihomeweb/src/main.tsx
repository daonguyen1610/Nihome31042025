import { createRoot } from "react-dom/client";
import App from "./App.tsx";
import { loadDisplaySettings } from "./lib/displaySettings";
import "./index.css";

// Resolve the admin-configured time zone and currency before the first paint
// so no date or amount flashes in a different format. The loader always
// resolves (Vietnam defaults when the API is unavailable).
void loadDisplaySettings().finally(() => {
  createRoot(document.getElementById("root")!).render(<App />);
});
