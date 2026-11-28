## PRD v2 – NICON Management System

Nov 19, 2026 · @Trí Nguyên Võ

PRD v2 gộp ba nguồn — bộ quy trình ISO 2018, PRD v1 (8 module, 11 vị trí KPI) và web đang chạy tại niconvn.com/admin — thành một quy trình duy nhất: 7 giai đoạn dự án, 4 loại đối tượng dùng chung, duyệt tối đa 3 bước, 35 biểu mẫu số thay cho khoảng 150 biểu mẫu giấy. Bản này thay thế PRD v1; quy trình ISO sẽ được viết lại theo bản này.

### 1. Mục tiêu và nguyên tắc tối giản

Mục tiêu: mọi việc của NICON — từ lúc có khách đến khi hết bảo hành — được giao, làm, duyệt, lưu hồ sơ và chấm điểm trên một hệ thống, không cần báo cáo tay hay file Excel riêng.

Bảy nguyên tắc:

Một mã dự án xuyên suốt (PJ-yyyy-nnnn). Mọi công việc, phiếu, biên bản, hợp đồng, khoản tiền, thư mục Drive đều gắn mã này. Việc nội bộ dùng mã phòng ban.

Nhập một lần, tại nơi phát sinh. Các module khác chỉ đọc lại. Ví dụ: ngày và giá trị lấy từ hợp đồng; tiến độ, BOQ, mua hàng đều kế thừa.

Bốn loại đối tượng thay cho hàng trăm biểu mẫu: Công việc · Phiếu đề xuất · Biên bản/Hồ sơ · Hợp đồng & mốc tiền (mục 3).

Duyệt tối đa 3 bước: Lập → Kiểm tra → Phê duyệt theo hạn mức, có ủy quyền khi vắng mặt (mục 4).

Cổng giai đoạn: dự án chỉ sang giai đoạn sau khi đủ điều kiện trong checklist; hệ thống chặn thay vì chỉ cảnh báo.

Biểu mẫu ISO là đầu ra, không phải đầu vào: người dùng nhập trên màn hình; hệ thống xuất file đúng mẫu, đúng mã ISO để ký và tự lưu Drive.

KPI chỉ tính từ dữ liệu vận hành cộng một phần đánh giá của quản lý trực tiếp.

Phạm vi: áp dụng cho cả 4 loại hợp đồng trong quy trình chung của công ty — chỉ thiết kế; thiết kế và thi công tách hợp đồng; D&B chào giá; D&B đấu thầu. Cùng một quy trình, loại hợp đồng quyết định giai đoạn nào được bỏ qua.

### 2. Quy trình thống nhất: 7 giai đoạn, 7 cổng duyệt

Thay cho 7 quy trình ISO rời (D.1–D.7), 16 quy trình con thi công và luồng liên vết 13 bước của PRD v1, mọi dự án đi theo một chuỗi 7 giai đoạn. Mỗi giai đoạn kết thúc bằng một cổng duyệt có checklist; chưa qua cổng thì hệ thống không cho mở chức năng của giai đoạn sau.

quy trình thống nhất · 7 giai đoạn, 7 cổng duyệt

| Giai đoạn | Việc chính | Cổng duyệt (điều kiện qua cổng) | Người duyệt | Gộp từ ISO |
| --- | --- | --- | --- | --- |
| 1. Cơ hội | Ghi nhận lead, phiếu thông tin KH, khảo sát hiện trạng (app), phân loại loại hợp đồng | Đủ thông tin KH + kết quả khảo sát + đánh giá rủi ro sơ bộ → quyết định đi tiếp / dừng | BGĐ | D.1 bước 1–2; D.2 bước 1–2 |
| 2. Báo giá – Đấu thầu | Concept/TKSB, khái toán hoặc BOQ dự thầu, hỏi giá NCC/thầu phụ, hồ sơ năng lực, nộp thầu | Báo giá / hồ sơ thầu đủ checklist, biên lợi nhuận dự kiến ≥ ngưỡng | TP Kinh doanh → BGĐ | D.2; D.3 bước 1–3 |
| 3. Hợp đồng & mở dự án | Thương thảo, xem xét HĐ, ký. Khi ký: tạo mã PJ, cây thư mục Drive, đội dự án (RACI), tiến độ tổng thể, BOQ hợp đồng = ngân sách gốc, lịch mốc thu tiền | HĐ đã ký đính kèm; báo giá trúng bị khóa; cơ hội = Thắng; KH = Đang hợp tác | Pháp chế → TP KD → Giám đốc | D.1 bước 4; D.2 (DT-M07) |
| 4. Thiết kế & Pháp lý | TKCS → thẩm tra → xin phép (GPXD, PCCC, môi trường, đấu nối); TKKTTC; nghiệm thu hồ sơ thiết kế với CĐT từng giai đoạn; phát hành IFC | Có GPXD (nếu cần) + bộ bản vẽ IFC + hồ sơ điều kiện khởi công | Chủ nhiệm TK → PM | D.3; phần xin phép của D.1 |
| 5. Thi công | Quyết định lập BCH, kế hoạch chất lượng, giao nhận mặt bằng; shop drawing, duyệt mẫu vật tư, MR – mua hàng – kho; nhật ký, tiến độ cuốn chiếu 2 tuần; QA/QC, nghiệm thu công việc/giai đoạn, HSE; phát sinh (VO); đợt QS với CĐT và thầu phụ | Mỗi đợt: biên bản nghiệm thu QS được CĐT ký → mở mốc thu tiền tương ứng | CHT → Phòng CM → PM | D.4 (16 quy trình con), D.6 |
| 6. Bàn giao & quyết toán | Nghiệm thu nội bộ → CĐT, chạy thử, danh sách khiếm khuyết, hồ sơ hoàn công, sổ tay vận hành, bàn giao; quyết toán CĐT và thầu phụ; thanh lý HĐ | Biên bản bàn giao + quyết toán hai chiều được duyệt | PM → BGĐ | D.4 (NTBG), D.5 |
| 7. Bảo hành & chăm sóc KH | Tiếp nhận khiếu nại / yêu cầu bảo hành, xử lý, giải toả tiền bảo lưu, đánh giá NCC/thầu phụ sau thi công, bài học kinh nghiệm | Hết hạn bảo hành, không còn khiếu nại mở, công nợ = 0 → đóng dự án | PM → BGĐ | D.1 (5.2 chăm sóc KH), D.4 (TP-M10) |

