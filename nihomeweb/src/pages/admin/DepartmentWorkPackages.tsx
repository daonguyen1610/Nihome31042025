import { useDepartmentWorkPackages } from "./work-packages/useDepartmentWorkPackages";
import { newPriceLine } from "./work-packages/workPackageFormatting";
import { money } from "./work-packages/workPackageFormatting";
import {
  Field,
  ChoiceGrid,
  ReviewCheck,
  PriceOfferDialog,
  Summary,
} from "./work-packages/WorkPackageControls";
import { collectLookupPages } from "@/lib/pagination";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { FieldScope } from "@/components/ui/field-scope";
import { useConfirm } from "@/components/ui/confirmation";
import { personOption } from "@/lib/people";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { PeopleChecklist } from "@/components/admin/PeopleChecklist";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import {
  AlertTriangle,
  ArrowLeft,
  BriefcaseBusiness,
  CalendarDays,
  CheckCircle2,
  ChevronDown,
  ClipboardList,
  Download,
  Loader2,
  Paperclip,
  Plus,
  Search,
  SlidersHorizontal,
  RotateCcw,
  Trash2,
  Upload,
  Users,
} from "lucide-react";
import { DeletionImpactDialog } from "@/components/admin/DeletionImpactDialog";
import { WorkPackageBoqSection } from "@/components/admin/WorkPackageBoqSection";
import type { WorkPackageApprovalPreview, DeletionImpactResponse } from "@/services/adminApi";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { MoneyInput } from "@/components/ui/money-input";
import { MultiFilePicker } from "@/components/ui/multi-file-picker";
import { Label } from "@/components/ui/label";
import { Progress } from "@/components/ui/progress";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { usePermissions } from "@/hooks/usePermissions";
import { useAppSelector } from "@/store";
import { useToast } from "@/hooks/use-toast";
import { newIdempotencyKey } from "@/lib/api";
import { extractApiError } from "@/lib/apiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { formatDate } from "@/lib/displaySettings";
import { translateError, useI18n } from "@/lib/i18n";
import { isValidEmail } from "@/lib/validation";
import { cn } from "@/lib/utils";
import {
  adminApi,
  type ContractResponse,
  type CreateDepartmentWorkPackageRequest,
  type DepartmentWorkPackageOptionsResponse,
  type DepartmentWorkPackageResponse,
  type DepartmentWorkTaskResponse,
  type DepartmentWorkTaskAttachmentResponse,
  type InputPriceOfferResponse,
  type UpsertFinanceReviewRequest,
  type UpsertInputPriceOfferRequest,
  type OperationalProjectListItemResponse,
  type ProjectScheduleResponse,
  type UpsertDepartmentWorkTaskRequest,
  type UpdateDepartmentWorkPackageRequest,
  type WorkPackageKind,
  type WorkPackagePurpose,
  type WorkPackageStatus,
} from "@/services/adminApi";

