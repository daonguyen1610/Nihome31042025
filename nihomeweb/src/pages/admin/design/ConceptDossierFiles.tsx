import { useEffect, useRef, useState } from "react";
import { CheckCircle2, CircleAlert, CircleDashed, CircleMinus, Upload } from "lucide-react";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { LocalizedFileInput } from "@/components/ui/localized-inputs";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { newIdempotencyKey } from "@/lib/api";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type ConceptArtifactKind,
  type ConceptRevisionResponse,
  type ProjectDocumentResponse,
} from "@/services/adminApi";

// Display order only: which items are required comes with each revision.
const rowKinds: ConceptArtifactKind[] = [
  "Perspective",
  "Model3D",
  "SiteRoofPlan",
  "FloorPlan",
  "Section",
  "Presentation",
  "Other",
];

type OpenForm = { kind: ConceptArtifactKind; mode: "file" | "waiver" } | null;

interface Props {
  projectId: number;
  optionId: number;
  revision: ConceptRevisionResponse;
  available: ProjectDocumentResponse[];
  editable: boolean;
  working: boolean;
  run: (action: () => Promise<unknown>) => Promise<void>;
}

const acceptFor = (kind: ConceptArtifactKind) =>
  kind === "Presentation"
    ? "application/pdf"
    : kind === "Perspective"
      ? "image/*"
      : kind === "SiteRoofPlan" || kind === "FloorPlan" || kind === "Section"
        ? "application/pdf,image/*"
        : undefined;

