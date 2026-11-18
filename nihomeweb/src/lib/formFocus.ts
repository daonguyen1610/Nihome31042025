/**
 * Moves focus to the control of a form field marked `data-form-field="<field>"`
 * in the open dialog, so a user who pressed save sees what needs attention.
 */
export function focusFormField(field: string) {
  requestAnimationFrame(() =>
    document
      .querySelector<HTMLElement>(
        `[role="dialog"] [data-form-field="${field}"] :is(input, textarea, button, [role="combobox"])`,
      )
      ?.focus(),
  );
}
