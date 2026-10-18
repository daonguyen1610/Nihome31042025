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

Baseline nghiệp vụ điều khiển cho lần rà soát này là
`docs/Nicon/MoMs/2026-10-06/NICON_Meeting_2026-10-03_Tom-tat-yeu-cau-moi.md`.
Nếu tài liệu hoặc giả định triển khai về sau mâu thuẫn với baseline này mà chưa
có xác nhận bằng văn bản của NICON, baseline ngày 03/10/2026 được ưu tiên và
điểm mâu thuẫn phải được giữ ở trạng thái `Open decision`.

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
- Baseline 03/10 xác định năm nhóm/phòng: `Kinh doanh/CRM`,
  `Hành chính nhân sự`, `Thiết kế`, `Quản lý thi công` và
  `Tài chính kế toán`. Vị trí tổ chức của Cung ứng và Pháp lý chưa được chốt.
  Mỗi nhóm có
  bộ quyền mẫu do quản trị viên cấu hình; khi tạo vai trò, quản trị viên có thể
  sao chép bộ quyền mẫu rồi chỉnh độc lập. Thay đổi quyền mẫu không hồi tố vào
  vai trò đã tạo và không được vượt quá quyền hiện có của người thao tác.
- Quyền xem toàn bộ không kéo theo quyền sửa toàn bộ.
- Người lập không được thực hiện bước thẩm định độc lập trên cùng chứng từ khi
  quy trình yêu cầu kiểm soát chéo.
- Mỗi đầu việc Thiết kế có đúng một người duyệt, khác người thực hiện; người cần
  theo dõi dùng CC. Người thực hiện bấm hoàn thành để hệ thống tự chuyển sang
  `Pending`; chỉ người duyệt được chỉ định được duyệt hoặc trả lại. Design Lead
  duyệt đầu việc trực tiếp; Design Manager tổng hợp ý kiến và chốt chuyển giai đoạn.
- Backend phải kiểm tra quyền, scope Dự án, trạng thái và concurrency; ẩn nút ở
  frontend không phải biện pháp bảo mật.

### 2.2.1. Nhóm công việc liên phòng ban — Partial

- Kinh doanh phụ trách Cơ hội hoặc PM của Operational Project tạo một nhóm công
  việc cho phòng nhận; quản trị viên có quyền ngoại lệ. Nhóm theo Dự án luôn giữ
  `OperationalProjectId`; `ContractId` chỉ là liên kết tùy chọn và không làm đổi
  gốc dữ liệu khi ký hợp đồng.
- Design và Construction tiếp tục vận hành trên các aggregate chuyên môn hiện
  có; nhóm việc Dự án dùng chung chỉ nhận việc tổng quát cho Cung ứng hoặc Tài
  chính để tránh tạo thêm một bản sao Dự án theo phòng.
- Người chủ trì phòng nhận chỉ giao đầu việc cho nhân sự đang hoạt động thuộc
  cùng nhóm phòng ban. Mỗi đầu việc có WBS, thời gian, nguồn lực, người phụ
  trách, công tác tiên quyết và người theo dõi CC.
- Người phụ trách tự cập nhật tiến độ sau khi nhóm chuyển sang `Active`. Công
  tác chỉ được bắt đầu khi mọi công tác tiên quyết đã hoàn thành. Thông báo mở
  trực tiếp đúng đầu việc.
- Mọi đầu việc Dự án ở `Completed` cần người xác nhận độc lập chuyển sang
  `Verified` trước khi đóng nhóm; trả lại phải có lý do và đưa đầu việc về
  `InProgress`. Đầu việc không gắn KPI không phát sinh điểm KPI.
- Công việc nội bộ nằm ở tab riêng, không gắn Dự án, khách hàng hay hợp đồng.
  Một nhóm nội bộ có thể chọn nhiều phòng tham gia; mỗi đầu việc chỉ rõ một
  phòng phụ trách và người được giao phải thuộc phòng đó.
  Không bắt buộc duyệt mọi đầu việc. Nếu liên kết KPI, người xác nhận phải khác
  người thực hiện; chỉ đầu việc đã hoàn thành và được xác nhận mới được tính.
- Xóa Dự án xóa các nhóm việc thuộc Dự án theo deletion-impact. Xóa Hợp đồng
  hoặc Cơ hội chỉ gỡ liên kết tùy chọn và giữ nguyên nhóm việc dưới Dự án.

