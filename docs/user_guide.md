# NICON Platform — Hướng dẫn sử dụng

Phiên bản 1.1

Cập nhật lần cuối: 17/11/2026

---

## Mục lục

1. [Giới thiệu](#1-giới-thiệu)
2. [Tổng quan nghiệp vụ](#2-tổng-quan-nghiệp-vụ)
   - [2.5 Nghiệp vụ chính: quy trình, vai trò, quyết định](#25-nghiệp-vụ-chính)
3. [Nghiệp vụ theo phân hệ](#3-nghiệp-vụ-theo-phân-hệ)
   - [3.0 Dự án và Nhóm công việc](#30-dự-án)
   - [3.1 CRM, Báo giá, Đấu thầu, Hợp đồng](#31-crm-báo-giá-đấu-thầu-hợp-đồng)
   - [3.2 Thiết kế](#32-thiết-kế)
   - [3.3 Pháp lý](#33-pháp-lý)
   - [3.4 Thi công và Nghiệm thu](#34-thi-công-và-nghiệm-thu)
   - [3.5 Cung ứng](#35-cung-ứng)
   - [3.6 Tài chính](#36-tài-chính)
   - [3.7 Tài liệu dự án và Google Drive](#37-tài-liệu-dự-án-và-google-drive)
   - [3.8 Báo cáo và KPI](#38-báo-cáo-và-kpi)
   - [3.9 Thông báo và tính năng dùng chung](#39-thông-báo-và-tính-năng-dùng-chung)
4. [Vai trò và phân quyền](#4-vai-trò-và-phân-quyền)
5. [Đăng ký, đăng nhập, mật khẩu](#5-đăng-ký-đăng-nhập-mật-khẩu)
6. [Website công khai](#6-website-công-khai)
7. [Trang quản trị](#7-trang-quản-trị)
8. [Quản lý nội dung website](#8-quản-lý-nội-dung-website)
9. [Tuyển dụng](#9-tuyển-dụng)
10. [Liên hệ](#10-liên-hệ)
11. [Đa ngôn ngữ](#11-đa-ngôn-ngữ)
12. [Email](#12-email)
13. [Tải tệp lên](#13-tải-tệp-lên)
14. [Tài liệu kỹ thuật](#14-tài-liệu-kỹ-thuật)

---

## 1. Giới thiệu

NICON là công ty Design & Build. Hệ thống này quản lý toàn bộ vòng đời một công trình: tìm khách, báo giá, đấu thầu, ký hợp đồng, thiết kế, xin phép, thi công, nghiệm thu, cung ứng, tài chính, hoàn công và bàn giao. Ngoài ra còn có website công ty (dịch vụ, dự án, tin tức, hoạt động, tuyển dụng, liên hệ) và trang quản trị.

Tài liệu này dành cho nhân viên NICON. Mỗi phần nói phòng ban làm gì, bấm ở đâu, trạng thái nghĩa là gì, thao tác nào bị chặn và cần làm gì tiếp.

| Bộ phận | Việc chính trên hệ thống |
|---|---|
| Kinh doanh | Lead, Cơ hội, Khảo sát, Báo giá, Đấu thầu, Hợp đồng khách hàng |
| Thiết kế | Hồ sơ thiết kế 3 giai đoạn (Ý tưởng, Cơ sở, Chi tiết), revision, phát hành IFC |
| Pháp lý | Checklist pháp lý, theo dõi giấy phép |
| Phòng CM và Ban chỉ huy công trường (BCH) | Tiến độ, nhật ký công trường, QA/QC, nghiệm thu, punchlist, HSE, hoàn công, bàn giao |
| Cung ứng | Nhà cung cấp/thầu phụ, báo giá đầu vào, BOQ, RFQ, yêu cầu vật tư, kho, cảnh báo vật tư |
| Tài chính – Kế toán | Rà soát thương mại, phải thu, phải trả, đề nghị thanh toán, VO |
| Ban giám đốc | Duyệt, theo dõi danh mục dự án, báo cáo, KPI |
| Quản trị / Hành chính nhân sự | Nội dung website, liên hệ, tuyển dụng, email, bản dịch, người dùng và phân quyền |

**Cách đọc nhãn tình trạng trong tài liệu:**

- **Đã triển khai**: dùng được đầy đủ như mô tả.
- **Một phần**: dùng được phần đã nêu; phần còn thiếu được ghi rõ.
- **Mục tiêu**: NICON đã yêu cầu nhưng chưa có trên hệ thống. Không hướng dẫn người dùng dùng các phần này.

Căn cứ nghiệp vụ là biên bản họp `docs/Nicon/MoMs/2026-10-06/NICON_Meeting_2026-10-03_Tom-tat-yeu-cau-moi.md`. Bộ tài liệu gốc của NICON (PRD, quy trình, tiến độ mẫu, BOQ mẫu, biên bản họp) nằm trong `docs/Nicon/`. Khi các tài liệu gốc mâu thuẫn, quyết định trong biên bản họp mới nhất được áp dụng; ví dụ mỗi công việc thiết kế có một người duyệt và CC, thay cho chuỗi duyệt nhiều cấp trong PRD cũ. Tiến độ và BOQ mẫu chỉ minh họa dạng dữ liệu; tên sheet, màu, ngày, tên khách không phải quy tắc của hệ thống.

Hướng dẫn cài đặt, build, triển khai, API và mô hình dữ liệu nằm trong `application_developer.md`.

---

## 2. Tổng quan nghiệp vụ

### 2.1 Thông tin công ty

NICON có trụ sở tại TP. Hồ Chí Minh.

- Tên hiển thị: NICON
- Điện thoại: 028 7300 1976
- Email: info@nihome.vn
- Địa chỉ: 92 Đường 56, Phường Bình Trưng, TP. Hồ Chí Minh

### 2.2 Phạm vi và nguyên tắc chung

Mọi phân hệ dùng chung một cấu trúc: **một Khách hàng (công ty hoặc cá nhân) có nhiều Dự án, một Dự án có nhiều Hợp đồng**. Mã dự án là khóa truy vết xuyên suốt từ bán hàng đến bàn giao. Dự án hiển thị trên website và Hồ sơ thiết kế là hai thứ khác, không dùng thay mã dự án.

Khi tạo Dự án, hệ thống đề xuất mã tiếp theo. Người có quyền có thể sửa mã trước khi lưu, miễn đúng định dạng hiển thị và không trùng. Sau khi tạo, mã chỉ đổi được qua thao tác **Đổi mã dự án** (xem [3.0](#30-dự-án)).

Menu CRM gồm, theo thứ tự: Dự án, Cơ hội, Sổ hợp đồng, Quản lý Lead, Quản lý Khách hàng, Báo giá, Mẫu báo giá, Đấu thầu, Hồ sơ năng lực, Khảo sát, Báo cáo dự án. Người dùng chỉ thấy mục mình có quyền.

| Phân hệ | Phạm vi |
|---|---|
| 1. CRM / Bán hàng / Hợp đồng | Khách hàng, Lead, Cơ hội, Báo giá, Đấu thầu, Khảo sát, Hợp đồng khách hàng |
| 2. Thiết kế | Ba giai đoạn thiết kế, revision, phát hành IFC |
| 3. Pháp lý | Checklist pháp lý, theo dõi giấy phép |
| 4. Thi công và Nghiệm thu | Tiến độ, nhật ký, QA/QC, nghiệm thu, hoàn công, punchlist, bàn giao |
| 5. Cung ứng | Nhà cung cấp/thầu phụ, RFQ và so sánh giá, BOQ, yêu cầu vật tư, kho, cảnh báo |
| 6. Tài chính | Phải thu, phải trả, VO, dòng tiền, lãi lỗ |
| 7. Tài liệu số | Thư mục dự án, đồng bộ Google Drive, số hóa tài liệu |
| 8. Báo cáo | Báo cáo dự án, tỷ lệ trúng thầu, KPI |

### 2.3 Thao tác chung trên giao diện

- Ô tìm kiếm tự lọc sau khi bạn ngừng gõ một chút. Danh sách có nút Trước/Sau và tổng số bản ghi. Đổi bộ lọc thì quay về trang 1.
- Nếu một danh sách chọn không tải đủ, tải lại trang trước khi làm tiếp; đừng chọn trên danh sách thiếu.
- Hủy hộp xác nhận thì bản ghi giữ nguyên. Khi đang lưu, để hộp thoại mở đến khi có kết quả. Form lỗi sẽ báo lỗi và đưa con trỏ tới ô cần sửa; dữ liệu đã nhập không mất.
- Ô chọn người hiển thị tên, vai trò và phòng ban. Dùng **Phòng ban / Nhóm** để thu hẹp, rồi tìm theo tên, vai trò hoặc email. Lọc không cấp quyền giao việc và không bỏ người đã chọn; người đã chọn vẫn hiện dạng thẻ có nút xóa. Ô chọn thành viên dự án hiển thị thêm vai trò trong dự án và chức vụ. Trong **Người dùng**, bộ lọc phòng ban và vai trò áp dụng cùng lúc.
- Khi gặp lỗi "dữ liệu đã thay đổi" (người khác vừa sửa), tải lại bản mới rồi làm lại. Hệ thống không cho ghi đè.
- Hộp thoại tự cuộn trên màn hình thấp.

### 2.4 Tình trạng triển khai

| Thành phần | Tình trạng |
|---|---|
| Đăng ký, đăng nhập, OTP, quên mật khẩu | Đã triển khai |
| Website: tin tức, hoạt động, dự án, dịch vụ, slideshow, logo, quy trình | Đã triển khai |
| Tuyển dụng, liên hệ, email, tải tệp, cài đặt, thông báo trong ứng dụng | Đã triển khai |
| Đa ngôn ngữ (Việt, Anh, Trung, Nhật) | Đã triển khai |
| CRM: Khách hàng, Lead (có phân khúc), Cơ hội (5 bước) | Đã triển khai |
| Dự án dùng chung | Đã triển khai. Một số hồ sơ thi công và hoàn công cũ vẫn gắn với Hồ sơ thiết kế, chờ chuyển đổi |
| Báo giá, hồ sơ năng lực, đấu thầu | Đã triển khai |
| Khảo sát | Đã triển khai |
| Hợp đồng, phụ lục, tệp đính kèm, VO | Đã triển khai |
| Thiết kế | Một phần. Chưa có: xem 3D trực tiếp, ghi chú trên bản vẽ, nhập đầy đủ MS Project |
| Pháp lý | Một phần. Chưa có: danh mục mẫu checklist theo loại/quy mô công trình được NICON duyệt, tự chọn mẫu |
| Thi công (tiến độ, nhật ký, chuẩn bị CM, QA/QC, QS, HSE) | Một phần. Chưa có: rà soát toàn danh mục dự án, làm việc offline. Cách tính S-Curve theo quyết định NICON ủy quyền ngày 09/11/2026 |
| Nghiệm thu và bàn giao | Một phần. Chưa có: điều kiện thanh toán theo QS hai chiều, hồ sơ ba phần, cổng bàn giao đầy đủ |
| Punchlist | Một phần. Chưa có: CM giám sát quá hạn, ảnh/video đính kèm, ghim lỗi trên mặt bằng |
| Vi phạm HSE | Một phần. Ma trận duyệt/ủy quyền giữa BCH và CM chưa chốt |
| Nhà cung cấp/thầu phụ, BOQ, yêu cầu vật tư, kho | Đã triển khai (cần kết nối mạng) |
| Tài chính | Một phần. Chưa có: hồ sơ thanh toán hai chiều, báo cáo dòng tiền và lãi lỗ đầy đủ |
| Google Drive | Đã triển khai; cần quản trị viên kết nối tài khoản |
| Báo cáo | Một phần. Chưa có báo cáo tổng hợp toàn phân hệ cho Ban giám đốc |
| KPI | Một phần: 19 chỉ số trong 6 nhóm; chưa đủ khung 11 vị trí của NICON |
| Luồng duyệt chung, nhật ký thao tác, tìm kiếm toàn hệ thống, dữ liệu chuẩn | Một phần. Chưa có: ma trận duyệt thống nhất, ghi nhật ký mọi lượt tải tệp, tìm kiếm liên phân hệ, bộ lọc lưu sẵn, mẫu theo dự án |

**Về file `Nicon_BreakTask_v1.xlsx`.** File này là bằng chứng kế hoạch của khách hàng, không phải ước lượng giao hàng hiện tại. Lần rà soát ngày 20/10/2026 (revision `d884b3c6`) phân loại các dòng thành `Implemented`, `Partial`, `Target`, `Open decision`, kèm ghi chú phần đã có và phần còn thiếu. Các con số tổng task và công frontend công bố trước đây đã rút lại vì không khớp với file. Không dùng số task hay tổng công cho kế hoạch thương mại nếu chưa ghi rõ phiên bản file, dòng tính, công thức, phạm vi backend/QA/triển khai, giả định và ngày duyệt. Gantt kéo thả, trình dựng luồng duyệt, mobile offline, nhập BOQ nhiều sheet tổng quát và ghim lỗi trên mặt bằng cần ước lượng riêng sau khi chốt nghiệp vụ.

### 2.5 Nghiệp vụ chính

Mỗi giao dịch được ghi một lần, có người chịu trách nhiệm, trạng thái và bằng chứng, và truy được về cùng một Dự án. Các phòng ban dùng chung Dự án đó, không tạo dự án riêng cho phòng mình.

```text
Khách hàng
  └─ Lead / Cơ hội
       └─ Dự án (dùng chung)
            ├─ Báo giá / hồ sơ thầu
            ├─ Hợp đồng đầu ra với chủ đầu tư/khách hàng
            ├─ Hồ sơ thiết kế và hồ sơ pháp lý
            ├─ Thi công, QA/QC, HSE, QS, nghiệm thu
            ├─ BOQ, RFQ, hợp đồng đầu vào, kho
            ├─ Thu/chi, hồ sơ thanh toán, lãi lỗ
            └─ Tài liệu, nhật ký thao tác, báo cáo, KPI
```

**Thuật ngữ.** *Đầu ra (Upstream)*: hợp đồng, nghiệm thu, phải thu với chủ đầu tư/khách hàng. *Đầu vào (Downstream)*: hợp đồng, nghiệm thu, phải trả với nhà cung cấp, thầu phụ, tổ đội. *BCH*: Ban chỉ huy công trường tại hiện trường. *Phòng CM*: phòng quản lý thi công ở văn phòng, quản lý danh mục dự án và kiểm soát chéo. *IFC*: bản vẽ được phát hành chính thức cho thi công. *Baseline*: phiên bản kế hoạch đã công bố; sửa thì tạo phiên bản mới, không ghi đè.

#### 2.5.1 Các đường vào dự án

Không phải dự án nào cũng đi đủ một chuỗi.

| Luồng | Trình tự | Điều kiện chính |
|---|---|---|
| Design & Build | Lead → Cơ hội + Dự án → khảo sát, thiết kế, giá đầu vào → báo giá hoặc thống nhất giá trực tiếp → hợp đồng D&B → thiết kế, pháp lý, thi công | Có báo giá thì chỉ dùng báo giá đã duyệt; thi công chỉ dùng IFC đã phát hành |
| Thiết kế trước hợp đồng | Cơ hội + Dự án → Ý tưởng, Cơ sở, Chi tiết → BOQ/báo giá → hợp đồng thiết kế, D&B hoặc thi công | Thiết kế bắt đầu khi chưa có hợp đồng nhưng luôn thuộc một Dự án |
| Đấu thầu | Dự án → gói thầu → kế hoạch, checklist, dự toán → nộp → kết quả → thương thảo → hợp đồng | Kết quả, hạn nộp và phiên bản dự toán có lịch sử |
| Tư vấn / báo giá sơ bộ | Dự án → khảo sát/Ý tưởng → suất đầu tư hoặc BOQ sơ bộ → báo giá → chuyển tiếp nếu thành công | BOQ sơ bộ không dùng làm hạn mức cấp vật tư thi công |

#### 2.5.2 Quy trình từ Lead đến Bàn giao

| Bước | Phụ trách | Trạng thái chính | Điều kiện để qua bước |
|---|---|---|---|
| 1. Lead | Kinh doanh | Lead bị khóa sau khi chuyển đổi | Chuyển Lead tạo hoặc nối Khách hàng, Cơ hội và Dự án (Lập kế hoạch) |
| 2. Cơ hội | Kinh doanh | Tiếp cận → Khảo sát → Báo giá/Đấu thầu → Thương thảo → Ký hợp đồng; hoặc Thua | Ký hợp đồng cần hợp đồng liên kết có ngày ký, không Nháp/Đã hủy |
| 3. Dự án | Kinh doanh hoặc PM | Lập kế hoạch → Đang thực hiện / Tạm dừng / Hoàn thành / Đã hủy | Có khách hàng, mã, tên, người phụ trách. Mã đổi được sau khi tạo, có lý do và lịch sử |
| 4a. Báo giá | Kinh doanh; Cung ứng (giá đầu vào; cả phòng xem mọi báo giá); người duyệt theo Luồng duyệt báo giá, mặc định Trưởng phòng Cung ứng (duyệt lần lượt từng bước, nhận thông báo kèm link khi tới lượt); Tài chính (rà soát) | Nháp → Chờ duyệt nội bộ → Đã duyệt → Đã gửi khách → Khách đã duyệt | Gửi khách cần báo giá gắn Dự án và Tài chính rà soát đã duyệt đúng phiên bản |
| 4b. Đấu thầu | Kinh doanh/QS văn phòng; các phòng theo kế hoạch thầu; Tài chính | Đang chuẩn bị → Đã nộp → Trúng/Trượt | Nộp cần kế hoạch đã xong, checklist đủ, dự toán đã duyệt, Tài chính rà soát hồ sơ thầu đã duyệt |
| 5. Hợp đồng | Kinh doanh; Kế toán theo dõi thu/chi | Bản nháp → Đã ký → Đang thực hiện → Hoàn thành (Tạm dừng, Đã huỷ) | Từ báo giá đã duyệt hoặc tạo trực tiếp khi khách đã đồng ý giá trị. Đã ký cần bản scan hợp đồng đã ký |
| 6. Tiến độ tổng dự án | Người quản lý dự án lập; Trưởng phòng Kinh doanh (trước ký) hoặc PM (sau ký) duyệt | Nháp → chờ duyệt → phát hành | Ngày công việc phòng ban, thiết kế, thi công nằm trong mốc WBS |
| 7. Thiết kế | Chủ nhiệm thiết kế, Chủ trì thiết kế, thành viên thiết kế | Ý tưởng → Cơ sở → Chi tiết → IFC | Mỗi giai đoạn cần revision mới nhất được khách duyệt. IFC chỉ phát hành khi Shop Drawing được khách duyệt |
| 8. Pháp lý | PM/Admin kích hoạt; trưởng nhóm Pháp lý; người thực hiện và người duyệt từng giấy phép | Chưa chuẩn bị → Đang chuẩn bị → Đã nộp → Đang thẩm định → Đã cấp (Bổ sung hồ sơ, Bị từ chối, Hết hạn) | Hợp đồng thiết kế/thi công/D&B chỉ sang Đang thực hiện qua kích hoạt pháp lý. Giấy phép có hạn phải còn hiệu lực khi dùng |
| 9. Chuẩn bị thi công | PM Phòng CM | Chuẩn bị CM → đủ điều kiện khởi công | Hợp đồng còn hiệu lực, IFC cần cho mốc khởi công đã phát hành, giấy phép bắt buộc đã cấp và còn hạn, tiến độ cơ sở đã công bố, BCH và kế hoạch nguồn lực đã duyệt, Ngân sách cơ sở đã khóa |
| 10. Thi công | BCH thực hiện; Phòng CM kiểm soát chéo; Cung ứng; Thủ kho | Nhật ký, QA/QC, punchlist, HSE, đề xuất nguồn lực | Người lập không tự duyệt. Vật tư đi qua yêu cầu vật tư theo BOQ hợp đồng; máy/nhân công qua CM → Cung ứng ký hợp đồng và cấp → Tài chính thanh toán |
| 11. Nghiệm thu và thanh toán | QS công trường lập; Phòng CM duyệt cấp 2; Kế toán | QS đầu ra → mốc phải thu; QS đầu vào → đề nghị thanh toán | Khối lượng đã duyệt không sửa; sửa bằng phiên bản thay thế hoặc đảo |
| 12. Hoàn công và Bàn giao | Phòng CM, người phụ trách bàn giao | Bản nháp → Sẵn sàng bàn giao → Đã bàn giao | Có nghiệm thu đã duyệt, đủ danh mục hoàn công bắt buộc, chạy thử xong, hết punchlist chặn, checklist xong, ít nhất một người ký. Bàn giao kỹ thuật không có nghĩa đã thu/chi xong |

Chi tiết từng bước ở các mục 3.0 đến 3.8.

#### 2.5.3 Các loại BOQ

| Loại | Dùng để | Phụ trách | Là hạn mức vật tư/kho? |
|---|---|---|---|
| BOQ sơ bộ | Báo giá sớm, suất đầu tư | Kinh doanh/Đấu thầu | Không |
| BOQ dự thầu (**Đã duyệt (dự thầu)**) | Lập và so sánh giá dự thầu, nguồn dòng cho báo giá BOQ | Cung ứng upload, trưởng nhóm duyệt | Không |
| Khối lượng từ hồ sơ thiết kế | Đối chiếu | Thiết kế/QS | Chỉ để đối chiếu |
| BOQ hợp đồng | Hạn mức thực hiện sau khi ký | PM, Cung ứng/QS | Có, phiên bản đã duyệt hiện hành |
| BOQ cuối | Bản cuối khi dự án xong, gồm VO đã duyệt | PM CM/Tài chính | Dùng quyết toán, báo cáo, KPI |

Theo quyết định họp 31/10/2026, mỗi Dự án có một BOQ gốc; khi trúng thầu, BOQ dự thầu chuyển thành BOQ hợp đồng mà không tạo file mới. Phòng CM lập bản kế hoạch, BCH lập bản thực tế, hệ thống so sánh ba bản. Mỗi sheet là một hạng mục, sheet tổng cộng từ các sheet con. Phần bản kế hoạch, bản thực tế và so sánh ba bản chưa có.

#### 2.5.4 Tiến độ S-Curve

- **Độ chậm** = % kế hoạch − % thực tế, tính bằng điểm phần trăm, không chia cho % kế hoạch (xác nhận 24/10/2026). Chậm **lớn hơn 5 điểm** mới là cảnh báo đỏ, cần giải trình và kế hoạch khắc phục. Ví dụ: kế hoạch 50%, thực tế 46% là chậm 4 điểm; 50% và 45% chưa vượt ngưỡng; 50% và 44% vượt ngưỡng. Kỳ xem linh hoạt ngày/tháng/năm. Trên hệ thống hiện tại, ngưỡng bắt buộc giải trình theo cấu hình ([3.4.1](#341-tiến-độ-thi-công)).
- **Cách tính** (người đại diện chủ sản phẩm chốt theo ủy quyền của NICON ngày 09/11/2026): % thực tế lấy từ báo cáo của BCH, có người và thời điểm. CM chia tỷ trọng cho các công việc lá của WBS, tổng đúng 100%, PM duyệt độc lập; mốc và WBS tổng không có tỷ trọng. Tỷ trọng giải thích theo phạm vi, khối lượng hoặc độ phức tạp, không cần BOQ. Lịch làm việc mặc định Thứ Hai–Thứ Bảy, Chủ nhật nghỉ, khóa theo phiên bản. Báo cáo tháng/năm lấy số cuối kỳ, không nội suy. Thiếu báo cáo là "chưa có dữ liệu", không phải 0%. Baseline đã công bố không mở khóa; thay đổi tạo phiên bản mới và PM duyệt. Tổng hợp S-Curve toàn công ty chưa có.

#### 2.5.5 Quyết định đã xác nhận

| Ngày | Quyết định | Nguồn |
|---|---|---|
| 03/10/2026 | Biên bản họp làm căn cứ nghiệp vụ. Năm nhóm phòng: Kinh doanh/CRM, Hành chính nhân sự, Thiết kế, Quản lý thi công, Tài chính kế toán. Mỗi công việc thiết kế có đúng một người duyệt, người khác theo dõi qua CC. Người nhận trình nội bộ thiết kế xem và góp ý khi được chỉ định | NICON |
| 16/10/2026 | Hợp đồng đầu ra có hai cách tạo: từ báo giá đã duyệt, hoặc trực tiếp khi khách đã đồng ý giá trị | NICON |
| 18/10/2026 | Mỗi dự án có BCH riêng; một người tham gia BCH nhiều dự án được, quyền vẫn theo từng dự án. Máy/nhân công: CM duyệt → Cung ứng ký hợp đồng và cấp cho công trường → Tài chính thanh toán. Danh mục CM theo dõi: tiến độ, khối lượng (đầu ra/đầu vào tách riêng), giá trị thực hiện so dự toán, QA/QC, an toàn | Phản hồi NICON do người dùng chuyển |
| 20/10/2026 | Đã có lịch sử chăm sóc khách hàng thì không đổi chiều hợp đồng. Email nhắc gia hạn/kết thúc gửi tự động theo ngày đặt riêng cho từng hợp đồng, chỉ để thông báo. QS bị thay thế sau khi trình đề nghị thanh toán: tạo revision của cùng đề nghị, giữ số hóa đơn, duyệt lại; nếu bị thay thế sau khi duyệt thì không chi tiền đến khi revision mới được duyệt. Bộ ISO NICON đã nhận | Người dùng; NICON (QS/thanh toán) |
| 21/10/2026 | Từ lần đầu bắt đầu gửi email nhắc, khóa chiều hợp đồng, kể cả khi chưa rõ kết quả gửi | Người dùng |
| 22/10/2026 | Quy tắc tái ký/phát triển hợp đồng ([3.1.7](#317-hợp-đồng)) | Chỉ đạo của người dùng, chưa phải biên bản NICON |
| 23/10/2026 | Nhóm công việc Hoàn thành được mở lại để thêm việc; giữ nguyên kết quả đã xác nhận | Người dùng |
| 24/10/2026 | Công thức độ chậm S-Curve và ngưỡng 5 điểm | Người dùng |
| 25/10/2026 | Tạo báo giá mới chỉ cần Cơ hội thuộc Dự án và ghi chú; giá đến sau khi có giá đầu vào đã duyệt. Khi gửi duyệt giá đầu vào/rà soát Tài chính: xem trước email, sửa được To, thêm CC | Người dùng |
| 29–31/10/2026 | Người duyệt báo giá đầu ra là Trưởng phòng Cung ứng (chốt 29/10, đổi 30/10, bổ sung 31/10 về vai trò và phạm vi CC). Nhóm công việc chung giao được cho mọi phòng (29/10). Vai trò mới ở tab Đội ngũ tự thuộc Thiết kế (29/10) | Chủ sản phẩm / người dùng |
| 30/10/2026 | Một Dự án có nhiều luồng thiết kế, mỗi luồng có đội ngũ và tiến độ riêng. Tài chính trên hợp đồng dùng lại hồ sơ rà soát của báo giá nguồn | Chủ sản phẩm |
| 31/10/2026 | Nhóm công việc xếp tab theo việc đã gắn hợp đồng hay chưa. Công việc cha tự tổng hợp từ công việc con, nhiều cấp. Một BOQ gốc mỗi dự án. Menu Thiết kế gọi là **Hồ Sơ Thiết Kế** | Người dùng; biên bản họp |
| 08/11/2026 | Gửi IFC cho Cung ứng chỉ gồm nhận, xem, xác nhận; không phải duyệt thiết kế, không tự tạo mua sắm. Báo giá BOQ lấy dòng từ BOQ dự án đã duyệt | Chủ sản phẩm |
| 09/11/2026 | NICON ủy quyền người đại diện chủ sản phẩm chốt quy tắc S-Curve (2.5.4) | NICON ủy quyền |
| 13/11/2026 | Cung ứng upload BOQ trong nhóm công việc yêu cầu giá đầu vào; trang Mẫu báo giá → BOQ thành thư viện mẫu chỉ đọc | Triển khai |
| 14/11/2026 | Chuyển BOQ dự thầu thành BOQ hợp đồng; hai nguồn giá đầu vào chung khung **Báo giá đầu vào**; thêm 4 mẫu BOQ | Triển khai |
| 19/11/2026 | Cả phòng Cung ứng (trưởng phòng và nhân viên) xem được mọi báo giá của NICON; chỉ người được chọn duyệt mới duyệt được và nhận thông báo kèm link tới báo giá | Người dùng |

Mục tiêu đã ghi nhận ngày 07/11/2026 (chưa có): bản dịch cập nhật của mẫu checklist pháp lý và QA/QC áp dụng cả cho hồ sơ đã tạo; file BOQ tải trước khi đổi nhãn vẫn nhập được.

#### 2.5.6 Quyết định còn mở

NICON (hoặc người được ủy quyền) cần chốt:

1. Q-01: nhóm công việc tạo trước hợp đồng giữ ở Dự án hay chuyển sang Hợp đồng; xử lý khi một dự án có nhiều hợp đồng.
2. Q-02: công việc nội bộ có bắt buộc duyệt không; điều kiện và trọng số KPI.
3. Q-03: ai được giao việc liên phòng (PM dự án, người phụ trách Cơ hội hay cả hai).
4. Q-04: Cung ứng và Pháp lý là phòng riêng hay thuộc phòng nào.
5. Q-05: quy ước mã dự án.
6. Q-06: người nhận trình nội bộ thiết kế chỉ góp ý hay phải đồng ý trước khi trình khách.
7. Q-07: trưởng/phó phòng Thiết kế có được CC mặc định mọi dự án không.
8. Q-08: ai được xóa hồ sơ thiết kế không đạt; hồ sơ đã trình khách có được xóa không.
9. Q-09: danh mục giấy phép theo loại/quy mô công trình.
10. Q-10: ranh giới Chủ trì duyệt công việc và Chủ nhiệm chốt phương án/chuyển giai đoạn.
11. Q-11: phạm vi nhập MS Project (lịch, baseline, phân cấp).
12. Ma trận duyệt/ủy quyền cho yêu cầu vật tư, QA/QC, HSE, QS, VO; chính sách VO chính thức.
13. Chứng từ thanh toán cho 4 loại gói đầu vào (chỉ vật tư, chỉ nhân công, thuê máy, hỗn hợp) theo bộ ISO; FA (chị K/Kiara) bổ sung mục thiếu; điều kiện tạm ứng/trả trước.
14. Đối chiếu bộ ISO trong `docs/Nicon/ISO/` vào checklist QA/QC và điều kiện khởi công theo loại công trình; NICON xác minh các mẫu được dẫn chiếu nhưng chưa thấy file (`TC-M02`, `TC-M23`, `TQT-M00`, `TC-M00`) và các mã trùng.
15. Công thức ngưỡng BOQ 85/95/trên 100%.
16. Chuyển hồ sơ Thiết kế, Pháp lý, Thi công cũ sang Dự án; chuyển thư mục `01_Khao_sat` vào `01_CRM_PreDesign`.
17. Định nghĩa KPI cho các vị trí còn thiếu; sơ đồ phòng ban – vai trò chính thức.
18. Ủy quyền phát hành điện tử, thu hồi phiên bản cũ, thời hạn lưu và thẩm quyền hủy hồ sơ theo quy trình kiểm soát tài liệu ISO.

Lưu ý: 11 tiêu chí đánh giá cá nhân trong mẫu ISO `NS2-M01` khác với khung 11 vị trí KPI trong PRD. Con số 5% trong mẫu thanh toán ISO là khoản giữ lại bảo hành, không phải ngưỡng chậm tiến độ.

---

## 3. Nghiệp vụ theo phân hệ

### 3.0 Dự án

Mở **CRM → Dự án** để tạo hoặc tìm dự án trước khi làm việc ở các phân hệ khác. Danh sách hiển thị khách hàng, trạng thái và số Cơ hội, Báo giá, Hợp đồng liên kết. Trang chi tiết có liên kết tới các bản ghi đó và tới Hồ sơ thiết kế. Tìm theo mã hoặc tên dự án; bộ lọc trạng thái vẫn dùng được khi tìm không ra kết quả.

**Trạng thái dự án**

| Trạng thái | Ý nghĩa |
|---|---|
| Lập kế hoạch (`Planning`) | Trạng thái khi mới tạo |
| Đang thực hiện (`Active`) | Dự án đang chạy |
| Tạm dừng (`OnHold`) | Tạm ngưng |
| Hoàn thành (`Completed`) | Đã xong; vẫn xem được toàn bộ hợp đồng, giao dịch, tệp, lịch sử |
| Đã hủy (`Cancelled`) | Ngừng; vẫn xem được như trên |

- Chỉ xóa được dự án đang **Lập kế hoạch** và chưa có dữ liệu. Dự án đã có lịch sử nghiệp vụ thì chuyển **Đã hủy**.
- Không đổi được khách hàng khi dự án đã có bản ghi liên kết.
- Dự án không lưu trữ (archive) theo khối. Thao tác lưu trữ trong một luồng tài liệu chỉ áp dụng cho tài liệu đó.

**Quyền xem và sửa.** Mặc định bạn thấy dự án mình tạo hoặc quản lý. Vai trò xem được tất cả dự án thì thấy toàn bộ danh mục. Tạo và sửa cần quyền quản lý dự án.

**Đổi mã dự án** (cần quyền đổi mã dự án):

1. Mở chi tiết dự án, chọn **Đổi mã dự án**.
2. Nhập mã mới (2–40 ký tự) và lý do, rồi lưu.
3. Nếu màn hình đã cũ (người khác vừa sửa), hệ thống từ chối; tải lại rồi làm lại.

Nếu dự án đã có thư mục Drive, trang chi tiết hiển thị trạng thái đổi tên thư mục; lần đổi tên lỗi có thể xếp hàng chạy lại mà không tạo thư mục mới. Tìm theo mã cũ vẫn ra dự án. Dự án Hoàn thành hoặc Đã hủy phải mở lại trước khi đổi mã. Mục **Lịch sử thay đổi** cuối trang ghi mã cũ → mã mới, lý do, người đổi và thời điểm, cùng các thay đổi thành viên và phân công, mới nhất ở trên.

**Phân công.** Khi quản lý đổi người phụ trách một phân công đang chạy, phân công cũ được đánh dấu đã bàn giao và giữ trong lịch sử; hệ thống tạo phân công mới cho người được chọn.

**Timeline thanh toán.** Gom các đợt thanh toán của mọi hợp đồng thuộc dự án, kèm số hợp đồng để phân biệt. Ngày kế hoạch lấy từ hạn của đợt; ngày thực tế là ngày đã xác nhận trên đợt thanh toán của hợp đồng, không suy ra từ thời điểm cập nhật. Khi chuyển đợt sang **Đã thanh toán**, hệ thống đề xuất ngày hôm nay; người có quyền sửa hợp đồng có thể chọn ngày khác hoặc sửa sau. Chuyển về Chờ/Đã yêu cầu thì ngày thực tế bị xóa. Đợt cũ có thể để trống ngày thực tế đến khi có người xác nhận.

**Tài liệu dự án** là danh mục tệp chung của dự án:

- Người có quyền xem: xem trạng thái đồng bộ, tải tệp, mở bản trên Drive.
- Người có quyền quản lý: tải lên tối đa 20 tệp mỗi lần, dung lượng mỗi tệp theo **Cài đặt → Tài liệu dự án** (mặc định 100 MB), cho một danh mục. Tệp lỗi vẫn được giữ để thử lại. Có thể **Gửi lại lên Drive**, phân loại tệp lạ nhập từ Drive, chọn **Giữ cả hai** khi hai bên cùng sửa, và xóa tệp tải tay hoặc tệp nhập từ Drive.
- Tệp thuộc Báo giá, Hợp đồng, Khảo sát, Thiết kế, Giấy phép, Nghiệm thu, Hoàn công, Bàn giao phải thay hoặc xóa ở màn hình gốc của nó.

Chi tiết đồng bộ Drive xem [3.7](#37-tài-liệu-dự-án-và-google-drive).

#### 3.0.1 Nhóm công việc

Nhóm công việc dùng để giao việc giữa các phòng ban. Mở **Nhóm công việc** (mục riêng trên menu quản trị, ngay dưới **Thông báo**). Có ba tab:

| Tab | Dùng khi |
|---|---|
| **Theo dự án** | Việc của dự án khi chưa có hợp đồng |
| **Theo hợp đồng** | Việc gắn với một hợp đồng của dự án |
| **Công việc nội bộ** | Việc không có khách, dự án hay hợp đồng, ví dụ đào tạo nội bộ |

Nhóm nằm ở tab nào là do nó đã gắn hợp đồng hay chưa. Gắn hợp đồng sau thì nhóm tự chuyển sang tab Theo hợp đồng, dự án giữ nguyên.

**Tạo nhóm công việc**

1. Người phụ trách Cơ hội hoặc PM dự án chọn dự án, phòng ban nhận việc, trưởng nhóm và ngày kế hoạch. Có lối tắt **Giao việc cho phòng ban** trên trang Dự án và Cơ hội.
2. Nhóm theo hợp đồng phải chọn thêm một hợp đồng của dự án đó.
3. Nhóm nội bộ: chọn phòng ban chủ trì và các phòng ban tham gia. Mỗi công việc chọn phòng phụ trách; người thực hiện chỉ lấy từ thành viên đang hoạt động của phòng đó.
4. Chọn **Mục đích nghiệp vụ** nếu nhóm phục vụ báo giá, đấu thầu hoặc pháp lý (xem [3.0.2](#302-công-việc-phòng-ban-trước-hợp-đồng)).

Hồ sơ chuyên môn của Thiết kế và Thi công vẫn làm ở màn hình riêng; nhóm công việc chung chỉ để điều phối. Trên trang chi tiết, **Sửa** đổi được tên, mô tả, ngày kế hoạch; nhóm chung theo dự án đổi được hợp đồng (cùng dự án). Dự án và phòng nhận việc không đổi được. Nút **Quay lại** giữ đúng tab, từ khóa và bộ lọc trước đó.

**Tạo công việc trong nhóm.** Trưởng nhóm phòng nhận việc mở nhóm và tạo công việc: mã WBS, người thực hiện cùng phòng, ngày, công việc tiền nhiệm (tùy chọn), người theo dõi CC. Nếu công việc tính KPI, chọn một định nghĩa KPI đang áp dụng. Mọi công việc theo dự án và mọi công việc có KPI phải có người xác nhận khác người thực hiện; mặc định là trưởng phòng nếu không chọn. Ô chọn tiền nhiệm tự loại các công việc có thể tạo vòng lặp phụ thuộc.

- Đính kèm tối đa 10 tệp mỗi lần (PDF, Word, Excel, JPG, PNG); dung lượng mỗi tệp theo **Cài đặt → Tệp công việc** (mặc định 20 MB). Công việc được lưu trước, tệp tải sau; tệp lỗi được giữ để thử lại.
- Nút **Tệp đính kèm** trên mỗi công việc để xem và tải. Trưởng nhóm hoặc người thực hiện thêm/xóa tệp khi nhóm còn mở; trưởng nhóm vẫn xóa được sau khi đóng nhóm. Tệp công việc hiện lưu trên máy chủ, không vào danh mục Drive.
- **Thêm công việc con** trên công việc chưa xong để chia nhỏ. Công việc cha tự cộng tiến độ và trạng thái từ các việc con; chỉ cập nhật tiến độ ở việc lá. Có thể nhiều cấp. Công việc đang gắn KPI phải bỏ KPI trước khi thêm việc con.

**Thực hiện và xác nhận**

1. Bắt đầu nhóm (chuyển **Đang thực hiện**) trước khi giao người cập nhật tiến độ.
2. Người thực hiện nhận thông báo có liên kết tới công việc, cập nhật tiến độ 1–99%, bấm **Hoàn thành** khi đạt 100%. Công việc có tiền nhiệm chỉ bắt đầu được khi mọi tiền nhiệm đã xong.
3. Công việc theo dự án chuyển **Chờ xác nhận**. Người xác nhận xác nhận, hoặc trả về **Đang thực hiện** kèm lý do. Công việc nội bộ không có KPI thì không cần xác nhận.
4. Chỉ công việc có KPI đạt trạng thái đã xác nhận mới được tính KPI.
5. Đóng nhóm theo dự án khi mọi công việc đã được xác nhận.

| Trạng thái công việc | Ý nghĩa |
|---|---|
| Chưa bắt đầu | Mới tạo |
| Đang thực hiện | Đang làm, tiến độ 1–99% |
| Chờ xác nhận (`Completed`) | Người thực hiện báo xong, chờ người xác nhận |
| Đã xác nhận KPI (`Verified`) | Đã được xác nhận độc lập |
| Đã hủy | Ngừng |

Nhóm công việc đi theo **Dự kiến → Đang thực hiện → Hoàn thành**, hoặc **Đã hủy**. Nhóm chỉ hoàn thành khi mọi công việc đã kết thúc và mọi công việc theo dự án đã được xác nhận.

**Mở lại và xóa nhóm**

- Người quản lý có quyền chọn **Mở lại: Đang thực hiện** trên nhóm Hoàn thành để thêm việc. Kết quả đã xác nhận, KPI đã xác nhận, checklist tài chính đã duyệt vẫn giữ nguyên. Nhóm Đã hủy không mở lại được.
- **Xóa** mở bảng xem trước ảnh hưởng gồm ba nhóm: sẽ xóa, sẽ gỡ liên kết, đang chặn. Gõ đúng mã nhóm để xác nhận. Dự án và hợp đồng cha được giữ; checklist pháp lý/đấu thầu độc lập chỉ bị gỡ liên kết. Phải xử lý trước: bằng chứng giá/tài chính đã dùng, kích hoạt pháp lý, tệp còn lại (xóa tệp công việc trước). Xóa công việc đã xác nhận sẽ làm mất phần đóng góp của nó vào KPI.
- Xóa Dự án thì xóa luôn các nhóm công việc của dự án (có trong bảng xem trước). Xóa Hợp đồng hoặc Cơ hội nguồn chỉ gỡ liên kết; nhóm vẫn thuộc dự án.

#### 3.0.2 Công việc phòng ban trước hợp đồng

Trước khi gửi báo giá hoặc nộp thầu, các phòng ban làm phần việc của mình trong một nhóm công việc của dự án. **Mục đích nghiệp vụ** của nhóm quyết định nó gắn với báo giá hay gói thầu nào và trang hiển thị thao tác gì.

| Mục đích nghiệp vụ | Phòng | Gắn với |
|---|---|---|
| Cung ứng báo giá đầu vào | Cung ứng | Báo giá |
| Tài chính rà soát thương mại | Tài chính – Kế toán | Báo giá đã duyệt |
| Tài chính rà soát hồ sơ thầu | Tài chính – Kế toán | Gói thầu |
| Kế hoạch đấu thầu | Các phòng tham gia gói thầu | Gói thầu |
| Pháp lý và xin phép | Pháp lý | Dự án (xem [3.3](#33-pháp-lý)) |
| Công việc chung | Bất kỳ | Dự án/Hợp đồng |

**Luồng báo giá cho khách**

1. Kinh doanh tạo Báo giá từ một Cơ hội đã gắn Dự án. Hệ thống tạo bản nháp chưa có giá.
2. Trên trang báo giá, chọn **Yêu cầu giá đầu vào** để giao nhóm công việc cho Cung ứng. **Xem yêu cầu giá đầu vào** mở lại nhóm đó.
3. Cung ứng nhập giá đầu vào trong khung **Báo giá đầu vào** (BOQ Excel và/hoặc báo giá nhà cung cấp, thầu phụ) và gửi duyệt.
4. Khi đã có giá đầu vào được duyệt và còn hiệu lực, Kinh doanh chọn **Lập báo giá đầu ra**, chọn phương thức **Suất đầu tư** hoặc **BOQ**, gắn nguồn giá đầu vào, nhập diện tích/đơn giá hoặc áp BOQ rồi lưu.
5. **Gửi duyệt** nội bộ (chỉ bản nháp có giá trị hợp lệ). Chọn người duyệt cho từng bước của Luồng duyệt báo giá (mặc định một Trưởng phòng Cung ứng), xem trước To/CC. Hệ thống chỉ CC thành viên dự án có quyền xem báo giá; chỉ người của bước đang chờ mới duyệt hoặc từ chối được.
6. Sau khi báo giá được duyệt, chọn **Yêu cầu Tài chính rà soát**.
7. Khi Tài chính rà soát đã duyệt, chọn **Gửi khách**.

**Báo giá đầu vào (Cung ứng).** Từ 14/11/2026 khung **Báo giá đầu vào** có hai phần, mỗi phần có nút và danh sách riêng; cách gửi duyệt và duyệt giống nhau.

*Phần trên: BOQ dự án (file Excel)* cho cả công trình.

1. Tải mẫu từ **Thư viện mẫu BOQ** (**Mẫu báo giá → BOQ**), điền khối lượng và đơn giá trong Excel. Giữ tên sheet, dòng tiêu đề và dòng tổng. Dòng không có khối lượng sẽ không thành dòng BOQ.
2. Chọn **Upload BOQ từ Excel**, chọn mẫu, chọn hợp đồng hoặc giữ **Chưa có hợp đồng (BOQ dự thầu)**.
3. Chọn **Kiểm tra file**. Bước này chưa lưu gì. Kết quả hiển thị từng sheet: số hạng mục, dòng bỏ qua, thiếu đơn giá/đơn vị, tổng; hoặc liệt kê lỗi theo sheet, ô và cách sửa.
4. Chọn **Xác nhận tạo phiên bản Nháp**. Mỗi lần upload tạo một phiên bản R1, R2…; bản cũ giữ làm lịch sử. **Tải file BOQ** tải lại đúng file đã upload.
5. Khi đủ đơn giá, chọn **Gửi duyệt**, xem trước email tới trưởng nhóm rồi xác nhận. Bản còn thiếu đơn giá chỉ ở trạng thái Nháp.
6. Trưởng nhóm (hoặc quản trị viên), khác người upload, chọn **Duyệt** hoặc **Từ chối** kèm lý do.

BOQ được duyệt hiển thị **Đã duyệt (dự thầu)**: các phòng xem được, là nguồn dòng cho báo giá BOQ và cũng tính là giá đầu vào đã duyệt. BOQ dự thầu không phải hạn mức vật tư. Không sửa dòng BOQ trên web; muốn đổi thì sửa file và upload phiên bản mới. BOQ upload ở đây không gửi duyệt hay duyệt được từ **Kiểm soát cung ứng**. Nhân viên thấy nhóm công việc khi được giao việc trong nhóm hoặc là thành viên dự án.

*Phần dưới: Báo giá nhà cung cấp / thầu phụ*, nhập tay theo từng nhà cung cấp: nhà cung cấp/thầu phụ, số tham chiếu, tiền tệ, ngày hết hiệu lực và các dòng giá (hạng mục, mã, mô tả, đơn vị, khối lượng, đơn giá, VAT; thành tiền trước VAT tự tính). Nhập số theo kiểu Việt Nam, ví dụ `12.500` và `0,001`.

| Trạng thái | Ý nghĩa |
|---|---|
| Nháp | Đang soạn; sửa hoặc xóa được |
| Chờ duyệt (`Submitted`) | Đã gửi; khóa sửa |
| Đã duyệt (`Approved`) | Dùng được làm giá đầu vào |
| Từ chối (`Rejected`) | Sửa và gửi lại được |

Người duyệt phải khác người lập. Chào giá đã được báo giá tham chiếu thì bị khóa. Báo giá chỉ chọn được giá đầu vào **Đã duyệt**, còn hiệu lực, cùng dự án và cùng nhóm công việc Cung ứng. Khi đã chọn, tổng tiền được chụp lại vào báo giá; sửa giá đầu vào sau đó không đổi giá đã báo.

**Các mẫu BOQ báo giá.** Chỉ điền vào ô cho phép; không sửa công thức và dòng tổng.

| Mẫu | Cần điền | Hệ thống đọc | Tổng BOQ |
|---|---|---|---|
| Báo giá xây dựng nhà xưởng (23 sheet, theo file GNF) | Khối lượng (cột E), đơn giá vật liệu, nhân công, thiết bị (cột F, G, H). Không sửa cột I, J | 20 sheet hạng mục; bỏ qua trang bìa và **00.Tổng Hợp**. Đối chiếu tổng từng sheet với ô tổng đầu sheet | Tổng dòng hạng mục (chi phí trực tiếp); không gồm chi phí gián tiếp và làm tròn |
| Báo giá xây dựng, cơ điện (ME) & PCCC (18 sheet, có khối lượng mẫu) | Sửa khối lượng, điền đơn giá **Vật tư** và **NC&MTC** | Dòng diễn giải không có STT (F1, M1, "Trừ ô cửa"…) không phải hạng mục; điền giá vào dòng không STT sẽ báo lỗi. Sheet **PCCC** ghép quy cách vào tên. Không đọc **TỔNG HỢP** | Tổng các dòng, chưa VAT |
| Báo giá trọn gói thiết kế, xin phép & thi công (21 sheet, có khối lượng mẫu) | Đơn giá **Vật tư**, **Nhân công**, **MMTB** (MEP: Vật tư, Nhân công; Design and permit, Prelim: một cột đơn giá) | 18 sheet; bỏ qua **Cover**, **Total**, **Summary**. Hạng mục không cần STT; dòng kích thước (SL, dài, rộng, cao, KL) không phải hạng mục | Tổng các dòng, chưa VAT |
| Báo giá phần xây dựng (CSA), 3 Bill | Đơn giá **Material** và **Labour & Equipment** (Bill 1 một cột **Price**). Khối lượng Bill 2, 3 tự cộng từ dòng diễn giải ẩn; hạng mục chưa có diễn giải và dòng trọn gói Bill 1 tự nhập | Không đọc bìa, **Total Amount**, **Cost Summary** | Tổng các dòng, chưa VAT 8% |
| Báo giá PCCC căn hộ dịch vụ | Khối lượng, đơn giá | Các sheet chi tiết | Tổng các dòng |
| Báo giá thiết kế và xin phép | Khối lượng, đơn giá (hạng mục miễn phí nhập 0) | Chỉ bảng phí (khảo sát/thiết kế, xin phép) thành dòng BOQ, đặt tên theo dòng đầu của mô tả. Bảng quy mô công trình và dòng VAT giữ trong file | Tổng các dòng |

Với mọi mẫu, đơn giá lưu là tổng các cột đơn giá thành phần; thành tiền lấy theo cột tổng/thành tiền của mẫu.

**BOQ hợp đồng.** Khi trúng thầu và hợp đồng đầu ra đã ký, PM (hoặc BGĐ) mở BOQ dự thầu đã duyệt mới nhất, chọn **Chuyển thành BOQ hợp đồng**, chọn hợp đồng đã ký rồi xác nhận. Bản này giữ nguyên file và dòng, không tạo file mới, và từ đó là hạn mức cho yêu cầu vật tư, RFQ và kho. Mỗi dự án chỉ chuyển một lần. Khi BOQ hợp đồng khác BOQ dự thầu, hoặc có thay đổi sau hợp đồng, Cung ứng/QS chọn **Upload BOQ hợp đồng** ở mục BOQ của trang Dự án, chọn hợp đồng đã ký và file đã điền. Hệ thống tạo phiên bản Nháp; Cung ứng/QS **Gửi duyệt** ở trang chi tiết BOQ, PM **Duyệt**. Việc Cung ứng kiểm soát BOQ đã ký sau hợp đồng là bước riêng, không thay cho báo giá đầu vào.

**Tài chính rà soát thương mại.** Sau khi Kinh doanh duyệt báo giá, Tài chính rà soát điều kiện hợp đồng, bảo lãnh và điều khoản thanh toán: đánh dấu checklist, ghi nhận xét, rồi một người khác duyệt độc lập.

- **Gửi khách** chỉ mở khi báo giá thuộc một Dự án và có rà soát thương mại **Đã duyệt** trong nhóm công việc chưa hủy. Báo giá chưa có dự án sẽ được yêu cầu gắn dự án trước.
- Rà soát gắn với đúng phiên bản báo giá. Sửa giá sau khi duyệt đưa báo giá về **Nháp**; Kinh doanh phải duyệt lại và Tài chính phải rà soát lại.
- **Yêu cầu Tài chính rà soát** tạo nhóm công việc mới nếu chưa có nhóm dùng được, hoặc mở nhóm hiện có. Bản rà soát của phiên bản cũ giữ làm lịch sử.
- Khi ký hợp đồng đầu ra từ báo giá, dùng lại hồ sơ Tài chính đã duyệt của đúng phiên bản báo giá đó.

**Tài chính rà soát hồ sơ thầu.** Tài chính rà soát hồ sơ tài chính của gói thầu trước khi nộp. Gói thầu chỉ chuyển **Đang chuẩn bị → Đã nộp** khi có rà soát này **Đã duyệt** (cùng các điều kiện ở [3.1.5](#315-đấu-thầu)).

Khi gửi duyệt giá đầu vào hoặc rà soát Tài chính, xem kỹ email xem trước trước khi xác nhận (chi tiết ở [12.2](#122-mẫu-email-và-email-gửi-duyệt)).

### 3.1 CRM, Báo giá, Đấu thầu, Hợp đồng

#### 3.1.1 Khách hàng

Lưu khách hàng cá nhân và doanh nghiệp, nhiều người liên hệ cho mỗi khách, thông tin pháp lý (mã số thuế, địa chỉ, người đại diện). Khách hàng liên kết với Lead, Cơ hội, Báo giá, Hợp đồng và Dự án. Trạng thái: tiềm năng, đang giao dịch, đã ký, ngừng giao dịch. Trang chi tiết có tổng quan, người liên hệ, lịch sử chăm sóc (nhắc gọi lại, nhắc thăm) và các bản ghi liên quan.

**Lĩnh vực kinh doanh** (không bắt buộc): chọn từ danh mục 35 lĩnh vực của NICON (dữ liệu chuẩn `business_sector`, quản trị viên chỉnh tập trung), gõ vài chữ để tìm. **Chi tiết ngành nghề** tự nhập, ví dụ "Sản xuất bao bì giấy cho ngành thực phẩm". Danh sách lọc được theo lĩnh vực; **Xuất Excel** có cả hai cột này. Lĩnh vực bị tắt trong danh mục vẫn giữ trên khách hàng đã chọn nó.

Không xóa được khách hàng còn Cơ hội, Gói thầu, Hợp đồng hoặc Dự án độc lập; xử lý từng bản ghi đó trước (xem [3.4.7](#347-xóa-vĩnh-viễn)).

#### 3.1.2 Lead

Ghi nhận lead theo nguồn (marketing, giới thiệu…), giao cho nhân viên kinh doanh, lưu lịch sử tư vấn. Danh sách lọc theo nguồn, trạng thái, người phụ trách, phân khúc.

Mỗi Lead có một phân khúc lấy từ dữ liệu chuẩn `lead_segment`: mặc định Chưa phân loại, Nhà ở, Thương mại, Khách sạn – nghỉ dưỡng. Quản trị viên chỉnh danh mục này tập trung.

**Chuyển Lead thành Cơ hội:** hệ thống tạo (hoặc dùng lại) khách hàng, đồng thời tạo Cơ hội mới và một Dự án ở trạng thái **Lập kế hoạch**. Khảo sát, báo giá, hợp đồng tạo sau đó tự mang dự án này. Chuyển vào một Cơ hội có sẵn thì không tạo dự án. Hoàn tác một lần chuyển đổi vừa làm chỉ xóa dự án tự tạo khi dự án chưa có gì gắn vào.

#### 3.1.3 Cơ hội

Theo dõi giá trị dự kiến (VND), xác suất và giai đoạn. Có màn hình danh sách và bảng Kanban theo giai đoạn.

| Giai đoạn | Giá trị lưu |
|---|---|
| Tiếp cận | `Prospecting` |
| Khảo sát | `Qualification` |
| Báo giá / Đấu thầu | `Proposal` |
| Thương thảo | `Negotiation` |
| Ký hợp đồng | `Won` |
| Thua | `Lost` |

- Cơ hội mới luôn bắt đầu ở **Tiếp cận** và đi lần lượt; không nhảy cóc, không lùi.
- **Thua** chọn được từ bất kỳ giai đoạn đang mở, bắt buộc lý do và ghi chú.
- **Ký hợp đồng** cần một hợp đồng liên kết có ngày ký và không ở trạng thái Nháp hoặc Đã hủy. Nếu bị chặn, kiểm tra hợp đồng, nhờ người phụ trách hợp đồng xem lại rồi thử lại.
- Hợp đồng đủ điều kiện cuối cùng của một Cơ hội đã ký không được hủy, gỡ liên kết hay xóa, trừ khi còn hợp đồng đủ điều kiện khác.
- Dữ liệu cũ đã được chuyển một lần khi triển khai: Won có hợp đồng giữ Ký hợp đồng, Won không có hợp đồng thành Thương thảo, Lost được bổ sung lý do.

Trang chi tiết liệt kê các hợp đồng liên kết (với người có quyền xem); bấm số hợp đồng để mở, tải bản scan đã ký hoặc đổi trạng thái.

**Báo giá từ Cơ hội.** **Tạo Báo giá** có ở mọi giai đoạn đang mở (và ở Ký hợp đồng khi chưa có báo giá thắng). Tab **Báo giá** liệt kê báo giá của cơ hội: trạng thái, phiên bản, tổng tiền, báo giá thắng. Ở trang Báo giá, ô chọn Cơ hội tìm trên toàn bộ dữ liệu theo mã, tên, khách hàng, người phụ trách; không hiện Cơ hội Thua và Cơ hội đã ký có báo giá thắng.

**Thiết kế trước hợp đồng.** Kinh doanh có thể gắn Dự án của cùng khách hàng khi tạo/sửa Cơ hội.

- **Liên kết Dự án vận hành** mở ô chọn dự án. **Tạo Dự án vận hành mới** mở form Dự án với khách hàng đã chọn sẵn; tạo xong quay lại Cơ hội và lưu để xác nhận liên kết. Tạo dự án ở đây cần cả quyền quản lý Cơ hội và quyền quản lý Dự án; PM quản lý được dự án được giao nhưng không tạo được bản ghi bán hàng này.
- **Tạo hồ sơ thiết kế** (cạnh **Tạo Báo giá**, khi Cơ hội chưa Thua): xác nhận dự án và tên hồ sơ rồi lưu. Nếu Cơ hội chưa có dự án, chọn dự án có sẵn hoặc tạo qua **CRM → Dự án**. Lưu sẽ gắn dự án vào Cơ hội và mở một Hồ sơ thiết kế ở giai đoạn Ý tưởng, không cần hợp đồng. Lưu lại lần nữa trả về hồ sơ đã có, không tạo trùng. Nhân sự thiết kế nhận hồ sơ sau khi được phân công vào dự án.
- Tạo trực tiếp từ danh sách Hồ sơ thiết kế cần quyền quản lý Hồ sơ thiết kế, quản lý Cơ hội và quản lý đội ngũ Dự án; vai trò chỉ có quyền Thiết kế không khởi tạo được.

**Tạo hợp đồng từ Cơ hội.** Với Cơ hội chưa Thua, **Tạo hợp đồng** mở hợp đồng nháp kèm khách hàng và dự án. Không bắt buộc báo giá khi khách đã đồng ý giá trị. Nếu có báo giá đã duyệt còn dùng được, form điền sẵn báo giá và giá. Dự toán gói thầu không phải là báo giá CRM.

#### 3.1.4 Báo giá

| Trạng thái | Ý nghĩa |
|---|---|
| Nháp | Đang soạn |
| Chờ duyệt nội bộ | Đã gửi người duyệt |
| Đã duyệt | Duyệt nội bộ xong; chờ Tài chính rà soát rồi gửi khách |
| Đã gửi khách | Đã gửi email cho khách |
| Khách đã duyệt | Khách chấp nhận (kết thúc) |
| Từ chối | Bị từ chối nội bộ hoặc khách từ chối |
| Hết hạn | Quá ngày hiệu lực |
| Đã huỷ | Ngừng |

Luồng tạo và duyệt xem [3.0.2](#302-công-việc-phòng-ban-trước-hợp-đồng). Trang chi tiết có lịch sử phiên bản, so sánh phiên bản, duyệt nội bộ và **Tải Excel báo giá**. File Excel báo giá có bản Việt, Anh, Trung, Nhật; giữ thông tin khách hàng, Cơ hội, dòng giá (BOQ hoặc suất đầu tư), chiết khấu, VAT và tổng cuối dưới dạng số. Bấm lưu lại khi mạng lỗi không tạo báo giá trùng.

**Phương thức Suất đầu tư**

1. Tạo báo giá theo phương thức **Suất đầu tư**, nhập diện tích, chọn bảng đơn giá đang áp dụng và một ngày nằm trong phiên bản đã duyệt.
2. Hệ thống tự tính tổng đơn giá trên m². Chỉ bảng loại Suất đầu tư dùng được cho phương thức này.
3. Muốn ghi đè giá phải có quyền và nhập lý do bằng tiếng Việt; lý do hiện trong các phiên bản và file Excel.

Nếu chưa có bảng đơn giá nào đang áp dụng, form hiển thị ba bước thiết lập và liên kết tới trang **Mẫu báo giá → Suất đầu tư**; trang đó có nút quay lại đúng báo giá cho đến khi phiên bản được duyệt. Người không có quyền sẽ được hướng dẫn liên hệ QS hoặc trưởng phòng kinh doanh.

**Lập bảng Suất đầu tư** (**Mẫu báo giá → Suất đầu tư**):

1. Tạo phiên bản Nháp.
2. **Tải biểu mẫu Excel**, điền sheet **Entry**, giữ tên và thứ tự cột: mã/tên vật tư, đơn vị, định mức/m², đơn giá, % hao hụt. File có sẵn định dạng số, công thức thành tiền và sheet hướng dẫn; không cần xóa ví dụ hay đổi sang CSV.
3. Nhập lại chính file `.xlsx` đó vào phiên bản Nháp. Nhập theo kiểu tất cả hoặc không: lỗi một dòng thì không dòng nào được lưu. CSV UTF-8 vẫn dùng được.
4. Thành tiền = định mức/m² × đơn giá × (1 + % hao hụt / 100).
5. Trong phiên bản Nháp có thể thêm, sửa, xóa từng dòng; tổng tự tính lại.
6. Duyệt phiên bản. Các phiên bản đã duyệt không được chồng thời gian hiệu lực. Phiên bản Đã duyệt, Từ chối, Ngừng áp dụng chỉ xem.

Bảng đơn giá cũ (chưa cấu hình trường) có thể chọn nhiều bảng để xóa vĩnh viễn, mất mọi phiên bản và dòng. Bảng đã được báo giá dùng thì không xóa được; bảng không xóa được vẫn được chọn và có lý do kèm theo.

**Phương thức BOQ.** Báo giá BOQ lấy dòng và tạm tính từ BOQ dự án (Cung ứng upload, trưởng nhóm duyệt, xem 3.0.2).

1. Trong khung **BOQ dự án** của báo giá, xem phiên bản đã duyệt mới nhất (dự thầu hoặc hợp đồng), số dòng và tổng trước VAT.
2. Chọn **Áp dụng BOQ này**; lưu sẽ chép mọi dòng.
3. Chiết khấu, VAT và hạn hiệu lực nhập trên báo giá.

- Không thêm, sửa hay gõ dòng trên báo giá. Muốn đổi khối lượng hoặc giá, QS upload phiên bản BOQ mới; sau khi duyệt, Kinh doanh áp dụng lại. Nếu báo giá đã duyệt hoặc đã gửi, việc này tạo phiên bản báo giá mới.
- Không gửi duyệt được khi dự án có BOQ đã duyệt mới hơn bản báo giá đang dùng; khung BOQ hiện cảnh báo.
- Dự án chưa có BOQ đã duyệt: khung hiện liên kết tới trang dự án.
- Báo giá cũ lập từ bảng đơn giá hoặc dòng gõ tay vẫn xem được, nhưng phải áp dụng BOQ dự án trước khi lưu hoặc gửi duyệt lại.

**Mẫu báo giá → BOQ** là thư viện chỉ đọc các mẫu BOQ theo định dạng của khách. Mỗi mẫu là một thư mục, mỗi sheet là một mục con; chọn sheet để xem nhóm, hạng mục, dòng tổng. **Tải mẫu Excel** trả về đúng file mẫu. Trang không có phiên bản hay nhập liệu. Cung ứng và Trưởng phòng Cung ứng mở được trang này. Các màn hình bảng đơn giá BOQ cũ không còn trên menu; dữ liệu cũ vẫn giữ.

**Gửi khách.** Chọn **Gửi khách**, kiểm tra hoặc nhập email khách, xem tiêu đề và nội dung, rồi gửi. Email có dòng giá, tổng tiền và đính kèm file Excel báo giá. Sửa tiêu đề/nội dung cần quyền quản lý báo giá; gửi cần quyền gửi báo giá.

**Xóa báo giá** bị chặn khi có hợp đồng tham chiếu; bảng ảnh hưởng chỉ ra hợp đồng đó.

#### 3.1.5 Đấu thầu

**Ghi nhận thư mời thầu:** ngày nhận (trước hạn nộp), nguồn, người nhận, và nếu có thì số tham chiếu của khách và đường dẫn cổng thầu. Tệp thư mời tải lên ở trang chi tiết gói thầu.

| Trạng thái | Ý nghĩa |
|---|---|
| Đang chuẩn bị | Đang làm hồ sơ |
| Đã nộp | Đã nộp hồ sơ; chờ kết quả |
| Trúng / Trượt | Kết quả; chỉ ghi được từ Đã nộp |
| Đã huỷ | Ngừng |

**Checklist hồ sơ thầu**

- Mặc định có 7 dòng chuẩn; bản vẽ dự thầu là tùy chọn. Có thể thêm dòng riêng cho gói thầu, đặt dòng nào bắt buộc theo thư mời, hoặc đánh dấu dòng tùy chọn **Không áp dụng** kèm lý do. Chỉ xóa được dòng riêng còn trống; dòng chuẩn giữ lại để đối chiếu.
- Người có quyền quản lý đấu thầu giao từng dòng cho một người dùng đang hoạt động có quyền đấu thầu, đặt hạn nội bộ từ hôm nay đến ngày nộp. Người được giao nhận thông báo (lưu lại cùng người không gửi thông báo trùng).
- Dòng **Hồ sơ năng lực** phải chọn tài liệu có sẵn trong thư viện **Hồ sơ năng lực** dùng chung. Dòng riêng của dự án (BOQ, biện pháp thi công, bản vẽ dự thầu) thì tải tệp trực tiếp.
- Chỉ đánh dấu **Xong** hoặc **Đã nộp** khi đã có bằng chứng. Người đánh dấu Đã nộp phải khác người làm dòng đó. Tệp xem trước ngay trong trang; mọi lần thay tệp đều giữ trong lịch sử.
- Khung **Mức độ sẵn sàng** liệt kê từng điều kiện còn chặn và cách xử lý.

**Kế hoạch đấu thầu.** Tab này dùng nhóm công việc với mục đích Kế hoạch đấu thầu: tạo một nhóm cho mỗi phòng phụ trách, đặt người làm, người xác nhận độc lập, ngày bắt đầu/kết thúc, phụ thuộc và tiến độ. Ngày của nhóm và công việc phải trước hạn nộp thầu. Thời gian khoảng một tháng NICON nêu chỉ là hướng dẫn, hệ thống không chặn. Công việc kế hoạch thầu được xác nhận mà không cần gắn KPI.

**Dự toán dự thầu.** Tab có mẫu CSV UTF-8: mã hạng mục, mô tả, đơn vị, khối lượng, đơn giá nội bộ, đơn giá dự thầu, VAT, ghi chú. Mỗi lần nhập hợp lệ tạo một phiên bản Nháp, tính tạm tính chi phí, tạm tính dự thầu, VAT và tổng giá dự thầu. Gửi duyệt; người có quyền duyệt hoặc từ chối kèm lý do.

**Nộp thầu (Đang chuẩn bị → Đã nộp)** cần đủ:

1. Có ít nhất một nhóm Kế hoạch đấu thầu đang hoạt động, và mọi nhóm đã Hoàn thành sau khi các công việc được xác nhận độc lập.
2. Mọi dòng checklist bắt buộc đã sẵn sàng với đúng loại bằng chứng.
3. Có dự toán đã duyệt.
4. Gói thầu thuộc một Dự án và có **Tài chính rà soát hồ sơ thầu** đã duyệt.
5. Người nộp có quyền nộp thầu riêng (ghi kết quả Trúng/Trượt là quyền khác).

Khi nộp, ghi kênh nộp và số biên nhận (nếu có). Hệ thống lưu một bản chụp không sửa được gồm đúng các phiên bản bằng chứng, dự toán, kế hoạch, rà soát Tài chính, người nộp và thời điểm. Nếu bên mời thầu yêu cầu bổ sung, thêm phiên bản bằng chứng mới và tạo lần nộp bổ sung; các bản chụp và thời điểm nộp đầu không đổi. Sau Trúng, Trượt hoặc Đã huỷ, checklist bị khóa.

**Ghi Trúng thầu.** Chọn **Tạo cơ hội từ gói thầu này** (mặc định) hoặc một Cơ hội đang mở của cùng khách hàng và dự án. Cả hai cách đưa Cơ hội về **Thương thảo**. Cơ hội tạo mới mang tên gói thầu, giá trị bằng dự toán đã duyệt mới nhất và dùng dự án của gói thầu. Khung kết quả có liên kết tới Cơ hội và nút tiếp tục thương thảo và tạo hợp đồng; tạo hợp đồng vẫn là thao tác riêng sau thương thảo.

Mã gói thầu `TD-YYYY-NNNN` tiếp nối mã lớn nhất đang dùng, nên xóa gói thầu không ảnh hưởng việc tạo mới.

**Tỷ lệ trúng thầu.** Khung phía trên danh sách tính số gói trúng chia cho số gói nộp lần đầu trong kỳ. Chọn khoảng ngày nộp (giờ Việt Nam, tính cả hai đầu), Khách hàng, Người lập, rồi **Áp dụng**; mặc định là năm hiện tại. Lần nộp bổ sung chỉ tính một lần; gói chờ kết quả hoặc hủy sau khi nộp vẫn nằm trong mẫu số. Ví dụ 3 trúng / 10 nộp = 30%, dù 5 gói còn chờ kết quả. Kỳ không có gói nộp hiện trạng thái trống, khác với 0%.

- **Xem các gói được tính** lọc danh sách bên dưới theo kỳ. **Bỏ lọc đối chiếu** bỏ lọc kỳ và người lập. **Làm mới** tải lại báo cáo đã áp dụng.
- Kết quả dùng kết quả hiện tại, nên kỳ cũ có thể thay đổi khi có kết quả mới.
- Thông báo màu vàng cho biết các gói thiếu bằng chứng nộp lần đầu; các gói này bị loại đến khi được xác minh.

#### 3.1.6 Khảo sát

Ghi khảo sát gắn với Cơ hội hoặc Dự án: chụp ảnh, quay video ngay trong ứng dụng, tải tệp, ghi chú kỹ thuật, thời điểm và tọa độ. Tab **Điều kiện** ghi lộ giới, cao độ, tình trạng hạ tầng bằng form hoặc nhập CSV UTF-8 (tất cả hoặc không). Bề rộng lối vào và cao độ nền tính bằng mét. File PDF khảo sát gồm mọi điều kiện đã ghi.

- Quyền: có quyền xem khảo sát và quyền quản lý khảo sát riêng. Mặc định Kinh doanh có cả hai, PM chỉ xem.
- Mỗi khảo sát thuộc một Dự án; Cơ hội, Dự án và Khách hàng phải khớp nhau. Tệp lưu ở thư mục `01_Khao_sat` của dự án. Đổi dự án thì tệp được chuyển sang dự án mới.
- Trạng thái đồng bộ Drive hiển thị trên từng tệp (xem [3.7](#37-tài-liệu-dự-án-và-google-drive)).

#### 3.1.7 Hợp đồng

**Sổ hợp đồng** (**CRM → Sổ hợp đồng**) là nơi duy nhất để tạo, sửa, chuyển trạng thái, đính kèm tài liệu và phụ lục, và xóa hợp đồng. Màn hình Tài chính không tạo bản hợp đồng thứ hai: **Theo dõi phải thu** lọc hợp đồng đầu ra (Upstream, với khách hàng), **Theo dõi phải trả** lọc hợp đồng đầu vào (Downstream, với nhà cung cấp/thầu phụ).

**Tạo hợp đồng**

1. Chọn **Tạo hợp đồng** từ Cơ hội, từ báo giá đã duyệt, hoặc tạo mới trong Sổ hợp đồng.
2. Số hợp đồng được đề xuất theo dạng `HD-YYYY-NNNN`. Có thể sửa theo quy ước bên ngoài (duy nhất, tối đa 40 ký tự). Nếu giữ số đề xuất, số chính thức được cấp khi lưu nên có thể nhảy nếu có người vừa tạo hợp đồng khác.
3. Chọn Dự án của cùng khách hàng. Hợp đồng tạo từ Cơ hội hoặc báo giá dùng dự án của nguồn. Khách chỉ có một dự án thì hệ thống tự chọn; không có hoặc có nhiều dự án thì phải chọn. Chưa có dự án phù hợp thì chọn **Tạo dự án mới** dưới ô dự án (cần quyền quản lý dự án).
4. Hợp đồng khách hàng có 7 phạm vi: Thiết kế, Xin phép, Thi công và 4 tổ hợp của chúng.
5. Lưu. Mọi hợp đồng mới bắt đầu ở **Bản nháp**.

**Gắn báo giá.** Hợp đồng đầu ra không bắt buộc báo giá khi khách đã đồng ý giá trị. Nếu dùng báo giá, báo giá phải ở **Đã duyệt**, **Đã gửi khách** (còn hiệu lực) hoặc **Khách đã duyệt**; cùng khách hàng, cùng Cơ hội (nếu có) và cùng dự án. Một báo giá chỉ dùng cho một hợp đồng đang hiệu lực. Lưu hợp đồng có báo giá nguồn mà chưa chọn Cơ hội thì hệ thống tự gắn Cơ hội của báo giá; Cơ hội khác sẽ bị từ chối. Hợp đồng đã có dùng **Gắn báo giá đã duyệt** ở mục Nguồn. Hợp đồng đầu vào không gắn báo giá khách hàng. Dự toán gói thầu không phải báo giá CRM.

**Trạng thái hợp đồng**

| Trạng thái | Chuyển bằng | Điều kiện |
|---|---|---|
| Bản nháp | — | Trạng thái khi tạo |
| Đã ký | **Đánh dấu Đã ký** | Đã tải **Bản scan hợp đồng đã ký** ở tab **Tài liệu**; có ngày ký |
| Đang thực hiện | **Chuyển Đang thực hiện** | Có ngày ký. Với hợp đồng thiết kế/thi công/D&B, PM hoặc Admin hoàn tất kích hoạt pháp lý ([3.3](#33-pháp-lý)) |
| Tạm dừng | **Tạm dừng** / **Tiếp tục thực hiện** | — |
| Hoàn thành | **Hoàn thành** | — |
| Đã huỷ | **Huỷ hợp đồng** | — |

- Bản scan phải có loại tài liệu là bản scan hợp đồng đã ký và thuộc đúng hợp đồng này; tài liệu hỗ trợ không thay được. Thiếu bản scan thì ứng dụng mở tab Tài liệu và hợp đồng giữ Bản nháp. Hợp đồng đã ký trước đây mà chưa có bản scan vẫn giữ trạng thái; hãy bổ sung trước khi triển khai.
- Ngày phải theo thứ tự ký → bắt đầu → kết thúc. Sửa thông tin chung không bỏ qua được các kiểm tra trạng thái.
- Tệp hợp đồng và phụ lục có giới hạn dung lượng ở **Cài đặt → Tệp hợp đồng** (mặc định 100 MB, quản trị viên nâng được tới 5 GB).
- Tab lịch sử hợp đồng nằm sau các tab làm việc.

**Đợt thanh toán.** Lập các đợt tạm ứng, theo giai đoạn, quyết toán. Đổi trạng thái đợt và kế toán phụ trách bằng thao tác trên đợt; sửa lịch vẫn giữ đợt và lịch sử. Đợt đã xử lý hoặc đã có đề nghị thanh toán không xóa được. Khi đợt thu của hợp đồng đầu ra chưa thu tới hạn, người phụ trách hợp đồng và kế toán được giao nhận một thông báo; đổi hạn thì có nhắc mới.

**Tiến độ thực hiện hợp đồng** (tab thông tin) có ba phần riêng, không gộp thành một % hoàn thành:

- **Công việc**: nhóm công việc và công việc gắn trực tiếp, trong phạm vi bạn được xem. Hoàn thành và đã xác nhận đếm riêng; nhóm đã hủy không tính.
- **Nghiệm thu / QS**: hồ sơ QS theo chiều của hợp đồng, gồm lịch sử, chờ CM duyệt, đã duyệt hiệu lực. Bản bị thay thế hoặc đảo không tính.
- **Thanh toán**: trạng thái lịch và đợt theo giá trị hợp đồng gốc, không phải tiền thực thu/chi. **Xem lịch thanh toán** mở tab thanh toán.

Phần xem trước hiện tối đa 10 nguồn gần nhất; tổng tính mọi bản ghi bạn xem được. Nguồn bị giới hạn quyền hiển thị thông báo thay vì số 0. Dùng **Làm mới** hoặc **Thử lại** khi lỗi.

**Lead nguồn** (hợp đồng đầu ra) hiển thị các Lead đã chuyển thành Cơ hội nguồn, ngày chuyển theo giờ Việt Nam, chỉ trong phạm vi bạn được xem Lead. Hợp đồng tạo thẳng không có Cơ hội thì không truy được nguồn; hệ thống không đoán Lead từ khách hàng hay dự án.

**Chăm sóc khách hàng** (hợp đồng đầu ra, tab **Chăm sóc khách hàng**)

- Ghi cuộc gọi, email, cuộc họp, ghi chú (1–4.000 ký tự). Cần quyền quản lý hợp đồng và là người phụ trách hoặc quản lý CRM. Mới nhất ở trên. Lưu lỗi thì nội dung được giữ.
- Ghi chú này tách khỏi Timeline và lịch sử khách hàng; không đổi điều khoản, không duyệt VO, không đánh dấu thanh toán. Tài chính đọc được trong phạm vi hợp đồng của mình.
- Sau ghi chú đầu tiên, hoặc sau khi email nhắc đầu tiên bắt đầu gửi (kể cả khi chưa rõ kết quả), không đổi được chiều Upstream/Downstream của hợp đồng.

**Email nhắc gia hạn / kết thúc tự động**

1. Trong tab Chăm sóc khách hàng, chọn Nhắc gia hạn hoặc Thông báo kết thúc, chọn ngày gửi (từ hôm nay, lịch Việt Nam).
2. **Xem trước email**, chọn người liên hệ có email hợp lệ (người liên hệ chính được gợi ý trước).
3. Xác nhận để lên lịch.

Điều kiện: hợp đồng đầu ra, có ngày kết thúc, ở trạng thái Đã ký, Đang thực hiện hoặc Tạm dừng. Mỗi loại chỉ có một lịch chờ; muốn thay thì hủy lịch cũ trước. Hệ thống kiểm tra mỗi 5 phút. Email không tự gia hạn hay chấm dứt hợp đồng. Mẫu email sửa ở `/admin/email-templates`, áp dụng cho lịch mới. Nếu thông tin hợp đồng, ngày kết thúc, người nhận hoặc quyền thay đổi, phải xem trước lại. Gửi thành công qua SMTP thêm một mục Email vào lịch sử chăm sóc, nhưng không chứng minh khách đã nhận. Hệ thống không tự gửi lại; nếu không chắc đã gửi, hỏi quản trị mail trước khi lên lịch email khác. Mục kẹt ở Đang gửi sau sự cố cần kiểm tra vận hành. Màn hình liệt kê 100 lịch gần nhất.

**Tái ký / phát triển hợp đồng.** Trong tab Chăm sóc khách hàng của hợp đồng đầu ra đủ điều kiện:

- **Lập hợp đồng tiếp nối** khi đã thỏa thuận giá trị mới.
- **Theo dõi cơ hội tái ký / phát triển** khi cần thương thảo trong CRM.

Hợp đồng nguồn phải là hợp đồng đầu ra có ngày ký, ở Đã ký, Đang thực hiện, Tạm dừng hoặc Hoàn thành; không cần chờ hết hạn, và một hợp đồng có thể có nhiều lần tiếp nối. Chọn Tái ký hoặc Kinh doanh mới, nhập lý do (1–2.000 ký tự). Cơ hội tiếp nối bắt đầu ở Tiếp cận. Hợp đồng mới không chép ngày ký, thanh toán, VO, QS, tệp đã ký, email nhắc hay lịch sử chăm sóc của hợp đồng cũ, và không dùng lại ngầm báo giá cũ. Khách hàng giữ nguyên; chọn dự án của khách đó hoặc tạo mới. Hợp đồng mới là Bản nháp riêng; hợp đồng nguồn không đổi. Nếu nguồn thay đổi trước khi lưu, dùng **Cập nhật hợp đồng nguồn** rồi lưu lại; dữ liệu đã nhập vẫn giữ. Cơ hội Thua không tạo được hợp đồng; khách quay lại thì tạo Cơ hội mới. Hợp đồng và Cơ hội tiếp nối không xóa vĩnh viễn được. Đổi điều khoản của hợp đồng đang có thì dùng phụ lục. Nếu Cơ hội đến từ gói thầu trúng, mục Nguồn của hợp đồng hiện mã và tên gói thầu.

**Danh sách và tổng hợp.** Sổ hợp đồng lọc theo dự án, khách hàng, trạng thái, chiều, loại, khoảng ngày ký, khoảng giá trị; sắp theo số, ngày ký, ngày kết thúc, giá trị, cập nhật cuối. Xuất CSV gồm mọi dòng khớp bộ lọc trên tất cả các trang. Trang Dự án hiện giá trị hiện tại của hợp đồng đầu ra và đầu vào (không tính hợp đồng hủy, có cộng VO đã duyệt) và tiến độ thanh toán (tiền đợt đã thanh toán / tổng tiền các đợt), bấm vào để xem chi tiết. Xem tổng hợp theo quyền dự án; mở chi tiết hợp đồng cần thêm quyền hợp đồng.

**Kế toán** chỉnh lịch thu/chi, kế toán phụ trách, trạng thái đề nghị và ngày thu/chi thực tế. Không sửa được thông tin chính, trạng thái pháp lý, tài liệu, VO, trừ khi có thêm quyền CRM tương ứng.

#### 3.1.8 Tiến độ tổng dự án

Trong chi tiết Dự án, người có quyền quản lý dự án lập tiến độ tổng dạng WBS các mốc có ngày.

1. Lưu tạo một phiên bản nháp.
2. Chọn người duyệt và gửi. Trước khi ký hợp đồng đầu ra, người duyệt phải có vai trò `SALES_MANAGER` và quyền quản lý Cơ hội. Sau khi ký, chỉ PM của dự án được duyệt.
3. Người duyệt **Duyệt và phát hành** hoặc trả lại kèm lý do. Phiên bản đã phát hành trước đó vẫn hiệu lực đến khi bản mới được duyệt.

- Khi thêm hạng mục, hệ thống gợi ý mã WBS theo thứ tự trong cây; người lập sửa được trước khi gửi duyệt. Mã bắt buộc, tối đa 80 ký tự, không trùng trong cùng phiên bản (không phân biệt hoa thường). Đổi mã không tạo hạng mục mới; lịch sử giữ mã cũ. Quy tắc mã WBS chuẩn và mẫu WBS theo loại dự án còn chờ NICON chốt.
- Sau khi phát hành, công việc phòng ban, tiến độ thiết kế và công việc Gantt thi công chọn được mốc WBS. Ngày kế hoạch phải nằm trong ngày của mốc. Đề xuất nguồn lực, hồ sơ QA/QC và đợt QS cũng chọn một hạng mục của tiến độ đã phát hành. Lịch phòng ban không tự đổi ngày khi tiến độ tổng đổi.
- Không phát hành được nếu một công việc liên kết bị lệch ra ngoài ngày WBS mới, hoặc mốc nó tham chiếu bị xóa.
- Khi phiên bản phát hành thay đổi, người thực hiện bị ảnh hưởng nhận thông báo kèm ngày mới; quản lý dự án và phòng ban rà lại ngày công việc. Email thay đổi có đủ phần chênh lệch WBS.
- Trước khi gửi duyệt hoặc phát hành, xem email thực tế của từng người nhận rồi chọn **Tiếp tục**. Đổi người nhận/nội dung, hoặc quá 15 phút, phải xem trước lại.
- Email lỗi được gửi lại tự động; sau 5 lần thất bại chuyển `NeedsReview` và PM nhận cảnh báo. PM hỏi người nhận đã nhận chưa rồi chọn **Đã nhận** hoặc **Gửi lại** ở mục kiểm tra email của dự án (mỗi lỗi chỉ quyết một lần). Lỗi từ máy gửi không chứng minh chưa nhận được. Mẫu email sửa ở `/admin/email-templates`, tab **Tiến độ tổng thể** (dùng dữ liệu minh họa); quản trị viên cũng xem các dự án có email cần kiểm tra ở đây.

### 3.2 Thiết kế

Mục tiêu: kiểm soát hồ sơ kỹ thuật, tránh sai lệch trước khi thi công. Menu **Thiết kế → Hồ Sơ Thiết Kế**. Hồ sơ có các tab: Tổng quan, Thiết kế ý tưởng, Thiết kế cơ sở, Thiết kế chi tiết, Phát hành IFC, Đội ngũ, Tiến độ, Tài liệu, Trình duyệt. Tab đang mở được giữ trên URL, nên tải lại trang hay bấm quay lại vẫn đúng tab.

#### 3.2.1 Hồ sơ thiết kế, đội ngũ và tiến độ

**Tạo hồ sơ.** Hồ sơ thiết kế chỉ được tạo bằng thao tác chủ động, kể cả trước khi có hợp đồng (thiết kế trước, ký sau). Chuyển hợp đồng sang Đang thực hiện không tự tạo hồ sơ. Trong **Tạo hồ sơ thiết kế**, chọn Dự án hoặc một hợp đồng đã thuộc dự án. Ô Hợp đồng chỉ liệt kê hợp đồng ký với khách hàng đã chọn (không có đơn mua hàng với nhà cung cấp), mỗi dòng ghi đủ khách hàng và dự án; chọn hợp đồng thì ô Khách hàng tự điền theo hợp đồng. Hồ sơ tạo từ Cơ hội vẫn là hồ sơ đó khi dự án có thêm hợp đồng; tạo từ hợp đồng không sinh hồ sơ trùng. Kinh doanh cần quyền quản lý Cơ hội, quản lý Dự án và quản lý đội ngũ dự án để khởi tạo (xem [3.1.3](#313-cơ-hội)).

**Nhiều luồng thiết kế trong một dự án.** Một Dự án có thể có nhiều Hồ sơ thiết kế. Mở đúng hồ sơ trước khi sửa Đội ngũ hoặc Tiến độ: thành viên, quan hệ báo cáo, người thực hiện, người duyệt, CC thuộc riêng hồ sơ đó. Là thành viên một hồ sơ không có quyền vào hồ sơ khác.

**Vai trò**

| Vai trò | Việc |
|---|---|
| Chủ nhiệm thiết kế (`DESIGN_MANAGER`) | Chịu trách nhiệm hồ sơ cấp phòng; tạo đợt trình duyệt nội bộ, tổng hợp ý kiến, ghi nhận trình khách và quyết định của khách. Mỗi hồ sơ có một người |
| Chủ trì thiết kế (Design Lead) | Phụ trách nhóm; thường là người duyệt công việc. Có thể nhiều người, trong đó một Chủ trì chính |
| Thành viên thiết kế | Thực hiện công việc được giao |
| PM dự án | Quản lý Dự án chung; khác Chủ nhiệm thiết kế |

Chủ nhiệm và Chủ trì được giao mở được hồ sơ kể cả khi không phải PM dự án. Gán hoặc gỡ hai vai trò này ở **Đội ngũ** sẽ cập nhật tên trên Tổng quan và form hồ sơ, và ngược lại; vai trò cũ còn trong lịch sử. Hệ thống chặn thêm Chủ nhiệm thứ hai. Gỡ Chủ trì chính thì hệ thống tự chọn một Chủ trì khác còn hiệu lực, hoặc để trống nếu không còn ai.

**Tab Đội ngũ**

- Thêm nhân sự trước khi giao việc. Vai trò mới tự thuộc phân hệ Thiết kế. Vai trò của dự án chung vẫn hiển thị và không bị đổi ở đây.
- Kết thúc thành viên tại đây chỉ được khi mọi vai trò của người đó thuộc Thiết kế. Nếu người đó còn vai trò dự án chung, dùng **Chỉnh sửa thành viên** để gỡ vai trò thiết kế; muốn kết thúc hẳn thì làm ở màn hình đội ngũ của Dự án.
- Số thành viên đếm phân công hiện tại; phân công đã kết thúc đếm riêng bên dưới.
- Ô chọn người phụ trách ở Ý tưởng, Cơ sở, Chi tiết chỉ hiện thành viên đang hoạt động.
- **Quyền trên hồ sơ:** người phụ trách hồ sơ mở hộp quyền của thành viên để cấp Xem, Tạo, Sửa, Xóa riêng từng quyền. Cấp quyền ghi thì giữ quyền Xem. Thành viên lưu xong phải tải lại trang hồ sơ để dùng quyền mới. Chủ nhiệm và Chủ trì chính luôn đủ quyền (khóa, có giải thích). Quyền Xóa không cho xóa vĩnh viễn hồ sơ lỗi, không cho duyệt hay phát hành IFC. Lịch sử quyền ghi quyền đã lưu tại thời điểm đó, không phải so sánh cũ/mới.
- Bình luận phân công giữ nội dung chưa gửi khi gửi lỗi. Xóa bình luận có hộp xác nhận.

**Tab Tiến độ**

- Mỗi công việc có một người thực hiện nội bộ NICON, một người duyệt độc lập (thường là Chủ trì) và CC. CC chỉ được biết, không quyết định.
- Việc thuê ngoài: chọn cả nhà cung cấp và hợp đồng đầu vào loại Thiết kế/Thầu phụ của nhà cung cấp đó, cùng dự án. Thiếu một trong hai sẽ bị từ chối.
- **Hoàn thành** tự gửi công việc cho người duyệt. Sửa thông thường không đánh dấu xong được. Công việc chờ duyệt hoặc đã duyệt không sửa được; công việc bị trả lại thì sửa và gửi lại. Người thực hiện và người duyệt phải còn phân công trong dự án khi gửi hoặc duyệt.
- CC đã ngừng hoạt động vẫn hiện kèm nhãn và nút gỡ; gỡ trước khi lưu. Không thêm được người ngừng hoạt động vào việc mới.
- Công việc có thể gắn mốc WBS của tiến độ tổng dự án ([3.1.8](#318-tiến-độ-tổng-dự-án)).

#### 3.2.2 Thiết kế ý tưởng

Quản lý mô hình 3D, mặt bằng công năng, ảnh trình bày; ghi ý kiến khách hàng; quản lý phương án và phiên bản; trình khách duyệt.

1. Mở **Hồ sơ và ý kiến** của một phương án. Khung mở toàn chiều rộng bên dưới danh sách phương án, có hai phần **Hồ sơ** và **Ý kiến khách hàng và phản hồi**.
2. Ở **Hồ sơ**, tải lên: mô hình 3D gốc, phối cảnh, mặt bằng tổng thể/mái, mặt bằng các tầng (ghi tên tầng), mặt cắt, PDF trình bày. Ảnh và PDF xem ngay trong hệ thống (PDF có chuyển trang); mô hình 3D chỉ tải về. Checklist hiện tài liệu đã gắn, ngoại lệ đã ghi và phần còn thiếu, kèm trạng thái đồng bộ từng tệp. Mục nào không áp dụng thì ghi lý do, nhưng vẫn phải có mô hình 3D hoặc phối cảnh thật. Mọi tệp trình phải thuộc đúng phương án, đúng revision và đã đồng bộ. Gắn nhầm thì gỡ tệp hoặc ngoại lệ khỏi bản nháp; tệp gốc vẫn còn trong Tài liệu dự án.
3. Trình khách. Phiên bản đã trình bị khóa.
4. Sau khi trình, ghi nguyên văn ý kiến khách, người đại diện, kênh, thời điểm và bằng chứng (biên bản, email, tài liệu). Ghi phản hồi và kết quả xử lý trước khi trình phiên bản thay thế R01 trở đi. Nút tạo phiên bản sửa chỉ hiện khi lần trình gần nhất cho phép.
5. **Chốt phương án** chỉ mở khi khách đã duyệt và đã có biên bản nghiệm thu ký. Ở tab Trình duyệt, chọn tệp TK-M03 đã ký (đã đồng bộ, tách riêng), nhập người ký phía khách và ngày ký, rồi chốt đúng phương án đã duyệt.

Kinh doanh trình khách theo ISO; Chủ nhiệm ghi nhận việc trình và quyết định. Biên bản TK-M02 không thay TK-M03. Khách không cần đăng nhập hay ký qua cổng, và không cần trình xem 3D chuyên dụng để chốt. Chốt một phương án thì các phương án còn lại bị loại. Bỏ một phương án cần xác nhận; tài liệu đã trình và lịch sử vẫn giữ. Số lần trình nội bộ được ghi riêng với số revision của phương án.

#### 3.2.3 Thiết kế cơ sở

Bản vẽ phục vụ xin phép xây dựng, thuyết minh kỹ thuật, phân theo bộ môn Kiến trúc, Kết cấu, MEP; có phiên bản, tài liệu pháp lý liên quan, tải lên và xem trước tệp.

**Điều kiện mở Thiết kế chi tiết** (hiển thị trên tab):

1. Mỗi bộ môn Kiến trúc, Kết cấu, MEP có ít nhất một hồ sơ đã duyệt nội bộ.
2. Tạo đợt trình duyệt Thiết kế cơ sở từ các hồ sơ đã đồng bộ đó và ghi quyết định khách đã duyệt ở **Trình duyệt**.

**Mở khoá Thiết kế chi tiết** bị khóa đến khi lần trình gần nhất phủ đủ ba bộ môn. Dùng **Mở Trình duyệt** để làm bước còn thiếu.

#### 3.2.4 Thiết kế chi tiết

Bản vẽ thi công chi tiết theo bộ môn (Kiến trúc, Kết cấu, MEP, Nội thất), theo hạng mục công trình, có phiên bản, trình duyệt và rà soát chéo. Hệ thống quản lý thông tin bản vẽ, trạng thái, lý do revision và hồ sơ phát hành IFC; tệp tải lên, xem trước, tải về được và hiện ở tab **Tài liệu**. Bản vẽ được liên kết tới công việc thi công ở bước thi công ([3.4.1](#341-tiến-độ-thi-công)).

#### 3.2.5 Trình duyệt và quyết định của khách

Có hai nơi duyệt cho hai quyết định khác nhau:

1. **Tiến độ**: người thực hiện hoàn thành, người duyệt duy nhất của công việc (thường là Chủ trì) duyệt hoặc trả lại.
2. **Trình duyệt**: chỉ Chủ nhiệm thiết kế tạo đợt trình nội bộ và tổng hợp ý kiến. Người nhận được chỉ định xem bằng chứng và góp ý, không duyệt.
3. Chủ nhiệm ghi nhận đã trình khách và quyết định của khách. Đây không phải duyệt lại từng công việc.

- Trong tab Trình duyệt, chọn revision mới nhất; đợt trình dùng đúng tệp của revision đó.
- Người nhận góp ý chung cho cả hồ sơ, hoặc chọn một tài liệu dự án đính kèm trong đợt trình; không gắn được tệp khác của dự án.
- Thông báo trình duyệt mở thẳng đợt trình được giao, kể cả với người ngoài đội ngũ. Trang này không cho vào tab hay tài liệu khác của dự án.
- Chủ nhiệm chọn người nhận theo từng người, theo phòng ban, hoặc cả hai. Chọn phòng ban thì hệ thống thêm các thành viên đang hoạt động được phép nhận hồ sơ trình (trừ chính Chủ nhiệm); phòng không còn ai đủ điều kiện sẽ bị từ chối. Mặc định Kinh doanh, Trưởng phòng Kinh doanh, PM, BGĐ và các vai trò thiết kế được nhận. Quản trị viên có thể cho một vai trò tùy chỉnh nhận hồ sơ trình bằng quyền **Nhận hồ sơ trình nội bộ Thiết kế**; quyền này không mở các hồ sơ khác.
- Quyết định của khách: Khách đã duyệt, Khách yêu cầu sửa, hoặc Từ chối; hai quyết định sau bắt buộc ghi lý do.
- Tải tệp từ đợt trình sẽ kiểm tra tệp còn khớp với bản đã trình. Nếu đã đổi, hệ thống chặn và giải thích; liên hệ Chủ nhiệm để kiểm tra phiên bản. Bản đã trình không bị thay tự động.
- Bản nháp chưa trình không thay được quyết định của khách đã ghi. Nếu một đợt khác đã trình cùng revision, bản nháp còn lại chỉ để tham khảo, nút trình bị khóa.
- Ngày ký hiển thị theo lịch của trình duyệt.

**Khi Trình duyệt báo lỗi quyền:** kiểm tra tài khoản Chủ nhiệm thiết kế có vai trò `DESIGN_MANAGER` rồi tải lại dự án. Người không có tên trong đợt trình phải nhờ Chủ nhiệm thêm vào; thử lại không tự cấp quyền.

#### 3.2.6 Quản lý revision

Theo dõi lịch sử revision bản vẽ, tạo revision mới kèm lý do thay đổi, so sánh các phiên bản, đánh dấu bản đang hiệu lực, thu hồi hoặc khóa bản cũ, thông báo cho các bộ phận liên quan.

#### 3.2.7 Phát hành IFC

Trình duyệt bản vẽ trước khi phát hành, đóng dấu điện tử "Issued For Construction", khóa bản vẽ đã phát hành, phân phối cho đội thi công và giám sát, lưu lịch sử phát hành.

- Chỉ phát hành IFC khi revision Shop Drawing mới nhất đã được khách duyệt. Thi công chỉ dùng IFC đã phát hành. Hủy đợt phát hành không làm mất lịch sử.
- Bản vẽ có tệp chính thì đúng tệp đó phải khớp bằng chứng khách đã duyệt. Tệp phụ được duyệt không thay cho tệp chính. Nếu bị chặn, thông báo chỉ rõ bản vẽ; khôi phục hoặc tải lại tệp chính, hoàn tất duyệt của khách rồi thử lại. Đợt phát hành vẫn ở Nháp.

**Gửi IFC cho Cung ứng** (đã triển khai):

1. Khi đợt IFC còn Nháp, Thiết kế chọn người nhận Cung ứng trong số thành viên dự án đang hoạt động có quyền xem IFC của Cung ứng.
2. Sau khi phát hành, người đó mở **Cung ứng → Hồ sơ IFC nhận** của dự án, xem bản vẽ và chọn **Xác nhận đã nhận IFC** (cần quyền xác nhận IFC). Hệ thống ghi người và thời điểm xác nhận đầu tiên; Thiết kế không xác nhận thay được.

Xác nhận này không phải duyệt thiết kế và không tạo RFQ hay đề nghị mua. Chỉ đợt đã phát hành gửi cho mình mới hiện. Rời dự án hoặc mất quyền thì mất quyền xem. Bản vẽ khác với bản đã phát hành sẽ bị chặn tải; liên hệ Thiết kế kiểm tra. Người nhận bên ngoài (ghi tên) vẫn theo cách theo dõi cũ.

#### 3.2.8 Xóa hồ sơ thiết kế

- Hồ sơ đã trình khách thì không xóa vĩnh viễn được, kể cả khi khách từ chối. Sửa bằng cách ghi quyết định của khách và tạo revision thay thế.
- Chỉ BGD hoặc SUPER_ADMIN xóa vĩnh viễn được hồ sơ thất bại/đã hủy: mở bảng ảnh hưởng, xử lý mọi vướng mắc về tệp, IFC, đợt trình dùng chung, rồi gõ đúng mã hồ sơ. Bảng ảnh hưởng hết hạn khi dữ liệu liên quan thay đổi; làm mới rồi thử lại.
- Không xóa hàng loạt Shop Drawing; mỗi hồ sơ phải xem và xác nhận riêng.

**Chưa có:** xem 3D trực tiếp, video walkthrough, ghi chú trên bản vẽ, xuất gói IFC, xem DWG trực tiếp (xem trước tùy loại tệp hỗ trợ), nhập đầy đủ MS Project.

### 3.3 Pháp lý

Menu **Pháp lý** (`/admin/permits`).

#### 3.3.1 Kích hoạt pháp lý và checklist

Tạo Hồ sơ thiết kế không tạo giấy phép. Khi hợp đồng đầu ra loại Thiết kế, Thi công hoặc Design & Build chuyển từ **Đã ký** sang **Đang thực hiện**, PM hoặc Admin phải hoàn tất trình hướng dẫn kích hoạt pháp lý trên trang hợp đồng:

1. Kiểm tra hợp đồng và Dự án.
2. Chọn một mẫu checklist pháp lý đã xuất bản theo phạm vi và phiên bản. Mục bắt buộc không bỏ chọn được; chọn thêm mục tùy chọn và loại giấy phép khác nếu cần. Chưa có mẫu đã xuất bản thì không xác nhận được; nhờ Pháp lý/CM soạn và duyệt độc lập một mẫu.
3. Chọn trưởng nhóm Pháp lý, người thực hiện, người duyệt độc lập, ngày và giấy phép tiền nhiệm cho từng giấy phép. Người thực hiện và người duyệt phải là thành viên đang hoạt động của dự án, có thể khác phòng.
4. Xem lại và xác nhận. Trước khi xác nhận thành công, không có gì được tạo và hợp đồng chưa đổi trạng thái.

Lần xác nhận đầu tạo một nhóm công việc Pháp lý cho Dự án. Hợp đồng sau của cùng dự án dùng lại nhóm này, không tạo trùng giấy phép. Dự án cũ hoặc hợp đồng đã chạy dùng **Thiết lập Pháp lý** trong màn hình Pháp lý.

- Giấy phép đã có vẫn chọn được làm tiền nhiệm khi thêm giấy phép mới. Thiết lập phải dựa trên phiên bản mới nhất của nhóm. Đổi ngày hoặc trưởng nhóm được ghi lịch sử.
- Nhóm Hoàn thành hoặc Đã hủy không nhận thay đổi thiết lập. Hợp đồng khác có thể dùng lại nhóm Hoàn thành đã áp mẫu mà không đổi mẫu, trưởng nhóm, ngày, công việc; nhóm Đã hủy không dùng lại được.
- Dòng giấy phép thuộc nhóm Pháp lý không tạo, tạo lại hay xóa riêng được. Đổi phân công và lịch qua nhóm công việc.

**Mẫu checklist pháp lý** (mở rộng mục này trong màn hình Pháp lý):

1. Pháp lý/Admin soạn mã, tên và phạm vi bằng 4 ngôn ngữ (Việt, Anh, Trung, Nhật), chọn loại giấy phép theo thứ tự và đánh dấu mục bắt buộc.
2. Người duyệt CM/Admin kiểm từng mục và phạm vi, nhập số tham chiếu duyệt của CM/NICON, chọn **Duyệt và xuất bản**. Người soạn/sửa không tự xuất bản được, kể cả Admin.
3. Mẫu đã xuất bản không sửa được. **Soạn phiên bản mới** để sửa; **Ngừng áp dụng mới** giữ nguyên các dự án đã dùng.

Mẫu ví dụ trong môi trường phát triển vẫn là Nháp, không phải ma trận pháp lý NICON đã duyệt. PM chọn mẫu thủ công; hệ thống không tự suy loại hoặc quy mô công trình. Dự án cũ chưa có mẫu vẫn dùng được; PM/Admin có thể áp mẫu qua **Thiết lập Pháp lý**: tệp, tiến độ, lịch cũ giữ nguyên, mục còn thiếu được tạo hoặc liên kết. Mẫu đã áp không đổi hoặc miễn được trong bản này. Mục bắt buộc trong checklist không tự động là điều kiện cho mọi giai đoạn thi công.

Khi gặp lỗi dữ liệu cũ, làm mới để xem bản mới nhất (dữ liệu đã nhập được giữ). Trong trình soạn mẫu, xem định nghĩa hiện tại và xác nhận phiên bản mới nhất trước khi lưu lại.

#### 3.3.2 Theo dõi giấy phép

Theo dõi giấy phép xây dựng (GPXD), PCCC, cấp điện, cấp nước, vỉa hè, môi trường, an toàn, hoàn công và các loại đã cấu hình: trạng thái, cơ quan cấp, người phụ trách, ngày nộp, ngày cấp, hạn, hạn mục tiêu, ghi chú. Thẻ rủi ro và bộ lọc làm nổi giấy phép quá hạn, sắp đến hạn, sắp hết hiệu lực. Màn hình lọc theo dự án, loại, trạng thái, người thực hiện, người duyệt, hạn; có lịch sử.

| Trạng thái với cơ quan | Điều kiện |
|---|---|
| Chưa chuẩn bị, Đang chuẩn bị | — |
| Đã nộp, Đang thẩm định, Bổ sung hồ sơ | Đã tải bộ hồ sơ nộp và ghi ngày nộp |
| Đã cấp | Đã tải giấy phép và ghi ngày cấp |
| Bị từ chối, Hết hạn | — |

Trạng thái với cơ quan và trạng thái công việc nội bộ tách nhau. Chỉ khi giấy phép **Đã cấp**, người thực hiện mới gửi công việc liên kết là Hoàn thành. Người duyệt xác nhận hoặc trả lại kèm lý do bắt buộc; trả lại công việc không đổi trạng thái với cơ quan.

### 3.4 Thi công và Nghiệm thu

Mục tiêu: quản lý công trường, kiểm soát chất lượng, tiến độ và an toàn từng giai đoạn.

#### 3.4.0 Phân vai Phòng CM và Ban chỉ huy công trường

NICON tách việc giữa **Phòng CM** ở văn phòng và **Ban chỉ huy công trường (BCH)** tại công trình. Mỗi dự án có BCH riêng; một người có thể tham gia nhiều dự án.

**Menu Thi công**

| Khu vực | Mục |
|---|---|
| **Ban chỉ huy công trường** | **Vận hành công trường** (đề xuất nguồn lực, nhật ký công trường); **Hồ sơ QA/QC công trường** (hồ sơ QA/QC, punchlist, nghiệm thu từng phần, HSE); **QS công trường** |
| **Phòng CM** | **Tiến độ**; **Khối lượng và chi phí**; **Chất lượng**; **An toàn**; **Hợp đồng** |

| BCH tại công trường | Phòng CM |
|---|---|
| Đề xuất vật tư, máy móc, nhân công; so với hạn mức BOQ | Duyệt cấp 2 đề xuất vượt hạn mức hoặc ngoài kế hoạch trước khi chuyển Cung ứng |
| Nhật ký hằng ngày: thời tiết, nhân lực, thiết bị, tiến độ, sự cố, ảnh | Đối chiếu tiến độ với S-Curve kế hoạch; yêu cầu giải trình khi chậm |
| Hồ sơ QA/QC theo bản vẽ IFC; xử lý punchlist | Kiểm tra bằng chứng QA/QC, kiểm tra đột xuất, giám sát punchlist quá hạn |
| Ghi nhận HSE và vi phạm | Rà soát tuân thủ HSE, xử lý vi phạm |
| Đo khối lượng đầu ra (nghiệm thu với CĐT/TVGS) và đầu vào (thanh toán thầu phụ/tổ đội) | Duyệt cấp 2 cả hai chiều QS, thẩm định VO |

**Quy tắc chung**

- Phòng CM làm việc trên chính hồ sơ BCH lập, không giữ bộ hồ sơ thứ hai. Mỗi khu vực CM hiện mọi trạng thái, không chỉ hàng chờ duyệt.
- CM tạo được hồ sơ thay BCH, và sửa/gửi/xóa được bản nháp hoặc bản bị từ chối do BCH lập nếu có quyền duyệt của phân hệ. BCH chỉ làm trên bản nháp của mình.
- **Tách người lập và người duyệt:** người lập đề xuất vật tư, QS, hồ sơ QA/QC, vi phạm HSE hoặc VO không được tự duyệt độc lập hồ sơ đó. Nút Duyệt, Từ chối, Xác nhận, Đóng, Hoàn thành chỉ hiện với người duyệt, kể cả khi người lập là người của CM.
- Chỉ xóa hẳn bản nháp và bản bị từ chối; hồ sơ đã duyệt/đóng thay đổi bằng thao tác trạng thái.
- Bản nháp hoặc dữ liệu chưa đồng bộ không được tính là bằng chứng duyệt hay KPI.

| Vai trò | Làm | Không được |
|---|---|---|
| PM Phòng CM | Công bố baseline; duyệt QA/QC, tiến độ, HSE, yêu cầu vật tư, QS, VO trên các dự án được giao | Tự duyệt hồ sơ mình lập |
| Chỉ huy trưởng | Điều hành công trường được giao; gửi và duyệt việc nội bộ BCH | Duyệt cấp 2 thay CM; xem toàn danh mục |
| Kỹ sư hiện trường / QA-QC | Nhật ký, bằng chứng chất lượng, kiểm tra theo IFC, xử lý punchlist | Duyệt QS, mở khóa ngân sách, duyệt thay CM |
| QS công trường | Lập đợt nghiệm thu khối lượng đầu ra/đầu vào; upload và gửi BOQ gốc của dự án | Duyệt thanh toán cuối, tự duyệt đợt QS của mình, duyệt BOQ |
| Cán bộ HSE | Ghi kiểm tra và vi phạm của công trường | Xóa vi phạm đã xác nhận, đóng hành động khắc phục của CM |
| Thủ kho | Nhập, xuất, cấp phát vật tư đã duyệt | Duyệt đề xuất nguồn lực, đổi hạn mức BOQ |
| Cung ứng / Kế toán | Xử lý phần đã duyệt chuyển sang phân hệ mình | Sửa bằng chứng thi công, bỏ qua bước duyệt của CM |
- Mọi lần chuyển trạng thái ghi người, thời điểm, ý kiến và phiên bản được xem.
- Dự án, khu vực và tab đang chọn được giữ trên URL.
- Hồ sơ chất lượng đồng bộ vào thư mục Drive `04_Thi_cong_Nghiem_thu`; hệ thống vẫn là nơi quyết định trạng thái, quyền và lịch sử.

**Giai đoạn chuẩn bị (sau khi ký hợp đồng D&B/thi công)**

1. Lập WBS, Gantt thi công, S-Curve kế hoạch, mốc và đường găng.
2. Lập tổ chức BCH: Chỉ huy trưởng, Kỹ sư hiện trường/QA-QC, QS công trường, Cán bộ HSE, Thủ kho.
3. Lập kế hoạch vật tư, máy móc, nhân công theo mốc; phối hợp Cung ứng chọn nhà cung cấp/thầu phụ.
4. Lập và khóa Ngân sách cơ sở (vật tư, nhân công, máy, chi phí BCH). Thay đổi sau đó cần duyệt, có lịch sử, và có VO nếu đổi phạm vi hoặc giá trị hợp đồng.

Ký hợp đồng mới chỉ bắt đầu giai đoạn chuẩn bị, chưa cho phép thi công. Hệ thống kiểm tra **Điều kiện khởi công** và lưu kết quả kèm người kiểm tra: IFC đã phát hành, giấy phép đã cấp và còn hạn, tiến độ cơ sở đã phát hành, kế hoạch BCH/nguồn lực đã duyệt, Ngân sách cơ sở đã khóa. Bàn giao mặt bằng, biện pháp thi công và kế hoạch HSE chỉ bắt buộc khi CM đánh dấu bắt buộc cho phiên bản chuẩn bị đó (danh mục ISO của NICON chưa duyệt).

**Bằng chứng QA/QC.** Nhập mã từng tài liệu dự án đang hoạt động và phân loại: biên bản nghiệm thu, mẫu, kết quả thí nghiệm, chứng chỉ vật liệu, nhật ký công trường, khác. Mỗi tài liệu chỉ xuất hiện một lần trong một hồ sơ. Hệ thống không đoán loại từ tên tệp. Tài liệu phải có sẵn trong Tài liệu dự án. Mẫu QA/QC của dự án đã xuất bản và IFC đã phát hành vẫn áp dụng. Mẫu ISO NICON mới gửi chưa được CM duyệt thành checklist bắt buộc theo loại dự án.

**Chuyển Cung ứng (một phần).** Mở **Kiểm soát cung ứng**, chọn dự án và **Máy và nhân công đã duyệt**, lọc theo loại nguồn lực hoặc trạng thái cung ứng.

1. Ở **Dòng hợp đồng**, người có quyền xem hợp đồng mở **Sổ hợp đồng** của dự án (lối tắt này không cấp thêm quyền).
2. Cung ứng soạn hợp đồng đầu vào: chọn nhà cung cấp, loại, giá trị. Bản nháp không tự gắn hay tự ký.
3. Gắn hợp đồng vào đề xuất đã duyệt và lưu. Người quản lý hợp đồng có quyền hoàn tất ký. Nhiều đề xuất dùng chung một hợp đồng được; mỗi đề xuất một hợp đồng. Đổi hợp đồng chỉ trước khi xác nhận cung ứng.
4. Chọn một tài liệu dự án làm bằng chứng để xác nhận cung ứng. Hợp đồng chưa ký, tạm dừng, hủy hoặc đóng không cho xác nhận mới.

Xác nhận cung ứng không duyệt QS hay thanh toán, không tạo đề nghị thanh toán. Bằng chứng đang được tham chiếu không xóa được.

**Kiểm soát CM theo hồ sơ nguồn.** Mở từ khu vực CM hoặc báo cáo dự án. Tách các hồ sơ QS đầu ra/đầu vào, QA/QC, HSE đã duyệt đang hiệu lực, có xem chi tiết theo quyền. Đây là tình trạng hiện tại, không phải ảnh chụp lịch sử hay tổng khối lượng; mỗi QS đã duyệt hiện số tiền và kỳ riêng. So sánh ngân sách chưa có.

**Chưa triển khai (mục tiêu):**

- Ngưỡng cảnh báo BOQ trên yêu cầu vật tư: vàng 85%, cam 95%, đỏ trên 100% (đỏ chặn đặt hàng, cần VO). Chưa áp dụng đến khi chốt cách tính.
- Cảnh báo đỏ khi chậm quá 5% so với S-Curve, có thể tái lập.
- Điều kiện thanh toán đầu vào: hợp đồng đầu vào đã ký + phiếu nhập kho hợp lệ (khi có vật tư) + QS đầu vào đã duyệt = đủ điều kiện để Kế toán xem xét. Đủ điều kiện không có nghĩa tự thanh toán; bằng chứng thiếu, lệch, bị đảo hoặc thay thế sẽ chặn. Ma trận chứng từ cho 4 loại gói và các khoản tạm ứng/đề nghị trước nhận hàng chờ FA/CM chốt.
- Ba cổng mobile cho BCH, làm việc offline, mẫu ISO theo loại dự án, tự chuẩn bị khi ký hợp đồng.
- Theo làm rõ của NICON ngày 18/10/2026: danh mục CM gồm tiến độ, khối lượng (đầu ra/đầu vào tách riêng), giá trị thực hiện so dự toán, hồ sơ QA/QC và an toàn; sau khi CM duyệt máy/nhân công, Cung ứng ký và cấp cho công trường, FA thanh toán. Mẫu lấy từ bộ ISO NICON đã gửi (`docs/Nicon/ISO/`); chứng từ thanh toán còn thiếu chuyển FA. Các yêu cầu này đã xác nhận nhưng chưa có đầy đủ trên hệ thống.

Công việc thi công, nhật ký, punchlist, nghiệm thu từng phần, hoàn công và bàn giao hiện vẫn gắn với Hồ sơ thiết kế, chưa chuyển sang Dự án.

#### 3.4.1 Tiến độ thi công

Mở **Phòng CM → Tiến độ**. Chọn Dự án (tìm theo mã hoặc tên; lựa chọn giữ trên URL).

- **Trước khi có kế hoạch CM:** danh sách các bước chuẩn bị chỉ ra điều kiện CRM/WBS còn thiếu (phát hành tiến độ tổng, liên kết công việc với WBS, trách nhiệm CM/PM/công trường) và liên kết tới tiến độ tổng và các công việc chưa liên kết. Tiến độ tổng còn nháp chưa phải kế hoạch đã phát hành.
- **Lập kế hoạch CM:** người có quyền quản lý chuẩn bị CM tạo phiên bản, giao Chỉ huy trưởng cập nhật thực tế, xem trước email duyệt và gửi PM duyệt độc lập. PM được giao duyệt hoặc từ chối; các phiên bản đã phát hành trước giữ trong lịch sử. Ô chọn phiên bản hiện khi đã có phiên bản lưu.
- **Cập nhật thực tế BCH:** Chỉ huy trưởng được giao cập nhật % của công việc lá WBS. Khi chênh lệch kế hoạch–thực tế vượt ngưỡng cấu hình, Chỉ huy trưởng phải nhập giải trình, kế hoạch khắc phục và hạn; CM ghi đánh giá riêng. Lưu lỗi thì số đã nhập vẫn còn để thử lại.
- **S-Curve:** dùng trọng số WBS (theo phạm vi, khối lượng hoặc độ phức tạp; không cần BOQ) và lịch làm việc đã khóa theo phiên bản. Ngày thiếu báo cáo hiện "chưa có", không tính là 0%. Biểu đồ tháng/năm dùng số cuối kỳ. **Xem số liệu biểu đồ** hiện dạng bảng. **Cách tính tiến độ và cảnh báo** giải thích cách tính và ngưỡng. **Chi tiết kế hoạch và lịch làm việc** hiện lý do kế hoạch và lịch đã khóa.
- **Điều kiện khởi công:** mở rộng trong Tiến độ. Mỗi mốc cơ sở đi kèm tài liệu chứng minh; yêu cầu bổ sung và kết quả kiểm tra (chỉ đọc) tách riêng. Lưu bằng thanh thao tác phía trên, rồi đánh giá. Việc khởi công vẫn do máy chủ kiểm tra.
- Danh sách công việc thi công gồm WBS, giai đoạn, người phụ trách, ngày, % thực tế, phụ thuộc, cảnh báo quá hạn.

Màn hình không lưu thao tác offline để đồng bộ sau. Gantt kéo thả, đổi độ dài và phóng to/thu nhỏ là mục tiêu.

**Bản vẽ thi công.** Mở một công việc ở **Phòng CM → Tiến độ → Công việc**, tìm mục **Bản vẽ thi công**. Người có quyền quản lý công việc chọn tối đa 200 bản vẽ IFC đã phát hành của cùng dự án rồi **Lưu liên kết bản vẽ** (lưu riêng, không đụng ngày/tiến độ). Bỏ chọn chỉ gỡ liên kết. Danh sách hiện mã, tên bản vẽ, đợt IFC và revision lúc phát hành; xem trước được nếu có tệp. Không có bản vẽ để chọn nghĩa là Thiết kế chưa phát hành IFC. Khi xung đột, làm mới mục bản vẽ và chọn lại. Chưa có tự thay revision.

#### 3.4.2 Nhật ký công trường

Mở **Ban chỉ huy công trường → Vận hành công trường → Nhật ký công trường**. Ghi thời tiết, nhân lực, thiết bị, vật tư, khối lượng hoàn thành, sự cố, ghi chú nội bộ.

| Trạng thái | Ý nghĩa |
|---|---|
| Nháp | Sửa được |
| Đã gửi | Chờ xác nhận |
| Đã xác nhận | Đã xác nhận |

Muốn sửa nhật ký Đã gửi hoặc Đã xác nhận thì mở lại về Nháp. Người có quyền quản lý xóa vĩnh viễn được nhật ký ở mọi trạng thái (xóa từng cái hoặc nhiều cái); luôn có xác nhận và không hoàn tác được. Tải ảnh từ camera trên mobile chưa có.

#### 3.4.3 Nghiệm thu từng phần

Mở **Ban chỉ huy công trường → Hồ sơ QA/QC công trường → Nghiệm thu từng phần**. Tìm, lọc, sắp xếp theo dự án, công việc thi công, người phụ trách, ngày nghiệm thu, trạng thái. Hồ sơ gồm thành phần tham gia, kết quả, đường dẫn tài liệu/ảnh.

Trạng thái: Bản nháp → Chờ duyệt → Đã duyệt / Bị từ chối, hoặc Đã huỷ. Bị từ chối thì sửa và gửi lại; Đã duyệt và Đã huỷ bị khóa. Không có quyền xem tất cả thì chỉ thấy hồ sơ liên quan tới phân công dự án, công việc mình phụ trách, mình tạo hoặc tham gia duyệt.

#### 3.4.4 Bàn giao

Mở **Phòng CM → Chất lượng → Bàn giao dự án**. Hiện mỗi Hồ sơ thiết kế có một hồ sơ bàn giao; không tạo hồ sơ thứ hai cho cùng một dự án nghiệp vụ. Danh sách lọc theo dự án, người phụ trách, ngày dự kiến, trạng thái, mức độ sẵn sàng; xuất CSV.

Tạo/sửa hồ sơ ở trạng thái Nháp hoặc Mở lại: ngày dự kiến, người phụ trách, kết quả chạy thử, checklist, liên kết tài liệu và người ký. Liên kết phải là HTTP(S) hoặc đường dẫn nội bộ bắt đầu bằng một `/`; liên kết không an toàn chỉ hiện dạng chữ.

**Sẵn sàng bàn giao** được tính từ dữ liệu dự án, cần đủ:

1. Ít nhất một nghiệm thu từng phần đã duyệt.
2. Ít nhất một danh mục hoàn công bắt buộc đã cấu hình, và mọi danh mục bắt buộc đã duyệt. Chưa cấu hình danh mục bắt buộc thì luôn chưa sẵn sàng; quản trị viên phải cấu hình.
3. Không còn punchlist chưa xử lý.
4. Chạy thử đã xong.
5. Mọi mục checklist bàn giao đã xong.

| Trạng thái | Ý nghĩa |
|---|---|
| Bản nháp | Đang chuẩn bị |
| Sẵn sàng bàn giao | Đủ điều kiện; quản lý có thể trả về Nháp hoặc hủy |
| Đã bàn giao | Hoàn tất; khóa đến khi mở lại |
| Mở lại | Mở để sửa có kiểm soát |
| Đã huỷ | Ngừng |

Hoàn tất bàn giao cần quyền hoàn tất bàn giao và ít nhất một người ký; hệ thống ghi ngày bàn giao thực tế, người thao tác và lịch sử. Nếu người khác vừa sửa, tải lại rồi làm lại.

Quyền bàn giao tách thành: quyền mở màn hình bàn giao; quyền xem và xuất mọi dự án; quyền tạo, sửa, xóa trong phạm vi dự án của mình; quyền sửa mọi dự án; quyền hoàn tất. Xem tất cả không có nghĩa sửa tất cả. Người phụ trách không đổi được người phụ trách, trừ khi cũng là PM hoặc Chủ trì thiết kế. Không có quyền xem tất cả thì chỉ thấy hồ sơ mình tạo, mình phụ trách, hoặc dự án mình quản lý/chủ trì.

#### 3.4.5 Hồ sơ hoàn công

Mở **Phòng CM → Chất lượng → Hồ sơ hoàn công**. Tìm, lọc, sắp xếp theo dự án, danh mục, trạng thái; xuất CSV toàn bộ danh sách đã lọc. Thẻ tổng hợp hiện mức đủ danh mục bắt buộc của dự án.

Trạng thái: Bản nháp → Chờ duyệt → Đã duyệt → Đã lưu trữ, hoặc Đã huỷ. Đã duyệt và Đã lưu trữ bị khóa. Tên tài liệu không được trùng trong một dự án. Tự tổng hợp bản vẽ hoàn công và xuất gói hồ sơ là mục tiêu.

#### 3.4.6 Punchlist

Ghi lỗi tại công trường: mã, mô tả, vị trí (gõ chữ), mức độ, nguyên nhân, người thiết kế liên quan, người xử lý, hạn, cách xử lý, xác nhận, số lần mở lại.

Trạng thái: **Mở → Đang xử lý → Đã sửa → Đã xác nhận**, hoặc Đã huỷ; có thể mở lại. **Trễ hạn** hiện khi quá hạn. Ảnh/video đính kèm riêng và ghim lỗi trên mặt bằng là mục tiêu.

#### 3.4.7 Xóa vĩnh viễn

Áp dụng cho mọi bản ghi gốc (dự án, khách hàng, hợp đồng, hồ sơ…):

1. Cần quyền và phạm vi phù hợp.
2. Mở bảng ảnh hưởng mới nhất. Bảng chia ba nhóm: sẽ xóa, sẽ gỡ liên kết và giữ lại, đang chặn (phải xử lý trước).
3. Gõ đúng mã bản ghi để xác nhận.

- Khách hàng bị chặn xóa khi còn Cơ hội, Gói thầu, Hợp đồng, Dự án; Cơ hội bị chặn khi còn Báo giá. Xử lý từng bản ghi qua luồng riêng của nó; hệ thống không xóa dây chuyền.
- Tệp thuộc dự án phải xóa ở phân hệ sở hữu trước khi xóa dự án. Dọn tệp có thể chạy nền; chờ trạng thái Hoàn thành rồi mới coi là đã xóa. Lỗi sở hữu hoặc dọn tệp lỗi cần xử lý rồi thử lại.
- Nhật ký thao tác vẫn giữ.

Chi tiết kỹ thuật: [Permanent Aggregate Deletion](application_developer.md#77-permanent-aggregate-deletion).

### 3.5 Cung ứng

Các màn hình theo dự án nằm trong **Cung ứng → Kiểm soát cung ứng**: chọn Dự án rồi chọn tab.

#### 3.5.1 Nhà cung cấp / Thầu phụ

Mở **Cung ứng → Nhà cung cấp / Thầu phụ** (`/admin/vendors`). Tìm theo mã, tên công ty, mã số thuế, người liên hệ, điện thoại, email, ngành nghề; lọc theo loại và trạng thái hoạt động; xuất toàn bộ kết quả đã lọc. Người có quyền quản lý tạo, xem, sửa, xóa.

- Cần ít nhất một số điện thoại hoặc email hợp lệ. Mã và tên công ty không trùng.
- Trang chi tiết hiện thông tin công ty, liên hệ, tài liệu, trạng thái, người tạo/sửa cuối, hợp đồng và dự án liên kết, đánh giá nhà cung cấp, lịch sử thay đổi. Hồ sơ năng lực tải lên và xem trước được; hủy form thì bỏ tệp vừa tải, tệp bỏ dở tự hết hạn sau 24 giờ.
- Nhà cung cấp cần giữ cho báo cáo lịch sử thì chuyển **ngừng hoạt động**. Chỉ xóa bản ghi lỗi thời, trùng hoặc thử nghiệm.
- Xóa cần xem bảng ảnh hưởng và gõ đúng mã. Bị chặn khi còn hợp đồng, đánh giá, đề nghị thanh toán, tệp dùng chung.

#### 3.5.2 RFQ và so sánh giá

Mở **Cung ứng → RFQ & so sánh giá**. Từ **Cung ứng → Hợp đồng** của dự án có **Mở RFQ và so sánh báo giá** và **Sổ hợp đồng** (lọc hợp đồng đầu vào của dự án); các lối tắt này không cấp thêm quyền.

1. Cung ứng chọn dòng và khối lượng từ BOQ dự án đã duyệt, mời nhà cung cấp đang hoạt động và phát hành RFQ.
2. Nhà cung cấp có email nhận đường dẫn cổng báo giá có hạn, gửi báo giá (và bản sửa) đến hạn chót; tải được tài liệu đính kèm RFQ. Cung ứng có thể nhập báo giá thay nhà cung cấp. Báo giá giữ tiền tệ, tỷ giá nhập tay sang VND, cước, chiết khấu, VAT và lịch sử sửa. Thiếu giá khác với giá 0; báo giá thiếu dòng chỉ tham gia ở dòng có giá; nhà cung cấp ngừng hoạt động không được đánh dấu giá thấp nhất.
3. Khi đánh giá, Cung ứng ghi điểm thương mại và bằng chứng. Bảng so sánh kết hợp giá, thời gian giao và trọng số đánh giá nhà cung cấp. Điểm chỉ để tham khảo; chọn bên điểm thấp hơn phải ghi lý do.
4. BGĐ hoặc vai trò được giao quyền trao thầu phân bổ từng dòng và khối lượng cho một hay nhiều nhà cung cấp. Khối lượng cung cấp vật tư phải tham chiếu yêu cầu vật tư đã duyệt; thầu phụ thì không. Hệ thống chặn phân bổ thiếu hoặc vượt nhu cầu, rồi tạo một hợp đồng đầu vào Nháp cho mỗi nhà cung cấp được chọn. Giá trị và dòng đã trao được khóa; ký, kho, phải trả đi theo luồng riêng.

Ô tải tệp hiện giới hạn số tệp và dung lượng; tệp lỗi giữ lại để **Thử lại** hoặc bỏ, tệp đã tải không tải lại. Quy tắc chi tiết: [RFQ and Supplier Comparison](application_developer.md#718-rfq-and-supplier-comparison).

#### 3.5.3 BOQ dự án

Từ 13/11/2026, Cung ứng upload BOQ trong nhóm công việc yêu cầu giá đầu vào và trưởng nhóm duyệt ở đó (các bước ở [3.0.2](#302-công-việc-phòng-ban-trước-hợp-đồng)). Mục **BOQ dự án** dưới tiến độ tổng của trang Dự án chỉ liệt kê các phiên bản và trạng thái; phòng có quyền mở được từng phiên bản. **Kiểm soát cung ứng** không có nút upload.

- Trang phiên bản liệt kê các sheet và tổng; **Tải file BOQ** để xem chi tiết.
- BOQ dự thầu đã duyệt (**Đã duyệt (dự thầu)**) chỉ để đọc, không phải hạn mức vật tư. Chỉ BOQ gắn với hợp đồng đã ký mới là hạn mức cho yêu cầu vật tư, RFQ và kho (xem BOQ hợp đồng ở 3.0.2).
- BOQ liên kết với tiến độ thi công và lưu lịch sử thay đổi.

**Chưa có:** trình nhập BOQ nhiều sheet tổng quát (tự nhận sheet, ghép cột, đối chiếu tổng). Hiện chỉ dùng các mẫu cố định ở 3.0.2.

#### 3.5.4 Yêu cầu vật tư

Kỹ sư công trường lập yêu cầu theo BOQ, ghi khối lượng và ngày cần; văn phòng duyệt.

Mở **Kiểm soát cung ứng**, chọn dự án, tab **Yêu cầu vật tư**. Danh sách luôn giới hạn trong dự án đã chọn. Tìm theo mã, ghi chú, người được giao, hạng mục BOQ; lọc trạng thái, người phụ trách cung ứng, ngày cần (bấm để đổi chiều sắp xếp); xuất CSV theo cùng bộ lọc. Mỗi dòng hiện người yêu cầu, người phụ trách công trường, người phụ trách cung ứng, ngày cần, khối lượng yêu cầu, khối lượng đã nhận qua phiếu nhập đã ghi sổ, hạn mức BOQ còn lại.

**Xem chi tiết** hiện khách hàng, dự án, các hợp đồng liên quan, so sánh với BOQ, ghi chú và lịch sử.

| Trạng thái | Ý nghĩa |
|---|---|
| Nháp | Người yêu cầu sửa được; lỗi không làm mất dữ liệu đã nhập |
| Đã trình | Chờ duyệt; khóa sửa |
| Đã duyệt / Từ chối | Đã quyết định; khóa sửa |
| Đáp ứng một phần / Đã đáp ứng | Theo hàng đã nhận |
| Đã huỷ | Hủy kèm lý do (từ trạng thái chưa kết thúc) |

Khi tạo: chọn người trong dự án, ngày cần trong tương lai, hạng mục không trùng trong BOQ hiện hành, khối lượng không vượt hạn mức còn lại. Người phụ trách cung ứng và người yêu cầu nhận thông báo khi trách nhiệm thay đổi. Người ngoài dự án không thấy bản ghi; nút tạo/gửi/duyệt chỉ hiện khi có quyền.

#### 3.5.5 Kho và cấp phát vật tư

Mở **Kiểm soát cung ứng**, chọn dự án, tab **Nhập / xuất kho**. Phần tồn kho hiện số đã nhập, đã xuất, tồn sau khi trừ các giao dịch đảo. Lọc theo mã, vật tư, người phụ trách, hạng mục, loại, trạng thái, ngày; xuất CSV.

- Thủ kho tạo phiếu nhập và phiếu xuất ở trạng thái Nháp. Chỉ người tạo sửa được bản nháp.
- **Ghi sổ** cập nhật tồn kho và khóa phiếu (**Đã ghi sổ**). Sai thì dùng **Đảo giao dịch** kèm lý do (**Đã đảo**).
- Không đảo được phiếu nhập nếu làm tồn kho âm.
- Phiếu xuất giữ người nhận ở công trường và mã hạng mục để truy được vật tư cấp cho tổ đội nào.

#### 3.5.6 Cảnh báo vật tư

Mở **Kiểm soát cung ứng**, chọn dự án, tab **Cảnh báo vật tư** (số trên tab là cảnh báo chưa xử lý). Mặc định hiện cảnh báo chưa xử lý; dùng tổng hợp trạng thái hoặc bộ lọc để xem lịch sử. Tìm theo mã cảnh báo, mã vật tư, mô tả, người được giao; lọc loại, trạng thái, mức độ; xuất CSV.

| Loại | Nghĩa |
|---|---|
| **Xuất vượt BOQ** | Tổng xuất kho đã ghi sổ vượt hạn mức BOQ đã duyệt hiện hành |
| **Thiếu vật tư** | Nhập kho đã ghi sổ chưa đủ cho yêu cầu vật tư đã duyệt |

Cảnh báo tự cập nhật sau khi duyệt BOQ, duyệt/hủy yêu cầu vật tư, ghi sổ hoặc đảo phiếu kho. Sau sự cố hệ thống, người có quyền dùng **Đối chiếu dữ liệu nguồn** (không sửa kho hay BOQ).

Mở cảnh báo để so nhu cầu/hạn mức với số đã nhận, đã xuất, tồn và chênh lệch, kèm khách hàng, dự án, hợp đồng (không có giá trị tiền), chứng từ nguồn, người được giao, lịch sử. Người được giao hoặc PM chọn **Tiếp nhận xử lý**, ghi người xử lý, hành động và thời hạn. Việc này chỉ ghi trách nhiệm, không đóng cảnh báo. Hệ thống tự đóng khi dữ liệu nguồn được sửa và tự mở lại nếu tình trạng tái diễn.

### 3.6 Tài chính

Hợp đồng là một bản ghi duy nhất trong **Sổ hợp đồng** ([3.1.7](#317-hợp-đồng)). Tài chính chỉ lọc và theo dõi; bấm một dòng sẽ mở đúng hợp đồng đó, có nút quay lại danh sách đã lọc.

#### 3.6.1 Theo dõi phải thu

**Tài chính → Theo dõi phải thu** liệt kê hợp đồng đầu ra: lọc theo người phụ trách, dự án, trạng thái, ngày kết thúc; hiện giá trị hiện tại sau VO đã duyệt, tổng hợp rủi ro thu, khoản phải thu kế tiếp; xuất CSV toàn bộ kết quả lọc.

Kinh doanh tạo hợp đồng đầu ra theo một trong hai cách: tạo thẳng từ Cơ hội khi khách đã đồng ý giá trị, hoặc từ báo giá đã duyệt, còn hiệu lực, chưa dùng. Với cách thứ hai, Tài chính phải duyệt rà soát thương mại trước ([3.0.2](#302-công-việc-phòng-ban-trước-hợp-đồng)); báo giá phải khớp khách hàng, dự án và Cơ hội của hợp đồng. Đợt thanh toán gắn với tiến độ nghiệm thu; quy tắc đợt thanh toán và nhắc hạn ở [3.1.7](#317-hợp-đồng).

#### 3.6.2 Theo dõi phải trả và đề nghị thanh toán

**Tài chính → Theo dõi phải trả** cố định ở hợp đồng đầu vào (cung cấp vật tư, thầu phụ): lọc theo nhà cung cấp, dự án, người phụ trách, trạng thái, ngày, giá trị; tổng hợp nghĩa vụ thanh toán, đợt chưa trả kế tiếp, số đợt quá hạn và đến hạn trong 30 ngày; xuất CSV.

- Một dự án có thể có nhiều hợp đồng đầu vào. Chọn nhà cung cấp đang hoạt động đúng loại: Nhà cung cấp hoặc Cả hai cho hợp đồng cung cấp; Thầu phụ hoặc Cả hai cho hợp đồng thầu phụ.
- Kế toán xem được hợp đồng đầu vào trên toàn bộ dự án mình được xem, cập nhật lịch, kế toán phụ trách, trạng thái thanh toán. Không tạo hợp đồng, không sửa thông tin chính, trạng thái pháp lý, tệp, phụ lục, không xóa.
- Đề nghị thanh toán chỉ chọn được hợp đồng trong phạm vi dự án của mình. Tài chính toàn danh mục cần thêm quyền xem mọi dự án.

**Đề nghị thanh toán đầu vào**

| Trạng thái | Ý nghĩa |
|---|---|
| Nháp | Đang soạn; sửa hợp đồng, QS, tài liệu được |
| Đang kiểm tra | Kế toán kiểm tra |
| Sẵn sàng phê duyệt | Chờ duyệt độc lập |
| Đã phê duyệt / Đã từ chối | Đã quyết định |
| Đã thanh toán | Đã chi |
| Đã hủy | Ngừng |

1. Tạo đề nghị, chọn hợp đồng. Có thể chọn một đợt QS đầu vào đã duyệt của hợp đồng đó; bỏ trống khi là tạm ứng hoặc chưa có khối lượng nghiệm thu.
2. Bản nháp mở lại được từ danh sách để sửa trước khi gửi. QS cũ được đánh dấu trong ô chọn; xem có cần QS thay thế hay đây là tạm ứng hợp lệ.
3. Gửi kế toán kiểm tra rồi duyệt độc lập.

Nếu QS đã gắn bị thay thế, việc kiểm tra, duyệt và chi dừng lại. Dùng **Tạo revision** kèm lý do: bản trước và tài liệu vẫn xem được trong lịch sử, số hóa đơn giữ với đề nghị; sửa bản Nháp mới (chọn QS đúng, cập nhật tài liệu) rồi gửi lại. Đề nghị đã gửi hoặc đã duyệt revision được khi QS không còn hợp lệ; đề nghị bị từ chối revision được để sửa hồ sơ. Liên kết QS chỉ ghi nguồn gốc, không thay kiểm tra hóa đơn, chứng từ, hợp đồng. Người lập, sửa, gửi hoặc kiểm tra một đề nghị không được duyệt chính đề nghị đó.

#### 3.6.3 Phát sinh (VO)

Ghi thay đổi thiết kế hoặc phạm vi, tính tăng/giảm chi phí, gắn với hợp đồng, đính kèm phụ lục.

- Mỗi VO thuộc đúng một hợp đồng và theo dự án của hợp đồng đó.
- Luồng: **Nháp** → gửi duyệt → **Đã duyệt** hoặc **Từ chối**. VO bị từ chối sửa và gửi lại được.
- Chỉ VO **Đã duyệt** làm đổi giá trị hiện tại của hợp đồng.
- Người có quyền sửa VO xóa vĩnh viễn được VO ở mọi trạng thái (có xác nhận). Xóa VO đã duyệt sẽ trừ khoản điều chỉnh khỏi giá trị hợp đồng.

Đây là cách hệ thống đang chạy; chính sách VO chính thức của NICON vẫn chờ duyệt. VO không sửa ngầm baseline hay BOQ gốc.

**Kỳ kế toán** đi theo Đang mở → Đang đóng → Đã đóng. Sửa số liệu sau khi đóng kỳ dùng bút toán điều chỉnh hoặc đảo, không sửa trực tiếp số đã khóa.

#### 3.6.4 Dòng tiền và lãi lỗ (mục tiêu)

Dự kiến: ghi thu từ chủ đầu tư, chi cho vật tư và thầu phụ; so thu/chi thực tế theo dự án; trạng thái chưa trả/đã trả/quá hạn; biểu đồ dòng tiền; báo cáo lãi lỗ theo dự án, tháng/quý/năm; so chi phí thực tế với dự toán. Chưa có đầy đủ trên hệ thống.

### 3.7 Tài liệu dự án và Google Drive

Danh mục **Tài liệu dự án** ([3.0](#30-dự-án)) gom tệp của Báo giá, Hợp đồng, Khảo sát, Thiết kế cơ sở, Shop Drawing, Giấy phép, Nghiệm thu, Hoàn công, Bàn giao và tệp tải trực tiếp lên dự án. Dữ liệu cũ không tự nhập vào.

**Quyền đọc.** Thành viên dự án đang hoạt động đọc được tài liệu của các phân hệ/danh mục mình có quyền xem. Là thành viên không tự thấy tài liệu Tài chính và không có quyền tải lên, xóa hay gửi lại. Kết thúc thành viên thì mất quyền đọc; chủ/quản lý dự án và người có quyền xem tất cả giữ phạm vi của mình.

#### 3.7.1 Kết nối Google Drive

Đồng bộ Drive tắt cho đến khi quản trị viên kết nối.

1. Quản trị viên được ủy quyền tài khoản Google Drive đã duyệt qua OAuth 2.0. Không đưa hay lưu mật khẩu tài khoản vào hệ thống.
2. Đăng nhập tài khoản đó, tạo thư mục gốc trong **My Drive** hoặc Shared Drive đã duyệt, gửi đường dẫn cho quản trị viên. Mã thư mục là phần sau `/folders/` trong đường dẫn.
3. Người có quyền quản lý cài đặt hệ thống mở **Cài đặt → Google Drive**, nhập và lưu thông tin OAuth client, client secret (chỉ ghi), callback URL, thư mục gốc, định danh triển khai, đường dẫn thư mục, thiết lập đồng bộ và hai công tắc hướng xóa.
4. Chọn **Kết nối Google Drive**. Cửa sổ đăng nhập Google mở ra; chọn tài khoản đã duyệt và cấp quyền.
5. Kiểm tra tài khoản và trạng thái **Connected**. Tải thử một ảnh nhỏ: thẻ tệp chuyển từ Pending/Processing sang Synced và có liên kết Drive. Còn treo thì bấm **Kiểm tra kết nối**.

Đổi tài khoản: **Ngắt kết nối** rồi **Kết nối Google Drive**, hoặc **Đổi tài khoản Google**. Nếu đăng nhập tài khoản mới bị hủy hoặc lỗi, hệ thống ở trạng thái chưa kết nối. Ngắt kết nối không đăng xuất Google trên trình duyệt.

| Trạng thái kết nối | Nghĩa |
|---|---|
| `Connected` | Đang đồng bộ |
| `Read only` | Tài khoản thấy thư mục nhưng không thêm tệp được |
| `Invalid folder` | Mã thư mục không phải thư mục đang tồn tại |

**Đổi thư mục gốc, định danh triển khai hoặc đường dẫn** chỉ áp dụng cho thư mục tạo sau đó; tệp và thư mục cũ không bị chuyển hay xóa. Với thư mục đã liên kết: lưu đồng bộ ở trạng thái tắt, chờ các tệp đang xử lý xong, lưu cấu trúc mới khi vẫn tắt, kiểm tra đường dẫn rồi mới bật lại. Hệ thống từ chối đổi cấu trúc khi đang xử lý hoặc đồng bộ đang bật.

#### 3.7.2 Cấu trúc thư mục

Thư mục được tạo khi tệp đầu tiên của danh mục đồng bộ. Có 9 danh mục:

- `01_Khao_sat` và `01_CRM_PreDesign` (hai thư mục ngang hàng)
- `02_Thiet_ke` với 3 thư mục con (Ý tưởng, Cơ sở, Shop Drawing). Tạo Hồ sơ thiết kế sẽ lên lịch tạo 3 thư mục này ngay, kể cả chưa có hợp đồng hay tệp; Drive tắt thì chờ đến khi kết nối lại.
- `03_Xin_phep_Phap_ly`
- `04_Thi_cong_Nghiem_thu`
- `05_Cung_ung_Vat_tu`
- `06_Tai_chinh_Hop_dong`

NICON muốn đặt Khảo sát bên trong CRM Pre-Design, nhưng chưa chuyển tệp cho đến khi có đợt chuyển cấu trúc được duyệt và đảo ngược được. Đổi tên dự án không ảnh hưởng định danh nội bộ. Quyền truy cập do hệ thống kiểm soát; chia sẻ nhóm trên Drive chưa đồng bộ.

#### 3.7.3 Đồng bộ và xóa tệp

- Khi Drive ngắt kết nối vẫn tải lên được: tệp lưu tạm trên máy chủ, hiện **Pending**. Khi kết nối lại, tệp được đẩy lên, kiểm tra nội dung rồi xóa bản tạm. Mỗi tệp thử tự động tối đa 3 lần; tệp lỗi dùng **Gửi lại lên Drive** (vẫn dùng được khi đã hết lượt tự động). Không thử lại hay xóa được tệp đang đồng bộ.
- Tải tệp luôn qua hệ thống để kiểm quyền. Google Docs/Sheets/Slides gốc chỉ mở qua liên kết Drive, không tải nhị phân được.
- Tệp thêm hoặc sửa trực tiếp trong thư mục Drive được quản lý sẽ hiện trong danh mục. Hai bên cùng sửa thì giữ cả hai để xác nhận.
- Tệp thuộc phân hệ khác phải xóa ở phân hệ đó. Tệp đang được hồ sơ QA/QC hoặc QS tham chiếu không xóa được ở đâu cả; xử lý ở hồ sơ nguồn trước. Hồ sơ đã duyệt giữ bằng chứng của nó.

| Công tắc | Bật | Tắt |
|---|---|---|
| **Xóa trên web → xóa trên Drive** (mặc định tắt) | Xóa tài liệu trên hệ thống thì chuyển tệp Drive vào thùng rác; xóa dự án thì xóa cây thư mục Drive rỗng đã kiểm | Tệp và thư mục Drive được giữ, chỉ gỡ liên kết |
| **Xóa trên Drive → xóa trên web** (mặc định bật) | Tệp bị xóa hoặc chuyển vào thùng rác trên Drive được đánh dấu đã xóa trên web | Bản ghi trên web được giữ |

Tệp tải lên chưa liên kết và chỉ có trên máy chủ bị xóa ngay. Tệp lạ, tệp dùng chung và tài liệu CRM độc lập được bảng ảnh hưởng bảo vệ khi xóa dự án.

### 3.8 Báo cáo và KPI

#### 3.8.1 Bảng điều khiển

**Bảng điều khiển** quản trị hiện chỉ tổng hợp nội dung website và tuyển dụng. Bảng tổng hợp cho Ban giám đốc (lead, cơ hội, hợp đồng, dự án, tiến độ thi công, tài chính, cung ứng, phễu lead, cảnh báo tiến độ) là mục tiêu. Tỷ lệ trúng thầu có ở trang **Đấu thầu** ([3.1.5](#315-đấu-thầu)).

#### 3.8.2 Báo cáo dự án

Người có quyền xem báo cáo dự án mở **CRM → Báo cáo dự án** (`/admin/reports/projects`) để xem các dự án được phép theo danh mục hoặc từng dự án. Bộ lọc dự án và khoảng ngày lưu trên URL để tải lại hoặc gửi cho người khác.

Báo cáo hiện: tiến độ thiết kế có trọng số (khi kế hoạch cơ sở đủ), số lượng theo trạng thái thi công và nghiệm thu, việc quá hạn, cảnh báo giấy phép, giá trị báo giá/hợp đồng/đợt thanh toán theo hợp đồng. Người có quyền xuất báo cáo dự án tải XLSX hoặc PDF theo bộ lọc, bằng ngôn ngữ giao diện.

Số tiền đợt thanh toán là giá trị theo lịch hợp đồng, không phải tiền mặt, doanh thu hay công nợ. S-Curve lịch sử, tỷ lệ nghiệm thu, tài chính theo sổ thực tế, tồn kho, mức dùng BOQ, hiệu quả nhà cung cấp hiện ghi rõ "chưa có dữ liệu" theo từng nguồn, không tính số thay thế. Bộ lọc nâng cao lưu sẵn là mục tiêu.

#### 3.8.3 Đánh giá KPI

- `/admin/kpi`: chọn nhân viên và tháng, tính kết quả, xem bằng chứng nguồn, xuất file. Xem của mình hay của người khác theo quyền KPI được cấp.
- `/admin/kpi/configuration` (cần quyền quản lý KPI): chỉ tiêu, trọng số, chiều chấm điểm, ngưỡng cảnh báo.

Tính theo tháng lịch Việt Nam, tự chạy lúc 01:00 giờ Việt Nam; người có quyền tính tay được. Thiếu dữ liệu nguồn thì ghi `MissingData`, không tính 0 điểm; thiếu chỉ tiêu cũng làm chỉ số không đạt `Available`. Chỉ khóa kỳ được khi mọi chỉ số đang áp dụng đều `Available` và tổng trọng số đúng 100%. Khóa kỳ đóng băng định nghĩa và bằng chứng, không tính lại được, và không hoàn tác. Hướng dẫn sử dụng có sẵn 4 ngôn ngữ.

Hiện có 19 chỉ số trong 6 nhóm:

| Nhóm | Vai trò |
|---|---|
| Kinh doanh | `SALE`, `SALES_MANAGER` |
| Đấu thầu | `QS` |
| Thiết kế | `DESIGN`, `DESIGN_MANAGER`, `DESIGN_LEAD`, `ARCHITECT`, `MEP_ENGINEER`, `STRUCT_ENGINEER` |
| Công trường | `PM` |
| Cung ứng | `PROCUREMENT` |
| Kế toán dự án | `ACCOUNTANT` |

Chưa đủ khung 11 vị trí của NICON: PM CM, Chỉ huy trưởng BCH, Kỹ sư hiện trường/QA-QC, QS công trường, Pháp lý, Hành chính nhân sự cần có luồng nguồn và định nghĩa được duyệt trước. Nguồn và cách tính từng chỉ số: [KPI Framework and Source Evidence](application_developer.md#719-kpi-framework-and-source-evidence).

### 3.9 Thông báo và tính năng dùng chung

| Tính năng | Tình trạng |
|---|---|
| Trung tâm thông báo: bảng kéo xuống, trang danh sách, phân theo phân hệ, đánh dấu đã đọc, xóa, mở bản ghi | Đã triển khai |
| Đăng nhập, quên mật khẩu, hồ sơ cá nhân | Đã triển khai (xem [5](#5-đăng-ký-đăng-nhập-mật-khẩu)) |
| Thanh bên thu gọn/mở rộng, menu trên mobile, trang lỗi 403 | Đã triển khai |
| Vai trò và phân quyền | Đã triển khai (xem [4](#4-vai-trò-và-phân-quyền)) |
| Trình dựng luồng duyệt kéo thả | Mục tiêu |
| Tìm kiếm toàn hệ thống, bộ lọc lưu sẵn | Mục tiêu |
| Xem nhật ký thao tác đầy đủ | Một phần |
| Dữ liệu chuẩn (trạng thái, nguồn lead, loại hợp đồng, loại vật tư, mẫu checklist) | Một phần |

---

## 4. Vai trò và phân quyền

Quyền gắn với vai trò; mỗi vai trò là một bộ quyền quản trị viên chỉnh được. Menu và nút chỉ hiện khi bạn có quyền, nhưng hệ thống luôn kiểm tra lại ở máy chủ: ẩn nút không phải là cách chặn duy nhất. Ngoài quyền, hệ thống còn xét bạn có thuộc dự án không, bản ghi đang ở trạng thái nào và bạn có phải người lập không.

**Nguyên tắc chung**

- Xem toàn bộ không có nghĩa được sửa toàn bộ.
- Người lập không được thẩm định độc lập chính chứng từ của mình khi quy trình cần kiểm soát chéo.
- Quyền theo từng Dự án và từng luồng thiết kế; vào được dự án hay luồng này không mở dự án hay luồng khác.
- Quyền trên một phân hệ không mở rộng sang phân hệ khác. Ví dụ xem được Hợp đồng không có nghĩa xem được Lead nguồn của nó.

### 4.1 Vai trò hệ thống

| Vai trò | Quyền |
|---|---|
| `USER` | Tài khoản đăng ký mặc định; chỉ xem và sửa hồ sơ của mình |
| `ADMIN` | Gần như mọi quyền. Xem được người dùng và nhật ký nhưng không tạo/sửa/khóa người dùng, không đổi cấu hình lưu nhật ký, không xóa vĩnh viễn hồ sơ thiết kế không đạt |
| `SUPER_ADMIN` | Toàn bộ quyền. Không tự đổi vai trò, tự khóa mình, hay gỡ SUPER_ADMIN đang hoạt động cuối cùng |

### 4.2 Vai trò nghiệp vụ

Vai trò nghiệp vụ được tạo sẵn và chỉnh ở **Quản lý vai trò** (`/admin/roles`); chỉnh sửa được giữ khi khởi động lại.

| Vai trò | Làm gì trên hệ thống |
|---|---|
| Kinh doanh (`SALE`) | Lead, Cơ hội, Khảo sát, Báo giá, Đấu thầu, Hợp đồng trong phạm vi khách/cơ hội của mình. Tạo Dự án và khởi tạo hồ sơ thiết kế từ Cơ hội |
| Trưởng phòng Kinh doanh (`SALES_MANAGER`) | Như Kinh doanh; duyệt tiến độ tổng dự án trước khi ký hợp đồng |
| Trưởng phòng Cung ứng (`PROCUREMENT_MANAGER`) | Người duyệt báo giá đầu ra theo luồng mặc định (một bước); tải mẫu BOQ |
| Cung ứng (`PROCUREMENT`) | Giá đầu vào, BOQ, nhà cung cấp, RFQ, yêu cầu vật tư; nhận máy/nhân công CM đã duyệt, soạn và gắn hợp đồng đầu vào, xác nhận cung ứng. Không ký hợp đồng, không duyệt CM, không duyệt/chi thanh toán |
| Thủ kho (`WAREHOUSE`) | Nhập, xuất, cấp phát vật tư |
| Chủ nhiệm thiết kế (`DESIGN_MANAGER`) | Mọi việc của Chủ trì thiết kế, thêm phối hợp nhà cung cấp/RFQ và xem hợp đồng thầu phụ. Không xóa vĩnh viễn hồ sơ thiết kế không đạt (chờ Q-08) |
| Thiết kế (`DESIGN`, `DESIGN_LEAD`, `ARCHITECT`, `MEP_ENGINEER`, `STRUCT_ENGINEER`) | Hồ sơ thiết kế, công việc và bản vẽ theo bộ môn. Không tự tạo Dự án hay khởi tạo luồng từ Cơ hội |
| PM (`PM`) | Quản lý dự án được giao (không tạo được bản ghi bán hàng). Đại diện Phòng CM: chuẩn bị thi công, duyệt độc lập, xuất bản mẫu checklist pháp lý, kích hoạt pháp lý |
| Chỉ huy trưởng (`SITE_COMMANDER`), QA/QC (`SITE_QAQC`) | Việc tại công trường: đề xuất nguồn lực, nhật ký, QA/QC, punchlist, HSE |
| Pháp lý (`LEGAL_OFFICER`) | Soạn mẫu checklist pháp lý, quản lý giấy phép |
| QS (`QS`) | Đấu thầu/dự toán; QS công trường và đề xuất nguồn lực |
| Kế toán (`ACCOUNTANT`) | Xem hợp đồng đầu ra và đầu vào trên toàn danh mục dự án; cập nhật lịch thu/chi, kế toán phụ trách, ngày thu/chi thực tế; đề nghị thanh toán. Không tạo/sửa thông tin chính, ký, đính kèm, phụ lục, VO hay xóa hợp đồng |
| Hành chính nhân sự (`HR`) | Tuyển dụng, vị trí, tùy chọn tuyển dụng, liên hệ, dữ liệu chuẩn. Không có quyền thanh toán, kỳ kế toán, hợp đồng, duyệt hay đổi mã dự án |
| Ban giám đốc (`BGD`) | Xem rộng toàn công ty; trao thầu RFQ; xóa vĩnh viễn hồ sơ thiết kế không đạt |

Một người có thể giữ nhiều vai trò. Các vai trò này là bộ quyền, không phải khung 11 vị trí KPI; không gộp hay suy nghĩa vai trò từ nhóm KPI.

### 4.3 Nhóm phòng ban và quản lý vai trò

Vai trò được xếp vào 6 nhóm: CRM (Kinh doanh), Cung ứng (Cung ứng, Trưởng phòng Cung ứng, Thủ kho), Thi công (PM, công trường, Pháp lý, QS), Thiết kế, Tài chính (Kế toán), Hành chính nhân sự. BGĐ không thuộc nhóm nào. Đây là đề xuất triển khai; NICON mới nêu 5 phòng và chưa chốt vị trí của Cung ứng và Pháp lý. Nhóm phòng ban cũng là danh sách phòng ban dùng trong tiến độ thiết kế và vị trí tuyển dụng.

Trong **Quản lý vai trò**, quản trị viên:

- Xem vai trò theo nhóm, tìm vai trò và quyền, so sánh nhiều vai trò trên ma trận. Vai trò hệ thống chỉ xem.
- Tạo, sửa, xóa vai trò không phải hệ thống. Khi tạo có thể chép bộ quyền mẫu của nhóm làm điểm bắt đầu; sửa bộ quyền mẫu sau đó không đổi các vai trò đã tạo. Không ai cấp được quyền mình không có.
- Đổi tên hiển thị của vai trò; mã vai trò và quyền giữ nguyên.
- Tạo nhóm phòng ban với mã cố định và tên 4 ngôn ngữ. Xóa nhóm phải xem trước ảnh hưởng và gõ mã; bị chặn khi còn vai trò, nhóm công việc, công việc hay bộ quyền liên kết. 6 nhóm có sẵn không xóa được.

**Người dùng** (`/admin/users`): tìm, lọc theo vai trò và phòng ban, tạo/sửa, khóa/mở, xóa (ẩn tài khoản). Chỉ SUPER_ADMIN được thay đổi người dùng.

### 4.4 Quyền theo nghiệp vụ

| Nghiệp vụ | Ai được làm |
|---|---|
| Dự án | Tạo Dự án: người có cả quyền quản lý Cơ hội và quản lý Dự án. Sửa, chuyển trạng thái: người tạo/quản lý dự án. Đổi mã: quyền riêng. Người chuyển Lead thành Cơ hội là người tạo dự án tự sinh |
| Nhóm công việc | Tạo nhóm theo dự án: người phụ trách Cơ hội, PM dự án hoặc quản trị viên. Giao việc: trưởng phòng nhận việc. Cập nhật tiến độ: người thực hiện, khi nhóm đang thực hiện. Xác nhận: người xác nhận độc lập. Mở lại, xóa: người quản lý nhóm |
| Báo giá | Kinh doanh tạo/sửa trong phạm vi cơ hội của mình; duyệt và gửi khách là quyền riêng |
| Hợp đồng | Xem theo người phụ trách hoặc theo quyền xem toàn bộ. Chăm sóc khách hàng, email nhắc, tái ký: người quản lý hợp đồng và là người phụ trách (hoặc xem toàn bộ). Quyền Tài chính hay thành viên dự án không cho ghi chăm sóc hay gửi email nhắc |
| Thiết kế | Đội ngũ: người phụ trách hồ sơ. Duyệt công việc: đúng người duyệt được giao. Trình nội bộ: người được chỉ định chỉ xem và góp ý. Chốt phương án, phát hành IFC, quyết định khách: không suy ra từ quyền Xem/Tạo/Sửa/Xóa trên hồ sơ |
| Pháp lý | Kích hoạt, thiết lập: PM dự án hoặc Admin. Trưởng nhóm Pháp lý cấu hình gói. Người thực hiện cập nhật và gửi hoàn thành; người duyệt (hoặc Admin) xác nhận/trả lại; người thực hiện không tự duyệt. Chuyển giấy phép sang Đã cấp cần quyền riêng. Soạn mẫu: Pháp lý; xuất bản mẫu: PM/Admin chưa từng soạn/sửa phiên bản đó |
| Thi công | PM: chuẩn bị CM và duyệt cấp 2, không lập hồ sơ BCH. Chỉ huy trưởng, QA/QC, QS: lập hồ sơ công trường. Người lập không duyệt chính hồ sơ đó dù có vai trò khác cho phép |
| Bàn giao | Quyền xem, quyền sửa, quyền hoàn tất tách riêng; có thể giới hạn theo dự án hoặc mở cho mọi dự án ([3.4.4](#344-bàn-giao)) |
| Tài chính | Đề nghị thanh toán chỉ trong dự án mình là thành viên, quản lý hoặc người tạo; xem toàn danh mục cần thêm quyền xem mọi dự án. Người lập, sửa, gửi, kiểm tra không duyệt cùng đề nghị |
| Báo cáo | Báo cáo dự án, tỷ lệ trúng thầu, KPI: theo quyền xem phân hệ nguồn; thiếu quyền thì không hiện số |

Chủ nhiệm thiết kế là người phụ trách thiết kế cấp phòng; Chủ trì thiết kế là vai trò cấp nhóm. Mỗi công việc một người duyệt, người khác theo dõi qua CC; không áp chuỗi duyệt hai cấp của PRD cũ nếu chưa có quyết định mới.

**11 vị trí nghiệp vụ trong PRD của NICON** là trách nhiệm công việc, không phải tên khác của vai trò:

| Vị trí | Trách nhiệm | Trên hệ thống |
|---|---|---|
| PM Phòng CM | Tiến độ, ngân sách, QA/QC, HSE, QS, VO toàn danh mục | `PM`; phần chuẩn bị/duyệt CM đã có, toàn danh mục còn thiếu |
| Chỉ huy trưởng BCH | Điều hành một công trường | `SITE_COMMANDER`; offline/toàn bộ công trường còn thiếu |
| Kỹ sư hiện trường / QA-QC | Bằng chứng hằng ngày, QA/QC, punchlist | Cấp theo quyền thi công cụ thể, không chỉ theo tên vai trò |
| Kỹ sư QS hiện trường | Nghiệm thu khối lượng đầu ra và đầu vào | `QS` không tự động gồm cả QS công trường và đấu thầu |
| Nhân viên Cung ứng | RFQ, trao thầu, mua hàng, đánh giá nhà cung cấp | `PROCUREMENT`, tách với giữ kho |
| Nhân viên Kinh doanh / CRM | Lead, cơ hội, báo giá, doanh thu đầu ra | `SALE` / `SALES_MANAGER` |
| Kỹ sư Đấu thầu / QS văn phòng | Kế hoạch thầu, nộp thầu, dự toán | `QS` |
| Kỹ sư / Kiến trúc sư Thiết kế | Công việc thiết kế, revision, bản vẽ bộ môn | Các vai trò thiết kế |
| Kỹ sư Pháp lý & Xin phép | Checklist pháp lý, nộp hồ sơ cơ quan | `LEGAL_OFFICER` |
| Kế toán Dự án | Phải thu, phải trả, dòng tiền, lãi lỗ | `ACCOUNTANT` |
| Hành chính Nhân sự | Tuyển dụng, chốt/xuất KPI, yêu cầu nội bộ | `HR` cho tuyển dụng; KPI và yêu cầu nội bộ chưa có |

---

## 5. Đăng ký, đăng nhập, mật khẩu

**Đăng ký** (`/register`)

1. Nhập số điện thoại, họ tên, email, mật khẩu.
2. Nếu bật OTP, hệ thống gửi mã tới email; nhập mã để xác thực. Nút gửi lại bị khóa và đếm ngược khi mã cũ còn hiệu lực.
3. Tài khoản được tạo với vai trò `USER`. Nếu tắt OTP, đăng ký xong ngay sau bước 1.

**Đăng nhập** (`/login`) bằng số điện thoại hoặc email và mật khẩu. Phiên đăng nhập tự gia hạn (mã truy cập 7 ngày, mã làm mới 30 ngày) nên không phải đăng nhập lại thường xuyên. **Đăng xuất** kết thúc phiên.

**Quên mật khẩu** (`/forgot-password`)

1. Nhập số điện thoại đã đăng ký.
2. Nếu bật OTP, nhập mã gửi về email (cùng cơ chế đếm ngược gửi lại).
3. Đặt mật khẩu mới. Nếu tắt OTP, đặt lại trực tiếp.

Quản trị viên bật/tắt OTP cho đăng ký và quên mật khẩu ở **Cài đặt** (`/admin/settings`), tab Bảo mật.

---

## 6. Website công khai

| Trang | Đường dẫn | Nội dung |
|---|---|---|
| Trang chủ | `/` | Slideshow, các khối nội dung nổi bật |
| Dịch vụ | `/services`, `/services/:slug` | Danh sách và chi tiết dịch vụ |
| Dự án | `/projects`, `/projects/:slug` | Danh mục công trình; chi tiết có thư viện ảnh, thách thức, giải pháp |
| Tin tức | `/news`, `/news/:slug` | Tin công ty |
| Hoạt động | `/activities`, `/activities/:slug` | Sự kiện, hoạt động |
| Khách hàng | `/clients` | Logo khách hàng, đối tác, nhà cung cấp |
| Tuyển dụng | `/recruitment` | Vị trí đang tuyển, nộp hồ sơ |
| Liên hệ | `/contact` | Form liên hệ |
| Giới thiệu công ty | `/profile` | Thông tin công ty |
| Đăng nhập / Đăng ký / Quên mật khẩu | `/login`, `/register`, `/forgot-password` | Tài khoản |
| Hồ sơ của tôi | `/my-profile` | Hồ sơ, mật khẩu, tài liệu cá nhân |
| Không có quyền | `/forbidden` | Thông báo từ chối truy cập |

- Tìm dự án ở trang chủ theo loại, quy mô và địa điểm; kết quả mở trang Dự án với bộ lọc, kết hợp được với trạng thái và từ khóa. Bỏ từng bộ lọc bằng thẻ của nó. Bộ lọc giữ khi tải lại và khi chia sẻ đường dẫn.
- Chân trang liên kết tới Liên hệ; không có đăng ký nhận bản tin.
- Chọn ngôn ngữ ở thanh điều hướng (Việt, Anh, Trung, Nhật); áp dụng cho giao diện và nội dung đã dịch.
- Form liên hệ và nộp hồ sơ tuyển dụng không cần đăng nhập (xem [9](#9-tuyển-dụng), [10](#10-liên-hệ)).

---

## 7. Trang quản trị

Trang quản trị ở `/admin`, dành cho người đã đăng nhập và có quyền. Mỗi vai trò chỉ thấy menu và nút được cấp.

| Nhóm | Đường dẫn | Dùng cho |
|---|---|---|
| Bảng điều khiển | `/admin`, `/admin/notifications` | Tổng quan, thông báo cá nhân |
| CRM | `/admin/leads`, `/admin/customers`, `/admin/opportunities`, `/admin/quotes`, `/admin/capability-documents`, `/admin/tenders`, `/admin/surveys`, `/admin/contracts` | Bán hàng và hợp đồng |
| Thiết kế và Pháp lý | `/admin/design-projects`, `/admin/permits` | Hồ sơ thiết kế, IFC, giấy phép |
| Ban chỉ huy công trường | `/admin/construction/bch/operations`, `/admin/construction/bch/qa-qc`, `/admin/construction/bch/qs` | Thực hiện tại công trường |
| Phòng CM | `/admin/construction/cm/schedule`, `/admin/construction/cm/commercial`, `/admin/construction/cm/quality`, `/admin/construction/cm/safety`, `/admin/construction/cm/contracts` | Kế hoạch, giám sát, duyệt độc lập, xem hợp đồng dự án |
| Nội dung | `/admin/activities`, `/admin/news`, `/admin/projects`, `/admin/services`, `/admin/about`, `/admin/categories` | Nội dung website |
| Tuyển dụng và liên hệ | `/admin/recruitment`, `/admin/recruitment/employment-types`, `/admin/contacts` | Vị trí, hồ sơ ứng tuyển, liên hệ |
| Thương hiệu và quy trình | `/admin/clients`, `/admin/partners`, `/admin/suppliers`, `/admin/awards`, `/admin/processes/*` | Logo, quy trình nội bộ |
| Cấu hình | `/admin/settings`, `/admin/email-templates`, `/admin/languages`, `/admin/translations`, `/admin/master-data`, `/admin/workflows` | Cài đặt, email, bản dịch, dữ liệu chuẩn, luồng duyệt |
| Bảo mật | `/admin/users`, `/admin/roles`, `/admin/activity-log` | Người dùng, vai trò, nhật ký hoạt động |

Đường dẫn cũ `/admin/posts/*`, `/admin/project-categories`, `/admin/slideshow` tự chuyển sang vị trí mới.

**Luồng duyệt.** Trang **Cấu hình → Luồng duyệt** liệt kê từng nghiệp vụ cùng chuỗi người duyệt theo thứ tự (ví dụ "1 Phụ trách Pháp lý › 2 Trưởng phòng Kinh doanh"), tìm được theo tên luồng hoặc vai trò duyệt.

- Thêm hoặc sửa luồng: chọn **Nghiệp vụ cần duyệt** (nghiệp vụ đã có luồng sẽ mờ, vì mỗi nghiệp vụ chỉ có một luồng), đặt tên, rồi với mỗi bước chỉ cần chọn **Vai trò duyệt**. Tên bước để trống thì hệ thống ghi theo vai trò, ví dụ "Trưởng phòng Cung ứng duyệt". **Thời hạn xử lý** tính bằng giờ (24 giờ = 1 ngày). Tắt **Đang áp dụng** để tạm ngưng luồng mà không xoá.
- Mã kỹ thuật chỉ cần nhập khi chọn **Nghiệp vụ khác**.
- Luồng **Duyệt báo giá** được áp dụng thật: khi gửi duyệt, người gửi chọn người cho từng bước theo đúng thứ tự ở đây (xem mục **Gửi duyệt báo giá nội bộ** ở [12.2](#122-mẫu-email-và-email-gửi-duyệt)). Vai trò ở mỗi bước phải đang dùng và có quyền "Xem báo giá", "Duyệt báo giá"; vai trò có bước "mọi người đều phải duyệt" không lặp lại ở bước khác. Sai thì không lưu được, và hệ thống liệt kê một lần tất cả các bước cần sửa. Luồng đang tắt thì vẫn lưu được, nên có thể tắt một luồng đang lỗi trước rồi sửa sau. Nếu một bước đã lưu từ trước mà nay không ai duyệt được (vai trò ngừng dùng, thiếu quyền, hoặc chưa có ai đang hoạt động có email), danh sách đánh dấu bước đó và ghi cách xử lý. Sửa luồng chỉ áp dụng cho lần gửi duyệt sau; báo giá đang chờ giữ chuỗi lúc gửi.
- Các luồng khác (hợp đồng, gói thầu, thiết kế) hiện là quy trình chung để mọi người làm theo; người duyệt trên từng phiếu do màn hình của nghiệp vụ đó áp dụng.

**Xuất Excel.** Các danh sách liên hệ, hồ sơ ứng tuyển, bài đăng, dự án, bản dịch, danh mục hoạt động, hình thức làm việc, logo (khách hàng, đối tác, nhà cung cấp, giải thưởng) có nút **Xuất Excel**, tải dữ liệu đang lọc dưới dạng CSV UTF-8 mở được bằng Excel. Nút bị khóa khi danh sách trống.

---

## 8. Quản lý nội dung website

Mọi loại nội dung đều tạo, xem, sửa, xóa, sắp thứ tự hiển thị và dịch được. Mỗi mục có slug (đường dẫn) duy nhất.

| Loại | Thông tin chính |
|---|---|
| Hoạt động | Ngày, ảnh bìa, danh mục, tác giả, tiêu đề, tóm tắt, các đoạn nội dung, thứ tự. Danh mục bật/tắt riêng |
| Tin tức | Cùng cấu trúc với Hoạt động |
| Dự án (website) | Ảnh chính, thư viện ảnh, tên, khách hàng, địa điểm, quy mô, phạm vi, trạng thái (`ongoing`/`completed`), năm, loại, mô tả, thách thức, giải pháp, điểm nổi bật |
| Dịch vụ | Tiêu đề, tiêu đề ngắn (menu), khẩu hiệu, giới thiệu, các phần (tiêu đề + nội dung), điểm nổi bật |
| Slideshow | Ảnh nền, tiêu đề, tiêu đề phụ, liên kết và chữ nút (tùy chọn), bật/tắt hiển thị |
| Logo | Tên, ảnh, liên kết, loại: Khách hàng, Đối tác, Nhà cung cấp (và Giải thưởng) |
| Quy trình | Nhóm, mã, tiêu đề, thứ tự, ảnh và tệp đính kèm |

**Quy trình quản lý công việc.** Người có quyền tìm, tạo, sửa, xóa từng mục hoặc nhiều mục. Mục có ảnh hiện lưới ảnh nhỏ; bấm để xem toàn màn hình, có nút trước/sau; nút chuyển sang xem dọc toàn bộ ảnh. Mục có tệp hiện danh sách tải về. Nội dung ban đầu lấy từ dữ liệu khởi tạo; người vận hành cần kiểm tra cơ chế khởi tạo trước khi đổi tệp dữ liệu này.

---

## 9. Tuyển dụng

**Vị trí tuyển dụng:** tiêu đề, phòng ban, nơi làm việc, hình thức làm việc (quản lý ở **Hình thức làm việc**), cấp kinh nghiệm (`junior`, `mid`, `senior`), mô tả, yêu cầu, bật/tắt, thứ tự. Chỉ vị trí đang bật hiện trên website; quản trị viên thấy tất cả.

**Hồ sơ ứng tuyển:** ứng viên nộp trên website không cần đăng nhập, gồm họ tên, email, điện thoại, số năm kinh nghiệm, thư giới thiệu, tệp CV.

| Trạng thái | Ý nghĩa |
|---|---|
| `new` | Mới nộp, chờ xem |
| `interview` | Được mời phỏng vấn |
| `hired` | Đã tuyển |
| `rejected` | Từ chối |

Quản trị viên xem (lọc theo vị trí, trạng thái), đổi trạng thái, xóa hồ sơ. Khi có hồ sơ mới, hệ thống có thể gửi email tới địa chỉ nhận thông báo đã cấu hình.

---

## 10. Liên hệ

Khách gửi form liên hệ trên website (không cần đăng nhập): họ tên, email, điện thoại, tiêu đề, nội dung.

Ở **Liên hệ** (`/admin/contacts`), quản trị viên:

- Xem danh sách, lọc đã trả lời/chưa trả lời, xem chi tiết.
- **Trả lời**: nội dung gửi tới email người liên hệ.
- Đánh dấu đã trả lời mà không gửi email (khi đã xử lý qua kênh khác).
- Xóa tin nhắn.

Mỗi tin lưu trạng thái trả lời, nội dung và thời điểm trả lời.

---

## 11. Đa ngôn ngữ

Hệ thống hỗ trợ tiếng Việt (`vi`, ngôn ngữ gốc), tiếng Anh (`en`), tiếng Trung (`zh`), tiếng Nhật (`ja`).

**Bản dịch giao diện** (`/admin/translations`): mỗi mục gồm khóa (ví dụ `nav.home`), mã ngôn ngữ, giá trị, nhóm. Quản trị viên xem mọi khóa theo các ngôn ngữ, lọc theo nhóm và từ khóa, tạo/sửa, nhập hàng loạt, xóa khóa. Danh sách có nút Trước/Sau và cửa sổ trang di chuyển (kể cả sau trang 10).

**Dịch nội dung** (tab **Dịch nội dung**) gom bản dịch theo trường cho: hoạt động, tin tức, dự án, dịch vụ, slideshow, vị trí tuyển dụng, phần giới thiệu, nhóm vai trò, hình thức làm việc, tùy chọn tuyển dụng (cấp kinh nghiệm, phúc lợi), dữ liệu chuẩn, logo, và danh mục hoạt động/tin tức/dự án/hồ sơ hoàn công.

- Màn hình quản lý của từng loại chỉ giữ tên tiếng Việt; tên Anh, Trung, Nhật sửa ở đây. Sửa tên tiếng Việt giữ nguyên bản dịch đã nhập và cập nhật các giá trị tạm chưa dịch.
- Danh sách hiện tiến độ dịch cho ba ngôn ngữ. **Đặt lại** xóa bản dịch của mục và quay về nội dung gốc.
- Trường có cấu trúc (JSON) phải giữ đúng khóa, kiểu, số phần tử và thứ tự như bản tiếng Việt.
- Quyền: quyền xem dịch nội dung để mở, quyền quản lý dịch nội dung để lưu hoặc đặt lại (kể cả khi gửi tên dịch kèm danh mục hay nhóm vai trò).

---

## 12. Email

### 12.1 Các loại email

| Email | Người nhận | Khi nào |
|---|---|---|
| OTP đăng ký | Người đăng ký | Bắt đầu đăng ký (bật OTP) |
| OTP quên mật khẩu | Chủ tài khoản | Bắt đầu đặt lại mật khẩu (bật OTP) |
| Thông báo hồ sơ ứng tuyển | Địa chỉ nhận thông báo | Ứng viên nộp hồ sơ |
| Trả lời liên hệ | Người gửi liên hệ | Quản trị viên trả lời |
| Duyệt báo giá | Người duyệt được chọn | Kinh doanh gửi duyệt báo giá nháp |
| Duyệt nhóm công việc | Trưởng nhóm được giao | Gửi duyệt giá đầu vào, rà soát Tài chính |
| Báo giá cho khách | Email khách được chọn | Gửi báo giá đã duyệt, còn hiệu lực |
| Nhắc gia hạn / kết thúc hợp đồng | Người liên hệ của khách | Theo lịch đã đặt ([3.1.7](#317-hợp-đồng)) |
| Tiến độ tổng dự án | Người nhận theo dự án | Gửi duyệt, phát hành, thay đổi tiến độ ([3.1.8](#318-tiến-độ-tổng-dự-án)) |

### 12.2 Mẫu email và email gửi duyệt

Mở **Mẫu email** (`/admin/email-templates`, cần quyền quản lý cài đặt hệ thống). Mỗi tab có trình soạn HTML, nút chèn biến, xem trước tiêu đề/nội dung và xem toàn màn hình (Esc để đóng). **Khôi phục** tải lại cấu hình đã lưu; **Lưu thay đổi** ở đầu trang. Đổi tab không mất nội dung chưa lưu. Mọi email tự động dùng chung bố cục NICON; mỗi loại giữ tiêu đề, nội dung và biến riêng. Email đã xếp hàng giữ nguyên nội dung đã xác nhận.

- Chỉ chấp nhận HTML cơ bản và style nội dòng an toàn; script, thuộc tính sự kiện bị từ chối. Giá trị biến được mã hóa HTML trước khi chèn.
- Bản xem trước ở trang mẫu giữ nguyên tên biến; nội dung thật chỉ hiện trong hộp gửi/lên lịch ở nghiệp vụ tương ứng.

| Tab | Biến | Lưu ý |
|---|---|---|
| OTP | `{{siteName}}`, `{{otpCode}}` | |
| Hồ sơ ứng tuyển | `{{siteName}}`, `{{candidateName}}`, `{{positionTitle}}` | Địa chỉ nhận đặt trong cài đặt |
| **Báo giá** | `{{customerName}}`, `{{quoteCode}}`, `{{opportunityName}}`, `{{quoteLines}}`, `{{discountPercent}}`, `{{vatPercent}}`, `{{grandTotal}}`, `{{validUntil}}` | Nội dung phải giữ `{{quoteLines}}` và `{{grandTotal}}`; không chấp nhận nội dung chỉ có chữ thuần |
| Hợp đồng (**Nhắc gia hạn** / **Thông báo kết thúc**) | Tên khách, số hợp đồng, ngày kết thúc… | Lưu sẽ cập nhật cả hai mẫu |
| **Email duyệt Báo giá** | `{{recipientName}}`, `{{senderName}}`, `{{quoteCode}}`, `{{quoteVersion}}`, `{{grandTotal}}`, `{{validUntil}}`, `{{quoteUrl}}` | Phải có mã báo giá và liên kết `<a href="{{quoteUrl}}">` |
| **Duyệt nhóm công việc** | Mã/tên nhóm, loại hồ sơ, người gửi, đường dẫn… | |
| **Tiến độ tổng thể** | Mã/tên dự án, phiên bản, so sánh thay đổi… | Có danh sách email cần kiểm tra |

**Gửi duyệt báo giá nội bộ**

1. Ở báo giá Nháp, chọn **Gửi duyệt**. Hộp thoại hiện các bước của luồng **Duyệt báo giá** (Cấu hình → Luồng duyệt, mặc định một bước Trưởng phòng Cung ứng). Với mỗi bước, chọn một người có vai trò của bước đó, đang hoạt động, có quyền xem và duyệt báo giá, khác người gửi; mỗi người chỉ duyệt một bước. Bước "mọi người có vai trò này đều phải duyệt" không cần chọn: tất cả lần lượt duyệt. CC chỉ gồm thành viên dự án đang hoạt động có quyền xem báo giá đó. Nếu người duyệt bị đổi vai trò hoặc mất quyền sau khi gửi, email duyệt đang chờ sẽ không được gửi.
2. Xem bản xem trước thật (To là người duyệt bước 1, tiêu đề, nội dung) rồi xác nhận.
3. Trang báo giá hiện từng bước: người duyệt, đã duyệt lúc nào, bước đang chờ và hạn xử lý, cùng trạng thái gửi email.
4. Người của bước đang chờ bấm **Duyệt** (xác nhận) hoặc **Từ chối duyệt** (bắt buộc ghi lý do). Duyệt một bước thì báo giá chuyển cho người bước sau, kèm email và thông báo; duyệt bước cuối thì báo giá thành Đã duyệt. Từ chối ở bất kỳ bước nào thì báo giá về Nháp để Kinh doanh sửa và gửi lại.

Chỉ người của bước đang chờ duyệt hoặc từ chối được; người bước sau phải chờ tới lượt (hệ thống báo đang chờ ai, ở bước mấy). Người trong chuỗi xem được báo giá cả trước và sau lượt của mình.

Người trong chuỗi xem được cả các phiên bản và tài liệu của báo giá để quyết định.

Khi một người duyệt nghỉ việc, đổi vai trò hoặc mất quyền xem, duyệt báo giá:
- Trang báo giá ghi "Không còn đủ điều kiện duyệt" ở bước của họ, trước khi ai bấm gì.
- Người bước sau không còn đủ điều kiện: người đang duyệt không chuyển tiếp được. Họ **Từ chối duyệt** để Kinh doanh gửi lại với người khác, hoặc nhờ quản trị khôi phục tài khoản, quyền.
- Người đang giữ báo giá không còn đủ điều kiện: người phụ trách báo giá bấm **Rút lại để chọn người duyệt khác** (có xác nhận). Báo giá về Nháp để chọn lại người duyệt rồi gửi lại. Nút này chỉ có khi người đang giữ thật sự không duyệt được nữa; người khác trong chuỗi thấy ai rút lại được.
- Nếu trang đang mở đã cũ (báo giá vừa được duyệt, rút lại hoặc trả về), hệ thống báo tải lại trang thay vì làm theo trạng thái cũ. Việc duyệt làm trên hệ thống; email chỉ chứa liên kết. Báo giá đã gửi giữ nguyên chuỗi lúc gửi, kể cả khi quản trị đổi luồng sau đó; báo giá đang chờ từ trước khi có chuỗi vẫn do Trưởng phòng Cung ứng đã chọn quyết định. "Cần kiểm tra" không có nghĩa là chưa gửi được: nhờ quản trị viên kiểm tra SMTP và hỏi người duyệt trước khi gửi lại. Báo giá Chờ duyệt cũ chưa có người duyệt cần xử lý nghiệp vụ trước khi quyết định.

**Gửi duyệt giá đầu vào và rà soát Tài chính**

1. Chọn **Gửi duyệt**; hộp xem trước mở ra. To điền sẵn email trưởng nhóm được giao, sửa được.
2. CC tùy chọn: tối đa 20 địa chỉ, cách nhau bằng dấu phẩy, chấm phẩy hoặc xuống dòng (mỗi địa chỉ tối đa 150 ký tự). Địa chỉ trùng và trùng với To bị loại khi cập nhật.
3. Sau khi sửa To/CC, chọn **Cập nhật xem trước**, kiểm tra người nhận, tiêu đề, nội dung.
4. Chọn **Gửi duyệt và thông báo**. Hủy thì hồ sơ giữ nguyên.

Sửa người nhận không cấp quyền duyệt. Trưởng nhóm phải đang hoạt động, có quyền duyệt, có email hợp lệ và khác người lập; nếu bị chặn, sửa phân công trưởng nhóm rồi thử lại. Đổi hồ sơ, mẫu hoặc người nhận phải xem trước lại. Trưởng nhóm và người CC nhận cùng một liên kết tới nhóm công việc.

Sau khi gửi, màn hình theo dõi tối đa 2 phút và báo SMTP đã nhận hay cần kiểm tra. Mục **Email thông báo gửi duyệt** lưu To/CC và trạng thái; bấm Làm mới để xem. Nếu kẹt ở Đang gửi hoặc Cần kiểm tra, nhờ quản trị viên kiểm tra SMTP và người nhận. Hệ thống không tự gửi lại để tránh email trùng. Duyệt hay từ chối vẫn làm trên hệ thống.

### 12.3 Cài đặt email

Trong **Cài đặt** (`/admin/settings`): bật/tắt OTP đăng ký và quên mật khẩu (tab Bảo mật), tiêu đề và nội dung email OTP, tiêu đề và nội dung email hồ sơ ứng tuyển, địa chỉ nhận thông báo ứng tuyển, tiêu đề và nội dung điền sẵn khi gửi báo giá.

---

## 13. Tải tệp lên

| Loại | Giới hạn |
|---|---|
| Ảnh nội dung website | Tối đa 5 MB; JPG, JPEG, PNG, GIF, WebP, SVG. Ảnh không còn được dùng sẽ tự dọn định kỳ |
| CV ứng tuyển | Tải kèm hồ sơ ứng tuyển |
| Tài liệu dự án, hồ sơ chuẩn bị thi công (CM) | 20 tệp/lần; mỗi tệp theo **Cài đặt → Tài liệu dự án** (mặc định 100 MB, tối đa 100 MB); chỉ chặn tệp chạy được ([3.0](#30-dự-án)) |
| Tệp công việc | 10 tệp/lần; mỗi tệp theo **Cài đặt → Tệp công việc** (mặc định 20 MB, tối đa 100 MB); PDF, Word, Excel, JPG, PNG ([3.0.1](#301-nhóm-công-việc)) |
| Tệp hợp đồng, phụ lục | Theo **Cài đặt → Tệp hợp đồng** (mặc định 100 MB, tối đa 5 GB) |
| Tài liệu báo giá | Theo **Cài đặt → Tệp báo giá** (mặc định 20 MB, tối đa 100 MB); PDF, Word, Excel, JPG, PNG |
| Tệp minh chứng RFQ | Theo **Cài đặt → Tệp RFQ** (mặc định 20 MB, tối đa 100 MB); nhận cả bản vẽ CAD, chỉ chặn tệp chạy được |
| Tài liệu khách hàng | Theo **Cài đặt → Tệp khách hàng** (mặc định 20 MB, tối đa 100 MB); PDF, Word, Excel, JPG, PNG |
| BOQ Excel | Tối đa 20 MB |

Tệp lớn hơn 8 MB ở mọi màn hình được gửi từng phần, tiến độ hiện ở khay tải lên góc dưới:

- Mất mạng giữa chừng thì hệ thống tự gửi lại phần đang dở, không tải lại từ đầu.
- Nếu tải lại trang hoặc lần tải trước báo lỗi, hãy chọn lại **đúng tệp đó** rồi bấm tải lên: hệ thống gửi tiếp phần còn thiếu (trong vòng 24 giờ).
- Nếu tệp thực ra đã lưu xong nhưng màn hình báo mất kết nối, chọn lại tệp đó và bấm tải lên: hệ thống trả về đúng tài liệu đã lưu, không tạo bản trùng.
- Bấm ✕ trên khay để huỷ; phần đã gửi được xoá khỏi máy chủ.

---

## 14. Tài liệu kỹ thuật

Phần tham chiếu API (endpoint, ví dụ request/response) và mô hình dữ liệu dành cho lập trình viên, nằm trong `application_developer.md`. Khi chạy môi trường phát triển, tài liệu API tương tác ở Swagger (`/swagger`). Quyền trên API kiểm tra theo mã quyền, giống như trên giao diện.
