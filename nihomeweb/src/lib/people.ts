import type { SearchableSelectOption } from "@/components/ui/searchable-select";

export interface PersonIdentity {
  roleCode?: string;
  role?: string;
  roleName?: string | null;
  roleLabelKey?: string | null;
  departmentId?: number | null;
  roleGroupId?: number | null;
  departmentLabelKey?: string | null;
}

export async function collectPeoplePages<T extends { id: number }>(
  first: { items: T[]; total: number },
  loadPage: (skip: number, take: number) => Promise<{ items: T[]; total: number }>,
): Promise<T[]> {
  const items = [...first.items];
  for (let skip = items.length; skip < first.total; skip += 100) {
    const page = await loadPage(skip, 100);
    if (!page.items.length) break;
    items.push(...page.items);
  }
  return Array.from(new Map(items.map((person) => [person.id, person])).values());
}

export function personRole(person: PersonIdentity, t: (key: string) => string): string {
  const translated = person.roleLabelKey ? t(person.roleLabelKey) : null;
  return (
    (translated && translated !== person.roleLabelKey ? translated : person.roleName) ||
    person.roleCode ||
    person.role ||
    t("people.roleUnassigned")
  );
}

export function projectMemberOption(
  person: PersonIdentity & {
    id: number;
    userName: string;
    position: string;
    roles: { roleCode: string }[];
  },
  t: (key: string) => string,
): SearchableSelectOption {
  const option = personOption({ ...person, name: person.userName }, t);
  return {
    ...option,
    value: String(person.id),
    hint: `${projectMemberRole(person, t)} · ${option.department!.label}`,
  };
}

export function projectMemberRole(
  person: PersonIdentity & { position: string; roles: { roleCode: string }[] },
  t: (key: string) => string,
): string {
  const roles = person.roles.map((role) => {
    const key = `designProjects.team.role.${role.roleCode}`;
    const label = t(key);
    return label === key ? role.roleCode : label;
  });
  return [...new Set([personRole(person, t), person.position, ...roles].filter(Boolean))].join(
    " · ",
  );
}

export function mergePeople<T extends { userId: number }>(people: T[]): T[] {
  const merged = new Map<number, T>();
  for (const person of people)
    merged.set(person.userId, { ...merged.get(person.userId), ...person });
  return Array.from(merged.values());
}

export function personOption(
  person: PersonIdentity & {
    id?: number;
    userId?: number;
    fullName?: string | null;
    name?: string;
    userName?: string;
    email?: string | null;
  },
  t: (key: string) => string,
): SearchableSelectOption {
  const role = personRole(person, t);
  const hasDepartmentInformation =
    person.departmentId !== undefined || person.roleGroupId !== undefined;
  const department = person.departmentLabelKey
    ? t(person.departmentLabelKey)
    : t(hasDepartmentInformation ? "people.departmentUnassigned" : "people.departmentUnknown");
  return {
    value: String(person.userId ?? person.id),
    label:
      person.fullName ||
      person.name ||
      person.userName ||
      person.email ||
      `#${person.userId ?? person.id}`,
    hint: `${role} · ${department}`,
    keywords: `${person.email ?? ""} ${person.roleCode ?? person.role ?? ""}`,
    department: {
      value: String(
        person.departmentId ??
          person.roleGroupId ??
          (hasDepartmentInformation ? "ungrouped" : "unknown"),
      ),
      label: department,
    },
  };
}
