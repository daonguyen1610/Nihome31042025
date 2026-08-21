# Gói A — CRM demo-ready: nối chuỗi nghiệp vụ và sửa lỗi chặn vận hành

**Ngày:** 2026-08-20
**Jira:** NIH-435 (NIH-436 → NIH-446)
**Trạng thái:** chờ duyệt

---

## 1. Bối cảnh

Khách hàng review module CRM và ghi nhận 11 lỗi (NIH-435). Ngoài lỗi thao tác,
phản hồi chung là hệ thống "quá cơ bản, thiếu liên kết, chưa có quy trình rõ ràng".

Khảo sát code cho thấy chẩn đoán này chỉ đúng một nửa. Backend đã có chiều sâu
đáng kể: pipeline Kanban cho cơ hội, báo giá hai phương pháp (suất đầu tư + BOQ)
với version và approval log, stage-gating thật sự cho module Thiết kế, và
hợp đồng tự sinh dự án thiết kế khi chuyển sang `InProgress`.

Vấn đề thật nằm ở ba chỗ:

1. **Chuỗi nghiệp vụ đứt ở các mối nối.** Lead convert không tạo ra gì; hợp đồng
   không nhận `quoteId`/`opportunityId`; dự án thiết kế đã tự sinh nhưng không
   hiển thị ở đâu cả.
2. **Thiếu guard theo trạng thái.** Cơ hội Lost vẫn báo giá được; lưu báo giá
   không đổi gì vẫn bump version.
3. **Một số lỗi FE chặn thao tác cơ bản** khiến người dùng không đi hết được luồng.

Gói A xử lý cả ba, ưu tiên những item chứng minh được luồng nghiệp vụ chạy thông
trong buổi demo.

## 2. Mục tiêu

Sau gói A, một người vận hành phải đi được trọn luồng sau mà không gặp lỗi chặn:

```
Lead → Chuyển đổi → Khách hàng + Cơ hội → Báo giá → Duyệt → Hợp đồng
     → InProgress → Dự án thiết kế (nhìn thấy được từ hợp đồng)
```

## 3. Ngoài phạm vi

Các hạng mục dưới đây **cố ý hoãn**, đã thống nhất với chủ sản phẩm:

- Thực thể **Dự án vận hành** và luồng Hợp đồng → Dự án vận hành (row 21 của
  bảng yêu cầu). `Project` hiện tại là entity marketing website
  (`Slug`, `GalleryJson`, `ContentJson`) — không có `CustomerId`, `ContractId`,
  ngân sách hay PM.
- **Nợ kỹ thuật được ghi nhận:** `Survey.LinkedProjectId` đang trỏ vào entity
  marketing nói trên — sai ngữ nghĩa. Chấp nhận tạm cho tới khi làm Dự án vận hành.
  Sáu màn hình module Thi công cũng chưa có dự án để bám vào.
- Action theo ngữ cảnh trên mọi màn hình, timeline gộp trên Customer detail,
  Survey link Lead, gắn `construction_type` — thuộc **gói B**.
  (**Tạo hợp đồng từ báo giá đã được kéo vào gói A** — xem A9. Để nó ở gói B
  trong khi vẫn tuyên bố gói A demo được đầu-cuối là mâu thuẫn phạm vi, vì chuỗi
  sẽ đứt đúng chỗ khách đang chê.)
- Tách menu và danh sách xuyên dự án cho module Thiết kế — thuộc **gói C**.
- Regroup/giảm trường trong form (phần còn lại của NIH-439, và NIH-440, NIH-441)
  — thuộc gói B/C, làm sau khi chốt workflow để tránh sửa form hai lần.

---

## 4. Hạng mục

### A1 — Lead convert tạo Customer + Opportunity (NIH-436)

**Hiện trạng.** `LeadService.ConvertAsync` nhận `request.CustomerId` và
`request.OpportunityId` từ ngoài vào và chỉ gán vào lead
(`LeadService.cs:313-316`) — nó **không tạo mới gì cả**. Frontend gọi
`adminApi.convertLead(detail.id)` **không truyền body** (`Leads.tsx:266`).
Kết quả: lead chuyển sang `Converted` với `ConvertedCustomerId` và
`ConvertedOpportunityId` đều `null`.

**Yêu cầu.** Chuyển đổi phải sinh ra khách hàng và cơ hội thật, trong một
transaction, và liên kết ngược về lead.

**Cách làm.**

`ConvertLeadRequest` giữ `CustomerId`/`OpportunityId` là optional với ngữ nghĩa
mới: có giá trị nghĩa là "gắn vào bản ghi có sẵn", để trống nghĩa là "tạo mới".

Khi tạo mới Customer, ánh xạ từ lead:

