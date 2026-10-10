# Yêu cầu quản lý và vận hành NICON

## 1. Mục đích và phạm vi

NICON Management System là nền tảng quản trị Design & Build theo Dự án, kết nối
CRM, Thiết kế, Pháp lý, Thi công, Cung ứng, Tài chính, tài liệu và KPI. Mục tiêu
là mỗi giao dịch nghiệp vụ được ghi nhận một lần, có người chịu trách nhiệm,
trạng thái, bằng chứng và khả năng truy vết xuyên module.

Tài liệu này là hợp đồng nghiệp vụ mục tiêu. Trạng thái phần mềm thực tế được
mô tả trong `user_guide.md` và `application_developer.md`; yêu cầu xuất hiện ở
đây không tự động có nghĩa đã triển khai.

### 1.1. Nguyên tắc nguồn

1. Yêu cầu trực tiếp hiện tại của NICON.
2. Quyết định họp mới nhất đã được xác nhận.
3. Tài liệu canonical này và `Nicon-workflow.md`.
4. PRD/workflow gốc, BreakTask, tiến độ và BOQ mẫu trong `docs/Nicon/`.
5. Mã nguồn là bằng chứng trạng thái triển khai, không tự thay đổi yêu cầu.

Đề xuất hoặc câu nói chưa thống nhất được giữ thành quyết định mở. Dữ liệu mẫu,
tên sheet, màu sắc, ngày và tên khách hàng trong file không phải quy tắc sản phẩm.

### 1.2. Thuật ngữ bắt buộc

- **Operational Project / Dự án:** aggregate dùng chung xuyên tám module.
- **Upstream:** hợp đồng, nghiệm thu, phải thu với Chủ đầu tư/khách hàng.
- **Downstream:** hợp đồng, nghiệm thu, phải trả với NCC/thầu phụ/tổ đội.
- **Design Project:** aggregate chuyên môn Thiết kế; phải thuộc một Operational
  Project, không thay thế Dự án dùng chung.
- **BCH:** Ban Chỉ huy Công trình tại hiện trường.
- **Phòng CM:** Phòng Quản lý Thi công tại văn phòng, thực hiện quản lý danh mục
  dự án và kiểm soát chéo.
- **IFC:** revision hồ sơ được phát hành chính thức cho thi công.
- **Baseline:** phiên bản kế hoạch đã công bố; sửa đổi tạo version mới, không ghi
  đè lịch sử.

## 2. Yêu cầu xuyên suốt

### 2.1. Dữ liệu và audit

- Một khách hàng có thể có nhiều Dự án; một Dự án có thể có nhiều cơ hội, báo
  giá và hợp đồng theo quan hệ nghiệp vụ hợp lệ.
- Bản ghi vận hành phải có project, actor/responsible owner, lifecycle status,
  timestamps, creator/updater và concurrency token khi có rủi ro ghi đè.
- Sự kiện phê duyệt, từ chối, hủy, reversal, reopen và correction giữ lịch sử;
  không chỉnh trực tiếp dữ liệu terminal để che thay đổi.
- Mọi write path gồm UI, API, import, seed và migration phải giữ cùng invariant.

### 2.2. Quyền và phân tách nhiệm vụ

- Hệ thống dùng permission; role là bộ permission có thể quản trị.
- Quyền xem toàn bộ không kéo theo quyền sửa toàn bộ.
- Người lập không được thực hiện bước thẩm định độc lập trên cùng chứng từ khi
  quy trình yêu cầu kiểm soát chéo.
- Mỗi đầu việc Thiết kế có một người duyệt; người cần theo dõi dùng CC. Việc ai
  duyệt đầu việc và ai chốt chuyển giai đoạn còn theo quyết định Q-10.
- Backend phải kiểm tra quyền, scope Dự án, trạng thái và concurrency; ẩn nút ở
  frontend không phải biện pháp bảo mật.

### 2.3. Trạng thái và tích hợp

- Chỉ sự kiện nghiệp vụ terminal được duyệt/xác nhận/posted/paid mới kích hoạt
  downstream hoặc KPI.
- Retry không tạo bản ghi hoặc side effect trùng. Rejection phải giữ downstream
  state không đổi, ngoài audit/notification được phép.
- File được quản lý bằng metadata Nicon và binary storage; đường dẫn file không
  được dùng làm khóa liên kết nghiệp vụ.

