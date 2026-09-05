# **ĐỀ CƯƠNG CHI TIẾT: HỆ THỐNG PHẦN MỀM QUẢN LÝ CÔNG VIỆC VÀ ĐIỀU HÀNH \- NICON MANAGEMENT SYSTEM**

**Loại hình công ty:** Tổng thầu xây dựng Design & Build (Thiết kế, Xin phép, Thi công)  
**Nền tảng lưu trữ lõi:** Google Drive Integration (Tự động hóa cấu trúc và phân quyền)  
**Tiêu chuẩn vận hành:** Quản trị dữ liệu khép kín, cập nhật thời gian thực (Real-time), đánh giá nhân sự định lượng tự động.

## **MỤC TIÊU HỆ THỐNG**

> * Số hóa toàn bộ chuỗi giá trị của NICON từ lúc tiếp cận khách hàng đến khi bàn giao công trình và đánh giá hiệu suất nhân sự.  
> * Tự động hóa việc đồng bộ, lưu trữ tài sản kỹ thuật và hồ sơ pháp lý lên Google Drive.  
> * Loại bỏ hoàn toàn việc báo cáo thủ công và đánh giá cảm tính nhờ hệ thống dữ liệu liên kết thời gian thực.

## ---

**CHI TIẾT 8 MODULE CHỨC NĂNG (ĐỀ BÀI TRIỂN KHAI)**

### **1\. MODULE 1: QUẢN LÝ KHÁCH HÀNG & TIỀN DỰ ÁN (CRM & PRE-DESIGN)**

**Mục tiêu:** Quản lý linh hoạt nguồn khách hàng, tối ưu hóa tỷ lệ chuyển đổi và số hóa dữ liệu khảo sát ban đầu.

> * **Quản lý Khách hàng tiềm năng (Lead Management):** Ghi nhận thông tin liên hệ, nguồn khách (Marketing, mối quan hệ), phân loại phân khúc và lưu lịch sử tương tác.  
> * **Phễu bán hàng (Sales Pipeline):** Theo dõi trạng thái cơ hội theo quy trình: *Tiếp cận \-\> Khảo sát \-\> Báo giá/Đấu thầu \-\> Thương thảo \-\> Ký hợp đồng.*  
> * **Linh hoạt 02 phương thức chào giá đầu vào:**  
  * **Phương thức 1 \- Chào giá trực tiếp:** Dành cho khách lẻ/gói thầu chỉ định. Tự động tính suất đầu tư nhanh theo m2 dựa trên định biên vật liệu để xuất file báo giá sơ bộ.  
  * **Phương thức 2 \- Đấu thầu:** Quản lý danh mục đầu việc chuẩn bị hồ sơ năng lực, lập dự toán dự thầu, theo dõi thời hạn nộp thầu và cập nhật kết quả đấu thầu.  
> * **Số hóa khảo sát hiện trạng (Mobile App):** Kỹ sư hiện trường dùng app chụp ảnh, quay video, ghi chú tọa độ, lộ giới, cao độ, hạ tầng xung quanh. Dữ liệu tự động đẩy về Folder 01\_Khao\_sat trên Drive của dự án.

### **2\. MODULE 2: QUẢN LÝ THIẾT KẾ 3 GIAI ĐOẠN (DESIGN MANAGEMENT)**

**Mục tiêu:** Quản lý tài sản kỹ thuật chặt chẽ, kiểm soát "độ chín" của hồ sơ, chặn đứng rủi ro thi công sai bản vẽ.

