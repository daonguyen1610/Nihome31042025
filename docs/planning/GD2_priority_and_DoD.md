# GĐ2 – Auth · CRM · Design · Permitting

> Kế hoạch ưu tiên + DoD viết lại theo hướng nghiệp vụ (human-written) cho sprint đang active `GĐ2 - Auth CRM Design` trên Jira project `NIH`.
>
> Nguồn nghiệp vụ tham chiếu: `docs/Nicon_BreakTask_v1.xlsx` (sheet **Yêu cầu ban đầu từ Nicon** + sheet **Nicon** + sheet **BreakTask_Estimate**).

---

## 1. Bối cảnh hiện tại (Jira NIH)

| Sprint | State | Số issue | Ghi chú |
|---|---|---|---|
| GĐ1 – Website Nicon | closed | 60 (Done) | Marketing site đã live. |
| **GĐ2 – Auth CRM Design** | **active** | **55 (all To Do)** | Đang triển khai — phạm vi bài này. |
| GĐ3 – Build Proc Finance | future | 62 | Sau khi GĐ2 xong. |
| GĐ4 – Assets Analytics KPI | future | 20 | Cuối cùng. |
| (không sprint) | – | 11 | Cần triage. |

Trong GĐ2 có **7 Epic/Story cha** (M0 Auth, M1 CRM, M2 Design, M3 Permitting) và 47 subtask CRUD List/Create-Edit/Detail lặp lại.

### Vấn đề DoD hiện tại
Toàn bộ description được sinh tự động → mọi subtask đều giống hệt nhau về hình thức:
- Câu chữ generic ("Người dùng xem được danh sách X, tìm kiếm/lọc nhanh…").
- Không có acceptance criteria đo được (Gherkin/số liệu cụ thể).
- Không phản ánh rule nghiệp vụ đặc thù (VD báo giá phải có version, IFC phải đóng dấu điện tử, khảo sát phải đồng bộ Drive `01_Khao_sat`…).
- Có PlantUML nhưng workflow rập khuôn "mở danh sách → filter → lưu → audit" cho **mọi** loại nghiệp vụ.
- Không nêu API/entity/permission cụ thể → dev/QA không biết dựa vào đâu để verify.

Đây chính là điểm cần fix.

---

## 2. Nguyên tắc ưu tiên GĐ2

Ưu tiên dựa trên 3 tiêu chí, xếp theo thứ tự:

1. **Chặn kỹ thuật** – phải có mới làm được cái khác (auth, RBAC, master data).
2. **Chuỗi giá trị nghiệp vụ (business flow)** – theo đúng luồng Sales → Design → Permit của Nicon.
3. **ROI sớm cho khách hàng** – ưu tiên các màn cho phép Sales/PM dùng được, sinh dữ liệu thực tế đưa vào M4+ ở GĐ3.

---

## 3. Thứ tự ưu tiên đề xuất

### 🟥 P0 – Nền tảng (bắt buộc trước tất cả)

| # | Jira | Việc | Vì sao đầu tiên |
|---|---|---|---|
| 1 | **NIH-350** | [GĐ2] Auth & Navigation (FE bổ sung) | Không có login/route/menu theo role thì cả CRM/Design/Permit không dùng được. |
| 2 | *(cần tạo)* | **Master data seed**: role matrix, nguồn lead, loại KH, loại hợp đồng, loại giấy phép, bộ môn thiết kế, checklist template | Toàn bộ dropdown/lookup của M1–M3 phụ thuộc vào master data. Chưa thấy story trong GĐ2 — **đề xuất tạo mới**. |

### 🟧 P1 – CRM/Sale chain (M1) – theo đúng flow Lead → Contract

| # | Jira | Việc | Ghi chú thứ tự |
|---|---|---|---|
| 3 | *(cần tạo)* | **Lead management** (List/Form/Detail) | Break-task Excel liệt kê nhưng **thiếu trong Jira**. Đây là bước 0 của CRM. |
| 4 | NIH-78 (+80/81/82) | Khách hàng | Là root entity dùng ở mọi module sau. |
| 5 | NIH-83 (+88/89/90/91) | Cơ hội + Pipeline | Sau khi có khách. Cần Pipeline Kanban. |
| 6 | NIH-86 (+99/100/101) | Khảo sát | Cần trước khi báo giá — cung cấp đầu vào kỹ thuật/hình ảnh. |
| 7 | NIH-84 (+92/93/94) | Báo giá trực tiếp | Sau Khảo sát. Có version, duyệt nội bộ, PDF. |
| 8 | NIH-85 (+95/96/97/98) | Đấu thầu | Song song với Báo giá — Sales có thể chọn 1 trong 2 flow. |
| 9 | NIH-87 (+102/103/104) | Hợp đồng | Kết thúc CRM flow — chuyển dữ liệu sang Design/Finance. |

### 🟨 P2 – Design chain (M2) – phụ thuộc Hợp đồng từ P1

| # | Jira | Việc | Ghi chú |
|---|---|---|---|
| 10 | NIH-113 (+119/120/121) | Dự án thiết kế – Tổng quan | Root của Design. |
| 11 | NIH-114 (+122/123/124) | Concept | Giai đoạn thiết kế 1. |
| 12 | NIH-115 (+125/126/127) | Basic Design | Cần trước Permitting (dùng cho GPXD). |
| 13 | NIH-116 (+128/129/130) | Shop Drawing (theo bộ môn) | Sau Basic Design. |
| 14 | NIH-117 (+131/132/133) | Revision Control | Chạy song song với Shop Drawing. |
| 15 | NIH-118 (+134/135/136) | Phát hành IFC | Cuối chuỗi Design — cần dấu điện tử. |

