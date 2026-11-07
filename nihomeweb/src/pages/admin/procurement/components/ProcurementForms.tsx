import { FieldScope } from "@/components/ui/field-scope";
import { personOption } from "@/lib/people";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import {
  type ContractResponse,
  type ProcurementContractLineResponse,
  type ProjectBoqLineResponse,
} from "@/services/adminApi";
import { AddButton, RemoveButton } from "./procurementShared";
import {
  UserOption,
  Translate,
  BoqLineDraft,
  emptyBoqLine,
  RequestLineDraft,
  emptyRequestLine,
  ReceiptLineDraft,
  emptyReceiptLine,
  IssueLineDraft,
  emptyIssueLine,
} from "./procurementValues";

export const UserField = ({
  id,
  label,
  value,
  onChange,
  users,
  t,
}: {
  id: string;
  label: string;
  value: number;
  onChange: (value: number) => void;
  users: UserOption[];
  t: Translate;
}) => (
  <div className="space-y-2">
    <Label htmlFor={id}>{label}</Label>
    {users.length ? (
      <SearchableSelect
        value={value ? String(value) : ""}
        onChange={(next) => onChange(Number(next))}
        id={id}
        ariaLabel={label}
        options={[...users.map((user) => personOption(user, t))]}
      />
    ) : (
      <Input
        id={id}
        type="number"
        min={1}
        value={value || ""}
        onChange={(event) => onChange(Number(event.target.value))}
        placeholder={t("procurement.lookup.userIdPlaceholder")}
      />
    )}
  </div>
);

export const BoqLineField = ({
  value,
  onChange,
  t,
}: {
  value: number;
  onChange: (value: number) => void;
  t: Translate;
}) => (
  <Input
    type="number"
    min={0}
    step="any"
    value={value}
    onChange={(event) => onChange(Number(event.target.value))}
    aria-label={t("procurement.field.quantity")}
  />
);

