# Gói A phần 1 — Nối chuỗi CRM: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nối thông chuỗi Lead → Khách hàng + Cơ hội → Báo giá → Hợp đồng → Dự án thiết kế, để luồng nghiệp vụ đi được đầu-cuối trong buổi demo.

**Architecture:** Toàn bộ thay đổi bám vào entity và endpoint đã có. Backend bổ sung một endpoint unconvert, một endpoint tạo dự án thiết kế từ hợp đồng, và bốn trường dẫn xuất trên `ContractResponse` lấy bằng LEFT JOIN. Frontend bổ sung dialog chuyển đổi, khối dự án thiết kế trên chi tiết hợp đồng, và prefill form hợp đồng từ báo giá. **Không có EF migration nào trong plan này.**

**Tech Stack:** ASP.NET Core 8, EF Core (SQL Server; InMemory cho unit test), xUnit + Moq, React 18 + TypeScript, Vite, shadcn/ui, react-router-dom.

**Spec:** `docs/superpowers/specs/2026-08-20-crm-design-package-a-design.md`

## Global Constraints

- **Không được tạo EF migration.** Container backend đang chạy thiếu `dotnet-ef`. Mọi trường mới phải là trường dẫn xuất trong DTO, không phải cột mới. Nếu một task tưởng như cần đổi schema thì dừng lại và báo, đừng tự thêm cột.
- **Không hardcode chuỗi hiển thị trong React.** Mọi text phải qua `t("key")` từ `useI18n()`.
- **Mỗi key i18n mới phải có đủ bốn ngôn ngữ** `vi`, `en`, `zh`, `ja`, thêm vào file seed tương ứng trong `nihomebackend/Data/Seeds/i18n/`. Restart backend để `TranslationSeeder` upsert vào DB.
- **Transaction chỉ mở khi provider là quan hệ**, qua `db.Database.IsRelational()`. Xem mục "Tính nguyên tử của convert" ngay dưới. Ngoài Task 1, mọi chỗ khác giữ pattern sẵn có: mỗi đơn vị công việc là một `SaveChangesAsync`.
- **Truy vấn chỉ đọc dùng `AsNoTracking()`.**
- **Test chạy trong container SDK dùng một lần, không phải container backend.**
  Container `nihome31042025-backend` chỉ mount `nihomebackend/`, nên nó không nhìn
  thấy `nihomebackend.tests/`. CI dùng `actions/setup-dotnet` trên máy chủ với toàn
  bộ repo, và lệnh tương đương ở local là:

  ```bash
  docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
    dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true
  ```
  Cờ `-p:SkipNihomeWebBuild=true` là **bắt buộc**: `nihomebackend.csproj` có target
  chạy `npm ci` trước khi build, mà container SDK không có npm — thiếu cờ này thì
  build đứt với `MSB3073 ... npm: not found`. CI cũng truyền đúng cờ đó.


  Chạy từ thư mục gốc repo. `dotnet build` và `dotnet format` thì vẫn dùng
  `docker exec nihome31042025-backend` được, vì chúng chỉ đụng `nihomebackend/`.
- Chạy `docker exec nihome31042025-backend dotnet format` trước khi commit backend.
- Chạy `cd nihomeweb && npm run lint` trước khi commit frontend.
- Branch từ `main`, commit theo 50/72 (subject ≤ 50 ký tự, thân ≤ 72 ký tự/dòng).
- Chỉ stage file liên quan tới commit đó. Không `git add -A`.

## Tính nguyên tử của convert

Spec A1 yêu cầu convert nằm trọn trong một transaction. Bản plan đầu định bỏ
transaction vì codebase chưa có chỗ nào dùng và EF InMemory trong unit test không
hỗ trợ — nhưng cách đó để lại rủi ro thật: nếu tiến trình chết giữa hai lần
`SaveChangesAsync`, Customer và Opportunity tồn tại còn Lead thì chưa `Converted`.
Lần thử lại sẽ bắt được Customer trùng, **nhưng Opportunity mồ côi thì vẫn nằm đó**
và người dùng không có giao diện nào để gắn nó vào đâu cả.

Vì vậy Task 1 mở transaction thật, có rào theo provider:

```csharp
var useTransaction = db.Database.IsRelational();
```

Production chạy SQL Server nên `IsRelational()` trả `true` và convert là nguyên tử
đúng như spec. Unit test chạy InMemory nên trả `false` và bỏ qua transaction, không
cần đổi `DbContextFactory`. Đây là lần đầu codebase dùng transaction tường minh —
có chủ đích, vì đây cũng là chỗ đầu tiên một thao tác tạo hai aggregate cùng lúc.

Thứ tự ghi vẫn giữ nguyên: tạo Customer và Opportunity trước, đóng dấu Lead sau.
Thứ tự ngược lại sẽ để lead `Converted` trỏ vào bản ghi chưa tồn tại.

## File Structure

**Backend — sửa:**
- `nihomebackend/Models/DTOs/Requests/ConvertLeadRequest.cs` — thêm trường doanh nghiệp cho A1
- `nihomebackend/Models/DTOs/Requests/UnconvertLeadRequest.cs` — **tạo mới**, payload cho A2
- `nihomebackend/Models/DTOs/Responses/LeadResponse.cs` — thêm kết quả nhánh unconvert
- `nihomebackend/Services/ILeadService.cs` + `LeadService.cs` — logic A1 và A2
- `nihomebackend/Controllers/LeadsController.cs` — endpoint unconvert
- `nihomebackend/Models/DTOs/Responses/ContractResponses.cs` — bốn trường dự án thiết kế (A5)
- `nihomebackend/Services/ContractService.cs` — projection bốn trường (A5)
- `nihomebackend/Controllers/ContractsController.cs` — endpoint tạo dự án thiết kế (A6)

**Frontend — sửa:**
- `nihomeweb/src/services/adminApi.ts` — type và hàm gọi API mới
- `nihomeweb/src/pages/admin/Leads.tsx` — dialog chuyển đổi, nút hoàn tác
- `nihomeweb/src/pages/admin/ContractDetail.tsx` — khối dự án thiết kế
- `nihomeweb/src/pages/admin/Contracts.tsx` — nhận prefill từ báo giá, gửi `quoteId`/`opportunityId`
- `nihomeweb/src/pages/admin/QuoteDetail.tsx` — nút tạo hợp đồng
- `nihomeweb/src/pages/admin/DesignProjects.tsx` — lọc hợp đồng đã có dự án

**Seed i18n — sửa:**
- `nihomebackend/Data/Seeds/i18n/leads.json`
- `nihomebackend/Data/Seeds/i18n/contracts.json`
- `nihomebackend/Data/Seeds/i18n/quotes.json`
- `nihomebackend/Data/Seeds/i18n/design-projects.json`

**Test — sửa/tạo:**
- `nihomebackend.tests/Services/LeadServiceTests.cs`
- `nihomebackend.tests/Services/ContractServiceTests.cs`

---

## Task 1: Lead convert tạo Customer và Opportunity (A1, backend)

**Files:**
- Modify: `nihomebackend/Models/DTOs/Requests/ConvertLeadRequest.cs`
- Modify: `nihomebackend/Services/LeadService.cs:287-334` (`ConvertAsync`)
- Test: `nihomebackend.tests/Services/LeadServiceTests.cs`

**Interfaces:**
- Consumes: `Lead`, `Customer`, `CustomerContact`, `Opportunity` models; `LeadOperationException`.
- Produces: `ConvertLeadRequest` với các trường mới `TaxId`, `Address`, `RepresentativeName`. `ConvertAsync` giữ nguyên chữ ký `Task<LeadResponse?> ConvertAsync(int id, ConvertLeadRequest request, int callerUserId, bool canConvert, CancellationToken ct = default)` — Task 3 và Task 2 dựa vào chữ ký này.

- [ ] **Step 1: Viết test thất bại cho đường tạo mới cả hai**

Thêm vào `nihomebackend.tests/Services/LeadServiceTests.cs`, trong vùng test convert:

```csharp
[Fact]
public async Task ConvertAsync_CreatesCustomerAndOpportunity_WhenNoIdsGiven()
{
    var sales = await SeedUserAsync(UserRole.USER);
    SeedSource("marketing");
    var lead = await SeedLeadAsync(LeadStatus.Interested, ownerId: sales.Id);

    var response = await _sut.ConvertAsync(
        lead.Id,
        new ConvertLeadRequest(),
        sales.Id,
        canConvert: true);

    Assert.NotNull(response);
    Assert.NotNull(response!.ConvertedCustomerId);
    Assert.NotNull(response.ConvertedOpportunityId);

    var customer = await _db.Customers
        .Include(c => c.Contacts)
        .SingleAsync(c => c.Id == response.ConvertedCustomerId);
    Assert.Equal(CustomerType.Individual, customer.Type);
    Assert.Equal("Ms. Nga", customer.Name);
    Assert.Equal("marketing", customer.SourceCode);
    Assert.Equal(CustomerRelationshipStatus.Prospect, customer.RelationshipStatus);
    Assert.Equal(sales.Id, customer.OwnerUserId);

    var contact = Assert.Single(customer.Contacts);
    Assert.True(contact.IsPrimary);
    Assert.Equal("0900000000", contact.Phone);

    var opportunity = await _db.Opportunities
        .SingleAsync(o => o.Id == response.ConvertedOpportunityId);
    Assert.Equal(customer.Id, opportunity.CustomerId);
    Assert.Equal(OpportunityStage.Prospecting, opportunity.Stage);
    Assert.Equal(sales.Id, opportunity.OwnerUserId);
}
```

- [ ] **Step 2: Chạy test, xác nhận thất bại**

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~ConvertAsync_CreatesCustomerAndOpportunity"
```

Kỳ vọng: FAIL — `response.ConvertedCustomerId` là `null` vì `ConvertAsync` hiện chỉ gán `request.CustomerId`.

- [ ] **Step 3: Mở rộng `ConvertLeadRequest`**

Thay toàn bộ nội dung `nihomebackend/Models/DTOs/Requests/ConvertLeadRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>
/// Payload cho POST /api/leads/{id}/convert.
///
/// Ngữ nghĩa của <see cref="CustomerId"/> và <see cref="OpportunityId"/>:
/// có giá trị nghĩa là "gắn vào bản ghi có sẵn", để trống nghĩa là "tạo mới".
///
/// Ba trường doanh nghiệp chỉ bắt buộc khi lead có CompanyName và
/// <see cref="CustomerId"/> để trống — vì <c>CustomerService.ValidateForType</c>
/// yêu cầu khách hàng loại Company phải đủ MST, địa chỉ, người đại diện, mà model
/// Lead lại không mang hai trường đầu.
/// </summary>
public class ConvertLeadRequest
{
    /// <summary>Id of an already-existing customer to link the lead to.</summary>
    public int? CustomerId { get; set; }

    /// <summary>Id of an already-created opportunity spawned from this lead.</summary>
    public int? OpportunityId { get; set; }

    [StringLength(50)]
    public string? TaxId { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(200)]
    public string? RepresentativeName { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}
```

- [ ] **Step 4: Viết lại `ConvertAsync`**

Thêm `using Microsoft.EntityFrameworkCore.Storage;` vào đầu `LeadService.cs` để có
`IDbContextTransaction`. Rồi trong `nihomebackend/Services/LeadService.cs`, thay thân
`ConvertAsync` từ dòng `var now = DateTime.UtcNow;` cho tới trước `return MapLead(...)` bằng:

Nếu bất kỳ nhánh nào ở giữa ném exception, transaction chưa commit sẽ rollback khi
`DbContext` bị dispose ở cuối request — không cần try/catch riêng.

```csharp
        // Một mốc thời gian duy nhất cho cả ba bản ghi. A2 dựa vào dấu trùng khít
        // này để nhận ra bản ghi nào do convert sinh ra — xem spec A2. Đừng thay
        // bằng nhiều lần gọi DateTime.UtcNow.
        var now = DateTime.UtcNow;