## 3. Module 1 — CRM, Sales và tiền dự án

### 3.1. Mục tiêu CRM và tiền dự án

Quản lý nguồn khách, cơ hội, khảo sát, báo giá/đấu thầu và chuyển đổi thành Dự án
và hợp đồng mà không nhập lại dữ liệu.

### 3.2. Yêu cầu CRM và tiền dự án

- Lead: nguồn, phân khúc, người phụ trách, lịch sử tương tác và deadline phản hồi.
- Chuyển Lead tạo/liên kết Customer, Opportunity và Operational Project một cách
  idempotent; Lead sau chuyển đổi bị khóa.
- Pipeline chuẩn: Tiếp cận → Khảo sát → Báo giá/Đấu thầu → Thương thảo → Ký HĐ.
- Báo giá trực tiếp hỗ trợ suất đầu tư và BOQ sơ bộ, tính VAT/chiết khấu/tổng,
  version và duyệt nội bộ/khách hàng. Báo giá xuất và gửi khách dưới dạng file
  Excel có đầy đủ dòng giá, VAT, chiết khấu và tổng cộng.
- Đấu thầu có kế hoạch, checklist, deadline, hồ sơ năng lực, dự toán versioned và
  kết quả trúng/trượt.
- Khảo sát mobile ghi điều kiện hiện trường, tọa độ, ảnh/video/file và checklist;
  đồng bộ có trạng thái, retry và xung đột rõ ràng.
- Hợp đồng Upstream có thể lập trực tiếp từ Cơ hội khi khách đã thống nhất giá
  trị, không bắt buộc có báo giá. Nếu cần báo giá, Kinh doanh duyệt nội bộ rồi
  xem trước email, chọn địa chỉ khách và gửi; báo giá được gắn vào hợp đồng phải
  đã duyệt, còn hiệu lực khi gắn và đúng khách hàng/Dự án. Hợp đồng Downstream
  với NCC/thầu phụ không dùng báo giá CRM đầu ra.

### Điểm mở

Quy ước mã Dự án, quyền giao việc liên phòng và các field CRM NICON muốn bỏ cần
được xác nhận bằng văn bản.

## 4. Module 2 — Thiết kế ba giai đoạn

### 4.1. Mục tiêu Thiết kế

Kiểm soát độ chín, version và người chịu trách nhiệm của hồ sơ Thiết kế; chỉ IFC
được phát hành mới được đưa ra công trường.

### Giai đoạn và cổng

1. **Concept:** quản lý nhiều phương án; trình nội bộ/khách hàng; chỉ một phương
   án được Finalized để mở giai đoạn sau.
2. **Basic Design:** quản lý theo bộ môn, hồ sơ xin phép và duyệt nội bộ; điều
   kiện mở Detailed/Shop Drawing phải kiểm tra trên server.
3. **Detailed/Shop Drawing:** theo bộ môn và hạng mục; review, revision, trạng
   thái Approved/Pending IFC/Released.
4. **IFC:** gói phát hành có drawing/revision cụ thể, người nhận và xác nhận đã
   nhận; hủy gói không được làm mất lịch sử.

Thiết kế có thể bắt đầu trước hợp đồng nhưng luôn thuộc Dự án. Team và tiến độ
Thiết kế sử dụng Operational Project; file Basic/Shop hiện có upload/preview,
trong khi media/feedback theo file của Concept và review markup vẫn là mục tiêu.

### Quyết định mở

- Q-06: trình nội bộ chỉ bình luận hay cần đồng thuận bắt buộc.
- Q-07: CC mặc định cho trưởng/phó phòng.
- Q-08: quyền xóa hồ sơ không đạt, đặc biệt hồ sơ đã trình khách.
- Q-10: Design Lead duyệt đầu việc và Design Manager chốt giai đoạn hay mô hình
  khác.
- Q-11: team và schedule gộp hay tách tab liên kết.

## 5. Module 3 — Pháp lý và xin phép

### 5.1. Mục tiêu Pháp lý

Bảo đảm hồ sơ pháp lý đúng loại công trình, đúng hạn và đủ điều kiện sử dụng.

### 5.2. Yêu cầu Pháp lý