### 🟩 P3 – Permitting (M3) – có thể song song P2 sau Basic Design

| # | Jira | Việc |
|---|---|---|
| 16 | NIH-137 (+138/139/140) | Checklist pháp lý (GPXD, PCCC, điện/nước, vỉa hè, hoàn công…) |

### 📌 Suy nghĩ thứ tự thực thi trong sprint

- **Tuần 1**: P0 (Auth + Master data seed) — song song FE & BE.
- **Tuần 2–3**: Lead + Khách hàng + Cơ hội + Khảo sát (chuỗi CRM đầu).
- **Tuần 4–5**: Báo giá + Đấu thầu + Hợp đồng.
- **Tuần 6–7**: Design M2 (Tổng quan → Concept → Basic Design).
- **Tuần 8**: Shop Drawing + Revision + IFC + Permitting.

Nếu team đông có thể chia 2 nhóm: nhóm CRM (Sales), nhóm Design/Permit (Kỹ thuật), gặp nhau tại module **Contract → Design project**.

### ⚠️ Gap cần bổ sung Jira trước khi start

1. Tạo Story **Lead** (List/Create-Edit/Detail) — thuộc M1, hiện thiếu.
2. Tạo Story **Master data** (loại KH, nguồn lead, loại hợp đồng, loại giấy phép, bộ môn thiết kế, checklist template) — thuộc M0/Core.
3. Tạo Story **RBAC seeding** — định nghĩa roles: Admin, Sales, Design Lead, Kiến trúc sư, MEP, PM, QS, Kế toán, Kho, BGĐ.
4. Tạo Task **Notification foundation** — email + in-app cho các event duyệt báo giá/hợp đồng, hết hạn giấy phép, revision mới. (Có thể defer sang GĐ3 nếu ưu tiên demo Sales-flow trước.)

---

## 4. Chuẩn viết DoD mới (áp dụng cho toàn bộ story/subtask)

Mỗi issue DoD gồm 5 block cố định:

```
## 🎯 Business Objective
(1–2 câu, nói bằng ngôn ngữ khách hàng.)

## 👥 Actors & Permissions
- Ai được làm gì. Ghi rõ role → action.

## ✅ Acceptance Criteria (business rules)
1. Rule 1…
2. Rule 2…
n. Rule n…
(Bullet number rõ ràng, có ngữ cảnh "Khi X thì Y", đo được.)

## 🔌 API / Data Contract
- Endpoint(s) + entity chính + trường bắt buộc.

## 🧪 Verification (QA-ready)
- Happy path + edge cases + non-functional (perf/i18n/RBAC/audit).
```

**Không viết**: workflow diagram rập khuôn, "phụ thuộc chung Core" chung chung, câu "story này tập trung vào…".

---

## 5. DoD viết lại (mẫu đầy đủ) – các story P0/P1/P2/P3

Dưới đây là bản viết lại thủ công, sử dụng đúng rule nghiệp vụ trong file Excel. Mỗi story kèm DoD cho các subtask con.

---

### 🟥 NIH-350 · [GĐ2] Auth & Navigation

**🎯 Business Objective**
Người dùng đăng nhập một lần, thấy đúng menu theo vai trò, và không bị lộ dữ liệu ngoài phạm vi được cấp quyền.

**👥 Actors & Permissions**
- **Admin**: thấy toàn bộ menu + màn quản lý user/role/permission.
- **Sales**: thấy CRM (Lead, KH, Cơ hội, Báo giá, Đấu thầu, Khảo sát, Hợp đồng).
- **Design Lead / KTS / MEP**: thấy Design (Concept, Basic, Shop Drawing, Revision, IFC).
- **PM**: thấy Design + Permitting + Construction (khi có).
- **QS/Kế toán**: thấy Finance (khi có ở GĐ3).
- **BGĐ**: read-only tất cả + Dashboard.

**✅ Acceptance Criteria**
1. Có form login (email + password) với validation client-side (email hợp lệ, password ≥ 8 ký tự).
2. Sai credential 5 lần liên tiếp trong 15 phút → khoá đăng nhập IP đó 15 phút (khớp `docs/users-rbac.md`).
3. Login thành công → JWT lưu httpOnly cookie hoặc localStorage (thống nhất với BE hiện tại), redirect về URL trước khi bị chặn hoặc `/admin`.
4. Có luồng **Quên mật khẩu** → nhập email → gửi link reset (token expire 30 phút, 1-time use).
5. Có trang **Profile**: xem/sửa họ tên, avatar, đổi mật khẩu (yêu cầu password cũ).
6. **Sidebar** hiển thị menu theo permission thực tế của user (không chỉ ẩn UI — API cũng phải chặn 403).
7. **Route guard** (`ProtectedRoute`): chưa login → redirect `/login`; không đủ quyền → hiển thị trang 403 (không phải redirect vòng).
8. **Header** có: logo, breadcrumb, chuông thông báo (badge count), avatar dropdown (Profile / Logout).
9. Sidebar responsive: desktop expand/collapse; mobile chuyển thành drawer.
10. Có trang lỗi **404**, **403**, **500** với CTA quay về trang chủ / đăng nhập lại.
11. Đăng xuất xoá token, invalidate refresh token BE (nếu có), redirect `/login`.

**🔌 API / Data Contract**
- `POST /api/auth/login` → `{ token, user: { id, name, roles[], permissions[] } }`.
- `POST /api/auth/forgot-password`, `POST /api/auth/reset-password`.
- `GET /api/me` → user hiện tại + permission set.
- FE: `nihomeweb/src/App.tsx` (routes), `nihomeweb/src/components/layout/AdminLayout.tsx` (sidebar).

