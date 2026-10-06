---
title: "NICON Management System — Tổng quan dự án sau cập nhật quy trình"
audience: "NICON, BA và đội phát triển"
prepared_date: 2026-10-06
workflow_baseline: "Cuộc họp demo 03/10/2026"
status: "Bản tổng quan làm việc — các quyết định mở cần NICON xác nhận"
---

# NICON Management System — Tổng quan dự án sau cập nhật quy trình

## 1. Mục tiêu của tài liệu

Đây là bức tranh tổng thể của hệ thống sau khi quy trình đã được điều chỉnh
tại cuộc họp 03/10/2026. Tài liệu dùng để BA, NICON và đội phát triển thống
nhất phạm vi, cách các module liên kết và thứ tự phát triển; không thay thế
tài liệu yêu cầu gốc hoặc quyết định bằng văn bản của NICON.

Phiên bản này lấy **Dự án** làm trục chung. Một phòng ban không tạo lại dự án
trong module của mình; thay vào đó phòng ban nhận và xử lý gói công việc trên
dự án/hợp đồng đã tồn tại.

## 2. Tầm nhìn và mục tiêu vận hành

NICON Management System số hóa chuỗi giá trị của tổng thầu Design & Build:

```text
Tiếp cận khách hàng → Cơ hội → Dự án → Báo giá/Đấu thầu → Hợp đồng
→ Thiết kế/Pháp lý/Cung ứng/Thi công/Tài chính → Nghiệm thu → Bàn giao
→ Báo cáo sức khỏe dự án và KPI
```

Hệ thống cần đạt các mục tiêu sau:

- Một nguồn dữ liệu dự án thống nhất, truy vết được từ Lead đến bàn giao.
- Công việc liên phòng ban có người chịu trách nhiệm, hạn xử lý, người duyệt,
  CC và lịch sử rõ ràng.
- Hồ sơ kỹ thuật, pháp lý, hiện trường và tài chính được tổ chức theo dự án,
  phân quyền đúng vai trò và liên kết với Google Drive.
- Dữ liệu tiến độ, nghiệm thu, mua sắm và thanh toán là đầu vào cho dashboard
  và KPI, thay vì tổng hợp thủ công.

## 3. Phạm vi nghiệp vụ và đối tượng cốt lõi

```text
Khách hàng (cá nhân/tổ chức)
└── Dự án
    ├── Cơ hội, khảo sát, báo giá, gói thầu
    ├── Gói công việc trước hợp đồng
    │   ├── Thiết kế concept
    │   ├── Cung ứng: giá đầu vào
    │   └── TC–KT: điều kiện thương mại, bảo lãnh, hồ sơ thầu
    ├── Hợp đồng (một dự án có thể có nhiều hợp đồng)
    │   ├── Hợp đồng chính với Chủ đầu tư
    │   ├── Hợp đồng thầu phụ/NCC
    │   └── Phụ lục/VO
    └── Gói công việc sau hợp đồng
        ├── Thiết kế, Pháp lý, Cung ứng
        ├── Thi công: BCH công trường và Phòng CM
        └── Tài chính – Kế toán

Công việc nội bộ: không gắn Dự án/Hợp đồng; quản lý ở tab riêng và có thể
được tính KPI sau khi NICON xác nhận quy tắc.
```

### Quy tắc nền tảng

| Quy tắc | Ý nghĩa vận hành |
|---|---|
| Dự án là bản ghi chung | Kinh doanh tạo dự án hoặc dự án tự sinh khi chuyển Lead; phòng khác không tạo bản sao dữ liệu dự án. |
| Gói công việc theo phòng ban | Trước hợp đồng gắn Dự án; sau hợp đồng cần liên kết/gom theo Hợp đồng theo quy tắc NICON sẽ xác nhận. |
| Báo giá trước hợp đồng | Hợp đồng chỉ được tạo từ hoặc gắn với báo giá đã duyệt. |
| Thiết kế trước hợp đồng | Có thể khởi tạo thiết kế để phục vụ báo giá; không chờ hợp đồng. |
| Một người duyệt | Mỗi đầu việc/hồ sơ có một người duyệt; người cần biết dùng CC, không đồng thời là người duyệt. |
| Phân quyền ở server | UI chỉ hỗ trợ thao tác; mọi quyền xem, sửa, duyệt, xóa phải được kiểm tra ở backend. |
| Không mất hồ sơ | Xóa, đổi liên kết, phát hành và thay đổi giai đoạn cần lưu lịch sử; hồ sơ đã phát hành/đã trình khách chỉ xử lý theo quyền và quy tắc xác nhận. |

## 4. Vai trò tham gia