Theo loại hợp đồng: HĐ chỉ thiết kế đi 1 → 2 → 3 → 4 → 7 (phần bàn giao hồ sơ nằm ở cổng 4). Thiết kế và thi công tách HĐ lặp lại giai đoạn 2–3 cho HĐ thi công trước khi vào giai đoạn 5, cùng một mã PJ. D&B chào giá và D&B đấu thầu đi đủ 7 giai đoạn, khác nhau ở giai đoạn 2.

### 3. Bốn đối tượng dùng chung và nhịp vận hành

Mọi thao tác trên hệ thống thuộc một trong bốn loại đối tượng dưới đây. Lập trình viên chỉ cần làm bốn "động cơ" dùng chung (giao việc, phiếu + luồng duyệt, biên bản theo mẫu, hợp đồng + mốc tiền), rồi cấu hình thêm loại mới mà không phải viết màn hình mới.

| Đối tượng | Dùng cho | Trạng thái chuẩn | Thay cho biểu mẫu ISO |
| --- | --- | --- | --- |
| Công việc | Giao việc trong dự án và việc nội bộ: người giao, người làm, hạn, việc con, bình luận, file, gắn WBS | Mới → Đang làm → Chờ xác nhận → Xong / Hủy; quá hạn tự cảnh báo | DT-M04, DT-M06 Giấy giao việc, TC-TCNTBG-M13 Phiếu giao việc, TC-TD-M05 Phiếu nhắc nhở |
| Phiếu đề xuất (một động cơ, nhiều loại) | Vật tư (MR), thiết bị, nhân công, phát sinh (VO), tạm ứng, đề nghị thanh toán, RFI, trình duyệt mẫu vật tư, trình duyệt bản vẽ, gia hạn tiến độ, khiếu nại / bảo hành, nghỉ phép, tuyển dụng, sửa chữa thiết bị | Nháp → Chờ kiểm tra → Chờ duyệt → Đã duyệt / Trả lại (kèm lý do) / Từ chối → Đã thực hiện | MH-M01, TC-DM-M02/M05, TC-TB-M01, TC-PS-M01/M02, TQT-M07, MH-M04, TC-CB-M16, TC-SD-M04, TC-TD-M04, KH-M04, QLNS-M01/M11, TC-BTSC-M03 |
| Biên bản / Hồ sơ (theo mẫu có phiên bản) | Nhật ký công trường, báo cáo ngày, QA/QC (lấy mẫu, thí nghiệm), nghiệm thu (nội bộ / CĐT; công việc, giai đoạn, chạy thử, hoàn thành), HSE, giao nhận mặt bằng, bàn giao hồ sơ thiết kế, bàn giao công trình, đánh giá NCC/thầu phụ, phiếu nhập/xuất kho | Nháp → Ký nội bộ → Ký CĐT/TVGS (nếu cần) → Khóa, xuất PDF, lưu Drive | TC-NTCV/NTGD/NTBG, TC-TCNTBG-M01–M15, TC-ATVSLD, TC-CB-M14/M15, TK-M02/M03, TC-TD-M03, QLNS-M05, MH-M07, TC-TP-M08, TC-KHO-M18/M19 |
| Hợp đồng & mốc tiền | HĐ bán ra (CĐT) và mua vào (NCC, thầu phụ, giao khoán tổ đội), phụ lục VO, mốc thu/chi, tạm ứng và thu hồi, tiền bảo lưu, quyết toán, thanh lý | Nháp → Đã duyệt → Đã ký → Đang thực hiện → Quyết toán → Thanh lý | DT-M07/M08, TQT-M01–M09, TC-TP-M04a/M04b/M05/M06/M10/M13, MH-M03 |

Danh mục dùng chung (nhập một lần, mọi module cùng dùng): khách hàng; NCC/thầu phụ kèm danh sách được duyệt; vật tư và đơn giá theo phiên bản; máy móc thiết bị; cấu trúc WBS mẫu; mẫu biên bản; nhân sự và vai trò.

Nhịp vận hành (lịch cố định để hệ thống tự nhắc và tự tổng hợp; mốc có dấu * là đề xuất, BGĐ chốt):