**🧪 Verification**
- E2E Playwright: login OK, login sai 5 lần bị khoá, forgot-password flow, route bị chặn cho role khác.
- Integration test: `MeController` trả đúng permission theo role.
- i18n: form login hoạt động vi/en/zh/ja.
- Audit log: mỗi login success/failure có record.

---

### 🟧 NIH-78 · [M1] Khách hàng (Customer)

**🎯 Business Objective**
Sales có repository khách hàng cá nhân/doanh nghiệp, gắn được với Lead → Cơ hội → Báo giá → Hợp đồng → Dự án; theo dõi trạng thái quan hệ để không mất data khi Sales nghỉ việc.

**👥 Actors & Permissions**
- **Sales**: CRUD khách hàng do mình phụ trách + read all trong team.
- **Sales Manager / BGĐ**: full CRUD toàn bộ.
- **Kế toán**: read (dùng khi xuất hoá đơn).

**✅ Acceptance Criteria (áp dụng cho cả 3 subtask con)**
1. Khách hàng có 2 loại: **Cá nhân** và **Doanh nghiệp**. Loại Doanh nghiệp bắt buộc `MST` (mã số thuế), địa chỉ đăng ký, người đại diện.
2. Một khách hàng có ≥1 `Contact Person` (tên, chức vụ, phone, email); có 1 người đánh dấu **primary contact**.
3. Trường bắt buộc khi tạo: `Loại`, `Tên KH`, `Phone hoặc Email` (ít nhất 1), `Sales phụ trách`, `Nguồn` (Marketing / Giới thiệu / Website / …) — lấy từ master data.
4. Trạng thái quan hệ có 4 giá trị: **Tiềm năng · Đang giao dịch · Đã ký · Ngưng**. Chỉ Sales Manager/BGĐ được chuyển sang **Ngưng**.
5. Kiểm tra trùng: cùng `MST` (với DN) hoặc cùng `Phone` (với CN) → cảnh báo trước khi lưu, cho phép override có ghi audit.
6. Chi tiết KH hiển thị 4 tab: **Thông tin chung · Liên hệ · Cơ hội/Báo giá/Hợp đồng liên kết · Lịch sử chăm sóc (timeline)**.
7. Timeline chăm sóc: mỗi lần gọi/gặp/email → 1 entry (loại, ngày giờ, người tạo, ghi chú, đính kèm ≤5MB/file).
8. Có nút **Nhắc lịch gọi lại** ở detail: tạo reminder in-app + email cho Sales owner.
9. Danh sách: filter theo `Loại`, `Trạng thái`, `Sales phụ trách`, `Nguồn`, `Ngày tạo (from-to)`; search theo tên/MST/phone.
10. Xoá KH bị chặn nếu đang có Cơ hội/Hợp đồng đang mở → hiển thị lý do rõ ràng.
11. Không có KH nào bị lộ ra ngoài scope Sales của người đó (BE enforce, không chỉ ẩn UI).

**🔌 API / Data Contract**
- Entity: `Customer` (Id, Type, Name, TaxId, Address, RepresentativeName, Source, RelationshipStatus, OwnerUserId, CreatedAt, UpdatedAt).
- Entity: `CustomerContact` (Id, CustomerId, FullName, Position, Phone, Email, IsPrimary).
- Entity: `CustomerActivity` (Id, CustomerId, Type, OccurredAt, Note, Attachments).
- `GET/POST /api/customers`, `GET/PUT/DELETE /api/customers/{id}`.
- `POST /api/customers/{id}/activities` (timeline).

**🧪 Verification**
- Unit: validation trùng MST, chuyển trạng thái, block xoá khi có FK.
- Integration: RBAC Sales A không thấy KH của Sales B; Sales Manager thấy hết.
- E2E: tạo → thêm contact → thêm activity → xoá bị chặn khi có hợp đồng.

---

#### Subtask NIH-80 · Khách hàng – Danh sách
1. Trang mở default trả 20 record/trang, sort mặc định `UpdatedAt DESC`.
2. Cột hiển thị: Tên KH · Loại · Sales phụ trách · Trạng thái (badge màu) · SL cơ hội mở · Ngày tạo · Action.
3. Filter panel: `Loại`, `Trạng thái`, `Sales`, `Nguồn`, `Ngày tạo from-to`.
4. Search debounce 300 ms, match `Tên | MST | Phone | Email`.
5. Export CSV các bản ghi đang filter (chỉ user có permission `customer.export`).
6. Empty state có CTA "Tạo khách hàng đầu tiên".
7. Loading skeleton + error retry.
8. URL query giữ filter/sort/page để share link.

#### Subtask NIH-81 · Khách hàng – Tạo/Chỉnh sửa
1. Form 2 mode: `Tạo mới` và `Chỉnh sửa` cùng 1 component.
2. Toggle Loại KH thay đổi field bắt buộc (MST hiện khi Doanh nghiệp).
3. Section **Contact persons** hỗ trợ add/remove nhiều dòng, ít nhất 1, đúng 1 primary.
4. Cảnh báo trùng MST/Phone realtime khi blur field.
5. Lưu thành công → toast + điều hướng về Detail.
6. Server-side error trả field-level → highlight đúng ô, không mất dữ liệu đã nhập.
7. Nút "Huỷ" có confirm nếu form dirty.

