# Quy trình vận hành NICON

Tài liệu này mô tả luồng nghiệp vụ mục tiêu xuyên suốt tám module. Dự án là bản
ghi dùng chung; các phòng ban làm việc trên cùng dự án hoặc hợp đồng, không tạo
lại dự án riêng cho từng module.

## 1. Luồng tổng thể

```text
Khách hàng
  ↓
Lead → Cơ hội → Dự án dùng chung
  │                 │
  │                 ├─ Thiết kế trước hợp đồng (Module 2)
  │                 ├─ Pháp lý theo nhu cầu dự án (Module 3)
  │                 ├─ Giá đầu vào / BOQ / đấu thầu (Module 5)
  │                 └─ Rà soát thương mại - tài chính (Module 6)
  │
  └─ Báo giá đã duyệt → Hợp đồng Upstream đã ký (Module 6)
                              ↓
             Thiết kế / Pháp lý / Cung ứng / Thi công
                              ↓
             Nghiệm thu → Thanh toán → Hoàn công → Bàn giao
```

### Nguyên tắc liên kết

- Một khách hàng có nhiều dự án; một dự án có thể có nhiều hợp đồng.
- Thiết kế có thể bắt đầu từ Cơ hội hoặc Dự án trước khi có hợp đồng.
- Hợp đồng chỉ được lập từ báo giá đã duyệt.
- Hợp đồng D&B/Thi công Upstream đã ký là điểm kích hoạt Phòng CM tiếp nhận và
  chuẩn bị thi công.
- Module 7 (Google Drive) và Module 8 (Dashboard/KPI) là lớp xuyên suốt, không
  phải bước tuần tự nằm giữa các module nghiệp vụ.

## 2. Luồng trước hợp đồng

```text
MODULE 1 - CRM & PRE-DESIGN
  Lead
    → Khách hàng + Cơ hội + Dự án
    → Khảo sát / yêu cầu khách hàng
    → Chọn phương thức chào giá
       ├─ Chào giá trực tiếp
       └─ Đấu thầu: kế hoạch → phân công hồ sơ → nộp → kết quả

DỰ ÁN DÙNG CHUNG
  ├─ Module 2: Concept / Basic / Detailed Design
  ├─ Module 3: hồ sơ pháp lý cần triển khai sớm
  ├─ Module 5: giá đầu vào, BOQ, Bid Tabulation
  └─ Module 6: điều kiện hợp đồng, bảo lãnh, rủi ro tài chính
         ↓
  Báo giá đầu ra được duyệt
         ↓
  Thương thảo và ký Hợp đồng Upstream
```

## 3. Module 4 - Quản lý thi công và nghiệm thu

### 3.1. Điểm bắt đầu

```text
Hợp đồng D&B/Thi công Upstream đã ký (Module 6)
  ↓
Chuyển giao dự án hiện có cho Phòng Quản lý Thi công (CM)
  ↓
Giai đoạn 1 - Tiếp nhận dự án và chuẩn bị thi công
```

Việc chuyển giao không tạo dự án mới. Phòng CM tiếp tục sử dụng Dự án dùng
chung, hợp đồng, BOQ, hồ sơ thiết kế và hồ sơ pháp lý đã có.

### 3.2. Giai đoạn 1 - Phòng CM chuẩn bị thi công

```text
PM PHÒNG CM
  ├─ 1. Kế hoạch thi công tổng thể
  │    ├─ WBS
  │    ├─ Gantt Chart
  │    ├─ Baseline S-Curve
  │    ├─ Mốc móng / kết cấu / hoàn thiện / MEP
  │    └─ Đường găng
  │
  ├─ 2. Tổ chức Ban Chỉ huy Công trình (BCH)
  │    ├─ Chỉ huy trưởng
  │    ├─ Kỹ sư hiện trường / QA-QC
  │    ├─ Kỹ sư QS công trường
  │    ├─ Cán bộ HSE
  │    └─ Thủ kho
  │
  ├─ 3. Kế hoạch nguồn lực theo tiến độ
  │    ├─ Vật tư
  │    ├─ Thiết bị / máy thi công
  │    └─ Nhân công trực tiếp
  │
  ├─ 4. Phối hợp Module 5 và Module 6
  │    ├─ Bid Tabulation
  │    ├─ Đánh giá thầu phụ / tổ đội / nhà cung cấp
  │    └─ Ký Hợp đồng Downstream
  │
  └─ 5. Lập và khóa Budget Baseline
       ├─ Vật tư
       ├─ Nhân công
       ├─ Máy thi công
       └─ Chi phí quản lý BCH
            ↓
      Sẵn sàng khởi công
```

