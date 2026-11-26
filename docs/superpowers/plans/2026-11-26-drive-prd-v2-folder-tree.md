# Cây thư mục Drive theo PRD v2 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mọi dự án dùng đúng cây 8 thư mục của PRD v2 mục 7, kể cả dự án đã có thư mục cũ; tài liệu bàn giao vào `08_BanGiao_BaoHanh`; Admin được báo khi đồng bộ hỏng.

**Architecture:** Thêm loại tài liệu `Handover` để bàn giao/hoàn công có thư mục riêng. Thêm một bộ chuyển đổi chạy một lần (`DriveTreeMigrationService`): xem trước (dry-run), chuyển (đổi liên kết thư mục + di chuyển tệp trên Drive bằng `MoveAsync`, ghi từng bước vào bảng nhật ký), và hoàn tác từ nhật ký. Không tải lại tệp, vì tệp tải tay đã bị xoá bản lưu trên máy chủ sau khi đồng bộ (`PurgeLocalReplicaAsync`). Thông báo Admin dùng `INotificationService.CreateForAdminsAsync` khi một tệp đồng bộ thất bại hẳn.

**Tech Stack:** ASP.NET Core 8, EF Core 8 (SQL Server; test dùng InMemory/SQLite), Google Drive API v3 qua `IGoogleDriveAdapter`, React 18 + TypeScript, xUnit + Moq, Playwright.

**Spec:** `docs/Nicon/MoMs/2026-11-19/PRD v2 - NICON Management System.docx` mục 7; `docs/Nicon/MoMs/2026-11-19/Góp ý lập trình v2 - NICON Management System.docx` mục N-01, N-12; `AGENTS.md` (quy tắc topology Drive).

## Global Constraints

- Cây đích (copy nguyên văn PRD v2 §7): `01_CoHoi_BaoGia` (con `01_KhaoSat`), `02_HopDong`, `03_ThietKe/{01_Concept,02_CoSo,03_ChiTiet_IFC,04_ShopDrawing}`, `04_PhapLy`, `05_ThiCong/{01_ChuanBi,02_NhatKy_BaoCao,03_ChatLuong_NghiemThu,04_ATLD_HSE,05_QS_KhoiLuong}`, `06_CungUng`, `07_TaiChinh`, `08_BanGiao_BaoHanh`. Danh sách lá đã có ở `GoogleDriveFolderOptions.StandardTree`; không định nghĩa lại.
- AGENTS.md: giữ liên kết thư mục hiện tại cho tới khi có bước chuyển đổi **đã duyệt, hoàn tác được, chuyển và kiểm lại tệp cũ**. Không xoá gì trên Drive.
- Chuyển đổi chỉ chạy khi đồng bộ đang **tắt** (`Enabled = false`) và không tệp nào ở trạng thái `Processing` — worker bỏ qua mọi việc khi `Enabled = false`.
- Mọi nhãn mới có đủ vi, en, zh, ja (AGENTS.md).
- Quyền: dùng `system.settings` `view`/`manage` như các API Drive hiện có.
- Trước khi push: `dotnet build -c Release -p:SkipNihomeWebBuild=true -p:TreatWarningsAsErrors=true`, `dotnet format --no-restore --verify-no-changes`, `dotnet test --no-build -c Release`; frontend `npm run format:check`, `npm run lint`, `npm run test:unit`.
- Nghiệm thu bằng tài khoản không phải Super Admin khi có thể (Góp ý v2 mục 1); live test trên Chrome hiển thị cho người dùng xem.

## Review Focus

