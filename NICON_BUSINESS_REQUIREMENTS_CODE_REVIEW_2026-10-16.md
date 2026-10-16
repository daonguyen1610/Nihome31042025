# NICON — Đối chiếu yêu cầu 03/10 với mã nguồn

Ngày rà soát: 16/10/2026

Phạm vi: backend, frontend, migration/seed, unit test, integration test và E2E
trong repository hiện tại.

Nguồn nghiệp vụ điều khiển:
`docs/Nicon/MoMs/2026-10-06/NICON_Meeting_2026-10-03_Tom-tat-yeu-cau-moi.md`.

## 1. Kết luận điều hành

Mã nguồn đã có nền tảng nghiệp vụ đáng kể, đặc biệt ở CRM/Dự án dùng chung,
Thiết kế trước hợp đồng, phối hợp Cung ứng–Tài chính trước hợp đồng và Đấu thầu.
Tuy nhiên chưa thể tuyên bố toàn bộ yêu cầu 03/10 là business-ready vì:

1. Nhóm quyền HR đã được tách khỏi Finance trong seed/migration, nhưng cơ cấu
   phòng ban chính thức vẫn chờ NICON quyết định tại Q-04. Không được coi sáu
   group kỹ thuật là sơ đồ tổ chức đã duyệt. Riêng Hợp đồng Upstream không có
   Báo giá không còn là sai lệch: xác nhận ngày 16/10/2026 cho phép hướng này
   khi khách hàng đã biết và thống nhất giá trị Hợp đồng.
2. Một số lựa chọn kỹ thuật đang được vận hành như quy tắc mặc dù NICON chưa
   trả lời: cách gắn nhóm việc khi có hợp đồng, quyền giao việc liên phòng, xóa
   hồ sơ đã trình khách và cách kích hoạt thủ tục Pháp lý.
3. Thiết kế chi tiết, BOQ, Thi công CM/BCH, Tài chính và tích hợp Drive đã có
   các lát cắt sử dụng được nhưng chưa đủ phạm vi mục tiêu hoặc còn thiếu đầu
   vào chuẩn từ NICON.
4. Không có bằng chứng trong repository để xác nhận phiên bản host/UAT và tài
   khoản Drive NICON đang vận hành.

### Tổng hợp trạng thái

| Trạng thái | Năng lực |
|---|---|
| Implemented | CRM lõi, mã Dự án và cổng Báo giá → Hợp đồng Upstream; Thiết kế khởi tạo từ Cơ hội trước hợp đồng; kiểm soát Cung ứng/Tài chính trước hợp đồng; Đấu thầu |
| Partial | RBAC theo phòng; nhóm công việc dùng chung; BOQ; Thiết kế chi tiết; Pháp lý; Thi công CM/BCH; Tài chính |
| Target | Host/UAT và chuyển kết nối Drive sang tài khoản NICON |
| Open decision | Cơ cấu tổ chức, các tham số Q-01–Q-11 chưa có hiện vật nguồn/quyết định truy vết, biểu mẫu ISO/CM/thanh toán, công thức S-Curve và ma trận phê duyệt |

`Implemented` trong báo cáo này chỉ có nghĩa là mã nguồn hiện có persistence,
API, UI và test phù hợp với yêu cầu được nêu. Nó không xác nhận đã deploy hoặc
đã được NICON nghiệm thu.

## 2. Cách rà soát và giới hạn bằng chứng

- Đã lập inventory toàn bộ file dưới `nihomebackend`, `nihomeweb`,
  `nihomebackend.tests`, `nihomebackend.integration.tests` và E2E smoke.
- Mỗi nhóm yêu cầu được truy vết theo chuỗi: route/UI → DTO/controller → service
  rule → persistence/migration/seed → test.
- Bản rà soát gốc là static review. Các test chạy sau đó được ghi riêng ở mục
  9; tên test trong ma trận không mặc nhiên là bằng chứng test vừa chạy.
- Không kiểm tra host, Google Drive thật, SMTP thật hoặc dữ liệu UAT của NICON.

## 3. Ma trận yêu cầu → mã nguồn

### BR-01 — Permission, vai trò và nhóm phòng ban

**Trạng thái: Partial — có framework và HR group riêng, cơ cấu tổ chức chưa chốt.**

Đã làm tốt:

- Permission là catalog hệ thống; role nghiệp vụ có thể tạo, đổi nhãn và chỉnh
  ma trận tại `RbacController`/`RoleService`.
- Có role-group baseline và cơ chế copy quyền ban đầu; thay đổi role sau seed
  được giữ lại.
- Có permission riêng cho duyệt Thiết kế, chuyển bước, scope xem và các hành
  động CM/BCH.
- Có unit/integration/E2E quanh seed, chống nâng quyền, role editor và route
  matrix: `RbacSeederTests`, `RoleServiceTests`, `RbacControllerTests`,
  `admin-rbac.spec.ts`, `admin-roles-ui.spec.ts`.

Khoảng trống/sai lệch:

- `rbac-defaults.json` hiện seed sáu group chức năng: `CRM`, `HR`,
  `PROCUREMENT`, `CONSTRUCTION`, `DESIGN`, `FINANCE`; `HR` thuộc `HR`, không còn
  thuộc `FINANCE`. Đây không phải xác nhận NICON có sáu phòng.
- Biên bản 03/10 liệt kê năm phòng nhưng chưa xác định vị trí Cung ứng/Pháp lý.
  Ảnh sơ đồ tổ chức lưu trong `docs/Nicon/` là rev 2022, có Cung ứng riêng và
  gộp Kế toán–Tài chính–Nhân sự; không dùng ảnh cũ để phủ định biên bản mới.
  Mapping chính thức Role × Department × Permission vẫn cần NICON xác nhận.

Bằng chứng chính:

- `nihomebackend/Data/Rbac/rbac-defaults.json:230`
- `nihomebackend/Services/RoleService.cs:146`
- `nihomebackend/Services/RoleService.cs:319`
- `nihomebackend/Services/RoleService.cs:425`
- `nihomebackend.integration.tests/Controllers/RbacControllerTests.cs:87`

### BR-02 — CRM lõi, Lead/Cơ hội và mã Dự án

**Trạng thái: Implemented.**

Đã làm tốt:

- Lead và Opportunity là hai lifecycle riêng; chuyển Lead tạo/liên kết Customer,
  Opportunity và Operational Project.
- Khách cá nhân không bị bắt nhập mã số thuế; duplicate dùng số điện thoại,
  trong khi công ty dùng mã số thuế.
- Dự án có mã gợi ý nhưng cho phép nhập mã riêng; có uniqueness, concurrency,
  audit và retry đổi tên thư mục Drive.
- Frontend có route Dự án, Cơ hội, Hợp đồng riêng.
- Menu CRM đặt Operational Projects, Opportunities và Contracts ở ba vị trí đầu
  đúng thứ tự ưu tiên của biên bản.

Bằng chứng chính:

- `nihomebackend/Services/LeadService.cs:769`
- `nihomebackend/Services/CustomerService.cs:643`
- `nihomebackend/Controllers/OperationalProjectsController.cs:30`
- `nihomebackend/Services/OperationalProjectService.cs:400`
- `nihomeweb/src/components/layout/AdminLayout.tsx:157`
- `nihomeweb/e2e/smoke/admin-opportunities.spec.ts:331`
- `nihomeweb/e2e/smoke/admin-operational-project-rename.spec.ts`

### BR-03 — Một Dự án dùng chung và nhóm công việc

**Trạng thái: Partial — lõi đã có, quyết định Q-01–Q-03 chưa được chốt.**

Đã làm tốt:

- Có `OperationalProject`, `DepartmentWorkPackage`, task, predecessor, WBS,
  assignee, approver, CC, progress, notification deep-link.
- Có hai loại `Project` và `Internal`; nhóm Internal bị chặn liên kết project,
  contract hoặc opportunity.
- Có kiểm tra người giao/người nhận theo group và tách người thực hiện với người
  xác nhận cho task Dự án và task liên kết KPI.
- Có API/UI và unit/integration/E2E cho happy path, quyền, liên kết sai Dự án,
  dependency và verification.

Chưa thể coi là hoàn tất yêu cầu:

- Code chọn giữ package ở Operational Project và xem `ContractId` là liên kết
  tùy chọn; NICON chưa trả lời cách chuyển/gắn khi có nhiều hợp đồng.
- Code cho PM/người phụ trách Cơ hội và một số admin tạo package; phạm vi quyền
  giao việc liên phòng chưa được NICON chốt.
- Cách duyệt và tính KPI công việc nội bộ là giả định hiện tại.

Bằng chứng chính:

- `nihomebackend/Services/DepartmentWorkPackageService.cs:164`
- `nihomebackend/Services/DepartmentWorkPackageService.cs:635`
- `nihomebackend/Services/DepartmentWorkPackageService.cs:818`
- `nihomebackend/Services/DepartmentWorkPackageService.cs:1042`
- `nihomebackend.integration.tests/Controllers/DepartmentWorkPackagesControllerTests.cs:35`
- `nihomeweb/e2e/smoke/admin-work-packages.spec.ts:20`

