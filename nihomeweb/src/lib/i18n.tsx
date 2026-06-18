import { createContext, ReactNode, useContext, useEffect, useMemo, useState } from "react";
import api from "@/lib/api";

export type Lang = "vi" | "en" | "zh" | "ja";

type TranslationMap = Record<string, string>;

// --- Backend error message -> i18n key mapping ---
const backendErrorMap: Record<string, string> = {
  "Invalid credentials.": "auth.err.invalidCredentials",
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
  "OTP verification is required. Please use the standard forgot password flow.": "auth.err.otpRequiredForgot",
  "OTP verification is disabled. Complete registration from register/start.": "auth.err.otpDisabledComplete",
  "Network Error": "auth.err.networkError",
  "An unexpected error occurred": "auth.err.unexpected",
};

/** Translate a backend error message using the current i18n translate function. */
export const translateError = (t: (key: string) => string, message: string): string => {
  const key = backendErrorMap[message];
  return key ? t(key) : message;
};

type Ctx = {
  lang: Lang;
  setLang: (l: Lang) => void;
  t: (key: string) => string;
};

const I18nContext = createContext<Ctx | null>(null);

const LANGS: Lang[] = ["vi", "en", "zh", "ja"];

export const I18nProvider = ({ children }: { children: ReactNode }) => {
  const [lang, setLangState] = useState<Lang>(() => {
    const saved = typeof window !== "undefined" ? (localStorage.getItem("nicon_lang") as Lang) : null;
    return saved && LANGS.includes(saved) ? saved : "vi";
  });
  const [fallbackViMap, setFallbackViMap] = useState<TranslationMap>({});
  const [currentMap, setCurrentMap] = useState<TranslationMap>({});

  useEffect(() => {
    localStorage.setItem("nicon_lang", lang);
    document.documentElement.lang = lang;
  }, [lang]);

  useEffect(() => {
    let canceled = false;

    const loadTranslations = async () => {
      try {
        const viReq = api.get<{ languageCode: string; translations: TranslationMap }>("/translations/vi");
        const langReq = lang === "vi"
          ? null
          : api.get<{ languageCode: string; translations: TranslationMap }>(`/translations/${lang}`);

        const [viRes, langRes] = await Promise.all([
          viReq,
          langReq ?? Promise.resolve(null),
        ]);

        if (canceled) return;

        const viMap = viRes.data.translations ?? {};
        const selectedMap = langRes?.data.translations ?? viMap;

        setFallbackViMap(viMap);
        setCurrentMap(selectedMap);
      } catch {
        if (canceled) return;
        setFallbackViMap({});
        setCurrentMap({});
      }
    };

    void loadTranslations();

    return () => {
      canceled = true;
    };
  }, [lang]);

  const setLang = (l: Lang) => setLangState(l);

  const t = useMemo(() => {
    return (key: string) => currentMap[key] ?? fallbackViMap[key] ?? key;
  }, [currentMap, fallbackViMap]);

  return <I18nContext.Provider value={{ lang, setLang, t }}>{children}</I18nContext.Provider>;
};

export const useI18n = () => {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error("useI18n must be used within I18nProvider");
  return ctx;
};
