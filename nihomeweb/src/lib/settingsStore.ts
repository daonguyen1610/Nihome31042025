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

/* -------------------- Customer settings -------------------- */
export type CustomerSettings = {
  registrationMethod: "Standard" | "EmailValidation" | "AdminApproval" | "Disabled";
  notifyNewRegistration: boolean;
  requireRegistrationForDownloadable: boolean;
  passwordMinLength: number;
  unduplicatedPasswords: number;
  passwordFormat: "Hashed" | "Encrypted" | "Clear";
  passwordLifetime: number;
  recoveryDaysValid: number;
  maxLoginFailures: number;
  lockoutMinutes: number;
  forceEmailTwice: boolean;
  usernamesEnabled: boolean;
  customerNameFormat: "ShowEmails" | "ShowFirstName" | "ShowFullName" | "ShowUsernames";
  allowAvatars: boolean;
  hideDownloadable: boolean;
  hideBackInStock: boolean;
  hideNewsletter: boolean;
  newsletterUnsubscribe: boolean;
  storeLastVisited: boolean;
  allowViewProfiles: boolean;
  showLocation: boolean;
  showJoinDate: boolean;
  allowSelectTimezone: boolean;
  defaultTimezone: string;
  externalAuthAutoRegister: boolean;
};

const CUSTOMER_KEY = "nicon_admin_customer_settings_v1";
const customerDefaults: CustomerSettings = {
  registrationMethod: "Standard",
  notifyNewRegistration: false,
  requireRegistrationForDownloadable: false,
  passwordMinLength: 6,
  unduplicatedPasswords: 4,
  passwordFormat: "Hashed",
  passwordLifetime: 90,
  recoveryDaysValid: 7,
  maxLoginFailures: 0,
  lockoutMinutes: 30,
  forceEmailTwice: false,
  usernamesEnabled: false,
  customerNameFormat: "ShowFirstName",
  allowAvatars: false,
  hideDownloadable: false,
  hideBackInStock: false,
  hideNewsletter: false,
  newsletterUnsubscribe: false,
  storeLastVisited: false,
  allowViewProfiles: false,
  showLocation: false,
  showJoinDate: false,
  allowSelectTimezone: false,
  defaultTimezone: "(UTC+07:00) Bangkok, Hanoi, Jakarta",
  externalAuthAutoRegister: true,
};
export const getCustomerSettings = () => read(CUSTOMER_KEY, customerDefaults);
export const saveCustomerSettings = (v: CustomerSettings) => write(CUSTOMER_KEY, v);

/* -------------------- General settings -------------------- */
export type GeneralSettings = {
  facebook: string;
  twitter: string;
  youtube: string;
  googlePlus: string;
  sitemapEnabled: boolean;
  defaultPageTitle: string;
  pageTitleAdjustment: "PagenameAfterStorename" | "StorenameAfterPagename";
  pageTitleSeparator: string;
  defaultMetaKeywords: string;
  defaultMetaDescription: string;
  generateProductMeta: boolean;
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
  googlePlus: "https://plus.google.com/",
  sitemapEnabled: false,
  defaultPageTitle: "International General Constructor",
  pageTitleAdjustment: "PagenameAfterStorename",
  pageTitleSeparator: "International General Constructor",
  defaultMetaKeywords: "Nicon",
  defaultMetaDescription: "Nicon",
  generateProductMeta: true,
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
  importUsingHash: boolean;
  pictureZoom: boolean;
  productDetailSize: number;
  productThumbCatalog: number;
  productThumbProduct: number;
  associatedProductSize: number;
  categoryThumb: number;
  manufacturerThumb: number;
  vendorThumb: number;
  cartThumb: number;
  miniCartThumb: number;
  avatarSize: number;
};

const MEDIA_KEY = "nicon_admin_media_settings_v1";
const mediaDefaults: MediaSettings = {
  storage: "database",
  maxImageSize: 1980,
  multipleThumbDirs: false,
  defaultImageQuality: 80,
  importUsingHash: true,
  pictureZoom: false,
  productDetailSize: 550,
  productThumbCatalog: 415,
  productThumbProduct: 100,
  associatedProductSize: 220,
  categoryThumb: 450,
  manufacturerThumb: 420,
  vendorThumb: 450,
  cartThumb: 80,
  miniCartThumb: 70,
  avatarSize: 120,
};
export const getMediaSettings = () => read(MEDIA_KEY, mediaDefaults);
export const saveMediaSettings = (v: MediaSettings) => write(MEDIA_KEY, v);

