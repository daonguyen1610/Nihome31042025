import { useRef, useState } from "react";
import { Trash2, Upload, Plus } from "lucide-react";
import { adminApi } from "@/services/adminApi";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

interface GalleryEditorProps {
  items: string[];
  onChange: (items: string[]) => void;
  folder?: string;
}

const GalleryEditor = ({ items, onChange, folder }: GalleryEditorProps) => {
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
        const res = await adminApi.uploadImage(file, undefined, folder);
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
      <Button
        type="button"
        size="sm"
        variant="outline"
        onClick={() => inputRef.current?.click()}
        disabled={uploading}
      >
        <Upload className="mr-1.5 h-3.5 w-3.5" />
        {uploading ? t("media.gallery.uploading") : t("media.gallery.uploadMulti")}
      </Button>
      <input
        ref={inputRef}
        type="file"
        accept="image/*"
        multiple
        className="hidden"
        onChange={(e) => handleFiles(e.target.files)}
      />

      <details>
        <summary className="cursor-pointer select-none text-xs text-muted-foreground hover:text-foreground">
          {t("media.url.toggle")}
        </summary>
        <div className="mt-2 flex items-center gap-1">
          <Input
            className="h-9 flex-1"
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
          <Button
            type="button"
            size="icon"
            variant="outline"
            className="h-9 w-9"
            onClick={addManual}
            aria-label={t("media.url.add")}
          >
            <Plus className="h-4 w-4" />
          </Button>
        </div>
      </details>

      {items.length === 0 ? (
        <p className="text-xs italic text-muted-foreground">{t("media.gallery.empty")}</p>
      ) : (
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
          {items.map((url, idx) => (
            <div key={`${url}-${idx}`} className="group relative aspect-square overflow-hidden rounded-lg border bg-muted">
              <img
                src={url}
                alt=""
                className="h-full w-full object-cover"
                onError={(e) => ((e.target as HTMLImageElement).src = "/placeholder.svg")}
              />
              <Button
                type="button"
                size="icon"
                variant="secondary"
                className="absolute right-1 top-1 h-7 w-7 bg-white/90 text-destructive opacity-0 shadow transition group-hover:opacity-100 hover:bg-white hover:text-destructive"
                onClick={() => remove(idx)}
                aria-label={t("media.gallery.delete")}
              >
                <Trash2 className="h-3.5 w-3.5" />
              </Button>
              <span className="absolute bottom-1 left-1 rounded bg-black/60 px-1.5 py-0.5 text-[10px] text-white">
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