- Checklist theo loại/quy mô dự án: GPXD, PCCC, điện/nước, vỉa hè, môi trường,
  an toàn và hoàn công khi áp dụng.
- Mỗi permit có cơ quan cấp, owner, target deadline, ngày nộp/cấp/hết hạn, hồ sơ
  nộp, hồ sơ cấp và trạng thái.
- Luồng: Not Started → Preparing → Submitted → Under Review → Issued; Need More
  Docs, Rejected và Expired là các nhánh cần xử lý rõ.
- Cảnh báo quá hạn, sắp đến hạn và sắp hết hạn.
- Permit bắt buộc chưa Issued hoặc đã Expired phải chặn cổng khởi công liên quan.

Hiện phần mềm đã có checklist, owner/deadline, tài liệu và risk filters; work
package/template theo loại dự án và liên kết cổng khởi công còn cần hoàn thiện.

## 6. Module 4 — Thi công và nghiệm thu

### 6.1. Mô hình vận hành

Module 4 có hai giai đoạn và hai tuyến trách nhiệm. Hợp đồng Upstream D&B/thi
công đã ký kích hoạt **CM Preparation**, không tự động cho phép khởi công.

#### Giai đoạn 1 — Phòng CM chuẩn bị

1. WBS, Gantt, mốc móng/kết cấu/hoàn thiện/MEP, đường găng và Baseline S-Curve.
2. Cơ cấu BCH gồm Chỉ huy trưởng, Field/QA-QC, Site QS, HSE và Thủ kho.
3. Kế hoạch vật tư, máy/thiết bị và nhân công theo tiến độ.
4. Bid Tabulation, đánh giá NCC/thầu phụ/tổ đội và kế hoạch HĐ Downstream cùng
   Module 5/6.
5. Budget Baseline cho vật tư, nhân công, máy và chi phí BCH; sau khi khóa chỉ
   thay đổi qua quyền/version/audit và VO khi ảnh hưởng phạm vi/giá trị.

#### Cổng đủ điều kiện khởi công

Server phải xác nhận hợp đồng hiệu lực, IFC phù hợp, permit bắt buộc còn hiệu
lực, baseline đã công bố, BCH/resource plan được duyệt và Budget Baseline đã
khóa. Bàn giao mặt bằng, biện pháp thi công và kế hoạch HSE là điều kiện theo
template dự án sau khi NICON chốt bộ hồ sơ ISO.

#### Giai đoạn 2 — BCH và Phòng CM chạy song song

- **BCH Operations:** đề xuất vật tư/máy/nhân công; nhật ký điều phối hằng ngày.
- **QA/QC:** hồ sơ chất lượng, kiểm tra theo IFC, Punchlist, nhật ký và HSE.
- **Site QS:** nghiệm thu khối lượng Upstream và Downstream tách biệt.
- **Phòng CM:** thẩm định chất lượng, kiểm soát S-Curve, HSE, ngân sách, MR, QS
  và VO trên nhiều công trình.

Trễ tiến độ **lớn hơn 5%** so với baseline tạo cảnh báo đỏ, giải trình và kế
hoạch phục hồi. Công thức, kỳ chốt và nguồn progress phải lưu được để tái lập.

### 6.2. Nguồn lực và BOQ

- Đề xuất nguồn lực bắt buộc có loại, WBS, ngày cần, khối lượng và lý do.
- MR vật tư kiểm tra Execution BOQ revision được duyệt, không dùng BOQ sơ bộ.
- Trong hiện trạng, vượt allowance còn lại bị chặn. Mục tiêu 85% vàng, 95% cam,
  trên 100% đỏ/chặn chỉ triển khai sau khi chốt công thức và VO behavior.
- Đề xuất ngoài kế hoạch/vượt hạn mức cần CM level-2 review độc lập trước khi
  chuyển Procurement.

### 6.3. Chất lượng, an toàn và nhật ký

- QA/QC dossier gồm checklist, biên bản mẫu/thí nghiệm, chứng chỉ vật liệu, IFC
  revision và bằng chứng ảnh/file.
- Punchlist có vị trí, severity, root cause, assignee, deadline, xử lý, verify và
  reopen; lỗi chặn chưa đóng tham gia readiness nghiệm thu/bàn giao.
- HSE có người vi phạm/chịu trách nhiệm, remediation owner/deadline, evidence,
  confirmation độc lập, correction và close.
