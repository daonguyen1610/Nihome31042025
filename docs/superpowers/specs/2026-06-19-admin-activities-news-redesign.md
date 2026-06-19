# Admin Activities & News Redesign

**Date:** 2026-06-19  
**Status:** Approved  
**Scope:** Backend + Admin frontend + Client frontend

---

## Problem

- `/admin/posts` quản lý Activities nhưng đặt tên nhầm là "Posts" — không khớp với client `/activities`
- Không có trang admin nào cho News, dù backend `NewsController`/`NewsService` đã đầy đủ
- Content editor chỉ hỗ trợ text thuần, không cho phép xen ảnh vào giữa bài
- Gallery cuối bài cố định grid, không có tùy chọn xem dọc

---

## Goals

1. Rename `admin/posts` → `admin/activities` toàn hệ thống
2. Thêm section admin/news đầy đủ (list, create, edit, view)
3. Thêm `NewsCategory` model để quản lý categories của News
4. Nâng cấp content editor hỗ trợ block xen kẽ text + image
5. Thêm gallery view toggle (grid / vertical list) ở admin view và client detail

---

## Architecture

### Content Block Schema

`ContentJson` trên cả `Activity` và `NewsArticle` chuyển từ mảng string sang mảng block:

```json
[
  { "type": "text", "value": "Đoạn văn..." },
  { "type": "image", "url": "https://..." },
  { "type": "text", "value": "Đoạn văn tiếp..." }
]
```

**Backward compatibility:** Frontend detect — nếu item là `string` thì render như text block cũ. Không cần data migration.

### Gallery View Mode

Không thay đổi schema. Toggle state là `"grid" | "list"`, lưu `useState` local (reset khi rời trang).

---

## Backend Changes

### New: NewsCategory

| File | Action |
|------|--------|
| `Models/NewsCategory.cs` | Tạo mới — clone `ActivityCategory.cs` |
| `Models/NewsArticle.cs` | Thêm `NewsCategoryId` (nullable FK) + navigation property |
| `Models/DTOs/Requests/UpsertNewsCategoryRequest.cs` | Tạo mới |
| `Models/DTOs/Responses/NewsCategoryResponse.cs` | Tạo mới |
| `Models/DTOs/Requests/UpsertNewsRequest.cs` | Thêm `NewsCategoryId` |
| `Services/NewsCategoryService.cs` | Tạo mới — clone `ActivityCategoryService.cs` |
| `Controllers/NewsCategoriesController.cs` | Tạo mới — clone `ActivityCategoriesController.cs` |
| `Data/AppDbContext.cs` | Thêm `DbSet<NewsCategory>` |
| `Migrations/` | Tạo migration: bảng `NewsCategories` + cột `NewsCategoryId` trên `NewsArticles` |
| `Data/Seeds/news.json` | Không đổi |

`NewsController` và `NewsService` giữ nguyên, chỉ cập nhật mapping `NewsCategoryId` từ request.

---

## Admin Frontend Changes

### Rename posts → activities

| Từ | Sang |
|----|------|
| `pages/admin/Posts.tsx` | `pages/admin/Activities.tsx` (rename file) |
| `pages/admin/PostForm.tsx` | `pages/admin/ActivityForm.tsx` (rename file) |
| `pages/admin/PostView.tsx` | `pages/admin/ActivityView.tsx` (rename file) |
| Route `/admin/posts` | `/admin/activities` + redirect từ `/admin/posts` |
| i18n key `nav.posts` | `nav.activities` |
| i18n key prefix `posts.*` | `activities.*` |
| Sidebar label | "Activities" |

### New: Admin News pages

| File | Nội dung |
|------|---------|
| `pages/admin/News.tsx` | List với search + filter by category — clone `AdminActivities.tsx` |
| `pages/admin/NewsForm.tsx` | Create/edit form — clone `ActivityForm.tsx`, dùng `adminApi.createNews`/`updateNews` |
| `pages/admin/NewsView.tsx` | Preview detail — clone `ActivityView.tsx` |

Routes mới trong `App.tsx`:
```
/admin/news          → AdminNews
/admin/news/new      → NewsForm mode="create"
/admin/news/:slug    → NewsView
/admin/news/:slug/edit → NewsForm mode="edit"
```

