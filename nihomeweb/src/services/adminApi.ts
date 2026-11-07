// Compatibility facade: feature modules own API calls and data contracts.
import { contentApi } from "./admin/contentApi";
import { crmApi } from "./admin/crmApi";
import { quotesApi } from "./admin/quotesApi";
import { tendersApi } from "./admin/tendersApi";
import { surveysApi } from "./admin/surveysApi";
import { designApi } from "./admin/designApi";
import { constructionApi } from "./admin/constructionApi";
import { configurationApi } from "./admin/configurationApi";
import { operationalProjectsApi } from "./admin/operationalProjectsApi";
import { contractsApi } from "./admin/contractsApi";
import { usersAuditApi } from "./admin/usersAuditApi";
export * from "./admin/types/content";
export * from "./admin/types/leads";
export * from "./admin/types/workflows";
export * from "./admin/types/customers";
export * from "./admin/types/vendors";
export * from "./admin/types/opportunities";
export * from "./admin/types/contracts";
export * from "./admin/types/operationalProjects";
export * from "./admin/types/contractDocuments";
export * from "./admin/types/quotes";
export * from "./admin/types/capabilities";
export * from "./admin/types/tenders";
export * from "./admin/types/surveys";
export * from "./admin/types/designProjects";
export * from "./admin/types/permits";
export * from "./admin/types/conceptOptions";
export * from "./admin/types/basicDesign";
export * from "./admin/types/shopDrawings";
export * from "./admin/types/drawingRevisions";
export * from "./admin/types/ifcReleases";
export * from "./admin/types/constructionTasks";
export * from "./admin/types/siteDiaries";
export * from "./admin/types/punchList";
export * from "./admin/types/procurement";
export * from "./admin/types/finance";
export * from "./admin/types/asBuilt";
export * from "./admin/types/handover";
export * from "./admin/types/usersAudit";

export const adminApi = {
  ...contentApi,
  ...crmApi,
  ...quotesApi,
  ...tendersApi,
  ...surveysApi,
  ...designApi,
  ...constructionApi,
  ...configurationApi,
  ...operationalProjectsApi,
  ...contractsApi,
  ...usersAuditApi,
};

export const slugify = (input: string) =>
  input
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/(^-|-$)+/g, "")
    .slice(0, 80) || `item-${Date.now()}`;