1. **Chạy dở thì dừng** (mất mạng, Drive trả lỗi 403/5xx giữa chừng) — chạy lại phải tiếp tục đúng chỗ, không di chuyển hai lần, không tạo thư mục trùng. Test ở Task 3.
2. **Tệp đã bị xoá hoặc chuyển tay trên Drive** (`DriveFileId` không còn) — bỏ qua tệp đó, ghi lý do vào nhật ký, không dừng cả lượt. Test ở Task 3.
3. **Tệp lạ trong thư mục cũ** (người dùng tự kéo vào Drive, không có `ProjectDocument`) — vẫn phải chuyển theo thư mục, nếu không sẽ "mất" khỏi cây mới. Test ở Task 3.
4. **Hoàn tác sau khi đã bật lại đồng bộ và có tệp mới** — tệp mới không có trong nhật ký; hoàn tác phải từ chối khi đồng bộ đang bật, và chỉ chuyển lại tệp có trong nhật ký. Test ở Task 4.
5. **Đồng bộ lỗi lặp lại** — một tệp thất bại nhiều lần không được sinh hàng chục thông báo cho Admin. Test ở Task 6.

---

## File Structure

| File | Trách nhiệm |
|---|---|
| `nihomebackend/Models/ProjectDocument.cs` | Thêm `ProjectDocumentCategory.Handover = 10` |
| `nihomebackend/Models/GoogleDriveCredential.cs` | Thêm cột `HandoverFolder` |
| `nihomebackend/Services/GoogleDrive/GoogleDriveOptions.cs` | `GoogleDriveFolderOptions.Handover`, `For`, `ExtraTreePaths` |
| `nihomebackend/Services/GoogleDrive/GoogleDriveSettingsStore.cs` | Đọc/ghi `HandoverFolder`; `FolderPathsEqual` |
| `nihomebackend/Models/DriveTreeMigration.cs` (mới) | Entity `DriveTreeMigration`, `DriveTreeMigrationItem`, enum |
| `nihomebackend/Services/GoogleDrive/DriveTreeMigrationService.cs` (mới) | Xem trước, chuyển, hoàn tác |
| `nihomebackend/Controllers/GoogleDriveOAuthController.cs` | 3 endpoint `tree-migration` |
| `nihomebackend/Services/HandoverRecordService.cs`, `AsBuiltDocumentService.cs`, `DesignProjectService.cs` | Dùng `Handover` thay `ConstructionAcceptance` cho bàn giao, hoàn công |
| `nihomebackend/Services/ProjectDocumentService.cs` | Quyền xem loại `Handover` |
| `nihomebackend/Services/GoogleDrive/ProjectDriveSyncService.cs` | Báo Admin khi lỗi hẳn |
| `nihomeweb/src/pages/admin/settings/GoogleDriveConfigurationForm.tsx`, `nihomeweb/src/pages/admin/settings/DriveTreeMigrationPanel.tsx` (mới), `nihomeweb/src/services/admin/...` | Ô thư mục bàn giao; khung chuyển đổi |
| `nihomebackend/Data/Seeds/i18n/*.json` | Nhãn mới 4 ngôn ngữ |
| `README.md`, `docs/user_guide.md` | Mô tả cây mới, cách chuyển đổi |

---

### Task 1: Loại tài liệu Bàn giao → `08_BanGiao_BaoHanh`

PRD v2 §7: `08_BanGiao_BaoHanh` chứa hồ sơ hoàn công, sổ tay vận hành, khiếu nại. Hiện `HandoverRecord` và `AsBuiltDocument` dùng `ConstructionAcceptance` (→ `05_ThiCong/03_ChatLuong_NghiemThu`). Biên bản nghiệm thu (`AcceptanceRecord`) giữ nguyên ở 05/03.

