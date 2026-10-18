import { useCallback, useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { usePermissions } from "@/hooks/usePermissions";
import { useI18n } from "@/lib/i18n";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { formatVnd, parseVndDecimal } from "@/lib/numberFormat";
import {
  adminApi,
  type ConstructionProcurementResponse,
  type ContractClassificationOptions,
  type ContractType,
} from "@/services/adminApi";

type ContractOption = { id: number; contractNumber: string; status: string };
type SupplyDocument = { id: number; originalFileName: string };

function DraftContract({ projectId, onCreated }: { projectId: number; onCreated: () => void }) {
  const attempt = useRef<{ payload: string; key: string } | null>(null);
  const { t } = useI18n();
  const [options, setOptions] = useState<ContractClassificationOptions | null>(null);
  const [vendor, setVendor] = useState("");
  const [type, setType] = useState<ContractType | "">("");
  const [value, setValue] = useState("");
  const [scope, setScope] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let active = true;
    setError(null);
    setOptions(null);
    void adminApi
      .getContractClassificationOptions("Downstream")
      .then(({ data }) => {
        if (active) setOptions(data);
      })
      .catch((cause) => {
        if (active) setError(extractApiError(cause));
      });
    return () => {
      active = false;
    };
  }, [revision]);
  async function save() {
    const amount = parseVndDecimal(value);
    if (
      !vendor ||
      !type ||
      !value.trim() ||
      !Number.isFinite(amount) ||
      amount < 0 ||
      amount > Number.MAX_SAFE_INTEGER
    ) {
      setError(t("construction.handoff.draftValidation"));
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const body = {
        vendorId: Number(vendor),
        type,
        value: amount,
        scopeOfWork: scope.trim() || null,
      };
      const payload = JSON.stringify(body);
      if (attempt.current?.payload !== payload)
        attempt.current = { payload, key: crypto.randomUUID() };
      await adminApi.createConstructionProcurementContract(projectId, body, attempt.current.key);
      onCreated();
    } catch (cause) {
      const status = (cause as { response?: { status?: number } }).response?.status;
      if (status && status < 500) attempt.current = null;
      setError(extractApiError(cause));
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="space-y-3 rounded-lg border p-4" data-testid="construction-contract-draft">
      <h3 className="font-semibold">{t("construction.handoff.createDraft")}</h3>
      <p className="text-sm text-muted-foreground">{t("construction.handoff.draftBoundary")}</p>
      {!options && !error && <PageLoading />}
      {options && (
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="space-y-1">
            <Label htmlFor="supply-contract-vendor">{t("contracts.field.vendor")}</Label>
            <Select value={vendor} onValueChange={setVendor} disabled={busy}>
              <SelectTrigger id="supply-contract-vendor">
                <SelectValue placeholder={t("construction.handoff.selectVendor")} />
              </SelectTrigger>
              <SelectContent>
                {options.vendors.map((item) => (
                  <SelectItem key={item.id} value={String(item.id)}>
                    {item.vendorCode} · {item.companyName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <Label htmlFor="supply-contract-type">{t("contracts.field.type")}</Label>
            <Select
              value={type}
              onValueChange={(next) => setType(next as ContractType)}
              disabled={busy}
            >
              <SelectTrigger id="supply-contract-type">
                <SelectValue placeholder={t("construction.handoff.selectType")} />
              </SelectTrigger>
              <SelectContent>
                {options.allowedTypes.Downstream.map((item) => (
                  <SelectItem key={item} value={item}>
                    {t(`contracts.type.${item}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <Label htmlFor="supply-contract-value">{t("contracts.field.value")}</Label>
            <Input
              id="supply-contract-value"
              inputMode="decimal"
              value={value}
              maxLength={32}
              disabled={busy}
              onChange={(event) => setValue(event.target.value)}
              onBlur={() => {
                const amount = parseVndDecimal(value);
                if (value.trim() && Number.isFinite(amount)) setValue(formatVnd(amount));
              }}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="supply-contract-scope">{t("contracts.field.scopeOfWork")}</Label>
            <Textarea
              id="supply-contract-scope"
              maxLength={20000}
              value={scope}
              onChange={(event) => setScope(event.target.value)}
              disabled={busy}
            />
          </div>
          <Button disabled={busy || !vendor || !type || !value.trim()} onClick={() => void save()}>
            {t("common.save")}
          </Button>
        </div>
      )}
      {error && (
        <div role="alert" className="space-y-2 text-sm text-destructive">
          <p>{error}</p>
          {!options && (
            <Button variant="outline" onClick={() => setRevision((current) => current + 1)}>
              {t("common.refresh")}
            </Button>
          )}
        </div>
      )}
    </section>
  );
}

export default function ConstructionResourcesWorkspace({ projectId }: { projectId: number }) {
  const { t } = useI18n();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.procurementResourcesManage);
  const [rows, setRows] = useState<ConstructionProcurementResponse[]>([]);
  const [contracts, setContracts] = useState<ContractOption[]>([]);
  const [documents, setDocuments] = useState<SupplyDocument[]>([]);
  const [creating, setCreating] = useState(false);
  const [type, setType] = useState("all");
  const [state, setState] = useState("all");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    void Promise.all([
      adminApi.listConstructionProcurement(projectId, {
        type: type === "all" ? undefined : type,
        state: state === "all" ? undefined : state,
      }),
      canManage ? adminApi.listConstructionProcurementContracts(projectId) : Promise.resolve(null),
      canManage ? adminApi.listConstructionSupplyDocuments(projectId) : Promise.resolve(null),
    ])
      .then(([requests, contractList, documentList]) => {
        if (!active) return;
        setRows(requests.data);
        setContracts(contractList?.data ?? []);
        setDocuments(documentList?.data ?? []);
      })
      .catch((cause) => {
        if (active) setError(extractApiError(cause));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [projectId, type, state, revision, canManage]);
  const refresh = useCallback(() => setRevision((value) => value + 1), []);
  return (
    <section className="space-y-4" data-testid="construction-procurement">
      <div className="flex flex-wrap items-center gap-3">
        <Select value={type} onValueChange={setType}>
          <SelectTrigger className="w-52" aria-label={t("construction.handoff.type")}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t("construction.handoff.all")}</SelectItem>
            <SelectItem value="Equipment">
              {t("construction.resourceRequests.equipment")}
            </SelectItem>
            <SelectItem value="Labour">{t("construction.resourceRequests.labour")}</SelectItem>
          </SelectContent>
        </Select>
        <Select value={state} onValueChange={setState}>
          <SelectTrigger className="w-52" aria-label={t("construction.handoff.state")}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t("construction.handoff.all")}</SelectItem>
            {["AwaitingContract", "ContractLinked", "Supplied"].map((value) => (
              <SelectItem key={value} value={value}>
                {t(`construction.handoff.${value}`)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Button variant="outline" onClick={refresh}>
          {t("common.refresh")}
        </Button>
        {has(ADMIN_PERMS.contractsManage) && (
          <Button asChild variant="outline">
            <Link to={`/admin/contracts?direction=Downstream&projectId=${projectId}`}>
              {t("construction.handoff.openContracts")}
            </Link>
          </Button>
        )}
        {canManage && has(ADMIN_PERMS.contracts) && (
          <Button onClick={() => setCreating((value) => !value)}>
            {t("construction.handoff.createDraft")}
          </Button>
        )}
      </div>
      {creating && (
        <DraftContract
          key={projectId}
          projectId={projectId}
          onCreated={() => {
            setCreating(false);
            refresh();
          }}
        />
      )}
      <p className="text-sm text-muted-foreground">{t("construction.handoff.boundary")}</p>
      {has(ADMIN_PERMS.operationalProjects) && (
        <Button asChild variant="link">
          <Link to={`/admin/operational-projects/${projectId}`}>
            {t("construction.handoff.projectDocuments")}
          </Link>
        </Button>
      )}
      {loading ? (
        <PageLoading />
      ) : error ? (
        <PageError message={error} onRetry={refresh} />
      ) : rows.length === 0 ? (
        <PageEmpty message={t("construction.handoff.empty")} />
      ) : (
        rows.map((row) => (
          <ResourceCard
            key={`${row.id}-${row.rowVersion}`}
            row={row}
            contracts={contracts}
            documents={documents}
            canManage={canManage}
            refresh={refresh}
          />
        ))
      )}
    </section>
  );
}

function ResourceCard({
  row,
  contracts,
  documents,
  canManage,
  refresh,
}: {
  row: ConstructionProcurementResponse;
  contracts: ContractOption[];
  documents: SupplyDocument[];
  canManage: boolean;
  refresh: () => void;
}) {
  const { t, lang } = useI18n();
  const [contractId, setContractId] = useState(row.contractId ? String(row.contractId) : "");
  const [documentId, setDocumentId] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  async function act(kind: "contract" | "supply") {
    setBusy(true);
    setError(null);
    try {
      if (kind === "contract")
        await adminApi.linkConstructionProcurementContract(
          row.operationalProjectId,
          row.id,
          Number(contractId),
          row.rowVersion,
        );
      else
        await adminApi.confirmConstructionSupply(
          row.operationalProjectId,
          row.id,
          Number(documentId),
          row.rowVersion,
        );
      refresh();
    } catch (cause) {
      setError(extractApiError(cause));
    } finally {
      setBusy(false);
    }
  }
  const date = (value: string) =>
    new Intl.DateTimeFormat(lang, { dateStyle: "short", timeStyle: "short" }).format(
      new Date(value),
    );
  return (
    <article
      className="space-y-3 rounded-lg border p-4"
      data-testid={`construction-procurement-${row.id}`}
    >
      <div className="flex flex-wrap justify-between gap-2">
        <h3 className="font-semibold">
          {row.code} · {row.wbsCode}
        </h3>
        <span>
          {t(
            `construction.handoff.${row.suppliedAt ? "Supplied" : row.contractId ? "ContractLinked" : "AwaitingContract"}`,
          )}
        </span>
      </div>
      <p>
        {t(
          row.type === "Equipment"
            ? "construction.resourceRequests.equipment"
            : "construction.resourceRequests.labour",
        )}{" "}
        · {formatVnd(row.quantity)} {row.unit}
      </p>
      <p className="break-words text-sm">{row.reason}</p>
      {row.contractNumber && (
        <p>
          {t("construction.handoff.contract")}: {row.contractNumber}
        </p>
      )}
      {row.contractId && !row.contractValid && (
        <p role="alert" className="text-sm text-destructive">
          {t("construction.handoff.invalidContract")}
        </p>
      )}
      {row.suppliedAt && (
        <p className="text-sm">
          {t("construction.handoff.Supplied")}: {date(row.suppliedAt)} ·{" "}
          {t("construction.handoff.actor")}: {formatVnd(row.suppliedByUserId ?? 0)}
        </p>
      )}
      {canManage && !row.suppliedAt && (
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor={`resource-contract-${row.id}`}>
              {t("construction.handoff.contract")}
            </Label>
            <Select value={contractId} onValueChange={setContractId} disabled={busy}>
              <SelectTrigger id={`resource-contract-${row.id}`}>
                <SelectValue placeholder={t("construction.handoff.selectContract")} />
              </SelectTrigger>
              <SelectContent>
                {contracts.map((contract) => (
                  <SelectItem key={contract.id} value={String(contract.id)}>
                    {contract.contractNumber}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button
              disabled={busy || !contractId || Number(contractId) === row.contractId}
              onClick={() => void act("contract")}
            >
              {t("construction.handoff.link")}
            </Button>
          </div>
          <div className="space-y-2">
            <Label htmlFor={`resource-document-${row.id}`}>
              {t("construction.handoff.document")}
            </Label>
            <Select value={documentId} onValueChange={setDocumentId} disabled={busy}>
              <SelectTrigger id={`resource-document-${row.id}`}>
                <SelectValue placeholder={t("construction.handoff.selectDocument")} />
              </SelectTrigger>
              <SelectContent>
                {documents.map((document) => (
                  <SelectItem key={document.id} value={String(document.id)}>
                    {document.originalFileName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button
              disabled={busy || !documentId || !row.contractValid}
              onClick={() => void act("supply")}
            >
              {t("construction.handoff.confirm")}
            </Button>
          </div>
        </div>
      )}
      {error && (
        <div role="alert" className="space-y-2 text-sm text-destructive">
          <p>{error}</p>
          <Button variant="outline" onClick={refresh}>
            {t("common.refresh")}
          </Button>
        </div>
      )}
      <details>
        <summary className="cursor-pointer text-sm">{t("construction.handoff.history")}</summary>
        <ul className="mt-2 space-y-1 text-sm">
          {row.history.map((event) => (
            <li key={event.id}>
              {date(event.createdAt)} ·{" "}
              {t(
                event.supplyDocumentId
                  ? "construction.handoff.Supplied"
                  : "construction.handoff.ContractLinked",
              )}{" "}
              · {t("construction.handoff.contract")}: {formatVnd(event.contractId)} ·{" "}
              {t("construction.handoff.actor")}: {formatVnd(event.actorUserId)}
            </li>
          ))}
        </ul>
      </details>
    </article>
  );
}
