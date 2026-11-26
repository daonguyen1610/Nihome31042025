import { useEffect, useState } from "react";
import { isAxiosError } from "axios";
import { FolderTree, RefreshCw, Undo2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useConfirm } from "@/components/ui/confirmation";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { useUiError } from "@/hooks/useUiError";
import { formatNumber } from "@/lib/displaySettings";
import { driveTreeActions } from "@/lib/driveTreeMigration";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type DriveTreeMigrationPreview,
  type DriveTreeMigrationResult,
} from "@/services/adminApi";

const KEY = "settings.drive.treeMigration";

const BLOCKER_CODES = [
  "DRIVE_SYNC_ENABLED",
  "DRIVE_DOCUMENTS_PROCESSING",
  "DRIVE_NOT_CONNECTED",
  "DRIVE_TREE_ALREADY_STANDARD",
  "DRIVE_TREE_MIGRATION_RUNNING",
  "DRIVE_TREE_MIGRATION_NOT_COMPLETED",
  "DRIVE_TREE_UNDO_IN_PROGRESS",
];

/** The folder settings label of a document category; Survey is stored as "surveyMedia". */
const folderLabelKey = (category: string) =>
  `settings.drive.configuration.folder.${
    category === "Survey" ? "surveyMedia" : category[0].toLowerCase() + category.slice(1)
  }`;

type Operation = "start" | "resume" | "undo";

type Props = {
  canManage: boolean;
  reloadKey: number;
  /** A run or undo ended: the saved folder paths may have changed. */
  onChanged: () => void;
  onBusyChange: (busy: boolean) => void;
};

