---
title: "NICON — Tóm tắt buổi họp 03/10/2026 và các yêu cầu mới"
meeting_date: 2026-10-03
source: "_NICON__Meeting.docx (bản ghi tự động, ~3 giờ 8 phút)"
participants: "Nam, Trần Anh Dũng, Dao Nguyen (đội dev); Võ Trí Nguyên, Huỳnh Anh, chị Yến/Trinh (NICON)"
note: "Bản ghi tự động có nhiều chỗ nhận dạng sai; nội dung được suy luận theo ngữ cảnh. Các điểm chưa chắc ghi ở Mục 14."
---

# Tóm tắt buổi họp NICON ngày 03/10/2026

Buổi họp kéo dài khoảng 3 giờ. Nam (đội dev) demo luồng D&B từ Lead đến bàn giao. Anh Nguyên, chị Yến (trưởng phòng Thiết kế) và chị Huỳnh Anh (đầu mối) góp ý.

## 1. Phân quyền và vai trò

- **Quyền** là logic của hệ thống, chỉ dev thêm được. NICON tự tạo và chỉnh **vai trò**.
- Anh Nguyên yêu cầu **nhóm quyền theo phòng ban** để dễ chọn khi tạo vai trò. Năm phòng tương ứng năm module: Kinh doanh/CRM, Hành chính nhân sự, Thiết kế, Quản lý thi công, Tài chính kế toán.
- Dev sẽ cho đổi tên quyền. Vai trò mới thuộc phòng nào thì có sẵn bộ quyền mặc định của phòng đó để thêm bớt.
- Cần thêm các quyền mới: **phê duyệt thiết kế, phê duyệt chuyển bước, xem-only theo chỉ định**.

## 2. CRM

- Đưa ba tab **Dự án, Cơ hội, Hợp đồng** lên đầu menu CRM, vì đây là đối tượng dùng chung cho mọi module.
- Chị Huỳnh Anh đề nghị gộp Lead và Cơ hội cho đỡ nhiều trường. Dev giải thích hai thứ khác bản chất: Lead khóa sau khi chuyển đổi, còn Cơ hội mới tạo được báo giá và hợp đồng. Kết quả: **vẫn giữ riêng**.
- Khách cá nhân khi chuyển đổi không cần nhập mã số thuế.
- **Mã dự án** (ví dụ DB-2026-009) hiện tự sinh và không sửa được. NICON cần sửa được theo quy ước riêng. Dev nói chỉ là ID nên làm được, nhưng cần tránh trùng mã.

## 3. Thay đổi cốt lõi: dự án là bản ghi dùng chung

Đây là yêu cầu lớn nhất của buổi họp.

- Hiện mỗi phòng phải tạo lại "dự án" của mình nên bị trùng lặp. Anh Nguyên yêu cầu **bỏ cách này**.
- Kinh doanh tạo dự án một lần. Các phòng khác chọn dự án/hợp đồng có sẵn và **tạo nhóm công việc của phòng mình**; thông tin tự chuyển sang.
- Có hai loại công việc:
  - **Theo dự án/hợp đồng**: trước hợp đồng nằm ở dự án, có hợp đồng thì gom lại.
  - **Không theo hợp đồng** (ví dụ chương trình đào tạo toàn công ty): tab riêng, không gắn khách hàng/hợp đồng, có thể nối với module KPI. Ban đầu có ý kiến tạo "dự án nội bộ", cuối cùng thống nhất dùng tab công việc riêng.
- **Người chủ trì của phòng** mới được chia việc cho nhân viên phòng đó, kèm người phụ trách, thời gian, tiến độ. Người được giao nhận thông báo, bấm vào là mở thẳng đến việc.
- Chị Huỳnh Anh đề xuất giao việc ngay trong Dự án để tự chuyển sang phòng liên quan. Anh Nguyên thấy cách này và nút trên Cơ hội đều được. Dev ghi nhận làm ở đợt sau.

## 4. Thiết kế có thể bắt đầu trước hợp đồng

- Để báo giá được thì cần bản vẽ, nên Thiết kế phải chạy sớm, từ giai đoạn Cơ hội.
- Yêu cầu: **gỡ ràng buộc "phải có hợp đồng"** khỏi dự án thiết kế, và thêm **nút "Tạo dự án thiết kế" ngay trên Cơ hội** (cạnh nút tạo báo giá).
- Luồng vẫn giữ: **báo giá trước, rồi mới hợp đồng**.
- Chị Yến nhắc: phòng Thiết kế không được tạo dự án. Dự án luôn do Kinh doanh tạo; Thiết kế xuất hiện từ bước Concept.

