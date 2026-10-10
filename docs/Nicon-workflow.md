# Quy trình vận hành NICON

Tài liệu này mô tả quy trình nghiệp vụ mục tiêu của NICON và ranh giới với phần
mềm hiện có. Nó được tổng hợp từ yêu cầu tám module, tài liệu họp ngày
03–06/10/2026, bộ tiến độ mẫu, BOQ mẫu và mã nguồn tại thời điểm cập nhật.

## 1. Cách đọc trạng thái

- **Hiện có:** đã có model/API/UI tương ứng trong mã nguồn. Chức năng chỉ được
  coi là sẵn sàng bàn giao khi môi trường triển khai và kiểm thử liên quan cũng
  đạt yêu cầu.
- **Một phần:** đã có lõi nhưng còn thiếu một phần của quy trình NICON.
- **Mục tiêu:** yêu cầu nghiệp vụ đã ghi nhận nhưng chưa có đủ bằng chứng triển
  khai end-to-end.
- **Cần chốt:** còn thiếu quyết định của NICON; không được tự biến thành quy tắc
  phần mềm.

`Dự án` trong tài liệu này mặc định là **Operational Project** dùng chung. Các
`DesignProjectId` còn tồn tại trong Thiết kế, Pháp lý và một phần Thi công là
ranh giới tương thích của hệ thống hiện tại, không phải mô hình đích.

## 2. Trục dữ liệu xuyên suốt

```text
Khách hàng
  └─ Lead / Cơ hội
       └─ Operational Project (Dự án dùng chung)
            ├─ Báo giá / hồ sơ thầu
            ├─ Hợp đồng Upstream với CĐT/khách hàng
            ├─ Dự án Thiết kế và hồ sơ Pháp lý
            ├─ Thi công, QA/QC, HSE, QS và nghiệm thu
            ├─ BOQ thực hiện, RFQ, Hợp đồng Downstream và kho
            ├─ Thu/chi, hồ sơ thanh toán và P&L
            └─ Tài liệu, audit, dashboard và KPI
```

Các module dùng chung ID Dự án, không tạo lại dự án theo từng phòng ban. Team,
giao việc, hợp đồng, BOQ, tài liệu và bằng chứng KPI phải truy vết về cùng Dự án.

## 3. Các đường vào dự án

NICON có nhiều đường vào hợp lệ; không bắt buộc mọi dự án đi qua cùng một chuỗi:

| Luồng | Trình tự chính | Điều kiện quan trọng |
|---|---|---|
| Design & Build | Lead → Cơ hội + Dự án → khảo sát/thiết kế/giá đầu vào → báo giá nếu cần → HĐ D&B → thiết kế/pháp lý/thi công | Khách có thể đã thống nhất giá trị không qua báo giá; báo giá được dùng phải đã duyệt; thi công chỉ dùng IFC đã phát hành |
| Thiết kế trước hợp đồng | Cơ hội + Dự án → Concept/Basic/Detail → BOQ/báo giá → HĐ thiết kế hoặc D&B/thi công | Thiết kế được bắt đầu khi chưa có hợp đồng nhưng luôn thuộc Dự án |
| Đấu thầu | Dự án → gói thầu → kế hoạch/checklist/dự toán thầu → nộp → kết quả → thương thảo/HĐ | Kết quả, deadline và phiên bản dự toán phải có lịch sử |
| Tư vấn/báo giá sơ bộ | Dự án → khảo sát/Concept → suất đầu tư hoặc BOQ sơ bộ → báo giá → chuyển đổi nếu thành công | Không dùng BOQ sơ bộ làm hạn mức cấp phát thi công |

Lead sau chuyển đổi bị khóa theo hợp đồng hiện tại; Customer, Opportunity và
Operational Project được tạo/liên kết có kiểm soát. Việc hủy chuyển đổi chỉ được
thực hiện theo quyền và điều kiện an toàn dữ liệu hiện có.

## 4. Các cổng kiểm soát chính

