import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Award,
  BookOpen,
  Briefcase,
  Building2,
  Calendar,
  CheckCircle2,
  Compass,
  Download,
  FileText,
  Globe2,
  Hammer,
  Heart,
  Info,
  Layers,
  Loader2,
  Save,
  Search,
  Shield,
  Sparkles,
  Target,
  Users,
  Users2,
  Wrench,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { translationApi, type TranslationPair } from "@/services/contentApi";
import { useToast } from "@/hooks/use-toast";
import leadership from "@/assets/profile-leadership.jpg";
import activitiesImg from "@/assets/profile-activities.jpg";

type Lang = "vi" | "en" | "zh" | "ja";
type RowValues = Record<Lang, string>;
type FieldKind = "text" | "textarea";
type SectionId =
  | "header"
  | "nav"
  | "about"
  | "stats"
  | "values"
  | "strategy"
  | "organization"
  | "timeline"
  | "certifications"
  | "downloads";

type ProfileField = {
  key: string;
  label: string;
  hint: string;
  kind?: FieldKind;
  marker: string;
};

type ProfileSection = {
  id: SectionId;
  titleKey: string;
  fallbackTitle: string;
  descriptionKey: string;
  fallbackDescription: string;
  icon: LucideIcon;
  fields: ProfileField[];
};

const EMPTY_VALUES: RowValues = { vi: "", en: "", zh: "", ja: "" };

const LANGS: Array<{ code: Lang; label: string }> = [
  { code: "vi", label: "VI" },
  { code: "en", label: "EN" },
  { code: "zh", label: "ZH" },
  { code: "ja", label: "JA" },
];

