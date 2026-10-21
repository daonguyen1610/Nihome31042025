import { useEffect, useState } from "react";
import { adminApi, type ContractResponse } from "@/services/adminApi";
import { extractApiError } from "@/lib/apiError";

export function useContractContinuation(sourceId: number | null, opportunityId?: number | null) {
  const [source, setSource] = useState<ContractResponse | null>(null);
  const [kind, setKind] = useState("Renewal");
  const [reason, setReason] = useState("");
  const [kinds, setKinds] = useState<string[]>([]);
  const [inherited, setInherited] = useState(false);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  useEffect(() => {
    let active = true;
    setSource(null);
    setError("");
    setReason("");
    setKind("Renewal");
    setInherited(false);
    setLoading(true);
    void (async () => {
      try {
        let id = sourceId;
        if (opportunityId) {
          const { data } = await adminApi.getOpportunity(opportunityId);
          if (!active) return;
          id = data.sourceContractId ?? id;
          if (data.continuationKind) setKind(data.continuationKind);
          if (data.continuationReason) setReason(data.continuationReason);
          setInherited(!!data.sourceContractId);
        }
        if (id) {
          const [contract, related] = await Promise.all([
            adminApi.getContract(id),
            adminApi.getContractContinuations(id),
          ]);
          if (active) {
            setSource(contract.data);
            setKinds(related.data.kinds);
          }
        }
      } catch (e) {
        if (active) setError(extractApiError(e));
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [sourceId, opportunityId]);
  return {
    source,
    kind,
    setKind,
    reason,
    kinds,
    inherited,
    setReason,
    loading,
    error,
    payload: source
      ? {
          sourceContractId: source.id,
          sourceContractRowVersion: source.rowVersion,
          continuationKind: kind,
          continuationReason: reason.trim(),
        }
      : {},
  };
}
