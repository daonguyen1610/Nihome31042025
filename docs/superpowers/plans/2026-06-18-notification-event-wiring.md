# Notification Event Wiring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire in-app notifications to business events — project create, news create, and user account created/toggled — so the right audience sees relevant alerts in the notification bell without any new UI or model changes.

**Architecture:** The `INotificationService` / `Notification` model / notification bell / Redux slice are fully built. This plan only adds call sites. For admin-created content (projects, news), inject `INotificationService` directly into the controller alongside the existing `IAuditLogger` and call `CreateForAdminsAsync` after success. For user-facing events (account created/toggled), inject `INotificationService` into `UserService` and call `CreateAsync` targeting the affected user. All calls are fire-and-forget (best-effort) wrapped in try/catch, matching the existing pattern in `ContactMessageService` and `JobApplicationService`.

**Tech Stack:** C# / ASP.NET Core (controllers + services), xUnit + Moq (tests), TypeScript / React (icon mapping in `notificationUtils.ts`)

---

## Context — What Already Works

| Event | Notifies | Status |
|---|---|---|
| Contact form submitted (public) | All admins | ✅ Done — `ContactMessageService` |
| Job application submitted (public) | All admins | ✅ Done — `JobApplicationService` |
| RBAC role changes | All admins | ✅ Done — `RoleService` |
| Project created (admin) | All admins | ❌ Missing |
| News article created (admin) | All admins | ❌ Missing |
| User account created by admin | That user | ❌ Missing |
| User account activated/deactivated | That user | ❌ Missing |

## File Map

| File | Change |
|---|---|
| `nihomebackend/Controllers/ProjectsController.cs` | Inject `INotificationService`; call `CreateForAdminsAsync` on `Create` |
| `nihomebackend/Controllers/NewsController.cs` | Inject `INotificationService`; call `CreateForAdminsAsync` on `Create` |
| `nihomebackend/Services/UserService.cs` | Inject `INotificationService`; call `CreateAsync` in `CreateAsync` and `ToggleActiveAsync` |
| `nihomebackend.tests/Helpers/NoOpNotificationService.cs` | New — no-op `INotificationService` for controller unit tests |
| `nihomebackend.tests/Controllers/ProjectsControllerTests.cs` | Pass `NoOpNotificationService` in constructor; add notification assertion test |
| `nihomebackend.tests/Controllers/NewsControllerTests.cs` | Pass `NoOpNotificationService` in constructor; add notification assertion test |
| `nihomebackend.tests/Controllers/UsersControllerTests.cs` | Pass `NoOpNotificationService` in constructor; add notification assertion test |
| `nihomeweb/src/components/layout/notificationUtils.ts` | Add icons for `"Project"`, `"News"`, `"User"` modules |

---

## Task 1: Add `NoOpNotificationService` test helper

**Files:**
- Create: `nihomebackend.tests/Helpers/NoOpNotificationService.cs`

This mirrors the existing `NoOpAuditLogger`. It lets controller tests compile after the new `INotificationService` constructor parameter is added without needing a mock setup.

- [ ] **Step 1.1 — Create the no-op helper**

```csharp
// nihomebackend.tests/Helpers/NoOpNotificationService.cs
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;

namespace nihomebackend.tests.Helpers;

/// <summary>No-op INotificationService for controller unit tests.</summary>
public sealed class NoOpNotificationService : INotificationService
{
    public Task<NotificationResponse> CreateAsync(
        int userId, string module, string title, string? body = null, string? linkUrl = null)
        => Task.FromResult(new NotificationResponse());

    public Task<int> CreateForAdminsAsync(
        string module, string title, string? body = null, string? linkUrl = null)
        => Task.FromResult(0);

    public Task<List<NotificationResponse>> GetForUserAsync(int userId, int skip = 0, int take = 20)
        => Task.FromResult(new List<NotificationResponse>());

    public Task<int> GetUnreadCountAsync(int userId) => Task.FromResult(0);

    public Task<NotificationResponse?> MarkReadAsync(long notificationId, int userId)
        => Task.FromResult<NotificationResponse?>(null);

    public Task<int> MarkAllReadAsync(int userId) => Task.FromResult(0);

    public Task<bool> DeleteAsync(long notificationId, int userId) => Task.FromResult(false);
}
```