#### Subtask NIH-82 · Khách hàng – Chi tiết
1. Header sticky: tên KH, badge trạng thái, nút `Chỉnh sửa` / `Xoá` (theo quyền).
2. 4 tab như AC #6 story cha; deep-link mỗi tab qua URL.
3. Tab Liên kết hiển thị bảng Cơ hội/Báo giá/Hợp đồng với link chuyển màn.
4. Tab Timeline có form nhanh add activity (loại + ghi chú).
5. Nút **Nhắc lịch gọi lại** mở dialog chọn ngày giờ + note.
6. Xoá phải confirm 2 bước, hiển thị dependency count.

---

### 🟧 NIH-83 · [M1] Cơ hội (Opportunity)

**🎯 Business Objective**
Sales theo dõi pipeline chuyển đổi Lead → Cơ hội → Báo giá → Hợp đồng, đánh giá xác suất thắng và ghi nhận lý do thắng/thua để cải thiện.

**👥 Actors & Permissions**
- Sales: CRUD cơ hội của mình. Sales Manager/BGĐ: full.

**✅ Acceptance Criteria**
1. Cơ hội bắt buộc: `Tên`, `Khách hàng` (FK), `Sales owner`, `Giá trị dự kiến` (VND, ≥ 0), `Xác suất chốt` (%, 0-100), `Ngày dự kiến chốt`, `Stage`.
2. Stages master data (có thể thay đổi qua Admin): **Prospecting · Qualification · Proposal · Negotiation · Won · Lost**. Không cho chuyển ngược từ Won/Lost trở về.
3. Chuyển sang **Won** → yêu cầu chọn `Báo giá liên kết` hoặc `Gói thầu trúng` → tự động tạo bản nháp Hợp đồng.
4. Chuyển sang **Lost** → bắt buộc chọn `Lý do thua` (master data: Giá cao / Đối thủ / Timing / Yêu cầu kỹ thuật / Khác) + ghi chú.
5. Pipeline view (Kanban) hiển thị cột theo stage; drag-drop giữa các cột = update stage; tổng giá trị mỗi cột hiển thị header.
6. Filter pipeline theo: Sales owner, tháng dự kiến chốt, khách hàng, min-max giá trị.
7. Danh sách bảng có toggle sang Kanban.
8. Notification khi Sales owner của cơ hội thay đổi (assign).
9. Audit log mọi lần đổi stage/owner/giá trị.
10. Xoá bị chặn nếu có báo giá/hợp đồng liên kết.

**🔌 API / Data Contract**
- Entity: `Opportunity` (Id, Name, CustomerId, OwnerUserId, EstimatedValue, WinProbability, ExpectedCloseDate, StageId, LostReasonId?, LostNote?, ...).
- `GET /api/opportunities?stage=&owner=&customer=`.
- `PATCH /api/opportunities/{id}/stage` (body: `{ stageId, wonQuoteId?, lostReasonId?, lostNote? }`).

**🧪 Verification**
- Unit: rule chuyển stage, chặn ngược từ Won/Lost.
- Integration: RBAC + auto-create contract khi Won.
- E2E: drag-drop Kanban update BE.

---

#### Subtask NIH-88 · Opportunity – Danh sách
Cột: Tên · KH · Sales · Stage · Giá trị · Xác suất · Ngày dự kiến chốt. Filter/search như AC #6. Toggle View: Bảng ↔ Kanban.

#### Subtask NIH-89 · Opportunity – Tạo/Chỉnh sửa
Form theo entity. Chọn Stage `Won`/`Lost` mở modal bắt buộc thêm dữ liệu theo AC #3, #4.

#### Subtask NIH-90 · Opportunity – Chi tiết
Header cơ hội + tabs: **Overview · Báo giá/Đấu thầu liên kết · Tài liệu · Timeline hoạt động · Audit log**.

#### Subtask NIH-91 · Pipeline cơ hội
1. Kanban board 6 cột (mặc định).
2. Drag-drop có confirm khi chuyển sang Won/Lost.
3. Header mỗi cột: tên stage + `SL cơ hội` + `tổng giá trị VND`.
4. Card cơ hội hiển thị: Tên · KH · Giá trị · Owner avatar · Ngày chốt (badge đỏ nếu quá hạn).
5. Filter bar chung: owner, tháng, khách hàng.
6. Board có drag scroll ngang trên mobile.

---

### 🟧 NIH-84 · [M1] Báo giá trực tiếp (Direct Quote)

**🎯 Business Objective**
Sales lập báo giá nhanh cho khách hàng theo **suất đầu tư** (đơn giản: m² × đơn giá) hoặc **BOQ sơ bộ** (chi tiết theo hạng mục), quản lý version, duyệt nội bộ trước khi gửi khách và xuất PDF.

**👥 Actors & Permissions**
- Sales: tạo/sửa báo giá của cơ hội mình phụ trách (chỉ khi chưa gửi khách).
- Sales Manager: duyệt nội bộ trước khi cho gửi khách.
- BGĐ: read + duyệt báo giá có giá trị > ngưỡng (config được).

