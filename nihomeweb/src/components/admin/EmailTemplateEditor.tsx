import { useEffect, useRef, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { Eye, Maximize2, Minimize2, RotateCcw, Save } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useI18n } from "@/lib/i18n";

export function EmailTemplateActions({
  container,
  busy,
  saving,
  canSave,
  onReset,
  onSave,
}: {
  container: HTMLDivElement | null;
  busy: boolean;
  saving: boolean;
  canSave: boolean;
  onReset: () => void;
  onSave: () => void;
}) {
  const { t } = useI18n();
  if (!container) return null;
  return createPortal(
    <>
      <Button type="button" variant="outline" onClick={onReset} disabled={busy}>
        <RotateCcw className="mr-1.5 h-4 w-4" /> {t("emailTemplate.editor.reset")}
      </Button>
      <Button type="button" onClick={onSave} disabled={busy || !canSave}>
        <Save className="mr-1.5 h-4 w-4" />
        {t(saving ? "common.saving" : "emailTemplate.editor.save")}
      </Button>
    </>,
    container,
  );
}

export default function EmailTemplateEditor({
  id,
  subject,
  body,
  onSubjectChange,
  onBodyChange,
  tokens,
  disabled = false,
  subjectMaxLength = 200,
  bodyMaxLength,
  hint,
  beforeSubject,
  previewTitle = "Email preview",
}: {
  id: string;
  subject: string;
  body: string;
  onSubjectChange: (value: string) => void;
  onBodyChange: (value: string) => void;
  tokens: { token: string; desc: string }[];
  disabled?: boolean;
  subjectMaxLength?: number;
  bodyMaxLength?: number;
  hint?: string;
  beforeSubject?: ReactNode;
  previewTitle?: string;
}) {
  const { t } = useI18n();
  const [fullscreen, setFullscreen] = useState(false);
  const bodyRef = useRef<HTMLTextAreaElement>(null);
  const subjectRef = useRef<HTMLInputElement>(null);
  const targetRef = useRef<"subject" | "body">("body");

  useEffect(() => {
    if (!fullscreen) return;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setFullscreen(false);
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [fullscreen]);

  const insertToken = (token: string) => {
    const isSubject = targetRef.current === "subject";
    const field = isSubject ? subjectRef.current : bodyRef.current;
    if (!field) return;
    const value = isSubject ? subject : body;
    const start = field.selectionStart ?? value.length;
    const end = field.selectionEnd ?? value.length;
    const nextValue = value.slice(0, start) + token + value.slice(end);
    const maxLength = isSubject ? subjectMaxLength : bodyMaxLength;
    if (maxLength !== undefined && nextValue.length > maxLength) return;
    (isSubject ? onSubjectChange : onBodyChange)(nextValue);
    requestAnimationFrame(() => {
      field.focus();
      field.setSelectionRange(start + token.length, start + token.length);
    });
  };

  return (
    <div className="grid min-w-0 grid-cols-1 gap-4 xl:grid-cols-2">
      <div className="min-w-0 space-y-4">
        {beforeSubject}
        <div className="space-y-1.5">
          <Label className="text-xs" htmlFor={`${id}-subject`}>
            {t("emailTemplate.editor.subject")}
          </Label>
          <Input
            ref={subjectRef}
            id={`${id}-subject`}
            value={subject}
            onChange={(event) => onSubjectChange(event.target.value)}
            onFocus={() => (targetRef.current = "subject")}
            maxLength={subjectMaxLength}
            disabled={disabled}
          />
        </div>
        <div className="space-y-1.5">
          <Label className="text-xs" htmlFor={`${id}-body`}>
            {t("emailTemplate.editor.body")}
          </Label>
          <Textarea
            ref={bodyRef}
            id={`${id}-body`}
            className="font-mono text-xs"
            rows={18}
            value={body}
            onChange={(event) => onBodyChange(event.target.value)}
            onFocus={() => (targetRef.current = "body")}
            maxLength={bodyMaxLength}
            disabled={disabled}
            aria-describedby={hint ? `${id}-hint` : undefined}
          />
          {hint && (
            <p id={`${id}-hint`} className="text-xs text-muted-foreground">
              {hint}
            </p>
          )}
        </div>
        <div className="rounded-lg border bg-muted/50 p-4">
          <p className="mb-3 text-xs font-medium uppercase tracking-wide text-muted-foreground">
            {t("emailTemplate.editor.tokens")}
          </p>
          <div className="flex flex-wrap gap-2">
            {tokens.map(({ token, desc }) => (
              <button
                key={token}
                type="button"
                disabled={disabled}
                onClick={() => insertToken(token)}
                className="inline-flex max-w-full flex-wrap items-center gap-1.5 rounded-md border bg-background px-2.5 py-1.5 text-left text-xs transition hover:border-primary hover:text-primary disabled:pointer-events-none disabled:opacity-50"
                title={desc}
              >
                <code className="break-all font-mono">{token}</code>
                <span className="text-muted-foreground">– {desc}</span>
              </button>
            ))}
          </div>
        </div>
      </div>
      <div
        className={
          fullscreen ? "fixed inset-0 z-50 overflow-auto bg-background p-4 sm:p-6" : "min-w-0"
        }
      >
        <div className="mb-3 flex items-center justify-between gap-2">
          <div className="flex items-center gap-2 text-sm font-medium">
            <Eye className="h-4 w-4" /> {t("emailTemplate.editor.preview")}
          </div>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => setFullscreen(!fullscreen)}
            aria-expanded={fullscreen}
          >
            {fullscreen ? (
              <Minimize2 className="mr-1.5 h-3.5 w-3.5" />
            ) : (
              <Maximize2 className="mr-1.5 h-3.5 w-3.5" />
            )}
            {t(fullscreen ? "emailTemplate.editor.minimize" : "emailTemplate.editor.fullscreen")}
          </Button>
        </div>
        <div className="overflow-hidden rounded-lg border bg-white">
          <div className="truncate border-b bg-muted/50 px-4 py-2 font-mono text-xs text-muted-foreground">
            {t("emailTemplate.editor.subject")}: {subject}
          </div>
          <iframe
            title={previewTitle}
            srcDoc={body}
            className="w-full border-0 bg-white"
            style={{ height: fullscreen ? "calc(100dvh - 140px)" : "500px" }}
            sandbox=""
          />
        </div>
      </div>
    </div>
  );
}