> * **Giai đoạn 1 \- Thiết kế Sơ bộ (Concept Design):** Quản lý các phương án kiến trúc 2D/3D (Option 1, 2, 3\) để khách hàng so sánh. Tích hợp nút bấm "Phê duyệt phương án" từ phía khách hàng hoặc lãnh đạo để khóa ý tưởng trước khi triển khai chi tiết.  
> * **Giai đoạn 2 \- Thiết kế Cơ sở (Basic Design):** Triển khai bản vẽ khung theo đúng quy chuẩn quy hoạch, mật độ, lộ giới. Hệ thống tự động liên kết và đẩy hồ sơ sang Module 3 (Pháp lý) ngay khi được duyệt nội bộ.  
> * **Giai đoạn 3 \- Thiết kế Chi tiết / Bản vẽ Thi công (Detailed Design / Shop Drawing):** Phân tách danh mục quản lý theo bộ môn: Kiến trúc, Kết cấu, MEP, Nội thất.  
> * **Kiểm soát phiên bản (Revision Control):** Tự động đánh mã phiên bản (Rev 00, Rev 01, Rev 02...). Khi có phiên bản mới, hệ thống phát cảnh báo thu hồi bản cũ trên app của toàn bộ kỹ sư công trường.  
> * **Trạng thái Phát hành IFC (Issued for Construction):** Chỉ những bản vẽ được đóng dấu điện tử "IFC" trên phần mềm mới được hiển thị trên giao diện của đội ngũ thi công hiện trường.

### **3\. MODULE 3: QUẢN LÝ PHÁP LÝ & XIN PHÉP (PERMITTING)**

**Mục tiêu:** Kiểm soát các thủ tục hành chính, đảm bảo công trình khởi công và vận hành hợp pháp.

> * **Checklist hồ sơ pháp lý tự động:** Hệ thống tự động tạo danh mục hồ sơ cần chuẩn bị tùy theo quy mô công trình (Giấy phép xây dựng, thẩm duyệt PCCC, đấu nối điện nước, giấy phép sử dụng vỉa hè, hồ sơ hoàn công).  
> * **Theo dõi lộ trình hành chính (Tracking Process):** Cập nhật trạng thái xử lý tại cơ quan chức năng (Đã nộp \-\> Đang thụ lý \-\> Cần bổ sung \-\> Đã có kết quả). Cảnh báo đỏ nếu hồ sơ bị ngâm quá thời gian quy định của pháp luật.

### **4\. MODULE 4: QUẢN LÝ THI LẬP TIẾN ĐỘ, THI CÔNG & NGHIỆM THU (CONSTRUCTION & ACCEPTANCE)**

**Mục tiêu:** Số hóa toàn bộ nhật ký hiện trường và minh bạch hóa quy trình kiểm soát chất lượng.

> * **Quản lý Tiến độ trực quan (Gantt Chart & S-Curve):** Thiết lập bảng tiến độ tổng thể, phân chia hạng mục công việc (WBS). Hệ thống tự động vẽ biểu đồ S-Curve so sánh giữa Tiến độ kế hoạch và Tiến độ thực tế.  
> * **Nhật ký công trình điện tử (Mobile-First):** Kỹ sư công trường cập nhật báo cáo hàng ngày bằng điện thoại: Số lượng nhân công (theo từng tổ đội), máy móc sử dụng, tình hình thời tiết, hình ảnh/video thi công thực tế và các sự cố phát sinh.  
> * **Quy trình Nghiệm thu đa tầng (Acceptance Workflow):**  
  * **Nghiệm thu từng phần (Nghiệm thu bộ phận/Giai đoạn):** Áp dụng cho từng hạng mục khuất lấp hoặc cấu kiện (Cọc, cốt thép, móng, dầm sàn, xây tô, chống thấm...). Kỹ sư gửi yêu cầu kèm hình ảnh thước đo, checklist tiêu chuẩn kỹ thuật. Tư vấn giám sát/Chủ đầu tư ký duyệt online.  
  * **Nghiệm thu toàn công trình (Bàn giao):** Quy trình chạy thử thiết bị (Commissioning), lập biên bản nghiệm thu tổng thể để bàn giao đưa công trình vào sử dụng.  
> * **Quản lý lỗi hiện trường (Punchlist):\*\* Chụp ảnh lỗi thi công, khoanh vùng, gắn thẻ (tag) chỉ định người chịu trách nhiệm sửa chữa, đặt deadline khắc phục và theo dõi trạng thái đóng/mở lỗi.**  
> * **Tự động gom hồ sơ hoàn công: Hệ thống tự động liên kết toàn bộ biên bản nghiệm thu từng phần và bản vẽ có thay đổi thực tế để xuất nhanh bộ hồ sơ hoàn công vào cuối dự án.**

