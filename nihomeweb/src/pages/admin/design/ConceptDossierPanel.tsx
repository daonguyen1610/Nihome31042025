import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { CheckCircle2, Loader2, RefreshCw } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ConceptDossierFiles } from "./ConceptDossierFiles";
import { extractApiError } from "@/lib/apiError";
import { translateError, useI18n } from "@/lib/i18n";
import {
  adminApi,
  type ConceptRevisionResponse,
  type DesignReviewResponse,
  type ProjectDocumentResponse,
} from "@/services/adminApi";
import { formatDateTime } from "@/lib/displaySettings";

interface Props {
  projectId: number;
  optionId: number;
  reviews: DesignReviewResponse[];
  canManage: boolean;
  locked: boolean;
  onChanged: () => void | Promise<void>;
  onOpenReviews: () => void;
}

export const ConceptDossierPanel = ({
  projectId,
  optionId,
  reviews,
  canManage,
  locked,
  onChanged,
  onOpenReviews,
}: Props) => {
  const uiError = useUiError();
  const { t, lang } = useI18n();
  const prefix = `concept-${optionId}`;
  const loadRequest = useRef(0);
  const [revisions, setRevisions] = useState<ConceptRevisionResponse[]>([]);
  const [documents, setDocuments] = useState<ProjectDocumentResponse[]>([]);
  const [selectedRevisionId, setSelectedRevisionId] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [activeSection, setActiveSection] = useState<"files" | "feedback">("files");
  const [customerName, setCustomerName] = useState("");
  const [channel, setChannel] = useState("");
  const [evidenceSource, setEvidenceSource] = useState("");
  const [occurredAt, setOccurredAt] = useState("");
  const [comment, setComment] = useState("");
  const [commentDocumentId, setCommentDocumentId] = useState("");
  const [responses, setResponses] = useState<Record<number, string>>({});
  const [outcomes, setOutcomes] = useState<Record<number, string>>({});
  const [reasons, setReasons] = useState<{ code: string; name: string }[]>([]);
  const [revisionReason, setRevisionReason] = useState("");
  const working = busy || loading;

  const load = useCallback(async () => {
    const request = ++loadRequest.current;
    setLoading(true);
    try {
      const [revisionResult, documentResult] = await Promise.all([
        adminApi.listConceptRevisions(optionId),
        adminApi.listProjectDocuments(projectId),
      ]);
      if (request !== loadRequest.current) return false;
      setRevisions(revisionResult.data);
      setDocuments(documentResult.data);
      setSelectedRevisionId((current) =>
        current && revisionResult.data.some((revision) => revision.id === current)
          ? current
          : (revisionResult.data[0]?.id ?? null),
      );
      setError(null);
      return true;
    } catch (reason) {
      if (request === loadRequest.current) setError(translateError(t, uiError(reason)));
      return false;
    } finally {
      if (request === loadRequest.current) setLoading(false);
    }
  }, [optionId, projectId, t, uiError]);

  useEffect(() => {
    void load();
    return () => {
      loadRequest.current += 1;
    };
  }, [load]);

  const needsReasons = revisions.some((item) => item.reasonCode || item.canCreateRevision);
  useEffect(() => {
    if (!needsReasons) return;
    let cancelled = false;
    void adminApi
      .getMasterDataOptions("drawing_revision_reason", lang)
      .then(({ data }) => {
        if (!cancelled) setReasons((data ?? []).filter((option) => option.isActive));
      })
      .catch((reason) => {
        if (!cancelled) setError(translateError(t, uiError(reason)));
      });
    return () => {
      cancelled = true;
    };
  }, [needsReasons, lang, t, uiError]);

  const revision = revisions.find((item) => item.id === selectedRevisionId);
  const editable =
    canManage && !locked && !!revision && !revision.frozenAt && revision.id === revisions[0]?.id;
  const canCreateRevision =
    canManage && !locked && revision?.id === revisions[0]?.id && !!revision?.canCreateRevision;
  const hasClientSubmission = reviews.some(
    (review) => review.conceptRevisionId === revision?.id && !!review.clientSubmittedAt,
  );
  const attachedIds = useMemo(
    () =>
      new Set(
        revisions.flatMap((item) => item.artifacts.map((artifact) => artifact.projectDocumentId)),
      ),
    [revisions],
  );
  const available = documents.filter(
    (document) =>
      document.category === "DesignConcept" &&
      document.sourceEntityType === "ConceptOption" &&
      document.sourceRecordId === optionId &&
      !attachedIds.has(document.id),
  );
  const selectRevision = (value: string) => {
    setSelectedRevisionId(Number(value));
    setCommentDocumentId("");
    setComment("");
    setError(null);
    setSaved(false);
  };
  const run = async (action: () => Promise<unknown>) => {
    setBusy(true);
    setError(null);
    setSaved(false);
    try {
      await action();
      if (await load()) {
        await onChanged();
        setSaved(true);
      }
    } catch (reason) {
      // Refresh bindings so a successful upload can be reused if attachment failed.
      await load();
      setError(translateError(t, uiError(reason)));
    } finally {
      setBusy(false);
    }
  };

  const label = (number: number) => `R${String(number).padStart(2, "0")}`;
  const revisionLabel = revision ? label(revision.number) : "";
  const readiness = revision?.readiness;
  const isLatest = revision?.id === revisions[0]?.id;
  const hasDraftReview = reviews.some(
    (review) => review.conceptRevisionId === revision?.id && review.status === "Draft",
  );
  // One sentence that says where this version stands and what happens next.
  // The blockers come from the server, which applies the same checks on submission.
  const draftState = () => {
    if (!editable || !readiness) return { key: "concepts.dossier.state.draftReadOnly", count: 0 };
    const missing = readiness.missingKinds.length + (readiness.needsVisual ? 1 : 0);
    if (missing) return { key: "concepts.dossier.state.draftMissing", count: missing };
    if (readiness.filesBlocked)
      return { key: "concepts.dossier.state.draftBlocked", count: readiness.filesBlocked };
    if (readiness.filesWaiting)
      return { key: "concepts.dossier.state.draftSyncing", count: readiness.filesWaiting };
    if (readiness.hasUnansweredEarlierFeedback)
      return { key: "concepts.dossier.state.draftFeedback", count: 0 };
    return {
      key: hasDraftReview
        ? "concepts.dossier.state.draftReviewOpen"
        : "concepts.dossier.state.draftComplete",
      count: 0,
    };
  };
  const state = !revision
    ? null
    : !isLatest
      ? { key: "concepts.dossier.state.old", count: 0 }
      : revision.frozenAt
        ? {
            key: canCreateRevision
              ? "concepts.dossier.state.canRevise"
              : locked
                ? "concepts.dossier.state.frozen"
                : "concepts.dossier.state.waitClient",
            count: 0,
          }
        : draftState();
  const leadsToReviews =
    state?.key === "concepts.dossier.state.draftComplete" ||
    state?.key === "concepts.dossier.state.draftReviewOpen";
  const reasonName = revision?.reasonCode
    ? (reasons.find((option) => option.code === revision.reasonCode)?.name ?? "")
    : "";

  return (
    <section
      id={`concept-dossier-${optionId}`}
      aria-label={t("concepts.dossier.open")}
      aria-busy={working}
      className="min-w-0 space-y-5 pt-4 text-sm"
      data-testid={`concept-dossier-${optionId}`}
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          {revisions.length > 1 ? (
            <div className="flex flex-wrap items-center gap-2">
              <Label htmlFor={`${prefix}-revision`}>{t("concepts.dossier.revision")}</Label>
              <Select
                value={selectedRevisionId ? String(selectedRevisionId) : ""}
                onValueChange={selectRevision}
                disabled={working}
              >
                <SelectTrigger id={`${prefix}-revision`} className="min-h-11 w-40">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {revisions.map((item) => (
                    <SelectItem key={item.id} value={String(item.id)}>
                      {label(item.number)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          ) : revision ? (
            <p className="font-medium">
              {t("concepts.dossier.version.single").replace("{revision}", revisionLabel)}
            </p>
          ) : null}
          {reasonName ? (
            <p className="mt-1">
              {t("concepts.dossier.version.reason").replace("{reason}", reasonName)}
            </p>
          ) : null}
          {revision ? (
            <p className="mt-1 max-w-prose text-xs text-muted-foreground">
              {t("concepts.dossier.version.why")}
            </p>
          ) : null}
        </div>
        <Button
          className="min-h-11"
          size="sm"
          variant="ghost"
          disabled={working}
          onClick={() =>
            void (async () => {
              setSaved(false);
              if (await load()) await onChanged();
            })()
          }
        >
          <RefreshCw aria-hidden="true" className="mr-2 h-4 w-4" />
          {t(error ? "common.retry" : "common.refresh")}
        </Button>
      </div>
      {error ? (
        <p
          role="alert"
          className="rounded-md border border-destructive/20 bg-destructive/10 p-3 text-destructive"
        >
          {error}
        </p>
      ) : null}
      {saved && !working ? (
        <p role="status" className="flex items-center gap-2 text-success-strong">
          <CheckCircle2 aria-hidden="true" className="h-4 w-4" />
          {t("concepts.dossier.saved")}
        </p>
      ) : null}
      {working ? (
        <p role="status" className="flex items-center gap-2 py-4 text-muted-foreground">
          <Loader2 aria-hidden="true" className="h-4 w-4 animate-spin" />
          {t("common.loading")}
        </p>
      ) : null}
      {revision && state ? (
        <>
          <div className="space-y-3 rounded-md border bg-muted/40 p-3">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <p className="min-w-0 flex-1 basis-64">
                {t(state.key)
                  .replace("{revision}", revisionLabel)
                  .replace("{count}", String(state.count))}
              </p>
              {leadsToReviews ? (
                <Button className="min-h-11" onClick={onOpenReviews}>
                  {t("concepts.dossier.openReviews")}
                </Button>
              ) : null}
            </div>
            {canCreateRevision ? (
              <div className="flex flex-wrap items-end gap-3 border-t pt-3">
                <div className="min-w-0 flex-1 basis-56 space-y-1.5 sm:max-w-xs">
                  <Label htmlFor={`${prefix}-reason`}>
                    {t("concepts.dossier.version.reasonLabel")} *
                  </Label>
                  <Select
                    value={revisionReason}
                    onValueChange={setRevisionReason}
                    disabled={working}
                  >
                    <SelectTrigger
                      id={`${prefix}-reason`}
                      aria-required="true"
                      className="min-h-11 bg-card"
                    >
                      <SelectValue placeholder={t("drawingRevision.form.reasonPlaceholder")} />
                    </SelectTrigger>
                    <SelectContent>
                      {reasons.map((option) => (
                        <SelectItem key={option.code} value={option.code}>
                          {option.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <Button
                  className="min-h-11"
                  disabled={working || !revisionReason}
                  onClick={() =>
                    void run(async () => {
                      const result = await adminApi.createConceptRevision(optionId, revisionReason);
                      setRevisionReason("");
                      selectRevision(String(result.data.id));
                    })
                  }
                >
                  {t("concepts.dossier.newRevision")}
                </Button>
              </div>
            ) : null}
          </div>
          <Tabs
            value={activeSection}
            onValueChange={(value) => setActiveSection(value as "files" | "feedback")}
          >
            <TabsList className="h-auto flex-wrap justify-start">
              <TabsTrigger className="min-h-11" value="files">
                {t("concepts.dossier.tab.files")}
              </TabsTrigger>
              <TabsTrigger className="min-h-11 whitespace-normal" value="feedback">
                {t("concepts.dossier.feedback")}
                {revision.feedback.length ? ` (${revision.feedback.length})` : ""}
              </TabsTrigger>
            </TabsList>
            {/* Stays mounted so a half-filled form survives a look at the feedback tab. */}
            <TabsContent value="files" forceMount className="data-[state=inactive]:hidden">
              <ConceptDossierFiles
                key={revision.id}
                projectId={projectId}
                optionId={optionId}
                revision={revision}
                available={available}
                editable={editable}
                working={working}
                run={run}
              />
            </TabsContent>
            <TabsContent value="feedback" className="space-y-3">
              <p className="text-muted-foreground">{t("concepts.dossier.feedbackHint")}</p>
              {!revision.feedback.length ? (
                <p className="rounded-md border border-dashed p-4 text-muted-foreground">
                  {t("concepts.dossier.noFeedback")}
                </p>
              ) : null}
              {revision.feedback.map((item) => (
                <article key={item.id} className="min-w-0 space-y-3 rounded-md border p-4">
                  <div className="flex flex-wrap items-start justify-between gap-2">
                    <div className="min-w-0">
                      <h4 className="break-words font-medium">{item.customerName}</h4>
                      <p className="mt-1 break-words text-muted-foreground">
                        {item.channel} ·{" "}
                        <time dateTime={item.occurredAt}>
                          {formatDateTime(item.occurredAt, lang)}
                        </time>
                      </p>
                    </div>
                    <Badge variant="outline">
                      {t(
                        item.response
                          ? "concepts.dossier.responded"
                          : "concepts.dossier.awaitingResponse",
                      )}
                    </Badge>
                  </div>
                  <p className="whitespace-pre-wrap break-words">{item.comment}</p>
                  <dl className="grid gap-3 sm:grid-cols-2">
                    {item.evidenceSource ? (
                      <div className="min-w-0">
                        <dt className="text-muted-foreground">
                          {t("concepts.dossier.evidenceLabel")}
                        </dt>
                        <dd className="mt-1 whitespace-pre-wrap break-words">
                          {item.evidenceSource}
                        </dd>
                      </div>
                    ) : null}
                    {item.projectDocumentId ? (
                      <div className="min-w-0">
                        <dt className="text-muted-foreground">
                          {t("designProjects.reviews.commentTarget")}
                        </dt>
                        <dd className="mt-1 break-words">
                          {revision.artifacts.find(
                            (artifact) => artifact.projectDocumentId === item.projectDocumentId,
                          )?.fileName ?? "—"}
                        </dd>
                      </div>
                    ) : null}
                    {item.response ? (
                      <div className="min-w-0">
                        <dt className="text-muted-foreground">{t("concepts.dossier.response")}</dt>
                        <dd className="mt-1 whitespace-pre-wrap break-words">{item.response}</dd>
                      </div>
                    ) : null}
                    {item.outcome ? (
                      <div className="min-w-0">
                        <dt className="text-muted-foreground">{t("concepts.dossier.outcome")}</dt>
                        <dd className="mt-1 whitespace-pre-wrap break-words">{item.outcome}</dd>
                      </div>
                    ) : null}
                  </dl>
                  {!item.response && canManage ? (
                    <fieldset disabled={working} className="space-y-3 border-t pt-3">
                      <div className="grid gap-3 md:grid-cols-2">
                        <div className="space-y-1.5">
                          <Label htmlFor={`${prefix}-response-${item.id}`}>
                            {t("concepts.dossier.response")}
                          </Label>
                          <Textarea
                            id={`${prefix}-response-${item.id}`}
                            rows={3}
                            maxLength={4000}
                            value={responses[item.id] ?? ""}
                            onChange={(event) =>
                              setResponses((current) => ({
                                ...current,
                                [item.id]: event.target.value,
                              }))
                            }
                          />
                        </div>
                        <div className="space-y-1.5">
                          <Label htmlFor={`${prefix}-outcome-${item.id}`}>
                            {t("concepts.dossier.outcome")}
                          </Label>
                          <Textarea
                            id={`${prefix}-outcome-${item.id}`}
                            rows={3}
                            maxLength={2000}
                            value={outcomes[item.id] ?? ""}
                            onChange={(event) =>
                              setOutcomes((current) => ({
                                ...current,
                                [item.id]: event.target.value,
                              }))
                            }
                          />
                        </div>
                      </div>
                      <Button
                        className="min-h-11"
                        disabled={
                          working || !responses[item.id]?.trim() || !outcomes[item.id]?.trim()
                        }
                        onClick={() =>
                          void run(() =>
                            adminApi.respondConceptFeedback(
                              optionId,
                              revision.id,
                              item.id,
                              responses[item.id].trim(),
                              outcomes[item.id].trim(),
                            ),
                          )
                        }
                      >
                        {t("concepts.dossier.respond")}
                      </Button>
                    </fieldset>
                  ) : null}
                </article>
              ))}
              {canManage && hasClientSubmission ? (
                <fieldset disabled={working} className="space-y-4 rounded-md border p-4">
                  <legend className="px-1 font-medium">
                    {t("concepts.dossier.recordFeedback")}
                  </legend>
                  <p className="text-muted-foreground">{t("concepts.dossier.requiredFields")}</p>
                  <div className="grid gap-4 md:grid-cols-2">
                    <div className="space-y-1.5">
                      <Label htmlFor={`${prefix}-customer`}>
                        {t("concepts.dossier.customerName")}
                      </Label>
                      <Input
                        id={`${prefix}-customer`}
                        className="min-h-11"
                        maxLength={200}
                        value={customerName}
                        onChange={(event) => setCustomerName(event.target.value)}
                      />
                    </div>
                    <div className="space-y-1.5">
                      <Label htmlFor={`${prefix}-channel`}>{t("concepts.dossier.channel")}</Label>
                      <Input
                        id={`${prefix}-channel`}
                        className="min-h-11"
                        maxLength={80}
                        value={channel}
                        onChange={(event) => setChannel(event.target.value)}
                      />
                    </div>
                    <div className="space-y-1.5">
                      <Label htmlFor={`${prefix}-occurred`}>
                        {t("concepts.dossier.occurredAt")}
                      </Label>
                      <Input
                        id={`${prefix}-occurred`}
                        type="datetime-local"
                        className="min-h-11"
                        value={occurredAt}
                        onChange={(event) => setOccurredAt(event.target.value)}
                      />
                    </div>
                    <div className="space-y-1.5">
                      <Label htmlFor={`${prefix}-target`}>
                        {t("designProjects.reviews.commentTarget")}
                      </Label>
                      <Select
                        value={commentDocumentId || "general"}
                        onValueChange={(value) =>
                          setCommentDocumentId(value === "general" ? "" : value)
                        }
                        disabled={working}
                      >
                        <SelectTrigger id={`${prefix}-target`} className="min-h-11">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="general">
                            {t("designProjects.reviews.generalComment")}
                          </SelectItem>
                          {revision.artifacts.map((artifact) => (
                            <SelectItem
                              key={artifact.id}
                              value={String(artifact.projectDocumentId)}
                            >
                              {artifact.fileName}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-1.5 md:col-span-2">
                      <Label htmlFor={`${prefix}-evidence`}>
                        {t("concepts.dossier.evidenceLabel")}
                      </Label>
                      <Textarea
                        id={`${prefix}-evidence`}
                        rows={2}
                        maxLength={1000}
                        value={evidenceSource}
                        aria-describedby={`${prefix}-evidence-hint`}
                        onChange={(event) => setEvidenceSource(event.target.value)}
                      />
                      <p id={`${prefix}-evidence-hint`} className="text-muted-foreground">
                        {t("concepts.dossier.evidenceSource")}
                      </p>
                    </div>
                    <div className="space-y-1.5 md:col-span-2">
                      <Label htmlFor={`${prefix}-comment`}>{t("concepts.dossier.comment")}</Label>
                      <Textarea
                        id={`${prefix}-comment`}
                        rows={4}
                        maxLength={4000}
                        value={comment}
                        onChange={(event) => setComment(event.target.value)}
                      />
                    </div>
                  </div>
                  <Button
                    className="min-h-11"
                    disabled={
                      working ||
                      !customerName.trim() ||
                      !channel.trim() ||
                      !evidenceSource.trim() ||
                      !occurredAt ||
                      !comment.trim()
                    }
                    onClick={() =>
                      void run(async () => {
                        await adminApi.addConceptFeedback(optionId, revision.id, {
                          customerName: customerName.trim(),
                          channel: channel.trim(),
                          evidenceSource: evidenceSource.trim(),
                          occurredAt: new Date(occurredAt).toISOString(),
                          comment,
                          projectDocumentId: commentDocumentId ? Number(commentDocumentId) : null,
                        });
                        setComment("");
                        setCommentDocumentId("");
                      })
                    }
                  >
                    {t("concepts.dossier.recordFeedback")}
                  </Button>
                </fieldset>
              ) : canManage && !hasClientSubmission ? (
                <p className="text-muted-foreground">{t("concepts.dossier.feedbackNotSent")}</p>
              ) : null}
            </TabsContent>
          </Tabs>
        </>
      ) : !loading && !error ? (
        <p className="py-4 text-muted-foreground">{t("concepts.dossier.empty")}</p>
      ) : null}
    </section>
  );
};
