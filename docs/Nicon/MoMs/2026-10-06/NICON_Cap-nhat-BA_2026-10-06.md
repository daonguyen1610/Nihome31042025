---
title: "NICON — Cập nhật tình hình dự án và định hướng phát triển sau họp 03/10/2026"
audience: "Đội BA, đội phát triển và NICON (đầu mối: chị Huỳnh Anh)"
meeting_date: 2026-10-03
prepared_date: 2026-10-06
status: "Bản làm việc — cần NICON xác nhận các câu hỏi mở trước khi chốt phạm vi"
---

# NICON — Cập nhật tình hình dự án và định hướng phát triển

## 1. Mục đích và cách sử dụng

Tài liệu chuyển nội dung cuộc họp demo ngày 03/10/2026 thành bản cập nhật để
BA, đội phát triển và NICON thống nhất hiện trạng, hướng phát triển, dữ liệu
cần cung cấp và các quyết định còn mở.

Đây chưa phải thay đổi yêu cầu chính thức. Các câu hỏi ở [Mục 7](#7-câu-hỏi-cần-nicon-xác-nhận) phải được NICON xác nhận qua chị Huỳnh Anh hoặc Google Sheet trước khi chốt thiết kế dữ liệu và ước lượng.

> “Đã có trong mã nguồn” không đồng nghĩa “đã có trên host”. Deploy và phiên bản môi trường test phải được xác nhận riêng.

## 2. Tóm tắt điều hành

1. **Dự án là bản ghi dùng chung.** Kinh doanh tạo dự án; phòng ban khác không tạo lại dự án mà làm việc qua gói công việc của phòng mình.
2. **Thiết kế có thể bắt đầu trước hợp đồng.** Khả năng này đã có trong mã nguồn; cần bổ sung lối tắt khởi tạo từ Cơ hội và xác nhận phiên bản host.
3. **Ưu tiên đã thống nhất là CRM + Thiết kế.** Cung ứng, Pháp lý, Thi công và Tài chính sẽ dùng cùng nguyên tắc gói công việc, nhưng triển khai sau để giảm rủi ro.
4. **Không viết lại module đang có.** Dự án Thiết kế, tiến độ Thi công và checklist Pháp lý được giữ lại và liên kết dần với mô hình chung.
5. **Chưa thể chốt lịch/chi phí.** Bảy bộ mẫu dữ liệu và mười một câu hỏi nghiệp vụ là điều kiện để chốt giải pháp, migration và ước lượng.

## 3. Hiện trạng đã đối chiếu

| Hạng mục | Trạng thái mã nguồn | Ý nghĩa với kế hoạch |
|---|---|---|
| Chuyển Lead | Đã có tạo Khách hàng, Cơ hội, Dự án vận hành và thông báo bản ghi tạo ra | Giữ làm điểm vào chuẩn của CRM |
| Báo giá và hợp đồng | Đã có liên kết báo giá đã duyệt với hợp đồng | Giữ quy tắc phải có báo giá trước hợp đồng |
| Thiết kế trước hợp đồng | Đã có: `ContractId` của Dự án thiết kế là tùy chọn; thay đổi tại commit `f130cdb0` | Cần xác nhận deploy; thêm lối tắt từ Cơ hội |
| Dự án Thiết kế | Đã có các giai đoạn Concept, Cơ sở, Shop Drawing/IFC, tài liệu và luồng duyệt hiện hữu | Hoàn thiện vai trò, trình nội bộ và tiến độ theo mẫu NICON |
| Pháp lý | Đã có checklist giấy phép, cập nhật trạng thái và upload hồ sơ | Bổ sung giao việc/tiến độ theo từng giấy phép |
| Cung ứng sau hợp đồng | Đã có BOQ dự án, MR, RFQ, so sánh giá, kho và cảnh báo vật tư | Bổ sung giá đầu vào trước hợp đồng và thầu phụ dùng chung |
| Thi công | Đã có tiến độ, nhật ký, nghiệm thu, hoàn công và bàn giao | Tách BCH công trường/Phòng CM; thêm nguồn lực, QA/QC, QS |
| Tài chính | Đã có hợp đồng đầu ra/đầu vào, mốc thanh toán và kiểm soát tài chính | Bổ sung rà soát trước hợp đồng và hồ sơ thanh toán liên phòng ban |
| Gói công việc dùng chung | Chưa có thực thể chung cho phòng ban hoặc công việc nội bộ | Là nền tảng mới, chỉ bắt đầu sau Q-01 đến Q-03 |

**Cần xác minh môi trường:** mã nguồn cho thấy nhánh hiện tại có bốn commit chưa
có trên nhánh remote cùng tên, nhưng điều đó không chứng minh trạng thái host.
Đội phát triển cần công bố URL và commit/phiên bản đã deploy để NICON test.

## 4. Định hướng nghiệp vụ mục tiêu

```text
Khách hàng
└── Dự án (Kinh doanh tạo hoặc sinh khi chuyển Lead)
    ├── Gói công việc trước hợp đồng
    │   ├── Thiết kế concept
    │   ├── Cung ứng: giá đầu vào
    │   └── TC–KT: điều kiện hợp đồng, bảo lãnh, hồ sơ thầu
    ├── Gói thầu → kế hoạch đấu thầu → hồ sơ dự thầu
    ├── Báo giá đầu ra
    └── Hợp đồng (một dự án có thể có nhiều hợp đồng)
        └── Gói công việc sau hợp đồng: Thiết kế, Pháp lý,
            Cung ứng, Thi công và TC–KT

Công việc nội bộ: tab riêng, không gắn dự án/hợp đồng, có thể dùng cho KPI.
```

Nguyên tắc BA cần giữ xuyên suốt: một phòng ban nhận việc trên cùng dự án thay
vì nhập lại dự án. Người chủ trì của phòng mới được chia đầu việc; mỗi đầu việc
có một người duyệt, các bên chỉ cần theo dõi dùng CC.

Luồng D&B ưu tiên: `Lead → Khách hàng + Cơ hội + Dự án → Thiết kế/Cung ứng/TC–KT → Báo giá → Hợp đồng → Thiết kế/Pháp lý/Cung ứng/Thi công/TC–KT → Nghiệm thu, thanh toán, bàn giao`.

## 5. Quyết định đã ghi nhận từ cuộc họp

| Nhóm | Baseline nghiệp vụ cho BA |
|---|---|
| Quyền và vai trò | Hệ thống quản lý danh mục quyền; NICON tạo vai trò và gán quyền. Vai trò theo phòng ban có bộ quyền mặc định để chỉnh thêm/bớt. |
| CRM | Menu ưu tiên Dự án, Cơ hội, Hợp đồng. Lead và Cơ hội vẫn riêng; Lead khóa sau chuyển đổi. Mã dự án cần sửa được theo quy ước NICON. |
| Dự án/công việc | Có công việc theo dự án/hợp đồng và công việc nội bộ ở tab riêng. Phòng ban không tạo lại dự án. |
| Thương mại | Bắt buộc có báo giá trước hợp đồng. BOQ/suất đầu tư dùng Excel chuẩn. Giá đầu vào Cung ứng đi trước báo giá đầu ra. |
| Đấu thầu | Cần kế hoạch đấu thầu để phân công hồ sơ/tiến độ; trúng thầu chuyển sang thương thảo và hợp đồng. |
| Thiết kế | Khởi tạo trên dự án có sẵn, không bắt buộc hợp đồng; kích hoạt từ Cơ hội hoặc giao việc trong Dự án. Hoàn thành tự chuyển người duyệt. |
| Duyệt Thiết kế | Một đầu việc chỉ có một người duyệt; người khác dùng CC. PM phòng Thiết kế đổi thành Chủ nhiệm thiết kế/Design Manager, vẫn giữ Design Lead. |
| Trình nội bộ | Người chỉ định được xem và bình luận; Chủ nhiệm thiết kế ghi nhận quyết định của khách trước khi chuyển bước. |
| Hồ sơ Thiết kế | Giữ tab Tài liệu tổng hợp; upload mọi giai đoạn; IFC chuyển thẳng sang Thi công. Thầu phụ là đầu việc, không tách module. |
| Pháp lý/Cung ứng | Công việc pháp lý ở module Pháp lý. “Mua sắm” đổi nhãn thành “Cung ứng”; kiểm soát BOQ đã ký thuộc sau hợp đồng. |
| Thi công | Tách BCH công trường và Phòng CM; bổ sung đề xuất nguồn lực, QA/QC và QS. |
| Ưu tiên | Hoàn thiện CRM + Thiết kế trước; các module sau áp dụng cùng cấu trúc. |

## 6. Lộ trình đề xuất

Thứ tự dưới đây không phải cam kết ngày hoàn thành. BA chỉ chốt ước lượng sau
khi nhận đủ mẫu và câu trả lời chặn.

| Đợt | Phạm vi | Điều kiện/đầu ra |
|---|---|---|
| 0 — Xác thực demo | Deploy và ghi nhận phiên bản; gửi luồng D&B có ảnh; sắp menu CRM; đổi nhãn Cung ứng; vai trò Chủ nhiệm thiết kế; lối tắt tạo việc Thiết kế; hoàn thành tự gửi duyệt | NICON test đúng phiên bản, phản hồi trên Google Sheet |
| 1 — Nền tảng CRM + Thiết kế | Phòng ban/bộ quyền mẫu; thiết kế gói công việc; giao việc/thông báo; một người duyệt + CC; đầu việc con; upload/Drive; mã dự án sửa được | D-01, D-06 và Q-01 đến Q-05, Q-07, Q-10 |
| 2 — Tiền hợp đồng | Trình nội bộ, tiến độ MS Project, thầu phụ Thiết kế, quyền xóa; giá đầu vào Cung ứng, rà soát TC–KT, đấu thầu, BOQ nhiều sheet | D-02, D-03 và Q-06, Q-08, Q-11 |
| 3 — Phòng ban còn lại | Pháp lý; BCH/CM; nguồn lực; QA/QC; QS; hồ sơ thanh toán; thầu phụ dùng chung; Workspace NICON | D-04, D-05, quy trình ISO/thanh toán đã xác nhận |

## 7. Câu hỏi cần NICON xác nhận

| Mã | Câu hỏi cần chốt | Ảnh hưởng |
|---|---|---|
| Q-01 | Khi có hợp đồng, gói trước hợp đồng chuyển hẳn sang hợp đồng hay giữ ở Dự án và hiển thị chung? Nếu nhiều hợp đồng thì gắn hợp đồng nào? | Mô hình dữ liệu gói công việc |
| Q-02 | Công việc nội bộ có luồng duyệt không? Có tính KPI với trọng số riêng không? | Trạng thái, quyền, KPI |
| Q-03 | Ai giao việc cho phòng ban: chỉ PM dự án hay cả Kinh doanh phụ trách cơ hội? | Phân quyền giao việc |
| Q-04 | Cung ứng và Pháp lý là phòng độc lập hay trực thuộc phòng nào? | Nhóm quyền mặc định |
| Q-05 | Quy ước mã dự án: định dạng, năm/loại hợp đồng, quy tắc trùng mã? | Validation và migration mã |
| Q-06 | Người nhận trình nội bộ chỉ bình luận hay phải đồng ý trước khi trình khách? | Luồng trình nội bộ |
| Q-07 | Trưởng/phó phòng Thiết kế có là CC mặc định mọi dự án không? | Thông báo, quyền xem |
| Q-08 | Ai được xóa hồ sơ trình không đạt? Hồ sơ đã trình khách có được xóa không? | Phân quyền, lịch sử hồ sơ |
| Q-09 | Danh mục giấy phép chung hay thay đổi theo loại/quy mô công trình? | Template Pháp lý |
| Q-10 | Design Lead duyệt đầu việc, Chủ nhiệm thiết kế chốt phương án/chuyển giai đoạn: có đúng không? | Vai trò duyệt |
| Q-11 | Đội ngũ và Tiến độ Thiết kế gộp một màn hình hay hai tab liên kết? | UX và mẫu tiến độ |

## 8. Dữ liệu NICON cần gửi qua chị Huỳnh Anh

| Mã | Dữ liệu/mẫu | Dùng để chốt |
|---|---|---|
| D-01 | Sơ đồ phòng ban, vai trò, bộ quyền mặc định | Phân quyền theo phòng ban |
| D-02 | BOQ thực tế theo loại công trình, Excel nhiều sheet | Mẫu/import BOQ |
| D-03 | Mẫu tiến độ Thiết kế từ MS Project | Trường dữ liệu và giao diện tiến độ |
| D-04 | Danh sách giấy phép theo loại/quy mô dự án | Template Pháp lý |
| D-05 | Danh sách/nhóm công việc của Phòng CM | Menu và nghiệp vụ Thi công |
| D-06 | Quy ước mã dự án | Validation mã dự án |
| D-07 | Trường thừa trên Lead/Cơ hội, đánh dấu trên Sheet | Tinh gọn form CRM |

Bộ hồ sơ ISO BCH công trường và thanh toán là đầu vào đã có; BA cần cùng NICON
xác nhận danh mục rút gọn trước khi phát triển QA/QC và hồ sơ thanh toán.

## 9. Rủi ro và nguyên tắc kiểm soát

| Rủi ro | Kiểm soát |
|---|---|
| Gói công việc ảnh hưởng liên kết dự án/hợp đồng và dữ liệu cũ | Chỉ làm sau Q-01 đến Q-03; triển khai dần; có migration, rollback và integration test |
| Bản ghi họp nhận dạng không rõ | Để thành câu hỏi mở, không tự suy diễn thành yêu cầu |
| Thiếu BOQ/tiến độ/giấy phép mẫu | Không chốt import, trường dữ liệu hoặc ước lượng chi tiết |
| Vai trò duyệt/xem Thiết kế chưa thống nhất | Chốt Q-06 đến Q-10 bằng văn bản trước Đợt 1/2 |
| Test nhầm host cũ | Mỗi đợt công bố URL, commit/phiên bản, dữ liệu demo và checklist test |
| Tài liệu yêu cầu gốc chưa phản ánh họp | Chỉ cập nhật `docs/Nicon-workflow.md` sau khi NICON xác nhận |

## 10. Việc cần làm ngay

### Đội phát triển

- Xác minh và công bố phiên bản host; không coi trạng thái local là đã bàn giao.
- Gửi luồng D&B có ảnh; chuyển D-01…D-07 và Q-01…Q-11 thành các dòng có người phụ trách trên Google Sheet.
- Chuẩn bị các hạng mục Đợt 0 không phụ thuộc dữ liệu NICON.
- Đọc bộ ISO và đề xuất cấu trúc tối giản để BA/NICON duyệt.

### BA và NICON

- Chỉ định người trả lời từng câu hỏi ở Mục 7 và hạn phản hồi.
- Gửi bảy bộ dữ liệu/mẫu ở Mục 8 qua chị Huỳnh Anh.
- Test đúng host, đúng vai trò; phản hồi gồm URL, bước tái hiện, kết quả thực tế/mong muốn và ảnh nếu có.
- Xác nhận baseline này trước khi phát triển thay đổi kiến trúc hoặc dữ liệu.

## 11. Căn cứ cập nhật

- Bản tổng hợp cuộc họp: `NICON_Meeting_2026-10-03_Tong-hop-ke-hoach.md`.
- Đối chiếu mã nguồn ngày 06/10/2026, gồm thay đổi “Thiết kế trước hợp đồng” tại commit `f130cdb0` và các thay đổi CRM trên nhánh hiện tại.
- Tài liệu nghiệp vụ hiện hành: `docs/Nicon-QLVH.md` và `docs/Nicon-workflow.md`.

Tài liệu này không thay thế tài liệu nghiệp vụ gốc. Khi NICON chốt các điểm mở,
BA cần cập nhật quy trình chuẩn tại `docs/Nicon-workflow.md` trước khi phát triển
các thay đổi nghiệp vụ tương ứng.
