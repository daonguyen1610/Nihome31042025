# Gói A phần 2 — Guard nghiệp vụ và lỗi chặn thao tác: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Chặn các thao tác sai nghiệp vụ trên báo giá, gỡ những lỗi khiến người dùng không đi hết được luồng, và đánh bóng các primitive nhập liệu dùng chung.

**Architecture:** A3 và A4 là backend thuần trên `QuoteService`. A7 là frontend cộng ba route mới, không đụng backend vì các đường `linkUrl` đã đúng dạng. A8 gom helper trùng lặp về `src/lib/` rồi dựng primitive dùng chung. **Không có EF migration nào trong plan này.**

**Tech Stack:** ASP.NET Core 8, EF Core, xUnit + Moq, React 18 + TypeScript, react-router-dom, shadcn/ui.

**Spec:** `docs/superpowers/specs/2026-08-20-crm-design-package-a-design.md`

**Plan liên quan:** `docs/superpowers/plans/2026-08-20-crm-package-a-chain.md` (phần 1). Task 3 dưới đây **gỡ nợ cho phần 1** — Task 4 và Task 9 của phần 1 đã đặt link trỏ tới những đường chưa tồn tại.

## Global Constraints

- **Không được tạo EF migration.** Container backend đang chạy thiếu `dotnet-ef`.
- **Test chạy trong container SDK dùng một lần, không phải container backend.** Container `nihome31042025-backend` chỉ mount `nihomebackend/` nên không thấy `nihomebackend.tests/`. Lệnh tương đương CI:

  ```bash
  docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
    dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true
  ```
  Cờ `-p:SkipNihomeWebBuild=true` là **bắt buộc**: `nihomebackend.csproj` có target
  chạy `npm ci` trước khi build, mà container SDK không có npm — thiếu cờ này thì
  build đứt với `MSB3073 ... npm: not found`. CI cũng truyền đúng cờ đó.


  Chạy từ thư mục gốc repo. `dotnet build` và `dotnet format` vẫn dùng `docker exec nihome31042025-backend` được.
- **Không hardcode chuỗi hiển thị trong React.** Mọi text qua `t("key")`.
- **Mỗi key i18n mới phải đủ bốn ngôn ngữ** `vi`, `en`, `zh`, `ja` trong `nihomebackend/Data/Seeds/i18n/`. Restart backend để `TranslationSeeder` chạy.
- **Truy vấn chỉ đọc dùng `AsNoTracking()`.**
- Chạy `docker exec nihome31042025-backend dotnet format` trước khi commit backend, `cd nihomeweb && npm run lint` trước khi commit frontend.
- Commit theo 50/72. Chỉ stage file liên quan.

## Thứ tự thực hiện

Đây là thứ tự bắt buộc, không phải gợi ý:

1. **Task 1 — A3** guard cơ hội Lost/Won với báo giá
2. **Task 2 — A4** dirty-check báo giá, gồm cả bảo toàn identity của dòng BOQ
3. **Task 3 — A7a** route chi tiết cho Lead / Cơ hội / Vai trò, cộng deep-link cho Khách hàng
4. **Task 4 — A7b** lỗi ô search hợp đồng
5. **Task 5 — A7c** điều tra preview file đấu thầu trên host
6. **Task 6 — A8a** gom định dạng tiền về `lib/numberFormat` sẵn có và dựng ô nhập tiền
7. **Task 7 — A8b** thanh kéo phần trăm, giật khi đổi trạng thái, timeline

**Task 3 nên đi trước Task 6 và 7 nếu lịch gấp** — phần 1 đã dùng trước những đường mà Task 3 tạo ra, nên tới khi Task 3 xong thì phần 1 mới thật sự hoàn chỉnh.

## File Structure

**Backend — sửa:**
- `nihomebackend/Services/QuoteService.cs` — guard A3, dirty-check A4

**Frontend — sửa:**
- `nihomeweb/src/App.tsx` — ba route chi tiết
- `nihomeweb/src/pages/admin/Leads.tsx`, `Opportunities.tsx`, `Customers.tsx`, `users/RoleList.tsx` — nhận id từ route/query
- `nihomeweb/src/pages/admin/Contracts.tsx` — sửa unmount ô search
- `nihomeweb/src/components/ui/money-input.tsx` — **tạo mới**, dựng trên `lib/numberFormat.ts` sẵn có
- `nihomeweb/src/pages/admin/ContractDetail.tsx`, `Contracts.tsx` — bỏ `formatCurrency` cục bộ, chuyển sang `lib/numberFormat.ts`

> **Không tạo helper định dạng mới.** `nihomeweb/src/lib/numberFormat.ts` đã có
> `formatVnd`, `formatVndWithSymbol`, `parseVnd` và đang được bốn trang admin dùng.

**Test — sửa:**
- `nihomebackend.tests/Services/QuoteServiceTests.cs`

---

## Task 1: Chặn báo giá trên cơ hội đã đóng (A3)

**Files:**
- Modify: `nihomebackend/Services/QuoteService.cs:131-195` (`CreateAsync`), và các hàm `SubmitAsync` / `SendAsync`
- Test: `nihomebackend.tests/Services/QuoteServiceTests.cs`

**Interfaces:**
- Consumes: `OpportunityStage` enum (`Prospecting, Qualification, Proposal, Negotiation, Won, Lost`), `QuoteOperationException`.
- Produces: không đổi chữ ký công khai nào.

- [ ] **Step 1: Viết test thất bại**

```csharp
[Fact]
public async Task CreateAsync_RejectsQuoteOnLostOpportunity()
{
    var opportunity = await SeedOpportunityAsync(OpportunityStage.Lost);

    var ex = await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.CreateAsync(
        new CreateQuoteRequest
        {
            OpportunityId = opportunity.Id,
            Method = QuoteMethod.UnitCost,
            AreaSqm = 100m,
            UnitPricePerSqm = 1_000_000m,
        },
        callerUserId: 1,
        canManage: true));

    Assert.Contains("Lost", ex.Message);
    Assert.Empty(_db.Quotes);
}

[Fact]
public async Task CreateAsync_RejectsQuoteOnWonOpportunity()
{
    var opportunity = await SeedOpportunityAsync(OpportunityStage.Won);

    await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.CreateAsync(
        new CreateQuoteRequest
        {
            OpportunityId = opportunity.Id,
            Method = QuoteMethod.UnitCost,
            AreaSqm = 100m,
            UnitPricePerSqm = 1_000_000m,
        },
        callerUserId: 1,
        canManage: true));

    Assert.Empty(_db.Quotes);
}
```

> Đọc phần đầu `QuoteServiceTests.cs` để lấy đúng tên helper seed cơ hội đang dùng ở đó và đúng chữ ký `CreateQuoteRequest`. Nếu chưa có helper nhận `OpportunityStage`, mở rộng helper sẵn có thêm một tham số mặc định `OpportunityStage.Prospecting` — đừng tạo helper song song.

- [ ] **Step 2: Chạy test, xác nhận thất bại**

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~RejectsQuoteOn"
```

Kỳ vọng: FAIL — báo giá được tạo bình thường vì `CreateAsync` load cơ hội rồi không kiểm tra `Stage`.

- [ ] **Step 3: Thêm guard vào `CreateAsync`**

Trong `nihomebackend/Services/QuoteService.cs`, ngay sau dòng nạp `opportunity` và trước `ValidateMethodPayload`:

```csharp
        // Cơ hội đã đóng thì không phát sinh báo giá mới được nữa. Guard đặt ở
        // đây chứ không ở UpdateAsync: báo giá cũ của một cơ hội thua vẫn phải
        // đọc và sửa ghi chú được để phục vụ đối soát.
        if (opportunity.Stage is OpportunityStage.Won or OpportunityStage.Lost)
        {
            throw new QuoteOperationException(
                $"Cơ hội #{opportunity.Id} đang ở trạng thái {opportunity.Stage} — không thể tạo báo giá mới.");
        }
```

- [ ] **Step 4: Chạy test, xác nhận pass**

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~RejectsQuoteOn"
```

Kỳ vọng: PASS.

- [ ] **Step 5: Viết test cho trường hợp cơ hội thua *sau khi* báo giá đã tồn tại**

Đây là lỗ hổng còn lại: `CreateAsync` chỉ chặn lúc tạo, nhưng cơ hội có thể chuyển Lost về sau, khi báo giá đang Draft.

