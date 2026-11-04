import { useCallback, useEffect, useMemo, useState } from "react";
import { useAppSelector } from "@/store";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { extractApiError } from "@/lib/apiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import { usePermissions } from "@/hooks/usePermissions";
import {
  adminApi,
  type ProjectScheduleItemResponse,
  type ProjectScheduleResponse,
  type ProjectScheduleRevisionResponse,
  type SaveProjectScheduleItemRequest,
} from "@/services/adminApi";
import { useToast } from "@/hooks/use-toast";

interface ProjectMasterScheduleProps {
  projectId: number;
  projectStart?: string | null;
  projectEnd?: string | null;
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

const makeDefaultItem = (start?: string | null, end?: string | null): DraftItem => ({
  nodeId: crypto.randomUUID(),
  parentNodeId: "",
  wbsCode: "1",
  title: "",
  plannedStart: start?.slice(0, 10) ?? localDateToday(),
  plannedEnd: end?.slice(0, 10) ?? localDateToday(),
  sortOrder: 0,
});

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

function localDateToday() {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
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
}: ProjectMasterScheduleProps) => {
  const { t } = useI18n();
  const { has } = usePermissions();
  const { toast } = useToast();
  const currentUserId = useAppSelector((state) => state.auth.user?.userId ?? null);
  const canManage = has(ADMIN_PERMS.operationalProjectsManage);
  const [schedule, setSchedule] = useState<ProjectScheduleResponse | null>(null);
  const [revisionId, setRevisionId] = useState<number | null>(null);
  const [items, setItems] = useState<DraftItem[]>([]);
  const [changeReason, setChangeReason] = useState("");
  const [approverId, setApproverId] = useState("");
  const [reviewComment, setReviewComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    const response = await adminApi.getProjectSchedule(projectId);
    setSchedule(response.data);
    const editable = response.data.revisions.find(
      (item) => item.status === "Draft" || item.status === "Rejected",
    );
    const selected = editable ?? response.data.revisions[0];
    setRevisionId(selected?.id ?? null);
    setItems(selected ? selected.items.map(fromItem) : [makeDefaultItem(projectStart, projectEnd)]);
    setChangeReason(selected?.changeReason ?? "");
    setApproverId(selected?.approverUserId?.toString() ?? "");
    setReviewComment("");
    setError(null);
  }, [projectEnd, projectId, projectStart]);

  useEffect(() => {
    void reload().catch((reason: unknown) => setError(extractApiError(reason, t("common.error"))));
  }, [reload, t]);

  const selected = useMemo(
    () => schedule?.revisions.find((item) => item.id === revisionId) ?? null,
    [revisionId, schedule],
  );
  const editable =
    canManage && (!selected || selected.status === "Draft" || selected.status === "Rejected");
  const approverOptions = schedule?.approverOptions ?? [];
  const hasEditableRevision = Boolean(
    schedule?.revisions.some((revision) => revision.status !== "Published"),
  );

  const savePayload = (): { changeReason: string; items: SaveProjectScheduleItemRequest[] } => ({
    changeReason: changeReason.trim(),
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
    setApproverId("");
    setRevisionId(null);
    setError(null);
  };

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
          : await adminApi.updateProjectScheduleRevision(projectId, revisionId, savePayload());
      setSchedule(response.data);
      const saved =
        response.data.revisions.find((item) => item.id === revisionId) ??
        response.data.revisions.find((item) => item.status === "Draft") ??
        response.data.revisions[0] ??
        null;
      setRevisionId(saved?.id ?? null);
      setItems(saved ? saved.items.map(fromItem) : []);
      setChangeReason(saved?.changeReason ?? "");
      setApproverId(saved?.approverUserId?.toString() ?? "");
      return saved;
    } catch (reason) {
      setError(extractApiError(reason, t("common.error")));
      return null;
    } finally {
      setBusy(false);
    }
  };

  const save = async () => {
    const saved = await persist();
    if (saved) setToastSuccess("operationalProjects.schedule.saved");
  };

  const sendForApproval = async (target: ProjectScheduleRevisionResponse) => {
    setBusy(true);
    setError(null);
    try {
      const response = await adminApi.submitProjectScheduleRevision(
        projectId,
        target.id,
        Number(approverId),
        target.rowVersion,
      );
      setSchedule(response.data);
      setRevisionId(target.id);
      toast({ title: t("operationalProjects.schedule.submitted") });
    } catch (reason) {
      setError(extractApiError(reason, t("common.error")));
    } finally {
      setBusy(false);
    }
  };

  const submit = async () => {
    if (!selected || selected.status !== "Draft" || !approverId) return;
    // Do not silently drop edits made after the last save.
    const target = isDirty || revisionId == null ? await persist() : selected;
    if (!target || target.status !== "Draft") return;
    await sendForApproval(target);
  };

  const reassign = async () => {
    if (!selected || selected.status !== "PendingApproval" || !approverId) return;
    await sendForApproval(selected);
  };

  const review = async (decision: "Approve" | "Reject") => {
    if (!selected) return;
    setBusy(true);
    setError(null);
    try {
      const response = await adminApi.reviewProjectScheduleRevision(
        projectId,
        selected.id,
        decision,
        reviewComment,
        selected.rowVersion,
      );
      setSchedule(response.data);
      setRevisionId(selected.id);
      toast({
        title: t(
          decision === "Approve"
            ? "operationalProjects.schedule.published"
            : "operationalProjects.schedule.rejected",
        ),
      });
    } catch (reason) {
      setError(extractApiError(reason, t("common.error")));
    } finally {
      setBusy(false);
    }
  };

  const setToastSuccess = (key: string) => toast({ title: t(key) });
  const patchItem = (index: number, change: Partial<DraftItem>) =>
    setItems((current) =>
      current.map((item, itemIndex) => {
        if (itemIndex !== index) return item;
        const updated = { ...item, ...change };
        if (change.parentNodeId !== undefined && change.parentNodeId !== item.parentNodeId) {
          updated.wbsCode = suggestWbsCode(current, change.parentNodeId);
        }
        return updated;
      }),
    );
  const isApprover =
    selected?.status === "PendingApproval" && selected.approverUserId === currentUserId;

  return (
    <section
      id="project-master-schedule"
      className="space-y-4 rounded-lg border bg-card p-4 shadow-sm"
    >
      <header className="flex flex-row flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">{t("operationalProjects.schedule.title")}</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            {t(
              schedule?.hasSignedUpstreamContract
                ? "operationalProjects.schedule.approverPm"
                : "operationalProjects.schedule.approverCrm",
            )}
          </p>
        </div>
        {canManage && schedule?.publishedRevisionId != null && !hasEditableRevision && (
          <Button variant="outline" onClick={() => void startRevision()} disabled={busy}>
            {t("operationalProjects.schedule.newRevision")}
          </Button>
        )}
      </header>
      <div className="space-y-4">
        {error && (
          <div
            role="alert"
            className="rounded-md border border-destructive/40 bg-destructive/5 p-3 text-sm"
          >
            {error}
          </div>
        )}
        {!schedule ? (
          <p className="text-sm text-muted-foreground">{t("common.loading")}</p>
        ) : (
          <>
            {schedule.revisions.length > 0 && (
              <div className="flex flex-wrap items-end gap-3">
                <div className="min-w-56 flex-1 space-y-2">
                  <Label>{t("operationalProjects.schedule.revision")}</Label>
                  <Select
                    value={revisionId?.toString() ?? ""}
                    onValueChange={(value) => {
                      const revision = schedule.revisions.find(
                        (entry) => entry.id === Number(value),
                      );
                      setRevisionId(revision?.id ?? null);
                      setItems(revision?.items.map(fromItem) ?? []);
                      setChangeReason(revision?.changeReason ?? "");
                      setApproverId(revision?.approverUserId?.toString() ?? "");
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {schedule.revisions.map((revision) => (
                        <SelectItem
                          key={revision.id}
                          value={revision.id.toString()}
                        >{`v${revision.version} · ${t(`operationalProjects.schedule.status.${revision.status}`)}`}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                {selected?.status === "PendingApproval" && (
                  <p className="text-sm text-muted-foreground">
                    {t("operationalProjects.schedule.assignedApprover", {
                      name: selected.approverName ?? "",
                    })}
                  </p>
                )}
                {selected?.status === "Rejected" && (
                  <p className="text-sm text-destructive">{selected.reviewComment}</p>
                )}
              </div>
            )}
            {(revisionId == null ||
              selected?.status === "Draft" ||
              selected?.status === "Rejected") && (
              <div className="space-y-4">
                <div className="space-y-2">
                  <Label>{t("operationalProjects.schedule.changeReason")}</Label>
                  <Textarea
                    value={changeReason}
                    onChange={(event) => setChangeReason(event.target.value)}
                    maxLength={2000}
                    disabled={!editable}
                  />
                </div>
                <div className="space-y-3">
                  {items.map((item, index) => (
                    <div
                      key={item.nodeId || `new-${index}`}
                      className="grid gap-3 rounded-md border p-3 md:grid-cols-12"
                    >
                      <div className="space-y-2 md:col-span-1">
                        <Label>{t("operationalProjects.schedule.wbs")}</Label>
                        <Input
                          value={item.wbsCode}
                          onChange={(event) => patchItem(index, { wbsCode: event.target.value })}
                          maxLength={80}
                          disabled={!editable}
                        />
                      </div>
                      <div className="space-y-2 md:col-span-3">
                        <Label>{t("operationalProjects.schedule.item")}</Label>
                        <Input
                          value={item.title}
                          onChange={(event) => patchItem(index, { title: event.target.value })}
                          maxLength={300}
                          disabled={!editable}
                        />
                      </div>
                      <div className="space-y-2 md:col-span-2">
                        <Label>{t("operationalProjects.schedule.parent")}</Label>
                        <Select
                          value={item.parentNodeId || "none"}
                          onValueChange={(value) =>
                            patchItem(index, { parentNodeId: value === "none" ? "" : value })
                          }
                          disabled={!editable}
                        >
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="none">
                              {t("operationalProjects.schedule.root")}
                            </SelectItem>
                            {items
                              .filter(
                                (parent, parentIndex) => parentIndex !== index && parent.nodeId,
                              )
                              .map((parent) => (
                                <SelectItem key={parent.nodeId} value={parent.nodeId}>
                                  {parent.wbsCode} · {parent.title}
                                </SelectItem>
                              ))}
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="space-y-2 md:col-span-2">
                        <Label>{t("operationalProjects.schedule.start")}</Label>
                        <Input
                          type="date"
                          value={item.plannedStart}
                          onChange={(event) =>
                            patchItem(index, { plannedStart: event.target.value })
                          }
                          disabled={!editable}
                        />
                      </div>
                      <div className="space-y-2 md:col-span-2">
                        <Label>{t("operationalProjects.schedule.end")}</Label>
                        <Input
                          type="date"
                          value={item.plannedEnd}
                          onChange={(event) => patchItem(index, { plannedEnd: event.target.value })}
                          disabled={!editable}
                        />
                      </div>
                      {editable && (
                        <div className="flex items-end md:col-span-2">
                          <Button
                            type="button"
                            variant="outline"
                            onClick={() =>
                              setItems((current) =>
                                current.filter((_, itemIndex) => itemIndex !== index),
                              )
                            }
                          >
                            {t("common.delete")}
                          </Button>
                        </div>
                      )}
                    </div>
                  ))}
                </div>
                {editable && (
                  <div className="flex flex-wrap gap-2">
                    <Button
                      variant="outline"
                      onClick={() =>
                        setItems((current) => {
                          const item = makeDefaultItem(projectStart, projectEnd);
                          item.wbsCode = suggestWbsCode(current);
                          return [...current, item];
                        })
                      }
                    >
                      {t("operationalProjects.schedule.addItem")}
                    </Button>
                    <Button onClick={() => void save()} disabled={busy || items.length === 0}>
                      {t("common.save")}
                    </Button>
                  </div>
                )}
                {selected?.status === "Draft" && editable && (
                  <div className="grid gap-2 sm:max-w-lg">
                    <Label>{t("operationalProjects.schedule.approver")}</Label>
                    <Select value={approverId} onValueChange={setApproverId}>
                      <SelectTrigger>
                        <SelectValue
                          placeholder={t("operationalProjects.schedule.chooseApprover")}
                        />
                      </SelectTrigger>
                      <SelectContent>
                        {approverOptions.map((user) => (
                          <SelectItem key={user.userId} value={user.userId.toString()}>
                            {user.name || user.email}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <Button
                      variant="outline"
                      onClick={() => void submit()}
                      disabled={busy || !approverId}
                    >
                      {t("operationalProjects.schedule.submit")}
                    </Button>
                  </div>
                )}
              </div>
            )}
            {selected?.status === "PendingApproval" && canManage && (
              <div className="grid gap-2 sm:max-w-lg">
                <Label>{t("operationalProjects.schedule.reassignApprover")}</Label>
                <Select value={approverId} onValueChange={setApproverId}>
                  <SelectTrigger>
                    <SelectValue placeholder={t("operationalProjects.schedule.chooseApprover")} />
                  </SelectTrigger>
                  <SelectContent>
                    {approverOptions.map((user) => (
                      <SelectItem key={user.userId} value={user.userId.toString()}>
                        {user.name || user.email}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Button
                  variant="outline"
                  onClick={() => void reassign()}
                  disabled={busy || !approverId || Number(approverId) === selected.approverUserId}
                >
                  {t("operationalProjects.schedule.reassign")}
                </Button>
              </div>
            )}
            {selected?.status === "PendingApproval" && isApprover && (
              <div className="grid gap-3 sm:max-w-lg">
                <Label>{t("operationalProjects.schedule.reviewComment")}</Label>
                <Textarea
                  value={reviewComment}
                  onChange={(event) => setReviewComment(event.target.value)}
                  maxLength={2000}
                />
                <div className="flex gap-2">
                  <Button onClick={() => void review("Approve")} disabled={busy}>
                    {t("operationalProjects.schedule.approve")}
                  </Button>
                  <Button
                    variant="outline"
                    onClick={() => void review("Reject")}
                    disabled={busy || !reviewComment.trim()}
                  >
                    {t("operationalProjects.schedule.reject")}
                  </Button>
                </div>
              </div>
            )}
            {selected && (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[720px] text-sm">
                  <thead>
                    <tr className="border-b text-left">
                      <th className="p-2">{t("operationalProjects.schedule.wbs")}</th>
                      <th className="p-2">{t("operationalProjects.schedule.item")}</th>
                      <th className="p-2">{t("operationalProjects.schedule.parent")}</th>
                      <th className="p-2">{t("operationalProjects.schedule.start")}</th>
                      <th className="p-2">{t("operationalProjects.schedule.end")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {selected.items.map((item) => (
                      <tr key={item.nodeId} className="border-b">
                        <td className="p-2">{item.wbsCode}</td>
                        <td className="p-2">{item.title}</td>
                        <td className="p-2">
                          {selected.items.find((parent) => parent.nodeId === item.parentNodeId)
                            ?.wbsCode ?? "—"}
                        </td>
                        <td className="p-2">{item.plannedStart}</td>
                        <td className="p-2">{item.plannedEnd}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        )}
      </div>
    </section>
  );
};
