import { useCallback, useEffect, useMemo, useState } from "react";
import { Loader2, RefreshCw, UserRound, Users } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";
import { extractApiError } from "@/lib/apiError";
import { adminApi, type DesignProjectResponse } from "@/services/adminApi";

interface Props {
  project: DesignProjectResponse;
}

interface TeamMember {
  key: string;
  name: string;
  roles: string[];
}

const addMember = (
  members: Map<string, TeamMember>,
  userId: number | null | undefined,
  name: string | null | undefined,
  role: string,
) => {
  if (!name) return;
  const key = userId != null ? `user-${userId}` : `name-${name.toLocaleLowerCase()}`;
  const existing = members.get(key);
  if (existing) {
    if (!existing.roles.includes(role)) existing.roles.push(role);
    return;
  }
  members.set(key, { key, name, roles: [role] });
};

export const DesignProjectTeamTab = ({ project }: Props) => {
  const { t } = useI18n();
  const [members, setMembers] = useState<TeamMember[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [concepts, basicDocs, shopDrawings, ifcReleases] = await Promise.all([
        adminApi.listConceptOptions({ designProjectId: project.id, pageSize: 200 }),
        adminApi.listBasicDesignDocs({ designProjectId: project.id, pageSize: 200 }),
        adminApi.listShopDrawings({ designProjectId: project.id, pageSize: 200 }),
        adminApi.listIfcReleases({ designProjectId: project.id, pageSize: 200 }),
      ]);

      const next = new Map<string, TeamMember>();
      addMember(next, project.projectManagerUserId, project.projectManagerName, t("designProjects.team.role.projectManager"));
      addMember(next, project.designLeadUserId, project.designLeadName, t("designProjects.team.role.designLead"));
      concepts.data.items.forEach((item) => addMember(next, item.ownerUserId, item.ownerName, t("designProjects.team.role.concept")));
      basicDocs.data.items.forEach((item) => addMember(next, item.ownerUserId, item.ownerName, t("designProjects.team.role.basic")));
      shopDrawings.data.items.forEach((item) => addMember(next, item.ownerUserId, item.ownerName, t("designProjects.team.role.shop")));
      ifcReleases.data.items.forEach((item) => addMember(next, item.issuedByUserId, item.issuedByName, t("designProjects.team.role.ifc")));
      setMembers(Array.from(next.values()));
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setLoading(false);
    }
  }, [project, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const assignedCount = useMemo(() => members.length, [members]);

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm" data-testid="design-project-team-tab">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="flex items-center gap-2 text-base font-semibold text-slate-900">
            <Users className="h-4 w-4 text-slate-500" />
            {t("designProjects.team.title")}
          </h2>
          <p className="mt-1 text-sm text-slate-500">{t("designProjects.team.description")}</p>
        </div>
        {!loading && !error ? (
          <Badge variant="outline" className="border-slate-200 bg-slate-50 text-slate-700">
            {assignedCount} {t("designProjects.team.memberCount")}
          </Badge>
        ) : null}
      </div>

      {loading ? (
        <div className="flex justify-center py-12"><Loader2 className="h-6 w-6 animate-spin text-slate-400" /></div>
      ) : error ? (
        <div className="mt-4 rounded-md border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">
          <p>{error}</p>
          <Button variant="outline" size="sm" className="mt-3" onClick={() => void load()}>
            <RefreshCw className="mr-1.5 h-3.5 w-3.5" />
            {t("common.retry")}
          </Button>
        </div>
      ) : members.length === 0 ? (
        <div className="mt-4 rounded-md border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500">
          {t("designProjects.team.empty")}
        </div>
      ) : (
        <ul className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
          {members.map((member) => (
            <li key={member.key} className="rounded-md border border-slate-200 bg-slate-50/50 p-3">
              <div className="flex items-start gap-3">
                <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-slate-200 text-slate-600">
                  <UserRound className="h-4 w-4" />
                </div>
                <div className="min-w-0">
                  <p className="truncate text-sm font-semibold text-slate-900">{member.name}</p>
                  <div className="mt-2 flex flex-wrap gap-1.5">
                    {member.roles.map((role) => (
                      <Badge key={role} variant="outline" className="bg-white text-xs font-normal text-slate-600">
                        {role}
                      </Badge>
                    ))}
                  </div>
                </div>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
};