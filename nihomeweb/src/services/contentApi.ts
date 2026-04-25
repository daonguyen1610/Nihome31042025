import api from "@/lib/api";

// --- Types matching backend DTOs ---

export interface ActivityResponse {
  id: number;
  slug: string;
  date: string;
  imageUrl: string;
  category: string;
  author?: string;
  title: string;
  excerpt: string;
  content: string[];
}

export interface NewsResponse {
  id: number;
  slug: string;
  date: string;
  imageUrl: string;
  category: string;
  title: string;
  excerpt: string;
  content: string[];
}

export interface ProjectHighlight {
  label: string;
  value: string;
}

export interface ProjectResponse {
  id: number;
  slug: string;
  imageUrl: string;
  gallery?: string[];
  name: string;
  client: string;
  location: string;
  scale: string;
  scope: string;
  status: string;
  year?: string;
  category?: string;
  description?: string;
  challenges?: string[];
  solutions?: string[];
  highlights?: ProjectHighlight[];
}

export interface ServiceSection {
  heading: string;
  body: string[];
}

export interface ServiceResponse {
  id: number;
  slug: string;
  title: string;
  shortTitle: string;
  tagline: string;
  intro: string;
  sections: ServiceSection[];
  highlights: string[];
}

export interface LogoResponse {
  id: number;
  name: string;
  imageUrl: string;
  href?: string;
  kind: string;
}

export interface LogosGroupedResponse {
  clients: LogoResponse[];
  partners: LogoResponse[];
  suppliers: LogoResponse[];
}

export interface ProcessResponse {
  id: number;
  groupKey: string;
  code?: string;
  title: string;
}

export interface SlideshowResponse {
  id: number;
  slug: string;
  imageUrl: string;
  title: string;
  subtitle?: string;
  linkUrl?: string;
  linkText?: string;
  isActive: boolean;
  sortOrder: number;
}

// --- Translation types ---

export interface TranslationPair {
  key: string;
  category?: string;
  vietnameseValue: string;
  translations: Record<string, string>;
  createdAt: string;
}

export interface EntityTranslationRow {
  id: number;
  entityType: string;
  entityId: number;
  fieldName: string;
  languageCode: string;
  value: string;
}

const API_BASE = import.meta.env.VITE_API_URL || "/api";

function getApiOrigin() {
  const base = API_BASE;
  if (!base) return "";

  try {
    const origin = typeof window !== "undefined" ? window.location.origin : "http://localhost";
    return new URL(base, origin).origin;
  } catch {
    return "";
  }
}

function resolveImageUrl(value: string) {
  if (!value) return value;

  if (
    value.startsWith("http://") ||
    value.startsWith("https://") ||
    value.startsWith("data:")
  ) {
    return value;
  }

  if (!value.startsWith("/")) {
    return value;
  }

  const apiOrigin = getApiOrigin();
  return apiOrigin ? `${apiOrigin}${value}` : value;
}

function mapActivity(item: ActivityResponse): ActivityResponse {
  return { ...item, imageUrl: resolveImageUrl(item.imageUrl) };
}

function mapNews(item: NewsResponse): NewsResponse {
  return { ...item, imageUrl: resolveImageUrl(item.imageUrl) };
}

function mapProject(item: ProjectResponse): ProjectResponse {
  return {
    ...item,
    imageUrl: resolveImageUrl(item.imageUrl),
    gallery: item.gallery?.map(resolveImageUrl),
  };
}

function mapLogo(item: LogoResponse): LogoResponse {
  return { ...item, imageUrl: resolveImageUrl(item.imageUrl) };
}

function mapSlideshow(item: SlideshowResponse): SlideshowResponse {
  return { ...item, imageUrl: resolveImageUrl(item.imageUrl) };
}

function mapLogosGrouped(data: LogosGroupedResponse): LogosGroupedResponse {
  return {
    clients: data.clients.map(mapLogo),
    partners: data.partners.map(mapLogo),
    suppliers: data.suppliers.map(mapLogo),
  };
}

// --- API functions ---

export const contentApi = {
  // Activities (pass ?lang= for translation)
  getActivities: (lang = "vi") =>
    api.get<ActivityResponse[]>(`/activities?lang=${lang}`)
      .then((res) => ({ ...res, data: res.data.map(mapActivity) })),

  getActivity: (slug: string, lang = "vi") =>
    api.get<ActivityResponse>(`/activities/${slug}?lang=${lang}`)
      .then((res) => ({ ...res, data: mapActivity(res.data) })),

  // News
  getNews: (lang = "vi") =>
    api.get<NewsResponse[]>(`/news?lang=${lang}`)
      .then((res) => ({ ...res, data: res.data.map(mapNews) })),

  getNewsItem: (slug: string, lang = "vi") =>
    api.get<NewsResponse>(`/news/${slug}?lang=${lang}`)
      .then((res) => ({ ...res, data: mapNews(res.data) })),

  // Projects
  getProjects: () =>
    api.get<ProjectResponse[]>("/projects")
      .then((res) => ({ ...res, data: res.data.map(mapProject) })),

  getProject: (slug: string) =>
    api.get<ProjectResponse>(`/projects/${slug}`)
      .then((res) => ({ ...res, data: mapProject(res.data) })),

  // Services
  getServices: () => api.get<ServiceResponse[]>("/services"),
  getService: (slug: string) => api.get<ServiceResponse>(`/services/${slug}`),

  // Logos
  getLogos: () =>
    api.get<LogosGroupedResponse>("/logos")
      .then((res) => ({ ...res, data: mapLogosGrouped(res.data) })),

  // Processes
  getProcesses: () => api.get<Record<string, ProcessResponse[]>>("/processes"),

  // Slideshow
  getSlideshow: (lang = "vi") =>
    api.get<SlideshowResponse[]>(`/slideshow?lang=${lang}`)
      .then((res) => ({ ...res, data: res.data.map(mapSlideshow) })),
};

// --- Translation API (admin-managed) ---

export const translationApi = {
  // Static UI translations
  getTranslationMap: (lang: string) =>
    api.get<{ languageCode: string; translations: Record<string, string> }>(`/translations/${lang}`),

  // Admin: translation pairs
  getPairs: (params?: { category?: string; search?: string }) =>
    api.get<TranslationPair[]>("/translations/admin", { params }),

  getCategories: () => api.get<string[]>("/translations/categories"),

  upsertPair: (data: {
    key: string;
    vietnameseValue: string;
    translations?: Record<string, string>;
    category?: string;
  }) => api.post("/translations/pair", data),

  bulkUpsert: (items: Array<{ key: string; languageCode: string; value: string; category?: string }>) =>
    api.post("/translations/bulk", items),

  deleteKey: (key: string) => api.delete(`/translations/key/${encodeURIComponent(key)}`),

  // Entity translations
  getEntityTypes: () =>
    api.get<Array<{ type: string; fields: string[] }>>("/translations/entity/types"),

  getEntityTranslations: (entityType: string, entityId: number) =>
    api.get<EntityTranslationRow[]>(`/translations/entity/${entityType}/${entityId}`),

  saveEntityTranslations: (
    entityType: string,
    entityId: number,
    data: { languageCode: string; translations: Record<string, string> }
  ) => api.post(`/translations/entity/${entityType}/${entityId}`, data),

  deleteEntityTranslations: (entityType: string, entityId: number) =>
    api.delete(`/translations/entity/${entityType}/${entityId}`),
};
