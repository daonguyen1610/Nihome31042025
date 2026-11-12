import { Link } from "react-router-dom";
import { useUiError } from "@/hooks/useUiError";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useAppSelector } from "@/store";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { Textarea } from "@/components/ui/textarea";
import { PageError, PageLoading } from "@/components/PageState";
import { ConstructionStatusBadge } from "@/pages/admin/construction/constructionUi";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import { usePermissions } from "@/hooks/usePermissions";
import { cn } from "@/lib/utils";
import { AlertTriangle, ChevronDown, ChevronRight, Diamond, Plus, Trash2 } from "lucide-react";
import {
  adminApi,
  type ProjectScheduleItemResponse,
  type ProjectScheduleResponse,
  type ProjectScheduleEmailPreviewResponse,
  type ProjectScheduleEmailReviewItem,
  type ProjectScheduleRevisionResponse,
  type SaveProjectScheduleItemRequest,
} from "@/services/adminApi";
import { useToast } from "@/hooks/use-toast";

interface ProjectMasterScheduleProps {
  projectId: number;
  projectStart?: string | null;
  projectEnd?: string | null;
  projectManagerUserId?: number | null;
}

interface DraftItem {
  nodeId: string;
  parentNodeId: string;
  wbsCode: string;
  title: string;
  plannedStart: string;
  plannedEnd: string;
  sortOrder: number;
}

interface ScheduleTreeRow {
  item: DraftItem;
  depth: number;
  hasChildren: boolean;
}

const DAY_MS = 86_400_000;

const makeDefaultItem = (start?: string | null, end?: string | null): DraftItem => ({
  nodeId: crypto.randomUUID(),
  parentNodeId: "",
  wbsCode: "1",
  title: "",
  plannedStart: start?.slice(0, 10) ?? localDateToday(),
  plannedEnd: end?.slice(0, 10) ?? localDateToday(),
  sortOrder: 0,
});

type ScheduleDiffStatus = "Added" | "Removed" | "Changed";

interface ScheduleDiffRow {
  key: string;
  status: ScheduleDiffStatus;
  wbsCode: string;
  title: string;
  detail?: string;
}

const SCHEDULE_STATUS_TONE = {
  Draft: "muted",
  PendingApproval: "info",
  Published: "success",
  Rejected: "danger",
} as const;

function localDateToday() {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
}

function dayNumber(value?: string | null): number | null {
  if (!value) return null;
  const [year, month, day] = value.slice(0, 10).split("-").map(Number);
  if (!year || !month || !day) return null;
  return Date.UTC(year, month - 1, day) / DAY_MS;
}

function durationDays(start?: string | null, end?: string | null): number | null {
  const startDay = dayNumber(start);
  const endDay = dayNumber(end);
  if (startDay == null || endDay == null) return null;
  return Math.max(1, endDay - startDay + 1);
}

const isMilestone = (item: { plannedStart: string; plannedEnd: string }) =>
  Boolean(item.plannedStart) && item.plannedStart === item.plannedEnd;

function wbsParts(code: string): Array<string | number> {
  return code
    .trim()
    .toLocaleLowerCase()
    .split(/[.\-_/]/)
    .filter(Boolean)
    .map((part) => (/^\d+$/.test(part) ? Number(part) : part));
}

/** 1, 2, 3, 5 — not the string order 1, 2, 3, 5. */
function compareWbsCode(left: string, right: string): number {
  const a = wbsParts(left);
  const b = wbsParts(right);
  for (let index = 0; index < Math.max(a.length, b.length); index += 1) {
    if (a[index] === undefined) return -1;
    if (b[index] === undefined) return 1;
    if (typeof a[index] === "number" && typeof b[index] === "number") {
      const difference = (a[index] as number) - (b[index] as number);
      if (difference !== 0) return difference;
      continue;
    }
    const leftPart = String(a[index]);
    const rightPart = String(b[index]);
    if (leftPart !== rightPart) {
      return leftPart.localeCompare(rightPart, undefined, { numeric: true });
    }
  }
  return 0;
}

function buildTreeRows(items: DraftItem[], collapsed: Set<string>): ScheduleTreeRow[] {
  const ids = new Set(items.map((item) => item.nodeId));
  const children = new Map<string, DraftItem[]>();
  for (const item of items) {
    const parent = item.parentNodeId && ids.has(item.parentNodeId) ? item.parentNodeId : "";
    const siblings = children.get(parent) ?? [];
    siblings.push(item);
    children.set(parent, siblings);
  }
  for (const siblings of children.values()) {
    siblings.sort((left, right) => compareWbsCode(left.wbsCode, right.wbsCode));
  }

  const rows: ScheduleTreeRow[] = [];
  const visited = new Set<string>();
  const visit = (parent: string, depth: number) => {
    for (const item of children.get(parent) ?? []) {
      if (visited.has(item.nodeId)) continue;
      visited.add(item.nodeId);
      const childCount = (children.get(item.nodeId) ?? []).length;
      rows.push({ item, depth, hasChildren: childCount > 0 });
      if (childCount > 0 && !collapsed.has(item.nodeId)) visit(item.nodeId, depth + 1);
    }
  };
  visit("", 0);

  // A corrupt parent cycle must not hide rows from the person fixing it.
  for (const item of items) {
    if (!visited.has(item.nodeId)) rows.push({ item, depth: 0, hasChildren: false });
  }
  return rows;
}

function collectDescendantIds(items: DraftItem[], nodeId: string): Set<string> {
  const children = new Map<string, string[]>();
  for (const item of items) {
    if (!item.parentNodeId) continue;
    const siblings = children.get(item.parentNodeId) ?? [];
    siblings.push(item.nodeId);
    children.set(item.parentNodeId, siblings);
  }
  const result = new Set<string>();
  const stack = [...(children.get(nodeId) ?? [])];
  while (stack.length > 0) {
    const id = stack.pop()!;
    if (result.has(id) || id === nodeId) continue;
    result.add(id);
    stack.push(...(children.get(id) ?? []));
  }
  return result;
}

function suggestWbsCode(items: DraftItem[], parentNodeId = "") {
  const parent = items.find((item) => item.nodeId === parentNodeId);
  const siblings = items.filter((item) => item.parentNodeId === parentNodeId);
  const prefix = parent ? `${parent.wbsCode}.` : "";
  const ordinals = siblings
    .map((item) => {
      const code = parent ? item.wbsCode.slice(prefix.length) : item.wbsCode;
      return /^\d+$/.test(code) ? Number(code) : 0;
    })
    .filter(Number.isSafeInteger);
  return `${prefix}${Math.max(0, ...ordinals) + 1}`;
}

const fromItem = (item: ProjectScheduleItemResponse): DraftItem => ({
  nodeId: item.nodeId,
  parentNodeId: item.parentNodeId ?? "",
  wbsCode: item.wbsCode,
  title: item.title,
  plannedStart: item.plannedStart,
  plannedEnd: item.plannedEnd,
  sortOrder: item.sortOrder,
});

