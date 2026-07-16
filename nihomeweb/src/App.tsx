import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Navigate, Route, Routes, useParams } from "react-router-dom";
import { Provider } from "react-redux";
import { store } from "@/store";
import { Toaster as Sonner } from "@/components/ui/sonner";
import { Toaster } from "@/components/ui/toaster";
import { TooltipProvider } from "@/components/ui/tooltip";
import { I18nProvider } from "@/lib/i18n";
import ProtectedRoute from "@/components/auth/ProtectedRoute";
import RequirePermission from "@/components/auth/RequirePermission";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import Forbidden from "./pages/Forbidden.tsx";
import Index from "./pages/Index.tsx";
import Profile from "./pages/Profile.tsx";
import Services from "./pages/Services.tsx";
import ServiceDetail from "./pages/ServiceDetail.tsx";
import Projects from "./pages/Projects.tsx";
import ProjectDetail from "./pages/ProjectDetail.tsx";
import News from "./pages/News.tsx";
import NewsDetail from "./pages/NewsDetail.tsx";
import Activities from "./pages/Activities.tsx";
import ActivityDetail from "./pages/ActivityDetail.tsx";
import Clients from "./pages/Clients.tsx";
import Recruitment from "./pages/Recruitment.tsx";
import Contact from "./pages/Contact.tsx";
import Login from "./pages/Login.tsx";
import Register from "./pages/Register.tsx";
import ForgotPassword from "./pages/ForgotPassword.tsx";
import MyProfile from "./pages/MyProfile.tsx";
import AdminDashboard from "./pages/admin/Dashboard.tsx";
import AdminNotifications from "./pages/admin/Notifications.tsx";
import AdminUsers from "./pages/admin/users/UserList.tsx";
import AdminRoles from "./pages/admin/users/RoleList.tsx";
import AdminActivities from "./pages/admin/Activities.tsx";
import AdminNews from "./pages/admin/News.tsx";
import AdminProjects from "./pages/admin/Projects.tsx";
import AdminContacts from "./pages/admin/Contacts.tsx";
import AdminLeads from "./pages/admin/Leads.tsx";
import AdminCustomers from "./pages/admin/Customers.tsx";
import AdminOpportunities from "./pages/admin/Opportunities.tsx";
import AdminQuotes from "./pages/admin/Quotes.tsx";
import AdminQuoteDetail from "./pages/admin/QuoteDetail.tsx";
import AdminRecruitment from "./pages/admin/Recruitment.tsx";
import EmploymentTypes from "./pages/admin/EmploymentTypes.tsx";
import SettingsCenter from "./pages/admin/SettingsCenter.tsx";
import JobPositionForm from "./pages/admin/JobPositionForm.tsx";
import EmailTemplateConfig from "./pages/admin/EmailTemplateConfig.tsx";
import ProjectForm from "./pages/admin/ProjectForm.tsx";
import ProjectView from "./pages/admin/ProjectView.tsx";
import ActivityForm from "./pages/admin/ActivityForm.tsx";
import ActivityView from "./pages/admin/ActivityView.tsx";
import NewsForm from "./pages/admin/NewsForm.tsx";
import NewsView from "./pages/admin/NewsView.tsx";
import AdminCategories from "./pages/admin/Categories.tsx";
import AdminActivityLog from "./pages/admin/ActivityLog.tsx";
import AdminServices from "./pages/admin/Services.tsx";
import AdminLogosManager from "./pages/admin/LogosManager.tsx";
import AboutContent from "./pages/admin/AboutContent.tsx";
import ProcessList from "./pages/admin/ProcessList.tsx";
import LanguagesPage from "./pages/admin/settings/Languages.tsx";
import TranslationsPage from "./pages/admin/settings/Translations.tsx";
import NotFound from "./pages/NotFound.tsx";

const queryClient = new QueryClient();

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
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.customers} />}>
                <Route path="/admin/customers" element={<AdminCustomers />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.opportunities} />}>
                <Route path="/admin/opportunities" element={<AdminOpportunities />} />
              </Route>
              <Route element={<RequirePermission code={ADMIN_PERMS.quotes} />}>
                <Route path="/admin/quotes" element={<AdminQuotes />} />
                <Route path="/admin/quotes/:id" element={<AdminQuoteDetail />} />
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
        </BrowserRouter>
      </TooltipProvider>
    </I18nProvider>
  </QueryClientProvider>
  </Provider>
);

export default App;