| Chu kỳ | Việc | Ai làm | Hệ thống làm gì |
| --- | --- | --- | --- |
| Hằng ngày | Báo cáo ngày (việc đã làm, kế hoạch mai, đề xuất); nhật ký công trường | Mọi nhân viên; BCH | Tự điền sẵn từ công việc được giao; nhắc lúc 17:00* |
| Hằng tuần | Họp tuần theo tiến độ cuốn chiếu 2 tuần | PM, BCH, các phòng | Gom việc quá hạn, phiếu chờ duyệt, lệch tiến độ |
| Hằng tuần | Đợt chi: nộp đề nghị trước chiều thứ 6 → BGĐ duyệt sáng thứ 7 → chi sáng thứ 3 | Các bộ phận, Kế toán, BGĐ | Tự đóng đợt, lập bảng tổng hợp (TQT-M01), chỉ nhận đề nghị đã đối soát 3 bên |
| Hằng tháng | Đợt QS với CĐT và thầu phụ (theo kỳ của hợp đồng); báo cáo dòng tiền, P&L dự án | QS, Kế toán | Mở mốc thu/chi khi biên bản QS được ký |
| Hằng tháng | Chốt KPI tháng trước (ngày 5*) | Quản lý trực tiếp, HCNS | Tính điểm định lượng; mở phiếu đánh giá của quản lý; khóa kỳ |
| Hằng quý | Đánh giá NCC/thầu phụ; rà soát rủi ro, đánh giá nội bộ ISO | Cung ứng + hội đồng; thư ký ISO | Mở phiếu đánh giá, cập nhật danh sách NCC được duyệt |

### 4. Vai trò và ma trận duyệt

Web hiện có 25 vai trò, trong đó có vai trò trùng, vai trò 0 quyền và thiếu vai trò cho 2/11 vị trí KPI. PRD v2 rút còn 16 vai trò, khớp 1–1 với 11 vị trí KPI cộng 5 vai trò quản lý/hỗ trợ. "Trưởng bộ phận" là một vai trò chung gắn với phòng ban, không tách theo từng phòng.

| # | Vai trò | Mã trên web | Thay đổi so với web hiện tại |
| --- | --- | --- | --- |
| 1 | Ban Giám đốc | BGD | Giữ |
| 2 | Trưởng bộ phận (KD, TK, CM, Cung ứng, Kế toán, HCNS) | DEPT_HEAD + phòng ban | Gộp SALES_MANAGER, DESIGN_LEAD, PROCUREMENT_MANAGER, CM_MANAGER (đang 0 quyền), V_DESIGN_MANAGER (phó phòng = ủy quyền) |
| 3 | Chủ nhiệm thiết kế | DESIGN_MANAGER | Giữ |
| 4 | Kỹ sư QLDA (PM) | PM | Giữ; gán cho đúng người (PM dự án C-Hawk đang là Super Admin) |
| 5 | Chỉ huy trưởng | SITE_COMMANDER | Giữ |
| 6 | KS hiện trường / QA-QC | SITE_QAQC | Gộp CIVIL_ENGINEER (đang 0 quyền) |
| 7 | QS công trường | SITE_QS | Mới (tách khỏi QS) |
| 8 | Kỹ sư đấu thầu / QS văn phòng | TENDER_QS | Đổi tên từ QS "Bóc tách khối lượng" |
| 9 | KTS / KS thiết kế | ARCHITECT, STRUCT_ENGINEER, MEP_ENGINEER | Gộp DESIGN vào ARCHITECT; ba mã này cùng bộ KPI |
| 10 | Kinh doanh (Sales) | SALE | Giữ |
| 11 | Mua hàng / Cung ứng | PROCUREMENT | Giữ |
| 12 | Pháp lý (xin phép + pháp chế hợp đồng) | LEGAL_OFFICER | Mở rộng sang bước "Pháp chế" của luồng ký HĐ |
| 13 | Kế toán dự án | ACCOUNTANT | Giữ |
| 14 | Hành chính nhân sự | HR | Giữ |
| 15 | Thủ kho | WAREHOUSE | Giữ (không tính KPI riêng) |
| 16 | Quản trị hệ thống | ADMIN | Chỉ 1–2 người; bỏ vai trò USER; Super Admin chỉ dùng khi cài đặt |

Quyền theo dự án vẫn theo RACI của đội dự án (đã có trên web): vai trò quyết định được làm gì, RACI quyết định ở dự án nào. Người ngoài đội dự án không xem được dự án, trừ BGĐ và trưởng bộ phận liên quan.

Ma trận duyệt — mỗi loại phiếu tối đa 3 bước. A, B, C là hạn mức giá trị do BGĐ chốt và cấu hình được trên web. Người duyệt vắng mặt thì ủy quyền có thời hạn (theo mục "Thay thế khi vắng mặt" trong Mô tả công việc).

| Loại | Lập | Kiểm tra | Phê duyệt |
| --- | --- | --- | --- |
| Báo giá / nộp thầu | Sales / KS đấu thầu | TP Kinh doanh | BGĐ |
| Ký hợp đồng bán ra | Sales | Pháp lý + Kế toán | TP KD → Giám đốc |
| Phát hành bản vẽ IFC | KTS / KS thiết kế | Chủ nhiệm TK | PM |
| MR vật tư / thiết bị / nhân công (trong BOQ) | KS hiện trường | CHT | PM nếu ≤ A; BGĐ nếu > A. Vượt BOQ → chặn, phải có VO |
| Phát sinh (VO) | CHT / QS công trường | Phòng CM | PM nếu ≤ B; BGĐ nếu > B |
| Chọn NCC (≥ 3 báo giá) và đơn hàng / HĐ mua vào | Mua hàng | TP Cung ứng | PM nếu ≤ A; BGĐ nếu > A |
| Đợt QS với CĐT | QS công trường | Phòng CM | PM → gửi CĐT |
| Nghiệm thu nội bộ trước khi mời CĐT | KS QA/QC | CHT | Phòng CM |
| Đề nghị thanh toán NCC / thầu phụ | Mua hàng / QS | Kế toán (đối soát 3 bên) | BGĐ trong đợt chi tuần |
| Tạm ứng | Người đề nghị | Trưởng bộ phận | Kế toán trưởng nếu ≤ C; BGĐ nếu > C |
| Quyết toán CĐT / thầu phụ | QS công trường | Kế toán | BGĐ |
| Khiếu nại / bảo hành | Người tiếp nhận | — | BGĐ phân công, PM đóng phiếu |
| Nghỉ phép / đề nghị tuyển dụng | Nhân viên / TBP | HCNS | TBP / BGĐ |

