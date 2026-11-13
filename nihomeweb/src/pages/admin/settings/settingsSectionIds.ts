export const SETTINGS_SECTION_ID_PREFIX = "settings-section-";

export const settingsSectionId = (key: string) => `${SETTINGS_SECTION_ID_PREFIX}${key}`;

export const settingsSectionDescriptionId = (key: string) =>
  `${settingsSectionId(key)}-description`;
