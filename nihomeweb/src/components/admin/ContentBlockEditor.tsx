import { useRef, useState } from "react";
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from "@dnd-kit/core";
import {
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
  arrayMove,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { GripVertical, ImagePlus, Plus, Trash2, Type, Upload, Youtube } from "lucide-react";
import { adminApi, type UploadBucket } from "@/services/adminApi";
import type { ContentBlock, ContentItem } from "@/services/contentApi";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";

interface ContentBlockEditorProps {
  value: ContentItem[];
  onChange: (value: ContentItem[]) => void;
  category?: UploadBucket;
}

const normalizeBlock = (item: ContentItem): ContentBlock =>
  typeof item === "string" ? { type: "text", value: item } : item;

interface SortableBlockProps {
  id: string;
  children: (dragHandleProps: React.HTMLAttributes<HTMLButtonElement>) => React.ReactNode;
}

const SortableBlock = ({ id, children }: SortableBlockProps) => {
  const { t } = useI18n();
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id });
  const style: React.CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
  };
  return (
    <div ref={setNodeRef} style={style}>
      {children({ ...attributes, ...listeners, type: "button", "aria-label": t("contentBlocks.dragToReorder") } as React.HTMLAttributes<HTMLButtonElement>)}
    </div>
  );
};

const ContentBlockEditor = ({ value, onChange, category }: ContentBlockEditorProps) => {
  const { t } = useI18n();
  const { toast } = useToast();
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [uploadTarget, setUploadTarget] = useState<number | null>(null);
  const [uploading, setUploading] = useState(false);

  const blocks = value.map(normalizeBlock);
  const blockIds = blocks.map((_, i) => `block-${i}`);

  const updateBlocks = (next: ContentBlock[]) => onChange(next);

  const updateBlock = (index: number, block: ContentBlock) => {
    updateBlocks(blocks.map((item, i) => (i === index ? block : item)));
  };

  const addText = () => updateBlocks([...blocks, { type: "text", value: "" }]);
  const addImage = () => updateBlocks([...blocks, { type: "image", url: "" }]);
  const addYoutube = () => updateBlocks([...blocks, { type: "youtube", url: "" }]);

  const remove = (index: number) => {
    updateBlocks(blocks.filter((_, i) => i !== index));
  };

  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;
    if (over && active.id !== over.id) {
      const oldIndex = blockIds.indexOf(active.id as string);
      const newIndex = blockIds.indexOf(over.id as string);
      updateBlocks(arrayMove(blocks, oldIndex, newIndex));
    }
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
      const res = await adminApi.uploadImage(file, previous || undefined, category);
      const existingBlock = blocks[uploadTarget];
      const caption = existingBlock?.type === "image" ? existingBlock.caption : undefined;
      updateBlock(uploadTarget, { type: "image", url: res.data.imageUrl, caption });
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
        <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
          <SortableContext items={blockIds} strategy={verticalListSortingStrategy}>
            {blocks.map((block, index) => (
              <SortableBlock key={blockIds[index]} id={blockIds[index]}>
                {(dragHandleProps) => (
                  <div
                    className="rounded-xl border p-3 space-y-2"
                    style={{ borderColor: "hsl(var(--admin-border))" }}
                  >
                    <div className="flex items-center gap-1 justify-between">
                      <div className="flex items-center gap-1.5">
                        <button
                          {...dragHandleProps}
                          className="p-1 rounded cursor-grab active:cursor-grabbing hover:bg-muted"
                        >
                          <GripVertical className="w-3.5 h-3.5 text-muted-foreground" />
                        </button>
                        <span className="inline-flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider" style={{ color: "hsl(var(--admin-muted))" }}>
                          {block.type === "text"
                            ? <><Type className="w-3.5 h-3.5" />{t("contentBlocks.text")}</>
                            : block.type === "youtube"
                              ? <><Youtube className="w-3.5 h-3.5" />{t("contentBlocks.youtube")}</>
                              : <><ImagePlus className="w-3.5 h-3.5" />{t("contentBlocks.image")}</>}
                        </span>
                      </div>
                      <button
                        type="button"
                        className="p-1.5 rounded-md hover:bg-muted text-red-600"
                        onClick={() => remove(index)}
                        aria-label={t("common.delete")}
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </div>

                    {block.type === "text" ? (
                      <textarea
                        className="admin-input min-h-28"
                        value={block.value}
                        onChange={(event) => updateBlock(index, { type: "text", value: event.target.value })}
                      />
                    ) : block.type === "youtube" ? (
                      <div className="space-y-2">
                        <input
                          className="admin-input"
                          value={block.url}
                          onChange={(event) => updateBlock(index, { type: "youtube", url: event.target.value })}
                          placeholder={t("contentBlocks.youtubePlaceholder")}
                        />
                        {block.url && (() => {
                          const match = block.url.match(/(?:youtu\.be\/|[?&]v=|\/embed\/|\/shorts\/|\/live\/)([A-Za-z0-9_-]{11})/);
                          return match ? (
                            <div className="relative w-full aspect-video rounded-lg overflow-hidden bg-muted">
                              <iframe
                                src={`https://www.youtube.com/embed/${match[1]}`}
                                title="Preview"
                                className="absolute inset-0 w-full h-full"
                                allowFullScreen
                              />
                            </div>
                          ) : null;
                        })()}
                      </div>
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
                        <input
                          className="admin-input"
                          value={block.caption ?? ""}
                          onChange={(event) =>
                            updateBlock(index, { type: "image", url: block.url, caption: event.target.value || undefined })
                          }
                          placeholder={t("contentBlocks.captionPlaceholder")}
                        />
                      </div>
                    )}
                  </div>
                )}
              </SortableBlock>
            ))}
          </SortableContext>
        </DndContext>
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
        <button
          type="button"
          className="inline-flex items-center gap-2 px-3 py-2 text-xs font-bold border rounded-lg hover:bg-muted"
          style={{ borderColor: "hsl(var(--admin-border))" }}
          onClick={addYoutube}
        >
          <Plus className="w-3.5 h-3.5" />
          {t("contentBlocks.addYoutube")}
        </button>
      </div>
    </div>
  );
};

export default ContentBlockEditor;