| Trường Customer | Nguồn từ Lead |
|---|---|
| `Type` | `Company` nếu `CompanyName` có giá trị, ngược lại `Individual` |
| `Name` | `CompanyName` nếu là Company, ngược lại `Name` |
| `RepresentativeName` | `Name` khi là Company, ngược lại `null` |
| `SourceCode` | `SourceCode` của lead |
| `RelationshipStatus` | `Prospect` |
| `OwnerUserId` | `OwnerUserId` của lead |

`Phone`/`Email` của lead tạo thành một `CustomerContact` với cờ liên hệ chính.

**Lead không mang đủ dữ liệu cho khách hàng doanh nghiệp.**
`CustomerService.ValidateForType` (`CustomerService.cs:445-456`) bắt buộc khách
hàng `Company` phải có `TaxId`, `Address` và `RepresentativeName`. Model `Lead`
**không có `TaxId` lẫn `Address`**. Nên ánh xạ thẳng ở bảng trên sẽ ném exception
mỗi lần convert một lead doanh nghiệp.

**Cách xử lý — bổ sung qua dialog, không nới validation.** Nút Chuyển đổi mở một
dialog thay vì gọi API ngay:

- Dialog prefill sẵn từ lead những gì có (tên, công ty, phone, email, nguồn).
- Nếu lead có `CompanyName` → dialog hiện thêm ba trường **bắt buộc**: MST,
  địa chỉ đăng ký, người đại diện (prefill `RepresentativeName` bằng tên lead).
- Nếu lead không có `CompanyName` → tạo `Individual`, không hỏi thêm gì.
- Dialog cũng cho phép chọn khách hàng có sẵn thay vì tạo mới, phục vụ nhánh
  "gắn vào bản ghi có sẵn" và phục vụ xử lý 409 trùng.

Không nới `ValidateForType` và không lách bằng cách tạo `Individual` rồi nhét tên
công ty vào note — cả hai đều đẻ ra dữ liệu khách hàng sai loại, mà module Hợp
đồng và Báo giá phía sau lại đọc đúng loại này.

Khi tạo mới Opportunity: `CustomerId` trỏ vào customer vừa tạo,
`Stage = Prospecting`, `OwnerUserId` kế thừa từ lead, `Name` suy ra từ tên lead.

Toàn bộ nằm trong một transaction. Ghi một `LeadActivity` mô tả kết quả
chuyển đổi kèm id của hai bản ghi vừa tạo.

**Chặn tổ hợp vô nghĩa.** Nếu request có `OpportunityId` thì cơ hội đó đã thuộc về
một khách hàng rồi. Nên khi có `OpportunityId`, `CustomerId` phải hoặc để trống
(suy ra từ `opportunity.CustomerId`) hoặc trùng khớp với nó — mọi tổ hợp khác đều
bị từ chối. Không có chuyện tạo khách hàng mới rồi gắn vào một cơ hội có sẵn.

Quy tắc này khép kín bốn tổ hợp của A2: chỉ còn "cả hai mới", "khách cũ + cơ hội
mới", và "cả hai đều cũ" là hợp lệ.

**Kiểm tra trùng.** `CustomerService` đã có cơ chế phát hiện trùng theo `TaxId`
(Company) và `Phone` (Individual), trả 409 kèm `CustomerDuplicateResponse`.
Luồng convert phải tôn trọng cơ chế này: nếu phát hiện trùng, trả 409 cho FE
để người dùng chọn gắn vào khách hàng có sẵn thay vì tạo bản ghi trùng.

**Acceptance criteria.**

- Convert một lead không truyền id → tạo mới cả Customer lẫn Opportunity, lead
  có đủ `ConvertedCustomerId` và `ConvertedOpportunityId`.
- Convert lead **có `CompanyName`** mà chưa nhập MST/địa chỉ/người đại diện →
  dialog chặn submit và chỉ rõ trường thiếu; không có request nào bắn đi.
- Convert lead **không có `CompanyName`** → tạo `Individual`, dialog không hỏi
  thêm trường doanh nghiệp nào.
- Convert một lead có truyền `CustomerId` → dùng lại customer đó, chỉ tạo Opportunity.
- Lead có phone/taxId trùng khách hàng đã tồn tại → trả 409 kèm thông tin bản ghi
  trùng, không tạo gì.
- Convert thất bại giữa chừng → không để lại customer hoặc opportunity mồ côi.

---

### A2 — Undo/reopen lead trong khung thời gian ngắn (NIH-437)

**Hiện trạng.** `LeadService.UpdateAsync` chặn cứng mọi chỉnh sửa lead đã
convert (`LeadService.cs:205-207`). Không có đường quay lại.

**Yêu cầu.** Sale bấm nhầm Chuyển đổi phải hoàn tác được, nhưng không được
xoá mất dữ liệu người khác đã nhập.

**Quy tắc.** Endpoint mới `POST /api/leads/{id}/unconvert`. Ba nhánh:

**Chỉ được xoá bản ghi do chính convert sinh ra.** Nếu người dùng convert bằng
cách *gắn vào* một khách hàng có sẵn, unconvert tuyệt đối không được xoá khách
hàng đó — nó tồn tại trước và thuộc về người khác.

Cách nhận biết mà không cần thêm cột: **A1 phải đóng dấu cùng một mốc thời gian**
cho `lead.ConvertedAt`, `customer.CreatedAt` và `opportunity.CreatedAt` — dùng
đúng một biến `now` cho cả transaction. A2 coi một bản ghi là auto-created khi
`CreatedAt` của nó trùng khít `lead.ConvertedAt`.

Đây là ràng buộc thiết kế giữa A1 và A2, phải có unit test khoá lại. Nếu sau này
ai đó phá vỡ nó, A2 sẽ ngừng nhận diện được auto-created và **rơi về nhánh gỡ
liên kết** — tức hỏng theo hướng an toàn, không bao giờ xoá nhầm.

Trước hết định nghĩa **"cơ hội còn sạch"** — dùng chung cho các nhánh dưới:
`ConvertedAt` cách hiện tại dưới **24 giờ**, Opportunity vẫn ở
`Stage = Prospecting`, chưa có Quote, Survey hay Contract nào trỏ vào, và không có
`Tender` nào có `WonOpportunityId` trỏ vào nó.

**Nhánh 1 — xoá cả hai.** Khi Customer *và* Opportunity đều auto-created, cơ hội
còn sạch, và Customer đó không có Opportunity nào khác lẫn Contract nào.
Hành động: xoá Opportunity và Customer cùng các `CustomerContact` con.

**Nhánh 2 — xoá cơ hội, giữ khách hàng.** Khi Customer là bản ghi **có sẵn** được
gắn vào lúc convert, còn Opportunity là auto-created và còn sạch.
Hành động: chỉ xoá Opportunity. Customer giữ nguyên vì nó tồn tại từ trước và
không thuộc về lần convert này.

Nhánh này là trường hợp phổ biến — sale gặp lại khách cũ nên gắn vào khách hàng có
sẵn thay vì tạo mới. Thiếu nhánh này thì mỗi lần bấm nhầm sẽ để lại một cơ hội mồ
côi nghiệp vụ mà không ai dọn.

**Nhánh 3 — chỉ gỡ liên kết.** Mọi trường hợp còn lại: quá 24 giờ, cơ hội đã phát
sinh dữ liệu con, hoặc Opportunity cũng là bản ghi có sẵn được gắn vào.
Hành động: giữ nguyên Customer và Opportunity, chỉ clear ba trường liên kết trên lead.

**Cả ba nhánh** đều đặt lead về `Interested` và clear
`ConvertedAt`/`ConvertedCustomerId`/`ConvertedOpportunityId`.

FE phải nói rõ nhánh nào đã chạy và bản ghi nào được giữ lại, kèm link tới chúng —
người dùng không được phép đoán xem khách hàng của mình còn hay mất.

**Vì sao `Interested`.** Enum `LeadStatus` là
`New, Contacted, Interested, NotInterested, Converted, Junk` — không có
`Qualified`. `Interested` là trạng thái tiền-chuyển-đổi hợp lý nhất, và chọn nó
giúp **tránh phải thêm cột `StatusBeforeConvert`**, tức tránh một EF migration
cho gói A. Nếu sau này cần khôi phục chính xác trạng thái cũ thì mới thêm cột.

Mọi lần unconvert đều ghi `LeadActivity` nêu rõ đã đi nhánh nào.

**UI.** Dời nút Chuyển đổi sang trái khỏi cụm nút nguy hiểm và thêm bước xác nhận
nêu rõ sẽ tạo ra những gì. Đây là phần "hạn chế nhầm lẫn" của ticket.

**Acceptance criteria.**

- Convert tạo mới cả hai, unconvert trong 24h khi cơ hội còn sạch → cả customer
  lẫn opportunity biến mất, lead về `Interested`. (Nhánh 1)
- Convert gắn vào **khách hàng có sẵn**, unconvert trong 24h khi cơ hội còn sạch →
  **opportunity biến mất, customer còn nguyên**, lead về `Interested`. (Nhánh 2)
- Unconvert sau khi đã tạo báo giá cho cơ hội → cả hai bản ghi được giữ, lead vẫn
  về `Interested`, thông báo nêu rõ điều này. (Nhánh 3)
- Unconvert sau 24h → luôn đi nhánh 3, kể cả khi cơ hội còn sạch.
- Convert gắn vào **cơ hội có sẵn**, unconvert → luôn đi nhánh 3, không xoá gì.
- Lead đã unconvert convert lại được bình thường.

---

### A3 — Cơ hội Lost/Won không tạo được báo giá (NIH-443)