- [ ] **Step 1.2 — Run existing tests to confirm baseline passes**

```bash
cd /path/to/repo
dotnet test nihomebackend.tests --no-build -v q
```

Expected: all tests pass (no changes yet that could break things).

- [ ] **Step 1.3 — Commit**

```bash
git add nihomebackend.tests/Helpers/NoOpNotificationService.cs
git commit -m "test: add NoOpNotificationService helper for unit tests"
```

---

## Task 2: Wire notification on project create

**Files:**
- Modify: `nihomebackend/Controllers/ProjectsController.cs`
- Modify: `nihomebackend.tests/Controllers/ProjectsControllerTests.cs`

**Pattern to follow:** same structure as `IAuditLogger` injection already in `ProjectsController`.

- [ ] **Step 2.1 — Write failing test first**

Open `nihomebackend.tests/Controllers/ProjectsControllerTests.cs`.

Add a `_notificationSvc` field and update the constructor so it compiles after the controller changes, then add the assertion test:

```csharp
// At top of file add:
using NihomeBackend.Services;

// Replace the class fields and constructor:
private readonly AppDbContext _db;
private readonly ProjectsController _sut;
private readonly AppDbContext _notificationDb; // separate DbContext to verify side effects

public ProjectsControllerTests()
{
    _db = DbContextFactory.Create();

    var hostedImageService = new HostedImageService(
        Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == "/tmp"));
    var categoryService = new ProjectCategoryService(_db);
    var service = new ProjectService(_db, hostedImageService, categoryService);
    _notificationDb = _db; // same instance; tests run in-memory
    _sut = new ProjectsController(service, new NoOpAuditLogger(), new NoOpNotificationService());
}
```

Add this new test at the end of the class:

```csharp
[Fact]
public async Task Create_SendsAdminNotification()
{
    // Arrange — seed one admin so CreateForAdminsAsync has someone to notify
    _db.Users.Add(new ApplicationUser
    {
        PhoneNumber = "0900000001",
        PasswordHash = "hash",
        Role = UserRole.ADMIN,
        IsActive = true,
    });
    await _db.SaveChangesAsync();

    // Use real NotificationService so we can assert the DB row
    var notificationSvc = new NotificationService(_db);
    var hostedImageService = new HostedImageService(
        Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == "/tmp"));
    var categoryService = new ProjectCategoryService(_db);
    var projectSvc = new ProjectService(_db, hostedImageService, categoryService);
    var sut = new ProjectsController(projectSvc, new NoOpAuditLogger(), notificationSvc);

    var req = new UpsertProjectRequest
    {
        Slug = "test-project",
        Name = "Test Project",
        ImageUrl = "",
        Status = "active",
        Year = 2026,
        SortOrder = 0,
    };

    // Act
    await sut.Create(req);

    // Assert — one notification row created for the admin
    Assert.Equal(1, _db.Notifications.Count());
    Assert.Equal("Project", _db.Notifications.Single().Module);
}
```

- [ ] **Step 2.2 — Run new test to confirm it FAILS**

```bash
dotnet test nihomebackend.tests --filter "ProjectsControllerTests.Create_SendsAdminNotification" -v q
```

Expected: compile error — `ProjectsController` constructor does not accept third argument yet.

- [ ] **Step 2.3 — Update `ProjectsController` to accept and use `INotificationService`**

