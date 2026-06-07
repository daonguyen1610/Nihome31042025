import { useRef, useState } from "react";
import { Trash2, Upload, Plus } from "lucide-react";
import { adminApi } from "@/services/adminApi";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";

interface GalleryEditorProps {
  items: string[];
  onChange: (items: string[]) => void;
}

const GalleryEditor = ({ items, onChange }: GalleryEditorProps) => {
  const { t } = useI18n();
  const { toast } = useToast();
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [uploading, setUploading] = useState(false);
  const [manualUrl, setManualUrl] = useState("");

  const handleFiles = async (files: FileList | null) => {
    if (!files || files.length === 0) return;
    setUploading(true);
    try {
      const uploaded: string[] = [];
      for (const file of Array.from(files)) {
        const res = await adminApi.uploadImage(file);
        uploaded.push(res.data.imageUrl);
      }
      onChange([...items, ...uploaded]);
      toast({ title: t("form.updated"), description: t("media.gallery.countToast").replace("{count}", String(uploaded.length)) });
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setUploading(false);
      if (inputRef.current) inputRef.current.value = "";
    }
  };

  const remove = (index: number) => {
    onChange(items.filter((_, i) => i !== index));
  };

  const addManual = () => {
    const url = manualUrl.trim();
    if (!url) return;
    onChange([...items, url]);
    setManualUrl("");
  };

  return (
    <div className="space-y-3">
      <button
        type="button"
        onClick={() => inputRef.current?.click()}
        disabled={uploading}
        className="inline-flex items-center gap-2 px-3 py-2 text-xs font-bold border rounded-lg hover:bg-muted disabled:opacity-50"
        style={{ borderColor: "hsl(var(--admin-border))" }}
      >
        <Upload className="w-3.5 h-3.5" />
        {uploading ? t("media.gallery.uploading") : t("media.gallery.uploadMulti")}
      </button>
      <input
        ref={inputRef}
        type="file"
        accept="image/*"
        multiple
        className="hidden"
        onChange={(e) => handleFiles(e.target.files)}
      />

      <details>
        <summary className="text-xs cursor-pointer text-muted-foreground hover:text-foreground select-none">
          {t("media.url.toggle")}
        </summary>
        <div className="flex items-center gap-1 mt-2">
          <input
            className="admin-input flex-1"
            value={manualUrl}
            onChange={(e) => setManualUrl(e.target.value)}
            placeholder={t("media.url.placeholder")}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                e.preventDefault();
                addManual();
              }
            }}
          />
          <button
            type="button"
            onClick={addManual}
            className="p-2 border rounded-lg hover:bg-muted"
            style={{ borderColor: "hsl(var(--admin-border))" }}
            aria-label={t("media.url.add")}
          >
            <Plus className="w-4 h-4" />
          </button>
        </div>
      </details>

      {items.length === 0 ? (
        <p className="text-xs text-muted-foreground italic">{t("media.gallery.empty")}</p>
      ) : (
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
          {items.map((url, idx) => (
            <div key={`${url}-${idx}`} className="relative group aspect-square rounded-lg overflow-hidden bg-muted border" style={{ borderColor: "hsl(var(--admin-border))" }}>
              <img
                src={url}
                alt=""
                className="w-full h-full object-cover"
                onError={(e) => ((e.target as HTMLImageElement).src = "/placeholder.svg")}
              />
              <button
                type="button"
                onClick={() => remove(idx)}
                className="absolute top-1 right-1 p-1.5 rounded-md bg-white/90 text-red-600 opacity-0 group-hover:opacity-100 transition shadow"
                aria-label={t("media.gallery.delete")}
              >
                <Trash2 className="w-3.5 h-3.5" />
              </button>
              <span className="absolute bottom-1 left-1 text-[10px] px-1.5 py-0.5 rounded bg-black/60 text-white">
                #{idx + 1}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default GalleryEditor;