### BR-04 — Thiết kế bắt đầu từ Cơ hội trước hợp đồng

**Trạng thái: Implemented.**

Đã làm tốt:

- Cơ hội có hành động tạo/mở Dự án Thiết kế.
- API `POST /api/opportunities/{id}/design-project` tạo idempotent trên đúng
  Operational Project, không yêu cầu Contract.
- Phòng Thiết kế không tạo một Operational Project thứ hai.
- Có integration và E2E cho quyền, Opportunity mất/khác scope, tạo lặp và điều
  hướng frontend.

Bằng chứng chính:

- `nihomebackend/Controllers/OpportunitiesController.cs:249`
- `nihomebackend/Services/DesignProjectService.cs:174`
- `nihomeweb/src/App.tsx:348`
- `nihomeweb/src/pages/admin/OpportunityDesignCreate.tsx:123`
- `nihomebackend.integration.tests/Controllers/OpportunitiesControllerTests.cs:59`
- `nihomeweb/e2e/smoke/admin-opportunities.spec.ts:241`

### BR-05 — Cung ứng và Tài chính trước hợp đồng

**Trạng thái: Implemented.**

Đã làm tốt:

- `ProcurementInputPricing` gắn Cơ hội/Dự án, có dòng giá NCC/thầu phụ và vòng
  đời Draft → Submitted → Approved/Rejected.
- Người lập không tự duyệt; giá hết hạn/chưa duyệt/khác Dự án bị chặn khi gắn
  Báo giá; Báo giá lưu snapshot giá đầu vào.
- `FinanceCommercialReview` chốt version Báo giá; sửa giá trị buộc rà soát lại
  trước khi gửi khách.
- `FinanceTenderReview` và `TenderPlanning` chặn nộp thầu nếu chưa hoàn tất.
- BOQ thực hiện chỉ được tạo sau Hợp đồng Upstream đã ký.

Bằng chứng chính:

- `nihomebackend/Services/DepartmentWorkPackageService.cs:520`
- `nihomebackend/Services/DepartmentWorkPackageBusinessService.cs`
- `nihomebackend.integration.tests/Controllers/PrecontractCrossDepartmentControlsTests.cs:21`
- `nihomebackend.integration.tests/Controllers/PrecontractCrossDepartmentControlsTests.cs:142`
- `nihomebackend.integration.tests/Controllers/PrecontractCrossDepartmentControlsTests.cs:300`
- `nihomebackend.integration.tests/Controllers/PrecontractCrossDepartmentControlsTests.cs:385`

### BR-06 — Báo giá, BOQ và cổng tạo Hợp đồng

**Trạng thái: Partial — hai hướng tạo Hợp đồng đã đúng; BOQ nâng cao còn thiếu.**

Đã làm tốt:

- Báo giá hỗ trợ suất đầu tư và BOQ, version, duyệt, gửi email có preview và
  file Excel chi tiết.
- BOQ catalog hỗ trợ preview/mapping workbook một hoặc nhiều sheet; revision
  Approved được áp dụng vào Báo giá dưới dạng snapshot.
- API và UI cho phép tạo Hợp đồng Upstream trực tiếp khi khách hàng đã biết và
  thống nhất giá trị, không bắt buộc `QuoteId`.
- Khi người dùng chọn hướng có Báo giá, service bắt buộc Báo giá đã duyệt, còn
  hiệu lực, đúng Customer/Opportunity/Dự án và chưa được Hợp đồng live khác dùng.

Khoảng trống còn lại:

- Workbook import chưa chứng minh giữ đầy đủ hierarchy/merged cells/formula và
  provenance từng dòng cho mọi BOQ mẫu; không được coi các file mẫu là một schema
  đã duyệt.

Bằng chứng chính:

- `nihomebackend/Models/DTOs/Requests/ContractRequests.cs:29`
- `nihomebackend/Services/ContractService.cs:356`
- `nihomebackend.integration.tests/Controllers/CrmBusinessPipelineTests.cs:12`
- `nihomebackend.integration.tests/Controllers/ContractQuoteLinkTests.cs:81`
- `nihomebackend/Services/BoqCatalogWorkbookService.cs`
- `nihomeweb/e2e/smoke/admin-boq-catalog-workbook.spec.ts`

### BR-07 — Đấu thầu

**Trạng thái: Implemented.**

Đã làm tốt:

- Có Gói thầu, checklist theo HSMT, kế hoạch theo phòng, dependency/progress,
  dự toán, hồ sơ năng lực dùng chung, file theo gói và Finance review.
- Nộp thầu lưu snapshot; bổ sung không ghi đè lịch sử; kết quả trúng mở Cơ hội
  Negotiation trên Dự án hiện có và cho phép đi tiếp Hợp đồng.
- Có permission, validation deadline, concurrency/hard-delete safety và coverage
  unit/integration/E2E đáng kể.

Bằng chứng chính:

- `nihomebackend/Services/TenderService.cs`
- `nihomebackend/Services/TenderEstimateService.cs`
- `nihomebackend.integration.tests/Controllers/TendersControllerTests.cs:36`
- `nihomebackend.integration.tests/Controllers/PrecontractCrossDepartmentControlsTests.cs:300`
- `nihomeweb/e2e/smoke/admin-tenders.spec.ts`

### BR-08 — Thiết kế chi tiết, duyệt, hồ sơ và thầu phụ

**Trạng thái: Partial.**

Đã làm tốt:

- Có Concept, Basic, Shop/Detailed Drawing, review/revision và IFC release.
- Schedule có parent/predecessor, assignee, đúng một approver, CC, progress,
  critical flag và liên kết Vendor + Hợp đồng Downstream cùng Dự án.
- Review nội bộ giới hạn người nhận; Design Manager ghi nhận trình khách và
  quyết định; cổng chuyển phase/IFC dùng `ClientApproved`.
- Project Documents có metadata và đồng bộ Drive retryable.
- Hard-delete có impact plan, typed confirmation, concurrency và quyền riêng.

Khoảng trống/quyết định mở:

- Chưa có import `.mpp` đầy đủ calendar, hierarchy và baseline.
- Markup/feedback trực tiếp theo từng file chưa đủ bằng chứng end-to-end.
- Q-06, Q-07, Q-08, Q-10 và Q-11 chưa được NICON chốt.
- Code chặn mọi hard-delete sau `ClientSubmittedAt`; đây là mặc định an toàn,
  trong khi baseline để câu hỏi này mở.

Bằng chứng chính:

- `nihomebackend/Services/DetailDesignScheduleService.cs`
- `nihomebackend/Services/DesignReviewService.cs:116`
- `nihomebackend/Services/DesignReviewService.cs:194`
- `nihomebackend/Services/DesignRecordDeletionService.cs:164`
- `nihomebackend.integration.tests/Controllers/DetailDesignScheduleControllerTests.cs:173`
- `nihomebackend.integration.tests/Controllers/DesignReviewsControllerTests.cs:11`

### BR-09 — Pháp lý và xin phép

**Trạng thái: Partial.**

Đã làm tốt:

- Có workspace Permit, lifecycle hồ sơ, deadline/risk, assignee/approver độc lập,
  file và history.
- Có activation transaction an toàn, idempotent và dùng chung package Pháp lý
  cho nhiều hợp đồng trên một Dự án.

Khoảng trống:

- Baseline yêu cầu khi Hợp đồng `InProgress` thì tự sinh danh sách thủ tục để PM
  phân công. Code hiện chặn chuyển trạng thái trực tiếp và bắt PM/Admin mở wizard,
  tự chọn từ `permit_type` đang active rồi mới activate.
- Không thể tự sinh đúng danh sách cho đến khi NICON chốt Q-09 theo loại/quy mô
  công trình.

Bằng chứng chính:

- `nihomebackend/Services/LegalActivationService.cs:20`
- `nihomebackend/Services/LegalActivationService.cs:37`
- `nihomebackend/Services/LegalActivationService.cs:484`
- `nihomebackend.integration.tests/Controllers/LegalActivationControllerTests.cs:14`

### BR-10 — Thi công: BCH và Phòng CM

**Trạng thái: Partial — lát cắt lõi đã có, chưa đủ bộ nghiệp vụ mục tiêu.**

Đã làm tốt:

- Có route riêng BCH resource request, QA/QC, QS và CM workspace.
- Có role `SITE_COMMANDER`, `SITE_QAQC`, `PM`, `QS`; API tách quyền create tại
  công trường với review của CM và chặn tác giả tự review.
- Có CM preparation/readiness với WBS, schedule, S-Curve, BCH organization,
  resource plan, Budget Baseline và evidence snapshot.
- Resource request có ba loại material/equipment/labour; vật tư handoff sang MR.
- QA/QC gắn template version, IFC, WBS và evidence; QS bắt buộc chỉ rõ
  Upstream/Downstream và đúng loại Hợp đồng/milestone.
- QA/QC nay giữ loại minh chứng theo từng document ID; payload ID cũ được
  nhận, ID mới mặc định `General` và ID cũ giữ phân loại khi cập nhật. Không
  tự suy diễn loại từ tên file.

Khoảng trống:

