import { useRef, useState } from "react";
import { ArrowDown, ArrowUp, ImagePlus, Plus, Trash2, Type, Upload } from "lucide-react";
import { adminApi } from "@/services/adminApi";
import type { ContentBlock, ContentItem } from "@/services/contentApi";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";

interface ContentBlockEditorProps {
  value: ContentItem[];
  onChange: (value: ContentItem[]) => void;
}

const normalizeBlock = (item: ContentItem): ContentBlock =>
  typeof item === "string" ? { type: "text", value: item } : item;

const ContentBlockEditor = ({ value, onChange }: ContentBlockEditorProps) => {
  const { t } = useI18n();
  const { toast } = useToast();
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [uploadTarget, setUploadTarget] = useState<number | null>(null);
  const [uploading, setUploading] = useState(false);

  const blocks = value.map(normalizeBlock);

  const updateBlocks = (next: ContentBlock[]) => onChange(next);

  const updateBlock = (index: number, block: ContentBlock) => {
    updateBlocks(blocks.map((item, i) => (i === index ? block : item)));
  };

  const addText = () => updateBlocks([...blocks, { type: "text", value: "" }]);
  const addImage = () => updateBlocks([...blocks, { type: "image", url: "" }]);

  const remove = (index: number) => {
    updateBlocks(blocks.filter((_, i) => i !== index));
  };

  const move = (index: number, direction: -1 | 1) => {
    const nextIndex = index + direction;
    if (nextIndex < 0 || nextIndex >= blocks.length) return;
    const next = [...blocks];
    [next[index], next[nextIndex]] = [next[nextIndex], next[index]];
    updateBlocks(next);
  };

  const openUpload = (index: number) => {
    setUploadTarget(index);
    fileInputRef.current?.click();
  };

  const handleUpload = async (file: File | undefined) => {
    if (!file || uploadTarget == null) return;
    setUploading(true);
    try {
      const previous = blocks[uploadTarget]?.type === "image" ? blocks[uploadTarget].url : undefined;
      const res = await adminApi.uploadImage(file, previous || undefined);
      updateBlock(uploadTarget, { type: "image", url: res.data.imageUrl });
      toast({ title: t("form.updated"), description: file.name });
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setUploading(false);
      setUploadTarget(null);
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  };

  return (
    <div className="space-y-3">
      <input
        ref={fileInputRef}
        type="file"
        accept="image/*"
        className="hidden"
        onChange={(event) => handleUpload(event.target.files?.[0])}
      />

      {blocks.length === 0 ? (
        <p className="text-xs text-muted-foreground italic">{t("contentBlocks.empty")}</p>
      ) : (
        blocks.map((block, index) => (
          <div
            key={`${block.type}-${index}`}
            className="rounded-xl border p-3 space-y-2"
            style={{ borderColor: "hsl(var(--admin-border))" }}
          >
            <div className="flex items-center gap-1 justify-between">
              <span className="inline-flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider" style={{ color: "hsl(var(--admin-muted))" }}>
                {block.type === "text" ? <Type className="w-3.5 h-3.5" /> : <ImagePlus className="w-3.5 h-3.5" />}
                {block.type === "text" ? t("contentBlocks.text") : t("contentBlocks.image")}
              </span>
              <div className="flex items-center gap-1">
                <button type="button" className="p-1.5 rounded-md hover:bg-muted" onClick={() => move(index, -1)} disabled={index === 0} aria-label={t("contentBlocks.moveUp")}>
                  <ArrowUp className="w-3.5 h-3.5" />
                </button>
                <button type="button" className="p-1.5 rounded-md hover:bg-muted" onClick={() => move(index, 1)} disabled={index === blocks.length - 1} aria-label={t("contentBlocks.moveDown")}>
                  <ArrowDown className="w-3.5 h-3.5" />
                </button>
                <button type="button" className="p-1.5 rounded-md hover:bg-muted text-red-600" onClick={() => remove(index)} aria-label={t("common.delete")}>
                  <Trash2 className="w-3.5 h-3.5" />
                </button>
              </div>
            </div>

            {block.type === "text" ? (
              <textarea
                className="admin-input min-h-28"
                value={block.value}
                onChange={(event) => updateBlock(index, { type: "text", value: event.target.value })}
              />
            ) : (
              <div className="space-y-2">
                {block.url && (
                  <img src={block.url} alt="" className="w-full aspect-video rounded-lg object-cover bg-muted" />
                )}
                <div className="flex items-center gap-2">
                  <input
                    className="admin-input flex-1"
                    value={block.url}
                    onChange={(event) => updateBlock(index, { type: "image", url: event.target.value })}
                    placeholder={t("media.url.placeholder")}
                  />
                  <button
                    type="button"
                    onClick={() => openUpload(index)}
                    disabled={uploading}
                    className="inline-flex items-center gap-2 px-3 py-2 text-xs font-bold border rounded-lg hover:bg-muted disabled:opacity-50"
                    style={{ borderColor: "hsl(var(--admin-border))" }}
                  >
                    <Upload className="w-3.5 h-3.5" />
                    {t("media.url.upload")}
                  </button>
                </div>
              </div>
            )}
          </div>
        ))
      )}

      <div className="flex flex-wrap gap-2">
        <button type="button" className="inline-flex items-center gap-2 px-3 py-2 text-xs font-bold border rounded-lg hover:bg-muted" style={{ borderColor: "hsl(var(--admin-border))" }} onClick={addText}>
          <Plus className="w-3.5 h-3.5" />
          {t("contentBlocks.addText")}
        </button>
        <button type="button" className="inline-flex items-center gap-2 px-3 py-2 text-xs font-bold border rounded-lg hover:bg-muted" style={{ borderColor: "hsl(var(--admin-border))" }} onClick={addImage}>
          <Plus className="w-3.5 h-3.5" />
          {t("contentBlocks.addImage")}
        </button>
      </div>
    </div>
  );
};

export default ContentBlockEditor;
