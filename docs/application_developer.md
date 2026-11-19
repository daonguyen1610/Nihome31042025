# NICON Platform — Hướng dẫn phát triển ứng dụng

Version 1.1

Last Updated: 17/11/2026

---

## Mục lục

1. [Tổng quan](#1-tổng-quan)
2. [Kiến trúc](#2-kiến-trúc)
3. [Khởi chạy môi trường](#3-khởi-chạy-môi-trường)
4. [Cấu hình](#4-cấu-hình)
5. [Cơ sở dữ liệu](#5-cơ-sở-dữ-liệu)
6. [Quy ước backend](#6-quy-ước-backend)
7. [Xoá vĩnh viễn aggregate (Permanent Aggregate Deletion)](#7-xoá-vĩnh-viễn-aggregate-permanent-aggregate-deletion)
8. [CRM, báo giá, đấu thầu và hợp đồng](#8-crm-báo-giá-đấu-thầu-và-hợp-đồng)
9. [Dự án vận hành và gói công việc](#9-dự-án-vận-hành-và-gói-công-việc)
10. [BOQ](#10-boq)
11. [Cung ứng: nhà cung cấp, MR, kho, cảnh báo, RFQ, IFC](#11-cung-ứng-nhà-cung-cấp-mr-kho-cảnh-báo-rfq-ifc)
12. [Thiết kế và pháp lý](#12-thiết-kế-và-pháp-lý)
13. [Thi công và Module 4](#13-thi-công-và-module-4)
14. [Báo cáo và KPI](#14-báo-cáo-và-kpi)
15. [Dữ liệu tham chiếu của NICON](#15-dữ-liệu-tham-chiếu-của-nicon)
16. [Frontend](#16-frontend)
17. [Kiểm thử](#17-kiểm-thử)
18. [Triển khai](#18-triển-khai)
19. [Xử lý sự cố](#19-xử-lý-sự-cố)
20. [Kế hoạch hoàn thiện theo yêu cầu NICON](#20-kế-hoạch-hoàn-thiện-theo-yêu-cầu-nicon)

---

## 1. Tổng quan

Đây là nền tảng quản lý vận hành Design & Build của NICON. Ngoài website công
khai và tuyển dụng, ứng dụng có xác thực, RBAC động, CRM, báo giá/đấu thầu/hợp
đồng, thiết kế ba giai đoạn, pháp lý, thi công, nghiệm thu, hồ sơ hoàn công,
punch list và bàn giao công trình.

Tài liệu dành cho developer: cài đặt, cấu hình, database, quy ước code, hợp
đồng API theo module, kiểm thử, triển khai và kế hoạch hoàn thiện. Hướng dẫn
người dùng nằm ở `docs/user_guide.md`; hiện vật gốc của khách hàng (biên bản
họp, PRD, ISO, BOQ, tiến độ) ở `docs/Nicon/` (xem [mục 15](#15-dữ-liệu-tham-chiếu-của-nicon)).
Khi tài liệu và code khác nhau, controller/DTO và test là nguồn xác định hành vi thực tế.

| Thành phần | Công nghệ | Phiên bản |
|---|---|---|
| Backend | .NET SDK, ASP.NET Core, EF Core | 8.0 |
| Frontend | Node.js; React 18 + TypeScript + Vite | Node 20 trên CI, 22 trong image dev |
| Database | SQL Server | 2022 |
| Container | Docker, Docker Compose | bản hiện hành |
| Font PDF | Noto Sans, Noto Sans CJK | gói OS hoặc đường dẫn cấu hình |

Gói backend chính: `Microsoft.EntityFrameworkCore.SqlServer` và `.Design`
8.0.4, `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.0, `AutoMapper`
15.1.4, `MailKit` 4.16.0, `Swashbuckle.AspNetCore` 6.5.0. Frontend dùng
Tailwind CSS, shadcn/ui (Radix), React Router, Redux (trạng thái đăng nhập) và
Playwright cho E2E.

---

## 2. Kiến trúc

```
React (Vite + TS) --HTTP/API--> ASP.NET Core 8 Web API --EF Core--> SQL Server 2022
                                         |
                                         +--SMTP--> Mail server
                                         +--OAuth/Drive API v3--> Google Drive
```

```
Nihome31042025/
  docker-compose.yaml          -- runtime stack
  docker-compose-dev.yaml      -- dev stack có watch/HMR
  docker-compose-e2e.yaml      -- overlay E2E kèm hộp thư giả lập
  nihomebackend/               -- ASP.NET Core 8 Web API
    Controllers/ Services/ Models/ Mappings/ Extensions/
    Data/ (DbContext, seeder; Seeds/ là JSON nhúng; BoqTemplates/)
    Migrations/ Constants/ Localization/ wwwroot/
  nihomebackend.tests/         -- unit test (Controllers, Services, Mappings, Helpers)
  nihomebackend.integration.tests/
  nihomeweb/src/               -- pages, components (admin, layout, ui), services, hooks, lib, store
  nihomeweb/e2e/smoke/         -- Playwright spec
  tools/                       -- script SQL, kiểm tra translation key
  docs/
```

Nguyên tắc thiết kế:

- Controller mỏng: nhận request, kiểm tra đầu vào, gọi service, trả DTO. Logic
  nghiệp vụ nằm trong service, đăng ký qua DI. Không bao giờ trả entity.
- Content entity public định tuyến bằng slug. Dữ liệu lồng nhau (đoạn nội dung,
  gallery, requirements) lưu cột JSON dạng string (`ContentJson`,
  `SectionsJson`).
- Dịch nội dung: chỉ field đã đăng ký trong metadata registry của
  `TranslationsController` và ngôn ngữ đích `en`, `zh`, `ja` mới được ghi.
  Registry gồm cả catalog chỉ có `Name` (loại hình việc làm, dropdown tuyển
  dụng, master data, logo khách hàng): bản dịch lưu cho `Name`, còn màn CRUD
  vẫn sửa bản tiếng Việt gốc. Bản dịch có cấu trúc được validate đệ quy theo
  shape JSON của bản tiếng Việt trước khi lưu.
- Category đã bản địa hoá giữ cột ngôn ngữ riêng để tương thích truy vấn public.
  Giá trị fallback copy từ nguồn không chứng minh đã dịch; marker trong
  `entity_translations` ghi nhận bản dịch cố ý giống nguồn.
- Nhãn role group giữ translation key cũ. Placeholder copy từ nguồn được đánh
  dấu trong `entity_translations`, nên khi sửa nguồn chỉ placeholder đã đánh dấu
  bị cập nhật, bản dịch thật (kể cả trùng nguồn) được giữ. Ghi group kèm tên
  ngôn ngữ đích qua API cũ cần thêm quyền quản lý bản dịch. Group key không nằm
  trong danh sách UI translation; API pair, bulk và key-delete từ chối bằng
  `role_group_translation_use_content`.

---

## 3. Khởi chạy môi trường

### 3.1 Runtime stack (Docker Compose)

```bash
docker compose up -d                          # khởi động
docker compose down                           # dừng
docker compose up --build                     # build lại sau khi đổi source/dependency
docker compose down -v && docker compose up --build -d   # xoá volume, làm lại từ đầu
```

| Service | Container | Port |
|---|---|---|
| Backend API + SPA | `nihome31042025-backend` | 5043 |
| SQL Server | `nihome31042025-sqlserver` | 1433 |

Image build Vite và bản release ASP.NET ở các stage riêng rồi chạy binary đã
publish trên image runtime. Không có service frontend riêng: port 5043 phục vụ
cả SPA và API, port 8080 chỉ dùng khi chạy Vite trên host. Mọi thay đổi source
cần rebuild image. `.dockerignore` loại `test-results` và `TestResults` (kể cả
thư mục lồng nhau); để output chạy test ngoài build context.

Image backend cài và kiểm tra font Noto Latin/CJK cho xuất PDF. Môi trường
không dùng container có thể chỉ định font bằng `NIHOME_PDF_FONT_LATIN_PATH`,
`NIHOME_PDF_FONT_JA_PATH`, `NIHOME_PDF_FONT_ZH_PATH`; với file TTC đặt thêm biến
`_INDEX` tương ứng (số nguyên không âm). Mặc định đã bao Docker, Windows IIS và
macOS. Backend dừng khởi động nếu thiếu hoặc cấu hình sai font của bất kỳ ngôn
ngữ bắt buộc nào.

### 3.2 Lệnh backend và dev stack

Image runtime không có SDK hay file watcher. Biên dịch qua build stage:

```bash
docker compose build nihomeBackend
```

Format, test và EF tooling chạy trong một container .NET SDK riêng, mount repo
ở gốc solution, gồm cả project unit và integration khi verify format toàn
solution. Test SQL dùng mạng Compose và database test cô lập.

Để sửa code có watch, dùng `docker-compose-dev.yaml` (không dùng chung với
runtime stack):

```bash
docker compose -f docker-compose-dev.yaml up -d --build
```

- Backend: image có .NET SDK 8 và font PDF, chạy `dotnet watch --no-hot-reload`
  từ `/app` (restart khi đổi file để tránh lỗi Roslyn Hot Reload trên Linux).
- Frontend: Node 22 chạy Vite HMR, proxy `/api`, `/files`, `/images` về backend.
  Ứng dụng vẫn ở `http://localhost:5043`; backend và Swagger truy cập trực tiếp
  qua `http://localhost:5044`. Sửa frontend không kích hoạt rebuild ASP.NET.
- Cache build, managed files, ảnh upload/process và quarantine nằm trong named
  volume. Volume managed-file mới được khởi tạo bằng các PDF mẫu thiết kế/hoàn
  công có trong image backend.
- SQL Server dev bị giới hạn 1 GiB để compiler và Vite đủ RAM trong Docker
  Desktop; production không áp giới hạn này.
- Mặc định dùng native file events. Nếu bind mount không phát event, đặt
  `DOTNET_USE_POLLING_FILE_WATCHER=1` và `CHOKIDAR_USEPOLLING=true` trước khi
  khởi động.
- Lúc khởi động, dev backend chỉ xoá các file NuGet import viết hoa/thường lẫn
  lộn còn sót từ tên cũ `NihomeBackend.csproj` trong volume `obj`, tránh lỗi
  trùng import `MSB4011`.
- Connection string và `MSSQL_SA_PASSWORD` dùng cùng mật khẩu local cố định với
  runtime stack. Volume SQL có sẵn phải đang dùng mật khẩu đó; đổi biến môi
  trường không reset login đã lưu.
- Hai stack cùng publish port 5043 và mặc định dùng chung volume SQL/key: dừng
  runtime stack trước khi bật dev stack. Image dev có tag riêng.
- Database và volume file phải thuộc cùng một bộ dữ liệu. Khi dùng lại database
  có upload, backup và copy file/ảnh tương ứng vào volume dev trước khi khởi
  động. Database mới phải đi với volume file mới; không để worker dọn dẹp nhìn
  thấy upload của bộ dữ liệu khác.

### 3.3 Swagger

Chỉ bật khi backend chạy môi trường `Development`:

- Swagger UI: `http://localhost:5043/swagger` (dev stack: port 5044)
- OpenAPI JSON: `http://localhost:5043/swagger/v1/swagger.json`
- API base: `http://localhost:5043/api`

### 3.4 Chạy frontend trên host

```bash
cd nihomeweb
npm install
npm run dev     # http://localhost:8080
```

Khi chạy Vite trực tiếp trên host và backend ở origin khác, đặt `VITE_API_URL`.

### 3.5 Build production

```bash
docker compose build nihomeBackend   # backend (image runtime không có SDK)
cd nihomeweb && npm run build        # frontend, output ở nihomeweb/dist/
```

Khi triển khai, backend phục vụ SPA đã build như static file. Gói release cho
IIS được tạo bằng `auto-deployment.sh` (xem [18.2](#182-checklist-production)).

---

## 4. Cấu hình

Cấu hình backend (kể cả logging cho cả hai runtime) nằm trong
`nihomebackend/appsettings.json` và `appsettings.Development.json`.

### 4.1 Database

```
Server=<host>,1433;Database=NihomeDB;User Id=sa;Password=<development-password>;TrustServerCertificate=True;
```

| Chạy từ | `<host>` |
|---|---|
| Công cụ trên máy host | `localhost` |
| Service trong mạng Compose | `sqlserver` |
| Container riêng, không gắn mạng Compose | `host.docker.internal` (nếu nền tảng hỗ trợ) |

Key cấu hình: `ConnectionStrings:DefaultConnection`.

### 4.2 JWT, SMTP, CORS

```json
{
  "Jwt": {
    "Issuer": "nihome-api",
    "Audience": "nihome-client",
    "AccessTokenMinutes": 10080,
    "RefreshTokenDays": 30,
    "ActiveKeyId": "key2",
    "Keys": { "key1": "<base64-encoded-key>", "key2": "<base64-encoded-key>" }
  },
  "Smtp": {
    "Host": "mail9005.maychuemail.com", "Port": 465, "UseSsl": true, "UseStartTls": false,
    "Username": "<email-username>", "Password": "<email-password>",
    "FromName": "Nihome", "FromEmail": "noreply@nihome.vn"
  },
  "Frontend": {
    "AllowedOrigins": ["http://localhost:8080", "http://127.0.0.1:8080"]
  }
}
```

- Access token sống 7 ngày (10080 phút), refresh token 30 ngày. Hai khoá ký hỗ
  trợ xoay vòng; `ActiveKeyId` chọn khoá ký token mới. Refresh token chỉ dùng
  một lần.
- `Frontend:AllowedOrigins` phải khớp chính xác origin (giao thức và port).
- Link trong email (duyệt báo giá, nhóm công việc, mời báo giá RFQ) lấy địa chỉ từ
  chính request đang xử lý (`PublicOrigin.FromRequest`), không đọc cấu hình: header
  `Origin` của trình duyệt khi trùng host (hoặc localhost khi dev), nếu không thì
  `scheme://host` của request. Nên đặt `AllowedHosts` đúng tên miền để host lạ không
  lọt vào link.

### 4.3 Logging JSON

Section `Logging` trong hai file appsettings là nguồn duy nhất cho log level và
định danh log của backend lẫn frontend: ASP.NET đọc lúc chạy, Vite đọc
`Logging:Frontend` lúc build. Mỗi log là một object JSON trên một dòng:

```json
{
  "logger": "nihomebackend/Services/ExampleService.cs",
  "message": "date=\"2026-10-13T08:30:00.0000000+00:00\", process_id=\"314\", Example message",
  "properties": {
    "api_name": "Example.Get", "app_name": "NihomeBackend",
    "correlation_id": "request-123", "message_type": "api_request"
  },
  "severity": "INFO",
  "thread": ".NET TP Worker"
}
```

- Với category `ILogger<T>`, `logger` là đường dẫn source suy từ category. Class
  nằm trong file khác tên phải khai báo ở `Logging:Json:SourcePathOverrides`.
  Category framework không có file source giữ nguyên tên category.
- Giá trị của message template nằm trong `properties` với key snake_case, ngoài
  bốn field định danh bắt buộc.
- Code browser phải dùng `createLogger(source)` trong
  `nihomeweb/src/lib/logger.ts`. Browser không có process/thread id thật nên dùng
  id phiên browser và `browser-main`.
- Frontend gắn `X-Correlation-Id` riêng cho mỗi request. Backend validate, tự
  tạo id an toàn nếu thiếu/sai, trả lại trong response header và đưa vào logging
  scope. `HttpContext.TraceIdentifier` vẫn là request id, không bị thay. Log lỗi
  API ở frontend dùng lại correlation id để truy vết chung với backend. Log nền
  không có request dùng correlation id theo process.

### 4.4 Biến môi trường và volume Compose

| Biến | Giá trị | Mục đích |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` | Profile môi trường |
| `ASPNETCORE_URLS` | `http://0.0.0.0:5043` | Địa chỉ Kestrel |
| `ConnectionStrings__DefaultConnection` | chuỗi kết nối tới `sqlserver` | Database |
| `BUILD_SHA` | `$(git rev-parse HEAD)` | Ghi vào `buildSha` của health |

Health (`/api/system/health`) trả `buildSha`; CI chỉ coi stack đã sẵn sàng khi
giá trị này khớp commit đang checkout, tránh để process cũ vượt qua bước
readiness E2E. `docker-compose-e2e.yaml` thêm service `smtpSink` (không relay)
và ghi đè `EmailSettings__*`.

| Volume | Mount | Mục đích |
|---|---|---|
| `data_protection_keys` | `/root/.aspnet/DataProtection-Keys` | Key ring ASP.NET Data Protection |
| `sqlserver_data` | `/var/opt/mssql` | Dữ liệu SQL Server |
| `work_task_files` | `wwwroot/files/business-documents/work-tasks` | File đính kèm của task |

Không xoá `sqlserver_data` để làm mới code: volume này chứa dữ liệu nghiệp vụ.

### 4.5 Cấu hình nghiệp vụ trong `site_settings`

- `ContractMaxFileSizeMb`: dung lượng tối đa mỗi file hợp đồng/phụ lục, sửa tại
  `/admin/settings?tab=contracts`. Mặc định 100 MiB, nhận số nguyên 1–5.120 MiB
  (5 GiB). Mỗi lần upload đọc giá trị hiện hành; transport và bước kiểm tra
  file hợp đồng cho phép tối đa 5 GiB. Multipart cần thêm dung lượng đĩa tạm.
  Reverse proxy phải cho body ít nhất 5.126 MiB và đủ thời gian upload. API:
  `GET/PUT /api/site-settings/upload-limits/contracts`: mọi người đã đăng nhập đọc
  được để form upload kiểm tra trước, sửa cần `system.settings.manage`.
  Các endpoint upload khác giữ giới hạn riêng.
- Template email: xem [6.3](#63-email-ứng-dụng).
- Google Drive: cấu hình trong database, xem [18.3](#183-google-drive-trên-iis).

---

## 5. Cơ sở dữ liệu

### 5.1 Tổng quan và schema

Database `NihomeDB`, EF Core 8 dùng split query và command timeout 60 giây.
`AppDbContext` và model snapshot là nguồn chuẩn của schema.

| Miền | Aggregate/bảng chính |
|---|---|
| Định danh và kiểm soát | users, refresh/OTP, RBAC roles/permissions, notifications/templates, audit, idempotency, hard-delete operations |
| Nội dung public | activities/categories, news/categories, public projects/categories, services, slideshow, logos, about, processes, recruitment, contacts, settings, translations |
| CRM/tiền thiết kế | leads, customers/contacts/documents, opportunities, quotes/items/versions/approvals/documents, material-rate catalogs/revisions/lines, tenders/checklists/estimates, surveys/media |
| Dự án chung | operational projects, members/roles/assignments/history, project documents/folders, project reports |
| Hợp đồng | contracts, payment milestones/events, appendices/VO, attachments, contract lines |
| Thiết kế/pháp lý | design projects, concept options, basic documents, shop drawings, drawing revisions, IFC releases/items/recipients, design schedule, permit checklist items |
| Thi công | construction tasks/dependencies, site diaries, punch items, HSE, acceptance, as-built, handover |
| Cung ứng | vendors, BOQ revisions/lines, material requests, RFQ (lines/invitations/bids/events/awards), receipts/issues, vendor ratings, material alerts |
| Tài chính/KPI | payment requests, accounting periods/corrections, KPI definitions/periods/snapshots |
| Drive | Google Drive credential, project document catalog, folder bindings, trạng thái đồng bộ |

Quy tắc dữ liệu cần nhớ:

- `projects` (website public), `operational_projects` (dự án nội bộ trung tâm)
  và `design_projects` (luồng thiết kế) cùng tồn tại có chủ đích. Không join theo
  tên/mã, không dùng bảng này thay bảng kia; chỉ đi theo foreign key.
- Bàn giao assignment giữ định danh KPI gốc và ghi ID assignment trước đó lên
  bản thay thế; bàn giao lặp lại cho cùng người tạo định danh riêng. Migration
  `AllowAssignmentHandover` đổi unique index; rollback không tạo lại được index
  cũ nếu đã có cặp member/work-key trùng, phải có kế hoạch bảo toàn dữ liệu được
  duyệt trước.

Index đáng chú ý: unique `users.Phone`, `refresh_tokens.Token`; `Slug` unique
trên `activities`, `news_articles`, `projects`, `service_items`,
`slideshow_items`; unique `activity_categories.Name`; unique (`Key`,
`LanguageCode`) của `translations`; unique (`EntityType`, `EntityId`,
`FieldName`, `LanguageCode`) của `entity_translations`; `handover_records`
unique theo `DesignProjectId` và `HandoverCode`.

### 5.2 Migration

Mọi thay đổi schema đi qua EF Core migration, không sửa schema trực tiếp. Không
viết tay metadata migration hay model snapshot: sinh bằng EF trong container
.NET 8 SDK có `dotnet-ef` 8.x (image dev hiện chưa cài, nên
`docker exec ... dotnet ef` chưa chạy được), đọc lại migration và snapshot rồi
mới áp dụng.

```bash
dotnet ef migrations add <MigrationName>
dotnet ef database update
dotnet ef migrations remove        # chỉ migration cuối chưa áp dụng
dotnet ef migrations script        # sinh SQL để review (thêm --idempotent khi deploy)
dotnet ef migrations list
```

Quy tắc chung cho mọi migration có dữ liệu:

- Rehearse script trên bản restore của backup giống production trước cửa sổ
  bảo trì; backup ngay trước khi áp dụng.
- Không backfill bằng suy đoán (tên, khách hàng, ngày). Dữ liệu cũ không đủ
  bằng chứng giữ null/chưa phân loại.
- Nếu `Down` xoá dữ liệu mới (lịch sử, snapshot, link), export trước khi
  rollback; nhiều migration chủ động từ chối rollback khi đã có dữ liệu thật.
- Không gỡ một migration đã chạy ở môi trường nào đó. Không rollback bằng cách
  xoá volume.

Startup tự chạy `Database.Migrate()` và seed (trừ môi trường `IntegrationTests`).
`AddHandoverRecords` và `AddHandoverConcurrency` (thêm `rowversion` chống mất
cập nhật) phải có trước bản build NIH-144.

### 5.3 Seed dữ liệu

Startup áp dụng migration rồi chạy pipeline seed nền: users/settings, nội dung,
UI và entity translations, RBAC catalog/roles, master data, workflow,
notification templates và tài khoản theo vai trò nghiệp vụ. Dữ liệu mẫu CRM/
thiết kế/thi công chỉ seed ở môi trường `Development`
(`includeDevelopmentSamples`); integration test tự tạo fixture.

- Activities, news, projects backfill theo slug, giữ chỉnh sửa của admin.
  Process documents và logos đối soát cộng dồn: khôi phục dòng chuẩn bị thiếu,
  không xoá dòng tự tạo, không ghi đè giá trị admin.
- Process document chuẩn có `SeedKey` nullable (filtered unique index), suy từ
  process group và URL asset chuẩn đầu tiên nên đổi tiêu đề/thứ tự không đổi
  định danh. Dòng không có `SeedKey` thuộc về admin. Metadata ảnh/file chỉ được
  backfill khi tiêu đề chuẩn chưa đổi.
- Workflow manifest được validate trước khi lưu; định nghĩa lỗi, thứ tự bước
  trùng/sai hoặc role duyệt không tồn tại làm startup thất bại thay vì âm thầm
  nới lỏng chuỗi duyệt.
- Asset process nằm ngoài git ở `nihomebackend/wwwroot/process-assets/`
  (`images/`, `files/`), nguồn chuẩn là bản backup; phải có trên server thì URL
  trong `processes.json` mới mở được.
- UI translation mặc định nằm ở `nihomebackend/Data/Seeds/i18n/` (bốn ngôn ngữ,
  embedded). `TranslationSeeder` chỉ thêm key thiếu, điền category trống và giữ
  bản sửa của admin. Muốn đổi câu chữ của key đã seed: thêm giá trị `old` cạnh
  bản mới trong entry i18n và viết data migration chỉ cập nhật khi giá trị DB
  còn đúng giá trị cũ (ví dụ `SyncConstructionGuidanceTranslations`).
  `GET /api/translations/{lang}` có cache theo process nên cần restart backend
  sau migration.
- Tài khoản `SUPER_ADMIN`, `ADMIN` và các role nghiệp vụ được tạo cố định trong
  `DbSeeder`, `BusinessRoleUserSeeder`, `TestDataSeeder` (integration) và
  fixture Playwright. Không chép credential vào tài liệu vận hành. Production
  phải đổi mật khẩu hoặc vô hiệu các tài khoản này trước khi mở truy cập.

Xoá dữ liệu nghiệp vụ bằng SQL (CRM, Thiết kế, Cung ứng, Tài chính, Pháp lý, Thi
công, Analytics): mở `tools/sqlserver_business_data_purge.sql` trong SQL editor
và chạy cả script.

1. Đặt `@ExpectedDatabase` đúng tên database (`NihomeDB` ở local) và
   `@ExpectedSchema` (`dbo` ở local; schema thấy được trên deployment là
   `niconNew`).
2. Lần đầu giữ `@Execute = 0` để xem phạm vi bảng, số dòng và phụ thuộc FK.
   Bảng ở schema khác chỉ được đếm.
3. Dừng ghi từ backend, backup đã kiểm tra, rồi chạy lại với `@Execute = 1`.

Script giữ users, permissions, settings, translations, nội dung website, audit
và lịch sử EF migration. File managed và object trên Drive không bị xoá. Bảng
mới chưa phân loại làm script dừng cho tới khi được review. Host Development
seed lại dữ liệu mẫu khi khởi động, nên giữ backend tắt sau khi reset nếu không
muốn có dữ liệu mẫu.

### 5.4 Kiểm tra database

```bash
docker run --platform linux/amd64 -it --rm \
  --network container:nihome31042025-sqlserver \
  mcr.microsoft.com/mssql-tools \
  /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "<development-password>"
```

```sql
SELECT name FROM sys.databases;
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
  FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'YourTableName';
EXEC sp_helpindex 'YourTableName';
GO
```

### 5.5 Migration lịch sử về Operational Project (NIH-465)

<a id="67-operational-project-historical-migration"></a>

Migration `20260907021500_ReconcileOperationalProjectBackfill` gắn các dòng
`DesignProject`, `Contract`, `Opportunity`, `Quote` cũ vào aggregate
`OperationalProject`. `Project` của website không thuộc phạm vi. Migration giữ
nguyên dòng nguồn và lịch sử, chỉ điền `OperationalProjectId` còn thiếu, chỉ
tạo project mới khi không kế thừa được từ cùng chuỗi nghiệp vụ, không gộp hay
xoá project có sẵn.

Thứ tự ưu tiên:

1. Design Project kế thừa project của Contract.
2. Design Project chưa có project → tạo `PJ-MIG-DP-{id}`; Contract kế thừa.
3. Opportunity kế thừa project duy nhất mà Contract/Quote của nó đang dùng.
4. Opportunity chưa có → tạo `PJ-MIG-OP-{id}`; Quote/Contract kế thừa.
5. Contract trực tiếp không có nguồn → tạo `PJ-MIG-CT-{id}`.

Project kế thừa phải cùng Customer. Nhiều ứng viên, map chéo khách hàng, FK
hỏng, chuỗi không khớp hoặc trùng mã đều làm migration dừng (`THROW 51020` đến
`51029`) và rollback trong transaction; sửa dữ liệu nguồn, không tắt kiểm tra
hay sửa migration để bỏ qua dòng lỗi. Mã cố định và guard `NOT EXISTS` cho phép
chạy lại.

Triển khai: rehearse trên bản restore, lưu output và số dòng trước/sau làm bằng
chứng, dừng ghi của ứng dụng khi chạy production, rồi backup lần hai ngay sau
khi kiểm tra đạt. Nếu đã cho ghi lại, ưu tiên sửa tiến (forward correction) vì
restore backup cũ sẽ mất giao dịch mới.

```bash
docker exec <backend-container> dotnet ef migrations script \
  20260906150351_AddFinanceControlWorkflows \
  20260907021500_ReconcileOperationalProjectBackfill --idempotent
docker exec <backend-container> dotnet ef database update
```

```sql
SELECT 'DesignProject' EntityType, COUNT(*) TotalRows, COUNT(OperationalProjectId) MappedRows FROM design_projects
UNION ALL SELECT 'Contract', COUNT(*), COUNT(OperationalProjectId) FROM contracts
UNION ALL SELECT 'Opportunity', COUNT(*), COUNT(OperationalProjectId) FROM opportunities
UNION ALL SELECT 'Quote', COUNT(*), COUNT(OperationalProjectId) FROM quotes;
```

`TotalRows` phải bằng `MappedRows` ở cả bốn nhóm. Sau khi deploy thành công mà
cần rollback: dừng ghi và restore backup trước deploy; không tự null project ID
hay xoá project đã sinh. Route và DTO không đổi; field project nullable vẫn
tương thích. Ràng buộc runtime "mỗi Contract mới thuộc đúng một Operational
Project" thuộc NIH-466.

---

## 6. Quy ước backend

### 6.1 Tổ chức code

```
Controller (mỏng) --> Service (nghiệp vụ) --> AppDbContext (dữ liệu)
```

- Models chia entity và DTO request/response; Mappings là profile AutoMapper;
  Extensions đăng ký DI, CORS, middleware.
- Dùng `async/await` cho mọi I/O, `AsNoTracking()` cho truy vấn chỉ đọc.
- Status code: 200, 201, 204, 400, 401, 403, 404, 409, 500. Action controller
  nên dưới 20 dòng.

Service hạ tầng đáng chú ý:

| Service | Vai trò |
|---|---|
| `JwtService`, `RefreshTokenService`, `PasswordService`, `OtpService` | Token, refresh token, hash mật khẩu (Identity), OTP |
| `EmailService`, `EmailTemplateFormatter`, `EmailTemplateBaseline` | Gửi SMTP qua MailKit, thay placeholder, khung HTML NICON chung |
| `TimeService` | Nguồn thời gian UTC tập trung |
| `OrphanedManagedFileCleanupService` | Dọn file managed không còn tham chiếu (xem [6.4](#64-file-managed-và-tài-liệu-nghiệp-vụ)) |
| `OperationalProjectService` | CRUD, vòng đời, rollup, cấp mã và concurrency của dự án trung tâm |
| `HandoverRecordService` | Scope, validate, readiness, vòng đời và optimistic concurrency của bàn giao |
| `TranslationService`, `EntityTranslationService` | UI translation tĩnh và bản dịch nội dung động |

Thêm một entity nội dung mới:

1. Entity và DTO trong `Models/`.
2. `DbSet` trong `Data/AppDbContext.cs`, cấu hình bảng trong `OnModelCreating`.
3. Sinh và review migration ([5.2](#52-migration)).
4. Service trong `Services/`, controller trong `Controllers/`, mapping trong
   `Mappings/AutoMapperProfile.cs`.
5. Đăng ký service ở `Extensions/ServiceCollectionExtensions.cs`.
6. Unit test cho logic tách biệt, integration test cho HTTP, phân quyền và
   persistence.

### 6.2 Quy tắc ghi dùng chung

Các module bên dưới chỉ ghi phần khác biệt; mặc định áp dụng:

- **Idempotency.** Thao tác tạo và chuyển trạng thái nhận header
  `Idempotency-Key` (1–120 ký tự; thiếu, rỗng hoặc dài hơn trả `400`). Gửi lại
  cùng key và cùng payload trả response đã lưu; cùng key khác payload trả `409`.
  Khi replay, server kiểm lại scope hiện tại, nên người đã mất quyền không lấy
  lại được response cũ. Client giữ key khi thử lại cùng payload sau lỗi không
  chắc chắn, đổi key khi payload đổi.
- **Concurrency.** Cập nhật và chuyển trạng thái gửi `rowVersion` trong body
  hoặc header `If-Match`; response chi tiết trả ETag. Thiếu hoặc sai định dạng
  trả `400`, body và header mâu thuẫn trả `400`, bản cũ trả `409` (thường là
  `crm_concurrency_conflict` qua global handler). Client tải lại dữ liệu và giữ
  bản nháp người dùng đang nhập.
- **Phạm vi truy cập.** Ngoài permission toàn cục, service kiểm scope bản ghi
  (owner, thành viên dự án qua `IProjectAccessService`, phòng ban). Bản ghi
  ngoài scope hoặc không tồn tại trả `404` để không lộ sự tồn tại; thiếu đăng
  nhập `401`; thiếu permission `403`. Permission `*.view.all` chỉ mở rộng phạm vi
  đọc, không cấp quyền ghi.
- **Tách người làm và người duyệt.** Người tạo/trình không tự duyệt bản ghi của
  mình; server kiểm, UI chỉ ẩn nút.
- **Từ chối không để lại dấu vết.** Thao tác bị từ chối không đổi bản ghi gốc,
  phụ thuộc, lịch sử, file hay notification (trừ audit/record từ chối được phép).
- **Audit.** Mọi chuyển trạng thái ghi audit chuẩn với actor, đối tượng và kết
  quả. Notification gửi sau commit, best effort, không rollback giao dịch đã lưu.
- **Khoá SQL.** Kiểm tra phụ thuộc chạy trong transaction serializable; luồng có
  tranh chấp dùng SQL Server application lock thuộc transaction (RFQ, kho, cấp
  mã, hồ sơ Concept, đại diện pháp luật của Customer). Timeout khoá trả conflict;
  client tải lại rồi thử lại.

### 6.3 Email ứng dụng

Mọi email ứng dụng mới hoặc thay đổi phải có template sửa được tại
`/admin/email-templates`, hợp đồng biến rõ ràng, preview thật trước khi gửi,
render và HTML-encode ở server, permission gửi và sửa template tách riêng.
Notification nội bộ không tự động cần email.

Quy tắc template (GET/PUT cần `system.settings.view` / `system.settings.manage`):

- Subject 1–200 ký tự, một dòng. Body HTML 1–8.000 ký tự, chỉ thẻ cấu trúc cơ
  bản và inline style an toàn theo allowlist dùng chung. Biến viết
  `{{variableName}}`; biến lạ hoặc sai cú pháp bị từ chối. Sau render: subject
  tối đa 200, body tối đa 16.000 ký tự. Giá trị động được HTML-encode ở server.
- `EmailTemplateBaseline` định nghĩa khung HTML NICON trong code (card 600px,
  header đỏ) và áp khi render email/preview mới; snapshot đã xếp hàng không bị
  viết lại. `DbSeeder` lưu default cho settings mới và nâng cấp từng body còn
  thiếu hoặc còn đúng bản cũ; body đã tuỳ chỉnh được giữ. Revert code không
  revert body đã lưu; muốn khôi phục thì PUT lại HTML cũ qua API template.

| Template | API | Biến | Body bắt buộc chứa |
|---|---|---|---|
| Gửi báo giá cho khách | `/api/site-settings/email-templates` | `customerName`, `quoteCode`, `opportunityName`, `quoteLines`, `discountPercent`, `vatPercent`, `grandTotal`, `validUntil` | `quoteLines`, `grandTotal` |
| Duyệt báo giá nội bộ (tab **Email duyệt Báo giá**) | `/api/site-settings/quote-approval-template` | `recipientName`, `senderName`, `quoteCode`, `quoteVersion`, `grandTotal`, `validUntil`, `quoteUrl` | `quoteCode`, `<a href="{{quoteUrl}}">` |
| Nhắc gia hạn/kết thúc HĐ (`renewalSubject`, `renewalBody`, `endSubject`, `endBody`) | `/api/site-settings/contract-reminder-templates` | `customerName`, `contractNumber`, `endDate` (`dd/MM/yyyy`) | `{{contractNumber}}`, `{{endDate}}` |
| Duyệt gói công việc (tab **Duyệt nhóm công việc**) | `/api/site-settings/work-package-approval-template` | `recipientName`, `workPackageCode`, `workPackageName`, `recordType`, `recordReference`, `senderName`, `workPackageUrl` | `{{workPackageCode}}`, `{{recordReference}}`, `<a href="{{workPackageUrl}}">` |
| Duyệt/ban hành tiến độ tổng | `POST /api/site-settings/email-templates/project-schedule/preview` (`system.settings.manage`, chỉ render, không lưu/gửi) | — | — |

Template tuỳ chỉnh cũ của gói công việc được chèn link khi tải (trong bộ nhớ),
nội dung gốc giữ nguyên. Body built-in cũ của email tiến độ được thay bằng layout
NICON hiện tại khi đọc; body tuỳ chỉnh không đổi.

Luồng gửi:

1. Client gọi endpoint preview; server render đúng nội dung sẽ gửi và trả
   `previewToken` được bảo vệ, hết hạn sau 15 phút, gắn với người gọi, ngữ cảnh,
   bản ghi/phiên bản, người nhận (To/CC) và nội dung đã render.
2. Thao tác gửi/trình kèm `previewToken`. Server render lại; token thiếu, hết hạn
   hoặc nội dung đã đổi thì từ chối trước khi đổi trạng thái, người dùng phải
   preview lại.
3. Dòng email xếp hàng commit cùng chuyển trạng thái nghiệp vụ trong một
   transaction. Worker claim (`Sending`) trước khi gọi SMTP.
4. `Sent` nghĩa là SMTP đã nhận, không phải đã vào hộp thư. Lỗi SMTP hoặc ngữ
   cảnh đổi (người nhận, quyền, trạng thái) chuyển `NeedsReview` mà không gửi.
   `Sending` và `NeedsReview` không tự thử lại (trừ email tiến độ tổng, xem
   [9.1](#91-api-dự-án-vận-hành)). Crash sau khi claim có thể để lại `Sending`:
   kiểm log SMTP trước mọi khôi phục thủ công, không reset mù.

### 6.4 File managed và tài liệu nghiệp vụ

Form permit, nhà cung cấp, nghiệm thu từng phần, hoàn công và bàn giao cho phép
chọn file local ngoài field URL ngoài. File lưu ở
`wwwroot/files/business-documents/{area}/` với tên sinh tự động, tối đa 20 MB,
đuôi `.pdf`, `.doc`, `.docx`, `.xls`, `.xlsx`, `.png`, `.jpg`, `.jpeg`.

| Method | Route | Permission | Mục đích |
|---|---|---|---|
| `POST` | `/api/business-documents/vendors` | `proc.vendors.manage` | Upload hồ sơ năng lực nhà cung cấp |
| `DELETE` | `/api/business-documents/vendors` | `proc.vendors.manage` | Huỷ file đã upload khi chưa Vendor nào tham chiếu |
| `POST` | `/api/business-documents/acceptance` | `construction.acceptance.manage` | Upload tài liệu nghiệm thu từng phần |
| `POST` | `/api/business-documents/as-built` | `construction.asbuilt.manage` | Upload hồ sơ hoàn công |
| `POST` | `/api/business-documents/handover` | `construction.handover.manage` | Upload tài liệu bàn giao |
| `POST` | `/api/permits/{id}/documents/{kind}` | `permit.checklists.view` + scope dự án | Upload `SubmittedPackage` hoặc `IssuedPermit` |
| `GET` | `/api/vendors/{id}/capability-file/content` | `proc.vendors.view` | Đọc file của vendor đã lưu |
| `GET` | `/api/acceptance-records/{id}/documents/{fileName}/content` | `construction.acceptance.view` | Đọc trong scope dự án |
| `GET` | `/api/as-built-documents/{id}/content` | `construction.asbuilt.view` | Đọc file hồ sơ hoàn công |
| `GET` | `/api/handover-records/{id}/documents/{fileName}/content` | `construction.handover.view` | Đọc trong scope dự án |
| `GET` | `/api/permits/{id}/documents/{fileName}/content` | `permit.checklists.view` | Đọc file hồ sơ nộp/giấy phép |

Quy tắc:

- Số lượng file theo field hiện có: vendor và hoàn công một đường dẫn, nghiệm
  thu và bàn giao tối đa 20, permit một file hồ sơ nộp và một file giấy phép.
- Không có endpoint đọc chung theo area + tên file. Request đọc phải chỉ ra một
  bản ghi đã lưu mà metadata tham chiếu đúng đường dẫn host-relative đó, rồi áp
  scope của module. URL HTTP(S) ngoài vẫn được hỗ trợ và không bị coi là file
  managed.
- Request static trực tiếp vào `/files/quotes`, `/files/customers`,
  `/files/contracts`, `/files/capability`, `/files/tenders`,
  `/files/business-documents`, `/files/design` trả `404` trước static-file
  middleware. React lấy byte qua route có xác thực dưới dạng Blob, nên upload
  hai bước chưa xem trước được cho tới khi metadata được lưu.
- Thay hoặc gỡ tham chiếu file của Contract, Design, Vendor, Acceptance,
  As-Built, Handover, Permit chỉ xoá file cũ sau khi database lưu thành công.
  Upload Basic Design và Detail Design kiểm giai đoạn đang hoạt động của dự án ở
  service.
- Chỉ kiểm đuôi file và giới hạn 20 MB; quét malware và kiểm chữ ký file đầy đủ
  vẫn là việc hardening khi triển khai.
- Customer, quote, contract có luồng tài liệu riêng; site diary và punch list
  chưa có hợp đồng tài liệu đầy đủ nên chưa hỗ trợ.

Worker `OrphanedManagedFileCleanupService` chạy mỗi 6 giờ trên `/images/upload`,
`/images/processes` và mọi thư mục con của `/files` (trừ quarantine của
hard-delete). File không còn tham chiếu và đã quá 24 giờ được chuyển nguyên tử
vào `.managed-file-quarantine` (ngoài `wwwroot`). Worker đọc lại tham chiếu
ngay trước và sau khi chuyển; file được tham chiếu lại thì trả về chỗ cũ. File
trong quarantine chỉ bị xoá hẳn sau thêm 24 giờ và một lần kiểm tham chiếu nữa.
JSON tham chiếu lỗi, validate thất bại hoặc đường dẫn symlink/reparse-point làm
worker dừng phần xoá.

File đính kèm assignment của dự án còn được coi là đang tham chiếu cho tới khi có
`StorageDeletedAt`, kể cả metadata đã soft-delete chờ dọn. Xoá Design/Operational
Project đưa metadata và đường dẫn của chúng vào impact token và quarantine qua
durable operation; đường dẫn sai hoặc dùng chung chặn kế hoạch xoá.

---

<a id="77-permanent-aggregate-deletion"></a>

## 7. Xoá vĩnh viễn aggregate (Permanent Aggregate Deletion)

Hard delete là thao tác nghiệp vụ, không phải `DbSet.Remove`. Mọi root có nút xoá
cho người dùng phải theo hợp đồng dưới đây, kể cả bản ghi seed/demo, để không
xoá dở dang hoặc che giấu dữ liệu phụ thuộc.

### 7.1 Hợp đồng API

1. `GET /api/{resource}/{id}/deletion-impact` trả nhãn root, mã xác nhận, plan
   token tất định, tổng số ảnh hưởng, có được xoá hay không và các nhóm phụ thuộc:
   - `Delete`: dữ liệu thuộc aggregate, xoá cùng root.
   - `Unlink`: bản ghi độc lập hoặc tài nguyên ngoài, chỉ gỡ liên kết.
   - `Block`: phải xử lý sạch trước khi xoá root.
2. `DELETE /api/{resource}/{id}` (body `ConfirmDeletionRequest`) gửi
   `planToken`, `confirmation` và `rowVersion` (bắt buộc khi root có optimistic
   concurrency).

Plan gồm mọi ID phụ thuộc có ý nghĩa nghiệp vụ, trực tiếp lẫn lồng nhau; thêm
hoặc bớt một con sau khi preview làm token cũ mất hiệu lực. Server tính lại
impact và tạo durable operation trong cùng một transaction serializable, commit,
rồi mới xử lý.

| Tình huống | Kết quả |
|---|---|
| Opportunity/Quote/Contract thiếu hoặc sai định dạng `rowVersion` | `400` |
| `rowVersion` cũ hoặc plan đã đổi | `409` |
| Sai mã xác nhận hoặc còn blocker | `400` |
| Hoàn tất cleanup ngoài, finalizer DB và purge quarantine | `204` |
| Durable operation còn pending/retry/cần xử lý tay | `202` kèm operation ID và trạng thái |

Request bị từ chối giữ nguyên root và phụ thuộc, không ghi audit. Client có thể
poll kết quả an toàn; operation chỉ chứng minh root đã xoá khi ở `Completed`.

### 7.2 Quy tắc thực thi

- Controller phân quyền và dịch kết quả sang HTTP. Service kiểm plan, xác nhận,
  concurrency và blocker. Service xoá của từng aggregate chịu thứ tự phụ thuộc
  và staging file.
- Khi có file local managed hoặc item Drive do NICON sở hữu, endpoint tạo
  durable operation (định danh GUID, không có FK tới root). Mỗi resource type +
  ID chỉ có một operation chưa xong.
- File local phải là đường dẫn host-relative dưới storage root riêng; được
  chuyển nguyên tử vào quarantine cùng volume trước mọi bước không đảo ngược.
- Chỉ xoá vĩnh viễn item Drive khi metadata chứng minh thuộc `InstanceId` NICON
  hiện tại, mọi app property kỳ vọng khớp, parent khớp (nếu có) và Drive xác
  nhận tài khoản đang kết nối sở hữu và được xoá. Item import, chia sẻ, không
  khớp hoặc không rõ nguồn là blocker. Item đã mất trên Drive coi như xoá thành
  công (idempotent).
- Đúng một audit thành công, do processor ghi trong cùng lần lưu đánh dấu
  `Completed`, sau finalizer và purge. Không audit lúc nhận request, lúc lỗi
  validate hay trước khi hoàn tất.
- Root seed bị xoá ghi tombstone để seed không tạo lại: Opportunity theo đúng tên
  seed, Contract theo số `HD-SAMPLE-`, Survey theo mã `SV-SAMPLE-`, Tender theo mã
  `TD-SAMPLE-`, Capability Document theo đường dẫn managed khi mô tả bắt đầu
  `[SAMPLE_CAP]`. Seed lọc root và phụ thuộc trước khi chèn và bỏ qua tự phục hồi
  file Capability đã tombstone.
- Chính sách đồng bộ xoá Drive (hai chiều) nằm trong plan; đổi cấu hình làm plan
  cũ mất hiệu lực (xem [18.3](#183-google-drive-trên-iis)).

### 7.3 Quy tắc theo aggregate

| Root | Xoá cùng root | Gỡ liên kết, giữ lại | Chặn xoá |
|---|---|---|---|
| Design Project / Operational Project | Dữ liệu sở hữu (gói công việc, cảnh báo vật tư, lịch thiết kế, link bản vẽ của task...) | CRM độc lập (Opportunity, Quote, Contract) cùng file local/Drive của chúng; chỉ metadata đồng bộ gắn dự án bị xoá | Mọi file phụ thuộc còn lại (local hoặc Drive), có link tới trang chi tiết nguồn; RFQ |
| Tender | Upload checklist dưới `/files/tenders` (quarantine + purge) | Tham chiếu tới Capability Document | File checklist ngoài thư viện nằm ngoài `/files/tenders` |
| Quote | File dưới `/files/quotes`; replica Drive đủ điều kiện | Opportunity tham chiếu | Bất kỳ Contract nào tham chiếu |
| Opportunity | Activities, translations | Contract, Survey, Lead đã convert, Tender thắng | Quote; continuation opportunity |
| Contract | Milestones, attachments, appendices, file dưới `/files/contracts/`, care history, replica Drive đủ điều kiện | Design Project liên kết | Đường dẫn không an toàn/dùng chung; Contract là hợp đồng đủ điều kiện cuối cùng của Opportunity Won; quan hệ continuation hai chiều; reminder đang `Sending` |
| Survey | Checklist results, site conditions | Binding thư mục Drive ngoài | Survey Media chưa xoá qua luồng file riêng |
| Capability Document | Versions, file dưới `/files/capability/` | — | Đường dẫn không an toàn/dùng chung; tham chiếu từ checklist Tender (kể cả lịch sử evidence) |
| Customer | Contacts, activities, documents, translations, file dưới `/files/customers/{customerId}/` | Link metadata của Lead đã convert và Project Document | Opportunity, Tender, Contract, Design Project, Operational Project |
| Vendor | File hồ sơ năng lực managed | — | Contract, Vendor Rating, Payment Request, file dùng chung/không an toàn, RFQ |
| Work package | Xem [9.2](#92-gói-công-việc-phòng-ban-department-work-package) | | |

Chi tiết thêm:

- Design/Operational Project không bao giờ tự xoá file phụ thuộc. Người dùng xoá
  bản ghi nguồn hoặc project document qua luồng có quyền của module đó; service
  đồng bộ hiện có sẽ xoá replica Drive an toàn. Sidecar xoá Drive thất bại vẫn
  là blocker; quản lý Operational Project có quyền có thể thử lại thủ công kể cả
  khi đã hết lượt tự động.
- Xoá Operational Project đưa cây thư mục dự án đã xác minh vào plan sau khi dọn
  file nguồn, kiểm kê cả item trong thùng rác. Replica trong thùng rác khớp
  instance, replica key và generation của tài liệu đã xoá được đưa vào plan
  trước thư mục cha; replica đã khôi phục khỏi thùng rác bị từ chối. Thư mục rỗng
  xoá trước root (kiểm lại rỗng khi thực thi). Nội dung không rõ, import, chia sẻ
  hoặc thư mục có file CRM độc lập chặn xoá. Drive lỗi thì root DB còn nguyên tới
  khi dọn xong. Xoá Design Project giữ thư mục dùng chung của Operational
  Project; binding Survey chưa migrate giữ hành vi unlink cũ.
- Sidecar project-document của Quote, `ContractAttachment`, `ContractAppendix`
  chỉ đủ điều kiện khi là binding CRM chính xác tới đường dẫn đã chuẩn hoá, sở
  hữu NICON ổn định, không conflict/processing lease, và hoặc đã sync đủ metadata
  sở hữu Drive, hoặc đã xoá xong không còn Drive file ID. Operation xoá replica
  bằng app property đã xác minh rồi giữ và kết thúc sidecar; finalizer không xếp
  hàng xoá Drive nền. Sidecar import, chia sẻ, mơ hồ, thiếu, không khớp, đang
  pending hoặc trùng định danh Drive chặn xoá.
- Scope quản lý Survey: `crm.surveys.manage.all`, người khảo sát được giao, người
  tạo survey, quản lý hoặc người tạo Operational Project.
- Huỷ convert Lead luôn giữ Customer, và chỉ xoá project mà lần convert đó mở
  khi impact plan của project chỉ còn đúng opportunity đó. Customer chỉ xoá được
  qua hợp đồng preview-and-confirm này.
- Resource có owner kiểm lại scope ở bước finalize.

### 7.4 Vòng đời durable operation

Trạng thái: `Preparing`, `Ready`, `Processing`, `Completed`, `Failed`,
`ManualActionRequired`.

1. Domain service kiểm quyền, xác nhận, concurrency, blocker và plan hiện tại
   rồi mới tạo operation.
2. Processor quarantine file local, rồi xoá vĩnh viễn các item Drive đã xác minh.
3. Resource handler đăng ký theo resource type chạy finalizer DB idempotent.
   Không dùng finalizer kiểu delegate vì không sống sót qua restart.
4. Processor purge quarantine, rồi ghi audit hoàn tất và đánh dấu `Completed`
   trong cùng một lần lưu.

Lỗi trước lần xoá Drive đầu tiên khôi phục file quarantine và có thể thử lại với
backoff thận trọng. Từ khi đã xoá Drive hoặc chạy finalizer, không rollback được
nữa: operation chỉ đi tiếp tới khi các bước idempotent còn lại xong. Sai sở hữu
hoặc hết lượt thử chuyển `ManualActionRequired`; operator xử lý blocker rồi chủ
động retry. Handler dùng operation ID làm idempotency key vì restart có thể xảy
ra sau commit DB nhưng trước khi item được đánh dấu xong.

### 7.5 Frontend

- Dùng dialog dùng chung `DeletionImpactDialog`: tải preview, hiển thị số lượng
  và ví dụ theo nhóm, khoá nút xoá khi còn blocker, bắt gõ đúng mã xác nhận,
  poll operation và làm mới list/detail sau khi xong. Không xoá aggregate bằng
  nhiều lệnh API song song theo từng dòng.
- Blocker nghiệp vụ nên kèm URL danh sách ADMIN đã lọc (khi có) và link chi tiết
  cho các ví dụ; dialog mở ở tab mới để giữ preview. Link danh sách chỉ hiện khi
  còn blocker không có link chi tiết. Link chỉ để điều hướng; quyền ở trang đích
  theo RBAC của module đó.
- Nhãn hiển thị và nhãn phụ thuộc là content translation do backend seed đủ bốn
  ngôn ngữ.
- Xoá hàng loạt bị tắt trên các trang root cho tới khi có hợp đồng preview-and-
  confirm theo lô ở server.

### 7.6 Kiểm thử và phạm vi hiện tại

Integration test bao phủ phân quyền HTTP, model binding, độ chính xác preview,
plan cũ, xung đột row version, blocker, persistence, unlink và trạng thái không
đổi khi bị từ chối. Browser test chỉ cho render dialog, gõ xác nhận, hiển thị
blocker và làm mới UI.

Đã dùng luồng durable và dialog polling: Design Project, Operational Project,
Lead, Customer, Tender, Quote, Opportunity, Contract, Survey, Vendor, Capability
Document. Các trang root khác phải được chuyển riêng trước khi coi hard delete
toàn repo hoàn tất. Quy tắc riêng cho hồ sơ thiết kế ở
[12.4](#124-xoá-hồ-sơ-thiết-kế), cho bản ghi con thi công ở
[13.1](#131-bố-cục-workspace-thi-công-frontend).

---

## 8. CRM, báo giá, đấu thầu và hợp đồng

Hầu hết controller có alias `/api/v1/...` cùng hành vi với `/api/...`.

### 8.1 Customer và Lead

| Method | Route | Permission | Ghi chú |
|---|---|---|---|
| `GET` | `/api/customers/{id}/documents` | `crm.customers.view` | Customer trong scope owner |
| `GET` | `/api/customers/{id}/documents/{documentId}/content` | `crm.customers.view` | Xem/tải |
| `POST` | `/api/customers/{id}/documents` | `crm.customers.manage` | PDF, Word, Excel, ảnh; tối đa 20 MB |
| `DELETE` | `/api/customers/{id}/documents/{documentId}` | `crm.customers.manage` | Xoá metadata và file managed |

Metadata lưu ở `customer_documents`, file ở thư mục riêng của Customer.

Gán người đại diện pháp luật qua upsert contact được tuần tự hoá theo từng
Customer: gate trong process cho một instance, SQL application lock thuộc
transaction cho nhiều instance. Nhờ đó filtered unique index và bất biến "Company
luôn có đúng một contact đại diện pháp luật" giữ đúng khi hai người cùng thay.

`GET /api/contracts/{id}/source-leads` cần cả `crm.contracts.view` và
`crm.leads.view`, cộng scope đọc Contract (Contract không truy cập được trả
`404`). Scope Lead kiểm riêng: không có `crm.leads.view.all` thì chỉ trả Lead
giao cho người gọi; quyền Contract/dự án không mở rộng quyền Lead. Response là
mảng `{ id, name, convertedAt }`: với Contract Upstream, lấy mọi Lead đã convert
có `ConvertedOpportunityId` = `Contract.OpportunityId`, `ConvertedCustomerId`
khớp và có `ConvertedAt`, sắp theo thời điểm convert rồi ID. Không chọn bừa một
Lead, không suy từ customer/project chung. Contract Downstream hoặc không có
Opportunity trả mảng rỗng. DTO không có thông tin liên hệ hay ghi chú nội bộ. Tab
thông tin Contract chỉ hiện mục này cho người có quyền xem Lead. Test:
`ContractSourceLeadsTests`, `admin-contract-source-leads.spec.ts`.

### 8.2 Báo giá

Tài liệu báo giá lưu ở `quote_documents`, file ở
`nihomebackend/wwwroot/files/quotes/{quoteId}/`, cùng giới hạn 20 MB và đuôi file
như Customer. Truy cập theo owner, `crm.quotes.view.all` cho xem chéo owner.

| Method | Route | Permission | Mục đích |
|---|---|---|---|
| `GET` | `/api/quotes/{id}/documents` | `crm.quotes.view` | Danh sách tài liệu |
| `GET` | `/api/quotes/{id}/documents/{documentId}/content` | `crm.quotes.view` | Xem/tải |
| `POST` | `/api/quotes/{id}/documents` | `crm.quotes.manage` | Upload; mỗi tệp tối đa theo `GET /api/site-settings/upload-limits/quotes` (mặc định 20 MB, 1–100 MB) |
| `DELETE` | `/api/quotes/{id}/documents/{documentId}` | `crm.quotes.manage` | Xoá |
| `GET` | `/api/opportunities?quoteEligibleOnly=true&search=...` | — | Picker tạo báo giá |
| `GET` | `/api/quotes/{id}/email-preview` | `crm.quotes.send` | Điền sẵn email liên hệ chính, subject, body |
| `POST` | `/api/quotes/{id}/send` | `crm.quotes.send` | Gửi kèm file `.xlsx` |
| `GET` | `/api/quotes/{id}/export.xlsx?lang=vi` | `crm.quotes.view` | Workbook báo giá `vi`/`en`/`zh`/`ja` |
| `GET` | `/api/quotes/{id}/approval-candidates` | — | Người duyệt hợp lệ |
| `POST` | `/api/quotes/{id}/approval-email-preview` | — | Preview email duyệt |
| `POST` | `/api/quotes/{id}/submit` | — | Draft → PendingApproval |
| `POST` | `/api/quotes/{id}/approve`, `/reject-internal` | `crm.quotes.approve` | Chỉ người duyệt được chọn |
| `GET` | `/api/quotes/{id}/project-boq` | `crm.quotes.view` + scope | BOQ dự án đã duyệt mới nhất cho Sales |

Picker: server áp search và owner scope trước khi phân trang; loại Opportunity
Lost và Opportunity Won đã có báo giá thắng. Picker lấy tối đa 50 dòng mỗi lần
tìm và tải riêng Opportunity được chọn sẵn từ URL.

Tạo báo giá: `POST /api/quotes` nhận `draftOnly: true` cho luồng Sales hiện tại.
Opportunity phải thuộc một Operational Project; payload chỉ có field cơ bản, không
có `method`; tạo Draft giá trị 0. Lần cập nhật giá đầu tiên qua
`PUT /api/quotes/{id}` bắt buộc chọn `method` và một input price offer đã duyệt,
còn hạn, cùng dự án (riêng phương thức BOQ xem [10.6](#106-báo-giá-boq)). Hợp đồng
tạo kèm giá cũ vẫn giữ cho tích hợp và test lịch sử. Sales không tạo báo giá cho
Opportunity của owner khác. Gửi lại `Idempotency-Key` trả response tạo ban đầu.

Duyệt nội bộ (một bước, một Trưởng phòng Cung ứng `PROCUREMENT_MANAGER`; theo ghi
chú 30/10/2026 và xác nhận 31/10/2026):

- `approval-candidates` trả user đang hoạt động có role đó, có
  `crm.quotes.approve`, `crm.quotes.view`, email hợp lệ và khác người gọi.
- `approval-email-preview` nhận `approverUserId` và `rowVersion`, trả To,
  subject/HTML đã render, các CC hợp lệ và `previewToken` 15 phút. CC là thành
  viên đang hoạt động của dự án vận hành có quyền xem báo giá (owner hoặc
  `crm.quotes.view.all`).
- `submit` cần cùng approver, row version và token; server render và kiểm lại báo
  giá, người nhận, CC, template, token rồi đổi trạng thái và xếp hàng dòng
  `quote_approval_emails` trong một lần lưu. Chỉ người duyệt đó (không phải người
  trình) gọi `approve`/`reject-internal`.
- Email quyết định ghim người trình ở `QuoteApprovalEmail.RecipientUserId`; ngay
  trước khi gửi kiểm lại tài khoản, email hiện tại và quyền xem báo giá, đổi thì
  chuyển `NeedsReview`. Migration `20261107113000_PinQuoteDecisionRecipient`:
  quyết định cũ chưa xác minh được người nhận chuyển `NeedsReview`; nên xả hàng
  chờ trước khi deploy; rollback xoá cột và không tự xếp lại email.
- Báo giá đang chờ duyệt mà chưa có approver cần đối soát nghiệp vụ thủ công.

Gửi cho khách: `send` cần `toEmail`, `subject`, `body`, `languageCode`,
`rowVersion`. Chỉ `crm.quotes.manage` được sửa subject/body; người gửi có quyền
được đổi sang địa chỉ hợp lệ khác (hệ thống không xác minh chủ hộp thư). Body
phải là HTML an toàn; dialog preview trong iframe cô lập. Gửi thành công mới ghi
`SentToCustomer`; SMTP lỗi giữ `Approved`. Request chỉ đổi trạng thái không gửi
email. Chưa có outbox bền cho luồng này: gửi xong mà gặp xung đột trạng thái thì
phải đối soát tay. Điều kiện Finance trước khi gửi ở
[9.3](#93-kiểm-soát-liên-phòng-trước-hợp-đồng). Workbook xuất có ô ngày/số đúng
kiểu; báo giá Draft hoặc Pending ghi là sơ bộ.

### 8.3 Đấu thầu

Mỗi Tender mới bắt buộc có `OperationalProjectId` cùng Customer. Tender ghi thời
điểm nhận thư mời (bắt buộc, trước hạn nộp), nguồn, người nhận, mã tham chiếu
khách (tuỳ chọn), và URL cổng mời thầu hoặc tài liệu mời thầu upload. Mỗi file
upload lưu kích thước, media type, SHA-256.

Checklist hồ sơ:

- Bảy dòng chuẩn (bản vẽ thầu mặc định là tuỳ chọn), không xoá được. Có thể thêm
  dòng riêng của gói, đánh dấu bắt buộc theo thư mời, hoặc đặt dòng tuỳ chọn là
  `NotApplicable` kèm lý do.
- Chỉ dòng `TemplateCode=capability` gắn Capability Document có sẵn (bắt buộc
  `CapabilityDocumentId`, từ chối upload trực tiếp); dòng khác chỉ nhận upload
  trực tiếp vào `wwwroot/files/tenders/`. Lúc nộp kiểm lại bất biến này.
- Mỗi dòng trỏ tới revision bằng chứng hiện tại và giữ mọi revision cũ. Thêm bằng
  chứng mới reset xác minh của dòng.
- `crm.tenders.manage` giao việc cho user đang hoạt động có role cũng có quyền
  đó; đổi người nhận phát một notification `tender.checklist.assigned` (giữ
  nguyên thì không phát). Hạn nội bộ theo ngày, không trước hôm nay, không sau hạn
  nộp thầu (service kiểm, không chỉ form).
- Dòng chỉ sang `Done`/`Submitted` khi có bằng chứng. `Done` ghi người chuẩn bị;
  một người khác chuyển từng dòng bắt buộc sang `Submitted` như bước xác minh độc
  lập. Gate nộp thầu kiểm lại xác minh, loại bằng chứng và metadata file.
- Capability Document hoặc version còn được dòng hiện tại hay lịch sử bằng chứng
  tham chiếu thì không xoá được.
- Upload trực tiếp thất bại (Tender/dòng không còn, terminal, lỗi lưu, copy dở)
  thì xoá file vừa ghi.
- Sau `Submitted` vẫn làm tiếp checklist cho lần nộp bổ sung; `Won`, `Lost`,
  `Cancelled` khoá hoàn toàn.

Kế hoạch dự thầu dùng `DepartmentWorkPackage` với `SourceTenderId`,
`Purpose=TenderPlanning`, mỗi phòng chủ trì một gói (quy tắc ở
[9.3](#93-kiểm-soát-liên-phòng-trước-hợp-đồng)). Gói chỉ `Completed` khi mọi task
`Verified`; Tender chỉ `Submitted` khi có ít nhất một gói không huỷ và mọi gói như
vậy đã hoàn thành. Đây là gate server cho lập kế hoạch, chuẩn bị hồ sơ và rà
soát nội bộ; checklist vẫn là danh mục file/hồ sơ chuẩn. Cập nhật kế hoạch giữ
ngày bắt đầu/kết thúc, hạn nộp và khoảng ngày của task không huỷ. Kết quả `Won`,
`Lost`, `Cancelled` khoá sửa/mở lại gói, giao task, tiến độ và xác minh qua API
work-package chung; capability flag trả read-only. Dữ liệu cũ `Purpose=General`
có `SourceTenderId` vẫn đọc và qua gate được, ghi mới phải dùng `TenderPlanning`.

Nộp thầu tạo snapshot `TenderSubmission` đánh số, bất biến: revision và checksum
bằng chứng, nội dung dự toán đã duyệt, kế hoạch đã hoàn thành (metadata, task
WBS/nội dung/người/ngày/tiến độ/xác minh/CC/phụ thuộc; snapshot kế hoạch
`SchemaVersion` 1 còn có nguồn lực, từ 2 thì không vì trường đã bị xoá), Finance review
đã duyệt (checklist, phát hiện, quyết định, người, phiên bản concurrency), kênh
nộp, mã biên nhận, người và thời điểm. Field snapshot chỉ cộng thêm, trừ khi trường
nguồn bị xoá hẳn thì tăng `SchemaVersion`; snapshot cũ
thưa không backfill từ dữ liệu hiện tại. `SubmittedAt` đầu tiên bất biến. Backfill
chỉ tạo snapshot `LegacyUnknown` khi có audit `tender.transition` thành công
chứng minh lần Submitted (dùng thời điểm audit); không suy từ `UpdatedAt`.

| Method | Route | Permission | Mục đích |
|---|---|---|---|
| `GET` | `/api/tenders/assignee-options` | `crm.tenders.manage` | User có thể nhận việc checklist |
| `POST` | `/api/tenders/{id}/checklist` | `crm.tenders.manage` | Thêm dòng riêng khi Preparing |
| `DELETE` | `/api/tenders/{id}/checklist/{itemId}` | `crm.tenders.manage` | Xoá dòng riêng còn trống |
| `PATCH` | `/api/tenders/{id}/checklist/{itemId}` | `crm.tenders.manage` | Trạng thái, người làm, hạn nội bộ |
| `POST` | `/api/tenders/{id}/checklist/{itemId}/upload` | `crm.tenders.manage` | Upload thay file hiện tại |
| `POST` | `/api/tenders/{id}/checklist/attach-from-library` | `crm.tenders.manage` | Gắn Capability Document |
| `GET` | `/api/tenders/{id}/checklist/{itemId}/content` | `crm.tenders.view` | Xem/tải file của dòng |
| `POST` | `/api/tenders/{id}/invitation-documents` | `crm.tenders.manage` | Upload tài liệu mời thầu có checksum |
| `GET` | `/api/tenders/{id}/readiness` | `crm.tenders.view` | Blocker kế hoạch, hồ sơ, dự toán, Finance; ghi mốc đạt lần đầu |
| `POST` | `/api/tenders/{id}/submit` | `crm.tenders.submit` | Bắt buộc idempotency key, kênh, concurrency token, mọi gate |
| `POST` | `/api/tenders/{id}/transition` (`Submitted`) | `crm.tenders.submit` | Trả `400`; mọi client phải dùng `/submit` |
| `POST` | `/api/tenders/{id}/mark-won` | `crm.tenders.mark-result` | Xem dưới |

`mark-won` nhận hoặc `opportunityId` (của cùng Customer) hoặc
`createOpportunity: true`, không được cả hai (`400`). Tạo mới thì trong cùng
transaction mở Opportunity ở `Negotiation`, giá trị bằng `GrandBidTotal` của
revision dự toán đã duyệt mới nhất, trên Operational Project sẵn có của Tender;
không tạo root dự án thứ hai. Tạo hợp đồng vẫn là thao tác riêng sau đàm phán.

Thống kê tỷ lệ trúng thầu: `GET /api/tenders/statistics`,
`/api/tenders/report-customers`, `/api/tenders/report-preparers`
(`crm.tenders.view`; hai route sau trả lựa chọn đã dùng, kể cả người chuẩn bị cũ,
không cần quyền quản lý user).

- `fromDate`/`toDate` tuỳ chọn nhưng phải đi cặp, đúng thứ tự, ISO date, tính trọn
  ngày theo giờ Việt Nam (UTC+7); `fromDate` sau `0001-01-01`. `customerId`,
  `preparerUserId` là số nguyên dương. Sai thì `400`. List nhận cùng bộ lọc và
  `submittedOnly=true` để đối soát.
- Cohort: `TenderReporting.FirstSubmissions` chọn `TenderSubmission.Sequence == 1`
  trong kỳ (UTC half-open), mỗi Tender một lần, kể cả `LegacyUnknown`.
  `winRate = 100 * Won / cohort`, làm tròn 2 chữ số, null khi rỗng. Đang chờ kết
  quả và huỷ sau khi nộp vẫn ở mẫu số; huỷ trước khi nộp thì không. Bộ lọc
  status/search/phân trang của list không đổi mẫu số. Dùng trạng thái hiện tại nên
  cohort cũ có thể đổi khi có kết quả; KPI dùng cùng selector, snapshot đã khoá
  không đổi.
- Response: `submittedCount`, `wonCount`, `lostCount`, `pendingCount`,
  `cancelledCount`, `winRate`, `calculatedAt` (UTC), `missingSubmissionCount`
  (bản ghi Submitted/Won/Lost hoặc huỷ có `SubmittedAt` nhưng thiếu sự kiện nộp
  đầu, không gán kỳ được; loại ra và công bố, không bịa ngày).
- Test: `TenderStatisticsControllerTests`, `SampleCrmDataSeederTests`,
  `admin-tender-statistics.spec.ts`.

### 8.4 Hợp đồng: tạo, nguồn và trạng thái

- Hợp đồng mới luôn `Draft`; create/update từ chối mọi cách vượt state machine.
  Đổi trạng thái chỉ qua `POST /api/contracts/{id}/transition`.
- Draft → Signed cần attachment `SignedScan` của chính hợp đồng đó; thiếu trả
  `400` `CONTRACT_SIGNED_SCAN_REQUIRED` và không đổi gì. Attachment Supporting hoặc
  scan của hợp đồng khác không tính. Hợp đồng Signed cũ không có scan không bị hạ
  cấp. Signed trở đi cần ngày ký, ngày bắt đầu không trước ngày ký, ngày kết thúc
  không trước ngày bắt đầu.
- Contract Upstream thiết kế/thi công/D&B không thể Signed → InProgress qua
  transition chung (trả `LEGAL_ACTIVATION_REQUIRED`); xem
  [12.5](#125-kích-hoạt-pháp-lý-và-permit).
- `OwnerUserId` lấy từ `OwnerUserId` của Customer; owner chỉ định rõ (có quyền) được
  ưu tiên; Customer chưa có owner thì dùng người gọi. Sales không tạo hoặc chuyển
  hợp đồng sang scope khách hàng của người khác.
- Opportunity và Quote phải cùng Customer; Quote phải thuộc Opportunity đã chọn.
  Request có Quote mà thiếu `OpportunityId` thì suy Opportunity từ Quote trước khi
  validate; Opportunity mâu thuẫn bị từ chối. Bỏ Opportunity mà giữ Quote thì
  Opportunity của Quote vẫn được giữ; muốn gỡ cả hai phải xoá cả hai tham chiếu.
  Không gỡ được hợp đồng đủ điều kiện cuối cùng của Opportunity đã ký.
- Hai đường tạo hợp đồng Upstream đều hợp lệ (xác nhận 16/10): từ Quote đã duyệt,
  hoặc từ Opportunity có giá trị đã thoả thuận, không cần Quote. Dự toán của Tender
  thắng không thay cho báo giá CRM. Hợp đồng Downstream không tham chiếu báo giá
  CRM.
- `POST /api/contracts/{id}/link-quote` (`crm.contracts.manage`, idempotent, có
  concurrency) gắn Quote `Approved`, `SentToCustomer` hoặc `CustomerApproved` vào
  hợp đồng Upstream chưa huỷ/hoàn thành, chưa có Quote nguồn. Quote phải khớp
  Customer, Opportunity, dự án và chưa được hợp đồng còn hiệu lực nào dùng;
  Opportunity trống của hợp đồng được điền từ Quote. Tạo hoặc đổi Quote cũng theo
  quy tắc này; `Approved`/`SentToCustomer` còn phải trong thời hạn hiệu lực.
- Hợp đồng Upstream có Quote nguồn chỉ được ký khi có Finance commercial review đã
  duyệt cho đúng phiên bản Quote hiện tại ([9.3](#93-kiểm-soát-liên-phòng-trước-hợp-đồng)).
- Kiểm dữ liệu cũ (chỉ đọc, chạy trong container backend/database đúng môi trường,
  review trước khi sửa; không suy quan hệ theo tên):

```sql
SELECT c.Id AS ContractId, c.QuoteId, c.OpportunityId AS ContractOpportunityId,
       q.OpportunityId AS QuoteOpportunityId
FROM contracts AS c JOIN quotes AS q ON q.Id = c.QuoteId
WHERE c.OpportunityId IS NULL OR c.OpportunityId <> q.OpportunityId;
```

### 8.5 Hợp đồng: danh sách, phạm vi đọc và Finance

`GET /api/contracts` (`crm.contracts.view`) nhận `status`, `direction`, `type`,
`vendorId`, `ownerUserId`, `customerId`, `operationalProjectId`, `search`,
`signedFrom`, `signedTo`, `endFrom`, `endTo`, `valueMin`, `valueMax`, `page`,
`pageSize`, `sortBy` (`signedDate` mặc định, `endDate`, `value`,
`contractNumber`, `updatedAt`; giá trị lạ quay về signedDate), `sortDirection`
(mặc định giảm dần). Lọc và sắp theo giá trị hiện tại (`Value` + delta VO đã
duyệt). Response có tổng danh mục, số mốc quá hạn/sắp đến hạn, mốc chưa thanh toán
kế tiếp và số tiền còn lại theo lịch của từng dòng; Upstream là phải thu,
Downstream là phải trả. List/detail có `sourceTenderId`, `sourceTenderCode`,
`sourceTenderName` suy từ `WonOpportunityId` của Tender thắng (nhiều Tender thì
lấy cái đóng gần nhất). `GET /api/contracts/filter-options` trả đủ lựa chọn owner/
customer/dự án trong scope; `GET /api/contracts/export-data` dùng cùng bộ lọc,
sắp xếp, scope trong một truy vấn cho CSV.

Phạm vi đọc: không có `crm.contracts.view.all` thì theo owner. Kế toán có
`crm.contracts.view.upstream.all` và `crm.contracts.view.downstream.all` (chỉ đọc)
để đối soát phải thu/phải trả trên cùng danh mục dự án của
`operations.projects.view.all`. Cần cả quyền dự án và quyền theo hướng tương ứng,
và chỉ có hiệu lực khi list lọc rõ `direction=Upstream`/`Downstream` hoặc detail
đúng hướng đó; list không lọc hướng vẫn theo owner. Các quyền này không cho sửa.

Sổ hợp đồng chuẩn và mọi thao tác sửa ở `/admin/contracts`. Finance có hai màn chỉ
đọc trên cùng ID: `/admin/finance/contracts` (Upstream) và
`/admin/finance/input-contracts` (Downstream); chọn dòng mở
`/admin/contracts/{id}` kèm `returnTo` đã validate. URL Finance `/{id}` cũ
redirect. Header, trạng thái pháp lý, attachment, VO do `crm.contracts.manage`.
`PUT /api/contracts/{id}/milestones` (chỉ field lịch) và
`PATCH /api/contracts/{id}/milestones/{milestoneId}/status` (kế toán phụ trách,
trạng thái yêu cầu, ngày thu/chi thực tế) nhận `crm.contracts.manage` hoặc
`finance.payments.manage`, server kiểm lại hướng và scope dự án; quyền Finance
không nâng quyền header.

Mốc thanh toán:

- Cập nhật lịch giữ ID mốc, sửa tại chỗ để giữ tham chiếu tài chính và lịch sử.
  Mốc đã xử lý hoặc đã có payment request không xoá được qua sửa lịch.
- `ActualPaymentDate` là ngày nghiệp vụ người dùng xác nhận (trả về `actualDate`).
  Ghi `Paid` bắt buộc ngày này; về `Pending`/`Requested` thì xoá. Dòng cũ để null,
  không backfill.
- Worker 5 phút phát `crm.contract.milestone-due` một lần khi mốc thu Upstream
  chưa thanh toán đến hạn, gửi owner và kế toán phụ trách, lưu
  `DueNotificationSentAt` nguyên tử và xoá dấu này khi đổi ngày đến hạn.

Payment Request (`GET /api/finance/payment-references`,
`GET /api/finance/payment-requests` và các thao tác create, update, submit,
validate, decide, pay, cancel) theo scope Operational Project qua
`IProjectAccessService`; mọi thao tác ghi kiểm lại dự án thật của Contract. Quyền
finance có scope cần là thành viên dự án (hoặc PM/người tạo);
`operations.projects.view.all` mở toàn danh mục.

- Có thể gắn một QS batch Downstream đã CM duyệt, còn hiệu lực, cùng Contract và
  dự án qua `ConstructionQsBatchId`; kiểm lại ở create/update, validate và
  approve. Không gắn QS vẫn hợp lệ (tạm ứng, giai đoạn chưa chốt chứng từ); việc
  gắn QS không chứng nhận bộ hồ sơ ISO hay cho phép chi.
- Sửa Draft qua `PUT /finance/payment-requests/{id}` với row version hiện tại.
  So ngày hoá đơn với ngày của `ReceivedAt` theo `Asia/Ho_Chi_Minh`.
- `POST /finance/payment-requests/{id}/revise` (`finance.payments.manage`, lý do,
  idempotency key, row version) cho yêu cầu submitted, rejected hoặc approved chưa
  chi. Snapshot revision cũ và attachment, tăng số revision trên cùng request, giữ
  hoá đơn, xoá người review/duyệt cũ, về Draft; phải qua lại submit, kiểm kế toán và
  duyệt độc lập. Endpoint pay kiểm QS trong transaction serializable và chặn khi QS
  đã bị thay thế. Ràng buộc unique vendor/hoá đơn vẫn toàn cục. Migration
  `AddPaymentRequestRevisions` backfill revision 1 và từ chối rollback khi đã có
  lịch sử revision.
- Người tạo, trình, kiểm tra hoặc được giao làm kế toán của request không được
  duyệt, kể cả có `finance.payments.approve` (theo ISO `TQT` 5.1 và các vị trí ký
  riêng trên MH-M04). Chưa chốt ma trận chứng từ bốn loại gói và uỷ quyền duyệt.

### 8.6 Hợp đồng: tiến độ thực hiện, nhắc hạn, chăm sóc, nối tiếp

**Tiến độ thực hiện.** `GET /api/contracts/{id}/execution-progress`
(`crm.contracts.view` + scope đọc Contract; không truy cập được trả `404`) trả ba
thước đo độc lập `work`, `qs`, `payment` và `calculatedAt`; cố ý không có phần
trăm tổng.

- `work`: null nếu thiếu `work.packages.view`; dùng quy tắc hiển thị của list gói
  công việc, chỉ gói có `ContractId` = hợp đồng. Đếm gói (huỷ tách riêng), task
  (loại gói huỷ; Completed, Verified, Cancelled tách riêng và không chồng nhau),
  xem trước 10 gói cập nhật gần nhất. Không phải tiến độ có trọng số WBS.
- `qs`: null nếu thiếu `construction.qs.view` hoặc quyền vào dự án. Chỉ hồ sơ
  khớp Contract, dự án và hướng. Tổng lịch sử, số chờ CM duyệt và số đang duyệt
  hiệu lực (CmApproved, Active, lineage còn hiệu lực, không phải Reversal) tách
  riêng; không cộng số tiền, không suy tỷ lệ hoàn thành. Link mở workspace QS đã
  lọc theo Contract và neo hồ sơ nguồn.
- `payment`: theo lịch mốc như trang chi tiết; mỗi dòng
  `Contract.Value * PercentValue / 100` làm tròn. Không phải sổ tiền mặt, không
  gồm điều chỉnh VO.
- Null (bị giới hạn quyền) khác số 0 có quyền. Test:
  `ContractExecutionProgressTests`, `admin-contract-execution-progress.spec.ts`.

**Nhắc gia hạn/kết thúc** (lịch riêng, khác notification mốc thanh toán):

| Method | Route | Điều kiện |
|---|---|---|
| `GET` | `/api/contracts/{id}/reminders` | Scope xem Contract; 100 bản ghi mới nhất |
| `POST` | `/api/contracts/{id}/reminders/preview` | Quản lý Contract + owner/view-all; `{kind: "Renewal"|"End", reminderDate: "yyyy-MM-dd", contactId?}` |
| `POST` | `/api/contracts/{id}/reminders` | Như trên; `{previewToken}` |
| `POST` | `/api/contracts/{id}/reminders/{reminderId}/cancel` | Như trên; chỉ huỷ được `Scheduled` |
| `GET/PUT` | `/api/site-settings/contract-reminder-templates` | `system.settings.view/manage` |

- Ngày nhắc theo lịch Việt Nam, từ hôm nay trở đi. Preview cần hợp đồng Upstream ở
  Signed/InProgress/OnHold, có ngày kết thúc và email contact khách hợp lệ (tối đa
  254 ký tự); gợi ý contact chính trước. Token gắn actor/hợp đồng/người nhận/ngày/
  nội dung, 15 phút.
- Gửi lại token đã chấp nhận trả bản ghi có sẵn; mỗi hợp đồng/loại chỉ một nhắc
  `Scheduled`/`Sending`. Đặt lịch, huỷ và claim dùng transaction serializable.
  Lịch đã lưu giữ nội dung đã duyệt khi template đổi.
- `ContractReminderWorker` chạy 5 phút một lần, kể cả nhắc quá hạn sau downtime.
  Trước SMTP kiểm lại điều kiện hợp đồng, khách, số HĐ/ngày kết thúc/email, tài
  khoản và quyền người đặt lịch, xoá aggregate đang chờ. Đổi thì `NeedsReview`.
  Ghi `Sending` trước SMTP; sau khi SMTP nhận thì lưu `Sent` cùng một mục chăm sóc
  khách hàng loại Email. Lỗi SMTP: `NeedsReview`/`DELIVERY_UNCERTAIN`. Khi đối soát
  tay, dừng sender, giữ snapshot, thời điểm thử và audit. Huỷ không thu hồi email đã
  giao cho SMTP.
- `ContractReminder.AttemptedAt` khác null thì khoá vĩnh viễn hướng hợp đồng
  (`CONTRACT_REMINDER_DIRECTION_LOCKED`). Xoá vĩnh viễn công bố lịch/lịch sử và
  chặn khi có `Sending`.
- Migration `20261020162316_AddContractReminderEmails` thêm `contract_reminders` và
  bốn cột template; rollback bị chặn khi đã có lịch/lịch sử hoặc template đã sửa.
- Test: `ContractReminderServiceTests`, `ContractRemindersTests`,
  `e2e/smoke/contract-reminder-email.spec.ts` (cần `MAIL_SINK_URL`, xem
  [17.3](#173-e2e-gửi-email-qua-hộp-thư-giả-lập)).

**Lịch sử chăm sóc khách hàng** (`Nicon_BreakTask_v1.xlsx`, `Nicon!A62:D62`):
thuộc đúng một hợp đồng Upstream, khác audit hợp đồng và lịch sử hoạt động của
Customer.

- `GET /api/contracts/{id}/care-activities?page=1&pageSize=20`
  (`crm.contracts.view` + scope đọc) trả `total`, `page`, `pageSize`, `items`, mới
  nhất trước; pageSize kẹp 1–100. Scope đọc của dự án/Finance không cho ghi.
- `POST /api/contracts/{id}/care-activities` cần `crm.contracts.manage`, owner hoặc
  `crm.contracts.view.all`, và `Idempotency-Key`. Body: `type` (`Call`, `Email`,
  `Meeting`, `Note`), `content` (1–4.000 ký tự, trim), `occurredAt` tuỳ chọn (chuẩn
  hoá UTC, mặc định giờ server). Downstream trả `400`. Chỉ thêm, không đổi điều
  khoản/VO/mốc/thanh toán, không đặt lịch nhắc.
- Theo xác nhận của người dùng ngày 20/10/2026: đã có lịch sử chăm sóc thì không
  đổi hướng hợp đồng (`400` `CONTRACT_CARE_DIRECTION_LOCKED`).
- Migration `AddContractCareActivities` không backfill (ghi chú cấp Customer không
  gán được cho một hợp đồng); rollback bị chặn khi bảng có dữ liệu. Xoá hợp đồng
  công bố và xoá các mục này; thêm mục làm plan cũ mất hiệu lực. Mẫu:
  `HD-SAMPLE-003`. Test: `ContractCareActivitiesTests`,
  `admin-contract-care.spec.ts`.

**Hợp đồng nối tiếp (gia hạn/mở rộng).** Chính sách do người dùng uỷ quyền ngày
22/10/2026 (quyết định triển khai, không phải xác nhận trong họp NICON): Renewal và Expansion từ hợp đồng Upstream có ngày ký và trạng
thái Signed/InProgress/OnHold/Completed.

- Migration `20261021171128_AddContractContinuations` thêm `SourceContractId`,
  `ContinuationKind` (20), `ContinuationReason` (2000) cho Contract và Opportunity;
  FK chặn xoá nguồn. Không suy quan hệ cho dữ liệu cũ. Rollback từ chối khi đã có
  quan hệ.
- Route tạo có sẵn nhận thêm field nối tiếp và `sourceContractRowVersion`. Cần quyền
  xem hợp đồng + owner/view-all trên nguồn (độc lập quyền Opportunity); tạo từ
  Opportunity còn cần quyền xem Opportunity. Kind bắt buộc, lý do 1–2.000 ký tự,
  nguồn cùng Customer. Hợp đồng mới luôn Draft, kế thừa nguồn từ Opportunity nối
  tiếp; Opportunity Lost không sinh hợp đồng. Không copy bằng chứng ký, thanh toán,
  VO, QS, chăm sóc, nhắc hạn. Không đổi nguồn qua update/link-quote; quan hệ chặn
  đổi Customer/hướng của hợp đồng và Customer của Opportunity nối tiếp.
- Tạo nối tiếp bắt buộc `Idempotency-Key`, transaction serializable; replay kiểm
  lại quyền xem nguồn.
- `GET /api/contracts/{id}/continuations` trả hợp đồng nguồn/kế tiếp và Opportunity
  kế tiếp mà người gọi xem được (kiểm quyền từng module, mỗi nhóm tối đa 100), không
  lộ tên hay tổng số bị ẩn.
- Xoá: quan hệ nối tiếp hai chiều và Opportunity nối tiếp là Block; không cascade.
- Demo `HD-SAMPLE-CONT-001` (định danh theo số hợp đồng, không theo tên pursuit).
  Test: `ContractContinuationsTests`, `ContractsControllerTests`,
  `ContractQuoteLinkTests`, `OpportunitiesControllerTests`,
  `contract-continuations.spec.ts`.

---

## 9. Dự án vận hành và gói công việc

### 9.1 API dự án vận hành

`OperationalProject` là root nghiệp vụ dùng chung cho mọi module; không nhầm với dự án
website hay `DesignProject`. Route `/api/operational-projects` (và `/api/v1`):

| Method | Path (tương đối) | Permission | Mục đích |
|---|---|---|---|
| `GET` | `/`, `/{id}`, `/{id}/timeline` | `operations.projects.view` | List theo scope; chi tiết (ETag); mốc thanh toán của các hợp đồng (chỉ đọc) |
| `GET`/`POST`/`PUT` | `/code-suggestion`, `/`, `/{id}` | `operations.projects.manage` | Mã đề xuất; tạo dự án Planning (`PJ-YYYY-NNNN` hoặc `code` tuỳ chọn); sửa metadata/vòng đời (không sửa mã) |
| `PATCH`/`POST` | `/{id}/code`, `/{id}/code/drive-retry` | `operations.projects.rename` | Đổi mã kèm lý do và lịch sử; thử lại đổi tên thư mục Drive |
| `DELETE` | `/{id}` | `operations.projects.manage` | Theo [mục 7](#7-xoá-vĩnh-viễn-aggregate-permanent-aggregate-deletion) |
| `GET` | `/{id}/team/history` | xem dự án | Sự kiện đội ngũ và lịch sử đổi mã |
| `GET`/`POST`/`PUT` | `/{id}/master-schedule`, `.../revisions[/{revisionId}]`, `/submit`, `/review`, `/email-preview` | thành viên xem; `operations.projects.manage` soạn/trình; người duyệt được giao duyệt | Tiến độ tổng (WBS) |
| `GET`/`POST` | `/{id}/master-schedule/emails/needs-review`, `.../{emailId}/resolve` | quản lý dự án được giao | Xử lý email tiến độ gửi lỗi |
| `GET` | `/document-categories`, `/{id}/documents`, `.../{documentId}/content` | `operations.projects.view` | Danh mục, catalog, tải file riêng tư |
| `POST`/`DELETE` | `/{id}/documents` (≤ 100 MiB), `.../retry`, `/classify`, `/resolve-conflict`, `/{documentId}` | `operations.projects.manage` | Upload, thử lại đồng bộ, phân loại file từ Drive, giữ cả hai phiên bản, xoá |

**Mã dự án.** Mã `PJ-`, `TD-`, `SV-` lấy số lớn nhất đang dùng cộng một
(`SequentialCodes`), không đếm dòng; cấp mã dự án dùng SQL application lock theo năm
(`OperationalProjectCodeAllocator`). `code` khi tạo được viết hoa, dài 2–40 ký tự an toàn,
không trùng. Đổi mã sau đó cần lý do và row version, ghi lịch sử cũ/mới bất biến; liên kết
giữa module không đổi vì dùng `OperationalProjectId`; thư mục Drive gốc được đổi tên (giữ
Drive ID), trạng thái ở `latestCodeChange`. Lịch sử mã có `entityType=ProjectCode`,
`action=Renamed`; client định danh mục lịch sử bằng cả entity type và ID. Migration
`AddProjectCodeAuditAndDesignVendors` (rollback mất lịch sử đổi mã) cũng gỡ các quyền nhạy
cảm mặc định khỏi baseline nhóm `FINANCE`.

**Vòng đời.** Không có archive/restore dự án (DEC-08); `Completed`/`Cancelled` giữ lịch sử,
gỡ dự án chỉ qua xoá vĩnh viễn. Archive cấp tài liệu (như As-Built) không archive dự án.

**WBS và tiến độ tổng.** `NodeId` là khoá ổn định qua các revision; `WbsCode` là mã hiển
thị (bắt buộc, ≤ 80 ký tự, không trùng trong revision). UI gợi ý mã theo vị trí cây; mã task
gói công việc cũng duy nhất trong gói. NICON chưa có mẫu đánh số WBS chung (D-03): không áp
regex hay template cố định. Resource request, QA/QC, QS lưu `projectScheduleNodeId` thuộc
revision đã ban hành mới nhất; server lấy mã từ revision, bỏ mã client gửi; không ban hành
revision bỏ node đang được dùng. Dữ liệu cũ chỉ được link khi mã khớp đúng một node.

Email trình/ban hành tiến độ tổng xếp hàng ở `project_schedule_emails` theo
[6.3](#63-email-ứng-dụng) (preview `action=Submit&recipientUserId=...` hoặc
`action=Publish`; `Reject` không cần). Khác các email khác, worker 15 giây tự thử lại sau
1, 2, 4, 8 phút; sau 5 lần thì `NeedsReview` và báo quản lý dự án, người này kiểm với người
nhận rồi ghi `ConfirmReceived` hoặc `Retry` (quyết định trùng trả `409`). Admin xem hàng
đợi chỉ đọc tại `GET /api/site-settings/email-templates/project-schedule/needs-review`
(`system.settings.view`, không lộ địa chỉ hay nội dung).

**Chuyển sang Thiết kế trước hợp đồng.** `POST /api/opportunities/{id}/design-project`
(`name` 1–300, `note` ≤ 4.000; cần `crm.opportunities.manage` và
`operations.projects.manage`) tạo Design Project giai đoạn Concept, không cần hợp đồng.
Từ chối Opportunity Lost hoặc chưa có dự án; gọi lại idempotent (response có `created`).

**Nhiều luồng thiết kế.** Một dự án có nhiều Design Project; một hợp đồng có tối đa một.
Field số ít cũ trả luồng cũ nhất; đọc hợp đồng không mượn luồng của hợp đồng khác.

- Đội ngũ tách theo luồng (thành viên gốc có `DesignProjectId = null`). Mỗi luồng có một
  Design Manager và một Design Lead, đồng bộ với `ProjectManagerUserId` và
  `DesignLeadUserId`; PM của dự án là role gốc riêng.
- Quyền hồ sơ từng thành viên: `PUT .../team/members/{memberId}/design-permissions`
  (`canView`, `canCreate`, `canUpdate`, `canDelete`; chỉ quản lý đội đổi được; không hạn
  chế được Design Manager/Lead chính). Duyệt, phát hành và xoá vẫn là gate riêng.
- Pháp lý chỉ giao cho thành viên gốc (`LEGAL_MEMBER_INVALID`); trách nhiệm thiết kế của
  Punch thuộc luồng của Punch (`PUNCH_DESIGN_MEMBER_INVALID`).
- Giao việc, duyệt, CC mới cần cả membership (`isActive`) và tài khoản (`isUserActive`)
  đang hoạt động. Picker user phân trang tối đa 100; `departmentId` âm trả `400`
  `PEOPLE_DEPARTMENT_FILTER_INVALID`.

**Ranh giới `DesignProjectId`.** Task thi công, nhật ký, Punch, nghiệm thu, hoàn công, bàn
giao, permit còn lưu `DesignProjectId`; HSE, Cung ứng, RFQ, Tài chính, tài liệu dự án lưu
`OperationalProjectId`. Aggregate liên module mới phải dùng `OperationalProjectId`; API cũ
phải xác định đúng một dự án trước khi phát sự kiện liên module. Không thêm khoá dự án thứ
hai khi chưa có backfill, quy tắc unique, chính sách xung đột, rehearsal và rollback.

**Tài liệu dự án và Drive.** `ProjectDocument` là catalog và sidecar đồng bộ; SQL quyết
định catalog, quyền và trạng thái.

- Upload tay lưu bản host riêng tư và bản ghi `Pending` (kể cả khi Drive tắt); worker đẩy
  lên Drive, đọc lại so byte và SHA-256 rồi mới xoá bản host. Lệch thì giữ bản host.
- File của module nguồn (báo giá, hợp đồng, Basic Design, Shop Drawing, permit, nghiệm thu,
  hoàn công, bàn giao, khảo sát) stage sidecar cùng transaction với bản ghi gốc và chỉ gỡ
  được ở module nguồn. File lịch sử chưa có sidecar không được tự tìm.
- Đổi dự án của Opportunity, Contract, Design Project, Survey chuyển file sang dự án mới và
  xếp hàng xoá replica cũ. Survey luôn phải có dự án.
- Quyền đọc theo category của module sở hữu (thiết kế `design.*.view`, pháp lý
  `permit.checklists.view`, khảo sát `crm.surveys.view`, tiền thiết kế
  `crm.opportunities.view`, tài chính `finance.payments.view`...) cộng quyền module nguồn;
  file chưa phân loại không mở cho thành viên.
- Drive là nguồn chuẩn cho file đã đồng bộ và file tạo thẳng trong thư mục managed: sửa từ
  xa được phản ánh, file vào thùng rác thì đánh dấu xoá. Sửa đồng thời trên sidecar nguồn cũ
  tạo mục xung đột tới khi người có quyền chọn **Keep both**.
- Thư mục Concept/Basic/Shop được khởi tạo nền sau khi chuyển giao, không gọi Drive trong
  transaction nghiệp vụ.
- Đây là tập con, chưa hoàn tất hợp đồng Drive toàn khách hàng; phải kiểm trên môi trường
  thật, mock không đủ.

### 9.2 Gói công việc phòng ban (Department work-package)

`DepartmentWorkPackage` là aggregate giao việc liên phòng, route `/api/work-packages`
(và `/api/v1`).

- Gói Project cần `OperationalProjectId`. `kind` trả về là `Contract` khi có
  `ContractId`, ngược lại `Project` (không cần migration). Request `Contract` mới cần
  hợp đồng cùng dự án; request `Project` cũ có `ContractId` vẫn nhận và trả
  `Contract`; filter `kind=Project` loại gói có liên kết. Gói General nhắm mọi phòng
  đang hoạt động (product owner, 29/10/2026); không tạo vỏ dự án trùng. Gói Internal
  từ chối mọi tham chiếu root nghiệp vụ; tạo gói Internal cần cả
  `work.packages.manage` và `work.packages.assign`, người gọi phải là lead được chọn
  trong cùng role group. Gói Internal lưu mọi role group tham gia; mỗi task có một role
  group phụ trách và kiểm assignee/approver theo nhóm đó.
- Subtask (`department_work_tasks.ParentTaskId`, migration `AddWorkTaskSubtasks`):
  cha chưa kết thúc, cùng gói, không đổi cha sau khi tạo. Trạng thái và tiến độ cha
  là rollup của con trực tiếp; tiến độ gói chỉ tính task lá. Task cha không nhận tiến
  độ/xác minh trực tiếp, không có KPI. Request cũ bỏ `parentTaskId` không gỡ liên kết.
- Phụ thuộc trong cùng gói, không vòng; bắt đầu tiến độ cần gói Active và task trước đã
  xong. Tiến độ người được giao đơn điệu tăng. Gói và lịch sử snapshot commit cùng
  transaction.
- Gói Completed về `Active` qua PUT: xoá `CompletedAt`, ghi snapshot `Reopened`, giữ
  tiến độ/xác minh. Cancelled là trạng thái cuối.
- File đính kèm task (`AddWorkTaskAttachments`): kiểm 20 MB và đuôi như tài liệu
  nghiệp vụ, API không trả đường dẫn public. Xoá dọn file host trước rồi mới ẩn bản
  ghi; dọn thất bại thì thử lại được và chặn plan xoá gói. Chưa stage lên Drive. Backup
  thư mục `wwwroot/files/business-documents/work-tasks` cùng database.
- KPI `VerifiedWorkTaskCount` chỉ đếm task được xác minh độc lập, giao cho người được
  đánh giá, trong kỳ. Định nghĩa seed mặc định inactive.

| Method | Path | Permission | Mục đích |
|---|---|---|---|
| `GET` | `/api/work-packages` | `work.packages.view` | List có scope, lọc ở server |
| `GET` | `/api/work-packages/options` | `view` + `manage` hoặc `assign` | Phòng, user, KPI công việc |
| `GET` | `/api/work-packages/{id}`, `/{id}/history` | `work.packages.view` | Chi tiết, access flags, ETag; lịch sử bất biến |
| `POST`/`PUT` | `/api/work-packages`, `/{id}` | `work.packages.manage` | Tạo/sửa gói, hợp đồng tuỳ chọn, vòng đời |
| `GET`/`DELETE` | `/{id}/deletion-impact`, `/{id}` | `manage` + scope quản lý | Xoá cứng với mã, planToken, rowVersion, idempotency key |
| `POST`/`PUT` | `/{id}/tasks`, `/{id}/tasks/{taskId}` | `work.packages.assign` | Thêm/sửa task (WBS, phụ thuộc, CC, KPI) chưa kết thúc |
| `GET`/`POST`/`DELETE` | `/{id}/tasks/{taskId}/attachments[/{attachmentId}[/content]]` | `view` + scope gói (upload/xoá: lead/assignee; lead dọn được gói đã đóng) | Một file multipart `file` mỗi request kèm `Idempotency-Key` |
| `PUT` | `/{id}/tasks/{taskId}/progress` | `work.packages.progress` | Ghi tiến độ |
| `POST` | `/{id}/tasks/{taskId}/verify` | `work.packages.verify` | Xác minh độc lập task KPI đã xong |
| `POST`/`PUT` | `/{id}/input-price-offers[/{offerId}]` | `work.packages.progress` | Thêm/sửa offer giá đầu vào (Draft/Rejected) |
| `POST` | `/{id}/input-price-offers/{offerId}/approval-email-preview`, `/{id}/finance-review/approval-email-preview` | `progress` + scope phòng | Preview email duyệt, token 15 phút |
| `POST` | `/{id}/input-price-offers/{offerId}/submit`, `/{id}/finance-review/submit` | `work.packages.progress` | Trình duyệt độc lập |
| `POST` | `/{id}/input-price-offers/{offerId}/decision`, `/{id}/finance-review/decision` | `work.packages.verify` | Duyệt/từ chối |
| `PUT` | `/{id}/finance-review` | `work.packages.progress` | Lưu checklist Tài chính – Kế toán |
| (xem [10.4](#104-boq-trong-gói-giá-đầu-vào)) | `/{id}/boq-workbooks` | | BOQ trong gói giá đầu vào |

Email duyệt (quyết định của người dùng 25/10/2026; quy tắc chung ở
[6.3](#63-email-ứng-dụng)):

- Trình offer giá đầu vào hoặc Finance review bắt buộc preview; client cũ phải thêm
  bước này. Trình lại bản ghi đã Submitted trả bản ghi, không xếp email mới.
- Lead phòng được giao phải đang hoạt động, khác tác giả, có `work.packages.verify`
  và email hợp lệ theo `ContactValidation`; nếu không thì chặn trình.
- Preview nhận `{ toEmail, ccEmails }` tuỳ chọn; rỗng thì To là lead, CC rỗng. Mỗi
  địa chỉ trim, tối đa 150 ký tự, một mailbox; CC tối đa 20, bỏ trùng và bỏ địa chỉ
  trùng To. Đổi To chỉ đổi nơi nhận, không đổi người duyệt. Danh sách chuẩn hoá được
  gắn vào token; submit không ghi đè được.
- Migration `20261024174814_AddWorkPackageApprovalEmails` (bảng
  `work_package_approval_emails`, cột template) và
  `20261024183305_AddWorkPackageEmailRecipients` (`CcEmailsJson` mặc định `[]`,
  `LeadEmail` backfill từ `ToEmail`). Worker 15 giây kiểm lại lead theo `LeadEmail`
  và generation trình, gửi một message có To và CC. Detail trả `approvalEmails` (50
  dòng mới nhất) và `ccEmails`; UI poll trạng thái tối đa 2 phút. `Sending` chặn xoá
  vĩnh viễn. Bản ghi Submitted cũ không được gửi bù.
- Rollback: dừng worker trước khi deploy assembly cũ; export hàng chờ, lịch sử gửi và
  danh sách người nhận vì `Down` xoá chúng.

Xoá gói: tính lại plan trong transaction serializable, xoá task, CC/phụ thuộc, người
tham gia, lịch sử, offer/dòng, Finance review, translations. Checklist permit/tender
độc lập được giữ và gỡ link task; Project, Contract, Opportunity, Quote, Tender giữ
nguyên; BOQ của gói được giữ và gỡ link. Chặn khi: giá đầu vào đã dùng (kể cả snapshot
phiên bản báo giá), báo giá đã gửi dùng commercial review, lần nộp thầu dùng Finance/
kế hoạch, kích hoạt pháp lý của hợp đồng, file nguồn còn lại. API này không xoá file
local hay Drive. Thành công trả `204`. Xoá Operational Project coi gói là `Delete`;
xoá Contract/Opportunity coi tham chiếu là `Unlink`.

### 9.3 Kiểm soát liên phòng trước hợp đồng

`DepartmentWorkPackage.Purpose` chọn luồng; mỗi purpose gắn một phòng nhận và một
bản ghi nguồn (migration `AddPrecontractDepartmentControls`, `Purpose` mặc định
`General`). Mọi purpose trước hợp đồng từ chối `ContractId`.

| Purpose | Phòng | Nguồn | Ghi chú |
|---|---|---|---|
| `ProcurementInputPricing` | `PROCUREMENT` | `SourceOpportunityId` | Chứa `ProcurementInputPriceOffer` (dòng, vendor, mã tham chiếu NCC, tiền tệ, hiệu lực) và BOQ upload |
| `FinanceCommercialReview` | `FINANCE` | `SourceQuoteId` | Ghim `ReviewedQuoteVersion` khi lưu |
| `FinanceTenderReview` | `FINANCE` | `SourceTenderId` | |
| `TenderPlanning` | role group phụ trách đang hoạt động | `SourceTenderId` + dự án của Tender | Ngày gói không quá hạn nộp; mỗi Tender/phòng một gói không huỷ; mọi task cần người xác minh độc lập, KPI tuỳ chọn |

Offer và Finance review dùng chung vòng đời `Draft → Submitted → Approved | Rejected`;
chỉ sửa ở Draft/Rejected, từ chối cần lý do, tác giả không tự quyết
(`CreatedByUserId == callerUserId` bị từ chối), offer đã được báo giá tham chiếu là
bất biến.

Gate ở server:

- Gửi báo giá cho khách (`QuoteService`) cần `FinanceCommercialReview` `Approved`
  trong gói không huỷ có `SourceQuoteId`, và báo giá phải có `OperationalProjectId`.
  Báo giá lên phiên bản mới sau khi sửa trọng yếu cần review mới. Response Finance
  review (kể cả lồng trong `GET /api/work-packages`) có `reviewedQuoteVersion`;
  package có `sourceQuoteVersion`. UI hợp đồng chọn hồ sơ theo phiên bản
  (`selectQuoteFinanceReview`), không theo thời gian cập nhật.
- Ký hợp đồng Upstream có Quote (`ContractService`): cùng điều kiện review cho phiên
  bản hiện tại, áp ở tạo đã ký, update, transition và gắn Quote vào hợp đồng đã ký.
- Gắn `inputPriceOfferId` vào báo giá chỉ khi offer `Approved`, còn hạn, thuộc gói
  `ProcurementInputPricing` cùng dự án; báo giá lưu `InputPriceSnapshotJson`.
- Nộp thầu (`TenderService`): có `OperationalProjectId`, ít nhất một gói
  `TenderPlanning` đã xong và `FinanceTenderReview` `Approved`.
- Tạo/sửa BOQ thi công (`ProcurementService`) cần hợp đồng Upstream cùng dự án đã ký,
  không Draft/Cancelled.

Editor giá đầu vào: nhãn gắn với field, desktop 5 cột, mobile xếp dọc, thân dialog
cuộn riêng; thành tiền chỉ để xem (server làm tròn và tính thuế). Khoảng giá trị theo
`InputPriceLineRequest`. `MoneyInput` nhận formatter tuỳ chọn và dấu phẩy thập phân.
Test: `quote-pricing-responsive-live.spec.ts` (320/390/768/1440px).

---

## 10. BOQ

Năm loại BOQ không thay thế nhau:

| Hiện vật | Aggregate | Giới hạn được MR/kho? |
|---|---|---|
| BOQ báo giá sơ bộ | `Quote`/`QuoteItem` hoặc nguồn material-rate đã duyệt | Không |
| Dự toán thầu | `TenderEstimateRevision` | Không |
| Khối lượng thiết kế | Bằng chứng Design (aggregate riêng còn là Target) | Không, trừ khi được nâng thành revision thi công đã duyệt |
| BOQ thi công | `ProjectBoqRevision`/`ProjectBoqLine` trạng thái `Approved` | Có |
| BOQ quyết toán | Revision thi công đã duyệt cuối cùng sau khi dự án hoàn thành và VO đã duyệt | Báo cáo, quyết toán, KPI |

Mọi import hay nâng cấp ghi dự án, hiện vật/phiên bản nguồn, hash file, người import,
thời điểm, kết quả preview và người duyệt. Không suy ra hạn mức tài chính/kho chỉ vì
file Excel có chữ `BOQ` hay đến từ Module 2.

### 10.1 BOQ dự án (Procurement BOQ)

Revision thuộc một Operational Project, mã vật tư duy nhất không phân biệt hoa thường,
vòng đời `Draft → Submitted → Approved | Rejected`; chỉ Draft/Rejected sửa được (sửa
Rejected đưa về Draft). Approved bất biến vì MR và dòng hợp đồng dùng làm bằng chứng
hạn mức. Thành tiền dòng = khối lượng duyệt × đơn giá dự toán, làm tròn 4 chữ số
(away from zero); tổng lưu ở revision. Trình báo PM; quyết định báo người lập.

Route `/api/operational-projects/{projectId}/procurement/` (quyền `proc.boq.*` tương
ứng, kiểm scope dự án trước):

| Method | Route | Permission | Mục đích |
|---|---|---|---|
| `GET` | `boq-revisions`, `boq-revisions/{id}`, `boq-revisions/export` | `proc.boq.view` | List, chi tiết, CSV |
| `POST`/`PUT` | `boq-revisions`, `boq-revisions/{id}` | `proc.boq.manage` | Tạo Draft kế tiếp; thay nội dung Draft/Rejected (dòng từ workbook: `workbookReadOnly`) |
| `POST` | `boq-revisions/{id}/submit`, `/decision` | `proc.boq.manage` / quyết định | Trình (thiếu giá: `missingPrices`); duyệt/từ chối |
| `GET` | `boq-templates`, `boq-templates/{code}/file` | `proc.boq.view` | Danh sách mẫu, file mẫu |
| `POST` | `boq-workbooks/preview` | `proc.boq.manage` | Multipart `templateCode`, `file`; tóm tắt theo sheet và `sourceHash`; không lưu |
| `POST` | `boq-workbooks` | `proc.boq.manage` | `templateCode`, `file`, `sourceHash`, `upstreamContractId` tuỳ chọn; tạo Draft |
| `GET` | `boq-revisions/{id}/workbook` | `proc.boq.view` | File đã upload |
| `POST` | `boq-revisions/{id}/contract` | `proc.boq.approve` | `upstreamContractId`, `rowVersion`; chuyển BOQ thầu đã duyệt thành BOQ hợp đồng |

UI: `/admin/procurement-control`, chi tiết
`/admin/procurement-control/projects/{projectId}/boq/{boqId}`. Từ 13/11/2026 upload
trên UI chỉ ở gói giá đầu vào (10.4); `ProjectBoqSection` (`#project-boq`) trên trang
dự án chỉ đọc, ngoại trừ nút **Upload contract BOQ** cho người có `proc.boq.manage` +
`crm.contracts.view` khi dự án có hợp đồng Upstream đã ký. QS có `proc.boq.view` và
`proc.boq.manage` (`20261108100000_GrantQsProjectBoqPermissions`).

BOQ thầu và BOQ hợp đồng:

- Revision upload không có `upstreamContractId` là BOQ thầu; duyệt đặt
  `TenderApproved`, nằm ngoài mọi truy vấn `Approved`, nên không bao giờ thành hạn mức
  MR, RFQ, kho hay QS. Revision gắn hợp đồng đã ký đi theo `Approved` bình thường.
- Từ 14/11/2026 (MoM 31/10: "trúng thầu thì chuyển thành BOQ hợp đồng, không tạo file
  mới") `ConvertTenderBoqToContractAsync` gắn revision `TenderApproved` mới nhất với
  hợp đồng Upstream đã ký và đặt `Approved` tại chỗ, giữ số revision, file, dòng và
  thông tin duyệt thầu; audit `proc.boq.contract`. Lỗi `boqWorkbook.error.{code}`:
  `notTender`, `notLatestTender`, `contractBoqExists` (dự án đã có BOQ `Approved`;
  thay đổi sau là revision upload mới), `contract`. Gọi lại cùng hợp đồng trả revision.
  Cảnh báo vật tư được đánh giá lại. Nút **Make it the contract BOQ** trên
  `BoqRevisionDetailPage` cho người có `proc.boq.approve` (PM, BGD). Revision có
  `sourceWorkPackageId` ẩn nút trình/quyết định vì được review trong gói.
- Migration `AddProjectBoqWorkbooks` thêm cột nullable, `project_boq_lines.PriceMissing`
  và bảng `project_boq_sheets`; `Down` làm revision mất danh sách sheet và link file
  (export file trước).

### 10.2 Workbook mẫu và parser

Mỗi mẫu là bản đã làm sạch của một workbook khách hàng trong `Data/BoqTemplates/`
(embedded, liệt kê ở `templates.json`). Download trả đúng từng byte, giữ sheet, cột,
merge, style và công thức. File gốc trong `docs/Nicon/BOQ/` chỉ là bằng chứng, không
được đọc lúc chạy.

| Mã | Nội dung | Sheet đọc |
|---|---|---|
| `PCCC-CAN-HO` | PCCC căn hộ | `ptct` |
| `THIET-KE-XIN-PHEP` | Khảo sát/thiết kế và phí xin phép | `Bao gia` từ dòng 29 tới `Tổng cộng trước VAT (B+C)` |
| `NHA-XUONG` | BOQ nhà xưởng GNF, 23 sheet | 20 sheet hạng mục; bỏ cover, `Cover 1`, `00.Tổng Hợp` |
| `XAY-DUNG-ME` | Xây dựng, M&E, PCCC nhà xưởng, 18 sheet | 17 sheet; bỏ `TỔNG HỢP` |
| `TRON-GOI` | Trọn gói thiết kế, xin phép, thi công, 21 sheet | 18 sheet; bỏ `Cover `, `Total `, `Summary ` |
| `CSA-WORK` | Civil/structural/architectural, 6 sheet | 3 bill; bỏ `01.BÌA`, `Total Amount `, ` COST SUMMARY` |

Profile mẫu khai báo sheet, ô header kỳ vọng, dòng đầu, cột STT/mô tả/đơn vị/khối
lượng/đơn giá/thành tiền, tiền tố dòng cộng phụ, nhãn tổng, tiền tệ và dung sai. Tuỳ
chọn: `ignoredSheets`; `descriptionFirstLine` (chỉ lấy dòng đầu làm tên);
`totalCell` (tổng nằm trên các dòng) với `totalDecimals` (số chữ số ROUND, ví dụ GNF
C4 là -5); `itemsWithoutStt` (dòng là hạng mục khi có đơn giá hoặc thành tiền và có
đơn vị hoặc khối lượng); `extraPriceColumns` (đơn giá = tổng các cột vật tư, nhân
công, máy); `measurementRows` và `measurementColumn` (dòng diễn giải kích thước dưới
hạng mục không phải hạng mục, khối lượng không đọc, có giá thì báo `sttRequired`);
`specColumn` (nối cột quy cách vào tên). Nhãn tổng cũng được tìm ở cột STT.

`ProjectBoqWorkbookParser` đọc từ dòng đầu tới dòng tổng: STT số nguyên là hạng mục,
dòng bắt đầu bằng tiền tố cộng phụ là subtotal, dòng chữ khác là nhóm. Hạng mục không
có khối lượng bị bỏ qua (đếm riêng). Dòng giữ thành tiền workbook đã lưu khi khớp
khối lượng × đơn giá. Mã hạng mục `S{sheet:00}-R{row:0000}`, đổi khi chèn dòng nên
không ổn định giữa các revision. Chỉ đọc kết quả công thức Excel đã lưu, không tính
lại. Từ chối trả `boqWorkbook.error.invalidRows` với `issueCount` và tối đa 200
`issues` (`code`, `messageKey`, `sheet`, `cell`, `limit`): sheet/header, ô số, số âm
hoặc quá lớn, lỗi Excel, công thức chưa tính, khối lượng trên dòng không có STT,
thành tiền lệch khối lượng × đơn giá (ô "-" tính là 0), tổng lệch tổng hạng mục (theo
dung sai). Đơn vị trống chỉ đếm cảnh báo cho tới khi NICON chốt. Giới hạn upload:
20 MB, 500 phần ZIP, 200 MB giải nén; từ chối macro, external link, data connection.
File lưu `BusinessDocumentArea.Boq`, `SourceWorkbookPath` nằm trong
`ReferencedManagedFiles`; xác nhận lại cùng file cho cùng Draft trả revision có sẵn.
Test: `ProjectBoqWorkbookParserTests`, `ProjectBoqWorkbooksControllerTests`,
`boq-workbook-upload.spec.ts`, `contract-boq.spec.ts`. Workbook ME quá nặng cho
ClosedXML tính lại, nên test ghi kết quả thẳng vào XML (`BoqWorkbookPatch`).

### 10.3 Thư viện mẫu

`/admin/material-rates/boq` hiển thị `BoqTemplateLibrary`, cây chỉ đọc mẫu → sheet.
`BoqTemplatesController` (`/api/boq-templates`, `crm.material-rates.view`, không theo
dự án) trả danh sách mẫu kèm sheet và số hạng mục, file, và outline một sheet
(`ProjectBoqWorkbookParser.Outline`, cùng quy tắc với `Parse`, bỏ header lặp; cache
trong `ProjectBoqTemplateCatalog.Outline`). Migration
`20261113100000_GrantProcurementBoqTemplateLibrary` cấp `crm.material-rates.view` cho
role Cung ứng và Trưởng phòng Cung ứng còn thiếu.

### 10.4 BOQ trong gói giá đầu vào

`WorkPackageBoqWorkbooksController` (`/api/work-packages/{id}/boq-workbooks`) preview,
import, download, trình và quyết định BOQ trong gói `ProcurementInputPricing`. Upload
và trình cần `work.packages.progress` + thuộc phòng của gói; download cần
`work.packages.view` + thấy gói; quyết định cần `work.packages.verify`, do lead của gói
hoặc admin khác người upload. Import dùng lại `ProjectBoqWorkbookService`, lưu
`ProjectBoqRevision.SourceWorkPackageId` (`AddWorkPackageBoqSource`). Trình dùng
`WorkPackageApprovalEmailService` với record type `ProjectBoqRevision`; duyệt đặt
`TenderApproved`. Procurement control từ chối trình/quyết định các revision này
(`boqWorkbook.error.workPackageReview`). Detail gói trả `boqWorkbooks`.

### 10.5 Material-rate catalog (API còn giữ)

Catalog phân biệt `InvestmentRate` và `Boq` (dữ liệu cũ là `InvestmentRate`). Báo giá
theo đơn giá chỉ nhận revision Investment-rate `Approved` đang hiệu lực.

- Từ 14/11/2026 không màn nào gọi API nhập workbook catalog
  (`/api/material-rate-catalogs/{catalogId}/revisions/{revisionId}/boq-workbook`
  preview/import, `/api/material-rate-catalogs/boq-import-templates`) và nhập theo
  category cấu hình (`PUT {id}/boq-configuration`,
  `GET {id}/configured-boq-template?language=vi`, `POST {id}/configured-boq-preview`,
  `PUT {id}/revisions/{revisionId}/configured-boq-rows`,
  `POST {id}/revisions/{revisionId}/configured-boq-import`). API và bảng giữ tới khi
  có migration gỡ. Các API này cần `crm.material-rates.manage` (template download cần
  `.view`), chỉ sửa revision `Draft`, so `sourceHash`/`configurationHash`/
  `expectedUpdatedAt` (cũ trả `409`). Category cấu hình (`BoqConfigurationJson`,
  `BoqRowsJson`, migration `AddConfiguredBoqEntries`) không dùng được đường xoá cũ
  (deactivate thay thế); `Down` xoá cấu hình và dữ liệu tham chiếu, export trước.
  Giới hạn schema và workbook do `BoqConfigurationRules` và `MaterialRateService`
  thực thi.
- Người có `crm.material-rates:manage` tạo/sửa/xoá dòng trong revision `Boq` Draft
  (trim, mã không trùng, khối lượng 4 chữ số, đơn giá 2 chữ số, thành tiền làm tròn 4
  chữ số away from zero); mỗi thao tác trả revision đã tính lại. Field thập phân trả
  dạng chuỗi JSON; client giữ chuỗi khi sửa, chỉ đổi số tại ranh giới rõ ràng.
- Quyền đó gồm cả xoá vĩnh viễn catalog (revision và dòng là một aggregate). Bị
  `409` `materialRates.catalog.deleteBlocked` khi báo giá hoặc snapshot phiên bản báo
  giá tham chiếu revision. Xoá hàng loạt best effort: ID lỗi vẫn được chọn kèm lý do.
- `NormalizeBoqCatalogReferenceSource` chuẩn hoá scale và đối soát tổng báo giá cũ;
  `Down` không khôi phục được độ chính xác đã bỏ.

### 10.6 Báo giá BOQ

Từ 08/11/2026 báo giá BOQ copy dòng từ `ProjectBoqRevision` đã duyệt mới nhất của dự
án (`Approved` hoặc `TenderApproved`, số revision cao nhất). Create/update nhận
`projectBoqRevisionId`, từ chối `materialRateCatalogId` và `items`; mỗi
`ProjectBoqLine` copy mô tả, đơn vị, khối lượng, đơn giá, thành tiền làm tròn 2 chữ
số; lưu `Quote.ProjectBoqRevisionId` (snapshot cũng giữ). Đổi revision là thay đổi
trọng yếu (phiên bản mới nếu đã duyệt). Trình bị từ chối (trước khi xếp email) khi
chưa có BOQ dự án hoặc đã có revision duyệt mới hơn. Draft chưa có offer đã duyệt vẫn
chọn được phương thức BOQ khi trỏ tới BOQ dự án đã duyệt mới nhất (`hasApprovedInput`).
Migration `AddQuoteProjectBoqSource` mở rộng `quote_items` (mã 80, tên 500, đơn vị
50, khối lượng 18,6, đơn giá 18,4). Báo giá trước thay đổi này dùng
`MaterialRateRevisionId`, `PricingEffectiveDate`, `CatalogReference`.

Công thức (server tạo và cập nhật, `src/lib/quoteTotals.ts` mô phỏng để preview): thành
tiền = khối lượng × đơn giá; subtotal = tổng thành tiền đã làm tròn; chiết khấu trước
VAT; tổng làm tròn 2 chữ số away from zero. Server từ chối dòng thiếu, tên/đơn vị
trống, khối lượng ≤ 0, giá âm, phần trăm ngoài 0–100, giá trị vượt `decimal(18,*)`.
Snapshot phiên bản giữ bộ dòng nguồn sau khi sửa.

Dải tiêu thụ BOQ mục tiêu của PRD (85% vàng, 95% cam, >100% đỏ/chặn) phải cấu hình
được và tính ở server; cho tới khi hợp đồng tính (hạn mức, khối lượng ròng, hoàn, quy
đổi đơn vị, độ chính xác, hạn mức 0) được duyệt, giữ kiểm tra cứng từ chối MR vượt hạn
mức còn lại.

---

## 11. Cung ứng: nhà cung cấp, MR, kho, cảnh báo, RFQ, IFC

### 11.1 Nhà cung cấp

Bảng `procurement_vendors` lưu nhà cung cấp và thầu phụ. Mã vendor được trim và viết
hoa; mã và tên công ty đã chuẩn hoá là duy nhất (trùng trả `409`). Cần ít nhất một số
điện thoại hoặc email hợp lệ. `IsActive=false` giữ vendor cho báo cáo lịch sử nhưng
chặn tạo hợp đồng mới. Chi tiết trả hợp đồng, dự án, đánh giá vendor, người cập nhật
và audit (`vendor.create`, `vendor.update`).

| Method | Route | Permission | Mục đích |
|---|---|---|---|
| `GET` | `/api/vendors`, `/api/vendors/{id}` | `proc.vendors.view` | Tìm, lọc, sắp, phân trang (tối đa 100/trang); chi tiết |
| `POST`/`PUT` | `/api/vendors`, `/api/vendors/{id}` | `proc.vendors.manage` | Tạo (active), sửa hồ sơ hoặc trạng thái |
| `GET`/`DELETE` | `/api/vendors/{id}/deletion-impact`, `/api/vendors/{id}` | `proc.vendors.manage` | Xoá vĩnh viễn theo [mục 7](#7-xoá-vĩnh-viễn-aggregate-permanent-aggregate-deletion) |

`proc.vendors.export` chỉ điều khiển nút xuất ở frontend, không tự cấp quyền đọc API.
Upload hồ sơ năng lực trả claim token mờ; create/update dùng file đó phải gửi đúng
token và server claim file trong cùng lần ghi Vendor. Form huỷ thì bỏ token; worker
xoá upload chưa claim quá 24 giờ. Upload đã claim không huỷ được và không vendor khác
claim được. UI: `/admin/vendors`, `/admin/vendors/:id`; CSV lấy mọi trang khớp bộ lọc
(chưa có endpoint export riêng).

### 11.2 Material Request (MR)

`GET /api/operational-projects/{projectId}/procurement/material-requests`
(`proc.material-requests.view`; ngoài scope dự án trả `404`) nhận `search`, `status`,
`siteRequesterUserId`, `responsibleSiteUserId`, `assignedProcurementUserId`,
`requiredFrom`, `requiredTo`, `sortBy`, `sortDirection`, `page`, `pageSize` (≤100);
khoảng ngày ngược bị từ chối. Mỗi dòng có khối lượng yêu cầu, đã nhận ròng (phiếu
nhập posted và hoàn), khối lượng BOQ duyệt và còn lại (tính theo các trạng thái đã
cam kết Approved, PartiallyFulfilled, Fulfilled). `currentApprovedBoq` chỉ trả số
revision, định danh vật tư, đơn vị, khối lượng duyệt và còn lại, không có đơn giá hay
tổng, để người chỉ có quyền MR chọn đúng hạng mục mà không cần `proc.boq.view`. Chi
tiết `/{id}` trả dấu thời gian/người theo vòng đời, bằng chứng hạn mức và ngữ cảnh
Customer/Project/Contract tối thiểu (không có giá trị hợp đồng).

- Vòng đời `Draft → Submitted → Approved | Rejected`; Approved chuyển
  `PartiallyFulfilled`/`Fulfilled` chỉ qua phiếu nhập kho posted. Chỉ người yêu cầu
  sửa Draft. Bản ghi chưa kết thúc huỷ được với lý do ≥ 3 ký tự.
- Create/update từ chối hạng mục trùng, ngày cần trong quá khứ, user ngoài dự án,
  người được giao không thuộc Cung ứng, revision BOQ cũ, khối lượng ≤ 0 và vượt hạn mức
  còn lại. Duyệt kiểm lại hạn mức trong transaction serializable.
- Trình báo người phụ trách cung ứng, quyết định báo người yêu cầu, huỷ báo bên còn
  lại. UI chi tiết:
  `/admin/procurement-control/projects/{projectId}/material-requests/{requestId}`.

### 11.3 Nhập/xuất kho

Tồn kho là sổ suy ra trong một Operational Project. `WarehouseReceipt` ghi vật tư đã
kiểm theo dòng MR đã duyệt; `WarehouseIssue` cấp cho người phụ trách công trường kèm mã
hạng mục tuỳ chọn. Phiếu posted bất biến; sửa bằng phiếu hoàn trỏ về phiếu gốc.

- `GET .../procurement/warehouse-transactions` (`proc.warehouse.view`): tìm, loại
  nhập/xuất, trạng thái, người phụ trách, khoảng ngày, sắp, phân trang; kèm tồn theo
  BOQ duyệt hiện tại (đã nhập, đã xuất, tồn sau hoàn).
- Draft sửa bằng `PUT` idempotent có row version; `POST .../post` tác động tồn;
  `POST .../reverse` cần lý do ≥ 3 ký tự. Audit ghi ID phiếu thật.
- Ghi kho tuần tự hoá bằng SQL application lock thuộc transaction (đánh đổi: ghi kho
  tuần tự giữa các dự án), giữ kiểm tồn Serializable; list đọc với ReadCommitted.
- Luồng kho đang online-first. Ghi nhận offline và tự đồng bộ cần app shell cache,
  hàng đợi client bền, ID offline ổn định, chính sách xung đột và quan sát được; chưa
  có. Không coi browser storage thông thường là hỗ trợ offline.

### 11.4 Cảnh báo vật tư

Projection bền từ BOQ duyệt, nhu cầu MR đã duyệt và sổ kho posted; người dùng không
tạo hay tự đóng cảnh báo. So theo mã vật tư (không phân biệt hoa thường):

- `OverBoq`: xuất ròng vượt hạn mức BOQ; chênh = xuất − hạn mức; mức Critical.
- `Shortage`: nhu cầu đã duyệt chưa xuất vượt tồn; chênh = nhu cầu − đã nhận; Critical
  khi tồn ≤ 0, còn lại Warning.

Vòng đời `Open → Acknowledged → Resolved`. Người cung ứng được giao hoặc PM xác nhận
cảnh báo Open với ghi chú 3–2.000 ký tự (không xoá chênh lệch). Resolved tự động khi
dữ liệu hết vi phạm, tự mở lại khi vi phạm quay lại. Mỗi sự kiện ghi
`MaterialAlertEvent`. Duyệt BOQ, duyệt/huỷ MR, post/hoàn phiếu kho đánh giá lại sau
commit (best effort).

| Method | Route (dưới `.../procurement/material-alerts`) | Permission |
|---|---|---|
| `GET` | `/`, `/{id}` | `proc.material-alerts.view` |
| `POST` | `/evaluate` (đối soát idempotent), `/{id}/acknowledge` | `proc.material-alerts.manage` |

PM và Cung ứng xem và quản lý; Kho xem; BGD xem qua pattern view. Response không có
đơn giá BOQ, giá thương lượng hay giá trị hợp đồng. Báo người được giao và PM (không
trùng nếu là một người). Xoá dự án xoá cảnh báo và lịch sử.

<a id="718-rfq-and-supplier-comparison"></a>

### 11.5 RFQ và so sánh báo giá nhà cung cấp

Nguồn: NIH-166, NIH-174/175/176 (kèm ghi chú nghiệm thu 7/9) và
`Nicon_BreakTask_v1.xlsx` (danh sách, tạo/sửa và so sánh RFQ).

Hợp đồng nghiệp vụ:

- RFQ thuộc một Operational Project và giữ Customer của dự án. Cung ứng chọn dòng và
  khối lượng từ Project BOQ VND đã duyệt (không vượt dòng nguồn). Mời một hoặc nhiều
  vendor đang hoạt động; danh mục vendor là master data dùng chung, lời mời và báo giá
  thuộc RFQ.
- Giá thấp nhất hợp lệ (quy VND) được tô sáng, kể cả đồng hạng; vendor inactive vẫn
  hiện giá như bằng chứng nhưng không được tô. Trọng số giá, thời gian giao, điểm
  đánh giá vendor đã duyệt và điểm thương mại chấm tay cộng đúng 100%. Điểm chỉ để
  tham khảo; chọn dưới điểm cao nhất cần lý do.
- Award theo lô là nguyên tử: phân bổ từng dòng và khối lượng cho một hoặc nhiều báo
  giá hợp lệ, mỗi khối lượng RFQ phải được phủ chính xác. Với Supply, phân bổ thủ công
  vào nhu cầu MR đã duyệt chưa đáp ứng của cùng dòng BOQ (không vượt); Subcontract
  không dùng MR. Mỗi vendor được chọn sinh một hợp đồng **Draft Downstream
  Supply/Subcontract**; award không ký hợp đồng. Phân bổ RFQ-MR là giữ chỗ mua hàng,
  không đổi trạng thái MR (chỉ phiếu nhập posted mới làm vậy).
- Giá trị, khách hàng, dự án, vendor, phân loại của hợp đồng sinh từ award không sửa
  qua editor hợp đồng chung; dòng đã award không sửa/thêm/chuyển qua endpoint cung ứng,
  kể cả khi Draft. Ghi chú Draft, ký và thanh toán vẫn dùng luồng có sẵn.
- Bid ghi tiền tệ 3 chữ cái và tỷ giá sang VND nhập tay, bất biến (VND = 1). Tổng
  thương mại: chiết khấu (phần trăm hoặc số tiền, không cả hai) → cộng vận chuyển →
  VAT. Giữ cả tổng gốc và tổng quy đổi. Đơn giá và thành tiền 4 chữ số, khối lượng 6;
  giá trị hợp đồng quy đổi làm tròn 2 chữ số, đơn giá dòng hợp đồng giữ 4. Dòng hợp
  đồng giữ đơn giá gốc quy đổi; header gồm thêm vận chuyển, chiết khấu, VAT phân bổ.
  Đối soát chênh lệch qua snapshot thương mại bất biến của award, không sửa dòng.

| Quyền | Cấp mặc định |
|---|---|
| `proc.rfqs.view` | Procurement, PM, BGD, ADMIN, SUPER_ADMIN |
| `proc.rfqs.manage` | Procurement, ADMIN, SUPER_ADMIN |
| `proc.rfqs.export` | Procurement, BGD, ADMIN, SUPER_ADMIN |
| `proc.rfqs.award` | BGD, ADMIN, SUPER_ADMIN |

Owner RFQ phải là user Cung ứng đang hoạt động và là thành viên dự án (kiểm khi tạo,
phát hành, award). Dự án Completed/Cancelled từ chối ghi. Award kiểm lại vendor active,
revision/hạn bid, trạng thái dự án, owner và BOQ duyệt hiện tại trong transaction; có
BOQ duyệt mới hơn thì huỷ RFQ cũ và lập RFQ mới.

| Trạng thái | Được phép |
|---|---|
| Draft | Sửa phạm vi, gắn file gói, phát hành, huỷ (có lý do) |
| Issued | Nộp revision báo giá tới hạn, rút báo giá hiện tại (có lý do), bắt đầu đánh giá, huỷ |
| UnderEvaluation | Chấm điểm, phân bổ MR theo lô, award, huỷ |
| Awarded | Xem bằng chứng và hợp đồng sinh ra; đóng RFQ |
| Closed / Cancelled | Đọc/xuất bằng chứng |

- Phát hành đóng băng mô tả, khối lượng, đơn vị dòng BOQ và lời mời, tạo token cổng,
  gửi email cho vendor sau commit. Bắt đầu đánh giá dừng nhận báo giá, kể cả trước hạn.
- Revision báo giá không ghi đè bản cũ; bản mới nhất là hiện tại kể cả đã rút, rút
  không làm sống lại bản cũ. Ô giá trống là thiếu, 0 là miễn phí. Báo giá thiếu dòng
  chỉ được chọn cho dòng đã báo; chọn trọn gói cần báo giá đầy đủ. Báo giá hết hạn,
  đã rút, vendor inactive hoặc bị thay thế không award được. Hiệu lực báo giá phải phủ
  hạn RFQ.
- Mọi thao tác ghi cần `Idempotency-Key`; sửa, chuyển trạng thái, bid, award và gắn
  file cần `rowVersion`/`If-Match`. Root, lịch sử báo giá, snapshot award và hợp đồng
  sinh ra commit trong một transaction.
- SQL Server: update lock trên root RFQ và các collection con theo thứ tự cố định;
  application lock thuộc transaction tách tạo RFQ khỏi các thao tác RFQ khác (tạo
  tuần tự giữa các dự án). Cấp mã BOQ revision, MR, phiếu nhập/xuất, payment request
  giữ range trước khi đọc số kế tiếp. Lock RFQ và phân bổ MR có timeout 15 giây; hết
  giờ trả conflict, client tải lại số khả dụng rồi mới thử lại.
- File dùng validate và vòng đời Drive của ProjectDocument, category `Procurement`:
  upload thành tài liệu dự án trước, rồi gắn vào gói RFQ Draft hoặc revision báo giá.
  Form bỏ dở không xoá tài liệu. File khác dự án, đã gắn, hoặc đang chờ xoá không gắn
  được; tải file kiểm lại scope; bằng chứng đã gắn không xoá qua endpoint chung. Cần
  Drive đã cấu hình để upload.
- Phát hành và award báo owner và PM qua notification template; worker 5 phút đánh dấu
  RFQ quá hạn (Issued/UnderEvaluation) và báo một lần (dấu và notification cùng
  transaction).
- RFQ không có hard delete; giữ bằng chứng qua huỷ. Xoá dự án, vendor được mời hoặc hợp
  đồng đã award coi RFQ là blocker.

Route `/api/operational-projects/{projectId}/procurement/rfqs` (và `/api/v1`):

| Thao tác | Method / suffix |
|---|---|
| List / tham chiếu / CSV | GET `/`, `/references`, `/export` |
| Chi tiết / gói bằng chứng JSON | GET `/{id}`, `/{id}/export` |
| Tạo / sửa | POST `/`, PUT `/{id}` |
| Vòng đời | POST `/{id}/issue`, `/evaluate`, `/cancel`, `/close` |
| Nộp / rút báo giá | POST `/{id}/bids`, `/{id}/bids/{bidId}/withdraw` |
| Chấm điểm / award theo lô | POST `/{id}/bids/evaluate`, `/{id}/award-batch` |
| Gửi lại lời mời | POST `/{id}/invitations/resend` |
| Upload / gắn file | POST `/documents/upload`, `/{id}/documents` |
| Tải file | GET `/{id}/documents/{documentId}/download` |

`POST /{id}/award` (award trọn gói cũ) trả `410` khi deploy; chỉ bật trong cấu hình test
tương thích cô lập.

Cổng vendor công khai: `GET /api/vendor-rfqs`, `POST /api/vendor-rfqs/bids`,
`GET /api/vendor-rfqs/documents/{documentId}/download`. Token ngẫu nhiên nằm trong
fragment URL email, SPA gửi qua header `X-RFQ-Portal-Token`; chỉ lưu SHA-256 của token.
Token hết hạn ở hạn RFQ (`LastPortalAccessAt` chỉ là telemetry, không gia hạn);
response `Cache-Control: no-store`, không chứa Customer, ngân sách BOQ, báo giá đối thủ,
user hay lịch sử nội bộ. Nộp qua cổng cần idempotency key và kiểm lại trạng thái, hạn,
vendor active, field thương mại. Gửi email lỗi được ghi lại; người có quyền xoay token
và gửi lại. Ghi từ cổng dùng owner RFQ làm FK kỹ thuật, nhưng read model và sự kiện
`portal-bid-submitted` ghi nhận vendor.

Validate (`RfqRequests.cs` cho shape, `RfqService.cs` cho quan hệ):

| Field | Quy tắc |
|---|---|
| Mã, tiền tệ, dự án/khách, tổng | Server sinh/suy, không ghi được |
| Tiêu đề | Bắt buộc sau trim, ≤ 200 |
| Revision BOQ | VND, Approved, cùng dự án; award cần revision hiện tại |
| Hạn | Thời điểm tương lai có timezone; phát hành vẫn phải ở tương lai |
| Vendor | 1–100 ID duy nhất, active, loại Supplier/SubContractor/Both |
| Dòng BOQ / dòng báo giá | 1–500 dòng nguồn duy nhất; dòng bỏ qua là thiếu |
| Khối lượng | > 0, ≤ khối lượng duyệt, ≤ 6 chữ số thập phân |
| Ghi chú RFQ/báo giá | ≤ 2.000 sau trim |
| Đơn giá | 0–999999999999.9999, ≤ 4 chữ số |
| Thành tiền / tổng báo giá | ≤ 99999999999999.9999 VND |
| Thời gian giao | Số ngày 0–3.650 |
| Điều khoản thanh toán | Bắt buộc, ≤ 1.000 |
| File báo giá | 0–20 file Procurement cùng dự án, chưa gắn |
| Trọng số | Bốn giá trị 0–100, tổng đúng 100 |
| Điểm thương mại | 0–100 kèm ghi chú 3–2.000 ký tự |
| Lý do award/huỷ/rút | 3–2.000 sau trim |
| Loại hợp đồng | Supply/Subcontract khớp loại vendor |
| Row version | Token 8 byte bắt buộc khi thay đổi |
| List | Search ≤ 200; sort trong whitelist; page 1–1000000; pageSize 1–100 |

Hạn lưu và trả UTC; filter ngày ở UI quy đổi đầu/cuối ngày local sang UTC. Ô CSV vô
hiệu hoá tiền tố công thức. UI `/admin/procurement-control/rfqs?projectId=...&rfqId=...`.

Migration `20260908134224_AddRfqBidComparison` (7 bảng, FK hạn chế, quyền RFQ; `Down`
xoá bằng chứng RFQ) và `20260909050114_CompleteRfqWorkflow` (chấm điểm, giao lời mời,
snapshot thương mại, award tách, phân bổ MR; RFQ cũ backfill trọng số 50/20/20/10, bid
cũ thành VND tỷ giá 1). Demo RFQ chỉ có ở `Development`; production không tạo
`PJ-SAMPLE-RFQ`.

### 11.6 Nhận IFC phía Cung ứng

- `GET /api/operational-projects/{projectId}/procurement/ifc?page=1`: hộp 20 packet
  `Released` gửi tới thành viên đang hoạt động hiện tại.
- `POST .../ifc/{recipientId}/acknowledge` cần View + Acknowledge và `rowVersion`.
  Lần nhận đầu lưu người/thời điểm (UTC có `Z`); gọi lại giữ giá trị đầu, không audit
  lần hai.
- `GET .../ifc/{recipientId}/items/{itemId}/content` chỉ phục vụ bản vẽ đã phát hành
  sau khi đối chiếu SHA-256 với bằng chứng khách đã duyệt; thiếu/lệch trả `409`
  `IFC_RELEASED_FILE_CHANGED`. Cung ứng không nhận đường dẫn file hay quyền Design rộng.
- Phát hành đọc đúng file chính của bản vẽ và yêu cầu SHA-256 khớp snapshot khách đã
  duyệt; file phụ không thay được. Thiếu/không đọc được: `400`
  `IFC_PRIMARY_FILE_UNAVAILABLE`; chưa duyệt: `400` `IFC_PRIMARY_FILE_NOT_APPROVED`
  (kèm `drawingCode`). Hash chỉ áp khi mọi file hợp lệ. Bản vẽ không có file giữ hành
  vi phát hành chỉ metadata.
- `GET /api/ifc-releases/procurement-recipients?designProjectId=...` trả thành viên dự án
  hợp lệ; thêm người nhận nhận `recipientUserId` tuỳ chọn. Packet đã phát hành không
  thêm người nhận.
- Migration `20261108150000_AddProcurementIfcReceipt`: ID người nhận/người xác nhận,
  SHA-256 item, rowversion; filtered unique index chống trùng người nhận nội bộ; chỉ cấp
  hai quyền mặc định cho Cung ứng. Export dữ liệu nhận/hash trước khi `Down`; quyền
  được giữ khi `Down`. Rehearse Up/Down/Up.
- Test: `IfcReleasesControllerTests`, `ProcurementIfcConcurrencyTests` (cần
  `NICON_SQL_TEST_CONNECTION`), `PermissionServiceTests`, `tests/apiError.test.mjs`,
  `procurement-ifc-receipt-live.spec.ts`. Fixture SQL E2E dùng `sqlcmd -I` để bật
  `QUOTED_IDENTIFIER`. Tab IFC mở bằng `tab=ifc` hoặc mặc định cho user chỉ có quyền IFC.

---

## 12. Thiết kế và pháp lý

### 12.1 Lịch thiết kế chi tiết (Module 2)

Lịch theo từng luồng Design cho ba giai đoạn, bảng `design_schedule_*` riêng, không
đụng `construction_tasks` hay API của nó. Route
`/api/operational-projects/{projectId}/design-schedule` (và `/api/v1`):

| Method | Route | Permission | Mục đích |
|---|---|---|---|
| `GET` | `/` | `operations.projects.view` | Giai đoạn, nguồn rollup, task phân trang |
| `POST` | `/initialize` | `design.schedule.manage` + lãnh đạo dự án | Tạo baseline ba giai đoạn |
| `PUT` | `/phases/{phaseId}` | như trên | Ngày, trạng thái, tiến độ, trọng số giai đoạn |
| `POST` | `/phases/{phaseId}/tasks` | như trên | Tạo task hoặc milestone |
| `PUT` | `/tasks/{taskId}` | như trên | Sửa task, thay link predecessor |
| `POST` | `/tasks/{id}/complete` | người được giao | Ghi hoàn thành, chuyển `Pending` chờ duyệt |
| `POST` | `/tasks/{id}/review` | người duyệt được giao | Duyệt; từ chối cần lý do, về `InProgress` |
| `GET` | `/outsource-options` | `design.schedule.manage` + scope dự án | Cặp vendor/hợp đồng hợp lệ cho task thuê ngoài |

- Gửi `designProjectId` khi đọc và khởi tạo; chỉ được bỏ khi dự án có một luồng.
  Cha, predecessor, assignee, approver, CC phải cùng luồng.
- Quyền sửa: `CanManageDesignScheduleAsync` xác nhận người gọi là PM của Operational
  hoặc Design Project, Design Lead, hoặc có assignment PM/Design Lead toàn dự án hoặc
  module Design đang hiệu lực. Assignment chỉ theo bộ môn là chỉ đọc. Admin có cả
  `design.schedule.manage` và `operations.projects.view.all` chỉ quản lý mọi dự án
  khi system role là `ADMIN`/`SUPER_ADMIN`; role tuỳ chỉnh hay quyền xem toàn danh mục
  không mở quyền sửa.
- Khởi tạo tường minh, idempotent: Design Project cần ngày bắt đầu và hạn cách nhau ít
  nhất 3 ngày, đúng ba giai đoạn `Concept`, `BasicDesign`, `ShopDrawing`, trọng số
  tổng 100; chia khoảng ngày thành ba đoạn liên tiếp không chồng. Baseline thiếu hoặc
  không chuẩn bị từ chối. Migration không tự tạo lịch cho dự án cũ.
- Mỗi task có đúng một `ApproverMemberId` đang hoạt động khác người được giao, CC tuỳ
  chọn, mô tả, task cha, tóm tắt nguồn lực/thầu phụ, cờ critical path. Update chung
  không ghi được `Completed`.
- Task thuê ngoài có cả `VendorId` và `DownstreamContractId` (không có một trong
  hai). Hợp đồng phải cùng dự án, khớp vendor, hướng `Downstream`, loại `Design` hoặc
  `Subcontract`, không huỷ. Người được giao nội bộ và Design Lead duyệt vẫn bắt buộc.

| Từ | Được sang |
|---|---|
| `NotStarted` | `InProgress`, `OnHold`, `WaitingForDepartment` |
| `InProgress` | `Completed`, `OnHold`, `WaitingForDepartment` |
| `OnHold` | `InProgress`, `WaitingForDepartment` |
| `WaitingForDepartment` | `InProgress`, `OnHold` |
| `Completed` | — |

Giữ nguyên trạng thái hiện tại thì hợp lệ. `NotStarted`: tiến độ 0, không có ngày thực
tế. `InProgress`: có ngày bắt đầu thực tế. `Completed`: đủ hai ngày thực tế và 100%.
Trạng thái khác không có ngày kết thúc thực tế. Ngày kết thúc không trước ngày bắt đầu.
Trọng số 1–100, tiến độ 0–100; sửa một giai đoạn làm tổng ba trọng số khác 100 bị từ
chối. Milestone có `IsMilestone = true` và ngày bắt đầu = kết thúc. Quá hạn tính lúc
đọc (kết thúc kế hoạch trước ngày UTC hiện tại và chưa `Completed`), không lưu. Bộ môn
phải là option đang hoạt động trong category master-data `project-department` (Design,
Architecture, Structural, MEP, Interior; bốn ngôn ngữ). Assignee là user đang hoạt
động có `OperationalProjectMember` chưa kết thúc trong dự án. Predecessor cùng dự án;
tự phụ thuộc và vòng bị từ chối.

Tiến độ theo chính sách `design-schedule-weighted-v1`: giai đoạn sẵn sàng khi có ít
nhất một task và trọng số task tổng 100, `P_phase = Σ(w_i·p_i)/100`; dự án sẵn sàng khi
đủ ba giai đoạn, trọng số giai đoạn tổng 100 và mọi giai đoạn sẵn sàng,
`P_project = Σ(W_j·P_j)/100`. Chưa sẵn sàng thì null. Response trả ID, trọng số, tiến
độ nguồn và giá trị có trọng số để kiểm toán. Bộ lọc (`phase`, `assigneeMemberId`,
`departmentCode`, `status`, `plannedFrom`, `plannedTo` theo giao khoảng, `overdueOnly`,
`page`, `pageSize`) chỉ áp cho list; rollup luôn dùng toàn lịch.

Mỗi thao tác ghi tạo audit và snapshot lịch sử. Xoá Design Project báo giai đoạn, task,
phụ thuộc, lịch sử trong impact plan. Migration `AddDetailDesignSchedule` chỉ thêm bảng.
`AddDesignApprovalWorkflow`: trước khi áp cho môi trường đã có task, mỗi Design Project
cần Design Lead hoặc Design Manager đang hoạt động, là thành viên dự án và khác từng
assignee; migration dừng thay vì bịa người duyệt; rollback xoá dữ liệu duyệt/CC.

### 12.2 Rà soát hồ sơ thiết kế

Route `/api/design-projects/{projectId}/reviews`. Design Manager tạo revision với người
nhận nội bộ được chỉ định và các Project Document ID thuộc category Design tương ứng.
Đủ điều kiện nhận: user/role đang hoạt động có `design.reviews.receive` (kể cả role tuỳ
chỉnh); việc được giao từng review vẫn quyết định quyền truy cập.

- Người nhận chỉ thấy review của mình và bình luận khi review còn nội bộ.
  Notification mở `/admin/design-reviews/{projectId}?reviewId={reviewId}`, chỉ tải API
  review và file của review đó; link cũ `?tab=reviews` rơi về cùng view khi không có
  quyền dự án. Review không cấp thêm quyền dự án.
- Tải file của review kiểm byte thật với `Sha256Snapshot` trước khi gửi header (đệm
  64 KiB trong RAM, lớn hơn thì ra thư mục tạm ASP.NET, giới hạn 100 MiB). Lệch byte:
  `409`; hash thiếu/sai, nội dung rỗng hoặc không xác minh được cũng thất bại. Người
  không được giao nhận `404` trước khi đọc storage. Lệch không bao giờ ghi lại snapshot
  hay trạng thái duyệt. Đây là bảo vệ Partial: chưa lưu trữ/khôi phục byte lịch sử,
  chính sách lưu giữ còn mở.
- `POST .../{reviewId}/comments` nhận `projectDocumentId` tuỳ chọn (phải thuộc tài
  liệu đã trình của review đó); null là bình luận cấp hồ sơ
  (`LinkDesignReviewCommentsToDocuments`).
- Design Manager tổng hợp ý kiến, ghi trình khách và quyết định của khách. Chốt
  Concept, mở Basic sang Shop và phát hành IFC cần review khớp gần nhất ở
  `ClientApproved`.
- `EnsureDesignReviewEvidenceSnapshot` là sửa tương thích chỉ đi tiếp: thêm cột/index
  snapshot còn thiếu, backfill từ Project Document liên kết, dừng nếu có bằng chứng
  không xác định được; `Down` không xoá gì.

### 12.3 Hồ sơ Concept

- `GET/POST /api/concept-options/{id}/revisions`, artifact và miễn checklist
  (`POST/DELETE` dưới revision), phản hồi và phản hồi một lần (`POST`). R00 tạo cùng
  option. Bản nháp gắn Project Document thuộc option; gỡ binding không xoá tài liệu.
- Trình cần bộ file đã sync chính xác và checklist áp dụng, rồi đóng băng revision.
  Phản hồi khách chỉ gắn sau khi trình khách; phản hồi mới cần `evidenceSource`
  (1–1.000 ký tự), giữ nguyên văn bình luận; trả lời cần `outcome` (1–2.000). Cả hai bất
  biến; dữ liệu cũ để null (`AddConceptFeedbackEvidence`, rollback bị chặn khi đã có
  giá trị).
- Revision thay thế đi sau quyết định yêu cầu sửa hoặc từ chối. `ClientApproved` cần
  Project Document TK-M03 đã ký và sync, người ký, ngày ký; chốt kiểm bằng chứng đó trên
  revision mới nhất. Filtered unique index cho phép một option được chốt mỗi Design
  Project; trạng thái option là concurrency token. `AddConceptDossierRevisions` backfill
  R00.
- Mọi thao tác ghi hồ sơ Concept (sửa, trình, quyết định khách, chuyển trạng thái option,
  xoá tài liệu tay) lấy SQL application lock `concept-dossier-{optionId}` trước khi đọc
  guard; đường ghi mới phải dùng cùng lock. Bằng chứng đã đóng băng/đã trình và file
  nghiệm thu không xoá được qua Project Documents. Response có `canCreateRevision`.
  R00 dùng chung kiểu cũ không biến thành phê duyệt option; revision mới vẫn qua gate
  trình và TK-M03.
- Test `ConceptDossierConcurrencyTests` cần `NICON_SQL_TEST_CONNECTION`, mỗi case tạo/xoá
  database `NiconConceptTest_<guid>` riêng. Spec browser Concept dùng
  `E2E_BACKEND_CONTAINER` (mặc định `nihome31042025-backend`) và `E2E_SQL_CONTAINER`.

### 12.4 Xoá hồ sơ thiết kế

Concept, Basic Design, Shop Drawing và IFC dùng `GET /api/<resource>/{id}/deletion-impact`
rồi `DELETE` với `ConfirmDeletionRequest`. Chỉ Concept `Discarded`, Basic/Shop
`Rejected`, IFC `Cancelled` đủ điều kiện. Project Document đang hoạt động, bằng chứng
review dùng chung và tham chiếu IFC chặn xoá; lịch sử review riêng chỉ xoá cùng hồ sơ bị
từ chối khi chưa từng trình khách. Review có `ClientSubmittedAt` luôn chặn hard delete.
Xoá hàng loạt Shop Drawing bị tắt. Đây là chính sách an toàn đang áp, chưa phải lời giải
đã xác nhận cho Q-08 (baseline 03/10 chỉ xác nhận xoá hồ sơ không đạt thuộc cấp quyền
cao nhất và để mở việc xoá sau khi trình khách). Upload Shop Drawing từ chối khi
`Released` hoặc thuộc packet IFC đã phát hành; trạng thái và đường dẫn file là
concurrency token nên upload chạy đua với phát hành IFC trả `409` và giữ file gốc.

### 12.5 Kích hoạt pháp lý và permit

Tạo permit là luồng tường minh của Operational Project. Contract Upstream `Design`,
`Construction` hoặc `DesignAndBuild` không chuyển `Signed → InProgress` qua transition
chung (trả `LEGAL_ACTIVATION_REQUIRED`); PM/Admin phải xác nhận wizard Legal. Tạo
`DesignProject` không tự sinh permit.

Transaction kích hoạt tạo hoặc dùng lại gói `LegalPermitting` duy nhất của dự án, tạo một
task cho mỗi permit được chọn, ghi phụ thuộc và lịch sử, tạo bản ghi kích hoạt hợp đồng
rồi mới đổi trạng thái hợp đồng; lỗi validate hoặc concurrency rollback toàn bộ. Unique:
`ContractLegalActivation.ContractId`; gói theo (`OperationalProjectId`,
`LegalPermitting`); loại permit theo dự án; `WorkTaskId` (tuỳ chọn). `DesignProjectId`
nullable cho đọc dữ liệu cũ.

| Method | Route | Permission + scope | Mục đích |
|---|---|---|---|
| `GET` | `/api/contracts/{id}/legal-activation-preview` | `crm.contracts.view`; PM/Admin dự án | Điều kiện, loại permit, gói hiện có, thành viên |
| `POST` | `/api/contracts/{id}/activate-with-legal` | như trên; idempotency key, row version | Kích hoạt hợp đồng và cấu hình Legal nguyên tử |
| `GET`/`POST` | `/api/operational-projects/{id}/legal-setup` | `permit.checklists.manage`; PM/Admin | Setup cho dự án cũ/đã kích hoạt, không đổi trạng thái hợp đồng |
| `GET` | `/api/legal-checklist-templates?publishedOnly=true`, `/{id}` | `permit.templates.view` | List (bỏ filter để xem mọi version), chi tiết |
| `POST`/`PUT` | `/api/legal-checklist-templates`, `/{id}` | `permit.templates.manage` | Tạo Draft (idempotency key), sửa Draft (row version) |
| `POST` | `/api/legal-checklist-templates/{id}/versions` | `permit.templates.manage` | Clone bản Published/Retired thành Draft mới |
| `POST` | `/api/legal-checklist-templates/{id}/publish`, `/retire` | `permit.templates.publish` | CM/Admin rà soát độc lập kèm mã phê duyệt; ngừng áp dụng mới |

Template: `Code` ASCII bất biến (1–60 chữ/số/`-`/`_`, bắt đầu bằng chữ hoặc số);
`NameVi/Name/NameZh/NameJa` (200) và `ScopeVi/Scope/ScopeZh/ScopeJa` (2.000) bắt buộc;
`Items` có thứ tự, 1–100 mã `permit_type` đang hoạt động, không trùng, có `IsRequired`,
ít nhất một mục bắt buộc. Publish cần `ApprovalReference` (≤ 1.000); người tạo và mọi
người sửa version đó không được publish. `Draft → Published → Retired` do server thực
thi; nội dung đã publish bất biến; không có hard delete template.

Áp template khi setup/kích hoạt: preview trả `Templates`, `AppliedTemplate`,
`WorkPackageRowVersion`, lead/ngày của gói, người/thời điểm áp template. Write nhận
`TemplateId`, `TemplateRowVersion`, `WorkPackageRowVersion`. Gói mới cần template
Published đúng version; dùng lại gói có sẵn luôn cần row version của gói. Bản cũ trả
`409` (`CONCURRENCY_CONFLICT` cho kích hoạt, `crm_concurrency_conflict` cho setup).
Dự án đã có snapshot tiếp tục dùng nó kể cả khi template retire; template khác bị từ
chối. Mục bắt buộc phải đã có task hoặc được đưa vào request, nếu không trả
`LEGAL_REQUIRED_PERMITS_MISSING` trước mọi ghi. Gói Completed/Cancelled không setup được
(`LEGAL_PACKAGE_CLOSED`); hợp đồng thứ hai được dùng lại gói Completed chỉ đọc khi
snapshot đã áp, mục bắt buộc đã link và request không đổi gì. Task mới được phụ thuộc
permit đã cấu hình của cùng dự án; predecessor lạ, chưa link hoặc khác dự án bị từ chối.
Đổi lịch/lead ghi `UpdatedByLegalSetup` kèm giá trị trước/sau. `/api/work-packages`
chung từ chối tạo `LegalPermitting`. Cờ `IsRequired` điều khiển checklist, không phải
yêu cầu permit theo giai đoạn của gate thi công. Q-09/D-04 vẫn do CM/NICON quyết; template
demo chỉ ở Draft. Migration `20261022111459_AddLegalChecklistTemplates` chỉ thêm (permit
cũ `IsRequired=false`, không backfill); không rollback sau khi đã dùng thật nếu chưa có
kế hoạch bảo toàn.

Route `/api/permits` (và `/api/v1/permits`):

| Method | Route | Permission | Mục đích |
|---|---|---|---|
| `GET` | `/api/permits`, `/{id}`, `/{id}/history` | `permit.checklists.view` | Lọc, phân trang, tóm tắt rủi ro; chi tiết; lịch sử trạng thái cơ quan/tài liệu |
| `PATCH` | `/api/permits/{id}` | `permit.checklists.view` + scope actor dự án | Trạng thái cơ quan, cơ quan, ngày, ghi chú |
| `POST` | `/api/permits/{id}/documents/{kind}` | như trên | Upload bằng chứng nộp/cấp, nguồn module `Legal` |
| `POST` | `/api/permits`, `/api/permits/design-project/{projectId}/ensure` | `permit.checklists.manage` | Đã ngừng: `410 LEGAL_SETUP_REQUIRED` |
| `DELETE` | `/api/permits/{id}` | `permit.checklists.manage` | Bị chặn `409`; dùng luồng cấu hình/xoá an toàn theo aggregate |

`Submitted`, `UnderReview`, `NeedMoreDocs` cần ngày nộp và file hồ sơ nộp; `Issued` cần
ngày cấp, file giấy phép và `permit.checklists.mark-issued`. File nằm trong catalog
`LegalPermits` dưới `03_Xin_phep_Phap_ly` (`ProjectDocumentSourceModule.Legal`).
`DepartmentWorkTask` liên kết sở hữu assignee, approver, kế hoạch, tiến độ, phụ thuộc.
Assignee chỉ trình `Completed` khi permit đã `Issued` đủ bằng chứng; xác minh chuyển
`Completed → Verified`; trả về cần lý do và chuyển `Completed → InProgress` mà không
đổi trạng thái cơ quan.

---

## 13. Thi công và Module 4

### 13.1 Bố cục workspace thi công (frontend)

Menu Thi công chia theo vai trò; mỗi màn thuộc đúng một mục menu.

| Nhóm | Route | Nội dung |
|---|---|---|
| Ban chỉ huy công trường (BCH) | `/admin/construction/bch/operations` | Tab `resources` (yêu cầu nguồn lực), `diary` (nhật ký) |
| | `/admin/construction/bch/qa-qc` | Tab `dossiers`, `punchlist`, `acceptance`, `hse` (ẩn khi thiếu quyền xem) |
| | `/admin/construction/bch/qs` | QS batch |
| Phòng CM | `/admin/construction/cm/schedule` | Tab `plan` (tiến độ CM + chuẩn bị), `tasks`, `diary` (scope review) |
| | `/admin/construction/cm/commercial` | Tab `resources`, `qs` |
| | `/admin/construction/cm/quality` | Tab `dossiers` (kèm quản lý template QA/QC), `punchlist`, `acceptance`, `asbuilt`, `handover` |
| | `/admin/construction/cm/safety` | HSE |
| | `/admin/construction/cm/contracts` | Hợp đồng |

- Mọi trang bọc trong `ConstructionWorkspaceShell`: giữ `?projectId` trên URL (mặc định
  dự án đầu tiên), tải dự án và truyền `{ projectId, designProjectId, designProjects }`.
  Resource, QA/QC, QS lưu theo Operational Project; nhật ký, punch list, nghiệm thu, hoàn
  công, bàn giao, task thi công lưu theo Design Project (`?designProjectId`, có bộ chọn
  khi dự án có nhiều luồng thiết kế).
- Panel nhận `scope: "execute" | "review"` (`constructionScope.ts`). Scope là ngữ cảnh
  portal, không phải gate quản lý: `useConstructionActions` cho `canManage` khi có quyền
  `manage`/`create` của module hoặc, ở scope review, quyền giám sát `cm-review`;
  `canApprove` chỉ ở scope review nên BCH không bao giờ thấy nút duyệt, từ chối, xác
  nhận, xác minh, đóng, hoàn thành. Sửa/xoá dòng cần `canManage` và là giám sát hoặc
  tác giả.
- CM tạo, sửa, trình, xoá và giám sát bản ghi BCH thực hiện. Người giám sát CM sửa bản
  nháp của BCH không trở thành tác giả, nên `createdByUserId ≠ approverUserId` vẫn giữ.
  Server luôn kiểm review độc lập.
- Resource request, hồ sơ QA/QC, QS batch, HSE có `DELETE` sau cùng gate. Chỉ xoá Draft
  và CmRejected (HSE: Draft, Rejected, Cancelled); bản ghi khác phải đi qua chuyển trạng
  thái. Xoá không đụng project document liên kết. Quyền xoá HSE
  `construction.hse.delete`; PM nhận qua wildcard trong `RbacSeeder`, BCH chỉ có
  `hse.manage`.
- Tab lưu ở `?tab=` (replace), bỏ qua tab bị ẩn khi chọn mặc định. URL cũ đi qua
  `ConstructionRedirect.tsx`, giữ query và hash.
- Trường người dùng mới khó đoán có placeholder ví dụ và một dòng gợi ý gắn
  `aria-describedby`.
- Dữ liệu mẫu (`ConstructionSampleSeeder`, chỉ Development): dự án `PJ-SAMPLE-003` có
  đủ trạng thái cho mọi màn CM/BCH (`QA-SAMPLE-*`, `QS-SAMPLE-*`, `HSE-SAMPLE-*`,
  `WBS-BASELINE-ANPHU-R1`, `RR-SAMPLE-ANPHU-*`). Không dùng các mã này trong logic
  production.

### 13.2 Phần đã triển khai

**Tiến độ CM và so sánh với thực tế (Partial).** Route
`/api/operational-projects/{projectId}/construction/schedule`:

| Method/path | Permission | Mục đích |
|---|---|---|
| `GET /` | `construction.tasks.view` | Revision CRM, task, phiên bản CM, snapshot, số liệu thực tế, thành viên BCH hợp lệ |
| `POST /baselines` | `construction.cm-preparation.manage` | Tạo bản nháp đánh số, giới hạn bởi revision CRM đã ban hành |
| `GET /baselines/{id}/email-preview`, `POST /baselines/{id}/submit` | như trên | Preview rồi trình PM duyệt độc lập |
| `POST /baselines/{id}/decision` | `construction.cm-preparation.view` | PM được giao duyệt/từ chối, tách tác giả |
| `POST /tasks/{taskId}/actual` | `construction.tasks.view` | Chỉ huy trưởng được giao cập nhật tiến độ thực tế |
| `POST /snapshots` | `construction.tasks.view` | Chụp tiến độ; báo cáo BCH thiếu thì không tạo |
| `GET /baselines/{baselineId}/report?period=day|month|year&throughDate=...` | `construction.tasks.view` | Đường cong CRM/CM và snapshot cuối kỳ |
| `POST /snapshots/{id}/recovery` | `construction.tasks.view` | Giải trình và kế hoạch khắc phục khi chậm quá ngưỡng |
| `POST /snapshots/{id}/review` | `construction.cm-preparation.manage` | CM rà soát kế hoạch khắc phục |

Quy tắc S-Curve (NICON uỷ quyền cho chủ sản phẩm ngày 09/11/2026; đây là quyết định được
uỷ quyền, không phải phê duyệt trực tiếp mới của NICON):

- CM gán tỷ trọng 0–100 cho các hạng mục WBS lá, tổng 100%, ghi cơ sở (phạm vi/khối
  lượng/độ phức tạp; không bắt buộc BOQ); PM duyệt độc lập. Dòng tổng hợp và milestone
  có tỷ trọng 0.
- Baseline mới đóng băng lịch làm việc (mặc định Thứ Hai–Thứ Bảy, nghỉ Chủ nhật, kèm
  ngày nghỉ riêng); baseline cũ dùng đủ 7 ngày. Kế hoạch tăng theo ngày làm việc.
- Thực tế lấy từ `ConstructionTask.ProgressPercent` do BCH báo, lưu người và thời điểm
  báo. Khi đã có baseline CM ban hành, chỉ chỉ huy trưởng được giao, tài khoản đang hoạt
  động và còn là thành viên dự án mới được báo.
- Worker mỗi phút chụp snapshot khi dữ liệu đủ, bỏ trùng trong ngày. Thiếu báo cáo là
  không có dữ liệu (null), không phải 0%, không nội suy. Tháng/năm dùng giá trị cuối kỳ.
- Baseline đã ban hành bất biến; thay đổi tạo phiên bản mới, PM duyệt lại.
- Chậm = kế hoạch − thực tế (điểm phần trăm, chủ sản phẩm xác nhận 24/10/2026). Chỉ khi
  vượt **hơn** 5 điểm (5 điểm thì không) mới tạo một cảnh báo đỏ cho mỗi baseline/ngày
  và buộc chỉ huy trưởng giải trình, lập kế hoạch khắc phục kèm hạn.
- Tổng hợp S-Curve toàn danh mục dự án chưa thuộc phạm vi.

**Liên kết bản vẽ cho task thi công.** `GET /api/construction-tasks/{id}/drawings`
(`construction.tasks.view` + quyền dự án) trả `version`, `linked`, `available`: chỉ Shop
Drawing cùng dự án đã `Released` trong packet IFC đã phát hành. `PUT` cùng route
(`construction.tasks.manage`) nhận `{ "version": "<GUID>", "ifcReleaseItemIds": [...] }`
(bắt buộc, có thể rỗng, tối đa 200 ID dương không trùng); ID sai trả `400`, version cũ
trả `409`, gửi lại đúng tập đã lưu trả `200`. `GET .../drawings/{itemId}/content` chỉ phục
vụ bản vẽ đã link và phát hành. Gỡ link không xoá bản vẽ hay file. Số R hiển thị là
metadata revision cuối cùng trước ngày phát hành IFC (R0 nếu không có). Migration
`AddConstructionTaskDrawingLinks`. Test SQL `ConstructionDrawingConcurrencyTests`:

```bash
dotnet test nihomebackend.integration.tests/nihomebackend.integration.tests.csproj --filter FullyQualifiedName~ConstructionDrawingConcurrencyTests
```

**Chuẩn bị CM, yêu cầu nguồn lực, QA/QC, QS.** Route
`/api/operational-projects/{projectId}/construction` có phiên bản chuẩn bị/readiness của
CM, yêu cầu nguồn lực có loại, template và hồ sơ QA/QC, QS batch có hướng rõ ràng.

- Readiness kiểm hợp đồng Upstream thi công đã ký, IFC đã phát hành, permit bắt buộc còn
  hiệu lực, tham chiếu chuẩn bị, Budget Baseline đã khoá và các cờ bàn giao mặt bằng,
  biện pháp thi công, kế hoạch HSE; lưu snapshot bằng chứng và người đánh giá.
- Duyệt vật tư dùng lại `IProcurementService` trong cùng transaction để tạo và trình MR;
  thiết bị/nhân công dừng ở trạng thái sẵn sàng chuyển Cung ứng.
- Hồ sơ QA/QC cần template đa ngôn ngữ đã publish, packet IFC đã phát hành cùng dự án,
  checklist hợp lệ và Project Document đang hoạt động cùng dự án; mỗi tài liệu giữ phân
  loại rõ ràng. QS kiểm hướng hợp đồng, ngày, tổng dòng, tham chiếu mốc/phụ lục/BOQ duyệt
  và chuỗi thay thế/hoàn. Bản đã trình bất biến, chỉ sửa qua vòng review/điều chỉnh.
- Tài liệu là bằng chứng QA/QC hoặc QS (kể cả nháp) không xoá được; tạo/sửa hồ sơ ghim
  row version của tài liệu nên lệnh xoá chạy đua sẽ lỗi concurrency.
- Người tạo MR, hồ sơ QA/QC, QS batch, HSE hoặc VO không được tự duyệt. Với HSE, cả người
  tạo và người báo cáo đều không được xác nhận/từ chối: trả `400`
  `HSE_INDEPENDENT_REVIEW_REQUIRED` kèm `operation`, `projectId`, `violationId`.

**Chuyển nguồn lực sang Cung ứng (Partial).** Route
`/api/operational-projects/{projectId}/procurement/construction-resources`:

| Method/path | Permission | Ý chính |
|---|---|---|
| `GET /` | `proc.resource-requests.view` | Thiết bị/nhân công CM đã duyệt; lọc `type`, `state=AwaitingContract|ContractLinked|Supplied` |
| `GET /contracts` | như trên | Hợp đồng Downstream Supply/Subcontract cùng dự án ở Draft/Signed/InProgress |
| `GET /documents` | `proc.resource-requests.manage` | Tài liệu cùng dự án (trừ đang chờ xoá) |
| `POST /contracts` | như trên | Tạo hợp đồng Downstream Draft từ yêu cầu đã duyệt (vendor, loại, giá trị, phạm vi) |
| `POST /{id}/contract` | như trên | Gắn hợp đồng: một hợp đồng mỗi yêu cầu, nhiều yêu cầu mỗi hợp đồng; khoá sau khi cung cấp |
| `POST /{id}/supply` | như trên | Xác nhận đã cung cấp kèm tài liệu; hợp đồng phải Signed/InProgress |

Giá trị Draft 0–9.007.199.254.740.991, phạm vi tối đa 20.000 ký tự. Không nhận thời
điểm, người, khách, dự án hay hướng từ body. Sự kiện chuyển giao chỉ thêm, ghi cùng
transaction serializable; tài liệu bằng chứng đã dùng không xoá được. Migration
`20261018063226_AddConstructionProcurementHandoff` không backfill trạng thái đã cung cấp.
`GET /reports/projects` thêm `cmControl` theo dự án (QS hai hướng, QA/QC, HSE đếm theo bản
ghi, `periodPolicy=CurrentState`); không suy công thức tài chính mới.

<a id="720-module-4-target-integration-contract"></a>

### 13.3 Module 4 Target Integration Contract

Đây là hợp đồng mục tiêu cho Thi công và Nghiệm thu, bắt buộc tham chiếu khi làm phần thi
công. Baseline 03/10 xác nhận việc tách CM/BCH, yêu cầu nguồn lực, QA/QC và QS; gate,
ma trận và template chi tiết vẫn chờ quyết định. Không có nghĩa mọi mục dưới đây đã có
trong API.

**Ranh giới nghiệp vụ** (phản hồi khách 18/10/2026,
[NICON CM/ISO response](Nicon/MoMs/2026-10-18/NICON_Phan-hoi_CM_ISO.md)):

- Hai giai đoạn: Phòng CM chuẩn bị dự án sau khi hợp đồng Upstream D&B/thi công được ký;
  trong thi công, BCH và Phòng CM chạy song song.
- Mỗi dự án có BCH riêng; một người có thể thuộc nhiều BCH, nhưng quyền ở dự án này không
  mở quyền dự án khác.
- Sau khi CM duyệt yêu cầu thiết bị/nhân công, Cung ứng lập hợp đồng và cung cấp cho công
  trường, rồi Tài chính – Kế toán (FA) thanh toán theo hồ sơ áp dụng.
- Hợp đồng đã ký mở bước chuẩn bị trên Operational Project sẵn có, không tạo dự án trùng.
  Kế hoạch chuẩn bị của CM sở hữu WBS baseline, critical path, Baseline S-Curve, tổ chức
  BCH, kế hoạch nguồn lực và Budget Baseline có phiên bản.
- Bản ghi hiện trường thuộc dự án và ghi hạng mục WBS, ngày báo cáo, tác giả, vai trò
  BCH, trạng thái, row version. QA/QC, Punchlist, HSE, yêu cầu nguồn lực, QS và VO là các
  aggregate riêng nối bằng ID ổn định, không nối bằng đường dẫn Drive, nhãn hay text.
  Module 4 tham chiếu nhà cung cấp/báo giá của Cung ứng và hợp đồng, mốc, hồ sơ thanh
  toán, VO của Module 6, không sao chép master.
- Bộ ISO của NICON là nguồn template cho mọi quy trình và bước (xem
  [mục 15](#15-dữ-liệu-tham-chiếu-của-nicon)); không tự đặt chứng từ bắt buộc hay ngoại
  lệ thanh toán chỉ dựa vào tên file.

**Luồng và kiểm soát:**

1. CM chỉ ban hành baseline khi WBS, mốc, vai trò BCH, kế hoạch nguồn lực và Budget
   Baseline qua validate ở server.
2. Hợp đồng đã ký chỉ cho phép chuẩn bị, chưa cho khởi công. Gate khởi công còn cần IFC
   áp dụng đã phát hành, permit bắt buộc còn hiệu lực, baseline tiến độ đã ban hành, kế
   hoạch BCH/nguồn lực đã duyệt và Budget Baseline đã khoá. Bàn giao mặt bằng, biện pháp
   thi công, kế hoạch HSE theo checklist ISO khi NICON duyệt.
3. BCH nộp bằng chứng hiện trường từ mobile. Dữ liệu nháp/offline không phải bằng chứng
   đã duyệt, không kích hoạt Cung ứng, thanh toán hay KPI.
4. Yêu cầu nguồn lực vượt mức hoặc ngoài kế hoạch cần CM cấp 2 duyệt độc lập trước khi
   sang Cung ứng; từ chối không đổi cam kết BOQ.
5. QS Upstream và Downstream tách hướng và liên kết hợp đồng. Khối lượng đã duyệt bất
   biến; sửa bằng phiên bản thay thế hoặc bút toán hoàn có lịch sử.
6. CM so tiến độ BCH báo với Baseline S-Curve theo quy tắc ở 13.2.
7. Người tạo không tự duyệt bản ghi của mình (server thực thi).
8. Hoàn thành/bàn giao dùng trạng thái nghiệm thu, QA/QC, hoàn công, chạy thử và Punchlist
   chặn đã duyệt; không tin cờ sẵn sàng từ client.

Phạm vi báo cáo CM đã xác nhận: tiến độ, khối lượng hoàn thành với nghiệm thu Upstream/
Downstream riêng, giá trị thực hiện so dự toán, hồ sơ nghiệm thu QA/QC và an toàn. Kỳ báo
cáo, định nghĩa giá trị thực hiện và nguồn tính còn phải thống nhất; không gộp giá trị
nghiệm thu, cam kết và đã chi thành một chỉ số.

Ba khu vực BCH (Vận hành, QA/QC, QS công trường) dùng chung dự án, WBS/baseline, đội ngũ,
dịch vụ tài liệu, audit và kiểm soát truy cập; tách route không được nhân bản master dự
án, nhà cung cấp, hợp đồng, BOQ hay user.

| Sự kiện nguồn | Tác động bắt buộc |
|---|---|
| Hợp đồng Upstream đã ký | Mở chuẩn bị CM một lần, idempotent |
| IFC phát hành | Chỉ revision đã phát hành hiện cho người thi công |
| CM duyệt yêu cầu nguồn lực | Chuyển yêu cầu sang Cung ứng, không copy BOQ |
| QS Upstream duyệt | Bằng chứng cho mốc phải thu tương ứng |
| QS Downstream duyệt | Bằng chứng cho hồ sơ phải trả tương ứng |
| VO duyệt | Tạo phiên bản ngân sách/BOQ bị ảnh hưởng, không sửa ngầm baseline gốc |
| Tài liệu QA/QC được chấp nhận | Đồng bộ file vào `04_Thi_cong_Nghiem_thu`, giữ metadata |
| Chậm tiến độ quá 5% | Một cảnh báo đỏ cho mỗi dự án/baseline/kỳ tới khi xử lý hoặc bị thay |

Thanh toán Downstream theo khối lượng nghiệm thu (mục tiêu) đối chiếu cùng vendor, hợp
đồng, dự án, cơ sở tiền tệ/số tiền và khối lượng/kỳ: (1) hợp đồng Downstream đã ký hoặc
phụ lục được duyệt; (2) phiếu nhập kho posted (trừ hoàn) khi là vật tư; (3) QS Downstream
đã duyệt cho khối lượng đã thực hiện. Ma trận áp dụng cho gói chỉ cung cấp, chỉ nhân công,
thiết bị và thầu phụ hỗn hợp còn là quyết định sản phẩm. Quy trình mua hàng cho phép đề
nghị thanh toán trước khi nhận hàng, quy trình thanh toán và `TQT-M07` có tạm ứng; tạm ứng
có vào Tài chính hay không, khi nào, với uỷ quyền và chứng từ gì vẫn là quyết định mở của
FA/CM. Không áp gate khối lượng cho tạm ứng mặc định, không coi thiếu phiếu nhập/QS là miễn
trừ, không tạo phiếu nhập hay QS giả để khớp. Đối chiếu thất bại trả chênh lệch cụ thể và
không đổi Payment Request, sổ, notification, audit (trừ bản ghi từ chối).

Ghi file ra ngoài dùng mẫu thao tác tài liệu bền: transaction lưu metadata và thao tác
chờ; Drive lỗi không được đánh dấu hồ sơ đã đồng bộ.

Còn mở: bộ ISO đã nhận ngày 20/10/2026, nhưng ánh xạ quy trình/bước được duyệt, ngoại lệ
thanh toán theo gói, công thức giá trị thực hiện và kỳ báo cáo vẫn thiếu. Không publish
template ISO production hay bật tính toán tài chính/baseline giai đoạn 3–4 cho tới khi
CM/FA/Product xác nhận; không seed danh sách chứng từ hay công thức tạm. Ngoài phạm vi hiện
tại: tổng hợp S-Curve danh mục, điều kiện Tài chính cho thiết bị/nhân công, nội dung
template ISO đã duyệt, aggregate thi công cũ theo Design Project và offline/kết nối lại.
Kế hoạch hoàn thiện ở [mục 20](#20-kế-hoạch-hoàn-thiện-theo-yêu-cầu-nicon).

Kiểm thử bắt buộc: unit cho chênh S-Curve, biên ngưỡng (5 điểm không phải > 5 điểm),
rollup ngân sách, khối lượng BOQ còn lại, tổng QS, chọn phiên bản baseline; integration
cho scope dự án/hợp đồng, quyền BCH và CM, tách người làm/duyệt, vòng đời, row version cũ,
idempotency, từ chối không đổi trạng thái downstream, liên kết thanh toán Upstream/
Downstream, và trạng thái Drive pending/retry/conflict/failure; browser cho luồng mobile
BCH và review/cảnh báo desktop CM (loading, rỗng, lỗi, offline/retry, responsive).

### 13.4 Bàn giao công trình

Route `/api/handover-records` (và `/api/v1`); UI `/admin/construction/handover`
(`HandoverRecordsPage.tsx`).

| Method | Route | Permission | Mục đích |
|---|---|---|---|
| `GET` | `/api/handover-records`, `/export`, `/{id}` | `construction.handover.view` | List có lọc/sắp/phân trang và tổng hợp; CSV đầy đủ; chi tiết, readiness, lịch sử |
| `POST` | `/api/handover-records` | `construction.handover.manage` | Tạo bản bàn giao duy nhất của dự án |
| `PUT` | `/api/handover-records/{id}` | `construction.handover.manage` | Sửa khi Draft/Reopened |
| `POST` | `/api/handover-records/{id}/status` | `construction.handover.manage` | Chuyển trạng thái chưa kết thúc |
| `POST` | `/api/handover-records/{id}/complete` | `construction.handover.complete` | Hoàn thành khi sẵn sàng và đã ký |
| `DELETE` | `/api/handover-records/{id}` | `construction.handover.manage` | Xoá vĩnh viễn ở mọi trạng thái |

`view.all` mở đọc không giới hạn, `manage.all` mở ghi không giới hạn (độc lập nhau); chỉ
có quyền cơ bản thì giới hạn ở bản ghi mình tạo/sở hữu và dự án mình quản lý/chủ trì.
Lỗi nghiệp vụ `400`, ẩn/không có `404`, trùng hoặc ghi đồng thời `409`. Readiness tính ở
server từ nghiệm thu từng phần đã duyệt, các hạng mục hoàn công bắt buộc đã duyệt, punch
chưa xử lý, chạy thử và checklist; client chỉ hiển thị. Khoá duy nhất hiện là
`DesignProjectId` (ranh giới tương thích); chuyển sang `OperationalProjectId` phải giữ bất
biến một bàn giao, lịch sử, row version, tài liệu, scope, deep link và xử lý dự án thi
công không có Design Project.

---

## 14. Báo cáo và KPI

### 14.1 Báo cáo vận hành dự án

Báo cáo tóm tắt theo `OperationalProjectId`, không tính điểm KPI, chỉ trả dự án người gọi
được truy cập qua `IProjectAccessService`.

- `GET /api/reports/projects`: danh mục khi bỏ `projectId`; `?projectId={id}` trả một
  dự án hoặc `404`.
- `GET /api/reports/projects/export?format=xlsx|pdf&language=vi|en|zh|ja`: cùng bộ lọc,
  render đủ kết quả ở server, ghi audit kèm bộ lọc và số dự án.
- `from`/`to` là `DateOnly` tuỳ chọn, tính trọn ngày; ngược thứ tự trả `400`. Khoảng
  ngày áp riêng cho từng ngày nguồn: hạn kế hoạch task thi công, ngày nghiệm thu, hạn
  hoặc ngày hết hạn permit, ngày tạo báo giá, ngày ký hợp đồng (chưa ký thì ngày tạo),
  ngày quyết định VO, hạn mốc thanh toán. Tiến độ thiết kế là hiện tại vì chưa có snapshot
  lịch sử. Response có `generatedAtUtc`, `asOfUtc`; drill-down chỉ là route tương đối.

| Role | Xem | Xuất |
|---|---|---|
| `SUPER_ADMIN`, `ADMIN` | Có (wildcard) | Có |
| `BGD`, `ACCOUNTANT` | Có (`reports.projects.view`) | Có (`reports.projects.export`) |
| `PM` | Có, theo dự án | Không |
| Role khác | Không mặc định | Không mặc định |

Chỉ số có: định danh dự án, PM/khách; tiến độ thiết kế theo
`design-schedule-weighted-v1` (chưa đủ baseline thì `DESIGN_BASELINE_INCOMPLETE`); số
task thi công theo trạng thái và quá hạn (không tính % trung bình); số nghiệm thu theo
trạng thái, số lần sửa, quá hạn (không tính tỷ lệ đạt lần đầu); permit quá hạn, sắp đến hạn,
sắp hết hạn (cửa sổ 30 ngày); tổng báo giá, giá trị hợp đồng gốc, delta VO duyệt, giá trị
hiện tại, giá trị mốc theo trạng thái (ghi nhãn **Contractual schedule**, không phải tiền
mặt, doanh thu hay phải thu).

Chỉ số chưa có trả mã lý do thay vì số 0 giả: `HISTORICAL_SNAPSHOTS_UNAVAILABLE`
(S-curve lịch sử), `CONSTRUCTION_WEIGHTS_UNAVAILABLE`,
`ACCEPTANCE_OUTCOME_DATA_UNAVAILABLE`, `ACTUAL_FINANCE_LEDGER_UNAVAILABLE` (dòng tiền,
doanh thu, chi phí, P&L, phải thu thực tế), `INVENTORY_LEDGER_UNAVAILABLE`,
`BOQ_USAGE_DATA_UNAVAILABLE`, `VENDOR_PERFORMANCE_DATA_UNAVAILABLE`. Dự án thiếu nguồn
thi công/nghiệm thu/permit liên kết thiết kế trả phần đó `Unavailable` với
`SOURCE_NOT_CONFIGURED`.

<a id="719-kpi-framework-and-source-evidence"></a>

### 14.2 KPI và bằng chứng nguồn

KPI hiện có 19 chỉ số trong sáu nhóm (Kinh doanh, Đấu thầu, Thiết kế, Công trường, Cung
ứng, Kế toán dự án). Đây là tập con có nguồn dữ liệu của khung 11 vị trí mục tiêu của
NICON, không được mô tả là đã phủ đủ 11 vị trí. Chỉ số thiếu luồng nguồn là `MissingData`,
không bao giờ thành điểm 0.

- Một kỳ cho mỗi nhân viên/năm/tháng, ranh giới theo `Asia/Ho_Chi_Minh`, lưu UTC.
- Tổng trọng số chỉ số đang hoạt động của một vị trí không quá 100%. Chỉ khoá kỳ khi mọi
  chỉ số đang hoạt động là `Available` và tổng trọng số snapshot bằng 100%. Kỳ đã khoá bất
  biến, không tính lại (mở lại cần luồng riêng được duyệt).
- Snapshot đóng băng mã, phiên bản, trọng số, mục tiêu, chiều tính điểm, ngưỡng, kết quả
  và bằng chứng nguồn.
- Tự tính lúc 01:00 giờ Việt Nam hằng ngày; người có quyền tính tay được. Ngưỡng điểm thấp
  phát tối đa một notification mỗi nhân viên/kỳ.
- `analytics.kpi.view` chỉ xem của mình; `view.all`, `manage`, `export` lần lượt cho xem
  người khác, quản lý định nghĩa/kỳ và xuất.
- UI: `/admin/kpi` (chọn nhân viên/kỳ, tính, xem bằng chứng, xuất, khoá);
  `/admin/kpi/configuration` (trọng số, mục tiêu, chiều, ngưỡng, bật/tắt; cần
  `analytics.kpi.manage`; cờ `RequiresTarget` do backend cung cấp).

| Vị trí | Chỉ số (trọng số) | Nguồn |
|---|---|---|
| Kinh doanh | Tỷ lệ Lead → hợp đồng (40%); doanh thu hợp đồng mới ký (40%, cần mục tiêu); thời gian tương tác đầu tiên (20%, cần mục tiêu) | Lead, hợp đồng Upstream, activity đầu tiên không phải ghi chú |
| Đấu thầu | Tỷ lệ trúng (40%); chuẩn bị đúng hạn (30%); độ chính xác dự toán (30%) | Lần nộp đầu, checklist, dự toán duyệt so BOQ thi công cuối |
| Thiết kế | Giao bản vẽ đúng hạn (40%); duyệt lần đầu (30%); lỗi thiết kế công trường báo (30%, cần mục tiêu) | Lịch thiết kế, revision bản vẽ, Punch đã xác minh có nguyên nhân Design |
| Công trường | Chênh tiến độ (30%); hao hụt vật tư (30%); nghiệm thu lần đầu (20%); vi phạm HSE (20%) — các chỉ số trừ nghiệm thu cần mục tiêu | Task thi công, xuất kho so BOQ cuối, nghiệm thu, HSE đã xác nhận |
| Cung ứng | Tối ưu chi phí mua (40%); thời gian giao (30%, cần mục tiêu); đánh giá chất lượng NCC (30%) | Đơn giá BOQ cuối so dòng hợp đồng Downstream, MR duyệt → phiếu nhập cuối, Vendor Rating PM duyệt |
| Kế toán dự án | Thu đúng hạn (40%); thời gian xử lý thanh toán đối tác (30%, mặc định 72 giờ); độ chính xác số liệu (30%, mặc định 1 điều chỉnh) | Mốc Upstream theo kế toán phụ trách, Payment Request đã kiểm → đã chi, điều chỉnh sau khoá sổ |

Quy tắc nguồn KPI:

1. Lưu rõ `OperationalProjectId`, nhân viên chịu trách nhiệm, thời điểm sự kiện, trạng
   thái, người tạo/sửa, row version. Trách nhiệm ghi tại sự kiện; không suy từ role hay
   đội ngũ hiện tại, cũng không từ người tình cờ ghi nhận thanh toán.
2. Chỉ đếm sự kiện kết thúc (`Approved`, `Confirmed`, `Posted`, `Paid`); nháp, từ chối,
   huỷ, xoá không phải bằng chứng. Khoảng half-open giờ Việt Nam.
3. Bản ghi tài chính/kho đã duyệt hoặc posted bất biến; sửa bằng hoàn hoặc phiên bản mới.
4. Áp quy tắc ghi chung ([6.2](#62-quy-tắc-ghi-dùng-chung)) và audit; JSON bằng chứng lưu
   loại thực thể, ID, phiên bản nguồn, người chịu trách nhiệm, thời điểm.
5. Không có mẫu số hoặc bản ghi duyệt thì `MissingData`.

Giả định triển khai hiện tại (chưa phải quyết định NICON khi baseline 03/10 không nêu hoặc
mâu thuẫn):

| Mã | Giả định |
|---|---|
| D-01 | Seed sáu nhóm quyền chức năng CRM, HR, Procurement, Construction, Design, Finance; Pháp lý dùng nhóm Construction. Không phải sơ đồ tổ chức đã duyệt; cơ cấu năm phòng và vị trí Cung ứng/Pháp lý là Q-04 |
| D-02 | BOQ thi công duyệt mới nhất thành BOQ cuối khi dự án hoàn thành; VO chỉ ảnh hưởng qua revision BOQ được duyệt |
| D-03 | Punch đã xác minh có nguyên nhân Design tính đều cho người thiết kế chịu trách nhiệm; người sửa không tự động là người chịu trách nhiệm |
| D-04 | HSE đã xác nhận tính đều cho người phụ trách công trường; nháp, đã báo, từ chối, huỷ và nhật ký không tính |
| D-05 | Hao hụt = `100 * max(xuất ròng − hạn mức BOQ cuối, 0) / hạn mức`; hạn mức thiếu hoặc 0 không cho điểm giả |
| D-06 | Thời gian giao tính giờ lịch từ duyệt MR tới phiếu nhập cuối đáp ứng đủ |
| D-07 | Mỗi hợp đồng Downstream hoàn thành một phiếu đánh giá; chất lượng, tiến độ, chi phí, HSE bằng trọng số; Cung ứng lập, PM dự án duyệt |
| D-08 | Thu đúng hạn: mốc Upstream đến hạn trong tháng, chỉ `Paid` đủ vào/trước hạn mới đạt |
| D-09 | Xử lý thanh toán tính giờ lịch từ `ValidatedAt` tới `PaidAt` theo kế toán được giao |
| D-10 | Mỗi điều chỉnh sau khoá sổ được duyệt tính một lần cho kế toán của bút toán gốc; hoàn không tính thêm |
| D-11 | Triển khai đủ luồng nguồn cùng lúc; mục tiêu số do admin cấu hình; kỳ không khoá được khi chỉ số thiếu nguồn hoặc cấu hình |

Migration nguồn: `AddPunchRootCauseAttribution`, `AddReceivableAccountability`,
`AddHseViolations`, `AddProcurementControlChain`, `AddFinanceControlWorkflows`. Punch và
mốc cũ chưa phân loại/gán bị loại tới khi được xử lý; không tạo sự kiện duyệt giả hay chia
giá trị hợp đồng cũ thành dòng. Import số dư đầu kỳ cần hash nguồn, người import, thời
điểm, preview, validate và xác nhận. KPI không có hard delete; tắt định nghĩa vẫn giữ bằng
chứng đã khoá.

---

## 15. Dữ liệu tham chiếu của NICON

`docs/Nicon/` lưu hiện vật của khách, chỉ đọc: không đổi tên, chuẩn hoá, dịch hay xuất lại.

- Biên bản `MoMs/2026-10-06/NICON_Meeting_2026-10-03_Tom-tat-yeu-cau-moi.md` là baseline
  nghiệp vụ hiện hành; các file khác cùng thư mục là tổng hợp hỗ trợ, không tự đóng câu
  hỏi mở của baseline.
- PRD đầy đủ mô tả tám module, các bước chuyển giao giữa phòng, 11 vị trí mục tiêu, trọng
  số KPI, cây Drive và kỳ vọng UI. File workflow DOCX là bối cảnh lịch sử.
- PDF tiến độ cho thấy WBS thật (phân cấp, thời lượng, phụ thuộc, việc pháp lý/thiết kế
  song song, mốc, baseline). `BOQ/` gồm workbook một và nhiều sheet.
- `ISO/` (nhận 20/10/2026, 170 file đã mở hoặc phân loại: 108 Word, 40 workbook, 3 PDF,
  2 RAR, 17 file khoá/tạm) gồm quy trình kiểm soát tài liệu, đánh giá nội bộ, cải tiến và
  các quy trình khách hàng, đấu thầu, thiết kế, thi công, thanh toán, mua hàng, thiết bị,
  nhân sự. Không có thư mục `D.5`; hỏi NICON trước khi coi bộ quy trình là đủ.

Nhận file không có nghĩa đã có chứng nhận ISO, phê duyệt phát hành hay nghiệm thu nghiệp
vụ. Nhiều file là hồ sơ dự án đã điền hoặc có sheet ví dụ, không phải mẫu trắng; một số mã
form trùng, thiếu hoặc lệch tên (ví dụ `TC-M02`, `TC-M23`, `TQT-M00`, `TC-M00`, `DT-M03`
không tìm thấy; `TC-M15`, `TC-M24`, `KH-M01` có hai nghĩa; `NS1-M01` thực là phiếu tuyển
dụng; `TQT-M04` có lỗi `=#REF!+C13`). Quy trình `TC` (2016) và `TQT` (2020) mang vai trò,
lịch nộp và thời hạn lưu trữ cũ, không tự thành quy tắc hiện hành; quy trình mua hàng cũ
giao đặt hàng cho Kế toán, còn luồng đã xác nhận giao cho Cung ứng. Giữ workflow hiện tại
cho tới khi chủ nghiệp vụ xác nhận ánh xạ. Hỏi NICON về form thiếu và FA (chị K/Kiara) về
chứng từ thanh toán.

Ứng viên chứng từ cho bốn loại gói Downstream (chưa phải ma trận bắt buộc):

| Gói | Ứng viên | FA/CM còn phải quyết |
|---|---|---|
| Cung cấp vật tư | `MH-M01`–`MH-M04`, đơn hàng, biên bản nhận hàng, hoá đơn; phiếu nhập kho cho hàng thực nhận | Bộ chứng từ theo đợt, nguồn khối lượng duyệt, tạm ứng/giữ lại, ngoại lệ |
| Nhân công | `TC-M27`, `TC-M29`, `TQT-M05`/`TQT-M06`, khối lượng công trường duyệt | Phân biệt tổ đội và thầu phụ; chứng từ hợp đồng/chấm công/nghiệm thu cần thiết |
| Thiết bị | `LK-M01`–`LK-M08` (bàn giao, tồn, bảo dưỡng) | Mua hay thuê, bằng chứng thời gian sử dụng, sở hữu |
| Thầu phụ hỗn hợp | Biểu mẫu nghiệm thu và quyết toán thầu phụ trong TC/TQT | Bằng chứng theo dòng vật tư/nhân công/thiết bị, phân bổ, gate thanh toán không cần phiếu nhập giả |

Kiểm soát tài liệu (`C.1`, `QTC-KSTLHS`): PTBP → ĐDCL → GĐ duyệt; TKISO phát hành và thu
hồi; GĐ duyệt huỷ hồ sơ. Uỷ quyền điện tử cho CM/Admin, phát hành/thay thế và lưu trữ/huỷ
vẫn là quyết định mở; việc publish checklist pháp lý trong sản phẩm không chứng nhận toàn
bộ quy trình QTC. 11 tiêu chí chấm điểm cá nhân trong `NS2-M01` khác 11 vị trí KPI của PRD;
mức giữ lại 5% trong hợp đồng mẫu không phải công thức cảnh báo tiến độ.

Thư mục Drive (PRD "8 lớp" không phải số thư mục cố định; code có chín
`ProjectDocumentCategory`):

| Category | Đường dẫn |
|---|---|
| Survey | `01_Khao_sat` |
| CRM Pre-Design | `01_CRM_PreDesign` |
| Design Concept | `02_Thiet_ke/01_So_bo_Concept` |
| Design Basic | `02_Thiet_ke/02_Co_so` |
| Design Shop Drawing | `02_Thiet_ke/03_Chi_tiet_ShopDrawing` |
| Legal | `03_Xin_phep_Phap_ly` |
| Construction/Acceptance | `04_Thi_cong_Nghiem_thu` |
| Procurement | `05_Cung_ung_Vat_tu` |
| Finance/Contracts | `06_Tai_chinh_Hop_dong` |

Mục tiêu nghiệp vụ đặt Survey dưới CRM Pre-Design. Giữ hai đường dẫn hiện tại cho tới khi có
migration topology được duyệt (tắt sync, xả claim, kiểm kê binding, xử lý xung đột, di
chuyển/rebind, kiểm checksum, có rollback); đổi cấu hình không di chuyển file cũ.

Ranh giới import:

- **Tiến độ:** profile parser có phiên bản và preview trước khi lưu; giữ hash file, ID
  dòng/task nguồn, tên, cấp, thời lượng, ngày kế hoạch, milestone, predecessor (quan hệ,
  lag), lịch, ràng buộc, baseline. Từ chối cha/predecessor thiếu, vòng, ngày/thời lượng
  sai, ID trùng, kết thúc trước bắt đầu. Import tạo phiên bản Draft; ban hành là bước
  riêng; import lại cùng file idempotent; file đổi tạo phiên bản mới, không ghi đè tiến độ
  thực tế.
- **Workbook BOQ:** nhận diện mọi sheet (kể cả ẩn) theo thứ tự; phân loại sheet bằng
  mapping người dùng duyệt; chỉ đọc kết quả công thức đã lưu và báo thiếu/cũ thay vì tính
  lại; chuẩn hoá sau preview; validate theo ô; đối chiếu tổng chi tiết với tổng hợp; lưu
  hash, người import, thời điểm, phiên bản mapping và kết quả từng dòng; chỉ tạo Draft sau
  khi xác nhận, BOQ đã duyệt không đổi khi import lỗi hay dở dang. Giới hạn dung lượng,
  số sheet/dòng, công thức, macro và thời gian xử lý phải cấu hình rõ, có thông báo bốn
  ngôn ngữ.

---

## 16. Frontend

- API gọi qua module có kiểu trong `src/services/`: `authApi.ts`, `contentApi.ts`;
  `adminApi.ts` là facade tương thích phía trên các client `services/admin/*Api.ts` và
  kiểu trong `services/admin/types/`. Mở rộng client của tính năng, không phình facade;
  client chuyên biệt như `rbacApi.ts` vẫn là nguồn chuẩn.
- Vị trí: tab chi tiết hợp đồng `pages/admin/contracts/`; gói công việc
  `pages/admin/work-packages/`; form và panel cung ứng
  `pages/admin/procurement/components/`. Nghiệp vụ nằm trong hook/client, trình bày ở
  view.
- Giao diện chính thức là **light** (chủ sản phẩm xác nhận). Dùng token ngữ nghĩa
  (foreground, surface, border, primary), không dùng class slate/white cố định; khung
  dark token không phải theme đã giao.
- Nhãn: một control dùng `FieldScope` với `Label` và Input/Textarea/SelectTrigger/
  SearchableSelect; có sẵn id thì dùng `htmlFor`/`id`. `FieldControl` chỉ để bọc control
  native/tuỳ chỉnh. Mỗi dòng lặp có scope riêng; không lấy placeholder làm tên truy cập;
  hint của picker không đè nhãn. `npm run check:a11y` kiểm liên kết nhãn.
- `useConfirm` cho xác nhận đơn giản; xoá aggregate dùng `DeletionImpactDialog`.
  `useUiError` giữ lý do validate/xung đột an toàn từ server, dịch lỗi mạng, ẩn lỗi nội bộ.
  Tải file lỗi đọc JSON Blob có giới hạn qua helper dùng chung.
- Danh sách hiển thị phân trang theo tổng thật (`ListPagination`, `useListPage`,
  `useDebouncedValue`, `useRequestGuard` bỏ response cũ). `collectLookupPages` chỉ dùng
  cho option tham chiếu: đi hết trang server, nhận `total`/`totalCount`, từ chối kết quả
  thiếu.
- Link tài liệu không chèn thẳng vào anchor: qua `src/lib/url.ts` (chỉ nhận đường dẫn
  bắt đầu một `/` hoặc URL HTTP(S)).
- Xuất CSV qua `src/lib/exportCsv.ts` và `AdminExportButton.tsx` (UTF-8 BOM, vô hiệu hoá
  tiền tố công thức). Xuất phải giữ bộ lọc và thứ tự, đủ toàn bộ kết quả, tắt khi không có
  dòng. Danh sách nhỏ xuất dữ liệu đã tải; danh sách vừa có thể lấy hết trang; dữ liệu lớn
  hoặc cần phân quyền/audit dùng endpoint backend (ví dụ
  `GET /api/as-built-documents/export`, `GET /api/handover-records/export`).
- Khi reload, SPA lấy user qua `GET /users/me` với token hiện có; chỉ xoay refresh token
  sau `401` của access token. Lỗi tạm thời giữ token và cho thử lại; phiên không hợp lệ
  về trang đăng nhập.

| Lệnh | Mục đích |
|---|---|
| `npm run dev` / `build` / `build:dev` / `preview` | Dev server, build production/dev, xem bản build |
| `npm run lint`, `npm run check:a11y` | ESLint, kiểm nhãn |
| `npm run format`, `format:check` | Định dạng |
| `npm run test:unit` | Kiểm thử helper |
| `npm run test:e2e` | Playwright |

---

## 17. Kiểm thử

### 17.1 Phân tầng

| Tầng | Chứng minh | Không chứng minh |
|---|---|---|
| Unit (`nihomebackend.tests`) | Logic, validate, công thức, ngưỡng, mapping | HTTP, quyền, persistence |
| Integration (`nihomebackend.integration.tests/Controllers`) | Status code, model binding, phân quyền, middleware, persistence, trạng thái không đổi khi từ chối | Ràng buộc SQL, khoá, rollback (EF InMemory) |
| SQL cô lập (`NICON_SQL_TEST_CONNECTION`) | Unique index, race, rollback thật; mỗi test tạo/xoá database riêng (`Nicon*Test_<GUID>`) | — |
| Browser E2E (`nihomeweb/e2e/smoke`) | Render, luồng người dùng, wiring trên stack Compose | Request API thuần trong Playwright không phải bằng chứng thao tác UI |

Drive, email và quyền sở hữu ngoài chỉ chứng minh được trên dịch vụ đã cấu hình; mock
không chứng minh truyền tải. Ghi kết quả chạy và blocker ở task/PR hoặc artifact, không
thêm báo cáo có ngày vào `docs/`. Mỗi luồng cần thử sai role/dự án/owner, field sai, nhảy
trạng thái, thiếu lý do, version cũ, replay idempotent, payload xung đột, và đổi phụ thuộc
sau khi form đã tải; browser kiểm lỗi/rỗng/đang tải/thử lại, giữ dữ liệu form, bàn phím,
mobile/tablet, bản dịch và dữ liệu sau reload.

```bash
dotnet test nihomebackend.tests/nihomebackend.tests.csproj
dotnet test nihomebackend.integration.tests/nihomebackend.integration.tests.csproj
```

Chạy trong container .NET 8 SDK (backend Compose chỉ mount `nihomebackend/`). Cả hai
project test loại `**/TestResults/**` khỏi MSBuild; giữ regression trong thư mục test được
track.

### 17.2 Browser E2E

```bash
docker compose up -d --build
cd nihomeweb
BASE_URL=http://localhost:5043 npx playwright test
CI=1 BASE_URL=http://localhost:5043 npx playwright test --shard=1/2   # tái hiện shard CI
```

- CI chạy cho mọi pull request (kể cả PR chồng) và push lên `main`; chia hai shard, mỗi
  shard một stack Compose riêng với một worker vì spec dùng chung dữ liệu seed và thay đổi
  RBAC/cấu hình.
- Spec có fixture SQL bắt buộc `E2E_SQL_CONTAINER` (không có mặc định, tránh ghi nhầm vào
  database dev dùng chung).
- Luồng Design duyệt bằng chứng cần Google Drive đã kết nối; nếu chưa, spec báo skip.
  Skip không phải bằng chứng Drive chạy đúng.
- Kiểm điều hướng phải xem nội dung trang đích, không chỉ URL. Test lịch sử BOQ trên mobile
  có timeout 90 giây vì chờ hai lượt worker email.
- Output Playwright để ngoài thư mục frontend (ví dụ `--output=/tmp/...`) để watcher không
  restart backend. Spec tiêu biểu: `admin-asbuilt.spec.ts`, `admin-handover.spec.ts`,
  `crm-business-pipeline.spec.ts`, `construction-handover-pipeline.spec.ts`,
  `rfq-business-pipeline.spec.ts`.

### 17.3 E2E gửi email qua hộp thư giả lập

```bash
BUILD_SHA=$(git rev-parse HEAD) docker compose \
  -f docker-compose.yaml -f docker-compose-e2e.yaml up -d --build
cd nihomeweb
BASE_URL=http://localhost:5043 MAIL_SINK_URL=http://127.0.0.1:8025 \
  E2E_SQL_CONTAINER=nihome31042025-sqlserver \
  npx playwright test e2e/smoke/direct-contract-from-opportunity.spec.ts \
  e2e/smoke/quote-email-delivery.spec.ts
```

Dùng người nhận `example.test`, không bao giờ dùng SMTP production. Xem chậm:
`PLAYWRIGHT_SLOW_MO=1500` với `--headed --workers=1`. Xong thì khôi phục backend bằng
`docker compose up -d --force-recreate nihomeBackend`, rồi
`docker compose -f docker-compose.yaml -f docker-compose-e2e.yaml stop smtpSink` và
`... rm -f smtpSink`. Job CI `e2e` đặt
`COMPOSE_FILE=docker-compose.yaml:docker-compose-e2e.yaml` và `MAIL_SINK_URL`.

### 17.4 Kiểm tra tĩnh và thủ công

```bash
docker exec nihome31042025-backend dotnet format --no-restore --verify-no-changes   # container có SDK
cd nihomeweb && npm run lint
python tools/translation_keys/check_translation_keys.py
python -m unittest discover -s tools/translation_keys -p "test_*.py"
```

- Container chạy `dotnet format` cần `shm_size` 1 GB (mặc định 64 MB làm Roslyn crash
  `AccessViolationException`); đổi `shm_size` phải recreate bằng
  `docker compose up -d --no-deps nihomeBackend`.
- Script translation quét key ở frontend/backend và key từ seed notification, master data,
  RBAC, kiểm đủ bốn ngôn ngữ. Thiếu key trả exit `1`, lỗi đọc trả `2`; job CI
  `translations (missing keys)` đăng bảng lên GitHub Job Summary.
- Smoke thủ công: đăng nhập `SUPER_ADMIN` tại `http://localhost:5043/login`, đi qua
  `/admin/construction/asbuilt` (tạo, lọc, xuất `as-built-documents-YYYY-MM-DD.csv`, trình,
  duyệt, archive) và `/admin/construction/handover` (tạo, readiness, hoàn thành cần người
  ký, mở lại, `409` khi sửa đồng thời, xuất CSV đủ dòng). Tài khoản `SALE` phải bị chặn;
  nếu không, kiểm role được gán trong database lâu năm trước khi báo lỗi.
- Sau sửa cuối, chỉ chạy nhóm test liên quan, không mặc định full suite. Thiếu môi trường
  thì ghi Not Run, không lấy kết quả cũ thay thế. Thay đổi chỉ tài liệu không cần khởi động
  Docker hay chạy test code.

---

## 18. Triển khai

### 18.1 Môi trường dev

`docker compose up --build -d`; backend 5043, SQL 1433 (`sqlserver_data`). Kiểm
`/api/system/health` có `buildSha` đúng revision.

### 18.2 Checklist production

1. Đổi mọi mật khẩu mặc định; đổi hoặc tắt tài khoản seed.
2. Xoay khoá ký JWT.
3. `ASPNETCORE_ENVIRONMENT=Production`; không bật Swagger bằng cách chuyển sang
   Development.
4. CORS chỉ cho domain frontend production; đặt `AllowedHosts` là tên miền production.
5. Dùng secrets manager cho credential database, SMTP, JWT. Bật HTTPS.
6. Không dùng `sa`; tạo user SQL riêng quyền hạn chế.
7. Thu thập log JSON (mỗi dòng một object) và giám sát.
8. Review script migration; startup tự chạy `Database.Migrate()` và seed.
9. Build bằng `auto-deployment.sh` → `deployment-config/output/publish-release.zip`
   (backend + SPA cho IIS, kèm `web.config`). `NIHOMEWEB_DIST_PATH` ghi đè thư mục SPA;
   thư mục không tồn tại thì startup lỗi.
10. Job publish trên CI hiện chưa khai báo phụ thuộc vào build, test, E2E và security;
    kiểm các job đó đã qua trước khi dùng artifact.
11. Kiểm font PDF: trên IIS cài Arial, Microsoft YaHei, Meiryo cho tài khoản application
    pool hoặc đặt các biến `NIHOME_PDF_FONT_*` (và `_INDEX` cho TTC).
12. Ghi revision/`buildSha` và phiên bản migration khi deploy; smoke rồi UAT với NICON.

### 18.3 Google Drive trên IIS

Đồng bộ tài liệu dùng Google Drive API v3 với OAuth user. Toàn bộ cấu hình nghiệp vụ ở
**Settings > Google Drive**, lưu trong dòng `google_drive_credentials`; client secret và
refresh token được mã hoá bằng Data Protection, chỉ ghi, không bao giờ trả qua API.
Không đưa giá trị OAuth vào config trong git, log hay ảnh chụp. Không có cấu hình thì coi
như tắt, worker không gọi Drive. Drive settings cố ý không đọc từ `appsettings.json` hay
`GoogleDrive__*`; chỉ đường dẫn key ring nằm ở hạ tầng.

Tạo OAuth credential: trong Google Cloud Console bật **Google Drive API**; Auth Platform
audience External (khi Testing, thêm tài khoản sở hữu thư mục làm test user); chỉ scope
`https://www.googleapis.com/auth/drive`; tạo client **Web application** (Desktop không
dùng được); đăng ký đúng callback, ví dụ
`https://nicon.example.com/api/site-settings/google-drive/oauth/callback`; lấy
`<FOLDER_ID>` của thư mục gốc. Không tạo refresh token thủ công. Trước khi dùng thật, đưa
app sang Production (token ở chế độ Testing có hạn).

Sau khi giải nén gói trên IIS:

1. Đặt `DataProtection:KeysPath` (hoặc `DataProtection__KeysPath`) tới thư mục bền, ví dụ
   `D:\NiconSecrets\DataProtection-Keys`, chỉ application pool và admin có quyền Modify.
   Mất key ring thì secret trong SQL không giải mã được, phải nhập lại secret và kết nối
   lại.
2. Người có `system.settings.manage` nhập OAuth client, callback, đường dẫn trả về nội bộ,
   thư mục gốc, `InstanceId` (ổn định, duy nhất mỗi môi trường), tên ứng dụng, đường dẫn
   các thư mục nghiệp vụ, chế độ tương thích, chu kỳ polling, chính sách đồng bộ xoá và
   trạng thái bật. Để trống secret thì giữ secret cũ; đổi client ID/secret xoá kết nối hiện
   có.
3. Bấm **Connect Google Drive** (popup; callback kiểm state, PKCE, quyền admin và quyền ghi
   thư mục gốc). Kiểm trạng thái `Connected`, upload thử. Cấu hình có hiệu lực ngay, không
   cần recycle.

| Trạng thái | Nghĩa |
|---|---|
| `Connected` | Đã xác thực, thư mục gốc tồn tại và được thêm file |
| `ReadOnly` | Thấy thư mục nhưng không được thêm |
| `InvalidRoot` | `RootFolderId` không phải thư mục hoặc nằm trong thùng rác |
| `ReconnectRequired` | Refresh token thiếu, hết hạn, bị thu hồi hoặc không giải mã được; admin kết nối lại |
| `Unavailable` | Lỗi xác thực hoặc truy cập thư mục |

- `FrontendReturnUrl` phải là đường dẫn nội bộ `/admin/` (không backslash, fragment, ký tự
  điều khiển), mặc định quay về `/admin/settings?tab=drive`.
- Đổi tài khoản: **Disconnect** rồi **Connect**, hoặc **Switch Google account**. NICON xoá
  credential local trước khi yêu cầu Google thu hồi; thu hồi lỗi thì admin nhận hướng dẫn
  thu hồi tay.
- Đổi topology khi đã có binding: lưu tắt đồng bộ, chờ hết `Processing`, lưu thư mục gốc/
  định danh/đường dẫn mới khi vẫn tắt, rồi bật lại ở lần lưu riêng. Server từ chối đổi
  topology khi đang xử lý hoặc binding đang bật. File cũ trên Drive không tự di chuyển.
  Bản nâng cấp từ cấu hình cũ khởi động ở trạng thái tắt; admin nhập lại và kết nối lại
  trong cửa sổ bảo trì.
- Chính sách xoá: **Web → Drive** mặc định tắt (xoá trên web giữ file và thư mục Drive);
  bật thì xoá tài liệu đưa replica đã xác minh vào thùng rác Drive, xoá aggregate chỉ xoá
  item qua kiểm sở hữu. **Drive → Web** mặc định bật (file Drive mất thì đánh dấu bản ghi
  đã xoá). Cả hai nằm trong plan xoá aggregate. Chia sẻ/đồng bộ quyền Drive tắt cho tới khi
  IT cung cấp mapping nhóm được duyệt.
- Upload tay bắt buộc `Idempotency-Key`; fingerprint tính trên field và byte file (không
  theo boundary), nên retry cùng ý định tạo tối đa một dòng catalog và một file Drive.
  Form giữ key khi lỗi, đổi key khi đổi file, category hoặc dự án. Scope dự án kiểm trước.
- Mỗi tài liệu tối đa ba lần claim tự động mỗi vòng; **Retry** (kể cả sau lần ba) reset
  vòng. Đang `Processing` thì không retry hay xoá được. Đồng bộ tắt hoặc thư mục gốc không
  ghi được thì không claim, không tốn lượt. Ứng dụng dừng giữa chừng thì lease hết hạn tiếp
  tục đúng lần thử đó. Upload sidecar cũ có heartbeat 5 phút; mất claim thì huỷ request
  Drive, generation fencing chặn hoàn tất cũ; file trùng replica key và generation do race
  được đưa vào thùng rác, giữ bản SQL đang trỏ.
- Docker dev: lưu cấu hình qua UI với callback loopback, key ring nằm ở volume
  `data_protection_keys`; áp dụng bằng
  `docker compose up -d --build --force-recreate nihomeBackend`.

### 18.4 Dữ liệu demo

Seeder chỉ chạy ở Development: hai lead, năm khách hàng/dự án, sáu cơ hội, tám báo giá, sáu
hợp đồng, ba dự án thiết kế, hai khảo sát, hai gói thầu, đi từ concept trước hợp đồng tới
bàn giao. Chuỗi chính là Nhà máy may An Phú (KCN Quế Võ, hư cấu); Nhà máy cơ khí Hòa Bình
có hai RFQ, BOQ điện đã duyệt, vendor và báo giá cạnh tranh. Tên hiển thị không có tiền tố
`[SAMPLE]`; dữ liệu demo nhận diện bằng mã ổn định và marker nội bộ (`[SAMPLE_CONTRACT]`,
`[SAMPLE_DP]`), bản ghi con chỉ gắn vào dự án mẫu có marker. KPI seed chỉ là định nghĩa,
không phải kết quả. Seed idempotent: chạy lại giữ số dòng và nội dung/trạng thái/quan hệ
admin đã sửa; thu nhỏ manifest không xoá dòng đã seed; backfill chỉ điền quan hệ còn thiếu
(Sales thuộc user SALE, dự án và thi công ưu tiên PM, tài liệu thiết kế ưu tiên DESIGN_LEAD
rồi DESIGN, pháp lý ưu tiên LEGAL_OFFICER). Khi có web root, seed tạo PDF giữ chỗ dưới
`wwwroot/files/capability/`, `files/contracts/`, `files/asbuilt/`. Mọi tên, số điện thoại,
email trong dữ liệu mẫu là giả.

---

## 19. Xử lý sự cố

| Triệu chứng | Kiểm tra |
|---|---|
| Backend không kết nối database | `docker ps | grep sqlserver`; connection string theo [4.1](#41-database). Health check Compose chỉ kiểm process SQL tồn tại, cần thử kết nối thật |
| Migration lỗi | Dùng SDK có `dotnet-ef` 8.x; `dotnet ef migrations list`; chỉ `remove` migration cuối chưa áp; sinh `script` để review |
| Lỗi CORS | `Frontend:AllowedOrigins` khớp chính xác giao thức và port; restart backend |
| Email không gửi | Cấu hình SMTP, mạng từ container tới SMTP, log MailKit; trạng thái `NeedsReview` của hàng chờ |
| `401` khi đã đăng nhập | Token hết hạn → refresh; `Issuer`/`Audience` khớp; `ActiveKeyId` trỏ tới khoá có thật |
| Sửa code không có tác dụng / E2E chạy binary cũ | `BUILD_SHA=$(git rev-parse HEAD) docker compose up -d --build --force-recreate nihomeBackend`, kiểm `buildSha` trước khi chạy Playwright. Không xoá `sqlserver_data` |
| Đổi bản dịch không hiện | Cần data migration có guard giá trị cũ và restart backend (cache) |

---

## 20. Kế hoạch hoàn thiện theo yêu cầu NICON

### 20.1 Phạm vi và nguyên tắc

Kế hoạch phủ đủ 14 mục của
[baseline họp 03/10/2026](Nicon/MoMs/2026-10-06/NICON_Meeting_2026-10-03_Tom-tat-yeu-cau-moi.md),
đọc cùng `Nicon_BreakTask_v1.xlsx` và hiện vật khách hàng. Các xác nhận mới hơn được giữ:
hai đường tạo hợp đồng Upstream (16/10), BOQ cấu hình (17/10), phản hồi CM (18/10), bộ ISO
(20/10), uỷ quyền quyết định S-Curve (09/11). Không quay lại quy tắc đã bị thay, không coi
code hiện tại là quyết định mới.

- Mọi gói là **Target**; chỉ ghi **Partial** khi có bằng chứng một phần, nhánh chưa có
  người có thẩm quyền quyết giữ **Open decision**. Không đánh dấu **Implemented** từ kế
  hoạch, endpoint, menu hay file test chưa chạy.
- Đích bàn giao là luồng nghiệp vụ đầy đủ: dữ liệu, API có phân quyền, UI, tài liệu, seed
  và kiểm thử. Ưu tiên CRM + Thiết kế; sửa quyền và bảo toàn bằng chứng liên module trước.
- Không tự mở rộng sang ERP, tái tạo Excel tuỳ ý, import `.mpp` đầy đủ hay ACL Drive ngoài
  phạm vi NICON đã xác nhận. Đầu việc Target trong BreakTask (báo cáo tài chính/kho, chức
  năng chung) vẫn phải được map và xếp đợt, không âm thầm bỏ.
- Không tự đặt ngày hoàn thành hay effort khi chưa chốt đầu vào; đội phát triển và Product
  Owner phân bổ theo năng lực thực tế. Cột estimate/status của workbook không phải lịch cam
  kết.

### 20.2 Quyết định còn mở

Huỳnh Anh là đầu mối khách hàng; Product Owner hoặc người NICON uỷ quyền chốt quy tắc;
CM/FA/Thiết kế xác nhận nội dung chuyên môn; IT xác nhận triển khai, Drive và lưu trữ. Mỗi
quyết định ghi người quyết, ngày/nguồn, phương án, tác động dữ liệu cũ và tiêu chí nghiệm
thu.

| Mã | Cần chốt | Gói bị ảnh hưởng |
|---|---|---|
| Q-01 | Tiền hợp đồng theo nhóm việc; phân bổ khi một dự án có nhiều hợp đồng | ROOT-01, WORK-01, COMM-01 |
| Q-02 | Việc nội bộ có cần duyệt; chỉ số, trọng số, kỳ khoá KPI | WORK-01, REPORT-01 |
| Q-03 / Q-04 | Ai giao việc liên phòng; cơ cấu năm phòng, vị trí Cung ứng/Pháp lý; bộ quyền mẫu | RBAC-01, ROOT-01, WORK-01 |
| Q-05 | Quy ước mã dự án, chuẩn hoá, thời điểm được đổi/khoá | CRM-01 |
| Q-06 / Q-07 / Q-10 | Người nhận chỉ góp ý hay phải đồng thuận; CC mặc định; ranh giới Lead duyệt việc và Manager chốt giai đoạn | DESIGN-01, DESIGN-02 |
| Q-08 | Ai được xoá hồ sơ không đạt; hồ sơ đã trình khách; lưu giữ bằng chứng | SAFE-02, RBAC-01, DRIVE-03 |
| Q-09 | Permit áp dụng theo loại/quy mô, bắt buộc/miễn, phiên bản mẫu | LEGAL-01 |
| Q-11 | Màn Đội ngũ/Tiến độ; phạm vi WBS/lịch/baseline/import | DESIGN-03 |
| ISO / CM / FA | Ánh xạ quy trình → bước → form; mã thiếu/trùng; chứng từ theo gói Supply/Labour/Equipment/Mixed, tạm ứng | ISO-01, CM-01, FIELD-01/02, QS-01, FIN-01 |
| Duyệt / công thức | Ma trận duyệt MR/QA-QC/HSE/QS/VO; giá trị thực hiện; hạn mức BOQ; KPI còn thiếu (S-Curve dự án đã chốt theo uỷ quyền 09/11) | CM-01, SUPPLY-01, REPORT-01 |
| Hiệu lực duyệt | Huỷ/rút gói Finance có thu hồi duyệt không; thay đổi nào buộc duyệt lại | COMM-01, TENDER-01 |
| Nguồn báo cáo | Thu/chi thực tế, chi phí, P&L, kho theo kỳ; tìm kiếm toàn cục, lưu bộ lọc | FIN-02, REPORT-01, COMMON-01 |
| Chính sách tài liệu | Ai làm gì với tài liệu nào; lưu byte lịch sử; import vào hồ sơ; upload riêng IFC; migration Survey. Chia sẻ/ACL Drive giữ tắt | SAFE-01/02, DRIVE-02/03 |

Quy tắc đã rõ làm trước, không chờ cả bảng. Phần chưa chốt không được publish thành mẫu bắt
buộc hay âm thầm chọn mặc định. Không yêu cầu NICON gửi lại ISO/BOQ/MS Project đã nhận; chỉ
hỏi phần còn thiếu.

### 20.3 Thứ tự bàn giao

| Đợt | Gói | Điều kiện chuyển đợt |
|---|---|---|
| 0 | PLAN-01, ISO-01 (khảo sát) | Truy vết từng yêu cầu; tách lỗi, khoảng trống và quyết định mở; mỗi quyết định có người chịu trách nhiệm |
| 1 | SAFE-01/02/03, DRIVE-01, chặn của TENDER-01, QA-01 | Hết lỗ hổng đã biết về quyền, xoá bằng chứng, duyệt cũ, sửa kế hoạch đã kết thúc |
| 2 | RBAC-01, ROOT-01, WORK-01, CRM-01, COMMON-01 (khảo sát) | Một root dự án; phòng/role đúng quyết định; giao việc và notification đúng scope |
| 3 | DESIGN-01/02/03/04, DRIVE-02/03 | Luồng Cơ hội → Concept/Basic/Shop/IFC có bằng chứng, tài liệu đúng phiên bản |
| 4 | COMM-01, BOQ-01, TENDER-01 | Giá đầu vào → báo giá/Finance → hợp đồng; thầu có snapshot bất biến |
| 5 | LEGAL-01, ISO-01 (publish) | Permit/task/kích hoạt không trùng; form có nguồn và phiên bản được duyệt |
| 6 | CM-01, FIELD-01/02, SUPPLY-01, QS-01 | Gate khởi công, baseline, chuyển nguồn lực, QA/QC/HSE/QS duyệt độc lập |
| 7 | FIN-01/02, REPORT-01, COMMON-01 | Hồ sơ thanh toán hai chiều đủ ba phần; báo cáo có nguồn |
| 8 | MIG-01, RELEASE-01 | Rehearsal/rollback, tài liệu, demo, UAT NICON trên bản deploy xác định; không còn lỗi High/Critical |

Khảo sát đợt sau có thể chạy song song. Hợp đồng QS/Finance và template phải thiết kế thống
nhất trước. Phần Tài chính – Kế toán trước hợp đồng thuộc COMM-01. LEGAL-01 và IFC cấp bằng
chứng cho CM-01; baseline ban hành và BOQ thi công đã duyệt cấp hạn mức cho SUPPLY-01.
SAFE và DRIVE áp dụng xuyên suốt.

### 20.4 Các gói và tiêu chí hoàn thành

| Gói | Nội dung chính | Hoàn thành khi |
|---|---|---|
| PLAN-01 | Map từng mục MoM/BreakTask → vai trò, quy tắc, vòng đời, dữ liệu/API/UI → tiêu chí và test | Mỗi khoảng trống có gói xử lý, mỗi quyết định có người chịu trách nhiệm; BA duyệt cơ sở test trước QA |
| SAFE-01 | Phân quyền thống nhất: quyền + thành viên dự án + quyền nguồn cho tài liệu, Design, permit, task | Người được giao làm được; người ngoài dự án, đã hết hạn tham gia hoặc chỉ CC bị chặn; list/đếm/tải không lộ dữ liệu |
| SAFE-02 | Bảo toàn bằng chứng khi xoá, thay file, sửa/xoá từ Drive | Từ chối không đổi dữ liệu/file/lịch sử; hồ sơ đã duyệt không trỏ sang byte mới; mọi hard delete theo mục 7 |
| SAFE-03 | Duyệt gắn đúng nội dung: không lách `Pending`, duyệt cũ mất hiệu lực khi phạm vi/người/lịch đổi | Đúng một người duyệt độc lập; CC không duyệt; trả về có lý do; gate không nhận duyệt cũ |
| QA-01 | Đưa probe tái hiện vào regression; sửa test lỗi thời và fixture | Test hiện có pass trên source cuối; không sửa câu chữ để test xanh |
| RBAC-01 | Role/nhóm theo phòng, nhãn sửa được, phạm vi duyệt và xoá | Role mới nhận bộ quyền đúng; không nâng quyền hay reset role người dùng; DESIGN_LEAD không vô tình có quyền xoá cao nhất |
| ROOT-01 | Operational Project là root; Kinh doanh tạo, các phòng nhận workspace liên kết bằng ID | Không tạo lại khách/dự án theo phòng; không tự gắn việc vào hợp đồng đầu tiên |
| WORK-01 | Giao việc phòng ban/nội bộ: đúng người/phòng, lịch, WBS, phụ thuộc, tiến độ, KPI theo Q-02 | Không tạo trùng việc/notification; kết thúc assignment chặn thao tác cũ; notification có retry và link đúng |
| CRM-01 | Thứ tự Dự án/Cơ hội/Hợp đồng; Lead riêng, khoá sau convert; cá nhân không bắt mã số thuế; đổi mã có lịch sử | Validate ở FE và server; đổi mã đồng thời không mất liên kết; cả hai đường tạo hợp đồng Upstream hợp lệ |
| DESIGN-01 | Kinh doanh chuyển sang Concept trước hợp đồng; Manager điều phối, Lead chia và duyệt, người khác CC | Chuyển giao idempotent; tác giả không tự duyệt; người mất scope không xử lý được |
| DESIGN-02 | Trình nội bộ/khách: người nhận được chỉ định, tổng hợp, ghi quyết định khách, gate giai đoạn | Người nhận ngoài dự án chỉ mở đúng review; góp ý không thành duyệt; file đúng revision/hash |
| DESIGN-03 | Tiến độ và đội ngũ: WBS, ngày, người, phụ thuộc, lịch, critical path | Không làm phẳng PDF/MPP thành checklist; phân trang đủ dữ liệu trên 100 bản ghi; giao diện mobile/tablet |
| DESIGN-04 | Thầu phụ thiết kế, nhiều file mỗi giai đoạn, IFC tới Thi công | IFC chưa phát hành không dùng thi công; không mở dữ liệu NCC ngoài scope |
| DRIVE-01 | Độ tin cậy cấu hình: health khi giải mã lỗi, URL trả về an toàn, thứ tự đổi tên, chặn đổi topology khi đang xử lý | Phân biệt đúng các trạng thái kết nối; secret chỉ ghi; không log token |
| DRIVE-02 | Khởi tạo thư mục bền khi chuyển giao, upload mọi giai đoạn có kiểm checksum | Drive lỗi không rollback nghiệp vụ; retry không tạo trùng; kiểm thật trên Drive, không chỉ upload General |
| DRIVE-03 | Phủ tài liệu cho Tender, Finance, nhật ký/Punch, QA/QS | Module nguồn sở hữu vòng đời file; import Drive không tự thành bằng chứng Design đã duyệt |
| COMM-01 | Giá đầu vào, Finance đúng phiên bản báo giá/thầu, gate gửi/nộp | Sai mục đích, offer hết hạn/chưa duyệt, tự duyệt, nội dung cũ đều bị chặn; giá đầu vào không thành hạn mức BOQ thi công |
| BOQ-01 | Báo giá đơn giá/BOQ, phiên bản, tổng/VAT/chiết khấu; import workbook có preview, mapping, hash | Import lỗi không ghi dở; giữ file gốc; không hứa tính lại Excel tuỳ ý |
| TENDER-01 | Thư mời → kế hoạch phòng → hồ sơ/dự toán/Finance → nộp → kết quả | Snapshot tái dựng được lần nộp; khoá sau kết quả; nộp lại tạo lần mới, retry không trùng |
| ISO-01 | Map ISO thực tế thành form/checklist được duyệt, publish độc lập | CM/FA/NICON duyệt mapping; phiên bản đã publish bất biến; mẫu mới không sửa hồ sơ cũ |
| LEGAL-01 | Permit theo áp dụng (sau Q-09), PM phân công, chủ trì giao từng permit | Kích hoạt nguyên tử, không trùng permit/task; bằng chứng thiếu/hết hạn chặn gate |
| CM-01 | WBS/Gantt/lịch/S-Curve, tổ chức BCH, kế hoạch nguồn lực, Budget Baseline có phiên bản | Gate khởi công đủ điều kiện ở [13.3](#133-module-4-target-integration-contract); không ghi đè baseline đã ban hành |
| FIELD-01 | BCH thực hiện, CM review độc lập; form ISO theo áp dụng | Tác giả không tự review; nháp/offline không tạo trùng hay tự gộp dữ liệu đã duyệt |
| FIELD-02 | QA/QC, HSE, Punch, nhật ký, nghiệm thu, hoàn công, bàn giao giữ vòng đời riêng | Hoàn thành không bỏ qua nghiệm thu/hoàn công bắt buộc hay Punch chặn |
| SUPPLY-01 | Vật tư/thiết bị/nhân công: CM cấp 2 cho vượt mức, Cung ứng lập hợp đồng/cung cấp, FA thanh toán | CM duyệt không đồng nghĩa đã mua/chi; retry không tạo hợp đồng/thanh toán trùng; phi vật tư không ép phiếu nhập kho |
| QS-01 | Khối lượng hai hướng gắn hợp đồng, mốc, hồ sơ phải thu/phải trả | Không suy hướng từ dấu tiền; không đếm trùng bản bị thay/hoàn |
| FIN-01 | Hồ sơ thanh toán hai chiều, ba phần Công trường/CM/Kế toán | Đủ ba phần và chứng từ mới gửi khách/chi; không phiếu nhập giả; giữ unique hoá đơn và duyệt độc lập |
| FIN-02 | Nguồn thu/chi thực tế, chi phí, P&L, kho theo dự án/kỳ (không dựng sổ kế toán) | Hồ sơ duyệt không đồng nghĩa đã thu/chi; thiếu nguồn không báo số 0 |
| REPORT-01 | Tiến độ danh mục, QS hai hướng, giá trị thực hiện so ngân sách, QA/QC/HSE, KPI | S-Curve dự án theo 13.2; tổng hợp danh mục chưa thuộc slice; KPI khác cần chốt nguồn |
| COMMON-01 | Master data, bản dịch, notification/audit, xuất, tìm kiếm/lọc | Không trả bản ghi hay số đếm ngoài scope; giữ bộ lọc khi reload/back |
| MIG-01 | Kiểm kê ID, phiên bản, binding, quyền tuỳ chỉnh, tài liệu cũ; migration chỉ thêm | Dry-run, checkpoint, rollback, rehearsal; dữ liệu cũ thiếu bằng chứng ghi rõ chưa đủ |
| RELEASE-01 | Cập nhật hướng dẫn, RBAC, seed, bản dịch; deploy xác định; UAT; bàn giao PDF quy trình kịch bản A có ảnh, nhận phản hồi qua Sheet | `buildSha`/migration khớp; đóng hết High/Critical; có bằng chứng UAT và bàn giao riêng |

Role tuỳ chỉnh hợp lệ phải tham gia workflow theo permission và assignment, không hardcode
role mẫu SALE/PM/DESIGN. Test âm phải chứng minh role tuỳ chỉnh không nâng quyền, nhảy phòng,
đọc nguồn ngoài scope hay tự duyệt.

### 20.5 Nguyên tắc kỹ thuật cho các gói

- Giữ ASP.NET/EF/React và workflow bền hiện có; không thêm broker, microservice hay
  dependency khi intent/outbox trong database đủ dùng. Ghi nghiệp vụ và intent nguyên tử;
  worker gọi Google ngoài transaction dài, có idempotency, fencing, retry và lỗi quan sát
  được.
- Mở rộng tối thiểu: thêm liên kết root dùng chung thay vì viết lại module thành task chung;
  giữ snapshot JSON có schema version khi đủ; cần cấu trúc mới thì migration chỉ thêm.
- Duyệt và nộp là bằng chứng của đúng revision. Chưa có chính sách lưu byte lịch sử thì phát
  hiện và chặn khi lệch, không phục vụ byte mới dưới danh nghĩa bằng chứng cũ.
- Retry tài liệu/notification không được chạy lại chuyển trạng thái hay gửi trùng.
- Form có trạng thái đang tải/rỗng/lỗi/thành công/xung đột, responsive, số nhập dễ đọc (như
  `100.000`). Dữ liệu chọn lấy từ master data API; bản dịch qua `/admin/translations` đủ
  vi/en/zh/ja, ghi đủ ở mọi đường tạo/import/seed/migration, không fallback lúc đọc.
- Email mới theo [6.3](#63-email-ứng-dụng).

### 20.6 Kiểm thử và nghiệm thu

| Rủi ro | Tầng thấp nhất | Bằng chứng |
|---|---|---|
| Validate, version/hash, công thức đã chốt | Unit | Biên, thiếu, sai, trùng; ngưỡng chính xác |
| Role × dự án/nguồn × thao tác; mất assignment; người duyệt/CC/tác giả | Integration HTTP | Không lộ list/đếm/nội dung; từ chối giữ nguyên dữ liệu, file, lịch sử, notification |
| Rowversion, mã/hoá đơn unique, race gate–snapshot, xoá–duyệt bằng chứng | SQL cô lập | Hai thao tác cạnh tranh, một kết quả hợp lệ; không thay bằng InMemory |
| Import BOQ/tiến độ/ISO, migration | Fixture từ nguồn + integration | Mapping/tổng/cấp/độ chính xác đúng; lỗi không ghi dở; file gốc nguyên vẹn |
| Lead → Cơ hội/Dự án → Thiết kế/giá đầu vào → Báo giá/Finance → Hợp đồng | Integration + browser | Liên kết nguồn và gate âm ở API; đúng role và deep link trên UI |
| Thư mời → kế hoạch/hồ sơ → nộp → trúng → hợp đồng | Integration/SQL + browser | Snapshot cũ bất biến; khoá sau kết quả |
| Thiết kế hoàn thành → Lead duyệt → người nhận → khách → IFC → Thi công | Integration + browser | Một người duyệt; người nhận ngoài dự án chỉ đọc review được giao |
| Kích hoạt hợp đồng → permit → readiness; BCH → CM → Cung ứng/FA → bàn giao | Integration/SQL + browser | Review độc lập; sai hướng/dự án/nguồn bị chặn an toàn |
| Hồ sơ Upstream gửi khách, Downstream chi | Unit + integration + browser | Đủ ba phần; email preview khi gửi thật; không phiếu nhập giả |
| Drive upload/thay/import/sửa/xoá/retry | Fake-provider và Drive thật tách riêng | Checksum, sở hữu, dọn host; mock không tính là OAuth/Drive thật |
| BCH mobile, CM desktop/tablet, bốn ngôn ngữ | E2E Compose | 390/768/1440px không tràn, không lỗi JS; nộp trùng/mất response/kết nối lại không mất nháp |

Mỗi gói qua `.github/skills/senior-business-analyst/SKILL.md` rồi
`.github/skills/business-functional-qa/SKILL.md`, ưu tiên reviewer độc lập. BA duyệt là cơ sở
test, chưa phải tính năng sẵn sàng; rủi ro yêu cầu chỉ chủ nghiệp vụ được chấp nhận.

### 20.7 Migration, rollout và tài liệu

1. Kiểm kê và dry-run trước khi đổi schema, quyền, ID hay topology; backup dữ liệu, key
   ring, manifest file/binding/checksum (không đưa secret vào Git); rehearsal trên bản SQL
   sao chép.
2. Triển khai schema/backend tương thích trước, backfill có checkpoint, kiểm rồi mới bật
   UI/worker/gate. Không bật gate còn thiếu quyết định, không tự đánh dấu dữ liệu cũ là đủ
   hồ sơ.
3. Đổi root/instance/đường dẫn Drive cần cửa sổ bảo trì, dừng phát sinh intent và xả
   `Processing`.
4. Rollback tắt tính năng/worker mới, giữ bằng chứng và lịch sử. Email đã gửi, thanh toán đã
   ghi, file đã xoá vĩnh viễn không hoàn tác được.
5. Seed/demo dùng tình huống NICON có tên có nghĩa, đủ bốn ngôn ngữ, không giả duyệt; seed
   bổ sung idempotent, không reset nội dung/quyền/bản dịch admin đã sửa.
6. Cập nhật `user_guide.md` và tài liệu này khi hành vi, API hay vận hành đổi; không sửa hiện
   vật khách thành lịch sử triển khai; báo cáo chạy và ảnh chụp để ở artifact/PR, không thêm
   vào `docs/Nicon/`. Không tự xoá hiện vật khách để qua kiểm tra layout.
7. Mỗi gói một nhánh và commit tập trung; PR nêu vấn đề, hành vi, tiêu chí, test, migration,
   rủi ro còn lại. Deploy ghi revision/`buildSha` và migration, smoke rồi UAT với NICON qua
   đầu mối được chỉ định; trạng thái host không suy từ nhánh local.

Kế hoạch chỉ đóng khi mọi tiêu chí trong phạm vi đã xác nhận có bằng chứng, quyết định mở
được xử lý đúng thẩm quyền và NICON hoàn tất UAT; số test pass hay trạng thái Drive
`Connected` không thay được điều kiện này.

| Mục MoM 03/10 | Gói chịu trách nhiệm |
|---|---|
| 1 Quyền/vai trò | RBAC-01, SAFE-01/02 |
| 2 CRM | CRM-01, ROOT-01 |
| 3 Dự án/việc chung | ROOT-01, WORK-01, REPORT-01 |
| 4 Thiết kế trước hợp đồng | DESIGN-01, DRIVE-02 |
| 5 Liên phòng trước hợp đồng | COMM-01, SUPPLY-01 |
| 6 Báo giá/BOQ | BOQ-01 |
| 7 Đấu thầu | TENDER-01, COMM-01, WORK-01 |
| 8 Thiết kế | DESIGN-01/02/03/04, SAFE, DRIVE |
| 9 Pháp lý | LEGAL-01, ISO-01, SAFE-01 |
| 10 Thi công | CM-01, FIELD-01/02, SUPPLY-01, QS-01 |
| 11 Tài chính | COMM-01, FIN-01/02, QS-01, REPORT-01 |
| 12 Giao hàng/Drive | DRIVE-01/02/03, RELEASE-01 |
| 13 Hiện vật NICON | PLAN-01, ISO-01, BOQ-01, DESIGN-03, LEGAL-01 |
| 14 Điểm chưa rõ | Bảng 20.2 và gói bị ảnh hưởng |