Các quy tắc giữ nhóm ở Dự án khi có hợp đồng, người được quyền khởi tạo giao
việc liên phòng và việc duyệt/KPI của công việc nội bộ là hành vi triển khai hiện
tại, chưa phải quyết định đã được NICON chốt (Q-01 đến Q-03).

### 2.2.2. Liên phòng ban ở giai đoạn trước hợp đồng — Implemented

Yêu cầu MoM §5. Cả ba điểm giao việc dưới đây đều chạy trên "nhóm công việc dự
án" hiện có, không thêm tab hay màn hình riêng.

- **Cung ứng cung cấp giá đầu vào.** Kinh doanh giao nhóm việc mục đích
  `ProcurementInputPricing` cho phòng Cung ứng, gắn Cơ hội và Dự án dùng chung.
  Cung ứng nhập chào giá nhà cung cấp/thầu phụ (dòng hạng mục, số lượng, đơn
  giá, VAT, hiệu lực). Trước hợp đồng Cung ứng chỉ cần thông tin Dự án; hợp đồng
  chưa bắt buộc.
- **Vòng đời giá đầu vào.** `Draft → Submitted → Approved | Rejected`. Chỉ sửa
  được ở `Draft`/`Rejected`; người lập không được tự duyệt hoặc từ chối. Giá đã
  được báo giá tham chiếu không sửa nữa.
- **Kinh doanh làm báo giá đầu ra từ giá đầu vào.** Báo giá có thể chọn một giá
  đầu vào `Approved`, còn hiệu lực, thuộc đúng Dự án và đúng nhóm việc Cung ứng.
  Báo giá lưu bản chụp (`InputPriceSnapshotJson`) tại thời điểm gắn để truy vết;
  đổi giá đầu vào không làm đổi báo giá đã phát hành.
- **Kiểm soát BOQ đã ký là việc sau hợp đồng.** Đây là bước tách biệt với giá đầu
  vào: chỉ BOQ thực hiện gắn hợp đồng đầu ra đã ký, còn hiệu lực và đúng Dự án
  mới được tạo/cập nhật; BOQ nháp, đã hủy hoặc khác Dự án bị chặn.
- **Tài chính – Kế toán rà soát điều kiện hợp đồng sau báo giá.** Nhóm việc mục
  đích `FinanceCommercialReview` gắn Báo giá nguồn. Checklist gồm điều kiện hợp
  đồng, bảo lãnh và điều kiện thanh toán; hồ sơ phải được duyệt độc lập trước
  khi báo giá được gửi khách hàng. Hồ sơ gắn với **phiên bản báo giá** đã rà
  soát (`ReviewedQuoteVersion`): báo giá đổi giá trị sau khi duyệt sẽ tăng
  phiên bản và phải được Tài chính rà soát lại trước khi gửi.
- **Tài chính – Kế toán rà soát hồ sơ tài chính dự thầu trước khi nộp.** Nhóm
  việc mục đích `FinanceTenderReview` gắn Gói thầu nguồn. Checklist gồm hồ sơ tài
  chính và bảo lãnh; hồ sơ phải được duyệt độc lập trước khi Gói thầu chuyển
  `Submitted`.
- **Điều kiện bắt buộc của cổng.** Báo giá phải gắn Dự án vận hành để Tài chính
  có chỗ ghi rà soát; Gói thầu phải gắn Dự án vận hành trước khi nộp.
- **Đổi nhãn.** Tên phòng ban hiển thị là "Cung ứng" (trước đây "Mua sắm"); module
  nghiệp vụ vẫn là Module 5 — Cung ứng và kho.

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
  Trên Hợp đồng Upstream, truy vết Lead đã chuyển đổi qua đúng Cơ hội nguồn và
  khách hàng; hiển thị link theo quyền xem Lead độc lập. Không thêm trường chọn
  Lead trực tiếp, không suy diễn từ khách hàng/Dự án và không bắt buộc Hợp đồng
  tạo trực tiếp phải có Lead nguồn.
- Pipeline chuẩn: Tiếp cận → Khảo sát → Báo giá/Đấu thầu → Thương thảo → Ký HĐ.
- Báo giá trực tiếp hỗ trợ suất đầu tư và BOQ sơ bộ, tính VAT/chiết khấu/tổng,
  version và duyệt nội bộ/khách hàng. Báo giá xuất và gửi khách dưới dạng file
  Excel có đầy đủ dòng giá, VAT, chiết khấu và tổng cộng.
- Workbook BOQ một/nhiều sheet được preview, mapping và chuẩn hoá tại Danh mục
  đơn giá BOQ, không import trực tiếp vào Báo giá. Báo giá BOQ áp dụng một
  revision danh mục đã duyệt và lưu bản chụp; dòng ngoại lệ được nhập bằng bảng
  cột/hàng có validation hoặc thêm từng dòng, không cập nhật ngược danh mục.