/** One row per dossier item: its status, its files and the actions for that item. */
export const ConceptDossierFiles = ({
  projectId,
  optionId,
  revision,
  available,
  editable,
  working,
  run,
}: Props) => {
  const { t } = useI18n();
  const prefix = `concept-${optionId}`;
  const [open, setOpen] = useState<OpenForm>(null);
  const [floorLabel, setFloorLabel] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [existingDocumentId, setExistingDocumentId] = useState("");
  const [waiverReason, setWaiverReason] = useState("");
  const [fileError, setFileError] = useState<string | null>(null);
  const formRef = useRef<HTMLFieldSetElement>(null);
  const opener = useRef<HTMLButtonElement | null>(null);
  const openKey = open ? `${open.kind}-${open.mode}` : "";

  useEffect(() => {
    if (openKey) formRef.current?.querySelector<HTMLElement>("input, textarea, button")?.focus();
  }, [openKey]);

  const reset = () => {
    setFloorLabel("");
    setFile(null);
    setExistingDocumentId("");
    setWaiverReason("");
    setFileError(null);
  };
  const openForm = (next: NonNullable<OpenForm>, trigger: HTMLButtonElement) => {
    reset();
    opener.current = trigger;
    setOpen(next);
  };
  const close = () => {
    reset();
    setOpen(null);
    opener.current?.focus();
  };

  const attach = (kind: ConceptArtifactKind) => {
    if (file && file.size > 100 * 1024 * 1024) {
      setFileError(t("concepts.dossier.fileTooLarge"));
      return;
    }
    void run(async () => {
      let documentId = Number(existingDocumentId);
      if (file) {
        const uploaded = await adminApi.uploadProjectDocument(
          projectId,
          file,
          "DesignConcept",
          newIdempotencyKey(),
          { module: "Design", entityType: "ConceptOption", recordId: optionId },
        );
        documentId = uploaded.data.id;
        // Keep the uploaded document selected, so a failed attach can be retried without uploading again.
        setExistingDocumentId(String(documentId));
        setFile(null);
      }
      await adminApi.addConceptArtifact(optionId, revision.id, {
        projectDocumentId: documentId,
        kind,
        floorLabel: kind === "FloorPlan" ? floorLabel.trim() : null,
      });
      reset();
      setOpen(null);
    });
  };

  const rows = rowKinds.map((kind) => {
    const artifacts = revision.artifacts.filter((artifact) => artifact.kind === kind);
    const waiver = revision.waivers.find((item) => item.kind === kind);
    const optional = !revision.requiredKinds.includes(kind);
    return { kind, artifacts, waiver, optional };
  });

  return (
    <div className="space-y-2">
      <p className="text-muted-foreground">{t("concepts.dossier.items.hint")}</p>
      <ul aria-label={t("concepts.dossier.items.title")} className="divide-y rounded-md border">
        {rows
          .filter((row) => editable || !row.optional || row.artifacts.length > 0)
          .map(({ kind, artifacts, waiver, optional }) => {
            const name = t(`concepts.dossier.kind.${kind}`);
            const needsVisual = kind === "Model3D" && !!revision.readiness?.needsVisual;
            const state = artifacts.length
              ? {
                  Icon: CheckCircle2,
                  tone: "text-success-strong",
                  key: "concepts.dossier.attached",
                }
              : waiver
                ? {
                    Icon: CircleMinus,
                    tone: "text-muted-foreground",
                    key: "concepts.dossier.notApplicable",
                  }
                : needsVisual
                  ? {
                      Icon: CircleAlert,
                      tone: "text-warning-strong",
                      key: "concepts.dossier.items.needVisual",
                    }
                  : optional
                    ? {
                        Icon: CircleDashed,
                        tone: "text-muted-foreground",
                        key: "concepts.dossier.items.optional",
                      }
                    : {
                        Icon: CircleAlert,
                        tone: "text-warning-strong",
                        key: "concepts.dossier.missing",
                      };
            const formId = `${prefix}-form-${kind}`;
            const isOpen = open?.kind === kind;
            return (
              <li key={kind} data-testid={`concept-item-${kind}`} className="min-w-0 p-3">
                <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-2">
                  <div className="flex min-w-0 flex-1 basis-56 items-start gap-2">
                    <state.Icon
                      aria-hidden="true"
                      className={`mt-0.5 h-4 w-4 shrink-0 ${state.tone}`}
                    />
                    <div className="min-w-0">
                      <h4 className="break-words font-medium">{name}</h4>
                      <p className={state.tone}>{t(state.key)}</p>
                    </div>
                  </div>
                  {editable ? (
                    <div className="flex flex-wrap gap-2">
                      {!waiver ? (
                        <Button
                          className="min-h-11"
                          size="sm"
                          variant="outline"
                          disabled={working}
                          aria-expanded={isOpen && open?.mode === "file"}
                          aria-controls={formId}
                          aria-label={`${t("concepts.dossier.items.addFile")}: ${name}`}
                          onClick={(event) => openForm({ kind, mode: "file" }, event.currentTarget)}
                        >
                          <Upload aria-hidden="true" className="mr-2 h-4 w-4" />
                          {t("concepts.dossier.items.addFile")}
                        </Button>
                      ) : null}
                      {!optional && !waiver && !artifacts.length ? (
                        <Button
                          className="min-h-11"
                          size="sm"
                          variant="ghost"
                          disabled={working}
                          aria-expanded={isOpen && open?.mode === "waiver"}
                          aria-controls={formId}
                          aria-label={`${t("concepts.dossier.items.skip")}: ${name}`}
                          onClick={(event) =>
                            openForm({ kind, mode: "waiver" }, event.currentTarget)
                          }
                        >
                          {t("concepts.dossier.items.skip")}
                        </Button>
                      ) : null}
                    </div>
                  ) : null}
                </div>

                {waiver ? (
                  <div className="mt-2 flex flex-wrap items-center justify-between gap-2 pl-6">
                    <p className="min-w-0 flex-1 basis-56 whitespace-pre-wrap break-words text-muted-foreground">
                      {waiver.reason}
                    </p>
                    {editable ? (
                      <Button
                        className="min-h-11"
                        size="sm"
                        variant="ghost"
                        disabled={working}
                        aria-label={`${t("concepts.dossier.removeWaiver")}: ${name}`}
                        onClick={() =>
                          void run(() => adminApi.removeConceptWaiver(optionId, revision.id, kind))
                        }
                      >
                        {t("concepts.dossier.removeWaiver")}
                      </Button>
                    ) : null}
                  </div>
                ) : null}

                {artifacts.length ? (
                  <ul className="mt-2 space-y-1 pl-6">
                    {artifacts.map((artifact) => (
                      <li
                        key={artifact.id}
                        className="flex min-w-0 flex-wrap items-center justify-between gap-2 rounded-md bg-muted/40 py-1 pl-3 pr-1"
                      >
                        <div className="min-w-0 flex-1 basis-52">
                          <p className="break-words">{artifact.fileName}</p>
                          <p className="text-xs text-muted-foreground">
                            {artifact.floorLabel ? `${artifact.floorLabel} · ` : ""}
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
                              variant="ghost"
                              disabled={working}
                              aria-label={`${t("concepts.dossier.detach")}: ${artifact.fileName}`}
                              onClick={() =>
                                void run(() =>
                                  adminApi.removeConceptArtifact(
                                    optionId,
                                    revision.id,
                                    artifact.id,
                                  ),
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
                ) : null}

                {editable && isOpen && open?.mode === "file" ? (
                  <fieldset
                    ref={formRef}
                    id={formId}
                    disabled={working}
                    aria-label={`${t("concepts.dossier.items.addFile")}: ${name}`}
                    className="mt-3 min-w-0 max-w-xl space-y-3 rounded-md bg-muted/40 p-3"
                  >
                    {kind === "FloorPlan" ? (
                      <div className="space-y-1.5">
                        <Label htmlFor={`${prefix}-floor`}>
                          {t("concepts.dossier.floorLabel")} *
                        </Label>
                        <Input
                          id={`${prefix}-floor`}
                          className="min-h-11 bg-card"
                          value={floorLabel}
                          maxLength={80}
                          required
                          aria-required="true"
                          onChange={(event) => setFloorLabel(event.target.value)}
                        />
                      </div>
                    ) : null}
                    <div className="space-y-1.5">
                      <Label htmlFor={`${prefix}-file`}>{t("concepts.dossier.chooseFile")}</Label>
                      <LocalizedFileInput
                        id={`${prefix}-file`}
                        accept={acceptFor(kind)}
                        selectedFileName={file?.name}
                        aria-describedby={`${prefix}-file-hint`}
                        onChange={(event) => {
                          setFile(event.target.files?.[0] ?? null);
                          setExistingDocumentId("");
                          setFileError(null);
                        }}
                      />
                      <p id={`${prefix}-file-hint`} className="text-xs text-muted-foreground">
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
                            setFile(null);
                          }}
                          disabled={working}
                        >
                          <SelectTrigger
                            id={`${prefix}-existing`}
                            className="min-h-11 min-w-0 bg-card"
                          >
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
                    {fileError ? (
                      <p role="alert" className="text-destructive">
                        {fileError}
                      </p>
                    ) : null}
                    <div className="flex flex-wrap gap-2">
                      <Button
                        className="min-h-11"
                        disabled={
                          working ||
                          (!file && !existingDocumentId) ||
                          (kind === "FloorPlan" && !floorLabel.trim())
                        }
                        onClick={() => attach(kind)}
                      >
                        {t("concepts.dossier.attach")}
                      </Button>
                      <Button className="min-h-11" variant="ghost" onClick={close}>
                        {t("common.cancel")}
                      </Button>
                    </div>
                  </fieldset>
                ) : null}

                {editable && isOpen && open?.mode === "waiver" ? (
                  <fieldset
                    ref={formRef}
                    id={formId}
                    disabled={working}
                    aria-label={`${t("concepts.dossier.items.skip")}: ${name}`}
                    className="mt-3 min-w-0 max-w-xl space-y-3 rounded-md bg-muted/40 p-3"
                  >
                    <div className="space-y-1.5">
                      <Label htmlFor={`${prefix}-waiver-reason`}>
                        {t("concepts.dossier.waiverReason")}
                      </Label>
                      <Textarea
                        id={`${prefix}-waiver-reason`}
                        rows={3}
                        maxLength={1000}
                        className="bg-card"
                        value={waiverReason}
                        aria-describedby={`${prefix}-waiver-hint`}
                        onChange={(event) => setWaiverReason(event.target.value)}
                      />
                      <p id={`${prefix}-waiver-hint`} className="text-xs text-muted-foreground">
                        {t("concepts.dossier.items.skipHint")}
                      </p>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Button
                        className="min-h-11"
                        disabled={working || waiverReason.trim().length < 10}
                        onClick={() =>
                          void run(async () => {
                            await adminApi.waiveConceptChecklist(optionId, revision.id, {
                              kind,
                              reason: waiverReason.trim(),
                            });
                            reset();
                            setOpen(null);
                          })
                        }
                      >
                        {t("concepts.dossier.recordWaiver")}
                      </Button>
                      <Button className="min-h-11" variant="ghost" onClick={close}>
                        {t("common.cancel")}
                      </Button>
                    </div>
                  </fieldset>
                ) : null}
              </li>
            );
          })}
      </ul>
    </div>
  );
};
