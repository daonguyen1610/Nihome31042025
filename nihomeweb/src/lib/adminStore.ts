// Lightweight client-side admin store layered over the static seed data.
// Persists overrides/additions/deletions in localStorage so admin CRUD
// pages feel real without a backend.

import { activities as seedActivities, type Activity } from "@/data/activities";
import { projects as seedProjects, type Project } from "@/data/projects";

const PROJECTS_KEY = "nicon_admin_projects_v1";
const POSTS_KEY = "nicon_admin_posts_v1";

type Store<T> = { items: T[] };

function load<T>(key: string, seed: T[]): T[] {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return [...seed];
    const parsed = JSON.parse(raw) as Store<T>;
    return parsed.items ?? [...seed];
  } catch {
    return [...seed];
  }
}

function save<T>(key: string, items: T[]) {
  try {
    localStorage.setItem(key, JSON.stringify({ items }));
    window.dispatchEvent(new CustomEvent(`${key}:changed`));
  } catch {
    /* ignore */
  }
}

/* -------------------- Projects -------------------- */

export const getAllProjects = (): Project[] => load<Project>(PROJECTS_KEY, seedProjects);

export const getProject = (id: string): Project | undefined =>
  getAllProjects().find((p) => p.id === id);

export const upsertProject = (p: Project) => {
  const list = getAllProjects();
  const idx = list.findIndex((x) => x.id === p.id);
  if (idx >= 0) list[idx] = p;
  else list.unshift(p);
  save(PROJECTS_KEY, list);
};

export const deleteProject = (id: string) => {
  save(
    PROJECTS_KEY,
    getAllProjects().filter((p) => p.id !== id),
  );
};

/* -------------------- Posts (Activities) -------------------- */

export const getAllPosts = (): Activity[] => load<Activity>(POSTS_KEY, seedActivities);

export const getPost = (id: string): Activity | undefined =>
  getAllPosts().find((p) => p.id === id);

export const upsertPost = (p: Activity) => {
  const list = getAllPosts();
  const idx = list.findIndex((x) => x.id === p.id);
  if (idx >= 0) list[idx] = p;
  else list.unshift(p);
  save(POSTS_KEY, list);
};

export const deletePost = (id: string) => {
  save(
    POSTS_KEY,
    getAllPosts().filter((p) => p.id !== id),
  );
};

/* -------------------- helpers -------------------- */

export const slugify = (input: string) =>
  input
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/(^-|-$)+/g, "")
    .slice(0, 80) || `item-${Date.now()}`;
