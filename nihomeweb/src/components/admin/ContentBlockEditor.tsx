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
import { adminApi } from "@/services/adminApi";
import type { ContentBlock, ContentItem } from "@/services/contentApi";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";

interface ContentBlockEditorProps {
  value: ContentItem[];
  onChange: (value: ContentItem[]) => void;
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

const ContentBlockEditor = ({ value, onChange }: ContentBlockEditorProps) => {
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
      const res = await adminApi.uploadImage(file, previous || undefined);
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
        <p className="text-xs italic text-muted-foreground">{t("contentBlocks.empty")}</p>
      ) : (
        <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
          <SortableContext items={blockIds} strategy={verticalListSortingStrategy}>
            {blocks.map((block, index) => (
              <SortableBlock key={blockIds[index]} id={blockIds[index]}>
                {(dragHandleProps) => (
                  <div className="space-y-2 rounded-lg border bg-card p-3">
                    <div className="flex items-center justify-between gap-1">
                      <div className="flex items-center gap-1.5">
                        <button
                          {...dragHandleProps}
                          className="cursor-grab rounded p-1 hover:bg-muted active:cursor-grabbing"
                        >
                          <GripVertical className="h-3.5 w-3.5 text-muted-foreground" />
                        </button>
                        <span className="inline-flex items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                          {block.type === "text"
                            ? <><Type className="h-3.5 w-3.5" />{t("contentBlocks.text")}</>
                            : block.type === "youtube"
                              ? <><Youtube className="h-3.5 w-3.5" />{t("contentBlocks.youtube")}</>
                              : <><ImagePlus className="h-3.5 w-3.5" />{t("contentBlocks.image")}</>}
                        </span>
                      </div>
                      <Button
                        type="button"
                        size="icon"
                        variant="ghost"
                        className="h-7 w-7 text-destructive hover:text-destructive"
                        onClick={() => remove(index)}
                        aria-label={t("common.delete")}
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    </div>

                    {block.type === "text" ? (
                      <Textarea
                        className="min-h-28"
                        value={block.value}
                        onChange={(event) => updateBlock(index, { type: "text", value: event.target.value })}
                      />
                    ) : block.type === "youtube" ? (
                      <div className="space-y-2">
                        <Input
                          className="h-9"
                          value={block.url}
                          onChange={(event) => updateBlock(index, { type: "youtube", url: event.target.value })}
                          placeholder={t("contentBlocks.youtubePlaceholder")}
                        />
                        {block.url && (() => {
                          const match = block.url.match(/(?:youtu\.be\/|[?&]v=|\/embed\/|\/shorts\/|\/live\/)([A-Za-z0-9_-]{11})/);
                          return match ? (
                            <div className="relative aspect-video w-full overflow-hidden rounded-lg bg-muted">
                              <iframe
                                src={`https://www.youtube.com/embed/${match[1]}`}
                                title="Preview"
                                className="absolute inset-0 h-full w-full"
                                allowFullScreen
                              />
                            </div>
                          ) : null;
                        })()}
                      </div>
                    ) : (
                      <div className="space-y-2">
                        {block.url && (
                          <img src={block.url} alt="" className="aspect-video w-full rounded-lg bg-muted object-cover" />
                        )}
                        <div className="flex items-center gap-2">
                          <Input
                            className="h-9 flex-1"
                            value={block.url}
                            onChange={(event) => updateBlock(index, { type: "image", url: event.target.value })}
                            placeholder={t("media.url.placeholder")}
                          />
                          <Button
                            type="button"
                            size="sm"
                            variant="outline"
                            onClick={() => openUpload(index)}
                            disabled={uploading}
                          >
                            <Upload className="mr-1.5 h-3.5 w-3.5" />
                            {t("media.url.upload")}
                          </Button>
                        </div>
                        <Input
                          className="h-9"
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
        <Button type="button" size="sm" variant="outline" onClick={addText}>
          <Plus className="mr-1.5 h-3.5 w-3.5" />
          {t("contentBlocks.addText")}
        </Button>
        <Button type="button" size="sm" variant="outline" onClick={addImage}>
          <Plus className="mr-1.5 h-3.5 w-3.5" />
          {t("contentBlocks.addImage")}
        </Button>
        <Button type="button" size="sm" variant="outline" onClick={addYoutube}>
          <Plus className="mr-1.5 h-3.5 w-3.5" />
          {t("contentBlocks.addYoutube")}
        </Button>
      </div>
    </div>
  );
};

export default ContentBlockEditor;