```csharp
[Fact]
public async Task SubmitAsync_RejectsWhenOpportunityWentLostAfterQuoteWasDrafted()
{
    var opportunity = await SeedOpportunityAsync(OpportunityStage.Negotiation);
    var quote = await _sut.CreateAsync(
        new CreateQuoteRequest
        {
            OpportunityId = opportunity.Id,
            Method = QuoteMethod.UnitCost,
            AreaSqm = 100m,
            UnitPricePerSqm = 1_000_000m,
        },
        callerUserId: 1,
        canManage: true);

    var tracked = await _db.Opportunities.SingleAsync(o => o.Id == opportunity.Id);
    tracked.Stage = OpportunityStage.Lost;
    await _db.SaveChangesAsync();

    var ex = await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.SubmitAsync(
        quote.Id, new QuoteWorkflowRequest(), 1, canManage: true, canSeeAll: true));

    Assert.Contains("Lost", ex.Message);

    var saved = await _db.Quotes.SingleAsync(q => q.Id == quote.Id);
    Assert.Equal(QuoteStatus.Draft, saved.Status);
}

[Fact]
public async Task SubmitAsync_ReportsPermissionFailure_BeforeOpportunityStage()
{
    var opportunity = await SeedOpportunityAsync(OpportunityStage.Negotiation);
    var quote = await _sut.CreateAsync(
        new CreateQuoteRequest
        {
            OpportunityId = opportunity.Id,
            Method = QuoteMethod.UnitCost,
            AreaSqm = 100m,
            UnitPricePerSqm = 1_000_000m,
        },
        callerUserId: 1,
        canManage: true);

    var tracked = await _db.Opportunities.SingleAsync(o => o.Id == opportunity.Id);
    tracked.Stage = OpportunityStage.Lost;
    await _db.SaveChangesAsync();

    // Không có quyền thì phải nhận lỗi quyền, không phải trạng thái cơ hội —
    // nếu không là rò rỉ tình hình kinh doanh của bản ghi họ không được xem.
    var ex = await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.SubmitAsync(
        quote.Id, new QuoteWorkflowRequest(), 1, canManage: false, canSeeAll: true));

    Assert.DoesNotContain("Lost", ex.Message);
}
```

> Đọc chữ ký thật của `SubmitAsync` và `QuoteWorkflowRequest` trong `QuoteService.cs` / `IQuoteService.cs` trước khi viết — chúng đi qua helper `TransitionAsync` với nhiều tham số.

- [ ] **Step 6: Chặn Submit và Send trên cơ hội đã đóng**

`SubmitAsync` và `SendAsync` đều uỷ quyền cho `TransitionAsync`. Guard phải nằm
**bên trong** `TransitionAsync`, không phải trước lời gọi.

Lý do là thứ tự kiểm tra: `TransitionAsync` ném lỗi thiếu quyền trước
(`if (!permitted) throw`), rồi mới nạp báo giá, rồi mới kiểm tra quyền sở hữu
(`if (!canSeeAll && quote.OwnerUserId != callerUserId) return null;`). Đặt guard
trước lời gọi thì một người **không có quyền** hoặc **không sở hữu** báo giá vẫn
nhận được thông báo "cơ hội đang Lost" — tức là rò rỉ trạng thái kinh doanh của
một bản ghi mà lẽ ra họ không được biết là có tồn tại.

Thêm tham số vào `TransitionAsync`:

```csharp
    private async Task<QuoteResponse?> TransitionAsync(
        int id,
        int callerUserId,
        bool canSeeAll,
        QuoteStatus[] allowedFrom,
        QuoteStatus to,
        QuoteWorkflowAction action,
        bool permitted,
        string? note,
        Action<Quote> beforeSave,
        CancellationToken ct,
        bool requireOpenOpportunity = false)
```

Nạp kèm cơ hội — dòng `Include` hiện có đổi thành:

```csharp
        var quote = await db.Quotes
            .Include(q => q.Items)
            .Include(q => q.Opportunity)
            .FirstOrDefaultAsync(q => q.Id == id, ct);
```

Rồi chèn guard **ngay sau** dòng kiểm tra quyền sở hữu và **trước**
`AutoExpireIfNeeded(quote)`:

```csharp
        // Cơ hội có thể chuyển sang Lost sau khi báo giá đã soạn xong, nên guard
        // lúc tạo là chưa đủ. Đặt sau kiểm tra quyền sở hữu để người không có
        // quyền nhận đúng lỗi quyền, chứ không phải trạng thái kinh doanh.
        if (requireOpenOpportunity &&
            quote.Opportunity.Stage is OpportunityStage.Won or OpportunityStage.Lost)
        {
            throw new QuoteOperationException(
                $"Cơ hội của báo giá đang ở trạng thái {quote.Opportunity.Stage} — không thể chuyển tiếp báo giá.");
        }
```

Cuối cùng, truyền `requireOpenOpportunity: true` từ `SubmitAsync` và `SendAsync`.
Hai hàm này đang là expression-bodied nên chỉ cần thêm đối số vào lời gọi sẵn có,
không phải đổi thành thân khối.

**Không** truyền cờ này ở `CancelAsync`, `CustomerApproveAsync`,
`CustomerRejectAsync` — đóng sổ một báo giá thuộc cơ hội đã thua là thao tác hợp lệ
và cần thiết.

- [ ] **Step 7: Chạy toàn bộ test QuoteService**

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~QuoteServiceTests"
```

Kỳ vọng: PASS toàn bộ. Test cũ nào seed cơ hội ở stage `Won`/`Lost` rồi tạo báo giá sẽ đỏ — sửa test cho khớp hành vi mới.

- [ ] **Step 8: Build, format, commit**

```bash
docker exec nihome31042025-backend dotnet build
docker exec nihome31042025-backend dotnet format
git add nihomebackend/Services/QuoteService.cs \
        nihomebackend.tests/Services/QuoteServiceTests.cs
git commit -m "Stop quoting against a closed opportunity

Create loaded the opportunity but never looked at its stage, so a
lost pursuit kept accepting new quotes. Submit and send are guarded
too, since an opportunity can go lost after the quote was drafted.

Reading and editing an old quote stays open: a lost deal still has
to be reconcilable."
```

---

## Task 2: Dirty-check báo giá và bảo toàn identity dòng BOQ (A4)

**Files:**
- Modify: `nihomebackend/Services/QuoteService.cs:194-285` (`UpdateAsync`)
- Test: `nihomebackend.tests/Services/QuoteServiceTests.cs`

**Interfaces:**
- Consumes: `QuoteItem` (`ItemCode`, `Name`, `Unit`, `Quantity`, `UnitPrice`, `Amount`, `SortOrder`), `QuoteWorkflowAction`.
- Produces: không đổi chữ ký công khai nào.

**Ba mức hành vi phải đạt được:**

| Trường hợp | Version | Status | Log | Dòng BOQ |
|---|---|---|---|---|
| PUT không đổi gì | giữ nguyên | giữ nguyên | không ghi | `Id` **không đổi** |
| Chỉ đổi `Note` / `OwnerUserId` | giữ nguyên | giữ nguyên | ghi `Update` | không đổi |
| Đổi trường ảnh hưởng giá hoặc `ValidUntil` | bump | về `Draft` | ghi `NewVersion` + `Update` | dựng lại |

- [ ] **Step 1: Viết test thất bại cho no-op**

Test này bắt cả hai triệu chứng: version bị bump oan, và `Id` của dòng BOQ bị đổi.

```csharp
[Fact]
public async Task UpdateAsync_NoOp_KeepsVersionStatusLogsAndItemIds()
{
    var opportunity = await SeedOpportunityAsync(OpportunityStage.Negotiation);
    var created = await _sut.CreateAsync(
        new CreateQuoteRequest
        {
            OpportunityId = opportunity.Id,
            Method = QuoteMethod.Boq,
            Items = new List<QuoteItemInput>
            {
                new() { Name = "Bê tông", Unit = "m3", Quantity = 10m, UnitPrice = 1_500_000m },
            },
        },
        callerUserId: 1,
        canManage: true);

    // Đưa báo giá về trạng thái sau duyệt — đây là vùng mà bug xảy ra.
    var tracked = await _db.Quotes.SingleAsync(q => q.Id == created.Id);
    tracked.Status = QuoteStatus.Approved;
    await _db.SaveChangesAsync();

    var itemIdsBefore = await _db.QuoteItems
        .Where(i => i.QuoteId == created.Id)
        .Select(i => i.Id)
        .OrderBy(id => id)
        .ToListAsync();
    var logCountBefore = await _db.QuoteApprovalLogs.CountAsync(l => l.QuoteId == created.Id);

    // Gửi lại đúng những gì đang có — không đổi gì cả.
    await _sut.UpdateAsync(
        created.Id,
        new UpdateQuoteRequest
        {
            DiscountPercent = created.DiscountPercent,
            VatPercent = created.VatPercent,
            ValidUntil = created.ValidUntil,
            Note = created.Note,
            Items = new List<QuoteItemInput>
            {
                new() { Name = "Bê tông", Unit = "m3", Quantity = 10m, UnitPrice = 1_500_000m, SortOrder = 1 },
            },
        },
        callerUserId: 1,
        canManage: true,
        canSeeAll: true);

    var after = await _db.Quotes.SingleAsync(q => q.Id == created.Id);
    Assert.Equal(1, after.Version);
    Assert.Equal(QuoteStatus.Approved, after.Status);

    var logCountAfter = await _db.QuoteApprovalLogs.CountAsync(l => l.QuoteId == created.Id);
    Assert.Equal(logCountBefore, logCountAfter);

    var itemIdsAfter = await _db.QuoteItems
        .Where(i => i.QuoteId == created.Id)
        .Select(i => i.Id)
        .OrderBy(id => id)
        .ToListAsync();
    Assert.Equal(itemIdsBefore, itemIdsAfter);
}
```

- [ ] **Step 2: Chạy test, xác nhận thất bại**

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~UpdateAsync_NoOp"
```