### G0 — Khởi tạo Dự án

- Có khách hàng, mã/tên dự án và người phụ trách hợp lệ.
- Cơ hội, báo giá, hợp đồng và các module sau phải liên kết lại Dự án này.
- Khi tạo Dự án, hệ thống đề xuất mã tự động và người tạo được chỉnh mã trước
  khi lưu. Sau khi Dự án được tạo, mã không được thay đổi để giữ ổn định việc
  đối soát, tên thư mục và tài liệu lịch sử. Mã phải duy nhất toàn hệ thống;
  quy ước chi tiết vẫn theo quyết định Q-05 của NICON.

### G1 — Chuyển sang hợp đồng

- Hợp đồng Upstream được lập trực tiếp từ Cơ hội khi khách đã thống nhất giá trị.
  Nếu gắn báo giá CRM, báo giá phải được duyệt, còn hiệu lực khi gắn, đúng
  khách hàng/Dự án và đúng Cơ hội khi Hợp đồng gắn Cơ hội. Hợp đồng Downstream
  không dùng báo giá CRM đầu ra.
- Hợp đồng xác định rõ hướng `Upstream` hoặc `Downstream` và loại Design,
  Construction, Design & Build, Supply hoặc Subcontract.
- Một Dự án có thể có nhiều hợp đồng; không gộp chúng thành một bản ghi.

### G2 — Phát hành hồ sơ thi công

- Concept được chốt trước khi mở giai đoạn tiếp theo.
- Basic Design đủ điều kiện nội bộ/pháp lý theo loại công trình.
- Shop/Detailed Drawing được duyệt, đưa vào gói IFC và phát hành phiên bản cụ
  thể. Chỉ revision IFC đã phát hành mới được dùng làm chỉ dẫn thi công.

### G3 — Pháp lý

- Checklist giấy phép được tạo theo loại/quy mô dự án, có chủ trì, hạn xử lý,
  trạng thái, hồ sơ nộp và hồ sơ được cấp.
- Giấy phép có ngày hết hạn phải còn hiệu lực tại thời điểm sử dụng.
- Danh mục giấy phép chuẩn theo loại công trình vẫn là điểm NICON cần chốt.

### G4 — Đủ điều kiện khởi công

Hợp đồng Upstream D&B/thi công đã ký chỉ làm cho Dự án **đủ điều kiện tiếp nhận
vào Phòng CM**; nó không tự động cho phép khởi công. Cổng khởi công phải kiểm tra
trên server, lưu người xác nhận và snapshot bằng chứng:

1. Hợp đồng Upstream D&B/thi công còn hiệu lực.
2. Phạm vi IFC cần cho mốc khởi công đã phát hành đúng revision.
3. Giấy phép bắt buộc cho mốc đó đã được cấp và còn hiệu lực.
4. WBS/Gantt, mốc, đường găng và Baseline S-Curve đã công bố.
5. BCH, người chịu trách nhiệm và kế hoạch nguồn lực đã được phê duyệt.
6. Budget Baseline đã khóa; gói Downstream thiết yếu đã sẵn sàng theo kế hoạch.
7. Các điều kiện theo dự án như bàn giao mặt bằng, biện pháp thi công và kế
   hoạch HSE được đánh dấu bắt buộc khi NICON/template dự án yêu cầu.

Nếu thiếu một điều kiện bắt buộc, trạng thái chỉ là `CM Preparation`, không phải
`Ready to Start` hoặc `In Construction`.

### G5 — Nghiệm thu, hoàn công và bàn giao

- Có nghiệm thu được duyệt, hồ sơ hoàn công đủ category bắt buộc, commissioning
  hoàn tất và không còn Punchlist chặn.
- Bàn giao cần checklist hoàn tất, ít nhất một bên ký và quyền hoàn tất riêng.
- Hồ sơ thanh toán và nghĩa vụ hợp đồng vẫn theo Module 6; bàn giao kỹ thuật
  không tự động đồng nghĩa đã thu/chi xong.