Sidebar: thêm mục **News** dưới **Activities** trong nhóm Content.

### Update: admin/categories — thêm tab News

`CategoryKind` hiện là `"posts" | "projects"` → đổi thành `"activities" | "projects" | "news"`.

- Tab `"posts"` → `"activities"` (rename, giữ logic ActivityCategory)
- Tab `"news"` thêm mới: dùng `NewsCategoryResponse`, `adminApi.createNewsCategory`, v.v.
- i18n: `cat.tabPosts` → `cat.tabActivities`; thêm `cat.tabNews`

### New: ContentBlockEditor component

`components/admin/ContentBlockEditor.tsx` — dùng chung cho `ActivityForm` và `NewsForm`.

- Danh sách block, mỗi block có type: `"text"` | `"image"`
- **Text block**: `<textarea>` resize
- **Image block**: nút upload → `adminApi.uploadImage()`, preview thumbnail + nút xóa
- Toolbar mỗi block: `↑` `↓` `Xóa`
- Footer: `+ Thêm đoạn văn` | `+ Thêm ảnh`

Serialize về `ContentJson`:
```ts
type ContentBlock = { type: "text"; value: string } | { type: "image"; url: string }
```

### Gallery toggle in ActivityView + NewsView

Thêm 2 icon button (Grid / List) ở header của section gallery:
- **Grid**: layout hiện tại
- **List**: mỗi ảnh `w-full aspect-video object-cover`, xếp dọc

---

## Client Frontend Changes

### ContentBlocks component

`components/ContentBlocks.tsx` — shared renderer:

```tsx
function renderBlock(block: string | ContentBlock) {
  if (typeof block === "string") return <p>{block}</p>  // backward compat
  if (block.type === "text") return <p>{block.value}</p>
  if (block.type === "image") return <img src={block.url} loading="lazy" className="w-full rounded-2xl" />
}
```

Dùng trong `pages/ActivityDetail.tsx` và `pages/NewsDetail.tsx`.

### Gallery toggle in ActivityDetail + NewsDetail

Toggle **Grid / List** ở header section gallery (giống admin view). Cùng logic `useState<"grid" | "list">`.

---

## i18n Keys cần thêm

Seed files: `nihomebackend/Data/Seeds/admin-system.json` (admin keys), `nihomebackend/Data/Seeds/news.json` (gallery keys dùng chung)

> **Lưu ý prefix**: Client đã dùng `newsPage.*` → admin News dùng prefix `adminNews.*` để tránh collision.
> Client đã dùng `actPage.*` → admin Activities dùng prefix `activities.*`.

```
nav.activities          (vi/en/zh/ja)  — đổi từ nav.posts
nav.news                (vi/en/zh/ja)  — mới

activities.title        (vi/en/zh/ja)  — đổi từ posts.title
activities.create       ...
activities.col.post     ...
activities.col.category ...
activities.col.author   ...
activities.empty        ...
activities.searchPlaceholder ...
activities.allCategories ...

adminNews.title         (vi/en/zh/ja)  — mới
adminNews.create        ...
adminNews.col.post      ...
adminNews.col.category  ...
adminNews.col.author    ...
adminNews.empty         ...
adminNews.searchPlaceholder ...
adminNews.allCategories ...

cat.tabActivities       (vi/en/zh/ja)  — đổi từ cat.tabPosts
cat.tabNews             (vi/en/zh/ja)  — mới

gallery.viewGrid        (vi/en/zh/ja)  — mới (dùng chung admin + client)
gallery.viewList        (vi/en/zh/ja)  — mới
```

---

## TypeScript types (adminApi.ts + contentApi.ts)

- Thêm `NewsCategoryResponse`, `UpsertNewsCategoryRequest` vào `adminApi.ts`
- Thêm các admin methods cho news categories
- `NewsResponse.category` giữ nguyên string; thêm `newsCategoryId?: number` nếu cần filter

---

## Out of Scope

- Reorder bằng drag-and-drop (dùng ↑↓ buttons thay thế)
- Persist gallery view mode giữa các lần truy cập
- Migration dữ liệu content cũ sang block format

---

## Risks

- `ContentJson` cũ là mảng string — cần test backward compat kỹ trên client và admin view
- Nhiều file rename → cần cập nhật tất cả import, đặc biệt trong `App.tsx`
