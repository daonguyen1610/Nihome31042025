# Góp ý lập trình v2 – NICON Management System

*19/11/2026 · @Trí Nguyên Võ*

Bản này thay cho tài liệu góp ý ngày 19/11/2026 (bản 1, dạng nhận xét). Nội dung được viết lại thành danh sách việc cho đội lập trình, xếp theo 3 giai đoạn triển khai của PRD v2. Mỗi việc có mã, hiện trạng quan sát được trên niconvn.com/admin, yêu cầu và tiêu chí nghiệm thu. Tài liệu gốc để đối chiếu: PRD v2 và Bộ quy trình quản lý công việc NICON – ISO 9001:2015 (ban hành lần 2).

## Mục lục

1. [Nguyên tắc và định nghĩa hoàn thành](#1-nguyên-tắc-và-định-nghĩa-hoàn-thành)
2. [Giai đoạn 1 – Nền](#2-giai-đoạn-1--nền)
3. [Giai đoạn 2 – Dự án mẫu](#3-giai-đoạn-2--dự-án-mẫu)
4. [Giai đoạn 3 – Quản trị](#4-giai-đoạn-3--quản-trị)
5. [Dọn dữ liệu và cấu hình trước khi chạy thật](#5-dọn-dữ-liệu-và-cấu-hình-trước-khi-chạy-thật)
6. [Kịch bản nghiệm thu end-to-end](#6-kịch-bản-nghiệm-thu-end-to-end)
7. [Trả lời câu hỏi của đội lập trình](#7-trả-lời-câu-hỏi-của-đội-lập-trình-19112026)

---

## 1. Nguyên tắc và định nghĩa hoàn thành

Một hạng mục chỉ được coi là xong khi đạt đủ các điều kiện sau, ngoài tiêu chí riêng của hạng mục đó:

- Nhập một lần: dữ liệu đã có ở module khác thì đọc lại, không bắt người dùng nhập lại (ngày và giá trị HĐ, BOQ, khách hàng, NCC).

- Gắn mã PJ: mọi bản ghi nghiệp vụ gắn mã dự án vận hành; việc nội bộ gắn mã phòng ban.

- Đúng ma trận duyệt: tối đa 3 bước, trả lại phải có lý do, hỗ trợ ủy quyền, có thông báo cho người cần xử lý.

- Xuất đúng mẫu: biểu mẫu xuất PDF / Word theo mã trong danh mục 35 mẫu; biên bản đã ký tự lưu vào đúng thư mục Drive.

- Không xóa cứng: chỉ Hủy / Lưu trữ có lý do; mọi thao tác ghi nhật ký với đúng người thực hiện.

- Phân quyền: kiểm thử bằng ít nhất 3 vai trò (người lập, người duyệt, người ngoài đội dự án), không dùng Super Admin để nghiệm thu.

- Mobile: màn hình dùng ở công trường phải dùng được trên điện thoại, thao tác chính ≤ 3 lần chạm.

- Định dạng: ngày dd/mm/yyyy; tiền VND số nguyên; nhãn tiếng Việt đúng ngữ cảnh.

Mã việc: N = Giai đoạn 1 (Nền), M = Giai đoạn 2 (Dự án mẫu), Q = Giai đoạn 3 (Quản trị). Cột "Trạng thái" để đội lập trình cập nhật.

## 2. Giai đoạn 1 – Nền

Mục tiêu: hệ thống an toàn, đúng vai trò, và mọi nhân viên dùng hằng ngày để giao việc và báo cáo. Tiêu chí hoàn thành giai đoạn: toàn bộ nhân viên văn phòng nộp báo cáo ngày trên web 2 tuần liên tục. Làm N-01 đến N-04 trước vì đây là lỗi an toàn dữ liệu.

| Mã | Hạng mục | Hiện trạng (quan sát 19/11) | Yêu cầu | Tiêu chí nghiệm thu | Trạng thái |
| --- | --- | --- | --- | --- | --- |
| N-01 | Tích hợp Google Drive | Trang dự án PJ-2026-0002 báo "Không thể giải mã cấu hình Google Drive"; nhật ký có project-document.sync_failure | Khóa mã hóa secret cố định giữa các lần deploy; nhập lại cấu hình; nút "Kiểm tra kết nối"; cảnh báo Admin khi đồng bộ lỗi | Tải lên và xem file trên trang dự án C-Hawk thành công; giả lập lỗi → Admin nhận cảnh báo | Chưa làm |
| N-02 | Nhật ký hoạt động | Giữ 30 ngày, có nút "Xoá trước ngày"; đăng nhập ghi người thực hiện là "anonymous" | Bỏ nút xóa và cấu hình xóa tự động; lưu không thời hạn; ghi đúng người thực hiện | Không vai trò nào xóa được nhật ký; dòng đăng nhập hiện đúng tên người dùng | Chưa làm |
| N-03 | Bỏ xóa cứng | Báo giá có nút "Xóa vĩnh viễn"; nhật ký có operational-project.delete, opportunity.delete, quote.delete | Đổi thành Hủy / Lưu trữ có lý do; chặn hủy khi đã có dữ liệu liên kết | Không còn nút xóa vĩnh viễn; hủy dự án đã có HĐ bị chặn | Chưa làm |
| N-04 | Đăng nhập & bảo mật | Trang đăng nhập có "Đăng ký ngay"; đăng nhập bằng số điện thoại + mật khẩu | Tắt tự đăng ký; đăng nhập bằng tài khoản Google Workspace công ty; xác thực 2 lớp cho Admin và BGĐ | Không tạo được tài khoản từ trang đăng nhập; Admin bắt buộc 2 lớp | Chưa làm |
| N-05 | 16 vai trò | 25 vai trò; CM_MANAGER, CIVIL_ENGINEER 0 quyền; 9 Super Admin; PM dự án C-Hawk mang vai trò Super Admin | Cấu hình 16 vai trò theo PRD v2 mục 4; gộp và bỏ vai trò thừa; Super Admin tối đa 2 | Mỗi người dùng có đúng 1 vai trò nghiệp vụ; ma trận quyền xuất ra khớp PRD v2 | Chưa làm |
| N-06 | Động cơ luồng duyệt | 5 luồng: Ký HĐ, Duyệt Concept, IFC, Duyệt báo giá, Nộp thầu | Luồng cấu hình được, tối đa 3 bước, hạn mức A/B/C, ủy quyền có thời hạn, trả lại kèm lý do; đủ 15 loại trong ma trận (ISO mục 6). Chờ BGĐ chốt giá trị A/B/C | MR vượt A tự chuyển BGĐ; người duyệt nghỉ → người được ủy quyền duyệt, lịch sử ghi "duyệt thay" | Chờ BGĐ chốt |
| N-07 | Thông báo | Trung tâm thông báo trống dù đợt thu C-Hawk quá hạn từ 15/11 | Thông báo app + email: giao việc, sắp / quá hạn, chờ duyệt, bị trả lại, cổng mở, đợt thu quá hạn | Đợt thu quá hạn của C-Hawk sinh thông báo cho Kế toán và PM | Chưa làm |
| N-08 | Công việc (M0) | Có nhóm công việc, phân công, việc con; mã dạng WP-2026-C7DC56A144FB | Màn hình "Việc của tôi" (hôm nay, sắp hạn, quá hạn, phiếu chờ duyệt), Kanban, mã ngắn PJ-…-WPnn, quá hạn 2 lần báo TBP | Nhân viên thấy đủ việc và phiếu chờ mình trên một màn hình | Chưa làm |
| N-09 | Báo cáo ngày / tuần (CV-02) | Chưa có; thư mục Drive "Báo cáo công việc" trống | Theo mẫu QLNS-M05: tự điền việc đang làm, % hoàn thành, đề xuất, kế hoạch mai, ảnh; nhắc 17:00; TBP nhận xét; tự tổng hợp tuần | Nhân viên nộp một báo cáo dưới 2 phút; TBP xem được tổng hợp tuần của phòng | Chưa làm |
| N-10 | Động cơ Phiếu đề xuất | Mỗi chức năng một kiểu (đề xuất nguồn lực, đề nghị thanh toán…) | Một động cơ: loại phiếu cấu hình được (trường, luồng duyệt, mẫu xuất). Làm trước PH-02, PH-05, PH-11, PH-14 | Thêm một loại phiếu mới bằng cấu hình, không phải viết màn hình mới | Chưa làm |
| N-11 | Cổng giai đoạn | Trạng thái dự án chọn tay; C-Hawk "Đang thực hiện" mà chưa có tiến độ tổng thể, BOQ, thiết kế vẫn ở "ý tưởng"; trang BCH mặc định mở dự án Duy Anh chưa có HĐ | 7 cổng với checklist (ISO mục 3); chặn chức năng của giai đoạn sau; trang tổng dự án hiện giai đoạn và checklist cổng kế tiếp | C-Hawk bị báo thiếu tiến độ tổng thể và BOQ; trang BCH chỉ liệt kê dự án đã qua cổng 4 | Chưa làm |
| N-12 | Drive 8 thư mục | Cài đặt Drive có 9 đường dẫn rời theo loại tài liệu | Tạo cây 8 thư mục theo giai đoạn khi mở PJ (PRD v2 mục 7); Shared Drive; quyền theo RACI; biên bản khóa → PDF tự lưu | Mở PJ mới → đúng cây thư mục; thành viên rời đội dự án mất quyền xem | Chưa làm |
| N-13 | Tách website | Menu, Cài đặt (slideshow) và Dashboard (bài đăng, "49/127 dự án" của website) trộn với hệ thống điều hành | Khu quản trị website riêng; dashboard điều hành theo vai trò | Người dùng nghiệp vụ không thấy menu website; dashboard chỉ còn số liệu vận hành | Chưa làm |
| N-14 | Định dạng & nhãn | Tiền có số lẻ (2.044.047.119,6 ₫); ngày lẫn "10 thg 8, 2026" và "10/8/2026"; bộ lọc "Chủ nhiệm thiết kế" hiện "Tất cả quản lý dự án"; trang Phải trả lọc theo Khách hàng / Sales | Sửa theo mục 1 điểm 8; đợt thanh toán làm tròn, phần lẻ dồn đợt cuối | Rà toàn bộ màn hình, không còn các lỗi đã nêu | Chưa làm |

## 3. Giai đoạn 2 – Dự án mẫu

Mục tiêu: một dự án đang chạy đi hết giai đoạn 3–5 trên web, không dùng Excel riêng cho MR, nghiệm thu, thanh toán. Ưu tiên M-04, M-09, M-10, M-13 vì đây là các chỗ dữ liệu đang đứt.

| Mã | Hạng mục | Hiện trạng (quan sát 19/11) | Yêu cầu | Tiêu chí nghiệm thu | Trạng thái |
| --- | --- | --- | --- | --- | --- |
| M-01 | Cổng 1 – đánh giá cơ hội | Chưa có bước BGĐ duyệt đi tiếp / dừng | PH-01 kèm đánh giá rủi ro sơ bộ; BGĐ duyệt | Cơ hội chưa qua cổng 1 không tạo được báo giá | Chưa làm |
| M-02 | Báo giá & đơn giá | Danh mục đơn giá trống; báo giá QT-2026-0001 của C-Hawk vẫn "Nháp – Chờ giá đầu vào", 0 ₫ | 3 loại báo giá (thiết kế / thi công / thiết kế – thi công); nạp đơn giá có hiệu lực; hiển thị biên lợi nhuận dự kiến | Lập được báo giá suất đầu tư từ đơn giá có hiệu lực | Chưa làm |
| M-03 | Đấu thầu | Có gói thầu, checklist; RFQ chỉ mở sau khi có dự án vận hành | Bảng phân chia công việc thầu tự sinh việc; hỏi giá NCC (PH-03) ngay ở giai đoạn thầu | Tạo gói thầu → sinh việc cho KD, Thiết kế, Đấu thầu; gửi RFQ khi chưa có PJ | Chưa làm |
| M-04 | Ký HĐ tự cập nhật dữ liệu | C-Hawk: HĐ ký 10/8/2026 nhưng cơ hội ghi chốt 14/10/2026; 3 khung ngày khác nhau (dự án, HĐ, thiết kế); KH vẫn "Tiềm năng"; Sales trên HĐ khác Sales trên cơ hội | Khi HĐ "Đã ký": khóa báo giá trúng, cơ hội = Thắng, KH = Đang hợp tác, Sales lấy từ cơ hội, khung ngày dự án lấy từ HĐ; cảnh báo khi module khác vượt khung | Sau khi chuẩn hóa, C-Hawk chỉ còn một khung ngày và một Sales phụ trách | Chưa làm |
| M-05 | Thiết kế | 3 giai đoạn, IFC; hồ sơ mang tên cơ hội thay vì mã PJ; hạn TK C-Hawk 14/10/2027 sau cả ngày kết thúc thi công | Mốc từng giai đoạn; bắt buộc lý do Rev; BB-02 nghiệm thu hồ sơ → mở mốc thu tiền thiết kế; sổ shop drawing (PH-07) | Chỉ Rev "lỗi nội bộ" tính vào KPI; ký BB-02 → Kế toán thấy mốc thu mới | Chưa làm |
| M-06 | Pháp lý | Checklist gắn theo "dự án thiết kế"; chưa có hạng mục nào | Gắn mã PJ; trạng thái Đã nộp → Đang thụ lý → Cần bổ sung → Có kết quả; đếm số lần bị trả; yêu cầu sửa tạo việc cho Thiết kế | Hồ sơ bị trả 2 lần → KPI Pháp lý ghi đúng 2 | Chưa làm |
| M-07 | Chuẩn bị thi công | "Điều kiện khởi công" đã có khung trong trang Tiến độ | Gộp BB-03 (kế hoạch chất lượng, QĐ BCH) và BB-04 (giao nhận mặt bằng) vào checklist cổng 4 | Thiếu BB-04 thì không mở được nhật ký công trường | Chưa làm |
| M-08 | Tiến độ | Gantt / S-curve cần tiến độ tổng thể, C-Hawk chưa có | Tiến độ cuốn chiếu 2 tuần do BCH cập nhật; phiếu gia hạn tiến độ (PH-09) | Họp tuần xem được kế hoạch 2 tuần và độ lệch | Chưa làm |
| M-09 | Chất lượng & nghiệm thu | 0 mẫu QA/QC được phát hành nên BCH không lập được hồ sơ | Nạp sẵn mẫu BB-06, BB-07 từ biểu mẫu ISO; nghiệm thu 2 bước nội bộ → CĐT; chạy thử thiết bị đơn / liên động | BCH lập được hồ sơ QA/QC đầu tiên mà không cần tạo mẫu | Chưa làm |
| M-10 | Phát sinh, RFI, duyệt mẫu vật tư | Chưa có | PH-05 VO (cập nhật BOQ và phụ lục HĐ), PH-08 RFI, PH-06 duyệt mẫu | MR vượt 100% BOQ bị chặn cho đến khi VO được duyệt | Chưa làm |
| M-11 | Cung ứng | Danh sách NCC trống; có RFQ và so sánh giá | Bắt buộc ≥ 3 báo giá (NCC độc quyền phải ghi lý do); chỉ chọn từ danh sách được duyệt; đánh giá 5 nhóm tiêu chí (BB-10); đơn hàng PH-04 | Không chốt được NCC khi dưới 3 báo giá mà không có lý do | Chưa làm |
| M-12 | Kho & thiết bị | Kho theo dự án, chỉ có vật tư | Thêm máy móc thiết bị, công cụ dụng cụ, điều chuyển giữa công trường (BB-09); lý lịch máy, lịch bảo trì, hiệu chuẩn trắc đạc; PH-13 | Xem được máy nào đang ở công trường nào và hạn bảo trì | Chưa làm |
| M-13 | Tài chính | Phải thu, phải trả, đề nghị thanh toán; đợt thu quá hạn vẫn "Chưa yêu cầu" | Đợt chi tuần (nộp trước chiều thứ 6 – BGĐ duyệt sáng thứ 7 – chi sáng thứ 3); đối soát 3 bên; tạm ứng và hoàn ứng; bảo lưu; quyết toán; thanh lý; nhắc nợ tự động (HD-06) | Đề nghị không khớp 3 bên không vào được đợt chi; còn tạm ứng chưa hoàn thì không tạm ứng mới | Chưa làm |
| M-14 | Thư viện quy trình | 8 trang ảnh và file tĩnh của bộ ISO 2018 | Đăng bộ quy trình lần 2; danh mục 35 mẫu có mã, phiên bản, ngày hiệu lực; mỗi bước có nút mở đúng chức năng; bộ 2018 chuyển "Hết hiệu lực" | Từ trang QT-05 bấm thẳng vào tạo MR | Chưa làm |
| M-15 | Xuất biểu mẫu | Chưa thấy xuất theo mẫu ISO | Xuất PDF / Word đúng 35 mẫu, có mã biểu mẫu và mã PJ ở chân trang | Mẫu xuất ra in ký được ngay, không phải chỉnh tay | Chưa làm |

## 4. Giai đoạn 3 – Quản trị

Mục tiêu: KPI tự động và dashboard dùng được cho quản lý. Chỉ bắt đầu khi giai đoạn 2 đã có dữ liệu thật, vì KPI tính trên dữ liệu thiếu sẽ gây tranh cãi.

| Mã | Hạng mục | Hiện trạng (quan sát 19/11) | Yêu cầu | Tiêu chí nghiệm thu | Trạng thái |
| --- | --- | --- | --- | --- | --- |
| Q-01 | KPI 11 vị trí | 6 nhóm KPI, nhiều chỉ số chưa có mục tiêu, 100% định lượng | 11 vị trí theo PRD v2 mục 6; 70% định lượng + 30% đánh giá quản lý; quy đổi 0–120; loại trừ có duyệt; nhập trọng số theo %. Chờ BGĐ chốt tỷ lệ và mục tiêu | 2 kỳ liên tiếp khóa đúng hạn, mọi chỉ số "Có dữ liệu" | Chờ BGĐ chốt |
| Q-02 | Dashboard theo vai trò | Dashboard hiện số liệu website | BGĐ: dòng tiền, phải thu quá hạn, tiến độ các dự án, cảnh báo đỏ. PM, BCH, cá nhân: việc, phiếu, mốc của mình | Mỗi vai trò mở hệ thống thấy ngay việc cần làm hôm nay | Chưa làm |
| Q-03 | App hiện trường offline | Chưa chốt hình thức app | Chốt app cài đặt hay web dạng app; lưu tạm khi mất mạng, tự đồng bộ; nén ảnh | Lập nhật ký và biên bản QA/QC khi tắt mạng, bật mạng lên tự đồng bộ không mất dữ liệu | Chờ BGĐ chốt |
| Q-04 | P&L và dòng tiền dự án | Chưa thấy | Thực thu / thực chi, biên lợi nhuận gộp, so với ngân sách gốc (BOQ HĐ + VO) | Số liệu P&L khớp báo cáo Kế toán cho dự án mẫu | Chưa làm |
| Q-05 | Xuất dữ liệu kế toán | Chưa có | Xuất theo định dạng phần mềm kế toán đang dùng (Kế toán cung cấp mẫu) | Kế toán nhập được file xuất mà không sửa tay | Chưa làm |
| Q-06 | Cổng 6–7: bàn giao, bảo hành, đóng dự án | Có tab "Bàn giao dự án", "Hồ sơ hoàn công" | BB-12, HD-04, HD-05; PH-12 khiếu nại / bảo hành; đếm hạn bảo hành; checklist đóng dự án; bài học kinh nghiệm | Dự án mẫu đóng được khi công nợ = 0 và không còn khiếu nại mở | Chưa làm |

## 5. Dọn dữ liệu và cấu hình trước khi chạy thật

Làm song song với giai đoạn 1, do Quản trị hệ thống và các TBP phối hợp. Dữ liệu sai lúc bắt đầu sẽ làm sai cổng giai đoạn và KPI về sau.

- [ ] PJ-2026-0002 C-Hawk: lấy ngày theo HĐ (ký 10/8/2026; thực hiện 15/11/2026 – 15/2/2027) cho dự án, thiết kế và cơ hội; cơ hội = Thắng; KH = Đang hợp tác; xử lý báo giá QT-2026-0001 (nhập giá trị trúng hoặc đánh dấu không dùng); thống nhất một Sales phụ trách; nhập tiến độ tổng thể, BOQ hợp đồng, ngày kế hoạch đợt thu 2–5; gán PM đúng vai trò PM

- [ ] PJ-2026-0003 Duy Anh: cơ hội mới ở giai đoạn Tiếp cận, chưa có HĐ → chuyển về giai đoạn 1, không phải dự án vận hành; rà lại khung ngày 1/8/2026 – 31/12/2027

- [ ] PJ-2026-0001 Nihome: giá trị 0 ₫, người phụ trách là Super Admin → xác nhận dự án thật hay dữ liệu thử; thử thì hủy

- [ ] Tài khoản: khóa các tài khoản thử (email ...@nihome.vn, số điện thoại 091100001x–2x); chuyển nhân sự dùng Gmail cá nhân sang email công ty; gán lại 16 vai trò; Super Admin tối đa 2

- [ ] Danh mục dùng chung: NCC / thầu phụ (mỗi nhóm hàng chính ≥ 3); đơn giá có hiệu lực; mẫu QA/QC; WBS mẫu cho nhà xưởng; checklist pháp lý mẫu

- [ ] Google Drive: nhập lại cấu hình; chuyển sang Shared Drive; dời HO SO NHAN VIEN 2019.pdf và THÔNG TIN NHÂN VIÊN.XLS khỏi thư mục "Quản lý chung"; chuyển bộ ISO 2018 vào "Lưu trữ – hết hiệu lực"; dọn file tạm ~$..., ~WRL...tmp

- [ ] Cấu hình chờ BGĐ chốt: hạn mức A/B/C; tỷ lệ KPI 70/30 và mục tiêu; ngưỡng biên lợi nhuận cổng 2; giờ nhắc báo cáo; ngày khóa KPI

## 6. Kịch bản nghiệm thu end-to-end

Chạy trên môi trường staging với một dự án thử mô phỏng C-Hawk (D&B nhà ăn + văn phòng). Mỗi bước dùng đúng tài khoản của vai trò đó, không dùng Super Admin. Giai đoạn 2 chỉ được coi là xong khi cả 12 bước đạt kết quả mong đợi.

| Bước | Vai trò | Thao tác | Kết quả mong đợi |
| --- | --- | --- | --- |
| 1 | Sales | Tạo lead → cơ hội, điền PH-01, khảo sát BB-01 trên điện thoại | Ảnh khảo sát vào 01_CoHoi_BaoGia; BGĐ nhận thông báo duyệt |
| 2 | BGĐ | Duyệt cổng 1 | Tạo mã PJ và cây thư mục Drive; mở được báo giá (trước đó bị chặn) |
| 3 | Mua hàng, KS đấu thầu, Sales | Hỏi giá 3 NCC; lập báo giá D&B; TBP KD kiểm tra → BGĐ duyệt (cổng 2) | Báo giá có biên lợi nhuận; lịch sử duyệt 2 bước |
| 4 | Sales, Pháp lý, Kế toán, Giám đốc | Lập HD-01, ký HĐ (cổng 3) | Dự án chuyển giai đoạn 3, khóa báo giá, cơ hội = Thắng, KH = Đang hợp tác, lịch mốc thu |
| 5 | PM | Gán đội RACI, lập tiến độ tổng thể ngoài khung HĐ | Bị cảnh báo vượt khung ngày HĐ |
| 6 | KTS, Chủ nhiệm TK, PM, Pháp lý | Phát hành bản vẽ Rev 00 → Rev 01 (lý do: CĐT); xin phép bị trả 1 lần; ký BB-02; qua cổng 4 | Rev 01 không trừ KPI KTS; KPI Pháp lý ghi 1 lần trả; mốc thu tiền thiết kế mở |
| 7 | CHT, KS hiện trường | BB-03, BB-04; nhật ký ngày đầu trên điện thoại khi tắt mạng | Nhật ký tự đồng bộ khi có mạng (nếu Q-03 đã làm) |
| 8 | KS hiện trường, CHT, PM | MR đạt 90% BOQ, rồi MR vượt 100% | MR đầu cảnh báo vàng/cam; MR sau bị chặn |
| 9 | CHT, Phòng CM, PM | Lập VO cho phần vượt → duyệt | BOQ cập nhật; MR bị chặn được mở |
| 10 | KS QA/QC, CHT, Phòng CM | Nghiệm thu nội bộ → CĐT (BB-07), một lần không đạt → punchlist → đóng | Tỷ lệ đạt lần đầu và thời gian đóng punchlist hiện trong KPI |
| 11 | QS, Phòng CM, PM | Đợt khối lượng BB-11 → CĐT ký (cổng 5) | Mốc thu đợt 2 mở; Kế toán nhận thông báo lập HD-06 |
| 12 | Mua hàng, Kế toán, BGĐ | Đề nghị thanh toán NCC thiếu phiếu nhập kho; bổ sung; vào đợt chi thứ 6 → BGĐ duyệt thứ 7 | Lần đầu bị trả lại (thiếu đối soát 3 bên); lần sau vào đợt chi; công nợ cập nhật sau ngày chi |

## 7. Trả lời câu hỏi của đội lập trình (19/11/2026)

Trả lời theo PRD v2 và Bộ quy trình ISO lần 2. Câu nào cần BGĐ hoặc Kế toán chốt thì ghi rõ ở cuối câu. Riêng câu 1 có một điều chỉnh nhỏ so với PRD v2: mã PJ được tạo từ cổng 1 thay vì cổng 3. PRD v2 và bộ ISO đã được sửa theo.

### Dự án và phân công

#### 1. Công việc trước hợp đồng — không chuyển đi đâu, vẫn nằm ở dự án.

- Mã PJ được tạo khi cơ hội qua cổng 1 (BGĐ duyệt "đi tiếp"). Trước cổng 1 chỉ có lead / cơ hội. Cách này khớp với web hiện tại (đã tạo dự án vận hành từ giai đoạn cơ hội) và với thư mục Drive 01_CoHoi_BaoGia.

- Việc thiết kế sơ bộ, hỏi giá, hồ sơ thầu làm ở giai đoạn 1–2 đã gắn PJ từ đầu. Khi ký HĐ, dự án chỉ đổi giai đoạn, không có bước chuyển dữ liệu. Cơ hội trượt thì dự án đóng với trạng thái "Trượt" kèm lý do (không xóa).

- Công việc không gắn vào hợp đồng. Việc gắn vào PJ + giai đoạn (và WBS nếu có). Chỉ các đối tượng liên quan đến tiền mang thêm số HĐ: mốc thu/chi, VO, đợt QS, đề nghị thanh toán, quyết toán.

- Một dự án nhiều HĐ (thiết kế riêng, thi công riêng): tất cả cùng một PJ. Việc thiết kế thuộc giai đoạn 4, việc thi công thuộc giai đoạn 5. Biên bản nghiệm thu hồ sơ thiết kế mở mốc thu của HĐ thiết kế; đợt QS mở mốc thu của HĐ thi công. HĐ mua vào (NCC, thầu phụ) cũng gắn PJ, chọn HĐ bán ra liên quan nếu cần tính P&L riêng.

#### 2. Mã dự án — PJ-yyyy-nnnn.

- yyyy = năm tạo mã (năm qua cổng 1); nnnn = số thứ tự trong năm, bắt đầu lại từ 0001 mỗi năm. Vì có năm nên không bao giờ trùng giữa các năm; trong cùng năm hệ thống không cấp trùng.

- Không đưa loại hợp đồng vào mã: loại HĐ là một trường và có thể thay đổi (ví dụ bắt đầu là HĐ thiết kế rồi ký thêm thi công), còn mã thì không đổi.

- Nút "Đổi mã dự án" hiện có: chỉ Quản trị hệ thống dùng, chỉ trước cổng 3, bắt buộc ghi lý do vào nhật ký.

- Các mã con theo mã PJ: việc PJ-2026-0002-WP01, phiếu PJ-2026-0002-PH02-001… Số hợp đồng giữ đúng số trên HĐ thật.

#### 3. Giao việc cho phòng khác — do "chủ dự án" của giai đoạn đó giao.

- Giai đoạn 1–2 (trước HĐ): Sales phụ trách cơ hội là chủ dự án, được giao việc cho Thiết kế, Đấu thầu, Cung ứng (đúng QT-02 bước 1).

- Từ cổng 3 đến khi đóng dự án: PM là chủ dự án. Sales vẫn ở trong đội (vai trò C/I), không giao việc cho phòng khác nữa.

- TBP luôn giao việc được cho người trong phòng mình. CHT giao việc trong BCH.

- Giao sang phòng khác: nếu người nhận đã có trong đội dự án thì giao thẳng; nếu chưa thì việc vào hàng chờ của TBP phòng nhận để phân người.

- Nhân viên khác không giao việc sang phòng khác; khi cần thì tạo phiếu (RFI, MR…) hoặc đề nghị chủ dự án giao.

#### 4. Công việc nội bộ — chỉ theo dõi, không có bước duyệt; không có trọng số KPI riêng.

- Công việc (cả nội bộ lẫn dự án) đi theo trạng thái Mới → Đang làm → Chờ xác nhận → Xong. Người giao bấm xác nhận "Xong"; đó không phải luồng duyệt.

- Việc nào cần duyệt thật (tiền, vật tư, nhân sự…) thì dùng Phiếu, không dùng Công việc.

- KPI: 70% định lượng lấy từ dữ liệu nghiệp vụ, không đếm việc nội bộ. Việc nội bộ đi vào phần 30% đánh giá của quản lý (tiêu chí "mức độ hoàn thành"). Hệ thống hiển thị cho TBP số việc đúng hạn / quá hạn của từng người trong kỳ để làm căn cứ chấm.

#### 5. Phòng ban và KPI.

- Sơ đồ chính thức là mục 2 của Bộ quy trình ISO lần 2 (có hiệu lực khi BGĐ ký). File "Sơ đồ tổ chức" năm 2018 trên Drive đã hết hiệu lực.

- 6 khối: Phát triển kinh doanh; Thiết kế; Quản lý dự án – Kỹ thuật thi công; Cung ứng (phòng riêng); Kế toán – Tài chính; Hành chính nhân sự. Pháp lý thuộc khối Quản lý dự án – Kỹ thuật thi công (cùng Phòng CM, BCH, Đấu thầu). Khớp với nhóm vai trò trên web hiện tại.

- KPI 11 vị trí: PRD v2 mục 6. Các vai trò còn lại (đề xuất, BGĐ chốt):

| Vai trò | KPI đề xuất |
| --- | --- |
| Trưởng bộ phận | 70% = điểm định lượng trung bình của nhân sự trong phòng; 30% = BGĐ đánh giá |
| Chủ nhiệm thiết kế | 70% = KPI thiết kế (đúng mốc, đạt lần đầu, lỗi từ công trường) tính trên các dự án mình phụ trách; 30% = TBP Thiết kế đánh giá |
| Thủ kho | 70% = chênh lệch kiểm kê / tồn sổ sách và phiếu kho nhập đúng ngày; 30% = CHT đánh giá |
| BGĐ, Quản trị hệ thống | Không tính KPI tự động |

### Thiết kế và pháp lý

#### 6. Phân vai thiết kế — hiểu đúng, bổ sung một điểm: phát hành IFC cần PM duyệt.

| Vai | Làm gì trên hệ thống |
| --- | --- |
| KTS / KS thiết kế | Lập bản vẽ, tạo Rev, chọn lý do Rev |
| Chủ trì (bộ môn / dự án) | Giao và xác nhận "Xong" từng đầu việc trong bộ môn. Đây là xác nhận công việc, không phải một bước duyệt riêng |
| Chủ nhiệm thiết kế | Chốt phương án (duyệt Concept), chuyển giai đoạn thiết kế, bước Kiểm tra trước khi phát hành, ký BB-02 bàn giao / nghiệm thu hồ sơ với CĐT |
| PM | Bước Phê duyệt phát hành IFC (bản vẽ ra công trường ảnh hưởng chi phí và tiến độ); xác nhận cổng 4 |

Trưởng phòng và phó phòng Thiết kế không nhận thông báo mặc định của mọi dự án (tránh nhiễu). TBP chỉ nhận: việc giao cho phòng chưa phân người; phiếu cần mình xử lý; cảnh báo trễ mốc giai đoạn thiết kế; báo cáo tuần tổng hợp của phòng. Muốn theo dõi sát dự án nào thì tự thêm mình vào đội dự án với vai trò I. Phó phòng nhận thông báo của TBP khi được ủy quyền.

#### 7. Xóa hồ sơ thiết kế — không ai xóa; chỉ Hủy hoặc Thay thế.

| Trường hợp | Xử lý | Ai được làm |
| --- | --- | --- |
| Bản vẽ / file nháp, chưa trình duyệt nội bộ | Hủy kèm lý do (vẫn còn trong lịch sử) | Người lập hoặc Chủ nhiệm TK |
| Đã trình nội bộ nhưng không đạt | Trả lại → sửa thành Rev mới; không hủy | — |
| Đã trình khách / đã phát hành IFC | Không xóa, không hủy. Chỉ thay bằng Rev mới; bản cũ tự chuyển "Hết hiệu lực" và bị thu hồi phía công trường | — |
| Cả bộ hồ sơ thiết kế không tiếp tục (khách dừng, trượt thầu) | Đóng bộ hồ sơ với lý do; dữ liệu giữ nguyên | Trưởng phòng Thiết kế |

Nút "Xoá" hiện có trên danh sách Hồ sơ thiết kế cần đổi thành "Đóng / Hủy" theo bảng trên (việc N-03).

#### 8. Giấy phép — một danh mục chung + checklist mẫu theo loại và quy mô.

- Danh mục chung: tất cả loại giấy phép / thủ tục công ty từng làm (xây dựng, PCCC, môi trường, đấu nối điện nước, vỉa hè, hoàn công…), mỗi loại có hồ sơ cần nộp và thời gian xử lý chuẩn.

- Checklist mẫu = tập con của danh mục, theo loại công trình và quy mô (ví dụ: nhà xưởng trong KCN, nhà xưởng ngoài KCN, nhà ở, công trình công cộng). Web đã có trang "Mẫu checklist pháp lý".

- Khi dự án vào giai đoạn 4, Pháp lý chọn mẫu → hệ thống sinh checklist → Pháp lý được thêm / bớt hạng mục có ghi lý do.

- Nội dung danh mục và các mẫu do bộ phận Pháp lý lập theo quy định hiện hành (việc dọn dữ liệu mục 5). Đội lập trình chỉ cần làm cơ chế "danh mục → mẫu → checklist dự án".

### BOQ, tiến độ

Nguyên tắc chung cho câu 9–10: hệ thống không bao giờ chặn việc ghi nhận sự thật (nhật ký, đo đạc, nghiệm thu nội bộ ghi đúng số thực tế). Hệ thống chỉ chặn các bước làm phát sinh tiền hoặc cấp thêm vật tư cho phần vượt: MR, đơn hàng, đề nghị thanh toán, khối lượng trình CĐT.

#### 9. Ngưỡng 85% / 95% / 100%.

Tính theo từng dòng BOQ: (khối lượng MR đã duyệt + MR đang chờ duyệt) / BOQ hiện hành. BOQ hiện hành = BOQ hợp đồng + các VO đã duyệt. Mục đích là báo sớm để kịp làm VO trước khi hết khối lượng, không phải thêm bước duyệt.

| Mốc | Hệ thống làm gì | Ai nhận thông báo | Ai phải duyệt |
| --- | --- | --- | --- |
| ≥ 85% (vàng) | Hiện cảnh báo trên MR và trên dòng BOQ | CHT, QS công trường | Không thêm bước; MR đi luồng thường (CHT → PM / BGĐ theo hạn mức A) |
| ≥ 95% (cam) | Như trên + yêu cầu QS ghi dự báo khối lượng còn cần đến khi xong hạng mục | CHT, QS, PM, Mua hàng | Không thêm bước; nếu dự báo vượt thì QS lập VO ngay |
| > 100% (đỏ) | Chặn MR vượt cho đến khi có VO được duyệt; phần trong BOQ vẫn duyệt bình thường | CHT, QS, PM, Phòng CM | Theo luồng VO: CHT/QS lập → Phòng CM kiểm tra → PM nếu ≤ B, BGĐ nếu > B |

#### 10. Khối lượng thực tế vượt BOQ (ví dụ BOQ 500 tấn thép, thực tế 600 tấn).

| Chỗ phát sinh | Cảnh báo hay chặn | Ai nhận |
| --- | --- | --- |
| Cấp vật tư (MR, đơn hàng): đã cấp 500 tấn, xin thêm 100 | Chặn phần vượt → phải có VO (câu 9) | CHT, QS, PM, Phòng CM |
| Đo đạc / nghiệm thu nội bộ ghi 600 tấn | Không chặn, ghi đúng 600; dòng BOQ chuyển đỏ | QS, PM |
| Đợt khối lượng gửi CĐT (BB-11) | Chặn đưa 100 tấn vượt vào đợt thanh toán khi chưa có VO được CĐT xác nhận; phần này nằm ở mục "Khối lượng chờ VO" | QS, PM, Kế toán |
| Thanh toán thầu phụ / NCC | Chặn phần vượt HĐ mua vào khi chưa có phụ lục | Mua hàng, Kế toán, PM |
| Vật tư xuất kho lớn hơn khối lượng nghiệm thu | Không chặn; tính là hao hụt, vào KPI Chỉ huy trưởng | CHT, PM |

VO cần ghi nguyên nhân (CĐT thay đổi / sai sót thiết kế / sai sót BOQ đấu thầu / hiện trường) để tính vào KPI đúng người (KS đấu thầu, Thiết kế, CHT).

#### 11. Tiến độ từ MS Project — nhập cả baseline và các cấp công việc.

- Nhập: cấp công việc (outline level) và mã WBS; tên; ngày bắt đầu / kết thúc; thời lượng; mốc (milestone); quan hệ phụ thuộc (FS / SS / FF + lag); baseline (ngày bắt đầu / kết thúc kế hoạch gốc). Baseline bắt buộc vì S-curve và KPI tiến độ so thực tế với baseline.

- Không nhập: tài nguyên, chi phí trong MS Project (chi phí lấy từ BOQ trên hệ thống).

- Định dạng: file XML của MS Project (Save As → XML) hoặc mẫu Excel do hệ thống cung cấp; không cần đọc trực tiếp .mpp. Không giới hạn số cấp; tối thiểu 2 cấp (hạng mục → công việc).

- Trọng số để tính %: theo giá trị BOQ của công việc nếu đã liên kết WBS ↔ BOQ; chưa liên kết thì theo thời lượng. Phòng CM được chỉnh tay.

- Phiên bản: mỗi lần nhập là một phiên bản. Baseline khóa khi PM duyệt kế hoạch. Sau đó BCH chỉ cập nhật thực tế trên web, không nhập lại từ MS Project. Đổi baseline phải có lý do và PM duyệt; nếu do CĐT đổi yêu cầu thì ghi lý do từ khách (web đã có trường này từ phiên bản 2).

### Duyệt và thanh toán

#### 12. Ai duyệt gì — theo ma trận phê duyệt (ISO lần 2, mục 6), tối đa 3 bước.

| Loại | Lập | Kiểm tra | Phê duyệt | Ghi chú |
| --- | --- | --- | --- | --- |
| Yêu cầu vật tư / thiết bị / nhân công (MR) | KS hiện trường | CHT | PM nếu ≤ A; BGĐ nếu > A | Nộp trước ít nhất 1 tuần; vượt BOQ thì chặn (câu 9) |
| QA/QC – nghiệm thu nội bộ | KS QA/QC | CHT | Phòng CM | Đạt thì mới mời CĐT/TVGS; chữ ký CĐT ghi nhận bằng bản scan biên bản đã ký |
| HSE – biên bản vi phạm | CHT / KS hiện trường | — | Phòng CM xác nhận và đóng sau khi khắc phục | Phạt tiền thầu phụ: PM duyệt, khoản phạt tự vào mục khấu trừ của đề nghị thanh toán kế tiếp. Sự cố nghiêm trọng: báo BGĐ trong ngày |
| QS – đợt khối lượng với CĐT | QS công trường | Phòng CM | PM → gửi CĐT | CĐT ký → mở mốc thu |
| QS – khối lượng thầu phụ / tổ đội | QS công trường | Phòng CM | PM | Duyệt xong chuyển thành căn cứ đề nghị thanh toán |
| Phát sinh (VO) | CHT / QS | Phòng CM | PM nếu ≤ B; BGĐ nếu > B | VO làm thay đổi giá với CĐT → thêm phụ lục HĐ, ký theo luồng ký hợp đồng |

Giá trị A và B: BGĐ chưa chốt (việc N-06). Khi chưa có số, đội lập trình để là tham số cấu hình.

Chính sách VO: NICON đã có quy trình kiểm soát phát sinh trong bộ ISO 2018 (TC-PS). Quy trình này nay là QT-05 bước 10 + mẫu PH-05 của bộ ISO lần 2, nhưng chưa có hạn mức và cách phân loại. Đề xuất nội dung để BGĐ ban hành:

- Phân loại VO theo nguyên nhân: CĐT thay đổi (tính tiền CĐT); sai sót thiết kế NICON; sai sót BOQ đấu thầu; điều kiện hiện trường. Ba loại sau là chi phí nội bộ, tính vào KPI người gây ra.

- Không thi công phần phát sinh trước khi VO được duyệt. Trường hợp khẩn cấp được làm trước, nhưng phải lập VO trong 48 giờ.

- VO tính tiền CĐT phải có xác nhận bằng văn bản của CĐT trước khi thi công.

- VO duyệt xong tự cập nhật BOQ hiện hành và ngân sách.

#### 13. Chứng từ thanh toán — bản nháp để Kế toán (chị K) xác nhận và bổ sung.

Bộ ISO 2018 chỉ có danh mục mẫu (MH-M04, TC-TP-M05, M06, M09, M11–M13, TQT-M01–M09), chưa quy định bộ chứng từ cho từng loại gói. Bảng dưới đây dựng từ các mẫu đó và quy tắc đối soát 3 bên. Nhờ chị K sửa trực tiếp vào bảng; đội lập trình làm theo bản đã xác nhận.

| Loại gói | Chứng từ mỗi đợt (ngoài đề nghị thanh toán PH-11, HĐ / đơn hàng, hóa đơn hợp lệ) | Đối soát 3 bên |
| --- | --- | --- |
| Chỉ vật tư (NCC) | Biên bản giao nhận + phiếu nhập kho có kiểm tra chất lượng (BB-09); chứng chỉ xuất xưởng / CO-CQ nếu HĐ yêu cầu | Đơn hàng ↔ phiếu nhập kho ↔ hóa đơn |
| Chỉ nhân công (thầu phụ nhân công, tổ đội giao khoán) | Bảng khối lượng đợt có chữ ký giám sát (BB-11); biên bản nghiệm thu chất lượng liên quan (BB-07); nếu khoán theo công thì thêm bảng chấm công (TC-TP-M09) | HĐ giao khoán ↔ khối lượng nghiệm thu ↔ đề nghị |
| Thuê máy | Biên bản bàn giao máy khi nhận và trả; bảng xác nhận ca / giờ máy có CHT ký | HĐ thuê ↔ ca máy xác nhận ↔ hóa đơn |
| Hỗn hợp (vật tư + nhân công, thầu phụ trọn gói) | Khối lượng lũy kế (BB-11 / TC-TP-M11); nghiệm thu chất lượng (BB-07); bảng khấu trừ: thu hồi tạm ứng, vật tư NICON cấp, phạt, tiền bảo lưu | HĐ ↔ khối lượng nghiệm thu (+ phiếu nhập nếu vật tư qua kho) ↔ đề nghị |
| Đợt quyết toán (mọi loại) | Thêm biên bản nghiệm thu hoàn thành / bàn giao, quyết toán (HD-04), biên bản thanh lý (HD-05) | — |

**Tạm ứng và trả trước (đề xuất, chị K điền số):**

- Chỉ tạm ứng / trả trước khi HĐ có điều khoản này; mức tối đa ____% giá trị HĐ.

- Tạm ứng trên ____ đồng phải có bảo lãnh tạm ứng.

- Thu hồi dần qua các đợt thanh toán theo tỷ lệ HĐ quy định; hệ thống tự tính số còn phải thu hồi.

- Giữ lại bảo lưu ____% mỗi đợt, giải toả khi hết bảo hành (HD-05).

- Tạm ứng nội bộ cho nhân viên: phải hoàn ứng trước khi ứng lần mới (QT-10 bước 7).
