import { Suspense } from "react";
import { lazyWithReload } from "@/lib/lazyWithReload";
import { PageLoadBoundary } from "@/components/PageLoadBoundary";
import { QueryClientProvider } from "@tanstack/react-query";
import {
  BrowserRouter,
  Navigate,
  Outlet,
  Route,
  Routes,
  useLocation,
  useParams,
} from "react-router-dom";
import { Provider } from "react-redux";
import { store } from "@/store";
import { queryClient } from "@/lib/queryClient";
import { Toaster as Sonner } from "@/components/ui/sonner";
import { Toaster } from "@/components/ui/toaster";
import { TooltipProvider } from "@/components/ui/tooltip";
import { I18nProvider } from "@/lib/i18n";
import { ConfirmationProvider } from "@/components/ui/confirmation";
import ProtectedRoute from "@/components/auth/ProtectedRoute";
import RequirePermission from "@/components/auth/RequirePermission";
import { PageLoading } from "@/components/PageState";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import Forbidden from "./pages/Forbidden.tsx";

const Index = lazyWithReload(() => import("./pages/Index.tsx"));
const Profile = lazyWithReload(() => import("./pages/Profile.tsx"));
const Services = lazyWithReload(() => import("./pages/Services.tsx"));
const ServiceDetail = lazyWithReload(() => import("./pages/ServiceDetail.tsx"));
const Projects = lazyWithReload(() => import("./pages/Projects.tsx"));
const ProjectDetail = lazyWithReload(() => import("./pages/ProjectDetail.tsx"));
const News = lazyWithReload(() => import("./pages/News.tsx"));
const NewsDetail = lazyWithReload(() => import("./pages/NewsDetail.tsx"));
const Activities = lazyWithReload(() => import("./pages/Activities.tsx"));
const ActivityDetail = lazyWithReload(() => import("./pages/ActivityDetail.tsx"));
const Clients = lazyWithReload(() => import("./pages/Clients.tsx"));
const Recruitment = lazyWithReload(() => import("./pages/Recruitment.tsx"));
const Contact = lazyWithReload(() => import("./pages/Contact.tsx"));
const Login = lazyWithReload(() => import("./pages/Login.tsx"));
const Register = lazyWithReload(() => import("./pages/Register.tsx"));
const ForgotPassword = lazyWithReload(() => import("./pages/ForgotPassword.tsx"));
const VendorRfqPortal = lazyWithReload(() => import("./pages/VendorRfqPortal.tsx"));
const MyProfile = lazyWithReload(() => import("./pages/MyProfile.tsx"));
const AdminDashboard = lazyWithReload(() => import("./pages/admin/Dashboard.tsx"));
const AdminNotifications = lazyWithReload(() => import("./pages/admin/Notifications.tsx"));
const AdminUsers = lazyWithReload(() => import("./pages/admin/users/UserList.tsx"));
const AdminRoles = lazyWithReload(() => import("./pages/admin/users/RoleList.tsx"));
const AdminActivities = lazyWithReload(() => import("./pages/admin/Activities.tsx"));
const AdminNews = lazyWithReload(() => import("./pages/admin/News.tsx"));
const AdminProjects = lazyWithReload(() => import("./pages/admin/Projects.tsx"));
const AdminContacts = lazyWithReload(() => import("./pages/admin/Contacts.tsx"));
const AdminLeads = lazyWithReload(() => import("./pages/admin/Leads.tsx"));
const AdminCustomers = lazyWithReload(() => import("./pages/admin/Customers.tsx"));
const VendorPage = lazyWithReload(() => import("./pages/admin/procurement/VendorPage.tsx"));
const VendorDetail = lazyWithReload(() => import("./pages/admin/procurement/VendorDetail.tsx"));
const RfqPage = lazyWithReload(() => import("./pages/admin/procurement/RfqPage.tsx"));
const ProcurementControlPage = lazyWithReload(
  () => import("./pages/admin/procurement/ProcurementControlPage.tsx"),
);
const BoqRevisionDetailPage = lazyWithReload(
  () => import("./pages/admin/procurement/BoqRevisionDetailPage.tsx"),
);
const MaterialRequestDetailPage = lazyWithReload(
  () => import("./pages/admin/procurement/MaterialRequestDetailPage.tsx"),
);
const MaterialAlertDetailPage = lazyWithReload(
  () => import("./pages/admin/procurement/MaterialAlertDetailPage.tsx"),
);
const WarehouseTransactionDetailPage = lazyWithReload(
  () => import("./pages/admin/procurement/WarehouseTransactionDetailPage.tsx"),
);
const FinanceControlPage = lazyWithReload(
  () => import("./pages/admin/finance/FinanceControlPage.tsx"),
);
const AdminOpportunities = lazyWithReload(() => import("./pages/admin/Opportunities.tsx"));
const OpportunityDesignCreate = lazyWithReload(
  () => import("./pages/admin/OpportunityDesignCreate.tsx"),
);
const AdminQuotes = lazyWithReload(() => import("./pages/admin/Quotes.tsx"));
const AdminQuoteDetail = lazyWithReload(() => import("./pages/admin/QuoteDetail.tsx"));
const AdminMaterialRates = lazyWithReload(() => import("./pages/admin/MaterialRates.tsx"));
const BoqTemplateLibrary = lazyWithReload(() => import("./pages/admin/BoqTemplateLibrary.tsx"));
const AdminCapabilityDocuments = lazyWithReload(
  () => import("./pages/admin/CapabilityDocuments.tsx"),
);
const AdminTenders = lazyWithReload(() => import("./pages/admin/Tenders.tsx"));
const AdminTenderDetail = lazyWithReload(() => import("./pages/admin/TenderDetail.tsx"));
const AdminSurveys = lazyWithReload(() => import("./pages/admin/Surveys.tsx"));
const AdminSurveyDetail = lazyWithReload(() => import("./pages/admin/SurveyDetail.tsx"));
const AdminRecruitment = lazyWithReload(() => import("./pages/admin/Recruitment.tsx"));
const EmploymentTypes = lazyWithReload(() => import("./pages/admin/EmploymentTypes.tsx"));
const SettingsCenter = lazyWithReload(() => import("./pages/admin/SettingsCenter.tsx"));
const JobPositionForm = lazyWithReload(() => import("./pages/admin/JobPositionForm.tsx"));
const EmailTemplateConfig = lazyWithReload(() => import("./pages/admin/EmailTemplateConfig.tsx"));
const ProjectForm = lazyWithReload(() => import("./pages/admin/ProjectForm.tsx"));
const ProjectView = lazyWithReload(() => import("./pages/admin/ProjectView.tsx"));
const ActivityForm = lazyWithReload(() => import("./pages/admin/ActivityForm.tsx"));
const ActivityView = lazyWithReload(() => import("./pages/admin/ActivityView.tsx"));
const NewsForm = lazyWithReload(() => import("./pages/admin/NewsForm.tsx"));
const NewsView = lazyWithReload(() => import("./pages/admin/NewsView.tsx"));
const AdminCategories = lazyWithReload(() => import("./pages/admin/Categories.tsx"));
const AdminActivityLog = lazyWithReload(() => import("./pages/admin/ActivityLog.tsx"));
const AdminServices = lazyWithReload(() => import("./pages/admin/Services.tsx"));
const AdminLogosManager = lazyWithReload(() => import("./pages/admin/LogosManager.tsx"));
const AboutContent = lazyWithReload(() => import("./pages/admin/AboutContent.tsx"));
const ProcessList = lazyWithReload(() => import("./pages/admin/ProcessList.tsx"));
const LanguagesPage = lazyWithReload(() => import("./pages/admin/settings/Languages.tsx"));
const TranslationsPage = lazyWithReload(() => import("./pages/admin/settings/Translations.tsx"));
const MasterDataPage = lazyWithReload(() => import("./pages/admin/MasterData.tsx"));
const WorkflowsPage = lazyWithReload(() => import("./pages/admin/Workflows.tsx"));
const ContractsPage = lazyWithReload(() => import("./pages/admin/Contracts.tsx"));
const ContractDetailPage = lazyWithReload(() => import("./pages/admin/ContractDetail.tsx"));
const OperationalProjects = lazyWithReload(() => import("./pages/admin/OperationalProjects.tsx"));
const DepartmentWorkPackages = lazyWithReload(
  () => import("./pages/admin/DepartmentWorkPackages.tsx"),
);
const KpiDashboard = lazyWithReload(() => import("./pages/admin/KpiDashboard.tsx"));
const KpiConfiguration = lazyWithReload(() => import("./pages/admin/KpiConfiguration.tsx"));
const ProjectReports = lazyWithReload(() => import("@/pages/admin/ProjectReports"));
const AdminDesignProjects = lazyWithReload(() => import("./pages/admin/DesignProjects.tsx"));
const AdminDesignProjectDetail = lazyWithReload(
  () => import("./pages/admin/DesignProjectDetail.tsx"),
);
const AdminDesignReviewInbox = lazyWithReload(() => import("./pages/admin/DesignReviewInbox.tsx"));
const AdminDesignApprovals = lazyWithReload(() => import("./pages/admin/DesignApprovals.tsx"));
const AdminPermits = lazyWithReload(() => import("./pages/admin/Permits.tsx"));
const BchOperationsPortal = lazyWithReload(() =>
  import("./pages/admin/construction/BchOperationsPortal").then((module) => ({
    default: module.BchOperationsPortal,
  })),
);
const BchQaQcPortal = lazyWithReload(() =>
  import("./pages/admin/construction/BchQaQcPortal").then((module) => ({
    default: module.BchQaQcPortal,
  })),
);
const BchQsPage = lazyWithReload(() =>
  import("./pages/admin/construction/BchQsPage").then((module) => ({
    default: module.BchQsPage,
  })),
);
const CmAreaPage = lazyWithReload(() =>
  import("./pages/admin/construction/CmAreaPage").then((module) => ({
    default: module.CmAreaPage,
  })),
);
const AsBuiltDocumentCategories = lazyWithReload(
  () => import("./pages/admin/construction/AsBuiltDocumentCategoriesPage"),
);
const legacyRedirectImports = () => import("./pages/admin/construction/ConstructionRedirect");
const LegacyConstructionRedirect = lazyWithReload(() =>
  legacyRedirectImports().then((module) => ({
    default: module.LegacyConstructionRedirect,
  })),
);
const LegacyCmAreaRedirect = lazyWithReload(() =>
  legacyRedirectImports().then((module) => ({ default: module.LegacyCmAreaRedirect })),
);
const LegacyPunchRedirect = lazyWithReload(() =>
  legacyRedirectImports().then((module) => ({ default: module.LegacyPunchRedirect })),
);
const LegacyAcceptanceRedirect = lazyWithReload(() =>
  legacyRedirectImports().then((module) => ({ default: module.LegacyAcceptanceRedirect })),
);
const LegacyHseRedirect = lazyWithReload(() =>
  legacyRedirectImports().then((module) => ({ default: module.LegacyHseRedirect })),
);
const NotFound = lazyWithReload(() => import("./pages/NotFound.tsx"));