### 5. Yêu cầu chức năng theo module

Giữ 8 module của PRD v1 và thêm 2 module nền: M0 Công việc & Báo cáo, M9 Hệ thống & ISO. Mục đánh dấu [Mới] là yêu cầu chưa có trong PRD v1, lấy từ quy trình ISO hoặc từ thực tế web.

M0 – Công việc & Báo cáo [Mới] (lõi của "quản lý công việc", dùng cho mọi phòng)

Màn hình "Việc của tôi": hôm nay, sắp đến hạn, quá hạn, phiếu chờ tôi duyệt. Dạng danh sách và Kanban.

Giao việc cá nhân hoặc phòng ban, việc con, bình luận, file, gắn dự án và WBS. Mã việc ngắn theo dự án (PJ-2026-0002-WP01).

Báo cáo ngày theo mẫu QLNS-M05: tự điền việc đang làm, nhân viên ghi % hoàn thành, đề xuất, kế hoạch mai, ảnh. Tự tổng hợp tuần/tháng theo phòng ban.

Thông báo qua app + email (cân nhắc Zalo OA): được giao việc, sắp/quá hạn, chờ duyệt, bị trả lại, cổng giai đoạn mở.

Ủy quyền có thời hạn khi vắng mặt.

M1 – Khách hàng, Báo giá & Đấu thầu (giai đoạn 1–3, 7)

Lead → cơ hội theo pipeline; [Mới] cổng BGĐ "đi tiếp / dừng" có đánh giá rủi ro.

Khảo sát hiện trạng trên app (ảnh, video, tọa độ) → tự lưu Drive.

Báo giá 3 loại (thiết kế; thi công; thiết kế – thi công), theo suất đầu tư hoặc BOQ, có phiên bản và biên lợi nhuận dự kiến.

Gói thầu: checklist hồ sơ, hạn nộp, kết quả; [Mới] bảng phân chia công việc thầu tự sinh task; hỏi giá NCC ngay ở giai đoạn thầu.

[Mới] Khi HĐ được ký: khóa báo giá trúng, cơ hội = Thắng, KH = Đang hợp tác, mở dự án (cổng 3).

[Mới] Khiếu nại / bảo hành và chăm sóc KH sau bàn giao (giai đoạn 7).

M2 – Thiết kế (giai đoạn 2, 4)

3 giai đoạn Concept → Cơ sở → Chi tiết, mỗi giai đoạn có mốc kế hoạch (để tính KPI đúng hạn); tách bộ môn KT / KC / MEP / NT.

Rev tự đánh mã; [Mới] bắt buộc chọn lý do Rev: CĐT yêu cầu / cơ quan nhà nước / lỗi nội bộ.

Duyệt KTS → Chủ nhiệm TK → PM; chỉ bản IFC hiển thị ở công trường; Rev mới tự thu hồi bản cũ.

[Mới] Biên bản bàn giao và nghiệm thu hồ sơ thiết kế với CĐT (TK-M02/M03) → mở mốc thu tiền thiết kế.

[Mới] Sổ shop drawing: kế hoạch trình, trình duyệt, phân phối, thu hồi; cả bản vẽ từ CĐT/TVGS.

M3 – Pháp lý (giai đoạn 4, 6)

Checklist theo quy mô công trình; trạng thái Đã nộp → Đang thụ lý → Cần bổ sung → Có kết quả; đếm số lần bị trả; cảnh báo hết hạn.

Gắn theo mã PJ (không theo hồ sơ thiết kế); yêu cầu sửa bản vẽ tự tạo việc cho Thiết kế.

M4 – Thi công (giai đoạn 5–6; BCH lập, Phòng CM kiểm tra — giữ cách web đang làm)

[Mới] Chuẩn bị: quyết định lập BCH, kế hoạch chất lượng, giao nhận và định vị mặt bằng, danh mục kiểm tra → gộp vào "Điều kiện khởi công".

Tiến độ: tổng thể lấy khung ngày từ HĐ, WBS, Gantt, S-curve; [Mới] tiến độ cuốn chiếu 2 tuần, phiếu gia hạn tiến độ.

Nhật ký công trường mobile (nhân công, máy, thời tiết, ảnh), offline.

Chất lượng: mẫu QA/QC nạp sẵn từ biểu mẫu ISO; [Mới] nghiệm thu 2 bước nội bộ → CĐT cho công việc, giai đoạn, chạy thử thiết bị, hoàn thành; punchlist / danh sách khiếm khuyết.

[Mới] Phiếu trình duyệt mẫu vật tư, RFI.

HSE: biên bản vi phạm, kiểm tra định kỳ; [Mới] báo cáo nhanh sự cố, kế hoạch PCCC / đội sơ cứu.

QS: đợt nghiệm thu khối lượng với CĐT (mở mốc thu tiền) và với thầu phụ (khối lượng lũy kế).

[Mới] Phát sinh (VO) theo quy trình TC-PS: yêu cầu → xem xét → kê khai thay đổi so với thiết kế → duyệt → cập nhật BOQ và phụ lục HĐ.

Hoàn công & bàn giao: tự gom biên bản, QA/QC, bản vẽ thay đổi; sổ tay vận hành bảo trì.

M5 – Cung ứng, Kho & Thiết bị (giai đoạn 5)

