---
title: "NICON — Tổng hợp cuộc họp demo 03/10/2026 và kế hoạch triển khai tiếp theo"
meeting_date: 2026-10-03
prepared_date: 2026-10-04
source: "Bản chép lời tự động cuộc họp [NICON] Meeting (3 giờ 8 phút)"
participants: [Trần Anh Dũng, Nam, Dao Nguyen, Võ Trí Nguyên, Huỳnh Anh, Nicon_Trinh (chị Yến – phòng Thiết kế)]
status: "Bản nháp — chờ NICON xác nhận các mục ở Phần 6"
---

# NICON — Tổng hợp cuộc họp demo 03/10/2026 và kế hoạch triển khai tiếp theo

> **Lưu ý:** Tài liệu này được tổng hợp từ bản chép lời tự động, vốn có nhiều lỗi nhận dạng giọng nói. Mốc thời gian `[h:mm:ss]` trỏ về đoạn gốc để đối chiếu. Mục nào còn mơ hồ được ghi ở **Phần 6 – Câu hỏi cần NICON xác nhận** chứ không tự suy diễn thành yêu cầu.
>
> **Đầu mối phía NICON:** chị **Huỳnh Anh** — mọi trao đổi, xác nhận và mẫu dữ liệu gửi qua chị Huỳnh Anh `[3:02:56]`.
> **Kênh góp ý:** Google Sheet chung. Đội phát triển gửi kèm file PDF quy trình có ảnh chụp màn hình từng bước `[0:06:37]`.

---

## 1. Tóm tắt

1. **Dự án là trung tâm, hợp đồng gắn sau.** Phòng Kinh doanh tạo dự án. Các phòng khác (Thiết kế, Cung ứng, Tài chính – Kế toán, Pháp lý, Thi công) **không tạo lại dự án** mà **tạo "nhóm công việc" của phòng mình** trên dự án hoặc hợp đồng đã có. Trước khi có hợp đồng thì gắn vào dự án; khi có hợp đồng thì gom về hợp đồng `[0:18:00]–[0:28:38]`, `[1:04:45]–[1:05:50]`.
2. **Thiết kế bắt đầu trước hợp đồng, thậm chí trước báo giá**, vì phải có bản vẽ thì mới báo giá được. Cần thêm nút chuyển sang Thiết kế ngay trên **Cơ hội** `[0:29:18]–[0:33:35]`.
3. **Báo giá đầu vào (Cung ứng) chạy trước báo giá đầu ra (Kinh doanh).** Tài chính – Kế toán tham gia từ giai đoạn báo giá và đấu thầu để rà soát điều kiện hợp đồng và bảo lãnh `[1:01:58]–[1:16:21]`.
4. **Đấu thầu cần bổ sung quy trình đầy đủ**, có bước *lập kế hoạch đấu thầu* để phân công hồ sơ cho từng phòng ban `[1:22:21]–[1:25:46]`.
5. **Phân quyền nhóm theo phòng ban.** Khi tạo vai trò mới thì áp sẵn bộ quyền mặc định của phòng ban, sau đó thêm hoặc bớt quyền `[0:10:16]–[0:13:47]`.
6. **Phòng Thiết kế:** đổi tên vai trò, tự động chuyển duyệt, thêm luồng **trình nội bộ** (chỉ xem và bình luận), upload file ở mọi giai đoạn, tiến độ theo mẫu MS Project `[1:29:11]–[2:27:56]`.
7. **Thi công tách 2 nhóm:** *Ban chỉ huy công trường* (tại hiện trường, mỗi ban một dự án) và *Phòng CM* (văn phòng, nhiều dự án, phê duyệt và giám sát). Bổ sung đề xuất nguồn lực, QA/QC và QS `[2:33:48]–[2:53:11]`.
8. **Thứ tự ưu tiên đã thống nhất:** làm cho chuẩn **CRM + Thiết kế** trước, các module sau đi theo cùng cấu trúc `[3:01:04]–[3:02:30]`.

---

## 2. Các quyết định đã thống nhất trong cuộc họp