const LegacyPostRedirect = ({ edit = false }: { edit?: boolean }) => {
  const { slug } = useParams();
  return <Navigate to={`/admin/activities/${slug ?? ""}${edit ? "/edit" : ""}`} replace />;
};

const LegacyFinanceContractRedirect = ({ direction }: { direction: "Upstream" | "Downstream" }) => {
  const { id } = useParams();
  const location = useLocation();
  const search = new URLSearchParams(location.search);
  if (!search.has("returnTo")) {
    search.set(
      "returnTo",
      direction === "Upstream" ? "/admin/finance/contracts" : "/admin/finance/input-contracts",
    );
  }
  return <Navigate to={`/admin/contracts/${id ?? ""}?${search.toString()}`} replace />;
};

const DesignProjectDetailAccess = () => {
  const location = useLocation();
  if (new URLSearchParams(location.search).get("tab") === "reviews") return <Outlet />;
  return <RequirePermission code={ADMIN_PERMS.designProjects} />;
};

const App = () => (
  <Provider store={store}>
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <TooltipProvider>
          <Toaster />
          <Sonner />
          <BrowserRouter>
            <ConfirmationProvider>
              <PageLoadBoundary>
                <Suspense fallback={<PageLoading />}>
                  <Routes>
                    <Route path="/" element={<Index />} />
                    <Route path="/profile" element={<Profile />} />
                    <Route path="/services" element={<Services />} />
                    <Route path="/services/:slug" element={<ServiceDetail />} />
                    <Route path="/projects" element={<Projects />} />
                    <Route path="/projects/:slug" element={<ProjectDetail />} />
                    <Route path="/news" element={<News />} />
                    <Route path="/news/:slug" element={<NewsDetail />} />
                    <Route path="/activities" element={<Activities />} />
                    <Route path="/activities/:slug" element={<ActivityDetail />} />
                    <Route path="/clients" element={<Clients />} />
                    <Route path="/recruitment" element={<Recruitment />} />
                    <Route path="/contact" element={<Contact />} />
                    <Route path="/login" element={<Login />} />
                    <Route path="/register" element={<Register />} />
                    <Route path="/forgot-password" element={<ForgotPassword />} />
                    <Route path="/vendor/rfqs" element={<VendorRfqPortal />} />
                    <Route element={<ProtectedRoute />}>
                      <Route path="/my-profile" element={<MyProfile />} />
                    </Route>
                    <Route path="/forbidden" element={<Forbidden />} />
                    <Route element={<ProtectedRoute />}>
                      <Route element={<RequirePermission code={ADMIN_PERMS.dashboard} />}>
                        <Route path="/admin" element={<AdminDashboard />} />
                        <Route path="/admin/notifications" element={<AdminNotifications />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.users} />}>
                        <Route path="/admin/users" element={<AdminUsers />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.rbacRoles} />}>
                        <Route path="/admin/roles" element={<AdminRoles />} />
                        <Route path="/admin/roles/:id" element={<AdminRoles />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.activities} />}>
                        <Route path="/admin/activities" element={<AdminActivities />} />
                        <Route
                          path="/admin/activities/new"
                          element={<ActivityForm mode="create" />}
                        />
                        <Route path="/admin/activities/:slug" element={<ActivityView />} />
                        <Route
                          path="/admin/activities/:slug/edit"
                          element={<ActivityForm mode="edit" />}
                        />
                        <Route
                          path="/admin/posts"
                          element={<Navigate to="/admin/activities" replace />}
                        />
                        <Route
                          path="/admin/posts/new"
                          element={<Navigate to="/admin/activities/new" replace />}
                        />
                        <Route path="/admin/posts/:slug" element={<LegacyPostRedirect />} />
                        <Route
                          path="/admin/posts/:slug/edit"
                          element={<LegacyPostRedirect edit />}
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.news} />}>
                        <Route path="/admin/news" element={<AdminNews />} />
                        <Route path="/admin/news/new" element={<NewsForm mode="create" />} />
                        <Route path="/admin/news/:slug" element={<NewsView />} />
                        <Route path="/admin/news/:slug/edit" element={<NewsForm mode="edit" />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.projects} />}>
                        <Route path="/admin/projects" element={<AdminProjects />} />
                        <Route path="/admin/projects/new" element={<ProjectForm mode="create" />} />
                        <Route path="/admin/projects/:slug" element={<ProjectView />} />
                        <Route
                          path="/admin/projects/:slug/edit"
                          element={<ProjectForm mode="edit" />}
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.services} />}>
                        <Route path="/admin/services" element={<AdminServices />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.contacts} />}>
                        <Route path="/admin/contacts" element={<AdminContacts />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.leads} />}>
                        <Route path="/admin/leads" element={<AdminLeads />} />
                        {/* Lead notifications link to /admin/leads/{id} (LeadService.cs:490).
                    The list page reads the id and opens that record itself. */}
                        <Route path="/admin/leads/:id" element={<AdminLeads />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.customers} />}>
                        <Route path="/admin/customers" element={<AdminCustomers />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.vendors} />}>
                        <Route path="/admin/vendors" element={<VendorPage />} />
                        <Route path="/admin/vendors/:id" element={<VendorDetail />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.procurementRfqs} />}>
                        <Route path="/admin/procurement-control/rfqs" element={<RfqPage />} />
                      </Route>
                      <Route
                        element={
                          <RequirePermission
                            code={[
                              ADMIN_PERMS.procurement,
                              ADMIN_PERMS.procurementMaterialRequests,
                              ADMIN_PERMS.procurementResources,
                              ADMIN_PERMS.procurementIfc,
                              ADMIN_PERMS.procurementWarehouse,
                              ADMIN_PERMS.procurementMaterialAlerts,
                            ]}
                          />
                        }
                      >
                        <Route
                          path="/admin/procurement-control"
                          element={<ProcurementControlPage />}
                        />
                      </Route>
                      <Route
                        element={<RequirePermission code={ADMIN_PERMS.procurementMaterialAlerts} />}
                      >
                        <Route
                          path="/admin/procurement-control/projects/:projectId/material-alerts/:alertId"
                          element={<MaterialAlertDetailPage />}
                        />
                      </Route>
                      <Route
                        element={
                          <RequirePermission code={ADMIN_PERMS.procurementMaterialRequests} />
                        }
                      >
                        <Route
                          path="/admin/procurement-control/projects/:projectId/material-requests/:requestId"
                          element={<MaterialRequestDetailPage />}
                        />
                      </Route>
                      <Route
                        element={<RequirePermission code={ADMIN_PERMS.procurementWarehouse} />}
                      >
                        <Route
                          path="/admin/procurement-control/projects/:projectId/warehouse/:transactionType/:transactionId"
                          element={<WarehouseTransactionDetailPage />}
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.procurement} />}>
                        <Route
                          path="/admin/procurement-control/projects/:projectId/boq/:boqId"
                          element={<BoqRevisionDetailPage />}
                        />
                      </Route>
                      <Route
                        element={
                          <RequirePermission
                            code={[
                              ADMIN_PERMS.financePayments,
                              ADMIN_PERMS.financePeriods,
                              ADMIN_PERMS.financeCorrections,
                            ]}
                          />
                        }
                      >
                        <Route path="/admin/finance-control" element={<FinanceControlPage />} />
                      </Route>
                      <Route
                        element={
                          <RequirePermission
                            code={[
                              ADMIN_PERMS.contractsViewAll,
                              ADMIN_PERMS.contractsViewUpstreamAll,
                            ]}
                          />
                        }
                      >
                        <Route
                          path="/admin/finance/contracts"
                          element={<ContractsPage mode="upstream" />}
                        />
                        <Route
                          path="/admin/finance/contracts/:id"
                          element={<LegacyFinanceContractRedirect direction="Upstream" />}
                        />
                      </Route>
                      <Route
                        element={
                          <RequirePermission
                            code={[
                              ADMIN_PERMS.contractsViewAll,
                              ADMIN_PERMS.contractsViewDownstreamAll,
                            ]}
                          />
                        }
                      >
                        <Route
                          path="/admin/finance/input-contracts"
                          element={<ContractsPage mode="downstream" />}
                        />
                        <Route
                          path="/admin/finance/input-contracts/:id"
                          element={<LegacyFinanceContractRedirect direction="Downstream" />}
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.opportunities} />}>
                        <Route path="/admin/opportunities" element={<AdminOpportunities />} />
                        <Route path="/admin/opportunities/:id" element={<AdminOpportunities />} />
                        <Route
                          element={<RequirePermission code={ADMIN_PERMS.opportunitiesManage} />}
                        >
                          <Route
                            element={
                              <RequirePermission code={ADMIN_PERMS.operationalProjectsManage} />
                            }
                          >
                            <Route
                              path="/admin/opportunities/:id/design-project/new"
                              element={<OpportunityDesignCreate />}
                            />
                          </Route>
                        </Route>
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.quotes} />}>
                        <Route path="/admin/quotes" element={<AdminQuotes />} />
                        <Route path="/admin/quotes/:id" element={<AdminQuoteDetail />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.materialRates} />}>
                        <Route path="/admin/material-rates" element={<AdminMaterialRates />} />
                        <Route
                          path="/admin/material-rates/investment"
                          element={<AdminMaterialRates />}
                        />
                        <Route path="/admin/material-rates/boq" element={<BoqTemplateLibrary />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.capabilityDocs} />}>
                        <Route
                          path="/admin/capability-documents"
                          element={<AdminCapabilityDocuments />}
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.tenders} />}>
                        <Route path="/admin/tenders" element={<AdminTenders />} />
                        <Route path="/admin/tenders/:id" element={<AdminTenderDetail />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.surveys} />}>
                        <Route path="/admin/surveys" element={<AdminSurveys />} />
                        <Route path="/admin/surveys/:id" element={<AdminSurveyDetail />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.contracts} />}>
                        <Route path="/admin/contracts" element={<ContractsPage />} />
                        <Route path="/admin/contracts/:id" element={<ContractDetailPage />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.operationalProjects} />}>
                        <Route
                          path="/admin/operational-projects"
                          element={<OperationalProjects />}
                        />
                        <Route
                          path="/admin/operational-projects/:id"
                          element={<OperationalProjects />}
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.workPackages} />}>
                        <Route path="/admin/work-packages" element={<DepartmentWorkPackages />} />
                        <Route
                          path="/admin/work-packages/projects"
                          element={<DepartmentWorkPackages />}
                        />
                        <Route
                          path="/admin/work-packages/contracts"
                          element={<DepartmentWorkPackages />}
                        />
                        <Route
                          path="/admin/work-packages/internal"
                          element={<DepartmentWorkPackages />}
                        />
                        <Route
                          path="/admin/work-packages/:id"
                          element={<DepartmentWorkPackages />}
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.kpi} />}>
                        <Route path="/admin/kpi" element={<KpiDashboard />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.kpiManage} />}>
                        <Route path="/admin/kpi/configuration" element={<KpiConfiguration />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.projectReports} />}>
                        <Route path="/admin/reports/projects" element={<ProjectReports />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.designProjects} />}>
                        <Route path="/admin/design-projects" element={<AdminDesignProjects />} />
                      </Route>
                      <Route element={<DesignProjectDetailAccess />}>
                        <Route
                          path="/admin/design-projects/:id"
                          element={<AdminDesignProjectDetail />}
                        />
                      </Route>
                      <Route
                        path="/admin/design-reviews/:id"
                        element={<AdminDesignReviewInbox />}
                      />
                      <Route path="/admin/design-approvals" element={<AdminDesignApprovals />} />
                      <Route element={<RequirePermission code={ADMIN_PERMS.permits} />}>
                        <Route path="/admin/permits" element={<AdminPermits />} />
                      </Route>
                      <Route
                        element={
                          <RequirePermission
                            code={[
                              ADMIN_PERMS.constructionResourceRequests,
                              ADMIN_PERMS.constructionDiary,
                            ]}
                          />
                        }
                      >
                        <Route
                          path="/admin/construction/bch/operations"
                          element={<BchOperationsPortal />}
                        />
                      </Route>
                      <Route
                        element={
                          <RequirePermission
                            code={[
                              ADMIN_PERMS.constructionQaQc,
                              ADMIN_PERMS.constructionPunch,
                              ADMIN_PERMS.constructionAcceptance,
                              ADMIN_PERMS.constructionHse,
                            ]}
                          />
                        }
                      >
                        <Route path="/admin/construction/bch/qa-qc" element={<BchQaQcPortal />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.constructionQs} />}>
                        <Route path="/admin/construction/bch/qs" element={<BchQsPage />} />
                      </Route>
                      <Route
                        element={
                          <RequirePermission
                            code={[
                              ADMIN_PERMS.constructionCmPreparation,
                              ADMIN_PERMS.constructionTasks,
                            ]}
                          />
                        }
                      >
                        <Route
                          path="/admin/construction/cm/schedule"
                          element={<CmAreaPage area="schedule" />}
                        />
                      </Route>
                      <Route
                        element={
                          <RequirePermission
                            code={[
                              ADMIN_PERMS.constructionResourceRequestsReview,
                              ADMIN_PERMS.constructionResourceRequests,
                              ADMIN_PERMS.constructionQsReview,
                              ADMIN_PERMS.constructionQs,
                            ]}
                          />
                        }
                      >
                        <Route
                          path="/admin/construction/cm/commercial"
                          element={<CmAreaPage area="commercial" />}
                        />
                      </Route>
                      <Route
                        element={
                          <RequirePermission
                            code={[
                              ADMIN_PERMS.constructionQaQcReview,
                              ADMIN_PERMS.constructionQaQc,
                              ADMIN_PERMS.constructionPunchVerify,
                              ADMIN_PERMS.constructionPunch,
                              ADMIN_PERMS.constructionAcceptanceApprove,
                              ADMIN_PERMS.constructionAcceptance,
                              ADMIN_PERMS.constructionAsBuiltApprove,
                              ADMIN_PERMS.constructionAsBuilt,
                              ADMIN_PERMS.constructionHandoverComplete,
                              ADMIN_PERMS.constructionHandover,
                            ]}
                          />
                        }
                      >
                        <Route
                          path="/admin/construction/cm/quality"
                          element={<CmAreaPage area="quality" />}
                        />
                      </Route>
                      <Route
                        element={
                          <RequirePermission
                            code={[
                              ADMIN_PERMS.constructionHseConfirm,
                              ADMIN_PERMS.constructionHseClose,
                              ADMIN_PERMS.constructionHse,
                            ]}
                          />
                        }
                      >
                        <Route
                          path="/admin/construction/cm/safety"
                          element={<CmAreaPage area="safety" />}
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.contracts} />}>
                        <Route
                          path="/admin/construction/cm/contracts"
                          element={<CmAreaPage area="contracts" />}
                        />
                      </Route>
                      <Route
                        element={
                          <RequirePermission code={ADMIN_PERMS.constructionAsBuiltCategories} />
                        }
                      >
                        <Route
                          path="/admin/construction/asbuilt-categories"
                          element={<AsBuiltDocumentCategories />}
                        />
                      </Route>

                      {/* Legacy routes: preserve query and redirect to the role workspace. */}
                      <Route element={<RequirePermission code={ADMIN_PERMS.constructionTasks} />}>
                        <Route
                          path="/admin/construction/tasks"
                          element={
                            <LegacyConstructionRedirect
                              path="/admin/construction/cm/schedule"
                              tab="tasks"
                            />
                          }
                        />
                        <Route
                          path="/admin/construction/progress"
                          element={
                            <LegacyConstructionRedirect path="/admin/construction/cm/schedule" />
                          }
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.constructionDiary} />}>
                        <Route
                          path="/admin/construction/diary"
                          element={
                            <LegacyConstructionRedirect
                              path="/admin/construction/bch/operations"
                              tab="diary"
                            />
                          }
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.constructionPunch} />}>
                        <Route
                          path="/admin/construction/punchlist"
                          element={<LegacyPunchRedirect />}
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.constructionHse} />}>
                        <Route path="/admin/construction/hse" element={<LegacyHseRedirect />} />
                      </Route>
                      <Route
                        element={<RequirePermission code={ADMIN_PERMS.constructionAcceptance} />}
                      >
                        <Route
                          path="/admin/construction/acceptance"
                          element={<LegacyAcceptanceRedirect />}
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.constructionAsBuilt} />}>
                        <Route
                          path="/admin/construction/asbuilt"
                          element={
                            <LegacyConstructionRedirect
                              path="/admin/construction/cm/quality"
                              tab="asbuilt"
                            />
                          }
                        />
                      </Route>
                      <Route
                        element={<RequirePermission code={ADMIN_PERMS.constructionHandover} />}
                      >
                        <Route
                          path="/admin/construction/handover"
                          element={
                            <LegacyConstructionRedirect
                              path="/admin/construction/cm/quality"
                              tab="handover"
                            />
                          }
                        />
                      </Route>
                      <Route
                        element={
                          <RequirePermission code={ADMIN_PERMS.constructionResourceRequests} />
                        }
                      >
                        <Route
                          path="/admin/construction/bch/resource-requests"
                          element={
                            <LegacyConstructionRedirect
                              path="/admin/construction/bch/operations"
                              tab="resources"
                            />
                          }
                        />
                      </Route>
                      <Route
                        element={
                          <RequirePermission
                            code={[
                              ADMIN_PERMS.constructionCmPreparation,
                              ADMIN_PERMS.constructionResourceRequestsReview,
                              ADMIN_PERMS.constructionQaQcReview,
                              ADMIN_PERMS.constructionQsReview,
                            ]}
                          />
                        }
                      >
                        <Route path="/admin/construction/cm" element={<LegacyCmAreaRedirect />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.recruitment} />}>
                        <Route path="/admin/recruitment" element={<AdminRecruitment />} />
                        <Route
                          path="/admin/recruitment/new"
                          element={<JobPositionForm mode="create" />}
                        />
                        <Route
                          path="/admin/recruitment/:id/edit"
                          element={<JobPositionForm mode="edit" />}
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.recruitmentOptions} />}>
                        <Route
                          path="/admin/recruitment/employment-types"
                          element={<EmploymentTypes />}
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.emailTemplates} />}>
                        <Route path="/admin/email-templates" element={<EmailTemplateConfig />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.settings} />}>
                        <Route path="/admin/settings" element={<SettingsCenter />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.translations} />}>
                        <Route path="/admin/languages" element={<LanguagesPage />} />
                        <Route path="/admin/translations" element={<TranslationsPage />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.masterData} />}>
                        <Route path="/admin/master-data" element={<MasterDataPage />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.workflow} />}>
                        <Route path="/admin/workflows" element={<WorkflowsPage />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.categories} />}>
                        <Route path="/admin/categories" element={<AdminCategories />} />
                        <Route
                          path="/admin/project-categories"
                          element={<Navigate to="/admin/categories?tab=projects" replace />}
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.activityLog} />}>
                        <Route path="/admin/activity-log" element={<AdminActivityLog />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.logos} />}>
                        <Route
                          path="/admin/clients"
                          element={<AdminLogosManager kind="clients" titleKey="nav.clients" />}
                        />
                        <Route
                          path="/admin/partners"
                          element={<AdminLogosManager kind="partners" titleKey="nav.partners" />}
                        />
                        <Route
                          path="/admin/suppliers"
                          element={<AdminLogosManager kind="suppliers" titleKey="nav.suppliers" />}
                        />
                        <Route
                          path="/admin/awards"
                          element={<AdminLogosManager kind="awards" titleKey="nav.awards" />}
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.settings} />}>
                        <Route
                          path="/admin/slideshow"
                          element={
                            <Navigate to="/admin/settings?tab=general&section=slideshow" replace />
                          }
                        />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.about} />}>
                        <Route path="/admin/about" element={<AboutContent />} />
                      </Route>
                      <Route element={<RequirePermission code={ADMIN_PERMS.processes} />}>
                        <Route
                          path="/admin/processes/general"
                          element={<ProcessList groupKey="general" titleKey="proc.general" />}
                        />
                        <Route
                          path="/admin/processes/ptcskh"
                          element={<ProcessList groupKey="ptcskh" titleKey="proc.ptcskh" />}
                        />
                        <Route
                          path="/admin/processes/dt"
                          element={<ProcessList groupKey="dt" titleKey="proc.dt" />}
                        />
                        <Route
                          path="/admin/processes/tk"
                          element={<ProcessList groupKey="tk" titleKey="proc.tk" />}
                        />
                        <Route
                          path="/admin/processes/tc"
                          element={<ProcessList groupKey="tc" titleKey="proc.tc" />}
                        />
                        <Route
                          path="/admin/processes/ttqtct"
                          element={<ProcessList groupKey="ttqtct" titleKey="proc.ttqtct" />}
                        />
                        <Route
                          path="/admin/processes/qlns"
                          element={<ProcessList groupKey="qlns" titleKey="proc.qlns" />}
                        />
                        <Route
                          path="/admin/processes/mhdgncu"
                          element={<ProcessList groupKey="mhdgncu" titleKey="proc.mhdgncu" />}
                        />
                      </Route>
                    </Route>
                    {/* ADD ALL CUSTOM ROUTES ABOVE THE CATCH-ALL "*" ROUTE */}
                    <Route path="*" element={<NotFound />} />
                  </Routes>
                </Suspense>
              </PageLoadBoundary>
            </ConfirmationProvider>
          </BrowserRouter>
        </TooltipProvider>
      </I18nProvider>
    </QueryClientProvider>
  </Provider>
);

export default App;
