import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Route, Routes } from "react-router-dom";
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
import AdminDashboard from "./pages/admin/Dashboard.tsx";
import AdminPosts from "./pages/admin/Posts.tsx";
import AdminProjects from "./pages/admin/Projects.tsx";
import AdminContacts from "./pages/admin/Contacts.tsx";
import AdminRecruitment from "./pages/admin/Recruitment.tsx";
import AdminSettings from "./pages/admin/Settings.tsx";
import ProjectForm from "./pages/admin/ProjectForm.tsx";
import ProjectView from "./pages/admin/ProjectView.tsx";
import PostForm from "./pages/admin/PostForm.tsx";
import PostView from "./pages/admin/PostView.tsx";
import AdminCategories from "./pages/admin/Categories.tsx";
import AdminCustomers from "./pages/admin/Customers.tsx";
import AdminCustomerRoles from "./pages/admin/CustomerRoles.tsx";
import AdminOnlineCustomers from "./pages/admin/OnlineCustomers.tsx";
import AdminActivityLog from "./pages/admin/ActivityLog.tsx";
import AdminLogosManager from "./pages/admin/LogosManager.tsx";
import AdminSimplePage from "./pages/admin/SimplePage.tsx";
import ProcessList from "./pages/admin/ProcessList.tsx";
import CustomerSettingsPage from "./pages/admin/settings/CustomerSettings.tsx";
import GeneralSettingsPage from "./pages/admin/settings/GeneralSettings.tsx";
import MediaSettingsPage from "./pages/admin/settings/MediaSettings.tsx";
import AllSettingsPage from "./pages/admin/settings/AllSettings.tsx";
import EmailAccountsPage from "./pages/admin/settings/EmailAccounts.tsx";
import StoresPage from "./pages/admin/settings/Stores.tsx";
import CountriesPage from "./pages/admin/settings/Countries.tsx";
import LanguagesPage from "./pages/admin/settings/Languages.tsx";
import SystemLog from "./pages/admin/system/SystemLog.tsx";
import WarningsPage from "./pages/admin/system/Warnings.tsx";
import MaintenancePage from "./pages/admin/system/Maintenance.tsx";
import MessageQueue from "./pages/admin/system/MessageQueue.tsx";
import ScheduleTasks from "./pages/admin/system/ScheduleTasks.tsx";
import SeNamesPage from "./pages/admin/system/SeNames.tsx";
import HelpPage from "./pages/admin/Help.tsx";
import {
  generalProcesses,
  ptcskhProcesses,
  dtProcesses,
  tkProcesses,
  tcProcesses,
  ttqtctProcesses,
  qlnsProcesses,
  mhdgncuProcesses,
} from "./data/processes.ts";
import NotFound from "./pages/NotFound.tsx";

const queryClient = new QueryClient();