| Mã | Quyết định | Nguồn |
|---|---|---|
| QĐ-01 | Danh mục quyền do hệ thống quản lý (dev bổ sung), NICON không tự tạo quyền mới. NICON tạo vai trò và gán quyền. | `[0:09:01]–[0:10:16]` |
| QĐ-02 | Nhóm quyền theo phòng ban. Vai trò mới thuộc phòng nào thì áp sẵn bộ quyền của phòng đó và cho phép thêm hoặc bớt. | `[0:10:16]–[0:12:53]` |
| QĐ-03 | Menu CRM: đưa **Dự án, Cơ hội, Hợp đồng** lên đầu, vì đây là các đối tượng dùng chung cho mọi module. | `[0:17:12]` |
| QĐ-04 | Giữ **Lead** và **Cơ hội** là 2 chức năng riêng (không gộp như đề xuất của chị Huỳnh Anh), vì mỗi bên có lịch sử theo dõi riêng. Lead bị khóa sau khi chuyển đổi. | `[0:34:27]–[0:35:47]` |
| QĐ-05 | Có 2 nhóm công việc: **theo dự án/hợp đồng** và **nội bộ, không theo dự án/hợp đồng** (ví dụ "Đào tạo 2026"). Nhóm nội bộ nằm ở tab riêng, không nằm trong Dự án. | `[0:18:00]`, `[0:22:05]–[0:26:19]` |
| QĐ-06 | Phòng Thiết kế không được tạo dự án. Thiết kế khởi tạo nhóm công việc trên dự án/hợp đồng có sẵn và tự lấy thông tin. Bỏ điều kiện bắt buộc có hợp đồng. | `[0:20:18]`, `[0:33:10]–[0:33:35]`, `[0:44:16]` |
| QĐ-07 | Kích hoạt công việc Thiết kế bằng **nút trên Cơ hội** hoặc **giao việc cho phòng Thiết kế trong Dự án**. NICON chấp nhận cả hai cách. | `[0:30:14]–[0:30:52]`, `[0:49:39]–[0:49:54]` |
| QĐ-08 | Vẫn bắt buộc **có báo giá trước khi tạo hợp đồng**. | `[0:51:24]` |
| QĐ-09 | Báo giá BOQ/suất đầu tư nhập qua **mẫu Excel chuẩn**. NICON gửi các mẫu BOQ thực tế theo loại công trình để đội dev thiết kế mẫu chung. | `[0:56:40]–[1:01:58]` |
| QĐ-10 | Báo giá đầu vào của Cung ứng chạy trước. Không cần tab riêng, dùng cơ chế "khởi tạo nhóm công việc dự án" giống Thiết kế. | `[1:07:27]`, `[1:09:21]` |
| QĐ-11 | Chức năng "Mua sắm" (kiểm soát BOQ đã ký) thuộc giai đoạn **sau ký hợp đồng**. Đổi tên module **"Mua sắm" thành "Cung ứng"** cho khớp tên phòng ban. | `[1:08:54]`, `[1:27:40]` |
| QĐ-12 | Đấu thầu phải có bước **lập kế hoạch đấu thầu** (phân công hồ sơ và tiến độ theo phòng ban). Dự án và khách hàng có trước gói thầu. Trúng thầu thì chuyển sang thương thảo và tạo hợp đồng. | `[1:21:48]`, `[1:22:21]`, `[1:24:42]` |
| QĐ-13 | Mã/ký hiệu dự án phải **chỉnh sửa được** theo quy ước của NICON. Đội dev đồng ý. | `[1:32:11]–[1:33:13]` |
| QĐ-14 | Bấm **"Hoàn thành"** thì tự động chuyển duyệt cho đúng người được phân vai, không gửi thủ công. | `[1:34:18]–[1:35:23]` |
| QĐ-15 | Mỗi đầu việc chỉ có **một người duyệt**. Những người cần nắm thông tin thì dùng **CC**. | `[1:45:37]–[1:53:28]` |
| QĐ-16 | Đổi tên vai trò Thiết kế: **PM (phòng Thiết kế) thành "Chủ nhiệm thiết kế / Design Manager"**, vẫn giữ **Design Lead** (tương đương nhóm trưởng). Chủ nhiệm thiết kế có quyền cao hơn Design Lead. | `[1:48:10]`, `[1:57:00]–[1:59:39]` |
| QĐ-17 | **Trình nội bộ:** Chủ nhiệm thiết kế trình bộ phương án cho người được chỉ định (Kinh doanh, CM, Ban giám đốc). Người nhận **chỉ xem và bình luận**. Sau khi thống nhất thì trình khách. Chỉ Chủ nhiệm thiết kế ghi nhận quyết định của khách. | `[2:00:51]–[2:06:44]` |
| QĐ-18 | Công việc pháp lý thuộc **module Pháp lý** (kể cả khi nhân sự Thiết kế làm), do PM toàn dự án phân công từ đầu. | `[2:07:33]–[2:08:28]` |
| QĐ-19 | Thầu phụ thiết kế **không làm module riêng**. Thể hiện thành đầu việc trong tiến độ, kết quả upload vào hồ sơ của giai đoạn tương ứng. Chủ nhiệm thiết kế được cấp quyền dùng chức năng Nhà cung cấp/Thầu phụ (tìm NCC, so sánh giá, theo dõi hợp đồng đầu vào). | `[2:16:00]`, `[2:19:37]` |
| QĐ-20 | **Giữ tab Tài liệu** của dự án thiết kế, là nơi tổng hợp tài liệu mọi giai đoạn theo dự án. | `[2:24:10]–[2:25:28]` |
| QĐ-21 | Phát hành IFC thì chuyển thẳng sang Thi công. | `[2:22:10]–[2:22:33]` |
| QĐ-22 | Menu Thi công tách thành **Ban chỉ huy công trường** và **Phòng CM**. | `[2:51:11]`, `[2:53:11]` |
| QĐ-23 | Ưu tiên hoàn thiện **CRM + Thiết kế** trước. | `[3:01:04]–[3:02:30]` |

---

## 3. Mô hình nghiệp vụ mục tiêu (sau cuộc họp)

### 3.1 Cấp bậc đối tượng

```text
KHÁCH HÀNG
 └── DỰ ÁN (do phòng Kinh doanh tạo, hoặc tự sinh khi chuyển Lead)
      ├── Nhóm công việc theo phòng ban (TRƯỚC hợp đồng)
      │     ├── Thiết kế (concept phục vụ báo giá)
      │     ├── Cung ứng (báo giá đầu vào)
      │     └── Tài chính – Kế toán (rà soát ĐK hợp đồng, bảo lãnh, hồ sơ thầu)
      ├── Gói thầu (kịch bản C) → Kế hoạch đấu thầu → Hồ sơ dự thầu
      ├── Báo giá đầu ra (bắt buộc trước hợp đồng)
      └── HỢP ĐỒNG (1 dự án – n hợp đồng)
            └── Nhóm công việc theo phòng ban (SAU hợp đồng, gom cùng nhóm trước HĐ)
                  Thiết kế | Pháp lý | Cung ứng | Thi công (BCH + CM) | TC–KT

CÔNG VIỆC NỘI BỘ (không dự án, không hợp đồng) — tab riêng, liên kết KPI
```

### 3.2 Luồng kịch bản lớn nhất (A – D&B trọn gói), đã điều chỉnh

```text
Lead ──chuyển đổi──► Khách hàng + Cơ hội + Dự án (Planning)
                         │
     ┌───────────────────┼──────────────────────────┐
     ▼                   ▼                          ▼
[Thiết kế concept]  [Cung ứng: giá đầu vào]   [TC–KT: rà soát ĐK HĐ]
     └───────────────────┼──────────────────────────┘
                         ▼
               Báo giá đầu ra (suất đầu tư / BOQ)
                         ▼
               Thương thảo → Hợp đồng (bắt buộc có báo giá) → Cơ hội "Thắng"
                         ▼
   Thiết kế cơ sở → chi tiết → IFC ──► Thi công (BCH công trường + Phòng CM)
   Pháp lý (giao việc theo từng giấy phép)            │
   Cung ứng (BOQ đã ký, MR, RFQ, kho) ◄── đề xuất nguồn lực
                         ▼
   TC–KT chạy SONG SONG từ khi ký HĐ: thanh toán theo tiến độ (đầu ra – CĐT,
   đầu vào – thầu phụ/NCC), quản lý chi phí đầu ra và đầu vào
                         ▼
               Nghiệm thu → Hồ sơ hoàn công → Bàn giao
```

---

## 4. Yêu cầu chi tiết theo module: hiện trạng và phương án đề xuất