### **5\. MODULE 5: QUẢN LÝ CUNG ỨNG & KHO VẬT TƯ (PROCUREMENT & MATERIALS)**

**Mục tiêu: Tối ưu hóa chi phí mua sắm thông qua chọn lọc nhà thầu phụ/nhà cung cấp và triệt tiêu hao hụt vật tư.**

> * **Quy trình Chọn lọc & Đánh giá Nhà cung cấp, Thầu phụ:**  
  * **Hồ sơ năng lực số (Vendor Directory): Lưu trữ danh mục nhà cung cấp/thầu phụ theo nhóm ngành hàng, xếp hạng uy tín.**  
  * **Ma trận so sánh giá thầu (Bid Tabulation System):\*\* Khi có nhu cầu mua sắm/giao khoán, hệ thống tự động lập bảng so sánh đơn giá, tiến độ, điều khoản thanh toán giữa các bên cung cấp để hỗ trợ chọn thầu tối ưu.**  
  * **Đánh giá định kỳ (Vendor Rating): Sau khi kết thúc gói thầu, hệ thống yêu cầu cấu phần quản lý chấm điểm đối tác theo 4 tiêu chí định lượng: *Chất lượng vật liệu/thi công \- Tiến độ giao hàng/thực hiện \- Giá thành cạnh tranh \- An toàn lao động.***  
> * **Quản lý Vật tư dự án và Chống hao hụt:**  
  * **Quản lý hạn mức BOQ: Khóa trần khối lượng vật tư tối đa dựa trên bảng dự toán chi tiết được duyệt từ Module 2\.**  
  * **Yêu cầu cấp phát vật tư (Material Request \- MR): Kỹ sư hiện trường gửi lệnh gọi vật tư. Hệ thống tự động đối chiếu với BOQ. Nếu khối lượng gọi vượt quá hạn mức còn lại, hệ thống sẽ chặn và yêu cầu giải trình lý do vượt định mức.**  
  * **Quản lý Kho hiện trường (Site Inventory): Theo dõi báo cáo Nhập \- Xuất \- Tồn kho thời gian thực tại từng công trường. Ghi nhận chi tiết vật tư được xuất cho tổ đội nào để làm căn cứ quy trách nhiệm nếu xảy ra lãng phí.**

### **6\. MODULE 6: QUẢN LÝ TÀI CHÍNH, CHI PHÍ & HỢP ĐỒNG (FINANCE & CONTRACT)**

**Mục tiêu: Kiểm soát chặt chẽ dòng tiền, quản lý rủi ro pháp lý các cam kết kinh tế và đo lường biên lợi nhuận.**

> * **Hệ thống quản lý Hợp đồng đa tầng:**  
  * **Hợp đồng chính (Main Contract \- Upstream): Quản lý hợp đồng ký với Chủ đầu tư. Theo dõi lịch trình, mốc thu tiền dựa theo tiến độ nghiệm thu giai đoạn thực tế ở Module 4\. Tự động thông báo khi đến hạn thu tiền.**  
  * **Hợp đồng thầu phụ / Cung ứng (Downstream): Quản lý hệ thống hợp đồng ký với các đối tác được chọn ở Module 5\. Theo dõi tiến độ giải ngân, giữ tiền bảo hành công trình.**  
  * **Quản lý Phát sinh & Phụ lục (Variation Order \- VO): Ghi nhận toàn bộ thay đổi thiết kế/khối lượng làm tăng hoặc giảm giá trị hợp đồng ban đầu. Mọi khoản chi phát sinh tại hiện trường bắt buộc phải gắn liền với một mã VO được duyệt.**  