## 5. Module 4 — Thi công và nghiệm thu

### 5.1. Điểm bắt đầu và Giai đoạn 1 — Phòng CM chuẩn bị

```text
HĐ Upstream D&B/thi công đã ký
  ↓
Operational Project hiện có được chuyển vào hàng đợi CM Preparation
  ↓
PM Phòng CM lập và trình:
  ├─ WBS, Gantt, mốc, đường găng, Baseline S-Curve
  ├─ Sơ đồ BCH: Chỉ huy trưởng, Field/QA-QC, Site QS, HSE, Thủ kho
  ├─ Kế hoạch vật tư, thiết bị/máy, nhân công theo tiến độ
  ├─ Bid Tabulation và kế hoạch HĐ Downstream cùng Module 5/6
  └─ Budget Baseline: vật tư, nhân công, máy, chi phí BCH
  ↓
Cổng G4 kiểm tra điều kiện khởi công
```

Baseline đã công bố không bị ghi đè. Mỗi lần điều chỉnh tạo version, lý do,
người duyệt và lịch sử; thay đổi phạm vi/giá trị hợp đồng phải liên kết VO phù
hợp. Công thức S-Curve, kỳ chốt và thẩm quyền mở khóa vẫn cần NICON xác nhận.

### 5.2. Giai đoạn 2 — BCH thực hiện, Phòng CM kiểm soát chéo

| Luồng | BCH tại hiện trường | Phòng CM tại văn phòng | Đầu ra/liên kết |
|---|---|---|---|
| Nguồn lực | Gửi đề xuất vật tư, thiết bị/máy hoặc nhân công theo WBS và ngày cần | Duyệt tầng 2 khi ngoài kế hoạch/vượt hạn mức | Vật tư chuyển MR Module 5; loại khác chuyển đúng quy trình mua/thuê/nhân lực |
| Nhật ký | Ghi thời tiết, tổ đội, nhân sự, thiết bị, vật tư, công việc, sự cố và ảnh | Kiểm tra thiếu dữ liệu và đối soát với tiến độ | Bằng chứng ngày; không tự coi Draft/offline là số liệu KPI |
| QA/QC | Lập biên bản lấy mẫu, thí nghiệm, chứng chỉ, checklist kiểm tra theo IFC | Thẩm định hồ sơ và kiểm tra đột xuất | Metadata/audit ở Nicon, file đồng bộ `04_Thi_cong_Nghiem_thu` |
| Punchlist | Ghi lỗi, vị trí, ảnh, nguyên nhân, người xử lý và hạn | Giám sát lỗi quá hạn, xác nhận đóng/mở lại | Lỗi chặn tham gia điều kiện nghiệm thu/bàn giao |
| HSE | Báo cáo vi phạm và bằng chứng, thực hiện khắc phục | Xác nhận độc lập, theo dõi hạn và đóng vi phạm | Sự kiện HSE xác nhận mới là bằng chứng KPI |
| Tiến độ | Cập nhật tiến độ và bằng chứng thực tế | So với Baseline S-Curve | Trễ **lớn hơn 5%** tạo cảnh báo đỏ và yêu cầu giải trình/kế hoạch phục hồi |
| QS Upstream | Đo và lập đợt nghiệm thu với CĐT/TVGS | Duyệt tầng 2 | Liên kết HĐ Upstream, WBS, BOQ/VO và mốc phải thu |
| QS Downstream | Đo khối lượng tổ đội/thầu phụ | Duyệt tầng 2 | Liên kết HĐ Downstream và hồ sơ phải trả |

Người tạo MR, hồ sơ QA/QC, QS, HSE hoặc VO không được đồng thời thực hiện bước
thẩm định độc lập của Phòng CM trên cùng bản ghi. Quy tắc này phải được kiểm tra
tại API, không chỉ ẩn nút trên giao diện.

### 5.3. Đề xuất nguồn lực