Kỳ vọng: FAIL ở cả ba assert — version thành 2, status về Draft, và `Id` dòng BOQ đổi vì `RemoveRange` rồi chèn lại.

- [ ] **Step 3: Viết hàm so sánh thay đổi**

Thêm vào `QuoteService.cs`, cạnh `ApplyItems`:

```csharp
    /// <summary>
    /// Payload có đổi gì so với bản ghi hiện tại không, và có đổi thứ ảnh hưởng giá không.
    ///
    /// <c>Material</c> quyết định bump version; <c>Any</c> quyết định có ghi log
    /// <c>Update</c> hay không. Phân biệt hai mức này là điểm mấu chốt: "không đổi
    /// gì" và "đổi thứ không ảnh hưởng giá" là hai chuyện khác nhau.
    /// </summary>
    private readonly record struct QuoteChangeSet(bool Any, bool Material);

    private static QuoteChangeSet DetectChanges(Quote quote, UpdateQuoteRequest request)
    {
        // Chuẩn hoá null thành 0 trước khi so, vì FE gửi 0 thay cho ô bỏ trống —
        // không chuẩn hoá thì mọi lần lưu đều trông như có thay đổi.
        static decimal N(decimal? value) => value ?? 0m;
        static string? S(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        var material = false;

        if (quote.Method == QuoteMethod.UnitCost)
        {
            material |= N(quote.AreaSqm) != N(request.AreaSqm);
            material |= N(quote.UnitPricePerSqm) != N(request.UnitPricePerSqm);
            material |= S(quote.PackageDescription) != S(request.PackageDescription);
        }
        else
        {
            material |= !BoqItemsEqual(quote.Items, request.Items);
        }

        material |= quote.DiscountPercent != request.DiscountPercent;
        material |= quote.VatPercent != request.VatPercent;

        if (request.ValidUntil.HasValue)
        {
            material |= quote.ValidUntil != request.ValidUntil.Value;
        }

        // Không ảnh hưởng giá, nhưng vẫn là thay đổi cần ghi vết.
        var cosmetic = S(quote.Note) != S(request.Note)
            || (request.OwnerUserId.HasValue && request.OwnerUserId.Value != quote.OwnerUserId);

        return new QuoteChangeSet(Any: material || cosmetic, Material: material);
    }

    private static bool BoqItemsEqual(
        IReadOnlyCollection<QuoteItem> current,
        IReadOnlyCollection<QuoteItemInput> incoming)
    {
        if (current.Count != incoming.Count) return false;

        // SortOrder phải tham gia so sánh: đổi thứ tự dòng là thay đổi thật, người
        // dùng nhìn thấy nó trên bản in.
        //
        // Nhưng phải chuẩn hoá trước. ApplyItems gán
        // `SortOrder = i.SortOrder == 0 ? ++sort : i.SortOrder`, nghĩa là FE có thể
        // gửi toàn số 0 và để backend tự đánh số. So thẳng số 0 đó với SortOrder
        // 1..n đã lưu thì mọi lần lưu đều trông như có thay đổi — đúng cái bug
        // task này đang đi sửa.
        var normalisedIncoming = new List<(string? Code, string Name, string Unit, decimal Qty, decimal Price, int Sort)>();
        var running = 0;
        foreach (var item in incoming)
        {
            running++;
            normalisedIncoming.Add((
                string.IsNullOrWhiteSpace(item.ItemCode) ? null : item.ItemCode.Trim(),
                item.Name?.Trim() ?? string.Empty,
                item.Unit?.Trim() ?? string.Empty,
                item.Quantity,
                item.UnitPrice,
                item.SortOrder == 0 ? running : item.SortOrder));
        }

        var left = current.OrderBy(i => i.SortOrder).ToList();
        var right = normalisedIncoming.OrderBy(i => i.Sort).ToList();

        for (var i = 0; i < left.Count; i++)
        {
            var a = left[i];
            var b = right[i];
            if (a.SortOrder != b.Sort) return false;
            if (!string.Equals(a.Name?.Trim(), b.Name, StringComparison.Ordinal)) return false;
            if (!string.Equals(a.Unit?.Trim(), b.Unit, StringComparison.Ordinal)) return false;
            if (!string.Equals(
                    string.IsNullOrWhiteSpace(a.ItemCode) ? null : a.ItemCode.Trim(),
                    b.Code,
                    StringComparison.Ordinal)) return false;
            if (a.Quantity != b.Qty) return false;
            if (a.UnitPrice != b.Price) return false;
        }

        return true;
    }
```

- [ ] **Step 4: Thoát sớm khi không có thay đổi nào**

Trong `UpdateAsync`, ngay sau `ValidateMethodPayload(...)` và **trước** `var now = DateTime.UtcNow;`:

```csharp
        var changes = DetectChanges(quote, request);

        // Thoát trước khi chạm vào entity. Không chỉ để tránh log thừa: đường ghi
        // bên dưới gọi QuoteItems.RemoveRange rồi dựng lại toàn bộ dòng BOQ, nên
        // một PUT không đổi gì vẫn cấp Id mới cho mọi dòng. Bất cứ thứ gì tham
        // chiếu tới các Id đó đều gãy theo.
        if (!changes.Any)
        {
            return await GetAsync(quote.Id, callerUserId, canSeeAll: true, ct);
        }
```

- [ ] **Step 5: Gác khối bump version bằng `Material`**

Đổi điều kiện `isPostApproval` để nó xét cả thay đổi thực chất:

```csharp
        var isPostApproval = changes.Material && quote.Status is QuoteStatus.Approved
            or QuoteStatus.SentToCustomer
            or QuoteStatus.Expired;
```

Phần thân khối `if (isPostApproval)` giữ nguyên.

- [ ] **Step 6: Không dựng lại dòng BOQ khi chúng không đổi**

Thay khối `RemoveRange` + `ApplyItems` bằng:

```csharp
        // Chỉ dựng lại khi tập dòng thật sự khác. Dựng lại vô cớ sẽ đổi Id.
        if (quote.Method == QuoteMethod.Boq && !BoqItemsEqual(quote.Items, request.Items))
        {
            db.QuoteItems.RemoveRange(quote.Items);
            quote.Items = new List<QuoteItem>();
            ApplyItems(quote, quote.Method, request.Items);
        }

        RecomputeTotals(quote);
```

`RecomputeTotals` vẫn chạy mọi lần, vì `DiscountPercent` hoặc `VatPercent` có thể đổi mà dòng BOQ thì không.