**Files:**
- Modify: `nihomebackend/Models/ProjectDocument.cs:70-82`, `nihomebackend/Models/GoogleDriveCredential.cs:15-23`, `nihomebackend/Services/GoogleDrive/GoogleDriveOptions.cs:27-50`, `nihomebackend/Services/GoogleDrive/GoogleDriveSettingsStore.cs` (map + `FolderPathsEqual` + validate), `nihomebackend/Services/HandoverRecordService.cs:306`, `nihomebackend/Services/AsBuiltDocumentService.cs:290`, `nihomebackend/Services/DesignProjectService.cs:430,439`, `nihomebackend/Services/ProjectDocumentService.cs:~466` (category theo quyền), DTO cấu hình Drive và `nihomeweb/src/services/admin/types/content.ts:484`, `nihomeweb/src/pages/admin/settings/GoogleDriveConfigurationForm.tsx:33`, `nihomebackend/Data/Seeds/i18n/` (nhãn ô "Bàn giao / Bảo hành")
- Create: migration `AddHandoverDocumentCategory` (cột `HandoverFolder nvarchar NOT NULL DEFAULT N'08_BanGiao_BaoHanh'`; cập nhật `project_documents.Category` của tài liệu `SourceEntityType IN ('HandoverRecord','AsBuiltDocument')` sang 10, `Down` trả về 6)
- Test: `nihomebackend.tests/Services/GoogleDriveOptionsTests.cs`, `nihomebackend.tests/Data/AddHandoverDocumentCategoryMigrationTests.cs` (mới, SQLite theo mẫu `AddUserDepartmentMigrationTests`)

**Interfaces:**
- Produces: `ProjectDocumentCategory.Handover = 10`; `GoogleDriveFolderOptions.Handover` (mặc định `"08_BanGiao_BaoHanh"`); `For(Handover)` trả đường dẫn đó.

- [ ] **Step 1: Viết test lỗi** — trong `GoogleDriveOptionsTests`:
  - `Handover_category_uses_the_prd_v2_handover_folder`: `new GoogleDriveFolderOptions().For(ProjectDocumentCategory.Handover) == "08_BanGiao_BaoHanh"`.
  - `Default_options_have_no_extra_folder_that_a_category_now_owns`: `new GoogleDriveFolderOptions().ExtraTreePaths()` không chứa `"08_BanGiao_BaoHanh"`, vẫn chứa `"07_TaiChinh"`.
  - Migration test: tài liệu `HandoverRecord` → 10, `AsBuiltDocument` → 10, `AcceptanceRecord` giữ 6; `Down` trả 10 → 6.
- [ ] **Step 2:** `dotnet test nihomebackend.tests --filter "FullyQualifiedName~GoogleDriveOptions|FullyQualifiedName~AddHandoverDocumentCategory"` → FAIL (chưa có `Handover`).
- [ ] **Step 3:** Thêm enum, cột, option, map trong settings store, đổi 4 chỗ dùng category cho bàn giao/hoàn công, quyền xem `Handover` giống `ConstructionAcceptance`, ô cấu hình trên form, nhãn 4 ngôn ngữ, sinh migration bằng `dotnet ef migrations add AddHandoverDocumentCategory` rồi thêm câu SQL cập nhật category.
- [ ] **Step 4:** Chạy lại filter → PASS; chạy toàn bộ `nihomebackend.tests` và `nihomebackend.integration.tests` → PASS.
- [ ] **Step 5:** Commit `Store handover and as-built files in 08_BanGiao_BaoHanh`.

---

### Task 2: Xem trước chuyển đổi (dry-run)

**Files:**
- Create: `nihomebackend/Models/DriveTreeMigration.cs`, `nihomebackend/Services/GoogleDrive/DriveTreeMigrationService.cs`, migration `AddDriveTreeMigrations`
- Modify: `nihomebackend/Data/AppDbContext.cs` (2 DbSet, bảng `drive_tree_migrations`, `drive_tree_migration_items`), `nihomebackend/Extensions/ServiceCollectionExtensions.cs` (đăng ký scoped)
- Test: `nihomebackend.tests/Services/DriveTreeMigrationServiceTests.cs` (mới; dựng giống `ProjectDriveSyncProcessorTests`: `Mock<IGoogleDriveAdapter>`, `TestGoogleDriveSettingsStore`, `DbContextFactory.CreateRelational()`)