- Draft/offline chưa đồng bộ không phải bằng chứng duyệt hoặc KPI.

### 6.4. QS, nghiệm thu và thanh toán

- Upstream QS liên kết HĐ Upstream, CĐT/TVGS, WBS, BOQ/VO và milestone phải thu.
- Downstream QS liên kết HĐ Downstream, NCC/thầu phụ/tổ đội, WBS và dossier phải
  trả.
- Khối lượng đã duyệt bất biến; correction dùng version thay thế hoặc reversal.
- Payment review Downstream dùng HĐ hợp lệ + QS được duyệt + warehouse receipt
  posted khi gói có vật tư. Ma trận theo loại package vẫn cần NICON chốt.

### 6.5. Hoàn công và bàn giao

- Có ít nhất một acceptance được duyệt, đủ category as-built bắt buộc, không còn
  Punchlist chặn, commissioning và checklist hoàn tất.
- Final handover cần signatory và quyền `complete`; reopen giữ status history.
- Điều kiện kỹ thuật không thay thế điều kiện tài chính/hợp đồng.

### 6.6. Ranh giới hiện trạng

Construction Task, Diary, Punch, Acceptance, As-built và Handover hiện dùng
`DesignProjectId`; HSE dùng `OperationalProjectId`. Baseline/S-Curve, BCH
organization, Budget Baseline, QA/QC dossier, resource request cho máy/nhân công
và QS hai hướng chưa có aggregate hoàn chỉnh. Mọi mở rộng mới dùng Operational
Project và cần kế hoạch migration tương thích cho dữ liệu cũ.

## 7. Module 5 — Cung ứng và kho

### 7.1. Mục tiêu Cung ứng và kho

Quản lý giá đầu vào, lựa chọn NCC/thầu phụ, hạn mức thực hiện và tồn kho dự án.

### 7.2. Yêu cầu Cung ứng và kho

- Vendor directory, hồ sơ năng lực, trạng thái active và rating có phê duyệt.
- RFQ: lines, invited vendors, portal token, bid revisions/withdrawal, đánh giá,
  bid tabulation, award và audit; award liên kết HĐ Downstream.
- Tách rõ material-rate catalog, Tender Estimate, Quote BOQ và Project Execution
  BOQ. Chỉ execution revision Approved hiện hành làm allowance MR/kho.
- MR: Draft → Submitted → Approved/Rejected → Partially Fulfilled/Fulfilled hoặc
  Cancelled; approval không tự nhập kho.
- Warehouse receipt/issue: Draft → Posted → Reversed; tồn kho là ledger dẫn xuất,
  không sửa số tồn trực tiếp.
- BOQ import workbook một/nhiều sheet có preview, mapping, lỗi theo sheet/ô,
  provenance và xác nhận; không hard-code mẫu khách hàng.

## 8. Module 6 — Tài chính, chi phí và hợp đồng

### 8.1. Mục tiêu Tài chính và hợp đồng

Kiểm soát hợp đồng hai chiều, phải thu/phải trả, dòng tiền và lợi nhuận Dự án.

### 8.2. Yêu cầu Tài chính và hợp đồng

- Hợp đồng Upstream/Downstream có direction/type rõ, milestone, appendix/VO,
  attachment, lifecycle và Operational Project.
- VO được version, submit, approve/reject và không sửa ngầm baseline/BOQ gốc.
- Payment Request: Draft → Under Validation → Ready for Approval → Approved →
  Paid, với Rejected/Cancelled; actor và reference bắt buộc theo loại chứng từ.
- Accounting Period: Open → Closing → Closed; post-close correction có lifecycle
  và reversal, không sửa trực tiếp số liệu đã khóa.
- Cashflow/P&L phân biệt committed, accrued/accepted, paid/received và forecast;
  không gọi báo cáo là real-time nếu nguồn chưa đủ.

## 9. Module 7 — Tài liệu và Google Drive

### Cây nghiệp vụ mục tiêu

```text
[ProjectCode]_[ProjectName]
├─ 01_CRM_PreDesign/01_Khao_sat
├─ 02_Thiet_ke/{01_So_bo_Concept,02_Co_so,03_Chi_tiet_ShopDrawing}
├─ 03_Xin_phep_Phap_ly
├─ 04_Thi_cong_Nghiem_thu
├─ 05_Cung_ung_Vat_tu
└─ 06_Tai_chinh_Hop_dong
```