export default function DriveTreeMigrationPanel({
  canManage,
  reloadKey,
  onChanged,
  onBusyChange,
}: Props) {
  const { t, lang } = useI18n();
  const uiError = useUiError();
  const confirmAction = useConfirm();
  const [preview, setPreview] = useState<DriveTreeMigrationPreview | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const [running, setRunning] = useState<Operation | null>(null);
  const [result, setResult] = useState<(DriveTreeMigrationResult & { undo: boolean }) | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setLoadFailed(false);
    adminApi
      .getDriveTreeMigrationPreview()
      .then(({ data }) => {
        if (!cancelled) setPreview(data);
      })
      .catch(() => {
        // Without a fresh preview no action is known to be safe.
        if (!cancelled) {
          setPreview(null);
          setLoadFailed(true);
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [reloadKey, attempt]);

  const actions = preview ? driveTreeActions(preview) : null;
  const count = (value: number) => formatNumber(value, lang);
  const untrackedCounted = actions?.forward === "start" && actions.forwardBlockers.length === 0;
  const blockers = actions
    ? [...new Set([...actions.forwardBlockers, ...actions.undoBlockers])]
    : [];
  const blockerText = (code: string) =>
    BLOCKER_CODES.includes(code) ? t(`${KEY}.blocker.${code}`) : code;

  const execute = async (operation: Operation) => {
    if (!preview) return;
    const migrationId = preview.latestMigration?.id;
    const message = t(`${KEY}.confirm.${operation}`, {
      total: count(preview.documentCount + preview.untrackedFileCount),
      documents: count(preview.documentCount),
      untracked: count(preview.untrackedFileCount),
      projects: count(preview.projectCount),
    });
    const confirmed = await confirmAction(message, {
      destructive: false,
      confirmLabel: t(`${KEY}.action.${operation}`),
    });
    if (!confirmed) return;

    setRunning(operation);
    onBusyChange(true);
    setResult(null);
    setActionError(null);
    try {
      const { data } =
        operation === "undo"
          ? await adminApi.undoDriveTreeMigration(migrationId!)
          : await adminApi.runDriveTreeMigration();
      setResult({ ...data, undo: operation === "undo" });
    } catch (error) {
      const response = isAxiosError<{ code?: string }>(error) ? error.response : undefined;
      const code = response?.data?.code;
      if (code && BLOCKER_CODES.includes(code)) setActionError(blockerText(code));
      // No answer, or a proxy timeout: the server may still be moving files.
      else if (!response || response.status >= 500) setActionError(t(`${KEY}.noResponse`));
      else setActionError(uiError(error, t(`${KEY}.actionFailed`)));
    } finally {
      setRunning(null);
      onBusyChange(false);
      setLoading(true);
      setAttempt((value) => value + 1);
      onChanged();
    }
  };

  const busy = loading || running !== null;
  const errorDetail = (detail: string) => (
    <p className="mt-1 break-words text-xs">
      {t(`${KEY}.errorDetail`)} {detail}
    </p>
  );

  return (
    <div className="min-w-0 space-y-4 border-t pt-5" data-testid="drive-tree-migration">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h3 className="text-sm font-semibold">{t(`${KEY}.title`)}</h3>
          <p className="mt-1 text-xs text-muted-foreground">{t(`${KEY}.description`)}</p>
        </div>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={() => {
            setActionError(null);
            setAttempt((value) => value + 1);
          }}
          disabled={busy}
        >
          <RefreshCw className={`mr-1.5 h-4 w-4 ${loading ? "animate-spin" : ""}`} />
          {t(`${KEY}.refresh`)}
        </Button>
      </div>

      {running && (
        <p className="rounded-md bg-info-soft px-3 py-2 text-sm text-info-strong" role="status">
          {t(`${KEY}.inProgress`)}
        </p>
      )}

      {actionError && (
        <p
          className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
          role="alert"
        >
          {actionError}
        </p>
      )}

      {result && (
        <div
          className={`rounded-md px-3 py-2 text-sm ${
            result.status === "Failed"
              ? "bg-destructive/10 text-destructive"
              : "bg-success-soft text-success-strong"
          }`}
          role="status"
          data-testid="drive-tree-migration-result"
        >
          <p className="font-medium">
            {t(`${KEY}.result.${result.undo ? "undo" : "run"}.${result.status}`)}
          </p>
          <p className="mt-1">
            {t(`${KEY}.result.${result.undo ? "undo" : "run"}.counts`, {
              moved: count(result.moved),
              skipped: count(result.skipped),
            })}
          </p>
          {result.skipped > 0 && <p className="mt-1">{t(`${KEY}.result.skippedHelp`)}</p>}
          {result.error && errorDetail(result.error)}
        </div>
      )}

      {loading && !preview && (
        <p className="text-sm text-muted-foreground" role="status">
          {t(`${KEY}.loading`)}
        </p>
      )}
      {loadFailed && (
        <p className="text-sm text-destructive" role="alert">
          {t(`${KEY}.loadError`)}
        </p>
      )}

      {preview && actions && (
        <>
          {actions.state !== "ready" && (
            <div
              className={`rounded-md px-3 py-2 text-sm ${
                actions.state === "completed" || actions.state === "rolledBack"
                  ? "bg-info-soft text-info-strong"
                  : "bg-warning-soft text-warning-strong"
              }`}
              data-testid="drive-tree-migration-state"
            >
              <p>{t(`${KEY}.state.${actions.state}`)}</p>
              {!result &&
                preview.latestMigration?.error &&
                errorDetail(preview.latestMigration.error)}
            </div>
          )}

          <dl className="grid gap-3 sm:grid-cols-3">
            {(
              [
                ["projects", count(preview.projectCount)],
                ["documents", count(preview.documentCount)],
                ["untracked", untrackedCounted ? count(preview.untrackedFileCount) : "—"],
              ] as const
            )
              // Only a new move counts unknown files, so other states have no number to show.
              .filter(([name]) => name !== "untracked" || actions.forward === "start")
              .map(([name, value]) => (
                <div key={name} className="rounded-lg border bg-muted/20 p-3">
                  <dt className="text-xs text-muted-foreground">{t(`${KEY}.count.${name}`)}</dt>
                  <dd className="mt-1 font-medium tabular-nums">{value}</dd>
                </div>
              ))}
          </dl>
          {!untrackedCounted && actions.forward === "start" && (
            <p className="text-xs text-muted-foreground">{t(`${KEY}.count.untrackedPending`)}</p>
          )}

          {preview.changes.length > 0 && (
            // Phones stack each row so long paths stay readable without sideways scrolling.
            <Table aria-label={t(`${KEY}.table.label`)}>
              <TableHeader className="hidden sm:table-header-group">
                <TableRow>
                  <TableHead>{t(`${KEY}.table.category`)}</TableHead>
                  <TableHead>{t(`${KEY}.table.from`)}</TableHead>
                  <TableHead>{t(`${KEY}.table.to`)}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {preview.changes.map((change) => (
                  <TableRow key={change.category} className="block py-2 sm:table-row sm:py-0">
                    <TableCell className="block px-0 py-1 font-medium sm:table-cell sm:p-3">
                      {t(folderLabelKey(change.category))}
                    </TableCell>
                    {(["from", "to"] as const).map((side) => (
                      <TableCell
                        key={side}
                        className="block break-all px-0 py-1 sm:table-cell sm:p-3"
                      >
                        <span className="block text-xs text-muted-foreground sm:hidden">
                          {t(`${KEY}.table.${side}`)}
                        </span>
                        {change[side]}
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}

          {blockers.length > 0 && (
            <ul
              id="drive-tree-migration-blockers"
              className="space-y-1 rounded-md border border-warning/40 bg-warning-soft px-3 py-2 text-sm text-warning-strong"
            >
              {blockers.map((code) => (
                <li key={code}>{blockerText(code)}</li>
              ))}
            </ul>
          )}

          {canManage ? (
            <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap">
              {actions.forward && (
                <Button
                  type="button"
                  variant="outline"
                  className="h-auto min-h-10 whitespace-normal"
                  onClick={() => void execute(actions.forward!)}
                  disabled={busy || actions.forwardBlockers.length > 0}
                  aria-describedby={
                    actions.forwardBlockers.length > 0 ? "drive-tree-migration-blockers" : undefined
                  }
                >
                  <FolderTree className="mr-1.5 h-4 w-4 shrink-0" />
                  {t(`${KEY}.action.${actions.forward}`)}
                </Button>
              )}
              {actions.undo && (
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => void execute("undo")}
                  disabled={busy || actions.undoBlockers.length > 0}
                  aria-describedby={
                    actions.undoBlockers.length > 0 ? "drive-tree-migration-blockers" : undefined
                  }
                >
                  <Undo2 className="mr-1.5 h-4 w-4" />
                  {t(`${KEY}.action.undo`)}
                </Button>
              )}
            </div>
          ) : (
            <p className="text-xs text-muted-foreground">
              {t("settings.drive.managePermissionRequired")}
            </p>
          )}
        </>
      )}
    </div>
  );
}