Quy ước: **Hiện trạng** là kết quả đối chiếu mã nguồn (nhánh hiện tại) ngày 04/10/2026. **Mức độ:** S = nhỏ (≤ 1 ngày), M = vừa (2–4 ngày), L = lớn (≥ 1 tuần), cần ước lượng lại khi có mẫu dữ liệu.

### 4.1 Người dùng, vai trò và phân quyền

| # | Yêu cầu | Hiện trạng | Phương án đề xuất | Mức |
|---|---|---|---|---|
| U-1 | Nhóm quyền theo phòng ban để dễ tìm và gán `[0:10:16]` | Màn hình Vai trò có lọc theo *module kỹ thuật* (tiền tố mã quyền), chưa có nhóm theo phòng ban | Thêm khái niệm **Phòng ban** (danh mục do quản trị cấu hình) và ánh xạ *phòng ban → nhóm quyền*. Ma trận quyền hiển thị theo nhóm phòng ban, có nút chọn hoặc bỏ cả nhóm | M |
| U-2 | Vai trò mới thuộc phòng ban thì áp sẵn bộ quyền mặc định, sau đó thêm hoặc bớt `[0:12:31]` | Tạo vai trò mới với ma trận trống | Khi tạo vai trò: chọn **Phòng ban**, hệ thống điền sẵn bộ quyền mẫu (template) của phòng. Người dùng chỉnh tay trước khi lưu. Bộ quyền mẫu cũng sửa được trên web | M |
| U-3 | Đổi tên hiển thị quyền và vai trò cho dễ hiểu `[0:11:44]` | Tên quyền là khóa i18n trong seed | Tên hiển thị quản lý qua `/admin/translations` (4 ngôn ngữ). Mã quyền giữ nguyên để không ảnh hưởng chính sách phân quyền | S |
| U-4 | Quyền **phê duyệt chuyển bước / phê duyệt thiết kế**, gán theo dự án khi lập team `[2:03:03]` | Team dự án có các vai trò PM, Design Lead, Architect…; chưa có quyền duyệt chuyển bước tách riêng | Thêm cờ **"Người duyệt chuyển bước"** trên thành viên team (theo module). PM toàn dự án gán khi lập team | M |
| U-5 | Quyền xóa hồ sơ trình không đạt chỉ cấp cho **một** người có quyền cao nhất `[2:21:28]–[2:21:55]` | Xóa theo quyền module | Quyền xóa riêng cho hồ sơ phương án/phiên bản bị loại, mặc định chỉ cấp cho Chủ nhiệm thiết kế (chờ xác nhận ở Q-08). Tuân thủ chính sách hard-delete hiện hành | S–M |

**Đầu vào cần NICON:** danh sách phòng ban và vai trò thuộc từng phòng, dựa trên sơ đồ tổ chức đã gửi (xem D-01).

### 4.2 CRM: Lead, Khách hàng, Cơ hội, Dự án

| # | Yêu cầu | Hiện trạng | Phương án đề xuất | Mức |
|---|---|---|---|---|
| C-1 | Sắp lại menu: **Dự án, Cơ hội, Hợp đồng** lên đầu `[0:17:12]` | Thứ tự hiện tại: Dự án vận hành, Báo cáo, Lead, Khách hàng, Cơ hội, Báo giá, Danh mục giá, Hợp đồng, Đấu thầu… | Thứ tự mới: Dự án, Cơ hội, Hợp đồng, Lead, Khách hàng, Báo giá, Danh mục đơn giá, Đấu thầu, Hồ sơ năng lực, Khảo sát, Báo cáo | S |
| C-2 | Khách hàng cá nhân không bắt buộc MST khi chuyển đổi `[0:14:00]` | Đã có | Giữ nguyên | — |
| C-3 | Chuyển Lead tạo Khách hàng + Cơ hội (+ Dự án), có thông báo bản ghi vừa tạo và nhảy thẳng tới bản ghi `[0:14:00]` | Đã có (Lead chuyển đổi mở Dự án `Planning`, có toast "bản ghi vừa tạo") | Giữ nguyên | — |
| C-4 | Nút **"Chuyển thiết kế / Tạo công việc thiết kế"** trên Cơ hội, cạnh "Tạo báo giá" và "Tạo hợp đồng" `[0:30:14]`, `[0:48:54]` | Cơ hội đã có tạo báo giá và hợp đồng. Thiết kế tạo được không cần hợp đồng (commit `f130cdb0`, **đã có ở local, chưa deploy lên host**) | Thêm nút trên Cơ hội: tạo nhóm công việc Thiết kế cho dự án của cơ hội, gửi thông báo cho trưởng phòng Thiết kế. Đây chỉ là **lối tắt** của cơ chế giao việc ở mục 4.3, không phải luồng riêng | S–M |
| C-5 | Gộp Lead và Cơ hội để bớt trường (đề xuất của chị Huỳnh Anh) `[0:33:52]` | Hai chức năng riêng | **Không gộp** (QĐ-04). Đề xuất rà soát và rút gọn các trường bắt buộc trên form Lead và Cơ hội sau khi NICON đánh dấu trường thừa trên Sheet | S |
| C-6 | Mã dự án chỉnh sửa được theo quy ước NICON `[1:32:11]` | Dự án thiết kế tự sinh mã `DP-{năm}-{số}`, không sửa được | Tách **ID nội bộ** (không đổi, dùng cho liên kết) và **Mã dự án** hiển thị (sửa được, kiểm tra trùng phía server, có định dạng hợp lệ). Mã đề xuất tự sinh nhưng cho sửa. Chờ quy ước mã ở Q-05 | M |

### 4.3 Mô hình "Nhóm công việc theo phòng ban" (dùng chung mọi module)

**Vấn đề hiện tại** `[0:18:00]`, `[0:42:31]`: mỗi phòng phải tạo lại dự án trong module của mình, nên dữ liệu bị trùng và thao tác 2 lần. Phân công công việc trong Dự án không tự hiện sang giao diện của phòng được giao.

**Hiện trạng code:** đã có Dự án vận hành, team dự án theo vai trò và module, Dự án thiết kế, tiến độ thiết kế chi tiết, tiến độ thi công (Gantt), checklist pháp lý, thông báo. **Chưa có** thực thể công việc chung theo phòng ban, chưa có công việc nội bộ không gắn dự án.

