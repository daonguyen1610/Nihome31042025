import { lazy, Suspense } from "react";
import { QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Navigate, Route, Routes, useParams } from "react-router-dom";
import { Provider } from "react-redux";
import { store } from "@/store";
import { queryClient } from "@/lib/queryClient";
import { Toaster as Sonner } from "@/components/ui/sonner";
import { Toaster } from "@/components/ui/toaster";
import { TooltipProvider } from "@/components/ui/tooltip";
import { I18nProvider } from "@/lib/i18n";
import ProtectedRoute from "@/components/auth/ProtectedRoute";
import RequirePermission from "@/components/auth/RequirePermission";
import { PageLoading } from "@/components/PageState";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import Forbidden from "./pages/Forbidden.tsx";

const Index = lazy(() => import("./pages/Index.tsx"));
const Profile = lazy(() => import("./pages/Profile.tsx"));
const Services = lazy(() => import("./pages/Services.tsx"));
const ServiceDetail = lazy(() => import("./pages/ServiceDetail.tsx"));
const Projects = lazy(() => import("./pages/Projects.tsx"));
const ProjectDetail = lazy(() => import("./pages/ProjectDetail.tsx"));
const News = lazy(() => import("./pages/News.tsx"));
const NewsDetail = lazy(() => import("./pages/NewsDetail.tsx"));
const Activities = lazy(() => import("./pages/Activities.tsx"));
const ActivityDetail = lazy(() => import("./pages/ActivityDetail.tsx"));
const Clients = lazy(() => import("./pages/Clients.tsx"));
const Recruitment = lazy(() => import("./pages/Recruitment.tsx"));
const Contact = lazy(() => import("./pages/Contact.tsx"));
const Login = lazy(() => import("./pages/Login.tsx"));
const Register = lazy(() => import("./pages/Register.tsx"));
const ForgotPassword = lazy(() => import("./pages/ForgotPassword.tsx"));
const MyProfile = lazy(() => import("./pages/MyProfile.tsx"));
const AdminDashboard = lazy(() => import("./pages/admin/Dashboard.tsx"));
const AdminNotifications = lazy(() => import("./pages/admin/Notifications.tsx"));
const AdminUsers = lazy(() => import("./pages/admin/users/UserList.tsx"));
const AdminRoles = lazy(() => import("./pages/admin/users/RoleList.tsx"));
const AdminActivities = lazy(() => import("./pages/admin/Activities.tsx"));
const AdminNews = lazy(() => import("./pages/admin/News.tsx"));
const AdminProjects = lazy(() => import("./pages/admin/Projects.tsx"));
const AdminContacts = lazy(() => import("./pages/admin/Contacts.tsx"));
const AdminLeads = lazy(() => import("./pages/admin/Leads.tsx"));
const AdminCustomers = lazy(() => import("./pages/admin/Customers.tsx"));
const VendorPage = lazy(() => import("./pages/admin/procurement/VendorPage.tsx"));
const VendorDetail = lazy(() => import("./pages/admin/procurement/VendorDetail.tsx"));
const ProcurementControlPage = lazy(() => import("./pages/admin/procurement/ProcurementControlPage.tsx"));
const FinanceControlPage = lazy(() => import("./pages/admin/finance/FinanceControlPage.tsx"));
const AdminOpportunities = lazy(() => import("./pages/admin/Opportunities.tsx"));
const AdminQuotes = lazy(() => import("./pages/admin/Quotes.tsx"));
const AdminQuoteDetail = lazy(() => import("./pages/admin/QuoteDetail.tsx"));
const AdminMaterialRates = lazy(() => import("./pages/admin/MaterialRates.tsx"));
const AdminCapabilityDocuments = lazy(() => import("./pages/admin/CapabilityDocuments.tsx"));
const AdminTenders = lazy(() => import("./pages/admin/Tenders.tsx"));
const AdminTenderDetail = lazy(() => import("./pages/admin/TenderDetail.tsx"));
const AdminSurveys = lazy(() => import("./pages/admin/Surveys.tsx"));
const AdminSurveyDetail = lazy(() => import("./pages/admin/SurveyDetail.tsx"));
const AdminRecruitment = lazy(() => import("./pages/admin/Recruitment.tsx"));
const EmploymentTypes = lazy(() => import("./pages/admin/EmploymentTypes.tsx"));
const SettingsCenter = lazy(() => import("./pages/admin/SettingsCenter.tsx"));
const JobPositionForm = lazy(() => import("./pages/admin/JobPositionForm.tsx"));
const EmailTemplateConfig = lazy(() => import("./pages/admin/EmailTemplateConfig.tsx"));
const ProjectForm = lazy(() => import("./pages/admin/ProjectForm.tsx"));
const ProjectView = lazy(() => import("./pages/admin/ProjectView.tsx"));
const ActivityForm = lazy(() => import("./pages/admin/ActivityForm.tsx"));
const ActivityView = lazy(() => import("./pages/admin/ActivityView.tsx"));
const NewsForm = lazy(() => import("./pages/admin/NewsForm.tsx"));
const NewsView = lazy(() => import("./pages/admin/NewsView.tsx"));
const AdminCategories = lazy(() => import("./pages/admin/Categories.tsx"));
const AdminActivityLog = lazy(() => import("./pages/admin/ActivityLog.tsx"));
const AdminServices = lazy(() => import("./pages/admin/Services.tsx"));
const AdminLogosManager = lazy(() => import("./pages/admin/LogosManager.tsx"));
const AboutContent = lazy(() => import("./pages/admin/AboutContent.tsx"));
const ProcessList = lazy(() => import("./pages/admin/ProcessList.tsx"));
const LanguagesPage = lazy(() => import("./pages/admin/settings/Languages.tsx"));
const TranslationsPage = lazy(() => import("./pages/admin/settings/Translations.tsx"));
const MasterDataPage = lazy(() => import("./pages/admin/MasterData.tsx"));
const WorkflowsPage = lazy(() => import("./pages/admin/Workflows.tsx"));
const ContractsPage = lazy(() => import("./pages/admin/Contracts.tsx"));
const ContractDetailPage = lazy(() => import("./pages/admin/ContractDetail.tsx"));
const OperationalProjects = lazy(() => import("./pages/admin/OperationalProjects.tsx"));
const KpiDashboard = lazy(() => import("./pages/admin/KpiDashboard.tsx"));
const KpiConfiguration = lazy(() => import("./pages/admin/KpiConfiguration.tsx"));
const ProjectReports = lazy(() => import("@/pages/admin/ProjectReports"));
const AdminDesignProjects = lazy(() => import("./pages/admin/DesignProjects.tsx"));
const AdminDesignProjectDetail = lazy(() => import("./pages/admin/DesignProjectDetail.tsx"));
const AdminPermits = lazy(() => import("./pages/admin/Permits.tsx"));
const AdminConstructionTasks = lazy(() => import("./pages/admin/construction/ConstructionTasksPage.tsx"));
const AdminSiteDiary = lazy(() => import("./pages/admin/construction/SiteDiaryPage.tsx"));
const AdminPunchList = lazy(() => import("./pages/admin/construction/PunchListPage.tsx"));
const AdminHseViolations = lazy(() => import("./pages/admin/construction/HseViolationsPage.tsx"));
const AdminAcceptanceRecords = lazy(() => import("./pages/admin/construction/AcceptanceRecordsPage.tsx"));
const AdminAsBuiltDocuments = lazy(() => import("./pages/admin/construction/AsBuiltDocumentsPage.tsx"));
const AdminAsBuiltDocumentCategories = lazy(() => import("./pages/admin/construction/AsBuiltDocumentCategoriesPage.tsx"));
const AdminHandoverRecords = lazy(() => import("./pages/admin/construction/HandoverRecordsPage.tsx"));
const NotFound = lazy(() => import("./pages/NotFound.tsx"));

