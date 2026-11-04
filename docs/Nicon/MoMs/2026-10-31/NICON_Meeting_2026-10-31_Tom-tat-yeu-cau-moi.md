# TÓM TẮT BUỔI DEMO LẦN 2 NICON (FEEDBACK SAU HỌP 03/10/2026)

Ghi chú: File ghi âm không ghi ngày họp. Bản ghi tự động có nhiều chỗ nhận dạng
sai nên một số điểm được suy luận theo ngữ cảnh. Phần demo Cung ứng bị bỏ qua,
Pháp lý và Thi công hẹn buổi sau.

## 1. ANH NGUYÊN ĐÃ CONFIRM HOẶC QUYẾT ĐỊNH

### Tiến độ chạy một chiều từ trên xuống

- CRM tạo TIẾN ĐỘ TỔNG THỂ cho dự án. Các phòng khác (Thiết kế, CM, BCH, đấu
  thầu) chỉ được chi tiết hóa TRONG KHUNG đó, không vượt và không mâu thuẫn.
  Lý do: tiến độ này đã thống nhất với khách và ghi trong hợp đồng.
- Khi khách đổi yêu cầu thì CẬP NHẬT TRÊN BẢN GỐC và thông báo tự động đến các
  phòng liên quan, ghi rõ điều chỉnh gì so với bản gốc.
- Người duyệt tiến độ: trước hợp đồng là trưởng phòng CRM, sau hợp đồng là PM
  dự án. Nên làm linh hoạt để gán được người duyệt, giống chọn watcher. Hệ
  thống gửi mail và chỉ người được chỉ định mới bấm duyệt được.

### Hai file xuyên suốt dự án: BOQ và tiến độ

- Mỗi dự án có một BOQ GỐC. Giai đoạn dự thầu là BOQ dự thầu, trúng thầu thì
  chuyển thành BOQ hợp đồng, không tạo file mới.
- Phòng CM lập thêm bản KẾ HOẠCH (version 1). BCH công trường lập bản THỰC TẾ
  (version 2). Hệ thống phải so sánh ba bản (gốc, kế hoạch, thực tế) để biết
  vượt hay đạt tiến độ, khối lượng, chi phí. Tiến độ làm tương tự.
- Bản đã duyệt được phát hành cho các phòng cùng xem. Bản cũ giữ làm lịch sử.
- Thời gian và khối lượng/chi phí là hai nhóm quyết định KPI nhiều nhất.

### BOQ

- Mỗi sheet là một hạng mục công trình nên PHẢI HIỂN THỊ ĐỦ CÁC SHEET, không
  ẩn. Sheet tổng cộng lại chi tiết từ các sheet con.
- Dev đề xuất chỉ hiện giá trị tổng trên web và tải file để xem chi tiết. Anh
  Nguyên chấp nhận để chi tiết trong file, nhưng nhấn mạnh yêu cầu chính: các
  trường khối lượng và đơn giá PHẢI LIÊN KẾT ĐƯỢC với module khác. Ví dụ BOQ
  ghi 500 tấn thép, thực tế 600 tấn thì hệ thống phải đối chiếu được.
- Dev trả lời "sẽ điều chỉnh". Chưa chốt cách làm (nhập trực tiếp trên web hay
  upload).

### Báo giá đầu vào trước hợp đồng

- Bản chất là BÁO GIÁ DỰ THẦU / BOQ DỰ THẦU. Cung ứng cùng QS lấy giá thầu phụ
  và nhà cung cấp qua một nhóm công việc riêng, sau đó QS nhập vào BOQ chính
  thức.
- Dev nhận phần này cần chỉnh thêm.

### Google Drive

- Xóa trên web KHÔNG xóa trên Drive. Xóa trên Drive thì web cũng xóa.
- Dev thêm tùy chọn trong cấu hình để chọn đồng bộ xóa một chiều hay hai chiều.
- Lý do: bảo mật dữ liệu; chỉ người có quyền quản lý dữ liệu mới nên xóa trực
  tiếp trên Drive.

### Đổi tên

- "Dự án thiết kế" đổi thành "HỒ SƠ THIẾT KẾ". Một dự án có nhiều hợp đồng,
  mỗi hợp đồng có một bộ hồ sơ thiết kế. Dự án gốc tự tạo khi chuyển Lead,
  không tạo lại.

### Nhóm công việc

- Tách thành MỤC RIÊNG Ở NGOÀI CRM (đặt dưới mục Thông báo), có phân quyền
  riêng để mọi phòng thấy. Ban đầu anh Nguyên lo rườm rà, nhưng đồng ý thử.
  Dev đề xuất gộp quản lý công việc về một nơi.

### Luồng duyệt trong Cấu hình

