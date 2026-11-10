import type { ReactNode } from "react";
import { cn } from "@/lib/utils";
import { STATUS_TONE_CLASS, type StatusTone } from "./constructionStatus";

/** The one status pill for every construction module: same shape, tone map and text label. */
export function ConstructionStatusBadge({
  tone,
  className,
  children,
}: {
  tone: StatusTone;
  className?: string;
  children: ReactNode;
}) {
  return <span className={cn(STATUS_TONE_CLASS[tone], className)}>{children}</span>;
}
