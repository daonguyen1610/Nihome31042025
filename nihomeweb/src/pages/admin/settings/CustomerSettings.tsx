import { useState } from "react";
import { Save } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  SettingSection,
  SettingRow,
  TextInput,
  NumberInput,
  SelectInput,
  Toggle,
} from "@/components/admin/SettingsControls";
import {
  getCustomerSettings,
  saveCustomerSettings,
  type CustomerSettings,
} from "@/lib/settingsStore";

const CustomerSettingsPage = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [s, setS] = useState<CustomerSettings>(() => getCustomerSettings());
  const upd = <K extends keyof CustomerSettings>(k: K, v: CustomerSettings[K]) =>
    setS((prev) => ({ ...prev, [k]: v }));

  const save = () => {
    saveCustomerSettings(s);
    toast({ title: t("settings.saved") });
  };

  return (
    <AdminLayout>
      <div className="flex items-center justify-between mb-6">
        <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">
          {t("set.customer")}
        </h1>
        <button onClick={save} className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm">
          <Save className="w-4 h-4" /> {t("common.save")}
        </button>
      </div>

      <div className="space-y-5">
        <SettingSection title={t("set.section.common")}>
          <SettingRow label="Registration method">
            <SelectInput
              value={s.registrationMethod}
              onChange={(e) => upd("registrationMethod", e.target.value as CustomerSettings["registrationMethod"])}
              options={[
                { value: "Standard", label: "Standard" },
                { value: "EmailValidation", label: "Email validation" },
                { value: "AdminApproval", label: "Admin approval" },
                { value: "Disabled", label: "Disabled" },
              ]}
            />
          </SettingRow>
          <SettingRow label="Notify about new customer registration">
            <Toggle on={s.notifyNewRegistration} onChange={(v) => upd("notifyNewRegistration", v)} />
          </SettingRow>
          <SettingRow label="Require registration for downloadable products">
            <Toggle
              on={s.requireRegistrationForDownloadable}
              onChange={(v) => upd("requireRegistrationForDownloadable", v)}
            />
          </SettingRow>
        </SettingSection>

        <SettingSection title={t("set.section.password")}>
          <SettingRow label="Password minimum length">
            <NumberInput value={s.passwordMinLength} onChange={(e) => upd("passwordMinLength", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Unduplicated passwords number">
            <NumberInput value={s.unduplicatedPasswords} onChange={(e) => upd("unduplicatedPasswords", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Default password format">
            <SelectInput
              value={s.passwordFormat}
              onChange={(e) => upd("passwordFormat", e.target.value as CustomerSettings["passwordFormat"])}
              options={[
                { value: "Hashed", label: "Hashed" },
                { value: "Encrypted", label: "Encrypted" },
                { value: "Clear", label: "Clear" },
              ]}
            />
          </SettingRow>
          <SettingRow label="Password lifetime (days)">
            <NumberInput value={s.passwordLifetime} onChange={(e) => upd("passwordLifetime", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Password recovery link. Days valid">
            <NumberInput value={s.recoveryDaysValid} onChange={(e) => upd("recoveryDaysValid", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Maximum login failures">
            <NumberInput value={s.maxLoginFailures} onChange={(e) => upd("maxLoginFailures", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Lockout time (minutes)">
            <NumberInput value={s.lockoutMinutes} onChange={(e) => upd("lockoutMinutes", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Force entering email twice">
            <Toggle on={s.forceEmailTwice} onChange={(v) => upd("forceEmailTwice", v)} />
          </SettingRow>
        </SettingSection>

        <SettingSection title={t("set.section.account")}>
          <SettingRow label="'Usernames' enabled">
            <Toggle on={s.usernamesEnabled} onChange={(v) => upd("usernamesEnabled", v)} />
          </SettingRow>
          <SettingRow label="Customer name format">
            <SelectInput
              value={s.customerNameFormat}
              onChange={(e) => upd("customerNameFormat", e.target.value as CustomerSettings["customerNameFormat"])}
              options={[
                { value: "ShowEmails", label: "Show emails" },
                { value: "ShowFirstName", label: "Show first name" },
                { value: "ShowFullName", label: "Show full name" },
                { value: "ShowUsernames", label: "Show usernames" },
              ]}
            />
          </SettingRow>
          <SettingRow label="Allow customers to upload avatars">
            <Toggle on={s.allowAvatars} onChange={(v) => upd("allowAvatars", v)} />
          </SettingRow>
          <SettingRow label="Hide 'Downloadable products' tab">
            <Toggle on={s.hideDownloadable} onChange={(v) => upd("hideDownloadable", v)} />
          </SettingRow>
          <SettingRow label="Hide 'Back in stock subscriptions' tab">
            <Toggle on={s.hideBackInStock} onChange={(v) => upd("hideBackInStock", v)} />
          </SettingRow>
          <SettingRow label="Hide newsletter box">
            <Toggle on={s.hideNewsletter} onChange={(v) => upd("hideNewsletter", v)} />
          </SettingRow>
          <SettingRow label="Newsletter box. Allow to unsubscribe">
            <Toggle on={s.newsletterUnsubscribe} onChange={(v) => upd("newsletterUnsubscribe", v)} />
          </SettingRow>
          <SettingRow label="Store last visited page">
            <Toggle on={s.storeLastVisited} onChange={(v) => upd("storeLastVisited", v)} />
          </SettingRow>
        </SettingSection>

        <SettingSection title={t("set.section.profile")}>
          <SettingRow label="Allow viewing of customer profiles">
            <Toggle on={s.allowViewProfiles} onChange={(v) => upd("allowViewProfiles", v)} />
          </SettingRow>
          <SettingRow label="Show customers' location">
            <Toggle on={s.showLocation} onChange={(v) => upd("showLocation", v)} />
          </SettingRow>
          <SettingRow label="Show customers' join date">
            <Toggle on={s.showJoinDate} onChange={(v) => upd("showJoinDate", v)} />
          </SettingRow>
        </SettingSection>

        <SettingSection title={t("set.section.timezone")}>
          <SettingRow label="Allow customers to select time zone">
            <Toggle on={s.allowSelectTimezone} onChange={(v) => upd("allowSelectTimezone", v)} />
          </SettingRow>
          <SettingRow label="Default store time zone">
            <TextInput value={s.defaultTimezone} onChange={(e) => upd("defaultTimezone", e.target.value)} />
          </SettingRow>
        </SettingSection>

        <SettingSection title={t("set.section.external")}>
          <SettingRow label="External authentication. Auto register enabled">
            <Toggle on={s.externalAuthAutoRegister} onChange={(v) => upd("externalAuthAutoRegister", v)} />
          </SettingRow>
        </SettingSection>
      </div>
    </AdminLayout>
  );
};

export default CustomerSettingsPage;
