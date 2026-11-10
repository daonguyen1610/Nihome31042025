interface ProjectOption {
  id: number;
}

export function selectAccessibleProjectId(
  requestedProjectId: number,
  projects: ProjectOption[],
): number | null {
  if (
    Number.isInteger(requestedProjectId) &&
    requestedProjectId > 0 &&
    projects.some((project) => project.id === requestedProjectId)
  ) {
    return requestedProjectId;
  }
  return projects[0]?.id ?? null;
}

export function selectDesignProjectId(
  requestedDesignProjectId: number,
  defaultDesignProjectId: number | null,
  projects: ProjectOption[],
): number | null {
  if (
    Number.isInteger(requestedDesignProjectId) &&
    requestedDesignProjectId > 0 &&
    projects.some((project) => project.id === requestedDesignProjectId)
  ) {
    return requestedDesignProjectId;
  }
  if (
    defaultDesignProjectId != null &&
    projects.some((project) => project.id === defaultDesignProjectId)
  ) {
    return defaultDesignProjectId;
  }
  return projects[0]?.id ?? null;
}