export const BoqForm = ({
  upstreamContractId,
  setUpstreamContractId,
  upstreamContracts,
  currency,
  setCurrency,
  lines,
  setLines,
  t,
}: {
  upstreamContractId: number;
  setUpstreamContractId: (value: number) => void;
  upstreamContracts: ContractResponse[];
  currency: string;
  setCurrency: (value: string) => void;
  lines: BoqLineDraft[];
  setLines: React.Dispatch<React.SetStateAction<BoqLineDraft[]>>;
  t: Translate;
}) => (
  <div className="space-y-4">
    <div className="grid gap-3 sm:grid-cols-2">
      <FieldScope className="space-y-2">
        <Label>{t("procurement.field.upstreamContract")}</Label>
        <Select
          value={upstreamContractId ? String(upstreamContractId) : ""}
          onValueChange={(value) => setUpstreamContractId(Number(value))}
        >
          <SelectTrigger>
            <SelectValue placeholder={t("procurement.boq.selectSignedContract")} />
          </SelectTrigger>
          <SelectContent>
            {upstreamContracts.map((contract) => (
              <SelectItem key={contract.id} value={String(contract.id)}>
                {contract.contractNumber}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {upstreamContracts.length === 0 && (
          <p className="text-xs text-muted-foreground">{t("procurement.boq.noSignedContract")}</p>
        )}
      </FieldScope>
      <div className="space-y-2">
        <Label htmlFor="boq-currency">{t("procurement.field.currency")}</Label>
        <Input
          id="boq-currency"
          maxLength={3}
          value={currency}
          onChange={(event) => setCurrency(event.target.value.toUpperCase())}
        />
      </div>
    </div>
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <span>{t("procurement.field.lines")}</span>
        <AddButton onClick={() => setLines((current) => [...current, emptyBoqLine()])}>
          {t("procurement.action.addLine")}
        </AddButton>
      </div>
      {lines.map((line, index) => (
        <div
          className="grid gap-3 border-t pt-3 sm:grid-cols-2 lg:grid-cols-[1fr_2fr_0.8fr_1fr_1fr_auto]"
          key={index}
        >
          <FieldScope>
            <Label>{t("procurement.field.itemCode")}</Label>
            <Input
              maxLength={80}
              value={line.itemCode}
              onChange={(event) =>
                setLines((current) =>
                  current.map((item, itemIndex) =>
                    itemIndex === index ? { ...item, itemCode: event.target.value } : item,
                  ),
                )
              }
            />
          </FieldScope>
          <FieldScope>
            <Label>{t("procurement.field.description")}</Label>
            <Input
              maxLength={500}
              value={line.description}
              onChange={(event) =>
                setLines((current) =>
                  current.map((item, itemIndex) =>
                    itemIndex === index ? { ...item, description: event.target.value } : item,
                  ),
                )
              }
            />
          </FieldScope>
          <FieldScope>
            <Label>{t("procurement.field.unit")}</Label>
            <Input
              maxLength={50}
              value={line.unit}
              onChange={(event) =>
                setLines((current) =>
                  current.map((item, itemIndex) =>
                    itemIndex === index ? { ...item, unit: event.target.value } : item,
                  ),
                )
              }
            />
          </FieldScope>
          <FieldScope>
            <Label>{t("procurement.field.quantity")}</Label>
            <BoqLineField
              value={line.approvedQuantity}
              onChange={(value) =>
                setLines((current) =>
                  current.map((item, itemIndex) =>
                    itemIndex === index ? { ...item, approvedQuantity: value } : item,
                  ),
                )
              }
              t={t}
            />
          </FieldScope>
          <FieldScope>
            <Label>{t("procurement.field.budgetPrice")}</Label>
            <Input
              type="number"
              min={0}
              step="any"
              value={line.budgetUnitPrice}
              onChange={(event) =>
                setLines((current) =>
                  current.map((item, itemIndex) =>
                    itemIndex === index
                      ? { ...item, budgetUnitPrice: Number(event.target.value) }
                      : item,
                  ),
                )
              }
            />
          </FieldScope>
          <div className="self-end">
            <RemoveButton
              label={t("procurement.action.removeLine")}
              disabled={lines.length === 1}
              onClick={() =>
                setLines((current) => current.filter((_, itemIndex) => itemIndex !== index))
              }
            />
          </div>
        </div>
      ))}
    </div>
  </div>
);

export const RequestForm = ({
  form,
  setForm,
  lines,
  setLines,
  users,
  procurementUsers,
  boqLines,
  t,
}: {
  form: {
    responsibleSiteUserId: number;
    assignedProcurementUserId: number;
    requiredAt: string;
    note: string;
  };
  setForm: React.Dispatch<React.SetStateAction<typeof form>>;
  lines: RequestLineDraft[];
  setLines: React.Dispatch<React.SetStateAction<RequestLineDraft[]>>;
  users: UserOption[];
  procurementUsers: UserOption[];
  boqLines: Array<{ id: number; itemCode: string; description: string; remainingQuantity: number }>;
  t: Translate;
}) => (
  <div className="space-y-4">
    <div className="grid gap-4 sm:grid-cols-2">
      <UserField
        id="request-site-user"
        label={t("procurement.field.responsibleSite")}
        value={form.responsibleSiteUserId}
        onChange={(value) => setForm((current) => ({ ...current, responsibleSiteUserId: value }))}
        users={users}
        t={t}
      />
      <UserField
        id="request-proc-user"
        label={t("procurement.field.procurementOwner")}
        value={form.assignedProcurementUserId}
        onChange={(value) =>
          setForm((current) => ({ ...current, assignedProcurementUserId: value }))
        }
        users={procurementUsers}
        t={t}
      />
      <div className="space-y-2">
        <Label htmlFor="request-required-at">{t("procurement.field.requiredAt")}</Label>
        <Input
          id="request-required-at"
          type="datetime-local"
          value={form.requiredAt}
          onChange={(event) =>
            setForm((current) => ({ ...current, requiredAt: event.target.value }))
          }
        />
      </div>
      <div className="space-y-2">
        <Label htmlFor="request-note">{t("procurement.field.note")}</Label>
        <Input
          id="request-note"
          maxLength={2000}
          value={form.note}
          onChange={(event) => setForm((current) => ({ ...current, note: event.target.value }))}
        />
      </div>
    </div>
    <DynamicChoiceLines
      lines={lines}
      setLines={setLines}
      choices={boqLines.map((line) => ({
        id: line.id,
        label: `${line.itemCode} · ${line.description} · ${t("procurement.request.remaining", { quantity: line.remainingQuantity })}`,
      }))}
      idKey="projectBoqLineId"
      quantityKey="requestedQuantity"
      add={emptyRequestLine}
      t={t}
    />
  </div>
);

export const ContractLineForm = ({
  form,
  setForm,
  users,
  contracts,
  contractsLookupAvailable,
  boqLines,
  t,
}: {
  form: {
    contractId: number;
    projectBoqLineId: number;
    procurementOwnerUserId: number;
    quantity: number;
    negotiatedUnitPrice: number;
  };
  setForm: React.Dispatch<React.SetStateAction<typeof form>>;
  users: UserOption[];
  contracts: ContractResponse[];
  contractsLookupAvailable: boolean;
  boqLines: ProjectBoqLineResponse[];
  t: Translate;
}) => (
  <div className="grid gap-4 sm:grid-cols-2">
    <div className="space-y-2">
      <Label htmlFor="contract-id">
        {contractsLookupAvailable
          ? t("procurement.field.contract")
          : t("procurement.field.contractId")}
      </Label>
      {contractsLookupAvailable ? (
        <Select
          value={form.contractId ? String(form.contractId) : undefined}
          onValueChange={(value) =>
            setForm((current) => ({ ...current, contractId: Number(value) }))
          }
        >
          <SelectTrigger id="contract-id">
            <SelectValue placeholder={t("procurement.lookup.contractPlaceholder")} />
          </SelectTrigger>
          <SelectContent>
            {contracts.map((contract) => (
              <SelectItem value={String(contract.id)} key={contract.id}>
                {contract.contractNumber}
                {contract.vendorName ? ` · ${contract.vendorName}` : ""}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      ) : (
        <Input
          id="contract-id"
          type="number"
          min={1}
          value={form.contractId || ""}
          onChange={(event) =>
            setForm((current) => ({ ...current, contractId: Number(event.target.value) }))
          }
          placeholder={t("procurement.lookup.contractIdPlaceholder")}
        />
      )}
    </div>
    <FieldScope className="space-y-2">
      <Label>{t("procurement.field.item")}</Label>
      <Select
        value={form.projectBoqLineId ? String(form.projectBoqLineId) : undefined}
        onValueChange={(value) =>
          setForm((current) => ({ ...current, projectBoqLineId: Number(value) }))
        }
      >
        <SelectTrigger>
          <SelectValue placeholder={t("procurement.lookup.boqLinePlaceholder")} />
        </SelectTrigger>
        <SelectContent>
          {boqLines.map((line) => (
            <SelectItem value={String(line.id)} key={line.id}>
              {line.itemCode} · {line.description}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </FieldScope>
    <UserField
      id="contract-owner"
      label={t("procurement.field.owner")}
      value={form.procurementOwnerUserId}
      onChange={(value) => setForm((current) => ({ ...current, procurementOwnerUserId: value }))}
      users={users}
      t={t}
    />
    <div className="space-y-2">
      <Label htmlFor="contract-quantity">{t("procurement.field.quantity")}</Label>
      <Input
        id="contract-quantity"
        type="number"
        min={0.000001}
        step="any"
        value={form.quantity}
        onChange={(event) =>
          setForm((current) => ({ ...current, quantity: Number(event.target.value) }))
        }
      />
    </div>
    <div className="space-y-2">
      <Label htmlFor="contract-price">{t("procurement.field.negotiatedPrice")}</Label>
      <Input
        id="contract-price"
        type="number"
        min={0}
        step="any"
        value={form.negotiatedUnitPrice}
        onChange={(event) =>
          setForm((current) => ({ ...current, negotiatedUnitPrice: Number(event.target.value) }))
        }
      />
    </div>
  </div>
);

export const ReceiptForm = ({
  inspectedAt,
  setInspectedAt,
  lines,
  setLines,
  requestLines,
  contractLines,
  t,
  number,
}: {
  inspectedAt: string;
  setInspectedAt: (value: string) => void;
  lines: ReceiptLineDraft[];
  setLines: React.Dispatch<React.SetStateAction<ReceiptLineDraft[]>>;
  requestLines: Array<{
    id: number;
    projectBoqLineId: number;
    itemCode: string;
    requestedQuantity: number;
    receivedQuantity: number;
    requestCode: string;
  }>;
  contractLines: ProcurementContractLineResponse[];
  t: Translate;
  number: Intl.NumberFormat;
}) => (
  <div className="space-y-4">
    <div className="max-w-sm space-y-2">
      <Label htmlFor="receipt-at">{t("procurement.field.inspectedAt")}</Label>
      <Input
        id="receipt-at"
        type="datetime-local"
        value={inspectedAt}
        onChange={(event) => setInspectedAt(event.target.value)}
      />
    </div>
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <span>{t("procurement.field.lines")}</span>
        <AddButton onClick={() => setLines((current) => [...current, emptyReceiptLine()])}>
          {t("procurement.action.addLine")}
        </AddButton>
      </div>
      {lines.map((line, index) => {
        const selectedRequestLine = requestLines.find(
          (item) => item.id === line.materialRequestLineId,
        );
        const matchingContracts = contractLines.filter(
          (item) =>
            !selectedRequestLine || item.projectBoqLineId === selectedRequestLine.projectBoqLineId,
        );
        return (
          <div className="grid gap-3 border-t pt-3 sm:grid-cols-[2fr_2fr_1fr_auto]" key={index}>
            <FieldScope className="space-y-2">
              <Label>{t("procurement.field.requestLine")}</Label>
              <Select
                value={line.materialRequestLineId ? String(line.materialRequestLineId) : ""}
                onValueChange={(value) =>
                  setLines((current) =>
                    current.map((item, itemIndex) =>
                      itemIndex === index
                        ? { ...item, materialRequestLineId: Number(value), contractLineId: null }
                        : item,
                    ),
                  )
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder={t("procurement.lookup.requestLinePlaceholder")} />
                </SelectTrigger>
                <SelectContent>
                  {requestLines.map((item) => (
                    <SelectItem value={String(item.id)} key={item.id}>
                      {item.requestCode} · {item.itemCode} ·{" "}
                      {number.format(item.requestedQuantity - item.receivedQuantity)}{" "}
                      {t("procurement.receipt.remaining")}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FieldScope>
            <FieldScope className="space-y-2">
              <Label>{t("procurement.field.contractLine")}</Label>
              <Select
                value={line.contractLineId ? String(line.contractLineId) : "none"}
                onValueChange={(value) =>
                  setLines((current) =>
                    current.map((item, itemIndex) =>
                      itemIndex === index
                        ? { ...item, contractLineId: value === "none" ? null : Number(value) }
                        : item,
                    ),
                  )
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">{t("procurement.lookup.noContractLine")}</SelectItem>
                  {matchingContracts.map((item) => (
                    <SelectItem value={String(item.id)} key={item.id}>
                      {item.contractNumber} · {item.itemCode}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FieldScope>
            <FieldScope className="space-y-2">
              <Label>{t("procurement.field.receivedQuantity")}</Label>
              <Input
                type="number"
                min={0.000001}
                step="any"
                value={line.receivedQuantity}
                onChange={(event) =>
                  setLines((current) =>
                    current.map((item, itemIndex) =>
                      itemIndex === index
                        ? { ...item, receivedQuantity: Number(event.target.value) }
                        : item,
                    ),
                  )
                }
              />
            </FieldScope>
            <div className="self-end">
              <RemoveButton
                label={t("procurement.action.removeLine")}
                disabled={lines.length === 1}
                onClick={() =>
                  setLines((current) => current.filter((_, itemIndex) => itemIndex !== index))
                }
              />
            </div>
          </div>
        );
      })}
    </div>
  </div>
);

export const IssueForm = ({
  form,
  setForm,
  lines,
  setLines,
  users,
  boqLines,
  t,
}: {
  form: { issuedAt: string; responsibleSiteUserId: number; workItemCode: string };
  setForm: React.Dispatch<React.SetStateAction<typeof form>>;
  lines: IssueLineDraft[];
  setLines: React.Dispatch<React.SetStateAction<IssueLineDraft[]>>;
  users: UserOption[];
  boqLines: ProjectBoqLineResponse[];
  t: Translate;
}) => (
  <div className="space-y-4">
    <div className="grid gap-4 sm:grid-cols-2">
      <div className="space-y-2">
        <Label htmlFor="issue-at">{t("procurement.field.issuedAt")}</Label>
        <Input
          id="issue-at"
          type="datetime-local"
          value={form.issuedAt}
          onChange={(event) => setForm((current) => ({ ...current, issuedAt: event.target.value }))}
        />
      </div>
      <UserField
        id="issue-site-user"
        label={t("procurement.field.responsibleSite")}
        value={form.responsibleSiteUserId}
        onChange={(value) => setForm((current) => ({ ...current, responsibleSiteUserId: value }))}
        users={users}
        t={t}
      />
      <div className="space-y-2 sm:col-span-2">
        <Label htmlFor="issue-work-item">{t("procurement.field.workItem")}</Label>
        <Input
          id="issue-work-item"
          maxLength={100}
          value={form.workItemCode}
          onChange={(event) =>
            setForm((current) => ({ ...current, workItemCode: event.target.value }))
          }
        />
      </div>
    </div>
    <DynamicChoiceLines
      lines={lines}
      setLines={setLines}
      choices={boqLines.map((line) => ({
        id: line.id,
        label: `${line.itemCode} · ${line.description}`,
      }))}
      idKey="projectBoqLineId"
      quantityKey="issuedQuantity"
      add={emptyIssueLine}
      t={t}
    />
  </div>
);

export const RatingForm = ({
  form,
  setForm,
  contracts,
  contractsLookupAvailable,
  t,
}: {
  form: {
    contractId: number;
    qualityScore: number;
    scheduleScore: number;
    costScore: number;
    hseScore: number;
    comments: string;
  };
  setForm: React.Dispatch<React.SetStateAction<typeof form>>;
  contracts: ContractResponse[];
  contractsLookupAvailable: boolean;
  t: Translate;
}) => (
  <div className="space-y-4">
    <div className="space-y-2">
      <Label htmlFor="rating-contract-id">
        {contractsLookupAvailable
          ? t("procurement.field.contract")
          : t("procurement.field.contractId")}
      </Label>
      {contractsLookupAvailable ? (
        <Select
          value={form.contractId ? String(form.contractId) : undefined}
          onValueChange={(value) =>
            setForm((current) => ({ ...current, contractId: Number(value) }))
          }
        >
          <SelectTrigger id="rating-contract-id">
            <SelectValue placeholder={t("procurement.lookup.contractPlaceholder")} />
          </SelectTrigger>
          <SelectContent>
            {contracts.map((contract) => (
              <SelectItem value={String(contract.id)} key={contract.id}>
                {contract.contractNumber} · {contract.vendorName}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      ) : (
        <Input
          id="rating-contract-id"
          type="number"
          min={1}
          value={form.contractId || ""}
          onChange={(event) =>
            setForm((current) => ({ ...current, contractId: Number(event.target.value) }))
          }
          placeholder={t("procurement.lookup.contractIdPlaceholder")}
        />
      )}
    </div>
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {(["qualityScore", "scheduleScore", "costScore", "hseScore"] as const).map((field) => (
        <div className="space-y-2" key={field}>
          <Label htmlFor={`rating-${field}`}>{t(`procurement.field.${field}`)}</Label>
          <Input
            id={`rating-${field}`}
            type="number"
            min={0}
            max={100}
            step="any"
            value={form[field]}
            onChange={(event) =>
              setForm((current) => ({ ...current, [field]: Number(event.target.value) }))
            }
          />
        </div>
      ))}
    </div>
    <div className="space-y-2">
      <Label htmlFor="rating-comments">{t("procurement.field.comments")}</Label>
      <Textarea
        id="rating-comments"
        maxLength={4000}
        value={form.comments}
        onChange={(event) => setForm((current) => ({ ...current, comments: event.target.value }))}
      />
    </div>
  </div>
);

export const DynamicChoiceLines = <
  T extends Record<I | Q, number>,
  I extends keyof T,
  Q extends keyof T,
>({
  lines,
  setLines,
  choices,
  idKey,
  quantityKey,
  add,
  t,
}: {
  lines: T[];
  setLines: React.Dispatch<React.SetStateAction<T[]>>;
  choices: Array<{ id: number; label: string }>;
  idKey: I;
  quantityKey: Q;
  add: () => T;
  t: Translate;
}) => (
  <div className="space-y-3">
    <div className="flex items-center justify-between">
      <span>{t("procurement.field.lines")}</span>
      <AddButton onClick={() => setLines((current) => [...current, add()])}>
        {t("procurement.action.addLine")}
      </AddButton>
    </div>
    {lines.map((line, index) => (
      <div className="grid gap-3 border-t pt-3 sm:grid-cols-[2fr_1fr_auto]" key={index}>
        <FieldScope className="space-y-2">
          <Label>{t("procurement.field.item")}</Label>
          <Select
            value={line[idKey] ? String(line[idKey]) : ""}
            onValueChange={(value) =>
              setLines((current) =>
                current.map((item, itemIndex) =>
                  itemIndex === index ? { ...item, [idKey]: Number(value) } : item,
                ),
              )
            }
          >
            <SelectTrigger>
              <SelectValue placeholder={t("procurement.lookup.boqLinePlaceholder")} />
            </SelectTrigger>
            <SelectContent>
              {choices.map((choice) => (
                <SelectItem value={String(choice.id)} key={choice.id}>
                  {choice.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </FieldScope>
        <FieldScope className="space-y-2">
          <Label>{t("procurement.field.quantity")}</Label>
          <Input
            type="number"
            min={0.000001}
            step="any"
            value={line[quantityKey]}
            onChange={(event) =>
              setLines((current) =>
                current.map((item, itemIndex) =>
                  itemIndex === index
                    ? { ...item, [quantityKey]: Number(event.target.value) }
                    : item,
                ),
              )
            }
          />
        </FieldScope>
        <div className="self-end">
          <RemoveButton
            label={t("procurement.action.removeLine")}
            disabled={lines.length === 1}
            onClick={() =>
              setLines((current) => current.filter((_, itemIndex) => itemIndex !== index))
            }
          />
        </div>
      </div>
    ))}
  </div>
);