export const ProjectMasterSchedule = ({
  projectId,
  projectStart,
  projectEnd,
  projectManagerUserId,
}: ProjectMasterScheduleProps) => {
  const uiError = useUiError();
  const { t, lang } = useI18n();
  const { has } = usePermissions();
  const { toast } = useToast();
  const dateFormat = useMemo(() => new Intl.DateTimeFormat(lang), [lang]);
  const monthLabel = useMemo(() => {
    const short = new Intl.DateTimeFormat(lang, { month: "short", timeZone: "UTC" });
    return (date: Date) => (lang === "vi" ? `T${date.getUTCMonth() + 1}` : short.format(date));
  }, [lang]);
  const formatScheduleDate = useCallback(
    (value?: string | null) => {
      if (!value) return "—";
      const date = new Date(`${value.slice(0, 10)}T00:00:00`);
      return Number.isNaN(date.getTime()) ? "—" : dateFormat.format(date);
    },
    [dateFormat],
  );
  const formatDuration = useCallback(
    (days: number | null) => {
      if (days == null) return "—";
      return t(days === 1 ? "scheduleUx.day" : "scheduleUx.days", { count: days });
    },
    [t],
  );
  const currentUserId = useAppSelector((state) => state.auth.user?.userId ?? null);
  const canManage =
    has(ADMIN_PERMS.operationalProjectsManage) && has(ADMIN_PERMS.opportunitiesManage);
  const [schedule, setSchedule] = useState<ProjectScheduleResponse | null>(null);
  const [revisionId, setRevisionId] = useState<number | null>(null);
  const [items, setItems] = useState<DraftItem[]>([]);
  const [changeReason, setChangeReason] = useState("");
  const [approverId, setApproverId] = useState("");
  const [reviewComment, setReviewComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [collapsed, setCollapsed] = useState<Set<string>>(() => new Set());
  const [editorOpen, setEditorOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<{
    nodeId: string;
    title: string;
    count: number;
  } | null>(null);
  // Switching away from an unsaved new revision would discard it, so the choice
  // waits here until the user confirms.
  const [pendingRevision, setPendingRevision] = useState<ProjectScheduleRevisionResponse | null>(
    null,
  );
  const errorRef = useRef<HTMLDivElement>(null);
  const [emailPreview, setEmailPreview] = useState<ProjectScheduleEmailPreviewResponse | null>(
    null,
  );
  const [previewIndex, setPreviewIndex] = useState(0);
  const [pendingEmailAction, setPendingEmailAction] = useState<
    ((previewToken: string) => Promise<void>) | null
  >(null);
  const [emailsNeedingReview, setEmailsNeedingReview] = useState<ProjectScheduleEmailReviewItem[]>(
    [],
  );
  const isProjectManager = currentUserId != null && currentUserId === projectManagerUserId;

  useEffect(() => {
    if (error) errorRef.current?.scrollIntoView({ block: "nearest" });
  }, [error]);

  const loadEmailsNeedingReview = useCallback(async () => {
    if (!isProjectManager) return;
    const response = await adminApi.getProjectScheduleEmailsForReview(projectId);
    setEmailsNeedingReview(response.data);
  }, [isProjectManager, projectId]);

  useEffect(() => {
    void loadEmailsNeedingReview().catch((reason: unknown) =>
      setError(uiError(reason, t("common.error"))),
    );
  }, [loadEmailsNeedingReview, t, uiError]);

  const reload = useCallback(async () => {
    const response = await adminApi.getProjectSchedule(projectId);
    setSchedule(response.data);
    const editableRevision = response.data.revisions.find(
      (item) => item.status === "Draft" || item.status === "Rejected",
    );
    const selectedRevision = editableRevision ?? response.data.revisions[0];
    setRevisionId(selectedRevision?.id ?? null);
    setItems(
      selectedRevision
        ? selectedRevision.items.map(fromItem)
        : [makeDefaultItem(projectStart, projectEnd)],
    );
    setChangeReason(selectedRevision?.changeReason ?? "");
    setApproverId(
      selectedRevision?.approverUserId?.toString() ??
        (response.data.approverOptions.length === 1
          ? String(response.data.approverOptions[0].userId)
          : ""),
    );
    setReviewComment("");
    setCollapsed(new Set());
    setEditorOpen(canManage && response.data.revisions.length === 0);
    setError(null);
  }, [canManage, projectEnd, projectId, projectStart]);

  useEffect(() => {
    void reload().catch((reason: unknown) => setError(uiError(reason, t("common.error"))));
  }, [reload, t, uiError]);

  const selected = useMemo(
    () => schedule?.revisions.find((item) => item.id === revisionId) ?? null,
    [revisionId, schedule],
  );
  const canEditSelected =
    !selected || selected.status === "Draft" || selected.status === "Rejected";
  const editableDraft = canManage && canEditSelected;
  const approverOptions = schedule?.approverOptions ?? [];
  const hasEditableRevision = Boolean(
    schedule?.revisions.some((revision) => revision.status !== "Published"),
  );
  const currentPublished =
    schedule?.revisions.find((revision) => revision.id === schedule.publishedRevisionId) ?? null;
  const isSuperseded =
    selected?.status === "Published" && selected.id !== schedule?.publishedRevisionId;
  const isDirty = useMemo(() => {
    if (!selected) return items.length > 0;
    const comparable = (value: DraftItem[]) =>
      value
        .map((item) => ({
          nodeId: item.nodeId,
          parentNodeId: item.parentNodeId,
          wbsCode: item.wbsCode.trim(),
          title: item.title.trim(),
          plannedStart: item.plannedStart,
          plannedEnd: item.plannedEnd,
          sortOrder: item.sortOrder,
        }))
        .sort((left, right) => left.nodeId.localeCompare(right.nodeId));
    return (
      changeReason.trim() !== (selected.changeReason ?? "").trim() ||
      JSON.stringify(comparable(items)) !== JSON.stringify(comparable(selected.items.map(fromItem)))
    );
  }, [changeReason, items, selected]);
  const showEditor = editorOpen && editableDraft;
  const revisionSelectDisabled = busy || (isDirty && selected != null);
  const nextVersion = useMemo(
    () =>
      (schedule?.revisions.reduce((max, revision) => Math.max(max, revision.version), 0) ?? 0) + 1,
    [schedule],
  );

  // Draft, rejected and new revisions all work on the same live copy. Published
  // and pending revisions are read from the server response.
  const viewItems = useMemo(
    () =>
      selected && selected.status !== "Draft" && selected.status !== "Rejected"
        ? selected.items.map(fromItem)
        : items,
    [items, selected],
  );
  const treeRows = useMemo(() => buildTreeRows(viewItems, collapsed), [collapsed, viewItems]);
  const editorRows = useMemo(() => buildTreeRows(items, collapsed), [collapsed, items]);
  const sortedItems = useMemo(
    () => [...items].sort((left, right) => compareWbsCode(left.wbsCode, right.wbsCode)),
    [items],
  );
  const descendantIds = useMemo(
    () =>
      new Map(
        items.map((item) => [item.nodeId, collectDescendantIds(items, item.nodeId)] as const),
      ),
    [items],
  );

  const scheduleBounds = useMemo(() => {
    const starts = viewItems
      .map((item) => item.plannedStart)
      .filter(Boolean)
      .sort();
    const ends = viewItems
      .map((item) => item.plannedEnd)
      .filter(Boolean)
      .sort();
    return {
      start: starts[0] ?? projectStart?.slice(0, 10) ?? null,
      end: ends[ends.length - 1] ?? projectEnd?.slice(0, 10) ?? null,
    };
  }, [projectEnd, projectStart, viewItems]);
  const totalDuration = durationDays(scheduleBounds.start, scheduleBounds.end);
  const milestoneCount = viewItems.filter(isMilestone).length;
  const outsideFrameCount = useMemo(() => {
    const frameStart = dayNumber(projectStart);
    const frameEnd = dayNumber(projectEnd);
    if (frameStart == null || frameEnd == null) return 0;
    return viewItems.filter((item) => {
      const start = dayNumber(item.plannedStart);
      const end = dayNumber(item.plannedEnd);
      return start == null || end == null || start < frameStart || end > frameEnd;
    }).length;
  }, [projectEnd, projectStart, viewItems]);

  const timeline = useMemo(() => {
    const starts = viewItems
      .map((item) => dayNumber(item.plannedStart))
      .filter((value): value is number => value != null);
    const ends = viewItems
      .map((item) => dayNumber(item.plannedEnd))
      .filter((value): value is number => value != null);
    const frameStart = dayNumber(projectStart);
    const frameEnd = dayNumber(projectEnd);
    if (frameStart != null) starts.push(frameStart);
    if (frameEnd != null) ends.push(frameEnd);
    if (starts.length === 0 || ends.length === 0) return null;

    const start = Math.min(...starts);
    const end = Math.max(...ends);
    const total = Math.max(1, end - start + 1);
    const months: Array<{
      key: string;
      left: number;
      width: number;
      label: string;
      /** The range starts or ends inside this month, so the column is narrower. */
      partial: boolean;
    }> = [];
    const first = new Date(start * DAY_MS);
    let year = first.getUTCFullYear();
    let month = first.getUTCMonth();
    while (true) {
      const monthStart = Date.UTC(year, month, 1) / DAY_MS;
      const nextMonthStart = Date.UTC(year, month + 1, 1) / DAY_MS;
      const visibleStart = Math.max(start, monthStart);
      const visibleEnd = Math.min(end + 1, nextMonthStart);
      if (visibleEnd > visibleStart) {
        const date = new Date(Date.UTC(year, month, 1));
        months.push({
          key: `${year}-${month}`,
          left: ((visibleStart - start) / total) * 100,
          width: ((visibleEnd - visibleStart) / total) * 100,
          label: monthLabel(date),
          partial: visibleStart > monthStart || visibleEnd < nextMonthStart,
        });
      }
      if (nextMonthStart > end) break;
      month += 1;
      if (month > 11) {
        month = 0;
        year += 1;
      }
    }

    // The year sits in its own band above the months so a month label never has
    // to carry four digits and get clipped in a narrow column.
    const years: Array<{ key: string; left: number; width: number; label: string }> = [];
    for (const month of months) {
      const yearKey = month.key.slice(0, month.key.indexOf("-"));
      const current = years[years.length - 1];
      if (current?.key === yearKey) {
        current.width = month.left + month.width - current.left;
      } else {
        years.push({ key: yearKey, left: month.left, width: month.width, label: yearKey });
      }
    }

    const today = dayNumber(localDateToday());
    return {
      start,
      total,
      months,
      years,
      // Every month is labelled while the axis has room for it. Longer spans
      // fall back to every second or third month so labels stay readable; each
      // label then spans the months it stands for instead of being clipped.
      labelEvery: months.length > 12 ? Math.ceil(months.length / 12) : 1,
      todayLeft:
        today != null && today >= start && today <= end
          ? ((today - start + 0.5) / total) * 100
          : null,
      frameLeft: frameStart != null ? ((frameStart - start) / total) * 100 : null,
      frameWidth:
        frameStart != null && frameEnd != null ? ((frameEnd - frameStart + 1) / total) * 100 : null,
    };
  }, [monthLabel, projectEnd, projectStart, viewItems]);

  const barStyle = useCallback(
    (item: DraftItem) => {
      if (!timeline) return null;
      const start = dayNumber(item.plannedStart);
      const end = dayNumber(item.plannedEnd);
      if (start == null || end == null) return null;
      return {
        left: `${((start - timeline.start) / timeline.total) * 100}%`,
        width: `${Math.max(((end - start + 1) / timeline.total) * 100, 0.6)}%`,
      };
    },
    [timeline],
  );
  const milestoneStyle = useCallback(
    (item: DraftItem) => {
      if (!timeline) return null;
      const start = dayNumber(item.plannedStart);
      if (start == null) return null;
      return { left: `${((start - timeline.start + 0.5) / timeline.total) * 100}%` };
    },
    [timeline],
  );

  const diffBaseline = useMemo(() => {
    const published = (schedule?.revisions ?? [])
      .filter((revision) => revision.status === "Published")
      .sort((left, right) => right.version - left.version);
    if (selected?.status === "Published") {
      return published.find((revision) => revision.version < selected.version) ?? null;
    }
    return published[0] ?? null;
  }, [schedule, selected]);

  const diffRows = useMemo<ScheduleDiffRow[]>(() => {
    if (!diffBaseline) return [];
    const currentItems =
      revisionId == null || selected?.status === "Draft" || selected?.status === "Rejected"
        ? items
        : (selected?.items.map(fromItem) ?? []);
    const baselineItems = diffBaseline.items.map(fromItem);
    const baselineById = new Map(baselineItems.map((item) => [item.nodeId, item]));
    const currentById = new Map(currentItems.map((item) => [item.nodeId, item]));
    const rows: ScheduleDiffRow[] = [];

    for (const current of currentItems) {
      const previous = baselineById.get(current.nodeId);
      if (!previous) {
        rows.push({
          key: `added-${current.nodeId}`,
          status: "Added",
          wbsCode: current.wbsCode,
          title: current.title,
        });
        continue;
      }

      const details: string[] = [];
      if (previous.wbsCode.trim() !== current.wbsCode.trim()) {
        details.push(`${t("scheduleUx.wbs")}: ${previous.wbsCode} → ${current.wbsCode}`);
      }
      if (previous.title.trim() !== current.title.trim()) {
        details.push(`${t("scheduleUx.workItem")}: ${previous.title} → ${current.title}`);
      }
      if (previous.parentNodeId !== current.parentNodeId) {
        const previousParent = previous.parentNodeId
          ? (baselineById.get(previous.parentNodeId)?.wbsCode ?? "—")
          : t("scheduleUx.rootItem");
        const currentParent = current.parentNodeId
          ? (currentById.get(current.parentNodeId)?.wbsCode ?? "—")
          : t("scheduleUx.rootItem");
        details.push(`${t("scheduleUx.parentItem")}: ${previousParent} → ${currentParent}`);
      }
      if (previous.plannedStart !== current.plannedStart) {
        details.push(
          `${t("operationalProjects.schedule.start")}: ${formatScheduleDate(previous.plannedStart)} → ${formatScheduleDate(current.plannedStart)}`,
        );
      }
      if (previous.plannedEnd !== current.plannedEnd) {
        details.push(
          `${t("operationalProjects.schedule.end")}: ${formatScheduleDate(previous.plannedEnd)} → ${formatScheduleDate(current.plannedEnd)}`,
        );
      }
      if (details.length > 0) {
        rows.push({
          key: `changed-${current.nodeId}`,
          status: "Changed",
          wbsCode: current.wbsCode,
          title: current.title,
          detail: details.join(" · "),
        });
      }
    }

    for (const previous of baselineItems) {
      if (!currentById.has(previous.nodeId)) {
        rows.push({
          key: `removed-${previous.nodeId}`,
          status: "Removed",
          wbsCode: previous.wbsCode,
          title: previous.title,
        });
      }
    }
    return rows;
  }, [diffBaseline, formatScheduleDate, items, revisionId, selected, t]);

  const savePayload = (
    rowVersion?: string | null,
  ): {
    changeReason: string;
    items: SaveProjectScheduleItemRequest[];
    rowVersion?: string | null;
  } => ({
    changeReason: changeReason.trim(),
    rowVersion: rowVersion ?? null,
    items: items.map((item) => ({
      nodeId: item.nodeId,
      parentNodeId: item.parentNodeId || null,
      wbsCode: item.wbsCode.trim(),
      title: item.title.trim(),
      plannedStart: item.plannedStart,
      plannedEnd: item.plannedEnd,
      sortOrder: item.sortOrder,
    })),
  });

  const validateSchedule = (): string | null => {
    if (items.length === 0) return t("operationalProjects.schedule.validation.atLeastOneItem");
    if (items.length > 500) return t("operationalProjects.schedule.validation.tooManyItems");

    const codes = new Set<string>();
    const byId = new Map(items.map((item) => [item.nodeId, item]));
    for (const [index, item] of items.entries()) {
      const code = item.wbsCode.trim();
      if (!code)
        return t("operationalProjects.schedule.validation.wbsRequired", { index: index + 1 });
      if (code.length > 80)
        return t("operationalProjects.schedule.validation.wbsTooLong", { index: index + 1 });
      const normalizedCode = code.toLocaleLowerCase();
      if (codes.has(normalizedCode))
        return t("operationalProjects.schedule.validation.wbsDuplicate", { code });
      codes.add(normalizedCode);

      const title = item.title.trim();
      if (!title)
        return t("operationalProjects.schedule.validation.titleRequired", { index: index + 1 });
      if (title.length > 300)
        return t("operationalProjects.schedule.validation.titleTooLong", { index: index + 1 });
      if (!item.plannedStart)
        return t("operationalProjects.schedule.validation.startRequired", { index: index + 1 });
      if (!item.plannedEnd)
        return t("operationalProjects.schedule.validation.endRequired", { index: index + 1 });
      if (item.plannedEnd < item.plannedStart)
        return t("operationalProjects.schedule.validation.dateOrder", { index: index + 1 });
    }

    for (const [index, item] of items.entries()) {
      if (!item.parentNodeId) continue;
      const parent = byId.get(item.parentNodeId);
      if (!parent || parent.nodeId === item.nodeId)
        return t("operationalProjects.schedule.validation.parentInvalid", { index: index + 1 });
      if (item.plannedStart < parent.plannedStart || item.plannedEnd > parent.plannedEnd)
        return t("operationalProjects.schedule.validation.parentDates", { index: index + 1 });
    }

    for (const item of items) {
      const visited = new Set<string>();
      let current: DraftItem | undefined = item;
      while (current?.parentNodeId) {
        if (visited.has(current.nodeId))
          return t("operationalProjects.schedule.validation.parentCycle");
        visited.add(current.nodeId);
        current = byId.get(current.parentNodeId);
      }
    }
    return null;
  };

  const startRevision = async () => {
    if (!schedule) return;
    const published = schedule.revisions.find((item) => item.id === schedule.publishedRevisionId);
    setItems(
      published ? published.items.map(fromItem) : [makeDefaultItem(projectStart, projectEnd)],
    );
    setChangeReason("");
    const previousApprover = published?.approverUserId;
    const previousApproverEligible =
      previousApprover != null &&
      schedule.approverOptions.some((option) => option.userId === previousApprover);
    setApproverId(
      previousApproverEligible
        ? String(previousApprover)
        : schedule.approverOptions.length === 1
          ? String(schedule.approverOptions[0].userId)
          : "",
    );
    setRevisionId(null);
    setCollapsed(new Set());
    setEditorOpen(true);
    setError(null);
  };

  const selectRevision = (revision: ProjectScheduleRevisionResponse) => {
    setRevisionId(revision.id);
    setItems(revision.items.map(fromItem));
    setChangeReason(revision.changeReason ?? "");
    setApproverId(
      revision.approverUserId?.toString() ??
        (approverOptions.length === 1 ? String(approverOptions[0].userId) : ""),
    );
    setCollapsed(new Set());
    setEditorOpen(false);
    setError(null);
  };

  const persist = async (): Promise<ProjectScheduleRevisionResponse | null> => {
    const validationError = validateSchedule();
    if (validationError) {
      setError(validationError);
      return null;
    }
    setBusy(true);
    setError(null);
    try {
      const response =
        revisionId == null
          ? await adminApi.createProjectScheduleRevision(projectId, savePayload())
          : await adminApi.updateProjectScheduleRevision(
              projectId,
              revisionId,
              savePayload(schedule?.revisions.find((item) => item.id === revisionId)?.rowVersion),
            );
      setSchedule(response.data);
      const saved =
        response.data.revisions.find((item) => item.id === revisionId) ??
        response.data.revisions.find((item) => item.status === "Draft") ??
        response.data.revisions[0] ??
        null;
      setRevisionId(saved?.id ?? null);
      setItems(saved ? saved.items.map(fromItem) : []);
      setChangeReason(saved?.changeReason ?? "");
      const savedApprover = saved?.approverUserId?.toString();
      const fallbackApprover =
        response.data.approverOptions.length === 1
          ? String(response.data.approverOptions[0].userId)
          : "";
      setApproverId((current) => savedApprover || current || fallbackApprover);
      return saved;
    } catch (reason) {
      setError(uiError(reason, t("common.error")));
      return null;
    } finally {
      setBusy(false);
    }
  };

  const save = async () => {
    const saved = await persist();
    if (saved) setToastSuccess("operationalProjects.schedule.saved");
  };

  const sendForApproval = async (target: ProjectScheduleRevisionResponse, previewToken: string) => {
    setBusy(true);
    setError(null);
    try {
      const response = await adminApi.submitProjectScheduleRevision(
        projectId,
        target.id,
        Number(approverId),
        target.rowVersion,
        previewToken,
      );
      setSchedule(response.data);
      setRevisionId(target.id);
      setEditorOpen(false);
      toast({ title: t("operationalProjects.schedule.submitted") });
    } catch (reason) {
      setError(uiError(reason, t("common.error")));
    } finally {
      setBusy(false);
    }
  };

  const showEmailPreview = async (
    target: ProjectScheduleRevisionResponse,
    action: "Submit" | "Publish",
    next: (previewToken: string) => Promise<void>,
  ) => {
    setBusy(true);
    setError(null);
    try {
      const response = await adminApi.previewProjectScheduleForAction(
        projectId,
        target.id,
        action,
        action === "Submit" ? Number(approverId) : undefined,
      );
      setEmailPreview(response.data);
      setPreviewIndex(0);
      setPendingEmailAction(() => next);
    } catch (reason) {
      setError(uiError(reason, t("common.error")));
    } finally {
      setBusy(false);
    }
  };

  const submit = async () => {
    if (!approverId || !canEditSelected) return;
    const version = selected?.version ?? nextVersion;
    if (version > 1 && !changeReason.trim()) {
      setError(t("scheduleUx.changeReasonRequired"));
      setEditorOpen(true);
      return;
    }
    // Saving a rejected revision puts it back into Draft on the server. Unsaved
    // edits and the first revision also need a save before the preview.
    const needsPersist =
      !selected || revisionId == null || isDirty || selected.status === "Rejected";
    const target = needsPersist ? await persist() : selected;
    if (!target || target.status !== "Draft") return;
    await showEmailPreview(target, "Submit", (token) => sendForApproval(target, token));
  };

  const reassign = async () => {
    if (!selected || selected.status !== "PendingApproval" || !approverId) return;
    await showEmailPreview(selected, "Submit", (token) => sendForApproval(selected, token));
  };

  const review = async (decision: "Approve" | "Reject") => {
    if (!selected) return;
    if (decision === "Approve") {
      await showEmailPreview(selected, "Publish", (token) =>
        performReview(selected, decision, token),
      );
      return;
    }
    await performReview(selected, decision);
  };

  const performReview = async (
    target: ProjectScheduleRevisionResponse,
    decision: "Approve" | "Reject",
    previewToken?: string,
  ) => {
    setBusy(true);
    setError(null);
    try {
      const response = await adminApi.reviewProjectScheduleRevision(
        projectId,
        target.id,
        decision,
        reviewComment,
        target.rowVersion,
        previewToken,
      );
      setSchedule(response.data);
      setRevisionId(target.id);
      setEditorOpen(false);
      setReviewComment("");
      toast({
        title: t(
          decision === "Approve"
            ? "operationalProjects.schedule.published"
            : "operationalProjects.schedule.rejected",
        ),
      });
    } catch (reason) {
      setError(uiError(reason, t("common.error")));
    } finally {
      setBusy(false);
    }
  };

  const resolveEmail = async (emailId: number, decision: "ConfirmReceived" | "Retry") => {
    setBusy(true);
    setError(null);
    try {
      await adminApi.resolveProjectScheduleEmail(projectId, emailId, decision);
      await loadEmailsNeedingReview();
      toast({ title: t(`operationalProjects.schedule.email.${decision}Done`) });
    } catch (reason) {
      setError(uiError(reason, t("common.error")));
    } finally {
      setBusy(false);
    }
  };

  const setToastSuccess = (key: string) => toast({ title: t(key) });
  const displayTitle = (item: DraftItem) => item.title.trim() || t("scheduleUx.untitled");
  const toggleCollapsed = (nodeId: string) =>
    setCollapsed((current) => {
      const next = new Set(current);
      if (next.has(nodeId)) next.delete(nodeId);
      else next.add(nodeId);
      return next;
    });
  const patchItem = (nodeId: string, change: Partial<DraftItem>) =>
    setItems((current) =>
      current.map((item) => {
        if (item.nodeId !== nodeId) return item;
        const updated = { ...item, ...change };
        if (change.parentNodeId !== undefined && change.parentNodeId !== item.parentNodeId) {
          updated.wbsCode = suggestWbsCode(current, change.parentNodeId);
        }
        return updated;
      }),
    );
  const addChild = (parent: DraftItem) => {
    setItems((current) => {
      const child = makeDefaultItem(projectStart, projectEnd);
      child.parentNodeId = parent.nodeId;
      child.wbsCode = suggestWbsCode(current, parent.nodeId);
      child.plannedStart = parent.plannedStart;
      child.plannedEnd = parent.plannedEnd;
      return [...current, child];
    });
    setCollapsed((current) => {
      if (!current.has(parent.nodeId)) return current;
      const next = new Set(current);
      next.delete(parent.nodeId);
      return next;
    });
  };
  const addRootItem = () =>
    setItems((current) => {
      const item = makeDefaultItem(projectStart, projectEnd);
      item.wbsCode = suggestWbsCode(current);
      return [...current, item];
    });
  const requestDelete = (item: DraftItem) => {
    const descendants = collectDescendantIds(items, item.nodeId);
    if (descendants.size === 0) {
      setItems((current) => current.filter((entry) => entry.nodeId !== item.nodeId));
      return;
    }
    setDeleteTarget({
      nodeId: item.nodeId,
      title: displayTitle(item),
      count: descendants.size,
    });
  };
  const confirmDelete = () => {
    if (!deleteTarget) return;
    const target = deleteTarget;
    setItems((current) => {
      const descendants = collectDescendantIds(current, target.nodeId);
      return current.filter(
        (entry) => entry.nodeId !== target.nodeId && !descendants.has(entry.nodeId),
      );
    });
    setDeleteTarget(null);
  };

  const isApprover =
    selected?.status === "PendingApproval" && selected.approverUserId === currentUserId;
  const stateText = t(
    isSuperseded
      ? "scheduleUx.masterState.Superseded"
      : `scheduleUx.masterState.${selected?.status ?? "Draft"}`,
    isSuperseded ? { version: currentPublished?.version ?? "" } : undefined,
  );
  const approverSelectOptions = approverOptions.map((user) => ({
    value: String(user.userId),
    label: user.name || user.email || String(user.userId),
    hint: user.name && user.email ? user.email : undefined,
  }));
  const renderEditor = () =>
    showEditor ? (
      <section aria-labelledby="schedule-editor-heading" className="space-y-3 border-t pt-4">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div className="max-w-3xl space-y-1">
            <h3 id="schedule-editor-heading" className="text-sm font-semibold">
              {t("scheduleUx.editSchedule")}
            </h3>
            <p className="text-sm text-muted-foreground">{t("scheduleUx.masterFormGuide")}</p>
            <p className="text-xs text-muted-foreground">{t("scheduleUx.constructionOrderHint")}</p>
          </div>
          <Button variant="ghost" size="sm" onClick={() => setEditorOpen(false)}>
            {t("scheduleUx.hideEditor")}
          </Button>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="schedule-change-reason">
            {t("operationalProjects.schedule.changeReason")}
          </Label>
          <Textarea
            id="schedule-change-reason"
            value={changeReason}
            onChange={(event) => setChangeReason(event.target.value)}
            maxLength={2000}
            rows={3}
            aria-describedby="schedule-change-reason-hint"
          />
          <p id="schedule-change-reason-hint" className="text-xs text-muted-foreground">
            {t("scheduleUx.changeReasonHint")}
          </p>
        </div>
        <div className="-mx-4 overflow-x-auto border-y [contain:layout] sm:mx-0">
          <table className="w-full min-w-[900px] text-sm">
            <caption className="sr-only">{t("scheduleUx.editSchedule")}</caption>
            <thead>
              <tr className="border-b text-left text-xs text-muted-foreground">
                <th scope="col" className="py-2 pl-3 pr-2 font-medium">
                  {t("scheduleUx.wbs")}
                </th>
                <th scope="col" className="py-2 pl-8 pr-2 font-medium">
                  {t("scheduleUx.workItem")}
                </th>
                <th scope="col" className="py-2 pr-2 font-medium">
                  {t("scheduleUx.parentItem")}
                </th>
                <th scope="col" className="py-2 pr-2 font-medium">
                  {t("operationalProjects.schedule.start")}
                </th>
                <th scope="col" className="py-2 pr-2 font-medium">
                  {t("operationalProjects.schedule.end")}
                </th>
                <th scope="col" className="whitespace-nowrap py-2 pr-2 font-medium">
                  {t("scheduleUx.duration")}
                </th>
                <th scope="col" className="py-2 pl-2 text-right font-medium">
                  <span className="sr-only">{t("common.edit")}</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {editorRows.map((row) => {
                const item = row.item;
                const blocked = descendantIds.get(item.nodeId);
                const parentCandidates = sortedItems.filter(
                  (parent) => parent.nodeId !== item.nodeId && !blocked?.has(parent.nodeId),
                );
                const selectedParent = item.parentNodeId
                  ? items.find((parent) => parent.nodeId === item.parentNodeId)
                  : null;
                const duration = durationDays(item.plannedStart, item.plannedEnd);
                return (
                  <tr
                    key={item.nodeId}
                    className={cn(
                      "border-b align-top last:border-0",
                      row.depth === 0 && "bg-muted/20",
                    )}
                  >
                    <td className="py-2 pl-3 pr-2">
                      <div className="space-y-1">
                        <Label htmlFor={`schedule-wbs-${item.nodeId}`} className="sr-only">
                          {t("scheduleUx.wbsCode")}
                        </Label>
                        <Input
                          id={`schedule-wbs-${item.nodeId}`}
                          value={item.wbsCode}
                          onChange={(event) =>
                            patchItem(item.nodeId, { wbsCode: event.target.value })
                          }
                          maxLength={80}
                          className="w-24"
                        />
                      </div>
                    </td>
                    <td className="py-2 pr-2">
                      <div className="space-y-1">
                        <Label htmlFor={`schedule-title-${item.nodeId}`} className="sr-only">
                          {t("scheduleUx.workItem")}
                        </Label>
                        <div
                          className="flex items-start gap-1"
                          style={{ paddingLeft: `${row.depth * 16}px` }}
                        >
                          <span className="mt-1 flex h-7 w-7 shrink-0 items-center justify-center">
                            {row.hasChildren ? (
                              <button
                                type="button"
                                onClick={() => toggleCollapsed(item.nodeId)}
                                aria-expanded={!collapsed.has(item.nodeId)}
                                aria-label={t(
                                  collapsed.has(item.nodeId)
                                    ? "scheduleUx.expandItem"
                                    : "scheduleUx.collapseItem",
                                  { name: displayTitle(item) },
                                )}
                                className="inline-flex h-7 w-7 shrink-0 items-center justify-center rounded text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                              >
                                {collapsed.has(item.nodeId) ? (
                                  <ChevronRight className="h-4 w-4" />
                                ) : (
                                  <ChevronDown className="h-4 w-4" />
                                )}
                              </button>
                            ) : null}
                          </span>
                          <Input
                            id={`schedule-title-${item.nodeId}`}
                            value={item.title}
                            placeholder={t("scheduleUx.taskExample")}
                            onChange={(event) =>
                              patchItem(item.nodeId, { title: event.target.value })
                            }
                            maxLength={300}
                            className="min-w-[220px]"
                          />
                        </div>
                      </div>
                    </td>
                    <td className="py-2 pr-2">
                      <div className="space-y-1">
                        <Label htmlFor={`schedule-parent-${item.nodeId}`} className="sr-only">
                          {t("scheduleUx.parentItem")}
                        </Label>
                        <Select
                          value={item.parentNodeId || "none"}
                          onValueChange={(value) =>
                            patchItem(item.nodeId, {
                              parentNodeId: value === "none" ? "" : value,
                            })
                          }
                        >
                          <SelectTrigger
                            id={`schedule-parent-${item.nodeId}`}
                            className="min-w-[160px]"
                            title={
                              selectedParent
                                ? `${selectedParent.wbsCode} · ${displayTitle(selectedParent)}`
                                : t("scheduleUx.rootItem")
                            }
                          >
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="none">{t("scheduleUx.rootItem")}</SelectItem>
                            {parentCandidates.map((parent) => (
                              <SelectItem key={parent.nodeId} value={parent.nodeId}>
                                {parent.wbsCode} · {displayTitle(parent)}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                    </td>
                    <td className="py-2 pr-2">
                      <div className="space-y-1">
                        <Label htmlFor={`schedule-start-${item.nodeId}`} className="sr-only">
                          {t("operationalProjects.schedule.start")}
                        </Label>
                        <Input
                          id={`schedule-start-${item.nodeId}`}
                          type="date"
                          value={item.plannedStart}
                          onChange={(event) =>
                            patchItem(item.nodeId, { plannedStart: event.target.value })
                          }
                          className="min-w-[140px]"
                        />
                      </div>
                    </td>
                    <td className="py-2 pr-2">
                      <div className="space-y-1">
                        <Label htmlFor={`schedule-end-${item.nodeId}`} className="sr-only">
                          {t("operationalProjects.schedule.end")}
                        </Label>
                        <Input
                          id={`schedule-end-${item.nodeId}`}
                          type="date"
                          value={item.plannedEnd}
                          onChange={(event) =>
                            patchItem(item.nodeId, { plannedEnd: event.target.value })
                          }
                          className="min-w-[140px]"
                        />
                      </div>
                    </td>
                    <td className="whitespace-nowrap py-2 pr-2 align-middle text-xs tabular-nums text-muted-foreground">
                      {isMilestone(item) ? t("scheduleUx.milestone") : formatDuration(duration)}
                    </td>
                    <td className="py-2 pl-2 align-middle">
                      <div className="flex items-center justify-end gap-1">
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          className="h-8 w-8"
                          onClick={() => addChild(item)}
                          aria-label={`${t("scheduleUx.addChild")}: ${displayTitle(item)}`}
                          title={t("scheduleUx.addChild")}
                        >
                          <Plus className="h-4 w-4" />
                        </Button>
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          className="h-8 w-8 text-muted-foreground hover:text-destructive"
                          onClick={() => requestDelete(item)}
                          aria-label={`${t("common.delete")}: ${displayTitle(item)}`}
                          title={t("common.delete")}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={addRootItem}>
            {t("scheduleUx.addItem")}
          </Button>
          <Button
            variant="outline"
            onClick={() => void save()}
            disabled={busy || items.length === 0 || !isDirty}
          >
            {t("common.save")}
          </Button>
        </div>
      </section>
    ) : null;

  return (
    <section
      id="project-master-schedule"
      className="space-y-5 rounded-xl border bg-card p-4 sm:p-6"
    >
      <Dialog
        open={emailPreview !== null}
        onOpenChange={(open) => {
          if (!open) {
            setEmailPreview(null);
            setPendingEmailAction(null);
          }
        }}
      >
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>
              {emailPreview?.emails.length === 0
                ? t("operationalProjects.schedule.email.noRecipientsTitle")
                : t("operationalProjects.schedule.email.previewTitle")}
            </DialogTitle>
          </DialogHeader>
          {emailPreview && emailPreview.emails.length > 0 && (
            <p className="text-sm text-muted-foreground">
              {t("operationalProjects.schedule.email.previewGuidance")}
            </p>
          )}
          {emailPreview?.emails.length ? (
            <>
              <Label htmlFor="schedule-email-recipient">
                {t("operationalProjects.schedule.email.recipient")}
              </Label>
              <Select
                value={String(previewIndex)}
                onValueChange={(value) => setPreviewIndex(Number(value))}
              >
                <SelectTrigger id="schedule-email-recipient">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {emailPreview.emails.map((preview, index) => (
                    <SelectItem key={preview.recipientUserId} value={String(index)}>
                      {preview.toEmail}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-sm font-semibold">{emailPreview.emails[previewIndex]?.subject}</p>
              <iframe
                title={t("operationalProjects.schedule.email.previewTitle")}
                sandbox=""
                srcDoc={emailPreview.emails[previewIndex]?.body}
                className="h-80 w-full rounded border bg-card"
              />
            </>
          ) : (
            <p className="text-sm">{t("operationalProjects.schedule.email.noRecipients")}</p>
          )}
          <div className="flex justify-end gap-2">
            <Button
              variant="outline"
              onClick={() => {
                setEmailPreview(null);
                setPendingEmailAction(null);
              }}
            >
              {t("common.cancel")}
            </Button>
            <Button
              disabled={busy}
              onClick={() => {
                const action = pendingEmailAction;
                const token = emailPreview?.previewToken;
                setEmailPreview(null);
                setPendingEmailAction(null);
                if (action && token) void action(token);
              }}
            >
              {t("operationalProjects.schedule.email.continue")}
            </Button>
          </div>
        </DialogContent>
      </Dialog>

      <AlertDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => {
          if (!open) setDeleteTarget(null);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("scheduleUx.deleteParentTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {deleteTarget
                ? t("scheduleUx.deleteParentBody", {
                    title: deleteTarget.title,
                    count: deleteTarget.count,
                  })
                : ""}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              className={buttonVariants({ variant: "destructive" })}
              onClick={confirmDelete}
            >
              {t("common.delete")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog
        open={pendingRevision !== null}
        onOpenChange={(open) => {
          if (!open) setPendingRevision(null);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("scheduleUx.discardNewRevisionTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("scheduleUx.discardNewRevisionBody", { version: nextVersion })}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              className={buttonVariants({ variant: "destructive" })}
              onClick={() => {
                if (pendingRevision) selectRevision(pendingRevision);
                setPendingRevision(null);
              }}
            >
              {t("scheduleUx.discardNewRevisionAction")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <header className="flex flex-col gap-4 border-b pb-4 lg:grid lg:grid-cols-[minmax(0,1fr)_auto] lg:items-start lg:gap-6">
        <div className="min-w-0">
          <h2 className="text-lg font-semibold">{t("operationalProjects.schedule.title")}</h2>
          <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
            {t("scheduleUx.masterSubtitle")}
          </p>
          {selected && (
            <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
              <ConstructionStatusBadge tone={SCHEDULE_STATUS_TONE[selected.status]}>
                {t(`operationalProjects.schedule.status.${selected.status}`)}
              </ConstructionStatusBadge>
              <span className="text-muted-foreground">
                v{selected.version}
                {isSuperseded
                  ? ` · ${t("scheduleUx.revisionSuperseded")}`
                  : selected.status === "Published"
                    ? ` · ${t("scheduleUx.revisionCurrent")}`
                    : ""}
              </span>
              {selected.approverName && (
                <span className="text-muted-foreground">
                  {t("operationalProjects.schedule.assignedApprover", {
                    name: selected.approverName,
                  })}
                </span>
              )}
            </div>
          )}
          {currentPublished && selected?.id !== currentPublished.id && (
            <p
              role="status"
              className="mt-3 rounded-md border border-info/40 bg-info/10 p-3 text-sm text-foreground"
            >
              {t("scheduleUx.effectiveMasterSchedule", {
                version: currentPublished.version,
              })}
            </p>
          )}
          {!selected && canManage && (schedule?.revisions.length ?? 0) > 0 && (
            <div className="mt-2 flex flex-wrap items-center gap-2 text-sm">
              <ConstructionStatusBadge tone="muted">
                {t("operationalProjects.schedule.status.Draft")}
              </ConstructionStatusBadge>
              <span className="text-muted-foreground">v{nextVersion}</span>
            </div>
          )}
        </div>
        {schedule && (
          <div className="flex w-full flex-col gap-2 sm:w-auto sm:flex-row sm:flex-wrap sm:items-start">
            {schedule.revisions.length > 0 && (
              <div className="w-full space-y-1 sm:w-56">
                <Select
                  value={revisionId?.toString() ?? "new"}
                  onValueChange={(value) => {
                    if (value === "new") {
                      if (revisionId == null) return;
                      void startRevision();
                      return;
                    }
                    const revision = schedule.revisions.find((entry) => entry.id === Number(value));
                    if (!revision) return;
                    if (isDirty) {
                      setPendingRevision(revision);
                      return;
                    }
                    selectRevision(revision);
                  }}
                  disabled={revisionSelectDisabled}
                >
                  <SelectTrigger
                    aria-label={t("operationalProjects.schedule.revision")}
                    className="w-full"
                  >
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {revisionId == null && (
                      <SelectItem value="new">
                        {`v${nextVersion} · ${t("operationalProjects.schedule.status.Draft")}`}
                      </SelectItem>
                    )}
                    {schedule.revisions.map((revision) => (
                      <SelectItem key={revision.id} value={revision.id.toString()}>
                        {`v${revision.version} · ${t(`operationalProjects.schedule.status.${revision.status}`)}${
                          revision.status === "Published" &&
                          revision.id !== schedule.publishedRevisionId
                            ? ` · ${t("scheduleUx.revisionSuperseded")}`
                            : ""
                        }`}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {revisionSelectDisabled && selected && isDirty && (
                  <p className="text-xs text-muted-foreground">
                    {t("scheduleUx.saveBeforeSwitch")}
                  </p>
                )}
              </div>
            )}
            {isSuperseded && currentPublished && (
              <Button
                variant="outline"
                className="w-full sm:w-auto"
                onClick={() => selectRevision(currentPublished)}
              >
                {t("scheduleUx.currentRevision")}
              </Button>
            )}
            {schedule.publishedRevisionId != null && has(ADMIN_PERMS.constructionTasks) && (
              <Button asChild variant="outline" className="w-full sm:w-auto">
                <Link to={`/admin/construction/cm/schedule?projectId=${projectId}`}>
                  {t("scheduleUx.openProgress")}
                </Link>
              </Button>
            )}
            {canManage &&
              schedule.publishedRevisionId != null &&
              !hasEditableRevision &&
              revisionId != null && (
                <Button
                  variant="outline"
                  className="w-full sm:w-auto"
                  onClick={() => void startRevision()}
                  disabled={busy}
                >
                  {t("operationalProjects.schedule.newRevision")}
                </Button>
              )}
          </div>
        )}
      </header>

      <div className="space-y-4">
        {error && schedule && (
          <div
            ref={errorRef}
            role="alert"
            className="rounded-md border border-destructive/40 bg-destructive/5 p-3 text-sm"
          >
            {error}
          </div>
        )}
        {!schedule ? (
          error ? (
            <PageError message={error} onRetry={() => void reload()} />
          ) : (
            <PageLoading />
          )
        ) : (
          <>
            {selected?.status === "Rejected" && selected.reviewComment && (
              <div className="rounded-lg bg-destructive/5 p-3" role="note">
                <p className="text-sm font-medium text-destructive">
                  {t("operationalProjects.schedule.reviewComment")}
                </p>
                <p className="mt-1 whitespace-pre-line text-sm">{selected.reviewComment}</p>
              </div>
            )}

            {editableDraft && (schedule.revisions.length > 0 || revisionId != null) && (
              <div className="rounded-lg bg-muted/50 p-3 sm:p-4">
                {schedule.revisions.length > 0 && (
                  <p className="text-sm text-muted-foreground" role="status">
                    {stateText}
                  </p>
                )}
                <div className="mt-3 grid gap-3 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-end">
                  <div className="space-y-1.5">
                    <Label htmlFor="schedule-approver">
                      {t("operationalProjects.schedule.approver")}
                    </Label>
                    {approverOptions.length === 0 ? (
                      <p className="text-sm text-destructive">
                        {t("operationalProjects.schedule.noApprovers")}
                      </p>
                    ) : (
                      <SearchableSelect
                        id="schedule-approver"
                        className="w-full max-w-md"
                        value={approverId}
                        onChange={setApproverId}
                        options={approverSelectOptions}
                        placeholder={t("operationalProjects.schedule.chooseApprover")}
                        searchPlaceholder={t("common.search")}
                        emptyText={t("operationalProjects.schedule.noApprovers")}
                        ariaLabel={t("operationalProjects.schedule.approver")}
                        aria-describedby="schedule-approver-hint"
                        data-testid="schedule-approver"
                      />
                    )}
                    <p id="schedule-approver-hint" className="text-xs text-muted-foreground">
                      {t("scheduleUx.approverHint")}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button
                      className="w-full sm:w-auto"
                      onClick={() => void submit()}
                      disabled={busy || !approverId}
                    >
                      {t("operationalProjects.schedule.submit")}
                    </Button>
                    {!showEditor && (
                      <Button
                        className="w-full sm:w-auto"
                        variant="outline"
                        onClick={() => setEditorOpen(true)}
                      >
                        {t("scheduleUx.editSchedule")}
                      </Button>
                    )}
                  </div>
                </div>
              </div>
            )}

            {selected?.status === "PendingApproval" && isApprover && (
              <div className="rounded-lg bg-info-soft/60 p-3 sm:p-4">
                <p className="text-sm font-medium">
                  {t("scheduleUx.reviewAssigned", { version: selected.version })}
                </p>
                <div className="mt-3 max-w-lg space-y-1.5">
                  <Label htmlFor="schedule-review-comment">
                    {t("operationalProjects.schedule.reviewComment")}
                  </Label>
                  <Textarea
                    id="schedule-review-comment"
                    value={reviewComment}
                    onChange={(event) => setReviewComment(event.target.value)}
                    maxLength={2000}
                    rows={3}
                    aria-describedby="schedule-review-comment-hint"
                  />
                  <p id="schedule-review-comment-hint" className="text-xs text-muted-foreground">
                    {t("scheduleUx.reviewCommentHint")}
                  </p>
                </div>
                <div className="mt-3 flex flex-wrap gap-2">
                  <Button
                    className="w-full sm:w-auto"
                    onClick={() => void review("Approve")}
                    disabled={busy}
                  >
                    {t("operationalProjects.schedule.approve")}
                  </Button>
                  <Button
                    className="w-full sm:w-auto"
                    variant="outline"
                    onClick={() => void review("Reject")}
                    disabled={busy || !reviewComment.trim()}
                  >
                    {t("operationalProjects.schedule.reject")}
                  </Button>
                </div>
              </div>
            )}

            {selected?.status === "PendingApproval" && !isApprover && (
              <div className="space-y-2">
                <p className="text-sm text-muted-foreground" role="status">
                  {t("scheduleUx.masterState.PendingApproval")}
                  {selected.approverName
                    ? ` ${t("operationalProjects.schedule.assignedApprover", {
                        name: selected.approverName,
                      })}`
                    : ""}
                </p>
                {canManage && (
                  <details className="text-sm">
                    <summary className="w-fit cursor-pointer text-muted-foreground underline-offset-4 hover:underline">
                      {t("operationalProjects.schedule.reassignApprover")}
                    </summary>
                    <div className="mt-2 flex flex-col gap-2 sm:flex-row sm:items-end">
                      <div className="w-full space-y-1.5 sm:max-w-md">
                        <Label htmlFor="schedule-reassign-approver">
                          {t("operationalProjects.schedule.approver")}
                        </Label>
                        {approverOptions.length === 0 ? (
                          <p className="text-sm text-destructive">
                            {t("operationalProjects.schedule.noApprovers")}
                          </p>
                        ) : (
                          <SearchableSelect
                            id="schedule-reassign-approver"
                            value={approverId}
                            onChange={setApproverId}
                            options={approverSelectOptions}
                            placeholder={t("operationalProjects.schedule.chooseApprover")}
                            searchPlaceholder={t("common.search")}
                            emptyText={t("operationalProjects.schedule.noApprovers")}
                            ariaLabel={t("operationalProjects.schedule.approver")}
                          />
                        )}
                      </div>
                      <Button
                        variant="outline"
                        className="w-full sm:w-auto"
                        onClick={() => void reassign()}
                        disabled={
                          busy || !approverId || Number(approverId) === selected.approverUserId
                        }
                      >
                        {t("operationalProjects.schedule.reassign")}
                      </Button>
                    </div>
                  </details>
                )}
              </div>
            )}

            {isSuperseded && (
              <div className="rounded-lg bg-muted/50 p-3" role="status">
                <p className="text-sm text-muted-foreground">{stateText}</p>
              </div>
            )}

            {schedule.revisions.length === 0 ? (
              canManage ? (
                <div className="space-y-4">
                  <div className="space-y-3">
                    <p className="text-sm font-medium">{t("scheduleUx.masterNew")}</p>
                    {!showEditor && (
                      <Button variant="outline" onClick={() => setEditorOpen(true)}>
                        {t("scheduleUx.editSchedule")}
                      </Button>
                    )}
                  </div>
                  {renderEditor()}
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">{t("scheduleUx.masterNone")}</p>
              )
            ) : (
              <>
                {viewItems.length > 0 && (
                  <>
                    <div className="space-y-2">
                      <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-3 lg:grid-cols-5">
                        <div>
                          <dt className="text-xs text-muted-foreground">
                            {t("operationalProjects.schedule.start")}
                          </dt>
                          <dd className="mt-0.5 text-sm font-medium tabular-nums">
                            {formatScheduleDate(scheduleBounds.start)}
                          </dd>
                        </div>
                        <div>
                          <dt className="text-xs text-muted-foreground">
                            {t("operationalProjects.schedule.end")}
                          </dt>
                          <dd className="mt-0.5 text-sm font-medium tabular-nums">
                            {formatScheduleDate(scheduleBounds.end)}
                          </dd>
                        </div>
                        <div>
                          <dt className="text-xs text-muted-foreground">
                            {t("scheduleUx.duration")}
                          </dt>
                          <dd className="mt-0.5 text-sm font-medium tabular-nums">
                            {formatDuration(totalDuration)}
                          </dd>
                        </div>
                        <div>
                          <dt className="text-xs text-muted-foreground">{t("scheduleUx.items")}</dt>
                          <dd className="mt-0.5 text-sm font-medium tabular-nums">
                            {viewItems.length}
                          </dd>
                        </div>
                        <div>
                          <dt className="text-xs text-muted-foreground">
                            {t("scheduleUx.milestone")}
                          </dt>
                          <dd className="mt-0.5 text-sm font-medium tabular-nums">
                            {milestoneCount}
                          </dd>
                        </div>
                      </dl>
                      {projectStart && projectEnd && (
                        <p className="text-xs text-muted-foreground">
                          {t("scheduleUx.projectFrame")}: {formatScheduleDate(projectStart)} –{" "}
                          {formatScheduleDate(projectEnd)}
                        </p>
                      )}
                    </div>

                    {outsideFrameCount > 0 && (
                      <div
                        className="flex items-start gap-2 rounded-md bg-warning-soft px-3 py-2 text-sm"
                        role="note"
                      >
                        <AlertTriangle
                          className="mt-0.5 h-4 w-4 shrink-0 text-foreground/70"
                          aria-hidden
                        />
                        <p>{t("scheduleUx.outsideFrame", { count: outsideFrameCount })}</p>
                      </div>
                    )}

                    <div className="-mx-4 overflow-x-auto border-y [contain:layout] sm:mx-0">
                      <table className="w-full min-w-[1000px] table-fixed text-sm">
                        <caption className="sr-only">
                          {t("operationalProjects.schedule.title")} — {t("scheduleUx.items")}:{" "}
                          {viewItems.length}
                        </caption>
                        <colgroup>
                          <col className="w-[9%]" />
                          <col className="w-[26%]" />
                          <col className="w-[8%]" />
                          <col className="w-[10%]" />
                          <col className="w-[10%]" />
                          <col className="w-[37%]" />
                        </colgroup>
                        <thead>
                          <tr className="border-b text-left text-xs text-muted-foreground">
                            <th scope="col" className="py-2 pl-3 pr-3 font-medium">
                              {t("scheduleUx.wbs")}
                            </th>
                            <th scope="col" className="py-2 pl-8 pr-3 font-medium">
                              {t("scheduleUx.workItem")}
                            </th>
                            <th scope="col" className="py-2 pr-3 font-medium">
                              {t("scheduleUx.duration")}
                            </th>
                            <th scope="col" className="py-2 pr-3 font-medium">
                              {t("operationalProjects.schedule.start")}
                            </th>
                            <th scope="col" className="py-2 pr-3 font-medium">
                              {t("operationalProjects.schedule.end")}
                            </th>
                            <th scope="col" className="py-2 pl-3 font-medium">
                              {t("scheduleUx.timeline")}
                            </th>
                          </tr>
                          {timeline && (
                            <tr className="border-b" aria-hidden>
                              <th colSpan={5} className="p-0" />
                              <th scope="col" className="p-0">
                                <div className="relative h-9">
                                  {timeline.years.map((year) => (
                                    <div
                                      key={year.key}
                                      className="absolute top-0 h-4 border-l border-border/60"
                                      style={{
                                        left: `${year.left}%`,
                                        width: `${year.width}%`,
                                      }}
                                    >
                                      <span className="block truncate px-1 text-[10px] leading-4 text-muted-foreground">
                                        {year.label}
                                      </span>
                                    </div>
                                  ))}
                                  {timeline.months.map((month, index) => {
                                    // Partial edge columns are too narrow for a
                                    // label; the year band still covers them.
                                    const labelled =
                                      (!month.partial || timeline.months.length === 1) &&
                                      index % timeline.labelEvery === 0;
                                    const span = Math.min(
                                      timeline.labelEvery,
                                      timeline.months.length - index,
                                    );
                                    return (
                                      <div
                                        key={month.key}
                                        className="absolute bottom-0 top-4 border-l border-border/60"
                                        style={{
                                          left: `${month.left}%`,
                                          width: `${month.width}%`,
                                        }}
                                      >
                                        {labelled && (
                                          <span
                                            className="absolute inset-y-0 left-0 block truncate px-1 text-[10px] leading-5 text-muted-foreground"
                                            style={{ width: `${span * 100}%` }}
                                          >
                                            {month.label}
                                          </span>
                                        )}
                                      </div>
                                    );
                                  })}
                                  {timeline.todayLeft != null && (
                                    <span
                                      className="absolute inset-y-0 w-px bg-info"
                                      style={{ left: `${timeline.todayLeft}%` }}
                                    />
                                  )}
                                </div>
                              </th>
                            </tr>
                          )}
                        </thead>
                        <tbody>
                          {treeRows.map((row) => {
                            const item = row.item;
                            const duration = durationDays(item.plannedStart, item.plannedEnd);
                            const bar = barStyle(item);
                            const milestone = milestoneStyle(item);
                            return (
                              <tr
                                key={item.nodeId}
                                className={cn(
                                  "border-b transition-colors last:border-0 hover:bg-muted/30",
                                  row.depth === 0 && "bg-muted/30",
                                )}
                              >
                                <td className="py-2 pl-3 pr-3 align-middle text-xs font-medium tabular-nums text-muted-foreground">
                                  {item.wbsCode}
                                </td>
                                <td className="py-2 pr-3 align-middle">
                                  <div
                                    className="flex min-w-0 items-start gap-1"
                                    style={{ paddingLeft: `${row.depth * 16}px` }}
                                  >
                                    {/* One 28px slot on every row keeps the
                                        chevron, the milestone mark and plain
                                        titles on the same left edge. */}
                                    <span className="-mt-1 flex h-7 w-7 shrink-0 items-center justify-center">
                                      {row.hasChildren ? (
                                        <button
                                          type="button"
                                          onClick={() => toggleCollapsed(item.nodeId)}
                                          aria-expanded={!collapsed.has(item.nodeId)}
                                          aria-label={t(
                                            collapsed.has(item.nodeId)
                                              ? "scheduleUx.expandItem"
                                              : "scheduleUx.collapseItem",
                                            { name: displayTitle(item) },
                                          )}
                                          className="inline-flex h-7 w-7 shrink-0 items-center justify-center rounded text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                                        >
                                          {collapsed.has(item.nodeId) ? (
                                            <ChevronRight className="h-4 w-4" />
                                          ) : (
                                            <ChevronDown className="h-4 w-4" />
                                          )}
                                        </button>
                                      ) : isMilestone(item) ? (
                                        <Diamond
                                          className="h-3 w-3 shrink-0 text-muted-foreground"
                                          aria-hidden
                                        />
                                      ) : null}
                                    </span>
                                    <span
                                      className={cn(
                                        "min-w-0 break-words",
                                        row.depth === 0 && "font-medium",
                                      )}
                                    >
                                      {displayTitle(item)}
                                    </span>
                                  </div>
                                </td>
                                <td className="whitespace-nowrap py-2 pr-3 align-middle text-xs tabular-nums text-muted-foreground">
                                  {isMilestone(item)
                                    ? t("scheduleUx.milestone")
                                    : formatDuration(duration)}
                                </td>
                                <td className="whitespace-nowrap py-2 pr-3 align-middle text-xs tabular-nums">
                                  {formatScheduleDate(item.plannedStart)}
                                </td>
                                <td className="whitespace-nowrap py-2 pr-3 align-middle text-xs tabular-nums">
                                  {formatScheduleDate(item.plannedEnd)}
                                </td>
                                <td className="py-2 align-middle">
                                  {timeline ? (
                                    <div className="relative h-8">
                                      {timeline.frameLeft != null &&
                                        timeline.frameWidth != null && (
                                          <span
                                            className="absolute inset-y-2 rounded-sm bg-muted/60"
                                            style={{
                                              left: `${timeline.frameLeft}%`,
                                              width: `${timeline.frameWidth}%`,
                                            }}
                                            aria-hidden
                                          />
                                        )}
                                      {isMilestone(item) && milestone ? (
                                        <span
                                          className="absolute top-1/2 h-2.5 w-2.5 -translate-x-1/2 -translate-y-1/2 rotate-45 rounded-[1px] bg-foreground ring-2 ring-background"
                                          style={milestone}
                                          aria-hidden
                                        />
                                      ) : bar ? (
                                        <span
                                          className={cn(
                                            "absolute top-1/2 h-2 -translate-y-1/2 rounded-sm",
                                            row.hasChildren
                                              ? "bg-foreground/30"
                                              : "bg-foreground/55",
                                          )}
                                          style={{ ...bar, minWidth: "3px" }}
                                          aria-hidden
                                        />
                                      ) : null}
                                      {timeline.todayLeft != null && (
                                        <span
                                          className="absolute inset-y-0 w-px bg-info"
                                          style={{ left: `${timeline.todayLeft}%` }}
                                          aria-hidden
                                        />
                                      )}
                                    </div>
                                  ) : (
                                    <span className="text-xs text-muted-foreground">—</span>
                                  )}
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                    <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-xs text-muted-foreground">
                      <span className="inline-flex items-center gap-1.5">
                        <span className="h-2 w-4 rounded-sm bg-foreground/55" aria-hidden />
                        {t("scheduleUx.duration")}
                      </span>
                      <span className="inline-flex items-center gap-1.5">
                        <span className="h-2 w-2 rotate-45 bg-foreground" aria-hidden />
                        {t("scheduleUx.milestone")}
                      </span>
                      {timeline?.todayLeft != null && (
                        <span className="inline-flex items-center gap-1.5">
                          <span className="h-3 w-px bg-info" aria-hidden />
                          {t("scheduleUx.today")}
                        </span>
                      )}
                      <span>{t("scheduleUx.timelineHint")}</span>
                    </div>
                  </>
                )}

                {renderEditor()}

                {diffBaseline && (
                  <details className="border-t pt-3" open={selected?.status !== "Published"}>
                    <summary className="cursor-pointer py-2 text-sm font-medium">
                      {t("operationalProjects.schedule.diffTitle")} ({diffRows.length})
                    </summary>
                    <div className="space-y-2 pt-2">
                      <p className="text-xs text-muted-foreground">
                        v{diffBaseline.version} → v{selected?.version ?? nextVersion}
                      </p>
                      {diffRows.length === 0 ? (
                        <p className="text-sm text-muted-foreground">
                          {t("operationalProjects.schedule.diffNone")}
                        </p>
                      ) : (
                        <ul className="space-y-2">
                          {diffRows.map((row) => (
                            <li
                              key={row.key}
                              className="flex flex-wrap items-baseline gap-x-3 gap-y-1 border-b pb-2 text-sm last:border-0 last:pb-0"
                            >
                              <ConstructionStatusBadge
                                tone={
                                  row.status === "Added"
                                    ? "success"
                                    : row.status === "Removed"
                                      ? "danger"
                                      : "warning"
                                }
                              >
                                {t(`operationalProjects.schedule.diff.${row.status}`)}
                              </ConstructionStatusBadge>
                              <span className="font-medium">{row.wbsCode}</span>
                              <span>{row.title}</span>
                              {row.detail && (
                                <span className="w-full text-muted-foreground sm:w-auto">
                                  {row.detail}
                                </span>
                              )}
                            </li>
                          ))}
                        </ul>
                      )}
                    </div>
                  </details>
                )}

                {!showEditor && selected?.changeReason && (
                  <details
                    className="border-t pt-3"
                    open={selected.status === "PendingApproval" ? true : undefined}
                  >
                    <summary className="cursor-pointer py-2 text-sm font-medium">
                      {t("scheduleUx.masterHistory")}
                    </summary>
                    <div className="space-y-2 pt-2">
                      <p className="text-sm font-medium">
                        {t("operationalProjects.schedule.changeReason")}
                      </p>
                      <p className="whitespace-pre-line text-sm text-muted-foreground">
                        {selected.changeReason}
                      </p>
                    </div>
                  </details>
                )}
              </>
            )}

            {isProjectManager && (
              <details
                className="border-t pt-3"
                open={emailsNeedingReview.length > 0 ? true : undefined}
              >
                <summary className="cursor-pointer py-2 text-sm font-medium">
                  {t("operationalProjects.schedule.email.reviewTitle")} (
                  {emailsNeedingReview.length})
                </summary>
                <div className="space-y-3 pt-2">
                  <div className="flex justify-end">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => void loadEmailsNeedingReview()}
                    >
                      {t("emailTemplate.schedule.delivery.refresh")}
                    </Button>
                  </div>
                  {emailsNeedingReview.length === 0 && (
                    <p className="text-sm text-muted-foreground">
                      {t("emailTemplate.schedule.delivery.empty")}
                    </p>
                  )}
                  {emailsNeedingReview.map((email) => (
                    <div key={email.id} className="space-y-2 border-t pt-3 text-sm">
                      <p className="font-medium">
                        {email.toEmail} · v{email.version}
                      </p>
                      <p>{email.subject}</p>
                      <iframe
                        title={`${t("operationalProjects.schedule.email.previewTitle")} ${email.id}`}
                        sandbox=""
                        srcDoc={email.body ?? ""}
                        className="h-64 w-full rounded border bg-card"
                      />
                      <p className="text-muted-foreground">
                        {t("operationalProjects.schedule.email.checkFirst")}
                      </p>
                      <div className="flex flex-wrap gap-2">
                        <Button
                          disabled={busy}
                          onClick={() => void resolveEmail(email.id, "ConfirmReceived")}
                        >
                          {t("operationalProjects.schedule.email.confirmReceived")}
                        </Button>
                        <Button
                          disabled={busy}
                          variant="outline"
                          onClick={() => void resolveEmail(email.id, "Retry")}
                        >
                          {t("operationalProjects.schedule.email.retry")}
                        </Button>
                      </div>
                    </div>
                  ))}
                </div>
              </details>
            )}
          </>
        )}
      </div>
    </section>
  );
};