- Chưa có đầy đủ danh sách nhóm việc Phòng CM do NICON cung cấp.
- Bộ form/checklist ISO chuẩn cho QA/QC, HSE, nghiệm thu và hồ sơ thanh toán chưa
  được xác nhận đầy đủ.
- Chưa đủ bằng chứng cho baseline versioning hoàn chỉnh, portfolio exception,
  công thức cảnh báo trễ >5%, offline/reconnect và conflict trên thiết bị BCH.
- Approval matrix cho MR, QA/QC, HSE, QS và VO chưa được chốt.

Bằng chứng chính:

- `nihomeweb/src/App.tsx:453`
- `nihomeweb/src/App.tsx:458`
- `nihomeweb/src/App.tsx:461`
- `nihomeweb/src/App.tsx:475`
- `nihomebackend/Services/ConstructionOperationsService.cs:255`
- `nihomebackend/Services/ConstructionOperationsService.cs:297`
- `nihomebackend/Services/ConstructionOperationsService.cs:465`
- `nihomebackend.integration.tests/Controllers/ConstructionOperationsControllerTests.cs:36`
- `nihomebackend.integration.tests/Controllers/ConstructionOperationsControllerTests.cs:81`

### BR-11 — Tài chính, Upstream/Downstream và hồ sơ thanh toán

**Trạng thái: Partial.**

Đã làm tốt:

- Contract có direction Upstream/Downstream; Finance hiển thị projection trên
  cùng Contract, không tạo master trùng.
- Có receivable milestones, nhắc hạn, Payment Request Downstream, phân tách người
  lập/người duyệt, chống invoice trùng, Paid immutability.
- Có Accounting Period và correction/reversal workflow.

Khoảng trống:

- Chưa có một payment dossier hoàn chỉnh hai chiều gắn QS/milestone.
- Chưa có ba phần chứng từ Công trường → CM → Kế toán theo mẫu NICON.
- Chưa chốt thành phần bắt buộc và three-way matching cho supply, labour,
  equipment và mixed package.
- Dashboard cashflow/P&L/KPI chưa thể coi là hoàn chỉnh nếu nguồn dữ liệu hoặc
  target còn `MissingData`.

Bằng chứng chính:

- `nihomebackend/Services/FinanceService.cs`
- `nihomebackend.integration.tests/Controllers/FinanceControllerTests.cs:32`
- `nihomebackend.integration.tests/Controllers/FinanceControllerTests.cs:199`
- `nihomebackend.integration.tests/Controllers/FinanceControllerTests.cs:326`
- `nihomebackend.integration.tests/Controllers/FinanceControllerTests.cs:356`

### BR-12 — Google Drive, host và bàn giao môi trường

**Trạng thái: Target — code có, trạng thái môi trường chưa có bằng chứng.**

Đã có trong mã:

- OAuth/configuration store, mã hóa credential, folder mapping, background sync,
  retry/lease/conflict và hard-delete policy.
- Project Document là metadata authoritative; Drive là external storage.

Chưa được chứng minh:

- Host nào đang chạy commit nào; smoke/health/UAT của NICON.
- OAuth đã chuyển sang tài khoản Drive do NICON cung cấp.
- Topology đích có gộp Survey vào CRM Pre-Design hay giữ hai category hiện tại.

Bằng chứng chính:

- `nihomebackend/Services/GoogleDrive/GoogleDriveOAuthService.cs`
- `nihomebackend/Services/GoogleDrive/GoogleDriveSettingsStore.cs`
- `nihomebackend/Services/GoogleDrive/ProjectDriveSyncService.cs`
- `nihomebackend/Services/GoogleDrive/GoogleDriveOptions.cs:37`

## 4. Sai lệch cần ưu tiên xử lý sau khi duyệt tài liệu

| Mức | Sai lệch | Ảnh hưởng | Hành động đề xuất |
|---|---|---|---|
| Resolved | Hợp đồng Upstream không bắt buộc `QuoteId` | Đây là hướng nghiệp vụ hợp lệ khi khách hàng đã biết và thống nhất giá trị; hướng có Báo giá vẫn bắt buộc Báo giá đã duyệt | Duy trì test hồi quy cho cả hai hướng và không biến `QuoteId` thành bắt buộc toàn cục |
| High | Sáu group quyền kỹ thuật chưa được xác nhận là năm phòng NICON | Có thể sai ownership, bộ quyền mặc định và báo cáo/KPI nếu coi group là cơ cấu tổ chức | NICON trả lời Q-04; rehearsal migration/rebind và kiểm tra quyền runtime trước rollout |
| Medium | Wizard Pháp lý cần chọn thủ công | Không đạt yêu cầu tự sinh khi Hợp đồng chạy | Chốt permit matrix; tự tạo idempotent và giữ bước PM phân công |
| Medium | Quy tắc package/assignment đang đi trước quyết định | Có thể phải migrate liên kết và quyền sau khi NICON trả lời | Đóng Q-01–Q-03 trước khi mở rộng dữ liệu thật |
| Medium | Chặn xóa sau trình khách là giả định kỹ thuật | Có thể lệch thẩm quyền/lưu trữ hồ sơ NICON | Giữ chặn an toàn cho tới khi Q-08 được trả lời |

## 5. Phương án thiết kế để triển khai và đối chiếu UAT

Các câu trả lời dưới đây là **đề xuất thiết kế theo yêu cầu hiện tại của người
dùng**, dựa trên biên bản 03/10, codebase và nguyên tắc kiểm soát nội bộ. Người
dùng cho biết NICON đã ủy quyền mình quyết định hướng thực hiện; điều đó cho
phép chọn phương án triển khai, nhưng không phải bằng chứng NICON đã nghiệm thu
hoặc từng quyết định đã được ghi nhận với ID/phạm vi/ngày hiệu lực. Những chỗ
cần sơ đồ tổ chức, biểu mẫu ISO, mẫu BOQ, hồ sơ pháp lý, chính sách lưu trữ
hoặc cấu hình môi trường thật vẫn cần đầu vào từ NICON.
Không coi phương án trong mục này là tính năng `Implemented` khi chưa có đủ
persistence, API authorization, UI và test tương ứng.

### P0 — Chặn hợp đồng nghiệp vụ và mô hình quyền

#### Q-01 — Gắn nhóm việc khi xuất hiện Hợp đồng

1. Nhóm việc tạo trước hợp đồng có giữ `OperationalProjectId` làm gốc và chỉ gắn
   thêm `ContractId`, hay phải chuyển ownership sang Hợp đồng?
2. Nếu một nhóm việc phục vụ nhiều Hợp đồng, cho phép nhiều liên kết hay phải
   tách package? Ai có quyền tách và phần trăm tiến độ được phân bổ thế nào?
3. Khi Hợp đồng bị hủy/thay thế, package được giữ, chuyển sang Hợp đồng mới hay
   đóng? Lịch sử và file có cần snapshot tại thời điểm chuyển không?
4. Báo cáo/KPI quy về Dự án hay Hợp đồng khi package đã gắn Hợp đồng?

**Phương án Q-01:**

1. `OperationalProject` luôn là business root. Package trước hợp đồng được giữ
   nguyên; `ContractId` chỉ là liên kết phân loại bổ sung, không chuyển ownership.
2. Một package chỉ được gắn tối đa một Hợp đồng. Công việc dùng chung nhiều Hợp
   đồng giữ ở package cấp Dự án; phần riêng phải tách thành package khác. PM hoặc
   người chủ trì package thực hiện tách, không phân bổ phần trăm hồi tố.
3. Hợp đồng bị hủy/thay thế không tự động chuyển package. Package cũ được đóng
   hoặc hủy có lý do; package kế tiếp liên kết Hợp đồng mới. History, file và
   snapshot tại thời điểm chuyển được giữ nguyên.
4. KPI và báo cáo tổng hợp theo `OperationalProjectId`; `ContractId` là chiều
   drill-down. Không tính trùng một task cho cả Dự án và Hợp đồng.

#### Q-02 — Công việc nội bộ và KPI

1. Công việc nội bộ có cần người duyệt/Verifier bắt buộc cho mọi task không?
2. Trạng thái chuẩn là gì; ai được hủy, mở lại hoặc sửa sau hoàn thành?
3. Task nào được tính KPI; cần metric riêng hay dùng chung với task Dự án?
4. Trọng số, kỳ ghi nhận và bằng chứng tối thiểu là gì? Task trễ qua hai kỳ tính
   vào kỳ hoàn thành hay kỳ deadline?
5. Task không được duyệt hoặc bị trả lại có ảnh hưởng KPI không?

**Phương án Q-02:**

1. Task gắn KPI hoặc task Dự án bắt buộc có verifier độc lập với assignee. Task
   nội bộ không gắn KPI không bắt buộc verifier và được hoàn tất trực tiếp;
   nếu gắn KPI thì người duyệt không được tự duyệt việc của mình.
2. Task cần duyệt theo `Planned → InProgress → Completed → Verified`; task nội
   bộ không KPI có thể dừng ở `Completed`. `Cancelled` là terminal. Với task
   cần duyệt, `Completed` nghĩa là chờ xác minh. Hủy hoặc mở lại phải do người
   chủ trì/Admin thực hiện với lý do và audit; sửa task đã `Verified` phải mở lại
   và xác minh lại, không ghi đè kết quả cũ.
