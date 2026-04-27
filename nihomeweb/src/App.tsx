import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { Provider } from "react-redux";
import { store } from "@/store";
import { Toaster as Sonner } from "@/components/ui/sonner";
import { Toaster } from "@/components/ui/toaster";
import { TooltipProvider } from "@/components/ui/tooltip";
import { I18nProvider } from "@/lib/i18n";
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
import AdminDashboard from "./pages/admin/Dashboard.tsx";
import AdminPosts from "./pages/admin/Posts.tsx";
import AdminProjects from "./pages/admin/Projects.tsx";
import AdminContacts from "./pages/admin/Contacts.tsx";
import AdminRecruitment from "./pages/admin/Recruitment.tsx";
import EmploymentTypes from "./pages/admin/EmploymentTypes.tsx";
import SettingsCenter from "./pages/admin/SettingsCenter.tsx";
import JobPositionForm from "./pages/admin/JobPositionForm.tsx";
import EmailTemplateConfig from "./pages/admin/EmailTemplateConfig.tsx";
import ProjectForm from "./pages/admin/ProjectForm.tsx";
import ProjectView from "./pages/admin/ProjectView.tsx";
import PostForm from "./pages/admin/PostForm.tsx";
import PostView from "./pages/admin/PostView.tsx";
import AdminCategories from "./pages/admin/Categories.tsx";
import AdminActivityLog from "./pages/admin/ActivityLog.tsx";
import AdminLogosManager from "./pages/admin/LogosManager.tsx";
import AdminSimplePage from "./pages/admin/SimplePage.tsx";
import AboutContent from "./pages/admin/AboutContent.tsx";
import ProcessList from "./pages/admin/ProcessList.tsx";
import LanguagesPage from "./pages/admin/settings/Languages.tsx";
import TranslationsPage from "./pages/admin/settings/Translations.tsx";
import SystemLog from "./pages/admin/system/SystemLog.tsx";
import WarningsPage from "./pages/admin/system/Warnings.tsx";
import MaintenancePage from "./pages/admin/system/Maintenance.tsx";
import HelpPage from "./pages/admin/Help.tsx";
import NotFound from "./pages/NotFound.tsx";

const queryClient = new QueryClient();

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
            <Route path="/admin" element={<AdminDashboard />} />
            <Route path="/admin/posts" element={<AdminPosts />} />
            <Route path="/admin/posts/new" element={<PostForm mode="create" />} />
            <Route path="/admin/posts/:slug" element={<PostView />} />
            <Route path="/admin/posts/:slug/edit" element={<PostForm mode="edit" />} />
            <Route path="/admin/projects" element={<AdminProjects />} />
            <Route path="/admin/projects/new" element={<ProjectForm mode="create" />} />
            <Route path="/admin/projects/:slug" element={<ProjectView />} />
            <Route path="/admin/projects/:slug/edit" element={<ProjectForm mode="edit" />} />
            <Route path="/admin/contacts" element={<AdminContacts />} />
            <Route path="/admin/recruitment" element={<AdminRecruitment />} />
            <Route path="/admin/recruitment/employment-types" element={<EmploymentTypes />} />
            <Route path="/admin/recruitment/new" element={<JobPositionForm mode="create" />} />
            <Route path="/admin/recruitment/:id/edit" element={<JobPositionForm mode="edit" />} />
            <Route path="/admin/email-templates" element={<EmailTemplateConfig />} />
            <Route path="/admin/settings" element={<SettingsCenter />} />
            <Route path="/admin/languages" element={<LanguagesPage />} />
            <Route path="/admin/translations" element={<TranslationsPage />} />
            <Route path="/admin/categories" element={<AdminCategories />} />
            <Route path="/admin/activity-log" element={<AdminActivityLog />} />
            <Route path="/admin/clients" element={<AdminLogosManager kind="clients" titleKey="nav.clients" />} />
            <Route path="/admin/partners" element={<AdminLogosManager kind="partners" titleKey="nav.partners" />} />
            <Route path="/admin/suppliers" element={<AdminLogosManager kind="suppliers" titleKey="nav.suppliers" />} />
            <Route path="/admin/awards" element={<AdminLogosManager kind="awards" titleKey="nav.awards" />} />
            <Route path="/admin/slideshow" element={<Navigate to="/admin/settings?tab=slideshow" replace />} />
            <Route path="/admin/map" element={<AdminSimplePage titleKey="nav.map" />} />
            <Route path="/admin/about" element={<AboutContent />} />
            <Route path="/admin/help" element={<HelpPage />} />
            <Route path="/admin/system/log" element={<SystemLog />} />
            <Route path="/admin/system/warnings" element={<WarningsPage />} />
            <Route path="/admin/system/maintenance" element={<MaintenancePage />} />
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