```text
BCH tạo đề xuất có loại + WBS + ngày cần + khối lượng + lý do
  ↓
Server kiểm tra Dự án, quyền, baseline và hạn mức
  ├─ Trong kế hoạch/hạn mức → luồng duyệt thông thường
  └─ Ngoài kế hoạch/vượt hạn mức → CM review độc lập
       ├─ Từ chối → không tạo commitment hoặc chứng từ downstream
       └─ Duyệt → chuyển Module 5/đơn vị liên quan bằng stable ID
```

Hiện tại phần mềm đã có MR vật tư và chặn khi vượt allowance còn lại. Đề xuất
máy/thiết bị và nhân công, CM level-2 review và ba portal BCH là **mục tiêu**.
Các dải 85% vàng, 95% cam và trên 100% đỏ/chặn chưa thay thế validation hiện có
cho đến khi chốt mẫu số, quy đổi đơn vị, làm tròn, reversal và BOQ bằng 0.

### 5.4. Nghiệm thu và thanh toán

```text
Upstream QS được duyệt
  → cung cấp bằng chứng cho mốc phải thu Module 6

Downstream QS được duyệt
  + HĐ Downstream đã ký/phụ lục hợp lệ
  + Phiếu nhập kho Posted, trừ reversal (khi gói có vật tư)
  → đủ điều kiện để Kế toán review Payment Request
```

`Đủ điều kiện review` không phải tự động thanh toán. Supply-only, labour-only,
equipment và mixed subcontract cần ma trận chứng từ riêng; không tạo chứng từ
giả để đủ three-way matching.

### 5.5. Hiện trạng Module 4 trong mã nguồn

| Năng lực | Trạng thái thực tế | Khóa dữ liệu hiện tại |
|---|---|---|
| Construction task/WBS, dependency, progress | Hiện có; chưa có baseline snapshot/S-Curve/resource loading/critical-path engine | `DesignProjectId` |
| Site diary | Hiện có Draft → Submitted → Confirmed; chưa có media/offline queue đầy đủ | `DesignProjectId` |
| Punchlist | Hiện có Open → InProgress → Fixed → Verified, root cause/KPI attribution | `DesignProjectId` |
| HSE violation | Hiện có lifecycle, correction, concurrency và evidence paths | `OperationalProjectId` |
| Partial acceptance | Hiện có workflow chung; chưa phân hướng Upstream/Downstream QS | `DesignProjectId` |
| As-built | Hiện có category, workflow và completeness | `DesignProjectId` |
| Handover | Hiện có readiness, commissioning, checklist, signatory và row version | `DesignProjectId` |
| CM preparation/BCH organization/Budget Baseline | Chưa có aggregate hoàn chỉnh | Mục tiêu `OperationalProjectId` |
| QA/QC dossier và Site QS hai hướng | Chưa có aggregate hoàn chỉnh | Mục tiêu `OperationalProjectId` + WBS/contract/BOQ |

Không mở rộng các aggregate mới dựa trên `DesignProjectId`. Phần hiện có cần
resolve an toàn qua Design Project sang Operational Project và được migration
dần; không được phá API/dữ liệu cũ trong một lần chuyển đổi không có rehearsal.

## 6. Phân loại BOQ và nguồn chuẩn

| Loại | Mục đích | Chủ trì | Có dùng làm hạn mức MR/kho? |
|---|---|---|---|
| BOQ sơ bộ | Báo giá sớm/suất đầu tư | Sales/Tendering | Không |
| Tender Estimate/BOQ dự thầu | Lập và so sánh giá dự thầu | Tendering/QS văn phòng | Không trực tiếp |
| Design quantity takeoff | Khối lượng kỹ thuật từ hồ sơ thiết kế | Design/QS | Chỉ là nguồn đối chiếu |
| Execution BOQ revision | Hạn mức thực hiện đã duyệt sau hợp đồng | PM CM + Procurement/QS theo quyền | Có; revision `Approved` hiện hành |
| Final BOQ | Phiên bản thực hiện cuối khi Dự án hoàn tất, gồm VO đã duyệt | PM CM/Finance | Dùng quyết toán, báo cáo và KPI |

