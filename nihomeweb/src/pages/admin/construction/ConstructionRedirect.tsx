import { Navigate, useLocation } from "react-router-dom";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";

interface Destination {
  path: string;
  tab?: string;
}

function RedirectTo({ path, tab }: Destination) {
  const location = useLocation();
  const params = new URLSearchParams(location.search);
  params.delete("area");
  if (tab) params.set("tab", tab);
  const search = params.toString();
  return <Navigate to={`${path}${search ? `?${search}` : ""}${location.hash}`} replace />;
}

/** Keeps every query param except `area`, so project/task/record deep links survive. */
export function LegacyConstructionRedirect(destination: Destination) {
  return <RedirectTo {...destination} />;
}

const CM_AREAS: (Destination & { area: string; permissions: readonly string[] })[] = [
  {
    area: "schedule",
    path: "/admin/construction/cm/schedule",
    permissions: [ADMIN_PERMS.constructionCmPreparation, ADMIN_PERMS.constructionTasks],
  },
  {
    area: "commercial",
    path: "/admin/construction/cm/commercial",
    permissions: [
      ADMIN_PERMS.constructionResourceRequestsReview,
      ADMIN_PERMS.constructionResourceRequests,
      ADMIN_PERMS.constructionQsReview,
      ADMIN_PERMS.constructionQs,
    ],
  },
  {
    area: "qa-qc",
    path: "/admin/construction/cm/quality",
    tab: "dossiers",
    permissions: [ADMIN_PERMS.constructionQaQcReview, ADMIN_PERMS.constructionQaQc],
  },
  {
    area: "hse",
    path: "/admin/construction/cm/safety",
    permissions: [ADMIN_PERMS.constructionHseConfirm, ADMIN_PERMS.constructionHse],
  },
  {
    area: "contracts",
    path: "/admin/construction/cm/contracts",
    permissions: [ADMIN_PERMS.contracts],
  },
];

export function LegacyCmAreaRedirect() {
  const location = useLocation();
  const { has } = usePermissions();
  const requested = new URLSearchParams(location.search).get("area");
  const allowed = (entry: (typeof CM_AREAS)[number]) =>
    entry.permissions.some((permission) => has(permission));
  const requestedEntry = CM_AREAS.find((entry) => entry.area === requested);
  const destination =
    (requestedEntry && allowed(requestedEntry) ? requestedEntry : undefined) ??
    CM_AREAS.find(allowed) ??
    CM_AREAS[0];
  return <RedirectTo path={destination.path} tab={destination.tab} />;
}

export function LegacyPunchRedirect() {
  const { has } = usePermissions();
  return has(ADMIN_PERMS.constructionPunchVerify) ? (
    <RedirectTo path="/admin/construction/cm/quality" tab="punchlist" />
  ) : (
    <RedirectTo path="/admin/construction/bch/qa-qc" tab="punchlist" />
  );
}

export function LegacyAcceptanceRedirect() {
  const { has } = usePermissions();
  return has(ADMIN_PERMS.constructionAcceptanceApprove) ? (
    <RedirectTo path="/admin/construction/cm/quality" tab="acceptance" />
  ) : (
    <RedirectTo path="/admin/construction/bch/qa-qc" tab="acceptance" />
  );
}

export function LegacyHseRedirect() {
  const { has } = usePermissions();
  return has(ADMIN_PERMS.constructionHseConfirm) || has(ADMIN_PERMS.constructionHseClose) ? (
    <RedirectTo path="/admin/construction/cm/safety" />
  ) : (
    <RedirectTo path="/admin/construction/bch/qa-qc" tab="hse" />
  );
}