const profileSections: ProfileSection[] = [
  {
    id: "header",
    titleKey: "aboutAdmin.section.header",
    fallbackTitle: "Header trang",
    descriptionKey: "aboutAdmin.section.headerDesc",
    fallbackDescription: "Tiêu đề lớn và mô tả đầu trang Profile.",
    icon: BookOpen,
    fields: [
      { key: "profilePage.eyebrow", label: "Nhãn đầu trang", hint: "Eyebrow trên PageHeader", marker: "H1" },
      { key: "profilePage.title", label: "Tiêu đề trang", hint: "Headline chính của trang Profile", marker: "H2" },
      { key: "profilePage.desc", label: "Mô tả trang", hint: "Đoạn mô tả dưới tiêu đề", marker: "H3", kind: "textarea" },
    ],
  },
  {
    id: "nav",
    titleKey: "aboutAdmin.section.nav",
    fallbackTitle: "Menu neo",
    descriptionKey: "aboutAdmin.section.navDesc",
    fallbackDescription: "Các nhãn điều hướng sticky bên dưới header.",
    icon: Compass,
    fields: [
      { key: "profilePage.nav.about", label: "Tab About", hint: "Liên kết tới #about", marker: "N1" },
      { key: "profilePage.nav.strategy", label: "Tab Strategy", hint: "Liên kết tới #strategy", marker: "N2" },
      { key: "profilePage.nav.org", label: "Tab Organization", hint: "Liên kết tới #org", marker: "N3" },
      { key: "profilePage.nav.timeline", label: "Tab Timeline", hint: "Liên kết tới #timeline", marker: "N4" },
      { key: "profilePage.nav.certs", label: "Tab Certifications", hint: "Liên kết tới #certs", marker: "N5" },
      { key: "profilePage.nav.downloads", label: "Tab Downloads", hint: "Liên kết tới #downloads", marker: "N6" },
    ],
  },
  {
    id: "about",
    titleKey: "aboutAdmin.section.about",
    fallbackTitle: "About",
    descriptionKey: "aboutAdmin.section.aboutDesc",
    fallbackDescription: "Ảnh lãnh đạo, headline 2 phần và phần giới thiệu công ty.",
    icon: Info,
    fields: [
      { key: "profilePage.about.eyebrow", label: "Nhãn nhỏ", hint: "Eyebrow phía trên tiêu đề", marker: "A1" },
      { key: "profilePage.about.titleA", label: "Tiêu đề dòng chính", hint: "Phần chữ thường của headline", marker: "A2" },
      { key: "profilePage.about.titleB", label: "Tiêu đề nhấn mạnh", hint: "Phần chữ gradient", marker: "A3" },
      { key: "profilePage.about.p1", label: "Đoạn mô tả chính", hint: "Paragraph lớn đầu tiên", marker: "A4", kind: "textarea" },
      { key: "profilePage.about.p2", label: "Đoạn mô tả phụ", hint: "Paragraph nhỏ bên dưới", marker: "A5", kind: "textarea" },
    ],
  },
  {
    id: "stats",
    titleKey: "aboutAdmin.section.stats",
    fallbackTitle: "Số liệu",
    descriptionKey: "aboutAdmin.section.statsDesc",
    fallbackDescription: "Các card số liệu ngay dưới section About.",
    icon: Calendar,
    fields: [
      { key: "profilePage.stat.yearsValue", label: "Giá trị số năm", hint: "Số lớn card 1", marker: "S1" },
      { key: "profilePage.stat.years", label: "Nhãn số năm", hint: "Label card 1", marker: "S2" },
      { key: "profilePage.stat.projectsValue", label: "Giá trị dự án", hint: "Số lớn card 2", marker: "S3" },
      { key: "profilePage.stat.projects", label: "Nhãn dự án", hint: "Label card 2", marker: "S4" },
      { key: "profilePage.stat.clientsValue", label: "Giá trị khách hàng", hint: "Số lớn card 3", marker: "S5" },
      { key: "profilePage.stat.clients", label: "Nhãn khách hàng", hint: "Label card 3", marker: "S6" },
      { key: "profilePage.stat.isoTop", label: "Giá trị chứng nhận", hint: "Dòng lớn card 4", marker: "S7" },
      { key: "profilePage.stat.isoBottom", label: "Nhãn chứng nhận", hint: "Label card 4", marker: "S8" },
    ],
  },
  {
    id: "values",
    titleKey: "aboutAdmin.section.values",
    fallbackTitle: "Giá trị cốt lõi",
    descriptionKey: "aboutAdmin.section.valuesDesc",
    fallbackDescription: "Bốn giá trị cốt lõi đang hiển thị trên trang Profile.",
    icon: Target,
    fields: [
      { key: "profilePage.values.eyebrow", label: "Nhãn giá trị", hint: "Eyebrow section Values", marker: "V1" },
      { key: "profilePage.values.titleA", label: "Tiêu đề giá trị", hint: "Headline trước chữ NICON", marker: "V2" },
      { key: "profilePage.v1.title", label: "Giá trị 1 - tiêu đề", hint: "Card giá trị thứ nhất", marker: "V3" },
      { key: "profilePage.v1.desc", label: "Giá trị 1 - mô tả", hint: "Mô tả card thứ nhất", marker: "V4", kind: "textarea" },
      { key: "profilePage.v2.title", label: "Giá trị 2 - tiêu đề", hint: "Card giá trị thứ hai", marker: "V5" },
      { key: "profilePage.v2.desc", label: "Giá trị 2 - mô tả", hint: "Mô tả card thứ hai", marker: "V6", kind: "textarea" },
      { key: "profilePage.v3.title", label: "Giá trị 3 - tiêu đề", hint: "Card giá trị thứ ba", marker: "V7" },
      { key: "profilePage.v3.desc", label: "Giá trị 3 - mô tả", hint: "Mô tả card thứ ba", marker: "V8", kind: "textarea" },
      { key: "profilePage.v4.title", label: "Giá trị 4 - tiêu đề", hint: "Card giá trị thứ tư", marker: "V9" },
      { key: "profilePage.v4.desc", label: "Giá trị 4 - mô tả", hint: "Mô tả card thứ tư", marker: "V10", kind: "textarea" },
    ],
  },
  {
    id: "strategy",
    titleKey: "aboutAdmin.section.strategy",
    fallbackTitle: "Chiến lược",
    descriptionKey: "aboutAdmin.section.strategyDesc",
    fallbackDescription: "Headline, tầm nhìn, định hướng tương lai và 6 lĩnh vực hoạt động.",
    icon: Shield,
    fields: [
      { key: "profilePage.strategy.eyebrow", label: "Nhãn chiến lược", hint: "Eyebrow section Strategy", marker: "ST1" },
      { key: "profilePage.strategy.titleA", label: "Tiêu đề dòng chính", hint: "Phần chữ thường", marker: "ST2" },
      { key: "profilePage.strategy.titleB", label: "Tiêu đề nhấn mạnh", hint: "Phần chữ gradient", marker: "ST3" },
      { key: "profilePage.strategy.visionLabel", label: "Nhãn tầm nhìn", hint: "Label in đậm", marker: "ST4" },
      { key: "profilePage.strategy.visionText", label: "Nội dung tầm nhìn", hint: "Đoạn văn tầm nhìn", marker: "ST5", kind: "textarea" },
      { key: "profilePage.strategy.futureLabel", label: "Nhãn tương lai", hint: "Label in đậm", marker: "ST6" },
      { key: "profilePage.strategy.futureText", label: "Nội dung tương lai", hint: "Đoạn văn định hướng", marker: "ST7", kind: "textarea" },
      { key: "profilePage.bl1.title", label: "Lĩnh vực 1 - tiêu đề", hint: "Business line 1 ở cuối section Strategy", marker: "B1" },
      { key: "profilePage.bl1.desc", label: "Lĩnh vực 1 - mô tả", hint: "Business line 1 ở cuối section Strategy", marker: "B2", kind: "textarea" },
      { key: "profilePage.bl2.title", label: "Lĩnh vực 2 - tiêu đề", hint: "Business line 2 ở cuối section Strategy", marker: "B3" },
      { key: "profilePage.bl2.desc", label: "Lĩnh vực 2 - mô tả", hint: "Business line 2 ở cuối section Strategy", marker: "B4", kind: "textarea" },
      { key: "profilePage.bl3.title", label: "Lĩnh vực 3 - tiêu đề", hint: "Business line 3 ở cuối section Strategy", marker: "B5" },
      { key: "profilePage.bl3.desc", label: "Lĩnh vực 3 - mô tả", hint: "Business line 3 ở cuối section Strategy", marker: "B6", kind: "textarea" },
      { key: "profilePage.bl4.title", label: "Lĩnh vực 4 - tiêu đề", hint: "Business line 4 ở cuối section Strategy", marker: "B7" },
      { key: "profilePage.bl4.desc", label: "Lĩnh vực 4 - mô tả", hint: "Business line 4 ở cuối section Strategy", marker: "B8", kind: "textarea" },
      { key: "profilePage.bl5.title", label: "Lĩnh vực 5 - tiêu đề", hint: "Business line 5 ở cuối section Strategy", marker: "B9" },
      { key: "profilePage.bl5.desc", label: "Lĩnh vực 5 - mô tả", hint: "Business line 5 ở cuối section Strategy", marker: "B10", kind: "textarea" },
      { key: "profilePage.bl6.title", label: "Lĩnh vực 6 - tiêu đề", hint: "Business line 6 ở cuối section Strategy", marker: "B11" },
      { key: "profilePage.bl6.desc", label: "Lĩnh vực 6 - mô tả", hint: "Business line 6 ở cuối section Strategy", marker: "B12", kind: "textarea" },
    ],
  },
  {
    id: "organization",
    titleKey: "aboutAdmin.section.organization",
    fallbackTitle: "Tổ chức",
    descriptionKey: "aboutAdmin.section.organizationDesc",
    fallbackDescription: "Tiêu đề section và danh sách lãnh đạo.",
    icon: Users2,
    fields: [
      { key: "profilePage.org.eyebrow", label: "Nhãn tổ chức", hint: "Eyebrow section Organization", marker: "O1" },
      { key: "profilePage.org.titleA", label: "Tiêu đề dòng chính", hint: "Phần chữ thường", marker: "O2" },
      { key: "profilePage.org.titleB", label: "Tiêu đề nhấn mạnh", hint: "Phần chữ gradient", marker: "O3" },
      { key: "profilePage.org.board", label: "Tiêu đề HĐQT", hint: "Card bên trái", marker: "O4" },
      { key: "profilePage.org.exec", label: "Tiêu đề điều hành", hint: "Card bên phải", marker: "O5" },
      { key: "profilePage.ld.role.chair", label: "Vai trò Chủ tịch", hint: "Role trong HĐQT", marker: "L1" },
      { key: "profilePage.ld.name.chair", label: "Tên Chủ tịch", hint: "Tên trong HĐQT", marker: "L2" },
      { key: "profilePage.ld.role.viceChair", label: "Vai trò Phó Chủ tịch", hint: "Dùng cho 2 dòng", marker: "L3" },
      { key: "profilePage.ld.name.viceChair1", label: "Tên Phó Chủ tịch 1", hint: "Tên trong HĐQT", marker: "L4" },
      { key: "profilePage.ld.name.viceChair2", label: "Tên Phó Chủ tịch 2", hint: "Tên trong HĐQT", marker: "L5" },
      { key: "profilePage.ld.role.secretary", label: "Vai trò Thư ký", hint: "Role trong HĐQT", marker: "L6" },
      { key: "profilePage.ld.name.secretary", label: "Tên Thư ký", hint: "Tên trong HĐQT", marker: "L7" },
      { key: "profilePage.ld.role.ceo", label: "Vai trò CEO", hint: "Role điều hành", marker: "L8" },
      { key: "profilePage.ld.name.ceo", label: "Tên CEO", hint: "Tên điều hành", marker: "L9" },
      { key: "profilePage.ld.role.bdJp", label: "Vai trò BD Japan", hint: "Role điều hành", marker: "L10" },
      { key: "profilePage.ld.name.bdJp", label: "Tên BD Japan", hint: "Tên điều hành", marker: "L11" },
      { key: "profilePage.ld.role.bdAsia", label: "Vai trò BD Asia", hint: "Role điều hành", marker: "L12" },
      { key: "profilePage.ld.name.bdAsia", label: "Tên BD Asia", hint: "Tên điều hành", marker: "L13" },
      { key: "profilePage.ld.role.design", label: "Vai trò Design", hint: "Role điều hành", marker: "L14" },
      { key: "profilePage.ld.name.design", label: "Tên Design", hint: "Tên điều hành", marker: "L15" },
    ],
  },
  {
    id: "timeline",
    titleKey: "aboutAdmin.section.timeline",
    fallbackTitle: "Timeline",
    descriptionKey: "aboutAdmin.section.timelineDesc",
    fallbackDescription: "Tiêu đề timeline và các mốc phát triển.",
    icon: Calendar,
    fields: [
      { key: "profilePage.timeline.eyebrow", label: "Nhãn timeline", hint: "Eyebrow section Timeline", marker: "T1" },
      { key: "profilePage.timeline.titleA", label: "Tiêu đề dòng chính", hint: "Phần chữ thường", marker: "T2" },
      { key: "profilePage.timeline.titleB", label: "Tiêu đề nhấn mạnh", hint: "Phần chữ gradient", marker: "T3" },
      ...["2006", "2007", "2010", "2016", "2018", "2024"].flatMap((year, index) => [
        { key: `profilePage.ms.${year}.t`, label: `Mốc ${year} - tiêu đề`, hint: `Tiêu đề milestone ${year}`, marker: `M${index + 1}A` },
        { key: `profilePage.ms.${year}.d`, label: `Mốc ${year} - mô tả`, hint: `Mô tả milestone ${year}`, marker: `M${index + 1}B`, kind: "textarea" as FieldKind },
      ]),
    ],
  },
  {
    id: "certifications",
    titleKey: "aboutAdmin.section.certifications",
    fallbackTitle: "Chứng nhận",
    descriptionKey: "aboutAdmin.section.certificationsDesc",
    fallbackDescription: "Tiêu đề section và 3 card chứng nhận.",
    icon: Award,
    fields: [
      { key: "profilePage.certs.eyebrow", label: "Nhãn chứng nhận", hint: "Eyebrow section Certifications", marker: "C1" },
      { key: "profilePage.certs.titleA", label: "Tiêu đề dòng chính", hint: "Phần chữ thường", marker: "C2" },
      { key: "profilePage.certs.titleB", label: "Tiêu đề nhấn mạnh", hint: "Phần chữ gradient", marker: "C3" },
      { key: "profilePage.cert.iso2008.n", label: "ISO 2008 - tên", hint: "Card chứng nhận 1", marker: "C4" },
      { key: "profilePage.cert.iso2008.d", label: "ISO 2008 - mô tả", hint: "Card chứng nhận 1", marker: "C5", kind: "textarea" },
      { key: "profilePage.cert.iso2015.n", label: "ISO 2015 - tên", hint: "Card chứng nhận 2", marker: "C6" },
      { key: "profilePage.cert.iso2015.d", label: "ISO 2015 - mô tả", hint: "Card chứng nhận 2", marker: "C7", kind: "textarea" },
      { key: "profilePage.cert.iso14001.n", label: "ISO 14001 - tên", hint: "Card chứng nhận 3", marker: "C8" },
      { key: "profilePage.cert.iso14001.d", label: "ISO 14001 - mô tả", hint: "Card chứng nhận 3", marker: "C9", kind: "textarea" },
    ],
  },
  {
    id: "downloads",
    titleKey: "aboutAdmin.section.downloads",
    fallbackTitle: "Tài liệu tải xuống",
    descriptionKey: "aboutAdmin.section.downloadsDesc",
    fallbackDescription: "Tiêu đề, mô tả và danh sách tài liệu tải xuống.",
    icon: Download,
    fields: [
      { key: "profilePage.dl.eyebrow", label: "Nhãn download", hint: "Eyebrow section Downloads", marker: "D1" },
      { key: "profilePage.dl.titleA", label: "Tiêu đề dòng chính", hint: "Phần chữ thường", marker: "D2" },
      { key: "profilePage.dl.titleB", label: "Tiêu đề nhấn mạnh", hint: "Phần chữ gradient", marker: "D3" },
      { key: "profilePage.dl.desc", label: "Mô tả download", hint: "Đoạn mô tả dưới tiêu đề", marker: "D4", kind: "textarea" },
      ...[1, 2, 3, 4].flatMap((item) => [
        { key: `profilePage.dl.f${item}`, label: `Tài liệu ${item} - tên`, hint: `Tên file ${item}`, marker: `F${item}A` },
        { key: `profilePage.dl.f${item}.type`, label: `Tài liệu ${item} - loại`, hint: `Loại file ${item}`, marker: `F${item}B` },
        { key: `profilePage.dl.f${item}.size`, label: `Tài liệu ${item} - dung lượng`, hint: `Dung lượng file ${item}`, marker: `F${item}C` },
      ]),
    ],
  },
];

