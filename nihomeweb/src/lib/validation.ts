/**
 * Phone and email format rules for CRM contact fields.
 *
 * Mirrors `nihomebackend/Services/ContactValidation.cs` — the backend is the
 * authority, this exists so the user is told before the request goes out. Change
 * one and change the other.
 */

/** Vietnamese numbers with or without the country code, once separators are gone. */
const PHONE_SHAPE = /^(?:\+?84|0)\d{8,10}$/;

/**
 * Stricter than a bare "has an @": the domain needs a dot with a label on each
 * side, which is what rules out values like "345@434".
 */
const EMAIL_SHAPE = /^[^@\s]+@[^@\s.]+(?:\.[^@\s.]+)+$/;

/** Strips the separators people type, leaving digits and a leading +. */
export const normalizePhone = (phone: string): string => phone.replace(/[\s.\-()]/g, "");

/** Blank counts as valid — presence is a separate rule from shape. */
export const isValidPhone = (phone?: string | null): boolean => {
  if (!phone || !phone.trim()) return true;
  return PHONE_SHAPE.test(normalizePhone(phone.trim()));
};

/** Blank counts as valid — presence is a separate rule from shape. */
export const isValidEmail = (email?: string | null): boolean => {
  if (!email || !email.trim()) return true;
  const trimmed = email.trim();
  return trimmed.length <= 150 && EMAIL_SHAPE.test(trimmed);
};

export type ContactIssue = "missing" | "phone" | "email" | null;

/**
 * The rule every CRM contact shares: at least one way to reach the person, and
 * whatever was supplied has to be well formed.
 *
 * Returns which rule broke so the caller can point at the right field and use
 * its own translated message.
 */
export const validateContact = (
  phone?: string | null,
  email?: string | null,
): ContactIssue => {
  const hasPhone = Boolean(phone && phone.trim());
  const hasEmail = Boolean(email && email.trim());
  if (!hasPhone && !hasEmail) return "missing";
  if (!isValidPhone(phone)) return "phone";
  if (!isValidEmail(email)) return "email";
  return null;
};
