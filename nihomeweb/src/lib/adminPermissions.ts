/**
 * Single source of truth mapping admin route prefixes to the permission code
 * required to access them. Consumed by both <RequirePermission> route wrappers
 * in App.tsx and the sidebar filter in AdminLayout so a route never appears in
 * navigation that the user cannot actually open.
 *
 * Keep codes in sync with rbac-defaults.json. Wildcard-bearing roles
 * (SUPER_ADMIN, ADMIN, BGD) get all of these automatically through pattern
 * expansion at the server.
 */
export const ADMIN_PERMS = {
  dashboard: "dashboard.view",
  notifications: "dashboard.view",
  posts: "content.news.view",
  postsManage: "content.news.manage",
  projects: "content.projects.view",
  projectsManage: "content.projects.manage",
  services: "content.services.view",
  servicesManage: "content.services.manage",
  contacts: "contacts.view",
  contactsManage: "contacts.manage",
  recruitment: "recruitment.applications.view",
  recruitmentPositions: "recruitment.positions.view",
  recruitmentOptions: "recruitment.options.view",
  categories: "content.project-categories.view",
  about: "content.about.view",
  logos: "content.logos.view",
  processes: "processes.view",
  processesManage: "processes.manage",
  settings: "system.settings.view",
  translations: "content.translations.view",
  translationsManage: "content.translations.manage",
  emailTemplates: "system.settings.manage",
  activityLog: "system.audit.view",
  users: "users.view",
  usersManage: "users.manage",
  rbacRoles: "rbac.roles.view",
  rbacRolesManage: "rbac.roles.manage",
} as const;