const DepartmentWorkPackages = () => {
  const {
    LIST_PAGE_SIZE,
    id,
    location,
    params,
    setParams,
    navigate,
    t,
    currentUserId,
    has,
    packageId,
    kindFromPath,
    kind,
    subPages,
    activeSubPage,
    canViewProjects,
    canManage,
    canAssign,
    canProgress,
    canVerifyBusiness,
    canCreate,
    rows,
    total,
    detail,
    detailBackTarget,
    options,
    projects,
    contracts,
    projectSchedule,
    search,
    departmentFilter,
    statusFilter,
    purposeFilter,
    projectFilter,
    page,
    activeFilterCount,
    setPage,
    updateFilter,
    loading,
    error,
    filtersOpen,
    setFiltersOpen,
    packageOpen,
    setPackageOpen,
    packageSaveError,
    packageEditOpen,
    setPackageEditOpen,
    packageEditForm,
    setPackageEditForm,
    taskOpen,
    setTaskOpen,
    taskFiles,
    attachmentsOpen,
    setAttachmentsOpen,
    attachmentTask,
    attachments,
    attachmentFiles,
    attachmentBusy,
    attachmentError,
    progressOpen,
    setProgressOpen,
    saving,
    deleteOpen,
    setDeleteOpen,
    approvalPreview,
    setApprovalPreview,
    approvalBusy,
    approvalTo,
    setApprovalTo,
    approvalCc,
    setApprovalCc,
    approvalDirty,
    setApprovalDirty,
    approvalError,
    setApprovalError,
    approvalFeedback,
    approvalDeliveries,
    approvalNoticeRef,
    deleteImpact,
    deleteLoading,
    deleteError,
    deleting,
    taskEditing,
    progressTaskEditing,
    progressPercent,
    setProgressPercent,
    rejectTaskEditing,
    setRejectTaskEditing,
    rejectReason,
    setRejectReason,
    priceOpen,
    setPriceOpen,
    priceEditing,
    priceForm,
    setPriceForm,
    reviewForm,
    setReviewForm,
    packageForm,
    setPackageForm,
    taskForm,
    setTaskForm,
    unavailablePredecessorIds,
    load,
    departmentUsers,
    taskUsers,
    orderedTasks,
    availableDepartments,
    openCreatePackage,
    packageFieldProps,
    packageControlProps,
    savePackage,
    openEditPackage,
    saveEditedPackage,
    openTask,
    selectTaskFiles,
    openAttachments,
    addAttachments,
    downloadAttachment,
    deleteAttachment,
    previewDelete,
    confirmDelete,
    saveTask,
    transitionPackage,
    progressTask,
    openProgress,
    verifyTask,
    rejectTask,
    resetPriceForm,
    openPriceOffer,
    savePriceOffer,
    submitPriceOffer,
    recipientsValid,
    refreshApprovalPreview,
    confirmApproval,
    decidePriceOffer,
    submitBoq,
    decideBoq,
    saveFinanceReview,
    submitFinanceReview,
    decideFinanceReview,
    dateValue,
    suggestTaskWbsCode,
    apiDate,
    TASK_FILE_EXTENSIONS,
    statusTone,
  } = useDepartmentWorkPackages();
  const { lang } = useI18n();
  const taskWbsDuplicate = Boolean(
    taskForm.wbsCode.trim() &&
    detail?.tasks.some(
      (task) =>
        task.id !== taskEditing?.id &&
        task.wbsCode.trim().toLocaleLowerCase() === taskForm.wbsCode.trim().toLocaleLowerCase(),
    ),
  );
  const blockingPredecessorCodes = (task: DepartmentWorkTaskResponse) =>
    (task.blockingPredecessorTaskIds ?? [])
      .map((id) => detail?.tasks.find((item) => item.id === id)?.wbsCode ?? `#${id}`)
      .join(", ");
  if (loading && packageId)
    return (
      <AdminLayout>
        <PageLoading />
      </AdminLayout>
    );
  if (error)
    return (
      <AdminLayout>
        <PageError message={error} onRetry={() => void load()} />
      </AdminLayout>
    );

  return (
    <AdminLayout>
      <DeletionImpactDialog
        open={deleteOpen}
        impact={deleteImpact}
        loading={deleteLoading}
        deleting={deleting}
        error={deleteError}
        onOpenChange={setDeleteOpen}
        onConfirm={confirmDelete}
        onCompleted={() => {
          setDeleteOpen(false);
          navigate(detailBackTarget);
        }}
      />
      <div className="space-y-6" data-testid="work-packages-page">
        {approvalFeedback && (
          <div
            ref={approvalNoticeRef}
            role={approvalFeedback.failed ? "alert" : "status"}
            data-testid="approval-email-feedback"
            className={`rounded-lg border p-4 text-sm ${approvalFeedback.failed ? "border-destructive text-destructive" : "border-emerald-300 bg-emerald-50 text-emerald-900"}`}
          >
            {approvalFeedback.message}
          </div>
        )}
        {detail ? (
          <>
            <div className="min-w-0 space-y-4" data-testid="work-package-detail-header">
              <Button variant="ghost" asChild className="-ml-3">
                <Link data-testid="work-package-back" to={detailBackTarget}>
                  <ArrowLeft className="mr-2 h-4 w-4" />
                  {t("workPackages.back")}
                </Link>
              </Button>
              <div className="flex min-w-0 flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                <div className="min-w-0 space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <h1 className="min-w-0 max-w-full text-2xl font-bold [overflow-wrap:anywhere]">
                      {detail.code} · {detail.name}
                    </h1>
                    <Badge className={statusTone[detail.status]} variant="outline">
                      {t(`workPackages.status.${detail.status}`)}
                    </Badge>
                    <Badge variant="secondary">{t(`workPackages.purpose.${detail.purpose}`)}</Badge>
                    {detail.isOverdue && (
                      <Badge
                        className="border-rose-200 bg-rose-50 text-rose-700"
                        variant="outline"
                        title={t("workPackages.overdue.help")}
                      >
                        <AlertTriangle className="mr-1 h-3.5 w-3.5" />
                        {(detail.overdueDays ?? 0) === 1
                          ? t("workPackages.overdue.badgeOne")
                          : t("workPackages.overdue.badge", { count: detail.overdueDays ?? 0 })}
                      </Badge>
                    )}
                  </div>
                  <p className="flex items-center gap-1.5 text-sm text-muted-foreground">
                    <CalendarDays className="h-4 w-4 shrink-0" aria-hidden />
                    <span>
                      {t("workPackages.field.timeline")}:{" "}
                      {formatDate(detail.plannedStart, lang, { timeZone: "UTC" })} →{" "}
                      {formatDate(detail.plannedEnd, lang, { timeZone: "UTC" })}
                    </span>
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {detail.description || t("workPackages.noDescription")}
                  </p>
                  {detail.sourceQuoteId && (
                    <Link
                      className="inline-flex items-center gap-1 text-sm text-primary hover:underline"
                      to={`/admin/quotes/${detail.sourceQuoteId}`}
                    >
                      {t("workPackages.field.sourceQuote")}: {detail.sourceQuoteCode}
                    </Link>
                  )}
                </div>
                <div className="flex min-w-0 flex-wrap gap-2 lg:shrink-0 lg:justify-end [&_button]:h-auto [&_button]:min-h-10 [&_button]:whitespace-normal">
                  {canManage && detail.canManagePackage && detail.status !== "Cancelled" && (
                    <Button
                      data-testid="work-package-edit"
                      variant="outline"
                      onClick={() => void openEditPackage()}
                    >
                      {t("common.edit")}
                    </Button>
                  )}
                  {canManage && detail.canManagePackage && detail.status === "Completed" && (
                    <Button onClick={() => void transitionPackage("Active")}>
                      <RotateCcw className="mr-2 h-4 w-4" />
                      {t("workPackages.action.reopen")}
                    </Button>
                  )}
                  {canManage && detail.canManagePackage && (
                    <Button
                      variant="outline"
                      className="text-destructive hover:text-destructive"
                      onClick={() => void previewDelete()}
                    >
                      <Trash2 className="mr-2 h-4 w-4" />
                      {t("common.delete")}
                    </Button>
                  )}
                  {detail.canManagePackage && detail.status === "Planned" && (
                    <Button onClick={() => void transitionPackage("Active")}>
                      {t("workPackages.action.start")}
                    </Button>
                  )}
                  {detail.canManagePackage && detail.status === "Active" && (
                    <Button
                      variant="outline"
                      onClick={() => void transitionPackage("Completed")}
                      disabled={detail.completionBlockers.length > 0}
                      aria-describedby={
                        detail.completionBlockers.length > 0
                          ? "work-package-completion-blockers"
                          : undefined
                      }
                    >
                      {t("workPackages.action.completePackage")}
                    </Button>
                  )}
                  {canAssign &&
                    detail.canAssignTasks &&
                    detail.purpose !== "LegalPermitting" &&
                    !["Completed", "Cancelled"].includes(detail.status) && (
                      <Button className="w-full sm:w-auto" onClick={() => openTask()}>
                        <Plus className="mr-2 h-4 w-4" />
                        {t("workPackages.action.addTask")}
                      </Button>
                    )}
                </div>
              </div>
              {detail.status === "Active" && detail.completionBlockers.length > 0 && (
                <div
                  id="work-package-completion-blockers"
                  className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900"
                >
                  <p className="flex items-center gap-2 font-medium">
                    <AlertTriangle className="h-4 w-4 shrink-0" aria-hidden="true" />
                    {t("workPackages.blockers.title")}
                  </p>
                  <ul className="mt-1 list-disc space-y-0.5 pl-6">
                    {detail.completionBlockers.map((code) => (
                      <li key={code}>{t(`workPackages.blockers.${code}`)}</li>
                    ))}
                  </ul>
                </div>
              )}
            </div>

            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <Summary
                icon={<BriefcaseBusiness />}
                label={t("workPackages.field.scope")}
                value={
                  detail.kind !== "Internal"
                    ? `${detail.operationalProjectCode} · ${detail.operationalProjectName}`
                    : t("workPackages.kind.Internal")
                }
                href={
                  detail.operationalProjectId
                    ? `/admin/operational-projects/${detail.operationalProjectId}`
                    : undefined
                }
              />
              <Summary
                icon={<Users />}
                label={t("workPackages.field.department")}
                value={t(detail.departmentLabelKey)}
              />
              <Summary
                icon={<Users />}
                label={t("workPackages.field.lead")}
                value={detail.leadUserName}
              />
              <Summary
                icon={<ClipboardList />}
                label={t("workPackages.field.contract")}
                value={detail.contractNumber || "—"}
                href={detail.contractId ? `/admin/contracts/${detail.contractId}` : undefined}
              />
            </div>

            {detail.purpose === "ProcurementInputPricing" && (
              <WorkPackageBoqSection
                packageId={detail.id}
                open={!["Completed", "Cancelled"].includes(detail.status)}
                boqs={detail.boqWorkbooks ?? []}
                canProgress={canProgress}
                // Only the package lead or an administrator decides, never the uploader.
                canVerify={canVerifyBusiness && detail.canAssignTasks}
                currentUserId={currentUserId}
                onChanged={() => void load()}
                onSubmit={(boq) => void submitBoq(boq.id, boq.rowVersion)}
                onDecide={(boq, approved) => void decideBoq(boq.id, approved, boq.rowVersion)}
              />
            )}

            {detail.purpose === "ProcurementInputPricing" && (
              <section className="rounded-xl border bg-card">
                <div className="flex flex-wrap items-center justify-between gap-3 border-b p-4">
                  <div>
                    <h2 className="font-semibold">{t("workPackages.inputPrice.title")}</h2>
                    <p className="text-sm text-muted-foreground">
                      {t("workPackages.inputPrice.help")}
                    </p>
                  </div>
                  {canProgress && !["Completed", "Cancelled"].includes(detail.status) && (
                    <Button onClick={() => openPriceOffer()}>
                      <Plus className="mr-2 h-4 w-4" />
                      {t("workPackages.inputPrice.add")}
                    </Button>
                  )}
                </div>
                <div className="divide-y">
                  {detail.inputPriceOffers.map((offer) => (
                    <article key={offer.id} className="space-y-3 p-4">
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div>
                          <div className="flex flex-wrap items-center gap-2">
                            <strong>
                              {offer.vendorCode} · {offer.vendorName}
                            </strong>
                            <Badge variant="outline">
                              {t(`workPackages.businessStatus.${offer.status}`)}
                            </Badge>
                          </div>
                          <p className="text-sm text-muted-foreground">
                            {offer.supplierReference} · {t("workPackages.inputPrice.validUntil")}{" "}
                            {formatDate(offer.validUntil, lang)}
                          </p>
                        </div>
                        <strong>{money(offer.grandTotal, offer.currency)}</strong>
                      </div>
                      <div className="overflow-x-auto">
                        <table className="w-full min-w-[640px] text-sm">
                          <thead>
                            <tr className="text-left text-muted-foreground">
                              <th className="py-2">{t("workPackages.inputPrice.item")}</th>
                              <th>{t("workPackages.inputPrice.unit")}</th>
                              <th className="text-right">
                                {t("workPackages.inputPrice.quantity")}
                              </th>
                              <th className="text-right">
                                {t("workPackages.inputPrice.unitPrice")}
                              </th>
                              <th className="text-right">{t("workPackages.inputPrice.amount")}</th>
                            </tr>
                          </thead>
                          <tbody>
                            {offer.lines.map((line) => (
                              <tr key={line.id} className="border-t">
                                <td className="py-2">
                                  {line.itemCode} · {line.description}
                                </td>
                                <td>{line.unit}</td>
                                <td className="text-right">
                                  {new Intl.NumberFormat("vi-VN").format(line.quantity)}
                                </td>
                                <td className="text-right">
                                  {money(line.unitPrice, offer.currency)}
                                </td>
                                <td className="text-right">{money(line.amount, offer.currency)}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                      <div className="flex flex-wrap justify-end gap-2">
                        {canProgress && ["Draft", "Rejected"].includes(offer.status) && (
                          <Button size="sm" variant="outline" onClick={() => openPriceOffer(offer)}>
                            {t("common.edit")}
                          </Button>
                        )}
                        {canProgress && ["Draft", "Rejected"].includes(offer.status) && (
                          <Button
                            size="sm"
                            onClick={() => void submitPriceOffer(offer.id, offer.rowVersion)}
                          >
                            {t("workPackages.action.submit")}
                          </Button>
                        )}
                        {canVerifyBusiness && offer.status === "Submitted" && (
                          <>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() =>
                                void decidePriceOffer(offer.id, false, offer.rowVersion)
                              }
                            >
                              {t("common.reject")}
                            </Button>
                            <Button
                              size="sm"
                              onClick={() =>
                                void decidePriceOffer(offer.id, true, offer.rowVersion)
                              }
                            >
                              {t("common.approve")}
                            </Button>
                          </>
                        )}
                      </div>
                    </article>
                  ))}
                  {detail.inputPriceOffers.length === 0 && (
                    <p className="p-8 text-center text-sm text-muted-foreground">
                      {t("workPackages.inputPrice.empty")}
                    </p>
                  )}
                </div>
              </section>
            )}

            {(detail.purpose === "FinanceCommercialReview" ||
              detail.purpose === "FinanceTenderReview") && (
              <section className="rounded-xl border bg-card p-4">
                <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <h2 className="font-semibold">
                      {t(`workPackages.review.title.${detail.purpose}`)}
                    </h2>
                    <p className="text-sm text-muted-foreground">{t("workPackages.review.help")}</p>
                  </div>
                  <Badge variant="outline">
                    {t(`workPackages.businessStatus.${detail.financeReview?.status ?? "Draft"}`)}
                  </Badge>
                </div>
                <div className="grid gap-3 sm:grid-cols-2">
                  {detail.purpose === "FinanceCommercialReview" && (
                    <>
                      <ReviewCheck
                        label={t("workPackages.review.contractConditions")}
                        checked={reviewForm.contractConditionsReviewed}
                        disabled={
                          !canProgress ||
                          detail.financeReview?.status === "Submitted" ||
                          detail.financeReview?.status === "Approved"
                        }
                        onChange={(contractConditionsReviewed) =>
                          setReviewForm((current) => ({ ...current, contractConditionsReviewed }))
                        }
                      />
                      <ReviewCheck
                        label={t("workPackages.review.paymentTerms")}
                        checked={reviewForm.paymentTermsReviewed}
                        disabled={
                          !canProgress ||
                          detail.financeReview?.status === "Submitted" ||
                          detail.financeReview?.status === "Approved"
                        }
                        onChange={(paymentTermsReviewed) =>
                          setReviewForm((current) => ({ ...current, paymentTermsReviewed }))
                        }
                      />
                    </>
                  )}
                  <ReviewCheck
                    label={t("workPackages.review.guarantees")}
                    checked={reviewForm.guaranteesReviewed}
                    disabled={
                      !canProgress ||
                      detail.financeReview?.status === "Submitted" ||
                      detail.financeReview?.status === "Approved"
                    }
                    onChange={(guaranteesReviewed) =>
                      setReviewForm((current) => ({ ...current, guaranteesReviewed }))
                    }
                  />
                  {detail.purpose === "FinanceTenderReview" && (
                    <ReviewCheck
                      label={t("workPackages.review.financialDossier")}
                      checked={reviewForm.financialDossierReviewed}
                      disabled={
                        !canProgress ||
                        detail.financeReview?.status === "Submitted" ||
                        detail.financeReview?.status === "Approved"
                      }
                      onChange={(financialDossierReviewed) =>
                        setReviewForm((current) => ({ ...current, financialDossierReviewed }))
                      }
                    />
                  )}
                  <Field label={t("workPackages.review.findings")} className="sm:col-span-2">
                    <Textarea
                      maxLength={4000}
                      disabled={
                        !canProgress ||
                        detail.financeReview?.status === "Submitted" ||
                        detail.financeReview?.status === "Approved"
                      }
                      value={reviewForm.findings ?? ""}
                      onChange={(event) =>
                        setReviewForm((current) => ({ ...current, findings: event.target.value }))
                      }
                    />
                  </Field>
                </div>
                <div className="mt-4 flex flex-wrap justify-end gap-2">
                  {canProgress &&
                    (!detail.financeReview ||
                      ["Draft", "Rejected"].includes(detail.financeReview.status)) && (
                      <Button variant="outline" onClick={() => void saveFinanceReview()}>
                        {t("common.save")}
                      </Button>
                    )}
                  {canProgress && detail.financeReview?.status === "Draft" && (
                    <Button onClick={() => void submitFinanceReview()}>
                      {t("workPackages.action.submit")}
                    </Button>
                  )}
                  {canVerifyBusiness && detail.financeReview?.status === "Submitted" && (
                    <>
                      <Button variant="outline" onClick={() => void decideFinanceReview(false)}>
                        {t("common.reject")}
                      </Button>
                      <Button onClick={() => void decideFinanceReview(true)}>
                        {t("common.approve")}
                      </Button>
                    </>
                  )}
                </div>
              </section>
            )}

            <section className="rounded-xl border bg-card">
              <div className="flex items-center justify-between border-b p-4">
                <div>
                  <h2 className="font-semibold">{t("workPackages.tasks.title")}</h2>
                  <p className="text-sm text-muted-foreground">{t("workPackages.tasks.help")}</p>
                </div>
                <span className="text-sm font-medium">{detail.progressPercent}%</span>
              </div>
              <div className="p-4">
                <Progress value={detail.progressPercent} />
              </div>
              <div className="divide-y">
                {orderedTasks.map(({ task, depth }) => (
                  <article
                    key={task.id}
                    id={`task-${task.id}`}
                    data-testid="work-task-row"
                    className={cn(
                      "p-4",
                      depth > 0 && "border-l-2 border-l-primary/30",
                      params.get("taskId") === String(task.id) && "bg-amber-50",
                    )}
                    style={{ marginLeft: Math.min(depth, 3) * 12 }}
                  >
                    <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                      <div className="min-w-0 space-y-1 lg:flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <Badge variant="outline">{task.wbsCode}</Badge>
                          <h3 className="font-medium">{task.title}</h3>
                          <Badge variant="secondary">
                            {t(`workPackages.taskStatus.${task.status}`)}
                          </Badge>
                          {task.hasSubtasks && (
                            <Badge variant="outline">{t("workPackages.task.aggregated")}</Badge>
                          )}
                          {task.kpiDefinitionCode && <Badge>{t("workPackages.tasks.kpi")}</Badge>}
                          {task.isOverdue && (
                            <Badge
                              className="border-rose-200 bg-rose-50 text-rose-700"
                              variant="outline"
                              title={t("workPackages.overdue.help")}
                            >
                              <AlertTriangle className="mr-1 h-3.5 w-3.5" />
                              {(task.overdueDays ?? 0) === 1
                                ? t("workPackages.overdue.badgeOne")
                                : t("workPackages.overdue.badge", { count: task.overdueDays ?? 0 })}
                            </Badge>
                          )}
                        </div>
                        <p className="text-sm text-muted-foreground">
                          {task.assigneeName} ·{" "}
                          {formatDate(task.plannedStart, lang, { timeZone: "UTC" })} →{" "}
                          {formatDate(task.plannedEnd, lang, { timeZone: "UTC" })}
                        </p>
                        {(task.blockingPredecessorTaskIds ?? []).length > 0 && (
                          <p className="text-sm text-amber-700">
                            {t("workPackages.blockers.predecessors", {
                              tasks: blockingPredecessorCodes(task),
                            })}
                          </p>
                        )}
                      </div>
                      <div className="flex flex-wrap items-center gap-2 lg:shrink-0 lg:justify-end">
                        {canAssign &&
                          detail.canAssignTasks &&
                          !task.kpiDefinitionId &&
                          ["Planned", "InProgress"].includes(task.status) && (
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => openTask(undefined, task)}
                            >
                              {t("workPackages.action.addSubtask")}
                            </Button>
                          )}
                        <span className="w-12 text-right text-sm font-medium">
                          {task.progressPercent}%
                        </span>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => void openAttachments(task)}
                        >
                          <Paperclip className="mr-1.5 h-4 w-4" />
                          {t("workPackages.attachments.title")}
                        </Button>
                        {canAssign &&
                          detail.canAssignTasks &&
                          ["Planned", "InProgress"].includes(task.status) && (
                            <Button variant="outline" size="sm" onClick={() => openTask(task)}>
                              {t("common.edit")}
                            </Button>
                          )}
                        {detail.status === "Active" &&
                          task.canUpdateProgress &&
                          ["Planned", "InProgress"].includes(task.status) && (
                            <Button variant="outline" size="sm" onClick={() => openProgress(task)}>
                              {t("workPackages.action.progress")}
                            </Button>
                          )}
                        {detail.status === "Active" &&
                          task.canUpdateProgress &&
                          ["Planned", "InProgress"].includes(task.status) && (
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => void progressTask(task, true)}
                              disabled={(task.blockingPredecessorTaskIds ?? []).length > 0}
                              title={
                                (task.blockingPredecessorTaskIds ?? []).length > 0
                                  ? t("workPackages.blockers.completeTask")
                                  : undefined
                              }
                            >
                              {t("workPackages.action.completeTask")}
                            </Button>
                          )}
                        {detail.status === "Active" && task.canVerify && (
                          <>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => {
                                setRejectTaskEditing(task);
                                setRejectReason("");
                              }}
                            >
                              {t("common.reject")}
                            </Button>
                            <Button size="sm" onClick={() => void verifyTask(task)}>
                              <CheckCircle2 className="mr-2 h-4 w-4" />
                              {t("workPackages.action.verify")}
                            </Button>
                          </>
                        )}
                      </div>
                    </div>
                  </article>
                ))}
                {detail.tasks.length === 0 && (
                  <div className="p-10 text-center text-sm text-muted-foreground">
                    {t("workPackages.tasks.empty")}
                  </div>
                )}
              </div>
            </section>

            {approvalDeliveries.length > 0 && (
              <section
                data-testid="approval-email-history"
                className="min-w-0 overflow-hidden rounded-xl border bg-card"
              >
                <div className="flex flex-wrap items-center justify-between gap-3 border-b p-4">
                  <h2 className="font-semibold">{t("approvalEmail.delivery")}</h2>
                  <Button variant="outline" size="sm" onClick={() => void load()}>
                    {t("common.refresh")}
                  </Button>
                </div>
                <div className="divide-y">
                  {approvalDeliveries.map((email) => (
                    <div
                      key={email.id}
                      className="flex min-w-0 flex-col gap-2 p-4 text-sm sm:flex-row sm:items-start sm:justify-between sm:gap-4"
                    >
                      <div className="min-w-0 space-y-1">
                        <p className="break-words font-medium">
                          {email.recordType === "InputPriceOffer"
                            ? detail.inputPriceOffers.find((offer) => offer.id === email.recordId)
                                ?.supplierReference
                            : email.recordType === "ProjectBoqRevision"
                              ? `BOQ R${
                                  detail.boqWorkbooks?.find((boq) => boq.id === email.recordId)
                                    ?.revisionNumber ?? ""
                                }`
                              : t(`workPackages.purpose.${detail.purpose}`)}
                        </p>
                        <p className="break-all text-muted-foreground">{email.toEmail}</p>
                        {(email.ccEmails?.length ?? 0) > 0 && (
                          <p className="break-all text-muted-foreground">
                            CC: {email.ccEmails.join(", ")}
                          </p>
                        )}
                      </div>
                      <Badge
                        className="w-fit shrink-0"
                        variant={email.status === "NeedsReview" ? "destructive" : "secondary"}
                      >
                        {t(`approvalEmail.status.${email.status}`)}
                      </Badge>
                    </div>
                  ))}
                </div>
                {approvalDeliveries.some(
                  (email) => email.status === "Sending" || email.status === "NeedsReview",
                ) && (
                  <p className="border-t p-4 text-sm text-muted-foreground">
                    {t("approvalEmail.reviewHint")}
                  </p>
                )}
              </section>
            )}
          </>
        ) : (
          <>
            <div className="flex flex-wrap items-end justify-between gap-4">
              <div>
                <h1 className="text-3xl font-bold">{t(activeSubPage.titleKey)}</h1>
                <p className="mt-1 text-muted-foreground">{t(activeSubPage.descriptionKey)}</p>
              </div>
              {canCreate && (
                <Button onClick={openCreatePackage}>
                  <Plus className="mr-2 h-4 w-4" />
                  {t("workPackages.action.create")}
                </Button>
              )}
            </div>
            <nav
              className="grid grid-cols-3 gap-1 rounded-lg bg-muted p-1 sm:inline-flex sm:flex-wrap"
              aria-label={t("workPackages.subpage.navLabel")}
            >
              {subPages.map((page) => {
                const next = new URLSearchParams(params);
                next.delete("kind");
                next.delete("projectId");
                const query = next.toString();
                const active = page.kind === kind;
                return (
                  <Link
                    key={page.kind}
                    to={query ? `${page.path}?${query}` : page.path}
                    aria-current={active ? "page" : undefined}
                    className={cn(
                      "flex items-center justify-center whitespace-nowrap rounded-md px-1.5 py-2 text-center text-[11px] font-medium transition-colors sm:px-3 sm:py-1.5 sm:text-sm",
                      active
                        ? "bg-background text-foreground shadow-sm"
                        : "text-muted-foreground hover:text-foreground",
                    )}
                  >
                    {t(page.tabKey)}
                  </Link>
                );
              })}
            </nav>
            <section
              className="space-y-3 rounded-xl border bg-card p-4"
              aria-label={t("workPackages.filters.title")}
            >
              <div className="space-y-1">
                <Label htmlFor="wp-search">{t("workPackages.filters.search")}</Label>
                <div className="relative">
                  <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                  <Input
                    id="wp-search"
                    value={search}
                    onChange={(event) => updateFilter("search", event.target.value)}
                    placeholder={t("workPackages.search")}
                    className="pl-9"
                  />
                </div>
              </div>
              <Button
                type="button"
                variant="outline"
                className="w-full sm:hidden"
                aria-expanded={filtersOpen}
                aria-controls="wp-filters-more"
                onClick={() => setFiltersOpen((open) => !open)}
              >
                <SlidersHorizontal className="mr-2 h-4 w-4" />
                {t("workPackages.filters.title")}
                {activeFilterCount > 0 ? ` (${activeFilterCount})` : ""}
                <ChevronDown
                  className={cn(
                    "ml-auto h-4 w-4 transition-transform",
                    filtersOpen && "rotate-180",
                  )}
                />
              </Button>
              <div
                id="wp-filters-more"
                className={cn(
                  "grid gap-3 sm:grid-cols-2 xl:grid-cols-4",
                  !filtersOpen && "hidden sm:grid",
                )}
              >
                <div className="min-w-0 space-y-1">
                  <Label htmlFor="wp-department">{t("workPackages.field.department")}</Label>
                  <select
                    id="wp-department"
                    className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                    value={departmentFilter}
                    onChange={(event) => updateFilter("departmentId", event.target.value)}
                  >
                    <option value="">{t("workPackages.filters.allDepartments")}</option>
                    {options.departments.map((department) => (
                      <option key={department.id} value={department.id}>
                        {t(department.labelKey)}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="min-w-0 space-y-1">
                  <Label htmlFor="wp-status">{t("workPackages.filters.status")}</Label>
                  <select
                    id="wp-status"
                    className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                    value={statusFilter}
                    onChange={(event) => updateFilter("status", event.target.value)}
                  >
                    <option value="">{t("workPackages.filters.allStatuses")}</option>
                    {(Object.keys(statusTone) as WorkPackageStatus[]).map((status) => (
                      <option key={status} value={status}>
                        {t(`workPackages.status.${status}`)}
                      </option>
                    ))}
                  </select>
                </div>
                {kind !== "Internal" && canViewProjects && (
                  <div className="min-w-0 space-y-1 sm:col-span-2">
                    <Label htmlFor="wp-project">{t("workPackages.field.project")}</Label>
                    <select
                      id="wp-project"
                      className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                      value={projectFilter}
                      onChange={(event) => updateFilter("projectId", event.target.value)}
                    >
                      <option value="">{t("workPackages.filters.allProjects")}</option>
                      {projects.map((project) => (
                        <option key={project.id} value={project.id}>
                          {project.code} · {project.name}
                        </option>
                      ))}
                    </select>
                  </div>
                )}
                <div className="min-w-0 space-y-1">
                  <Label htmlFor="wp-purpose">{t("workPackages.field.purpose")}</Label>
                  <select
                    id="wp-purpose"
                    className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                    value={purposeFilter}
                    onChange={(event) => updateFilter("purpose", event.target.value)}
                  >
                    <option value="">{t("workPackages.filters.allPurposes")}</option>
                    {(
                      [
                        "General",
                        "ProcurementInputPricing",
                        "FinanceCommercialReview",
                        "FinanceTenderReview",
                        "TenderPlanning",
                        "LegalPermitting",
                      ] as WorkPackagePurpose[]
                    ).map((purpose) => (
                      <option key={purpose} value={purpose}>
                        {t(`workPackages.purpose.${purpose}`)}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="flex items-end xl:col-span-2 xl:col-start-3 xl:justify-end">
                  <Button
                    variant="outline"
                    className="w-full sm:w-auto"
                    disabled={
                      !search &&
                      !departmentFilter &&
                      !statusFilter &&
                      !purposeFilter &&
                      !projectFilter
                    }
                    onClick={() => setParams(kindFromPath || kind === "Project" ? {} : { kind })}
                  >
                    {t("workPackages.filters.clear")}
                  </Button>
                </div>
              </div>
            </section>
            {loading && <PageLoading />}
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              {!loading &&
                rows.map((item) => (
                  <Link
                    key={item.id}
                    data-testid="work-package-card"
                    to={`/admin/work-packages/${item.id}${location.search}`}
                    state={{ workPackageListPath: location.pathname }}
                    className="min-w-0 rounded-xl border bg-card p-5 text-left transition hover:border-primary/50 hover:shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                  >
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge variant="outline" className="whitespace-nowrap">
                        {item.code}
                      </Badge>
                      <Badge
                        className={cn("whitespace-nowrap", statusTone[item.status])}
                        variant="outline"
                      >
                        {t(`workPackages.status.${item.status}`)}
                      </Badge>
                    </div>
                    <h2 className="mt-4 font-semibold">{item.name}</h2>
                    <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">
                      {item.kind === "Internal"
                        ? t("workPackages.kind.Internal")
                        : item.kind === "Contract"
                          ? `${item.contractNumber} · ${item.operationalProjectName}`
                          : `${item.operationalProjectCode} · ${item.operationalProjectName}`}
                    </p>
                    <div className="mt-4 flex items-center justify-between text-sm">
                      <span>
                        {t(item.departmentLabelKey)} · {item.leadUserName}
                      </span>
                      <strong>{item.progressPercent}%</strong>
                    </div>
                    <Progress value={item.progressPercent} className="mt-2" />
                  </Link>
                ))}
              {!loading && rows.length === 0 && (
                <div className="col-span-full rounded-xl border border-dashed p-12 text-center text-muted-foreground">
                  {t("workPackages.empty")}
                </div>
              )}
            </div>
            {!loading && total > 0 && (
              <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground">
                <div>{t("common.pagination.total").replace("{total}", String(total))}</div>
                <div className="flex gap-1">
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={page <= 1}
                    onClick={() => setPage(page - 1)}
                  >
                    {t("common.pagination.prev")}
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={page * LIST_PAGE_SIZE >= total}
                    onClick={() => setPage(page + 1)}
                  >
                    {t("common.pagination.next")}
                  </Button>
                </div>
              </div>
            )}
          </>
        )}
      </div>

      <Dialog open={packageOpen} onOpenChange={setPackageOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader className="pr-6">
            <DialogTitle>
              {packageForm.sourceQuoteId && packageForm.purpose === "ProcurementInputPricing"
                ? t("workPackages.create.inputPriceForQuote", { quote: packageForm.sourceQuoteId })
                : t("workPackages.create.title")}
            </DialogTitle>
            <DialogDescription>{t("workPackages.create.help")}</DialogDescription>
          </DialogHeader>
          {packageForm.sourceQuoteId && (
            <p
              className="rounded-lg border bg-muted p-3 text-sm"
              data-testid="work-package-source-quote"
            >
              {t("workPackages.field.sourceQuote")}: #{packageForm.sourceQuoteId}
            </p>
          )}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t("workPackages.field.type")} {...packageFieldProps("type")}>
              <Select
                value={packageForm.kind}
                onValueChange={(value: WorkPackageKind) =>
                  setPackageForm((current) => ({
                    ...current,
                    kind: value,
                    purpose: "General",
                    operationalProjectId: null,
                    contractId: null,
                    sourceOpportunityId: null,
                    sourceQuoteId: null,
                    sourceTenderId: null,
                    departmentRoleGroupId: 0,
                    participantDepartmentRoleGroupIds: [],
                    leadUserId: 0,
                  }))
                }
              >
                <SelectTrigger {...packageControlProps("type")}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Project">{t("workPackages.kind.Project")}</SelectItem>
                  <SelectItem value="Contract">{t("workPackages.kind.Contract")}</SelectItem>
                  {canAssign && (
                    <SelectItem value="Internal">{t("workPackages.kind.Internal")}</SelectItem>
                  )}
                </SelectContent>
              </Select>
            </Field>
            {packageForm.kind === "Project" && (
              <Field label={t("workPackages.field.purpose")} {...packageFieldProps("purpose")}>
                <Select
                  disabled={packageForm.sourceTenderId != null || packageForm.sourceQuoteId != null}
                  value={packageForm.purpose}
                  onValueChange={(purpose: WorkPackagePurpose) => {
                    const code =
                      purpose === "ProcurementInputPricing"
                        ? "PROCUREMENT"
                        : purpose.startsWith("Finance")
                          ? "FINANCE"
                          : "";
                    const department = options.departments.find((group) => group.code === code);
                    setPackageForm((current) => ({
                      ...current,
                      purpose,
                      contractId: purpose === "General" ? current.contractId : null,
                      departmentRoleGroupId: department?.id ?? 0,
                      leadUserId: 0,
                    }));
                  }}
                >
                  <SelectTrigger
                    {...packageControlProps("purpose")}
                    data-testid="work-package-purpose"
                  >
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="General">{t("workPackages.purpose.General")}</SelectItem>
                    <SelectItem value="ProcurementInputPricing">
                      {t("workPackages.purpose.ProcurementInputPricing")}
                    </SelectItem>
                    <SelectItem value="FinanceCommercialReview">
                      {t("workPackages.purpose.FinanceCommercialReview")}
                    </SelectItem>
                    <SelectItem value="FinanceTenderReview">
                      {t("workPackages.purpose.FinanceTenderReview")}
                    </SelectItem>
                    {packageForm.sourceTenderId != null && (
                      <SelectItem value="TenderPlanning">
                        {t("workPackages.purpose.TenderPlanning")}
                      </SelectItem>
                    )}
                  </SelectContent>
                </Select>
              </Field>
            )}
            {packageForm.kind !== "Internal" && (
              <Field
                label={t("workPackages.field.project")}
                {...packageFieldProps("project", true)}
              >
                <Select
                  disabled={packageForm.sourceTenderId != null || packageForm.sourceQuoteId != null}
                  value={packageForm.operationalProjectId?.toString() ?? ""}
                  onValueChange={(value) =>
                    setPackageForm((current) => ({
                      ...current,
                      operationalProjectId: Number(value),
                      contractId: null,
                    }))
                  }
                >
                  <SelectTrigger
                    {...packageControlProps("project", true)}
                    data-testid="work-package-project"
                  >
                    <SelectValue placeholder={t("workPackages.placeholder.project")} />
                  </SelectTrigger>
                  <SelectContent>
                    {projects.map((project) => (
                      <SelectItem key={project.id} value={String(project.id)}>
                        {project.code} · {project.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
            )}
            {packageForm.kind === "Contract" && (
              <Field
                label={t("workPackages.field.requiredContract")}
                {...packageFieldProps("contract", true)}
              >
                <Select
                  value={packageForm.contractId?.toString() ?? ""}
                  onValueChange={(value) =>
                    setPackageForm((current) => ({
                      ...current,
                      contractId: value === "none" ? null : Number(value),
                    }))
                  }
                >
                  <SelectTrigger {...packageControlProps("contract", true)}>
                    <SelectValue placeholder={t("workPackages.placeholder.contract")} />
                  </SelectTrigger>
                  <SelectContent>
                    {contracts.map((contract) => (
                      <SelectItem key={contract.id} value={String(contract.id)}>
                        {contract.contractNumber}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
            )}
            <Field
              label={t("workPackages.field.name")}
              {...packageFieldProps("name", true)}
              className="sm:col-span-2"
            >
              <Input
                {...packageControlProps("name", true)}
                data-testid="work-package-name"
                maxLength={300}
                value={packageForm.name}
                onChange={(event) =>
                  setPackageForm((current) => ({ ...current, name: event.target.value }))
                }
              />
            </Field>
            <Field
              label={t("workPackages.field.department")}
              {...packageFieldProps("department", true)}
            >
              <Select
                disabled={
                  packageForm.purpose !== "General" && packageForm.purpose !== "TenderPlanning"
                }
                value={
                  packageForm.departmentRoleGroupId ? String(packageForm.departmentRoleGroupId) : ""
                }
                onValueChange={(value) =>
                  setPackageForm((current) => ({
                    ...current,
                    departmentRoleGroupId: Number(value),
                    participantDepartmentRoleGroupIds:
                      current.participantDepartmentRoleGroupIds.filter(
                        (id) => id !== Number(value),
                      ),
                    leadUserId: 0,
                  }))
                }
              >
                <SelectTrigger
                  {...packageControlProps("department", true)}
                  data-testid="work-package-department"
                >
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {availableDepartments.map((group) => (
                    <SelectItem key={group.id} value={String(group.id)}>
                      {t(group.labelKey)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
            <Field label={t("workPackages.field.lead")} {...packageFieldProps("lead", true)}>
              <div data-testid="work-package-lead">
                <SearchableSelect
                  {...packageControlProps("lead", true)}
                  required
                  value={packageForm.leadUserId ? String(packageForm.leadUserId) : ""}
                  onChange={(value) =>
                    setPackageForm((current) => ({ ...current, leadUserId: Number(value) }))
                  }
                  ariaLabel={t("workPackages.field.lead")}
                  options={departmentUsers.map((user) => personOption(user, t))}
                />
              </div>
            </Field>
            {packageForm.kind === "Internal" && (
              <Field label={t("workPackages.field.participants")} className="sm:col-span-2">
                <ChoiceGrid
                  emptyLabel={t("workPackages.participants.none")}
                  items={options.departments
                    .filter((group) => group.id !== packageForm.departmentRoleGroupId)
                    .map((group) => ({ id: group.id, label: t(group.labelKey) }))}
                  selected={packageForm.participantDepartmentRoleGroupIds}
                  onChange={(participantDepartmentRoleGroupIds) =>
                    setPackageForm((current) => ({ ...current, participantDepartmentRoleGroupIds }))
                  }
                />
              </Field>
            )}
            <Field
              label={t("workPackages.field.start")}
              {...packageFieldProps("start", packageForm.purpose === "TenderPlanning")}
            >
              <Input
                {...packageControlProps("start", packageForm.purpose === "TenderPlanning")}
                data-testid="work-package-start"
                type="date"
                value={dateValue(packageForm.plannedStart)}
                onChange={(event) =>
                  setPackageForm((current) => ({
                    ...current,
                    plannedStart: event.target.value ? apiDate(event.target.value) : null,
                  }))
                }
              />
            </Field>
            <Field
              label={t("workPackages.field.end")}
              {...packageFieldProps("end", packageForm.purpose === "TenderPlanning")}
            >
              <Input
                {...packageControlProps("end", packageForm.purpose === "TenderPlanning")}
                data-testid="work-package-end"
                type="date"
                value={dateValue(packageForm.plannedEnd)}
                onChange={(event) =>
                  setPackageForm((current) => ({
                    ...current,
                    plannedEnd: event.target.value ? apiDate(event.target.value) : null,
                  }))
                }
              />
            </Field>
            <Field
              label={t("workPackages.field.description")}
              {...packageFieldProps("description")}
              className="sm:col-span-2"
            >
              <Textarea
                {...packageControlProps("description")}
                maxLength={4000}
                value={packageForm.description ?? ""}
                onChange={(event) =>
                  setPackageForm((current) => ({ ...current, description: event.target.value }))
                }
              />
            </Field>
          </div>
          {packageSaveError && (
            <p role="alert" className="text-sm text-destructive">
              {packageSaveError}
            </p>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setPackageOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button disabled={saving} onClick={() => void savePackage()}>
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={packageEditOpen} onOpenChange={setPackageEditOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>{t("workPackages.edit.title")}</DialogTitle>
            <DialogDescription>{t("workPackages.edit.help")}</DialogDescription>
          </DialogHeader>
          {detail && packageEditForm && (
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label={t("workPackages.field.name")} className="sm:col-span-2">
                <Input
                  maxLength={300}
                  value={packageEditForm.name}
                  onChange={(event) =>
                    setPackageEditForm((current) =>
                      current ? { ...current, name: event.target.value } : current,
                    )
                  }
                />
              </Field>
              {detail.kind !== "Internal" && detail.purpose === "General" && (
                <Field label={t("workPackages.field.contract")} className="sm:col-span-2">
                  <Select
                    value={packageEditForm.contractId?.toString() ?? "none"}
                    onValueChange={(value) =>
                      setPackageEditForm((current) =>
                        current
                          ? { ...current, contractId: value === "none" ? null : Number(value) }
                          : current,
                      )
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">{t("workPackages.contract.none")}</SelectItem>
                      {detail.contractId &&
                        !contracts.some((contract) => contract.id === detail.contractId) && (
                          <SelectItem value={String(detail.contractId)}>
                            {detail.contractNumber}
                          </SelectItem>
                        )}
                      {contracts.map((contract) => (
                        <SelectItem key={contract.id} value={String(contract.id)}>
                          {contract.contractNumber}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </Field>
              )}
              <Field label={t("workPackages.field.start")}>
                <Input
                  type="date"
                  value={dateValue(packageEditForm.plannedStart)}
                  onChange={(event) =>
                    setPackageEditForm((current) =>
                      current
                        ? {
                            ...current,
                            plannedStart: event.target.value ? apiDate(event.target.value) : null,
                          }
                        : current,
                    )
                  }
                />
              </Field>
              <Field label={t("workPackages.field.end")}>
                <Input
                  type="date"
                  value={dateValue(packageEditForm.plannedEnd)}
                  onChange={(event) =>
                    setPackageEditForm((current) =>
                      current
                        ? {
                            ...current,
                            plannedEnd: event.target.value ? apiDate(event.target.value) : null,
                          }
                        : current,
                    )
                  }
                />
              </Field>
              <Field label={t("workPackages.field.description")} className="sm:col-span-2">
                <Textarea
                  maxLength={4000}
                  value={packageEditForm.description ?? ""}
                  onChange={(event) =>
                    setPackageEditForm((current) =>
                      current ? { ...current, description: event.target.value } : current,
                    )
                  }
                />
              </Field>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setPackageEditOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button disabled={saving || !packageEditForm} onClick={() => void saveEditedPackage()}>
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={!!approvalPreview}
        onOpenChange={(open) => {
          if (!open && !approvalBusy) setApprovalPreview(null);
        }}
      >
        <DialogContent className="flex max-h-[92dvh] w-[calc(100%_-_2rem)] max-w-4xl flex-col overflow-hidden">
          <DialogHeader>
            <DialogTitle>{t("approvalEmail.preview")}</DialogTitle>
            <DialogDescription>{t("approvalEmail.confirmHint")}</DialogDescription>
          </DialogHeader>
          {approvalPreview && (
            <div className="min-h-0 space-y-3 overflow-y-auto p-1.5">
              <div className="space-y-2">
                <Label htmlFor="approval-to">{t("approvalEmail.recipient")}</Label>
                <Input
                  id="approval-to"
                  type="email"
                  maxLength={150}
                  value={approvalTo}
                  disabled={approvalBusy}
                  onChange={(event) => {
                    setApprovalTo(event.target.value);
                    setApprovalDirty(true);
                    setApprovalError("");
                  }}
                />
                <Label htmlFor="approval-cc">{t("approvalEmail.cc")}</Label>
                <Textarea
                  id="approval-cc"
                  rows={2}
                  maxLength={4000}
                  value={approvalCc}
                  disabled={approvalBusy}
                  placeholder="cm@nicon.vn, qs@nicon.vn"
                  aria-describedby="approval-recipient-hint"
                  onChange={(event) => {
                    setApprovalCc(event.target.value);
                    setApprovalDirty(true);
                    setApprovalError("");
                  }}
                />
                <p id="approval-recipient-hint" className="text-xs text-muted-foreground">
                  {t("approvalEmail.recipientHint")}
                </p>
                {!recipientsValid && (
                  <p role="alert" className="text-sm text-destructive">
                    {t("approvalEmail.invalidRecipients")}
                  </p>
                )}
                {approvalDirty && (
                  <p className="text-sm text-muted-foreground">
                    {t("approvalEmail.refreshRequired")}
                  </p>
                )}
                {approvalError && (
                  <p role="alert" className="text-sm text-destructive">
                    {approvalError}
                  </p>
                )}
                <Button
                  variant="outline"
                  disabled={approvalBusy || !recipientsValid}
                  onClick={() => void refreshApprovalPreview()}
                >
                  {t("approvalEmail.refreshPreview")}
                </Button>
              </div>
              <p className="break-words text-sm">
                <strong>{t("contractReminder.subject")}: </strong>
                {approvalPreview.subject}
              </p>
              <iframe
                title={t("approvalEmail.preview")}
                srcDoc={approvalPreview.body}
                sandbox=""
                className="h-96 w-full rounded border bg-card"
              />
            </div>
          )}
          <DialogFooter>
            <Button
              variant="outline"
              disabled={approvalBusy}
              onClick={() => setApprovalPreview(null)}
            >
              {t("common.cancel")}
            </Button>
            <Button
              disabled={approvalBusy || approvalDirty || !recipientsValid}
              onClick={() => void confirmApproval()}
            >
              {t("approvalEmail.confirm")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <PriceOfferDialog
        open={priceOpen}
        onOpenChange={(value) => {
          setPriceOpen(value);
          if (!value) resetPriceForm();
        }}
        editing={priceEditing !== null}
        form={priceForm}
        setForm={setPriceForm}
        vendors={options.vendors}
        saving={saving}
        onSave={() => void savePriceOffer()}
        t={t}
      />

      <Dialog open={taskOpen} onOpenChange={setTaskOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {t(taskEditing ? "workPackages.task.edit" : "workPackages.task.create")}
            </DialogTitle>
            <DialogDescription>{t("workPackages.task.help")}</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            {taskEditing?.parentTaskId && (
              <p className="text-sm text-muted-foreground sm:col-span-2">
                {t("workPackages.field.parentTask")}:{" "}
                {detail?.tasks.find((task) => task.id === taskEditing.parentTaskId)?.title}
              </p>
            )}
            {!taskEditing && (detail?.tasks.length ?? 0) > 0 && (
              <Field label={t("workPackages.field.parentTask")} className="sm:col-span-2">
                <Select
                  value={taskForm.parentTaskId?.toString() ?? "none"}
                  onValueChange={(value) =>
                    setTaskForm((current) => ({
                      ...current,
                      parentTaskId: value === "none" ? null : Number(value),
                      wbsCode: suggestTaskWbsCode(
                        detail?.tasks ?? [],
                        value === "none" ? null : Number(value),
                      ),
                    }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">{t("workPackages.parent.none")}</SelectItem>
                    {detail?.tasks
                      .filter(
                        (task) =>
                          !task.kpiDefinitionId && ["Planned", "InProgress"].includes(task.status),
                      )
                      .map((task) => (
                        <SelectItem key={task.id} value={String(task.id)}>
                          {task.wbsCode} · {task.title}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
              </Field>
            )}
            <Field label={t("workPackages.field.wbs")}>
              <Input
                required
                maxLength={120}
                value={taskForm.wbsCode}
                aria-invalid={taskWbsDuplicate}
                aria-describedby="work-task-wbs-help"
                onChange={(event) =>
                  setTaskForm((current) => ({ ...current, wbsCode: event.target.value }))
                }
              />
              <p
                id="work-task-wbs-help"
                className={cn(
                  "text-xs",
                  taskWbsDuplicate ? "text-destructive" : "text-muted-foreground",
                )}
              >
                {t(
                  taskWbsDuplicate
                    ? "workPackages.validation.wbsDuplicate"
                    : "workPackages.field.wbsHelp",
                )}
              </p>
            </Field>
            <Field label={t("workPackages.field.taskName")}>
              <Input
                maxLength={300}
                value={taskForm.title}
                onChange={(event) =>
                  setTaskForm((current) => ({ ...current, title: event.target.value }))
                }
              />
            </Field>
            {projectSchedule?.publishedRevisionId && (
              <Field label={t("operationalProjects.schedule.milestone")} className="sm:col-span-2">
                <Select
                  value={taskForm.projectScheduleNodeId ?? ""}
                  onValueChange={(value) =>
                    setTaskForm((current) => ({ ...current, projectScheduleNodeId: value }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue placeholder={t("operationalProjects.schedule.chooseMilestone")} />
                  </SelectTrigger>
                  <SelectContent>
                    {projectSchedule.revisions
                      .find((revision) => revision.id === projectSchedule.publishedRevisionId)
                      ?.items.map((item) => (
                        <SelectItem key={item.nodeId} value={item.nodeId}>
                          {item.wbsCode} · {item.title} ({item.plannedStart} – {item.plannedEnd})
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
              </Field>
            )}
            {detail?.kind === "Internal" && (
              <Field label={t("workPackages.field.department")}>
                <Select
                  value={taskForm.departmentRoleGroupId?.toString() ?? ""}
                  onValueChange={(value) =>
                    setTaskForm((current) => ({
                      ...current,
                      departmentRoleGroupId: Number(value),
                      assigneeUserId: 0,
                      approverUserId: null,
                      ccUserIds: [],
                    }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {detail.participantDepartments.map((group) => (
                      <SelectItem key={group.id} value={String(group.id)}>
                        {t(group.labelKey)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
            )}
            <Field label={t("workPackages.field.assignee")}>
              <SearchableSelect
                value={taskForm.assigneeUserId ? String(taskForm.assigneeUserId) : ""}
                onChange={(value) =>
                  setTaskForm((current) => ({
                    ...current,
                    assigneeUserId: Number(value),
                    ccUserIds: current.ccUserIds.filter((id) => id !== Number(value)),
                    approverUserId:
                      current.approverUserId === Number(value) ? null : current.approverUserId,
                  }))
                }
                ariaLabel={t("workPackages.field.assignee")}
                options={taskUsers.map((user) => personOption(user, t))}
              />
            </Field>
            <Field
              label={t(
                detail?.purpose === "TenderPlanning"
                  ? "workPackages.field.independentApprover"
                  : "workPackages.field.approver",
              )}
            >
              <SearchableSelect
                value={taskForm.approverUserId?.toString() ?? "lead"}
                onChange={(value) =>
                  setTaskForm((current) => ({
                    ...current,
                    approverUserId: value === "lead" ? null : Number(value),
                  }))
                }
                ariaLabel={t("workPackages.field.approver")}
                options={[
                  ...(detail?.leadUserId !== taskForm.assigneeUserId
                    ? [{ value: "lead", label: t("workPackages.approver.lead") }]
                    : []),
                  ...taskUsers
                    .filter((user) => user.userId !== taskForm.assigneeUserId)
                    .map((user) => personOption(user, t)),
                ]}
              />
            </Field>
            <Field label={t("workPackages.field.start")}>
              <Input
                type="date"
                value={taskForm.plannedStart}
                onChange={(event) =>
                  setTaskForm((current) => ({ ...current, plannedStart: event.target.value }))
                }
              />
            </Field>
            <Field label={t("workPackages.field.end")}>
              <Input
                type="date"
                value={taskForm.plannedEnd}
                onChange={(event) =>
                  setTaskForm((current) => ({ ...current, plannedEnd: event.target.value }))
                }
              />
            </Field>
            <Field label={t("workPackages.field.kpi")} className="sm:col-span-2">
              <Select
                value={taskForm.kpiDefinitionId?.toString() ?? "none"}
                onValueChange={(value) =>
                  setTaskForm((current) => ({
                    ...current,
                    kpiDefinitionId: value === "none" ? null : Number(value),
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">{t("workPackages.kpi.none")}</SelectItem>
                  {options.kpiDefinitions.map((definition) => (
                    <SelectItem key={definition.id} value={String(definition.id)}>
                      {t(definition.nameKey)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
            <Field label={t("workPackages.field.predecessors")} className="sm:col-span-2">
              <ChoiceGrid
                emptyLabel={
                  taskEditing
                    ? t("workPackages.predecessors.noneEligible")
                    : t("workPackages.predecessors.none")
                }
                items={(detail?.tasks ?? [])
                  .filter((task) => !unavailablePredecessorIds.has(task.id))
                  .map((task) => ({ id: task.id, label: `${task.wbsCode} · ${task.title}` }))}
                selected={taskForm.predecessorTaskIds}
                onChange={(predecessorTaskIds) =>
                  setTaskForm((current) => ({ ...current, predecessorTaskIds }))
                }
              />
              {taskEditing && (
                <p className="text-sm text-muted-foreground">
                  {t("workPackages.predecessors.cycleHelp")}
                </p>
              )}
            </Field>
            <Field label={t("workPackages.field.cc")} className="sm:col-span-2">
              <PeopleChecklist
                people={taskUsers.filter((user) => user.userId !== taskForm.assigneeUserId)}
                selected={taskForm.ccUserIds}
                onChange={(ccUserIds) => setTaskForm((current) => ({ ...current, ccUserIds }))}
              />
            </Field>
            <Field label={t("workPackages.field.description")} className="sm:col-span-2">
              <Textarea
                maxLength={4000}
                value={taskForm.description ?? ""}
                onChange={(event) =>
                  setTaskForm((current) => ({ ...current, description: event.target.value }))
                }
              />
            </Field>
            <Field label={t("workPackages.field.resources")} className="sm:col-span-2">
              <Textarea
                maxLength={2000}
                value={taskForm.resources ?? ""}
                onChange={(event) =>
                  setTaskForm((current) => ({ ...current, resources: event.target.value }))
                }
              />
            </Field>
            <Field
              label={t("workPackages.attachments.addFiles")}
              htmlFor="work-task-files"
              className="sm:col-span-2"
            >
              <MultiFilePicker
                id="work-task-files"
                files={taskFiles}
                onChange={(files) => selectTaskFiles(files, "form")}
                accept={TASK_FILE_EXTENSIONS.join(",")}
                maxFiles={10}
                hint={t("workPackages.attachments.help")}
                disabled={saving}
                testId="work-task-files"
              />
            </Field>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setTaskOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button disabled={saving} onClick={() => void saveTask()}>
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={attachmentsOpen} onOpenChange={setAttachmentsOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>{t("workPackages.attachments.title")}</DialogTitle>
            <DialogDescription>
              {attachmentTask?.wbsCode} · {attachmentTask?.title}
            </DialogDescription>
          </DialogHeader>
          {attachmentError && (
            <p role="alert" className="break-words text-sm text-destructive">
              {attachmentError}
            </p>
          )}
          {attachmentBusy && attachments.length === 0 ? (
            <p className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              {t("common.loading")}
            </p>
          ) : attachments.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("workPackages.attachments.empty")}</p>
          ) : (
            <ul className="divide-y rounded-md border">
              {attachments.map((attachment) => (
                <li
                  key={attachment.id}
                  className="flex flex-wrap items-center justify-between gap-2 p-3"
                >
                  <span className="min-w-0 break-all text-sm">{attachment.originalFileName}</span>
                  <div className="flex gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={attachmentBusy}
                      onClick={() => void downloadAttachment(attachment)}
                    >
                      <Download className="mr-1.5 h-4 w-4" />
                      {t("workPackages.attachments.download")}
                    </Button>
                    {detail &&
                      attachmentTask &&
                      (detail.canAssignTasks ||
                        (detail.status !== "Completed" &&
                          detail.status !== "Cancelled" &&
                          attachmentTask.status !== "Cancelled" &&
                          attachmentTask.assigneeUserId === currentUserId)) && (
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={attachmentBusy}
                          onClick={() => void deleteAttachment(attachment)}
                        >
                          <Trash2 className="mr-1.5 h-4 w-4" />
                          {t("common.delete")}
                        </Button>
                      )}
                  </div>
                </li>
              ))}
            </ul>
          )}
          {detail &&
            attachmentTask &&
            detail.status !== "Completed" &&
            detail.status !== "Cancelled" &&
            attachmentTask.status !== "Cancelled" &&
            (detail.canAssignTasks || attachmentTask.assigneeUserId === currentUserId) && (
              <div className="space-y-2">
                <Label htmlFor="work-task-attachments">
                  {t("workPackages.attachments.addFiles")}
                </Label>
                <MultiFilePicker
                  id="work-task-attachments"
                  files={attachmentFiles}
                  onChange={(files) => selectTaskFiles(files, "attachments")}
                  accept={TASK_FILE_EXTENSIONS.join(",")}
                  maxFiles={10}
                  hint={t("workPackages.attachments.help")}
                  disabled={attachmentBusy}
                  testId="work-task-attachments"
                />
                <Button
                  disabled={attachmentBusy || attachmentFiles.length === 0}
                  onClick={() => void addAttachments()}
                >
                  <Upload className="mr-1.5 h-4 w-4" />
                  {t("workPackages.attachments.upload")}
                </Button>
              </div>
            )}
        </DialogContent>
      </Dialog>

      <Dialog open={progressOpen} onOpenChange={setProgressOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t("workPackages.progress.title")}</DialogTitle>
            <DialogDescription>
              {progressTaskEditing?.wbsCode} · {progressTaskEditing?.title}
            </DialogDescription>
          </DialogHeader>
          <Field label={t("workPackages.progress.percent")}>
            <Input
              type="number"
              inputMode="numeric"
              min={1}
              max={99}
              value={progressPercent}
              onChange={(event) => setProgressPercent(Number(event.target.value))}
            />
            <p className="text-xs text-muted-foreground">{t("workPackages.progress.help")}</p>
          </Field>
          <DialogFooter>
            <Button variant="outline" onClick={() => setProgressOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button
              disabled={!progressTaskEditing || progressPercent < 1 || progressPercent > 99}
              onClick={() =>
                progressTaskEditing &&
                void progressTask(progressTaskEditing, false, progressPercent)
              }
            >
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={rejectTaskEditing !== null}
        onOpenChange={(open) => {
          if (!open) {
            setRejectTaskEditing(null);
            setRejectReason("");
          }
        }}
      >
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t("workPackages.reject.taskTitle")}</DialogTitle>
            <DialogDescription>
              {rejectTaskEditing?.wbsCode} · {rejectTaskEditing?.title}
            </DialogDescription>
          </DialogHeader>
          <Field label={t("workPackages.reject.reason")}>
            <Textarea
              maxLength={2000}
              value={rejectReason}
              onChange={(event) => setRejectReason(event.target.value)}
            />
          </Field>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejectTaskEditing(null)}>
              {t("common.cancel")}
            </Button>
            <Button
              variant="destructive"
              disabled={!rejectReason.trim()}
              onClick={() => void rejectTask()}
            >
              {t("common.reject")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

export default DepartmentWorkPackages;