- Đấu thầu bắt buộc gắn Dự án đã có. Kế hoạch dùng nhóm công việc theo từng
  phòng ban với người phụ trách, người duyệt độc lập, ngày bắt đầu/kết thúc,
  dependency và tiến độ; khoảng một tháng là hướng dẫn lập kế hoạch, không phải
  giới hạn cứng. Chỉ được nộp khi các nhóm việc đã hoàn thành/xác nhận, checklist
  hồ sơ hoàn tất, dự toán được duyệt và TC–KT duyệt hồ sơ tài chính/bảo lãnh.
  Hồ sơ năng lực liên kết thư viện dùng chung; BOQ, biện pháp và bản vẽ được tải
  riêng theo gói. Mục hồ sơ bắt buộc/không bắt buộc bám theo HSMT; mỗi lần nộp
  phải lưu bản chốt bất biến của đúng phiên bản bằng chứng, dự toán, kế hoạch,
  duyệt Tài chính, kênh/biên nhận, người và thời điểm nộp. Yêu cầu bổ sung được
  lập thành lần nộp mới, không ghi đè lịch sử; kết quả cuối khóa chỉnh sửa.
  Trúng thầu mở Cơ hội ở `Negotiation` trên đúng Dự án hiện có,
  sau đó người dùng chủ động lập hợp đồng.
  Thống kê tỷ lệ trúng = số gói trúng / tổng gói đã nộp, mỗi gói tính một lần
  theo ngày nộp đầu tiên trong kỳ giờ Việt Nam. Mẫu số gồm gói chờ kết quả và
  gói đã nộp rồi hủy; không gồm gói chưa nộp. Báo cáo dùng kết quả hiện tại,
  lọc khách hàng/người phụ trách và mở danh sách đối chiếu. Thiếu bằng chứng
  nộp lần đầu phải cảnh báo và loại khỏi kỳ; không tự suy diễn lịch sử.
- Khảo sát mobile ghi điều kiện hiện trường, tọa độ, ảnh/video/file và checklist;
  đồng bộ có trạng thái, retry và xung đột rõ ràng.
- Hợp đồng Upstream có hai hướng tạo đã được xác nhận ngày 16/10/2026: (1) tạo
  từ Báo giá đã duyệt; hoặc (2) tạo trực tiếp không cần Báo giá khi khách hàng
  đã biết và thống nhất giá trị Hợp đồng. Nếu chọn hướng Báo giá, Kinh doanh lập
  báo giá theo suất đầu tư hoặc BOQ, hoàn tất duyệt nội bộ và rà soát Tài chính
  trước khi lập Hợp đồng; không được gắn Báo giá nháp/chưa duyệt để lách luồng.
- Kinh doanh duyệt nội bộ báo giá rồi
  xem trước email, chọn địa chỉ khách và gửi; báo giá được gắn vào hợp đồng phải
  đã duyệt, còn hiệu lực khi gắn và đúng khách hàng/Dự án. Hợp đồng Downstream
  với NCC/thầu phụ không dùng báo giá CRM đầu ra.

Theo dõi thực hiện Hợp đồng hiển thị riêng công việc, nghiệm thu/QS và trạng
thái lịch thanh toán; chưa gộp thành một phần trăm hoàn thành. Chỉ lấy nguồn gắn
trực tiếp Hợp đồng, theo quyền module nguồn độc lập, có link đối chiếu. Không gán
toàn bộ công việc/nghiệm thu cấp Dự án cho một Hợp đồng. Mốc thanh toán không
đồng nghĩa sổ thực thu/thực chi; hồ sơ QS lịch sử/đảo không tính là duyệt còn hiệu lực.

### Điểm mở

Quy ước mã Dự án và các field CRM NICON muốn bỏ cần được xác nhận bằng văn bản.

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

### 4.2. Luồng duyệt và điều hành — Implemented

- Tiến độ và Đội ngũ là hai tab liên kết trên cùng Operational Project. Đầu việc
  có mô tả, ngày kế hoạch/thực tế, người phụ trách, đúng một người duyệt, CC,
  đầu việc cha, tiền nhiệm, nguồn lực/thầu phụ và cờ công tác găng.
- Công việc thuê ngoài liên kết bằng `VendorId` và `DownstreamContractId`; hợp
  đồng phải cùng Dự án, đúng nhà cung cấp, có chiều Downstream và thuộc loại
  Design/Subcontract. Người phụ trách nội bộ vẫn bắt buộc.