**Interfaces:**
- Produces:
  - `public enum DriveTreeMigrationStatus { Running, Completed, Failed, RolledBack }`
  - `public enum DriveTreeMigrationItemKind { Binding, Document, UntrackedFile }`
  - `DriveTreeMigration { int Id; DriveTreeMigrationStatus Status; string OldFoldersJson; string NewFoldersJson; DateTime StartedAt; int StartedByUserId; DateTime? FinishedAt; string? Error; List<DriveTreeMigrationItem> Items }`
  - `DriveTreeMigrationItem { long Id; int MigrationId; int OperationalProjectId; ProjectDocumentCategory Category; DriveTreeMigrationItemKind Kind; long? ProjectDocumentId; string? DriveFileId; string FromFolderId; string? ToFolderId; DateTime? MovedAt; string? SkipReason }` — chỉ mục duy nhất `(MigrationId, Kind, OperationalProjectId, Category, DriveFileId)`.
  - `Task<DriveTreeMigrationPreview> PreviewAsync(CancellationToken ct)` trả `DriveTreeMigrationPreview(IReadOnlyList<CategoryChange> Changes, int ProjectCount, int DocumentCount, int UntrackedFileCount, IReadOnlyList<string> Blockers)` với `CategoryChange(ProjectDocumentCategory Category, string From, string To)`.
  - Blocker codes (chuỗi, frontend dịch): `DRIVE_SYNC_ENABLED`, `DRIVE_DOCUMENTS_PROCESSING`, `DRIVE_NOT_CONNECTED`, `DRIVE_TREE_ALREADY_STANDARD`, `DRIVE_TREE_MIGRATION_RUNNING`.

- [ ] **Step 1: Viết test lỗi** (`DriveTreeMigrationServiceTests`):
  - `Preview_lists_each_category_whose_path_differs_from_prd_v2`: settings dùng 9 đường dẫn cũ đang lưu trên dev (`01_Khao_sat`, `01_CRM_PreDesign`, `02_Thiet_ke/01_So_bo_Concept`, `02_Thiet_ke/02_Co_so`, `02_Thiet_ke/03_Chi_tiet_ShopDrawing`, `03_Xin_phep_Phap_ly`, `04_Thi_cong_Nghiem_thu`, `05_Cung_ung_Vat_tu`, `06_Tai_chinh_Hop_dong`; khai báo lại trong test vì mảng trong migration là `private`) → `Changes` có 9 dòng, ví dụ `(Survey, "01_Khao_sat", "01_CoHoi_BaoGia/01_KhaoSat")`, `(FinanceContracts, "06_Tai_chinh_Hop_dong", "02_HopDong")`.
  - `Preview_counts_projects_documents_and_untracked_files`: 2 dự án có liên kết, 3 tài liệu có `DriveFileId` (1 tài liệu `DeletedAt != null` không tính), `ListChildrenAsync` của một thư mục cũ trả thêm 1 tệp không thuộc `ProjectDocument` → `ProjectCount 2, DocumentCount 3, UntrackedFileCount 1`.
  - `Preview_blocks_while_sync_is_enabled_or_documents_are_processing`: `Enabled = true` → có `DRIVE_SYNC_ENABLED`; một tài liệu `Processing` → có `DRIVE_DOCUMENTS_PROCESSING`.
  - `Preview_reports_already_standard`: settings đã là mặc định → `Changes` rỗng, có `DRIVE_TREE_ALREADY_STANDARD`.
- [ ] **Step 2:** Chạy `--filter FullyQualifiedName~DriveTreeMigrationService` → FAIL (chưa có service).
- [ ] **Step 3:** Thêm entity, bảng, migration `AddDriveTreeMigrations`, `PreviewAsync`. Đích lấy từ `new GoogleDriveFolderOptions()` (mặc định PRD v2); nguồn lấy từ `settingsStore.GetRuntimeAsync().Folders`. Đếm tệp lạ bằng `drive.ListChildrenAsync(binding.DriveFolderId)` trừ các `DriveFileId` đã có trong `project_documents`; chỉ gọi Drive khi không có blocker khác `DRIVE_*` về cấu hình.
- [ ] **Step 4:** Chạy filter → PASS.
- [ ] **Step 5:** Commit `Preview the move to the PRD v2 Drive tree`.

