import { ArrowDownAZ, ArrowUpAZ, Search } from "lucide-react";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

type UserOption = { userId: number; userName: string };
type Translate = (key: string, params?: Record<string, string | number>) => string;

type MaterialRequestListToolbarProps = {
  search: string;
  statusValue: string;
  owner: string;
  requiredFrom: string;
  requiredTo: string;
  sortDirection: "asc" | "desc";
  users: UserOption[];
  total: number;
  page: number;
  pageSize: number;
  loading: boolean;
  exporting: boolean;
  t: Translate;
  onSearch: (value: string) => void;
  onStatus: (value: string) => void;
  onOwner: (value: string) => void;
  onRequiredFrom: (value: string) => void;
  onRequiredTo: (value: string) => void;
  onToggleSort: () => void;
  onPage: (value: number) => void;
  onExport: () => void;
};

const STATUSES = ["Draft", "Submitted", "Approved", "Rejected", "PartiallyFulfilled", "Fulfilled", "Cancelled"];

export default function MaterialRequestListToolbar({
  search,
  statusValue,
  owner,
  requiredFrom,
  requiredTo,
  sortDirection,
  users,
  total,
  page,
  pageSize,
  loading,
  exporting,
  t,
  onSearch,
  onStatus,
  onOwner,
  onRequiredFrom,
  onRequiredTo,
  onToggleSort,
  onPage,
  onExport,
}: MaterialRequestListToolbarProps) {
  const pages = Math.max(1, Math.ceil(total / pageSize));

  return (
    <section className="space-y-3 border-y py-4" data-testid="material-request-list-toolbar">
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-[minmax(220px,1fr)_180px_220px_160px_160px_auto_auto]">
        <div className="relative">
          <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            value={search}
            onChange={(event) => onSearch(event.target.value)}
            placeholder={t("procurement.request.searchPlaceholder")}
            className="pl-9"
          />
        </div>
        <Select value={statusValue} onValueChange={onStatus}>
          <SelectTrigger aria-label={t("procurement.field.status")}><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t("procurement.request.filter.allStatuses")}</SelectItem>
            {STATUSES.map((status) => <SelectItem key={status} value={status}>{t(`procurement.status.${status}`)}</SelectItem>)}
          </SelectContent>
        </Select>
        <Select value={owner} onValueChange={onOwner}>
          <SelectTrigger aria-label={t("procurement.field.procurementOwner")}><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t("procurement.request.filter.allOwners")}</SelectItem>
            {users.map((user) => <SelectItem key={user.userId} value={String(user.userId)}>{user.userName}</SelectItem>)}
          </SelectContent>
        </Select>
        <div>
          <Label htmlFor="request-required-from" className="sr-only">{t("procurement.request.filter.requiredFrom")}</Label>
          <Input id="request-required-from" type="date" value={requiredFrom} onChange={(event) => onRequiredFrom(event.target.value)} title={t("procurement.request.filter.requiredFrom")} />
        </div>
        <div>
          <Label htmlFor="request-required-to" className="sr-only">{t("procurement.request.filter.requiredTo")}</Label>
          <Input id="request-required-to" type="date" value={requiredTo} onChange={(event) => onRequiredTo(event.target.value)} title={t("procurement.request.filter.requiredTo")} />
        </div>
        <Button type="button" variant="outline" size="icon" onClick={onToggleSort} title={t("procurement.request.sort.requiredAt")} aria-label={t("procurement.request.sort.requiredAt")}>
          {sortDirection === "asc" ? <ArrowDownAZ className="h-4 w-4" /> : <ArrowUpAZ className="h-4 w-4" />}
        </Button>
        <AdminExportButton onClick={onExport} disabled={loading || exporting || total === 0} />
      </div>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground">{t("procurement.request.total", { count: total })}</p>
        <div className="flex items-center gap-2">
          <Button type="button" variant="outline" size="sm" disabled={loading || page <= 1} onClick={() => onPage(page - 1)}>{t("common.prev")}</Button>
          <span className="text-sm">{page} / {pages}</span>
          <Button type="button" variant="outline" size="sm" disabled={loading || page >= pages} onClick={() => onPage(page + 1)}>{t("common.next")}</Button>
        </div>
      </div>
    </section>
  );
}