import { Diamond, Link2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { useI18n } from "@/lib/i18n";
import type { DesignScheduleTaskResponse, OperationalProjectMemberResponse } from "@/services/adminApi";

interface Props {
  tasks: DesignScheduleTaskResponse[];
  members: OperationalProjectMemberResponse[];
}

const day = 86_400_000;
const timestamp = (value: string) => Date.parse(`${value.slice(0, 10)}T00:00:00Z`);

export const DesignScheduleTimeline = ({ tasks, members }: Props) => {
  const { t, lang } = useI18n();
  if (!tasks.length) return <div className="rounded-md border border-dashed p-8 text-center text-sm text-slate-500">{t("designProjects.schedule.timeline.empty")}</div>;

  const start = Math.min(...tasks.map((task) => timestamp(task.plannedStart)));
  const end = Math.max(...tasks.map((task) => timestamp(task.plannedEnd)));
  const totalDays = Math.max(1, Math.round((end - start) / day) + 1);
  const format = (value: number) => new Intl.DateTimeFormat(lang, { dateStyle: "medium", timeZone: "UTC" }).format(new Date(value));
  const taskById = new Map(tasks.map((task) => [task.id, task]));
  const memberById = new Map(members.map((member) => [member.id, member]));

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm" aria-labelledby="design-schedule-timeline-title">
      <div className="flex flex-wrap items-end justify-between gap-2"><div><h3 id="design-schedule-timeline-title" className="font-semibold text-slate-900">{t("designProjects.schedule.timeline.title")}</h3><p className="mt-1 text-sm text-slate-500">{t("designProjects.schedule.timeline.description")}</p></div><span className="text-xs text-slate-500">{format(start)} – {format(end)}</span></div>
      <div className="mt-4 overflow-x-auto pb-2">
        <div className="min-w-[720px] space-y-2">
          {tasks.map((task) => {
            const left = ((timestamp(task.plannedStart) - start) / day / totalDays) * 100;
            const duration = Math.round((timestamp(task.plannedEnd) - timestamp(task.plannedStart)) / day) + 1;
            const width = Math.max(1.5, (duration / totalDays) * 100);
            return (
              <article key={task.id} className="grid grid-cols-[220px_1fr] items-center gap-3 rounded-md border border-slate-100 p-2">
                <div className="min-w-0"><div className="flex items-center gap-1.5">{task.isMilestone ? <Diamond className="h-3.5 w-3.5 shrink-0 fill-amber-400 text-amber-600" /> : null}<strong className="truncate text-sm text-slate-900">{task.name}</strong></div><p className="truncate text-xs text-slate-500">{task.code} · {memberById.get(task.assigneeMemberId)?.userName ?? t("designProjects.schedule.value.unknown")}</p></div>
                <div>
                  <div className="relative h-8 rounded bg-slate-100" aria-label={`${task.name}: ${task.plannedStart} – ${task.plannedEnd}`}><div className={task.overdue ? "absolute top-1 h-6 rounded bg-rose-400" : "absolute top-1 h-6 rounded bg-sky-500"} style={{ left: `${left}%`, width: `${width}%` }}><div className="h-full rounded bg-emerald-500/80" style={{ width: `${task.progressPercent}%` }} /></div></div>
                  {task.predecessorTaskIds.length ? <div className="mt-1 flex flex-wrap items-center gap-1 text-xs text-slate-500"><Link2 className="h-3 w-3" /><span>{t("designProjects.schedule.timeline.after")}</span>{task.predecessorTaskIds.map((id) => <Badge key={id} variant="outline" className="px-1 py-0 font-mono text-[10px]">{taskById.get(id)?.code ?? `#${id}`}</Badge>)}</div> : null}
                </div>
              </article>
            );
          })}
        </div>
      </div>
    </section>
  );
};