---

### Task 3: Chạy chuyển đổi (đổi liên kết + di chuyển tệp, chạy tiếp được)

**Files:**
- Modify: `nihomebackend/Services/GoogleDrive/DriveTreeMigrationService.cs`
- Test: `nihomebackend.tests/Services/DriveTreeMigrationServiceTests.cs`

**Interfaces:**
- Consumes: Task 2 entities; `IProjectDriveFolderService.EnsureTreeAsync(OperationalProject, int?, CancellationToken)`; `IGoogleDriveAdapter.MoveAsync(string fileId, string destinationFolderId, CancellationToken)` (bỏ qua nếu đã đúng thư mục); `GetMetadataAsync(string, CancellationToken)`.
- Produces: `Task<DriveTreeMigrationResult> RunAsync(int userId, CancellationToken ct)` → `DriveTreeMigrationResult(int MigrationId, DriveTreeMigrationStatus Status, int Moved, int Skipped, string? Error)`. Ném `DriveTreeMigrationBlockedException(string code)` khi `PreviewAsync` có blocker (trừ `DRIVE_TREE_MIGRATION_RUNNING` khi chạy tiếp lượt đang `Running`).

Thứ tự trong `RunAsync` (đây là quyết định, không phải gợi ý):
1. Nếu có lượt `Running` → chạy tiếp lượt đó; ngược lại tạo lượt mới, lưu `OldFoldersJson`/`NewFoldersJson`, ghi mục `Binding` cho mọi liên kết cũ (`FromFolderId` = thư mục cũ), rồi **trong cùng transaction** ghi đường dẫn mới vào `google_drive_credentials` và xoá `project_drive_folders` (không đi qua `GoogleDriveSettingsStore.UpdateAsync`, vì hàm đó chỉ xoá liên kết).
2. Với từng dự án có mục `Binding`: `EnsureTreeAsync` tạo cây mới; cập nhật `ToFolderId` của mục `Binding`.
3. Với từng tài liệu còn hiệu lực có `DriveFileId` của dự án: đích = liên kết mới theo `Category`; nếu `DriveFolderId` khác đích → ghi mục `Document` (chưa `MovedAt`), `MoveAsync`, đặt `MovedAt`, cập nhật `ProjectDocument.DriveFolderId`, `SaveChanges` từng tệp.
4. Tệp lạ trong mỗi thư mục cũ (`ListChildrenAsync`, trừ tệp đã có `ProjectDocument`) → mục `UntrackedFile`, chuyển vào liên kết mới cùng loại.
5. `GetMetadataAsync` trả `null` (tệp không còn) → đặt `SkipReason = "DRIVE_FILE_MISSING"`, tiếp tục.
6. Lỗi Drive khác → `Status = Failed`, lưu `Error`, dừng; chạy lại sẽ bỏ qua mục đã có `MovedAt`.
7. Xong → `Completed`. Không tự bật lại đồng bộ.