        // Convert tạo hai aggregate rồi mới đóng dấu lead, nên nó phải nguyên tử.
        // EF InMemory trong unit test không hỗ trợ transaction, vì vậy rào theo
        // provider: production (SQL Server) có transaction, test thì không.
        IDbContextTransaction? transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        // Cơ hội có sẵn đã thuộc về một khách hàng. Không cho phép tạo khách hàng
        // mới rồi gắn vào nó.
        int? customerIdFromOpportunity = null;
        if (request.OpportunityId.HasValue)
        {
            var existingOpportunity = await db.Opportunities.AsNoTracking()
                .Where(o => o.Id == request.OpportunityId.Value)
                .Select(o => new { o.Id, o.CustomerId })
                .FirstOrDefaultAsync(ct);

            if (existingOpportunity is null)
            {
                throw new LeadOperationException($"Opportunity #{request.OpportunityId} not found.");
            }

            if (request.CustomerId.HasValue && request.CustomerId.Value != existingOpportunity.CustomerId)
            {
                throw new LeadOperationException(
                    "CustomerId must match the opportunity's customer, or be omitted.");
            }

            customerIdFromOpportunity = existingOpportunity.CustomerId;
        }

        var linkedCustomerId = request.CustomerId ?? customerIdFromOpportunity;

        Customer? createdCustomer = null;
        Opportunity? createdOpportunity = null;

        if (linkedCustomerId.HasValue)
        {
            var customerExists = await db.Customers
                .AnyAsync(c => c.Id == linkedCustomerId.Value, ct);
            if (!customerExists)
            {
                throw new LeadOperationException($"Customer #{linkedCustomerId} not found.");
            }
        }
        else
        {
            await EnsureNoDuplicateCustomerAsync(lead, request, ct);
            createdCustomer = BuildCustomerFromLead(lead, request, callerUserId, now);
            db.Customers.Add(createdCustomer);
        }

        if (!request.OpportunityId.HasValue)
        {
            createdOpportunity = new Opportunity
            {
                Name = string.IsNullOrWhiteSpace(lead.CompanyName)
                    ? $"Cơ hội từ lead {lead.Name}"
                    : $"Cơ hội từ lead {lead.CompanyName}",
                Stage = OpportunityStage.Prospecting,
                OwnerUserId = lead.OwnerUserId,
                EstimatedValue = 0m,
                WinProbability = 0,
                CreatedAt = now,
                CreatedByUserId = callerUserId,
                UpdatedAt = now,
                UpdatedByUserId = callerUserId,
            };

            // Gán qua navigation property khi khách hàng cũng mới, để EF chèn
            // khách hàng trước rồi mới chèn cơ hội trong cùng một SaveChanges.
            if (createdCustomer is not null)
            {
                createdOpportunity.Customer = createdCustomer;
            }
            else
            {
                createdOpportunity.CustomerId = linkedCustomerId!.Value;
            }

            db.Opportunities.Add(createdOpportunity);
        }

        // Save #1 — khách hàng và cơ hội. Lead được đóng dấu sau chứ không phải
        // trước, để một lần hỏng không để lại lead trỏ vào bản ghi chưa tồn tại.
        if (createdCustomer is not null || createdOpportunity is not null)
        {
            await db.SaveChangesAsync(ct);
        }

        lead.Status = LeadStatus.Converted;
        lead.ConvertedAt = now;
        lead.ConvertedCustomerId = createdCustomer?.Id ?? linkedCustomerId;
        lead.ConvertedOpportunityId = createdOpportunity?.Id ?? request.OpportunityId;
        lead.UpdatedAt = now;
        lead.UpdatedByUserId = callerUserId;

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            db.LeadActivities.Add(new LeadActivity
            {
                LeadId = lead.Id,
                Type = LeadActivityType.Note,
                Content = $"[Convert] {request.Note.Trim()}",
                CreatedByUserId = callerUserId,
                CreatedAt = now,
            });
        }

        db.LeadActivities.Add(new LeadActivity
        {
            LeadId = lead.Id,
            Type = LeadActivityType.Note,
            Content =
                $"[Convert] customerId={lead.ConvertedCustomerId}, " +
                $"opportunityId={lead.ConvertedOpportunityId}",
            CreatedByUserId = callerUserId,
            CreatedAt = now,
        });

        // Save #2 — chỉ đụng vào lead.
        await db.SaveChangesAsync(ct);

        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
            await transaction.DisposeAsync();
        }
```

- [ ] **Step 5: Thêm helper dựng khách hàng từ lead**

Thêm vào vùng helper riêng ở cuối `LeadService.cs`, cạnh các private helper sẵn có:

```csharp
    private static Customer BuildCustomerFromLead(
        Lead lead,
        ConvertLeadRequest request,
        int callerUserId,
        DateTime now)
    {
        var isCompany = !string.IsNullOrWhiteSpace(lead.CompanyName);

        if (isCompany &&
            (string.IsNullOrWhiteSpace(request.TaxId)
             || string.IsNullOrWhiteSpace(request.Address)
             || string.IsNullOrWhiteSpace(request.RepresentativeName)))
        {
            throw new LeadOperationException(
                "Company leads require TaxId, Address and RepresentativeName to convert.");
        }

        return new Customer
        {
            Type = isCompany ? CustomerType.Company : CustomerType.Individual,
            Name = isCompany ? lead.CompanyName!.Trim() : lead.Name.Trim(),
            TaxId = string.IsNullOrWhiteSpace(request.TaxId) ? null : request.TaxId.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            RepresentativeName = isCompany
                ? request.RepresentativeName!.Trim()
                : null,
            SourceCode = lead.SourceCode,
            RelationshipStatus = CustomerRelationshipStatus.Prospect,
            OwnerUserId = lead.OwnerUserId,
            CreatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedAt = now,
            UpdatedByUserId = callerUserId,
            Contacts = new List<CustomerContact>
            {
                new()
                {
                    FullName = lead.Name.Trim(),
                    Phone = lead.Phone,
                    Email = lead.Email,
                    IsPrimary = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                },
            },
        };
    }
```

- [ ] **Step 6: Tôn trọng cơ chế chống trùng khách hàng**

Spec A1 yêu cầu convert không được đẻ ra khách hàng trùng. `CustomerService` đã có
sẵn quy tắc và cả kiểu exception — tái dùng đúng kiểu đó để controller trả 409
giống hệt đường tạo khách hàng thông thường, chứ đừng phát minh mã lỗi mới.

Thêm helper vào `LeadService.cs`, cạnh `BuildCustomerFromLead`:

```csharp
    /// <summary>
    /// Cùng quy tắc với <c>CustomerService.EnsureNoDuplicateAsync</c>: Company đối
    /// chiếu theo TaxId, Individual đối chiếu theo phone của liên hệ chính. Ném
    /// đúng <see cref="CustomerDuplicateException"/> để controller trả 409 kèm
    /// bản ghi đang trùng, cho FE mời người dùng gắn vào khách hàng có sẵn.
    /// </summary>
    private async Task EnsureNoDuplicateCustomerAsync(
        Lead lead,
        ConvertLeadRequest request,
        CancellationToken ct)
    {
        var isCompany = !string.IsNullOrWhiteSpace(lead.CompanyName);
        Customer? conflict = null;
        var field = string.Empty;
        var value = string.Empty;

        if (isCompany && !string.IsNullOrWhiteSpace(request.TaxId))
        {
            var taxId = request.TaxId.Trim();
            conflict = await db.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.TaxId == taxId, ct);
            if (conflict is not null) { field = "TaxId"; value = taxId; }
        }
        else if (!isCompany && !string.IsNullOrWhiteSpace(lead.Phone))
        {
            var phone = lead.Phone.Trim();
            conflict = await db.Customers.AsNoTracking()
                .Include(c => c.Contacts)
                .Where(c => c.Type == CustomerType.Individual)
                .FirstOrDefaultAsync(c => c.Contacts.Any(ct2 => ct2.IsPrimary && ct2.Phone == phone), ct);
            if (conflict is not null) { field = "Phone"; value = phone; }
        }

        if (conflict is null) return;

        throw new CustomerDuplicateException(new CustomerDuplicateResponse
        {
            Field = field,
            Value = value,
            ExistingCustomerId = conflict.Id,
            ExistingCustomerName = conflict.Name,
            Message =
                $"Khách hàng có {field} '{value}' đã tồn tại (#{conflict.Id} — {conflict.Name}). "
                + "Hãy chuyển đổi bằng cách gắn vào khách hàng này.",
        });
    }
```

Rồi map sang 409 trong `nihomebackend/Controllers/LeadsController.cs`. Thêm khối catch
này vào action `Convert`, **đặt trước** khối `catch (LeadOperationException ex)` sẵn có:

```csharp
        catch (CustomerDuplicateException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "lead.convert",
                ResourceType = EntityTypes.Lead,
                ResourceId = id.ToString(),
                Message = ex.Detail.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Detail.Message,
            });
            return Conflict(ex.Detail);
        }
```

Viết test đi kèm:

```csharp
[Fact]
public async Task ConvertAsync_RejectsDuplicateIndividualByPrimaryPhone()
{
    var sales = await SeedUserAsync(UserRole.USER);
    SeedSource("marketing");
    var lead = await SeedLeadAsync(LeadStatus.Interested, ownerId: sales.Id);

    _db.Customers.Add(new Customer
    {
        Type = CustomerType.Individual,
        Name = "Trùng số",
        SourceCode = "marketing",
        Contacts = new List<CustomerContact>
        {
            new() { FullName = "Trùng số", Phone = "0900000000", IsPrimary = true },
        },
    });
    await _db.SaveChangesAsync();

    var ex = await Assert.ThrowsAsync<CustomerDuplicateException>(() => _sut.ConvertAsync(
        lead.Id, new ConvertLeadRequest(), sales.Id, canConvert: true));

    Assert.Equal("Phone", ex.Detail.Field);
    Assert.Single(_db.Customers);
    Assert.Empty(_db.Opportunities);

    var saved = await _db.Leads.SingleAsync(l => l.Id == lead.Id);
    Assert.NotEqual(LeadStatus.Converted, saved.Status);
}
```

Chạy: `docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true --filter "FullyQualifiedName~RejectsDuplicateIndividual"` — kỳ vọng PASS.

- [ ] **Step 7: Chạy test, xác nhận pass**

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~ConvertAsync_CreatesCustomerAndOpportunity"
```

Kỳ vọng: PASS.

- [ ] **Step 8: Viết test khoá ràng buộc timestamp A1↔A2**

Test này là bắt buộc theo spec — nó bảo vệ dấu hiệu duy nhất mà A2 dùng để nhận diện bản ghi auto-created.