> * **Quản lý Dòng tiền Dự án (Cashflow Control): Ghi nhận dòng tiền Thực thu (tiền về tài khoản công ty từ CĐT) và Thực chi (chi trả vật tư, nhân công, máy móc, chi phí quản lý dự án).**  
> * **Báo cáo Lãi/Lỗ thời gian thực (Real-time P\&L): Biểu đồ cập nhật tự động biên lợi nhuận gộp của từng công trình dựa trên dữ liệu doanh thu thực tế đối chi với các khoản chi phí kho, chi phí nhân công hiện trường.**

### **7\. MODULE 7: LƯU TRỮ & TÍCH HỢP GOOGLE DRIVE (DIGITAL ASSETS)**

**Mục tiêu: Biến Google Drive thành kho lưu trữ bảo mật cao, tổ chức khoa học tự động và không giới hạn dung lượng.**

> * **Cấu trúc cây thư mục tự động (Auto-Folder Structure): Khi một dự án mới được khởi tạo trên Web/App (Ví dụ: Dự án NICON-01), hệ thống thông qua API tự động tạo ra một cây thư mục chuẩn trên Google Drive của doanh nghiệp theo cấu trúc:**  
>   **`📁 [Mã_Dự_Án]_[Tên_Dự_Án]`**  
>      **`├── 📁 01_CRM_PreDesign (Hồ sơ khảo sát, hồ sơ thầu, nhu cầu khách)`**  
>      **`├── 📁 02_Thiet_ke`**  
>      **`│     ├── 📁 01_So_bo_Concept (Hình ảnh 3D, mặt bằng phương án)`**  
>      **`│     ├── 📁 02_Co_so (Bản vẽ xin phép, thuyết minh kỹ thuật)`**  
>      **`│     └── 📁 03_Chi_tiet_ShopDrawing (Bản vẽ thi công IFC, Thống kê vật tư)`**  
>      **`├── 📁 03_Xin_phep_Phap_ly (GPXD, Thẩm duyệt PCCC, Hồ sơ hoàn công)`**  
>      **`├── 📁 04_Thi_cong_Nghiem_thu (Nhật ký công trình, Biên bản nghiệm thu, Punchlist)`**  
>      **`├── 📁 05_Cung_ung_Vat_tu (Báo giá nhà thầu, Phiếu Nhập-Xuất kho, Đánh giá NCC)`**  
>      **`└── 📁 06_Tai_chinh_Hop_dong (Hợp đồng gốc, Phụ lục VO, Đề nghị thanh toán)`**  
> * **Đồng bộ phân quyền hai chiều (Permission Sync): Quyền truy cập các thư mục trên Google Drive được cấu hình tự động dựa trên phân quyền vai trò trên Web/App NICON. *(Ví dụ: Nhân viên thiết kế chỉ có quyền xem/sửa folder 02\_Thiet\_ke; Kỹ sư công trường chỉ xem được bản vẽ IFC và sửa folder 04\_Thi\_cong; Chỉ Ban Giám đốc và Kế toán trưởng mới nhìn thấy folder 06\_Tai\_chinh).***  
> * **Xem file trực tuyến (In-app Viewer): Tích hợp trình xem của Google API để người dùng có thể mở, đọc các định dạng file (.pdf, .dwg chuyển đổi, .docx, .xlsx, hình ảnh) trực tiếp trên giao diện ứng dụng NICON mà không cần tải file về thiết bị cá nhân.**

### **8\. MODULE 8: QUẢN TRỊ, DASHBOARD & ĐÁNH GIÁ KPI TỰ ĐỘNG (REAL-TIME PERFORMANCE)**

**Mục tiêu: Số hóa hoàn toàn công tác quản trị nhân sự, chấm điểm hiệu suất định lượng 100% dựa trên dữ liệu vận hành thực tế.**