const markerStyle = {
  background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(22 95% 58%))",
  color: "white",
};

const normalizePair = (pair: TranslationPair): RowValues => ({
  vi: pair.vietnameseValue ?? "",
  en: pair.translations?.en ?? "",
  zh: pair.translations?.zh ?? "",
  ja: pair.translations?.ja ?? "",
});

const makeEmptyValues = () => ({ ...EMPTY_VALUES });

const textFor = (values: Record<string, RowValues>, key: string, lang: Lang) => values[key]?.[lang] ?? "";

const textOrDash = (values: Record<string, RowValues>, key: string, lang: Lang) => textFor(values, key, lang) || "—";

const translateWithFallback = (t: (key: string) => string, key: string, fallback: string) => {
  const translated = t(key);
  return translated === key ? fallback : translated;
};

const Marker = ({ value }: { value: string }) => (
  <span className="inline-flex h-6 min-w-6 shrink-0 items-center justify-center rounded-full px-2 text-[10px] font-extrabold" style={markerStyle}>
    {value}
  </span>
);

const PreviewCard = ({ marker, children }: { marker: string; children: React.ReactNode }) => (
  <div className="rounded-2xl border p-4" style={{ borderColor: "hsl(var(--admin-border))", background: "hsl(var(--admin-surface))" }}>
    <div className="mb-2"><Marker value={marker} /></div>
    {children}
  </div>
);