Budget Baseline là căn cứ kiểm soát P&L tại Module 6 và KPI tại Module 8. Thay
đổi sau khi khóa phải có thẩm quyền, lịch sử và VO khi làm thay đổi phạm vi hoặc
giá trị hợp đồng.

### 3.3. Giai đoạn 2 - Thi công và kiểm soát chéo

Khi công trình khởi công, BCH và Phòng CM làm việc song song trên cùng dữ liệu.
BCH tạo bằng chứng vận hành tại hiện trường; Phòng CM giám sát và thẩm định từ
văn phòng.

| Luồng | BCH tại hiện trường | Phòng CM tại văn phòng | Đầu ra/liên kết |
|---|---|---|---|
| Nguồn lực | Lập phiếu đề xuất vật tư, máy/thiết bị hoặc nhân công trên Mobile App; đối chiếu BOQ | Duyệt tầng 2 khi vượt định mức hoặc phát sinh | Đề xuất hợp lệ chuyển Module 5; chi phí liên kết Module 6 |
| QA/QC | Lập hồ sơ lấy mẫu, thí nghiệm, chứng chỉ vật liệu; giám sát theo bản vẽ IFC | Thẩm định hồ sơ, kiểm tra đột xuất | Hồ sơ đồng bộ `04_Thi_cong_Nghiem_thu` tại Module 7 |
| Punchlist | Ghi nhận lỗi bằng ảnh, vị trí, người xử lý và hạn khắc phục; theo dõi đến khi đóng | Theo dõi và xử lý các lỗi quá hạn | Trạng thái lỗi là điều kiện nghiệm thu/hoàn công/bàn giao |
| Nhật ký | Báo cáo hằng ngày về thời tiết, nhân công, thiết bị, công việc, sự cố và hình ảnh | Đối soát tiến độ thực tế với Baseline S-Curve | Dữ liệu tiến độ và KPI Module 8 |
| Tiến độ | Cập nhật khối lượng/tiến độ thực tế có bằng chứng | Cảnh báo đỏ cho Chỉ huy trưởng khi độ trễ `> 5%`; yêu cầu giải trình và kế hoạch điều chỉnh | Cảnh báo, phương án phục hồi và lịch sử xử lý |
| HSE | Ghi nhật ký an toàn và biên bản vi phạm | Giám sát BHLĐ, thẩm định và xử lý vi phạm | Hồ sơ HSE và dữ liệu KPI |
| QS Upstream | Đo đạc, lập biên bản nghiệm thu khối lượng theo đợt với CĐT/TVGS | Rà soát và duyệt tầng 2 | Kích hoạt căn cứ thu tiền theo Hợp đồng Upstream tại Module 6 |
| QS Downstream | Đo đạc khối lượng tổ đội/thầu phụ và lập đề nghị thanh toán | Rà soát và duyệt tầng 2 | Chuyển hồ sơ thanh toán Hợp đồng Downstream cho Module 6 |
| VO | Ghi nhận nhu cầu phát sinh và bằng chứng hiện trường | Thẩm định, lập hồ sơ phụ lục trình CĐT | VO được duyệt cập nhật căn cứ ngân sách/hợp đồng theo phiên bản |

### 3.4. Luồng đề xuất nguồn lực

```text
BCH lập đề xuất trên Mobile App
  ↓
Phân loại: Vật tư | Thiết bị/Máy thi công | Nhân công
  ↓
Đối chiếu BOQ / kế hoạch nguồn lực / Budget Baseline
  ├─ Trong hạn mức → đi theo luồng duyệt chuẩn
  └─ Vượt hạn mức hoặc phát sinh
       → BCH giải trình
       → PM Phòng CM duyệt tầng 2
       → nếu thay đổi phạm vi/giá trị: gắn VO được duyệt
  ↓
Module 5 thực hiện mua sắm/cấp phát
  ↓
Kho/BCH xác nhận nhận và sử dụng tại công trường
  ↓
Module 6 ghi nhận nghĩa vụ chi theo chứng từ hợp lệ
```