**Hiện trạng.** `QuoteService.CreateAsync` load opportunity rồi **không hề kiểm
tra `Stage`**. Cơ hội đã `Lost` vẫn tạo báo giá mới được.

**Cách làm.** Thêm guard: từ chối tạo báo giá khi `Stage` là `Won` hoặc `Lost`,
kèm thông báo tiếng Việt nêu rõ lý do. Đồng thời chặn `Submit` và `Send` trên
báo giá thuộc cơ hội đã Lost — vì cơ hội có thể chuyển Lost *sau khi* báo giá
đã được tạo.

Không chặn `UpdateAsync` và không chặn xem: báo giá cũ vẫn phải đọc và sửa
được ghi chú để phục vụ đối soát.

**Acceptance criteria.**

- Tạo báo giá trên cơ hội Lost → 400 với thông báo rõ ràng, không tạo bản ghi.
- Cơ hội chuyển Lost sau khi có báo giá Draft → báo giá đó không submit được.
- Báo giá đã Approved thuộc cơ hội Lost vẫn xem được.

---

### A4 — Dirty-check trước khi bump version báo giá (NIH-445)

**Hiện trạng.** `QuoteService.UpdateAsync` xác định `isPostApproval` **chỉ dựa
trên `Status`** (`QuoteService.cs:226-231`). Mọi PUT lên báo giá đang
`Approved`/`SentToCustomer`/`Expired` đều: ghi snapshot, tăng `Version`, reset
`Status` về `Draft`, xoá sạch các mốc `SubmittedAt`/`ApprovedAt`/`SentAt`, và
ghi một `QuoteApprovalLog` hành động `NewVersion` — **kể cả khi không đổi gì**.

**Cách làm.** Trước khối `isPostApproval`, so payload với giá trị hiện tại.
Chỉ bump khi có thay đổi thực chất.

Tập trường coi là thay đổi thực chất:
- Chế độ suất đầu tư: `AreaSqm`, `UnitPricePerSqm`, `PackageDescription`
- Chế độ BOQ: tập `Items` (thêm, xoá, đổi số lượng, đơn giá, mô tả)
- Chung: `DiscountPercent`, `VatPercent`, `ValidUntil`

Không coi là thay đổi thực chất: `OwnerUserId`, `Note`. Đổi riêng hai trường này
thì lưu bình thường, giữ nguyên `Version` và `Status`.

**Ba mức hành vi.** Cần phân biệt "không đổi gì" với "đổi thứ không ảnh hưởng
giá" — đây là hai chuyện khác nhau và phải xử lý khác nhau:

| Trường hợp | Version | Status | Log |
|---|---|---|---|
| PUT không đổi bất cứ trường nào | giữ nguyên | giữ nguyên | **không ghi log nào** |
| Chỉ đổi `Note` và/hoặc `OwnerUserId` | giữ nguyên | giữ nguyên | ghi `Update` |
| Đổi trường ảnh hưởng giá hoặc `ValidUntil` | bump | về `Draft` | ghi `NewVersion` (và `Update` như hiện tại) |

**Log `Update` hiện đang vô điều kiện.** `QuoteService.cs:273-281` ghi
`QuoteWorkflowAction.Update` trên **mọi** PUT, kể cả no-op. Nên A4 không chỉ gác
khối bump version mà còn phải gác cả khối log này bằng cờ "có thay đổi gì không".

**Hệ quả nữa của no-op, không chỉ là log thừa.** `QuoteService.cs:268-271` gọi
`db.QuoteItems.RemoveRange(quote.Items)` rồi dựng lại toàn bộ dòng BOQ trên mỗi
PUT. Một PUT không đổi gì vẫn xoá và chèn lại toàn bộ item với `Id` mới. Vì vậy
nhánh no-op phải **thoát sớm trước khi chạm vào entity**, không phải chỉ bỏ qua
phần ghi log — nếu không, `Id` của các dòng BOQ vẫn đổi và mọi thứ tham chiếu tới
chúng vẫn bị ảnh hưởng.

**Không cần thêm cơ chế audit ownership mới.** `QuoteWorkflowAction` không có khái
niệm reassign; log `Update` ở trên đã đủ ghi lại việc đổi người phụ trách.

So sánh số thập phân phải dùng so sánh theo giá trị, không so tham chiếu, và
chuẩn hoá `null` với `0` trước khi so để tránh bump giả do FE gửi `0` thay vì bỏ trống.

**Acceptance criteria.**

- PUT không đổi gì lên báo giá Approved → `Version` giữ nguyên, `Status` vẫn
  `Approved`, **không sinh `QuoteApprovalLog` nào**, và `Id` của các dòng BOQ
  không đổi.