3. Chỉ task `Verified` có `KpiDefinitionId` mới được tính KPI. Task Dự án và nội
   bộ dùng chung engine nhưng có dimension `Kind` để báo cáo riêng.
4. Trọng số nằm ở KPI definition, không nhập tự do trên task. Thành tích ghi vào
   kỳ của `VerifiedAt`; đúng hạn/trễ được đánh giá riêng theo `PlannedEnd`, nên
   task trễ qua kỳ không được chuyển deadline để làm đẹp KPI.
5. Task bị trả lại quay về `InProgress`, không ghi nhận KPI. Task hủy hoặc chưa
   xác minh không tạo điểm, nhưng vẫn giữ lịch sử phục vụ đo rework/SLA.

#### Q-03 — Quyền khởi tạo và giao việc liên phòng

1. PM Dự án, người phụ trách Cơ hội hay cả hai được tạo package trước hợp đồng?
2. Sau hợp đồng, Contract Owner có quyền tương đương PM không?
3. Trưởng phòng nhận có được thay assignee/approver của task không? PM gửi việc
   có được chỉ định trực tiếp nhân viên hay chỉ chọn trưởng phòng?
4. BGD/Admin có quyền ngoại lệ không; có bắt buộc ghi lý do và audit không?
5. Khi người dùng đổi phòng/nghỉ việc, task đang mở được tự động chuyển hay chặn
   cho tới khi người quản lý reassign?

**Phương án Q-03:**

1. Trước hợp đồng, PM Dự án và owner Cơ hội có quyền quản lý đúng Dự án được tạo
   package; người gửi chỉ chọn phòng nhận và người chủ trì đủ quyền.
2. Contract Owner không mặc nhiên có quyền PM. Muốn quản lý package phải là
   thành viên/PM của Dự án hoặc được cấp permission và scope rõ ràng.
3. Người chủ trì phòng nhận mới chọn/thay assignee và approver trong phòng. PM
   gửi việc không chỉ định trực tiếp nhân viên phòng khác, trừ package Pháp lý
   có rule cross-department riêng đã audit.
4. `SUPER_ADMIN`/`ADMIN` có emergency override; thao tác thay ownership,
   assignee hoặc approver phải có lý do và audit. BGD không mặc nhiên mutate.
5. Khi user đổi phòng/ngừng hoạt động, hệ thống không tự chuyển task. Task mở bị
   chặn transition cho tới khi người chủ trì/Admin reassign có audit; lịch sử
   assignee cũ được giữ.

#### Q-04 — Năm phòng, vị trí Cung ứng và Pháp lý

1. Xác nhận đúng năm nhóm cấp cao: CRM, Hành chính nhân sự, Thiết kế, Quản lý
   thi công, Tài chính kế toán?
2. Cung ứng là phòng thứ sáu, nhóm con của Quản lý thi công hay capability xuyên
   phòng? Kho thuộc Cung ứng hay tách quyền kiểm soát độc lập?
3. Pháp lý thuộc Quản lý thi công, Hành chính hay là vai trò xuyên phòng?
4. `QS` cần tách thành QS Đấu thầu/văn phòng và Site QS không?
5. Một người có thể thuộc nhiều phòng/group không; khi có nhiều role thì baseline
   quyền cộng dồn hay chọn một vai trò hoạt động?
6. Đề nghị NICON cung cấp bảng Role × Department × Permission baseline chính thức.

**Phương án Q-04:**

1. Đề xuất sáu **nhóm quyền chức năng** độc lập: `CRM`, `HR`, `DESIGN`,
   `CONSTRUCTION`, `PROCUREMENT`, `FINANCE`. Đây không phải xác nhận NICON có sáu
   phòng: biên bản nêu năm phòng và chưa chốt vị trí Cung ứng/Pháp lý. Tách HR
   khỏi Finance để baseline quyền không trộn hai chức năng.
2. Cung ứng là group độc lập; Kho thuộc Cung ứng nhưng dùng role/permission riêng
   để không tự duyệt yêu cầu nguồn lực hoặc thay BOQ.
3. Tạm xếp role Pháp lý vào group quyền `CONSTRUCTION` để tái sử dụng baseline
   project-scoped; điều này không xác định Pháp lý thuộc Phòng CM về tổ chức.
   Task liên phòng cần PM phân công và server kiểm tra scope.
4. Giữ một role `QS`; vị trí Site/Office và hướng Upstream/Downstream được xác
   định bằng project membership, task và permission hành động, không nhân đôi
   role trước khi có nhu cầu phân quyền khác biệt thực tế.
5. Mỗi tài khoản có một business role hoạt động và một group tại một thời điểm.
   Không cộng dồn role ngầm; nhu cầu bổ sung dùng custom role có permission được
   duyệt hoặc đổi active role có audit.
6. Seed là baseline kỹ thuật theo quyết định này; NICON vẫn có thể chỉnh custom
   role, nhưng không được làm mất separation of duties server-side.

#### Q-COM-01 — Hai hướng tạo Hợp đồng (đã xác nhận 16/10/2026)

Quyết định: cho phép (1) tạo từ Báo giá đã duyệt và (2) tạo trực tiếp không cần
Báo giá khi khách hàng đã biết và thống nhất giá trị Hợp đồng.

Các chi tiết dưới đây chỉ còn là câu hỏi mở nếu NICON muốn siết thêm ngoại lệ
hoặc bằng chứng cho hướng tạo trực tiếp:

1. Với hướng tạo trực tiếp, hệ thống có cần lưu cờ/lý do hoặc bằng chứng khách
   hàng đã biết và thống nhất giá trị hay chỉ cần giá trị Hợp đồng?
2. Gia hạn, phụ lục, hợp đồng khung và chỉ định thầu đi theo hướng trực tiếp hay
   cần quy tắc riêng? Nếu cần, ai duyệt và bằng chứng bắt buộc là gì?
3. Báo giá phải ở trạng thái nội bộ nào và có bắt buộc đã gửi/được khách chấp
   thuận trước khi tạo Hợp đồng?
4. Nếu Báo giá hết hiệu lực trong lúc thương thảo nhưng đã được khách chấp thuận,
   cần gia hạn version hay vẫn được ký?
5. Một Báo giá có thể sinh nhiều Hợp đồng Upstream không; một Hợp đồng có thể
   tổng hợp nhiều Báo giá không?

**Phương án chi tiết Q-COM-01 (ngoài hai hướng đã xác nhận):**

1. Hướng trực tiếp phải lưu `CommercialSource = DirectAgreedValue`, giá trị đã
   thống nhất và ghi chú căn cứ tối thiểu; không giả lập một Báo giá rỗng.
2. Phụ lục/gia hạn thuộc lifecycle Hợp đồng hiện hữu. Hợp đồng khung/chỉ định
   thầu có thể dùng hướng trực tiếp nhưng vẫn cần căn cứ và audit; phạm vi thương
   mại mới dùng Báo giá mới.
3. Hướng Báo giá chấp nhận `Approved`, `SentToCustomer` hoặc
   `CustomerApproved`; không bắt buộc đã gửi khách để lập bản nháp Hợp đồng.
4. `Approved`/`SentToCustomer` hết hạn phải gia hạn hoặc tạo version mới;
   `CustomerApproved` được coi là quyết định khách hàng cuối cùng và không bị
   vô hiệu chỉ vì ngày hiệu lực danh nghĩa đã qua.
5. Một Báo giá chỉ sinh tối đa một Hợp đồng live và một Hợp đồng chỉ có một Báo
   giá nguồn. Gộp nhiều Báo giá phải lập Báo giá tổng hợp/version mới trước.

### P1 — Thiết kế, hồ sơ và Pháp lý

#### Q-05 — Quy ước mã Dự án

1. Cấu trúc chính xác gồm đơn vị/năm/loại dự án/số thứ tự nào?
2. Uniqueness toàn công ty, theo năm hay theo chi nhánh?
3. Có phân biệt hoa/thường, cho phép dấu cách/ký tự đặc biệt không?
4. Mã được sửa đến trạng thái nào; ai duyệt; lý do và chứng từ gì bắt buộc?
5. Khi đổi mã, tên folder Drive, mã trên Báo giá/Hợp đồng/file đã phát hành có
   đổi hồi tố không? Alias mã cũ phải tìm kiếm được trong bao lâu?

**Phương án Q-05:**

1. Mặc định hệ thống cấp `PJ-YYYY-NNNN`; người có quyền được thay bằng quy ước
   NICON như `DB-2026-009` qua endpoint đổi mã chuyên biệt.
2. Mã duy nhất toàn công ty, so sánh sau khi chuẩn hóa uppercase.
3. Cho phép 2–40 ký tự `A-Z`, `0-9`, `-`, `_`; không dấu cách, Unicode hoặc ký
   tự đặc biệt. UI/API dùng cùng rule.
