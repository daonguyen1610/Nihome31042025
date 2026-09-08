import { ArrowDownToLine, ArrowUpFromLine, Download, Eye, PackageCheck, PackageMinus, Search } from "lucide-react";
import { Link } from "react-router-dom";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import type { WarehouseStockItemResponse, WarehouseTransactionListItemResponse } from "@/services/adminApi";

type UserOption = { userId: number; userName: string };
type Translate = (key: string, params?: Record<string, string | number>) => string;

interface WarehouseWorkspaceProps {
  projectId: number;
  rows: WarehouseTransactionListItemResponse[];
  stock: WarehouseStockItemResponse[];
  total: number;
  page: number;
  pageSize: number;
  loading: boolean;
  error: string | null;
  search: string;
  type: string;
  status: string;
  responsibleUserId: string;
  occurredFrom: string;
  occurredTo: string;
  sortDirection: "asc" | "desc";
  users: UserOption[];
  canPost: boolean;
  canCreateReceipt: boolean;
  canCreateIssue: boolean;
  exporting: boolean;
  t: Translate;
  formatDate: (value?: string | null) => string;
  formatNumber: (value: number) => string;
  onSearch: (value: string) => void;
  onType: (value: string) => void;
  onStatus: (value: string) => void;
  onResponsibleUser: (value: string) => void;
  onOccurredFrom: (value: string) => void;
  onOccurredTo: (value: string) => void;
  onToggleSort: () => void;
  onPage: (page: number) => void;
  onExport: () => void;
  onRetry: () => void;
  onCreateReceipt: () => void;
  onCreateIssue: () => void;
}

const statusClass: Record<string, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  Posted: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Reversed: "border-rose-200 bg-rose-50 text-rose-700",
};