MR đối chiếu BOQ: vàng 85%, cam 95%, đỏ 100% = chặn, cần VO. [Mới] Nộp trước ít nhất 1 tuần (theo ISO).

RFQ bắt buộc ≥ 3 báo giá, chỉ từ danh sách NCC được duyệt; bảng so sánh; đơn hàng.

Đánh giá NCC/thầu phụ một bộ tiêu chí thống nhất (thang 1–10, hàng quý và sau mỗi dự án): Chất lượng · Giá & thanh toán · Giao hàng (đúng hạn, đủ số lượng) · Phục vụ & xử lý sự cố · Năng lực, uy tín & an toàn. Gộp 9 tiêu chí ISO và 4 tiêu chí PRD v1.

Kho: nhập – xuất – tồn theo công trường; [Mới] cả máy móc thiết bị và công cụ dụng cụ, điều chuyển giữa công trường, xuất cho từng tổ đội.

[Mới] Thiết bị thi công: yêu cầu, điều động, bàn giao, lịch bảo trì, lý lịch máy, lịch hiệu chuẩn thiết bị trắc đạc.

M6 – Tài chính & Hợp đồng (giai đoạn 3, 5, 6)

Một sổ hợp đồng hai chiều bán ra / mua vào (giữ cách web đang làm); phụ lục VO; lịch mốc thu / chi.

Đối soát 3 bên (HĐ + phiếu nhập kho + nghiệm thu QS) trước khi được vào đợt chi.

[Mới] Đợt chi tuần (mục 3); tạm ứng và thu hồi tạm ứng; tiền bảo lưu và giải toả; phạt thầu phụ; quyết toán và thanh lý HĐ.

[Mới] Nhắc nợ tự động: đợt thu quá hạn → sinh thông báo / công văn thanh toán (TQT-M08/M09).

Dòng tiền và P&L theo dự án; [Mới] xuất dữ liệu cho phần mềm kế toán đang dùng.

M7 – Lưu trữ Google Drive — xem mục 7.

M8 – KPI & Dashboard — KPI xem mục 6. Dashboard theo vai trò: BGĐ (dòng tiền, phải thu quá hạn, tiến độ các dự án, cảnh báo đỏ), PM, BCH, cá nhân. Không dùng số liệu website trên dashboard điều hành.

M9 – Hệ thống & ISO [Mới]

Danh mục biểu mẫu có mã, phiên bản, ngày hiệu lực (thay quy trình C.1 Kiểm soát tài liệu); 8 trang quy trình dẫn thẳng tới chức năng.

Vai trò, quyền, hạn mức duyệt, ủy quyền cấu hình được.

Nhật ký hoạt động chỉ đọc, không xóa; không xóa cứng dữ liệu nghiệp vụ (chỉ Hủy / Lưu trữ).

Tách khu quản trị nội dung website khỏi hệ thống điều hành.

### 6. KPI 11 vị trí

Tổng điểm tháng = 70% định lượng (3 chỉ số tự tính từ dữ liệu) + 30% đánh giá của quản lý trực tiếp. Tỷ lệ 70/30 là đề xuất, BGĐ chốt. Phần 30% gộp 11 tiêu chí A–E của mẫu QLNS-M05 còn 5: mức độ hoàn thành, tuân thủ quy trình, độ chính xác, phối hợp, thái độ.

Quy tắc tính điểm (áp cho mọi chỉ số):

Mỗi chỉ số có mục tiêu, chiều tính (cao hơn / thấp hơn là tốt) và cách quy đổi: đạt mục tiêu = 100 điểm, nội suy tuyến tính, sàn 0, trần 120. Trang Cấu hình KPI trên web đã có khung này.

Kỳ tính theo tháng, chốt và khóa theo nhịp ở mục 3. Chỉ số thiếu dữ liệu trong kỳ thì không tính, trọng số chia lại cho các chỉ số còn lại.

Loại trừ có duyệt: trễ do CĐT, do cơ quan nhà nước, hoặc bất khả kháng (PM hoặc BGĐ duyệt, lưu vết).

Nhân viên xem được bằng chứng nguồn của từng điểm (web đã có "Xem bằng chứng nguồn").

