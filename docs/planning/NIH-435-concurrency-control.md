# NIH-435: Implement Concurrency Control for CRM Entities

**Type:** Technical Debt / Enhancement  
**Priority:** 🔴 HIGH  
**Sprint:** GĐ2 - Auth CRM Design  
**Epic:** M1 - CRM  
**Estimate:** 5 story points  

---

## 📋 Summary

Hiện tại hầu hết các CRM entities (Quote, Contract, Opportunity, Lead, Customer) không có optimistic concurrency control. Khi nhiều user cùng sửa 1 record, sẽ xảy ra **lost update** - user sau ghi đè toàn bộ thay đổi của user trước mà không có cảnh báo.

---

## 🎯 Acceptance Criteria

### AC1: RowVersion cho các entity CRM quan trọng
```gherkin
Given entity Quote/Contract/Opportunity/Lead/Customer
When migration được apply
Then mỗi entity có column RowVersion (SQL Server rowversion/timestamp)
And EF Core config IsRowVersion() cho mỗi property
```

### AC2: Service handle DbUpdateConcurrencyException
```gherkin
Given User A và User B cùng mở Quote #123
And User A edit giá trị, submit trước
When User B submit sau với version cũ
Then API return 409 Conflict
And response body chứa message "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại."
```

### AC3: Frontend hiển thị conflict dialog
```gherkin
Given User B nhận 409 Conflict từ API
When response được xử lý
Then hiển thị Toast/Dialog thông báo conflict
And có nút "Tải lại" để refresh dữ liệu mới nhất
```

### AC4: ETag header (Optional - Phase 2)
```gherkin
Given GET /api/quotes/123
When response trả về
Then header ETag chứa Base64(RowVersion)
And client có thể gửi If-Match header để validate trước update
```

---

## 🔍 Current State Analysis

| Entity | RowVersion | Concurrency Handling | Priority |
|--------|------------|---------------------|----------|
| HandoverRecord | ✅ Yes | ✅ Yes | - |
| Quote | ❌ No | ❌ No | 🔴 HIGH |
| Contract | ❌ No | ❌ No | 🔴 HIGH |
| Opportunity | ❌ No | ❌ No | 🟡 MEDIUM |
| Lead | ❌ No | ❌ No | 🟡 MEDIUM |
| Customer | ❌ No | ❌ No | 🟡 MEDIUM |
| AcceptanceRecord | ❌ No | ❌ No | 🟡 MEDIUM |
| SiteDiary | ❌ No | ❌ No | 🟡 MEDIUM |
| DesignProject | ❌ No | ❌ No | 🟡 MEDIUM |

---

## 📝 Tasks Breakdown

### Task 1: Add RowVersion to CRM Models (BE) ⏱️ 1h
- [ ] Add `byte[] RowVersion` property to: Quote, Contract, Opportunity, Lead, Customer
- [ ] Configure `IsRowVersion()` in AppDbContext.cs for each entity
- [ ] Generate migration: `AddCrmConcurrencyTokens`

### Task 2: Update Services with Concurrency Handling (BE) ⏱️ 3h
- [ ] Create helper method `SaveWithConcurrencyAsync()` (reuse pattern from HandoverRecordService)
- [ ] QuoteService: Update/ChangeStatus wrap with concurrency handling
- [ ] ContractService: Update/Sign wrap with concurrency handling  
- [ ] OpportunityService: Update/ChangeStage wrap with concurrency handling
- [ ] LeadService: Update/Convert/Revert wrap with concurrency handling
- [ ] CustomerService: Update wrap with concurrency handling

### Task 3: Create Concurrency Exception Types (BE) ⏱️ 30m
- [ ] Create `CrmConcurrencyException` base class
- [ ] Create specific exceptions: QuoteConcurrencyException, ContractConcurrencyException, etc.
- [ ] Map exceptions to 409 Conflict in controllers

### Task 4: Update Controllers to Return 409 (BE) ⏱️ 1h
- [ ] QuotesController: Catch concurrency exception → 409
- [ ] ContractsController: Catch concurrency exception → 409
- [ ] OpportunitiesController: Catch concurrency exception → 409
- [ ] LeadsController: Catch concurrency exception → 409
- [ ] CustomersController: Catch concurrency exception → 409

### Task 5: Frontend Conflict Handling (FE) ⏱️ 2h
- [ ] Create `useConcurrencyHandler` hook
- [ ] Handle 409 in API interceptor
- [ ] Show conflict toast with "Reload" action
- [ ] Update CRM forms to refresh on conflict

### Task 6: Unit & Integration Tests (QA) ⏱️ 2h
- [ ] Unit test: Service throws exception on concurrent update
- [ ] Integration test: API returns 409 on race condition
- [ ] E2E test: Verify conflict dialog appears

---

## 🏗️ Technical Design

### 1. Model Changes
```csharp
// Quote.cs, Contract.cs, etc.
public byte[] RowVersion { get; set; } = [];
```

### 2. DbContext Configuration
```csharp
modelBuilder.Entity<Quote>(b =>
{
    // ... existing config
    b.Property(q => q.RowVersion).IsRowVersion();
});
```

### 3. Service Pattern (follow HandoverRecordService)
```csharp
private async Task SaveWithConcurrencyAsync(CancellationToken ct)
{
    try
    {
        await db.SaveChangesAsync(ct);
    }
    catch (DbUpdateConcurrencyException ex)
    {
        logger.LogWarning(ex, "Concurrent update rejected");
        throw new QuoteConcurrencyException(
            "Báo giá đã được người khác cập nhật. Vui lòng tải lại trước khi thử lại.");
    }
}
```

### 4. Controller Error Handling
```csharp
catch (QuoteConcurrencyException ex)
{
    return Conflict(new { message = ex.Message });
}
```

### 5. Frontend Hook
```typescript
const useConcurrencyHandler = () => {
  const handleError = (error: AxiosError) => {
    if (error.response?.status === 409) {
      toast.error(error.response.data.message, {
        action: { label: 'Tải lại', onClick: () => window.location.reload() }
      });
      return true;
    }
    return false;
  };
  return { handleError };
};
```

---

## 🔗 Dependencies

- None - standalone improvement

## 📊 Impact

- **Risk Mitigation:** Prevents data loss from concurrent edits
- **User Experience:** Clear feedback when conflicts occur
- **Data Integrity:** Guaranteed consistent state

## 🧪 Test Scenarios

1. **Happy path:** Single user edit → success
2. **Race condition:** Two users edit same record → first succeeds, second gets 409
3. **Reload and retry:** User reloads after 409 → gets latest data → can edit again
4. **Workflow transition:** Quote status change conflicts with parallel edit → 409

---

## 📅 Implementation Order

**Phase 1 (This ticket):**
1. Quote + Contract (highest business impact)
2. Opportunity + Lead + Customer

**Phase 2 (Future ticket):**
1. AcceptanceRecord, SiteDiary, AsBuiltDocument
2. DesignProject, ShopDrawing, BasicDesignDoc
3. HTTP ETag header support

---

## ✅ Definition of Done

- [ ] Migration applied successfully
- [ ] All P1 entities have RowVersion
- [ ] Services throw on concurrent update
- [ ] API returns 409 with clear message
- [ ] Frontend shows conflict toast
- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] Manual test: 2 browser tabs editing same Quote → conflict detected
