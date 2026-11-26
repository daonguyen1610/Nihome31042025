import type { DriveTreeMigrationPreview } from "@/services/admin/types/content";

/** Blockers that also stop an undo; the others concern only moving forward. */
const UNDO_BLOCKERS = ["DRIVE_SYNC_ENABLED", "DRIVE_DOCUMENTS_PROCESSING", "DRIVE_NOT_CONNECTED"];

/** Blockers the stopped run causes itself; the server ignores them when resuming. */
const IGNORED_ON_RESUME = ["DRIVE_TREE_MIGRATION_RUNNING", "DRIVE_TREE_ALREADY_STANDARD"];

export type DriveTreeActions = {
  state: "ready" | "rolledBack" | "completed" | "running" | "failed" | "undoStarted" | "unknown";
  forward: "start" | "resume" | null;
  undo: boolean;
  forwardBlockers: string[];
  undoBlockers: string[];
};

/** The only actions that are safe to offer for what the server last recorded. */
export function driveTreeActions({
  blockers,
  latestMigration: latest,
}: Pick<DriveTreeMigrationPreview, "blockers" | "latestMigration">): DriveTreeActions {
  const offer = (
    state: DriveTreeActions["state"],
    forward: DriveTreeActions["forward"],
    undo: boolean,
  ): DriveTreeActions => ({
    state,
    forward,
    undo,
    forwardBlockers:
      forward === null
        ? []
        : blockers.filter((code) => forward === "start" || !IGNORED_ON_RESUME.includes(code)),
    undoBlockers: undo ? blockers.filter((code) => UNDO_BLOCKERS.includes(code)) : [],
  });

  if (!latest) return offer("ready", "start", false);
  if (latest.status === "RolledBack") return offer("rolledBack", "start", false);
  // A half-done undo may already have returned files, so only finishing it is safe.
  if (latest.undoStarted) return offer("undoStarted", null, true);
  if (latest.status === "Completed") return offer("completed", null, true);
  if (latest.status === "Failed") return offer("failed", "resume", true);
  if (latest.status === "Running") return offer("running", "resume", false);
  return offer("unknown", null, false);
}