- Đổi mỗi `Note` → `Version` và `Status` giữ nguyên, **có** một log `Update`.
- Đổi mỗi `OwnerUserId` → `Version` và `Status` giữ nguyên, **có** một log `Update`.
- Đổi `DiscountPercent` → bump version, về `Draft`, có log `NewVersion`.
- Thêm một dòng BOQ → bump version.

---

### A5 — Lỗi 409 khi tạo dự án thiết kế

**Nguyên nhân gốc.** Hai thứ cộng lại:

1. `AppDbContext.cs:891` khai báo
   `HasIndex(dp => dp.ContractId).IsUnique().HasFilter("[ContractId] IS NOT NULL")`
   — một hợp đồng chỉ được có một dự án thiết kế, cưỡng chế ở tầng DB.
2. `ContractService.cs:324-336` **tự tạo** dự án thiết kế khi hợp đồng chuyển
   sang `InProgress`, qua `EnsureForContractAsync` (idempotent, đã có unit test).

Người dùng không biết dự án đã tồn tại, chọn lại chính hợp đồng đó trong form
tạo dự án thiết kế, submit → vi phạm unique index → 409 thô.

**Cách làm.** Bổ sung bốn trường vào `ContractResponse`: `DesignProjectId`,
`DesignProjectCode`, `DesignProjectName`, `DesignProjectCurrentStage`.

Cần đủ cả bốn vì A6 phải hiện mã, tên **và** giai đoạn hiện tại. Nếu chỉ đưa id và
code thì A6 sẽ phải gọi thêm `GET /api/design-projects/{id}` chỉ để lấy hai chuỗi —
thêm một round-trip cho mỗi lần mở chi tiết hợp đồng, trong khi dữ liệu đã nằm sẵn
trong cùng một join.

Đây là **trường dẫn xuất, chỉ đọc** — lấy bằng LEFT JOIN từ `design_projects` trong
truy vấn của `ContractService` (`ListAsync` và `GetAsync`), **không thêm cột nào vào
bảng `contracts`**, do đó không cần EF migration. Join bám vào chính unique index
trên `ContractId` nên rẻ. `CurrentStage` trả về dạng chuỗi, khớp cách
`DesignProjectResponse` đang làm. Truy vấn đọc dùng `AsNoTracking()`. Từ đó:

- Dropdown chọn hợp đồng trong form tạo dự án thiết kế lọc bỏ / disable những
  hợp đồng đã có dự án, kèm nhãn giải thích.
- Nếu vẫn xảy ra 409 (ví dụ hai người thao tác đồng thời), map lỗi unique
  violation thành thông báo tiếng Việt kèm link mở dự án hiện có, thay vì hiện
  lỗi kỹ thuật.

Trường này cũng phục vụ A6 — đó là lý do A5 và A6 nằm chung một spec.

**Acceptance criteria.**

- **Mọi hợp đồng đã có dự án thiết kế** đều không xuất hiện như lựa chọn tạo mới
  trong form, bất kể trạng thái hợp đồng. Unique index áp lên `ContractId` chứ
  không áp theo `Status`, và dự án thiết kế tạo tay được cho cả hợp đồng `Draft`.
- Gặp 409 → thông báo tiếng Việt kèm link, không hiện mã lỗi.
- Hợp đồng chưa có dự án vẫn tạo được bình thường, kể cả khi đang `Draft`.

---

### A6 — Hiển thị dự án thiết kế trên chi tiết hợp đồng

**Hiện trạng.** Mối nối đã chạy ở backend nhưng vô hình với người dùng:
`ContractDetail.tsx` không nhắc gì tới dự án thiết kế.

**Cách làm.** Thêm một khối trên trang chi tiết hợp đồng, dùng bốn trường dự án
thiết kế trên `ContractResponse` từ A5 — không gọi thêm API nào:

- Đã có dự án → hiện mã, tên, giai đoạn hiện tại, và link sang chi tiết dự án.
- Chưa có và hợp đồng chưa `InProgress` → dòng giải thích rằng dự án sẽ được tạo
  khi hợp đồng chuyển sang Đang thực hiện.
- Chưa có và hợp đồng đã `InProgress` (auto-create từng thất bại — nó là
  best-effort, nuốt exception) → nút "Tạo dự án thiết kế".

Nút này cần một endpoint mới `POST /api/contracts/{id}/design-project`, uỷ quyền
cho `IDesignProjectService.EnsureForContractAsync` (đã tồn tại và idempotent).
Hiện `DesignProjectsController` chỉ có CRUD thuần và không phơi ra hàm này, nên
frontend chưa có cách nào gọi tới. Endpoint dùng chung quyền với thao tác tạo dự
án thiết kế, và trả về `DesignProjectResponse` để FE điều hướng thẳng sang.

**Vì sao item này không được cắt.** Cùng với A1, đây là bằng chứng khả kiến duy
nhất trong gói A cho thấy luồng nghiệp vụ nối thông đầu-cuối. Chi phí thấp vì
backend đã xong.

