import { createRoot } from "react-dom/client";
import App from "./App.tsx";
import { loadDisplaySettings, subscribeDisplaySettings } from "./lib/displaySettings";
import "./index.css";
import { reloadOnceForNewVersion, shouldReloadForPreloadError } from "./lib/newVersionReload";

// A bundle file removed by a deploy: reload once for the new version. Other
// failures reported this way (a broken PDF, a page error) are left to the page.
window.addEventListener("vite:preloadError", (event) => {
  const payload = (event as Event & { payload?: unknown }).payload;
  if (shouldReloadForPreloadError(payload) && reloadOnceForNewVersion()) event.preventDefault();
});

const root = createRoot(document.getElementById("root")!);
const render = () => root.render(<App />);

// Paint immediately with the Vietnam + VND defaults, then re-render the tree
// once the admin settings arrive or change.
render();
subscribeDisplaySettings(render);
void loadDisplaySettings();
