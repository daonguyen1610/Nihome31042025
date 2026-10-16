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

1. Sai lệch role group vẫn chờ NICON quyết định cơ cấu phòng ban tại Q-04;
   không được tự đổi seed vì có thể làm sai quyền dữ liệu. Việc Hợp đồng
   Upstream không có Báo giá không còn là sai lệch: xác nhận ngày 16/10/2026
   cho phép hướng này khi khách hàng đã biết và thống nhất giá trị Hợp đồng.
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
| Open decision | Cơ cấu tổ chức, Q-01–Q-11, biểu mẫu ISO/CM/thanh toán, công thức S-Curve và ma trận phê duyệt |

`Implemented` trong báo cáo này chỉ có nghĩa là mã nguồn hiện có persistence,
API, UI và test phù hợp với yêu cầu được nêu. Nó không xác nhận đã deploy hoặc
đã được NICON nghiệm thu.

## 2. Cách rà soát và giới hạn bằng chứng

- Đã lập inventory toàn bộ file dưới `nihomebackend`, `nihomeweb`,
  `nihomebackend.tests`, `nihomebackend.integration.tests` và E2E smoke.
- Mỗi nhóm yêu cầu được truy vết theo chuỗi: route/UI → DTO/controller → service
  rule → persistence/migration/seed → test.
- Kiểm tra này là static review trên working tree. Theo quy định repository cho
  thay đổi chỉ liên quan tài liệu, không chạy build, unit, integration hoặc E2E.
  Tên test chứng minh có coverage trong mã, không phải bằng chứng test vừa chạy.
- Không kiểm tra host, Google Drive thật, SMTP thật hoặc dữ liệu UAT của NICON.

## 3. Ma trận yêu cầu → mã nguồn

### BR-01 — Permission, vai trò và nhóm phòng ban

**Trạng thái: Partial — có framework tốt, sai cơ cấu group baseline.**

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

- `rbac-defaults.json` seed `CRM`, `PROCUREMENT`, `CONSTRUCTION`, `DESIGN`,
  `FINANCE`; baseline yêu cầu `Kinh doanh/CRM`, `Hành chính nhân sự`, `Thiết kế`,
  `Quản lý thi công`, `Tài chính kế toán`.
- `HR` đang thuộc `FINANCE`; `PROCUREMENT` là group độc lập; `LEGAL_OFFICER`
  thuộc `CONSTRUCTION`. Đây là giả định triển khai, chưa phải quyết định NICON.

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
  xác nhận khi task liên kết KPI.
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
| High | Role group seed không khớp năm phòng baseline | Sai ownership, bộ quyền mặc định và báo cáo/KPI | NICON trả lời Q-04; lập migration/rebind có audit thay vì đổi seed đơn thuần |
| Medium | Wizard Pháp lý cần chọn thủ công | Không đạt yêu cầu tự sinh khi Hợp đồng chạy | Chốt permit matrix; tự tạo idempotent và giữ bước PM phân công |
| Medium | Quy tắc package/assignment đang đi trước quyết định | Có thể phải migrate liên kết và quyền sau khi NICON trả lời | Đóng Q-01–Q-03 trước khi mở rộng dữ liệu thật |
| Medium | Chặn xóa sau trình khách là giả định kỹ thuật | Có thể lệch thẩm quyền/lưu trữ hồ sơ NICON | Giữ chặn an toàn cho tới khi Q-08 được trả lời |

## 5. Câu hỏi cần NICON làm rõ

Mỗi câu trả lời nên ghi: người phê duyệt, ngày hiệu lực, phạm vi dự án áp dụng,
ngoại lệ, ví dụ dữ liệu và tài liệu/form mẫu kèm theo. Không nên trả lời chỉ
“đồng ý/không đồng ý” nếu câu hỏi có nhiều nhánh.

### P0 — Chặn hợp đồng nghiệp vụ và mô hình quyền

#### Q-01 — Gắn nhóm việc khi xuất hiện Hợp đồng

1. Nhóm việc tạo trước hợp đồng có giữ `OperationalProjectId` làm gốc và chỉ gắn
   thêm `ContractId`, hay phải chuyển ownership sang Hợp đồng?
