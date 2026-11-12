import { createRoot } from "react-dom/client";
import App from "./App.tsx";
import { loadDisplaySettings, subscribeDisplaySettings } from "./lib/displaySettings";
import "./index.css";

const root = createRoot(document.getElementById("root")!);
const render = () => root.render(<App />);

// Paint immediately with the Vietnam + VND defaults, then re-render the tree
// once the admin settings arrive or change.
render();
subscribeDisplaySettings(render);
void loadDisplaySettings();