**Phương án A (khuyến nghị): Gói công việc phòng ban dùng chung, triển khai dần**

- Thực thể **Gói công việc** (`WorkPackage`) gồm: Dự án (bắt buộc với công việc dự án), Hợp đồng (tùy chọn, tự gắn khi hợp đồng của dự án được tạo), Phòng ban, Người chủ trì (trưởng phòng hoặc người được giao), trạng thái.
- **Đầu việc** (`WorkTask`) trong gói, theo trường của mẫu MS Project (chờ D-03): mã WBS, tên, mô tả, ngày bắt đầu và kết thúc, người phụ trách, công tác trước và sau, nguồn lực, % hoàn thành, người duyệt, CC.
- **Quy tắc:**
  - Chỉ Kinh doanh/PM dự án **giao việc cho phòng ban**.
  - Chỉ người chủ trì của phòng đó **chia việc cho thành viên phòng mình** `[1:56:51]`.
  - Người nhận được thông báo và bấm vào thông báo thì vào thẳng đầu việc `[0:41:56]`.
- **Lối tắt** tạo gói: nút trên Cơ hội (Thiết kế), trên Báo giá ("Yêu cầu giá đầu vào" gửi Cung ứng, "Yêu cầu rà soát tài chính" gửi TC–KT), trên Gói thầu (kế hoạch đấu thầu).
- **Không viết lại** các module đã chạy:
  - *Dự án thiết kế* đóng vai trò gói công việc của phòng Thiết kế (liên kết 1-1).
  - *Tiến độ thi công* là gói của Thi công.
  - *Checklist pháp lý* là gói của Pháp lý.
  - Thực thể chung chỉ dùng mới cho các phòng chưa có "vỏ" công việc: Cung ứng trước HĐ, TC–KT, Pháp lý (giao việc theo giấy phép) và Công việc nội bộ.
- **Công việc nội bộ:** tab riêng (không trong Dự án), có thể liên phòng ban (ví dụ chương trình đào tạo toàn công ty). Là nguồn dữ liệu cho KPI (Module 8) `[0:24:49]`.

**Phương án B: chỉ thêm "yêu cầu phối hợp" vào từng module**
Ít thay đổi dữ liệu hơn, nhưng mỗi module có logic giao việc riêng, không giải quyết được công việc nội bộ, khó tổng hợp KPI. **Không khuyến nghị.**

> Đây là thay đổi kiến trúc lớn nhất của đợt này. Cần NICON xác nhận Q-01 đến Q-03 trước khi chốt thiết kế dữ liệu.

### 4.4 Báo giá, danh mục đơn giá và BOQ

| # | Yêu cầu | Hiện trạng | Phương án đề xuất | Mức |
|---|---|---|---|---|
| Q-1 | Upload BOQ có sẵn (nhiều sheet, nhiều hạng mục) `[0:56:11]` | Danh mục đơn giá có tải mẫu Excel, import, thêm/sửa hạng mục, nhiều phiên bản và chỉ một phiên bản được duyệt | Thiết kế **mẫu BOQ chuẩn nhiều sheet**: mỗi sheet một hạng mục (nhà xưởng chính, kho, văn phòng, ký túc xá, hạ tầng…), cấu trúc cột cố định, số sheet tùy biến. Import kiểm tra từng sheet và báo lỗi theo vị trí ô. **Phụ thuộc D-02** | L |
| Q-2 | Báo giá đầu ra lấy giá đầu vào từ Cung ứng `[1:06:51]–[1:11:22]` | Báo giá lấy đơn giá từ danh mục. Chưa có liên kết với giá đầu vào của Cung ứng | Gói công việc Cung ứng (trước HĐ) cho phép ghi nhận giá đầu vào (tham khảo cơ chế RFQ và so sánh giá đã có). Khi tạo báo giá đầu ra thì xem hoặc chọn giá đầu vào làm tham chiếu. Giá đầu vào chỉ là **một phần** dữ liệu đầu vào, không bắt buộc đủ 100% | M–L |
| Q-3 | Bắt buộc báo giá trước hợp đồng `[0:51:24]` | Đã có (gắn báo giá đã duyệt vào hợp đồng) | Giữ nguyên | — |

### 4.5 Đấu thầu (Kịch bản C)

**Quy trình NICON mô tả** `[1:18:42]`, `[1:22:21]`, `[1:25:31]`:
nhận thông tin, lập kế hoạch đấu thầu, triển khai lập hồ sơ thầu (nhiều phòng), đánh giá hồ sơ thầu nội bộ, nộp thầu, kết quả. Nếu trúng thì thương thảo và lập hợp đồng.

| # | Yêu cầu | Hiện trạng | Phương án đề xuất | Mức |
|---|---|---|---|---|
| T-1 | Gói thầu gắn với **Dự án** (dự án có trước) và thường tương ứng 1 hợp đồng `[1:18:42]`, `[1:21:48]` | Gói thầu gắn Khách hàng. Trúng thầu thì tạo Cơ hội, từ Cơ hội tạo Hợp đồng | Thêm liên kết Gói thầu và Dự án (chọn dự án có sẵn của khách hàng). Trúng thầu thì Cơ hội/Hợp đồng kế thừa dự án | M |
| T-2 | **Kế hoạch đấu thầu**: phân hồ sơ cho từng phòng (TK, TC, Cung ứng, TC–KT), có tiến độ (~1 tháng) `[1:22:46]` | Checklist hồ sơ có người phụ trách và hạn nội bộ, nhưng chưa có khái niệm phòng ban và chưa có tiến độ tổng | Nâng checklist thành **Kế hoạch đấu thầu**: mỗi mục gắn phòng ban, người phụ trách, ngày bắt đầu và kết thúc, trạng thái. Có giao việc và thông báo cho trưởng phòng. Có màn hình tiến độ tổng | M–L |
| T-3 | Phân biệt **hồ sơ cố định** (hồ sơ năng lực, dùng lại cho nhiều gói) và **hồ sơ thay đổi theo gói** (BOQ, biện pháp thi công, bản vẽ dự thầu) `[1:22:21]` | Đã có thư viện Hồ sơ năng lực và liên kết vào checklist | Giữ thư viện. Thêm loại mục "hồ sơ theo gói" để upload riêng | S |
| T-4 | TC–KT rà soát hồ sơ hợp đồng, bảo lãnh ngân hàng **trước khi nộp thầu** `[1:15:00]` | Chưa có | Bước **Đánh giá hồ sơ nội bộ** trước "Nộp thầu": các mục của TC–KT phải hoàn thành hoặc được duyệt thì mới chuyển trạng thái được (server kiểm tra) | M |
| T-5 | Trạng thái quy trình đủ bước | `Preparing, Submitted, Won, Lost, Cancelled` | Bổ sung bước Lập kế hoạch và Đánh giá nội bộ. Ánh xạ trạng thái cũ, có migration | M |