| # | Vị trí | Chỉ số 1 (40%) | Chỉ số 2 (30%) | Chỉ số 3 (30%) | Thay đổi so với v1 |
| --- | --- | --- | --- | --- | --- |
| 1 | PM | Chi phí thực tế / ngân sách gốc | Độ lệch S-curve so với kế hoạch | Thời gian đóng punchlist trung bình | Ngân sách gốc = BOQ hợp đồng + VO đã duyệt |
| 2 | Chỉ huy trưởng | % mốc WBS hoàn thành đúng hạn | Hao hụt vật tư (xuất kho / định mức BOQ) | HSE: % đạt kiểm tra ATLĐ định kỳ + % vi phạm đóng đúng hạn | Không chấm theo số vi phạm bị lập, tránh khuyến khích giấu lỗi |
| 3 | KS hiện trường / QA-QC | % nghiệm thu đạt lần đầu | Thời gian xử lý punchlist | Hồ sơ chất lượng đủ và đúng hạn | Giữ |
| 4 | QS công trường | Đợt QS với CĐT nộp đúng kỳ HĐ | Thời gian duyệt khối lượng thầu phụ + tỷ lệ khiếu nại | Giá trị VO được CĐT duyệt / giá trị VO trình | Giữ |
| 5 | Mua hàng | Tiết kiệm so với đơn giá BOQ | Thời gian từ MR được duyệt đến khi nhập kho | Điểm đánh giá NCC trung bình | Dùng bộ tiêu chí NCC thống nhất (mục 5, M5) |
| 6 | Kinh doanh | Tỷ lệ cơ hội → hợp đồng (40%) | Giá trị HĐ ký mới (40%) | Thời gian phản hồi lead (20%) | Doanh số ghi cho "Sales phụ trách" trên cơ hội; HĐ lấy từ cơ hội, không nhập lại |
| 7 | KS đấu thầu / QS VP | Tỷ lệ trúng thầu / chốt báo giá | % hồ sơ thầu, BOQ xong đúng hạn | Độ lệch BOQ dự thầu so với quyết toán | So với quyết toán thay vì "bóc tách thi công" |
| 8 | KTS / KS thiết kế | Phát hành đúng mốc từng giai đoạn | % bản vẽ đạt lần duyệt đầu | Số lỗi thiết kế bị gắn từ công trường | Chỉ đếm Rev do lỗi nội bộ, không đếm Rev do CĐT yêu cầu |
| 9 | Pháp lý | Cấp phép đúng hạn | % hồ sơ đạt lần nộp đầu | % hồ sơ gốc đã scan lưu Drive | Giữ |
| 10 | Kế toán dự án | % công nợ CĐT thu đúng hạn | % đề nghị thanh toán xử lý trong đợt chi | Số lần điều chỉnh / sai lệch số liệu | Gắn với đợt chi tuần |
| 11 | Hành chính nhân sự | Tuyển đúng hạn + tỷ lệ đạt thử việc | Chốt KPI toàn công ty đúng hạn | Thời gian xử lý phiếu hành chính | Cần phiếu tuyển dụng và phiếu hành chính trong M0 mới có dữ liệu |

Trọng số 40/30/30 giữ như v1 (Kinh doanh 40/40/20). Trên web hiện mới có 6 nhóm KPI; cần tách đủ 11 vị trí và gắn theo vai trò ở mục 4.

### 7. Lưu trữ Google Drive

Mỗi dự án có đúng 8 thư mục cấp 1, đánh số theo giai đoạn, tạo tự động khi mở mã PJ (cổng 3). Cây này thay cho cây 6 thư mục trong PRD v1 và các đường dẫn rời trong Cài đặt Drive của web. Cần đối chiếu lần cuối với biểu mẫu TC-CB-M17 "Hệ thống lưu trữ hồ sơ công trình".

[PJ-yyyy-nnnn]_[Tên dự án]
├── 01_CoHoi_BaoGia khảo sát, hồ sơ thầu, báo giá (GĐ 1–2)
├── 02_HopDong HĐ bán ra, phụ lục VO, xem xét HĐ (GĐ 3)
├── 03_ThietKe
│ ├── 01_Concept
│ ├── 02_CoSo
│ ├── 03_ChiTiet_IFC
│ └── 04_ShopDrawing
├── 04_PhapLy GPXD, PCCC, môi trường, hoàn công (GĐ 4, 6)
├── 05_ThiCong
│ ├── 01_ChuanBi QĐ BCH, kế hoạch chất lượng, mặt bằng
│ ├── 02_NhatKy_BaoCao
│ ├── 03_ChatLuong_NghiemThu
│ ├── 04_ATLD_HSE
│ └── 05_QS_KhoiLuong
├── 06_CungUng RFQ, đơn hàng, HĐ mua vào, kho, đánh giá NCC (GĐ 5)
├── 07_TaiChinh đề nghị thanh toán, hóa đơn, quyết toán (GĐ 3–6)
└── 08_BanGiao_BaoHanh hồ sơ hoàn công, sổ tay vận hành, khiếu nại (GĐ 6–7)

Dùng Shared Drive của công ty để file không phụ thuộc tài khoản cá nhân; mọi thành viên dùng email công ty.

Quyền theo RACI đội dự án và vai trò: ví dụ 07_TaiChinh chỉ Kế toán, PM, BGĐ xem; thành viên rời dự án tự mất quyền.

Biên bản, phiếu đã khóa tự xuất PDF (đúng mẫu, mã ISO) vào đúng thư mục. Người dùng không cần tự tải lên.

Xem trực tiếp trong app: PDF, ảnh, Office; .dwg cần trình xem riêng.

Có trang kiểm tra kết nối và cảnh báo Admin khi đồng bộ lỗi (hiện trên web đang lỗi giải mã cấu hình).

Thư mục dùng chung ngoài dự án: Quy trình & Biểu mẫu (chỉ bản hiệu lực), Hồ sơ năng lực, Nhân sự (chỉ HCNS + BGĐ), Truyền thông.

### 8. Danh mục biểu mẫu số (gộp từ bộ ISO)

Khoảng 150 biểu mẫu ISO được gộp thành 35 mẫu số, chia theo bốn loại đối tượng ở mục 3. Mỗi mẫu số xuất được file đúng mẫu ISO gốc (giữ mã cũ trong chân trang để tra cứu). Các tài liệu chỉ để đọc (nội quy PCCC, nội quy y tế, biển cảnh báo, sơ đồ tổ chức, quy định bốc dỡ…) không số hóa, chỉ đăng ở thư viện quy trình.