```csharp
// nihomebackend/Controllers/ProjectsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Authorize]
[RequirePermission("content.projects", "view")]
[Route("api/projects")]
[Route("api/v1/projects")]
public class ProjectsController(
    ProjectService svc,
    IAuditLogger audit,
    INotificationService notifications) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll() => Ok(await svc.GetAllAsync());

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var item = await svc.GetBySlugAsync(slug);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [RequirePermission("content.projects", "manage")]
    public async Task<IActionResult> Create([FromBody] UpsertProjectRequest req)
    {
        var result = await svc.CreateAsync(req);
        audit.Log(new AuditEvent
        {
            Action = "project.create",
            ResourceType = "Project",
            ResourceId = result.Id.ToString(),
            Message = $"Created project '{result.Name}'",
            NewValue = result,
        });
        _ = notifications.CreateForAdminsAsync(
            "Project",
            $"Dự án mới được tạo: {result.Name}",
            null,
            $"/admin/projects/{result.Slug}");
        return CreatedAtAction(nameof(GetBySlug), new { slug = result.Slug }, result);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("content.projects", "manage")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertProjectRequest req)
    {
        var result = await svc.UpdateAsync(id, req);
        if (result == null)
        {
            audit.Log(new AuditEvent
            {
                Action = "project.update",
                ResourceType = "Project",
                ResourceId = id.ToString(),
                Message = $"Update failed: project {id} not found",
                Status = AuditStatus.Failure,
                FailureReason = "not_found",
            });
            return NotFound();
        }
        audit.Log(new AuditEvent
        {
            Action = "project.update",
            ResourceType = "Project",
            ResourceId = id.ToString(),
            Message = $"Updated project '{result.Name}'",
            NewValue = result,
        });
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("content.projects", "manage")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await svc.DeleteAsync(id);
        if (!ok)
        {
            audit.Log(new AuditEvent
            {
                Action = "project.delete",
                ResourceType = "Project",
                ResourceId = id.ToString(),
                Message = $"Delete failed: project {id} not found",
                Status = AuditStatus.Failure,
                FailureReason = "not_found",
            });
            return NotFound();
        }
        audit.Log("project.delete", "Project", id.ToString(), $"Deleted project {id}");
        return NoContent();
    }
}
```

Note: `_ = notifications.CreateForAdminsAsync(...)` discards the `Task` intentionally (fire-and-forget). Exceptions from this call are swallowed. This matches the pattern already in `ContactMessageService` (try/catch). If you want explicit error capture, wrap in a try/catch; for MVP the discard is sufficient since the audit log already captures the business action.

- [ ] **Step 2.4 — Update all remaining constructor calls in `ProjectsControllerTests`**

All `ProjectsController` instantiations in `ProjectsControllerTests.cs` must pass the third argument. The default constructor already set `_sut` with `new NoOpNotificationService()` in Step 2.1. Verify no other test in the file directly instantiates `ProjectsController` — fix any that do.

- [ ] **Step 2.5 — Run test to confirm it PASSES**

```bash
dotnet test nihomebackend.tests --filter "ProjectsControllerTests" -v q
```

Expected: all `ProjectsControllerTests` pass including the new one.

- [ ] **Step 2.6 — Build backend**

```bash
dotnet build nihomebackend/nihomebackend.csproj
```

Expected: 0 errors.

- [ ] **Step 2.7 — Commit**

```bash
git add nihomebackend/Controllers/ProjectsController.cs \
        nihomebackend.tests/Controllers/ProjectsControllerTests.cs
git commit -m "feat(notify): send admin notification on project create"
```

---

## Task 3: Wire notification on news create

**Files:**
- Modify: `nihomebackend/Controllers/NewsController.cs`
- Modify: `nihomebackend.tests/Controllers/NewsControllerTests.cs`

Same pattern as Task 2.

- [ ] **Step 3.1 — Write failing test**

Open `nihomebackend.tests/Controllers/NewsControllerTests.cs`.

Update constructor and add test:

```csharp
// Add to using block at top:
using NihomeBackend.Services;

// Update constructor:
public NewsControllerTests()
{
    _db = DbContextFactory.Create();

    var entityTranslationSvc = new EntityTranslationService(_db, Mock.Of<IMemoryCache>());
    var hostedImageService = new HostedImageService(
        Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == "/tmp"));
    var service = new NewsService(_db, entityTranslationSvc, hostedImageService);
    _sut = new NewsController(service, new NoOpAuditLogger(), new NoOpNotificationService());
}
```

Add new test:

```csharp
[Fact]
public async Task Create_SendsAdminNotification()
{
    // Arrange
    _db.Users.Add(new ApplicationUser
    {
        PhoneNumber = "0900000001",
        PasswordHash = "hash",
        Role = UserRole.ADMIN,
        IsActive = true,
    });
    await _db.SaveChangesAsync();

    var notificationSvc = new NotificationService(_db);
    var entityTranslationSvc = new EntityTranslationService(_db, Mock.Of<IMemoryCache>());
    var hostedImageService = new HostedImageService(
        Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == "/tmp"));
    var newsSvc = new NewsService(_db, entityTranslationSvc, hostedImageService);
    var sut = new NewsController(newsSvc, new NoOpAuditLogger(), notificationSvc);

    var req = new UpsertNewsRequest
    {
        Slug = "test-news",
        Title = "Test News Article",
        Date = "2026-06-18",
        ImageUrl = "",
        Category = "General",
        Excerpt = "Test excerpt",
        Content = new[] { "Body" },
        SortOrder = 0,
    };

    // Act
    await sut.Create(req);

    // Assert
    Assert.Equal(1, _db.Notifications.Count());
    Assert.Equal("News", _db.Notifications.Single().Module);
}
```

- [ ] **Step 3.2 — Run new test to confirm FAIL**

```bash
dotnet test nihomebackend.tests --filter "NewsControllerTests.Create_SendsAdminNotification" -v q
```

Expected: compile error — `NewsController` constructor has no third argument.

- [ ] **Step 3.3 — Update `NewsController`**

```csharp
// nihomebackend/Controllers/NewsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Authorize]
[RequirePermission("content.news", "view")]
[Route("api/news")]
[Route("api/v1/news")]
public class NewsController(
    NewsService svc,
    IAuditLogger audit,
    INotificationService notifications) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] string lang = "vi") => Ok(await svc.GetAllAsync(lang));

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(string slug, [FromQuery] string lang = "vi")
    {
        var item = await svc.GetBySlugAsync(slug, lang);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [RequirePermission("content.news", "manage")]
    public async Task<IActionResult> Create([FromBody] UpsertNewsRequest req)
    {
        var result = await svc.CreateAsync(req);
        audit.Log(new AuditEvent
        {
            Action = "news.create",
            ResourceType = "NewsArticle",
            ResourceId = result.Id.ToString(),
            Message = $"Created news '{result.Title}'",
            NewValue = result,
        });
        _ = notifications.CreateForAdminsAsync(
            "News",
            $"Tin tức mới được tạo: {result.Title}",
            null,
            $"/admin/posts/{result.Slug}");
        return CreatedAtAction(nameof(GetBySlug), new { slug = result.Slug }, result);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("content.news", "manage")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertNewsRequest req)
    {
        var result = await svc.UpdateAsync(id, req);
        if (result == null)
        {
            audit.Log(new AuditEvent
            {
                Action = "news.update",
                ResourceType = "NewsArticle",
                ResourceId = id.ToString(),
                Message = $"Update failed: news {id} not found",
                Status = AuditStatus.Failure,
                FailureReason = "not_found",
            });
            return NotFound();
        }
        audit.Log(new AuditEvent
        {
            Action = "news.update",
            ResourceType = "NewsArticle",
            ResourceId = id.ToString(),
            Message = $"Updated news '{result.Title}'",
            NewValue = result,
        });
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("content.news", "manage")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await svc.DeleteAsync(id);
        if (!ok)
        {
            audit.Log(new AuditEvent
            {
                Action = "news.delete",
                ResourceType = "NewsArticle",
                ResourceId = id.ToString(),
                Message = $"Delete failed: news {id} not found",
                Status = AuditStatus.Failure,
                FailureReason = "not_found",
            });
            return NotFound();
        }
        audit.Log("news.delete", "NewsArticle", id.ToString(), $"Deleted news {id}");
        return NoContent();
    }
}
```

