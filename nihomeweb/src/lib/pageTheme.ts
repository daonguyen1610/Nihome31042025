import { createContext, useContext } from "react";
import type { LucideIcon } from "lucide-react";

/** The current page's menu icon, so its heading matches where the person is. */
export const PageIconContext = createContext<LucideIcon | null>(null);
export const usePageIcon = () => useContext(PageIconContext);