| Mã mới | Mẫu số | GĐ | Gộp từ biểu mẫu ISO |
| --- | --- | --- | --- |
| CV-01 | Giao việc | 1–7 | DT-M04, DT-M06, TC-TCNTBG-M13, TC-TD-M05 |
| CV-02 | Báo cáo ngày / tuần | 1–7 | QLNS-M05, TC-TD-M02, TC-TCNTBG-M14 |
| PH-01 | Đánh giá cơ hội – đi tiếp / dừng | 1 | KH-M02, QT6.1-BM01 (rủi ro) |
| PH-02 | Yêu cầu vật tư / thiết bị / nhân công | 5 | MH-M01, TC-DM-M05, TC-TB-M01 |
| PH-03 | Yêu cầu báo giá NCC / thầu phụ + so sánh | 2, 5 | DT-M05, MH-M02, TC-TP-M14 |
| PH-04 | Đơn đặt hàng | 5 | MH-M03 |
| PH-05 | Phát sinh (VO) | 5 | TC-PS-M01, M02, M03 |
| PH-06 | Trình duyệt mẫu vật tư | 5 | TC-DM-M01–M04 |
| PH-07 | Trình duyệt / phân phối / thu hồi bản vẽ | 4–5 | TC-SD-M01–M06 |
| PH-08 | Yêu cầu cung cấp thông tin (RFI) | 4–5 | TC-CB-M16 |
| PH-09 | Gia hạn tiến độ | 5 | TC-TD-M04 |
| PH-10 | Đề nghị tạm ứng | 3–6 | TQT-M07, TC-TP-M05 |
| PH-11 | Đề nghị thanh toán (NCC, thầu phụ, tổ đội) | 5–6 | MH-M04, TC-TP-M12, TC-TP-M13 |
| PH-12 | Khiếu nại / bảo hành | 7 | KH-M04, KH-M06 |
| PH-13 | Sửa chữa thiết bị, yêu cầu trắc đạc | 5 | TC-BTSC-M03, TC-TrD-M02 |
| PH-14 | Nhân sự: tuyển dụng, nghỉ phép, nghỉ việc | — | QLNS-M01, M06, M11 |
| BB-01 | Khảo sát hiện trạng | 1 | TK-M01, TC-CB phiếu khảo sát |
| BB-02 | Bàn giao & nghiệm thu hồ sơ thiết kế | 4 | TK-M02, TK-M03 |
| BB-03 | Kế hoạch chất lượng & điều kiện khởi công | 5 | TC-CB-M01–M13 |
| BB-04 | Giao nhận & định vị mặt bằng | 5 | TC-CB-M14, M15 |
| BB-05 | Nhật ký công trường | 5 | TC-TD-M03, M06 |
| BB-06 | Lấy mẫu, thí nghiệm, nghiệm thu vật liệu | 5 | TC-TCNTBG-M01–M07 |
| BB-07 | Nghiệm thu (nội bộ / CĐT; công việc, giai đoạn, chạy thử, hoàn thành) | 5–6 | TC-NTCV-M01–M05, TC-NTGD-M01–M07, TC-NTBG-M01–M07, M11 |
| BB-08 | Kiểm tra ATLĐ, vi phạm, sự cố | 5 | TC-ATVSLD-M03–M06, M09, TC-TCNTBG-M10, TC-THKC-M01–M03 |
| BB-09 | Nhập / xuất / điều chuyển / kiểm kê kho | 5 | TC-KHO-M01–M19, TC-TB-M02, M03, M05, M06 |
| BB-10 | Đánh giá NCC / thầu phụ | 5, 7 | MH-M05–M08, TC-TP-M01–M03, M08 |
| BB-11 | Khối lượng QS (CĐT, thầu phụ, lũy kế) | 5–6 | TC-M14, TC-TP-M11, TQT-M05 |
| BB-12 | Bàn giao công trình, danh sách khiếm khuyết, sổ tay vận hành | 6 | TC-NTBG-M08, M09, M10 |
| BB-13 | Biên bản họp | 1–7 | TC-TCNTBG-M12 |
| HD-01 | Xem xét & chấp thuận hợp đồng | 3 | DT-M07 |
| HD-02 | Hợp đồng / phụ lục / giao khoán | 3, 5 | DT-M09, TC-TP-M04a, M04b |
| HD-03 | Bảng tổng hợp đợt chi, phiếu chi | 5–6 | TQT-M01, TQT-M03 |
| HD-04 | Quyết toán CĐT / thầu phụ | 6 | TQT-M04, TC-TP-M06 |
| HD-05 | Thanh lý HĐ, giải toả bảo lưu | 6–7 | DT-M08, TQT-M06, TC-TP-M10 |
| HD-06 | Đề nghị thanh toán CĐT, thông báo / công văn thanh toán | 5–6 | TQT-M02, M08, M09 |

### 9. Yêu cầu giao diện và kỹ thuật

Giao diện

Mobile-first cho hiện trường (khảo sát, nhật ký, QA/QC, nghiệm thu, MR, kho, báo cáo ngày): nút lớn, ≤ 3 lần chạm, nén ảnh khi sóng yếu, lưu tạm offline và tự đồng bộ khi có mạng. Chốt hình thức app (cài đặt hay web dạng app) trước khi làm giai đoạn 3.

Web cho văn phòng: dashboard theo vai trò, Gantt / S-curve, bảng so sánh giá, đợt chi, dòng tiền.

Mỗi dự án có một trang tổng: giai đoạn hiện tại, checklist cổng tiếp theo, việc quá hạn, phiếu chờ duyệt, mốc tiền.

Thống nhất định dạng: ngày dd/mm/yyyy; tiền VND số nguyên (chia đợt thì phần lẻ dồn vào đợt cuối); nhãn bộ lọc đúng ngữ cảnh (Phải trả lọc theo NCC, không theo khách hàng).

Tách hẳn khu quản trị website (tin tức, slideshow, tuyển dụng công khai…) khỏi menu điều hành.

Kỹ thuật và bảo mật

Đăng nhập bằng tài khoản Google Workspace công ty (OAuth 2.0); xác thực 2 lớp cho Admin và BGĐ; không cho tự đăng ký.