- [ ] **Step 7: Chạy test no-op, xác nhận pass**

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~UpdateAsync_NoOp"
```

Kỳ vọng: PASS.

- [ ] **Step 8: Viết test cho hai mức còn lại**

```csharp
[Fact]
public async Task UpdateAsync_NoteOnly_LogsUpdateWithoutBumpingVersion()
{
    var created = await SeedApprovedUnitCostQuoteAsync();

    await _sut.UpdateAsync(
        created.Id,
        new UpdateQuoteRequest
        {
            AreaSqm = created.AreaSqm,
            UnitPricePerSqm = created.UnitPricePerSqm,
            DiscountPercent = created.DiscountPercent,
            VatPercent = created.VatPercent,
            ValidUntil = created.ValidUntil,
            Note = "Ghi chú nội bộ mới",
        },
        callerUserId: 1, canManage: true, canSeeAll: true);

    var after = await _db.Quotes.SingleAsync(q => q.Id == created.Id);
    Assert.Equal(1, after.Version);
    Assert.Equal(QuoteStatus.Approved, after.Status);

    Assert.Contains(
        await _db.QuoteApprovalLogs.Where(l => l.QuoteId == created.Id).ToListAsync(),
        l => l.Action == QuoteWorkflowAction.Update);
    Assert.DoesNotContain(
        await _db.QuoteApprovalLogs.Where(l => l.QuoteId == created.Id).ToListAsync(),
        l => l.Action == QuoteWorkflowAction.NewVersion);
}

[Fact]
public async Task UpdateAsync_DiscountChange_BumpsVersionAndReturnsToDraft()
{
    var created = await SeedApprovedUnitCostQuoteAsync();

    await _sut.UpdateAsync(
        created.Id,
        new UpdateQuoteRequest
        {
            AreaSqm = created.AreaSqm,
            UnitPricePerSqm = created.UnitPricePerSqm,
            DiscountPercent = created.DiscountPercent + 5m,
            VatPercent = created.VatPercent,
            ValidUntil = created.ValidUntil,
            Note = created.Note,
        },
        callerUserId: 1, canManage: true, canSeeAll: true);

    var after = await _db.Quotes.SingleAsync(q => q.Id == created.Id);
    Assert.Equal(2, after.Version);
    Assert.Equal(QuoteStatus.Draft, after.Status);
    Assert.Contains(
        await _db.QuoteApprovalLogs.Where(l => l.QuoteId == created.Id).ToListAsync(),
        l => l.Action == QuoteWorkflowAction.NewVersion);
}

[Fact]
public async Task UpdateAsync_ReorderingBoqLines_BumpsVersion()
{
    var created = await SeedApprovedBoqQuoteAsync();

    // Cùng tập dòng, đảo thứ tự — đây là thay đổi thật, người dùng thấy trên bản in.
    var items = await _db.QuoteItems
        .Where(i => i.QuoteId == created.Id)
        .OrderBy(i => i.SortOrder)
        .ToListAsync();
    Assert.True(items.Count >= 2, "Helper phải seed ít nhất hai dòng BOQ.");

    await _sut.UpdateAsync(
        created.Id,
        new UpdateQuoteRequest
        {
            DiscountPercent = created.DiscountPercent,
            VatPercent = created.VatPercent,
            ValidUntil = created.ValidUntil,
            Note = created.Note,
            Items = items
                .AsEnumerable()
                .Reverse()
                .Select((i, index) => new QuoteItemInput
                {
                    ItemCode = i.ItemCode,
                    Name = i.Name,
                    Unit = i.Unit,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    SortOrder = index + 1,
                })
                .ToList(),
        },
        callerUserId: 1, canManage: true, canSeeAll: true);

    var after = await _db.Quotes.SingleAsync(q => q.Id == created.Id);
    Assert.Equal(2, after.Version);
}

[Fact]
public async Task UpdateAsync_AddingBoqLine_BumpsVersion()
{
    var created = await SeedApprovedBoqQuoteAsync();

    await _sut.UpdateAsync(
        created.Id,
        new UpdateQuoteRequest
        {
            DiscountPercent = created.DiscountPercent,
            VatPercent = created.VatPercent,
            ValidUntil = created.ValidUntil,
            Note = created.Note,
            Items = new List<QuoteItemInput>
            {
                new() { Name = "Bê tông", Unit = "m3", Quantity = 10m, UnitPrice = 1_500_000m, SortOrder = 1 },
                new() { Name = "Thép", Unit = "kg", Quantity = 500m, UnitPrice = 20_000m, SortOrder = 2 },
            },
        },
        callerUserId: 1, canManage: true, canSeeAll: true);

    var after = await _db.Quotes.SingleAsync(q => q.Id == created.Id);
    Assert.Equal(2, after.Version);
    Assert.Equal(2, await _db.QuoteItems.CountAsync(i => i.QuoteId == created.Id));
}
```

> Hai helper `SeedApprovedUnitCostQuoteAsync` và `SeedApprovedBoqQuoteAsync` gói lại
> đúng phần dựng ở Step 1 (tạo báo giá rồi đặt `Status = Approved`). Viết chúng ở
> cuối class test, cạnh các helper sẵn có. `SeedApprovedBoqQuoteAsync` phải seed
> **ít nhất hai dòng** BOQ — test đảo thứ tự ở trên cần vậy mới có nghĩa.

- [ ] **Step 9: Chạy toàn bộ test QuoteService**

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~QuoteServiceTests"
```

Kỳ vọng: PASS toàn bộ.

- [ ] **Step 10: Build, format, commit**

```bash
docker exec nihome31042025-backend dotnet build
docker exec nihome31042025-backend dotnet format
git add nihomebackend/Services/QuoteService.cs \
        nihomebackend.tests/Services/QuoteServiceTests.cs
git commit -m "Only version a quote when something actually changed

Saving an approved quote bumped the version, reset it to draft and
wrote an approval log even when the payload was identical, because
the decision read the status alone.

The no-op path now returns before touching the entity. That matters
beyond the stray log: the write path deletes and rebuilds every BOQ
line, so an unchanged save was handing them all new ids."
```

---

## Task 3: Route chi tiết cho Lead, Cơ hội, Vai trò và deep-link Khách hàng (A7a)

**Files:**
- Modify: `nihomeweb/src/App.tsx:158-170, 124-129`
- Modify: `nihomeweb/src/pages/admin/Leads.tsx`
- Modify: `nihomeweb/src/pages/admin/Opportunities.tsx`
- Modify: `nihomeweb/src/pages/admin/Customers.tsx`
- Modify: `nihomeweb/src/pages/admin/users/RoleList.tsx`

**Interfaces:**
- Consumes: `openDetail(id)` sẵn có ở `Leads.tsx:164`, `Opportunities.tsx:282`, `Customers.tsx:258`.
- Produces: route `/admin/leads/:id`, `/admin/opportunities/:id`, `/admin/roles/:id`, và hỗ trợ `?open=` trên trang Khách hàng. **Plan 1 Task 4 và Task 9 phụ thuộc trực tiếp vào task này.**

**Vì sao dùng route chứ không phải query param.** Backend đã phát `linkUrl` dạng đường dẫn ở ba chỗ — `LeadService.cs:490`, `OpportunityService.cs:524` và `:553`, `RoleService.cs:177, 284, 372` — nhưng App.tsx không có route nào khớp, nên bấm notification rơi vào trang 404. Thêm route thì **không phải đụng backend**, và URL giữ đúng hình dạng khi sau này dựng trang chi tiết thật.

`?open=` vẫn phải tiếp tục chạy: `Opportunities.tsx:312-318` đã dùng nó, và `Customers.tsx:1524` đang trỏ sang `/admin/opportunities?open=`. Hai cách sống song song.

- [ ] **Step 1: Thêm ba route**

Trong `nihomeweb/src/App.tsx`, thêm route con vào đúng khối `RequirePermission` sẵn có của từng trang:

```tsx
              <Route element={<RequirePermission code={ADMIN_PERMS.leads} />}>
                <Route path="/admin/leads" element={<AdminLeads />} />
                <Route path="/admin/leads/:id" element={<AdminLeads />} />
              </Route>
```

```tsx
              <Route element={<RequirePermission code={ADMIN_PERMS.opportunities} />}>
                <Route path="/admin/opportunities" element={<AdminOpportunities />} />
                <Route path="/admin/opportunities/:id" element={<AdminOpportunities />} />
              </Route>
```

```tsx
              <Route element={<RequirePermission code={ADMIN_PERMS.rbacRoles} />}>
                <Route path="/admin/roles" element={<AdminRoles />} />
                <Route path="/admin/roles/:id" element={<AdminRoles />} />
              </Route>
```

Cùng một component phục vụ cả hai đường — trang danh sách chỉ đọc thêm tham số và tự mở bản ghi.

- [ ] **Step 2: Leads mở bản ghi từ route**

Trong `nihomeweb/src/pages/admin/Leads.tsx`, thêm cạnh các hook khác:

```tsx
  const { id: routeId } = useParams();
  const [handledRouteId, setHandledRouteId] = useState<number | null>(null);

  // Notification của lead phát linkUrl dạng /admin/leads/{id} (LeadService.cs:490).
  // Không có chỗ này thì bấm vào noti rơi thẳng vào trang 404.
  useEffect(() => {
    const parsed = Number(routeId);
    if (!Number.isInteger(parsed) || parsed <= 0) return;
    if (handledRouteId === parsed) return;
    setHandledRouteId(parsed);
    void openDetail(parsed);
    // openDetail là hàm thường khai báo trong thân component, nên nó đổi tham
    // chiếu mỗi lần render — đưa vào deps sẽ khiến effect chạy lại liên tục.
    // Cờ handledRouteId đã chặn mở lại cùng một bản ghi.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [routeId, handledRouteId]);
```

Bổ sung `useParams` vào import từ `react-router-dom`.

> `Opportunities.tsx` thì khác: `openDetail` ở đó là `useCallback` (dòng 282) nên
> tham chiếu ổn định và **phải** nằm trong deps — effect ở Step 3 giữ nguyên nó.
> Đừng sao chép dòng eslint-disable sang file đó.

- [ ] **Step 3: Cơ hội mở bản ghi từ route**

`Opportunities.tsx:312-318` đã có effect đọc `?open=`. Thêm route id vào cùng effect đó thay vì viết effect thứ hai:

```tsx
  const { id: routeId } = useParams();

  useEffect(() => {
    const fromRoute = Number(routeId);
    const fromQuery = Number(searchParams.get("open"));
    const openId = Number.isInteger(fromRoute) && fromRoute > 0 ? fromRoute : fromQuery;
    if (Number.isInteger(openId) && openId > 0 && handledOpenId !== openId) {
      setHandledOpenId(openId);
      void openDetail(openId);
    }
  }, [handledOpenId, openDetail, searchParams, routeId]);
```

Route thắng query param khi cả hai cùng có, nhưng `?open=` vẫn chạy nguyên vẹn cho các link cũ.

- [ ] **Step 4: Khách hàng nhận `?open=`**

`Customers.tsx` hiện **không đọc searchParams nào cả** — nên link `/admin/customers?open={id}` mà Plan 1 Task 4 dựng sẽ chỉ mở danh sách. Thêm:

```tsx
  const [searchParams] = useSearchParams();
  const [handledOpenId, setHandledOpenId] = useState<number | null>(null);

  useEffect(() => {
    const openId = Number(searchParams.get("open"));
    if (Number.isInteger(openId) && openId > 0 && handledOpenId !== openId) {
      setHandledOpenId(openId);
      void openDetail(openId);
    }
    // Cùng lý do như Leads: openDetail ở file này là hàm thường, không phải
    // useCallback, nên deps chỉ giữ những giá trị thật sự điều khiển effect.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [handledOpenId, searchParams]);
```

Bổ sung `useSearchParams` vào import từ `react-router-dom`.

> Không thêm route `/admin/customers/:id` ở task này: không có `linkUrl` nào của backend trỏ tới nó, nên sẽ là code không ai gọi.

- [ ] **Step 5: Vai trò cuộn tới thẻ tương ứng**

`RoleList.tsx` bày mọi vai trò thành thẻ trên một trang, **không có dialog chi tiết**. Nên route chỉ cần cuộn tới và làm nổi thẻ đúng:

```tsx
  const { id: routeId } = useParams();

  // RoleService phát linkUrl /admin/roles/{id} ở ba chỗ (dòng 177, 284, 372).
  // Trang này không có dialog chi tiết, nên "mở" nghĩa là cuộn tới đúng thẻ.
  useEffect(() => {
    const parsed = Number(routeId);
    if (!Number.isInteger(parsed) || parsed <= 0) return;
    const node = document.getElementById(`role-card-${parsed}`);
    if (!node) return;
    node.scrollIntoView({ behavior: "smooth", block: "center" });
    node.classList.add("ring-2", "ring-primary");
    const timer = window.setTimeout(
      () => node.classList.remove("ring-2", "ring-primary"),
      2000,
    );
    return () => window.clearTimeout(timer);
  }, [routeId, roles]);
```

Và gắn `id={`role-card-${role.id}`}` vào phần tử gốc của mỗi thẻ vai trò.

> `roles` trong mảng phụ thuộc là danh sách vai trò đã tải — dùng đúng tên biến đang có trong file. Effect phải chạy lại sau khi dữ liệu về, nếu không thẻ chưa tồn tại trong DOM lúc effect chạy lần đầu.

- [ ] **Step 6: Lint, build, kiểm tra bằng tay**

```bash
cd nihomeweb && npm run lint && npm run build
```

Kiểm tra bằng tay bốn đường:
1. Tạo một lead mới để sinh notification, bấm vào noti → mở đúng lead, không phải 404.
2. Mở `/admin/opportunities/{id}` trực tiếp → mở đúng cơ hội. Mở `/admin/opportunities?open={id}` → vẫn chạy.
3. Mở `/admin/roles/{id}` → cuộn tới đúng thẻ vai trò và nó sáng lên.
4. Quay lại **Plan 1**: mở một lead đã chuyển đổi, bấm "Xem khách hàng" và "Xem cơ hội" → cả hai giờ mở đúng bản ghi.

Bước 4 chính là phần nợ mà task này trả.

- [ ] **Step 7: Commit**

```bash
git add nihomeweb/src/App.tsx \
        nihomeweb/src/pages/admin/Leads.tsx \
        nihomeweb/src/pages/admin/Opportunities.tsx \
        nihomeweb/src/pages/admin/Customers.tsx \
        nihomeweb/src/pages/admin/users/RoleList.tsx
git commit -m "Make notification links reach their record

Leads, opportunities and roles all emit path-style links, but no
route matched them, so every one of those notifications landed on
the 404 page. The list pages now accept an id from the route and
open the record themselves.

Customers gains the ?open= handling the repo already uses
elsewhere, which the lead conversion links depend on."
```

---

## Task 4: Ô search hợp đồng mất focus (A7b)

**Files:**
- Modify: `nihomeweb/src/pages/admin/Contracts.tsx:196-232, 428-433`

**Interfaces:** không đổi gì công khai.

**Nguyên nhân gốc.** `Contracts.tsx:428` early-return `if (loading && contracts.length === 0)` thay **toàn bộ trang** bằng `<PageLoading />`, kể cả panel filter chứa ô search. `load()` chạy lại mỗi lần `search` đổi và set `loading = true`. Gõ tới khi không còn kết quả → `contracts` rỗng → ký tự tiếp theo làm điều kiện thành true → cả trang unmount → input mất focus, không gõ và không xoá được nữa. Đó là lý do lỗi trông như lúc có lúc không: nó chỉ xuất hiện sau khi kết quả về rỗng.

- [ ] **Step 1: Tách trạng thái tải lần đầu khỏi tải lại**

Thêm state cạnh `loading`:

```tsx
  // Chỉ lần tải đầu tiên mới được phép thay cả trang. Mọi lần tải lại về sau chỉ
  // được hiện trạng thái bận bên trong vùng bảng, nếu không panel filter sẽ
  // unmount và cướp focus khỏi ô search đang gõ.
  const [initialLoaded, setInitialLoaded] = useState(false);
```

Trong `load()`, ở khối `finally`, đặt cờ sau lần chạy đầu:

```tsx
    } finally {
      setLoading(false);
      setInitialLoaded(true);
    }
```

- [ ] **Step 2: Sửa điều kiện early-return**

Thay dòng 428:

```tsx
  if (!initialLoaded && loading) {
```

Điều kiện cũ phụ thuộc `contracts.length === 0`, tức là gắn việc dựng khung trang vào *kết quả tìm kiếm* — đó chính là bug.

- [ ] **Step 3: Hiện trạng thái bận trong vùng bảng**

Ngay trên phần tử bảng, thêm:

```tsx
        {loading && initialLoaded && (
          <p className="px-1 py-2 text-xs text-muted-foreground">{t("common.loading")}</p>
        )}
```

- [ ] **Step 4: Debounce ô search**

Hiện mỗi phím gõ bắn một request vì `search` nằm trong mảng phụ thuộc của `load`. Tách giá trị gõ khỏi giá trị dùng để truy vấn:

```tsx
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedSearch(search), 300);
    return () => window.clearTimeout(timer);
  }, [search]);
```

Rồi trong `load()` dùng `debouncedSearch` thay cho `search`:

```tsx
      if (debouncedSearch.trim()) params.search = debouncedSearch.trim();
```