- [ ] **Step 3.4 — Run all news controller tests**

```bash
dotnet test nihomebackend.tests --filter "NewsControllerTests" -v q
```

Expected: all pass.

- [ ] **Step 3.5 — Build backend**

```bash
dotnet build nihomebackend/nihomebackend.csproj
```

Expected: 0 errors.

- [ ] **Step 3.6 — Commit**

```bash
git add nihomebackend/Controllers/NewsController.cs \
        nihomebackend.tests/Controllers/NewsControllerTests.cs
git commit -m "feat(notify): send admin notification on news create"
```

---

## Task 4: Wire user account notifications in `UserService`

**Files:**
- Modify: `nihomebackend/Services/UserService.cs`
- Modify: `nihomebackend.tests/Controllers/UsersControllerTests.cs`

This notifies the **specific user** (not all admins) via `CreateAsync(userId, ...)`. Two events: account created and account toggled active/inactive.

Why per-user instead of broadcast: these events are personal — the user is told about their own account, not about someone else's.

- [ ] **Step 4.1 — Write failing test**

Open `nihomebackend.tests/Controllers/UsersControllerTests.cs`.

Add a field for the notification DB and update the constructor:

```csharp
// Add using:
using NihomeBackend.Services;

// Replace class fields and constructor:
private readonly AppDbContext _db;
private readonly UsersController _sut;
private readonly NotificationService _notificationSvc;

public UsersControllerTests()
{
    _db = DbContextFactory.Create();
    _notificationSvc = new NotificationService(_db);
    var service = new UserService(_db, new PasswordService(), _notificationSvc);
    _sut = new UsersController(service)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = BuildUserPrincipal(100),
            },
        },
    };
}
```

Add new tests:

```csharp
[Fact]
public async Task Create_SendsWelcomeNotificationToNewUser()
{
    var result = await _sut.Create(new CreateUserRequest
    {
        PhoneNumber = "0910000099",
        FullName = "New Person",
        Password = "Secret123",
        Role = "USER",
    });

    var created = Assert.IsType<CreatedAtActionResult>(result.Result);
    var user = Assert.IsType<UserDetailResponse>(created.Value);

    Assert.Equal(1, _db.Notifications.Count());
    var notification = _db.Notifications.Single();
    Assert.Equal(user.Id, notification.UserId);
    Assert.Equal("User", notification.Module);
}

[Fact]
public async Task ToggleActive_SendsStatusNotificationToUser()
{
    // Arrange — seed a non-self user
    var target = await SeedUser("0910000088", "Target", UserRole.USER);

    // Act — deactivate
    await _sut.ToggleActive(target.Id);

    Assert.Equal(1, _db.Notifications.Count());
    var notification = _db.Notifications.Single();
    Assert.Equal(target.Id, notification.UserId);
    Assert.Equal("User", notification.Module);
}
```

- [ ] **Step 4.2 — Run new tests to confirm FAIL**

```bash
dotnet test nihomebackend.tests --filter "UsersControllerTests.Create_SendsWelcomeNotificationToNewUser|UsersControllerTests.ToggleActive_SendsStatusNotificationToUser" -v q
```

Expected: compile error — `UserService` constructor does not accept `INotificationService` yet.

- [ ] **Step 4.3 — Update `UserService` to accept and use `INotificationService`**

Add `INotificationService notifications` as third constructor parameter. Add calls in `CreateAsync` and `ToggleActiveAsync`. Wrap in try/catch (best-effort, same pattern as `ContactMessageService`).

