import {
  DialogKind,
  DecisionKind,
  UserOption,
  BoqLineDraft,
  RequestLineDraft,
  ReceiptLineDraft,
  IssueLineDraft,
  procurementTabs,
  emptyBoqLine,
  emptyRequestLine,
  emptyReceiptLine,
  emptyIssueLine,
  localDateTime,
  apiDateTime,
  statusClass,
  Translate,
} from "./procurementValues";
import { type PersonIdentity } from "@/lib/people";
import { type ReactNode } from "react";
import { Check, Plus, Send, Trash2, X } from "lucide-react";
import { Button } from "@/components/ui/button";

export const PanelHeader = ({
  title,
  description,
  action,
}: {
  title: string;
  description: string;
  action?: ReactNode;
}) => (
  <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
    <div>
      <h2 className="text-lg font-semibold">{title}</h2>
      <p className="text-sm text-muted-foreground">{description}</p>
    </div>
    {action}
  </div>
);

export const Empty = ({ children }: { children: ReactNode }) => (
  <div className="border border-dashed p-8 text-center text-sm text-muted-foreground">
    {children}
  </div>
);

export const ActionButton = ({
  icon,
  children,
  ...props
}: React.ComponentProps<typeof Button> & { icon: ReactNode }) => (
  <Button size="sm" {...props}>
    {icon}
    {children}
  </Button>
);

export const TableShell = ({ children }: { children: ReactNode }) => (
  <div className="hidden overflow-x-auto border-y md:block">{children}</div>
);

export const MobileList = ({ children }: { children: ReactNode }) => (
  <div className="grid gap-3 md:hidden">{children}</div>
);

export const MobileCard = ({
  title,
  badge,
  children,
  actions,
}: {
  title: string;
  badge?: ReactNode;
  children: ReactNode;
  actions?: ReactNode;
}) => (
  <article className="rounded-md border p-4">
    <div className="flex items-start justify-between gap-3">
      <h3 className="min-w-0 break-words font-semibold">{title}</h3>
      {badge}
    </div>
    <dl className="mt-3 grid grid-cols-2 gap-x-3 gap-y-2 text-sm">{children}</dl>
    {actions && <div className="mt-4 flex flex-wrap gap-2 border-t pt-3">{actions}</div>}
  </article>
);

export const Datum = ({ label, children }: { label: string; children: ReactNode }) => (
  <div className="min-w-0">
    <dt className="text-xs text-muted-foreground">{label}</dt>
    <dd className="mt-0.5 break-words">{children}</dd>
  </div>
);

export const Table = ({
  headers,
  children,
  minWidth = "min-w-[880px]",
}: {
  headers: string[];
  children: ReactNode;
  minWidth?: string;
}) => (
  <table className={`w-full ${minWidth} text-sm`}>
    <thead>
      <tr className="border-b text-left text-xs uppercase text-muted-foreground">
        {headers.map((header) => (
          <th className="px-3 py-3 font-medium" key={header}>
            {header}
          </th>
        ))}
      </tr>
    </thead>
    <tbody>{children}</tbody>
  </table>
);

export const AddButton = ({ onClick, children }: { onClick: () => void; children: ReactNode }) => (
  <Button type="button" variant="outline" size="sm" onClick={onClick}>
    <Plus className="mr-2 h-4 w-4" />
    {children}
  </Button>
);

export const RemoveButton = ({
  onClick,
  label,
  disabled,
}: {
  onClick: () => void;
  label: string;
  disabled: boolean;
}) => (
  <Button
    type="button"
    variant="ghost"
    size="icon"
    title={label}
    aria-label={label}
    onClick={onClick}
    disabled={disabled}
  >
    <Trash2 className="h-4 w-4 text-destructive" />
  </Button>
);

export const RecordActions = ({
  statusValue,
  canManage,
  canApprove,
  busy,
  t,
  onSubmit,
  onDecision,
  allowRejectedSubmit = false,
}: {
  statusValue: string;
  canManage: boolean;
  canApprove: boolean;
  busy: boolean;
  t: Translate;
  onSubmit: () => void;
  onDecision: (approved: boolean) => void;
  allowRejectedSubmit?: boolean;
}) => (
  <div className="flex flex-wrap gap-2">
    {canManage &&
      (statusValue === "Draft" || (allowRejectedSubmit && statusValue === "Rejected")) && (
        <ActionButton
          variant="outline"
          disabled={busy}
          onClick={onSubmit}
          icon={<Send className="mr-2 h-4 w-4" />}
        >
          {t("procurement.action.submit")}
        </ActionButton>
      )}
    {canApprove && statusValue === "Submitted" && (
      <>
        <ActionButton
          disabled={busy}
          onClick={() => onDecision(true)}
          icon={<Check className="mr-2 h-4 w-4" />}
        >
          {t("procurement.action.approve")}
        </ActionButton>
        <ActionButton
          variant="destructive"
          disabled={busy}
          onClick={() => onDecision(false)}
          icon={<X className="mr-2 h-4 w-4" />}
        >
          {t("procurement.action.reject")}
        </ActionButton>
      </>
    )}
  </div>
);
