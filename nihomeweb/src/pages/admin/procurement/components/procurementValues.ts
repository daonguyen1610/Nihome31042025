import { type PersonIdentity } from "@/lib/people";
import { type ReactNode } from "react";
import { Check, Plus, Send, Trash2, X } from "lucide-react";
import { Button } from "@/components/ui/button";
export type DialogKind = "boq" | "request" | "contract" | "receipt" | "issue" | "rating" | null;

export type DecisionKind = "boq" | "request" | "rating";

export type UserOption = PersonIdentity & { userId: number; userName: string };

export type BoqLineDraft = {
  itemCode: string;
  description: string;
  unit: string;
  approvedQuantity: number;
  budgetUnitPrice: number;
};

export type RequestLineDraft = { projectBoqLineId: number; requestedQuantity: number };

export type ReceiptLineDraft = {
  materialRequestLineId: number;
  contractLineId: number | null;
  receivedQuantity: number;
};

export type IssueLineDraft = { projectBoqLineId: number; issuedQuantity: number };

export const procurementTabs = [
  "boq",
  "alerts",
  "requests",
  "resources",
  "contracts",
  "warehouse",
  "ratings",
] as const;

export const emptyBoqLine = (): BoqLineDraft => ({
  itemCode: "",
  description: "",
  unit: "",
  approvedQuantity: 1,
  budgetUnitPrice: 0,
});

export const emptyRequestLine = (): RequestLineDraft => ({
  projectBoqLineId: 0,
  requestedQuantity: 1,
});

export const emptyReceiptLine = (): ReceiptLineDraft => ({
  materialRequestLineId: 0,
  contractLineId: null,
  receivedQuantity: 1,
});

export const emptyIssueLine = (): IssueLineDraft => ({ projectBoqLineId: 0, issuedQuantity: 1 });

export const localDateTime = (daysFromNow = 0) => {
  const now = new Date();
  now.setDate(now.getDate() + daysFromNow);
  now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
  return now.toISOString().slice(0, 16);
};

export const apiDateTime = (value: string) => new Date(value).toISOString();

export const statusClass: Record<string, string> = {
  Draft: "border-border bg-muted/30 text-foreground",
  Submitted: "border-sky-200 bg-sky-50 text-sky-700",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Rejected: "border-rose-200 bg-rose-50 text-rose-700",
  PartiallyFulfilled: "border-amber-200 bg-amber-50 text-amber-800",
  Fulfilled: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Cancelled: "border-border bg-muted/30 text-muted-foreground",
  Posted: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Reversed: "border-rose-200 bg-rose-50 text-rose-700",
  Superseded: "border-border bg-muted/30 text-muted-foreground",
};

export type Translate = (key: string, params?: Record<string, string | number>) => string;