- Hoàn thành đầu việc tự động gửi Design Lead/người duyệt đã chỉ định. CC chỉ
  nhận thông tin, không có quyền phê duyệt.
- Mỗi revision hồ sơ Concept, Basic Design hoặc Shop Drawing có danh sách người
  nhận nội bộ cụ thể. Người nhận chỉ xem và bình luận. Design Manager tổng hợp,
  ghi nhận đã trình khách và quyết định `ClientApproved`,
  `ClientChangesRequested` hoặc `Rejected`; quyết định không đạt bắt buộc lý do.
- Cổng chuyển giai đoạn yêu cầu revision mới nhất của đúng giai đoạn đã được
  khách hàng duyệt. Phát hành IFC yêu cầu Shop Drawing đã được khách duyệt;
  Thi công chỉ sử dụng IFC ở trạng thái Released.
- Tài liệu trình phải là Project Document đúng Dự án và đúng category giai đoạn;
  tab Tài liệu hỗ trợ upload, trạng thái đồng bộ và liên kết Google Drive.
- Baseline chỉ xác nhận rằng quyền xóa hồ sơ không đạt phải dành cho cấp quyền
  cao nhất. Cơ chế hiện tại dùng `design.records.hard-delete`, mặc định cấp BGD
  và SUPER_ADMIN, để
  xóa vĩnh viễn hồ sơ không đạt (`Discarded`, `Rejected` hoặc `Cancelled`).
  Người xóa phải xem trước ảnh hưởng, nhập đúng mã hồ sơ và dùng plan token còn
  hiệu lực. Hệ thống chặn xóa khi tệp chưa dọn an toàn, Shop Drawing còn nằm
  trong gói IFC hoặc đợt trình dùng chung với hồ sơ khác; đợt trình chỉ thuộc
  riêng hồ sơ được xóa cùng lịch sử liên quan.
- Sau khi một đợt review đã ghi nhận `ClientSubmittedAt`, cơ chế hiện tại chặn
  hard delete và yêu cầu revision thay thế. Đây là mặc định an toàn kỹ thuật;
  NICON vẫn phải chốt Q-08 về thẩm quyền xóa và việc hồ sơ đã trình khách có bao
  giờ được phép xóa hay không.

Vai trò `PM` thuộc nhóm `CONSTRUCTION` hiện là đại diện kỹ thuật cho người nhận
Phòng CM trong luồng trình nội bộ. Việc tách thêm chức danh PM Phòng CM/BCH chi
tiết vẫn theo quyết định vai trò Module 4, không làm rộng quyền sửa hồ sơ Thiết
kế của người nhận.

Q-11 về nhập đầy đủ định dạng MS Project (calendar/baseline/import) vẫn là mục
tiêu; màn hình hiện tại đã giữ các trường nghiệp vụ cần thiết nhưng chưa phải
trình nhập `.mpp`.

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

**Yêu cầu mục tiêu:** Khi hợp đồng chuyển sang `InProgress`, hệ thống tự sinh
danh sách thủ tục pháp lý áp dụng và PM phân công từng thủ tục ngay từ đầu.
Danh mục áp dụng theo loại/quy mô dự án vẫn cần NICON chốt tại Q-09.

**Hiện trạng triển khai:** Hợp đồng Upstream loại Design, Construction hoặc
DesignAndBuild chỉ chuyển từ `Signed` sang `InProgress` qua wizard Pháp lý do PM
của Operational Project hoặc Admin xác nhận. Wizard đề xuất các `permit_type`
đang hoạt động để PM chọn thủ công, chọn chủ trì Pháp lý, người thực hiện/người
duyệt, lịch và predecessor. Không tạo permit ngầm khi tạo `DesignProject`.

Mỗi Operational Project có tối đa một work package `LegalPermitting`; nhiều hợp
đồng cùng dự án dùng chung package nhưng có activation audit riêng. Mỗi permit
có một `DepartmentWorkTask` để quản lý WBS, assignee, approver, lịch, tiến độ và
duyệt nội bộ. Assignee và approver phải là thành viên dự án đang hoạt động và
không được trùng nhau. Permit chỉ được trình hoàn thành khi trạng thái cơ quan
là `Issued`, đã có ngày cấp và hồ sơ giấy phép. Duyệt nội bộ chuyển task
`Completed → Verified`; trả lại có lý do chuyển `Completed → InProgress` mà
không thay đổi trạng thái cơ quan.