| Nhóm/vài trò | Trách nhiệm chính |
|---|---|
| Ban giám đốc | Theo dõi sức khỏe dự án, tham gia trình nội bộ khi được chỉ định, quyết định theo thẩm quyền. |
| Kinh doanh/CRM | Quản lý Lead, Khách hàng, Cơ hội, Dự án, báo giá và chuyển giao yêu cầu cho phòng ban. |
| PM dự án | Lập team dự án, giao việc liên phòng ban, theo dõi tổng thể và phân vai duyệt theo dự án. |
| Chủ nhiệm thiết kế | Quản lý phương án/tiến độ thiết kế, trình nội bộ/khách, ghi nhận quyết định của khách. |
| Design Lead và thành viên thiết kế | Chia và thực hiện đầu việc chuyên môn trong phạm vi được giao. |
| Pháp lý | Quản lý giấy phép, hồ sơ, hạn xử lý và đầu việc theo từng giấy phép. |
| Cung ứng | Nhận yêu cầu giá/mua sắm, RFQ, so sánh giá, nhà cung cấp, vật tư và kho. |
| BCH công trường | Tổ chức thực hiện tại hiện trường, nhật ký, HSE, chất lượng, đề xuất nguồn lực và nghiệm thu. |
| Phòng CM | Giám sát nhiều dự án, kiểm tra/phê duyệt, đối chiếu hợp đồng và nghiệm thu. |
| Tài chính – Kế toán | Rà soát thương mại, bảo lãnh, quản lý dòng tiền và hồ sơ thanh toán đầu vào/đầu ra. |
| Quản trị hệ thống | Quản lý người dùng, vai trò; chọn quyền từ danh mục do hệ thống quản lý. |

Danh sách phòng ban, vị trí và bộ quyền mặc định chưa phải dữ liệu chốt. NICON
cần cung cấp D-01 để cấu hình đúng cơ cấu tổ chức thực tế.

## 5. Bốn kịch bản thương mại chính

| Kịch bản | Luồng sau cập nhật | Kết quả cần kiểm soát |
|---|---|---|
| A — D&B trọn gói | Lead/Cơ hội → Dự án → Thiết kế concept + giá đầu vào + rà soát TC–KT → Báo giá → Hợp đồng D&B → triển khai | Dữ liệu trước/sau hợp đồng không bị tách rời; tiến độ và chi phí truy vết theo dự án/hợp đồng. |
| B — Thiết kế trước, hợp đồng sau | Dự án/Cơ hội → Thiết kế concept/3D → BOQ, thương thảo → HĐ thiết kế hoặc HĐ thi công/D&B | Thiết kế được bắt đầu không cần hợp đồng nhưng vẫn có dự án chủ quản. |
| C — Đấu thầu cạnh tranh | Khách hàng + Dự án → Gói thầu → Kế hoạch đấu thầu, phân công hồ sơ → nộp thầu → trúng thầu → thương thảo/hợp đồng | Có kế hoạch, hạn và trách nhiệm của từng phòng trước khi nộp hồ sơ. |
| D — Tư vấn/báo giá sơ bộ | Dự án/Cơ hội → Concept → BOQ/suất đầu tư → báo giá → thương thảo/hợp đồng nếu thành công | Theo dõi rõ báo giá sơ bộ và khả năng chuyển đổi. |

## 6. Bản đồ chức năng toàn dự án

| Nhóm chức năng | Phạm vi sau cập nhật | Hiện trạng định hướng |
|---|---|---|
| 0. Quản trị, người dùng và quyền | Người dùng, vai trò, quyền theo phòng ban, bộ quyền mẫu, team dự án, người duyệt, CC | Có nền tảng quyền/team; cần nhóm quyền theo phòng và bộ quyền mẫu. |
| 1. CRM và tiền dự án | Lead, Khách hàng, Cơ hội, Dự án, khảo sát, báo giá, danh mục giá, đấu thầu | Có luồng cốt lõi; bổ sung lối tắt giao Thiết kế, menu và mã dự án sửa được. |
| 2. Thiết kế | Concept, Basic Design, Shop Drawing/IFC, phương án, revision, tiến độ, hồ sơ, trình nội bộ | Có nền tảng; là ưu tiên hoàn thiện đầu tiên. |
| 3. Pháp lý | Checklist giấy phép, hồ sơ, trạng thái cơ quan, giao việc/tiến độ theo giấy phép | Có checklist/hồ sơ; cần gói công việc và template theo loại dự án. |
| 4. Thi công và nghiệm thu | Tiến độ, nhật ký, HSE, punchlist, QA/QC, QS, nghiệm thu, hoàn công, bàn giao | Có nhiều chức năng nền; cần tách BCH/CM và bổ sung nguồn lực, chất lượng, QS. |
| 5. Cung ứng và kho | Giá đầu vào, BOQ đã ký, MR, RFQ, so sánh giá, NCC/thầu phụ, kho | Có luồng sau hợp đồng; cần hỗ trợ trước hợp đồng và dùng chung thầu phụ. |
| 6. Tài chính, chi phí và hợp đồng | HĐ chính/đầu vào, VO, mốc thanh toán, dòng tiền, hồ sơ thanh toán, P&L | Có hợp đồng và kiểm soát tài chính; cần liên thông hồ sơ thanh toán. |
| 7. Google Drive và tài liệu | Cây thư mục, upload, liên kết hồ sơ, phân quyền, viewer | Đã có tích hợp; trước go-live cần chuyển sang Workspace NICON. |
| 8. Dashboard, KPI và công việc nội bộ | Sức khỏe dự án, KPI theo dữ liệu nguồn, công việc không gắn dự án | Là đích đến; phụ thuộc dữ liệu vận hành và quy tắc KPI đã chốt. |
| Xuyên suốt | Thông báo, lịch sử thao tác, tìm kiếm, báo cáo, i18n vi/en/zh/ja, validation và audit | Áp dụng cho mọi module, không phát triển rời rạc từng nơi. |