Giá đầu vào, material-rate catalog, Quote BOQ và Execution BOQ là các aggregate
khác nhau. Việc import phải có preview, mapping, lỗi theo sheet/ô, checksum hoặc
provenance, người xác nhận và không hard-code tên sheet từ file mẫu.

## 7. Google Drive và tài liệu

### 7.1. Cây mục tiêu NICON

```text
[ProjectCode]_[ProjectName]
├─ 01_CRM_PreDesign
│  └─ 01_Khao_sat
├─ 02_Thiet_ke
│  ├─ 01_So_bo_Concept
│  ├─ 02_Co_so
│  └─ 03_Chi_tiet_ShopDrawing
├─ 03_Xin_phep_Phap_ly
├─ 04_Thi_cong_Nghiem_thu
├─ 05_Cung_ung_Vat_tu
└─ 06_Tai_chinh_Hop_dong
```

### 7.2. Tương thích hiện tại

Mã nguồn hiện lưu `01_Khao_sat` và `01_CRM_PreDesign` thành hai category sibling
cấu hình độc lập. Đây là hiện trạng phải giữ khi nâng cấp để không làm mất liên
kết. Việc đưa khảo sát vào dưới `01_CRM_PreDesign` cần migration topology riêng,
kiểm kê folder/file, xử lý xung đột và khả năng rollback; không đổi tên âm thầm.

Drive chỉ là nơi lưu binary. Nicon giữ metadata, nguồn, quyền, version, checksum,
trạng thái đồng bộ, conflict và audit. Trạng thái `Synced` chỉ được đặt sau khi
external write thành công; lỗi phải retry được và không tạo thành công giả.

## 8. Dashboard và KPI

Khung mục tiêu có 11 vị trí NICON. Mã nguồn hiện có 19 định nghĩa cho sáu nhóm
scorecard: Sales, Tendering, Design, Site, Procurement và Project Accounting.
Các vị trí mục tiêu PM CM, Chỉ huy trưởng, Field/QA-QC, Site QS, Legal và HR chưa
đều có scorecard độc lập.

KPI chỉ tính từ sự kiện nghiệp vụ terminal có project, người chịu trách nhiệm,
timestamp, status và version. Không có mẫu số/bằng chứng thì trả `MissingData`,
không tự đổi thành 0. Kỳ đã khóa là snapshot bất biến.

## 9. Quyết định còn cần NICON chốt

1. Q-01–Q-03: công việc trước/sau hợp đồng, duyệt công việc nội bộ và quyền giao
   việc liên phòng ban.
2. Q-04–Q-05: cơ cấu Cung ứng/Pháp lý và quy ước mã dự án.
3. Q-06–Q-10: trình nội bộ, CC mặc định, quyền xóa và phân vai Design
   Lead/Design Manager.
4. Q-09/Q-11: template giấy phép và cách hiển thị team/tiến độ Thiết kế.
5. Công thức/kỳ chốt S-Curve; quyền publish/unlock baseline; điều kiện VO.
6. Checklist QA/QC, hồ sơ ISO và cổng khởi công theo từng loại công trình.
7. Thành phần hồ sơ thanh toán Upstream/Downstream và ma trận three-way matching
   cho supply, labour, equipment và mixed package.
8. Quy tắc dải BOQ 85/95/>100: mẫu số, commitment, issue, reversal, conversion,
   precision và zero allowance.
9. Kế hoạch migration các record Module 2/3/4 từ `DesignProjectId` sang
   `OperationalProjectId`, gồm compatibility API và rollback.
10. Cây Drive đích và migration `01_Khao_sat` vào `01_CRM_PreDesign`.

Các điểm trên là blocker cho phần kiến trúc/dữ liệu tương ứng. Chỉ Product Owner
hoặc đại diện nghiệp vụ NICON được chấp nhận rủi ro yêu cầu.