- [ ] **Step 1: Viết test lỗi:**
  - `Run_rebinds_every_project_and_moves_each_file_into_its_prd_v2_folder`: 1 dự án, 2 tài liệu (Survey, FinanceContracts) ở thư mục cũ; mock `EnsureTreeAsync` tạo liên kết mới `new-survey`, `new-contract` → `MoveAsync("file-survey","new-survey")`, `MoveAsync("file-contract","new-contract")` mỗi cái đúng 1 lần; `ProjectDocument.DriveFolderId` cập nhật; settings bằng mặc định; `Status Completed`, `Moved 2`.
  - `Run_moves_untracked_files_from_old_folders`: 1 tệp lạ `"stray"` trong thư mục cũ của Survey → `MoveAsync("stray","new-survey")`, có mục `UntrackedFile`.
  - `Run_skips_a_file_deleted_on_drive_and_continues`: `GetMetadataAsync("gone")` trả `null` → không gọi `MoveAsync("gone",…)`, mục có `SkipReason "DRIVE_FILE_MISSING"`, các tệp khác vẫn được chuyển, `Status Completed`, `Skipped 1`.
  - `Run_resumes_after_a_drive_failure_without_moving_twice`: lần 1 `MoveAsync` tệp thứ 2 ném `GoogleApiException` → `Failed`; lần 2 `RunAsync` → tệp 1 không bị gọi lại, tệp 2 được chuyển, `Completed`.
  - `Run_refuses_while_sync_is_enabled`: `Enabled = true` → `DriveTreeMigrationBlockedException("DRIVE_SYNC_ENABLED")`, không gọi Drive, settings không đổi.
- [ ] **Step 2:** Chạy filter → FAIL.
- [ ] **Step 3:** Cài `RunAsync` theo thứ tự trên.
- [ ] **Step 4:** Chạy filter → PASS; chạy toàn bộ suite backend → PASS.
- [ ] **Step 5:** Commit `Move existing projects to the PRD v2 Drive tree`.

---

### Task 4: Hoàn tác

**Files:**
- Modify: `nihomebackend/Services/GoogleDrive/DriveTreeMigrationService.cs`
- Test: `nihomebackend.tests/Services/DriveTreeMigrationServiceTests.cs`

**Interfaces:**
- Produces: `Task<DriveTreeMigrationResult> UndoAsync(int migrationId, int userId, CancellationToken ct)`. Blocker thêm: `DRIVE_TREE_MIGRATION_NOT_COMPLETED` (chỉ hoàn tác lượt `Completed` hoặc `Failed` mới nhất).

Quyết định: chuyển ngược mọi mục `Document`/`UntrackedFile` có `MovedAt` về `FromFolderId`, khôi phục `ProjectDocument.DriveFolderId`, khôi phục đường dẫn từ `OldFoldersJson`, xoá liên kết hiện tại và tạo lại liên kết từ mục `Binding` (`FromFolderId`). Thư mục cây mới để nguyên trên Drive. `Status = RolledBack`.

- [ ] **Step 1: Viết test lỗi:**
  - `Undo_moves_files_back_and_restores_old_paths_and_bindings`: sau `RunAsync` thành công, `UndoAsync` → `MoveAsync("file-survey","old-survey")`; settings về 9 đường dẫn cũ; `project_drive_folders` trỏ lại thư mục cũ; `RolledBack`.
  - `Undo_refuses_while_sync_is_enabled`: bật `Enabled` sau khi chạy → `DRIVE_SYNC_ENABLED`, không gọi Drive.
  - `Undo_leaves_files_that_were_not_moved_by_the_migration`: tài liệu tạo sau lượt chuyển (không có mục nhật ký) → không bị gọi `MoveAsync`.
- [ ] **Step 2:** Chạy filter → FAIL.
- [ ] **Step 3:** Cài `UndoAsync`.
- [ ] **Step 4:** Chạy filter → PASS.
- [ ] **Step 5:** Commit `Undo the Drive tree move from its log`.

---

### Task 5: API và khung chuyển đổi trong Cài đặt Drive

**Files:**
- Modify: `nihomebackend/Controllers/GoogleDriveOAuthController.cs`
- Create: `nihomeweb/src/pages/admin/settings/DriveTreeMigrationPanel.tsx`; hàm gọi API trong service Drive hiện có của `nihomeweb/src/services/admin/`
- Modify: `nihomeweb/src/pages/admin/settings/GoogleDriveConfigurationForm.tsx` (đặt khung dưới phần "Cấu trúc thư mục nghiệp vụ"), `nihomebackend/Data/Seeds/i18n/` (nhãn, blocker)
- Test: `nihomebackend.integration.tests/Controllers/SiteSettingsControllerTests.cs` hoặc file mới `GoogleDriveTreeMigrationControllerTests.cs`; `nihomeweb/e2e/smoke/admin-drive-tree-migration.spec.ts` (mới)

