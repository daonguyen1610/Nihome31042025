import { createRoot } from "react-dom/client";
import App from "./App.tsx";
import { loadDisplaySettings, subscribeDisplaySettings } from "./lib/displaySettings";
import "./index.css";
import { reloadOnceForNewVersion } from "./lib/newVersionReload";

// A preloaded bundle file removed by a deploy: reload once for the new version.
window.addEventListener("vite:preloadError", (event) => {
  if (reloadOnceForNewVersion()) event.preventDefault();
});

const root = createRoot(document.getElementById("root")!);
const render = () => root.render(<App />);

// Paint immediately with the Vietnam + VND defaults, then re-render the tree
// once the admin settings arrive or change.
render();
subscribeDisplaySettings(render);
void loadDisplaySettings();
