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

### 2.1. Nhóm công việc của phòng ban — Hiện có

```text
Kinh doanh phụ trách Cơ hội / PM Dự án
  → chọn Dự án dùng chung và phòng nhận
  → chọn người chủ trì phòng
  → người chủ trì chia đầu việc cho nhân sự cùng phòng
  → người phụ trách cập nhật tiến độ
  → người xác nhận độc lập xác nhận đầu việc có liên kết KPI
```

Nhóm theo Dự án không được tạo một Dự án khác cho phòng nhận. Hợp đồng là liên
kết tùy chọn của nhóm và có thể bổ sung sau; nhóm vẫn thuộc Operational Project.
Các trạng thái nhóm là `Planned → Active → Completed`, hoặc `Cancelled` theo
chuyển trạng thái hợp lệ. Nhóm chỉ hoàn thành khi mọi đầu việc đã kết thúc và
mọi đầu việc KPI đã được xác nhận. Quan hệ tiên quyết không được tạo vòng lặp;
đầu việc sau không được bắt đầu trước khi đầu việc trước hoàn tất.

Công việc không theo hợp đồng dùng tab **Công việc nội bộ**, không mang project,
customer hoặc contract. Khi không chọn KPI, hoàn thành không cần bước xác nhận.
Khi chọn KPI, người xác nhận phải khác người thực hiện và sự kiện `Verified` mới
là bằng chứng chấm KPI.

### 2.2. Liên phòng ban trước hợp đồng — Hiện có

Ba luồng dưới đây dùng lại nhóm công việc dự án, không có tab riêng. Nhóm việc
tiền hợp đồng không được gắn `ContractId`.

#### 2.2.1. Cung ứng → Kinh doanh: báo giá đầu vào

```text
Kinh doanh (người phụ trách Cơ hội) hoặc PM Dự án
  → giao nhóm việc "Giá đầu vào" cho phòng Cung ứng, gắn Dự án + Cơ hội
  → Cung ứng nhập chào giá NCC/thầu phụ (Draft)
  → Cung ứng gửi duyệt (Submitted)
  → người duyệt khác người lập: Approved hoặc Rejected kèm lý do
  → Kinh doanh chọn giá Approved còn hiệu lực khi lập báo giá Boq đầu ra
```

Chỉ sửa được chào giá ở `Draft`/`Rejected`; chào giá đã bị báo giá tham chiếu thì
khoá lại. Báo giá lưu bản chụp giá đầu vào tại thời điểm gắn. Giá hết hiệu lực,
chưa duyệt, khác Dự án hoặc khác nhóm việc Cung ứng đều bị từ chối.

Kiểm soát **BOQ đã ký** của Cung ứng là bước **sau** hợp đồng và tách biệt: chỉ
BOQ thực hiện gắn hợp đồng đầu ra đã ký, còn hiệu lực, đúng Dự án mới được lập.

#### 2.2.2. Tài chính – Kế toán: rà soát sau báo giá và trước khi nộp thầu

```text
Kinh doanh gửi báo giá (đã duyệt nội bộ)
  → Tài chính lập hồ sơ rà soát điều kiện hợp đồng / bảo lãnh / điều khoản thanh toán
  → Tài chính gửi duyệt, người duyệt độc lập phê duyệt
  → Kinh doanh gửi báo giá cho khách hàng

Kinh doanh chuẩn bị hồ sơ dự thầu (gói thầu đã gắn Dự án vận hành)
  → lập Kế hoạch đấu thầu (`TenderPlanning`) theo từng phòng: người phụ trách, người duyệt độc lập,
    ngày bắt đầu/kết thúc, dependency và tiến độ
  → từng phòng hoàn thành đầu việc; người duyệt xác nhận
  → Tài chính lập hồ sơ rà soát tài chính dự thầu / bảo lãnh
  → Tài chính gửi duyệt, người duyệt độc lập phê duyệt
  → Kinh doanh nộp thầu (Submitted)
  → ghi nhận trúng/trượt; nếu trúng, mở Cơ hội Negotiation trên đúng Dự án
  → sau thương thảo, người dùng chủ động tạo Hợp đồng Upstream
```