và đổi `search` thành `debouncedSearch` trong mảng phụ thuộc của `useCallback(load, [...])` cùng effect xoá lựa chọn hàng loạt ở dòng 273. Ô `<Input>` vẫn bind vào `search`, giữ nguyên.

- [ ] **Step 5: Lint, build, kiểm tra bằng tay**

```bash
cd nihomeweb && npm run lint && npm run build
```

Kiểm tra bằng tay — đây là kịch bản tái hiện chính xác lỗi cũ: mở `/admin/contracts`, gõ vào ô search một chuỗi chắc chắn không khớp gì, ví dụ `zzzzzz`. Bảng về rỗng. Gõ thêm vài ký tự nữa rồi xoá dần bằng backspace. Con trỏ phải giữ nguyên trong ô suốt quá trình, không mất ký tự nào, và panel filter không nhấp nháy.

- [ ] **Step 6: Commit**

```bash
git add nihomeweb/src/pages/admin/Contracts.tsx
git commit -m "Keep the contract search box mounted while loading

The page swapped itself for a loading state whenever a fetch was in
flight and the list happened to be empty. Typing past the last
match therefore unmounted the very input being typed into, which
is why the box refused both new characters and backspace.

Only the first load replaces the page now, and the query is
debounced instead of firing on every keystroke."
```

---

## Task 5: Điều tra preview file đấu thầu trên host (A7c)

**Files:**
- Modify: `.gitignore` (vệ sinh repo, không liên quan bug)
- Còn lại **chưa xác định được** cho tới khi có kết quả điều tra — xem lý do dưới.

**Đây là task điều tra, không phải task sửa.** Spec ghi rõ chưa đủ dữ liệu để chốt nguyên nhân. File đấu thầu lưu đúng chỗ: `TendersController.cs:326` ghi vào `wwwroot/files/tenders`, cùng cách với module Hợp đồng và Báo giá đang chạy tốt. Nên nguyên nhân nằm ngoài code đường ghi.

**Không được đoán rồi sửa bừa.** Chạy hết các bước chẩn đoán dưới đây, ghi lại kết quả, rồi mới quyết định.

- [ ] **Step 1: Xác nhận lỗi tái hiện được trên host**

Upload một file đấu thầu mới trên host, rồi bấm xem trước. Ghi lại: mã HTTP trả về, URL chính xác của request, và nội dung response.

- [ ] **Step 2: File có thật sự nằm trên đĩa của host không**

```bash
docker exec nihome31042025-backend ls -la /app/wwwroot/files/tenders
docker exec nihome31042025-backend ls -la /app/wwwroot/files/contracts
```

So sánh hai thư mục. Nếu `contracts` có file mà `tenders` rỗng thì vấn đề nằm ở đường ghi. Nếu **cả hai đều rỗng trên host** trong khi giao diện vẫn liệt kê bản ghi, thì đây là mất dữ liệu do deploy chứ không phải lỗi preview — nhánh này dẫn tới Step 4.

- [ ] **Step 3: Đối chiếu với module đang chạy tốt**

Upload một file **hợp đồng** trên host rồi xem trước. Nếu hợp đồng chạy được mà đấu thầu thì không, khác biệt nằm trong code đấu thầu và Step 1 đã có URL để lần theo. Nếu **cả hai đều hỏng** trên host mà ở local đều chạy, khác biệt nằm ở tầng phục vụ file tĩnh hoặc reverse proxy của host — không phải code ứng dụng.

- [ ] **Step 4: Kiểm tra `wwwroot/files` có được giữ qua các lần deploy không**

```bash
cat deployment-config/*.yml deployment-config/*.yaml 2>/dev/null | grep -A 10 volumes
```

`docker-compose.yaml` ở gốc bind-mount `./nihomebackend:/app` cho môi trường dev, nên file sống sót ở local. Cấu hình deploy trên host có thể khác. Nếu không có volume nào giữ `wwwroot/files`, thì mọi lần build lại là mọi file upload biến mất — và cách sửa nằm ở tầng deployment, không phải code.

### Kết quả điều tra (2026-08-21) — chặn ở bước cần quyền host

Các bước chẩn đoán làm được không cần host đã chạy xong. Ghi lại để người có
quyền trên host tiếp tục:

**Loại trừ được: code đường ghi và đường phục vụ đều đúng.** Ở môi trường local,
`GET /files/tenders/{file}` trả **200**. File nằm đúng chỗ trong
`wwwroot/files/tenders`. Vậy lỗi không nằm ở `TendersController` hay
`UseStaticFiles`.

**Host không phải Docker mà là IIS trên Windows.** `deployment-config/web.config`
dùng `AspNetCoreModuleV2`, `hostingModel="inprocess"`, và trỏ tới
`D:\vhosts\vietnamconstruction.info\`. Nghĩa là toàn bộ suy luận theo
`docker-compose.yaml` không áp dụng cho host — đó là lý do Step 4 của plan gốc
tìm volume mà không thấy.

**Nghi vấn còn lại, và là nghi vấn mạnh nhất:** `auto-deployment.sh` dựng
`publish-release/wwwroot/files/` **chỉ từ** `deployment-config/files/`, mà thư mục
đó hiện **chỉ chứa `cv/`**. Không có `tenders/`, `contracts/`, `quotes/`,
`capability/`. Chính comment đầu script cũng mô tả quy trình này cho `images/`:
phải kéo dữ liệu từ host về `deployment-config` rồi mới deploy lại, nếu không sẽ
lệch. Việc đó đang được làm cho `images/` nhưng **không** làm cho `files/`.

Nên nếu bản zip được giải nén đè lên thư mục ứng dụng trên host, mọi file người
dùng upload lúc chạy đều có nguy cơ bị mất hoặc không bao giờ có mặt.

**Việc cần người có quyền host làm, đúng hai câu hỏi:**

1. File đang lỗi có còn nằm trên đĩa host không? Kiểm tra
   `D:\vhosts\...\wwwroot\files\tenders\`. Nếu trống trong khi giao diện
   vẫn liệt kê bản ghi → đây là mất dữ liệu khi deploy, không phải lỗi preview.
2. Nếu file có ở đó, gọi thẳng URL `/files/tenders/{tên file}` trên host và ghi
   lại mã HTTP. 404 với file có thật thì vấn đề nằm ở tầng IIS.

**Chưa sửa gì cho phần này** — sửa mà chưa phân biệt được hai nhánh trên thì chỉ
là đoán.

- [ ] **Step 5: Ghi kết quả rồi mới chọn cách sửa**

Viết phát hiện vào ticket NIH-446 kèm bằng chứng từ các bước trên. Cách sửa rơi vào một trong ba nhánh:

- **File không được ghi ra** → sửa đường upload trong `TendersController`.
- **File có mà không phục vụ được** → sửa cấu hình static file hoặc reverse proxy.
- **File biến mất sau deploy** → thêm volume giữ dữ liệu; đây là việc của người có quyền trên host, không phải thay đổi code.

Chỉ sau khi chốt được nhánh mới viết task sửa. **Đừng gộp bước sửa vào task này.**

- [ ] **Step 6: Bổ sung dòng thiếu trong `.gitignore`**

Việc này độc lập với bug, làm luôn cho gọn. `.gitignore` đã bỏ qua `wwwroot/files/` cho `capability/`, `contracts/`, `quotes/`, `business-documents/` nhưng thiếu `tenders/`, nên `nihomebackend/wwwroot/files/tenders/` đang nằm untracked trong working tree.

Thêm cạnh các dòng cùng loại:

```
nihomebackend/wwwroot/files/tenders/
```

```bash
git add .gitignore
git commit -m "Ignore uploaded tender files