2. Nếu một nhóm việc phục vụ nhiều Hợp đồng, cho phép nhiều liên kết hay phải
   tách package? Ai có quyền tách và phần trăm tiến độ được phân bổ thế nào?
3. Khi Hợp đồng bị hủy/thay thế, package được giữ, chuyển sang Hợp đồng mới hay
   đóng? Lịch sử và file có cần snapshot tại thời điểm chuyển không?
4. Báo cáo/KPI quy về Dự án hay Hợp đồng khi package đã gắn Hợp đồng?

#### Q-02 — Công việc nội bộ và KPI

1. Công việc nội bộ có cần người duyệt/Verifier bắt buộc cho mọi task không?
2. Trạng thái chuẩn là gì; ai được hủy, mở lại hoặc sửa sau hoàn thành?
3. Task nào được tính KPI; cần metric riêng hay dùng chung với task Dự án?
4. Trọng số, kỳ ghi nhận và bằng chứng tối thiểu là gì? Task trễ qua hai kỳ tính
   vào kỳ hoàn thành hay kỳ deadline?
5. Task không được duyệt hoặc bị trả lại có ảnh hưởng KPI không?

#### Q-03 — Quyền khởi tạo và giao việc liên phòng

1. PM Dự án, người phụ trách Cơ hội hay cả hai được tạo package trước hợp đồng?
2. Sau hợp đồng, Contract Owner có quyền tương đương PM không?
3. Trưởng phòng nhận có được thay assignee/approver của task không? PM gửi việc
   có được chỉ định trực tiếp nhân viên hay chỉ chọn trưởng phòng?
4. BGD/Admin có quyền ngoại lệ không; có bắt buộc ghi lý do và audit không?
5. Khi người dùng đổi phòng/nghỉ việc, task đang mở được tự động chuyển hay chặn
   cho tới khi người quản lý reassign?

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

### P1 — Thiết kế, hồ sơ và Pháp lý

#### Q-05 — Quy ước mã Dự án

1. Cấu trúc chính xác gồm đơn vị/năm/loại dự án/số thứ tự nào?
2. Uniqueness toàn công ty, theo năm hay theo chi nhánh?
3. Có phân biệt hoa/thường, cho phép dấu cách/ký tự đặc biệt không?
4. Mã được sửa đến trạng thái nào; ai duyệt; lý do và chứng từ gì bắt buộc?
5. Khi đổi mã, tên folder Drive, mã trên Báo giá/Hợp đồng/file đã phát hành có
   đổi hồi tố không? Alias mã cũ phải tìm kiếm được trong bao lâu?

#### Q-06 — Trình nội bộ Thiết kế

1. Người nhận nội bộ chỉ comment hay mỗi người phải Accept/Reject?
2. Nếu cần đồng ý, tất cả phải đồng ý hay theo quorum/thứ tự?
3. Comment nào là blocker; ai được resolve; có cho sửa/xóa comment không?
4. Design Manager được trình khách khi còn comment mở không?

#### Q-07 — CC mặc định

1. Trưởng/phó phòng Thiết kế có tự động là CC cho mọi Dự án không?
2. CC áp dụng mọi task, chỉ milestone hay chỉ hồ sơ trình nội bộ?
3. CC có được tải file/comment không hay chỉ nhận thông báo?
4. Khi nhân sự đổi chức danh, CC lịch sử giữ nguyên hay cập nhật động?

#### Q-08 — Xóa hồ sơ Thiết kế

1. “Cấp cao nhất” là BGD, SUPER_ADMIN hay một permission riêng do NICON gán?
2. Những trạng thái “không đạt” nào được xóa: Draft, Rejected, Discarded,
   Cancelled, ClientChangesRequested?
3. Hồ sơ đã trình khách có bao giờ được hard-delete không? Nếu có, cần approval
   hai cấp, thời gian chờ, lý do, biên bản và retention nào?
4. Nếu file đã đồng bộ Drive, xóa binary hay chỉ unlink/archive?
5. Dữ liệu audit, comment, submission receipt và revision thay thế phải giữ bao
   lâu? Có yêu cầu pháp lý cấm xóa không?

#### Q-09 — Template giấy phép

