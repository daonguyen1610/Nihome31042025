import { useCallback, useEffect, useMemo, useState } from "react";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
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
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type ConceptArtifactKind,
  type ConceptRevisionResponse,
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
const checklistKinds = kinds.filter((kind) => kind !== "Model3D" && kind !== "Other");

interface Props {
  projectId: number;
  optionId: number;
  canManage: boolean;
  locked: boolean;
  onChanged: () => void | Promise<void>;
}

export const ConceptDossierPanel = ({
  projectId,
  optionId,
  canManage,
  locked,
  onChanged,
}: Props) => {
  const { t, lang } = useI18n();
  const [revisions, setRevisions] = useState<ConceptRevisionResponse[]>([]);
  const [documents, setDocuments] = useState<ProjectDocumentResponse[]>([]);
  const [selectedRevisionId, setSelectedRevisionId] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [kind, setKind] = useState<ConceptArtifactKind>("Perspective");
  const [floorLabel, setFloorLabel] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [waiverKind, setWaiverKind] = useState<ConceptArtifactKind>("Section");
  const [waiverReason, setWaiverReason] = useState("");
  const [customerName, setCustomerName] = useState("");
  const [channel, setChannel] = useState("");
  const [occurredAt, setOccurredAt] = useState("");
  const [comment, setComment] = useState("");
  const [commentDocumentId, setCommentDocumentId] = useState("");
  const [responses, setResponses] = useState<Record<number, string>>({});

  const load = useCallback(async () => {
    try {
      const [revisionResult, documentResult] = await Promise.all([
        adminApi.listConceptRevisions(optionId),
        adminApi.listProjectDocuments(projectId),
      ]);
      setRevisions(revisionResult.data);
      setDocuments(documentResult.data);
      setSelectedRevisionId((current) =>
        current && revisionResult.data.some((revision) => revision.id === current)
          ? current
          : (revisionResult.data[0]?.id ?? null),
      );
      setError(null);
    } catch (reason) {
      setError(extractApiError(reason));
    }
  }, [optionId, projectId]);

  useEffect(() => {
    void load();
  }, [load]);

  const revision = revisions.find((item) => item.id === selectedRevisionId);
  const editable =
    canManage && !locked && !!revision && !revision.frozenAt && revision.id === revisions[0]?.id;
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
  const [existingDocumentId, setExistingDocumentId] = useState("");

  const run = async (action: () => Promise<unknown>) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      await load();
      await onChanged();
    } catch (reason) {
      setError(extractApiError(reason));
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
        if (file.size > 100 * 1024 * 1024) {
          throw new Error(t("concepts.dossier.fileTooLarge"));
        }
        const uploaded = await adminApi.uploadProjectDocument(
          projectId,
          file,
          "DesignConcept",
          newIdempotencyKey(),
          { module: "Design", entityType: "ConceptOption", recordId: optionId },
        );
        documentId = uploaded.data.id;
      }
      if (!documentId) throw new Error(t("concepts.dossier.fileRequired"));
      await adminApi.addConceptArtifact(optionId, revision.id, {
        projectDocumentId: documentId,
        kind,
        floorLabel: kind === "FloorPlan" ? floorLabel.trim() : null,
      });
      setFile(null);
      setExistingDocumentId("");
      setFloorLabel("");
    });
  };

  return (
    <section
      className="mt-3 space-y-3 border-t pt-3 text-sm"
      data-testid={`concept-dossier-${optionId}`}
    >
      <div className="flex flex-wrap items-center gap-2">
        <Label>{t("concepts.dossier.revision")}</Label>
        <Select
          value={selectedRevisionId ? String(selectedRevisionId) : ""}
          onValueChange={(value) => setSelectedRevisionId(Number(value))}
        >
          <SelectTrigger className="min-h-10 w-36">
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
        {canManage && !locked && revisions[0]?.frozenAt ? (
          <Button
            size="sm"
            variant="outline"
            disabled={busy}
            onClick={() => void run(() => adminApi.createConceptRevision(optionId))}
          >
            {t("concepts.dossier.newRevision")}
          </Button>
        ) : null}
        <Button size="sm" variant="ghost" onClick={() => void load()}>
          {t("common.retry")}
        </Button>
      </div>
      {error ? (
        <p role="alert" className="rounded bg-rose-50 p-2 text-rose-700">
          {error}
        </p>
      ) : null}
      {revision ? (
        <>
          <p className="text-xs text-slate-500">
            {revision.frozenAt ? t("concepts.dossier.frozen") : t("concepts.dossier.draft")}
          </p>
          <ul className="space-y-1">
            {revision.artifacts.map((artifact) => (
              <li
                key={artifact.id}
                className="flex flex-wrap items-center justify-between gap-2 rounded border px-2 py-1"
              >
                <span className="min-w-0 break-all">
                  {t(`concepts.dossier.kind.${artifact.kind}`)}
                  {artifact.floorLabel ? ` · ${artifact.floorLabel}` : ""}: {artifact.fileName}
                  <span className="ml-1 text-xs text-slate-500">({artifact.syncStatus})</span>
                </span>
                <span className="flex items-center gap-1">
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
                      size="sm"
                      variant="ghost"
                      disabled={busy}
                      onClick={() =>
                        void run(() =>
                          adminApi.removeConceptArtifact(optionId, revision.id, artifact.id),
                        )
                      }
                    >
                      {t("concepts.dossier.detach")}
                    </Button>
                  ) : null}
                </span>
              </li>
            ))}
          </ul>
          {editable ? (
            <div className="space-y-2 rounded border p-3">
              <Label>{t("concepts.dossier.addFile")}</Label>
              <Select value={kind} onValueChange={(value) => setKind(value as ConceptArtifactKind)}>
                <SelectTrigger>
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
              {kind === "FloorPlan" ? (
                <Input
                  value={floorLabel}
                  maxLength={80}
                  placeholder={t("concepts.dossier.floorLabel")}
                  onChange={(event) => setFloorLabel(event.target.value)}
                />
              ) : null}
              <Input type="file" onChange={(event) => setFile(event.target.files?.[0] ?? null)} />
              {available.length ? (
                <Select value={existingDocumentId} onValueChange={setExistingDocumentId}>
                  <SelectTrigger>
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
              ) : null}
              <Button
                size="sm"
                disabled={busy || (!file && !existingDocumentId)}
                onClick={() => void attach()}
              >
                {t("concepts.dossier.attach")}
              </Button>
            </div>
          ) : null}
          {revision.waivers.length ? (
            <ul className="space-y-1 text-xs text-amber-800">
              {revision.waivers.map((waiver) => (
                <li key={waiver.kind} className="flex items-center gap-2">
                  {t(`concepts.dossier.kind.${waiver.kind}`)}: {waiver.reason}
                  {editable ? (
                    <Button
                      size="sm"
                      variant="ghost"
                      disabled={busy}
                      onClick={() =>
                        void run(() =>
                          adminApi.removeConceptWaiver(
                            optionId,
                            revision.id,
                            waiver.kind as ConceptArtifactKind,
                          ),
                        )
                      }
                    >
                      {t("concepts.dossier.removeWaiver")}
                    </Button>
                  ) : null}
                </li>
              ))}
            </ul>
          ) : null}
          {editable ? (
            <div className="space-y-2 rounded border p-3">
              <Label>{t("concepts.dossier.waiver")}</Label>
              <Select
                value={waiverKind}
                onValueChange={(value) => setWaiverKind(value as ConceptArtifactKind)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {checklistKinds.map((value) => (
                    <SelectItem key={value} value={value}>
                      {t(`concepts.dossier.kind.${value}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Textarea
                maxLength={1000}
                value={waiverReason}
                onChange={(event) => setWaiverReason(event.target.value)}
                placeholder={t("concepts.dossier.waiverReason")}
              />
              <Button
                size="sm"
                variant="outline"
                disabled={busy || waiverReason.trim().length < 10}
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
            </div>
          ) : null}
          <div className="space-y-2">
            <h4 className="font-medium">{t("concepts.dossier.feedback")}</h4>
            {revision.feedback.map((item) => (
              <div key={item.id} className="space-y-1 rounded border p-2">
                <p className="text-xs text-slate-500">
                  {item.customerName} · {item.channel} ·{" "}
                  {new Date(item.occurredAt).toLocaleString(lang)}
                </p>
                <p className="whitespace-pre-wrap">{item.comment}</p>
                {item.response ? (
                  <p className="whitespace-pre-wrap text-emerald-800">{item.response}</p>
                ) : null}
                {!item.response && canManage ? (
                  <div className="flex flex-wrap gap-2">
                    <Input
                      className="min-w-40 flex-1"
                      maxLength={4000}
                      value={responses[item.id] ?? ""}
                      placeholder={t("concepts.dossier.response")}
                      onChange={(event) =>
                        setResponses((current) => ({ ...current, [item.id]: event.target.value }))
                      }
                    />
                    <Button
                      size="sm"
                      disabled={busy || !responses[item.id]?.trim()}
                      onClick={() =>
                        void run(() =>
                          adminApi.respondConceptFeedback(
                            optionId,
                            revision.id,
                            item.id,
                            responses[item.id].trim(),
                          ),
                        )
                      }
                    >
                      {t("concepts.dossier.respond")}
                    </Button>
                  </div>
                ) : null}
              </div>
            ))}
            {canManage && revision.frozenAt ? (
              <div className="space-y-2 rounded border p-3">
                <Input
                  maxLength={200}
                  value={customerName}
                  placeholder={t("concepts.dossier.customerName")}
                  onChange={(event) => setCustomerName(event.target.value)}
                />
                <Input
                  maxLength={80}
                  value={channel}
                  placeholder={t("concepts.dossier.channel")}
                  onChange={(event) => setChannel(event.target.value)}
                />
                <Input
                  type="datetime-local"
                  value={occurredAt}
                  onChange={(event) => setOccurredAt(event.target.value)}
                />
                <Textarea
                  maxLength={4000}
                  value={comment}
                  placeholder={t("concepts.dossier.comment")}
                  onChange={(event) => setComment(event.target.value)}
                />
                <Select
                  value={commentDocumentId || "general"}
                  onValueChange={(value) => setCommentDocumentId(value === "general" ? "" : value)}
                >
                  <SelectTrigger>
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
                <Button
                  size="sm"
                  disabled={
                    busy ||
                    !customerName.trim() ||
                    !channel.trim() ||
                    !occurredAt ||
                    !comment.trim()
                  }
                  onClick={() =>
                    void run(async () => {
                      await adminApi.addConceptFeedback(optionId, revision.id, {
                        customerName: customerName.trim(),
                        channel: channel.trim(),
                        occurredAt: new Date(occurredAt).toISOString(),
                        comment: comment.trim(),
                        projectDocumentId: commentDocumentId ? Number(commentDocumentId) : null,
                      });
                      setComment("");
                    })
                  }
                >
                  {t("concepts.dossier.recordFeedback")}
                </Button>
              </div>
            ) : null}
          </div>
        </>
      ) : (
        <p className="text-slate-500">{t("concepts.dossier.empty")}</p>
      )}
    </section>
  );
};