**✅ Acceptance Criteria**
1. Báo giá bắt buộc gắn với 1 `Cơ hội`.
2. Có 2 phương thức: **Suất đầu tư** (nhập `m²` + `đơn giá/m²` → tự tính tổng) hoặc **BOQ sơ bộ** (bảng chi tiết: hạng mục, đơn vị, khối lượng, đơn giá, thành tiền).
3. Tính tự động: `Tổng trước thuế` → áp `Chiết khấu %` → `Sau chiết khấu` → áp `VAT %` (default 8/10, config) → `Tổng thanh toán`.
4. **Version control**: mỗi lần lưu chỉnh sửa sau khi đã submit duyệt → tạo version mới (V1, V2…). Không cho sửa version cũ; version cũ read-only.
5. Trạng thái báo giá: **Draft · Chờ duyệt nội bộ · Đã duyệt · Đã gửi khách · Đã duyệt bởi khách · Từ chối · Hết hạn**. Chuyển trạng thái theo workflow, không được nhảy cóc.
6. `Hạn hiệu lực báo giá` (mặc định 30 ngày) — hết hạn tự chuyển **Hết hạn**, cần Sales Manager duyệt lại nếu muốn dùng.
7. Xuất **PDF** theo template có: logo, thông tin công ty, thông tin khách hàng, bảng chi tiết, tổng tiền viết bằng chữ, chỗ ký; preview trước khi export.
8. So sánh **version diff** (2 version bất kỳ): hiện dòng thêm/bớt, thay đổi đơn giá/khối lượng highlight màu.
9. Notification: gửi email cho Sales Manager khi có báo giá cần duyệt; gửi cho Sales khi khách phản hồi.
10. Audit: mọi thay đổi trạng thái + version tăng đều log user + thời điểm.
11. Xoá chỉ được với báo giá `Draft`; các trạng thái khác chỉ được `Đánh dấu huỷ` (soft-delete).

**🔌 API / Data Contract**
- Entity: `Quote` (Id, OpportunityId, Method[SuatDauTu|BOQ], SubTotal, DiscountPercent, VatPercent, GrandTotal, Status, ValidUntil, Version, ...).
- Entity: `QuoteItem` (Id, QuoteId, ItemName, Unit, Quantity, UnitPrice, Amount).
- `POST /api/quotes/{id}/submit-approval`, `POST /api/quotes/{id}/approve`, `POST /api/quotes/{id}/send-to-customer`.
- `GET /api/quotes/{id}/pdf` (stream).
- `GET /api/quotes/{id}/versions`, `GET /api/quotes/{id}/diff?from=V1&to=V2`.

**🧪 Verification**
- Unit: tính tổng, VAT, chiết khấu (kể cả edge case discount 0/100, VAT 0).
- Unit: chặn transition sai state.
- Integration: gen PDF không vỡ Unicode tiếng Việt.
- E2E: tạo báo giá BOQ → submit → duyệt → gửi khách → export PDF.

---

#### Subtask NIH-92 · Báo giá – Danh sách
Cột: Mã · Cơ hội · KH · Version · Tổng · Trạng thái · Hạn hiệu lực · Sales. Filter theo trạng thái, sales, khoảng thời gian. Badge đỏ cho báo giá sắp hết hạn ≤ 3 ngày.

#### Subtask NIH-93 · Báo giá – Tạo/Chỉnh sửa
1. Chọn method → hiển thị form tương ứng.
2. BOQ mode: bảng inline-edit, add/remove dòng, tính thành tiền realtime; hỗ trợ paste từ Excel.
3. Nút **Save Draft** (cho phép giá trị chưa hợp lệ) và **Submit duyệt** (validate đủ).
4. Sửa báo giá đã duyệt → confirm tạo version mới.
5. Preview PDF trước khi Save.

#### Subtask NIH-94 · Báo giá – Chi tiết
1. Tab **Nội dung** (bảng BOQ / suất đầu tư).
2. Tab **Versions** (danh sách + nút So sánh 2 version).
3. Tab **Workflow duyệt** (ai duyệt/từ chối, ngày, ghi chú).
4. Tab **Phản hồi khách** (upload email/note).
5. Nút export PDF; nút gửi email tới khách kèm PDF (SMTP config).

---

### 🟧 NIH-85 · [M1] Đấu thầu (Tender)

**🎯 Business Objective**
Với dự án theo hình thức đấu thầu, quản lý deadline hồ sơ dự thầu, checklist hồ sơ năng lực, kết quả trúng/trượt để cải thiện tỷ lệ trúng.

**✅ Acceptance Criteria**
1. Gói thầu bắt buộc: `Tên gói`, `Chủ đầu tư (KH)`, `Ngày mở thầu`, `Deadline nộp`, `Người phụ trách chuẩn bị`.
2. Có checklist mặc định (từ master data): Hồ sơ năng lực, Hồ sơ pháp nhân, Bảo lãnh dự thầu, BOQ, Thuyết minh biện pháp thi công, Tiến độ.
3. Mỗi mục checklist có trạng thái: **Chưa chuẩn bị · Đang chuẩn bị · Hoàn thành · Nộp**; có người phụ trách + deadline nội bộ.
4. Alert khi `Deadline nộp - now ≤ 3 ngày` mà checklist chưa 100%.
5. Trạng thái gói thầu: **Chuẩn bị · Đã nộp · Trúng · Trượt · Huỷ**. Chọn Trúng → yêu cầu chọn Cơ hội để chuyển sang Hợp đồng.
6. Có **Repository Hồ sơ năng lực** dùng chung — không phải upload lại mỗi gói: chọn từ library có sẵn (kiến trúc, kết cấu, MEP, ISO, giấy phép).
7. Thống kê tỷ lệ trúng thầu theo kỳ (tháng/quý/năm) hiển thị ở detail Sales dashboard (KPI GĐ4, chỉ chuẩn bị data ở GĐ2).
8. Audit + notification cho người phụ trách khi được assign mục checklist.

**🔌 API / Data Contract**
- Entity: `Tender`, `TenderChecklistItem` (link tới `TenderChecklistTemplate`), `CapabilityDocument` (repository).

**🧪 Verification**
- Alert cron 1 lần/ngày kiểm tra deadline.
- Unit: rule trạng thái Trúng phải chọn Cơ hội.

---

