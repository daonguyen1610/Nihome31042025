// Lightweight localStorage-backed store for the admin "Cấu hình" (Configuration) section.
// Mimics nopCommerce-like settings while keeping data fully client-side.

const read = <T,>(key: string, fallback: T): T => {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : fallback;
  } catch {
    return fallback;
  }
};

const write = <T,>(key: string, value: T) => {
  try {
    localStorage.setItem(key, JSON.stringify(value));
    window.dispatchEvent(new CustomEvent(`${key}:changed`));
  } catch {
    /* ignore */
  }
}

/* -------------------- General settings -------------------- */
export type GeneralSettings = {
  facebook: string;
  twitter: string;
  youtube: string;
  sitemapEnabled: boolean;
  defaultPageTitle: string;
  pageTitleSeparator: string;
  defaultMetaKeywords: string;
  defaultMetaDescription: string;
  jsBundling: boolean;
  cssBundling: boolean;
  wwwPrefix: "DoesntMatter" | "WithoutWww" | "WithWww";
  convertNonWestern: boolean;
  enableCanonical: boolean;
  twitterMeta: boolean;
  openGraphMeta: boolean;
  customHead: string;
  adminAllowedIp: string;
  forceSsl: boolean;
  xsrfAdmin: boolean;
  xsrfPublic: boolean;
  honeypot: boolean;
  encryptionKey: string;
  captchaEnabled: boolean;
};

const GENERAL_KEY = "nicon_admin_general_settings_v1";
const generalDefaults: GeneralSettings = {
  facebook: "https://www.facebook.com/niconvn",
  twitter: "https://www.nicon.info/",
  youtube: "https://www.youtube.com/",
  sitemapEnabled: false,
  defaultPageTitle: "International General Constructor",
  pageTitleSeparator: "International General Constructor",
  defaultMetaKeywords: "Nicon",
  defaultMetaDescription: "Nicon",
  jsBundling: true,
  cssBundling: true,
  wwwPrefix: "DoesntMatter",
  convertNonWestern: true,
  enableCanonical: true,
  twitterMeta: true,
  openGraphMeta: true,
  customHead: "",
  adminAllowedIp: "",
  forceSsl: true,
  xsrfAdmin: false,
  xsrfPublic: false,
  honeypot: false,
  encryptionKey: "1514784036695878",
  captchaEnabled: false,
};
export const getGeneralSettings = () => read(GENERAL_KEY, generalDefaults);
export const saveGeneralSettings = (v: GeneralSettings) => write(GENERAL_KEY, v);

/* -------------------- Media settings -------------------- */
export type MediaSettings = {
  storage: "database" | "filesystem";
  maxImageSize: number;
  multipleThumbDirs: boolean;
  defaultImageQuality: number;
  pictureZoom: boolean;
  projectThumbSize: number;
  postThumbSize: number;
  categoryThumb: number;
  avatarSize: number;
};

const MEDIA_KEY = "nicon_admin_media_settings_v1";
const mediaDefaults: MediaSettings = {
  storage: "database",
  maxImageSize: 1980,
  multipleThumbDirs: false,
  defaultImageQuality: 80,
  pictureZoom: false,
  projectThumbSize: 550,
  postThumbSize: 415,
  categoryThumb: 450,
  avatarSize: 120,
};
export const getMediaSettings = () => read(MEDIA_KEY, mediaDefaults);
export const saveMediaSettings = (v: MediaSettings) => write(MEDIA_KEY, v);

/* -------------------- Email accounts -------------------- */
export type EmailAccount = {
  id: string;
  email: string;
  displayName: string;
  host: string;
  port: number;
  username: string;
  enableSsl: boolean;
  isDefault: boolean;
};
const EMAIL_KEY = "nicon_admin_email_accounts_v1";
const emailSeed: EmailAccount[] = [
  {
    id: "em-1",
    email: "test@mail.com",
    displayName: "Nicon",
    host: "smtp.mail.com",
    port: 587,
    username: "test@mail.com",
    enableSsl: true,
    isDefault: true,
  },
];
export const getEmailAccounts = () => read(EMAIL_KEY, emailSeed);
export const saveEmailAccounts = (rows: EmailAccount[]) => write(EMAIL_KEY, rows);

/* -------------------- Languages -------------------- */
export type Language = {
  id: string;
  name: string;
  flag: string; // emoji or short code
  culture: string;
  displayOrder: number;
  published: boolean;
};
const LANG_KEY = "nicon_admin_languages_v1";
const langSeed: Language[] = [
  { id: "lg-1", name: "English", flag: "🇺🇸", culture: "en-US", displayOrder: 0, published: true },
  { id: "lg-2", name: "Viet Nam", flag: "🇻🇳", culture: "vi-VN", displayOrder: 1, published: true },
  { id: "lg-3", name: "China", flag: "🇨🇳", culture: "zh-CN", displayOrder: 2, published: true },
  { id: "lg-4", name: "Korea", flag: "🇰🇷", culture: "ko-KR", displayOrder: 3, published: true },
  { id: "lg-5", name: "Japan", flag: "🇯🇵", culture: "ja-JP", displayOrder: 4, published: true },
];
export const getLanguages = () => read(LANG_KEY, langSeed);
export const saveLanguages = (rows: Language[]) => write(LANG_KEY, rows);

/* -------------------- Helpers -------------------- */
export const newId = () => `id-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