### 4.6 Thiết kế (Module 2)

| # | Yêu cầu | Hiện trạng | Phương án đề xuất | Mức |
|---|---|---|---|---|
| D-1 | Đổi tên: **PM thiết kế thành "Chủ nhiệm thiết kế" (Design Manager)**, giữ **Design Lead** (nhóm trưởng) `[1:59:39]` | Vai trò team: `ProjectManager`, `DesignLead`… | Thêm vai trò team **Chủ nhiệm thiết kế** trong module Thiết kế, tách khỏi PM toàn dự án. Cập nhật nhãn ở cả 4 ngôn ngữ | S–M |
| D-2 | Phân cấp duyệt: thành viên → Design Lead → Chủ nhiệm thiết kế. Mỗi đầu việc **một người duyệt**, người khác chỉ **CC** `[1:45:37]`, `[1:53:34]` | Duyệt theo quyền | Mỗi đầu việc/phương án có **đúng một người duyệt** (server ép buộc) và danh sách CC nhận thông báo, chỉ xem. Trưởng/phó phòng có thể là CC mặc định (Q-07) | M |
| D-3 | Bấm **"Hoàn thành"** thì tự gửi duyệt cho người được phân vai `[1:34:47]` | Gửi duyệt nửa thủ công | Gộp "Hoàn thành" và "Gửi duyệt" thành một thao tác. Người duyệt lấy từ team và cấu hình của đầu việc. Có thông báo | S–M |
| D-4 | **Upload file ở mọi giai đoạn** (Concept, Cơ sở, Chi tiết, IFC). Tự tạo cây thư mục Drive cho nhóm công việc Thiết kế `[1:29:11]` | Đã có upload theo giai đoạn và đồng bộ thư mục Drive theo dự án | Rà soát để mọi phương án và giai đoạn đều upload được nhiều file. Bảo đảm thư mục `02_Thiet_ke/...` được tạo khi khởi tạo nhóm công việc Thiết kế (không chờ hợp đồng) | S–M |
| D-5 | **Trình nội bộ**: chọn người nhận (Kinh doanh, CM, BGĐ…). Người nhận chỉ xem và bình luận. Sau đó trình khách. Chỉ Chủ nhiệm thiết kế ghi nhận kết quả khách `[2:00:51]–[2:06:44]` | Trạng thái phương án đã có `PendingInternalReview`, `PresentedToClient`, `ClientRequestedChanges`, `Finalized` | Thêm **Phiên trình nội bộ**: danh sách người được chỉ định, quyền xem/bình luận **chỉ áp dụng cho phiên đó** (có quyền xem nhưng không được chỉ định thì không xem được; dự án khác cũng không xem được). Có luồng bình luận và thông báo. Không trao đổi qua email bên ngoài `[2:02:04]` | M–L |
| D-6 | **Tiến độ thiết kế** đủ thông tin theo mẫu MS Project. Gộp hoặc liên kết tab Đội ngũ và Tiến độ `[1:35:23]–[1:39:24]`, `[2:25:46]` | Tab Đội ngũ (vai trò) và tab Tiến độ (giai đoạn, đầu việc) tách rời, thiếu công tác trước/sau, nguồn lực | Chờ mẫu D-03, sau đó thiết kế lại. Mặc định gộp thành một màn hình (danh sách WBS + Gantt + người phụ trách). Nếu quá rối thì tách 2 tab có liên kết `[2:27:30]`. Hỗ trợ 2 nguồn mốc: mốc do Kinh doanh/Dự án ban hành và mốc Thiết kế tự đề xuất `[1:39:24]` | L |
| D-7 | Người chủ trì (Design Lead) tự tạo đầu việc con dưới mốc lớn `[1:43:17]` | Có đầu việc trong tiến độ | Cho Design Lead tạo và chia đầu việc con trong phạm vi dự án của mình. Thành viên báo cáo cho Design Lead, CC cho người khác | S–M |
| D-8 | Thầu phụ thiết kế: thể hiện trong tiến độ, kết quả vào hồ sơ giai đoạn. Chủ nhiệm thiết kế dùng được chức năng NCC/thầu phụ `[2:16:00]`, `[2:19:37]` | Đã có NCC, RFQ, so sánh giá, hợp đồng đầu vào | Thêm loại đầu việc "Thầu phụ" (liên kết NCC và hợp đồng đầu vào). Cấp quyền mặc định cho Chủ nhiệm thiết kế. Thêm mục lối tắt trong Thiết kế dẫn tới danh sách thầu phụ/HĐ đầu vào đã lọc theo dự án (đáp ứng yêu cầu "mục nhỏ" của chị Yến `[2:10:54]`) | M |
| D-9 | Tab Tài liệu tổng hợp theo dự án `[2:24:53]` | Đã có | Giữ nguyên. Bổ sung lọc theo giai đoạn/phương án nếu cần | S |
| D-10 | IFC chuyển thẳng sang Thi công `[2:22:10]` | Đã có phát hành IFC | Kiểm tra lại hiển thị ở phía Thi công (chỉ bản IFC) | S |

### 4.7 Pháp lý (Module 3)

| # | Yêu cầu | Hiện trạng | Phương án đề xuất | Mức |
|---|---|---|---|---|
| L-1 | Danh mục giấy phép mẫu do NICON cung cấp `[2:30:18]` | Checklist giấy phép tự sinh theo mẫu cấu hình sẵn, cập nhật trạng thái và upload giấy phép | Cập nhật mẫu theo danh sách NICON (D-04). Có thể nhiều mẫu theo loại hoặc quy mô công trình (Q-09) | S–M |
| L-2 | Quản lý như một dự án: người chủ trì Pháp lý **giao việc theo từng giấy phép** (người phụ trách, ngày bắt đầu và kết thúc), có bảng tiến độ `[2:30:50]–[2:32:13]` | Chưa có giao việc và tiến độ cho từng giấy phép | Mỗi giấy phép là một đầu việc trong gói công việc Pháp lý (mục 4.3): người phụ trách, kế hoạch, thực tế, cảnh báo quá hạn. Có màn hình tiến độ | M |
| L-3 | Liên kết từ Hợp đồng/Dự án sang Pháp lý ngay khi tạo `[2:30:50]` | Checklist gắn Dự án thiết kế | Gắn gói Pháp lý vào **Dự án** để không phụ thuộc việc có Dự án thiết kế. Cần migration dữ liệu hiện có | M |