const LegacyPostRedirect = ({ edit = false }: { edit?: boolean }) => {
  const { slug } = useParams();
  return <Navigate to={`/admin/activities/${slug ?? ""}${edit ? "/edit" : ""}`} replace />;
};

const App = () => (
  <Provider store={store}>
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <TooltipProvider>
          <Toaster />
          <Sonner />
          <BrowserRouter>
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
                <Route path="/admin/activities/new" element={<ActivityForm mode="create" />} />
                <Route path="/admin/activities/:slug" element={<ActivityView />} />
                <Route path="/admin/activities/:slug/edit" element={<ActivityForm mode="edit" />} />
                <Route path="/admin/posts" element={<Navigate to="/admin/activities" replace />} />
                <Route path="/admin/posts/new" element={<Navigate to="/admin/activities/new" replace />} />
                <Route path="/admin/posts/:slug" element={<LegacyPostRedirect />} />
                <Route path="/admin/posts/:slug/edit" element={<LegacyPostRedirect edit />} />
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
                <Route path="/admin/projects/:slug/edit" element={<ProjectForm mode="edit" />} />
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
              <Route element={<RequirePermission code={[ADMIN_PERMS.procurement, ADMIN_PERMS.procurementMaterialRequests]} />}>
                <Route path="/admin/procurement-control" element={<ProcurementControlPage />} />
              </Route>
              <Route element={<RequirePermission code={[ADMIN_PERMS.financePayments, ADMIN_PERMS.financePeriods, ADMIN_PERMS.financeCorrections]} />}>
                <Route path="/admin/finance-control" element={<FinanceControlPage />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.opportunities} />}>
                <Route path="/admin/opportunities" element={<AdminOpportunities />} />
                <Route path="/admin/opportunities/:id" element={<AdminOpportunities />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.quotes} />}>
                <Route path="/admin/quotes" element={<AdminQuotes />} />
                <Route path="/admin/quotes/:id" element={<AdminQuoteDetail />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.materialRates} />}>
                <Route path="/admin/material-rates" element={<AdminMaterialRates />} />
                <Route path="/admin/material-rates/investment" element={<AdminMaterialRates catalogType="InvestmentRate" />} />
                <Route path="/admin/material-rates/boq" element={<AdminMaterialRates catalogType="Boq" />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.capabilityDocs} />}>
                <Route path="/admin/capability-documents" element={<AdminCapabilityDocuments />} />
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
                <Route path="/admin/operational-projects" element={<OperationalProjects />} />
                <Route path="/admin/operational-projects/:id" element={<OperationalProjects />} />
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
                <Route path="/admin/design-projects/:id" element={<AdminDesignProjectDetail />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.permits} />}>
                <Route path="/admin/permits" element={<AdminPermits />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.constructionTasks} />}>
                <Route path="/admin/construction/tasks" element={<AdminConstructionTasks />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.constructionDiary} />}>
                <Route path="/admin/construction/diary" element={<AdminSiteDiary />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.constructionPunch} />}>
                <Route path="/admin/construction/punchlist" element={<AdminPunchList />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.constructionHse} />}>
                <Route path="/admin/construction/hse" element={<AdminHseViolations />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.constructionAcceptance} />}>
                <Route path="/admin/construction/acceptance" element={<AdminAcceptanceRecords />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.constructionAsBuilt} />}>
                <Route path="/admin/construction/asbuilt" element={<AdminAsBuiltDocuments />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.constructionAsBuiltCategories} />}>
                <Route path="/admin/construction/asbuilt-categories" element={<AdminAsBuiltDocumentCategories />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.constructionHandover} />}>
                <Route path="/admin/construction/handover" element={<AdminHandoverRecords />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.recruitment} />}>
                <Route path="/admin/recruitment" element={<AdminRecruitment />} />
                <Route path="/admin/recruitment/new" element={<JobPositionForm mode="create" />} />
                <Route path="/admin/recruitment/:id/edit" element={<JobPositionForm mode="edit" />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.recruitmentOptions} />}>
                <Route path="/admin/recruitment/employment-types" element={<EmploymentTypes />} />
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
                <Route path="/admin/project-categories" element={<Navigate to="/admin/categories?tab=projects" replace />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.activityLog} />}>
                <Route path="/admin/activity-log" element={<AdminActivityLog />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.logos} />}>
                <Route path="/admin/clients" element={<AdminLogosManager kind="clients" titleKey="nav.clients" />} />
                <Route path="/admin/partners" element={<AdminLogosManager kind="partners" titleKey="nav.partners" />} />
                <Route path="/admin/suppliers" element={<AdminLogosManager kind="suppliers" titleKey="nav.suppliers" />} />
                <Route path="/admin/awards" element={<AdminLogosManager kind="awards" titleKey="nav.awards" />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.settings} />}>
                <Route path="/admin/slideshow" element={<Navigate to="/admin/settings?tab=slideshow" replace />} />
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
        </BrowserRouter>
      </TooltipProvider>
    </I18nProvider>
  </QueryClientProvider>
  </Provider>
);

export default App;