**Partial:** Workspace `/admin/permits`, tài liệu nguồn `Legal`, risk filters,
history và cổng kiểm tra permit đã có. Việc xác định permit nào là bắt buộc cho
từng mốc khởi công vẫn phụ thuộc cấu hình dự án.

**Open decision (Q-09):** NICON chưa chốt template giấy phép theo loại/quy mô
công trình; hệ thống không tự suy đoán quy tắc này.

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

Theo phản hồi NICON do người dùng cung cấp ngày 18/10/2026 (không xác định ngày
NICON trả lời), mỗi Dự án có một BCH riêng; một nhân sự được tham gia BCH của
nhiều Dự án cùng thời điểm. Phân quyền và trách nhiệm vẫn theo từng Dự án,
không suy ra quyền trên Dự án khác từ việc cùng nhân sự.
Nguồn phản hồi được lưu tại
[NICON CM/ISO](Nicon/MoMs/2026-10-18/NICON_Phan-hoi_CM_ISO.md).

**Target:** màn hình tổng hợp CM theo dõi tiến độ; khối lượng hoàn thành với
nghiệm thu đầu ra Upstream và đầu vào Downstream tách biệt; ngân sách so sánh
giá trị thực hiện với dự toán; hồ sơ nghiệm thu chất lượng QA/QC; và an toàn.
Danh sách này đã được NICON xác nhận. Công thức giá trị thực hiện, kỳ báo cáo,
nguồn số liệu và baseline so sánh vẫn phải được thống nhất trước khi tính toán.

Trễ tiến độ **lớn hơn 5%** so với baseline tạo cảnh báo đỏ, giải trình và kế
hoạch phục hồi. Công thức, kỳ chốt và nguồn progress phải lưu được để tái lập.

### 6.2. Nguồn lực và BOQ

- Đề xuất nguồn lực bắt buộc có loại, WBS, ngày cần, khối lượng và lý do.
- MR vật tư kiểm tra Execution BOQ revision được duyệt, không dùng BOQ sơ bộ.
- Trong hiện trạng, vượt allowance còn lại bị chặn. Mục tiêu 85% vàng, 95% cam,
  trên 100% đỏ/chặn chỉ triển khai sau khi chốt công thức và VO behavior.
- Đề xuất ngoài kế hoạch/vượt hạn mức cần CM level-2 review độc lập trước khi
  chuyển Procurement.
- **Target:** sau CM duyệt đề xuất máy/thiết bị hoặc nhân công, hồ sơ chuyển
  phòng Cung ứng để lập hợp đồng đặt mua/cung cấp cho công trình, sau đó phòng
  FA xử lý thanh toán theo hồ sơ hợp lệ. CM duyệt không đồng nghĩa đã mua,
  đã cung cấp hoặc đã thanh toán; giữ liên kết đề xuất, HĐ Downstream và hồ sơ
  thanh toán bằng stable ID. Không suy diễn mọi loại máy đều là mua tài sản.

### 6.3. Chất lượng, an toàn và nhật ký

- QA/QC dossier gồm checklist, biên bản mẫu/thí nghiệm, chứng chỉ vật liệu, IFC
  revision và bằng chứng ảnh/file.
- Punchlist có vị trí, severity, root cause, assignee, deadline, xử lý, verify và
  reopen; lỗi chặn chưa đóng tham gia readiness nghiệm thu/bàn giao.
- HSE có người vi phạm/chịu trách nhiệm, remediation owner/deadline, evidence,
  confirmation độc lập, correction và close.
- Draft/offline chưa đồng bộ không phải bằng chứng duyệt hoặc KPI.

**Target:** dùng biểu mẫu trong bộ hồ sơ ISO NICON đã gửi làm nguồn chuẩn cho
từng quy trình và từng bước, bao gồm nghiệm thu, lấy mẫu, thí nghiệm, chứng chỉ
vật liệu và nhật ký khi áp dụng theo ISO. Không tự tạo mã mẫu hoặc suy luận một
mẫu áp dụng cho mọi công việc. Cần kiểm kê file nguồn, mã/phiên bản mẫu, quy
trình/bước áp dụng và điều kiện bắt buộc; giữ phiên bản mẫu trong hồ sơ đã lập.
Repository hiện chưa có bộ biểu mẫu ISO được nhận diện để kiểm kê. Đây là
khoảng trống hiện vật/mapping, không phải quyết định còn mở về chọn bộ ISO.
Sau đối chiếu bộ đã gửi, báo riêng từng form thiếu để NICON cung cấp bổ sung.

### 6.4. QS, nghiệm thu và thanh toán

