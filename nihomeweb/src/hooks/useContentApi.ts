import { useState, useEffect, useCallback } from "react";
import { useI18n } from "@/lib/i18n";
import {
  contentApi,
  type ActivityCategoryResponse,
  type EmploymentTypeResponse,
  type RecruitmentDropdownOptionResponse,
  type ActivityResponse,
  type NewsResponse,
  type ProjectCategoryResponse,
  type ProjectResponse,
  type ServiceResponse,
  type LogosGroupedResponse,
  type ProcessResponse,
  type SlideshowResponse,
  type ContactMessageResponse,
  type JobPositionResponse,
} from "@/services/contentApi";
import { adminApi } from "@/services/adminApi";

/* ------------------------------------------------------------------ */
/*  Generic fetch hook with loading / error / data pattern            */
/* ------------------------------------------------------------------ */

interface FetchState<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
  refetch: () => void;
}

function useFetch<T>(fetcher: () => Promise<{ data: T }>, deps: unknown[] = []): FetchState<T> {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetch = useCallback(() => {
    setLoading(true);
    setError(null);
    fetcher()
      .then((res) => setData(res.data))
      .catch((err) => setError(err?.response?.data?.message ?? err.message ?? "Unknown error"))
      .finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  useEffect(() => { fetch(); }, [fetch]);

  return { data, loading, error, refetch: fetch };
}

/* ------------------------------------------------------------------ */
/*  Typed hooks per entity                                            */
/* ------------------------------------------------------------------ */

export function useActivities() {
  const { lang } = useI18n();
  return useFetch<ActivityResponse[]>(() => contentApi.getActivities(lang), [lang]);
}

export function useActivity(slug: string) {
  const { lang } = useI18n();
  return useFetch<ActivityResponse>(() => contentApi.getActivity(slug, lang), [slug, lang]);
}

export function useActivityCategories(includeInactive = false) {
  return useFetch<ActivityCategoryResponse[]>(
    () => contentApi.getActivityCategories(includeInactive),
    [includeInactive],
  );
}

export function useProjectCategories(includeInactive = false) {
  return useFetch<ProjectCategoryResponse[]>(
    () => contentApi.getProjectCategories(includeInactive),
    [includeInactive],
  );
}

export function useEmploymentTypes(includeInactive = false) {
  return useFetch<EmploymentTypeResponse[]>(
    () => contentApi.getEmploymentTypes(includeInactive),
    [includeInactive],
  );
}

export function useExperienceLevelOptions() {
  return useFetch<RecruitmentDropdownOptionResponse[]>(
    () => contentApi.getRecruitmentDropdownOptions("experience-level"),
    [],
  );
}

export function useBenefitOptions() {
  return useFetch<RecruitmentDropdownOptionResponse[]>(
    () => contentApi.getRecruitmentDropdownOptions("benefit"),
    [],
  );
}

export function useNews() {
  const { lang } = useI18n();
  return useFetch<NewsResponse[]>(() => contentApi.getNews(lang), [lang]);
}

export function useNewsItem(slug: string) {
  const { lang } = useI18n();
  return useFetch<NewsResponse>(() => contentApi.getNewsItem(slug, lang), [slug, lang]);
}

export function useProjects() {
  return useFetch<ProjectResponse[]>(() => contentApi.getProjects(), []);
}

export function useProject(slug: string) {
  return useFetch<ProjectResponse>(() => contentApi.getProject(slug), [slug]);
}

export function useServices() {
  return useFetch<ServiceResponse[]>(() => contentApi.getServices(), []);
}

export function useService(slug: string) {
  return useFetch<ServiceResponse>(() => contentApi.getService(slug), [slug]);
}

export function useLogos() {
  return useFetch<LogosGroupedResponse>(() => contentApi.getLogos(), []);
}

export function useProcesses() {
  return useFetch<Record<string, ProcessResponse[]>>(() => contentApi.getProcesses(), []);
}

export function useJobPositions() {
  const { lang } = useI18n();
  return useFetch<JobPositionResponse[]>(() => contentApi.getJobPositions(lang), [lang]);
}

export function useSlideshow() {
  const { lang } = useI18n();
  return useFetch<SlideshowResponse[]>(() => contentApi.getSlideshow(lang), [lang]);
}

export function useContacts(replied?: boolean) {
  return useFetch<ContactMessageResponse[]>(
    () => adminApi.getContacts(replied) as Promise<{ data: ContactMessageResponse[] }>,
    [replied],
  );
}