### 4.8 Cung ứng (Module 5, đổi tên từ "Mua sắm")

| # | Yêu cầu | Hiện trạng | Phương án đề xuất | Mức |
|---|---|---|---|---|
| P-1 | Đổi tên module "Mua sắm" thành **"Cung ứng"** `[1:28:21]` | Nhãn "Mua sắm/Kiểm soát mua sắm" | Đổi nhãn ở 4 ngôn ngữ qua translations, giữ nguyên route và mã quyền | S |
| P-2 | Giá đầu vào trước HĐ (xem Q-2) | — | Gói công việc Cung ứng trước HĐ | M–L |
| P-3 | Sau HĐ: BOQ đã ký, MR, RFQ, kho, chi phí `[1:27:05]` | Đã có BOQ dự án, MR, RFQ, so sánh giá, kho, cảnh báo vật tư | Giữ nguyên. Nhận thêm đề xuất máy móc và nhân công từ Ban chỉ huy (T-C2) | — |
| P-4 | Thầu phụ dùng chung cho Thiết kế, Cung ứng, Thi công `[2:11:35]–[2:12:25]` | NCC/RFQ thuộc Cung ứng | Luồng NCC/thầu phụ dùng chung, gắn **phòng ban sử dụng** để mỗi phòng xem phần của mình | M |

### 4.9 Thi công (Module 4)

**Tổ chức** `[2:34:22]`:
- **Phòng CM** (văn phòng): quản lý nhiều dự án từ xa, chủ yếu kiểm tra, phê duyệt và so sánh với hợp đồng.
- **Ban chỉ huy công trường** (BCH, tại hiện trường): mỗi ban một dự án, triển khai chi tiết.

| # | Yêu cầu | Hiện trạng | Phương án đề xuất | Mức |
|---|---|---|---|---|
| CN-1 | Tách menu thành 2 nhóm: **Ban chỉ huy công trường** và **Phòng CM** `[2:51:11]` | Một nhóm menu: Tiến độ, Nhật ký, Punchlist, HSE, Nghiệm thu, Hoàn công, Danh mục hoàn công, Bàn giao | Sắp xếp lại theo danh sách công việc của CM do anh Nguyên gửi (D-05). Tạm thời: BCH gồm Nhật ký, Punchlist, HSE, đề xuất nguồn lực, QA/QC, QS. CM gồm Tiến độ tổng, Phê duyệt, Nghiệm thu, Hoàn công, Bàn giao | S–M |
| CN-2 | **Đề xuất nguồn lực** (vật tư, máy móc thiết bị, nhân công) do BCH lập, tự chuyển CM/PM dự án duyệt, rồi Cung ứng, rồi TC–KT, rồi giao hàng về công trường `[2:38:14]` | Đã có Yêu cầu vật tư (MR) đối chiếu BOQ. Chưa có máy móc và nhân công | Mở rộng thành **Phiếu đề xuất nguồn lực** có loại (Vật tư/Máy móc/Nhân công). Luồng duyệt tự động theo vai trò. Vật tư tái sử dụng MR hiện có | M–L |
| CN-3 | **QA/QC – hồ sơ chất lượng**: biên bản nghiệm thu, lấy mẫu, kết quả thí nghiệm, CO/CQ, chứng chỉ vật liệu, nhật ký. Tự lưu Drive `[2:39:09]` | Có nghiệm thu, nhật ký, hoàn công. Chưa có danh mục hồ sơ chất lượng theo ISO | Dựng **danh mục hồ sơ chất lượng chuẩn** (template từ bộ ISO đã gửi), upload vào `04_Thi_cong_Nghiem_thu`. Tận dụng cơ chế danh mục của Hoàn công | M |
| CN-4 | **QS – khối lượng và nghiệm thu thanh toán**: đầu ra (với CĐT) và đầu vào (với thầu phụ/NCC) `[2:39:09]` | Có mốc thanh toán hợp đồng và thanh toán ở TC–KT | Thêm **Đợt nghiệm thu khối lượng** gắn hợp đồng (đầu ra hoặc đầu vào), là căn cứ cho hồ sơ thanh toán (mục 4.10) | L |
| CN-5 | Danh sách công việc BCH là **mẫu chuẩn** cho mọi dự án (theo ISO) `[2:42:37]` | — | Cấu hình template, tự sinh khi khởi tạo gói Thi công | M |

### 4.10 Tài chính – Kế toán (Module 6)

| # | Yêu cầu | Hiện trạng | Phương án đề xuất | Mức |
|---|---|---|---|---|
| F-1 | TC–KT tham gia **trước HĐ**: rà soát điều kiện thanh toán, bảo lãnh, hồ sơ hợp đồng (báo giá và đấu thầu) `[1:13:55]–[1:16:17]` | Chưa có | Gói công việc TC–KT trước HĐ (mục 4.3) và bước rà soát trong đấu thầu (T-4) | M |
| F-2 | Chạy **song song từ khi ký HĐ**. Thanh toán theo tiến độ: hồ sơ công trường + hồ sơ CM + hồ sơ kế toán thành **bộ hồ sơ thanh toán** gửi CĐT `[2:55:54]` | Có HĐ chính và HĐ đầu vào, mốc thanh toán, kiểm soát tài chính (thanh toán, kỳ, điều chỉnh) | **Bộ hồ sơ thanh toán** theo đợt: checklist 3 phần (BCH, CM, TC–KT), mỗi phần một người duyệt. Đủ thì trạng thái "Sẵn sàng gửi CĐT" | L |
| F-3 | Đầu vào: thầu phụ/NCC nộp hồ sơ thanh toán, BCH + CM kiểm tra, rồi TC–KT chi `[2:58:34]` | Có HĐ đầu vào | Dùng cùng mô hình F-2 cho chiều đầu vào | M |
| F-4 | Quản lý chi phí đầu ra và đầu vào của dự án `[2:58:34]` | Có báo cáo vận hành dự án | Tổng hợp từ F-2, F-3 lên báo cáo dự án. P&L chi tiết làm ở đợt sau | M |

