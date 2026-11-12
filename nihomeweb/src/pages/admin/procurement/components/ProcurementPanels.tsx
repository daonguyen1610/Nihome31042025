import { type ReactNode } from "react";
import { Eye, PackageCheck, PackageMinus, Plus, Star } from "lucide-react";
import { Link } from "react-router-dom";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  type MaterialRequestResponse,
  type ProcurementContractLineResponse,
  type ProjectBoqRevisionListItemResponse,
  type VendorRatingResponse,
  type WarehouseIssueResponse,
  type WarehouseReceiptResponse,
} from "@/services/adminApi";
import {
  PanelHeader,
  Empty,
  TableShell,
  Table,
  RecordActions,
  MobileList,
  MobileCard,
  Datum,
  ActionButton,
} from "./procurementShared";
import { Translate } from "./procurementValues";
import { formatDate, formatMoney } from "@/lib/displaySettings";

export const BoqPanel = ({
  rows,
  canManage,
  canApprove,
  status,
  t,
  onCreate,
  onSubmit,
  onDecision,
  busy,
}: {
  rows: ProjectBoqRevisionListItemResponse[];
  canManage: boolean;
  canApprove: boolean;
  status: (value: string) => ReactNode;
  t: Translate;
  onCreate: () => void;
  onSubmit: (row: ProjectBoqRevisionListItemResponse) => void;
  onDecision: (row: ProjectBoqRevisionListItemResponse, approved: boolean) => void;
  busy: boolean;
}) => (
  <section>
    <PanelHeader
      title={t("procurement.boq.title")}
      description={t("procurement.boq.description")}
      action={
        canManage && (
          <Button onClick={onCreate}>
            <Plus className="mr-2 h-4 w-4" />
            {t("procurement.boq.create")}
          </Button>
        )
      }
    />
    {rows.length === 0 ? (
      <Empty>{t("procurement.boq.empty")}</Empty>
    ) : (
      <>
        <TableShell>
          <Table
            headers={[
              t("procurement.field.revision"),
              t("procurement.field.status"),
              t("procurement.field.lines"),
              t("procurement.field.total"),
              t("procurement.field.preparedBy"),
              t("procurement.field.actions"),
            ]}
          >
            <>
              {rows.map((row) => (
                <tr className="border-b" key={row.id}>
                  <td className="px-3 py-3 font-medium">
                    R{row.revisionNumber}
                    {row.isFinal && (
                      <Badge className="ml-2" variant="secondary">
                        {t("procurement.boq.final")}
                      </Badge>
                    )}
                  </td>
                  <td className="px-3 py-3">{status(row.status)}</td>
                  <td className="px-3 py-3">{row.lineCount}</td>
                  <td className="px-3 py-3">{formatMoney(row.costTotal, row.currency)}</td>
                  <td className="px-3 py-3">{row.preparedByName ?? `#${row.preparedByUserId}`}</td>
                  <td className="px-3 py-3">
                    <div className="flex flex-wrap gap-2">
                      <Button asChild variant="outline" size="sm">
                        <Link
                          to={`/admin/procurement-control/projects/${row.operationalProjectId}/boq/${row.id}`}
                        >
                          <Eye className="mr-2 h-4 w-4" />
                          {t("procurement.action.view")}
                        </Link>
                      </Button>
                      <RecordActions
                        statusValue={row.status}
                        canManage={canManage}
                        canApprove={canApprove}
                        busy={busy}
                        t={t}
                        onSubmit={() => onSubmit(row)}
                        onDecision={(approved) => onDecision(row, approved)}
                        allowRejectedSubmit
                      />
                    </div>
                  </td>
                </tr>
              ))}
            </>
          </Table>
        </TableShell>
        <MobileList>
          {rows.map((row) => (
            <MobileCard
              key={row.id}
              title={`R${row.revisionNumber}`}
              badge={status(row.status)}
              actions={
                <>
                  <Button asChild variant="outline" size="sm">
                    <Link
                      to={`/admin/procurement-control/projects/${row.operationalProjectId}/boq/${row.id}`}
                    >
                      <Eye className="mr-2 h-4 w-4" />
                      {t("procurement.action.view")}
                    </Link>
                  </Button>
                  <RecordActions
                    statusValue={row.status}
                    canManage={canManage}
                    canApprove={canApprove}
                    busy={busy}
                    t={t}
                    onSubmit={() => onSubmit(row)}
                    onDecision={(approved) => onDecision(row, approved)}
                    allowRejectedSubmit
                  />
                </>
              }
            >
              <Datum label={t("procurement.field.lines")}>{row.lineCount}</Datum>
              <Datum label={t("procurement.field.total")}>
                {formatMoney(row.costTotal, row.currency)}
              </Datum>
              <Datum label={t("procurement.field.preparedBy")}>
                {row.preparedByName ?? `#${row.preparedByUserId}`}
              </Datum>
              <Datum label={t("procurement.field.revision")}>
                {row.isFinal ? t("procurement.boq.final") : `R${row.revisionNumber}`}
              </Datum>
            </MobileCard>
          ))}
        </MobileList>
      </>
    )}
  </section>
);