- Anh Nguyên tự test thực tế rồi mới góp ý. Cấu hình luồng duyệt cần quyền
  riêng; Super Admin có sẵn.

## 2. YÊU CẦU MỚI CHO GIAI ĐOẠN SAU

- CUNG ỨNG: thêm tab HỢP ĐỒNG để tạo và quản lý hợp đồng với nhà cung cấp, từ
  lúc báo giá đến khi quản lý hợp đồng.
- PHÒNG CM có 5 đầu mục quản lý chính:
  1. Tiến độ
  2. Khối lượng và chi phí
  3. Chất lượng
  4. An toàn (HSE)
  5. Hợp đồng
- Mô hình: CM lập kế hoạch rồi giám sát và kiểm tra; BCH trực tiếp thực hiện.
  Cùng một công việc nhưng một người làm, một người kiểm tra. Anh Nguyên yêu
  cầu sắp xếp GỌN, tránh tab trùng giữa BCH và CM.
- Anh Nguyên không có thêm tài liệu. Dev dùng quy trình tổng thể đã gửi, thiếu
  thì hỏi.

## 3. NHỮNG GÌ DEV ĐÃ SỬA VÀ DEMO

- Vai trò: có tab nhóm quyền theo phòng ban. Tạo vai trò mới trong nhóm thì có
  quyền mặc định và chỉnh thêm được. Có thêm tab chi tiết quyền và bảng so
  sánh.
- CRM: ba tab Dự án, Cơ hội, Hợp đồng đã lên đầu. Cơ hội có liên kết dự án vận
  hành và giao việc cho phòng ban.
- Luồng báo giá mới:
  1. Tạo báo giá nháp.
  2. Tạo nhóm việc "yêu cầu báo giá đầu vào" cho Cung ứng; hệ thống gửi email
     cho trưởng phòng Cung ứng.
  3. Cung ứng xong thì TC–KT rà soát.
  4. Gửi khách (mẫu email chỉnh sửa được, đính kèm file).
  5. Khách duyệt.
  6. Gắn hoặc tạo hợp đồng.
- Hợp đồng liên kết báo giá, cơ hội, Lead và hồ sơ thiết kế. BẮT BUỘC upload
  bản scan hợp đồng đã ký trước khi chuyển sang trạng thái đã ký.
- Thiết kế:
  - Đội ngũ có 1 chủ nhiệm thiết kế, nhiều chủ trì và nhân viên. Vai trò
    trong dự án được gán độc lập với vai trò tài khoản. Chị Yến confirm: chỉ
    1 chủ nhiệm, chủ trì có thể 2-3 người.
  - Người trình và người duyệt phải khác nhau.
  - Trình duyệt chọn được phòng ban hoặc người nội bộ.
  - Tiến độ đã cập nhật theo mẫu MS Project, nhưng host chưa có dữ liệu để
    xem.
  - Phiếu phát hành IFC mới dừng ở bước "xác nhận đã nhận". Chuyển sang Cung
    ứng và Thi công chưa làm.
- Google Drive đã liên kết cho tài liệu thiết kế. Đồng bộ hai chiều, tạo thư
  mục theo cấu trúc NICON đã gửi. Dũng sẽ gửi file hướng dẫn kết nối.
- BOQ Cung ứng: có 6 file mẫu và đang dựng template chuẩn. File phức tạp,
  nhiều tab, có tab ẩn nên cần thêm thời gian kiểm chứng.

## 4. CÒN TỒN ĐỌNG VÀ LỖI

- Demo thiết kế trên host LỖI 403 khi dùng tài khoản nhân viên, do thiếu quyền
  hoặc xung đột dữ liệu cũ. Đồng bộ Drive cũng lỗi tạm thời. Dev hẹn kiểm tra
  lại và gửi file demo/kịch bản.
- Dữ liệu host đã dọn nên trống, còn local đầy đủ hơn. Dev sẽ tạo dữ liệu mẫu
  trên host.
- Chị Yến phản ánh phòng chị tốn thời gian chờ demo. Chị đề nghị buổi sau dev
  chuẩn bị mượt, phòng chị chỉ vào thao tác.

## 5. BƯỚC TIẾP THEO

- Tuần sau: dev sửa lỗi, đẩy dữ liệu mẫu lên host và báo NICON test. NICON
  không làm thứ 7, chị Yến dự kiến test vào thứ 5 hoặc 6.
- Khoảng 2 tuần sau: họp tiếp để chốt phản hồi.
- Dev gửi: tài liệu hướng dẫn theo từng vai trò, file kịch bản demo, bản ghi
  họp.
- Dev làm tiếp: chỉnh phần Thiết kế, quản lý nhóm công việc, BOQ. Các phần
  Cung ứng, Pháp lý, Thi công và hợp đồng Cung ứng làm ở giai đoạn sau.
