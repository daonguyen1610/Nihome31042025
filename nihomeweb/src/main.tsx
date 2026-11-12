import { createRoot } from "react-dom/client";
import App from "./App.tsx";
import { loadDisplaySettings } from "./lib/displaySettings";
import "./index.css";

// Render immediately with the Vietnam + VND defaults. The loader always
// resolves and App re-renders once the admin settings arrive.
void loadDisplaySettings();
createRoot(document.getElementById("root")!).render(<App />);