## 5. Liên phòng ban ở giai đoạn trước hợp đồng

- **Cung ứng** cung cấp **giá đầu vào** (nhà cung cấp, thầu phụ) và phải chạy trước để Kinh doanh làm **báo giá đầu ra**. Làm qua "nhóm công việc dự án", không cần tab riêng. Trước hợp đồng, Cung ứng chỉ cần có thông tin dự án.
- Phần **kiểm soát BOQ đã ký** của Cung ứng nằm **sau** hợp đồng. Đây là hai thứ khác nhau.
- **Tài chính kế toán** tham gia trước hợp đồng ở hai chỗ:
  - rà soát điều kiện hợp đồng, bảo lãnh, điều kiện thanh toán sau báo giá;
  - rà soát hồ sơ tài chính trong bộ hồ sơ dự thầu trước khi nộp.
- Đổi nhãn "Mua sắm" thành **"Cung ứng"**.

## 6. Báo giá và BOQ

- Hai hình thức báo giá: theo suất đầu tư và theo BOQ. Hệ thống đã có file Excel mẫu để tải xuống, điền và import. Báo giá có version; mỗi báo giá chỉ một version được duyệt.
- BOQ của NICON là file Excel **nhiều sheet**. Mỗi sheet là một hạng mục (nhà xưởng, nhà kho, văn phòng, ký túc xá, hạ tầng); số lượng và loại hạng mục khác nhau theo công trình.
- Dev cần một **template chuẩn** để import. NICON gửi nhiều mẫu BOQ theo loại công trình (nhà xưởng quy mô khác nhau, nhà ở, căn hộ dịch vụ, văn phòng) để dev tìm cách xử lý chung.

## 7. Đấu thầu

- Thiếu bước **lập kế hoạch đấu thầu**: bảng tiến độ khoảng 1 tháng, chia việc cho từng phòng làm hồ sơ gì, khi nào.
- Quy trình đầy đủ: nhận hồ sơ mời thầu → lập kế hoạch → lập hồ sơ dự thầu → đánh giá → nộp thầu → kết quả. Hệ thống chưa đủ các bước này; dev sẽ bổ sung.
- Hồ sơ năng lực là phần cố định, dùng chung cho mọi dự án. Phần còn lại (BOQ, biện pháp thi công, bản vẽ dự thầu) khác nhau theo từng dự án.
- Trúng thầu thì chuyển sang thương thảo và tạo hợp đồng.

## 8. Thiết kế

- **Duyệt:** mỗi đầu việc chỉ có **một người duyệt**; người khác chỉ nhận CC. Anh Nguyên giải thích hai người cùng duyệt sẽ mâu thuẫn. Chị Yến muốn trưởng/phó phòng cùng nắm thông tin, được đáp ứng bằng CC.
- **Vai trò:**
  - Mỗi dự án có Design Lead (nhóm trưởng) và PM thiết kế.
  - Đổi "PM thiết kế" thành **Chủ nhiệm thiết kế / Design Manager** để tránh trùng với PM của cả dự án.
  - Chủ nhiệm thiết kế cao hơn Design Lead, có thể quản lý nhiều nhóm (kiến trúc, kết cấu, MEP).
  - Design Lead duyệt trực tiếp đầu việc và được chia đầu việc con.
- **Tự động chuyển duyệt:** khi nhân viên bấm hoàn thành thì hệ thống tự gửi người duyệt, không thao tác thủ công.
- **Trình nội bộ:** sau khi Thiết kế duyệt xong, trình cho Kinh doanh, CM và lãnh đạo.
  - Người được trình chỉ **xem và bình luận**, và chỉ xem được khi được chỉ định.
  - Chủ nhiệm thiết kế ghi nhận ý kiến, trình khách, rồi ghi nhận quyết định của khách trước khi chuyển bước.
- **Upload tài liệu** ở mọi giai đoạn, tự lưu vào cây thư mục Google Drive.
  - Giữ tab Tài liệu tổng hợp theo dự án.
  - Hồ sơ IFC chuyển thẳng sang Thi công.
- **Tiến độ và Đội ngũ:**
  - Hai tab đang thiếu thông tin so với bản tiến độ MS Project: mô tả, ngày bắt đầu/kết thúc, người phụ trách, công tác nối tiếp, nguồn lực, công tác găng.
  - Có thể gộp hoặc tách tab; dev quyết theo mẫu NICON gửi.