/* -------------------- All settings (advanced key/value) -------------------- */
export type SettingRow = { id: string; name: string; value: string; store: string };
const ADVANCED_KEY = "nicon_admin_advanced_settings_v1";
const advancedSeed: SettingRow[] = [
  ["addresssettings.cityenabled", "True"],
  ["addresssettings.cityrequired", "True"],
  ["addresssettings.companyenabled", "True"],
  ["addresssettings.companyrequired", "False"],
  ["addresssettings.countryenabled", "True"],
  ["addresssettings.faxenabled", "True"],
  ["addresssettings.faxrequired", "False"],
  ["addresssettings.phoneenabled", "True"],
  ["addresssettings.phonerequired", "True"],
  ["addresssettings.stateprovinceenabled", "True"],
  ["addresssettings.streetaddress2enabled", "True"],
  ["addresssettings.streetaddress2required", "False"],
  ["addresssettings.streetaddressenabled", "True"],
  ["addresssettings.streetaddressrequired", "True"],
  ["addresssettings.zippostalcodeenabled", "True"],
  ["catalogsettings.allowanonymoususerstoreviewproduct", "True"],
  ["catalogsettings.defaultproductratingvalue", "5"],
  ["commonsettings.usefulldatabasecaching", "False"],
  ["customersettings.passwordminlength", "6"],
  ["mediasettings.productthumbpicturesize", "415"],
  ["seosettings.enablejsbundling", "True"],
  ["storeinformationsettings.storeclosed", "False"],
].map(([name, value], i) => ({
  id: `seed-${i}`,
  name: name as string,
  value: value as string,
  store: "All stores",
}));
export const getAllSettings = () => read<SettingRow[]>(ADVANCED_KEY, advancedSeed);
export const saveAllSettings = (rows: SettingRow[]) => write(ADVANCED_KEY, rows);

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
    displayName: "Store name",
    host: "smtp.mail.com",
    port: 587,
    username: "test@mail.com",
    enableSsl: true,
    isDefault: true,
  },
];
export const getEmailAccounts = () => read(EMAIL_KEY, emailSeed);
export const saveEmailAccounts = (rows: EmailAccount[]) => write(EMAIL_KEY, rows);

/* -------------------- Stores -------------------- */
export type StoreItem = {
  id: string;
  name: string;
  url: string;
  displayOrder: number;
  hosts: string;
};
const STORE_KEY = "nicon_admin_stores_v1";
const storeSeed: StoreItem[] = [
  { id: "st-1", name: "Nicon", url: "http://nicon.info/", displayOrder: 1, hosts: "nicon.info" },
];
export const getStores = () => read(STORE_KEY, storeSeed);
export const saveStores = (rows: StoreItem[]) => write(STORE_KEY, rows);

/* -------------------- Countries -------------------- */
export type Country = {
  id: string;
  name: string;
  allowsBilling: boolean;
  allowsShipping: boolean;
  twoLetterCode: string;
  threeLetterCode: string;
  numericCode: number;
  subjectToVat: boolean;
  numberOfStates: number;
  displayOrder: number;
  published: boolean;
};
const COUNTRY_KEY = "nicon_admin_countries_v1";
const countrySeed: Country[] = [
  ["United States", "US", "USA", 840, false, 62, 1],
  ["Viet Nam", "VN", "VNM", 704, false, 63, 2],
  ["Afghanistan", "AF", "AFG", 4, false, 0, 100],
  ["Albania", "AL", "ALB", 8, false, 0, 100],
  ["Algeria", "DZ", "DZA", 12, false, 0, 100],
  ["American Samoa", "AS", "ASM", 16, false, 0, 100],
  ["Andorra", "AD", "AND", 20, false, 0, 100],
  ["Angola", "AO", "AGO", 24, false, 0, 100],
  ["Anguilla", "AI", "AIA", 660, false, 0, 100],
  ["Antarctica", "AQ", "ATA", 10, false, 0, 100],
  ["Antigua and Barbuda", "AG", "ATG", 28, false, 0, 100],
  ["Argentina", "AR", "ARG", 32, false, 0, 100],
  ["Armenia", "AM", "ARM", 51, false, 0, 100],
  ["Aruba", "AW", "ABW", 533, false, 0, 100],
  ["Australia", "AU", "AUS", 36, false, 0, 100],
  ["Austria", "AT", "AUT", 40, true, 0, 100],
  ["Azerbaijan", "AZ", "AZE", 31, false, 0, 100],
].map(([name, two, three, num, vat, states, order], i) => ({
  id: `co-${i}`,
  name: name as string,
  allowsBilling: true,
  allowsShipping: true,
  twoLetterCode: two as string,
  threeLetterCode: three as string,
  numericCode: num as number,
  subjectToVat: vat as boolean,
  numberOfStates: states as number,
  displayOrder: order as number,
  published: true,
}));
export const getCountries = () => read(COUNTRY_KEY, countrySeed);
export const saveCountries = (rows: Country[]) => write(COUNTRY_KEY, rows);

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