**Acceptance criteria.**

- Hợp đồng `InProgress` hiện đúng dự án thiết kế và bấm sang được.
- Hợp đồng `Draft` hiện dòng giải thích, không hiện nút.
- Hợp đồng `InProgress` mà auto-create đã thất bại → nút tạo hoạt động và idempotent.

---

### A7 — Ba lỗi chặn thao tác

#### NIH-438 — Notification 404

**Nguyên nhân gốc, rộng hơn ticket mô tả.** `LeadService.cs:490` phát
`linkUrl: "/admin/leads/{id}"` nhưng **App.tsx không có route `/admin/leads/:id`**
— chỉ có `/admin/leads`. Điều hướng rơi vào `NotFound`.

Cùng lỗi này tồn tại ở hai chỗ nữa:
- `OpportunityService.cs:524` và `:553` phát `/admin/opportunities/{id}` — không có route.
- `RoleService.cs:177, 284, 372` phát `/admin/roles/{id}` — không có route.

(`/admin/tenders/{id}` thì có route, nên không dính.)

**Cách làm.** Thêm route `/admin/leads/:id`, `/admin/opportunities/:id`,
`/admin/roles/:id` render đúng trang danh sách hiện có và **tự mở dialog chi tiết**
của bản ghi tương ứng. Không đổi backend.

Chọn cách này thay vì đổi `linkUrl` thành dạng query-param vì URL giữ được hình
dạng đúng với yêu cầu "Detail page" trong bảng yêu cầu của khách — sau này khi
xây trang chi tiết thật, URL không phải đổi lần nữa.

**Acceptance criteria.** Bấm notification lead, cơ hội, vai trò đều mở đúng bản
ghi. Truy cập trực tiếp URL với id không tồn tại → thông báo lỗi trong trang,
không phải trang 404 toàn cục.

#### NIH-442 — Ô search hợp đồng không gõ, không xoá được

**Nguyên nhân gốc.** `Contracts.tsx:428` early-return
`if (loading && contracts.length === 0)` render `<PageLoading />` **thay cho toàn
bộ trang**, kể cả panel filter chứa ô search. `load()` chạy lại mỗi lần
`search` đổi và set `loading = true`.

Chuỗi sự kiện: gõ tới khi không còn kết quả → `contracts` rỗng → ký tự tiếp theo
làm `loading && contracts.length === 0` thành true → cả trang unmount → input mất
focus và mất luôn khả năng gõ hoặc xoá. Đây là lý do lỗi trông như lúc có lúc không:
nó chỉ xuất hiện sau khi kết quả về rỗng.

**Cách làm.** Tách `initialLoading` khỏi `refreshing`. Chỉ `initialLoading` được
phép early-return; refresh về sau chỉ hiện trạng thái loading *bên trong* vùng
bảng, panel filter không bao giờ unmount. Kèm theo, debounce ô search 300ms —
hiện mỗi phím gõ bắn một request.

**Acceptance criteria.** Gõ một chuỗi không khớp gì rồi xoá dần — input giữ focus
suốt, không mất ký tự. Panel filter không nhấp nháy khi refresh.

#### NIH-446 — Preview file đấu thầu lỗi trên host

**Chưa đủ dữ liệu để chốt nguyên nhân.** File đấu thầu lưu đúng chỗ:
`TendersController.cs:326` ghi vào `wwwroot/files/tenders`, cùng cách với các
module khác đang chạy tốt.

Hai giả thuyết, cần reproduce trên host mới phân biệt được:
1. Thư mục `wwwroot/files` không được persist qua lần deploy — file upload trước
   đó biến mất. `docker-compose.yaml` bind-mount `./nihomebackend:/app` cho dev
   nhưng cấu hình deploy trên host có thể khác.
2. Static file middleware hoặc reverse proxy trên host không phục vụ đường dẫn này.

**Việc cần làm trước tiên:** reproduce trên host, đối chiếu với module Hợp đồng
và Báo giá (đang chạy được) để khoanh vùng. Chỉ chốt cách sửa sau khi có kết quả.

**Ghi chú vệ sinh repo, không liên quan bug:** `.gitignore` đã bỏ qua
`wwwroot/files/` cho `capability/`, `contracts/`, `quotes/`, `business-documents/`
nhưng **thiếu `tenders/`** — nên `nihomebackend/wwwroot/files/tenders/` đang nằm
untracked trong working tree. Nên bổ sung dòng này.

---

### A8 — Primitive UI dùng chung (phần của NIH-439)

**Phạm vi.** Chỉ những thứ nằm ở `components/ui/`, tức primitive dùng chung
xuyên module, **không** đụng layout hay tập trường của form:

