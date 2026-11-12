import { History } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { type ContractTimelineEvent } from "@/services/adminApi";
import { formatDateTime } from "@/lib/displaySettings";

export const TimelineTab = ({ events }: { events: ContractTimelineEvent[] }) => {
  const { t } = useI18n();
  const actionKeys: Record<string, string> = {
    "contract.create": "contracts.timeline.action.create",
    "contract.care.create": "contracts.timeline.action.careCreate",
    "contract.update": "contracts.timeline.action.update",
    "contract.delete": "contracts.timeline.action.delete",
    "contract.transition": "contracts.timeline.action.transition",
    "contract.designProject.ensure": "contracts.timeline.action.designProjectEnsure",
    "contract.milestone.status": "contracts.timeline.action.milestoneStatus",
    "contract.vo.create": "contracts.timeline.action.voCreate",
    "contract.vo.update": "contracts.timeline.action.voUpdate",
    "contract.vo.submit": "contracts.timeline.action.voSubmit",
    "contract.vo.approve": "contracts.timeline.action.voApprove",
    "contract.vo.reject": "contracts.timeline.action.voReject",
    "contract.vo.delete": "contracts.timeline.action.voDelete",
    "contract.attachment.create": "contracts.timeline.action.attachmentCreate",
    "contract.attachment.delete": "contracts.timeline.action.attachmentDelete",
  };
  if (events.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-input p-6 text-center text-sm text-muted-foreground">
        {t("contracts.timeline.empty")}
      </div>
    );
  }
  return (
    <ol className="space-y-3">
      {events.map((ev) => (
        <li
          key={ev.id}
          className="flex gap-3 rounded-lg border border-border bg-card p-3 shadow-sm"
        >
          <div className="mt-1 flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-muted text-muted-foreground">
            <History className="h-4 w-4" />
          </div>
          <div className="min-w-0">
            <div className="flex flex-wrap items-baseline gap-x-2 text-sm">
              <span className="font-semibold text-foreground">
                {actionKeys[ev.action] ? t(actionKeys[ev.action]) : ev.action}
              </span>
              <span className="text-xs text-muted-foreground">{formatDateTime(ev.occurredAt)}</span>
            </div>
            {!actionKeys[ev.action] && ev.message ? (
              <p className="mt-0.5 text-sm text-foreground">{ev.message}</p>
            ) : null}
            {ev.userName ? (
              <p className="mt-1 text-xs text-muted-foreground">
                {t("contracts.timeline.by")}: {ev.userName}
              </p>
            ) : null}
          </div>
        </li>
      ))}
    </ol>
  );
};