const WarehouseWorkspace = ({
  projectId,
  rows,
  stock,
  total,
  page,
  pageSize,
  loading,
  error,
  search,
  type,
  status,
  responsibleUserId,
  occurredFrom,
  occurredTo,
  sortDirection,
  users,
  canPost,
  canCreateReceipt,
  canCreateIssue,
  exporting,
  t,
  formatDate,
  formatNumber,
  onSearch,
  onType,
  onStatus,
  onResponsibleUser,
  onOccurredFrom,
  onOccurredTo,
  onToggleSort,
  onPage,
  onExport,
  onRetry,
  onCreateReceipt,
  onCreateIssue,
}: WarehouseWorkspaceProps) => {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  const detailHref = (row: WarehouseTransactionListItemResponse) =>
    `/admin/procurement-control/projects/${projectId}/warehouse/${row.type.toLowerCase()}/${row.id}`;

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-3 border-b pb-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <h2 className="text-lg font-semibold">{t("procurement.warehouse.title")}</h2>
          <p className="mt-1 text-sm text-muted-foreground">{t("procurement.warehouse.description")}</p>
        </div>
        {canPost && <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap">
          <Button className="h-11 w-full sm:h-10 sm:w-auto" onClick={onCreateReceipt} disabled={!canCreateReceipt}><PackageCheck className="mr-2 h-4 w-4" />{t("procurement.receipt.create")}</Button>
          <Button className="h-11 w-full sm:h-10 sm:w-auto" variant="outline" onClick={onCreateIssue} disabled={!canCreateIssue}><PackageMinus className="mr-2 h-4 w-4" />{t("procurement.issue.create")}</Button>
        </div>}
      </div>

      <section aria-labelledby="warehouse-stock-heading" className="space-y-3">
        <div className="flex flex-wrap items-end justify-between gap-2">
          <div>
            <h3 id="warehouse-stock-heading" className="font-semibold">{t("procurement.warehouse.stockTitle")}</h3>
            <p className="text-sm text-muted-foreground">{t("procurement.warehouse.stockDescription")}</p>
          </div>
          <span className="text-sm text-muted-foreground">{t("procurement.warehouse.stockItems", { count: stock.length })}</span>
        </div>
        {stock.length === 0 ? <div className="border-y py-6"><p className="text-sm text-muted-foreground">{t("procurement.warehouse.stockEmpty")}</p><p className="mt-1 text-xs text-muted-foreground">{t("procurement.warehouse.stockEmptyHint")}</p><Button asChild variant="link" className="mt-2 h-auto p-0"><Link to={`/admin/procurement-control?projectId=${projectId}&tab=boq`}>{t("procurement.warehouse.goToBoq")}</Link></Button></div> : <>
          <div className="hidden overflow-x-auto border-y lg:block">
            <table className="w-full min-w-[760px] text-sm">
              <thead><tr className="border-b text-left text-xs uppercase text-muted-foreground">
                <th className="px-3 py-3 font-medium">{t("procurement.field.itemCode")}</th>
                <th className="px-3 py-3 font-medium">{t("procurement.field.description")}</th>
                <th className="px-3 py-3 text-right font-medium">{t("procurement.field.boqApprovedQuantity")}</th>
                <th className="px-3 py-3 text-right font-medium">{t("procurement.warehouse.received")}</th>
                <th className="px-3 py-3 text-right font-medium">{t("procurement.warehouse.issued")}</th>
                <th className="px-3 py-3 text-right font-medium">{t("procurement.warehouse.onHand")}</th>
              </tr></thead>
              <tbody>{stock.map((item) => <tr className="border-b" key={item.projectBoqLineId}>
                <td className="px-3 py-3 font-medium">{item.itemCode}<span className="ml-2 text-xs text-muted-foreground">{item.unit}</span></td>
                <td className="px-3 py-3">{item.description}</td>
                <td className="px-3 py-3 text-right">{formatNumber(item.boqApprovedQuantity)}</td>
                <td className="px-3 py-3 text-right">{formatNumber(item.receivedQuantity)}</td>
                <td className="px-3 py-3 text-right">{formatNumber(item.issuedQuantity)}</td>
                <td className="px-3 py-3 text-right font-semibold">{formatNumber(item.onHandQuantity)}</td>
              </tr>)}</tbody>
            </table>
          </div>
          <div className="grid gap-3 lg:hidden">{stock.map((item) => <article className="rounded-md border p-4" key={item.projectBoqLineId}>
            <div className="flex items-start justify-between gap-3"><div><h4 className="font-semibold">{item.itemCode}</h4><p className="mt-1 text-sm text-muted-foreground">{item.description}</p></div><Badge variant="outline">{item.unit}</Badge></div>
            <dl className="mt-4 grid grid-cols-2 gap-3 text-sm"><Datum label={t("procurement.field.boqApprovedQuantity")} value={formatNumber(item.boqApprovedQuantity)} /><Datum label={t("procurement.warehouse.received")} value={formatNumber(item.receivedQuantity)} /><Datum label={t("procurement.warehouse.issued")} value={formatNumber(item.issuedQuantity)} /><Datum label={t("procurement.warehouse.onHand")} value={formatNumber(item.onHandQuantity)} /></dl>
          </article>)}</div>
        </>}
      </section>

      <div className="grid gap-3 border-y py-4 sm:grid-cols-2 xl:grid-cols-[minmax(220px,1fr)_minmax(150px,auto)_minmax(160px,auto)_minmax(190px,auto)_150px_150px_auto_auto]">
        <div className="relative sm:col-span-2 xl:col-span-1"><Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" /><Input className="h-11 pl-9 sm:h-10" aria-label={t("procurement.warehouse.search")} placeholder={t("procurement.warehouse.searchPlaceholder")} value={search} onChange={(event) => onSearch(event.target.value)} /></div>
        <Select value={type} onValueChange={onType}><SelectTrigger className="h-11 sm:h-10" aria-label={t("procurement.warehouse.type")}><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{t("procurement.warehouse.allTypes")}</SelectItem><SelectItem value="receipt">{t("procurement.warehouse.receipt")}</SelectItem><SelectItem value="issue">{t("procurement.warehouse.issue")}</SelectItem></SelectContent></Select>
        <Select value={status} onValueChange={onStatus}><SelectTrigger className="h-11 sm:h-10" aria-label={t("procurement.field.status")}><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{t("procurement.boq.filter.allStatuses")}</SelectItem>{["Draft", "Posted", "Reversed"].map((value) => <SelectItem key={value} value={value}>{t(`procurement.status.${value}`)}</SelectItem>)}</SelectContent></Select>
        <Select value={responsibleUserId} onValueChange={onResponsibleUser}><SelectTrigger className="h-11 sm:h-10" aria-label={t("procurement.warehouse.responsibleUser")}><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{t("procurement.warehouse.allUsers")}</SelectItem>{users.map((user) => <SelectItem key={user.userId} value={String(user.userId)}>{user.userName}</SelectItem>)}</SelectContent></Select>
        <div><Label htmlFor="warehouse-from" className="sr-only">{t("procurement.warehouse.from")}</Label><Input id="warehouse-from" aria-label={t("procurement.warehouse.from")} type="date" value={occurredFrom} onChange={(event) => onOccurredFrom(event.target.value)} /></div>
        <div><Label htmlFor="warehouse-to" className="sr-only">{t("procurement.warehouse.to")}</Label><Input id="warehouse-to" aria-label={t("procurement.warehouse.to")} type="date" value={occurredTo} onChange={(event) => onOccurredTo(event.target.value)} /></div>
        <Button variant="outline" onClick={onToggleSort}>{sortDirection === "asc" ? <ArrowUpFromLine className="mr-2 h-4 w-4" /> : <ArrowDownToLine className="mr-2 h-4 w-4" />}{t("procurement.warehouse.occurredAt")}</Button>
        <Button variant="outline" onClick={onExport} disabled={exporting || total === 0}><Download className="mr-2 h-4 w-4" />{t("procurement.warehouse.export")}</Button>
      </div>

      {loading && rows.length === 0 ? <p className="py-8 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : error ? <div className="border-y py-6"><p className="text-sm text-destructive">{error}</p><Button className="mt-3" variant="outline" onClick={onRetry}>{t("common.retry")}</Button></div> : rows.length === 0 ? <p className="border-y py-6 text-sm text-muted-foreground">{t("procurement.warehouse.empty")}</p> : <>
        <div className="hidden overflow-x-auto border-y xl:block"><table className="w-full min-w-[980px] text-sm"><thead><tr className="border-b text-left text-xs uppercase text-muted-foreground">
          <th className="px-3 py-3 font-medium">{t("procurement.warehouse.type")}</th><th className="px-3 py-3 font-medium">{t("procurement.field.code")}</th><th className="px-3 py-3 font-medium">{t("procurement.field.status")}</th><th className="px-3 py-3 font-medium">{t("procurement.warehouse.occurredAt")}</th><th className="px-3 py-3 font-medium">{t("procurement.warehouse.responsibleUser")}</th><th className="px-3 py-3 font-medium">{t("procurement.field.lines")}</th><th className="px-3 py-3 text-right font-medium">{t("procurement.field.quantity")}</th><th className="px-3 py-3 font-medium">{t("procurement.field.actions")}</th>
        </tr></thead><tbody>{rows.map((row) => <tr className="border-b" key={`${row.type}-${row.id}`}>
          <td className="px-3 py-3">{t(`procurement.warehouse.${row.type.toLowerCase()}`)}</td><td className="px-3 py-3 font-medium">{row.code}</td><td className="px-3 py-3"><Badge variant="outline" className={statusClass[row.status] ?? ""}>{t(`procurement.status.${row.status}`)}</Badge></td><td className="px-3 py-3">{formatDate(row.occurredAt)}</td><td className="max-w-[180px] break-words px-3 py-3">{row.responsibleUserName ?? row.actorName ?? t("procurement.warehouse.userUnavailable")}</td><td className="px-3 py-3">{row.itemCodes.join(", ")}</td><td className="px-3 py-3 text-right">{formatNumber(row.totalQuantity)}</td><td className="px-3 py-3"><Button asChild variant="outline" size="sm"><Link to={detailHref(row)}><Eye className="mr-2 h-4 w-4" />{t("procurement.action.view")}</Link></Button></td>
        </tr>)}</tbody></table></div>
        <div className="grid gap-3 xl:hidden">{rows.map((row) => <article className="rounded-md border p-4" key={`${row.type}-${row.id}`}>
          <div className="flex items-start justify-between gap-3"><div><p className="text-xs font-medium uppercase text-muted-foreground">{t(`procurement.warehouse.${row.type.toLowerCase()}`)}</p><h3 className="mt-1 font-semibold">{row.code}</h3></div><Badge variant="outline" className={statusClass[row.status] ?? ""}>{t(`procurement.status.${row.status}`)}</Badge></div>
          <dl className="mt-4 grid grid-cols-2 gap-3"><Datum label={t("procurement.warehouse.occurredAt")} value={formatDate(row.occurredAt)} /><Datum label={t("procurement.warehouse.responsibleUser")} value={row.responsibleUserName ?? row.actorName ?? t("procurement.warehouse.userUnavailable")} /><Datum label={t("procurement.field.lines")} value={row.itemCodes.join(", ")} /><Datum label={t("procurement.field.quantity")} value={formatNumber(row.totalQuantity)} /></dl>
          <Button asChild className="mt-4" variant="outline" size="sm"><Link to={detailHref(row)}><Eye className="mr-2 h-4 w-4" />{t("procurement.action.view")}</Link></Button>
        </article>)}</div>
      </>}

      <div className="flex items-center justify-between gap-3"><p className="text-sm text-muted-foreground">{t("procurement.warehouse.total", { count: total })}</p><div className="flex items-center gap-2"><Button variant="outline" size="sm" disabled={page <= 1} onClick={() => onPage(page - 1)}>{t("common.prev")}</Button><span className="text-sm">{page} / {pages}</span><Button variant="outline" size="sm" disabled={page >= pages} onClick={() => onPage(page + 1)}>{t("common.next")}</Button></div></div>
    </section>
  );
};

const Datum = ({ label, value }: { label: string; value: string }) => <div className="min-w-0"><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-1 break-words font-medium">{value}</dd></div>;

export default WarehouseWorkspace;