4. Chỉ role có permission `operations.projects.rename` và scope quản lý Dự án
   được đổi khi Dự án chưa `Completed/Cancelled`; bắt buộc lý do, row version
   và audit. Dự án đã đóng phải mở lại qua workflow có quyền riêng trước khi
   đổi mã; PM không mặc nhiên có quyền đổi mã.
5. Chỉ tên folder Drive hiện tại được rename bất đồng bộ trên cùng folder ID.
   Báo giá, Hợp đồng, file/snapshot đã phát hành giữ mã tại thời điểm phát hành.
   Lịch sử mã cũ được giữ vĩnh viễn và phải tìm kiếm được như alias.

**Delivery note Q-05:** Danh sách Dự án đã tìm được bằng mã cũ trong lịch sử
đổi mã và vẫn áp project scope; Dự án `Completed/Cancelled` không cho đổi mã.
Chưa có ràng buộc duy nhất xuyên suốt lịch sử mã cũ ở cấp database; cần kiểm kê
dữ liệu cũ, thiết kế registry/unique constraint và rehearsal migration trước
khi cam kết không bao giờ tái sử dụng alias.

#### Q-06 — Trình nội bộ Thiết kế

1. Người nhận nội bộ chỉ comment hay mỗi người phải Accept/Reject?
2. Nếu cần đồng ý, tất cả phải đồng ý hay theo quorum/thứ tự?
3. Comment nào là blocker; ai được resolve; có cho sửa/xóa comment không?
4. Design Manager được trình khách khi còn comment mở không?

**Phương án Q-06:**

1. Người nhận nội bộ có quyền xem và comment; không tạo nhiều approval song
   song. Một Design Manager chịu trách nhiệm tổng hợp và chuyển trạng thái.
2. Không dùng quorum. Ý kiến là input tư vấn; trách nhiệm quyết định thuộc Design
   Manager, còn duyệt task kỹ thuật thuộc đúng một Design Lead/approver.
3. Comment có mức `Normal` hoặc `Blocker`. Tác giả hoặc Design Manager được
   resolve blocker với resolution note; comment không sửa/xóa sau khi gửi, chỉ
   được bổ sung correction.
4. Không được `ReadyForClient`/trình khách khi còn blocker mở. Comment thường có
   thể được tổng hợp vào `InternalSummary` và không chặn.

#### Q-07 — CC mặc định

1. Trưởng/phó phòng Thiết kế có tự động là CC cho mọi Dự án không?
2. CC áp dụng mọi task, chỉ milestone hay chỉ hồ sơ trình nội bộ?
3. CC có được tải file/comment không hay chỉ nhận thông báo?
4. Khi nhân sự đổi chức danh, CC lịch sử giữ nguyên hay cập nhật động?

**Phương án Q-07:**

1. Design Manager và Design Lead của Dự án tự động nhận thông báo milestone;
   không tự thêm trưởng/phó phòng toàn công ty vào mọi task.
2. CC task là danh sách explicit theo task. Hồ sơ trình nội bộ có recipient riêng;
   phase gate/IFC luôn thông báo Design Manager và Design Lead.
3. CC chỉ xem, tải file nếu có project/document scope và comment khi được chọn
   làm recipient; không được approve hoặc transition.
4. Danh sách recipient/CC được snapshot theo từng submission/task revision để
   bảo toàn lịch sử. Thay đổi chức danh chỉ áp dụng lần giao/trình tiếp theo.

#### Q-08 — Xóa hồ sơ Thiết kế

1. “Cấp cao nhất” là BGD, SUPER_ADMIN hay một permission riêng do NICON gán?
2. Những trạng thái “không đạt” nào được xóa: Draft, Rejected, Discarded,
   Cancelled, ClientChangesRequested?
3. Hồ sơ đã trình khách có bao giờ được hard-delete không? Nếu có, cần approval
   hai cấp, thời gian chờ, lý do, biên bản và retention nào?
4. Nếu file đã đồng bộ Drive, xóa binary hay chỉ unlink/archive?
5. Dữ liệu audit, comment, submission receipt và revision thay thế phải giữ bao
   lâu? Có yêu cầu pháp lý cấm xóa không?

**Phương án Q-08:**

1. Dùng permission riêng `design.records.hard-delete`, mặc định chỉ `BGD` và
   `SUPER_ADMIN`; không suy từ chức danh hoặc `design.*` wildcard thông thường.
2. Chỉ cho xóa vĩnh viễn record ở trạng thái loại bỏ rõ ràng: Concept
   `Discarded`, Basic/Shop `Rejected` sau khi phase đã đi tiếp, IFC `Cancelled`.
3. Hồ sơ đã trình khách không bao giờ hard-delete. Dùng supersede/archive và
   giữ receipt, quyết định, comment, checksum; đây là legal/audit hold.
4. File quản lý bởi ứng dụng phải hoàn thành durable cleanup và đưa binary vào
   Drive trash; shared/external binding chỉ unlink/archive, không xóa file ngoài
   ownership. Delete bị block khi cleanup đang pending.
5. Audit, submission receipt, comment và revision lineage được giữ khi chưa có
   retention schedule được NICON/pháp chế phê duyệt; không tự đặt thời hạn 10 năm
   hoặc tự động purge. Legal hold luôn chặn purge. Sau khi có chính sách lưu trữ,
   tác vụ purge phải là quy trình quản trị riêng, không đi qua hard-delete nghiệp vụ.

#### Q-09 — Template giấy phép

1. Danh sách permit theo loại công trình, quy mô, địa phương và phạm vi D&B là gì?
2. Permit nào bắt buộc trước khởi công, permit nào theo milestone và permit nào
   chỉ cảnh báo?
3. Ai duy trì template; thay đổi template có áp dụng hồi tố Dự án đang chạy không?
4. Khi Hợp đồng `InProgress`, hệ thống tự tạo toàn bộ permit hay tạo bản đề xuất
   để PM xác nhận? PM được bỏ permit nào và có cần lý do/duyệt không?
5. Một permit dùng chung nhiều Hợp đồng/Dự án được không; renewal/version xử lý
   thế nào?

**Phương án Q-09:**

1. Permit template có version và điều kiện: loại công trình, scale band, tỉnh/
   thành, phạm vi Design/Construction/D&B và ngày hiệu lực.
2. Mỗi item có gate `BeforeStart`, `Milestone` hoặc `Advisory`. `BeforeStart`
   bắt buộc `Issued` và còn hiệu lực tại cổng khởi công; `Milestone` chặn đúng
   milestone; `Advisory` chỉ cảnh báo.
3. Legal quản lý draft template, PM/role được ủy quyền publish. Version mới chỉ
   áp dụng activation mới; Dự án đang chạy giữ snapshot và chỉ migrate bằng
   change request có preview.
4. Khi Hợp đồng `Signed → InProgress`, hệ thống tạo một proposal idempotent từ
   template và PM xác nhận phân công. PM được thêm permit; chỉ được bỏ item không
   bắt buộc với lý do. Item bắt buộc không được bỏ qua bằng UI/API.
5. Permit thuộc một `OperationalProject` và có thể phục vụ nhiều Hợp đồng cùng
   Dự án; không dùng chung giữa hai Dự án. Gia hạn tạo revision nối tiếp, giữ hồ
   sơ cũ và trạng thái hiệu lực theo ngày.

#### Q-10 — Ranh giới Design Lead và Design Manager

1. Design Lead duyệt mọi task kỹ thuật hay chỉ task thuộc discipline/team mình?
2. Design Manager có được override quyết định task không?
3. Ai chốt Concept, mở Basic/Shop và phát hành IFC?
4. Khi Design Lead đồng thời là Design Manager, có vi phạm phân tách nhiệm vụ
   không; có cần người duyệt thay thế?

**Phương án Q-10:**

1. Design Lead duyệt task thuộc discipline/team mình hoặc task được chỉ định;
   không có quyền duyệt toàn bộ task chỉ vì chức danh.
2. Design Manager không override một quyết định task. Họ có thể reopen/reassign
   có lý do và audit, sau đó approver độc lập duyệt lại.
3. Design Lead duyệt task kỹ thuật; Design Manager chốt Concept, mở phase tiếp
   theo, tổng hợp trình khách và đề nghị IFC. Quyền `design.ifc.release` độc lập
   thực hiện phát hành IFC.
4. Một người có thể giữ hai chức danh trên Dự án nhưng không được vừa là assignee
   vừa approver của cùng task, hoặc vừa lập vừa phát hành IFC. Phải chỉ định
   approver/releaser thay thế đang hoạt động.

#### Q-11 — Team, tiến độ và MS Project

1. Xác nhận một màn hình hay hai tab liên kết là UX cuối.
2. Import `.mpp` bắt buộc giữ những trường nào: WBS hierarchy, duration, start,
   finish, predecessors, lag, constraints, calendars, resources, milestones,
   critical path, baseline và % complete?
3. Khi re-import, merge theo UID nào; task sửa trên Nicon hay MS Project là nguồn
   ưu tiên; conflict được giải quyết ra sao?
4. Có cần export ngược `.mpp` hay chỉ import?
5. Baseline nào được coi là published và ai có quyền thay thế?

**Phương án Q-11:**

1. Dùng hai tab `Đội ngũ` và `Tiến độ` trên cùng Design Project và chung member
   IDs; không tạo hai aggregate hoặc hai danh sách nhân sự.
