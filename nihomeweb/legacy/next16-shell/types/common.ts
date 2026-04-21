import type { ReactNode } from "react";

export type NavItem = {
  label: string;
  href: string;
  description?: string;
};

export type NavSection = {
  label: string;
  items: NavItem[];
};

export type StatusTone = "neutral" | "info" | "success" | "warning";

export type PageHeaderProps = {
  eyebrow?: string;
  title: string;
  description?: string;
  actions?: ReactNode;
};