- Upstream QS liên kết HĐ Upstream, CĐT/TVGS, WBS, BOQ/VO và milestone phải thu.
- Downstream QS liên kết HĐ Downstream, NCC/thầu phụ/tổ đội, WBS và dossier phải
  trả.
- Khối lượng đã duyệt bất biến; correction dùng version thay thế hoặc reversal.
- Payment review Downstream dùng HĐ hợp lệ + QS được duyệt + warehouse receipt
  posted khi gói có vật tư. Thành phần chứng từ theo loại package phải được
  đối chiếu bộ ISO NICON đã gửi; phần thiếu chuyển FA, chị K/Kiara, bổ sung.

### 6.5. Hoàn công và bàn giao

- Có ít nhất một acceptance được duyệt, đủ category as-built bắt buộc, không còn
  Punchlist chặn, commissioning và checklist hoàn tất.
- Final handover cần signatory và quyền `complete`; reopen giữ status history.
- Điều kiện kỹ thuật không thay thế điều kiện tài chính/hợp đồng.

### 6.6. Ranh giới hiện trạng

Construction Task, Diary, Punch, Acceptance, As-built và Handover hiện dùng
`DesignProjectId`; HSE dùng `OperationalProjectId`. Các aggregate mới
`ConstructionPreparation`, `ConstructionResourceRequest`, `QaQcDossier` và
`ConstructionQsBatch` đã dùng `OperationalProjectId` và đã có API/UI cơ bản.

**Partial:** CM Preparation hiện lưu các baseline chính dưới dạng document
reference và readiness snapshot, chưa có WBS/Gantt/S-Curve/Budget Baseline dạng
dữ liệu có cấu trúc. Đề xuất vật tư đã tạo được Material Request; đề xuất máy và
nhân công mới dừng ở trạng thái sẵn sàng chuyển tiếp, chưa có handoff mua/thuê và
liên kết HĐ Downstream hoàn chỉnh. QA/QC đã lưu loại minh chứng theo từng
tài liệu; nội dung ISO/checklist bắt buộc vẫn thiếu hiện vật nguồn và mapping
đã đối chiếu theo từng loại công trình.
QS đã tách Upstream/Downstream, có contract/BOQ/
milestone và correction lineage, nhưng CM approval chưa tạo hồ sơ thanh toán.

Mọi mở rộng tiếp theo tiếp tục dùng Operational Project. Migration các aggregate
cũ cần giữ API tương thích, có backfill, rehearsal và rollback; không đổi khóa
gốc trong một lần triển khai.

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

**Implemented — nhập BOQ theo danh mục cấu hình (yêu cầu 17/10/2026):**
Danh mục đơn giá BOQ hiện có giữ bộ sheet/trường riêng; không tạo một module
“loại BOQ” trùng với danh mục. Có sáu cấu hình ban đầu rút từ sáu workbook nguồn
có cấu trúc khác nhau (manifest `Data/Seeds/boq-categories.json`); các nhóm
trường giống nhau được tái sử dụng trong manifest. ADMIN/người có quyền
`crm.material-rates.manage` có thể tạo danh mục, sao chép/chỉnh cấu hình trước
phiên bản đầu tiên, chọn sheet thêm/sửa/bỏ dòng hoặc tải mẫu Excel đúng danh
mục → điền → kiểm tra → xác nhận import vào Draft.

Cùng cấu hình kiểm tra cả nhập tay và import trên server: đúng catalog/revision,
sheet, field keys, required/type/length/precision, mã dòng duy nhất trong sheet.
File có dấu nhận diện catalog và hash cấu hình; sai danh mục/cột/sheet bị từ
chối, không ghép tự do. Cấu hình khóa từ phiên bản đầu; đổi bộ trường phải tạo
catalog mới, không âm thầm chuyển dữ liệu cũ. Schema hiện hỗ trợ Text/Number,
nhãn VI/EN/ZH/JA và phân biệt sheet có giá với thông tin tham khảo. Dòng tham
khảo không trở thành đơn giá hoặc allowance; chỉ dòng có giá đi vào Quote
snapshot theo luồng hiện có. Trạng thái duyệt và hạn mức Execution BOQ vẫn là
quy tắc độc lập, không suy từ danh mục nội dung.