#### Subtask NIH-95/96/97 · Gói thầu – List/Form/Detail
Áp dụng chuẩn CRUD + rule story cha. Detail có 3 tab: **Thông tin · Checklist hồ sơ · Kết quả & lịch sử**.

#### Subtask NIH-98 · Hồ sơ năng lực – Danh sách/Tạo sửa
1. Grid có preview icon theo loại (PDF/DOCX/XLS/IMG).
2. Tag phân loại: Pháp nhân · Kiến trúc · Kết cấu · MEP · ISO · Giấy phép · Khác.
3. Upload đa file cùng lúc, tối đa 20MB/file, hỗ trợ replace giữ history.
4. Search theo tên + tag + năm cấp.
5. Download nhiều file dạng zip.

---

### 🟧 NIH-86 · [M1] Khảo sát (Site Survey)

**🎯 Business Objective**
Số hoá phiếu khảo sát hiện trường: chụp ảnh, quay video, upload bản vẽ hiện trạng, ghi chú kỹ thuật, tự đồng bộ vào folder Google Drive `01_Khao_sat/<Project>` để đội thiết kế dùng.

**✅ Acceptance Criteria**
1. Phiếu khảo sát gắn với 1 trong: Lead / Cơ hội / Dự án (tuỳ giai đoạn).
2. Trường bắt buộc: `Ngày khảo sát`, `Người khảo sát (multi)`, `Địa điểm`, `Loại công trình`.
3. Checklist hiện trạng (từ master data): Địa chất · Cấp điện · Cấp nước · Thoát nước · Giao thông tiếp cận · Xung quanh · Vướng mắc pháp lý sơ bộ.
4. Upload ảnh/video/file: mỗi file ≤ 100MB, tổng ≤ 2GB/phiếu; hỗ trợ chụp ảnh trực tiếp từ mobile (input `capture=camera`).
5. Mỗi ảnh có metadata: tên, ghi chú, geolocation (nếu browser cho phép).
6. Sau khi lưu → job async đẩy lên Google Drive folder `01_Khao_sat/<Mã dự án>/<Mã phiếu>` (retry 3 lần, log nếu fail).
7. Phân quyền xem: Sales owner, Design team, PM. Không public.
8. Tìm kiếm theo địa điểm, loại công trình, ngày khảo sát.
9. Có nút "Export báo cáo khảo sát" (PDF gồm thông tin + ảnh thumbnail).
10. Audit mỗi lần thêm/xoá file.

**🔌 API / Data Contract**
- Entity: `Survey`, `SurveyMedia`, `SurveyChecklistResult`.
- Integration: Google Drive service account (config `appsettings`).
- `POST /api/surveys/{id}/media` (multipart).
- Background job: `SurveyDriveSyncService` (queue).

**🧪 Verification**
- Unit: rule size, count.
- Integration: mock Drive API để test flow sync (retry, fail).
- E2E: upload ảnh từ mobile viewport.

---

#### Subtask NIH-99/100/101 – áp dụng chuẩn CRUD + rule story cha; Detail có gallery ảnh, map location (nếu có geo), timeline sync status.

---

### 🟧 NIH-87 · [M1] Quản lý Hợp đồng (Sales Contract)

**🎯 Business Objective**
Từ báo giá đã duyệt hoặc gói thầu trúng → tạo hợp đồng chính với khách hàng; theo dõi mốc thanh toán, phụ lục, và chuyển thông tin sang Design/Finance/Project.

**✅ Acceptance Criteria**
1. Hợp đồng bắt buộc gắn với 1 `Khách hàng` và tối thiểu 1 `Báo giá đã duyệt` **hoặc** 1 `Gói thầu Trúng`.
2. Trường: `Số hợp đồng` (unique, format do master data — VD `HD/YYYY/###`), `Ngày ký`, `Ngày bắt đầu`, `Ngày kết thúc dự kiến`, `Giá trị hợp đồng`, `Phạm vi công việc (rich-text)`.
3. Upload file bản scan hợp đồng đã ký (bắt buộc trước khi chuyển trạng thái sang **Đang thực hiện**).
4. **Lịch thanh toán**: danh sách các mốc (Tạm ứng %, Sau nghiệm thu móng %, …), tổng % = 100. Mỗi mốc có ngày dự kiến + trạng thái (Chưa/Đã thanh toán).
5. **Phụ lục (VO)**: 1 hợp đồng có N phụ lục, mỗi phụ lục có giá trị +/- và lý do; tổng giá trị hợp đồng update tự động.
6. Trạng thái: **Draft · Đã ký · Đang thực hiện · Tạm dừng · Hoàn thành · Huỷ**. `Đã ký → Đang thực hiện` yêu cầu file scan + xác nhận Sales Manager.
7. Chuyển sang **Đang thực hiện** → auto tạo 1 `DesignProject` (nếu chưa có) và gắn hợp đồng vào — làm đầu vào cho M2.
8. Nhắc nhở: `Ngày kết thúc dự kiến - now ≤ 30 ngày` mà chưa Hoàn thành → notify PM + Sales.
9. Chăm sóc post-contract: Sales có thể tạo activity "Cơ hội tái ký" gắn hợp đồng cũ → tự tạo Lead mới.
10. Tìm kiếm/lọc theo KH, trạng thái, khoảng ngày ký, giá trị min-max.

**🔌 API / Data Contract**
- Entity: `SalesContract`, `PaymentMilestone`, `ContractAppendix (VO)`, `ContractAttachment`.
- Event bus (nội bộ) khi chuyển sang Đang thực hiện → tạo `DesignProject`.