export const RequestPanel = ({
  rows,
  canManage,
  canApprove,
  status,
  number,
  formatDate,
  t,
  onCreate,
  onSubmit,
  onDecision,
  busy,
  hasApprovedBoq,
  currentUserId,
}: {
  rows: MaterialRequestResponse[];
  canManage: boolean;
  canApprove: boolean;
  status: (value: string) => ReactNode;
  number: Intl.NumberFormat;
  formatDate: (value?: string | null) => string;
  t: Translate;
  onCreate: () => void;
  onSubmit: (row: MaterialRequestResponse) => void;
  onDecision: (row: MaterialRequestResponse, approved: boolean) => void;
  busy: boolean;
  hasApprovedBoq: boolean;
  currentUserId?: number;
}) => (
  <section>
    <PanelHeader
      title={t("procurement.request.title")}
      description={t("procurement.request.description")}
      action={
        canManage &&
        hasApprovedBoq && (
          <Button onClick={onCreate}>
            <Plus className="mr-2 h-4 w-4" />
            {t("procurement.request.create")}
          </Button>
        )
      }
    />
    {!hasApprovedBoq && (
      <p className="mb-4 border-l-2 border-amber-400 px-3 text-sm text-amber-800">
        {t("procurement.request.requiresBoq")}
      </p>
    )}
    {rows.length === 0 ? (
      <Empty>{t("procurement.request.empty")}</Empty>
    ) : (
      <>
        <TableShell>
          <Table
            headers={[
              t("procurement.field.code"),
              t("procurement.field.status"),
              t("procurement.field.requiredAt"),
              t("procurement.field.requester"),
              t("procurement.field.responsibleSite"),
              t("procurement.field.procurementOwner"),
              t("procurement.field.lines"),
              t("procurement.field.actions"),
            ]}
            minWidth="min-w-[1240px]"
          >
            <>
              {rows.map((row) => (
                <tr className="border-b" key={row.id}>
                  <td className="px-3 py-3 font-medium">{row.code}</td>
                  <td className="px-3 py-3">{status(row.status)}</td>
                  <td className="px-3 py-3">{formatDate(row.requiredAt)}</td>
                  <td className="px-3 py-3">
                    {row.siteRequesterName ?? `#${row.siteRequesterUserId}`}
                  </td>
                  <td className="px-3 py-3">
                    {row.responsibleSiteUserName ?? `#${row.responsibleSiteUserId}`}
                  </td>
                  <td className="px-3 py-3">
                    {row.assignedProcurementUserName ?? `#${row.assignedProcurementUserId}`}
                  </td>
                  <td className="px-3 py-3">
                    <div className="space-y-1">
                      {row.lines.map((line) => (
                        <p key={line.id} className="whitespace-nowrap">
                          <span className="font-medium">{line.itemCode}</span>:{" "}
                          {number.format(line.requestedQuantity)} {line.unit}
                          <span className="ml-2 text-xs text-muted-foreground">
                            {t("procurement.field.receivedQuantity")}:{" "}
                            {number.format(line.receivedQuantity)} ·{" "}
                            {t("procurement.field.boqRemainingQuantity")}:{" "}
                            {number.format(line.boqRemainingQuantity)}
                          </span>
                        </p>
                      ))}
                    </div>
                  </td>
                  <td className="px-3 py-3">
                    <div className="flex flex-wrap gap-2">
                      <Button asChild variant="outline" size="sm">
                        <Link
                          to={`/admin/procurement-control/projects/${row.operationalProjectId}/material-requests/${row.id}`}
                        >
                          <Eye className="mr-2 h-4 w-4" />
                          {t("procurement.action.view")}
                        </Link>
                      </Button>
                      <RecordActions
                        statusValue={row.status}
                        canManage={canManage && row.siteRequesterUserId === currentUserId}
                        canApprove={canApprove}
                        busy={busy}
                        t={t}
                        onSubmit={() => onSubmit(row)}
                        onDecision={(approved) => onDecision(row, approved)}
                      />
                    </div>
                  </td>
                </tr>
              ))}
            </>
          </Table>
        </TableShell>
        <MobileList>
          {rows.map((row) => (
            <MobileCard
              key={row.id}
              title={row.code}
              badge={status(row.status)}
              actions={
                <>
                  <Button asChild variant="outline" size="sm">
                    <Link
                      to={`/admin/procurement-control/projects/${row.operationalProjectId}/material-requests/${row.id}`}
                    >
                      <Eye className="mr-2 h-4 w-4" />
                      {t("procurement.action.view")}
                    </Link>
                  </Button>
                  <RecordActions
                    statusValue={row.status}
                    canManage={canManage && row.siteRequesterUserId === currentUserId}
                    canApprove={canApprove}
                    busy={busy}
                    t={t}
                    onSubmit={() => onSubmit(row)}
                    onDecision={(approved) => onDecision(row, approved)}
                  />
                </>
              }
            >
              <Datum label={t("procurement.field.requiredAt")}>{formatDate(row.requiredAt)}</Datum>
              <Datum label={t("procurement.field.requester")}>
                {row.siteRequesterName ?? `#${row.siteRequesterUserId}`}
              </Datum>
              <Datum label={t("procurement.field.responsibleSite")}>
                {row.responsibleSiteUserName ?? `#${row.responsibleSiteUserId}`}
              </Datum>
              <Datum label={t("procurement.field.procurementOwner")}>
                {row.assignedProcurementUserName ?? `#${row.assignedProcurementUserId}`}
              </Datum>
              <Datum label={t("procurement.field.lines")}>
                <span className="space-y-1">
                  {row.lines.map((line) => (
                    <span key={line.id} className="block">
                      {line.itemCode}: {number.format(line.requestedQuantity)} {line.unit} ·{" "}
                      {t("procurement.field.receivedQuantity")}:{" "}
                      {number.format(line.receivedQuantity)} ·{" "}
                      {t("procurement.field.boqRemainingQuantity")}:{" "}
                      {number.format(line.boqRemainingQuantity)}
                    </span>
                  ))}
                </span>
              </Datum>
            </MobileCard>
          ))}
        </MobileList>
      </>
    )}
  </section>
);

