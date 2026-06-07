import { useRef, useState } from "react";
import { Trash2, Upload } from "lucide-react";
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
      toast({ title: t("form.updated"), description: `${uploaded.length} ảnh` });
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
        {uploading ? "Đang tải..." : "Tải nhiều ảnh"}
      </button>
      <input
        ref={inputRef}
        type="file"
        accept="image/*"
        multiple
        className="hidden"
        onChange={(e) => handleFiles(e.target.files)}
      />

      {items.length === 0 ? (
        <p className="text-xs text-muted-foreground italic">Chưa có ảnh nào. Bấm "Tải nhiều ảnh" để thêm.</p>
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
                aria-label="Xóa"
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