### 4.11 Google Drive (Module 7)

- Hiện đang dùng **tài khoản test** của đội dev. Upload, sửa và xóa đồng bộ đã chạy cả local và host `[1:30:08]`.
- Trước khi vận hành chính thức thì cấu hình sang tài khoản/Workspace của NICON. Tài khoản anh Nguyên đang bị chặn đăng nhập do xác thực vị trí, **chưa cần xử lý ở giai đoạn test** `[1:31:36]`.
- Bổ sung: thư mục Drive tạo ngay khi khởi tạo nhóm công việc Thiết kế (D-4), và cho hồ sơ chất lượng (CN-3).

---

## 5. Dữ liệu và mẫu cần NICON cung cấp

| Mã | Nội dung | Người phụ trách (NICON) | Dùng cho | Chặn hạng mục |
|---|---|---|---|---|
| D-01 | Danh sách phòng ban, vai trò thuộc từng phòng, bộ quyền mặc định mong muốn (dựa trên sơ đồ tổ chức) | Anh Nguyên / chị Huỳnh Anh | U-1, U-2 | Có |
| D-02 | Bộ BOQ mẫu theo loại công trình (nhà xưởng 1–5 ha, nhà ở, căn hộ dịch vụ, văn phòng…), file Excel nhiều sheet | Anh Nguyên | Q-1 | Có |
| D-03 | Mẫu tiến độ thiết kế (MS Project) | Chị Yến → chị Huỳnh Anh | D-6, mục 4.3 (trường đầu việc) | Có |
| D-04 | Danh sách giấy phép của một dự án (theo loại hoặc quy mô nếu khác nhau) | NICON | L-1 | Có |
| D-05 | Tóm tắt nhóm công việc của Phòng CM | Anh Nguyên | CN-1 | Một phần |
| D-06 | Quy ước đặt mã dự án | NICON | C-6 | Có |
| D-07 | Danh sách trường thừa trên form Lead và Cơ hội (đánh dấu trên Sheet) | Chị Huỳnh Anh | C-5 | Không |
| — | Bộ hồ sơ ISO (BCH công trường, thanh toán) **đã gửi**. Đội dev chủ động đọc và đề xuất cấu trúc gọn | Đội dev | CN-3, CN-5, F-2 | — |

---

## 6. Câu hỏi cần NICON xác nhận

| Mã | Câu hỏi | Vì sao cần |
|---|---|---|
| Q-01 | Khi hợp đồng được tạo, nhóm công việc trước HĐ (gắn dự án) **chuyển hẳn sang** hợp đồng hay **giữ ở dự án và hiển thị chung**? Nếu dự án có nhiều hợp đồng (thiết kế, thi công) thì nhóm công việc gắn hợp đồng nào? | `[1:05:50]` "tự động gom lại thành một", nhưng 1 dự án có n hợp đồng |
| Q-02 | Công việc nội bộ có cần **luồng duyệt** không, hay chỉ theo dõi (checking)? Có tính vào KPI theo trọng số riêng không? | `[0:22:54]`, `[0:24:49]` |
| Q-03 | Ai được **giao việc cho phòng ban** trên dự án: chỉ PM toàn dự án, hay cả Kinh doanh phụ trách cơ hội? | Phân quyền mục 4.3 |
| Q-04 | Danh sách 5 phòng ban chính: Kinh doanh (CRM), Hành chính – Nhân sự, Thiết kế, Quản lý thi công, Tài chính – Kế toán. **Cung ứng và Pháp lý** là phòng riêng hay thuộc phòng nào? | Bản chép lời `[0:12:55]` không rõ |
| Q-05 | Quy ước mã dự án: định dạng, có phân biệt loại hợp đồng hoặc năm không, có được trùng giữa các năm không? | C-6 |
| Q-06 | Khi trình nội bộ (D-5), người nhận có cần **bấm "Đồng ý"** để làm điều kiện trình khách, hay chỉ bình luận rồi Chủ nhiệm thiết kế tự quyết? | `[2:04:47]` nói chỉ xem và bình luận, `[2:05:27]` nhắc "đồng thuận" |
| Q-07 | Trưởng/phó phòng Thiết kế có được **CC mặc định** mọi dự án của phòng không? Chị Yến muốn cả hai cùng nắm thông tin `[1:51:01]` | D-2 |
| Q-08 | Quyền **xóa hồ sơ trình không đạt**: cấp cho Chủ nhiệm thiết kế của dự án hay Trưởng phòng Thiết kế? Hồ sơ đã trình khách có được xóa không? | `[2:21:28]–[2:21:55]` |
| Q-09 | Danh mục giấy phép có giống nhau cho mọi dự án không, hay thay đổi theo loại/quy mô công trình? | `[2:29:38]` |
| Q-10 | Duyệt trực tiếp đầu việc thiết kế là **Design Lead**, còn chốt phương án/chuyển giai đoạn là **Chủ nhiệm thiết kế**. Hiểu như vậy có đúng không? | `[1:49:36]` và `[2:05:00]` |
| Q-11 | Tiến độ thiết kế: Đội ngũ và Tiến độ **gộp làm một** hay giữ 2 tab có liên kết? (đội dev sẽ đề xuất sau khi có mẫu D-03) | `[2:27:24]` |

---

## 7. Kế hoạch triển khai đề xuất

Nguyên tắc: tập trung **CRM + Thiết kế** trước (QĐ-23). Mỗi đợt đều deploy lên host để NICON test và phản hồi qua Google Sheet. Hạng mục bị chặn bởi dữ liệu ở Phần 5 thì chỉ bắt đầu khi đã nhận mẫu.

### Đợt 0 — Ngay sau cuộc họp (không phụ thuộc NICON)

