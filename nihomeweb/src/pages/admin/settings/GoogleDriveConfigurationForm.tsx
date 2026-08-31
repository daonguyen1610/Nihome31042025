import { useEffect, useState } from "react";
import { Save } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type GoogleDriveAdminConfigurationResponse,
  type GoogleDriveFolderConfiguration,
  type UpdateGoogleDriveConfigurationRequest,
} from "@/services/adminApi";

const FOLDER_FIELDS: Array<keyof GoogleDriveFolderConfiguration> = [
  "surveyMedia",
  "crmPreDesign",
  "designConcept",
  "designBasic",
  "designShopDrawing",
  "legalPermits",
  "constructionAcceptance",
  "procurement",
  "financeContracts",
];

const CLIENT_ID_PATTERN = /^[A-Za-z0-9._-]+\.apps\.googleusercontent\.com$/;
const DRIVE_ID_PATTERN = /^[A-Za-z0-9_-]{10,200}$/;
const INSTANCE_ID_PATTERN = /^[A-Za-z0-9._-]{3,100}$/;

function validate(
  form: GoogleDriveAdminConfigurationResponse,
  clientSecret: string,
): string | null {
  if (form.pollIntervalSeconds < 5 || form.pollIntervalSeconds > 300)
    return "settings.drive.validation.pollInterval";
  const instanceId = form.instanceId.trim();
  if (instanceId && !INSTANCE_ID_PATTERN.test(instanceId))
    return "settings.drive.validation.instanceId";
  if (form.applicationName.trim().length > 100)
    return "settings.drive.validation.applicationName";
  const paths = FOLDER_FIELDS.map((field) => form.folders[field].trim());
  const hasAnyPath = paths.some(Boolean);
  if (hasAnyPath && paths.some((path) => !path || path.length > 500 || path.startsWith("/") || path.endsWith("/") || path.includes("\\") ||
      path.split("/").some((segment) => !segment || segment === "." || segment === ".." || segment.length > 100)))
    return "settings.drive.validation.folderPath";
  if (hasAnyPath && new Set(paths.map((path) => path.toLowerCase())).size !== paths.length)
    return "settings.drive.validation.folderDuplicate";
  if (!form.enabled) return null;
  if (!CLIENT_ID_PATTERN.test(form.clientId.trim()))
    return "settings.drive.validation.clientId";
  if (!form.hasClientSecret && clientSecret.trim().length < 8)
    return "settings.drive.validation.clientSecret";
  if (clientSecret.trim() && (clientSecret.trim().length < 8 || clientSecret.trim().length > 512))
    return "settings.drive.validation.clientSecret";
  try {
    const redirect = new URL(form.oAuthRedirectUri.trim());
    const loopbackHttp = redirect.protocol === "http:" && ["localhost", "127.0.0.1", "::1"].includes(redirect.hostname);
    if ((redirect.protocol !== "https:" && !loopbackHttp) || redirect.hash || redirect.username || redirect.password ||
        !redirect.pathname.endsWith("/api/site-settings/google-drive/oauth/callback"))
      return "settings.drive.validation.redirectUri";
  } catch {
    return "settings.drive.validation.redirectUri";
  }
  const returnUrl = form.frontendReturnUrl.trim();
  if (!returnUrl.startsWith("/admin/") || returnUrl.startsWith("//") || returnUrl.includes("\\") || returnUrl.includes("#"))
    return "settings.drive.validation.returnUrl";
  if (!DRIVE_ID_PATTERN.test(form.rootFolderId.trim()))
    return "settings.drive.validation.rootFolderId";
  if (!INSTANCE_ID_PATTERN.test(instanceId))
    return "settings.drive.validation.instanceId";
  if (!form.applicationName.trim() || form.applicationName.trim().length > 100)
    return "settings.drive.validation.applicationName";
  if (paths.some((path) => !path || path.length > 500 || path.startsWith("/") || path.endsWith("/") || path.includes("\\") ||
      path.split("/").some((segment) => !segment || segment === "." || segment === ".." || segment.length > 100)))
    return "settings.drive.validation.folderPath";
  if (new Set(paths.map((path) => path.toLowerCase())).size !== paths.length)
    return "settings.drive.validation.folderDuplicate";
  return null;
}

type Props = {
  canManage: boolean;
  reloadKey: number;
  onSaved: () => void;
};

