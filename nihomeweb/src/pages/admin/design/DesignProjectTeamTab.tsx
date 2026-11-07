import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { useConfirm } from "@/components/ui/confirmation";
import { FieldScope } from "@/components/ui/field-scope";
import { type FormEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { personOption, projectMemberOption, projectPositionLabel } from "@/lib/people";
import { SearchableSelect } from "@/components/ui/searchable-select";
import AdminDocumentUpload from "@/components/admin/AdminDocumentUpload";
import {
  CheckCircle2,
  ClipboardList,
  CornerDownRight,
  Download,
  History,
  Loader2,
  MessageSquare,
  Paperclip,
  Pencil,
  Plus,
  RefreshCw,
  Send,
  ShieldCheck,
  Trash2,
  UserRound,
  UserRoundX,
  Users,
  X,
  XCircle,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
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
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { useToast } from "@/hooks/use-toast";
import { newIdempotencyKey } from "@/lib/api";
import { extractApiError } from "@/lib/apiError";
import { translateError, useI18n } from "@/lib/i18n";
import { useAppSelector } from "@/store";
import {
  adminApi,
  type DesignProjectMemberPermissionsResponse,
  type OperationalProjectAssignmentAttachmentResponse,
  type OperationalProjectAssignmentCommentResponse,
  type OperationalProjectAssignmentResponse,
  type OperationalProjectMemberResponse,
  type OperationalProjectTeamHistoryResponse,
  type OperationalProjectTeamResponse,
  type ProjectAssignmentStatus,
  type ProjectMemberCandidateResponse,
  type ProjectMemberRoleRequest,
  type UpsertOperationalProjectAssignmentRequest,
  type UpsertOperationalProjectMemberRequest,
} from "@/services/adminApi";

interface Props {
  operationalProjectId?: number | null;
  refreshKey?: string;
  designProjectContext?: boolean;
  designProjectId?: number;
  designManagerUserId?: number | null;
  designLeadUserId?: number | null;
  onHistoryChange?: (projectId: number, history: OperationalProjectTeamHistoryResponse[]) => void;
  onProjectMayHaveChanged?: () => void;
}
type MemberDialogMode = "add" | "edit" | "end";
type AssignmentDialogMode = "add" | "edit";
type TerminalAssignmentStatus = "Completed" | "Cancelled";
interface MemberForm extends UpsertOperationalProjectMemberRequest {
  startedAt: string;
  endedAt: string | null;
}
interface AssignmentForm extends UpsertOperationalProjectAssignmentRequest {
  plannedStart: string | null;
  plannedEnd: string | null;
}

const today = () => new Date().toISOString().slice(0, 10);
const toDateValue = (value?: string | null) => value?.slice(0, 10) ?? "";
const toUtcDate = (value?: string | null) => (value ? `${value.slice(0, 10)}T00:00:00.000Z` : null);
const optional = (value?: string | null) => value?.trim() || null;
const emptyMemberForm = (): MemberForm => ({
  userId: 0,
  position: "",
  reportsToMemberId: null,
  startedAt: today(),
  endedAt: null,
  roles: [{ roleCode: "", scope: "", scopeValue: null }],
});
const emptyAssignmentForm = (): AssignmentForm => ({
  workKey: "",
  title: "",
  module: "",
  discipline: null,
  parallelGroup: null,
  parentAssignmentId: null,
  assigneeMemberId: 0,
  managerMemberId: null,
  status: "Planned",
  plannedStart: null,
  plannedEnd: null,
  note: null,
});
const assignmentBadge: Record<ProjectAssignmentStatus, string> = {
  Planned: "border-border bg-muted/30 text-foreground",
  InProgress: "border-sky-200 bg-sky-50 text-sky-700",
  Completed: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Cancelled: "border-rose-200 bg-rose-50 text-rose-700",
  Reassigned: "border-border bg-muted/30 text-foreground",
};

export const ProjectTeamHistory = ({
  history,
}: {
  history: OperationalProjectTeamHistoryResponse[];
}) => {
  const { t, lang } = useI18n();
  return (
    <section className="rounded-lg border border-border bg-card p-4 shadow-sm">
      <h2 className="flex items-center gap-2 font-semibold text-foreground">
        <History className="h-4 w-4 text-muted-foreground" />
        {t("designProjects.team.history.title")}
      </h2>
      {history.length === 0 ? (
        <p className="mt-4 text-sm text-muted-foreground">
          {t("designProjects.team.history.empty")}
        </p>
      ) : (
        <ol className="mt-4 space-y-2">
          {history.map((item) => (
            <li
              key={`${item.entityType}:${item.id}`}
              data-testid={`project-history-${item.entityType}-${item.id}`}
              className="flex flex-col justify-between gap-1 rounded-md border border-border bg-muted/30 p-3 text-sm sm:flex-row sm:items-center"
            >
              <div className="min-w-0 break-words">
                <strong>{item.changedByName}</strong> ·{" "}
                {t(`designProjects.team.history.entity.${item.entityType}`)} ·{" "}
                {t(`designProjects.team.history.action.${item.action}`)}
                {item.entityType === "ProjectCode" && (
                  <>
                    <p className="mt-1 font-medium">
                      {t("designProjects.team.history.codeChange", {
                        oldCode: item.oldCode ?? "",
                        newCode: item.newCode ?? "",
                      })}
                    </p>
                    <p className="mt-1 text-muted-foreground">
                      {t("designProjects.team.history.codeChangeReason", {
                        reason: item.reason ?? "",
                      })}
                    </p>
                  </>
                )}
              </div>
              <span className="shrink-0 text-xs text-muted-foreground">
                {new Intl.DateTimeFormat(lang, { dateStyle: "medium", timeStyle: "short" }).format(
                  new Date(item.changedAt),
                )}
              </span>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
};

const assignmentIsMutable = (assignment: OperationalProjectAssignmentResponse) =>
  assignment.status !== "Completed" &&
  assignment.status !== "Cancelled" &&
  assignment.status !== "Reassigned";

const AssignmentAttachments = ({
  operationalProjectId,
  assignment,
  designProjectId,
  canManage,
}: {
  operationalProjectId: number;
  assignment: OperationalProjectAssignmentResponse;
  designProjectId?: number;
  canManage: boolean;
}) => {
  const uiError = useUiError();
  const confirmAction = useConfirm();
  const { t } = useI18n();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<OperationalProjectAssignmentAttachmentResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const mutable = assignmentIsMutable(assignment);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.listOperationalProjectAssignmentAttachments(
        operationalProjectId,
        assignment.id,
        designProjectId,
      );
      setItems(data);
    } catch (loadError) {
      setError(uiError(loadError));
    } finally {
      setLoading(false);
    }
  }, [operationalProjectId, assignment.id, designProjectId, uiError]);

  useEffect(() => {
    if (open) void load();
  }, [open, load]);

  const download = async (attachment: OperationalProjectAssignmentAttachmentResponse) => {
    try {
      const response = await adminApi.downloadOperationalProjectAssignmentAttachment(
        operationalProjectId,
        assignment.id,
        attachment.id,
        designProjectId,
      );
      const url = URL.createObjectURL(response.data);
      const link = document.createElement("a");
      link.href = url;
      link.download = attachment.originalFileName;
      link.click();
      // Some browsers start the download on a later tick; release the blob later.
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (downloadError) {
      setError(uiError(downloadError));
    }
  };

  const remove = async (attachment: OperationalProjectAssignmentAttachmentResponse) => {
    if (!(await confirmAction(t("designProjects.team.attachments.confirmDelete")))) return;
    try {
      await adminApi.deleteOperationalProjectAssignmentAttachment(
        operationalProjectId,
        assignment.id,
        attachment.id,
        designProjectId,
      );
      toast({ title: t("designProjects.team.attachments.deleted") });
      await load();
    } catch (deleteError) {
      setError(uiError(deleteError));
    }
  };

  return (
    <div className="mt-3 border-t pt-3">
      <Button
        type="button"
        variant="ghost"
        size="sm"
        className="px-2"
        aria-expanded={open}
        onClick={() => setOpen((current) => !current)}
      >
        <Paperclip className="mr-1.5 h-4 w-4" />
        {t("designProjects.team.attachments.title")}
        {items.length > 0 ? ` (${items.length})` : ""}
      </Button>
      {open ? (
        <div className="mt-2 space-y-3 rounded-md bg-muted/30 p-3">
          {loading ? (
            <p className="text-sm text-muted-foreground">{t("common.loading")}</p>
          ) : items.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              {t("designProjects.team.attachments.empty")}
            </p>
          ) : (
            <ul className="space-y-2">
              {items.map((attachment) => (
                <li
                  key={attachment.id}
                  className="flex flex-wrap items-center justify-between gap-2 rounded-md border bg-card px-3 py-2 text-sm"
                >
                  <span className="min-w-0 break-all">{attachment.originalFileName}</span>
                  <div className="flex shrink-0 gap-1">
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      aria-label={t("designProjects.team.attachments.download")}
                      onClick={() => void download(attachment)}
                    >
                      <Download className="h-4 w-4" />
                    </Button>
                    {canManage && mutable ? (
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        aria-label={t("designProjects.team.attachments.delete")}
                        onClick={() => void remove(attachment)}
                      >
                        <Trash2 className="h-4 w-4 text-rose-600" />
                      </Button>
                    ) : null}
                  </div>
                </li>
              ))}
            </ul>
          )}
          {canManage && mutable ? (
            <AdminDocumentUpload
              multiple
              testId={`assignment-${assignment.id}-attachment`}
              uploadFile={async (file) => {
                const { data } = await adminApi.uploadOperationalProjectAssignmentAttachment(
                  operationalProjectId,
                  assignment.id,
                  file,
                  newIdempotencyKey(),
                  designProjectId,
                );
                return String(data.id);
              }}
              onUploaded={() => load()}
            />
          ) : null}
          {error ? (
            <p role="alert" className="text-sm text-rose-600">
              {error}
            </p>
          ) : null}
        </div>
      ) : null}
    </div>
  );
};

const AssignmentComments = ({
  operationalProjectId,
  assignment,
  designProjectId,
  canManage,
}: {
  operationalProjectId: number;
  assignment: OperationalProjectAssignmentResponse;
  designProjectId?: number;
  canManage: boolean;
}) => {
  const uiError = useUiError();
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const currentUserId = useAppSelector((state) => state.auth.user?.userId ?? null);
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<OperationalProjectAssignmentCommentResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [body, setBody] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] =
    useState<OperationalProjectAssignmentCommentResponse | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const deleteTriggerRef = useRef<HTMLButtonElement | null>(null);
  const commentsToggleRef = useRef<HTMLButtonElement>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const { data } = await adminApi.listOperationalProjectAssignmentComments(
        operationalProjectId,
        assignment.id,
        designProjectId,
      );
      setItems(data);
    } catch (loadError) {
      setLoadError(uiError(loadError));
    } finally {
      setLoading(false);
    }
  }, [operationalProjectId, assignment.id, designProjectId, uiError]);

  useEffect(() => {
    void load();
  }, [load]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const trimmed = body.trim();
    if (!trimmed) {
      setError(t("designProjects.team.comments.required"));
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await adminApi.addOperationalProjectAssignmentComment(
        operationalProjectId,
        assignment.id,
        trimmed,
        designProjectId,
      );
      setBody("");
      await load();
    } catch (submitError) {
      setError(translateError(t, uiError(submitError, t("common.error"))));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (comment: OperationalProjectAssignmentCommentResponse) => {
    if (deleting) return;
    setDeleting(true);
    setDeleteError(null);
    try {
      await adminApi.deleteOperationalProjectAssignmentComment(
        operationalProjectId,
        assignment.id,
        comment.id,
        designProjectId,
      );
      toast({ title: t("designProjects.team.comments.deleted") });
      setPendingDelete(null);
      await load();
    } catch (deleteError) {
      setDeleteError(translateError(t, uiError(deleteError, t("common.error"))));
    } finally {
      setDeleting(false);
    }
  };

  const formatWhen = (value: string) =>
    new Intl.DateTimeFormat(lang, { dateStyle: "medium", timeStyle: "short" }).format(
      new Date(value),
    );

  return (
    <div className="mt-2">
      <AlertDialog
        open={pendingDelete !== null}
        onOpenChange={(open) => {
          if (!open && !deleting) setPendingDelete(null);
        }}
      >
        <AlertDialogContent
          className="max-h-[90vh] overflow-y-auto"
          onCloseAutoFocus={(event) => {
            event.preventDefault();
            const target = deleteTriggerRef.current?.isConnected
              ? deleteTriggerRef.current
              : commentsToggleRef.current;
            target?.focus();
          }}
        >
          <AlertDialogHeader>
            <AlertDialogTitle>{t("designProjects.team.comments.delete")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("designProjects.team.comments.confirmDelete")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <p className="whitespace-pre-wrap break-words text-sm">{pendingDelete?.body}</p>
          {deleteError && (
            <p role="alert" className="text-sm text-destructive">
              {deleteError}
            </p>
          )}
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleting}>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              disabled={deleting}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={(event) => {
                event.preventDefault();
                if (pendingDelete) void remove(pendingDelete);
              }}
            >
              {deleting ? t("common.loading") : t("common.delete")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
      <Button
        ref={commentsToggleRef}
        type="button"
        variant="ghost"
        size="sm"
        className="px-2"
        aria-expanded={open}
        aria-controls={open ? `assignment-comments-${assignment.id}` : undefined}
        onClick={() => setOpen((current) => !current)}
      >
        <MessageSquare className="mr-1.5 h-4 w-4" />
        {t("designProjects.team.comments.title")}
        {items.length > 0 ? ` (${items.length})` : ""}
      </Button>
      {open ? (
        <div
          id={`assignment-comments-${assignment.id}`}
          className="mt-2 space-y-3 rounded-md bg-muted/50 p-3"
        >
          {loading ? (
            <p className="text-sm text-muted-foreground">{t("common.loading")}</p>
          ) : loadError ? (
            <div className="space-y-2">
              <p role="alert" className="text-sm text-rose-600">
                {translateError(t, loadError)}
              </p>
              <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
                {t("common.retry")}
              </Button>
            </div>
          ) : items.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              {t("designProjects.team.comments.empty")}
            </p>
          ) : (
            <ul className="space-y-3">
              {items.map((comment) => (
                <li key={comment.id} className="flex gap-2">
                  <div className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-muted text-xs font-semibold text-muted-foreground">
                    {comment.createdByName.trim().charAt(0).toUpperCase() || "?"}
                  </div>
                  <div className="min-w-0 flex-1 rounded-md border bg-card px-3 py-2">
                    <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
                      <span className="text-sm font-medium text-foreground">
                        {comment.createdByName}
                      </span>
                      <span className="text-xs text-muted-foreground">
                        {formatWhen(comment.createdAt)}
                      </span>
                    </div>
                    <p className="mt-1 whitespace-pre-wrap break-words text-sm text-foreground">
                      {comment.body}
                    </p>
                  </div>
                  {canManage || comment.createdByUserId === currentUserId ? (
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      className="h-8 w-8 shrink-0"
                      aria-label={t("designProjects.team.comments.delete")}
                      onClick={(event) => {
                        deleteTriggerRef.current = event.currentTarget;
                        setDeleteError(null);
                        setPendingDelete(comment);
                      }}
                    >
                      <Trash2 className="h-4 w-4 text-rose-600" />
                    </Button>
                  ) : null}
                </li>
              ))}
            </ul>
          )}
          <form onSubmit={submit} className="space-y-2">
            <Textarea
              value={body}
              onChange={(event) => setBody(event.target.value)}
              placeholder={t("designProjects.team.comments.placeholder")}
              maxLength={2000}
              rows={2}
              disabled={saving}
              aria-label={t("designProjects.team.comments.placeholder")}
            />
            {error && (
              <p role="alert" className="text-sm text-rose-600">
                {error}
              </p>
            )}
            <div className="flex justify-end">
              <Button type="submit" size="sm" disabled={saving || !body.trim()}>
                {saving ? (
                  <Loader2 className="mr-1 h-4 w-4 animate-spin" />
                ) : (
                  <Send className="mr-1 h-4 w-4" />
                )}
                {saving
                  ? t("designProjects.team.comments.submitting")
                  : t("designProjects.team.comments.submit")}
              </Button>
            </div>
          </form>
        </div>
      ) : null}
    </div>
  );
};

export const OperationalProjectTeamPanel = ({
  operationalProjectId,
  refreshKey,
  designProjectContext = false,
  designProjectId,
  designManagerUserId,
  designLeadUserId,
  onHistoryChange,
  onProjectMayHaveChanged,
}: Props) => {
  const uiError = useUiError();
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const [team, setTeam] = useState<OperationalProjectTeamResponse | null>(null);
  const [history, setHistory] = useState<OperationalProjectTeamHistoryResponse[]>([]);
  const [candidates, setCandidates] = useState<ProjectMemberCandidateResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [memberDialog, setMemberDialog] = useState<MemberDialogMode | null>(null);
  const [editingMember, setEditingMember] = useState<OperationalProjectMemberResponse | null>(null);
  const [permissionMember, setPermissionMember] = useState<OperationalProjectMemberResponse | null>(
    null,
  );
  const [permissionForm, setPermissionForm] = useState<DesignProjectMemberPermissionsResponse>({
    canView: true,
    canCreate: false,
    canUpdate: false,
    canDelete: false,
  });
  const [permissionSaving, setPermissionSaving] = useState(false);
  const [memberForm, setMemberForm] = useState<MemberForm>(emptyMemberForm());
  const [assignmentDialog, setAssignmentDialog] = useState<AssignmentDialogMode | null>(null);
  const [editingAssignment, setEditingAssignment] =
    useState<OperationalProjectAssignmentResponse | null>(null);
  const [assignmentForm, setAssignmentForm] = useState<AssignmentForm>(emptyAssignmentForm());
  const [terminalAction, setTerminalAction] = useState<{
    assignment: OperationalProjectAssignmentResponse;
    status: TerminalAssignmentStatus;
  } | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const memberIdempotencyKey = useRef("");
  const assignmentIdempotencyKey = useRef("");
  const load = useCallback(async () => {
    if (!operationalProjectId) {
      setLoading(false);
      setTeam(null);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const [teamResponse, historyResponse] = await Promise.all([
        adminApi.getOperationalProjectTeam(operationalProjectId, designProjectId),
        adminApi.getOperationalProjectTeamHistory(operationalProjectId, designProjectId),
      ]);
      setTeam(teamResponse.data);
      setHistory(historyResponse.data ?? []);
      onHistoryChange?.(operationalProjectId, historyResponse.data ?? []);
      if (teamResponse.data.canManage) {
        const candidateResponse = await adminApi.getOperationalProjectTeamCandidates(
          operationalProjectId,
          designProjectId,
        );
        setCandidates(candidateResponse.data ?? []);
      } else setCandidates([]);
    } catch (loadError) {
      setError(uiError(loadError));
    } finally {
      setLoading(false);
    }
  }, [operationalProjectId, designProjectId, onHistoryChange, uiError]);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);
  const activeMembers = useMemo(
    () => team?.members.filter((member) => member.isActive) ?? [],
    [team],
  );
  const currentDesignManagerUserId =
    designManagerUserId ??
    activeMembers.find((member) =>
      member.roles.some(
        (role) =>
          role.roleCode === "ProjectManager" &&
          role.scope === "Module" &&
          role.scopeValue === "Design" &&
          !role.endedAt,
      ),
    )?.userId;
  // Assigned dossier owners always have full rights on the server.
  const isDesignOwner = (userId: number) =>
    designProjectContext &&
    (userId === currentDesignManagerUserId ||
      (designLeadUserId != null && userId === designLeadUserId));
  const ownerPermissions: DesignProjectMemberPermissionsResponse = {
    canView: true,
    canCreate: true,
    canUpdate: true,
    canDelete: true,
  };
  const endedMembers = useMemo(
    () => team?.members.filter((member) => !member.isActive) ?? [],
    [team],
  );
  const availableCandidates = useMemo(() => {
    const activeUserIds = new Set(activeMembers.map((member) => member.userId));
    return candidates.filter((candidate) => !activeUserIds.has(candidate.userId));
  }, [activeMembers, candidates]);
  const formatDate = (value?: string | null, withTime = false) => {
    if (!value) return t("designProjects.team.value.none");
    return new Intl.DateTimeFormat(
      lang,
      withTime ? { dateStyle: "medium", timeStyle: "short" } : { dateStyle: "medium" },
    ).format(new Date(value));
  };
  const roleLabel = (code: string, scope?: string, scopeValue?: string | null) =>
    code === "ProjectManager" && scope === "Module" && scopeValue === "Design"
      ? t("designProjects.team.role.DesignManager")
      : t(`designProjects.team.role.${code}`);
  const scopeLabel = (scope: string) => t(`designProjects.team.scope.${scope}`);
  const scopeValueLabel = (scope: string, value: string | null | undefined) => {
    const raw = value?.trim() ?? "";
    if (!raw) return "";
    const key =
      scope === "Module"
        ? `designProjects.team.module.${raw}`
        : scope === "Discipline"
          ? `masterData.design_discipline.${raw}.label`
          : "";
    if (!key) return raw;
    const label = t(key);
    return label === key ? raw : label;
  };
  const isDesignRole = (role: ProjectMemberRoleRequest) =>
    role.scope === "Module" && role.scopeValue === "Design";
  const renderPermissions = (permissions?: DesignProjectMemberPermissionsResponse | null) => {
    if (!permissions) return null;
    const items = [
      ["view", permissions.canView],
      ["create", permissions.canCreate],
      ["update", permissions.canUpdate],
      ["delete", permissions.canDelete],
    ] as const;
    return (
      <div className="mt-1.5 flex flex-wrap gap-1">
        {items.map(([key, enabled]) => (
          <Badge
            key={key}
            variant="outline"
            className={
              enabled
                ? "whitespace-nowrap border-emerald-200 bg-emerald-50 text-emerald-700"
                : "whitespace-nowrap text-muted-foreground opacity-50"
            }
          >
            {t(`designProjects.team.permissions.${key}`)}
          </Badge>
        ))}
      </div>
    );
  };

  const createsReportingCycle = (memberId: number, managerId: number | null | undefined) => {
    let current = managerId ?? null;
    const visited = new Set<number>();
    while (current && !visited.has(current)) {
      if (current === memberId) return true;
      visited.add(current);
      current = team?.members.find((member) => member.id === current)?.reportsToMemberId ?? null;
    }
    return false;
  };

  const openAddMember = () => {
    const definition = team?.roleDefinitions[0];
    const form = emptyMemberForm();
    if (definition)
      form.roles = [
        designProjectContext
          ? { roleCode: "", scope: "Module", scopeValue: "Design" }
          : {
              roleCode: definition.code,
              scope: definition.allowedScopes[0] ?? "",
              scopeValue: null,
            },
      ];
    setEditingMember(null);
    setMemberForm(form);
    setFormError(null);
    memberIdempotencyKey.current = newIdempotencyKey();
    setMemberDialog("add");
  };
  const openMember = (
    member: OperationalProjectMemberResponse,
    mode: Exclude<MemberDialogMode, "add">,
  ) => {
    setEditingMember(member);
    setMemberForm({
      userId: member.userId,
      position: member.position,
      reportsToMemberId: member.reportsToMemberId ?? null,
      startedAt: toDateValue(member.startedAt),
      endedAt: mode === "end" ? today() : toDateValue(member.endedAt) || null,
      roles: member.roles.map((role) => ({
        roleCode: role.roleCode,
        scope: role.scope,
        scopeValue: role.scopeValue ?? null,
      })),
      rowVersion: member.rowVersion,
    });
    setFormError(null);
    memberIdempotencyKey.current = newIdempotencyKey();
    setMemberDialog(mode);
  };
  const openPermissions = (member: OperationalProjectMemberResponse) => {
    setFormError(null);
    setPermissionMember(member);
    setPermissionForm(
      member.designPermissions ?? {
        canView: true,
        canCreate: false,
        canUpdate: false,
        canDelete: false,
      },
    );
  };
  const savePermissions = async () => {
    if (!permissionMember || !operationalProjectId) return;
    if (
      !permissionForm.canView &&
      (permissionForm.canCreate || permissionForm.canUpdate || permissionForm.canDelete)
    ) {
      setFormError(t("designProjects.team.permissions.viewRequired"));
      return;
    }
    setPermissionSaving(true);
    setFormError(null);
    try {
      await adminApi.updateOperationalProjectMemberDesignPermissions(
        operationalProjectId,
        permissionMember.id,
        permissionForm,
      );
      toast({ title: t("designProjects.team.toast.permissionsSaved") });
      setPermissionMember(null);
      await load();
      onProjectMayHaveChanged?.();
    } catch (saveError) {
      toast({
        variant: "destructive",
        title: t("common.error"),
        description: translateError(t, uiError(saveError)),
      });
    } finally {
      setPermissionSaving(false);
    }
  };
  const updateRole = (index: number, patch: Partial<ProjectMemberRoleRequest>) =>
    setMemberForm((current) => ({
      ...current,
      roles: current.roles.map((role, roleIndex) =>
        roleIndex === index ? { ...role, ...patch } : role,
      ),
    }));

  const validateMember = () => {
    const position = memberForm.position.trim();
    if (!memberForm.userId) return t("designProjects.team.validation.userRequired");
    if (position.length < 2 || position.length > 150)
      return t("designProjects.team.validation.positionLength");
    if (!memberForm.startedAt) return t("designProjects.team.validation.startRequired");
    if (memberDialog === "end" && !memberForm.endedAt)
      return t("designProjects.team.validation.endRequired");
    if (memberForm.endedAt && memberForm.endedAt < memberForm.startedAt)
      return t("designProjects.team.validation.memberDates");
    if (!editingMember && activeMembers.some((member) => member.userId === memberForm.userId))
      return t("designProjects.team.validation.userAlreadyActive");
    if (!memberForm.roles.length) return t("designProjects.team.validation.roleRequired");
    if (
      designProjectContext &&
      currentDesignManagerUserId &&
      currentDesignManagerUserId !== memberForm.userId &&
      memberForm.roles.some((role) => role.roleCode === "ProjectManager" && isDesignRole(role))
    )
      return t("designProjects.team.validation.managerAlreadyAssigned");
    const roleKeys = new Set<string>();
    for (const role of memberForm.roles) {
      const definition = team?.roleDefinitions.find((item) => item.code === role.roleCode);
      if (!definition || !definition.allowedScopes.includes(role.scope))
        return t("designProjects.team.validation.roleInvalid");
      if (
        designProjectContext &&
        !isDesignRole(role) &&
        !editingMember?.roles.some(
          (existing) =>
            existing.roleCode === role.roleCode &&
            existing.scope === role.scope &&
            existing.scopeValue === role.scopeValue,
        )
      )
        return t("designProjects.team.validation.roleInvalid");
      const scopeValue = role.scopeValue?.trim() ?? "";
      if (role.scope === "Project" && scopeValue)
        return t("designProjects.team.validation.projectScopeValue");
      if (role.scope !== "Project" && (!scopeValue || scopeValue.length > 80))
        return t("designProjects.team.validation.scopeValue");
      const key = `${role.roleCode}|${role.scope}|${scopeValue}`.toLowerCase();
      if (roleKeys.has(key)) return t("designProjects.team.validation.duplicateRole");
      roleKeys.add(key);
    }
    if (editingMember && memberForm.reportsToMemberId === editingMember.id)
      return t("designProjects.team.validation.selfManager");
    if (editingMember && createsReportingCycle(editingMember.id, memberForm.reportsToMemberId))
      return t("designProjects.team.validation.managerCycle");
    if (
      memberForm.reportsToMemberId &&
      !activeMembers.some((member) => member.id === memberForm.reportsToMemberId)
    )
      return t("designProjects.team.validation.managerActive");
    return null;
  };
  const saveMember = async () => {
    if (!operationalProjectId) return;
    const validationError = validateMember();
    if (validationError) {
      setFormError(validationError);
      return;
    }
    setSaving(true);
    setFormError(null);
    const request: UpsertOperationalProjectMemberRequest = {
      ...memberForm,
      designProjectId,
      position: memberForm.position.trim(),
      startedAt: toUtcDate(memberForm.startedAt)!,
      endedAt: toUtcDate(memberForm.endedAt),
      roles: memberForm.roles.map((role) => ({
        ...role,
        scopeValue: role.scope === "Project" ? null : optional(role.scopeValue),
      })),
    };
    try {
      if (editingMember)
        await adminApi.updateOperationalProjectMember(
          operationalProjectId,
          editingMember.id,
          request,
          memberIdempotencyKey.current,
        );
      else
        await adminApi.addOperationalProjectMember(
          operationalProjectId,
          request,
          memberIdempotencyKey.current,
        );
      toast({
        title: t(
          memberDialog === "end"
            ? "designProjects.team.toast.memberEnded"
            : "designProjects.team.toast.memberSaved",
        ),
      });
      setMemberDialog(null);
      await load();
      onProjectMayHaveChanged?.();
    } catch (saveError) {
      setFormError(translateError(t, uiError(saveError)));
    } finally {
      setSaving(false);
    }
  };

  const openAddAssignment = () => {
    setEditingAssignment(null);
    setAssignmentForm(emptyAssignmentForm());
    setFormError(null);
    assignmentIdempotencyKey.current = newIdempotencyKey();
    setAssignmentDialog("add");
  };
  const openAddSubtask = (parent: OperationalProjectAssignmentResponse) => {
    setEditingAssignment(null);
    setAssignmentForm({
      ...emptyAssignmentForm(),
      module: parent.module,
      discipline: parent.discipline ?? null,
      parallelGroup: parent.parallelGroup ?? null,
      assigneeMemberId: parent.assigneeMemberId,
      parentAssignmentId: parent.id,
    });
    setFormError(null);
    assignmentIdempotencyKey.current = newIdempotencyKey();
    setAssignmentDialog("add");
  };
  const openEditAssignment = (assignment: OperationalProjectAssignmentResponse) => {
    setEditingAssignment(assignment);
    setAssignmentForm({
      workKey: assignment.workKey,
      title: assignment.title,
      module: assignment.module,
      discipline: assignment.discipline ?? null,
      parallelGroup: assignment.parallelGroup ?? null,
      parentAssignmentId: assignment.parentAssignmentId ?? null,
      assigneeMemberId: assignment.assigneeMemberId,
      managerMemberId: assignment.managerMemberId ?? null,
      status: assignment.status,
      plannedStart: toDateValue(assignment.plannedStart) || null,
      plannedEnd: toDateValue(assignment.plannedEnd) || null,
      note: assignment.note ?? null,
      rowVersion: assignment.rowVersion,
    });
    setFormError(null);
    assignmentIdempotencyKey.current = newIdempotencyKey();
    setAssignmentDialog("edit");
  };
  const validateAssignment = (form: AssignmentForm) => {
    const workKey = form.workKey.trim(),
      title = form.title.trim(),
      module = form.module.trim();
    if (workKey.length < 2 || workKey.length > 120)
      return t("designProjects.team.validation.workKeyLength");
    if (title.length < 2 || title.length > 300)
      return t("designProjects.team.validation.titleLength");
    if (module.length < 2 || module.length > 50)
      return t("designProjects.team.validation.moduleLength");
    if ((form.discipline?.trim().length ?? 0) > 50)
      return t("designProjects.team.validation.disciplineLength");
    if ((form.parallelGroup?.trim().length ?? 0) > 80)
      return t("designProjects.team.validation.parallelGroupLength");
    if ((form.note?.trim().length ?? 0) > 2000)
      return t("designProjects.team.validation.noteLength");
    if (!activeMembers.some((member) => member.id === form.assigneeMemberId))
      return t("designProjects.team.validation.assigneeActive");
    if (form.managerMemberId === form.assigneeMemberId)
      return t("designProjects.team.validation.assignmentSelfManager");
    if (form.managerMemberId && !activeMembers.some((member) => member.id === form.managerMemberId))
      return t("designProjects.team.validation.assignmentManagerActive");
    if (form.plannedStart && form.plannedEnd && form.plannedEnd < form.plannedStart)
      return t("designProjects.team.validation.assignmentDates");
    if (
      team?.assignments.some(
        (assignment) =>
          assignment.id !== editingAssignment?.id &&
          assignment.status !== "Reassigned" &&
          assignment.workKey.trim() === workKey &&
          assignment.assigneeMemberId === form.assigneeMemberId,
      )
    )
      return t("designProjects.team.validation.duplicateAssignment");
    return null;
  };
  const assignmentRequest = (form: AssignmentForm): UpsertOperationalProjectAssignmentRequest => ({
    ...form,
    designProjectId: designProjectId ?? null,
    workKey: form.workKey.trim(),
    title: form.title.trim(),
    module: form.module.trim(),
    discipline: optional(form.discipline),
    parallelGroup: optional(form.parallelGroup),
    plannedStart: toUtcDate(form.plannedStart),
    plannedEnd: toUtcDate(form.plannedEnd),
    note: optional(form.note),
  });
  const saveAssignment = async () => {
    if (!operationalProjectId) return;
    const validationError = validateAssignment(assignmentForm);
    if (validationError) {
      setFormError(validationError);
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      const request = assignmentRequest(assignmentForm);
      if (editingAssignment)
        await adminApi.updateOperationalProjectAssignment(
          operationalProjectId,
          editingAssignment.id,
          request,
          assignmentIdempotencyKey.current,
        );
      else
        await adminApi.addOperationalProjectAssignment(
          operationalProjectId,
          request,
          assignmentIdempotencyKey.current,
        );
      toast({ title: t("designProjects.team.toast.assignmentSaved") });
      setAssignmentDialog(null);
      await load();
      onProjectMayHaveChanged?.();
    } catch (saveError) {
      setFormError(uiError(saveError));
    } finally {
      setSaving(false);
    }
  };
  const runTerminalAction = async () => {
    if (!operationalProjectId || !terminalAction) return;
    const { assignment, status } = terminalAction;
    setSaving(true);
    setFormError(null);
    try {
      const request = assignmentRequest({
        workKey: assignment.workKey,
        title: assignment.title,
        module: assignment.module,
        discipline: assignment.discipline ?? null,
        parallelGroup: assignment.parallelGroup ?? null,
        parentAssignmentId: assignment.parentAssignmentId ?? null,
        assigneeMemberId: assignment.assigneeMemberId,
        managerMemberId: assignment.managerMemberId ?? null,
        status,
        plannedStart: toDateValue(assignment.plannedStart) || null,
        plannedEnd: toDateValue(assignment.plannedEnd) || null,
        note: assignment.note ?? null,
        rowVersion: assignment.rowVersion,
      });
      await adminApi.updateOperationalProjectAssignment(
        operationalProjectId,
        assignment.id,
        request,
        assignmentIdempotencyKey.current,
      );
      toast({
        title: t(
          status === "Completed"
            ? "designProjects.team.toast.assignmentCompleted"
            : "designProjects.team.toast.assignmentCancelled",
        ),
      });
      setTerminalAction(null);
      await load();
      onProjectMayHaveChanged?.();
    } catch (actionError) {
      setFormError(uiError(actionError));
    } finally {
      setSaving(false);
    }
  };
  const requestTerminalAction = (
    assignment: OperationalProjectAssignmentResponse,
    status: TerminalAssignmentStatus,
  ) => {
    assignmentIdempotencyKey.current = newIdempotencyKey();
    setFormError(null);
    setTerminalAction({ assignment, status });
  };
  const assignmentIds = new Set(team?.assignments.map((assignment) => assignment.id) ?? []);
  const rootAssignments =
    team?.assignments.filter(
      (assignment) =>
        !assignment.parentAssignmentId || !assignmentIds.has(assignment.parentAssignmentId),
    ) ?? [];
  const orderedAssignments = rootAssignments.flatMap((root) => [
    root,
    ...(team?.assignments.filter((item) => item.parentAssignmentId === root.id) ?? []),
  ]);

  if (loading)
    return (
      <div className="flex flex-col items-center justify-center gap-3 rounded-lg border bg-card py-16 text-sm text-muted-foreground">
        <Loader2 className="h-7 w-7 animate-spin text-muted-foreground" />
        <span>{t("designProjects.team.loading")}</span>
      </div>
    );
  if (!operationalProjectId)
    return (
      <section className="rounded-lg border border-dashed border-input bg-card p-8 text-center">
        <Users className="mx-auto h-8 w-8 text-muted-foreground" />
        <h2 className="mt-3 font-semibold text-foreground">
          {t("designProjects.team.unlinkedTitle")}
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          {t("designProjects.team.unlinkedDescription")}
        </p>
      </section>
    );
  if (error || !team)
    return (
      <div className="rounded-lg border border-rose-200 bg-rose-50 p-5 text-sm text-rose-700">
        <p>{error ?? t("common.error")}</p>
        <Button variant="outline" size="sm" className="mt-3" onClick={() => void load()}>
          <RefreshCw className="mr-1.5 h-3.5 w-3.5" />
          {t("common.retry")}
        </Button>
      </div>
    );

  return (
    <div className="space-y-4" data-testid="design-project-team-tab">
      <section className="rounded-lg border border-border bg-card p-4 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="flex items-center gap-2 text-base font-semibold text-foreground">
              <Users className="h-4 w-4 text-muted-foreground" />
              {t("designProjects.team.title")}
            </h2>
            <p className="mt-1 text-sm text-muted-foreground">
              {t("designProjects.team.description")}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <Badge variant="outline">
              {t("designProjects.team.memberTotal", { count: team.members.length })}
            </Badge>
            {team.canManage ? (
              <Button size="sm" onClick={openAddMember}>
                <Plus className="mr-1 h-4 w-4" />
                {t("designProjects.team.action.addMember")}
              </Button>
            ) : null}
          </div>
        </div>
        {activeMembers.length === 0 ? (
          <div className="mt-4 rounded-md border border-dashed border-input p-8 text-center text-sm text-muted-foreground">
            {t("designProjects.team.empty")}
          </div>
        ) : (
          <>
            <ul className="mt-4 grid grid-cols-1 gap-3 xl:hidden">
              {activeMembers.map((member) => (
                <li key={member.id} className="rounded-lg border border-border p-4">
                  <div
                    className={
                      designProjectContext
                        ? "flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between"
                        : "flex items-start justify-between gap-3"
                    }
                  >
                    <div className="flex min-w-0 items-start gap-3">
                      <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-muted text-muted-foreground">
                        <UserRound className="h-4 w-4" />
                      </div>
                      <div className="min-w-0">
                        <p
                          className={
                            designProjectContext
                              ? "break-words font-semibold text-foreground"
                              : "truncate font-semibold text-foreground"
                          }
                        >
                          {member.userName}
                        </p>
                        {designProjectContext ? (
                          <>
                            <p className="break-words text-sm text-muted-foreground">
                              {projectPositionLabel(member.position, t)}
                            </p>
                            <p className="break-all text-xs text-muted-foreground">
                              {member.email}
                            </p>
                          </>
                        ) : (
                          <p className="truncate text-sm text-muted-foreground">
                            {projectPositionLabel(member.position, t)} · {member.email}
                          </p>
                        )}
                      </div>
                    </div>
                    {team.canManage ? (
                      <div
                        className={
                          designProjectContext
                            ? "flex shrink-0 justify-end gap-1"
                            : "flex shrink-0 gap-1"
                        }
                      >
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={t("designProjects.team.action.editMember")}
                          onClick={() => openMember(member, "edit")}
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                        {designProjectContext ? (
                          isDesignOwner(member.userId) ? (
                            <Button
                              variant="ghost"
                              size="icon"
                              disabled
                              aria-label={t("designProjects.team.permissions.ownerLocked")}
                              title={t("designProjects.team.permissions.ownerLocked")}
                            >
                              <ShieldCheck className="h-4 w-4 text-muted-foreground/60" />
                            </Button>
                          ) : (
                            <Button
                              variant="ghost"
                              size="icon"
                              aria-label={t("designProjects.team.action.permissions")}
                              onClick={() => openPermissions(member)}
                            >
                              <ShieldCheck className="h-4 w-4" />
                            </Button>
                          )
                        ) : null}
                        {(!designProjectContext ||
                          (member.roles.length > 0 && member.roles.every(isDesignRole))) && (
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={t("designProjects.team.action.endMember")}
                            onClick={() => openMember(member, "end")}
                          >
                            <UserRoundX className="h-4 w-4 text-rose-600" />
                          </Button>
                        )}
                      </div>
                    ) : null}
                  </div>
                  <div className="mt-3 flex flex-wrap gap-2">
                    {member.roles.map((role, index) => {
                      const definition = team.roleDefinitions.find(
                        (item) => item.code === role.roleCode,
                      );
                      return (
                        <Badge
                          key={`${role.roleCode}-${role.scope}-${role.scopeValue ?? index}`}
                          variant="outline"
                          className="h-auto whitespace-normal rounded-md bg-muted/30 px-2 py-1 text-left leading-snug"
                        >
                          {roleLabel(role.roleCode, role.scope, role.scopeValue)} ·{" "}
                          {definition?.raci ?? t("designProjects.team.value.none")} ·{" "}
                          {scopeLabel(role.scope)}
                          {role.scopeValue
                            ? `: ${scopeValueLabel(role.scope, role.scopeValue)}`
                            : ""}
                        </Badge>
                      );
                    })}
                  </div>
                  {renderPermissions(
                    isDesignOwner(member.userId) ? ownerPermissions : member.designPermissions,
                  )}
                  <dl className="mt-3 grid grid-cols-1 gap-2 text-sm sm:grid-cols-2">
                    <div>
                      <dt className="text-xs text-muted-foreground">
                        {t("designProjects.team.field.reportsTo")}
                      </dt>
                      <dd>{member.reportsToName ?? t("designProjects.team.value.none")}</dd>
                    </div>
                    <div>
                      <dt className="text-xs text-muted-foreground">
                        {t("designProjects.team.field.memberDates")}
                      </dt>
                      <dd>{formatDate(member.startedAt)}</dd>
                    </div>
                  </dl>
                </li>
              ))}
            </ul>
            <div className="mt-4 hidden overflow-x-auto rounded-lg border border-border xl:block">
              <table className="min-w-full text-sm">
                <thead className="bg-muted/30 text-left text-xs uppercase tracking-wide text-muted-foreground">
                  <tr>
                    <th className="px-3 py-2">{t("designProjects.team.field.user")}</th>
                    <th className="px-3 py-2">{t("designProjects.team.field.position")}</th>
                    <th className="px-3 py-2">
                      {t(
                        designProjectContext
                          ? "designProjects.team.field.designRoles"
                          : "designProjects.team.field.roles",
                      )}
                    </th>
                    <th className="px-3 py-2">{t("designProjects.team.field.reportsTo")}</th>
                    <th className="px-3 py-2">{t("designProjects.team.field.memberDates")}</th>
                    {team.canManage ? (
                      <th className="px-3 py-2 text-right">{t("common.actions")}</th>
                    ) : null}
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {activeMembers.map((member) => (
                    <tr key={member.id}>
                      <td className="px-3 py-3">
                        <div className="font-medium text-foreground">{member.userName}</div>
                        <div className="text-xs text-muted-foreground">{member.email}</div>
                      </td>
                      <td className="px-3 py-3 text-foreground">
                        {projectPositionLabel(member.position, t)}
                      </td>
                      <td className="min-w-[18rem] px-3 py-3">
                        <div className="flex flex-wrap gap-1.5">
                          {member.roles.map((role, index) => {
                            const definition = team.roleDefinitions.find(
                              (item) => item.code === role.roleCode,
                            );
                            return (
                              <Badge
                                key={`${role.roleCode}-${role.scope}-${role.scopeValue ?? index}`}
                                variant="outline"
                                className="h-auto whitespace-normal rounded-md bg-muted/30 px-2 py-1 text-left leading-snug"
                              >
                                {roleLabel(role.roleCode, role.scope, role.scopeValue)} ·{" "}
                                {definition?.raci ?? t("designProjects.team.value.none")} ·{" "}
                                {scopeLabel(role.scope)}
                                {role.scopeValue
                                  ? `: ${scopeValueLabel(role.scope, role.scopeValue)}`
                                  : ""}
                              </Badge>
                            );
                          })}
                        </div>
                        {renderPermissions(
                          isDesignOwner(member.userId)
                            ? ownerPermissions
                            : member.designPermissions,
                        )}
                      </td>
                      <td className="px-3 py-3 text-foreground">
                        {member.reportsToName ?? t("designProjects.team.value.none")}
                      </td>
                      <td className="whitespace-nowrap px-3 py-3 text-foreground">
                        {formatDate(member.startedAt)}
                      </td>
                      {team.canManage ? (
                        <td className="px-3 py-3">
                          <div className="flex justify-end gap-1">
                            <Button
                              variant="ghost"
                              size="icon"
                              aria-label={t("designProjects.team.action.editMember")}
                              onClick={() => openMember(member, "edit")}
                            >
                              <Pencil className="h-4 w-4" />
                            </Button>
                            {designProjectContext ? (
                              isDesignOwner(member.userId) ? (
                                <Button
                                  variant="ghost"
                                  size="icon"
                                  disabled
                                  aria-label={t("designProjects.team.permissions.ownerLocked")}
                                  title={t("designProjects.team.permissions.ownerLocked")}
                                >
                                  <ShieldCheck className="h-4 w-4 text-muted-foreground/60" />
                                </Button>
                              ) : (
                                <Button
                                  variant="ghost"
                                  size="icon"
                                  aria-label={t("designProjects.team.action.permissions")}
                                  onClick={() => openPermissions(member)}
                                >
                                  <ShieldCheck className="h-4 w-4" />
                                </Button>
                              )
                            ) : null}
                            {(!designProjectContext ||
                              (member.roles.length > 0 && member.roles.every(isDesignRole))) && (
                              <Button
                                variant="ghost"
                                size="icon"
                                aria-label={t("designProjects.team.action.endMember")}
                                onClick={() => openMember(member, "end")}
                              >
                                <UserRoundX className="h-4 w-4 text-rose-600" />
                              </Button>
                            )}
                          </div>
                        </td>
                      ) : null}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
        {endedMembers.length ? (
          <div className="mt-5 border-t pt-4">
            <h3 className="text-sm font-semibold text-foreground">
              {t("designProjects.team.endedMembers")}
            </h3>
            <div className="mt-2 grid gap-2 md:grid-cols-2">
              {endedMembers.map((member) => (
                <div
                  key={member.id}
                  className="flex items-center justify-between rounded-md bg-muted/30 p-3 text-sm text-muted-foreground"
                >
                  <span>
                    <strong className="text-foreground">{member.userName}</strong> ·{" "}
                    {projectPositionLabel(member.position, t)}
                  </span>
                  <span>{formatDate(member.endedAt)}</span>
                </div>
              ))}
            </div>
          </div>
        ) : null}
      </section>

      <section className="rounded-lg border border-border bg-card p-4 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="flex items-center gap-2 font-semibold text-foreground">
              <ClipboardList className="h-4 w-4 text-muted-foreground" />
              {t("designProjects.team.assignments.title")}
            </h2>
            <p className="mt-1 text-sm text-muted-foreground">
              {t("designProjects.team.assignments.overview")}
            </p>
          </div>
          {team.canManage ? (
            <Button size="sm" disabled={!activeMembers.length} onClick={openAddAssignment}>
              <Plus className="mr-1 h-4 w-4" />
              {t("designProjects.team.action.addAssignment")}
            </Button>
          ) : null}
        </div>
        {team.assignments.length === 0 ? (
          <div className="mt-4 rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">
            {t("designProjects.team.assignments.empty")}
          </div>
        ) : (
          <div className="mt-4 grid gap-3 xl:grid-cols-2">
            {orderedAssignments.map((assignment) => {
              const mutable = assignmentIsMutable(assignment);
              const isSubtask = !!assignment.parentAssignmentId;
              return (
                <article
                  key={assignment.id}
                  className={
                    isSubtask
                      ? "rounded-lg border border-l-4 border-border border-l-slate-300 p-4"
                      : "rounded-lg border border-border p-4"
                  }
                  data-subtask={isSubtask ? "true" : undefined}
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        {isSubtask ? (
                          <CornerDownRight className="h-4 w-4 text-muted-foreground" aria-hidden />
                        ) : null}
                        <strong className="text-foreground">{assignment.title}</strong>
                        <Badge variant="outline" className={assignmentBadge[assignment.status]}>
                          {t(`designProjects.team.assignmentStatus.${assignment.status}`)}
                        </Badge>
                      </div>
                      <p className="mt-1 break-all font-mono text-xs text-muted-foreground">
                        {assignment.workKey}
                      </p>
                    </div>
                    {team.canManage && mutable ? (
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={t("designProjects.team.action.editAssignment")}
                        onClick={() => openEditAssignment(assignment)}
                      >
                        <Pencil className="h-4 w-4" />
                      </Button>
                    ) : null}
                  </div>
                  <dl className="mt-3 grid grid-cols-1 gap-2 text-sm sm:grid-cols-2">
                    <div>
                      <dt className="text-xs text-muted-foreground">
                        {t("designProjects.team.field.assignee")}
                      </dt>
                      <dd>{assignment.assigneeName}</dd>
                    </div>
                    <div>
                      <dt className="text-xs text-muted-foreground">
                        {t("designProjects.team.field.assignmentManager")}
                      </dt>
                      <dd>{assignment.managerName ?? t("designProjects.team.value.none")}</dd>
                    </div>
                    <div>
                      <dt className="text-xs text-muted-foreground">
                        {t("designProjects.team.field.moduleDiscipline")}
                      </dt>
                      <dd>
                        {scopeValueLabel("Module", assignment.module)}
                        {assignment.discipline
                          ? ` · ${scopeValueLabel("Discipline", assignment.discipline)}`
                          : ""}
                      </dd>
                    </div>
                    <div>
                      <dt className="text-xs text-muted-foreground">
                        {t("designProjects.team.field.plannedDates")}
                      </dt>
                      <dd>
                        {formatDate(assignment.plannedStart)} – {formatDate(assignment.plannedEnd)}
                      </dd>
                    </div>
                    <div>
                      <dt className="text-xs text-muted-foreground">
                        {t("designProjects.team.field.parallelGroup")}
                      </dt>
                      <dd>{assignment.parallelGroup ?? t("designProjects.team.value.none")}</dd>
                    </div>
                  </dl>
                  {assignment.note ? (
                    <p className="mt-3 whitespace-pre-wrap rounded-md bg-muted/30 p-2 text-sm text-muted-foreground">
                      {assignment.note}
                    </p>
                  ) : null}
                  {team.canManage && mutable ? (
                    <div className="mt-3 flex flex-wrap gap-2 border-t pt-3">
                      {!isSubtask ? (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => openAddSubtask(assignment)}
                        >
                          <CornerDownRight className="mr-1 h-4 w-4" />
                          {t("designProjects.team.action.addSubtask")}
                        </Button>
                      ) : null}
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => requestTerminalAction(assignment, "Completed")}
                      >
                        <CheckCircle2 className="mr-1 h-4 w-4" />
                        {t("designProjects.team.action.completeAssignment")}
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        className="text-rose-600"
                        onClick={() => requestTerminalAction(assignment, "Cancelled")}
                      >
                        <XCircle className="mr-1 h-4 w-4" />
                        {t("designProjects.team.action.cancelAssignment")}
                      </Button>
                    </div>
                  ) : null}
                  {operationalProjectId ? (
                    <AssignmentAttachments
                      operationalProjectId={operationalProjectId}
                      assignment={assignment}
                      designProjectId={designProjectId}
                      canManage={team.canManage}
                    />
                  ) : null}
                  {operationalProjectId ? (
                    <AssignmentComments
                      operationalProjectId={operationalProjectId}
                      assignment={assignment}
                      designProjectId={designProjectId}
                      canManage={team.canManage}
                    />
                  ) : null}
                </article>
              );
            })}
          </div>
        )}
      </section>

      {!onHistoryChange && <ProjectTeamHistory history={history} />}

      <Dialog open={memberDialog !== null} onOpenChange={(open) => !open && setMemberDialog(null)}>
        <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {t(`designProjects.team.dialog.member.${memberDialog ?? "add"}.title`)}
            </DialogTitle>
            <DialogDescription>
              {t(
                designProjectContext && memberDialog !== "end"
                  ? `designProjects.team.dialog.member.design.${memberDialog ?? "add"}.description`
                  : `designProjects.team.dialog.member.${memberDialog ?? "add"}.description`,
              )}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <FieldScope className="sm:col-span-2">
              <Label>{t("designProjects.team.field.user")} *</Label>
              <SearchableSelect
                ariaLabel={t("designProjects.team.field.user")}
                disabled={memberDialog !== "add"}
                value={memberForm.userId ? String(memberForm.userId) : ""}
                onChange={(value) =>
                  setMemberForm((current) => ({ ...current, userId: Number(value) }))
                }
                placeholder={t("designProjects.team.placeholder.user")}
                options={
                  memberDialog !== "add" && editingMember
                    ? [personOption({ ...editingMember, name: editingMember.userName }, t)]
                    : availableCandidates.map((candidate) => personOption(candidate, t))
                }
              />
            </FieldScope>
            <FieldScope className="sm:col-span-2">
              <Label>{t("designProjects.team.field.position")} *</Label>
              <Input
                className="mt-1"
                maxLength={150}
                disabled={memberDialog === "end"}
                value={memberForm.position}
                onChange={(event) =>
                  setMemberForm((current) => ({ ...current, position: event.target.value }))
                }
              />
            </FieldScope>
            <FieldScope>
              <Label>{t("designProjects.team.field.startedAt")} *</Label>
              <Input
                className="mt-1"
                type="date"
                disabled={memberDialog === "end"}
                value={memberForm.startedAt}
                onChange={(event) =>
                  setMemberForm((current) => ({ ...current, startedAt: event.target.value }))
                }
              />
            </FieldScope>
            {(!designProjectContext || memberDialog === "end") && (
              <FieldScope>
                <Label>
                  {t("designProjects.team.field.endedAt")}
                  {memberDialog === "end" ? " *" : ""}
                </Label>
                <Input
                  className="mt-1"
                  type="date"
                  min={memberForm.startedAt}
                  disabled={memberDialog !== "end"}
                  value={memberForm.endedAt ?? ""}
                  onChange={(event) =>
                    setMemberForm((current) => ({
                      ...current,
                      endedAt: event.target.value || null,
                    }))
                  }
                />
              </FieldScope>
            )}
            <FieldScope className="sm:col-span-2">
              <Label>{t("designProjects.team.field.reportsTo")}</Label>
              <SearchableSelect
                disabled={memberDialog === "end"}
                value={memberForm.reportsToMemberId ? String(memberForm.reportsToMemberId) : "none"}
                onChange={(value) =>
                  setMemberForm((current) => ({
                    ...current,
                    reportsToMemberId: value === "none" ? null : Number(value),
                  }))
                }
                ariaLabel={t("designProjects.team.field.reportsTo")}
                placeholder={t("designProjects.team.field.reportsTo")}
                options={[
                  { value: "none", label: t("designProjects.team.value.none") },
                  ...activeMembers
                    .filter((member) => member.id !== editingMember?.id)
                    .map((member) => projectMemberOption(member, t)),
                ]}
              />
            </FieldScope>
            <div className="space-y-3 sm:col-span-2">
              <div
                className={
                  designProjectContext
                    ? "flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between"
                    : "flex items-center justify-between"
                }
              >
                <span>
                  {t(
                    designProjectContext
                      ? "designProjects.team.field.designRoles"
                      : "designProjects.team.field.roles",
                  )}{" "}
                  *
                </span>
                {memberDialog !== "end" ? (
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={() => {
                      const definition = team.roleDefinitions[0];
                      if (definition)
                        setMemberForm((current) => ({
                          ...current,
                          roles: [
                            ...current.roles,
                            {
                              roleCode: designProjectContext ? "" : definition.code,
                              scope: designProjectContext
                                ? "Module"
                                : (definition.allowedScopes[0] ?? ""),
                              scopeValue: designProjectContext ? "Design" : null,
                            },
                          ],
                        }));
                    }}
                  >
                    <Plus className="mr-1 h-4 w-4" />
                    {t("designProjects.team.action.addRole")}
                  </Button>
                ) : null}
              </div>
              {memberForm.roles.map((role, index) => {
                const definition = team.roleDefinitions.find((item) => item.code === role.roleCode);
                const preservedRole = designProjectContext && !isDesignRole(role);
                const scopeOptions =
                  role.scope === "Module" ? team.moduleOptions : team.disciplineOptions;
                return (
                  <div
                    key={index}
                    data-testid="design-team-role-row"
                    className={`grid gap-2 rounded-md border p-3 ${designProjectContext ? "grid-cols-[minmax(0,1fr)_auto] items-center sm:grid-cols-[minmax(0,1fr)_auto_auto]" : "sm:grid-cols-[1fr_1fr_1fr_auto]"}`}
                  >
                    {preservedRole ? (
                      <div className="text-sm">
                        {roleLabel(role.roleCode, role.scope, role.scopeValue)} ·{" "}
                        {scopeLabel(role.scope)}
                        {role.scopeValue ? `: ${scopeValueLabel(role.scope, role.scopeValue)}` : ""}
                      </div>
                    ) : (
                      <Select
                        disabled={memberDialog === "end"}
                        value={role.roleCode}
                        onValueChange={(value) => {
                          const next = team.roleDefinitions.find((item) => item.code === value);
                          updateRole(index, {
                            roleCode: value,
                            scope: designProjectContext ? "Module" : (next?.allowedScopes[0] ?? ""),
                            scopeValue: designProjectContext ? "Design" : null,
                          });
                        }}
                      >
                        <SelectTrigger aria-label={t("a11yUi.role")}>
                          <SelectValue
                            placeholder={
                              designProjectContext
                                ? t("designProjects.team.placeholder.role")
                                : undefined
                            }
                          />
                        </SelectTrigger>
                        <SelectContent>
                          {team.roleDefinitions
                            .filter(
                              (item) =>
                                !designProjectContext ||
                                item.code !== "ProjectManager" ||
                                !currentDesignManagerUserId ||
                                currentDesignManagerUserId === memberForm.userId,
                            )
                            .map((item) => (
                              <SelectItem key={item.code} value={item.code}>
                                {roleLabel(item.code, role.scope, role.scopeValue)} · {item.raci}
                              </SelectItem>
                            ))}
                        </SelectContent>
                      </Select>
                    )}
                    {designProjectContext && !preservedRole ? (
                      <span className="col-start-1 row-start-2 text-sm text-muted-foreground sm:col-start-2 sm:row-start-1">
                        {t("designProjects.team.scope.designModule")}
                      </span>
                    ) : null}
                    {!designProjectContext && (
                      <Select
                        disabled={memberDialog === "end"}
                        value={role.scope}
                        onValueChange={(value) =>
                          updateRole(index, { scope: value, scopeValue: null })
                        }
                      >
                        <SelectTrigger aria-label={t("a11yUi.scope")}>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {definition?.allowedScopes.map((scope) => (
                            <SelectItem key={scope} value={scope}>
                              {scopeLabel(scope)}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                    {!designProjectContext && (
                      <Select
                        disabled={memberDialog === "end" || role.scope === "Project"}
                        value={role.scopeValue ?? ""}
                        onValueChange={(value) => updateRole(index, { scopeValue: value })}
                      >
                        <SelectTrigger aria-label={t("a11yUi.scopeValue")}>
                          <SelectValue
                            placeholder={t("designProjects.team.placeholder.scopeValue")}
                          />
                        </SelectTrigger>
                        <SelectContent>
                          {scopeOptions.map((option) => (
                            <SelectItem key={option} value={option}>
                              {scopeValueLabel(role.scope, option)}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                    {memberDialog !== "end" && !preservedRole ? (
                      <Button
                        type="button"
                        size="icon"
                        variant="ghost"
                        className={
                          designProjectContext
                            ? "col-start-2 row-start-1 sm:col-start-3"
                            : undefined
                        }
                        aria-label={t("designProjects.team.action.removeRole")}
                        onClick={() =>
                          setMemberForm((current) => ({
                            ...current,
                            roles: current.roles.filter((_, roleIndex) => roleIndex !== index),
                          }))
                        }
                      >
                        <X className="h-4 w-4" />
                      </Button>
                    ) : null}
                  </div>
                );
              })}
            </div>
          </div>
          {formError ? <p className="text-sm font-medium text-rose-600">{formError}</p> : null}
          <DialogFooter>
            <Button variant="outline" disabled={saving} onClick={() => setMemberDialog(null)}>
              {t("common.cancel")}
            </Button>
            <Button
              variant={memberDialog === "end" ? "destructive" : "default"}
              disabled={saving}
              onClick={() => void saveMember()}
            >
              {saving && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}
              {t(
                memberDialog === "end"
                  ? "designProjects.team.action.confirmEndMember"
                  : "common.save",
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={permissionMember !== null}
        onOpenChange={(open) => !open && setPermissionMember(null)}
      >
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t("designProjects.team.dialog.permissions.title")}</DialogTitle>
            <DialogDescription>
              {permissionMember
                ? `${permissionMember.userName} · ${t("designProjects.team.dialog.permissions.description")}`
                : t("designProjects.team.dialog.permissions.description")}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-3 py-2">
            {(
              [
                ["view", permissionForm.canView, "canView"],
                ["create", permissionForm.canCreate, "canCreate"],
                ["update", permissionForm.canUpdate, "canUpdate"],
                ["delete", permissionForm.canDelete, "canDelete"],
              ] as const
            ).map(([key, checked, field]) => (
              <label
                key={key}
                className="flex items-center gap-3 rounded-md border border-border px-3 py-2 text-sm"
              >
                <Checkbox
                  checked={checked}
                  onCheckedChange={(value) =>
                    setPermissionForm((current) => ({ ...current, [field]: value === true }))
                  }
                />
                <span>{t(`designProjects.team.permissions.${key}`)}</span>
              </label>
            ))}
          </div>
          {formError ? <p className="text-sm font-medium text-rose-600">{formError}</p> : null}
          <DialogFooter>
            <Button
              variant="outline"
              disabled={permissionSaving}
              onClick={() => setPermissionMember(null)}
            >
              {t("common.cancel")}
            </Button>
            <Button disabled={permissionSaving} onClick={() => void savePermissions()}>
              {permissionSaving && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}
              {t("designProjects.team.action.savePermissions")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={assignmentDialog !== null}
        onOpenChange={(open) => !open && setAssignmentDialog(null)}
      >
        <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {t(`designProjects.team.dialog.assignment.${assignmentDialog ?? "add"}.title`)}
            </DialogTitle>
            <DialogDescription>
              {t("designProjects.team.dialog.assignment.handoverDescription")}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <FieldScope>
              <Label>{t("designProjects.team.field.workKey")} *</Label>
              <Input
                className="mt-1"
                maxLength={120}
                disabled={assignmentDialog === "edit"}
                value={assignmentForm.workKey}
                onChange={(event) =>
                  setAssignmentForm((current) => ({ ...current, workKey: event.target.value }))
                }
              />
            </FieldScope>
            <FieldScope>
              <Label>{t("designProjects.team.field.assignmentStatus")} *</Label>
              <Select
                value={assignmentForm.status}
                onValueChange={(value) =>
                  setAssignmentForm((current) => ({
                    ...current,
                    status: value as ProjectAssignmentStatus,
                  }))
                }
              >
                <SelectTrigger className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(["Planned", "InProgress"] as const).map((status) => (
                    <SelectItem key={status} value={status}>
                      {t(`designProjects.team.assignmentStatus.${status}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FieldScope>
            <FieldScope className="sm:col-span-2">
              <Label>{t("designProjects.team.field.assignmentTitle")} *</Label>
              <Input
                className="mt-1"
                maxLength={300}
                value={assignmentForm.title}
                onChange={(event) =>
                  setAssignmentForm((current) => ({ ...current, title: event.target.value }))
                }
              />
            </FieldScope>
            <FieldScope>
              <Label>{t("designProjects.team.field.module")} *</Label>
              <Select
                value={assignmentForm.module}
                onValueChange={(value) =>
                  setAssignmentForm((current) => ({ ...current, module: value }))
                }
              >
                <SelectTrigger className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {team.moduleOptions
                    .filter((option) => !designProjectId || option === "Design")
                    .map((option) => (
                      <SelectItem key={option} value={option}>
                        {scopeValueLabel("Module", option)}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </FieldScope>
            <FieldScope>
              <Label>{t("designProjects.team.field.discipline")}</Label>
              <Select
                value={assignmentForm.discipline ?? "none"}
                onValueChange={(value) =>
                  setAssignmentForm((current) => ({
                    ...current,
                    discipline: value === "none" ? null : value,
                  }))
                }
              >
                <SelectTrigger className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">{t("designProjects.team.value.none")}</SelectItem>
                  {team.disciplineOptions.map((option) => (
                    <SelectItem key={option} value={option}>
                      {scopeValueLabel("Discipline", option)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FieldScope>
            <FieldScope>
              <Label>{t("designProjects.team.field.assignee")} *</Label>
              <SearchableSelect
                value={
                  assignmentForm.assigneeMemberId ? String(assignmentForm.assigneeMemberId) : ""
                }
                onChange={(value) =>
                  setAssignmentForm((current) => {
                    const assigneeMemberId = Number(value);
                    return {
                      ...current,
                      assigneeMemberId,
                      managerMemberId:
                        current.managerMemberId === assigneeMemberId
                          ? null
                          : current.managerMemberId,
                    };
                  })
                }
                ariaLabel={t("designProjects.team.field.assignee")}
                placeholder={t("designProjects.team.field.assignee")}
                options={[...activeMembers.map((member) => projectMemberOption(member, t))]}
              />
            </FieldScope>
            <FieldScope>
              <Label>{t("designProjects.team.field.assignmentManager")}</Label>
              <SearchableSelect
                value={
                  assignmentForm.managerMemberId ? String(assignmentForm.managerMemberId) : "none"
                }
                onChange={(value) =>
                  setAssignmentForm((current) => ({
                    ...current,
                    managerMemberId: value === "none" ? null : Number(value),
                  }))
                }
                ariaLabel={t("designProjects.team.field.assignmentManager")}
                placeholder={t("designProjects.team.field.assignmentManager")}
                options={[
                  { value: "none", label: t("designProjects.team.value.none") },
                  ...activeMembers
                    .filter((member) => member.id !== assignmentForm.assigneeMemberId)
                    .map((member) => projectMemberOption(member, t)),
                ]}
              />
            </FieldScope>
            <FieldScope>
              <Label>{t("designProjects.team.field.plannedStart")}</Label>
              <Input
                className="mt-1"
                type="date"
                value={assignmentForm.plannedStart ?? ""}
                onChange={(event) =>
                  setAssignmentForm((current) => ({
                    ...current,
                    plannedStart: event.target.value || null,
                  }))
                }
              />
            </FieldScope>
            <FieldScope>
              <Label>{t("designProjects.team.field.plannedEnd")}</Label>
              <Input
                className="mt-1"
                type="date"
                min={assignmentForm.plannedStart ?? undefined}
                value={assignmentForm.plannedEnd ?? ""}
                onChange={(event) =>
                  setAssignmentForm((current) => ({
                    ...current,
                    plannedEnd: event.target.value || null,
                  }))
                }
              />
            </FieldScope>
            <FieldScope className="sm:col-span-2">
              <Label>{t("designProjects.team.field.parallelGroup")}</Label>
              <Input
                className="mt-1"
                maxLength={80}
                value={assignmentForm.parallelGroup ?? ""}
                onChange={(event) =>
                  setAssignmentForm((current) => ({
                    ...current,
                    parallelGroup: event.target.value,
                  }))
                }
              />
            </FieldScope>
            <FieldScope className="sm:col-span-2">
              <Label>{t("designProjects.team.field.note")}</Label>
              <Textarea
                className="mt-1"
                maxLength={2000}
                value={assignmentForm.note ?? ""}
                onChange={(event) =>
                  setAssignmentForm((current) => ({ ...current, note: event.target.value }))
                }
              />
            </FieldScope>
          </div>
          {formError ? <p className="text-sm font-medium text-rose-600">{formError}</p> : null}
          <DialogFooter>
            <Button variant="outline" disabled={saving} onClick={() => setAssignmentDialog(null)}>
              {t("common.cancel")}
            </Button>
            <Button disabled={saving} onClick={() => void saveAssignment()}>
              {saving && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={terminalAction !== null}
        onOpenChange={(open) => !open && setTerminalAction(null)}
      >
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>
              {t(
                terminalAction?.status === "Completed"
                  ? "designProjects.team.dialog.complete.title"
                  : "designProjects.team.dialog.cancel.title",
              )}
            </DialogTitle>
            <DialogDescription>
              {t(
                terminalAction?.status === "Completed"
                  ? "designProjects.team.dialog.complete.description"
                  : "designProjects.team.dialog.cancel.description",
                { title: terminalAction?.assignment.title ?? "" },
              )}
            </DialogDescription>
          </DialogHeader>
          {formError ? <p className="text-sm font-medium text-rose-600">{formError}</p> : null}
          <DialogFooter>
            <Button variant="outline" disabled={saving} onClick={() => setTerminalAction(null)}>
              {t("common.cancel")}
            </Button>
            <Button
              variant={terminalAction?.status === "Cancelled" ? "destructive" : "default"}
              disabled={saving}
              onClick={() => void runTerminalAction()}
            >
              {saving && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}
              {t("common.confirm")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
};