The relevant changes (show only the constructor and the two methods that change; the rest of the file is unchanged):

```csharp
// nihomebackend/Services/UserService.cs
// Change the class declaration line:
public class UserService(AppDbContext db, PasswordService passwordService, INotificationService notifications)
{
    // ... all unchanged methods ...

    public async Task<UserDetailResponse> CreateAsync(CreateUserRequest req)
    {
        var phoneNumber = req.PhoneNumber.Trim();
        if (await db.Users.AsNoTracking().AnyAsync(u => u.PhoneNumber == phoneNumber))
        {
            throw new UserServiceException(
                UserServiceError.DuplicatePhoneNumber,
                "Phone number already registered.");
        }

        var user = new ApplicationUser
        {
            PhoneNumber = phoneNumber,
            FullName = req.FullName.Trim(),
            Email = NormalizeOptional(req.Email),
            Role = ParseRole(req.Role),
            IsActive = true,
        };
        user.PasswordHash = passwordService.Hash(user, req.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Notify the newly created user (best-effort)
        try
        {
            await notifications.CreateAsync(
                user.Id,
                "User",
                "Tài khoản của bạn đã được tạo",
                $"Chào mừng {user.FullName ?? user.PhoneNumber}! Tài khoản của bạn đã sẵn sàng.",
                "/admin");
        }
        catch { /* do not fail the create operation */ }

        return MapDetail(user);
    }

    public async Task<UserDetailResponse?> ToggleActiveAsync(int id, int currentUserId)
    {
        var user = await db.Users.Include(u => u.RefreshTokens).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return null;
        }

        var nextIsActive = !user.IsActive;
        await EnsureRoleAndStatusChangeAllowedAsync(user, currentUserId, user.Role, nextIsActive);

        user.IsActive = nextIsActive;
        await db.SaveChangesAsync();

        // Notify the affected user (best-effort)
        try
        {
            var message = nextIsActive
                ? "Tài khoản của bạn đã được kích hoạt trở lại."
                : "Tài khoản của bạn đã bị vô hiệu hóa. Liên hệ quản trị viên nếu cần hỗ trợ.";
            var title = nextIsActive
                ? "Tài khoản đã được kích hoạt"
                : "Tài khoản đã bị vô hiệu hóa";
            await notifications.CreateAsync(user.Id, "User", title, message, null);
        }
        catch { /* do not fail the toggle operation */ }

        return MapDetail(user);
    }

    // ... remaining unchanged methods ...
}
```

- [ ] **Step 4.4 — Fix `UsersControllerTests` constructor — update all `UserService` instantiations**

Any `new UserService(_db, new PasswordService())` calls in `UsersControllerTests.cs` must become `new UserService(_db, new PasswordService(), new NoOpNotificationService())` — except for tests that use `_notificationSvc` directly (the new assertion tests).

- [ ] **Step 4.5 — Run all users controller tests**

```bash
dotnet test nihomebackend.tests --filter "UsersControllerTests" -v q
```

Expected: all pass including the two new tests.

- [ ] **Step 4.6 — Build backend**

```bash
dotnet build nihomebackend/nihomebackend.csproj
```

Expected: 0 errors.

- [ ] **Step 4.7 — Run full test suite**

```bash
dotnet test nihomebackend.tests -v q
```

Expected: all tests pass.

- [ ] **Step 4.8 — Run dotnet format**

```bash
dotnet format nihomebackend.tests/nihomebackend.tests.csproj --verify-no-changes
dotnet format nihomebackend/nihomebackend.csproj --verify-no-changes
```

Fix any formatting issues before committing.

- [ ] **Step 4.9 — Commit**

```bash
git add nihomebackend/Services/UserService.cs \
        nihomebackend.tests/Controllers/UsersControllerTests.cs
git commit -m "feat(notify): notify user on account create and toggle-active"
```

---

## Task 5: Add module icons in frontend

