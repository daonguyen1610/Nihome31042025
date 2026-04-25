import axios from "axios";

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? "/api",
  withCredentials: true,
});

// ─── Request types ───────────────────────────────────────────

export interface UpsertActivityRequest {
  slug: string;
  date: string;
  imageUrl: string;
  category: string;
  author?: string;
  title: string;
  excerpt: string;
  content: string[];
  sortOrder?: number;
}

export interface UpsertNewsRequest {
  slug: string;
  date: string;
  imageUrl: string;
  category: string;
  title: string;
  excerpt: string;
  content: string[];
  sortOrder?: number;
}

export interface UpsertProjectRequest {
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
  highlights?: { label: string; value: string }[];
  sortOrder?: number;
}

export interface UpsertLogoRequest {
  name: string;
  imageUrl: string;
  href?: string;
  kind: string;
  sortOrder?: number;
}

// ─── Admin API ───────────────────────────────────────────────

export const adminApi = {
  // Activities / Posts
  createActivity: (data: UpsertActivityRequest) =>
    api.post("/activities", data),
  updateActivity: (id: number, data: UpsertActivityRequest) =>
    api.put(`/activities/${id}`, data),
  deleteActivity: (id: number) =>
    api.delete(`/activities/${id}`),

  // News
  createNews: (data: UpsertNewsRequest) =>
    api.post("/news", data),
  updateNews: (id: number, data: UpsertNewsRequest) =>
    api.put(`/news/${id}`, data),
  deleteNews: (id: number) =>
    api.delete(`/news/${id}`),

  // Projects
  createProject: (data: UpsertProjectRequest) =>
    api.post("/projects", data),
  updateProject: (id: number, data: UpsertProjectRequest) =>
    api.put(`/projects/${id}`, data),
  deleteProject: (id: number) =>
    api.delete(`/projects/${id}`),

  // Logos
  createLogo: (data: UpsertLogoRequest) =>
    api.post("/logos", data),
  updateLogo: (id: number, data: UpsertLogoRequest) =>
    api.put(`/logos/${id}`, data),
  deleteLogo: (id: number) =>
    api.delete(`/logos/${id}`),
};

// ─── Slug helper ─────────────────────────────────────────────

export const slugify = (input: string) =>
  input
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/(^-|-$)+/g, "")
    .slice(0, 80) || `item-${Date.now()}`;