> * **Cơ chế chấm điểm tự động từ dữ liệu nguồn (Data-driven KPI): Hệ thống loại bỏ việc nhân viên tự viết báo cáo và cấp trên chấm điểm cảm tính vào cuối tháng. Điểm KPI được phần mềm tự động quét và tính toán hàng ngày dựa trên hành vi và kết quả ghi nhận từ Module 1 đến Module 6\.**  
> * **Màn hình theo dõi thời gian thực (Real-time KPI Dashboard): Mỗi nhân sự và cấp quản lý đều có một màn hình cá nhân hiển thị điểm KPI hiện tại. Điểm số này biến động liên tục theo tiến độ hoàn thành công việc trong tháng.**  
> * **Xuất báo cáo tức thì (Instant Export): Tính năng cho phép cấp quản lý/phòng HR bấm nút xuất file đánh giá KPI của bất kỳ nhân sự nào hoặc toàn bộ công ty tại bất kỳ thời điểm nào trong tháng. File xuất ra được tự động cấu hình thành định dạng Google Sheets hoặc PDF, tự động lưu trữ vào thư mục quản trị trên Drive.**

## ---

**QUY CHUẨN KHUNG KPI ĐỊNH LƯỢNG NGẮN GỌN CHO CÁC VỊ TRÍ CỐT LÕI**

**Mỗi vị trí công việc tại NICON được cấu hình từ 3 \- 4 chỉ số đo lường then chốt, có số liệu cấu thành trực tiếp từ các hành động trên phần mềm:**  
                                                                                                             

| Vị trí nhân sự | Chỉ số KPI định lượng | Phương thức App tự động thu thập dữ liệu | Trọng số |
| :---- | :---- | :---- | :---- |
| **Nhân viên Kinh doanh (Sales CRM)** | **1\. Tỷ lệ chuyển đổi Lead thành Hợp đồng thành công.** | **Đếm số lượng Lead chuyển trạng thái thành "Ký Hợp đồng" trên tổng số Lead nhận trong Module 1\.** | **40%** |
|  | **2\. Tổng giá trị doanh thu ký mới mang về.** | **Lấy tổng giá trị dòng tiền hợp đồng được xác lập trong Module 6\.** | **40%** |
|  | **3\. Thời gian tương tác khách hàng trung bình.** | **Đo khoảng thời gian từ lúc Lead đổ về hệ thống đến khi Sales bấm cập nhật lịch sử cuộc gọi đầu tiên.** | **20%** |
| **Kỹ sư Đấu thầu (Tendering)** | **1\. Tỷ lệ trúng thầu dự án.** | **Tính toán tỷ lệ: (Số gói thầu cập nhật trạng thái "Trúng thầu") / (Tổng số hồ sơ thầu đã nộp) ở Module 1\.** | **40%** |
|  | **2\. Tiến độ hoàn thiện hồ sơ dự thầu.** | **Đo lường tỷ lệ các task chuẩn bị hồ sơ thầu hoàn thành trước hạn hoặc đúng hạn quy định.** | **30%** |
|  | **3\. Độ chính xác của dự toán thầu.** | **Thuật toán so sánh giá trị BOQ dự thầu với giá trị BOQ triển khai thực tế sau khi bóc tách chi tiết.** | **30%** |
| **Kỹ sư / Kiến trúc sư Thiết kế** | **1\. Đúng hạn tiến độ phát hành bản vẽ bản giai đoạn.** | **Hệ thống đo thời gian bàn giao file thiết kế thực tế so với deadline được thiết lập trên timeline dự án ở Module 2\.** | **40%** |
|  | **2\. Tỷ lệ bản vẽ đạt chuẩn ở lần duyệt đầu tiên.** | **Đếm số lượng Version của bản vẽ. Càng ít phiên bản chỉnh sửa (Rev 00, Rev 01\) thì điểm càng cao.** | **30%** |
|  | **3\. Số lượng lỗi kỹ thuật bị phản hồi từ công trường.** | **Đếm tổng số lỗi thi công phát sinh do sai sót thiết kế được kỹ sư hiện trường gắn thẻ phản hồi ở Module 4\.** | **30%** |
| **Chỉ huy trưởng / Kỹ sư hiện trường** | **1\. Tiến độ thi công hạng mục thực tế (S-Curve).** | **Đo lường phần trăm độ lệch giữa tiến độ báo cáo nhật ký nghiệm thu thực tế so với đường Base-line kế hoạch ở Module 4\.** | **30%** |
|  | **2\. Tỷ lệ hao hụt vật tư thực tế tại công trường.** | **Công thức đối chi bộ lọc: (Khối lượng vật tư xuất kho thực tế) / (Định mức tối đa quy định trong BOQ dự án) ở Module 5\.** | **30%** |
|  | **3\. Tỷ lệ nghiệm thu từng phần đạt chuẩn lần đầu.** | **Tính tỷ lệ: (Số biên bản nghiệm thu được CĐT/GS ký duyệt ngay lần đầu) / (Tổng số yêu cầu nghiệm thu được phát đi).** | **20%** |
|  | **4\. Số lượng lỗi vi phạm an toàn lao động (HSE).** | **Đếm tổng số vụ việc vi phạm an toàn hoặc vệ sinh công nghiệp bị ghi nhận, lập biên bản phạt trong Module 4\.** | **20%** |
| **Nhân viên Mua hàng (Procurement)** | **1\. Tỷ lệ tối ưu hóa chi phí mua sắm vật tư.** | **So sánh đối chiếu: (Đơn giá mua thực tế thương thảo ký kết) so với (Đơn giá định biên hạn mức dự toán trong BOQ).** | **40%** |
|  | **2\. Tiến độ cung ứng vật tư ra công trường.** | **Đo thời gian từ lúc lệnh MR hiện trường được duyệt đến khi thủ kho xác nhận xe hàng kiểm kho nhập bãi thành công.** | **30%** |
|  | **3\. Điểm đánh giá chất lượng nhà thầu phụ hệ thống.** | **Lấy điểm số trung bình chấm điểm định kỳ (Vendor Rating) của các đối tác do nhân viên đó quản lý và lựa chọn.** | **30%** |
| **Kế toán Dự án** | **1\. Tỷ lệ thu hồi công nợ Chủ đầu tư đúng hạn.** | **Theo dõi và chấm điểm dựa trên số ngày dòng tiền về tài khoản so với mốc điều khoản thanh toán trong hợp đồng ở Module 6\.** | **40%** |
|  | **2\. Tốc độ xử lý hồ sơ thanh toán đối tác.** | **Đo khoảng thời gian từ lúc nhận đề nghị thanh toán hợp lệ của thầu phụ đến khi lệnh duyệt chi thực tế được hoàn tất.** | **30%** |
|  | **3\. Tỷ lệ chính xác dòng tiền và số liệu báo cáo P\&L.** | **Đếm số lần phát hiện sai lệch số liệu dòng tiền, hóa đơn hoặc phải điều chỉnh hạch toán thủ công sau khi đã khóa kỳ.** | **30%** |