Hai hồ sơ dùng chung checklist và chung nguyên tắc "người lập không tự duyệt".
Hồ sơ đã gửi duyệt hoặc đã duyệt thì không sửa được nữa. Hồ sơ thương mại gắn
với phiên bản báo giá đã rà soát: báo giá thay đổi giá trị làm tăng phiên bản và
quay lại `Draft`, nên Tài chính phải rà soát lại trước khi gửi khách hàng.

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

- Báo giá đi trước hợp đồng chỉ khi lô thương mại đó dùng báo giá; nếu khách đã
  thống nhất giá trị trực tiếp thì chỉ cần hợp đồng. Kinh doanh quyết định theo
  từng lô (NICON xác nhận ngày 2026-10-11).
- Trước khi gửi báo giá cho khách, Tài chính – Kế toán phải duyệt hồ sơ rà soát
  điều kiện hợp đồng, bảo lãnh và điều kiện thanh toán. Báo giá chưa có hồ sơ
  duyệt thì không gửi được; báo giá cũng phải gắn Dự án vận hành để có chỗ ghi
  rà soát (xem 2.2).
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
Dòng tiêu đề nhóm, tiêu đề cột lặp và tổng phụ không được tính thành dòng đơn
giá; dòng chi tiết phải có tên, đơn vị, khối lượng dương và đơn giá không âm.

Workbook BOQ khách hàng được nhập tại Draft revision của material-rate catalog.
Quote BOQ chỉ áp dụng revision Approved phù hợp và lưu snapshot để lập báo giá;
không có luồng import workbook trực tiếp vào Quote. Bảng nhập nhiều dòng hoặc
thêm từng dòng tại Quote chỉ bổ sung ngoại lệ cục bộ, không thay đổi catalog và
không thay thế quy trình chuẩn hoá workbook. Không dùng vùng nhập text/clipboard
tự do cho BOQ của Quote.

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

KPI nghiệp vụ theo Dự án chỉ tính từ sự kiện terminal có project, người chịu
trách nhiệm, timestamp, status và version. Riêng `VerifiedWorkTaskCount` được
phép lấy đầu việc nội bộ không có project khi đầu việc có người chịu trách
nhiệm, trạng thái `Verified`, người xác nhận độc lập, timestamp và version.
Không có mẫu số/bằng chứng thì trả `MissingData`, không tự đổi thành 0. Kỳ đã
khóa là snapshot bất biến.

## 9. Quyết định còn cần NICON chốt

1. Q-05: quy ước mã dự án chi tiết. Cơ cấu Cung ứng/Pháp lý đã được đối chiếu
   với sơ đồ tổ chức NICON cập nhật ngày 11/10/2026.
2. Q-06–Q-10: trình nội bộ, CC mặc định, quyền xóa và phân vai Design
   Lead/Design Manager.
3. Q-09/Q-11: template giấy phép và cách hiển thị team/tiến độ Thiết kế.
4. Công thức/kỳ chốt S-Curve; quyền publish/unlock baseline; điều kiện VO.
5. Checklist QA/QC, hồ sơ ISO và cổng khởi công theo từng loại công trình.
6. Thành phần hồ sơ thanh toán Upstream/Downstream và ma trận three-way matching
   cho supply, labour, equipment và mixed package.
7. Quy tắc dải BOQ 85/95/>100: mẫu số, commitment, issue, reversal, conversion,
   precision và zero allowance.
8. Kế hoạch migration các record Module 2/3/4 từ `DesignProjectId` sang
   `OperationalProjectId`, gồm compatibility API và rollback.
9. Cây Drive đích và migration `01_Khao_sat` vào `01_CRM_PreDesign`.

Các điểm trên là blocker cho phần kiến trúc/dữ liệu tương ứng. Chỉ Product Owner
hoặc đại diện nghiệp vụ NICON được chấp nhận rủi ro yêu cầu.