1. Danh sách permit theo loại công trình, quy mô, địa phương và phạm vi D&B là gì?
2. Permit nào bắt buộc trước khởi công, permit nào theo milestone và permit nào
   chỉ cảnh báo?
3. Ai duy trì template; thay đổi template có áp dụng hồi tố Dự án đang chạy không?
4. Khi Hợp đồng `InProgress`, hệ thống tự tạo toàn bộ permit hay tạo bản đề xuất
   để PM xác nhận? PM được bỏ permit nào và có cần lý do/duyệt không?
5. Một permit dùng chung nhiều Hợp đồng/Dự án được không; renewal/version xử lý
   thế nào?

#### Q-10 — Ranh giới Design Lead và Design Manager

1. Design Lead duyệt mọi task kỹ thuật hay chỉ task thuộc discipline/team mình?
2. Design Manager có được override quyết định task không?
3. Ai chốt Concept, mở Basic/Shop và phát hành IFC?
4. Khi Design Lead đồng thời là Design Manager, có vi phạm phân tách nhiệm vụ
   không; có cần người duyệt thay thế?

#### Q-11 — Team, tiến độ và MS Project

1. Xác nhận một màn hình hay hai tab liên kết là UX cuối.
2. Import `.mpp` bắt buộc giữ những trường nào: WBS hierarchy, duration, start,
   finish, predecessors, lag, constraints, calendars, resources, milestones,
   critical path, baseline và % complete?
3. Khi re-import, merge theo UID nào; task sửa trên Nicon hay MS Project là nguồn
   ưu tiên; conflict được giải quyết ra sao?
4. Có cần export ngược `.mpp` hay chỉ import?
5. Baseline nào được coi là published và ai có quyền thay thế?

### P1 — BOQ, Thi công và Tài chính

#### Q-BOQ-01 — Chuẩn BOQ dùng chung

1. NICON phê duyệt template cột bắt buộc nào cho suất đầu tư, Quote BOQ, Tender
   Estimate, Execution BOQ và Final BOQ?
2. Sheet cover/summary/discipline nào cần import; merged cells, formula, subtotal
   và bilingual label được bảo toàn hay chuẩn hóa thế nào?
3. Mapping sai hoặc thiếu đơn vị/mã vật tư xử lý reject toàn workbook hay từng dòng?
4. Quy tắc version/approval và người có quyền promote một revision là gì?
5. Cách xử lý VO, thay thế revision và truy vết cell/sheet nguồn?

#### Q-CM-01 — Danh mục việc và quyền Phòng CM

1. NICON cung cấp danh sách nhóm việc CM chuẩn theo loại công trình, owner,
   approver, deadline và đầu ra bắt buộc.
2. PM CM xem toàn portfolio hay chỉ Dự án được giao?
3. Approval tầng 2 bắt buộc cho MR/QA-QC/QS/HSE/VO nào; ngưỡng giá trị hoặc mức
   rủi ro là bao nhiêu?
4. Ai thay thế reviewer khi chính PM CM là tác giả hoặc vắng mặt?

#### Q-CM-02 — Baseline, S-Curve và cảnh báo trễ

1. Variance >5% tính theo planned-vs-actual progress, earned value hay mốc thời
   gian; tính theo ngày/tuần/tháng?
2. Dùng baseline version nào khi có thay đổi được duyệt?
3. Ai publish/unlock; có cần BGD/Finance duyệt khi ảnh hưởng ngân sách?
4. Cảnh báo gửi Chỉ huy trưởng, PM CM và ai khác; SLA giải trình/recovery plan?
5. Khi dữ liệu thiếu hoặc kỳ chưa chốt, hiển thị MissingData hay cảnh báo?

#### Q-CM-03 — ISO, QA/QC, HSE và cổng khởi công

1. Checklist/form bắt buộc theo loại công trình và hạng mục là gì?
2. Site handover, method statement và HSE plan bắt buộc trong trường hợp nào?
3. Bằng chứng tối thiểu để QA/QC dossier được submit/approve?
4. Punch/HSE mức nào chặn nghiệm thu hoặc bàn giao?
5. Quy tắc offline, duplicate submit và conflict khi BCH mất mạng?