**Interfaces:**
- Consumes: Task 2–4.
- Produces: `GET api/site-settings/google-drive/tree-migration/preview` (`system.settings` view) → `DriveTreeMigrationPreview`; `POST api/site-settings/google-drive/tree-migration` (manage) → `DriveTreeMigrationResult`, 409 `{ code }` khi bị chặn; `POST api/site-settings/google-drive/tree-migration/{id:int}/undo` (manage). Ghi audit `drive.tree-migration.run` / `.undo`.

UI: bảng "Đường dẫn hiện tại → đường dẫn PRD v2" theo từng loại; số dự án, tệp, tệp lạ; blocker dịch sang lời dễ hiểu (ví dụ `DRIVE_SYNC_ENABLED` → "Tắt đồng bộ trước khi chuyển"); nút "Chuyển sang cây thư mục PRD v2" mở hộp xác nhận ghi rõ số tệp sẽ di chuyển; sau khi chạy hiện kết quả và nút "Hoàn tác". Theo DESIGN.md, dùng thành phần dùng chung (`Button`, `useConfirm`).

- [ ] **Step 1: Viết test lỗi (integration):**
  - `Preview_requires_settings_view` (Sale → 403; Admin → 200).
  - `Run_returns_409_with_code_while_sync_is_enabled` → `code == "DRIVE_SYNC_ENABLED"`.
  - `Run_and_undo_require_settings_manage` (Admin không có `manage` → 403 theo `ProtectedEndpointInventory`).
- [ ] **Step 2:** Chạy `dotnet test nihomebackend.integration.tests --filter FullyQualifiedName~TreeMigration` → FAIL.
- [ ] **Step 3:** Thêm endpoint, cập nhật `ProtectedEndpointInventory`, làm khung UI, nhãn 4 ngôn ngữ.
- [ ] **Step 4:** Chạy integration filter → PASS. Viết E2E `admin-drive-tree-migration.spec.ts`: Super Admin mở Cài đặt → Google Drive, thấy bảng xem trước và nút bị khoá kèm lời "Tắt đồng bộ trước khi chuyển" khi đồng bộ bật. (Không chạy chuyển thật trên E2E vì cần Drive thật.) Chạy `npm run test:e2e -- admin-drive-tree-migration` trên Docker Compose → PASS.
- [ ] **Step 5:** Commit `Let administrators preview, run and undo the Drive tree move`.

---

### Task 6: Báo Admin khi đồng bộ hỏng (N-01)

**Files:**
- Modify: `nihomebackend/Services/GoogleDrive/ProjectDriveSyncService.cs:693-727` (`RecordFailureAsync`), constructor nhận `INotificationService`
- Test: `nihomebackend.tests/Services/ProjectDriveSyncProcessorTests.cs`

**Interfaces:**
- Consumes: `INotificationService.CreateForAdminsAsync(string module, string title, string? body, string? linkUrl)`.

Quyết định: chỉ báo khi lỗi **hẳn** (`terminal == true`), một lần cho mỗi tài liệu ở mỗi `Generation` (lỗi hẳn chỉ xảy ra một lần mỗi generation vì trạng thái chuyển `Failed`). Tiêu đề: `"Đồng bộ Google Drive thất bại: {OriginalFileName}"`; thân: `"{ProjectCode} · {safeError}"`; link `/admin/operational-projects/{projectId}`; module `"GoogleDrive"`. Lỗi gửi thông báo không làm hỏng việc ghi trạng thái (bắt và ghi log như các dịch vụ khác).