- Datepicker: chuẩn hoá hành vi nhập và định dạng ngày
- Money input: định dạng phân tách hàng nghìn, không nhảy con trỏ khi gõ
- Thanh kéo phần trăm (xác suất chốt cơ hội)
- Khắc phục giật khi chuyển trạng thái khách hàng — nhiều khả năng do refetch
  toàn danh sách thay vì cập nhật lạc quan tại chỗ; cần xác nhận khi làm
- Timeline lịch sử chăm sóc: cải thiện trình bày

**Vì sao làm bây giờ mà không đợi chốt workflow.** Đây là primitive, không phải
layout form. Chốt workflow ở gói B/C sẽ đổi *trường nào hiện trong form nào*,
không đổi *cách một ô nhập tiền cư xử*. Nên làm bây giờ không tạo ra việc làm lại,
mà lại là thứ khách cảm nhận ngay trong demo.

**Phần còn lại của NIH-439** — gom nhóm field theo ngữ cảnh, bỏ trường dư —
để lại gói B/C cùng với NIH-440 và NIH-441.

---

### A9 — Tạo hợp đồng từ báo giá, bản tối thiểu

**Vì sao nằm ở gói A.** Mục tiêu ở mục 2 tuyên bố demo đi được tới Hợp đồng.
Nếu bước Báo giá → Hợp đồng vẫn phải nhập tay và không để lại liên kết nào thì
chuỗi đứt đúng chỗ khách đang phàn nàn, và tuyên bố ở mục 2 thành không trung thực.

**Chi phí thấp hơn tưởng: đây là việc thuần frontend.** Backend đã xong hẳn:

- `UpsertContractRequest` nhận sẵn `OpportunityId` và `QuoteId`
  (`ContractRequests.cs:18-19`).
- `ContractService.CreateAsync` lưu cả hai (`ContractService.cs:186-187`).
- Và nó **đã kiểm tra tính nhất quán**: `ContractService.cs:465-479` nạp báo giá,
  suy ra `quote.OpportunityId` cùng `quote.Opportunity.CustomerId`, rồi từ chối
  nếu `OpportunityId` gửi lên mâu thuẫn với cơ hội của báo giá.

Thứ duy nhất thiếu là frontend không bao giờ gửi hai trường đó —
`Contracts.tsx:369-377` dựng payload không có chúng.

**Phạm vi tối thiểu.**

- Nút "Tạo hợp đồng từ báo giá" trên trang chi tiết báo giá, chỉ hiện khi báo giá
  ở trạng thái `Approved` hoặc `CustomerApproved`.
- Nút mở form hợp đồng đã prefill: `quoteId`, `opportunityId`, `customerId` suy
  từ báo giá, và `value` lấy `GrandTotal` của báo giá.
- Form hợp đồng bổ sung hai trường vào payload. Khi tạo trực tiếp không qua báo
  giá thì hai trường để trống — hành vi cũ giữ nguyên.
- Trang chi tiết hợp đồng hiện link ngược về báo giá và cơ hội nguồn.

**Ngoài phạm vi tối thiểu này:** đồng bộ dòng BOQ của báo giá sang điều khoản hợp
đồng, và tự sinh mốc thanh toán từ báo giá — để gói B.

**Acceptance criteria.**

- Từ báo giá Approved bấm tạo hợp đồng → form mở với khách hàng, cơ hội và giá
  trị đã điền sẵn.
- Hợp đồng lưu xong có `quoteId` và `opportunityId` đúng, chi tiết hợp đồng bấm
  ngược về báo giá được.
- Tạo hợp đồng trực tiếp không qua báo giá vẫn chạy như cũ.
- Báo giá chưa duyệt không hiện nút.

---

## 5. Thứ tự thực hiện

Ràng buộc phụ thuộc:

- **A1 phải xong trước A2.** A1 định nghĩa convert tạo ra những gì; A2 là phép
  nghịch đảo và không thể thiết kế trước khi biết điều đó.
- **A5 và A6 dùng chung** bốn trường dự án thiết kế trên `ContractResponse`
  (`Id`, `Code`, `Name`, `CurrentStage`). Làm backend một lần, dùng cho cả hai.
- **A9 không phụ thuộc gì** và thuần frontend, nên giao song song được ngay từ đầu.
- A3, A4, A7, A8 độc lập hoàn toàn, chạy song song được.

Thứ tự đề xuất, cắt được ở bất kỳ điểm nào nếu lịch demo gấp:

1. A1 → A2 (chuỗi Lead, giá trị demo cao nhất)
2. A9 (chuỗi Báo giá → Hợp đồng; thuần FE nên rẻ nhất trong nhóm nối chuỗi)
3. A5 → A6 (chuỗi Hợp đồng → Thiết kế)
4. A3, A4 (guard nghiệp vụ)
5. A7 (lỗi chặn thao tác)
6. A8 (đánh bóng UI)