| Hạng mục | Mô tả |
|---|---|
| Deploy | Đẩy lên host các thay đổi đã có ở local nhưng chưa deploy: thiết kế trước hợp đồng, liên kết báo giá – hợp đồng, tạo hợp đồng từ cơ hội, trúng thầu thành cơ hội, chọn dự án có tìm kiếm, thông báo bản ghi vừa tạo |
| Tài liệu | Gửi NICON file PDF quy trình kịch bản A kèm ảnh chụp màn hình và tài liệu tổng hợp này |
| C-1 | Sắp lại menu CRM |
| P-1 | Đổi tên "Mua sắm" thành "Cung ứng" |
| D-1 | Đổi tên/bổ sung vai trò Chủ nhiệm thiết kế |
| C-4 | Nút "Chuyển thiết kế" trên Cơ hội (bản đầu: tạo dự án thiết kế cho dự án của cơ hội và thông báo trưởng phòng TK) |
| D-3 | "Hoàn thành" tự gửi duyệt |

### Đợt 1 — Nền tảng phân quyền và công việc (CRM + Thiết kế)

| Hạng mục | Điều kiện |
|---|---|
| U-1, U-2, U-3: nhóm quyền theo phòng ban, bộ quyền mẫu | Cần D-01, Q-04 |
| Mục 4.3: Gói công việc phòng ban (thiết kế dữ liệu, API, giao việc, thông báo, công việc nội bộ) | Cần Q-01, Q-02, Q-03 |
| D-2, D-7, U-4: một người duyệt + CC, đầu việc con, người duyệt chuyển bước | Cần Q-07, Q-10 |
| D-4: upload mọi giai đoạn, thư mục Drive khi khởi tạo | — |
| C-6: mã dự án sửa được | Cần D-06, Q-05 |

### Đợt 2 — Hoàn thiện Thiết kế và tiền hợp đồng

| Hạng mục | Điều kiện |
|---|---|
| D-5: trình nội bộ (xem và bình luận theo chỉ định) | Cần Q-06 |
| D-6: tiến độ thiết kế theo mẫu MS Project | Cần D-03, Q-11 |
| D-8: thầu phụ thiết kế | — |
| U-5: quyền xóa hồ sơ trình không đạt | Cần Q-08 |
| Q-2 / P-2 / F-1: giá đầu vào của Cung ứng, rà soát TC–KT trước HĐ | Sau mục 4.3 |
| T-1 đến T-5: quy trình đấu thầu đầy đủ | Sau mục 4.3 |
| Q-1: mẫu BOQ nhiều sheet | Cần D-02 |

### Đợt 3 — Pháp lý, Thi công, Tài chính (áp dụng cùng cấu trúc)

| Hạng mục | Điều kiện |
|---|---|
| L-1, L-2, L-3: Pháp lý giao việc theo giấy phép | Cần D-04, Q-09 |
| CN-1: tách BCH và CM | Cần D-05 |
| CN-2: đề xuất nguồn lực | — |
| CN-3, CN-5: QA/QC, template ISO | Đội dev đọc bộ ISO |
| CN-4, F-2, F-3, F-4: QS và bộ hồ sơ thanh toán đầu ra, đầu vào | Đọc quy trình thanh toán |
| P-4: thầu phụ dùng chung theo phòng ban | — |
| Drive: chuyển sang tài khoản Workspace NICON | Trước go-live |

### Tiêu chí hoàn thành mỗi hạng mục

- Kiểm tra dữ liệu ở cả frontend và server. Thông báo lỗi nêu rõ trường và quy tắc, có đủ 4 ngôn ngữ (vi/en/zh/ja).
- Phân quyền kiểm tra phía server. Có integration test cho các trường hợp được phép, bị từ chối và ngoài phạm vi.
- E2E smoke cho luồng chính của kịch bản A, kiểm tra trên mobile và tablet.
- Cập nhật `docs/user_guide.md`, `docs/users-rbac.md`, `docs/application_developer.md`. Sau khi NICON xác nhận thay đổi quy trình thì cập nhật `docs/Nicon-workflow.md`.
- Dữ liệu demo thể hiện đủ trạng thái bình thường, rỗng và lỗi để NICON test trên host.

---

## 8. Rủi ro và phụ thuộc

| Rủi ro | Tác động | Giảm thiểu |
|---|---|---|
| Mô hình Gói công việc (4.3) thay đổi cách mọi module liên kết với dự án và hợp đồng | Phạm vi lớn, có thể ảnh hưởng dữ liệu đang có | Triển khai dần, không viết lại module đang chạy. Chốt Q-01–Q-03 trước khi làm. Có migration và kế hoạch rollback |
| Chưa nhận mẫu BOQ, tiến độ, giấy phép | Trễ Q-1, D-6, L-1 | Ưu tiên hạng mục không phụ thuộc. Nhắc mẫu qua chị Huỳnh Anh |
| Yêu cầu Thiết kế có điểm chưa thống nhất nội bộ NICON (ai duyệt, ai được biết) | Làm lại luồng duyệt | Chốt Q-06, Q-07, Q-08, Q-10 bằng văn bản trước Đợt 1 |
| BOQ thực tế có cấu trúc khác nhau theo công trình | Import lỗi, dữ liệu sai | Mẫu chuẩn có sheet tùy biến, kiểm tra theo ô, báo lỗi rõ ràng |
| Tài liệu yêu cầu gốc (`Nicon-QLVH.md`, `Nicon-workflow.md`) chưa phản ánh thay đổi mới | Lệch giữa tài liệu và hệ thống | Cập nhật sau khi NICON xác nhận, đánh dấu nguồn là cuộc họp 03/10/2026 |
| Host chưa có các bản cập nhật mới nhất | NICON test trên bản cũ và báo lại lỗi đã sửa | Deploy ở Đợt 0, ghi phiên bản và ngày deploy trên Sheet |

---

## 9. Việc cần làm ngay

**Đội phát triển**
- [ ] Deploy các thay đổi local lên host và báo NICON test lại.
- [ ] Gửi PDF quy trình kịch bản A và tài liệu này cho NICON.
- [ ] Chuyển Phần 6 (câu hỏi) và Phần 5 (dữ liệu cần) lên Google Sheet, gán chị Huỳnh Anh.
- [ ] Đọc bộ hồ sơ ISO (BCH công trường, thanh toán), đề xuất cấu trúc rút gọn.
- [ ] Thực hiện các hạng mục Đợt 0.

**NICON**
- [ ] Gửi D-01 đến D-07 qua chị Huỳnh Anh.
- [ ] Trả lời Q-01 đến Q-11.
- [ ] Test trên host theo vai trò thực tế của từng người. Vai trò nào chưa đáp ứng thì phản hồi trên Google Sheet `[0:07:27]`.