**Files:**
- Modify: `nihomeweb/src/components/layout/notificationUtils.ts`

`moduleIcon()` currently returns `Wrench` as a fallback for any module other than `"JobApplication"` or `"Contact"`. Add explicit matches for `"Project"`, `"News"`, and `"User"` so the notification bell and notifications page show recognizable icons.

- [ ] **Step 5.1 — Update `notificationUtils.ts`**

```typescript
// nihomeweb/src/components/layout/notificationUtils.ts
import { Briefcase, Building2, Mail, Newspaper, User, Wrench } from "lucide-react";
import type { NotificationDto } from "@/services/notificationApi";

export function resolveCurrentLocale() {
  if (typeof document !== "undefined" && document.documentElement.lang) {
    return document.documentElement.lang;
  }

  if (typeof navigator !== "undefined" && navigator.language) {
    return navigator.language;
  }

  return "vi-VN";
}

export function formatRelativeTime(value: string) {
  const date = new Date(value);
  const timestamp = date.getTime();

  if (Number.isNaN(timestamp)) {
    return "";
  }

  const locale = resolveCurrentLocale();
  const diffSeconds = Math.max(0, Math.floor((Date.now() - timestamp) / 1000));
  const relativeTimeFormatter = new Intl.RelativeTimeFormat(locale, { numeric: "auto" });

  if (diffSeconds < 60) return relativeTimeFormatter.format(0, "second");
  const diffMinutes = Math.floor(diffSeconds / 60);
  if (diffMinutes < 60) return relativeTimeFormatter.format(-diffMinutes, "minute");
  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24) return relativeTimeFormatter.format(-diffHours, "hour");
  const diffDays = Math.floor(diffHours / 24);
  if (diffDays < 7) return relativeTimeFormatter.format(-diffDays, "day");

  return new Intl.DateTimeFormat(locale, {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(date);
}

export function moduleIcon(notification: NotificationDto) {
  if (notification.module === "JobApplication") return Briefcase;
  if (notification.module === "Contact") return Mail;
  if (notification.module === "Project") return Building2;
  if (notification.module === "News") return Newspaper;
  if (notification.module === "User") return User;
  return Wrench;
}
```

- [ ] **Step 5.2 — Run frontend lint**

```bash
cd nihomeweb && npm run lint
```

Expected: no new errors. `Building2`, `Newspaper`, `User` are all in `lucide-react` which is already installed.

- [ ] **Step 5.3 — Commit**

```bash
git add nihomeweb/src/components/layout/notificationUtils.ts
git commit -m "feat(notify): add module icons for Project, News, User notifications"
```

---

## Assumptions / Risks

1. **User notifications for non-admin users:** The notification page is at `/admin/notifications` (requires admin access). If a `USER`-role account receives a welcome notification, they won't have a UI to see it unless a public notification page is added later. The notification is still stored in DB and will be visible if the user is later given admin access. Considered acceptable for this phase.

2. **Fire-and-forget vs transactional:** Notification creation failures do not roll back the main operation (project/news/user create). This is consistent with the existing Contact/JobApplication pattern. A failed notification creates no user-facing error but will appear in logs.

3. **Module string values are `string` not `enum`:** The module values (`"Project"`, `"News"`, `"User"`) are plain strings. A future refactor could introduce a `NotificationModule` enum to prevent typos — but that requires a plan of its own and is out of scope here.

4. **`_ = Task` discard for notifications in controllers:** Tasks returned by `notifications.CreateForAdminsAsync(...)` in controllers are intentionally discarded (fire-and-forget). If strict async error handling is required, replace with an explicit `try { await notifications... } catch { /* log */ }` block.

5. **RBAC role-change notifications:** `RoleService` already notifies all admins using i18n key strings (`"rbac.notification.role-updated.title"`) as notification title text. This is inconsistent with other modules which use plain Vietnamese strings. This inconsistency is pre-existing and out of scope for this plan.