2. Import giữ external UID, WBS hierarchy, duration, planned start/finish,
   predecessor type/lag, constraints, calendar, resource assignment, milestone,
   critical flag, baseline và `% complete`. Trường không hỗ trợ phải báo trong
   preview, không silently drop.
3. Import vào draft bằng external UID + source file fingerprint. Re-import tạo
   change set; field sửa ở cả Nicon và file là conflict người dùng phải chọn.
   Baseline published không bị re-import ghi đè.
4. Giai đoạn đầu chỉ import MS Project XML/XER-equivalent đã chuẩn hóa; native
   `.mpp` cần adapter được cấp phép. Không export `.mpp` cho tới khi round-trip
   fidelity được chứng minh; CSV/XLSX không được gọi là MPP-compatible.
5. Design Manager publish baseline sau khi Design Lead/PM xác nhận. Thay baseline
   tạo version mới và change reason; không unlock/overwrite version cũ.

### P1 — BOQ, Thi công và Tài chính

#### Q-BOQ-01 — Chuẩn BOQ dùng chung

1. NICON phê duyệt template cột bắt buộc nào cho suất đầu tư, Quote BOQ, Tender
   Estimate, Execution BOQ và Final BOQ?
2. Sheet cover/summary/discipline nào cần import; merged cells, formula, subtotal
   và bilingual label được bảo toàn hay chuẩn hóa thế nào?
3. Mapping sai hoặc thiếu đơn vị/mã vật tư xử lý reject toàn workbook hay từng dòng?
4. Quy tắc version/approval và người có quyền promote một revision là gì?
5. Cách xử lý VO, thay thế revision và truy vết cell/sheet nguồn?

**Phương án Q-BOQ-01:**

1. Dùng năm schema/lifecycle tách biệt: Investment Rate, Quote BOQ, Tender
   Estimate, Execution BOQ và Final BOQ. Core columns của detail line là code,
   description, unit, quantity và unit price; schema chuyên biệt được version.
2. Cover/summary chỉ lưu file gốc và metadata, không tạo line hạn mức. Chỉ sheet
   detail được chọn trong preview. Giá trị formula được tính bằng engine workbook
   tại import; đồng thời lưu sheet, row, cell mapping, formula text và raw value.
   Merged/bilingual headers được mapping về canonical fields, file gốc bất biến.
3. Import atomic theo revision: lỗi mapping, thiếu unit/code hoặc số không hợp lệ
   chặn toàn workbook và trả lỗi sheet/row/cell. Warning không ảnh hưởng số liệu
   có thể được người dùng xác nhận trong preview.
4. Revision Draft được sửa/import lại; submit khóa nội dung; approver độc lập
   approve/reject. Chỉ approval action được promote; filename/sheet không quyết
   định loại hoặc hiệu lực.
5. VO đã duyệt sinh revision Execution BOQ kế tiếp có lineage; không sửa revision
   Approved. Final BOQ là revision được chốt lúc closeout. Mọi line giữ source
   workbook checksum, sheet, row/cell và predecessor revision/VO.

#### Q-CM-01 — Danh mục việc và quyền Phòng CM

1. NICON cung cấp danh sách nhóm việc CM chuẩn theo loại công trình, owner,
   approver, deadline và đầu ra bắt buộc.
2. PM CM xem toàn portfolio hay chỉ Dự án được giao?
3. Approval tầng 2 bắt buộc cho MR/QA-QC/QS/HSE/VO nào; ngưỡng giá trị hoặc mức
   rủi ro là bao nhiêu?
4. Ai thay thế reviewer khi chính PM CM là tác giả hoặc vắng mặt?

**Phương án Q-CM-01:**

1. Template CM tối thiểu gồm readiness/baseline, BCH & resource plan, HSE,
   QA/QC, MR, QS, VO, schedule recovery và closeout. Từng template version khai
   báo owner role, approver role, SLA và evidence bắt buộc theo loại công trình.
2. PM CM chỉ xem Dự án được phân công; portfolio toàn công ty cần permission
   `view.all`. BGD có read-only portfolio, không mặc nhiên review.
3. Tất cả MR vượt BOQ/unplanned, QA/QC dossier, QS batch, HSE violation mức
   Major/Critical và VO đều cần level-2 CM review. MR trong hạn mức vẫn cần CM
   decision trước handoff; Finance/BGD approval bổ sung theo giá trị/risk matrix
   cấu hình, không thay CM review.
4. Reviewer thay thế phải được delegate rõ Dự án, phạm vi và thời hạn; không là
   tác giả. Hệ thống lưu delegator, delegate, lý do, hiệu lực và action audit.

#### Q-CM-02 — Baseline, S-Curve và cảnh báo trễ

1. Variance >5% tính theo planned-vs-actual progress, earned value hay mốc thời
   gian; tính theo ngày/tuần/tháng?
2. Dùng baseline version nào khi có thay đổi được duyệt?
3. Ai publish/unlock; có cần BGD/Finance duyệt khi ảnh hưởng ngân sách?
4. Cảnh báo gửi Chỉ huy trưởng, PM CM và ai khác; SLA giải trình/recovery plan?
5. Khi dữ liệu thiếu hoặc kỳ chưa chốt, hiển thị MissingData hay cảnh báo?

**Phương án Q-CM-02:**

1. Variance là `planned cumulative physical progress - actual cumulative
   accepted progress`, theo điểm phần trăm, chốt hàng tuần theo timezone Dự án.
   Chỉ `> 5.00` điểm phần trăm tạo red alert; đúng `5.00` không đỏ.
2. So với baseline published đang có hiệu lực tại kỳ. Thay đổi được duyệt tạo
   version mới có effective date; kỳ lịch sử luôn dùng version lúc đã chốt.
3. PM CM chuẩn bị/publish; thay baseline cần change request. Nếu đổi budget/scope
   phải có VO và Finance/BGD approval tương ứng. Không unlock version cũ.
4. Alert gửi Chỉ huy trưởng, PM CM và Project Manager; Critical/không phản hồi
   escalates BGD. Giải trình trong 2 ngày làm việc, recovery plan trong 5 ngày.
5. Thiếu baseline, kỳ hoặc actual accepted data hiển thị `MissingData`, không
   sinh cảnh báo giả; đồng thời tạo data-quality notification cho owner.

#### Q-CM-03 — ISO, QA/QC, HSE và cổng khởi công

1. Checklist/form bắt buộc theo loại công trình và hạng mục là gì?
2. Site handover, method statement và HSE plan bắt buộc trong trường hợp nào?
3. Bằng chứng tối thiểu để QA/QC dossier được submit/approve?
4. Punch/HSE mức nào chặn nghiệm thu hoặc bàn giao?
5. Quy tắc offline, duplicate submit và conflict khi BCH mất mạng?

**Phương án Q-CM-03:**

1. Checklist/form là template versioned theo loại công trình/hạng mục; không
   hardcode danh mục ISO trong React. Dự án snapshot version khi bắt đầu.
2. Site handover, method statement và HSE plan chỉ chặn cổng khởi công khi
   template dự án/ISO được duyệt đánh dấu bắt buộc. `NotApplicable` cần PM CM
   duyệt lý do; không tự áp một danh mục giấy tờ cho mọi loại công trình.
3. QA/QC submit tối thiểu cần WBS, released IFC revision, checklist hoàn tất,
   người/giờ kiểm tra và ít nhất một evidence document cho mục yêu cầu evidence.
4. Punch Critical/Major chưa đóng và HSE Critical hoặc stop-work đang mở chặn
   acceptance/handover; Minor cảnh báo nhưng không tự chặn trừ template quy định.
5. Mobile tạo client UUID và idempotency key, lưu local draft/outbox. Retry cùng
   payload trả cùng kết quả; payload khác cùng key bị conflict. Row-version stale
   trả `409` và yêu cầu người dùng chọn reload/copy-as-new, không silent merge.

#### Q-FIN-01 — Hồ sơ thanh toán ba phần

1. Danh mục chứng từ bắt buộc ở phần Công trường, CM và Kế toán cho Upstream và
   từng loại Downstream package?
2. Ai lập, kiểm tra, trả lại và phê duyệt mỗi phần; có cho làm song song không?
3. Phần sau được bắt đầu khi phần trước Draft, Submitted hay Approved?
4. QS batch/milestone/BOQ/VO/invoice/receipt liên kết bắt buộc thế nào?
5. Three-way matching dùng tolerance bao nhiêu cho quantity, unit price, VAT và
   tổng tiền; ai duyệt vượt ngưỡng?
6. Hồ sơ bị trả lại tạo version mới hay sửa version hiện tại; SLA/KPI tính ra sao?

**Phương án Q-FIN-01:**

1. Mỗi dossier có ba section versioned: `Site` (QS/acceptance/evidence), `CM`
   (scope, BOQ/VO, quality/schedule confirmation) và `Accounting` (invoice,
   tax, banking, payment/receipt references). Template quy định chứng từ theo
   Upstream, Supply, Labour, Equipment và Mixed package.