export default function GoogleDriveConfigurationForm({ canManage, reloadKey, onSaved }: Props) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [form, setForm] = useState<GoogleDriveAdminConfigurationResponse | null>(null);
  const [clientSecret, setClientSecret] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    adminApi.getGoogleDriveConfiguration()
      .then(({ data }) => {
        if (!cancelled) {
          setForm(data);
          setClientSecret("");
        }
      })
      .catch(() => {
        if (!cancelled) {
          setForm(null);
          toast({
            title: t("common.error"),
            description: t("settings.drive.configuration.loadError"),
            variant: "destructive",
          });
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [reloadKey, t, toast]);

  const update = <Key extends keyof GoogleDriveAdminConfigurationResponse>(
    key: Key,
    value: GoogleDriveAdminConfigurationResponse[Key],
  ) => setForm((current) => current ? { ...current, [key]: value } : current);

  const updateFolder = (key: keyof GoogleDriveFolderConfiguration, value: string) =>
    setForm((current) => current ? {
      ...current,
      folders: { ...current.folders, [key]: value },
    } : current);

  const save = async () => {
    if (!form) return;
    const validationKey = validate(form, clientSecret);
    if (validationKey) {
      toast({
        title: t("common.error"),
        description: t(validationKey),
        variant: "destructive",
      });
      return;
    }
    const payload: UpdateGoogleDriveConfigurationRequest = {
      enabled: form.enabled,
      clientId: form.clientId.trim(),
      oAuthRedirectUri: form.oAuthRedirectUri.trim(),
      frontendReturnUrl: form.frontendReturnUrl.trim(),
      rootFolderId: form.rootFolderId.trim(),
      instanceId: form.instanceId.trim(),
      applicationName: form.applicationName.trim(),
      folders: Object.fromEntries(
        FOLDER_FIELDS.map((field) => [field, form.folders[field].trim()]),
      ) as unknown as GoogleDriveFolderConfiguration,
      supportsAllDrives: form.supportsAllDrives,
      pollIntervalSeconds: Number(form.pollIntervalSeconds),
      rowVersion: form.rowVersion,
    };
    if (clientSecret.trim()) payload.clientSecret = clientSecret.trim();

    setSaving(true);
    try {
      const { data } = await adminApi.updateGoogleDriveConfiguration(payload);
      setForm(data);
      setClientSecret("");
      toast({ title: t("settings.drive.configuration.saved") });
      onSaved();
    } catch (error) {
      toast({
        title: t("common.error"),
        description: extractApiError(error) || t("settings.drive.configuration.saveError"),
        variant: "destructive",
      });
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <p className="text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (!form) return <p className="text-sm text-destructive">{t("settings.drive.configuration.loadError")}</p>;

  const disabled = !canManage || saving;
  return (
    <section className="space-y-6 rounded-lg border bg-card p-4 sm:p-6">
      <div className="flex flex-col gap-4 border-b pb-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h2 className="text-lg font-semibold">{t("settings.drive.configuration.title")}</h2>
          <p className="mt-1 text-sm text-muted-foreground">{t("settings.drive.configuration.description")}</p>
        </div>
        <div className="flex items-center gap-3">
          <Label htmlFor="drive-enabled">{t("settings.drive.configuration.enabled")}</Label>
          <Switch id="drive-enabled" checked={form.enabled} onCheckedChange={(value) => update("enabled", value)} disabled={disabled} />
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Field id="drive-client-id" label={t("settings.drive.configuration.clientId")} value={form.clientId} disabled={disabled} onChange={(value) => update("clientId", value)} />
        <div className="space-y-1.5">
          <Label htmlFor="drive-client-secret">{t("settings.drive.configuration.clientSecret")}</Label>
          <Input id="drive-client-secret" type="password" value={clientSecret} disabled={disabled} onChange={(event) => setClientSecret(event.target.value)} autoComplete="new-password" />
          <p className="text-xs text-muted-foreground">
            {t(form.hasClientSecret ? "settings.drive.configuration.secretStored" : "settings.drive.configuration.secretMissing")}
          </p>
        </div>
        <Field id="drive-redirect" label={t("settings.drive.configuration.redirectUri")} value={form.oAuthRedirectUri} disabled={disabled} onChange={(value) => update("oAuthRedirectUri", value)} />
        <Field id="drive-return" label={t("settings.drive.configuration.returnUrl")} value={form.frontendReturnUrl} disabled={disabled} onChange={(value) => update("frontendReturnUrl", value)} />
        <Field id="drive-root" label={t("settings.drive.configuration.rootFolderId")} value={form.rootFolderId} disabled={disabled} onChange={(value) => update("rootFolderId", value)} />
        <Field id="drive-instance" label={t("settings.drive.configuration.instanceId")} value={form.instanceId} disabled={disabled} onChange={(value) => update("instanceId", value)} />
        <Field id="drive-app-name" label={t("settings.drive.configuration.applicationName")} value={form.applicationName} disabled={disabled} onChange={(value) => update("applicationName", value)} />
        <div className="space-y-1.5">
          <Label htmlFor="drive-poll">{t("settings.drive.configuration.pollInterval")}</Label>
          <Input id="drive-poll" type="number" min={5} max={300} value={form.pollIntervalSeconds} disabled={disabled} onChange={(event) => update("pollIntervalSeconds", Number(event.target.value))} />
        </div>
      </div>

      <div>
        <h3 className="text-sm font-semibold">{t("settings.drive.configuration.folders")}</h3>
        <div className="mt-3 grid gap-4 md:grid-cols-2">
          {FOLDER_FIELDS.map((field) => (
            <Field key={field} id={`drive-folder-${field}`} label={t(`settings.drive.configuration.folder.${field}`)} value={form.folders[field]} disabled={disabled} onChange={(value) => updateFolder(field, value)} />
          ))}
        </div>
      </div>

      <div className="flex flex-col gap-4 border-t pt-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3">
          <Switch id="drive-all-drives" checked={form.supportsAllDrives} onCheckedChange={(value) => update("supportsAllDrives", value)} disabled={disabled} />
          <Label htmlFor="drive-all-drives">{t("settings.drive.configuration.supportsAllDrives")}</Label>
        </div>
        {canManage && (
          <Button type="button" onClick={save} disabled={saving}>
            <Save className="mr-1.5 h-4 w-4" />
            {saving ? t("common.saving") : t("settings.drive.configuration.save")}
          </Button>
        )}
      </div>
    </section>
  );
}

function Field({ id, label, value, disabled, onChange }: {
  id: string;
  label: string;
  value: string;
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input id={id} value={value} disabled={disabled} onChange={(event) => onChange(event.target.value)} />
    </div>
  );
}