export const ContractLinesPanel = ({
  rows,
  canManage,
  money,
  number,
  t,
  onCreate,
  hasDependencies,
}: {
  rows: ProcurementContractLineResponse[];
  canManage: boolean;
  money: Intl.NumberFormat;
  number: Intl.NumberFormat;
  t: Translate;
  onCreate: () => void;
  hasDependencies: boolean;
}) => (
  <section>
    <PanelHeader
      title={t("procurement.contractLine.title")}
      description={t("procurement.contractLine.description")}
      action={
        canManage && (
          <Button onClick={onCreate} disabled={!hasDependencies}>
            <Plus className="mr-2 h-4 w-4" />
            {t("procurement.contractLine.create")}
          </Button>
        )
      }
    />
    {!hasDependencies && (
      <p className="mb-4 border-l-2 border-amber-400 px-3 text-sm text-amber-800">
        {t("procurement.contractLine.requiresData")}
      </p>
    )}
    {rows.length === 0 ? (
      <Empty>{t("procurement.contractLine.empty")}</Empty>
    ) : (
      <>
        <TableShell>
          <Table
            headers={[
              t("procurement.field.contract"),
              t("procurement.field.item"),
              t("procurement.field.owner"),
              t("procurement.field.quantity"),
              t("procurement.field.budgetPrice"),
              t("procurement.field.negotiatedPrice"),
            ]}
          >
            <>
              {rows.map((row) => (
                <tr className="border-b" key={row.id}>
                  <td className="px-3 py-3 font-medium">{row.contractNumber}</td>
                  <td className="px-3 py-3">{row.itemCode}</td>
                  <td className="px-3 py-3">
                    {row.procurementOwnerName ?? `#${row.procurementOwnerUserId}`}
                  </td>
                  <td className="px-3 py-3">{number.format(row.quantity)}</td>
                  <td className="px-3 py-3">{money.format(row.budgetUnitPrice)}</td>
                  <td className="px-3 py-3">{money.format(row.negotiatedUnitPrice)}</td>
                </tr>
              ))}
            </>
          </Table>
        </TableShell>
        <MobileList>
          {rows.map((row) => (
            <MobileCard key={row.id} title={`${row.contractNumber} · ${row.itemCode}`}>
              <Datum label={t("procurement.field.owner")}>
                {row.procurementOwnerName ?? `#${row.procurementOwnerUserId}`}
              </Datum>
              <Datum label={t("procurement.field.quantity")}>{number.format(row.quantity)}</Datum>
              <Datum label={t("procurement.field.budgetPrice")}>
                {money.format(row.budgetUnitPrice)}
              </Datum>
              <Datum label={t("procurement.field.negotiatedPrice")}>
                {money.format(row.negotiatedUnitPrice)}
              </Datum>
            </MobileCard>
          ))}
        </MobileList>
      </>
    )}
  </section>
);

