import {
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type ClipboardEvent,
  type KeyboardEvent,
} from "react";
import { X } from "lucide-react";
import { cn } from "@/lib/utils";
import { isValidEmail } from "@/lib/validation";

export interface EmailContact {
  name: string;
  email: string;
  /** Second line in the suggestion, e.g. department. */
  hint?: string;
}

// Addresses never contain spaces, so whitespace separates them too (a single-line
// field turns pasted or filled line breaks into spaces).
const SEPARATORS = /[;,\s]+/;
const MAX_SUGGESTIONS = 8;

export const splitEmails = (value: string) =>
  value
    .split(SEPARATORS)
    .map((address) => address.trim())
    .filter(Boolean);

// Lets "trang" find "Trang" and "Nguyễn" match "nguyen", the way people type names.
const fold = (text: string) =>
  text.normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/đ/g, "d").replace(/Đ/g, "D").toLowerCase();

/**
 * Address field that works like a mail client's To/CC line: each address is a
 * chip, typing suggests matching NICON users, and any valid address can still
 * be typed by hand. The value stays a comma-separated string.
 */
export function EmailRecipientsInput({
  id,
  value,
  onChange,
  contacts,
  disabled,
  placeholder,
  ariaDescribedBy,
  invalid,
  removeLabel,
  suggestionsLabel,
}: {
  id: string;
  value: string;
  onChange: (value: string) => void;
  contacts: EmailContact[];
  disabled?: boolean;
  placeholder?: string;
  ariaDescribedBy?: string;
  invalid?: boolean;
  /** Accessible name of a chip's remove button, given the address. */
  removeLabel: (address: string) => string;
  suggestionsLabel: string;
}) {
  const listId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [draft, setDraft] = useState("");
  const [active, setActive] = useState(0);
  const [open, setOpen] = useState(false);
  // Chips. An address still being typed is not a chip yet but already counts in
  // the value, so the form validates and previews it without the field having
  // to change shape when focus leaves it (which made the next click miss).
  const [addresses, setAddresses] = useState(() => splitEmails(value));
  const emitted = useRef(value);
  useEffect(() => {
    if (value === emitted.current) return;
    emitted.current = value;
    setAddresses(splitEmails(value));
    setDraft("");
  }, [value]);
  const emit = (chips: string[], typed: string) => {
    const taken = new Set(chips.map((address) => address.toLowerCase()));
    // A name typed to search people is not an address; only "@" text counts.
    const pending = splitEmails(typed).filter(
      (address) => address.includes("@") && !taken.has(address.toLowerCase()),
    );
    const next = [...chips, ...pending].join(", ");
    emitted.current = next;
    if (next !== value) onChange(next);
  };
  const byEmail = useMemo(
    () => new Map(contacts.map((contact) => [contact.email.toLowerCase(), contact])),
    [contacts],
  );

  const suggestions = useMemo(() => {
    const query = fold(draft.trim());
    if (!query) return [];
    const taken = new Set(addresses.map((address) => address.toLowerCase()));
    return contacts
      .filter(
        (contact) =>
          !taken.has(contact.email.toLowerCase()) &&
          (fold(contact.name).includes(query) || fold(contact.email).includes(query)),
      )
      .slice(0, MAX_SUGGESTIONS);
  }, [addresses, contacts, draft]);
  const listOpen = open && suggestions.length > 0;

  const add = (incoming: string[], rest = "") => {
    const seen = new Set(addresses.map((address) => address.toLowerCase()));
    const next = [...addresses];
    for (const address of incoming) {
      if (seen.has(address.toLowerCase())) continue;
      seen.add(address.toLowerCase());
      next.push(address);
    }
    setAddresses(next);
    setDraft(rest);
    setActive(0);
    emit(next, rest);
  };
  const commitDraft = () => {
    const typed = splitEmails(draft);
    if (typed.length) add(typed);
    else setDraft("");
  };
  const remove = (index: number) => {
    const next = addresses.filter((_, position) => position !== index);
    setAddresses(next);
    emit(next, draft);
    inputRef.current?.focus();
  };

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (listOpen && (event.key === "ArrowDown" || event.key === "ArrowUp")) {
      event.preventDefault();
      const step = event.key === "ArrowDown" ? 1 : -1;
      setActive((current) => (current + step + suggestions.length) % suggestions.length);
      return;
    }
    if ((event.key === "Enter" || event.key === "Tab") && listOpen) {
      event.preventDefault();
      add([suggestions[Math.min(active, suggestions.length - 1)].email]);
      return;
    }
    if (event.key === "Enter" || event.key === "," || event.key === ";") {
      if (!draft.trim()) {
        if (event.key !== "Enter") event.preventDefault();
        return;
      }
      event.preventDefault();
      commitDraft();
      return;
    }
    if (event.key === "Backspace" && !draft && addresses.length) {
      event.preventDefault();
      remove(addresses.length - 1);
      return;
    }
    if (event.key === "Escape" && listOpen) {
      // Close the suggestions, not the dialog around the field.
      event.stopPropagation();
      setOpen(false);
    }
  };

  const onPaste = (event: ClipboardEvent<HTMLInputElement>) => {
    const pasted = event.clipboardData.getData("text");
    if (!SEPARATORS.test(pasted)) return;
    event.preventDefault();
    add(splitEmails(`${draft}${pasted}`));
  };

  return (
    <div className="relative">
      <div
        className={cn(
          "flex min-h-10 w-full cursor-text flex-wrap items-center gap-1.5 rounded-md border border-input bg-background px-2 py-1.5 ring-offset-background focus-within:ring-2 focus-within:ring-ring focus-within:ring-offset-2",
          invalid && "border-destructive",
          disabled && "cursor-not-allowed opacity-50",
        )}
        onClick={() => inputRef.current?.focus()}
      >
        {addresses.map((address, index) => {
          const contact = byEmail.get(address.toLowerCase());
          const valid = isValidEmail(address);
          return (
            <span
              key={`${address}-${index}`}
              title={address}
              data-testid="email-recipient-chip"
              className={cn(
                "inline-flex max-w-full items-center gap-1 rounded-md border bg-muted px-2 py-0.5 text-sm",
                !valid && "border-destructive bg-destructive/10 text-destructive",
              )}
            >
              <span className="truncate">{contact?.name ?? address}</span>
              <button
                type="button"
                disabled={disabled}
                aria-label={removeLabel(address)}
                className="rounded-sm p-0.5 text-muted-foreground hover:bg-background hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                onClick={(event) => {
                  event.stopPropagation();
                  remove(index);
                }}
              >
                <X className="h-3 w-3" aria-hidden />
              </button>
            </span>
          );
        })}
        <input
          ref={inputRef}
          id={id}
          type="text"
          role="combobox"
          autoComplete="off"
          aria-autocomplete="list"
          aria-expanded={listOpen}
          aria-controls={listId}
          aria-activedescendant={listOpen ? `${listId}-${active}` : undefined}
          aria-describedby={ariaDescribedBy}
          aria-invalid={invalid ? true : undefined}
          disabled={disabled}
          value={draft}
          placeholder={addresses.length ? undefined : placeholder}
          className="min-w-[12rem] flex-1 bg-transparent px-1 py-0.5 text-base outline-none placeholder:text-muted-foreground disabled:cursor-not-allowed md:text-sm"
          onChange={(event) => {
            // A separator ends an address however it arrives: typed on a phone
            // keyboard that reports no key, autocompleted or dropped in.
            const text = event.target.value;
            const separator = Math.max(
              text.lastIndexOf(","),
              text.lastIndexOf(";"),
              text.lastIndexOf("\n"),
            );
            // A space ends an address only once one was typed, so searching
            // "Nguyễn Thị" by name keeps working.
            const space = text.search(/\s\S*$/);
            const end = space > separator && text.slice(0, space).includes("@") ? space : separator;
            if (end >= 0) {
              add(splitEmails(text.slice(0, end)), text.slice(end + 1).trimStart());
            } else {
              setDraft(text);
              setActive(0);
              emit(addresses, text);
            }
            setOpen(true);
          }}
          onKeyDown={onKeyDown}
          onPaste={onPaste}
          onFocus={() => setOpen(true)}
          onBlur={() => setOpen(false)}
        />
      </div>
      {listOpen && (
        <ul
          id={listId}
          role="listbox"
          aria-label={suggestionsLabel}
          className="absolute left-0 right-0 z-50 mt-1 max-h-64 overflow-y-auto rounded-md border bg-popover p-1 text-popover-foreground shadow-md"
        >
          {suggestions.map((contact, index) => (
            <li
              key={contact.email}
              id={`${listId}-${index}`}
              role="option"
              aria-selected={index === active}
              className={cn(
                "cursor-pointer rounded-sm px-2 py-1.5 text-sm",
                index === active && "bg-accent text-accent-foreground",
              )}
              // Keep focus in the field so the click lands before blur commits the draft.
              onMouseDown={(event) => event.preventDefault()}
              onMouseEnter={() => setActive(index)}
              onClick={() => add([contact.email])}
            >
              <div className="truncate font-medium">{contact.name}</div>
              <div className="truncate text-xs text-muted-foreground">
                {[contact.email, contact.hint].filter(Boolean).join(" · ")}
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
