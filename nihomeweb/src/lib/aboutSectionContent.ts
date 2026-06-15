import {
  Award,
  BadgeDollarSign,
  BadgePercent,
  BriefcaseBusiness,
  Building2,
  Calendar,
  ChartColumnIncreasing,
  CheckCircle2,
  Clock3,
  Compass,
  Eye,
  FileText,
  Gem,
  Gift,
  Globe,
  Hammer,
  Handshake,
  Headphones,
  Heart,
  House,
  KeyRound,
  Landmark,
  Layers,
  Lightbulb,
  Lock,
  Mail,
  MapPin,
  Paintbrush,
  Phone,
  Rocket,
  Scale,
  Search,
  Settings,
  Shield,
  Star,
  Target,
  ThumbsUp,
  Trophy,
  UserRound,
  Users,
  Users2,
  Wrench,
  Zap,
} from "lucide-react";

const ABOUT_ICON_META = {
  calendar: { icon: Calendar, label: "Calendar" },
  building: { icon: Building2, label: "Building" },
  users: { icon: Users, label: "Users" },
  award: { icon: Award, label: "Award" },
  target: { icon: Target, label: "Target" },
  shield: { icon: Shield, label: "Shield" },
  compass: { icon: Compass, label: "Compass" },
  heart: { icon: Heart, label: "Heart" },
  search: { icon: Search, label: "Search" },
  headphones: { icon: Headphones, label: "Headphones" },
  "file-text": { icon: FileText, label: "File Text" },
  key: { icon: KeyRound, label: "Key" },
  star: { icon: Star, label: "Star" },
  check: { icon: CheckCircle2, label: "Check Circle" },
  thumbs: { icon: ThumbsUp, label: "Thumbs Up" },
  trophy: { icon: Trophy, label: "Trophy" },
  gem: { icon: Gem, label: "Gem" },
  clock: { icon: Clock3, label: "Clock" },
  zap: { icon: Zap, label: "Zap" },
  rocket: { icon: Rocket, label: "Rocket" },
  globe: { icon: Globe, label: "Globe" },
  handshake: { icon: Handshake, label: "Handshake" },
  lock: { icon: Lock, label: "Lock" },
  eye: { icon: Eye, label: "Eye" },
  phone: { icon: Phone, label: "Phone" },
  mail: { icon: Mail, label: "Mail" },
  "map-pin": { icon: MapPin, label: "Map Pin" },
  home: { icon: House, label: "Home" },
  settings: { icon: Settings, label: "Settings" },
  lightbulb: { icon: Lightbulb, label: "Lightbulb" },
  chart: { icon: ChartColumnIncreasing, label: "Chart" },
  dollar: { icon: BadgeDollarSign, label: "Dollar" },
  percent: { icon: BadgePercent, label: "Percent" },
  gift: { icon: Gift, label: "Gift" },
  user: { icon: UserRound, label: "User" },
  briefcase: { icon: BriefcaseBusiness, label: "Briefcase" },
  scale: { icon: Scale, label: "Scale" },
  paintbrush: { icon: Paintbrush, label: "Paintbrush" },
  landmark: { icon: Landmark, label: "Landmark" },
  hammer: { icon: Hammer, label: "Hammer" },
  layers: { icon: Layers, label: "Layers" },
  wrench: { icon: Wrench, label: "Wrench" },
  "users-group": { icon: Users2, label: "Users Group" },
} as const;

export type AboutIconKey = keyof typeof ABOUT_ICON_META;

const LEGACY_ABOUT_ICON_CLASS_MAP: Record<string, AboutIconKey> = {
  "fa fa-search": "search",
  "fa fa-shield": "shield",
  "fa fa-building": "building",
  "fa fa-headphones": "headphones",
  "fa fa-file-text-o": "file-text",
  "fa fa-key": "key",
  "fa fa-star": "star",
  "fa fa-heart": "heart",
  "fa fa-check-circle": "check",
  "fa fa-thumbs-up": "thumbs",
  "fa fa-trophy": "trophy",
  "fa fa-diamond": "gem",
  "fa fa-clock-o": "clock",
  "fa fa-bolt": "zap",
  "fa fa-rocket": "rocket",
  "fa fa-globe": "globe",
  "fa fa-users": "users",
  "fa fa-handshake-o": "handshake",
  "fa fa-lock": "lock",
  "fa fa-eye": "eye",
  "fa fa-phone": "phone",
  "fa fa-envelope": "mail",
  "fa fa-map-marker": "map-pin",
  "fa fa-home": "home",
  "fa fa-cog": "settings",
  "fa fa-lightbulb-o": "lightbulb",
  "fa fa-line-chart": "chart",
  "fa fa-dollar": "dollar",
  "fa fa-percent": "percent",
  "fa fa-gift": "gift",
  "fa fa-certificate": "award",
  "fa fa-user": "user",
  "fa fa-briefcase": "briefcase",
  "fa fa-gavel": "scale",
  "fa fa-paint-brush": "paintbrush",
  "fa fa-university": "landmark",
};