const AboutContent = () => {
  const { t } = useI18n();
  const { toast } = useToast();

  const [loading, setLoading] = useState(true);
  const [savingSection, setSavingSection] = useState<SectionId | null>(null);
  const [search, setSearch] = useState("");
  const [activeLang, setActiveLang] = useState<Lang>("vi");
  const [activeSectionId, setActiveSectionId] = useState<SectionId>("about");
  const [baseValues, setBaseValues] = useState<Record<string, RowValues>>({});
  const [draftValues, setDraftValues] = useState<Record<string, RowValues>>({});

  const allFields = useMemo(() => profileSections.flatMap((section) => section.fields), []);
  const allKeys = useMemo(() => allFields.map((field) => field.key), [allFields]);
  const activeSection = profileSections.find((section) => section.id === activeSectionId) ?? profileSections[0];

  const loadPairs = useCallback(async () => {
    setLoading(true);
    try {
      const { data } = await translationApi.getPairs({ category: "profilePage" });
      const fromApi = data.reduce<Record<string, RowValues>>((acc, pair) => {
        acc[pair.key] = normalizePair(pair);
        return acc;
      }, {});
      const normalized = allKeys.reduce<Record<string, RowValues>>((acc, key) => {
        acc[key] = fromApi[key] ? { ...fromApi[key] } : makeEmptyValues();
        return acc;
      }, {});
      setBaseValues(normalized);
      setDraftValues(normalized);
    } catch {
      toast({
        title: t("auth.error"),
        description: t("aboutAdmin.loadError"),
        variant: "destructive",
      });
    } finally {
      setLoading(false);
    }
  }, [allKeys, t, toast]);

  useEffect(() => {
    void loadPairs();
  }, [loadPairs]);

  const updateValue = (key: string, lang: Lang, value: string) => {
    setDraftValues((prev) => ({
      ...prev,
      [key]: {
        ...(prev[key] ?? makeEmptyValues()),
        [lang]: value,
      },
    }));
  };

  const isDirtyKey = (key: string) => {
    const base = baseValues[key] ?? EMPTY_VALUES;
    const draft = draftValues[key] ?? EMPTY_VALUES;
    return base.vi !== draft.vi || base.en !== draft.en || base.zh !== draft.zh || base.ja !== draft.ja;
  };

  const isDirtySection = (section: ProfileSection) => section.fields.some((field) => isDirtyKey(field.key));

  const sectionMatchesSearch = useCallback((section: ProfileSection, q: string) => {
    if (!q) return true;
    return section.fields.some((field) => {
      const values = draftValues[field.key] ?? EMPTY_VALUES;
      return (
        field.key.toLowerCase().includes(q) ||
        field.label.toLowerCase().includes(q) ||
        field.hint.toLowerCase().includes(q) ||
        values.vi.toLowerCase().includes(q) ||
        values.en.toLowerCase().includes(q) ||
        values.zh.toLowerCase().includes(q) ||
        values.ja.toLowerCase().includes(q)
      );
    });
  }, [draftValues]);

  const visibleSections = useMemo(() => {
    const q = search.trim().toLowerCase();
    return profileSections.filter((section) => sectionMatchesSearch(section, q));
  }, [search, sectionMatchesSearch]);

  useEffect(() => {
    if (visibleSections.length > 0 && !visibleSections.some((section) => section.id === activeSectionId)) {
      setActiveSectionId(visibleSections[0].id);
    }
  }, [activeSectionId, visibleSections]);

  const visibleFields = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return activeSection.fields;
    return activeSection.fields.filter((field) => {
      const values = draftValues[field.key] ?? EMPTY_VALUES;
      return (
        field.key.toLowerCase().includes(q) ||
        field.label.toLowerCase().includes(q) ||
        field.hint.toLowerCase().includes(q) ||
        values.vi.toLowerCase().includes(q) ||
        values.en.toLowerCase().includes(q) ||
        values.zh.toLowerCase().includes(q) ||
        values.ja.toLowerCase().includes(q)
      );
    });
  }, [activeSection, draftValues, search]);

  const saveSection = async (section: ProfileSection) => {
    const missingVi = section.fields.find((field) => !draftValues[field.key]?.vi.trim());
    if (missingVi) {
      toast({
        title: t("aboutAdmin.missingDataTitle"),
        description: `${t("aboutAdmin.viRequired")} (${missingVi.label})`,
        variant: "destructive",
      });
      return;
    }

    const dirtyFields = section.fields.filter((field) => isDirtyKey(field.key));
    if (dirtyFields.length === 0) return;

    setSavingSection(section.id);
    try {
      await Promise.all(
        dirtyFields.map((field) => {
          const values = draftValues[field.key] ?? makeEmptyValues();
          const translations: Record<string, string> = {};
          if (values.en.trim()) translations.en = values.en;
          if (values.zh.trim()) translations.zh = values.zh;
          if (values.ja.trim()) translations.ja = values.ja;

          return translationApi.upsertPair({
            key: field.key,
            vietnameseValue: values.vi,
            translations,
            category: "profilePage",
          });
        }),
      );

      setBaseValues((prev) => {
        const next = { ...prev };
        dirtyFields.forEach((field) => {
          next[field.key] = { ...(draftValues[field.key] ?? makeEmptyValues()) };
        });
        return next;
      });
      toast({ title: t("form.updated") });
    } catch {
      toast({
        title: t("auth.error"),
        description: t("aboutAdmin.saveError"),
        variant: "destructive",
      });
    } finally {
      setSavingSection(null);
    }
  };

  const renderPreview = (section: ProfileSection) => {
    const v = draftValues;
    const get = (key: string) => textOrDash(v, key, activeLang);

    if (section.id === "header") {
      return (
        <div className="space-y-4">
          <PreviewCard marker="H1">
            <p className="text-xs font-bold uppercase tracking-[0.18em]" style={{ color: "hsl(var(--admin-primary))" }}>{get("profilePage.eyebrow")}</p>
          </PreviewCard>
          <PreviewCard marker="H2">
            <h2 className="text-2xl font-extrabold leading-tight">{get("profilePage.title")}</h2>
          </PreviewCard>
          <PreviewCard marker="H3">
            <p className="text-sm leading-relaxed" style={{ color: "hsl(var(--admin-muted))" }}>{get("profilePage.desc")}</p>
          </PreviewCard>
        </div>
      );
    }

    if (section.id === "nav") {
      return (
        <div className="flex flex-wrap gap-2">
          {section.fields.map((field) => (
            <span key={field.key} className="inline-flex items-center gap-2 rounded-full px-3 py-2 text-xs font-bold" style={{ background: "hsl(var(--admin-bg))" }}>
              <Marker value={field.marker} /> {get(field.key)}
            </span>
          ))}
        </div>
      );
    }

    if (section.id === "about") {
      return (
        <div className="grid gap-5 lg:grid-cols-2">
          <div className="overflow-hidden rounded-2xl bg-white">
            <img src={leadership} alt="NICON leadership" className="h-full min-h-64 w-full object-cover" />
          </div>
          <div className="space-y-4">
            <PreviewCard marker="A1">
              <p className="text-xs font-bold uppercase tracking-[0.18em]" style={{ color: "hsl(var(--admin-primary))" }}>{get("profilePage.about.eyebrow")}</p>
            </PreviewCard>
            <PreviewCard marker="A2/A3">
              <h2 className="text-2xl font-extrabold leading-tight">
                {get("profilePage.about.titleA")} <span style={{ color: "hsl(var(--admin-primary))" }}>{get("profilePage.about.titleB")}</span>.
              </h2>
            </PreviewCard>
            <PreviewCard marker="A4">
              <p className="text-base leading-relaxed">{get("profilePage.about.p1")}</p>
            </PreviewCard>
            <PreviewCard marker="A5">
              <p className="text-sm leading-relaxed" style={{ color: "hsl(var(--admin-muted))" }}>{get("profilePage.about.p2")}</p>
            </PreviewCard>
          </div>
        </div>
      );
    }

    if (section.id === "stats") {
      const stats = [
        ["S1", "profilePage.stat.yearsValue", "profilePage.stat.years", Calendar],
        ["S3", "profilePage.stat.projectsValue", "profilePage.stat.projects", Building2],
        ["S5", "profilePage.stat.clientsValue", "profilePage.stat.clients", Users],
        ["S7", "profilePage.stat.isoTop", "profilePage.stat.isoBottom", Award],
      ] as const;
      return (
        <div className="grid gap-3 sm:grid-cols-2">
          {stats.map(([marker, valueKey, labelKey, Icon]) => (
            <PreviewCard key={valueKey} marker={marker}>
              <Icon className="mb-3 h-6 w-6" style={{ color: "hsl(var(--admin-primary))" }} />
              <p className="text-3xl font-extrabold" style={{ color: "hsl(var(--admin-primary))" }}>{get(valueKey)}</p>
              <p className="text-sm" style={{ color: "hsl(var(--admin-muted))" }}>{get(labelKey)}</p>
            </PreviewCard>
          ))}
        </div>
      );
    }

    if (section.id === "values") {
      const valueItems = [
        ["V3", "profilePage.v1.title", "profilePage.v1.desc", Target],
        ["V5", "profilePage.v2.title", "profilePage.v2.desc", Shield],
        ["V7", "profilePage.v3.title", "profilePage.v3.desc", Compass],
        ["V9", "profilePage.v4.title", "profilePage.v4.desc", Heart],
      ] as const;
      return (
        <div className="space-y-5">
          <PreviewCard marker="V1/V2">
            <p className="text-xs font-bold uppercase tracking-[0.18em]" style={{ color: "hsl(var(--admin-primary))" }}>{get("profilePage.values.eyebrow")}</p>
            <h2 className="mt-2 text-2xl font-extrabold">{get("profilePage.values.titleA")} <span style={{ color: "hsl(var(--admin-primary))" }}>NICON</span>.</h2>
          </PreviewCard>
          <div className="grid gap-3 sm:grid-cols-2">
            {valueItems.map(([marker, titleKey, descKey, Icon]) => (
              <PreviewCard key={titleKey} marker={marker}>
                <Icon className="mb-3 h-5 w-5" style={{ color: "hsl(var(--admin-primary))" }} />
                <h3 className="font-extrabold">{get(titleKey)}</h3>
                <p className="mt-1 text-sm leading-relaxed" style={{ color: "hsl(var(--admin-muted))" }}>{get(descKey)}</p>
              </PreviewCard>
            ))}
          </div>
        </div>
      );
    }

    if (section.id === "strategy") {
      const businessItems = [
        ["B1", "profilePage.bl1.title", "profilePage.bl1.desc", Building2],
        ["B3", "profilePage.bl2.title", "profilePage.bl2.desc", Hammer],
        ["B5", "profilePage.bl3.title", "profilePage.bl3.desc", Layers],
        ["B7", "profilePage.bl4.title", "profilePage.bl4.desc", Wrench],
        ["B9", "profilePage.bl5.title", "profilePage.bl5.desc", Briefcase],
        ["B11", "profilePage.bl6.title", "profilePage.bl6.desc", Users2],
      ] as const;

      return (
        <div className="space-y-4">
          <PreviewCard marker="ST1/ST2/ST3">
            <p className="text-xs font-bold uppercase tracking-[0.18em]" style={{ color: "hsl(var(--admin-primary))" }}>{get("profilePage.strategy.eyebrow")}</p>
            <h2 className="mt-2 text-2xl font-extrabold">{get("profilePage.strategy.titleA")} <span style={{ color: "hsl(var(--admin-primary))" }}>{get("profilePage.strategy.titleB")}</span>.</h2>
          </PreviewCard>
          <PreviewCard marker="ST4/ST5">
            <p className="text-sm leading-relaxed"><strong>{get("profilePage.strategy.visionLabel")}:</strong> {get("profilePage.strategy.visionText")}</p>
          </PreviewCard>
          <PreviewCard marker="ST6/ST7">
            <p className="text-sm leading-relaxed"><strong>{get("profilePage.strategy.futureLabel")}:</strong> {get("profilePage.strategy.futureText")}</p>
          </PreviewCard>
          <div className="grid gap-3 sm:grid-cols-2">
            {businessItems.map(([marker, titleKey, descKey, Icon]) => (
              <PreviewCard key={titleKey} marker={marker}>
                <Icon className="mb-3 h-5 w-5" style={{ color: "hsl(var(--admin-primary))" }} />
                <h3 className="font-extrabold">{get(titleKey)}</h3>
                <p className="mt-1 text-sm leading-relaxed" style={{ color: "hsl(var(--admin-muted))" }}>{get(descKey)}</p>
              </PreviewCard>
            ))}
          </div>
        </div>
      );
    }

    if (section.id === "organization") {
      const board = [
        ["profilePage.ld.role.chair", "profilePage.ld.name.chair"],
        ["profilePage.ld.role.viceChair", "profilePage.ld.name.viceChair1"],
        ["profilePage.ld.role.viceChair", "profilePage.ld.name.viceChair2"],
        ["profilePage.ld.role.secretary", "profilePage.ld.name.secretary"],
      ];
      const directors = [
        ["profilePage.ld.role.ceo", "profilePage.ld.name.ceo"],
        ["profilePage.ld.role.bdJp", "profilePage.ld.name.bdJp"],
        ["profilePage.ld.role.bdAsia", "profilePage.ld.name.bdAsia"],
        ["profilePage.ld.role.design", "profilePage.ld.name.design"],
      ];
      return (
        <div className="space-y-4">
          <PreviewCard marker="O1/O2/O3">
            <p className="text-xs font-bold uppercase tracking-[0.18em]" style={{ color: "hsl(var(--admin-primary))" }}>{get("profilePage.org.eyebrow")}</p>
            <h2 className="mt-2 text-2xl font-extrabold">{get("profilePage.org.titleA")} <span style={{ color: "hsl(var(--admin-primary))" }}>{get("profilePage.org.titleB")}</span>.</h2>
          </PreviewCard>
          <div className="grid gap-3 lg:grid-cols-2">
            <PreviewCard marker="O4">
              <h3 className="mb-3 font-extrabold">{get("profilePage.org.board")}</h3>
              {board.map(([roleKey, nameKey]) => (
                <div key={`${roleKey}-${nameKey}`} className="flex justify-between gap-4 border-t py-2 text-sm" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <span style={{ color: "hsl(var(--admin-muted))" }}>{get(roleKey)}</span>
                  <strong className="text-right">{get(nameKey)}</strong>
                </div>
              ))}
            </PreviewCard>
            <PreviewCard marker="O5">
              <h3 className="mb-3 font-extrabold">{get("profilePage.org.exec")}</h3>
              {directors.map(([roleKey, nameKey]) => (
                <div key={`${roleKey}-${nameKey}`} className="flex justify-between gap-4 border-t py-2 text-sm" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <span style={{ color: "hsl(var(--admin-muted))" }}>{get(roleKey)}</span>
                  <strong className="text-right">{get(nameKey)}</strong>
                </div>
              ))}
            </PreviewCard>
          </div>
        </div>
      );
    }

    if (section.id === "timeline") {
      return (
        <div className="grid gap-5 lg:grid-cols-[220px,1fr]">
          <div>
            <PreviewCard marker="T1/T2/T3">
              <p className="text-xs font-bold uppercase tracking-[0.18em]" style={{ color: "hsl(var(--admin-primary))" }}>{get("profilePage.timeline.eyebrow")}</p>
              <h2 className="mt-2 text-2xl font-extrabold">{get("profilePage.timeline.titleA")} <span style={{ color: "hsl(var(--admin-primary))" }}>{get("profilePage.timeline.titleB")}</span>.</h2>
            </PreviewCard>
            <img src={activitiesImg} alt="" className="mt-3 rounded-2xl" />
          </div>
          <div className="space-y-3">
            {["2006", "2007", "2010", "2016", "2018", "2024"].map((year, index) => (
              <PreviewCard key={year} marker={`M${index + 1}A`}>
                <div className="flex gap-3">
                  <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full text-[10px] font-extrabold" style={markerStyle}>{year}</span>
                  <div>
                    <h3 className="font-extrabold">{get(`profilePage.ms.${year}.t`)}</h3>
                    <p className="mt-1 text-sm leading-relaxed" style={{ color: "hsl(var(--admin-muted))" }}>{get(`profilePage.ms.${year}.d`)}</p>
                  </div>
                </div>
              </PreviewCard>
            ))}
          </div>
        </div>
      );
    }

    if (section.id === "certifications") {
      return (
        <div className="space-y-4">
          <PreviewCard marker="C1/C2/C3">
            <p className="text-xs font-bold uppercase tracking-[0.18em]" style={{ color: "hsl(var(--admin-primary))" }}>{get("profilePage.certs.eyebrow")}</p>
            <h2 className="mt-2 text-2xl font-extrabold">{get("profilePage.certs.titleA")} <span style={{ color: "hsl(var(--admin-primary))" }}>{get("profilePage.certs.titleB")}</span>.</h2>
          </PreviewCard>
          <div className="grid gap-3 sm:grid-cols-3">
            {[
              ["C4", "profilePage.cert.iso2008.n", "profilePage.cert.iso2008.d"],
              ["C6", "profilePage.cert.iso2015.n", "profilePage.cert.iso2015.d"],
              ["C8", "profilePage.cert.iso14001.n", "profilePage.cert.iso14001.d"],
            ].map(([marker, nameKey, descKey]) => (
              <PreviewCard key={nameKey} marker={marker}>
                <Award className="mx-auto mb-3 h-8 w-8" style={{ color: "hsl(var(--admin-primary))" }} />
                <h3 className="text-center font-extrabold">{get(nameKey)}</h3>
                <p className="mt-1 text-center text-sm" style={{ color: "hsl(var(--admin-muted))" }}>{get(descKey)}</p>
              </PreviewCard>
            ))}
          </div>
        </div>
      );
    }

    return (
      <div className="space-y-4">
        <PreviewCard marker="D1/D2/D3/D4">
          <p className="text-xs font-bold uppercase tracking-[0.18em]" style={{ color: "hsl(var(--admin-primary))" }}>{get("profilePage.dl.eyebrow")}</p>
          <h2 className="mt-2 text-2xl font-extrabold">{get("profilePage.dl.titleA")} <span style={{ color: "hsl(var(--admin-primary))" }}>{get("profilePage.dl.titleB")}</span>.</h2>
          <p className="mt-2 text-sm leading-relaxed" style={{ color: "hsl(var(--admin-muted))" }}>{get("profilePage.dl.desc")}</p>
        </PreviewCard>
        {[1, 2, 3, 4].map((item) => (
          <PreviewCard key={item} marker={`F${item}A`}>
            <div className="flex items-center gap-4">
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl" style={{ background: "hsl(var(--admin-primary-soft))", color: "hsl(var(--admin-primary))" }}>
                <FileText className="h-5 w-5" />
              </span>
              <div className="min-w-0 flex-1">
                <p className="truncate font-extrabold">{get(`profilePage.dl.f${item}`)}</p>
                <p className="text-xs font-bold uppercase tracking-wider" style={{ color: "hsl(var(--admin-muted))" }}>
                  {get(`profilePage.dl.f${item}.type`)} · {get(`profilePage.dl.f${item}.size`)}
                </p>
              </div>
              <Download className="h-4 w-4 shrink-0" />
            </div>
          </PreviewCard>
        ))}
      </div>
    );
  };

  return (
    <AdminLayout>
      <div className="admin-card p-6 lg:p-8 mb-6" style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary-soft)), hsl(var(--admin-surface)))" }}>
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.18em] mb-2" style={{ color: "hsl(var(--admin-primary))" }}>
              {t("nav.about")}
            </p>
            <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("aboutAdmin.title")}</h1>
            <p className="text-sm mt-2 max-w-3xl" style={{ color: "hsl(var(--admin-muted))" }}>
              {translateWithFallback(t, "aboutAdmin.profileManagerDesc", "Quản lý toàn bộ nội dung text đang hiển thị trên trang Profile/Về chúng tôi của client.")}
            </p>
          </div>
          <div className="grid grid-cols-2 gap-3 text-xs">
            <div className="admin-chip inline-flex items-center gap-1.5">
              <Globe2 className="w-3.5 h-3.5" /> {t("aboutAdmin.languages")}
            </div>
            <div className="admin-chip inline-flex items-center gap-1.5">
              <Info className="w-3.5 h-3.5" /> profilePage.*
            </div>
          </div>
        </div>
      </div>

      <div className="admin-card p-4 mb-5">
        <div className="flex flex-col gap-3 xl:flex-row xl:items-center xl:justify-between">
          <div className="flex items-center gap-2 admin-input w-full xl:max-w-md px-3 py-2">
            <Search className="w-4 h-4 shrink-0" style={{ color: "hsl(var(--admin-muted))" }} />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t("aboutAdmin.searchPlaceholder")}
              className="bg-transparent outline-none w-full text-sm"
            />
          </div>

          <div className="flex items-center gap-2 overflow-x-auto">
            {LANGS.map((lang) => (
              <button
                key={lang.code}
                onClick={() => setActiveLang(lang.code)}
                className="px-3 py-1.5 rounded-full text-xs font-bold transition shrink-0"
                style={
                  activeLang === lang.code
                    ? { background: "hsl(var(--admin-primary))", color: "white" }
                    : { background: "hsl(var(--admin-bg))", color: "hsl(var(--admin-muted))" }
                }
                type="button"
              >
                {lang.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      {loading ? (
        <div className="admin-card p-12 flex items-center justify-center">
          <Loader2 className="w-6 h-6 animate-spin" style={{ color: "hsl(var(--admin-primary))" }} />
        </div>
      ) : visibleSections.length === 0 ? (
        <div className="admin-card p-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
          {t("aboutAdmin.empty")}
        </div>
      ) : (
        <div className="grid gap-5 xl:grid-cols-[280px,1fr]">
          <aside className="admin-card p-3 h-fit xl:sticky xl:top-24">
            <div className="space-y-2">
              {visibleSections.map((section) => {
                const Icon = section.icon;
                const dirty = isDirtySection(section);
                const active = section.id === activeSection.id;
                return (
                  <button
                    key={section.id}
                    onClick={() => setActiveSectionId(section.id)}
                    className="w-full rounded-xl px-3 py-3 text-left transition"
                    style={
                      active
                        ? { background: "hsl(var(--admin-primary-soft))", color: "hsl(var(--admin-primary))" }
                        : { color: "hsl(var(--admin-sidebar-text))" }
                    }
                    type="button"
                  >
                    <div className="flex items-center gap-3">
                      <Icon className="h-4 w-4 shrink-0" />
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-extrabold">{translateWithFallback(t, section.titleKey, section.fallbackTitle)}</p>
                        <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>{section.fields.length} fields</p>
                      </div>
                      {dirty ? (
                        <Sparkles className="h-4 w-4 shrink-0" style={{ color: "hsl(var(--admin-warning))" }} />
                      ) : (
                        <CheckCircle2 className="h-4 w-4 shrink-0" style={{ color: "hsl(var(--admin-success))" }} />
                      )}
                    </div>
                  </button>
                );
              })}
            </div>
          </aside>

          <section className="space-y-5">
            <div className="admin-card p-5">
              <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                <div>
                  <div className="flex items-center gap-3">
                    <activeSection.icon className="h-5 w-5" style={{ color: "hsl(var(--admin-primary))" }} />
                    <h2 className="text-xl font-extrabold">{translateWithFallback(t, activeSection.titleKey, activeSection.fallbackTitle)}</h2>
                  </div>
                  <p className="mt-2 text-sm" style={{ color: "hsl(var(--admin-muted))" }}>
                    {translateWithFallback(t, activeSection.descriptionKey, activeSection.fallbackDescription)}
                  </p>
                </div>
                <div className="flex flex-wrap items-center gap-3">
                  {isDirtySection(activeSection) ? (
                    <span className="inline-flex items-center gap-1.5 text-xs font-bold" style={{ color: "hsl(var(--admin-warning))" }}>
                      <Sparkles className="h-3.5 w-3.5" /> {t("aboutAdmin.unsaved")}
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1.5 text-xs font-bold" style={{ color: "hsl(var(--admin-success))" }}>
                      <CheckCircle2 className="h-3.5 w-3.5" /> {t("aboutAdmin.synced")}
                    </span>
                  )}
                  <button
                    onClick={() => void saveSection(activeSection)}
                    disabled={!isDirtySection(activeSection) || savingSection === activeSection.id}
                    className="admin-btn-primary inline-flex items-center gap-2 px-4 py-2 text-sm disabled:opacity-50 disabled:cursor-not-allowed"
                    type="button"
                  >
                    {savingSection === activeSection.id ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                    {translateWithFallback(t, "aboutAdmin.saveSection", "Lưu section")}
                  </button>
                </div>
              </div>
            </div>

            <div className="grid gap-5 2xl:grid-cols-[minmax(0,0.9fr),minmax(460px,1.1fr)]">
              <div className="space-y-3">
                {visibleFields.map((field) => {
                  const value = draftValues[field.key]?.[activeLang] ?? "";
                  const longText = field.kind === "textarea" || value.length > 120 || value.includes("\n");
                  return (
                    <div key={field.key} className="admin-card p-4">
                      <div className="mb-3 flex items-start gap-3">
                        <Marker value={field.marker} />
                        <div className="min-w-0 flex-1">
                          <label className="block text-sm font-extrabold">{field.label}</label>
                          <p className="mt-1 text-xs" style={{ color: "hsl(var(--admin-muted))" }}>{field.hint}</p>
                          <p className="mt-1 truncate font-mono text-[11px]" style={{ color: "hsl(var(--admin-muted))" }}>{field.key}</p>
                        </div>
                      </div>
                      {longText ? (
                        <textarea
                          value={value}
                          onChange={(e) => updateValue(field.key, activeLang, e.target.value)}
                          rows={4}
                          className="admin-input w-full"
                        />
                      ) : (
                        <input
                          value={value}
                          onChange={(e) => updateValue(field.key, activeLang, e.target.value)}
                          className="admin-input w-full"
                        />
                      )}
                    </div>
                  );
                })}
              </div>

              <div className="admin-card p-4 lg:p-5 h-fit 2xl:sticky 2xl:top-24">
                <div className="mb-4 flex items-center justify-between gap-3">
                  <div>
                    <p className="text-xs font-bold uppercase tracking-[0.16em]" style={{ color: "hsl(var(--admin-primary))" }}>
                      {translateWithFallback(t, "aboutAdmin.preview", "Xem trước")}
                    </p>
                    <h3 className="font-extrabold">{translateWithFallback(t, activeSection.titleKey, activeSection.fallbackTitle)}</h3>
                  </div>
                  <span className="rounded-full px-3 py-1 text-xs font-bold" style={{ background: "hsl(var(--admin-bg))", color: "hsl(var(--admin-muted))" }}>
                    {activeLang.toUpperCase()}
                  </span>
                </div>
                <div className="rounded-2xl p-3" style={{ background: "hsl(var(--admin-bg))" }}>
                  {renderPreview(activeSection)}
                </div>
              </div>
            </div>
          </section>
        </div>
      )}
    </AdminLayout>
  );
};

export default AboutContent;