**Partial cho phạm vi BOQ nâng cao:** mẫu tải xuống là mẫu nhập an toàn chuẩn
hóa theo danh mục, không phải bản sao giữ toàn bộ layout/formula của file nguồn.
Không tự tính khối lượng từ kích thước hoặc cộng các giá thành phần thành giá
tổng; người nhập cung cấp quantity/unit price tổng đã kiểm tra. Import nhận ô
số typed numeric, không formula, text-number, macro hay external link; file nguồn
trong docs giữ nguyên. Giữ hierarchy/provenance từng cell, tự chuyển đổi schema,
re-import hợp nhất theo UID và tính lại Excel đầy đủ vẫn là Target riêng. Quyết
định ISO/approval matrix hoặc công thức không được coi là đã chốt bởi việc có
cấu hình nhập liệu này.

## 8. Module 6 — Tài chính, chi phí và hợp đồng

### 8.1. Mục tiêu Tài chính và hợp đồng

Kiểm soát hợp đồng hai chiều, phải thu/phải trả, dòng tiền và lợi nhuận Dự án.

### 8.2. Yêu cầu Tài chính và hợp đồng

- Hợp đồng Upstream/Downstream có direction/type rõ, milestone, appendix/VO,
  attachment, lifecycle và Operational Project.
- VO được version, submit, approve/reject và không sửa ngầm baseline/BOQ gốc.
- Payment Request: Draft → Under Validation → Ready for Approval → Approved →
  Paid, với Rejected/Cancelled; actor và reference bắt buộc theo loại chứng từ.
- Hồ sơ thanh toán mục tiêu tách Upstream/Downstream, liên kết QS/milestone và có
  ba phần Công trường, CM, Kế toán theo template chứng từ đã được NICON duyệt.
- Accounting Period: Open → Closing → Closed; post-close correction có lifecycle
  và reversal, không sửa trực tiếp số liệu đã khóa.
- Cashflow/P&L phân biệt committed, accrued/accepted, paid/received và forecast;
  không gọi báo cáo là real-time nếu nguồn chưa đủ.

**Partial:** `PaymentRequest` hiện thực hiện luồng phải trả Downstream, yêu cầu
vendor, invoice và attachment chung. Hệ thống chưa có Payment Dossier hai chiều,
chưa lưu ba section Công trường/CM/Kế toán và chưa có ma trận chứng từ theo loại
Supply, Labour, Equipment hoặc Mixed Subcontract. Bộ ISO NICON và mapping biểu
mẫu là đầu vào bắt buộc trước khi publish template sản xuất. NICON xác nhận
chứng từ thanh toán cho bốn loại chỉ vật tư, chỉ nhân công, thuê máy/thiết bị và
hỗn hợp nằm trong bộ ISO đã gửi. Đội triển khai phải đối chiếu từng loại, bước
và chứng từ với file/mã/phiên bản ISO; chỉ yêu cầu FA (chị K/Kiara) bổ sung những
mục còn thiếu. Không tự quy định danh sách chứng từ hay bắt gói không có vật tư
tạo phiếu kho để qua gate.

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

Theo baseline 03/10/2026, Q-01 đến Q-11 cần quyết định có thẩm quyền và có
thể truy vết. Người dùng cho biết NICON đã ủy quyền mình quyết định hướng triển
khai; các phương án trong báo cáo đối chiếu là đầu vào để người được ủy quyền
chốt từng rule, không phải bằng chứng rằng code đã giao hoặc NICON đã nghiệm thu.
Biểu mẫu, danh mục và tham số chỉ có NICON nắm giữ vẫn cần hiện vật nguồn:

1. Nhóm việc trước hợp đồng được giữ ở Dự án hay chuyển/gắn sang Hợp đồng; cách
   xử lý khi một Dự án có nhiều Hợp đồng.
2. Công việc nội bộ có bắt buộc duyệt không; điều kiện và trọng số tính KPI.
3. Ai được khởi tạo giao việc liên phòng: PM Dự án, người phụ trách Cơ hội hay cả
   hai; phạm vi ngoại lệ của quản trị viên.
4. Cung ứng và Pháp lý là phòng độc lập hay trực thuộc phòng nào trong năm nhóm.
5. Quy ước mã Dự án: định dạng, thành phần, thời điểm khóa và xử lý trùng mã.
6. Người nhận trình nội bộ chỉ bình luận hay phải đồng ý trước khi trình khách.
7. Trưởng/phó phòng Thiết kế có được CC mặc định cho mọi Dự án không.
8. Cấp quyền nào được xóa hồ sơ không đạt; hồ sơ đã trình khách có được xóa
   không và điều kiện/bằng chứng bắt buộc.
9. Danh mục giấy phép dùng chung hay phụ thuộc loại/quy mô Dự án.
10. Ranh giới Design Lead duyệt đầu việc và Design Manager chốt phương án/chuyển
    giai đoạn.