Nếu buộc phải cắt, **A1, A9 và A6 không được cắt** — ba item này là toàn bộ chuỗi
Lead → Khách hàng/Cơ hội → Báo giá → Hợp đồng → Dự án thiết kế, và là bằng chứng
khả kiến duy nhất trong gói A rằng luồng nghiệp vụ chạy thông. Cắt bất kỳ mắt nào
thì chuỗi đứt và demo lại rơi vào đúng lời chê cũ.

## 6. i18n

Mọi chuỗi hiển thị mới phải qua `t("key")` và có key trong seed backend tương ứng
dưới `nihomebackend/Data/Seeds/i18n/`, đủ **bốn ngôn ngữ** `vi`, `en`, `zh`, `ja`.
Không hardcode chuỗi trong React.

Nhóm key dự kiến: `leads.*` (convert, unconvert, cảnh báo giữ lại bản ghi),
`quotes.*` (lý do từ chối theo stage), `contracts.*` (khối dự án thiết kế),
`designProjects.*` (thông báo trùng hợp đồng), `common.*` (thông báo dùng chung).

Cần restart backend để `TranslationSeeder` upsert key mới vào DB.

## 7. Kiểm thử

Theo phân tầng đã quy ước trong repo:

- **`nihomebackend.tests`** — logic service cô lập: ánh xạ trong A1, hai nhánh
  quyết định của A2, guard stage của A3, ma trận dirty-check của A4.
  **Bắt buộc có một test khoá ràng buộc A1↔A2:** khẳng định `lead.ConvertedAt`,
  `customer.CreatedAt` và `opportunity.CreatedAt` do convert sinh ra là cùng một
  mốc thời gian — đó là dấu hiệu duy nhất A2 dùng để nhận diện bản ghi
  auto-created, và nó không được phép trôi.
  Cũng cần test phủ **cả ba nhánh** của A2, đặc biệt nhánh 2 (khách hàng có sẵn +
  cơ hội auto-created): khẳng định cơ hội bị xoá còn khách hàng còn nguyên.
- **`nihomebackend.integration.tests`** — hành vi HTTP và hợp đồng API: mã trạng
  thái 409 cho trùng khách hàng và trùng hợp đồng, tính nguyên tử của transaction
  trong A1, phân quyền trên endpoint unconvert.
- **Playwright smoke** — chỉ hành vi phụ thuộc trình duyệt và deployment: giữ
  focus của ô search trong A7, điều hướng notification, khối dự án thiết kế của A6,
  và luồng A9 từ chi tiết báo giá sang form hợp đồng đã prefill.

Chạy `dotnet format` trước khi đóng mỗi hạng mục.

## 8. Rủi ro

**Rủi ro chính — demo vẫn bị chê "chưa có quy trình".** Nếu chỉ sửa lỗi thao tác
mà không nối chuỗi, khách sẽ thấy ít lỗi hơn nhưng cảm nhận về quy trình không đổi.
Giảm thiểu bằng cách bảo vệ A1, A9 và A6 khỏi mọi đợt cắt phạm vi.

**Mâu thuẫn phạm vi đã phát hiện và xử lý trong review.** Bản spec đầu để "tạo hợp
đồng từ báo giá" ở gói B trong khi mục tiêu lại tuyên bố demo tới Hợp đồng. Đã kéo
vào gói A thành A9. Chi phí thấp vì backend đã xong sẵn, nên không phải đánh đổi gì
đáng kể — nhưng nếu về sau A9 bị cắt vì lý do lịch, **phải sửa mục tiêu ở mục 2
xuống còn "tới Báo giá được duyệt"** thay vì im lặng để nguyên.

**A2 không có migration nhưng đánh đổi độ chính xác.** Quyết định đưa lead về
`Interested` thay vì khôi phục trạng thái trước đó là có chủ đích, để tránh EF
migration trong gói A. Nếu người dùng phản đối, chi phí là thêm một cột và một
migration ở gói sau.

**NIH-446 chưa chốt được nguyên nhân**, nên không ước lượng được. Nếu nguyên nhân
là persist volume trên host thì cách sửa nằm ở tầng deployment chứ không phải code,
và cần người có quyền trên host phối hợp.

**Đồng thời trên A5.** Kể cả sau khi lọc dropdown, hai người thao tác cùng lúc vẫn
có thể đụng unique index. Vì vậy phần map lỗi 409 sang thông báo thân thiện là bắt
buộc, không phải tuỳ chọn.

**Tình trạng migration của repo.** `CLAUDE.md` cảnh báo container backend đang chạy
không có `dotnet-ef`. Gói A được thiết kế để **không cần migration nào** — đó là
một trong các lý do chọn `Interested` ở A2 và chọn trường chỉ-đọc dẫn xuất ở A5.
Nếu phát sinh nhu cầu đổi schema, phải dựng môi trường tooling trước.