#### Q-FIN-01 — Hồ sơ thanh toán ba phần

1. Danh mục chứng từ bắt buộc ở phần Công trường, CM và Kế toán cho Upstream và
   từng loại Downstream package?
2. Ai lập, kiểm tra, trả lại và phê duyệt mỗi phần; có cho làm song song không?
3. Phần sau được bắt đầu khi phần trước Draft, Submitted hay Approved?
4. QS batch/milestone/BOQ/VO/invoice/receipt liên kết bắt buộc thế nào?
5. Three-way matching dùng tolerance bao nhiêu cho quantity, unit price, VAT và
   tổng tiền; ai duyệt vượt ngưỡng?
6. Hồ sơ bị trả lại tạo version mới hay sửa version hiện tại; SLA/KPI tính ra sao?

### P2 — Vận hành, dữ liệu và nghiệm thu

#### Q-OPS-01 — Drive và topology thư mục

1. NICON xác nhận tài khoản OAuth, root folder và người sở hữu dữ liệu.
2. Survey có chuyển vào `01_CRM_PreDesign` hay giữ `01_Khao_sat` riêng?
3. Quyền Drive được đồng bộ theo role/project team hay chỉ do Nicon kiểm soát?
4. Retention, archive, restore và xóa file khi hard-delete là gì?
5. Cutover cần backfill/rebind folder lịch sử và tiêu chí rollback nào?

#### Q-OPS-02 — Host/UAT và tiêu chí bàn giao

1. URL UAT, branch/commit, ngày deploy và bộ dữ liệu demo chuẩn là gì?
2. Danh sách role/user NICON dùng UAT; ai ký nghiệm thu từng module?
3. Browser/device/mobile width và điều kiện mạng yếu cần test?
4. Tiêu chí pass cho health, Drive, email, upload, retry và audit?
5. Cơ chế ghi defect, severity, SLA và retest trên phiên bản nào?

#### Q-GOV-01 — Thẩm quyền xác nhận yêu cầu

1. Xác nhận tên/chức danh chính xác của chị Yến/Trinh trong biên bản tự động.
2. Chị Huỳnh Anh là đầu mối tổng hợp hay có thẩm quyền phê duyệt thay từng trưởng
   phòng? Nội dung nào bắt buộc anh Nguyên/BGD xác nhận?
3. Kênh nào tạo quyết định có hiệu lực: biên bản ký, email, Google Sheet hay UAT
   sign-off? Khi các kênh khác nhau thì thứ tự ưu tiên là gì?
4. Mỗi quyết định cần mã, ngày hiệu lực và người duyệt để đóng Q-01–Q-11 hay chỉ
   cần cập nhật biên bản tổng hợp?

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

## 7. Trình tự đề xuất sau khi NICON trả lời

1. Chốt Q-COM-01 và Q-04 trước vì đang có hành vi code mâu thuẫn baseline.
2. Chốt Q-01–Q-03 trước khi migrate hoặc mở rộng nhóm công việc với dữ liệu thật.
3. Chốt Q-06–Q-11 trước khi hoàn tất review, hard-delete, Permit và import MPP.
4. Chốt BOQ/ISO/CM/Finance template trước khi mở rộng Module 4–6.
5. Sau mỗi nhóm quyết định: cập nhật canonical docs → cập nhật code/migration →
   unit/integration test đúng layer → E2E hành trình trình duyệt cần thiết → UAT
   trên commit được công bố.

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
Lý do là hai sai lệch High và các quyết định P0 chưa có chủ sở hữu xác nhận.
Các năng lực ghi `Implemented` có đủ bằng chứng tĩnh để đưa vào phạm vi QA tập
trung, nhưng chưa phải xác nhận UAT.

**QA verdict: Not Run / Blocked cho business-ready.** Đây là lượt căn chỉnh tài
liệu nên không chạy suite. Sau khi tài liệu được phê duyệt và code được sửa theo
các quyết định, cần chạy test tập trung cho RBAC, CRM Contract/Quote, Work Package,
Design, Legal, Construction và Finance; sau đó mới chọn các E2E trình duyệt phù
hợp. Việc cùng một người thực hiện cả đối chiếu và kiểm định là giới hạn độc lập
của review này.