const App = () => (
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
            <Route path="/projects/:id" element={<ProjectDetail />} />
            <Route path="/news" element={<News />} />
            <Route path="/news/:id" element={<NewsDetail />} />
            <Route path="/activities" element={<Activities />} />
            <Route path="/activities/:id" element={<ActivityDetail />} />
            <Route path="/clients" element={<Clients />} />
            <Route path="/recruitment" element={<Recruitment />} />
            <Route path="/contact" element={<Contact />} />
            <Route path="/login" element={<Login />} />
            <Route path="/register" element={<Register />} />
            <Route path="/admin" element={<AdminDashboard />} />
            <Route path="/admin/posts" element={<AdminPosts />} />
            <Route path="/admin/posts/new" element={<PostForm mode="create" />} />
            <Route path="/admin/posts/:id" element={<PostView />} />
            <Route path="/admin/posts/:id/edit" element={<PostForm mode="edit" />} />
            <Route path="/admin/projects" element={<AdminProjects />} />
            <Route path="/admin/projects/new" element={<ProjectForm mode="create" />} />
            <Route path="/admin/projects/:id" element={<ProjectView />} />
            <Route path="/admin/projects/:id/edit" element={<ProjectForm mode="edit" />} />
            <Route path="/admin/contacts" element={<AdminContacts />} />
            <Route path="/admin/recruitment" element={<AdminRecruitment />} />
            <Route path="/admin/settings" element={<AdminSettings />} />
            <Route path="/admin/settings/customer" element={<CustomerSettingsPage />} />
            <Route path="/admin/settings/general" element={<GeneralSettingsPage />} />
            <Route path="/admin/settings/media" element={<MediaSettingsPage />} />
            <Route path="/admin/settings/all" element={<AllSettingsPage />} />
            <Route path="/admin/email-accounts" element={<EmailAccountsPage />} />
            <Route path="/admin/stores" element={<StoresPage />} />
            <Route path="/admin/countries" element={<CountriesPage />} />
            <Route path="/admin/languages" element={<LanguagesPage />} />
            <Route path="/admin/categories" element={<AdminCategories />} />
            <Route path="/admin/customers" element={<AdminCustomers />} />
            <Route path="/admin/customer-roles" element={<AdminCustomerRoles />} />
            <Route path="/admin/online-customers" element={<AdminOnlineCustomers />} />
            <Route path="/admin/activity-log" element={<AdminActivityLog />} />
            <Route path="/admin/clients" element={<AdminLogosManager kind="clients" titleKey="nav.clients" />} />
            <Route path="/admin/partners" element={<AdminLogosManager kind="partners" titleKey="nav.partners" />} />
            <Route path="/admin/suppliers" element={<AdminLogosManager kind="suppliers" titleKey="nav.suppliers" />} />
            <Route path="/admin/awards" element={<AdminSimplePage titleKey="nav.awards" />} />
            <Route path="/admin/slideshow" element={<AdminSimplePage titleKey="nav.slideshow" />} />
            <Route path="/admin/map" element={<AdminSimplePage titleKey="nav.map" />} />
            <Route path="/admin/about" element={<AdminSimplePage titleKey="nav.about" />} />
            <Route path="/admin/help" element={<HelpPage />} />
            <Route path="/admin/system/log" element={<SystemLog />} />
            <Route path="/admin/system/warnings" element={<WarningsPage />} />
            <Route path="/admin/system/maintenance" element={<MaintenancePage />} />
            <Route path="/admin/system/queue" element={<MessageQueue />} />
            <Route path="/admin/system/tasks" element={<ScheduleTasks />} />
            <Route path="/admin/system/se-names" element={<SeNamesPage />} />
            <Route
              path="/admin/processes/general"
              element={<ProcessList storageKey="nicon_admin_proc_general_v1" titleKey="proc.general" seed={generalProcesses} />}
            />
            <Route
              path="/admin/processes/ptcskh"
              element={<ProcessList storageKey="nicon_admin_proc_ptcskh_v1" titleKey="proc.ptcskh" seed={ptcskhProcesses} />}
            />
            <Route
              path="/admin/processes/dt"
              element={<ProcessList storageKey="nicon_admin_proc_dt_v1" titleKey="proc.dt" seed={dtProcesses} />}
            />
            <Route
              path="/admin/processes/tk"
              element={<ProcessList storageKey="nicon_admin_proc_tk_v1" titleKey="proc.tk" seed={tkProcesses} />}
            />
            <Route
              path="/admin/processes/tc"
              element={<ProcessList storageKey="nicon_admin_proc_tc_v1" titleKey="proc.tc" seed={tcProcesses} />}
            />
            <Route
              path="/admin/processes/ttqtct"
              element={<ProcessList storageKey="nicon_admin_proc_ttqtct_v1" titleKey="proc.ttqtct" seed={ttqtctProcesses} />}
            />
            <Route
              path="/admin/processes/qlns"
              element={<ProcessList storageKey="nicon_admin_proc_qlns_v1" titleKey="proc.qlns" seed={qlnsProcesses} />}
            />
            <Route
              path="/admin/processes/mhdgncu"
              element={<ProcessList storageKey="nicon_admin_proc_mhdgncu_v1" titleKey="proc.mhdgncu" seed={mhdgncuProcesses} />}
            />
            {/* ADD ALL CUSTOM ROUTES ABOVE THE CATCH-ALL "*" ROUTE */}
            <Route path="*" element={<NotFound />} />
          </Routes>
        </BrowserRouter>
      </TooltipProvider>
    </I18nProvider>
  </QueryClientProvider>
);

export default App;