Nhật ký hoạt động chỉ đọc, không ai xóa được, lưu tối thiểu bằng thời hạn lưu hồ sơ công trình; ghi đúng người thực hiện.

Không xóa cứng dữ liệu nghiệp vụ; chặn hủy khi đã có dữ liệu liên kết.

Sao lưu cơ sở dữ liệu hằng ngày; môi trường staging tách production (dùng mã định danh hệ thống riêng cho từng môi trường).

Khóa bí mật tích hợp (Drive…) mã hóa bằng khóa cố định, không đổi khi deploy.

Xuất Excel / PDF cho mọi danh sách và biểu mẫu; xuất dữ liệu kế toán theo định dạng phần mềm kế toán đang dùng.

### 10. Phạm vi triển khai theo giai đoạn

Làm nền trước, chạy thật một dự án, rồi mới bật KPI tự động. KPI tính trên dữ liệu thiếu sẽ tạo tranh cãi hơn là giúp quản lý.

| Giai đoạn | Phạm vi | Tiêu chí hoàn thành |
| --- | --- | --- |
| 1. Nền | Sửa lỗi nghiêm trọng (Drive, nhật ký, xóa cứng, Super Admin, thông báo, đăng nhập); 16 vai trò + ma trận duyệt + ủy quyền; M0 Công việc & Báo cáo ngày; động cơ Phiếu đề xuất; cổng giai đoạn; cây Drive 8 thư mục | Toàn bộ nhân viên văn phòng nộp báo cáo ngày trên web 2 tuần liên tục; thư mục "Báo cáo công việc" trên Drive không còn cần |
| 2. Dự án mẫu | Bổ sung các mục [Mới] của M1–M6; 35 mẫu số (ưu tiên PH-02, PH-05, PH-11, BB-05, BB-07, HD-03); đợt chi tuần; nhập đủ dữ liệu dự án mẫu (tiến độ, BOQ, NCC, đơn giá, mẫu QA/QC) | Một dự án đang chạy đi hết giai đoạn 3–5 trên web, không dùng file Excel riêng cho MR, nghiệm thu, thanh toán |
| 3. Quản trị | KPI đủ 11 vị trí (70/30); dashboard theo vai trò; app hiện trường offline; P&L dự án; xuất dữ liệu kế toán; cổng 6–7 | 2 kỳ KPI liên tiếp khóa đúng hạn, mọi chỉ số "Có dữ liệu" |

### 11. Thay đổi so với PRD v1 và các điểm BGĐ cần chốt

Thay đổi chính so với PRD v1

| PRD v1 | PRD v2 | Lý do |
| --- | --- | --- |
| Luồng liên vết 13 bước vẽ bằng ký tự | 7 giai đoạn + 7 cổng duyệt có checklist | Một khung chung cho cả 4 loại hợp đồng; hệ thống chặn được khi thiếu điều kiện |
| Không có module giao việc / báo cáo chung | Thêm M0 Công việc & Báo cáo | Quy trình nhân sự ISO lấy báo cáo ngày/tuần làm gốc đánh giá |
| Mỗi module tự có phiếu, luồng duyệt riêng | 4 động cơ dùng chung + ma trận duyệt tối đa 3 bước theo hạn mức | Ít màn hình hơn, thêm loại phiếu bằng cấu hình |
| 11 vị trí, chưa có BGĐ / trưởng phòng / kho | 16 vai trò khớp 1–1 với KPI và với web | Luồng duyệt cần các vai trò quản lý |
| BOQ "từ Module 2" / "BOQ cơ sở M5" | Một BOQ gốc: dự thầu → hợp đồng (= ngân sách gốc) → cập nhật bằng VO | Hết mâu thuẫn nguồn BOQ giữa các module |
| KPI 100% định lượng | 70% định lượng + 30% đánh giá quản lý; sửa KPI Rev thiết kế, HSE, Sales | Tránh chỉ số gây phản tác dụng; giữ đánh giá định tính của ISO |
| Vendor Rating 4 tiêu chí | 5 nhóm tiêu chí gộp từ 9 tiêu chí ISO | Một bộ cho cả NCC và thầu phụ |
| Cây Drive "8 lớp" nhưng vẽ 6 thư mục | 8 thư mục cấp 1 theo giai đoạn, Shared Drive | Khớp với giai đoạn và phân quyền |
| ~150 biểu mẫu ISO đứng riêng | 35 mẫu số, xuất ra đúng mẫu ISO | Nhập một lần, hồ sơ tự sinh |
| Thiếu: khách hàng sau bán, phát sinh, thiết bị, đợt chi, bảo lưu, quyết toán, bảo hành | Đã bổ sung (các mục [Mới]) | Có trong quy trình ISO đã ban hành |

Các điểm BGĐ cần chốt trước khi lập trình

☐ Hạn mức duyệt A (MR, mua hàng), B (phát sinh), C (tạm ứng)

☐ Tỷ lệ KPI định lượng / đánh giá quản lý (đề xuất 70/30) và mục tiêu từng chỉ số

☐ Ngưỡng biên lợi nhuận tối thiểu để qua cổng 2

☐ Giao bước "Pháp chế" của luồng ký HĐ cho Pháp lý hay Kế toán trưởng

☐ Giữ lịch đợt chi tuần theo ISO (thứ 6 nộp – thứ 7 duyệt – thứ 3 chi) hay đổi

☐ App hiện trường: app cài đặt hay web dạng app

☐ Ban hành lại bộ quy trình ISO theo ISO 9001:2015, dựa trên PRD v2 (sơ đồ tổ chức, mô tả công việc, danh mục biểu mẫu)