Every other upload directory under wwwroot/files is ignored; this
one was missed, so real uploads showed up as untracked changes."
```

---

## Task 6: Tái dùng `numberFormat` và dựng ô nhập tiền (A8a)

**Files:**
- Create: `nihomeweb/src/components/ui/money-input.tsx`
- Modify: `nihomeweb/src/pages/admin/Contracts.tsx:121, 870-874`
- Modify: `nihomeweb/src/pages/admin/ContractDetail.tsx:107, 620`

**Interfaces:**
- Consumes: `formatVnd`, `formatVndWithSymbol`, `parseVnd` từ `@/lib/numberFormat` — **đã tồn tại**.
- Produces: component `<MoneyInput value={number} onChange={(next: number) => void} />`.

**Không tạo file helper mới.** `nihomeweb/src/lib/numberFormat.ts` đã có sẵn
`formatVnd`, `formatVndWithSymbol`, `formatFileSize` và `parseVnd`, và đang được
`QuoteDetail.tsx`, `Quotes.tsx`, `Opportunities.tsx`, `Customers.tsx` dùng.

Vấn đề là `Contracts.tsx:121` và `ContractDetail.tsx:107` **không dùng nó** — mỗi
file tự khai một `formatCurrency` cục bộ. Vậy là ba nơi định dạng tiền cho cùng một
sản phẩm. Task này gom hai kẻ lạc về helper chung, **không** dựng thêm nơi thứ tư.

- [ ] **Step 1: Đọc helper sẵn có và hai bản cục bộ**

```bash
cat nihomeweb/src/lib/numberFormat.ts
sed -n '121,130p' nihomeweb/src/pages/admin/Contracts.tsx
sed -n '107,116p' nihomeweb/src/pages/admin/ContractDetail.tsx
```

Ghi lại hai khác biệt về hành vi sẽ xảy ra khi chuyển sang helper chung:

1. **Ngôn ngữ.** `formatCurrency(value, lang)` cục bộ nhận `lang`; `formatVnd` cố
   định `vi-VN`. Sau khi đổi, số tiền trên màn hình hợp đồng luôn định dạng kiểu
   Việt Nam kể cả khi giao diện đang ở tiếng Anh, Trung hay Nhật.
2. **Giá trị rỗng.** `formatVnd` trả `"—"` cho `null`/`NaN`.

Cả hai đều là **đổi hành vi có chủ đích**, đổi lấy việc màn hình hợp đồng khớp với
Báo giá, Cơ hội và Khách hàng — vốn đã dùng `formatVnd` từ trước. Nêu rõ hai điểm
này trong commit message. Nếu người review thấy điểm 1 không chấp nhận được thì
dừng lại và hỏi, đừng tự thêm tham số `lang` vào helper chung.

- [ ] **Step 2: Bỏ hai bản cục bộ**

Trong `Contracts.tsx` và `ContractDetail.tsx`, xoá định nghĩa `formatCurrency` cục
bộ và thêm:

```ts
import { formatVnd, formatVndWithSymbol } from "@/lib/numberFormat";
```

Nơi gọi cũ ghép ký hiệu bằng tay, ví dụ `Contracts.tsx:874`:

```tsx
                <p className="text-xs text-muted-foreground">{formatCurrency(form.value, lang)} ₫</p>
```

đổi thành `formatVndWithSymbol` và **bỏ ký hiệu ghép tay**, nếu không sẽ ra hai dấu ₫:

```tsx
                <p className="text-xs text-muted-foreground">{formatVndWithSymbol(form.value)}</p>
```

Chỗ nào đang tự ghép `₫` thì dùng `formatVndWithSymbol`; chỗ nào không ghép thì dùng
`formatVnd`. Rà từng nơi gọi, đừng thay hàng loạt bằng một lệnh sed.

Nếu biến `lang` trở nên không còn ai dùng trong file, xoá luôn cả khai báo lấy nó —
linter sẽ báo biến thừa.

- [ ] **Step 3: Kiểm tra không còn bản sao nào**

```bash
grep -rn "const formatCurrency" nihomeweb/src
```

Kỳ vọng: không còn kết quả nào. Toàn bộ định dạng tiền giờ đi qua
`src/lib/numberFormat.ts`.

- [ ] **Step 4: Lint và build sau bước gom**

```bash
cd nihomeweb && npm run lint && npm run build
```

Chạy ngay ở đây, trước khi dựng component mới — nếu có nơi gọi nào bị sót thì lỗi
sẽ chỉ thẳng vào nó, thay vì lẫn với thay đổi của bước sau.

- [ ] **Step 5: Dựng `MoneyInput` trên helper sẵn có**

Tạo `nihomeweb/src/components/ui/money-input.tsx`:

```tsx
import { useEffect, useState } from "react";
import { Input } from "@/components/ui/input";
import { formatVnd, parseVnd } from "@/lib/numberFormat";
import { cn } from "@/lib/utils";

type MoneyInputProps = {
  value: number;
  onChange: (next: number) => void;
  id?: string;
  disabled?: boolean;
  className?: string;
};

/**
 * Ô nhập tiền có phân tách hàng nghìn, dựng trên formatVnd/parseVnd để cách hiển
 * thị khớp với mọi chỗ khác đang dùng hai hàm đó.
 *
 * Giữ chuỗi đang gõ trong state riêng thay vì format lại sau mỗi phím: format
 * ngay trong lúc gõ sẽ đẩy con trỏ về cuối mỗi lần độ dài chuỗi đổi. Chỉ format
 * lại khi ô mất focus, và khi giá trị bên ngoài đổi trong lúc ô không được focus
 * (ví dụ form được prefill từ báo giá).
 */
export const MoneyInput = ({
  value,
  onChange,
  id,
  disabled,
  className,
}: MoneyInputProps) => {
  // formatVnd trả "—" cho null/NaN, hợp cho chỗ hiển thị nhưng không hợp cho ô
  // nhập — người dùng không xoá được một dấu gạch. Ô trống thì để trống.
  const display = (amount: number) => (amount ? formatVnd(amount) : "");

  const [text, setText] = useState(() => display(value));
  const [focused, setFocused] = useState(false);

  useEffect(() => {
    if (focused) return;
    setText(display(value));
  }, [value, focused]);

  // parseVnd trả NaN cho chuỗi không đọc được; ô số phải quy về 0, đúng như
  // ghi chú trong numberFormat.ts dặn phía gọi.
  const read = (raw: string): number => {
    const parsed = parseVnd(raw);
    return Number.isFinite(parsed) ? parsed : 0;
  };

  return (
    <Input
      id={id}
      disabled={disabled}
      inputMode="numeric"
      className={cn("text-right tabular-nums", className)}
      value={text}
      onFocus={() => setFocused(true)}
      onChange={(e) => {
        setText(e.target.value);
        onChange(read(e.target.value));
      }}
      onBlur={() => {
        setFocused(false);
        const parsed = read(text);
        setText(display(parsed));
        onChange(parsed);
      }}
    />
  );
};
```

- [ ] **Step 6: Dùng ở ô giá trị hợp đồng**

Trong `Contracts.tsx`, thay `<Input id="c-value" type="number" ... />` (dòng ~870) bằng:

```tsx
                <MoneyInput
                  id="c-value"
                  value={form.value}
                  onChange={(next) => setForm((prev) => ({ ...prev, value: next }))}
                />
```

Giữ nguyên dòng xem trước ngay dưới nó, giờ đã dùng `formatVndWithSymbol` từ Step 2.

**Phạm vi dừng ở đây.** Không quét thay mọi `type="number"` trong toàn bộ admin —
ô lọc `valueMin`/`valueMax` và ô phần trăm mốc thanh toán giữ nguyên. Mở rộng thì
làm sau khi ô này đã chạy ổn trên một màn hình thật.

- [ ] **Step 7: Lint, build, kiểm tra bằng tay**

```bash
cd nihomeweb && npm run lint && npm run build
```

Kiểm tra bằng tay: mở form tạo hợp đồng, gõ `1500000000` vào ô giá trị — số hiện
phân tách hàng nghìn, con trỏ không nhảy về cuối giữa chừng. Click ra ngoài rồi
click lại, sửa một chữ số ở giữa — con trỏ ở nguyên chỗ đó. Xoá sạch ô — ô trống,
không hiện `—`. Lưu và mở lại, giá trị đúng.

Kiểm tra thêm phần gom: mở chi tiết một hợp đồng có mốc thanh toán, đối chiếu cách
hiển thị số tiền với trang Báo giá — hai nơi giờ phải giống hệt nhau.

- [ ] **Step 8: Commit**

```bash
git add nihomeweb/src/components/ui/money-input.tsx \
        nihomeweb/src/pages/admin/Contracts.tsx \
        nihomeweb/src/pages/admin/ContractDetail.tsx
git commit -m "Route contract amounts through the shared formatter

Both contract screens carried their own formatCurrency while the
rest of admin already used lib/numberFormat, making three copies of
one idea. They now use formatVnd and formatVndWithSymbol.

Two behaviour changes come with that: contract amounts always
format as vi-VN rather than following the interface language, and
empty values render as an em dash. Both match what quotes,
opportunities and customers have been doing all along.