export const WarehousePanel = ({
  receipts,
  issues,
  canPost,
  status,
  number,
  formatDate,
  t,
  onCreateReceipt,
  onCreateIssue,
  onPostReceipt,
  onPostIssue,
  busy,
  canCreateReceipt,
  canCreateIssue,
}: {
  receipts: WarehouseReceiptResponse[];
  issues: WarehouseIssueResponse[];
  canPost: boolean;
  status: (value: string) => ReactNode;
  number: Intl.NumberFormat;
  formatDate: (value?: string | null) => string;
  t: Translate;
  onCreateReceipt: () => void;
  onCreateIssue: () => void;
  onPostReceipt: (row: WarehouseReceiptResponse) => void;
  onPostIssue: (row: WarehouseIssueResponse) => void;
  busy: boolean;
  canCreateReceipt: boolean;
  canCreateIssue: boolean;
}) => (
  <section className="space-y-7">
    <div>
      <PanelHeader
        title={t("procurement.receipt.title")}
        description={t("procurement.receipt.description")}
        action={
          canPost && (
            <Button onClick={onCreateReceipt} disabled={!canCreateReceipt}>
              <PackageCheck className="mr-2 h-4 w-4" />
              {t("procurement.receipt.create")}
            </Button>
          )
        }
      />
      {receipts.length === 0 ? (
        <Empty>{t("procurement.receipt.empty")}</Empty>
      ) : (
        <>
          <TableShell>
            <Table
              headers={[
                t("procurement.field.code"),
                t("procurement.field.status"),
                t("procurement.field.inspectedAt"),
                t("procurement.field.lines"),
                t("procurement.field.actions"),
              ]}
            >
              <>
                {receipts.map((row) => (
                  <tr className="border-b" key={row.id}>
                    <td className="px-3 py-3 font-medium">{row.code}</td>
                    <td className="px-3 py-3">{status(row.status)}</td>
                    <td className="px-3 py-3">{formatDate(row.inspectedAt)}</td>
                    <td className="px-3 py-3">
                      {row.lines
                        .map((line) => `${line.itemCode}: ${number.format(line.receivedQuantity)}`)
                        .join(", ")}
                    </td>
                    <td className="px-3 py-3">
                      {canPost && row.status === "Draft" && (
                        <ActionButton
                          onClick={() => onPostReceipt(row)}
                          disabled={busy}
                          icon={<PackageCheck className="mr-2 h-4 w-4" />}
                        >
                          {t("procurement.action.post")}
                        </ActionButton>
                      )}
                    </td>
                  </tr>
                ))}
              </>
            </Table>
          </TableShell>
          <MobileList>
            {receipts.map((row) => (
              <MobileCard
                key={row.id}
                title={row.code}
                badge={status(row.status)}
                actions={
                  canPost && row.status === "Draft" ? (
                    <ActionButton
                      onClick={() => onPostReceipt(row)}
                      disabled={busy}
                      icon={<PackageCheck className="mr-2 h-4 w-4" />}
                    >
                      {t("procurement.action.post")}
                    </ActionButton>
                  ) : undefined
                }
              >
                <Datum label={t("procurement.field.inspectedAt")}>
                  {formatDate(row.inspectedAt)}
                </Datum>
                <Datum label={t("procurement.field.lines")}>{row.lines.length}</Datum>
              </MobileCard>
            ))}
          </MobileList>
        </>
      )}
    </div>
    <div className="border-t pt-6">
      <PanelHeader
        title={t("procurement.issue.title")}
        description={t("procurement.issue.description")}
        action={
          canPost && (
            <Button onClick={onCreateIssue} disabled={!canCreateIssue}>
              <PackageMinus className="mr-2 h-4 w-4" />
              {t("procurement.issue.create")}
            </Button>
          )
        }
      />
      {issues.length === 0 ? (
        <Empty>{t("procurement.issue.empty")}</Empty>
      ) : (
        <>
          <TableShell>
            <Table
              headers={[
                t("procurement.field.code"),
                t("procurement.field.status"),
                t("procurement.field.issuedAt"),
                t("procurement.field.responsibleSite"),
                t("procurement.field.workItem"),
                t("procurement.field.lines"),
                t("procurement.field.actions"),
              ]}
              minWidth="min-w-[980px]"
            >
              <>
                {issues.map((row) => (
                  <tr className="border-b" key={row.id}>
                    <td className="px-3 py-3 font-medium">{row.code}</td>
                    <td className="px-3 py-3">{status(row.status)}</td>
                    <td className="px-3 py-3">{formatDate(row.issuedAt)}</td>
                    <td className="px-3 py-3">
                      {row.responsibleSiteUserName ?? `#${row.responsibleSiteUserId}`}
                    </td>
                    <td className="px-3 py-3">{row.workItemCode ?? "—"}</td>
                    <td className="px-3 py-3">
                      {row.lines
                        .map((line) => `${line.itemCode}: ${number.format(line.issuedQuantity)}`)
                        .join(", ")}
                    </td>
                    <td className="px-3 py-3">
                      {canPost && row.status === "Draft" && (
                        <ActionButton
                          onClick={() => onPostIssue(row)}
                          disabled={busy}
                          icon={<PackageMinus className="mr-2 h-4 w-4" />}
                        >
                          {t("procurement.action.post")}
                        </ActionButton>
                      )}
                    </td>
                  </tr>
                ))}
              </>
            </Table>
          </TableShell>
          <MobileList>
            {issues.map((row) => (
              <MobileCard
                key={row.id}
                title={row.code}
                badge={status(row.status)}
                actions={
                  canPost && row.status === "Draft" ? (
                    <ActionButton
                      onClick={() => onPostIssue(row)}
                      disabled={busy}
                      icon={<PackageMinus className="mr-2 h-4 w-4" />}
                    >
                      {t("procurement.action.post")}
                    </ActionButton>
                  ) : undefined
                }
              >
                <Datum label={t("procurement.field.issuedAt")}>{formatDate(row.issuedAt)}</Datum>
                <Datum label={t("procurement.field.responsibleSite")}>
                  {row.responsibleSiteUserName ?? `#${row.responsibleSiteUserId}`}
                </Datum>
                <Datum label={t("procurement.field.workItem")}>{row.workItemCode ?? "—"}</Datum>
                <Datum label={t("procurement.field.lines")}>{row.lines.length}</Datum>
              </MobileCard>
            ))}
          </MobileList>
        </>
      )}
    </div>
  </section>
);