**🧪 Verification**
- Unit: rule tổng % thanh toán, unique số hợp đồng, VO cập nhật tổng.
- Integration: chuyển trạng thái tạo Design project.

---

#### Subtask NIH-102/103/104 – áp dụng chuẩn CRUD + rule story cha; Detail có 5 tab: **Thông tin · Lịch thanh toán · Phụ lục VO · Tài liệu · Timeline**.

---

### 🟨 NIH-113 · [M2] Dự án thiết kế – Tổng quan

**🎯 Business Objective**
Sau khi có Hợp đồng, mở dự án thiết kế; PM/Design Lead nhìn 1 màn thấy tiến độ 3 giai đoạn Concept → Basic → Shop Drawing.

**✅ Acceptance Criteria**
1. `DesignProject` tự tạo khi hợp đồng chuyển **Đang thực hiện**; cho phép Admin tạo tay khi cần.
2. Bắt buộc: `Mã dự án` (unique, format từ master data), `Tên`, `KH`, `Hợp đồng liên kết`, `PM`, `Design Lead`, `Ngày bắt đầu`, `Deadline tổng`.
3. Header hiển thị **progress bar 3 giai đoạn** với % complete và trạng thái từng stage.
4. Detail có tabs: **Overview · Concept · Basic Design · Shop Drawing · Revision · IFC · Tài liệu · Team**.
5. Team: gán KTS/MEP/Kết cấu theo bộ môn (từ master data).
6. Cảnh báo tự động khi 1 stage quá deadline nội bộ.
7. Danh sách filter theo PM, khách hàng, trạng thái tổng, deadline sắp tới.

---

#### Subtask NIH-119/120/121 – CRUD chuẩn + rule story cha.

---

### 🟨 NIH-114 · [M2] Concept (Giai đoạn 1)

**✅ Acceptance Criteria**
1. Một `DesignProject` có N phương án Concept (song song), mỗi phương án có version.
2. Upload: mô hình 3D (`.skp`, `.rvt`, `.ifc`, `.glb`, `.pdf`), mặt bằng công năng, hình ảnh trình bày.
3. Section **Feedback khách hàng**: comment thread với reference tới file cụ thể.
4. Trạng thái phương án: **Đang thiết kế · Chờ duyệt nội bộ · Đã trình khách · Khách yêu cầu sửa · Đã chốt · Loại bỏ**.
5. Chỉ 1 phương án được đánh dấu **Đã chốt**; khi chốt → khóa các phương án còn lại (Loại bỏ), unlock stage Basic Design.
6. Có nút "Tạo bản trình khách": xuất PDF gồm ảnh + mặt bằng + mô tả.
7. Audit + Notification cho Design Lead khi có feedback mới.

#### Subtask NIH-122/123/124 – CRUD + rule story cha.

---

### 🟨 NIH-115 · [M2] Basic Design (Giai đoạn 2)

**✅ Acceptance Criteria**
1. Chỉ unlock khi Concept đã chốt.
2. Hồ sơ phân loại theo bộ môn: **Kiến trúc · Kết cấu · MEP** (master data).
3. Có checklist hồ sơ mặc định (bản vẽ + thuyết minh kỹ thuật) cho mỗi bộ môn.
4. Bản vẽ có `Mã bản vẽ` unique trong dự án, format từ master data (VD `KT-BD-###`).
5. Trạng thái hồ sơ: **Đang làm · Đã submit review · Đã duyệt nội bộ · Đã submit xin phép · Đã cấp phép · Từ chối**.
6. Đường liên kết sang Module Permitting: mỗi bản vẽ Basic Design có thể attach vào 1 checklist item pháp lý (GPXD).
7. Không cho chuyển sang Shop Drawing nếu chưa có hồ sơ Basic Design **Đã duyệt nội bộ** ở đủ 3 bộ môn.

#### Subtask NIH-125/126/127 – CRUD + rule story cha.

---

### 🟨 NIH-116 · [M2] Shop Drawing (Giai đoạn 3)

**✅ Acceptance Criteria**
1. Tổ chức theo **Bộ môn** (Kiến trúc/Kết cấu/MEP/Nội thất) → **Hạng mục thi công** → **Bản vẽ**.
2. Bản vẽ có: `Mã bản vẽ` unique/dự án, `Bộ môn`, `Hạng mục`, file gốc + file PDF preview.
3. Review chéo giữa bộ môn: KTS review MEP, MEP review Kết cấu … workflow config được.
4. Trạng thái: **Đang vẽ · Review chéo · Chờ Design Lead duyệt · Đã duyệt · Chờ IFC · Đã phát hành IFC**.
5. Comment/markup trên PDF (giai đoạn 1: comment text; markup vẽ để dời sang GĐ3 nếu phức tạp).
6. Filter theo bộ môn + hạng mục + trạng thái.

#### Subtask NIH-128/129/130 – CRUD + rule story cha.

---

### 🟨 NIH-117 · [M2] Revision Control

**✅ Acceptance Criteria**
1. Mỗi bản vẽ (bất kỳ giai đoạn) có N revision, đánh số R0, R1, R2… tự tăng.
2. Tạo revision mới bắt buộc: `Lý do thay đổi` (dropdown master data: Yêu cầu khách / Sai kỹ thuật / Đồng bộ MEP / Điều chỉnh vật liệu / Khác) + `Ghi chú`.
3. Revision cũ tự động chuyển **Thu hồi**; UI phải cảnh báo đỏ khi mở bản đã thu hồi.
4. Chỉ 1 revision được đánh dấu **Đang sử dụng** (mặc định là revision mới nhất đã duyệt).
5. Diff giữa 2 revision: hiển thị metadata thay đổi + link tải cả 2 file để so sánh bằng eye.
6. Notification bắt buộc gửi cho tất cả bộ phận liên quan (Design team + PM + đội thi công nếu đã IFC).
7. Audit log không được xoá; revision không được xoá vật lý.