2. Ba section được chuẩn bị song song. Mỗi section có maker và checker độc lập;
   Finance chỉ submit/approve dossier khi cả ba section `Approved`. BGD/authorized
   approver quyết định chi/ghi nhận theo matrix, không tự lập section.
3. Section sau có thể Draft khi phần trước chưa duyệt nhưng không được Approved
   nếu dependency bắt buộc chưa Approved.
4. Upstream bắt buộc Contract + receivable milestone + approved Upstream QS;
   Downstream bắt buộc Contract + payable milestone + vendor invoice + approved
   Downstream QS/receipt theo loại package + applicable Execution BOQ/VO.
5. Default tolerance là zero cho contract/vendor/tax/currency và tổng tiền.
   Quantity/unit-price rounding theo đơn vị tiền nhưng không auto-approve chênh
   lệch; vượt bất kỳ tolerance cấu hình nào cần Finance Manager/BGD override có
   lý do và audit. Không dùng amount sign để suy direction.
6. Draft sửa tại chỗ. Sau submit, trả lại tạo revision mới nối lineage; bản đã
   submit/approved bất biến. SLA tính riêng từ submit→review và approved→paid;
   KPI ghi theo actor/timestamp thực, không reset khi tạo revision sửa.

### P2 — Vận hành, dữ liệu và nghiệm thu

#### Q-OPS-01 — Drive và topology thư mục

1. NICON xác nhận tài khoản OAuth, root folder và người sở hữu dữ liệu.
2. Survey có chuyển vào `01_CRM_PreDesign` hay giữ `01_Khao_sat` riêng?
3. Quyền Drive được đồng bộ theo role/project team hay chỉ do Nicon kiểm soát?
4. Retention, archive, restore và xóa file khi hard-delete là gì?
5. Cutover cần backfill/rebind folder lịch sử và tiêu chí rollback nào?

**Phương án Q-OPS-01:**

1. Dùng OAuth identity do NICON sở hữu (ưu tiên Shared Drive hoặc tài khoản chức
   năng, không dùng tài khoản cá nhân); root folder và Data Protection key ring
   là cấu hình môi trường, không commit vào repository.
2. Topology đích đặt Survey dưới `01_CRM_PreDesign`. Registry hiện hữu
   `01_Khao_sat` chỉ đổi sau migration có preview; không đổi string seed rồi làm
   mất binding lịch sử.
3. Ứng dụng là nguồn quyền authoritative. Không đồng bộ ACL Drive theo từng user
   ở giai đoạn này; OAuth identity sở hữu file, người dùng tải qua API có project
   scope. Chỉ bật Drive sharing sync khi NICON IT duyệt mapping group.
4. Xóa nghiệp vụ đưa file thuộc ownership ứng dụng vào Drive trash sau durable
   cleanup; external/shared file chỉ unlink. Restore/archive theo retention của
   record nguồn; legal hold luôn thắng purge.
5. Cutover: disable worker → đợi hết lease → inventory/checksum → preview mapping
   → move/rebind theo folder ID → reconcile → sample download → enable worker.
   Rollback giữ mapping cũ và folder ID; dừng nếu count/checksum/ownership lệch.

#### Q-OPS-02 — Host/UAT và tiêu chí bàn giao

1. URL UAT, branch/commit, ngày deploy và bộ dữ liệu demo chuẩn là gì?
2. Danh sách role/user NICON dùng UAT; ai ký nghiệm thu từng module?
3. Browser/device/mobile width và điều kiện mạng yếu cần test?
4. Tiêu chí pass cho health, Drive, email, upload, retry và audit?
5. Cơ chế ghi defect, severity, SLA và retest trên phiên bản nào?

**Phương án Q-OPS-02:**

1. Mỗi đợt UAT phải công bố URL, environment, branch, commit SHA, migration ID,
   seed version và thời điểm deploy; không nghiệm thu theo nhãn “latest”.
2. UAT dùng tài khoản riêng cho mọi business role và owner/non-owner scope. Chủ
   module NICON ký scenario của module; Product Owner/BGD ký cross-module và go-live.
3. Browser tối thiểu Chrome/Edge hai phiên bản gần nhất; desktop 1366×768, tablet
   768×1024, mobile 390×844. BCH test thêm mạng chậm, offline, reconnect và retry.
4. Pass khi health/database/migration OK; không có Critical/High defect; Drive,
   email, upload/download, retry/idempotency, permission denial và audit đều có
   bằng chứng; không có secret/PII trong log.
5. Defect lưu ID, build SHA, environment, role/data, steps, expected/actual và
   evidence. Critical: xử lý ngay/chặn release; High: trước sign-off; Medium/Low
   cần owner và kế hoạch. Retest đúng commit fix rồi chạy regression liên quan.

#### Q-GOV-01 — Thẩm quyền xác nhận yêu cầu

1. Xác nhận tên/chức danh chính xác của chị Yến/Trinh trong biên bản tự động.
2. Chị Huỳnh Anh là đầu mối tổng hợp hay có thẩm quyền phê duyệt thay từng trưởng
   phòng? Nội dung nào bắt buộc anh Nguyên/BGD xác nhận?
3. Kênh nào tạo quyết định có hiệu lực: biên bản ký, email, Google Sheet hay UAT
   sign-off? Khi các kênh khác nhau thì thứ tự ưu tiên là gì?
4. Mỗi quyết định cần mã, ngày hiệu lực và người duyệt để đóng Q-01–Q-11 hay chỉ
   cần cập nhật biên bản tổng hợp?

**Phương án Q-GOV-01:**

1. Không suy đoán tên/chức danh từ transcript tự động. Dùng role nghiệp vụ trong
   tài liệu cho tới khi NICON xác nhận danh tính bằng văn bản.
2. Đầu mối BA được tổng hợp nhưng không thay thẩm quyền: trưởng phòng duyệt rule
   module; BGD/Product Owner duyệt cross-module, cơ cấu quyền, tài chính, retention
   và go-live.
3. Thứ tự hiệu lực: change decision được ký/phê duyệt có ID → biên bản họp được
   xác nhận → email xác nhận từ owner → UAT sign-off. Google Sheet/chat chỉ là
   working evidence; UAT không tự thay đổi requirement nếu không có change ID.
4. Mỗi quyết định bắt buộc có ID, owner, approver, ngày hiệu lực, phạm vi, rule,
   exception, migration/backfill, acceptance criteria và link evidence. Canonical
   docs cập nhật sau đó; không đóng câu hỏi chỉ bằng sửa biên bản tổng hợp.

## 6. Đầu vào còn cần NICON cung cấp

| Đầu vào | Hiện trạng repository | Quyết định cần có |
|---|---|---|
| Sơ đồ phòng ban/vai trò/quyền | Có seed kỹ thuật nhưng mâu thuẫn baseline | Bảng chính thức và mapping migration |
| BOQ thực tế | Có workbook mẫu trong `docs/Nicon/BOQ` | Template/mapping được phê duyệt theo từng loại BOQ |
| Tiến độ MS Project | Có file mẫu trong `docs/Nicon` | Hợp đồng import/re-import và trường bắt buộc |
| Danh sách giấy phép | Chưa có matrix được duyệt | Permit template theo loại/quy mô/địa phương |
| Nhóm việc Phòng CM | Chưa có danh mục được duyệt | WBS, owner, approver, SLA, đầu ra |
| Quy ước mã Dự án | Chưa chốt | Format, uniqueness, đổi mã và lịch sử |
| Bộ ISO BCH/QA-QC/HSE | Chưa chốt phạm vi rút gọn | Form/checklist và gate áp dụng |
| Hồ sơ thanh toán | Chưa chốt ba phần | Chứng từ, role, transition và matching |

Ngày 16/10/2026, người dùng xác nhận **chưa có môi trường UAT**; bộ biểu mẫu
ISO/QA-QC/HSE/thanh toán và ma trận giấy phép sẽ được cung cấp sau. Do đó không
thể xác nhận các luồng phụ thuộc những đầu vào này đã business-ready hoặc chạy
deployed smoke trên môi trường NICON trong đợt hiện tại.

## 7. Kế hoạch thực thi và trạng thái ngày 16/10/2026