Hiện tại code cấu hình `01_Khao_sat` và `01_CRM_PreDesign` là hai category sibling
riêng. Phải giữ tương thích cho tới khi có migration topology được duyệt; tài
liệu không được mô tả hai cách này như cùng một trạng thái đã triển khai.

Nicon quản lý metadata, quyền, nguồn, checksum, version, desired operation,
sync status, retry và conflict. Drive giữ binary. File từ Drive đưa vào phải
được phân loại; file xóa qua Nicon đi theo chính sách trash/cleanup đã công bố.

## 10. Module 8 — Dashboard và KPI

### 10.1. Mục tiêu Dashboard và KPI

Tính KPI từ dữ liệu vận hành, có bằng chứng drill-down, kỳ thời gian Việt Nam và
snapshot khi khóa.

### Khung mục tiêu 11 vị trí

| Vị trí | Nhóm chỉ số mục tiêu |
|---|---|
| PM Phòng CM | Budget Baseline, S-Curve, thời gian đóng Punchlist |
| Chỉ huy trưởng BCH | Tiến độ, hao hụt vật tư, HSE |
| Field/QA-QC | Nghiệm thu lần đầu, đóng lỗi, hồ sơ chất lượng |
| Site QS | QS Upstream, QS Downstream, VO |
| Cung ứng | Tối ưu giá, thời gian cung ứng, Vendor Rating |
| Sales/CRM | Chuyển đổi, doanh thu ký mới, phản hồi Lead |
| Tendering/QS văn phòng | Tỷ lệ trúng, đúng hạn, độ chính xác dự toán |
| Design | Đúng hạn, duyệt lần đầu, lỗi thiết kế từ công trường |
| Legal | Permit đúng hạn, hồ sơ bổ sung, hết hạn/vi phạm |
| Project Accounting | Thu đúng hạn, xử lý phải trả, correction |
| HR/Admin | Tuyển dụng, hồ sơ/quy trình nhân sự theo phạm vi được duyệt |

Mã nguồn hiện mới có 19 metric cho sáu scorecard: Sales, Tendering, Design,
Site, Procurement và Project Accounting. Không được tuyên bố 11 vị trí đã hoàn
thành. Metric thiếu nguồn/target trả `MissingData`; tổng trọng số active phải
bằng 100% trước khi khóa kỳ.

## 11. Ngôn ngữ, thiết bị và phi chức năng

- Giao diện hiện hỗ trợ `vi`, `en`, `zh`, `ja`; mọi display text mới phải có đủ
  bốn ngôn ngữ. Đây là phạm vi triển khai mở rộng so với BreakTask gốc Việt/Anh.
- BCH ưu tiên mobile/tablet và kết nối yếu; CM/Finance/Procurement ưu tiên desktop
  cho đối soát. Offline queue chỉ được công bố khi có persistence, retry,
  idempotency và conflict handling thực tế.
- Dữ liệu tài chính, file và hành động nhạy cảm phải có authorization, audit và
  không lộ secret/token trong response/log.
- Export phải dùng cùng scope/filter/quyền với màn hình nguồn.

## 12. Các quyết định bắt buộc còn mở

Danh sách Q-01–Q-11 và các phụ thuộc chi tiết nằm trong MoM ngày 03–06/10/2026.
Ngoài ra, NICON cần chốt:

1. Cổng khởi công và checklist ISO theo loại công trình.
2. Công thức/kỳ chốt S-Curve và quyền publish/unlock baseline.
3. Approval/delegation matrix cho MR, QA/QC, HSE, QS và VO.
4. Hồ sơ thanh toán và three-way matching theo loại Downstream package.
5. Công thức dải BOQ 85/95/>100 và xử lý zero allowance/reversal/conversion.
6. Migration `DesignProjectId` → `OperationalProjectId` cho Module 2/3/4.
7. Migration cây Drive cho `01_Khao_sat`.
8. KPI definition, target và source cho các vị trí còn thiếu.

Không khóa thiết kế dữ liệu hoặc tuyên bố business-ready cho phần phụ thuộc cho
tới khi chủ sở hữu nghiệp vụ chấp thuận các quyết định tương ứng.