- [ ] **Step 1: Viết test lỗi:**
  - `Terminal_sync_failure_notifies_administrators_once`: tài liệu lỗi đến lần cuối → `CreateForAdminsAsync("GoogleDrive", It.Is<string>(t => t.Contains(fileName)), …)` gọi đúng 1 lần.
  - `Retryable_sync_failure_does_not_notify`: lỗi lần 1 (còn thử lại) → không gọi.
- [ ] **Step 2:** Chạy `--filter FullyQualifiedName~ProjectDriveSyncProcessor` → FAIL.
- [ ] **Step 3:** Cài như trên; cập nhật mọi chỗ khởi tạo `ProjectDriveSyncProcessor` trong test.
- [ ] **Step 4:** Chạy filter → PASS; toàn bộ suite → PASS.
- [ ] **Step 5:** Commit `Tell administrators when a Drive sync fails for good`.

---

### Task 7: Tài liệu, live test, cập nhật sheet

**Files:**
- Modify: `README.md` (đoạn Drive ~dòng 140–150: cây mới, `08`, cách chuyển đổi), `docs/user_guide.md` (Cài đặt Drive: chuyển đổi, hoàn tác, thông báo lỗi)

- [ ] **Step 1:** Cập nhật hai tài liệu.
- [ ] **Step 2: Live test trên Chrome hiển thị** (Docker dev, Drive thử của dự án):
  1. Tắt đồng bộ → mở khung chuyển đổi → xem trước khớp số tệp thật trong DB.
  2. Chạy chuyển → mở Google Drive, thấy dự án có đủ 8 thư mục + thư mục con, tệp cũ nằm đúng thư mục mới, thư mục cũ trống (không bị xoá).
  3. Bật đồng bộ → tải một biên bản bàn giao → tệp vào `08_BanGiao_BaoHanh`.
  4. Tắt đồng bộ → Hoàn tác trên một bản sao dữ liệu → tệp về thư mục cũ.
  5. Giả lập lỗi đồng bộ (thu hồi quyền thư mục) → Admin nhận thông báo đúng một lần.
  Chụp ảnh từng bước làm evidence.
- [ ] **Step 3:** Chạy 3 bước CI backend + kiểm tra frontend ở Global Constraints → sạch.
- [ ] **Step 4:** Commit `Document the PRD v2 Drive tree move`; mở PR.
- [ ] **Step 5:** Sau khi merge và chạy chuyển đổi trên môi trường thật: cập nhật sheet dòng 16 (cột F lời cho khách, không số PR; cột G chỉ tick khi chuyển đổi đã chạy trên môi trường thật và live test đạt).

---

## Ngoài phạm vi (kế hoạch riêng)

- **Quyền Drive theo đội dự án (RACI)** — N-12 "thành viên rời đội dự án mất quyền xem", `07_TaiChinh` chỉ Kế toán, PM, BGĐ. Cần Shared Drive và email công ty của mọi người (N-04 đăng nhập Google Workspace); làm sau khi có hai điều kiện đó.
- **Đề nghị thanh toán, hoá đơn, quyết toán vào `07_TaiChinh`** — tệp đính kèm tài chính hiện lưu riêng (`FinanceService` `FilePath`), chưa thành `ProjectDocument`; cần kế hoạch nối vào đồng bộ Drive.
- **Biên bản đã khoá tự xuất PDF vào đúng thư mục** — thuộc M-15 (xuất biểu mẫu).
- **Thư mục chung ngoài dự án** (Quy trình & Biểu mẫu, Hồ sơ năng lực, Nhân sự, Truyền thông).
- **Thư mục con thi công còn trống** (`01_ChuanBi`, `02_NhatKy_BaoCao`, `04_ATLD_HSE`, `05_QS_KhoiLuong`) và `03_ChiTiet_IFC`: cây đã tạo, nhưng các module tương ứng chưa đưa tệp lên Drive; định tuyến khi các module đó (M-07, M-09, M-10, IFC) có tệp riêng.