| Thứ tự | Workstream | Trạng thái | Bằng chứng/kết quả | Bước tiếp theo |
|---:|---|---|---|---|
| 1 | Hai hướng tạo Hợp đồng Upstream | Implemented | Code đã hỗ trợ tạo trực tiếp với `QuoteId = null` và kiểm tra Báo giá đã duyệt khi có `QuoteId`; quyết định được căn chỉnh vào canonical docs tại commit `be7a4fbc` | Duy trì QA-COM-01; chỉ bổ sung cờ/lý do khi NICON trả lời câu hỏi bằng chứng của hướng trực tiếp |
| 2 | Route RBAC BCH/CM và tài khoản seed cho mọi business role | Implemented | Commit `65bea63d`; Tier-1 route-discovery chạy lại ngày 16/10: `1 passed`; unit test tập trung gồm `DbSeederTests`: `92 passed` | Chạy deployed RBAC matrix khi Docker/UAT khả dụng |
| 3 | Nhóm quyền chức năng và mapping role | Partial | Đề xuất seed sáu group tách HR khỏi Finance; unit/integration RBAC tập trung đã pass. Biên bản vẫn nêu năm phòng, nên đây không phải sơ đồ tổ chức đã duyệt | NICON xác nhận Role × Department × Permission matrix trước rollout dữ liệu thật; rehearsal migration và kiểm tra quyền runtime |
| 4 | Ownership/liên kết nhóm việc | Partial | Task Dự án nay cần xác nhận độc lập kể cả khi không gắn KPI; task nội bộ không KPI giữ đường hoàn tất trực tiếp. Q-01–Q-03 vẫn chưa được NICON phê duyệt toàn bộ | Chốt ownership, cardinality, KPI kỳ/trọng số và quyền giao việc trước migration dữ liệu thật |
| 5 | BOQ đa mẫu và provenance từng dòng | Partial | Có import/mapping nhiều sheet; chưa có schema được duyệt cho hierarchy, formula và provenance | Duyệt mapping theo bộ workbook đại diện; sau đó thiết kế import contract và regression fixtures |
| 6 | Thiết kế chi tiết, MPP, review và hard-delete | Partial | Phần lõi đã có; Q-06–Q-08, Q-10–Q-11 ảnh hưởng trực tiếp lifecycle và schema | Ghi nhận quyết định có thẩm quyền rồi triển khai theo từng lát persistence → API → UI → test |
| 7 | Tự sinh hồ sơ Pháp lý | Partial | Wizard idempotent đã có; chưa có matrix permit áp dụng theo loại/quy mô | Nhận permit matrix Q-09; sau đó tự sinh idempotent và giữ bước PM phân công |
| 8 | CM/BCH, S-Curve và ISO | Partial | Lát cắt readiness/resource/QA-QC/QS đã có; công thức, checklist và approval matrix còn mở | Nhận ISO/rules; triển khai baseline cấu trúc, cảnh báo và test SoD/duplicate/reconnect |
| 9 | Hồ sơ thanh toán ba phần | Partial | Payment Request hiện có nhưng chưa đủ dossier hai chiều và matching | Ghi nhận Q-FIN-01 trước khi thiết kế aggregate, trạng thái và tolerance |
| 10 | Drive/UAT/host | Target | Repository chỉ chứng minh capability, không chứng minh cấu hình hoặc deploy NICON | Nhận OAuth/root folder/UAT URL và tiêu chí bàn giao; chạy deployed smoke trên commit công bố |

Nguyên tắc thực thi sau mỗi quyết định được phê duyệt: cập nhật canonical docs → cập nhật
code/migration → unit/integration test đúng layer → E2E hành trình trình duyệt
cần thiết → UAT trên commit được công bố. Phương án mục 5 là đầu vào thiết kế,
không tự đóng các ô `Open decision` hoặc thay thế bằng chứng phê duyệt của NICON.

## 8. QA handoff sau khi có quyết định

| Scenario | Priority | Điều kiện để chạy | Kết quả phải chứng minh | Layer |
|---|---|---|---|---|
| QA-COM-01 | Critical | Quyết định hai hướng ngày 16/10 | API/UI tạo được Hợp đồng trực tiếp khi đã thống nhất giá trị; hướng có Báo giá từ chối Báo giá chưa duyệt/hết hạn/sai ngữ cảnh | Unit + Integration + E2E hẹp |
| QA-RBAC-01 | Critical | Q-04 và matrix quyền được duyệt | Đúng group/role baseline; không nâng quyền; HR không nhận Finance; API chặn role sai | Unit + Integration |
| QA-WP-01 | High | Q-01–Q-03 được duyệt | Package giữ/chuyển liên kết đúng; quyền giao việc đúng; rejection giữ nguyên dữ liệu | Integration |
| QA-DES-01 | High | Q-06–Q-11 được duyệt | Một approver, CC đúng scope, phase gate và deletion policy đúng mọi trạng thái | Unit + Integration + E2E hẹp |
| QA-LEGAL-01 | High | Permit matrix Q-09 có hiệu lực | Kích hoạt Hợp đồng sinh đúng permit một lần; retry không trùng; bỏ permit trái rule bị chặn | Integration |
| QA-CM-01 | Critical | ISO/gate/approval matrix được duyệt | Readiness gate, SoD CM/BCH, typed resource, QA/QC và QS direction không thể bị bypass qua API | Unit + Integration |
| QA-FIN-01 | Critical | Payment dossier/matching được duyệt | Ba phần chứng từ, Upstream/Downstream, matching/tolerance, rejection và immutable paid state đúng | Unit + Integration |
| QA-OPS-01 | High | UAT/Drive account sẵn sàng | OAuth, upload/sync/retry/conflict, folder mapping và audit chạy trên đúng commit | E2E/deployed smoke |

Mọi scenario từ chối phải kiểm tra trạng thái cơ sở dữ liệu không đổi và không
phát sinh side effect ngoài audit thất bại được phép. Dữ liệu test cần có role
đúng/sai, owner/non-owner, Dự án có nhiều Hợp đồng, revision cũ, request lặp và
concurrency token stale.

## 9. BA/QA verdict

**BA verdict: Blocked cho việc tuyên bố toàn bộ yêu cầu 03/10 là hoàn tất.**
Hai hướng tạo Hợp đồng đã được xác nhận và không còn là sai lệch. Người dùng
được NICON ủy quyền chọn hướng triển khai nhưng chưa có hiện vật nguồn cho cơ
cấu role group Q-04, một số tham số Q-01–Q-11 và biểu mẫu nghiệp vụ; môi trường
UAT cũng chưa tồn tại. Các năng lực ghi `Implemented`
có đủ bằng chứng tĩnh để đưa vào phạm vi QA tập trung, nhưng chưa phải xác nhận
UAT.

**QA verdict: Partial / Blocked cho business-ready.** Các kiểm tra tập trung chạy
ngày 16/10/2026 từ source hiện tại đều pass: Tier-1 route-discovery `1/1`; unit
`DbSeederTests|ContractServiceTests` `92/92`; integration
`ContractsControllerTests|CrmBusinessPipelineTests|ContractQuoteLinkTests`
`59/59`. Docker daemon không chạy nên backend được build/test trực tiếp trên
host với `SkipNihomeWebBuild=true`; chưa chạy E2E hai hướng Hợp đồng hoặc toàn bộ
RBAC matrix trên deployed stack. Sau khi có các quyết định còn mở, cần chạy test
tập trung cho Work Package, Design, Legal, Construction và Finance, rồi E2E
trình duyệt phù hợp. Việc cùng một người thực hiện cả đối chiếu và kiểm định là
giới hạn độc lập của review này.

Bổ sung kiểm tra trong lượt HR/RBAC hiện tại: backend build trong container
`0 warning, 0 error`; `RbacSeederTests|RoleServiceTests` `60/60`; integration
`RbacControllerTests` `30/30`. Các kiểm tra này chứng minh seed, permission
isolation và API shape, **chưa** chứng minh migration dữ liệu hiện hữu trên bản
sao NICON hoặc đúng cơ cấu phòng ban khách hàng. EF CLI chưa cài trong backend
container nên chưa tạo được migration script bằng `dotnet ef`; không áp dụng
migration trên database đang chạy chỉ để thử.

Kiểm tra Q-05 bổ sung: `OperationalProjectServiceTests` `31/31` và integration
`OperationalProjectsControllerTests.RenameCode*` `2/2`. Phạm vi đã chứng minh
tìm bằng mã cũ có scope và chặn đổi mã ở trạng thái đóng; chưa chứng minh
unique alias xuyên lịch sử hay Drive rename trên môi trường NICON.

Kiểm tra Q-02 bổ sung: `DepartmentWorkPackageServiceTests` `20/20` và
`DepartmentWorkPackagesControllerTests` `6/6` trong container; test API bao
gồm task Dự án không KPI bị chặn đóng nhóm trước xác nhận độc lập. Frontend
format/lint/build đã pass; chưa chạy E2E trình duyệt cho nút trả việc mới.
Task Dự án cũ đang ở `Completed` nhưng chưa `Verified` sẽ cần người duyệt
xác nhận trước khi đóng package sau khi triển khai. Quy tắc này là phương án
kiểm soát theo yêu cầu hiện tại của người dùng, chưa phải phê duyệt của NICON.

Kiểm tra lát QA/QC evidence type: `ConstructionOperationsServiceTests`
`14/14`, `ConstructionOperationsControllerTests` `6/6`; API round-trip giữ
`LaboratoryResult`, client ID cũ tương thích và không làm mất loại đã lưu khi
cập nhật cùng tài liệu. Frontend format/lint/build và TypeScript typecheck đạt.
Playwright construction smoke `2/2` pass, xác nhận form gửi đúng ID và loại
minh chứng, đồng thời thẻ hồ sơ hiển thị loại cho Phòng CM kiểm tra. Lần chạy
đầu bị `ECONNRESET` vì backend `dotnet watch` restart do
polling watcher trùng `/nihomeweb/vite.config.ts`; đã chạy lại khi stack ổn
định và pass. Chưa kiểm chứng Drive thực của NICON hay checklist ISO theo loại
công trình.
