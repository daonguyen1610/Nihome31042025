import type { NavItem, NavSection } from "@/types/common";

export const CLIENT_NAV: NavItem[] = [
  {
    label: "Home",
    href: "/",
    description: "Client portal overview",
  },
  {
    label: "Projects",
    href: "/projects",
    description: "Project list placeholder",
  },
  {
    label: "Notifications",
    href: "/notifications",
    description: "Updates and alerts placeholder",
  },
];

export const ADMIN_NAV: NavSection[] = [
  {
    label: "Workspace",
    items: [
      {
        label: "Dashboard",
        href: "/admin/dashboard",
        description: "Admin shell entry point",
      },
    ],
  },
  {
    label: "Future Modules",
    items: [
      {
        label: "CRM",
        href: "/admin/dashboard",
        description: "Deferred to a later phase",
      },
      {
        label: "Construction",
        href: "/admin/dashboard",
        description: "Deferred to a later phase",
      },
    ],
  },
];

export const ROUTE_LABELS: Record<string, string> = {
  admin: "Admin",
  dashboard: "Dashboard",
  login: "Login",
  "forgot-password": "Forgot password",
  projects: "Projects",
  notifications: "Notifications",
};