11. Đội ngũ và Tiến độ Thiết kế dùng một màn hình hay hai tab liên kết; phạm vi
    import MS Project.

Ngoài ra, NICON cần chốt:

1. Đối chiếu bộ ISO đã gửi để map checklist/cổng khởi công theo loại công trình;
   báo NICON từng form thiếu. Nguồn chuẩn ISO đã được xác nhận.
2. Công thức/kỳ chốt S-Curve và quyền publish/unlock baseline.
3. Approval/delegation matrix cho MR, QA/QC, HSE, QS và VO.
4. Đối chiếu chứng từ thanh toán theo bốn loại Downstream package với ISO;
   FA (chị K/Kiara) bổ sung phần thiếu và xác nhận mapping/ngoại lệ chưa rõ.
5. Công thức dải BOQ 85/95/>100 và xử lý zero allowance/reversal/conversion.
6. Migration `DesignProjectId` → `OperationalProjectId` cho Module 2/3/4.
7. Migration cây Drive cho `01_Khao_sat`.
8. KPI definition, target và source cho các vị trí còn thiếu.

Không khóa thiết kế dữ liệu hoặc tuyên bố business-ready cho phần phụ thuộc cho
tới khi chủ sở hữu nghiệp vụ chấp thuận các quyết định tương ứng.

## 13. Truy vết biên bản ngày 03/10/2026

| Mục | Năng lực | Trạng thái | Bằng chứng triển khai hoặc khoảng trống |
|---:|---|---|---|
| 1 | Nhóm quyền/phòng ban | Partial | Có role/group/editor và sáu group chức năng trong seed, gồm HR riêng khỏi FINANCE. Đây chưa phải cơ cấu năm phòng được NICON xác nhận; vị trí tổ chức Cung ứng/Pháp lý và mapping quyền chính thức còn mở. |
| 2 | CRM và mã dự án | Implemented | Lead/Cơ hội/Dự án và đổi mã đã có; Hợp đồng Upstream hỗ trợ đúng hai hướng: từ Báo giá đã duyệt hoặc tạo trực tiếp khi khách hàng đã thống nhất giá trị. |
| 3 | Dự án dùng chung | Partial | Operational Project, work package, task, dependency, CC và thông báo đã có; Q-01 đến Q-03 còn mở. |
| 4 | Thiết kế trước hợp đồng | Implemented | Design Project và công việc thiết kế không phụ thuộc hợp đồng đã ký. |
| 5 | Liên phòng ban trước hợp đồng | Implemented | Procurement input, Finance review và gate báo giá/nộp thầu đã có. |
| 6 | BOQ | Partial | Có preview/mapping nhiều sheet; persistence chưa giữ hierarchy và provenance từng dòng. |
| 7 | Đấu thầu | Implemented | Kế hoạch, hồ sơ, năng lực, estimate, review, submission snapshot và kết quả đã có. |
| 8 | Thiết kế chi tiết | Partial | Schedule/review/Drive và liên kết Vendor/HĐ Downstream bằng stable ID đã có; nhập `.mpp` đầy đủ và markup theo file còn là mục tiêu. |
| 9 | Pháp lý | Partial | Có activation wizard, permit workflow và risk view; chưa tự sinh danh sách áp dụng vì template loại/quy mô công trình là Q-09. |
| 10 | Thi công | Partial | Có CM/BCH, resource request, QA/QC và QS; baseline cấu trúc, handoff và portfolio còn thiếu. |
| 11 | Tài chính kế toán | Partial | Có Payment Request Downstream; chưa có dossier hai chiều và ba section chứng từ. |
| 12 | Host và Drive NICON | Target | Chỉ công nhận sau deployment, OAuth NICON, health check và smoke test có bằng chứng. |
| 13 | Đầu vào NICON | Open decision | Nguồn ISO, luồng CM → Cung ứng → FA, danh sách kiểm soát CM và nhân sự BCH đa dự án đã xác nhận qua phản hồi được cung cấp 18/10/2026; còn thiếu hiện vật/mapping ISO, permit matrix và quy ước mã dự án. |
| 14 | Điểm chưa chắc chắn | Open decision | Công thức S-Curve, approval matrix và payment matrix chỉ được cấu hình sau xác nhận nghiệp vụ. |

`Implemented` yêu cầu đồng thời có persistence, API authorization, UI và test phù
hợp. Bảng này mô tả branch hiện tại, không phải bằng chứng trạng thái môi trường
host hoặc UAT của NICON.
