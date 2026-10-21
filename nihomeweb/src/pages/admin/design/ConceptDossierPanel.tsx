import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { CheckCircle2, Loader2, RefreshCw } from "lucide-react";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
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
import { newIdempotencyKey } from "@/lib/api";
import { extractApiError } from "@/lib/apiError";
import { translateError, useI18n } from "@/lib/i18n";
import {
  adminApi,
  type ConceptArtifactKind,
  type ConceptRevisionResponse,
  type DesignReviewResponse,
  type ProjectDocumentResponse,
} from "@/services/adminApi";

const kinds: ConceptArtifactKind[] = [
  "Model3D",
  "Perspective",
  "SiteRoofPlan",
  "FloorPlan",
  "Section",
  "Presentation",
  "Other",
];
const checklistKinds: ConceptArtifactKind[] = kinds.filter(
  (kind) => kind !== "Model3D" && kind !== "Other",
);

interface Props {
  projectId: number;
  optionId: number;
  reviews: DesignReviewResponse[];
  canManage: boolean;
  locked: boolean;
  onChanged: () => void | Promise<void>;
}

export const ConceptDossierPanel = ({
  projectId,
  optionId,
  reviews,
  canManage,
  locked,
  onChanged,
}: Props) => {
  const { t, lang } = useI18n();
  const prefix = `concept-${optionId}`;
  const fileInput = useRef<HTMLInputElement>(null);
  const loadRequest = useRef(0);
  const [revisions, setRevisions] = useState<ConceptRevisionResponse[]>([]);
  const [documents, setDocuments] = useState<ProjectDocumentResponse[]>([]);
  const [selectedRevisionId, setSelectedRevisionId] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [kind, setKind] = useState<ConceptArtifactKind>("Perspective");
  const [floorLabel, setFloorLabel] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [existingDocumentId, setExistingDocumentId] = useState("");
  const [waiverKind, setWaiverKind] = useState<ConceptArtifactKind>("Section");
  const [waiverReason, setWaiverReason] = useState("");
  const [customerName, setCustomerName] = useState("");
  const [channel, setChannel] = useState("");
  const [evidenceSource, setEvidenceSource] = useState("");
  const [occurredAt, setOccurredAt] = useState("");
  const [comment, setComment] = useState("");
  const [commentDocumentId, setCommentDocumentId] = useState("");
  const [responses, setResponses] = useState<Record<number, string>>({});
  const [outcomes, setOutcomes] = useState<Record<number, string>>({});
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
      if (request === loadRequest.current) setError(translateError(t, extractApiError(reason)));
      return false;
    } finally {
      if (request === loadRequest.current) setLoading(false);
    }
  }, [optionId, projectId, t]);

  useEffect(() => {
    void load();
    return () => {
      loadRequest.current += 1;
    };
  }, [load]);

  const revision = revisions.find((item) => item.id === selectedRevisionId);
  const editable =
    canManage && !locked && !!revision && !revision.frozenAt && revision.id === revisions[0]?.id;
  const latestReview = reviews
    .filter((review) => review.conceptRevisionId === revisions[0]?.id)
    .sort((a, b) => b.revisionNumber - a.revisionNumber)[0];
  const canCreateRevision =
    canManage &&
    !locked &&
    revision?.id === revisions[0]?.id &&
    !!revision?.frozenAt &&
    !!latestReview &&
    (["ClientChangesRequested", "Rejected"].includes(latestReview.status) ||
      (latestReview.status === "ClientApproved" && !latestReview.clientAcceptanceDocumentId));
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
  const waiverOptions = checklistKinds.filter(
    (value) =>
      !revision?.artifacts.some((artifact) => artifact.kind === value) &&
      !revision?.waivers.some((waiver) => waiver.kind === value),
  );
  const isDrawing = ["SiteRoofPlan", "FloorPlan", "Section"].includes(kind);
  const accept =
    kind === "Presentation"
      ? "application/pdf"
      : kind === "Perspective"
        ? "image/*"
        : isDrawing
          ? "application/pdf,image/*"
          : undefined;

  const resetFile = () => {
    setFile(null);
    if (fileInput.current) fileInput.current.value = "";
  };
  const selectRevision = (value: string) => {
    setSelectedRevisionId(Number(value));
    resetFile();
    setExistingDocumentId("");
    setFloorLabel("");
    setWaiverReason("");
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
      setError(translateError(t, extractApiError(reason)));
    } finally {
      setBusy(false);
    }
  };

  const attach = async () => {
    if (!revision || (kind === "FloorPlan" && !floorLabel.trim())) {
      setError(t("concepts.dossier.floorRequired"));
      return;
    }
    await run(async () => {
      let documentId = Number(existingDocumentId);
      if (file) {
        if (file.size > 100 * 1024 * 1024) throw new Error(t("concepts.dossier.fileTooLarge"));
        const uploaded = await adminApi.uploadProjectDocument(
          projectId,
          file,
          "DesignConcept",
          newIdempotencyKey(),
          { module: "Design", entityType: "ConceptOption", recordId: optionId },
        );
        documentId = uploaded.data.id;
        setExistingDocumentId(String(documentId));
        resetFile();
      }
      if (!documentId) throw new Error(t("concepts.dossier.fileRequired"));
      await adminApi.addConceptArtifact(optionId, revision.id, {
        projectDocumentId: documentId,
        kind,
        floorLabel: kind === "FloorPlan" ? floorLabel.trim() : null,
      });
      resetFile();
      setExistingDocumentId("");
      setFloorLabel("");
    });
  };

  return (
    <section
      id={`concept-dossier-${optionId}`}
      aria-label={t("concepts.dossier.open")}
      aria-busy={working}
      className="mt-4 min-w-0 space-y-5 border-t pt-4 text-sm"
      data-testid={`concept-dossier-${optionId}`}
    >
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="space-y-1.5">
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
                  R{String(item.number).padStart(2, "0")}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="flex flex-wrap gap-2">
          {canCreateRevision ? (
            <Button
              className="min-h-11"
              variant="outline"
              disabled={working}
              onClick={() =>
                void run(async () => {
                  const result = await adminApi.createConceptRevision(optionId);
                  selectRevision(String(result.data.id));
                })
              }
            >
              {t("concepts.dossier.newRevision")}
            </Button>
          ) : null}
          <Button
            className="min-h-11"
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
      </div>
      {error ? (
        <p role="alert" className="rounded-md border border-rose-200 bg-rose-50 p-3 text-rose-700">
          {error}
        </p>
      ) : null}
      {saved && !working ? (
        <p role="status" className="flex items-center gap-2 text-emerald-800">
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
      {revision ? (
        <>
          <p className="rounded-md bg-slate-50 p-3 text-slate-600">
            {revision.frozenAt ? t("concepts.dossier.frozen") : t("concepts.dossier.draftHint")}
          </p>
          {canManage && !locked && revision.frozenAt && !canCreateRevision ? (
            <p className="text-muted-foreground">{t("concepts.dossier.newRevisionHint")}</p>
          ) : null}
          <section aria-labelledby={`${prefix}-checklist`}>
            <h4 id={`${prefix}-checklist`} className="mb-2 font-semibold">
              {t("concepts.dossier.checklist")}
            </h4>
            <ul className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
              {checklistKinds.map((value) => {
                const artifacts = revision.artifacts.filter((artifact) => artifact.kind === value);
                const waiver = revision.waivers.find((item) => item.kind === value);
                return (
                  <li key={value} className="min-w-0 rounded-md border p-3">
                    <p className="font-medium">{t(`concepts.dossier.kind.${value}`)}</p>
                    <p
                      className={
                        artifacts.length
                          ? "mt-1 text-emerald-800"
                          : waiver
                            ? "mt-1 text-slate-600"
                            : "mt-1 text-amber-800"
                      }
                    >
                      {t(
                        artifacts.length
                          ? "concepts.dossier.attached"
                          : waiver
                            ? "concepts.dossier.notApplicable"
                            : "concepts.dossier.missing",
                      )}
                    </p>
                    {waiver ? (
                      <>
                        <p className="mt-1 whitespace-pre-wrap break-words text-slate-600">
                          {waiver.reason}
                        </p>
                        {editable ? (
                          <Button
                            className="mt-2 min-h-11"
                            size="sm"
                            variant="outline"
                            disabled={working}
                            onClick={() =>
                              void run(() =>
                                adminApi.removeConceptWaiver(optionId, revision.id, value),
                              )
                            }
                          >
                            {t("concepts.dossier.removeWaiver")}
                          </Button>
                        ) : null}
                      </>
                    ) : null}
                  </li>
                );
              })}
            </ul>
            <p className="mt-2 text-muted-foreground">{t("concepts.dossier.checklistHint")}</p>
          </section>
          <section aria-labelledby={`${prefix}-files`} className="space-y-3">
            <h4 id={`${prefix}-files`} className="font-semibold">
              {t("concepts.dossier.files")}
            </h4>
            {revision.artifacts.length ? (
              <ul className="divide-y rounded-md border">
                {revision.artifacts.map((artifact) => (
                  <li
                    key={artifact.id}
                    className="flex min-w-0 flex-wrap items-center justify-between gap-3 p-3"
                  >
                    <div className="min-w-0 flex-1 basis-52">
                      <p className="break-words font-medium">{artifact.fileName}</p>
                      <p className="mt-1 text-slate-600">
                        {t(`concepts.dossier.kind.${artifact.kind}`)}
                        {artifact.floorLabel ? ` · ${artifact.floorLabel}` : ""}
                      </p>
                      <p className="mt-1 text-slate-500">
                        {t(`operationalProjects.documents.syncStatus.${artifact.syncStatus}`)}
                      </p>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      <AdminFilePreview
                        fileName={artifact.fileName}
                        fetchFile={async () =>
                          (
                            await adminApi.downloadProjectDocument(
                              projectId,
                              artifact.projectDocumentId,
                            )
                          ).data
                        }
                        size="sm"
                      />
                      {editable ? (
                        <Button
                          className="min-h-11"
                          size="sm"
                          variant="outline"
                          disabled={working}
                          onClick={() =>
                            void run(() =>
                              adminApi.removeConceptArtifact(optionId, revision.id, artifact.id),
                            )
                          }
                        >
                          {t("concepts.dossier.detach")}
                        </Button>
                      ) : null}
                    </div>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="rounded-md border border-dashed p-4 text-muted-foreground">
                {t("concepts.dossier.noFiles")}
              </p>
            )}
            {editable ? (
              <div className="grid items-start gap-4 lg:grid-cols-2">
                <fieldset disabled={working} className="min-w-0 space-y-3 rounded-md border p-4">
                  <legend className="px-1 font-medium">{t("concepts.dossier.addFile")}</legend>
                  <div className="space-y-1.5">
                    <Label htmlFor={`${prefix}-kind`}>{t("concepts.dossier.documentKind")}</Label>
                    <Select
                      value={kind}
                      onValueChange={(value) => setKind(value as ConceptArtifactKind)}
                      disabled={working}
                    >
                      <SelectTrigger id={`${prefix}-kind`} className="min-h-11">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {kinds.map((value) => (
                          <SelectItem key={value} value={value}>
                            {t(`concepts.dossier.kind.${value}`)}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  {kind === "FloorPlan" ? (
                    <div className="space-y-1.5">
                      <Label htmlFor={`${prefix}-floor`}>{t("concepts.dossier.floorLabel")}</Label>
                      <Input
                        id={`${prefix}-floor`}
                        className="min-h-11"
                        value={floorLabel}
                        maxLength={80}
                        onChange={(event) => setFloorLabel(event.target.value)}
                      />
                    </div>
                  ) : null}
                  <div className="space-y-1.5">
                    <Label htmlFor={`${prefix}-file`}>{t("concepts.dossier.chooseFile")}</Label>
                    <Input
                      ref={fileInput}
                      id={`${prefix}-file`}
                      type="file"
                      accept={accept}
                      className="min-h-11"
                      aria-describedby={`${prefix}-file-hint`}
                      onChange={(event) => {
                        setFile(event.target.files?.[0] ?? null);
                        setExistingDocumentId("");
                      }}
                    />
                    <p id={`${prefix}-file-hint`} className="text-muted-foreground">
                      {t("concepts.dossier.fileHint")}
                    </p>
                  </div>
                  {available.length ? (
                    <div className="space-y-1.5">
                      <Label htmlFor={`${prefix}-existing`}>
                        {t("concepts.dossier.existingFile")}
                      </Label>
                      <Select
                        value={existingDocumentId}
                        onValueChange={(value) => {
                          setExistingDocumentId(value);
                          resetFile();
                        }}
                        disabled={working}
                      >
                        <SelectTrigger id={`${prefix}-existing`} className="min-h-11 min-w-0">
                          <SelectValue placeholder={t("concepts.dossier.existingFile")} />
                        </SelectTrigger>
                        <SelectContent>
                          {available.map((document) => (
                            <SelectItem key={document.id} value={String(document.id)}>
                              {document.originalFileName}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  ) : null}
                  <Button
                    className="min-h-11"
                    disabled={
                      working ||
                      (!file && !existingDocumentId) ||
                      (kind === "FloorPlan" && !floorLabel.trim())
                    }
                    onClick={() => void attach()}
                  >
                    {t("concepts.dossier.attach")}
                  </Button>
                </fieldset>
                {waiverOptions.length ? (
                  <fieldset disabled={working} className="min-w-0 space-y-3 rounded-md border p-4">
                    <legend className="px-1 font-medium">{t("concepts.dossier.waiver")}</legend>
                    <div className="space-y-1.5">
                      <Label htmlFor={`${prefix}-waiver-kind`}>
                        {t("concepts.dossier.documentKind")}
                      </Label>
                      <Select
                        value={waiverOptions.includes(waiverKind) ? waiverKind : ""}
                        onValueChange={(value) => setWaiverKind(value as ConceptArtifactKind)}
                        disabled={working}
                      >
                        <SelectTrigger id={`${prefix}-waiver-kind`} className="min-h-11">
                          <SelectValue placeholder={t("concepts.dossier.documentKind")} />
                        </SelectTrigger>
                        <SelectContent>
                          {waiverOptions.map((value) => (
                            <SelectItem key={value} value={value}>
                              {t(`concepts.dossier.kind.${value}`)}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-1.5">
                      <Label htmlFor={`${prefix}-waiver-reason`}>
                        {t("concepts.dossier.waiverReason")}
                      </Label>
                      <Textarea
                        id={`${prefix}-waiver-reason`}
                        rows={3}
                        maxLength={1000}
                        value={waiverReason}
                        onChange={(event) => setWaiverReason(event.target.value)}
                      />
                    </div>
                    <Button
                      className="min-h-11"
                      variant="outline"
                      disabled={
                        working ||
                        !waiverOptions.includes(waiverKind) ||
                        waiverReason.trim().length < 10
                      }
                      onClick={() =>
                        void run(async () => {
                          await adminApi.waiveConceptChecklist(optionId, revision.id, {
                            kind: waiverKind,
                            reason: waiverReason.trim(),
                          });
                          setWaiverReason("");
                        })
                      }
                    >
                      {t("concepts.dossier.recordWaiver")}
                    </Button>
                  </fieldset>
                ) : null}
              </div>
            ) : null}
          </section>
          <section aria-labelledby={`${prefix}-feedback`} className="space-y-3 border-t pt-4">
            <h4 id={`${prefix}-feedback`} className="font-semibold">
              {t("concepts.dossier.feedback")}
            </h4>
            <p className="text-slate-600">{t("concepts.dossier.feedbackHint")}</p>
            {!revision.feedback.length ? (
              <p className="rounded-md border border-dashed p-4 text-muted-foreground">
                {t("concepts.dossier.noFeedback")}
              </p>
            ) : null}
            {revision.feedback.map((item) => (
              <article key={item.id} className="min-w-0 space-y-3 rounded-md border p-4">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div className="min-w-0">
                    <h5 className="break-words font-medium">{item.customerName}</h5>
                    <p className="mt-1 break-words text-slate-500">
                      {item.channel} ·{" "}
                      <time dateTime={item.occurredAt}>
                        {new Date(item.occurredAt).toLocaleString(lang)}
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
                      <dt className="text-slate-500">{t("concepts.dossier.evidenceLabel")}</dt>
                      <dd className="mt-1 whitespace-pre-wrap break-words">
                        {item.evidenceSource}
                      </dd>
                    </div>
                  ) : null}
                  {item.projectDocumentId ? (
                    <div className="min-w-0">
                      <dt className="text-slate-500">
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
                      <dt className="text-slate-500">{t("concepts.dossier.response")}</dt>
                      <dd className="mt-1 whitespace-pre-wrap break-words">{item.response}</dd>
                    </div>
                  ) : null}
                  {item.outcome ? (
                    <div className="min-w-0">
                      <dt className="text-slate-500">{t("concepts.dossier.outcome")}</dt>
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
                <legend className="px-1 font-medium">{t("concepts.dossier.recordFeedback")}</legend>
                <p className="text-slate-500">{t("concepts.dossier.requiredFields")}</p>
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
                    <Label htmlFor={`${prefix}-occurred`}>{t("concepts.dossier.occurredAt")}</Label>
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
                          <SelectItem key={artifact.id} value={String(artifact.projectDocumentId)}>
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
                    <p id={`${prefix}-evidence-hint`} className="text-slate-500">
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
              <p className="text-slate-500">{t("concepts.dossier.feedbackUnavailable")}</p>
            ) : null}
          </section>
        </>
      ) : !loading && !error ? (
        <p className="py-4 text-slate-500">{t("concepts.dossier.empty")}</p>
      ) : null}
    </section>
  );
};