- **Thầu phụ thiết kế:** không tách module. Ghi thành đầu việc trong bảng tiến độ (người phụ trách, thời gian); kết quả upload vào hồ sơ giai đoạn.
  - Chủ nhiệm thiết kế được cấp thêm quyền truy cập phần nhà cung cấp để tìm, đàm phán, theo dõi hợp đồng thầu phụ.
  - Dev đã làm sớm phần thầu phụ ở Phase 3 nhưng chưa chắc đúng nghiệp vụ.
- **Quyền xóa hồ sơ** trình không đạt: chỉ cấp cho người quyền cao nhất. Xóa không khôi phục được.

## 9. Pháp lý

- Khi hợp đồng chuyển sang "đang thực hiện", hệ thống tự sinh danh sách giấy phép.
- Bổ sung: người chủ trì chia việc **theo từng giấy phép**, mỗi giấy phép có tiến độ riêng.
- Công việc pháp lý nằm trong module Pháp lý, kể cả luồng trình duyệt. Việc giao do PM toàn dự án phân từ đầu, dù người thực hiện có thể thuộc phòng khác.

## 10. Thi công

- Thi công gồm hai nhóm:
  - **Phòng CM** ở văn phòng: quản lý từ xa, nhiều dự án một lúc, chủ yếu kiểm tra và phê duyệt.
  - **Ban chỉ huy công trường (BCH):** tại công trường, mỗi BCH một dự án, thực hiện chi tiết.
- Tách menu Thi công thành hai nhóm này và thêm **ba nhóm công việc mới cho BCH**:
  - **Đề xuất nguồn lực** (vật tư, máy móc, nhân công): BCH tạo, tự chuyển về CM duyệt, rồi sang Cung ứng và Tài chính để làm hợp đồng và giao hàng.
  - **QA/QC:** biên bản nghiệm thu, lấy mẫu, kết quả thí nghiệm, chứng chỉ vật liệu, nhật ký công trường. Dữ liệu tự lưu về Drive công ty.
  - **QS:** nghiệm thu khối lượng và hồ sơ thanh toán, cả đầu ra (với khách) lẫn đầu vào (với thầu phụ, nhà cung cấp).
- Ba nhóm này là **form chuẩn cho mọi dự án**, nên làm thành template dựa trên bộ hồ sơ ISO NICON đã gửi.
- Anh Nguyên sẽ liệt kê thêm nhóm công việc của Phòng CM.

## 11. Tài chính kế toán

- Chạy **song song suốt vòng đời**, từ lúc ký hợp đồng, không phải chỉ sau bàn giao.
- Hai dòng:
  - **Đầu ra:** thanh toán của khách hàng.
  - **Đầu vào:** thanh toán cho thầu phụ, nhà cung cấp.
- Thanh toán theo tiến độ. Hồ sơ thanh toán gồm ba phần: công trường, CM, kế toán. Đủ ba phần mới gửi khách hoặc làm thủ tục chi cho thầu phụ.
- Chi tiết nằm trong bộ quy trình ISO đã gửi.

## 12. Ưu tiên, hạn chế và phối hợp

- Dev tập trung **CRM + Thiết kế trước**. Các module sau dùng cùng cấu trúc nên sẽ nhanh hơn.
- Dev chưa đẩy bản mới lên host. Khi đẩy xong sẽ báo NICON test và phản hồi trên Google Sheet.
- Nam sẽ gửi file quy trình có ảnh chụp màn hình (PDF).
- Chị Huỳnh Anh là đầu mối chính để dev trao đổi.
- Tính năng Google Drive đang test bằng tài khoản dev. Cần cấu hình cho tài khoản NICON sau này. Một số người NICON chưa đăng nhập Drive được do bước xác thực.

## 13. NICON cần gửi cho dev

- Phân nhóm quyền theo phòng ban.
- Nhiều mẫu BOQ Excel theo loại công trình.
- File tiến độ thiết kế mẫu từ MS Project (chị Yến gửi).
- Danh sách giấy phép của một dự án.
- Danh sách nhóm công việc Phòng CM.
- Quy ước mã dự án.

## 14. Chỗ chưa chắc chắn

- Đoạn nói về nút giao việc trong Dự án và trên Cơ hội hơi lẫn; kết luận là cả hai đều được.
- Chưa rõ giấy phép pháp lý dùng chung hay đổi theo loại công trình (Nam đã hỏi, chưa có trả lời).
- Chưa rõ hồ sơ đã trình khách có được xóa không.
- Ai được giao việc cho phòng ban khác chưa chốt: anh Nguyên nói phòng nào tự chia việc phòng đó; chị Yến muốn giới hạn ở cấp quản lý.
- Tên "Yến" và "Trinh" trong bản ghi có lúc lẫn nhau; có thể là cùng một người hoặc hai người.