## 7. Luồng thực hiện sau khi ký hợp đồng

```text
Hợp đồng đã ký
 ├── Thiết kế: cơ sở → chi tiết → IFC → chuyển cho Thi công
 ├── Pháp lý: checklist giấy phép → giao việc → theo dõi → kết quả
 ├── Cung ứng: BOQ ký → MR/RFQ → lựa chọn NCC/thầu phụ → kho/giao nhận
 ├── Thi công
 │   ├── BCH: nhật ký, HSE, nguồn lực, QA/QC, punchlist
 │   └── CM: tiến độ tổng, kiểm tra, phê duyệt, nghiệm thu
 └── TC–KT: hồ sơ thanh toán đầu ra/đầu vào → thu/chi → báo cáo chi phí
                           ↓
                  Hoàn công → nghiệm thu → bàn giao
```

### Liên kết bắt buộc giữa các module

| Nguồn | Đích | Liên kết cần có |
|---|---|---|
| Cơ hội | Thiết kế/Cung ứng/TC–KT | Tạo hoặc giao gói công việc trước hợp đồng, có thông báo người nhận. |
| Báo giá | Hợp đồng | Hợp đồng tham chiếu báo giá đã duyệt. |
| Thiết kế IFC | Thi công | Chỉ hồ sơ IFC được phát hành mới là đầu vào thi công. |
| Thiết kế cơ sở | Pháp lý | Hồ sơ/đầu việc pháp lý theo trạng thái được duyệt và loại dự án. |
| Thi công/BCH | Cung ứng/TC–KT | Đề xuất nguồn lực và chứng từ hiện trường là đầu vào cho cung ứng/thanh toán. |
| Nghiệm thu khối lượng | TC–KT | Là căn cứ hình thành bộ hồ sơ thanh toán đầu ra/đầu vào. |
| Mọi module | Drive | Tài liệu được lưu theo dự án, giai đoạn và quyền truy cập phù hợp. |
| Dữ liệu vận hành | Dashboard/KPI | Chỉ dùng dữ liệu đã xác định chủ sở hữu, trạng thái và quy tắc đo lường. |

## 8. Dữ liệu, tích hợp và yêu cầu chất lượng

### Dữ liệu NICON cần cung cấp

- Sơ đồ phòng ban, vai trò và bộ quyền mặc định.
- BOQ mẫu thực tế theo loại công trình, nhiều sheet Excel.
- Mẫu tiến độ Thiết kế từ MS Project.
- Danh mục giấy phép theo loại/quy mô công trình.
- Nhóm công việc của Phòng CM, quy ước mã dự án và các trường CRM cần tinh gọn.
- Bộ hồ sơ ISO BCH công trường và bộ hồ sơ thanh toán để chốt danh mục chuẩn.

### Google Drive

- Giai đoạn test dùng cấu hình hiện có; trước go-live phải chuyển sang tài khoản/
  Workspace của NICON.
- Hệ thống tạo và liên kết thư mục theo dự án; mọi thao tác upload, sửa, xóa,
  chia sẻ cần tôn trọng quyền trong hệ thống.
- Cấu trúc thư mục và quy tắc phân quyền là cấu hình vận hành phải được NICON
  xác nhận, không hard-code theo môi trường phát triển.

### Chất lượng, an toàn và trải nghiệm

- Mọi dữ liệu người dùng nhập đều được kiểm tra tại frontend và backend, có
  thông báo rõ ràng bằng bốn ngôn ngữ hỗ trợ.
- Luồng xem/sửa/duyệt/xóa phải có kiểm tra quyền server-side, lịch sử thao tác
  và kiểm thử cho trường hợp bị từ chối.