export const RatingsPanel = ({
  rows,
  canManage,
  canApprove,
  status,
  number,
  t,
  onCreate,
  onSubmit,
  onDecision,
  busy,
  hasContracts,
}: {
  rows: VendorRatingResponse[];
  canManage: boolean;
  canApprove: boolean;
  status: (value: string) => ReactNode;
  number: Intl.NumberFormat;
  t: Translate;
  onCreate: () => void;
  onSubmit: (row: VendorRatingResponse) => void;
  onDecision: (row: VendorRatingResponse, approved: boolean) => void;
  busy: boolean;
  hasContracts: boolean;
}) => (
  <section>
    <PanelHeader
      title={t("procurement.rating.title")}
      description={t("procurement.rating.description")}
      action={
        canManage && (
          <Button onClick={onCreate} disabled={!hasContracts}>
            <Star className="mr-2 h-4 w-4" />
            {t("procurement.rating.create")}
          </Button>
        )
      }
    />
    {!hasContracts && (
      <p className="mb-4 border-l-2 border-amber-400 px-3 text-sm text-amber-800">
        {t("procurement.rating.requiresContract")}
      </p>
    )}
    {rows.length === 0 ? (
      <Empty>{t("procurement.rating.empty")}</Empty>
    ) : (
      <>
        <TableShell>
          <Table
            headers={[
              t("procurement.field.vendor"),
              t("procurement.field.contract"),
              t("procurement.field.version"),
              t("procurement.field.status"),
              t("procurement.field.overallScore"),
              t("procurement.field.owner"),
              t("procurement.field.actions"),
            ]}
            minWidth="min-w-[980px]"
          >
            <>
              {rows.map((row) => (
                <tr className="border-b" key={row.id}>
                  <td className="px-3 py-3 font-medium">{row.vendorName}</td>
                  <td className="px-3 py-3">{row.contractNumber}</td>
                  <td className="px-3 py-3">v{row.versionNumber}</td>
                  <td className="px-3 py-3">{status(row.status)}</td>
                  <td className="px-3 py-3 font-semibold">{number.format(row.overallScore)}</td>
                  <td className="px-3 py-3">
                    {row.procurementOwnerName ?? `#${row.procurementOwnerUserId}`}
                  </td>
                  <td className="px-3 py-3">
                    <RecordActions
                      statusValue={row.status}
                      canManage={canManage}
                      canApprove={canApprove}
                      busy={busy}
                      t={t}
                      onSubmit={() => onSubmit(row)}
                      onDecision={(approved) => onDecision(row, approved)}
                    />
                  </td>
                </tr>
              ))}
            </>
          </Table>
        </TableShell>
        <MobileList>
          {rows.map((row) => (
            <MobileCard
              key={row.id}
              title={row.vendorName}
              badge={status(row.status)}
              actions={
                <RecordActions
                  statusValue={row.status}
                  canManage={canManage}
                  canApprove={canApprove}
                  busy={busy}
                  t={t}
                  onSubmit={() => onSubmit(row)}
                  onDecision={(approved) => onDecision(row, approved)}
                />
              }
            >
              <Datum label={t("procurement.field.contract")}>{row.contractNumber}</Datum>
              <Datum label={t("procurement.field.version")}>v{row.versionNumber}</Datum>
              <Datum label={t("procurement.field.overallScore")}>
                {number.format(row.overallScore)}
              </Datum>
              <Datum label={t("procurement.field.owner")}>
                {row.procurementOwnerName ?? `#${row.procurementOwnerUserId}`}
              </Datum>
            </MobileCard>
          ))}
        </MobileList>
      </>
    )}
  </section>
);