The money input builds on the same helpers and keeps the
in-progress string in its own state, since reformatting on every
keystroke pushes the caret to the end whenever the length changes."
```

---

## Task 7: Thanh kéo phần trăm, giật khi đổi trạng thái, timeline (A8b)

**Files:**
- Modify: `nihomeweb/src/pages/admin/Opportunities.tsx:878-890` (thanh kéo phần trăm)
- Modify: `nihomeweb/src/pages/admin/Customers.tsx:258-262` (giật khi mở lại) và phần timeline
- Modify: `nihomeweb/src/pages/admin/Leads.tsx` (timeline)
- Modify: `nihomeweb/src/pages/admin/Contracts.tsx:852-864` (chặn biên ô ngày, Step 5)

**Interfaces:** không đổi gì công khai.

- [ ] **Step 1: Thay `<input type="range">` bằng `Slider`**

`Opportunities.tsx:880-887` dùng thẻ `<input type="range">` thô với `className="accent-primary"`, trong khi repo đã có `components/ui/slider.tsx`. Thay bằng:

```tsx
                  <Slider
                    min={0}
                    max={100}
                    step={5}
                    value={[createForm.winProbability]}
                    onValueChange={([next]) =>
                      setCreateForm({ ...createForm, winProbability: next })
                    }
                    className="flex-1"
                    aria-label={t("opportunities.field.winProbability")}
                  />
```

Bổ sung `import { Slider } from "@/components/ui/slider";`. Giữ nguyên ô `<Input type="number">` bên cạnh — nó cho phép nhập chính xác khi bước nhảy 5 là quá thô.

Nếu form sửa cơ hội cũng dùng `<input type="range">` riêng, thay luôn cả chỗ đó để hai form không lệch nhau.

- [ ] **Step 2: Xác nhận nguyên nhân giật khi đổi trạng thái khách hàng**

Spec ghi đây là giả thuyết chưa xác nhận, nên **xác nhận trước, sửa sau**. Hai ứng viên:

1. `Customers.tsx:258-260` — `openDetail` gọi `setDetail(null)` trước khi fetch, làm nội dung dialog trắng một nhịp. Nếu đường đổi trạng thái có gọi lại `openDetail`, đó chính là cú giật.
2. Refetch toàn danh sách sau khi lưu, làm cả bảng dựng lại.

Mở DevTools, đổi trạng thái một khách hàng, quan sát: nội dung dialog có trắng một nhịp không, và có bao nhiêu request bắn đi. Ghi lại câu trả lời trước khi sang Step 3.

- [ ] **Step 3: Sửa theo đúng nguyên nhân đã xác nhận**

Nếu là ứng viên 1 — bỏ `setDetail(null)` và giữ nội dung cũ trong lúc tải:

```tsx
  const openDetail = async (id: number, options: { startEditing?: boolean } = {}) => {
    setDetailLoading(true);
    // Giữ nguyên nội dung đang hiện trong lúc tải. setDetail(null) ở đây làm
    // dialog trắng một nhịp mỗi lần làm mới sau khi lưu.
```

Nếu là ứng viên 2 — sau khi lưu, cập nhật đúng hàng trong `contracts`/`customers` tại chỗ thay vì gọi lại `load()`:

```tsx
      setCustomers((prev) => prev.map((c) => (c.id === data.id ? data : c)));
```

Nếu cả hai cùng góp phần thì sửa cả hai. **Đừng sửa cả hai một cách phòng xa nếu chỉ một cái là thủ phạm** — mỗi thay đổi phải trả lời được một quan sát cụ thể.

- [ ] **Step 4: Timeline lịch sử chăm sóc**

Timeline hiện là `<ol className="space-y-2 border-l pl-4">` với một chấm tròn định vị tuyệt đối cho mỗi mục (`Leads.tsx:800-810`, và khối tương ứng trong `Customers.tsx`). Ba chỉnh sửa, không hơn:

1. Nhóm theo ngày: chèn một dòng tiêu đề nhỏ khi ngày đổi so với mục trước, thay vì lặp lại ngày đầy đủ ở mọi dòng.
2. Đổi `{new Date(a.createdAt).toLocaleString()}` sang định dạng gọn cho mục trong ngày hôm nay, giữ đủ ngày cho mục cũ hơn.
3. Cho vùng timeline `max-h-[24rem] overflow-y-auto` để danh sách dài không đẩy nút hành động của dialog ra khỏi màn hình.

**Không** đổi cấu trúc dữ liệu, không thêm filter, không phân trang. Danh sách hoạt động do backend trả trong `LeadResponse.Activities` và `CustomerResponse.Activities` — giữ nguyên.

- [ ] **Step 5: Datepicker — phạm vi giới hạn có chủ đích**

Ngày đang nhập bằng `<Input type="date">` thuần ở hơn mười file. Thay toàn bộ bằng component lịch tuỳ biến **không nằm trong gói A** — đó đúng là kiểu lan phạm vi khiến A8 bị xếp cuối.

Việc trong phạm vi: `<Input type="date">` hiện không có `max`/`min` nên gõ tay ra được năm 0202. Thêm chặn biên cho các ô ngày trong form hợp đồng (`Contracts.tsx:852-864`):

```tsx
                <Input id="c-signed" type="date" min="2000-01-01" max="2099-12-31" ... />
```

Áp cho cả ba ô `signedDate`, `startDate`, `endDate`. Validation quan hệ giữa các ngày đã có sẵn ở `Contracts.tsx:339-347` — không đụng vào.

Nếu sau demo khách vẫn muốn lịch tuỳ biến, mở ticket riêng và làm dựa trên `components/ui/calendar.tsx` đã có trong repo.

- [ ] **Step 6: Lint, build, kiểm tra bằng tay**

```bash
cd nihomeweb && npm run lint && npm run build
```

Kiểm tra bằng tay: kéo thanh phần trăm trong form cơ hội — mượt, ô số bên cạnh cập nhật theo. Đổi trạng thái một khách hàng — không thấy nội dung dialog trắng nhịp nào. Mở một khách hàng có nhiều hoạt động — timeline cuộn trong vùng của nó, nút ở chân dialog vẫn với tới được. Gõ tay `0202` vào ô ngày ký hợp đồng — bị chặn.

- [ ] **Step 7: Commit**

```bash
git add nihomeweb/src/pages/admin/Opportunities.tsx \
        nihomeweb/src/pages/admin/Customers.tsx \
        nihomeweb/src/pages/admin/Leads.tsx \
        nihomeweb/src/pages/admin/Contracts.tsx
git commit -m "Polish the CRM input primitives

The win probability control was a bare range input while the repo
already ships a slider. Long care timelines pushed the dialog
actions off screen, and date fields accepted any year at all.

Date entry stays on the native control: swapping every one of them
for a custom calendar is a separate piece of work."
```

---

## Kiểm tra cuối gói

```bash
# Backend build + lint
docker exec nihome31042025-backend dotnet build
docker exec nihome31042025-backend dotnet format --verify-no-changes

# Unit test
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true

# Integration test
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.integration.tests/nihomebackend.integration.tests.csproj -p:SkipNihomeWebBuild=true

# Frontend
cd nihomeweb && npm run lint && npm run build

# Smoke
docker compose up -d --build
cd nihomeweb && BASE_URL=http://localhost:5043 npx playwright test
```

Kiểm tra bằng tay, gồm cả phần nợ mà plan này trả cho phần 1:

1. Cơ hội đã Lost → không tạo được báo giá, thông báo tiếng Việt rõ ràng.
2. Mở một báo giá đã duyệt, bấm Lưu mà không sửa gì → version giữ nguyên, trạng thái vẫn Đã duyệt.
3. Bấm notification của lead → mở đúng lead.
4. Mở lead đã chuyển đổi từ **phần 1**, bấm "Xem khách hàng" và "Xem cơ hội" → cả hai mở đúng bản ghi.
5. Gõ chuỗi không khớp vào ô search hợp đồng rồi xoá dần → không mất focus.
6. Chạy lại trọn kịch bản demo đầu-cuối ở cuối plan phần 1 → vẫn thông suốt.

Bước 4 và 6 là điều kiện để coi **cả gói A** đã xong, không riêng plan này.

## Việc còn nợ sau gói A

- NIH-446 chưa có cách sửa cho tới khi Task 5 điều tra xong.
- Datepicker tuỳ biến, nếu khách vẫn muốn sau demo.
- Phần còn lại của NIH-439 (gom nhóm field, bỏ trường dư), NIH-440, NIH-441 — thuộc gói B.
- Toàn bộ gói B (nối chuỗi sâu hơn, timeline gộp, Survey link Lead) và gói C (điều hướng module Thiết kế).