### 3.5. Luồng QA/QC, nghiệm thu và thanh toán

```text
Bản vẽ IFC đã phát hành (Module 2)
  ↓
BCH thi công + lập Nhật ký / QA-QC / HSE / Punchlist
  ↓
Phòng CM thẩm định và kiểm soát chéo
  ↓
Nghiệm thu khối lượng
  ├─ Upstream: BCH → CM → CĐT/TVGS → mốc thu tiền Module 6
  └─ Downstream: BCH → CM → Kế toán → hồ sơ chi Module 6
  ↓
Hoàn tất hồ sơ QA/QC + đóng lỗi chặn + hoàn công + commissioning
  ↓
Nghiệm thu toàn công trình và bàn giao
```

Người lập chứng từ không được đồng thời thực hiện bước thẩm định độc lập của
Phòng CM trên cùng chứng từ. Mọi bước duyệt phải lưu người thực hiện, thời điểm,
ý kiến và phiên bản dữ liệu được duyệt.

## 4. Liên kết giữa các module

| Nguồn | Đích | Dữ liệu/điều kiện chuyển giao |
|---|---|---|
| Module 1 | Module 2/3/5/6 | Dự án dùng chung và gói công việc trước hợp đồng |
| Module 2 | Module 3 | Hồ sơ thiết kế cơ sở đã được phép dùng cho thủ tục pháp lý |
| Module 2 | Module 4 | Chỉ bản vẽ IFC đã phát hành được dùng để thi công |
| Module 5 | Module 4 | BOQ, nhà cung cấp/thầu phụ, kết quả lựa chọn và cấp phát nguồn lực |
| Module 6 | Module 4 | Hợp đồng Upstream kích hoạt chuẩn bị; hợp đồng Downstream và VO làm căn cứ kiểm soát |
| Module 4 | Module 5 | Đề xuất nguồn lực đã qua các cấp duyệt bắt buộc |
| Module 4 | Module 6 | QS Upstream/Downstream và bộ hồ sơ thu/chi theo đợt |
| Module 4 | Module 7 | Nhật ký, QA/QC, nghiệm thu, Punchlist và hoàn công theo thư mục dự án |
| Module 4/5/6 | Module 8 | Tiến độ, chất lượng, an toàn, chi phí và kết quả nghiệm thu đã được xác nhận |

## 5. Lớp xuyên suốt

### Module 7 - Google Drive

- Tạo cây thư mục theo dự án và phân loại hồ sơ theo module/giai đoạn.
- Đồng bộ file nhưng không thay thế metadata, trạng thái, quyền, phiên bản và
  audit trail trong hệ thống.
- Lỗi đồng bộ phải hiển thị, cho phép retry an toàn và không được báo thành công
  khi file chưa lưu được trên Drive.

### Module 8 - Dashboard và KPI

- Chỉ sử dụng dữ liệu nguồn có chủ sở hữu, trạng thái và thời điểm rõ ràng.
- Không lấy bản nháp, dữ liệu offline chưa đồng bộ hoặc chứng từ bị từ chối làm
  bằng chứng KPI/tài chính.
- Baseline tiến độ/ngân sách và công thức đo phải có phiên bản để kết quả có thể
  tái hiện và kiểm toán.

## 6. Điểm cần chốt trước khi triển khai Module 4 mở rộng

1. Công thức, nguồn dữ liệu và kỳ chốt để xác định độ trễ S-Curve `> 5%`.
2. Người có quyền phát hành, mở khóa hoặc thay thế Baseline tiến độ và Budget
   Baseline.
3. Ma trận duyệt và quy tắc ủy quyền cho MR, QA/QC, HSE, QS và VO.
4. Checklist QA/QC bắt buộc theo loại công trình và điều kiện chặn nghiệm thu,
   hoàn công, bàn giao.
5. Thành phần bộ hồ sơ thanh toán Upstream/Downstream và thời điểm Module 6 được
   phép tạo yêu cầu thu/chi.