export const DEFAULT_STATS_ICON_KEYS: AboutIconKey[] = ["calendar", "building", "users", "award"];
export const DEFAULT_VALUE_ICON_KEYS: AboutIconKey[] = ["target", "shield", "compass", "heart"];
export const DEFAULT_STRATEGY_ICON_KEYS: AboutIconKey[] = ["building", "hammer", "layers", "wrench", "briefcase", "users-group"];

export function resolveAboutIconKey(value: string | null | undefined, fallback: AboutIconKey = "star"): AboutIconKey {
  const normalized = value?.trim().replace(/\s+/g, " ");
  if (!normalized) return fallback;

  if (normalized in ABOUT_ICON_META) {
    return normalized as AboutIconKey;
  }

  return LEGACY_ABOUT_ICON_CLASS_MAP[normalized] ?? fallback;
}

export function sortItemsBySortOrder<T extends { sortOrder?: number }>(items: T[]): T[] {
  return [...items].sort((left, right) => {
    const leftOrder = Number.isFinite(left.sortOrder) ? left.sortOrder ?? 0 : Number.MAX_SAFE_INTEGER;
    const rightOrder = Number.isFinite(right.sortOrder) ? right.sortOrder ?? 0 : Number.MAX_SAFE_INTEGER;
    return leftOrder - rightOrder;
  });
}

type OrganizationLikeItem = {
  role?: string;
  name?: string;
  isActive?: boolean;
  sortOrder?: number;
  group?: string;
  type?: string;
  section?: string;
  category?: string;
};

type ParsedOrganizationContent = {
  board: OrganizationLikeItem[];
  directors: OrganizationLikeItem[];
  companyChartUrl?: string;
  siteChartUrl?: string;
};

const toOrganizationItemArray = (value: unknown): OrganizationLikeItem[] =>
  Array.isArray(value) ? value.filter((item): item is OrganizationLikeItem => !!item && typeof item === "object") : [];

const resolveOrganizationGroup = (value: string | null | undefined): "board" | "directors" | null => {
  const normalized = value?.trim().toLowerCase();
  if (!normalized) return null;

  if (
    normalized.includes("board") ||
    normalized.includes("chair") ||
    normalized.includes("council") ||
    normalized.includes("hdqt")
  ) {
    return "board";
  }

  if (
    normalized.includes("director") ||
    normalized.includes("executive") ||
    normalized.includes("management") ||
    normalized.includes("dieu hanh") ||
    normalized.includes("điều hành")
  ) {
    return "directors";
  }

  return null;
};

const splitOrganizationList = (items: OrganizationLikeItem[]): ParsedOrganizationContent => {
  const board: OrganizationLikeItem[] = [];
  const directors: OrganizationLikeItem[] = [];

  items.forEach((item) => {
    const group =
      resolveOrganizationGroup(item.group) ??
      resolveOrganizationGroup(item.type) ??
      resolveOrganizationGroup(item.section) ??
      resolveOrganizationGroup(item.category);

    if (group === "directors") {
      directors.push(item);
      return;
    }

    board.push(item);
  });

  return { board, directors };
};

export function parseOrganizationContent(raw: string | null | undefined): ParsedOrganizationContent {
  if (!raw?.trim()) {
    return { board: [], directors: [] };
  }

  try {
    const parsed = JSON.parse(raw) as unknown;

    if (Array.isArray(parsed)) {
      return splitOrganizationList(toOrganizationItemArray(parsed));
    }

    if (!parsed || typeof parsed !== "object") {
      return { board: [], directors: [] };
    }

    const record = parsed as Record<string, unknown>;
    const board = toOrganizationItemArray(record.board ?? record.boardMembers ?? record.leadership ?? record.members);
    const directors = toOrganizationItemArray(record.directors ?? record.executives ?? record.management ?? record.executiveBoard);

    const companyChartUrl = typeof record.companyChartUrl === "string" ? record.companyChartUrl : undefined;
    const siteChartUrl = typeof record.siteChartUrl === "string" ? record.siteChartUrl : undefined;

    if (board.length > 0 || directors.length > 0) {
      return { board, directors, companyChartUrl, siteChartUrl };
    }

    const merged = toOrganizationItemArray(record.items ?? record.leaders ?? record.list);
    if (merged.length > 0) {
      const split = splitOrganizationList(merged);
      return { ...split, companyChartUrl, siteChartUrl };
    }
    return { board: [], directors: [], companyChartUrl, siteChartUrl };
  } catch {
    return { board: [], directors: [] };
  }
}

export { ABOUT_ICON_META };
