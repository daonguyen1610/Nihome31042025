import { useRef } from "react";
import { Upload, Image as ImageIcon, X } from "lucide-react";
import { useI18n } from "@/lib/i18n";

interface FeaturedImageUploaderProps {
  imageUrl: string;
  pendingPreview: string | null;
  pendingFileName?: string;
  onUrlChange: (url: string) => void;
  onFileSelected: (file: File) => void;
  onClearPending?: () => void;
  disabled?: boolean;
}

const FeaturedImageUploader = ({
  imageUrl,
  pendingPreview,
  pendingFileName,
  onUrlChange,
  onFileSelected,
  onClearPending,
  disabled,
}: FeaturedImageUploaderProps) => {
  const { t } = useI18n();
  const inputRef = useRef<HTMLInputElement | null>(null);
  const displayUrl = pendingPreview ?? imageUrl;
  const hasImage = Boolean(displayUrl);

  const handleClick = () => inputRef.current?.click();

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    onFileSelected(file);
    e.target.value = "";
  };

  return (
    <div className="space-y-3">
      <input
        ref={inputRef}
        type="file"
        accept="image/*"
        className="hidden"
        onChange={handleChange}
        disabled={disabled}
      />

      {hasImage ? (
        <div className="relative aspect-[16/10] rounded-xl overflow-hidden bg-muted border" style={{ borderColor: "hsl(var(--admin-border))" }}>
          <img
            src={displayUrl}
            alt=""
            className="w-full h-full object-cover"
            onError={(e) => ((e.target as HTMLImageElement).src = "/placeholder.svg")}
          />
          {pendingPreview && (
            <span className="absolute top-2 left-2 text-[10px] px-2 py-1 rounded bg-amber-500 text-white font-bold uppercase tracking-wider">
              {t("media.featured.pendingBadge")}
            </span>
          )}
          <div className="absolute inset-x-0 bottom-0 p-2 flex gap-2 bg-gradient-to-t from-black/60 to-transparent">
            <button
              type="button"
              onClick={handleClick}
              disabled={disabled}
              className="flex-1 inline-flex items-center justify-center gap-1.5 px-3 py-1.5 text-xs font-bold rounded-md bg-white text-black hover:bg-white/90 disabled:opacity-50"
            >
              <Upload className="w-3.5 h-3.5" />
              {pendingPreview ? t("media.featured.changeAnother") : t("media.featured.change")}
            </button>
            {pendingPreview && onClearPending && (
              <button
                type="button"
                onClick={onClearPending}
                disabled={disabled}
                className="inline-flex items-center justify-center px-2 py-1.5 text-xs font-bold rounded-md bg-white/90 text-red-600 hover:bg-white"
                aria-label={t("media.featured.cancelPending")}
              >
                <X className="w-3.5 h-3.5" />
              </button>
            )}
          </div>
        </div>
      ) : (
        <button
          type="button"
          onClick={handleClick}
          disabled={disabled}
          className="w-full aspect-[16/10] rounded-xl border-2 border-dashed flex flex-col items-center justify-center gap-2 hover:bg-muted/50 transition disabled:opacity-50"
          style={{ borderColor: "hsl(var(--admin-border))" }}
        >
          <ImageIcon className="w-8 h-8 text-muted-foreground" />
          <span className="text-sm font-bold">{t("media.featured.empty.title")}</span>
          <span className="text-xs text-muted-foreground">{t("media.featured.empty.hint")}</span>
        </button>
      )}

      {pendingFileName && (
        <p className="text-xs text-amber-600 font-medium">
          {t("media.featured.pendingHint").replace("{name}", pendingFileName)}
        </p>
      )}

      <details className="group">
        <summary className="text-xs cursor-pointer text-muted-foreground hover:text-foreground select-none">
          {t("media.url.toggle")}
        </summary>
        <input
          className="admin-input mt-2"
          value={imageUrl}
          onChange={(e) => onUrlChange(e.target.value)}
          placeholder={t("media.url.placeholder")}
        />
      </details>
    </div>
  );
};

export default FeaturedImageUploader;