#### Subtask NIH-131/132/133 – CRUD + rule story cha.

---

### 🟨 NIH-118 · [M2] Phát hành IFC (Issued For Construction)

**✅ Acceptance Criteria**
1. **Phiếu phát hành IFC** = 1 bộ nhiều bản vẽ Shop Drawing đã `Đã duyệt`, gộp lại phát hành cùng lúc.
2. Phiếu bắt buộc: `Số phiếu` (unique format), `Ngày phát hành`, `Nơi nhận` (multi: nhà thầu chính, giám sát, chủ đầu tư), `Người phát hành`, `Ghi chú`.
3. Trước khi phát hành → workflow duyệt: Design Lead + PM cùng ký.
4. Sau phát hành → mỗi bản vẽ trong bộ được:
   - Đóng **dấu điện tử "ISSUED FOR CONSTRUCTION"** (watermark PDF).
   - **Khoá chỉnh sửa** (mọi thay đổi phải qua Revision mới).
5. Xuất bộ hồ sơ IFC dạng zip có index.pdf mục lục.
6. Ghi nhận xác nhận đã nhận từ từng nơi nhận (email confirm hoặc upload biên bản).
7. Lịch sử phát hành hiển thị per dự án; filter theo ngày.

#### Subtask NIH-134/135/136 – CRUD phiếu phát hành + rule story cha.

---

### 🟩 NIH-137 · [M3] Checklist pháp lý

**🎯 Business Objective**
Đảm bảo dự án có đủ giấy phép trước khi thi công; theo dõi trạng thái nộp/duyệt và cảnh báo hồ sơ sắp hết hạn.

**✅ Acceptance Criteria**
1. Mỗi dự án tự sinh checklist mặc định từ template master data. Template gồm: **GPXD · PCCC · Cấp điện · Cấp nước · Vỉa hè · An toàn lao động · Môi trường · Hoàn công**.
2. Mỗi item pháp lý có: `Loại giấy phép`, `Cơ quan cấp`, `Người phụ trách`, `Deadline mục tiêu`, `Ngày nộp`, `Ngày cấp`, `Ngày hết hạn`, `File hồ sơ nộp`, `File giấy phép đã cấp`, `Trạng thái`.
3. Trạng thái: **Chưa chuẩn bị · Đang chuẩn bị · Đã nộp · Đang thẩm định · Bổ sung hồ sơ · Đã cấp · Bị từ chối · Hết hạn**.
4. Có timeline làm việc: mỗi lần gặp/gọi/nhận công văn → 1 entry (loại, ngày, người liên hệ, ghi chú, đính kèm).
5. Cảnh báo tự động:
   - Deadline mục tiêu - now ≤ 7 ngày và chưa **Đã cấp** → warning.
   - Ngày hết hạn - now ≤ 30 ngày → warning (chỉ áp dụng loại có hạn: PCCC…).
6. BGĐ có view "Rủi ro pháp lý toàn công ty" — list các item overdue/expiring across projects.
7. Không cho chuyển `DesignProject` sang stage Thi công (GĐ3) nếu chưa có GPXD `Đã cấp`.
8. Attach hồ sơ nộp: cho phép chọn từ **Basic Design** bản vẽ + thuyết minh (tránh upload lặp).

**🔌 API / Data Contract**
- Entity: `PermitChecklistItem`, `PermitActivity`, `PermitTemplate` (master).
- Cron job `PermitExpiryCheckJob` daily.

**🧪 Verification**
- Unit: rule cảnh báo, block sang thi công.
- Integration: seed template + auto-generate cho project mới.

#### Subtask NIH-138/139/140 – CRUD + rule story cha.

---

## 6. Rủi ro & lưu ý

1. **Google Drive integration** (dùng ở M1 Khảo sát + M2 file lưu trữ + M7 tự tạo cây thư mục) cần credential + rate-limit check sớm — nên spike ở tuần 1 dù story chính thức ở GĐ3.
2. **PDF export** (Báo giá, IFC watermark, Hoàn công) — cần chọn thư viện chung (QuestPDF cho .NET đã tốt); tránh dùng lib khác nhau mỗi module.
3. **RBAC/Permission granularity** — hiện tại code base đã có `PermissionAuthorizationFilter`; cần bổ sung permission per-entity-scope (Sales chỉ xem KH mình phụ trách) — story riêng ở P0.
4. **Notification foundation** — cần EmailService + In-app notification store. Đề xuất dùng chung `NotificationsController` đã tồn tại.
5. **Master data seed** — cần map với `Data/Seeds/*.json` hiện có, viết `Migration + Seeder` mới thay vì hard-code trong controller.
6. **i18n** — theo `CLAUDE.md`: mọi string mới phải seed 4 ngôn ngữ (vi/en/zh/ja) qua `TranslationSeeder`.

---

## 7. Action tiếp theo

- [ ] Chốt priority list này với PM/BGĐ.
- [ ] Tạo 4 Jira story còn thiếu (Lead · Master data · RBAC seed · Notification foundation).
- [ ] Áp DoD mới lên toàn bộ 55 issue trong GĐ2 (bằng script gọi Jira REST — sẵn sàng thực hiện nếu được phê duyệt).
- [ ] Split team theo 2 stream: **Sales stream** (M0→M1) và **Design/Permit stream** (M2/M3), sync tại điểm giao Contract ↔ DesignProject.