- Màn hình hiện trường ưu tiên mobile, thao tác ngắn, tải ảnh ổn định; màn hình
  văn phòng ưu tiên Gantt, so sánh giá, tiến độ và báo cáo.
- Mọi thay đổi schema cần migration, kiểm tra dữ liệu cũ, rollback và kiểm thử
  integration trước khi deploy.

## 9. Lộ trình phát triển và cổng quyết định

| Đợt | Trọng tâm | Cổng quyết định |
|---|---|---|
| 0 | Xác thực bản demo, deploy đúng phiên bản, tài liệu luồng D&B, các chỉnh sửa nhỏ không phụ thuộc mẫu | NICON xác nhận đang test đúng host/phiên bản. |
| 1 | CRM + Thiết kế: quyền theo phòng, gói công việc, giao việc, duyệt/CC, Drive, mã dự án | Chốt mô hình gói công việc, phòng ban và người có quyền giao/duyệt. |
| 2 | Hoàn thiện Thiết kế và tiền hợp đồng: trình nội bộ, tiến độ MS Project, BOQ, Cung ứng/TC–KT trước hợp đồng, đấu thầu | Nhận mẫu BOQ/tiến độ và quyết định các quyền/xóa/trình nội bộ. |
| 3 | Pháp lý, Thi công, Tài chính: template giấy phép, BCH/CM, QA/QC, QS, thanh toán | Nhận template pháp lý/CM/ISO và quy trình thanh toán đã duyệt. |
| 4 | Dashboard/KPI, tối ưu vận hành và go-live | KPI, nguồn dữ liệu, quyền dashboard và Workspace NICON được xác nhận. |

Không triển khai gói công việc dùng chung hay migration dữ liệu trước khi NICON
xác nhận cách gắn gói trước hợp đồng với một hay nhiều hợp đồng.

## 10. Điểm cần NICON/BA chốt

1. Khi dự án có nhiều hợp đồng, gói công việc trước hợp đồng được chuyển hay
   hiển thị chung; nếu chuyển thì theo hợp đồng nào?
2. Công việc nội bộ có duyệt, có KPI và có trọng số riêng hay không?
3. Ai có quyền giao việc liên phòng: PM dự án, Kinh doanh hay cả hai?
4. Cung ứng và Pháp lý thuộc phòng nào trong sơ đồ tổ chức?
5. Quy tắc mã dự án; phạm vi và điều kiện trùng mã.
6. Trình nội bộ là chỉ bình luận hay cần xác nhận đồng ý trước khi trình khách?
7. Quyền CC mặc định của trưởng/phó phòng Thiết kế.
8. Quyền xóa hồ sơ không đạt và chính sách với hồ sơ đã trình khách.
9. Danh mục giấy phép theo loại/quy mô công trình.
10. Phân vai duyệt Design Lead/Chủ nhiệm thiết kế.
11. Gộp hay liên kết hai khu vực Đội ngũ và Tiến độ Thiết kế.

## 11. Quản trị thay đổi và nghiệm thu

- Chị Huỳnh Anh là đầu mối tập hợp mẫu dữ liệu, câu trả lời và phản hồi NICON.
- Google Sheet chung là nơi ghi nhận câu hỏi, người phụ trách, ngày chốt và
  trạng thái; một yêu cầu chỉ được đưa vào sprint sau khi có quyết định rõ ràng.
- Mỗi đợt deploy phải công bố URL, commit/phiên bản, dữ liệu demo, vai trò test
  và checklist kịch bản. Không suy ra trạng thái host từ trạng thái local.
- Nghiệm thu một hạng mục phải bao gồm happy path, validation, phân quyền,
  thông báo, dữ liệu liên quan, responsive mobile/tablet khi có giao diện và
  regression của các luồng tích hợp.
- Sau khi NICON xác nhận thay đổi quy trình, cập nhật `docs/Nicon-workflow.md`,
  `docs/user_guide.md`, `docs/users-rbac.md` và `docs/application_developer.md`
  khi phạm vi nội dung của từng tài liệu bị ảnh hưởng.

## 12. Trạng thái tài liệu và nguồn tham chiếu

Tài liệu này được lập từ:

- `docs/Nicon-QLVH.md` — mục tiêu và phạm vi nghiệp vụ gốc.
- `docs/Nicon-workflow.md` — sơ đồ luồng nghiệp vụ gốc.
- `NICON_Meeting_2026-10-03_Tong-hop-ke-hoach.md` — biên bản tổng hợp cuộc họp.
- `NICON_Cap-nhat-BA_2026-10-06.md` — tình hình, lộ trình và câu hỏi cần chốt.

Các nội dung chưa được NICON xác nhận trong Mục 10 là rủi ro yêu cầu, không phải
quy tắc đã có hiệu lực để đội phát triển tự suy diễn hoặc triển khai.