```csharp
[Fact]
public async Task ConvertAsync_StampsIdenticalTimestampOnAllThreeRows()
{
    var sales = await SeedUserAsync(UserRole.USER);
    SeedSource("marketing");
    var lead = await SeedLeadAsync(LeadStatus.Interested, ownerId: sales.Id);

    var response = await _sut.ConvertAsync(
        lead.Id, new ConvertLeadRequest(), sales.Id, canConvert: true);

    var saved = await _db.Leads.SingleAsync(l => l.Id == lead.Id);
    var customer = await _db.Customers.SingleAsync(c => c.Id == response!.ConvertedCustomerId);
    var opportunity = await _db.Opportunities.SingleAsync(o => o.Id == response.ConvertedOpportunityId);

    // A2 nhận diện bản ghi auto-created bằng đúng dấu trùng khít này.
    // Nếu test này đỏ, A2 sẽ ngừng xoá được và luôn rơi về nhánh gỡ liên kết.
    Assert.Equal(saved.ConvertedAt, customer.CreatedAt);
    Assert.Equal(saved.ConvertedAt, opportunity.CreatedAt);
}
```

- [ ] **Step 9: Viết test cho lead doanh nghiệp thiếu trường**

```csharp
[Fact]
public async Task ConvertAsync_RejectsCompanyLeadWithoutTaxIdAddressRepresentative()
{
    var sales = await SeedUserAsync(UserRole.USER);
    SeedSource("marketing");
    var lead = await SeedLeadAsync(LeadStatus.Interested, ownerId: sales.Id);
    lead.CompanyName = "Công ty Alpha";
    await _db.SaveChangesAsync();

    var ex = await Assert.ThrowsAsync<LeadOperationException>(() => _sut.ConvertAsync(
        lead.Id, new ConvertLeadRequest(), sales.Id, canConvert: true));

    Assert.Contains("TaxId", ex.Message);
    Assert.Empty(_db.Customers);
    Assert.Empty(_db.Opportunities);
}

[Fact]
public async Task ConvertAsync_CreatesCompanyCustomer_WhenCompanyFieldsSupplied()
{
    var sales = await SeedUserAsync(UserRole.USER);
    SeedSource("marketing");
    var lead = await SeedLeadAsync(LeadStatus.Interested, ownerId: sales.Id);
    lead.CompanyName = "Công ty Alpha";
    await _db.SaveChangesAsync();

    var response = await _sut.ConvertAsync(
        lead.Id,
        new ConvertLeadRequest
        {
            TaxId = "0101234567",
            Address = "12 Nguyễn Trãi, Hà Nội",
            RepresentativeName = "Ms. Nga",
        },
        sales.Id,
        canConvert: true);

    var customer = await _db.Customers.SingleAsync(c => c.Id == response!.ConvertedCustomerId);
    Assert.Equal(CustomerType.Company, customer.Type);
    Assert.Equal("Công ty Alpha", customer.Name);
    Assert.Equal("0101234567", customer.TaxId);
    Assert.Equal("Ms. Nga", customer.RepresentativeName);
}
```

- [ ] **Step 10: Viết test cho tổ hợp bị chặn và cho đường gắn khách hàng có sẵn**

```csharp
[Fact]
public async Task ConvertAsync_RejectsNewCustomerLinkedToExistingOpportunity()
{
    var sales = await SeedUserAsync(UserRole.USER);
    SeedSource("marketing");
    var lead = await SeedLeadAsync(LeadStatus.Interested, ownerId: sales.Id);

    var otherCustomer = new Customer
    {
        Type = CustomerType.Individual,
        Name = "Khách cũ",
        SourceCode = "marketing",
    };
    _db.Customers.Add(otherCustomer);
    await _db.SaveChangesAsync();

    var opportunity = new Opportunity { Name = "Cơ hội cũ", CustomerId = otherCustomer.Id };
    _db.Opportunities.Add(opportunity);
    await _db.SaveChangesAsync();

    var ex = await Assert.ThrowsAsync<LeadOperationException>(() => _sut.ConvertAsync(
        lead.Id,
        new ConvertLeadRequest { OpportunityId = opportunity.Id, CustomerId = 999999 },
        sales.Id,
        canConvert: true));

    Assert.Contains("must match", ex.Message, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public async Task ConvertAsync_ReusesExistingCustomer_AndCreatesOpportunityOnly()
{
    var sales = await SeedUserAsync(UserRole.USER);
    SeedSource("marketing");
    var lead = await SeedLeadAsync(LeadStatus.Interested, ownerId: sales.Id);

    var existing = new Customer
    {
        Type = CustomerType.Individual,
        Name = "Khách cũ",
        SourceCode = "marketing",
    };
    _db.Customers.Add(existing);
    await _db.SaveChangesAsync();

    var response = await _sut.ConvertAsync(
        lead.Id,
        new ConvertLeadRequest { CustomerId = existing.Id },
        sales.Id,
        canConvert: true);

    Assert.Equal(existing.Id, response!.ConvertedCustomerId);
    Assert.Single(_db.Customers);
    var opportunity = await _db.Opportunities.SingleAsync();
    Assert.Equal(existing.Id, opportunity.CustomerId);
}
```

- [ ] **Step 11: Chạy toàn bộ test của LeadService**

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~LeadServiceTests"
```

Kỳ vọng: PASS toàn bộ. Nếu có test cũ đỏ vì `ConvertAsync` giờ tạo bản ghi thật, sửa test cũ cho khớp hành vi mới — đừng nới hành vi mới cho vừa test cũ.

- [ ] **Step 12: Format và commit**

```bash
docker exec nihome31042025-backend dotnet format
git add nihomebackend/Models/DTOs/Requests/ConvertLeadRequest.cs \
        nihomebackend/Services/LeadService.cs \
        nihomebackend/Controllers/LeadsController.cs \
        nihomebackend.tests/Services/LeadServiceTests.cs
git commit -m "Create customer and opportunity on lead convert

Convert previously only stamped ids the caller supplied, so a
converted lead pointed at nothing. It now builds both records and
stamps one shared timestamp across all three rows, which is how
unconvert later tells auto-created rows apart.

Company leads carry no TaxId or Address, so the request now takes
those three fields and rejects the convert without them rather
than failing deeper in customer validation."
```

---

## Task 2: Dialog chuyển đổi có trường doanh nghiệp (A1, frontend)

**Files:**
- Modify: `nihomeweb/src/services/adminApi.ts` (interface `ConvertLeadRequest`)
- Modify: `nihomeweb/src/pages/admin/Leads.tsx:860-880` (dialog xác nhận chuyển đổi)
- Modify: `nihomebackend/Data/Seeds/i18n/leads.json`

**Interfaces:**
- Consumes: `ConvertLeadRequest` backend từ Task 1 (`taxId`, `address`, `representativeName`).
- Produces: dialog gửi payload đầy đủ. Task 4 tái dùng chính `convertOpen` state này.

- [ ] **Step 1: Mở rộng type frontend**

Trong `nihomeweb/src/services/adminApi.ts`, tìm `interface ConvertLeadRequest` và thay bằng:

```ts
export interface ConvertLeadRequest {
  customerId?: number | null;
  opportunityId?: number | null;
  taxId?: string | null;
  address?: string | null;
  representativeName?: string | null;
  note?: string | null;
}
```

- [ ] **Step 2: Thêm state cho ba trường doanh nghiệp**

Trong `nihomeweb/src/pages/admin/Leads.tsx`, cạnh `const [convertOpen, setConvertOpen] = useState(false);`:

```tsx
  const [convertTaxId, setConvertTaxId] = useState("");
  const [convertAddress, setConvertAddress] = useState("");
  const [convertRepresentative, setConvertRepresentative] = useState("");

  // Lead có tên công ty thì khách hàng sinh ra là loại Company, mà Company bắt
  // buộc đủ MST + địa chỉ + người đại diện ở backend.
  const convertNeedsCompanyFields = Boolean(detail?.companyName?.trim());
  const convertCompanyFieldsMissing =
    convertNeedsCompanyFields &&
    (!convertTaxId.trim() || !convertAddress.trim() || !convertRepresentative.trim());
```

- [ ] **Step 3: Prefill người đại diện và reset khi mở dialog**

Thay `onClick={() => setConvertOpen(true)}` ở nút Chuyển đổi bằng:

```tsx
                  <Button
                    onClick={() => {
                      setConvertTaxId("");
                      setConvertAddress("");
                      setConvertRepresentative(detail.name ?? "");
                      setConvertOpen(true);
                    }}
                  >
```

- [ ] **Step 4: Thêm ba trường vào dialog**

Trong dialog xác nhận chuyển đổi, chèn giữa `</DialogHeader>` và `<DialogFooter`:

```tsx
          {convertNeedsCompanyFields && (
            <div className="space-y-3 py-2">
              <p className="rounded bg-muted p-2 text-xs text-muted-foreground">
                {t("leads.convert.companyNotice")}
              </p>
              <div className="space-y-1">
                <Label className="text-xs" htmlFor="convert-taxid">
                  {t("leads.convert.field.taxId")}
                </Label>
                <Input
                  id="convert-taxid"
                  value={convertTaxId}
                  onChange={(e) => setConvertTaxId(e.target.value)}
                />
              </div>
              <div className="space-y-1">
                <Label className="text-xs" htmlFor="convert-address">
                  {t("leads.convert.field.address")}
                </Label>
                <Input
                  id="convert-address"
                  value={convertAddress}
                  onChange={(e) => setConvertAddress(e.target.value)}
                />
              </div>
              <div className="space-y-1">
                <Label className="text-xs" htmlFor="convert-representative">
                  {t("leads.convert.field.representative")}
                </Label>
                <Input
                  id="convert-representative"
                  value={convertRepresentative}
                  onChange={(e) => setConvertRepresentative(e.target.value)}
                />
              </div>
            </div>
          )}
```

- [ ] **Step 5: Gửi payload và chặn submit khi thiếu**

Thay thân `handleConvert` (dòng ~262-275) bằng:

```tsx
  // Task 1 trả 409 kèm CustomerDuplicateResponse khi MST hoặc số điện thoại đã
  // thuộc về một khách hàng. Không bắt case này thì người dùng kẹt cứng: convert
  // báo lỗi mà không có đường nào đi tiếp.
  const [duplicate, setDuplicate] = useState<CustomerDuplicateDetail | null>(null);

  const handleConvert = async (linkToCustomerId?: number) => {
    if (!detail) return;
    setConverting(true);
    setDuplicate(null);
    try {
      await adminApi.convertLead(detail.id, {
        customerId: linkToCustomerId ?? null,
        taxId: convertTaxId.trim() || null,
        address: convertAddress.trim() || null,
        representativeName: convertRepresentative.trim() || null,
      });
      toast({ title: t("leads.convert.done") });
      setConvertOpen(false);
      closeDetail();
      await load();
    } catch (err) {
      const response = (err as {
        response?: { status?: number; data?: CustomerDuplicateDetail };
      }).response;
      if (response?.status === 409 && response.data) {
        setDuplicate(response.data);
        return;
      }
      toast({
        title: getErrorMessage(err) ?? t("common.error"),
        variant: "destructive",
      });
    } finally {
      setConverting(false);
    }
  };
```

`CustomerDuplicateDetail` đã tồn tại trong `adminApi.ts` — `Customers.tsx:43` đang
import chính kiểu này cho luồng tạo khách hàng. Import lại, đừng khai báo kiểu mới.

Và thay nút xác nhận trong dialog:

```tsx
            <Button
              onClick={() => void handleConvert()}
              disabled={converting || convertCompanyFieldsMissing || duplicate !== null}
            >
              {converting ? "…" : t("leads.convert.button")}
            </Button>