## ---

**YÊU CẦU KỸ THUẬT GIAO DIỆN (UI/UX) CHO ĐỘI PHÁT TRIỂN APP**

> 1. **Mobile-First cho khối Hiện trường: Giao diện hiển thị trên điện thoại của các Module 1 (Khảo sát), Module 4 (Nhật ký, Nghiệm thu, Punchlist) và Module 5 (Nhập xuất kho) phải thiết kế nút bấm to, thao tác tối giản dưới 3 lần chạm, tối ưu tải ảnh nhanh trong điều kiện sóng 3G/4G công trường yếu. Có tính năng lưu trữ tạm thời (Offline Mode) khi mất mạng và tự đồng bộ khi có kết nối trở lại.**  
> 2. **Web-Dashboard trực quan cho Khối Văn phòng & Ban Giám đốc: Giao diện Web hiển thị trên máy tính tập trung vào các biểu đồ Gantt Chart, biểu đồ S-Curve tiến độ, bảng ma trận so sánh giá thầu và biểu đồ cột tài chính thực tế dòng tiền P\&L.**  
> 3. **Tích hợp API kết nối sâu với Google Workspace: Hệ thống sử dụng phương thức xác thực phân quyền tài khoản (OAuth 2.0). Mọi hành động khởi tạo dự án, duyệt file bắt buộc phải kích hoạt lệnh chạy tự động cấu trúc folder và phân quyền thư mục tương ứng trên Google Drive thông qua hệ thống API chính thức.**