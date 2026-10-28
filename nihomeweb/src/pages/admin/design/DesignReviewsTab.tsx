import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  AlertCircle,
  Check,
  Download,
  FileCheck2,
  Loader2,
  MessageSquare,
  Plus,
  RefreshCw,
  Send,
  UserCheck,
  Users,
} from "lucide-react";
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
import { LocalizedDateInput, LocalizedFileInput } from "@/components/ui/localized-inputs";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { newIdempotencyKey } from "@/lib/api";
import { extractApiError, extractDownloadError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { useAppSelector } from "@/store";
import { usePermissions } from "@/hooks/usePermissions";
import {
  adminApi,
  type DesignProjectResponse,
  type DesignReviewResponse,
  type DesignReviewStage,
  type ConceptRevisionResponse,
  type ConceptOptionResponse,
  type ProjectDocumentResponse,
} from "@/services/adminApi";

type Props = (
  | { project: DesignProjectResponse; recipientProjectId?: never }
  | { project?: never; recipientProjectId: number }
) & { focusedReviewId?: number };
const categoryByStage: Record<DesignReviewStage, string> = {
  Concept: "DesignConcept",
  BasicDesign: "DesignBasic",
  ShopDrawing: "DesignShopDrawing",
};
const sourceTypeByStage: Record<DesignReviewStage, string> = {
  Concept: "ConceptOption",
  BasicDesign: "BasicDesignDoc",
  ShopDrawing: "ShopDrawing",
};

export const DesignReviewsTab = ({ project, recipientProjectId, focusedReviewId }: Props) => {
  const projectId = project?.id ?? recipientProjectId ?? 0;
  const operationalProjectId = project?.operationalProjectId;
  const { t, lang } = useI18n();
  const currentUserId = useAppSelector((state) => state.auth.user?.userId ?? null);
  const { has } = usePermissions();
  const isDesignManager =
    Boolean(project) &&
    (currentUserId === (project?.designManagerUserId ?? project?.projectManagerUserId) ||
      has("operations.projects.view.all"));
  const [reviews, setReviews] = useState<DesignReviewResponse[]>([]);
  const [recipientCandidates, setRecipientCandidates] = useState<
    Array<{ userId: number; name: string }>
  >([]);
  const [documents, setDocuments] = useState<ProjectDocumentResponse[]>([]);
  const [conceptOptions, setConceptOptions] = useState<ConceptOptionResponse[]>([]);
  const [conceptRevisions, setConceptRevisions] = useState<ConceptRevisionResponse[]>([]);
  const [conceptRevisionId, setConceptRevisionId] = useState("");
  const [acceptanceDocumentIds, setAcceptanceDocumentIds] = useState<Record<number, string>>({});
  const [acceptanceFiles, setAcceptanceFiles] = useState<Record<number, File | null>>({});
  const [signedBy, setSignedBy] = useState<Record<number, string>>({});
  const [signedAt, setSignedAt] = useState<Record<number, string>>({});
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [managerToolsReady, setManagerToolsReady] = useState(false);
  const [busy, setBusy] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [stage, setStage] = useState<DesignReviewStage>("Concept");
  const [title, setTitle] = useState("");
  const [recipientIds, setRecipientIds] = useState<number[]>([]);
  const [documentIds, setDocumentIds] = useState<number[]>([]);
  const [notes, setNotes] = useState<Record<number, string>>({});
  const [commentDocumentIds, setCommentDocumentIds] = useState<Record<number, string>>({});
  const createKey = useRef(newIdempotencyKey());
  const createTrigger = useRef<HTMLButtonElement>(null);

  const actionableError = useCallback(
    (reason: unknown) => {
      const status =
        typeof reason === "object" && reason !== null
          ? (reason as { response?: { status?: number } }).response?.status
          : undefined;
      if (status === 401) return t("designProjects.reviews.error.sessionExpired");
      if (status === 403) return t("designProjects.reviews.error.forbidden");
      if (status === 404) return t("designProjects.reviews.error.notFound");
      if (status === 409) return t("designProjects.reviews.error.conflict");
      if (status && status >= 500) return t("designProjects.reviews.error.server");
      return extractApiError(reason, t("designProjects.reviews.error.generic"));
    },
    [t],
  );

  const load = useCallback(async () => {
    if (!projectId) {
      setLoading(false);
      return;
    }
    setRefreshing(true);
    setError(null);
    setManagerToolsReady(false);
    try {
      const reviewResponse = await adminApi.listDesignReviews(projectId);
      setReviews(reviewResponse.data);
      if (isDesignManager) {
        const [teamResponse, documentResponse] = await Promise.all([
          adminApi.listDesignReviewRecipientCandidates(projectId),
          operationalProjectId
            ? adminApi.listProjectDocuments(operationalProjectId)
            : Promise.resolve({ data: [] }),
        ]);
        setRecipientCandidates(teamResponse.data);
        setDocuments(documentResponse.data);
        const options = (
          await adminApi.listConceptOptions({ designProjectId: projectId, pageSize: 100 })
        ).data.items;
        setConceptOptions(options);
        const revisionResults = await Promise.all(
          options.map((option) => adminApi.listConceptRevisions(option.id)),
        );
        setConceptRevisions(revisionResults.flatMap((result) => result.data));
        setManagerToolsReady(true);
      }
    } catch (reason) {
      setError(actionableError(reason));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [actionableError, isDesignManager, projectId, operationalProjectId]);
  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!loading && focusedReviewId && reviews.some((review) => review.id === focusedReviewId)) {
      const card = document.getElementById(`design-review-${focusedReviewId}`);
      card?.scrollIntoView({ block: "start" });
      card?.focus({ preventScroll: true });
    }
  }, [focusedReviewId, loading, reviews]);

  const eligibleDocuments = useMemo(
    () =>
      documents
        .filter(
          (item) =>
            item.category === categoryByStage[stage] &&
            item.sourceEntityType === sourceTypeByStage[stage] &&
            item.sourceRecordId &&
            item.syncStatus === "Synced",
        )
        .filter(
          (item) =>
            stage !== "Concept" ||
            conceptRevisions
              .find((revision) => revision.id === Number(conceptRevisionId))
              ?.artifacts.some((artifact) => artifact.projectDocumentId === item.id),
        ),
    [documents, stage, conceptRevisionId, conceptRevisions],
  );
  const recipients = recipientCandidates;
  const toggle = <T extends number>(items: T[], value: T, checked: boolean) =>
    checked ? [...items, value] : items.filter((item) => item !== value);

  const download = async (review: DesignReviewResponse, documentId: number, fileName: string) => {
    setBusy(review.id);
    setError(null);
    try {
      const response = await adminApi.downloadDesignReviewDocument(
        projectId,
        review.id,
        documentId,
      );
      const url = window.URL.createObjectURL(response.data);
      const anchor = window.document.createElement("a");
      anchor.href = url;
      anchor.download = fileName;
      anchor.click();
      window.URL.revokeObjectURL(url);
    } catch (reason) {
      setError(await extractDownloadError(reason));
    } finally {
      setBusy(null);
    }
  };

  const create = async () => {
    if (
      !title.trim() ||
      recipientIds.length === 0 ||
      documentIds.length === 0 ||
      (stage === "Concept" && !conceptRevisionId)
    ) {
      setError(t("designProjects.reviews.validation.create"));
      return;
    }
    setBusy(0);
    setError(null);
    try {
      await adminApi.createDesignReview(
        projectId,
        {
          stage,
          title: title.trim(),
          recipientUserIds: recipientIds,
          projectDocumentIds: documentIds,
          conceptRevisionId: stage === "Concept" ? Number(conceptRevisionId) : null,
        },
        createKey.current,
      );
      createKey.current = newIdempotencyKey();
      setOpen(false);
      setTitle("");
      setRecipientIds([]);
      setDocumentIds([]);
      setConceptRevisionId("");
      await load();
    } catch (reason) {
      setError(actionableError(reason));
    } finally {
      setBusy(null);
    }
  };

  const act = async (
    review: DesignReviewResponse,
    action: "internal" | "comment" | "consolidate" | "client" | "approve" | "changes" | "reject",
  ) => {
    setBusy(review.id);
    setError(null);
    const note = notes[review.id]?.trim() ?? "";
    try {
      const key = newIdempotencyKey();
      if (action === "internal")
        await adminApi.submitDesignReviewInternal(projectId, review.id, review.rowVersion, key);
      else if (action === "comment")
        await adminApi.commentDesignReview(
          projectId,
          review.id,
          note,
          commentDocumentIds[review.id] && commentDocumentIds[review.id] !== "general"
            ? Number(commentDocumentIds[review.id])
            : null,
          key,
        );
      else if (action === "consolidate")
        await adminApi.consolidateDesignReview(projectId, review.id, note, review.rowVersion, key);
      else if (action === "client")
        await adminApi.submitDesignReviewClient(projectId, review.id, review.rowVersion, key);
      else
        await adminApi.recordDesignClientDecision(
          projectId,
          review.id,
          action === "approve"
            ? "ClientApproved"
            : action === "changes"
              ? "ClientChangesRequested"
              : "Rejected",
          note || null,
          review.rowVersion,
          key,
          action === "approve" && review.stage === "Concept"
            ? {
                documentId: Number(acceptanceDocumentIds[review.id]),
                signedBy: signedBy[review.id]?.trim() ?? "",
                signedAt: signedAt[review.id]
                  ? new Date(`${signedAt[review.id]}T00:00:00`).toISOString()
                  : "",
              }
            : undefined,
        );
      setNotes((current) => ({ ...current, [review.id]: "" }));
      if (action === "comment")
        setCommentDocumentIds((current) => ({ ...current, [review.id]: "general" }));
      await load();
    } catch (reason) {
      setError(actionableError(reason));
    } finally {
      setBusy(null);
    }
  };

  if (loading && reviews.length === 0)
    return (
      <div className="flex justify-center rounded-lg border bg-white py-16">
        <Loader2 className="h-6 w-6 animate-spin" />
      </div>
    );
  return (
    <section className="space-y-4" data-testid="design-reviews-tab">
      <div className="rounded-lg border bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="font-semibold text-slate-900">{t("designProjects.reviews.title")}</h2>
            <p className="mt-1 text-sm text-slate-500">{t("designProjects.reviews.description")}</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button
              className="min-h-11"
              variant="outline"
              disabled={refreshing || busy !== null}
              onClick={() => void load()}
            >
              <RefreshCw
                aria-hidden="true"
                className={refreshing ? "mr-1 h-4 w-4 animate-spin" : "mr-1 h-4 w-4"}
              />
              {t("common.refresh")}
            </Button>
            {isDesignManager ? (
              <Button
                ref={createTrigger}
                data-testid="create-design-review"
                className="min-h-11"
                disabled={!managerToolsReady || refreshing}
                aria-describedby="design-review-role-guide"
                onClick={() => {
                  setError(null);
                  setOpen(true);
                }}
              >
                <Plus className="mr-1 h-4 w-4" />
                {t("designProjects.reviews.create")}
              </Button>
            ) : null}
          </div>
        </div>
        <div
          id="design-review-role-guide"
          data-testid="design-review-role-guide"
          className="mt-4 rounded-lg bg-slate-50 p-3 text-sm text-slate-700"
        >
          <p className="font-medium text-slate-900">{t("designProjects.reviews.guide.title")}</p>
          <ol className="mt-2 grid gap-3 md:grid-cols-3">
            <li className="flex gap-2">
              <UserCheck aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0 text-sky-700" />
              <span>{t("designProjects.reviews.guide.taskApproval")}</span>
            </li>
            <li className="flex gap-2">
              <Users aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0 text-violet-700" />
              <span>{t("designProjects.reviews.guide.internalReview")}</span>
            </li>
            <li className="flex gap-2">
              <FileCheck2 aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0 text-emerald-700" />
              <span>{t("designProjects.reviews.guide.clientDecision")}</span>
            </li>
          </ol>
        </div>
        {error && !open ? (
          <div
            role="alert"
            className="mt-3 rounded-md border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700"
          >
            <div className="flex items-start gap-2">
              <AlertCircle aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0" />
              <div>
                <p className="font-medium">{t("designProjects.reviews.error.title")}</p>
                <p className="mt-0.5">{error}</p>
              </div>
            </div>
            <Button variant="outline" size="sm" className="mt-2" onClick={() => void load()}>
              <RefreshCw className="mr-1 h-4 w-4" />
              {t("common.retry")}
            </Button>
          </div>
        ) : null}
      </div>
      {reviews.length === 0 ? (
        <div className="rounded-lg border border-dashed bg-white p-10 text-center text-sm text-slate-500">
          <p className="font-medium text-slate-700">{t("designProjects.reviews.emptyTitle")}</p>
          <p className="mx-auto mt-1 max-w-xl">
            {t(
              isDesignManager
                ? "designProjects.reviews.emptyManager"
                : "designProjects.reviews.emptyRecipient",
            )}
          </p>
        </div>
      ) : (
        reviews.map((review) => (
          <article
            key={review.id}
            id={`design-review-${review.id}`}
            tabIndex={-1}
            className="overflow-hidden rounded-lg border bg-white p-4 shadow-sm"
          >
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="min-w-0">
                <h3 className="break-words font-semibold">{review.title}</h3>
                <p className="text-xs text-slate-500">
                  {t(`designProjects.stage.${review.stage}`)} · {t("concepts.dossier.reviewRound")}{" "}
                  {review.revisionNumber.toLocaleString(lang)}
                </p>
                {review.conceptRevisionId &&
                conceptRevisions.some((item) => item.id === review.conceptRevisionId) ? (
                  <p className="mt-1 text-sm text-slate-600">
                    {conceptOptions.find((item) => item.id === review.conceptOptionId)?.name} · R
                    {String(
                      conceptRevisions.find((item) => item.id === review.conceptRevisionId)?.number,
                    ).padStart(2, "0")}
                  </p>
                ) : null}
              </div>
              <Badge variant="outline">{t(`designProjects.reviews.status.${review.status}`)}</Badge>
            </div>
            <p className="mt-2 text-xs text-slate-500">
              {t("designProjects.reviews.recipients")}:{" "}
              {review.recipients.map((item) => item.name).join(", ")}
            </p>
            <div className="mt-3 flex flex-wrap gap-2">
              {review.documents.map((document) => (
                <Button
                  key={document.projectDocumentId}
                  type="button"
                  size="sm"
                  variant="outline"
                  className="h-auto min-h-11 max-w-full py-2"
                  disabled={busy === review.id}
                  onClick={() =>
                    void download(review, document.projectDocumentId, document.originalFileName)
                  }
                >
                  <Download className="mr-1 h-4 w-4" />
                  <span className="min-w-0 break-all whitespace-normal text-left">
                    {document.originalFileName}
                  </span>
                </Button>
              ))}
            </div>
            {review.internalSummary ? (
              <p className="mt-2 whitespace-pre-wrap break-words rounded bg-slate-50 p-3 text-sm">
                {review.internalSummary}
              </p>
            ) : null}
            {review.comments.length ? (
              <div className="mt-3 space-y-2">
                {review.comments.map((comment) => (
                  <div key={comment.id} className="rounded-md border p-3 text-sm">
                    <div className="flex flex-wrap justify-between gap-2 text-xs text-slate-500">
                      <span>{comment.authorName}</span>
                      <time>{new Date(comment.createdAt).toLocaleString(lang)}</time>
                    </div>
                    <p className="mt-1 text-xs font-medium text-slate-600">
                      {comment.projectDocumentId
                        ? review.documents.find(
                            (document) => document.projectDocumentId === comment.projectDocumentId,
                          )?.originalFileName
                        : t("designProjects.reviews.generalComment")}
                    </p>
                    <p className="mt-1 whitespace-pre-wrap break-words">{comment.body}</p>
                  </div>
                ))}
              </div>
            ) : null}
            {review.clientDecisionNote ? (
              <p className="mt-3 break-words text-sm text-slate-700">
                {t("designProjects.reviews.clientNote")}: {review.clientDecisionNote}
              </p>
            ) : null}
            {review.clientAcceptanceDocumentId ? (
              <p className="mt-2 text-xs text-emerald-700">
                {t("concepts.dossier.acceptanceRecorded")}: {review.clientAcceptanceSignedBy} ·{" "}
                {review.clientAcceptanceSignedAt
                  ? new Date(review.clientAcceptanceSignedAt).toLocaleDateString(lang)
                  : null}
              </p>
            ) : null}
            {isDesignManager &&
            review.canManage &&
            review.stage === "Concept" &&
            review.status === "SubmittedToClient" &&
            review.conceptOptionId ? (
              <div className="mt-3 space-y-2 rounded border p-3">
                <Label className="block" htmlFor={`review-${review.id}-file`}>
                  {t("concepts.dossier.acceptance")}
                </Label>
                <LocalizedFileInput
                  id={`review-${review.id}-file`}
                  disabled={busy === review.id}
                  accept=".pdf,.jpg,.jpeg,.png"
                  selectedFileName={acceptanceFiles[review.id]?.name}
                  onChange={(event) =>
                    setAcceptanceFiles((current) => ({
                      ...current,
                      [review.id]: event.target.files?.[0] ?? null,
                    }))
                  }
                />
                <Button
                  size="sm"
                  variant="outline"
                  disabled={busy === review.id || !acceptanceFiles[review.id]}
                  onClick={() =>
                    void (async () => {
                      const file = acceptanceFiles[review.id];
                      if (!file || !operationalProjectId || !review.conceptOptionId) return;
                      setBusy(review.id);
                      try {
                        const uploaded = await adminApi.uploadProjectDocument(
                          operationalProjectId,
                          file,
                          "DesignConcept",
                          newIdempotencyKey(),
                          {
                            module: "Design",
                            entityType: "ConceptOption",
                            recordId: review.conceptOptionId,
                          },
                        );
                        setAcceptanceDocumentIds((current) => ({
                          ...current,
                          [review.id]: String(uploaded.data.id),
                        }));
                        await load();
                      } catch (reason) {
                        setError(actionableError(reason));
                      } finally {
                        setBusy(null);
                      }
                    })()
                  }
                >
                  {t("concepts.dossier.uploadAcceptance")}
                </Button>
                <Label className="block" htmlFor={`review-${review.id}-acceptance`}>
                  {t("concepts.dossier.selectAcceptance")}
                </Label>
                <Select
                  disabled={busy === review.id}
                  value={acceptanceDocumentIds[review.id] ?? ""}
                  onValueChange={(value) =>
                    setAcceptanceDocumentIds((current) => ({ ...current, [review.id]: value }))
                  }
                >
                  <SelectTrigger id={`review-${review.id}-acceptance`} className="min-h-11">
                    <SelectValue placeholder={t("concepts.dossier.selectAcceptance")} />
                  </SelectTrigger>
                  <SelectContent>
                    {documents
                      .filter(
                        (item) =>
                          item.sourceEntityType === "ConceptOption" &&
                          item.sourceRecordId === review.conceptOptionId &&
                          item.category === "DesignConcept" &&
                          !review.projectDocumentIds.includes(item.id) &&
                          ["application/pdf", "image/jpeg", "image/png"].includes(
                            item.contentType,
                          ) &&
                          item.syncStatus === "Synced",
                      )
                      .map((item) => (
                        <SelectItem key={item.id} value={String(item.id)}>
                          {item.originalFileName}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
                {acceptanceDocumentIds[review.id] &&
                documents.some(
                  (item) =>
                    item.id === Number(acceptanceDocumentIds[review.id]) &&
                    item.syncStatus !== "Synced",
                ) ? (
                  <p role="status" className="text-sm text-amber-800">
                    {t("concepts.dossier.awaitingSync")}
                  </p>
                ) : null}
                <Label className="block" htmlFor={`review-${review.id}-signer`}>
                  {t("concepts.dossier.signedBy")}
                </Label>
                <Input
                  id={`review-${review.id}-signer`}
                  disabled={busy === review.id}
                  className="min-h-11"
                  maxLength={200}
                  value={signedBy[review.id] ?? ""}
                  placeholder={t("concepts.dossier.signedBy")}
                  onChange={(event) =>
                    setSignedBy((current) => ({ ...current, [review.id]: event.target.value }))
                  }
                />
                <Label className="block" htmlFor={`review-${review.id}-signed-at`}>
                  {t("concepts.dossier.signedAt")}
                </Label>
                <LocalizedDateInput
                  id={`review-${review.id}-signed-at`}
                  disabled={busy === review.id}
                  className="min-h-11"
                  value={signedAt[review.id] ?? ""}
                  onChange={(event) =>
                    setSignedAt((current) => ({ ...current, [review.id]: event.target.value }))
                  }
                />
              </div>
            ) : null}
            {review.canComment ||
            (isDesignManager &&
              review.canManage &&
              ["InternalReview", "SubmittedToClient"].includes(review.status)) ? (
              <div className="mt-3 space-y-1.5">
                <Label htmlFor={`review-${review.id}-note`}>
                  {t("designProjects.reviews.notePlaceholder")}
                </Label>
                <Textarea
                  id={`review-${review.id}-note`}
                  disabled={busy === review.id}
                  maxLength={4000}
                  value={notes[review.id] ?? ""}
                  placeholder={t("designProjects.reviews.notePlaceholder")}
                  onChange={(event) =>
                    setNotes((current) => ({ ...current, [review.id]: event.target.value }))
                  }
                />
              </div>
            ) : null}
            {review.canComment && review.status === "InternalReview" ? (
              <div className="mt-3 max-w-sm">
                <Label htmlFor={`review-${review.id}-comment-target`}>
                  {t("designProjects.reviews.commentTarget")}
                </Label>
                <Select
                  value={commentDocumentIds[review.id] ?? "general"}
                  onValueChange={(value) =>
                    setCommentDocumentIds((current) => ({ ...current, [review.id]: value }))
                  }
                >
                  <SelectTrigger
                    id={`review-${review.id}-comment-target`}
                    className="mt-1 min-h-11"
                  >
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="general">
                      {t("designProjects.reviews.generalComment")}
                    </SelectItem>
                    {review.documents.map((document) => (
                      <SelectItem
                        key={document.projectDocumentId}
                        value={String(document.projectDocumentId)}
                      >
                        {document.originalFileName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            ) : null}
            <div className="mt-3 flex flex-wrap gap-2">
              {isDesignManager && review.canManage && review.status === "Draft" ? (
                <div className="space-y-2">
                  <Button
                    disabled={
                      busy === review.id ||
                      (review.stage === "Concept" &&
                        !!conceptRevisions.find((item) => item.id === review.conceptRevisionId)
                          ?.frozenAt)
                    }
                    onClick={() => void act(review, "internal")}
                  >
                    <Send className="mr-1 h-4 w-4" />
                    {t("designProjects.reviews.submitInternal")}
                  </Button>
                  {review.stage === "Concept" &&
                  conceptRevisions.find((item) => item.id === review.conceptRevisionId)
                    ?.frozenAt ? (
                    <p className="text-sm text-muted-foreground">
                      {t("concepts.dossier.supersededDraft")}
                    </p>
                  ) : null}
                </div>
              ) : null}
              {review.canComment && review.status === "InternalReview" ? (
                <Button
                  variant="outline"
                  disabled={busy === review.id || !notes[review.id]?.trim()}
                  onClick={() => void act(review, "comment")}
                >
                  <MessageSquare className="mr-1 h-4 w-4" />
                  {t("designProjects.reviews.comment")}
                </Button>
              ) : null}
              {isDesignManager && review.canManage && review.status === "InternalReview" ? (
                <Button
                  disabled={busy === review.id || !notes[review.id]?.trim()}
                  onClick={() => void act(review, "consolidate")}
                >
                  <Check className="mr-1 h-4 w-4" />
                  {t("designProjects.reviews.consolidate")}
                </Button>
              ) : null}
              {isDesignManager && review.canManage && review.status === "ReadyForClient" ? (
                <Button disabled={busy === review.id} onClick={() => void act(review, "client")}>
                  <Send className="mr-1 h-4 w-4" />
                  {t("designProjects.reviews.submitClient")}
                </Button>
              ) : null}
              {isDesignManager && review.canManage && review.status === "SubmittedToClient" ? (
                <>
                  <Button
                    disabled={
                      busy === review.id ||
                      (review.stage === "Concept" &&
                        (!acceptanceDocumentIds[review.id] ||
                          !documents.some(
                            (item) =>
                              item.id === Number(acceptanceDocumentIds[review.id]) &&
                              item.syncStatus === "Synced" &&
                              !review.projectDocumentIds.includes(item.id),
                          ) ||
                          !signedBy[review.id]?.trim() ||
                          !signedAt[review.id]))
                    }
                    onClick={() => void act(review, "approve")}
                  >
                    {t("designProjects.reviews.approve")}
                  </Button>
                  <Button
                    variant="outline"
                    disabled={busy === review.id || !notes[review.id]?.trim()}
                    onClick={() => void act(review, "changes")}
                  >
                    {t("designProjects.reviews.changes")}
                  </Button>
                  <Button
                    variant="destructive"
                    disabled={busy === review.id || !notes[review.id]?.trim()}
                    onClick={() => void act(review, "reject")}
                  >
                    {t("designProjects.reviews.reject")}
                  </Button>
                </>
              ) : null}
            </div>
          </article>
        ))
      )}
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent
          className="max-h-[94vh] overflow-y-auto sm:max-w-2xl"
          onCloseAutoFocus={(event) => {
            if (createTrigger.current) {
              event.preventDefault();
              createTrigger.current.focus();
            }
          }}
        >
          <DialogHeader>
            <DialogTitle>{t("designProjects.reviews.create")}</DialogTitle>
            <DialogDescription>{t("designProjects.reviews.createDescription")}</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <Label htmlFor="review-stage">{t("designProjects.reviews.stage")}</Label>
              <Select
                value={stage}
                onValueChange={(value) => {
                  setStage(value as DesignReviewStage);
                  setDocumentIds([]);
                  setConceptRevisionId("");
                }}
              >
                <SelectTrigger id="review-stage" className="mt-1 min-h-11">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(["Concept", "BasicDesign", "ShopDrawing"] as DesignReviewStage[]).map(
                    (item) => (
                      <SelectItem key={item} value={item}>
                        {t(`designProjects.stage.${item}`)}
                      </SelectItem>
                    ),
                  )}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="review-title">{t("designProjects.reviews.reviewTitle")}</Label>
              <Input
                id="review-title"
                className="mt-1 min-h-11"
                maxLength={300}
                value={title}
                onChange={(event) => setTitle(event.target.value)}
              />
            </div>
            {stage === "Concept" ? (
              <div className="mb-4">
                <Label htmlFor="review-concept-revision">{t("concepts.dossier.revision")}</Label>
                <Select
                  value={conceptRevisionId}
                  onValueChange={(value) => {
                    setConceptRevisionId(value);
                    setDocumentIds(
                      conceptRevisions
                        .find((item) => item.id === Number(value))
                        ?.artifacts.map((item) => item.projectDocumentId) ?? [],
                    );
                  }}
                >
                  <SelectTrigger id="review-concept-revision" className="mt-1 min-h-11">
                    <SelectValue placeholder={t("concepts.dossier.selectRevision")} />
                  </SelectTrigger>
                  <SelectContent>
                    {conceptRevisions
                      .filter(
                        (item) =>
                          !item.frozenAt &&
                          !conceptRevisions.some(
                            (other) =>
                              other.conceptOptionId === item.conceptOptionId &&
                              other.number > item.number,
                          ) &&
                          conceptOptions.some(
                            (option) =>
                              option.id === item.conceptOptionId &&
                              !["Finalized", "Discarded"].includes(option.status),
                          ),
                      )
                      .map((item) => (
                        <SelectItem key={item.id} value={String(item.id)}>
                          {conceptOptions.find((option) => option.id === item.conceptOptionId)
                            ?.name ?? `#${item.conceptOptionId}`}{" "}
                          · R{String(item.number).padStart(2, "0")}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
              </div>
            ) : null}
            <fieldset>
              <legend className="text-sm font-medium">
                {t("designProjects.reviews.recipients")}
              </legend>
              <div className="mt-1 max-h-44 overflow-y-auto rounded-md border p-2">
                {recipients.map((recipient) => (
                  <label key={recipient.userId} className="flex min-h-11 items-center gap-3 px-2">
                    <Checkbox
                      checked={recipientIds.includes(recipient.userId)}
                      onCheckedChange={(checked) =>
                        setRecipientIds((items) =>
                          toggle(items, recipient.userId, checked === true),
                        )
                      }
                    />
                    {recipient.name}
                  </label>
                ))}
              </div>
            </fieldset>
            <fieldset>
              <legend className="text-sm font-medium">
                {t("designProjects.reviews.documents")}
              </legend>
              <div className="mt-1 max-h-44 overflow-y-auto rounded-md border p-2">
                {eligibleDocuments.map((document) => (
                  <label key={document.id} className="flex min-h-11 items-center gap-3 px-2">
                    <Checkbox
                      checked={documentIds.includes(document.id)}
                      disabled={stage === "Concept"}
                      onCheckedChange={(checked) =>
                        setDocumentIds((items) => toggle(items, document.id, checked === true))
                      }
                    />
                    <span className="min-w-0 break-all text-sm">{document.originalFileName}</span>
                  </label>
                ))}
                {eligibleDocuments.length === 0 ? (
                  <p className="p-3 text-sm text-slate-500">
                    {t(
                      stage === "Concept" && !conceptRevisionId
                        ? "concepts.dossier.selectRevision"
                        : "designProjects.reviews.noDocuments",
                    )}
                  </p>
                ) : null}
              </div>
            </fieldset>
          </div>
          {error ? (
            <p role="alert" className="text-sm text-rose-700">
              {error}
            </p>
          ) : null}
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button
              disabled={
                busy === 0 ||
                !title.trim() ||
                !recipientIds.length ||
                !documentIds.length ||
                (stage === "Concept" && !conceptRevisionId)
              }
              onClick={() => void create()}
            >
              {busy === 0 ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : null}
              {t("designProjects.reviews.createDraft")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </section>
  );
};
