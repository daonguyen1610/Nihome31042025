import { useState } from "react";
import { Save } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  SettingSection,
  SettingRow,
  NumberInput,
  Toggle,
} from "@/components/admin/SettingsControls";
import {
  getMediaSettings,
  saveMediaSettings,
  type MediaSettings,
} from "@/lib/settingsStore";
import { Button } from "@/components/ui/button";

const MediaSettingsPage = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [s, setS] = useState<MediaSettings>(() => getMediaSettings());
  const upd = <K extends keyof MediaSettings>(k: K, v: MediaSettings[K]) =>
    setS((prev) => ({ ...prev, [k]: v }));

  const save = () => {
    saveMediaSettings(s);
    toast({ title: t("settings.saved") });
  };

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-wrap items-center justify-between gap-3">
          <h1 className="text-2xl font-semibold">{t("set.media")}</h1>
          <Button onClick={save}>
            <Save className="mr-1.5 h-4 w-4" /> {t("common.save")}
          </Button>
        </header>

        <div className="space-y-4">
        <SettingSection title={t("set.section.common")}>
          <SettingRow label="Pictures are stored into…">
            <div className="flex items-center gap-3">
              <span className="text-sm font-medium">{s.storage}</span>
              <Button
                type="button"
                size="sm"
                onClick={() => upd("storage", s.storage === "database" ? "filesystem" : "database")}
              >
                {t("common.change")}
              </Button>
            </div>
          </SettingRow>
          <SettingRow label="Maximum image size">
            <NumberInput value={s.maxImageSize} onChange={(e) => upd("maxImageSize", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Multiple thumb directories">
            <Toggle on={s.multipleThumbDirs} onChange={(v) => upd("multipleThumbDirs", v)} />
          </SettingRow>
          <SettingRow label="Default image quality (0 - 100)">
            <NumberInput value={s.defaultImageQuality} onChange={(e) => upd("defaultImageQuality", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Picture zoom">
            <Toggle on={s.pictureZoom} onChange={(v) => upd("pictureZoom", v)} />
          </SettingRow>
        </SettingSection>

        <SettingSection title={t("set.section.thumbnails")}>
          <SettingRow label="Project image size">
            <NumberInput value={s.projectThumbSize} onChange={(e) => upd("projectThumbSize", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Post thumbnail size">
            <NumberInput value={s.postThumbSize} onChange={(e) => upd("postThumbSize", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Category thumbnail size">
            <NumberInput value={s.categoryThumb} onChange={(e) => upd("categoryThumb", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Avatar image size">
            <NumberInput value={s.avatarSize} onChange={(e) => upd("avatarSize", +e.target.value)} />
          </SettingRow>
        </SettingSection>
        </div>
      </div>
    </AdminLayout>
  );
};

export default MediaSettingsPage;
