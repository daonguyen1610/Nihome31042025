import { createContext, ReactNode, useContext, useEffect, useMemo, useState } from "react";
import api from "@/lib/api";
import { setDisplayLanguage } from "@/lib/displaySettings";

export type Lang = "vi" | "en" | "zh" | "ja";
export const TRANSLATIONS_CHANGED_EVENT = "nicon:translations-changed";

type TranslationMap = Record<string, string>;

// --- Backend error message -> i18n key mapping ---
const backendErrorMap: Record<string, string> = {
  "Invalid credentials.": "auth.err.invalidCredentials",
  "Enter a valid phone number (example: 0987654321) or email (example: user@example.com).":
    "auth.err.invalidLoginIdentifier",
  "Account is inactive.": "auth.err.accountInactive",
  "Phone number already registered.": "auth.err.phoneRegistered",
  "Email already registered.": "auth.err.emailRegistered",
  "Email is required.": "auth.err.emailRequired",
  "Invalid OTP.": "auth.err.invalidOtp",
  "OTP session not found or expired.": "auth.err.otpExpired",
  "OTP request limit exceeded.": "auth.err.otpLimitExceeded",
  "Please wait before requesting a new OTP.": "auth.err.otpWait",
  "Account not found.": "auth.err.accountNotFound",
  "Refresh token is invalid.": "auth.err.refreshTokenInvalid",
  "User not found.": "auth.err.userNotFound",
  "OTP verification is disabled for registration.": "auth.err.otpDisabledRegister",
  "OTP verification is disabled for forgot password.": "auth.err.otpDisabledForgot",
  "OTP verification is required. Please use the standard forgot password flow.":
    "auth.err.otpRequiredForgot",
  "OTP verification is disabled. Complete registration from register/start.":
    "auth.err.otpDisabledComplete",
  "Network Error": "auth.err.networkError",
  "An unexpected error occurred": "auth.err.unexpected",
  "Người phụ trách phải là thành viên đang hiệu lực trong đội ngũ Hợp đồng Thiết kế.":
    "designProjects.team.validation.ownerActive",
  "Hợp đồng Thiết kế đã có Chủ nhiệm Thiết kế. Hãy thay đổi phân công hiện có trước.":
    "designProjects.team.validation.managerAlreadyAssigned",
};

/** Translate a backend error message using the current i18n translate function. */
export const translateError = (t: (key: string) => string, message: string): string => {
  const key = backendErrorMap[message];
  return key ? t(key) : message;
};

type Ctx = {
  lang: Lang;
  setLang: (l: Lang) => void;
  t: (key: string, params?: Record<string, string | number>) => string;
  /**
   * The same text in the source language (Vietnamese), for defaults a form saves as
   * content: forms collect only source-language content, whatever the UI language.
   */
  tSource: (key: string, params?: Record<string, string | number>) => string;
};

const I18nContext = createContext<Ctx | null>(null);

const LANGS: Lang[] = ["vi", "en", "zh", "ja"];

// The last loaded text, so a reload does not show raw keys while translations arrive.
// shortcut: a first-ever visit still shows keys briefly; gate rendering if that matters.
const cacheKey = (lang: Lang) => `nicon_i18n_${lang}`;
const readCache = (lang: Lang): TranslationMap => {
  try {
    return JSON.parse(localStorage.getItem(cacheKey(lang)) ?? "{}") as TranslationMap;
  } catch {
    return {};
  }
};
const writeCache = (lang: Lang, map: TranslationMap) => {
  try {
    localStorage.setItem(cacheKey(lang), JSON.stringify(map));
  } catch {
    // Storage full or blocked: the next load simply fetches again.
  }
};

export const I18nProvider = ({ children }: { children: ReactNode }) => {
  const [lang, setLangState] = useState<Lang>(() => {
    const saved =
      typeof window !== "undefined" ? (localStorage.getItem("nicon_lang") as Lang) : null;
    const initial = saved && LANGS.includes(saved) ? saved : "vi";
    // Requests read the language from display settings: set it before any child fetches.
    setDisplayLanguage(initial);
    return initial;
  });
  const [fallbackViMap, setFallbackViMap] = useState<TranslationMap>(() => readCache("vi"));
  const [currentMap, setCurrentMap] = useState<TranslationMap>(() => readCache(lang));
  const [translationRevision, setTranslationRevision] = useState(0);

  useEffect(() => {
    const reload = () => setTranslationRevision((revision) => revision + 1);
    window.addEventListener(TRANSLATIONS_CHANGED_EVENT, reload);
    return () => window.removeEventListener(TRANSLATIONS_CHANGED_EVENT, reload);
  }, []);

  useEffect(() => {
    localStorage.setItem("nicon_lang", lang);
    document.documentElement.lang = lang;
    setDisplayLanguage(lang);
  }, [lang]);

  useEffect(() => {
    let canceled = false;

    const loadTranslations = async () => {
      try {
        const viReq = api.get<{ languageCode: string; translations: TranslationMap }>(
          "/translations/vi",
        );
        const langReq =
          lang === "vi"
            ? null
            : api.get<{ languageCode: string; translations: TranslationMap }>(
                `/translations/${lang}`,
              );

        const [viRes, langRes] = await Promise.all([viReq, langReq ?? Promise.resolve(null)]);

        if (canceled) return;

        const viMap = viRes.data.translations ?? {};
        const selectedMap = langRes?.data.translations ?? viMap;

        setFallbackViMap(viMap);
        setCurrentMap(selectedMap);
        writeCache("vi", viMap);
        writeCache(lang, selectedMap);
      } catch {
        if (canceled) return;
        setFallbackViMap(readCache("vi"));
        setCurrentMap(readCache(lang));
      }
    };

    void loadTranslations();

    return () => {
      canceled = true;
    };
  }, [lang, translationRevision]);

  const setLang = (l: Lang) => {
    // Before the re-render, so fetches triggered by the new language send it.
    setDisplayLanguage(l);
    setLangState(l);
  };

  const [t, tSource] = useMemo(() => {
    const format = (text: string, params?: Record<string, string | number>) => {
      if (params) {
        for (const [k, v] of Object.entries(params)) {
          text = text.replace(new RegExp(`\\{${k}\\}`, "g"), String(v));
        }
      }
      return text;
    };
    return [
      (key: string, params?: Record<string, string | number>) =>
        format(currentMap[key] ?? fallbackViMap[key] ?? key, params),
      (key: string, params?: Record<string, string | number>) =>
        format(fallbackViMap[key] ?? key, params),
    ] as const;
  }, [currentMap, fallbackViMap]);

  return (
    <I18nContext.Provider value={{ lang, setLang, t, tSource }}>{children}</I18nContext.Provider>
  );
};

export const useI18n = () => {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error("useI18n must be used within I18nProvider");
  return ctx;
};