```

- [ ] **Step 5b: Hiện lối thoát khi trùng khách hàng**

Chèn vào dialog, ngay trên `<DialogFooter`:

```tsx
          {duplicate && (
            <div className="space-y-2 rounded border border-amber-300 bg-amber-50 p-3 text-sm">
              <p className="text-amber-900">
                {t("leads.convert.duplicate.found")}{" "}
                <span className="font-medium">
                  {duplicate.existingCustomerName} (#{duplicate.existingCustomerId})
                </span>
              </p>
              <div className="flex flex-wrap gap-2">
                <Button
                  size="sm"
                  onClick={() => void handleConvert(duplicate.existingCustomerId)}
                  disabled={converting}
                >
                  {t("leads.convert.duplicate.linkExisting")}
                </Button>
                <Button size="sm" variant="outline" onClick={() => setDuplicate(null)}>
                  {t("common.cancel")}
                </Button>
              </div>
            </div>
          )}
```

Bấm "gắn vào khách hàng này" gọi lại `handleConvert` với `customerId` — Task 1 sẽ đi
nhánh dùng lại khách hàng có sẵn và chỉ tạo cơ hội mới. Đó cũng chính là nhánh 2 mà
Task 3 xử lý khi hoàn tác.

Nhớ reset `setDuplicate(null)` trong handler mở dialog ở Step 3, cạnh các `setConvert*`.

- [ ] **Step 6: Thêm key i18n**

Thêm vào `nihomebackend/Data/Seeds/i18n/leads.json`, trong mảng gốc:

```json
  { "key": "leads.convert.companyNotice", "category": "leads", "vi": "Lead này có tên công ty nên sẽ tạo khách hàng doanh nghiệp. Cần bổ sung mã số thuế, địa chỉ đăng ký và người đại diện.", "en": "This lead has a company name, so a company customer will be created. Tax code, registered address and legal representative are required.", "zh": "该线索含公司名称，将创建企业客户。需填写税号、注册地址和法定代表人。", "ja": "このリードには会社名があるため法人顧客を作成します。税番号、登記住所、代表者が必要です。" },
  { "key": "leads.convert.field.taxId", "category": "leads", "vi": "Mã số thuế", "en": "Tax code", "zh": "税号", "ja": "税番号" },
  { "key": "leads.convert.field.address", "category": "leads", "vi": "Địa chỉ đăng ký", "en": "Registered address", "zh": "注册地址", "ja": "登記住所" },
  { "key": "leads.convert.field.representative", "category": "leads", "vi": "Người đại diện", "en": "Legal representative", "zh": "法定代表人", "ja": "法定代表者" },
  { "key": "leads.convert.duplicate.found", "category": "leads", "vi": "Đã có khách hàng trùng mã số thuế hoặc số điện thoại:", "en": "A customer with the same tax code or phone already exists:", "zh": "已存在税号或电话相同的客户：", "ja": "同じ税番号または電話番号の顧客が既に存在します:" },
  { "key": "leads.convert.duplicate.linkExisting", "category": "leads", "vi": "Gắn vào khách hàng này", "en": "Link to this customer", "zh": "关联到该客户", "ja": "この顧客に紐付ける" },
```

- [ ] **Step 7: Restart backend để seeder chạy**

```bash
docker restart nihome31042025-backend
```

- [ ] **Step 8: Lint và kiểm tra bằng tay**

```bash
cd nihomeweb && npm run lint && npm run build
```

Kiểm tra bằng tay: mở `/admin/leads`, mở một lead **không** có tên công ty, bấm Chuyển đổi — dialog không hỏi trường doanh nghiệp. Mở một lead **có** tên công ty — dialog hiện ba trường, nút xác nhận bị vô hiệu cho tới khi điền đủ.

Kiểm tra tiếp đường trùng: tạo một lead cá nhân có số điện thoại trùng với khách hàng
đã tồn tại, bấm Chuyển đổi — dialog hiện khối vàng nêu tên khách hàng đang trùng, bấm
"Gắn vào khách hàng này" thì convert thành công và **không** sinh khách hàng thứ hai.

- [ ] **Step 9: Commit**

```bash
git add nihomeweb/src/services/adminApi.ts \
        nihomeweb/src/pages/admin/Leads.tsx \
        nihomebackend/Data/Seeds/i18n/leads.json
git commit -m "Collect company fields when converting a lead

A lead with a company name produces a company customer, which the
backend requires to carry a tax code, address and representative.
The convert dialog now asks for those three and blocks submit
until they are filled."
```

---

## Task 3: Endpoint unconvert ba nhánh (A2, backend)

**Files:**
- Create: `nihomebackend/Models/DTOs/Responses/UnconvertLeadResponse.cs`
- Modify: `nihomebackend/Services/ILeadService.cs`
- Modify: `nihomebackend/Services/LeadService.cs`
- Modify: `nihomebackend/Controllers/LeadsController.cs`
- Test: `nihomebackend.tests/Services/LeadServiceTests.cs`

**Interfaces:**
- Consumes: dấu timestamp trùng khít do Task 1 đóng.
- Produces: `Task<UnconvertLeadResponse?> UnconvertAsync(int id, int callerUserId, bool canConvert, CancellationToken ct = default)`, và `enum UnconvertOutcome { DeletedBoth, DeletedOpportunity, UnlinkedOnly }`. Task 4 dùng `Outcome` để chọn thông báo.

- [ ] **Step 1: Viết test thất bại cho nhánh 1**

```csharp
[Fact]
public async Task UnconvertAsync_DeletesBoth_WhenBothAutoCreatedAndClean()
{
    var sales = await SeedUserAsync(UserRole.USER);
    SeedSource("marketing");
    var lead = await SeedLeadAsync(LeadStatus.Interested, ownerId: sales.Id);
    await _sut.ConvertAsync(lead.Id, new ConvertLeadRequest(), sales.Id, canConvert: true);

    var result = await _sut.UnconvertAsync(lead.Id, sales.Id, canConvert: true);

    Assert.NotNull(result);
    Assert.Equal(UnconvertOutcome.DeletedBoth, result!.Outcome);
    Assert.Empty(_db.Customers);
    Assert.Empty(_db.Opportunities);

    var saved = await _db.Leads.SingleAsync(l => l.Id == lead.Id);
    Assert.Equal(LeadStatus.Interested, saved.Status);
    Assert.Null(saved.ConvertedAt);
    Assert.Null(saved.ConvertedCustomerId);
    Assert.Null(saved.ConvertedOpportunityId);
}
```

- [ ] **Step 2: Chạy test, xác nhận không biên dịch được**

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~UnconvertAsync_DeletesBoth"
```

Kỳ vọng: FAIL khi build — `UnconvertAsync` và `UnconvertOutcome` chưa tồn tại.

- [ ] **Step 3: Tạo DTO kết quả**

Tạo `nihomebackend/Models/DTOs/Responses/UnconvertLeadResponse.cs`:

```csharp
namespace NihomeBackend.Models.DTOs.Responses;

/// <summary>Nhánh nào đã chạy khi hoàn tác chuyển đổi lead.</summary>
public enum UnconvertOutcome
{
    /// <summary>Cả khách hàng và cơ hội đều do convert tạo và đều còn sạch — xoá cả hai.</summary>
    DeletedBoth = 0,

    /// <summary>Khách hàng có sẵn từ trước, chỉ cơ hội là auto-created — xoá mỗi cơ hội.</summary>
    DeletedOpportunity = 1,

    /// <summary>Quá hạn hoặc đã phát sinh dữ liệu con — giữ nguyên cả hai, chỉ gỡ liên kết.</summary>
    UnlinkedOnly = 2,
}

public class UnconvertLeadResponse
{
    public UnconvertOutcome Outcome { get; set; }

    /// <summary>Khách hàng được giữ lại, nếu có — để FE dựng link cho người dùng.</summary>
    public int? KeptCustomerId { get; set; }

    /// <summary>Cơ hội được giữ lại, nếu có.</summary>
    public int? KeptOpportunityId { get; set; }

    public LeadResponse Lead { get; set; } = null!;
}
```

- [ ] **Step 4: Khai báo trong interface**

Thêm vào `nihomebackend/Services/ILeadService.cs`, cạnh `ConvertAsync`:

```csharp
    /// <summary>
    /// Hoàn tác chuyển đổi. Ba nhánh — xem spec A2. Chỉ xoá bản ghi do chính
    /// convert sinh ra, nhận diện bằng CreatedAt trùng khít Lead.ConvertedAt.
    /// Không nhận diện được thì rơi về gỡ liên kết, tức hỏng an toàn.
    /// </summary>
    Task<UnconvertLeadResponse?> UnconvertAsync(
        int id,
        int callerUserId,
        bool canConvert,
        CancellationToken ct = default);
```

- [ ] **Step 5: Cài đặt `UnconvertAsync`**

Thêm vào `LeadService.cs` ngay sau `ConvertAsync`:

```csharp
    private const int UnconvertWindowHours = 24;

    public async Task<UnconvertLeadResponse?> UnconvertAsync(
        int id,
        int callerUserId,
        bool canConvert,
        CancellationToken ct = default)
    {
        if (!canConvert)
        {
            throw new LeadOperationException("Caller does not have permission to convert leads.");
        }

        var lead = await db.Leads.Include(l => l.Owner).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null) return null;

        if (lead.Status != LeadStatus.Converted || lead.ConvertedAt is null)
        {
            throw new LeadOperationException("Only a converted lead can be unconverted.");
        }

        var convertedAt = lead.ConvertedAt.Value;
        var now = DateTime.UtcNow;
        var withinWindow = (now - convertedAt).TotalHours < UnconvertWindowHours;

        var customer = lead.ConvertedCustomerId is null
            ? null
            : await db.Customers
                .Include(c => c.Contacts)
                .FirstOrDefaultAsync(c => c.Id == lead.ConvertedCustomerId, ct);

        var opportunity = lead.ConvertedOpportunityId is null
            ? null
            : await db.Opportunities
                .FirstOrDefaultAsync(o => o.Id == lead.ConvertedOpportunityId, ct);

        // Dấu nhận diện auto-created: CreatedAt trùng khít mốc convert.
        var customerAutoCreated = customer is not null && customer.CreatedAt == convertedAt;
        var opportunityAutoCreated = opportunity is not null && opportunity.CreatedAt == convertedAt;

        var opportunityClean = opportunity is not null
            && withinWindow
            && opportunity.Stage == OpportunityStage.Prospecting
            && !await db.Quotes.AnyAsync(q => q.OpportunityId == opportunity.Id, ct)
            && !await db.Surveys.AnyAsync(s => s.LinkedOpportunityId == opportunity.Id, ct)
            && !await db.Contracts.AnyAsync(c => c.OpportunityId == opportunity.Id, ct)
            && !await db.Tenders.AnyAsync(tn => tn.WonOpportunityId == opportunity.Id, ct);

        // Activities và Documents của khách hàng đều cấu hình OnDelete(Cascade)
        // (AppDbContext.cs:465-468 và 485-488), nên xoá khách hàng sẽ **âm thầm**
        // cuốn theo cả hai. Với Documents còn tệ hơn: hàng bị xoá nhưng file trên
        // đĩa theo FilePath thì ở lại. Vì vậy chỉ cần có một bản ghi con là dừng,
        // rơi về nhánh gỡ liên kết.
        var customerHasOtherWork = customer is not null
            && (await db.Opportunities.AnyAsync(
                    o => o.CustomerId == customer.Id && o.Id != lead.ConvertedOpportunityId, ct)
                || await db.Contracts.AnyAsync(c => c.CustomerId == customer.Id, ct)
                || await db.CustomerActivities.AnyAsync(a => a.CustomerId == customer.Id, ct)
                || await db.CustomerDocuments.AnyAsync(d => d.CustomerId == customer.Id, ct));

        var outcome = UnconvertOutcome.UnlinkedOnly;

        if (opportunityAutoCreated && opportunityClean)
        {
            if (customerAutoCreated && !customerHasOtherWork)
            {
                db.Opportunities.Remove(opportunity!);
                db.CustomerContacts.RemoveRange(customer!.Contacts);
                db.Customers.Remove(customer);
                outcome = UnconvertOutcome.DeletedBoth;
            }
            else
            {
                db.Opportunities.Remove(opportunity!);
                outcome = UnconvertOutcome.DeletedOpportunity;
            }
        }

        var keptCustomerId = outcome == UnconvertOutcome.DeletedBoth ? null : lead.ConvertedCustomerId;
        var keptOpportunityId = outcome == UnconvertOutcome.UnlinkedOnly ? lead.ConvertedOpportunityId : null;

        db.LeadActivities.Add(new LeadActivity
        {
            LeadId = lead.Id,
            Type = LeadActivityType.Note,
            Content = $"[Unconvert] outcome={outcome}",
            CreatedByUserId = callerUserId,
            CreatedAt = now,
        });

        lead.Status = LeadStatus.Interested;
        lead.ConvertedAt = null;
        lead.ConvertedCustomerId = null;
        lead.ConvertedOpportunityId = null;
        lead.UpdatedAt = now;
        lead.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(ct);

        return new UnconvertLeadResponse
        {
            Outcome = outcome,
            KeptCustomerId = keptCustomerId,
            KeptOpportunityId = keptOpportunityId,
            Lead = MapLead(lead, lead.Owner?.FullName, activities: null),
        };
    }
```

- [ ] **Step 6: Chạy test nhánh 1, xác nhận pass**

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~UnconvertAsync_DeletesBoth"
```

Kỳ vọng: PASS.

- [ ] **Step 7: Viết test cho nhánh 2 và nhánh 3**

```csharp
[Fact]
public async Task UnconvertAsync_KeepsExistingCustomer_DeletesOpportunityOnly()
{
    var sales = await SeedUserAsync(UserRole.USER);
    SeedSource("marketing");
    var lead = await SeedLeadAsync(LeadStatus.Interested, ownerId: sales.Id);

    var existing = new Customer
    {
        Type = CustomerType.Individual,
        Name = "Khách cũ",
        SourceCode = "marketing",
    };
    _db.Customers.Add(existing);
    await _db.SaveChangesAsync();

    await _sut.ConvertAsync(
        lead.Id, new ConvertLeadRequest { CustomerId = existing.Id }, sales.Id, canConvert: true);

    var result = await _sut.UnconvertAsync(lead.Id, sales.Id, canConvert: true);

    Assert.Equal(UnconvertOutcome.DeletedOpportunity, result!.Outcome);
    Assert.Equal(existing.Id, result.KeptCustomerId);
    Assert.Single(_db.Customers);
    Assert.Empty(_db.Opportunities);
}

[Fact]
public async Task UnconvertAsync_OnlyUnlinks_WhenOpportunityHasQuote()
{
    var sales = await SeedUserAsync(UserRole.USER);
    SeedSource("marketing");
    var lead = await SeedLeadAsync(LeadStatus.Interested, ownerId: sales.Id);

    var converted = await _sut.ConvertAsync(
        lead.Id, new ConvertLeadRequest(), sales.Id, canConvert: true);

    _db.Quotes.Add(new Quote
    {
        Code = "QT-2026-0001",
        OpportunityId = converted!.ConvertedOpportunityId!.Value,
    });
    await _db.SaveChangesAsync();

    var result = await _sut.UnconvertAsync(lead.Id, sales.Id, canConvert: true);

    Assert.Equal(UnconvertOutcome.UnlinkedOnly, result!.Outcome);
    Assert.Single(_db.Customers);
    Assert.Single(_db.Opportunities);
    Assert.NotNull(result.KeptCustomerId);
    Assert.NotNull(result.KeptOpportunityId);
}

[Fact]
public async Task UnconvertAsync_OnlyUnlinks_WhenPastTwentyFourHours()
{
    var sales = await SeedUserAsync(UserRole.USER);
    SeedSource("marketing");
    var lead = await SeedLeadAsync(LeadStatus.Interested, ownerId: sales.Id);
    await _sut.ConvertAsync(lead.Id, new ConvertLeadRequest(), sales.Id, canConvert: true);

    // Đẩy lùi cả ba mốc để giữ nguyên dấu nhận diện auto-created, chỉ làm quá hạn.
    var stale = DateTime.UtcNow.AddHours(-25);
    var saved = await _db.Leads.SingleAsync(l => l.Id == lead.Id);
    var customer = await _db.Customers.SingleAsync();
    var opportunity = await _db.Opportunities.SingleAsync();
    saved.ConvertedAt = stale;
    customer.CreatedAt = stale;
    opportunity.CreatedAt = stale;
    await _db.SaveChangesAsync();

    var result = await _sut.UnconvertAsync(lead.Id, sales.Id, canConvert: true);

    Assert.Equal(UnconvertOutcome.UnlinkedOnly, result!.Outcome);
    Assert.Single(_db.Customers);
    Assert.Single(_db.Opportunities);
}

[Fact]
public async Task UnconvertAsync_KeepsCustomer_WhenItHasActivitiesOrDocuments()
{
    var sales = await SeedUserAsync(UserRole.USER);
    SeedSource("marketing");
    var lead = await SeedLeadAsync(LeadStatus.Interested, ownerId: sales.Id);

    var converted = await _sut.ConvertAsync(
        lead.Id, new ConvertLeadRequest(), sales.Id, canConvert: true);

    // Cascade delete sẽ cuốn bản ghi này đi mà không báo gì — đó là thứ guard chặn.
    _db.CustomerActivities.Add(new CustomerActivity
    {
        CustomerId = converted!.ConvertedCustomerId!.Value,
        Type = CustomerActivityType.Note,
        Content = "Đã gọi điện chăm sóc",
        CreatedByUserId = sales.Id,
    });
    await _db.SaveChangesAsync();

    var result = await _sut.UnconvertAsync(lead.Id, sales.Id, canConvert: true);

    Assert.Equal(UnconvertOutcome.DeletedOpportunity, result!.Outcome);
    Assert.Single(_db.Customers);
    Assert.Single(_db.CustomerActivities);
    Assert.Empty(_db.Opportunities);
}

[Fact]
public async Task UnconvertAsync_RejectsLeadThatWasNeverConverted()
{
    var sales = await SeedUserAsync(UserRole.USER);
    SeedSource("marketing");
    var lead = await SeedLeadAsync(LeadStatus.Interested, ownerId: sales.Id);

    var ex = await Assert.ThrowsAsync<LeadOperationException>(
        () => _sut.UnconvertAsync(lead.Id, sales.Id, canConvert: true));

    Assert.Contains("converted", ex.Message, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 8: Chạy toàn bộ test unconvert**

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~UnconvertAsync"
```

Kỳ vọng: PASS cả sáu test.

- [ ] **Step 9: Thêm endpoint vào controller**

Thêm vào `nihomebackend/Controllers/LeadsController.cs`, ngay sau action `Convert`:

```csharp
    [HttpPost("{id:int}/unconvert")]
    [RequirePermission("crm.leads", "convert")]
    public async Task<ActionResult<UnconvertLeadResponse>> Unconvert(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canConvert = await permissions.HasAsync(userId.Value, "crm.leads.convert", ct);

        try
        {
            var response = await svc.UnconvertAsync(id, userId.Value, canConvert, ct);
            if (response is null) return NotFound();

            audit.Log(new AuditEvent
            {
                Action = "lead.unconvert",
                ResourceType = EntityTypes.Lead,
                ResourceId = id.ToString(),
                Message = $"Lead #{id} unconverted (outcome={response.Outcome}).",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (LeadOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "lead.unconvert",
                ResourceType = EntityTypes.Lead,
                ResourceId = id.ToString(),
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }
```

- [ ] **Step 10: Build, format, commit**

```bash
docker exec nihome31042025-backend dotnet build
docker exec nihome31042025-backend dotnet format
git add nihomebackend/Models/DTOs/Responses/UnconvertLeadResponse.cs \
        nihomebackend/Services/ILeadService.cs \
        nihomebackend/Services/LeadService.cs \
        nihomebackend/Controllers/LeadsController.cs \
        nihomebackend.tests/Services/LeadServiceTests.cs
git commit -m "Add three-branch unconvert for leads

A misclicked convert had no way back. Unconvert now deletes only
what the convert itself created, told apart by a creation stamp
matching the lead's ConvertedAt, and falls back to unlinking when
it cannot prove ownership.

The common case of an existing customer with a fresh opportunity
gets its own branch so the opportunity does not survive as an
orphan."
```

---

## Task 4: Nút hoàn tác chuyển đổi (A2, frontend)

**Files:**
- Modify: `nihomeweb/src/services/adminApi.ts`
- Modify: `nihomeweb/src/pages/admin/Leads.tsx`
- Modify: `nihomebackend/Data/Seeds/i18n/leads.json`

**Interfaces:**
- Consumes: `POST /api/leads/{id}/unconvert` từ Task 3, trả `UnconvertLeadResponse` với `outcome` là một trong `"DeletedBoth" | "DeletedOpportunity" | "UnlinkedOnly"`.

- [ ] **Step 1: Thêm type và hàm gọi API**

Trong `nihomeweb/src/services/adminApi.ts`, cạnh `convertLead`:

```ts
export type UnconvertOutcome = "DeletedBoth" | "DeletedOpportunity" | "UnlinkedOnly";

export interface UnconvertLeadResponse {
  outcome: UnconvertOutcome;
  keptCustomerId: number | null;
  keptOpportunityId: number | null;
  lead: LeadResponse;
}
```

Và trong object `adminApi`:

```ts
  unconvertLead: (id: number) =>
    api.post<UnconvertLeadResponse>(`/leads/${id}/unconvert`, {}),
```

- [ ] **Step 2: Thêm handler**

Trong `nihomeweb/src/pages/admin/Leads.tsx`, cạnh `handleConvert`:

```tsx
  const [unconverting, setUnconverting] = useState(false);

  const handleUnconvert = async () => {
    if (!detail) return;
    if (!window.confirm(t("leads.unconvert.confirm"))) return;
    setUnconverting(true);
    try {
      const { data } = await adminApi.unconvertLead(detail.id);
      toast({ title: t(`leads.unconvert.done.${data.outcome}`) });
      closeDetail();
      await load();
    } catch (err) {
      toast({
        title: getErrorMessage(err) ?? t("common.error"),
        variant: "destructive",
      });
    } finally {
      setUnconverting(false);
    }
  };
```

- [ ] **Step 3: Hiện nút hoàn tác trong khối trạng thái đã chuyển đổi**

Thay khối `{detail.status === "Converted" && (...)}` (dòng ~780-787) bằng:

```tsx
                {detail.status === "Converted" && (
                  <div className="col-span-2 space-y-2 rounded bg-muted p-2 text-xs text-muted-foreground">
                    <div>
                      {t("leads.convert.locked")}
                      {detail.convertedCustomerId != null && (
                        <>
                          {" · "}
                          <Link
                            className="underline"
                            to={`/admin/customers?open=${detail.convertedCustomerId}`}
                          >
                            {t("leads.convert.viewCustomer")}
                          </Link>
                        </>
                      )}
                      {detail.convertedOpportunityId != null && (
                        <>
                          {" · "}
                          <Link
                            className="underline"
                            to={`/admin/opportunities/${detail.convertedOpportunityId}`}
                          >
                            {t("leads.convert.viewOpportunity")}
                          </Link>
                        </>
                      )}
                    </div>
                    {canConvert && (
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => void handleUnconvert()}
                        disabled={unconverting}
                      >
                        {unconverting ? "…" : t("leads.unconvert.action")}
                      </Button>
                    )}
                  </div>
                )}
```

Bổ sung `Link` vào import từ `react-router-dom` ở đầu file nếu chưa có.

> Đọc `nihomebackend/Models/CustomerActivity.cs` để lấy đúng tên thành viên enum
> `CustomerActivityType` dùng trong test trên — đừng đoán tên.

> Cả hai link này đều phụ thuộc **Plan 2 Task 3**. `Customers.tsx` hiện **không đọc
> searchParams nào cả**, nên `?open=` chưa có tác dụng cho tới khi Task đó thêm; còn
> route `/admin/opportunities/:id` thì chưa tồn tại. Dùng `?open=` chứ không phải
> `?focus=` vì đó là convention sẵn có trong repo — xem `Customers.tsx:1524` đang
> trỏ sang `/admin/opportunities?open=`. Cho tới khi Plan 2 Task 3 xong, hai link
> này chưa mở đúng bản ghi; Task 3 và Task 4 vẫn kiểm chứng độc lập được, nhưng
> đừng coi là đã xong khi review.

- [ ] **Step 4: Dời nút Chuyển đổi sang trái**

`DialogFooter` hiện có `className="flex-col-reverse gap-2 sm:flex-row"`, nên nút đầu tiên trong DOM nằm bên phải trên desktop. Đổi thành `sm:flex-row-reverse sm:justify-start` để nút Chuyển đổi rời khỏi vị trí sát nút đóng:

```tsx
              <DialogFooter className="flex-col-reverse gap-2 sm:flex-row-reverse sm:justify-start">
```

- [ ] **Step 5: Thêm key i18n**

Thêm vào `nihomebackend/Data/Seeds/i18n/leads.json`:

```json
  { "key": "leads.unconvert.action", "category": "leads", "vi": "Hoàn tác chuyển đổi", "en": "Undo conversion", "zh": "撤销转换", "ja": "変換を取り消す" },
  { "key": "leads.unconvert.confirm", "category": "leads", "vi": "Hoàn tác chuyển đổi lead này? Khách hàng và cơ hội chỉ bị xoá nếu chúng do lần chuyển đổi này tạo ra và chưa phát sinh dữ liệu.", "en": "Undo this lead conversion? The customer and opportunity are deleted only if this conversion created them and nothing else references them.", "zh": "撤销此线索转换？仅当客户和商机由本次转换创建且无关联数据时才会删除。", "ja": "このリード変換を取り消しますか？顧客と商談は、本変換で作成され参照がない場合のみ削除されます。" },
  { "key": "leads.unconvert.done.DeletedBoth", "category": "leads", "vi": "Đã hoàn tác. Khách hàng và cơ hội vừa tạo đã được xoá.", "en": "Undone. The newly created customer and opportunity were removed.", "zh": "已撤销。新建的客户和商机已删除。", "ja": "取り消しました。新規作成した顧客と商談を削除しました。" },
  { "key": "leads.unconvert.done.DeletedOpportunity", "category": "leads", "vi": "Đã hoàn tác. Cơ hội vừa tạo đã xoá, khách hàng có sẵn được giữ nguyên.", "en": "Undone. The new opportunity was removed; the existing customer was kept.", "zh": "已撤销。新建商机已删除，原有客户保留。", "ja": "取り消しました。新規商談を削除し、既存顧客は保持しました。" },
  { "key": "leads.unconvert.done.UnlinkedOnly", "category": "leads", "vi": "Đã hoàn tác liên kết. Khách hàng và cơ hội vẫn được giữ lại vì đã phát sinh dữ liệu hoặc quá hạn 24 giờ.", "en": "Link removed. The customer and opportunity were kept because they have related data or the 24-hour window passed.", "zh": "已解除关联。因存在关联数据或超过24小时，客户与商机予以保留。", "ja": "リンクを解除しました。関連データがあるか24時間を過ぎたため、顧客と商談は保持されます。" },
  { "key": "leads.convert.viewCustomer", "category": "leads", "vi": "Xem khách hàng", "en": "View customer", "zh": "查看客户", "ja": "顧客を表示" },
  { "key": "leads.convert.viewOpportunity", "category": "leads", "vi": "Xem cơ hội", "en": "View opportunity", "zh": "查看商机", "ja": "商談を表示" },
```

- [ ] **Step 6: Restart, lint, kiểm tra bằng tay**

```bash
docker restart nihome31042025-backend
cd nihomeweb && npm run lint && npm run build
```

Kiểm tra bằng tay ba nhánh: (a) convert rồi unconvert ngay → thông báo "đã xoá cả hai", danh sách khách hàng không có bản ghi mới. (b) convert gắn khách cũ rồi unconvert → thông báo "giữ khách hàng có sẵn". (c) convert, tạo một báo giá cho cơ hội, rồi unconvert → thông báo "vẫn giữ lại".

- [ ] **Step 7: Commit**

```bash
git add nihomeweb/src/services/adminApi.ts \
        nihomeweb/src/pages/admin/Leads.tsx \
        nihomebackend/Data/Seeds/i18n/leads.json
git commit -m "Surface lead unconvert in the admin UI

Each branch reports what actually happened so the operator never
has to guess whether their customer survived, and the convert
button moves away from the close button to cut misclicks."
```

---

## Task 5: Bốn trường dự án thiết kế trên ContractResponse (A5, backend)

**Files:**
- Modify: `nihomebackend/Models/DTOs/Responses/ContractResponses.cs`
- Modify: `nihomebackend/Services/ContractService.cs` (`ListAsync`, `GetAsync`, `Map`)
- Test: `nihomebackend.tests/Services/ContractServiceTests.cs`

**Interfaces:**
- Produces: `ContractResponse.DesignProjectId` (`int?`), `.DesignProjectCode` (`string?`), `.DesignProjectName` (`string?`), `.DesignProjectCurrentStage` (`string?`). Task 6 và Task 8 đều đọc bốn trường này.

- [ ] **Step 1: Viết test thất bại**

Thêm vào `nihomebackend.tests/Services/ContractServiceTests.cs`:

Class test này seed sẵn `_customerA`/`_customerB` trong constructor và dựng hợp đồng
qua helper `Req(...)` — dùng đúng pattern đó, đừng thêm helper mới.

```csharp
[Fact]
public async Task GetAsync_ExposesLinkedDesignProject()
{
    var contract = await _sut.CreateAsync(Req(customerId: _customerA), 1, canReassignOwner: true);

    _db.DesignProjects.Add(new DesignProject
    {
        ProjectCode = "DP-2026-0001",
        Name = "Dự án hợp đồng HD-001",
        CustomerId = _customerA,
        ContractId = contract.Id,
        CurrentStage = DesignProjectStage.BasicDesign,
        Status = DesignProjectStatus.Active,
    });
    await _db.SaveChangesAsync();

    var response = await _sut.GetAsync(contract.Id, 1, canSeeAll: true);

    Assert.Equal("DP-2026-0001", response!.DesignProjectCode);
    Assert.Equal("Dự án hợp đồng HD-001", response.DesignProjectName);
    Assert.Equal("BasicDesign", response.DesignProjectCurrentStage);
    Assert.NotNull(response.DesignProjectId);
}

[Fact]
public async Task GetAsync_LeavesDesignProjectFieldsNull_WhenNoneLinked()
{
    var contract = await _sut.CreateAsync(Req(customerId: _customerB), 1, canReassignOwner: true);

    var response = await _sut.GetAsync(contract.Id, 1, canSeeAll: true);

    Assert.Null(response!.DesignProjectId);
    Assert.Null(response.DesignProjectCode);
    Assert.Null(response.DesignProjectName);
    Assert.Null(response.DesignProjectCurrentStage);
}
```

> Đọc chữ ký `Req(...)` ở `ContractServiceTests.cs:32` để truyền đúng tham số —
> nó có giá trị mặc định cho hầu hết trường.

- [ ] **Step 2: Chạy test, xác nhận không biên dịch được**

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~ExposesLinkedDesignProject"
```

Kỳ vọng: FAIL khi build — `DesignProjectCode` chưa có trên `ContractResponse`.

- [ ] **Step 3: Thêm bốn trường vào DTO**

Trong `nihomebackend/Models/DTOs/Responses/ContractResponses.cs`, thêm vào `ContractResponse` ngay sau `QuoteCode`:

```csharp
    /// <summary>
    /// Dự án thiết kế gắn với hợp đồng này, nếu có. Bốn trường dưới đây là
    /// dẫn xuất — lấy bằng LEFT JOIN từ design_projects, không phải cột trên
    /// bảng contracts. Quan hệ là 1-1 nhờ unique index có filter trên
    /// DesignProject.ContractId.
    /// </summary>
    public int? DesignProjectId { get; set; }
    public string? DesignProjectCode { get; set; }
    public string? DesignProjectName { get; set; }
    public string? DesignProjectCurrentStage { get; set; }
```

- [ ] **Step 4: Thêm tham số vào `Map`**

Trong `ContractService.cs`, thêm tham số cuối vào chữ ký `Map`:

```csharp
        int appendixCount = 0,
        DesignProjectLink? designProject = null)
```

Và trong khối khởi tạo `new ContractResponse { ... }`, thêm ngay sau `QuoteCode = quoteCode,`:

```csharp
            DesignProjectId = designProject?.Id,
            DesignProjectCode = designProject?.Code,
            DesignProjectName = designProject?.Name,
            DesignProjectCurrentStage = designProject?.CurrentStage,
```

Thêm record giữ dữ liệu join, đặt cạnh `Map` ở cuối class:

```csharp
    /// <summary>Kết quả LEFT JOIN sang design_projects cho một hợp đồng.</summary>
    private sealed record DesignProjectLink(int Id, string Code, string Name, string CurrentStage);
```

- [ ] **Step 5: Nạp dữ liệu join trong `GetAsync` và `ListAsync`**

Thêm helper vào `ContractService.cs`:

```csharp
    private async Task<Dictionary<int, DesignProjectLink>> LoadDesignProjectLinksAsync(
        IReadOnlyCollection<int> contractIds,
        CancellationToken ct)
    {
        if (contractIds.Count == 0) return new Dictionary<int, DesignProjectLink>();

        return await db.DesignProjects.AsNoTracking()
            .Where(dp => dp.ContractId != null && contractIds.Contains(dp.ContractId.Value))
            .Select(dp => new
            {
                ContractId = dp.ContractId!.Value,
                Link = new DesignProjectLink(
                    dp.Id,
                    dp.ProjectCode,
                    dp.Name,
                    dp.CurrentStage.ToString()),
            })
            .ToDictionaryAsync(x => x.ContractId, x => x.Link, ct);
    }
```

Trong `GetAsync`, trước khi gọi `Map`, nạp link cho một id và truyền vào:

```csharp
        var designLinks = await LoadDesignProjectLinksAsync(new[] { entity.Id }, ct);
        designLinks.TryGetValue(entity.Id, out var designLink);
```

rồi thêm `designProject: designLink` vào lời gọi `Map`.

Trong `ListAsync`, sau khi đã có danh sách hợp đồng của trang, nạp một lần cho cả trang:

```csharp
        var designLinks = await LoadDesignProjectLinksAsync(
            items.Select(c => c.Id).ToList(), ct);
```

rồi trong vòng lặp dựng response, truyền `designProject: designLinks.GetValueOrDefault(c.Id)`.

- [ ] **Step 6: Chạy test, xác nhận pass**

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~ContractServiceTests"
```

Kỳ vọng: PASS toàn bộ.

- [ ] **Step 7: Format và commit**

```bash
docker exec nihome31042025-backend dotnet build
docker exec nihome31042025-backend dotnet format
git add nihomebackend/Models/DTOs/Responses/ContractResponses.cs \
        nihomebackend/Services/ContractService.cs \
        nihomebackend.tests/Services/ContractServiceTests.cs
git commit -m "Expose the linked design project on contracts

The contract already spawns a design project when it opens, but
nothing in the response said so. Four derived fields now ride along
from a left join, enough for the detail view to show the project
without a second request, and no new column."
```

---

## Task 6: Lọc hợp đồng đã có dự án và xử lý 409 (A5, frontend)

**Files:**
- Modify: `nihomeweb/src/services/adminApi.ts` (`ContractResponse`)
- Modify: `nihomeweb/src/pages/admin/DesignProjects.tsx`
- Modify: `nihomebackend/Data/Seeds/i18n/design-projects.json`

**Interfaces:**
- Consumes: bốn trường dự án thiết kế trên `ContractResponse` từ Task 5.

- [ ] **Step 1: Mở rộng type frontend**

Trong `nihomeweb/src/services/adminApi.ts`, thêm vào `interface ContractResponse` ngay sau `quoteId`:

```ts
  designProjectId?: number | null;
  designProjectCode?: string | null;
  designProjectName?: string | null;
  designProjectCurrentStage?: string | null;
```

- [ ] **Step 2: Lọc dropdown hợp đồng trong form tạo mới**

Trong `nihomeweb/src/pages/admin/DesignProjects.tsx`, tại nơi dựng danh sách lựa chọn cho `form.contractId` (dòng ~800), lọc theo dự án đã có. Thêm memo cạnh các memo hiện có:

```tsx
  // Unique index có filter trên DesignProject.ContractId cho phép đúng một dự án
  // mỗi hợp đồng, và hợp đồng tự sinh dự án khi chuyển sang InProgress. Nên
  // hợp đồng đã có dự án phải biến mất khỏi lựa chọn tạo mới — bất kể trạng thái.
  const selectableContracts = useMemo(
    () =>
      contracts.filter(
        (c) => c.designProjectId == null || c.id === form.contractId,
      ),
    [contracts, form.contractId],
  );
```

Rồi đổi nguồn dữ liệu của dropdown từ `contracts` sang `selectableContracts`.

> Điều kiện `c.id === form.contractId` giữ lại hợp đồng đang được chọn khi đang **sửa** một dự án đã tồn tại, nếu không dropdown sẽ tự xoá lựa chọn hiện tại.

- [ ] **Step 3: Map lỗi 409 thành thông báo có link**

Trong hàm submit của form, thay khối `catch` bằng:

```tsx
    } catch (err) {
      const status = (err as { response?: { status?: number } }).response?.status;
      if (status === 409) {
        const taken = contracts.find((c) => c.id === form.contractId);
        toast({
          title: t("designProjects.error.contractTaken"),
          description: taken?.designProjectCode ?? undefined,
          variant: "destructive",
        });
        if (taken?.designProjectId != null) {
          navigate(`/admin/design-projects/${taken.designProjectId}`);
        }
        return;
      }
      toast({
        title: getErrorMessage(err) ?? t("common.error"),
        variant: "destructive",
      });
    }
```

Bổ sung `useNavigate` từ `react-router-dom` nếu file chưa dùng.

- [ ] **Step 4: Thêm key i18n**

Thêm vào `nihomebackend/Data/Seeds/i18n/design-projects.json`:

```json
  { "key": "designProjects.error.contractTaken", "category": "designProjects", "vi": "Hợp đồng này đã có dự án thiết kế. Đang mở dự án hiện có.", "en": "This contract already has a design project. Opening the existing one.", "zh": "该合同已有设计项目，正在打开现有项目。", "ja": "この契約には既に設計プロジェクトがあります。既存のものを開きます。" },
  { "key": "designProjects.contract.alreadyUsed", "category": "designProjects", "vi": "Đã có dự án thiết kế", "en": "Already has a design project", "zh": "已有设计项目", "ja": "設計プロジェクトあり" },
```

- [ ] **Step 5: Restart, lint, kiểm tra bằng tay**

```bash
docker restart nihome31042025-backend
cd nihomeweb && npm run lint && npm run build
```

Kiểm tra bằng tay: chuyển một hợp đồng sang Đang thực hiện để nó tự sinh dự án, rồi mở form tạo dự án thiết kế — hợp đồng đó không còn trong dropdown.

- [ ] **Step 6: Commit**

```bash
git add nihomeweb/src/services/adminApi.ts \
        nihomeweb/src/pages/admin/DesignProjects.tsx \
        nihomebackend/Data/Seeds/i18n/design-projects.json
git commit -m "Hide contracts that already have a design project

Opening a contract auto-creates its design project, so picking that
same contract again hit the unique index and surfaced a raw 409.
Those contracts now drop out of the picker, and a 409 that still
slips through opens the existing project instead."
```

---

## Task 7: Endpoint tạo dự án thiết kế từ hợp đồng (A6, backend)

**Files:**
- Modify: `nihomebackend/Controllers/ContractsController.cs`
- Test: `nihomebackend.tests/Services/DesignProjectServiceTests.cs`

**Interfaces:**
- Consumes: `IDesignProjectService.EnsureForContractAsync(Contract, int?, CancellationToken)` — đã tồn tại và idempotent, có test ở `DesignProjectServiceTests.EnsureForContractAsync_Idempotent`.
- Produces: `POST /api/contracts/{id}/design-project` → `DesignProjectResponse`. Task 8 gọi endpoint này.

- [ ] **Step 1: Xác nhận hành vi idempotent đã được test**

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~EnsureForContractAsync"
```

Kỳ vọng: PASS. Hai test này đã có sẵn — chúng là lý do endpoint không cần logic riêng, chỉ uỷ quyền.

- [ ] **Step 2: Thêm endpoint**

Thêm vào `nihomebackend/Controllers/ContractsController.cs`, sau action `Transition`. Inject `IDesignProjectService designProjects` vào constructor nếu chưa có.

```csharp
    /// <summary>
    /// Tạo (hoặc lấy) dự án thiết kế của hợp đồng. Hợp đồng thường tự sinh dự án
    /// khi chuyển sang InProgress, nhưng đường tự sinh đó là best-effort và nuốt
    /// exception — endpoint này là đường thủ công để phục hồi khi nó đã thất bại.
    /// Idempotent: gọi nhiều lần trả về cùng một dự án.
    /// </summary>
    [HttpPost("{id:int}/design-project")]
    [RequirePermission("design.projects", "manage")]
    public async Task<ActionResult<DesignProjectResponse>> EnsureDesignProject(
        int id,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (contract is null) return NotFound();

        var response = await designProjects.EnsureForContractAsync(contract, userId.Value, ct);

        audit.Log(new AuditEvent
        {
            Action = "contract.designProject.ensure",
            ResourceType = EntityTypes.Contract,
            ResourceId = id.ToString(),
            Message = $"Design project ensured for contract #{id}.",
            NewValue = response,
        });

        return Ok(response);
    }
```

> Kiểm tra tên permission `design.projects.manage` khớp với hằng số đang dùng cho `ADMIN_PERMS.designProjects` — nếu khác, dùng đúng tên đang có, đừng tạo permission mới.

- [ ] **Step 3: Build và kiểm tra bằng curl**

```bash
docker exec nihome31042025-backend dotnet build
```

Kỳ vọng: build sạch. Sau khi backend chạy lại, gọi hai lần liên tiếp trên một hợp đồng chưa có dự án và xác nhận cả hai lần trả về cùng `id`.

- [ ] **Step 4: Format và commit**

```bash
docker exec nihome31042025-backend dotnet format
git add nihomebackend/Controllers/ContractsController.cs
git commit -m "Expose design project creation from a contract

EnsureForContractAsync was reachable only from the InProgress
transition, which swallows its own failures, leaving no way back if
it ever failed. The endpoint delegates straight to it and inherits
its idempotency."
```

---

## Task 8: Khối dự án thiết kế trên chi tiết hợp đồng (A6, frontend)

**Files:**
- Modify: `nihomeweb/src/services/adminApi.ts`
- Modify: `nihomeweb/src/pages/admin/ContractDetail.tsx`
- Modify: `nihomebackend/Data/Seeds/i18n/contracts.json`

**Interfaces:**
- Consumes: bốn trường từ Task 5; endpoint từ Task 7.

- [ ] **Step 1: Thêm hàm gọi API**

Trong `nihomeweb/src/services/adminApi.ts`, cạnh các hàm hợp đồng khác:

```ts
  ensureContractDesignProject: (contractId: number) =>
    api.post<DesignProjectResponse>(`/contracts/${contractId}/design-project`, {}),
```

- [ ] **Step 2: Dựng khối hiển thị**

Trong `nihomeweb/src/pages/admin/ContractDetail.tsx`, thêm vào phần thân trang, cạnh các khối thông tin sẵn có:

```tsx
      <section className="rounded-lg border bg-card p-4">
        <h2 className="mb-3 text-sm font-semibold">
          {t("contracts.designProject.title")}
        </h2>

        {contract.designProjectId != null ? (
          <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
            <Link
              className="font-medium underline"
              to={`/admin/design-projects/${contract.designProjectId}`}
            >
              {contract.designProjectCode} — {contract.designProjectName}
            </Link>
            {contract.designProjectCurrentStage && (
              <Badge variant="outline">
                {t(`designProjects.stage.${contract.designProjectCurrentStage}`)}
              </Badge>
            )}
          </div>
        ) : contract.status === "InProgress" ? (
          <div className="space-y-2">
            <p className="text-sm text-muted-foreground">
              {t("contracts.designProject.missing")}
            </p>
            <Button size="sm" onClick={() => void handleEnsureDesignProject()} disabled={ensuring}>
              {ensuring ? "…" : t("contracts.designProject.create")}
            </Button>
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">
            {t("contracts.designProject.pending")}
          </p>
        )}
      </section>
```

- [ ] **Step 3: Thêm handler**

```tsx
  const [ensuring, setEnsuring] = useState(false);

  const handleEnsureDesignProject = async () => {
    setEnsuring(true);
    try {
      const { data } = await adminApi.ensureContractDesignProject(contract.id);
      toast({ title: t("contracts.designProject.created") });
      navigate(`/admin/design-projects/${data.id}`);
    } catch (err) {
      toast({
        title: getErrorMessage(err) ?? t("common.error"),
        variant: "destructive",
      });
    } finally {
      setEnsuring(false);
    }
  };
```

- [ ] **Step 4: Thêm key i18n**

Thêm vào `nihomebackend/Data/Seeds/i18n/contracts.json`:

```json
  { "key": "contracts.designProject.title", "category": "contracts", "vi": "Dự án thiết kế", "en": "Design project", "zh": "设计项目", "ja": "設計プロジェクト" },
  { "key": "contracts.designProject.pending", "category": "contracts", "vi": "Dự án thiết kế sẽ được tạo tự động khi hợp đồng chuyển sang Đang thực hiện.", "en": "A design project is created automatically once the contract moves to In Progress.", "zh": "合同转为执行中后将自动创建设计项目。", "ja": "契約が進行中になると設計プロジェクトが自動作成されます。" },
  { "key": "contracts.designProject.missing", "category": "contracts", "vi": "Hợp đồng đang thực hiện nhưng chưa có dự án thiết kế. Có thể tạo lại thủ công.", "en": "This contract is in progress but has no design project yet. You can create it manually.", "zh": "该合同已在执行但尚无设计项目，可手动创建。", "ja": "この契約は進行中ですが設計プロジェクトがありません。手動で作成できます。" },
  { "key": "contracts.designProject.create", "category": "contracts", "vi": "Tạo dự án thiết kế", "en": "Create design project", "zh": "创建设计项目", "ja": "設計プロジェクトを作成" },
  { "key": "contracts.designProject.created", "category": "contracts", "vi": "Đã tạo dự án thiết kế.", "en": "Design project created.", "zh": "设计项目已创建。", "ja": "設計プロジェクトを作成しました。" },
```
> **Chú ý style:** `contracts.json` được format kiểu nhiều dòng (mỗi thuộc tính một
> dòng), khác với `leads.json`, `quotes.json` và `design-projects.json` vốn để mỗi
> bản ghi trên một dòng. Snippet trên viết theo kiểu một dòng cho gọn — khi thêm vào
> `contracts.json` phải trải ra đúng kiểu của file đó, đừng trộn hai style.


- [ ] **Step 5: Restart, lint, kiểm tra bằng tay**

```bash
docker restart nihome31042025-backend
cd nihomeweb && npm run lint && npm run build
```

Kiểm tra bằng tay ba trạng thái: hợp đồng `Draft` hiện dòng giải thích và không có nút; hợp đồng `InProgress` có dự án hiện mã, tên, giai đoạn và bấm sang được; hợp đồng `InProgress` không có dự án hiện nút tạo, bấm xong điều hướng sang dự án mới.

- [ ] **Step 6: Commit**

```bash
git add nihomeweb/src/services/adminApi.ts \
        nihomeweb/src/pages/admin/ContractDetail.tsx \
        nihomebackend/Data/Seeds/i18n/contracts.json
git commit -m "Show the design project on the contract detail page

The handoff from contract to design already ran server-side but was
invisible, so operators could not tell the chain had advanced. The
block names the project, its stage, and offers a manual create when
the automatic one failed."
```

---

## Task 9: Tạo hợp đồng từ báo giá (A9, frontend)

**Files:**
- Modify: `nihomeweb/src/pages/admin/QuoteDetail.tsx`
- Modify: `nihomeweb/src/pages/admin/Contracts.tsx`
- Modify: `nihomebackend/Data/Seeds/i18n/quotes.json`

**Interfaces:**
- Consumes: `UpsertContractRequest` backend đã nhận `opportunityId` và `quoteId` (`ContractRequests.cs:18-19`), lưu ở `ContractService.cs:186-187`, và tự kiểm tra nhất quán ở `ContractService.cs:465-479`. **Không cần đổi backend trong task này.**

- [ ] **Step 1: Thêm nút trên chi tiết báo giá**

Trong `nihomeweb/src/pages/admin/QuoteDetail.tsx`, thêm vào cụm nút hành động:

```tsx
        {(quote.status === "Approved" || quote.status === "CustomerApproved") && (
          <Button variant="outline" onClick={() => goToContractForm()}>
            {t("quotes.createContract.action")}
          </Button>
        )}
```

Và handler dựng URL, chỉ thêm tham số khi thực sự có giá trị:

```tsx
  // QuoteResponse.customerId là optional (adminApi.ts:1111) nên báo giá cũ có thể
  // thiếu. Dựng bằng URLSearchParams để tham số rỗng biến mất khỏi URL, thay vì
  // gửi chuỗi "undefined" cho trang hợp đồng parse.
  const goToContractForm = () => {
    const params = new URLSearchParams({ fromQuote: String(quote.id) });
    if (quote.opportunityId) params.set("opportunityId", String(quote.opportunityId));
    if (quote.customerId) params.set("customerId", String(quote.customerId));
    if (quote.grandTotal > 0) params.set("value", String(quote.grandTotal));
    navigate(`/admin/contracts?${params.toString()}`);
  };
```

- [ ] **Step 2: Đọc prefill ở trang hợp đồng**

Trong `nihomeweb/src/pages/admin/Contracts.tsx`, `useSearchParams` đã được import sẵn (dòng 2, 174). Thêm effect mở form với dữ liệu điền sẵn:

```tsx
  // Number(null) là 0 và Number.isFinite(0) là true, nên parse thẳng từ
  // searchParams.get() sẽ biến tham số thiếu thành id 0. Parse từ chuỗi thô và
  // chỉ chấp nhận số dương.
  const readPositiveParam = (name: string): number | null => {
    const raw = searchParams.get(name);
    if (raw === null || raw.trim() === "") return null;
    const parsed = Number(raw);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
  };

  const fromQuoteId = readPositiveParam("fromQuote");
  const prefillOpportunityId = readPositiveParam("opportunityId");
  const prefillCustomerId = readPositiveParam("customerId");
  const prefillValue = readPositiveParam("value");

  useEffect(() => {
    if (fromQuoteId === null) return;
    setForm({
      ...emptyForm,
      customerId: prefillCustomerId,
      opportunityId: prefillOpportunityId,
      quoteId: fromQuoteId,
      value: prefillValue ?? 0,
    });
    setDialogOpen(true);
    // Chạy đúng một lần cho mỗi lần điều hướng sang kèm tham số.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fromQuoteId]);
```

`customerId: null` là hợp lệ — form hợp đồng vốn đã bắt buộc chọn khách hàng trước
khi lưu (`Contracts.tsx:335-338`), nên báo giá thiếu `customerId` sẽ rơi vào đúng
validation sẵn có thay vì lưu bản ghi trỏ vào khách hàng #0.

- [ ] **Step 3: Thêm hai trường vào kiểu form và payload**

Mở rộng `FormData` và `emptyForm` với `opportunityId: number | null` và `quoteId: number | null` (mặc định `null`), rồi thêm vào payload gửi đi (dòng ~369-377):

```tsx
      opportunityId: form.opportunityId,
      quoteId: form.quoteId,
```

- [ ] **Step 4: Hiện nguồn báo giá trên chi tiết hợp đồng**

Trong `ContractDetail.tsx`, thêm vào khối thông tin:

```tsx
        {contract.quoteId != null && (
          <div>
            <dt className="text-xs text-muted-foreground">{t("contracts.field.sourceQuote")}</dt>
            <dd className="text-sm">
              <Link className="underline" to={`/admin/quotes/${contract.quoteId}`}>
                {contract.quoteCode ?? `#${contract.quoteId}`}
              </Link>
            </dd>
          </div>
        )}

        {contract.opportunityId != null && (
          <div>
            <dt className="text-xs text-muted-foreground">
              {t("contracts.field.sourceOpportunity")}
            </dt>
            <dd className="text-sm">
              <Link
                className="underline"
                to={`/admin/opportunities/${contract.opportunityId}`}
              >
                {contract.opportunityTitle ?? `#${contract.opportunityId}`}
              </Link>
            </dd>
          </div>
        )}
```

> Link cơ hội trỏ vào route được thêm ở **Plan 2 Task A7**, giống Task 4. Cho tới
> lúc đó nó rơi vào trang 404 — đúng như vậy là biết, đừng coi Task 9 là chưa xong.

- [ ] **Step 5: Thêm key i18n**

Thêm vào `nihomebackend/Data/Seeds/i18n/quotes.json`:

```json
  { "key": "quotes.createContract.action", "category": "quotes", "vi": "Tạo hợp đồng từ báo giá", "en": "Create contract from quote", "zh": "根据报价创建合同", "ja": "見積から契約を作成" },
```

Thêm vào `nihomebackend/Data/Seeds/i18n/contracts.json`:

```json
  { "key": "contracts.field.sourceQuote", "category": "contracts", "vi": "Báo giá nguồn", "en": "Source quote", "zh": "来源报价", "ja": "元の見積" },
  { "key": "contracts.field.sourceOpportunity", "category": "contracts", "vi": "Cơ hội nguồn", "en": "Source opportunity", "zh": "来源商机", "ja": "元の商談" },
```
> **Chú ý style:** `contracts.json` được format kiểu nhiều dòng (mỗi thuộc tính một
> dòng), khác với `leads.json`, `quotes.json` và `design-projects.json` vốn để mỗi
> bản ghi trên một dòng. Snippet trên viết theo kiểu một dòng cho gọn — khi thêm vào
> `contracts.json` phải trải ra đúng kiểu của file đó, đừng trộn hai style.


- [ ] **Step 6: Restart, lint, kiểm tra bằng tay**

```bash
docker restart nihome31042025-backend
cd nihomeweb && npm run lint && npm run build
```

Kiểm tra bằng tay: mở một báo giá Approved, bấm tạo hợp đồng — form hợp đồng mở với khách hàng và giá trị đã điền. Lưu xong, mở chi tiết hợp đồng và bấm link ngược về báo giá. Kiểm tra tiếp rằng báo giá ở trạng thái Draft **không** hiện nút, và tạo hợp đồng trực tiếp từ trang hợp đồng vẫn chạy như cũ.

- [ ] **Step 7: Commit**

```bash
git add nihomeweb/src/pages/admin/QuoteDetail.tsx \
        nihomeweb/src/pages/admin/Contracts.tsx \
        nihomeweb/src/pages/admin/ContractDetail.tsx \
        nihomebackend/Data/Seeds/i18n/quotes.json \
        nihomebackend/Data/Seeds/i18n/contracts.json
git commit -m "Create contracts from an approved quote

The backend already accepted and cross-checked quoteId and
opportunityId; the form simply never sent them, so every contract
sat outside the sales chain. An approved quote now hands the
contract form its customer, opportunity and total."
```

---

## Kiểm tra cuối gói

Sau khi cả chín task xong, chạy đủ bộ trước khi mở PR:

```bash
# Backend build + lint — chạy được trong container backend vì chỉ đụng nihomebackend/
docker exec nihome31042025-backend dotnet build
docker exec nihome31042025-backend dotnet format --verify-no-changes

# Unit test — container SDK dùng một lần, mount cả repo (xem Global Constraints)
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true

# Integration test — cùng cách, khớp job nihomebackend-integration trong CI
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.integration.tests/nihomebackend.integration.tests.csproj -p:SkipNihomeWebBuild=true

# Frontend
cd nihomeweb && npm run lint && npm run build

# Smoke
docker compose up -d --build
cd nihomeweb && BASE_URL=http://localhost:5043 npx playwright test
```

Đi bộ luồng đầu-cuối bằng tay, đúng thứ tự này — đây là kịch bản demo:

1. Tạo lead mới, chuyển đổi → khách hàng và cơ hội xuất hiện trong danh sách.
2. Từ cơ hội tạo báo giá, duyệt báo giá.
3. Từ chi tiết báo giá bấm tạo hợp đồng → form điền sẵn, lưu lại.
4. Chuyển hợp đồng sang Đang thực hiện → chi tiết hợp đồng hiện dự án thiết kế.
5. Bấm sang dự án thiết kế → mở đúng dự án, đang ở giai đoạn Concept.

Nếu bất kỳ bước nào đứt, gói này chưa xong bất kể test có xanh.

## Việc còn nợ sang Plan 2

- Route `/admin/opportunities/:id` và `/admin/leads/:id` (A7). Task 4 đã đặt link trỏ tới route cơ hội trước khi nó tồn tại.
- Guard cơ hội Lost không tạo báo giá (A3) — luồng demo ở trên không chạm tới, nhưng người test tò mò thì chạm.
- Dirty-check báo giá (A4), lỗi ô search hợp đồng (A7), primitive UI (A8).
