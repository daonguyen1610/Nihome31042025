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
      <div className="flex items-center justify-between mb-6">
        <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">
          {t("set.media")}
        </h1>
        <button onClick={save} className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm">
          <Save className="w-4 h-4" /> {t("common.save")}
        </button>
      </div>

      <div className="space-y-5">
        <SettingSection title={t("set.section.common")}>
          <SettingRow label="Pictures are stored into…">
            <div className="flex items-center gap-3">
              <span className="text-sm font-semibold">{s.storage}</span>
              <button
                type="button"
                onClick={() => upd("storage", s.storage === "database" ? "filesystem" : "database")}
                className="px-3 py-2 rounded-lg text-xs font-bold text-white"
                style={{ background: "hsl(var(--admin-primary))" }}
              >
                Change
              </button>
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
          <SettingRow label="Import product images using hash">
            <Toggle on={s.importUsingHash} onChange={(v) => upd("importUsingHash", v)} />
          </SettingRow>
          <SettingRow label="Picture zoom">
            <Toggle on={s.pictureZoom} onChange={(v) => upd("pictureZoom", v)} />
          </SettingRow>
        </SettingSection>

        <SettingSection title={t("set.section.product")}>
          <SettingRow label="Product detail image size">
            <NumberInput value={s.productDetailSize} onChange={(e) => upd("productDetailSize", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Product thumbnail image size (catalog)">
            <NumberInput value={s.productThumbCatalog} onChange={(e) => upd("productThumbCatalog", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Product thumbnail image size (product page)">
            <NumberInput value={s.productThumbProduct} onChange={(e) => upd("productThumbProduct", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Associated product image size">
            <NumberInput value={s.associatedProductSize} onChange={(e) => upd("associatedProductSize", +e.target.value)} />
          </SettingRow>
        </SettingSection>

        <SettingSection title={t("set.section.otherPages")}>
          <SettingRow label="Category thumbnail image size">
            <NumberInput value={s.categoryThumb} onChange={(e) => upd("categoryThumb", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Manufacturer thumbnail image size">
            <NumberInput value={s.manufacturerThumb} onChange={(e) => upd("manufacturerThumb", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Vendor thumbnail image size">
            <NumberInput value={s.vendorThumb} onChange={(e) => upd("vendorThumb", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Cart/Wishlist thumbnail image size">
            <NumberInput value={s.cartThumb} onChange={(e) => upd("cartThumb", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Mini-shopping cart thumbnail image size">
            <NumberInput value={s.miniCartThumb} onChange={(e) => upd("miniCartThumb", +e.target.value)} />
          </SettingRow>
          <SettingRow label="Avatar image size">
            <NumberInput value={s.avatarSize} onChange={(e) => upd("avatarSize", +e.target.value)} />
          </SettingRow>
        </SettingSection>
      </div>
    </AdminLayout>
  );
};

export default MediaSettingsPage;
