# Google Drive Stays Connected Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A connected Google Drive account stays connected and keeps syncing until a person signs out or switches account; only configuration or credential errors need a person, and everything else recovers by itself.

**Architecture:** One classifier splits Drive errors into Transient, Connection and Permanent. The sync worker counts only Permanent errors against the 3-attempt limit. Switching account no longer disconnects first: the old token is replaced only after the new sign-in succeeds. The status API and settings page say which of the three happened.

**Tech Stack:** ASP.NET Core 8, EF Core 8, Google.Apis.Drive.v3, React 18 + TypeScript, xUnit + Moq.

**Spec:** User requirement, 2026-11-26: "The user wants to use Drive as long as they can. Unless the configuration errors, or credentials failure, others like timeout can be automatically connected to the Drive, unless they want to sign out and switch to another Drive account." Plus Góp ý lập trình v2 N-01 (alert Admin when sync fails).

## What the code does today (verified on `main` 6e66f173)

- The stored refresh token is removed only by a settings save that changes Client ID or Secret, and by Disconnect. Nothing removes it on errors. **Keep this.**
- "Connect" while already connected (shown also when the status is `Unavailable` after a timeout) calls Disconnect, which **revokes the token at Google before the new sign-in succeeds**. Closing the popup leaves Drive disconnected.
- An upload that fails 3 times (backoff 1 min, 5 min) becomes `Failed` for good, whatever the cause, so an outage or timeout of about 11 minutes strands files until someone presses Retry.
- A connection check that fails does not consume attempts. Folder creation, reconciliation and project rename already retry forever.
- The status check reports a wrong Client Secret and a missing root folder as `Unavailable`, the same as a timeout.
- Google rule outside the code ([official page](https://developers.google.com/identity/protocols/oauth2#expiration)): an **External** OAuth app in **Testing** publishing status gets refresh tokens that **expire after 7 days** when Drive scope is requested.

## Global Constraints

- Only Disconnect, or a settings save that changes Client ID/Secret, may remove the stored refresh token.
- Never delete anything on Drive as part of recovery.
- A failed or cancelled account switch leaves the current account connected.
- New or changed UI text: `TranslationSeeder` never updates existing keys, so changed wording needs a **new key**. Every new key in vi, en, zh, ja.
- No new dependency, no database migration.
- Before push: `dotnet build -c Release -p:SkipNihomeWebBuild=true -p:TreatWarningsAsErrors=true`, `dotnet format --no-restore --verify-no-changes`, `dotnet test --no-build -c Release`; web: `npm run format:check`, `npm run lint`, `npm run test:unit`, `npm run check:a11y`, `npx tsc --noEmit -p tsconfig.app.json`, `npx vite build`; `python3 tools/translation_keys/check_translation_keys.py --format markdown --no-warnings --output "$(mktemp -d)/r.md"`.

## Review Focus

1. A request cancelled because the server is shutting down is not a Drive failure and must not count against a file.
2. A local source file that is missing is Permanent; it must not retry forever as "network".
3. A 403 means two different things: rate/storage limit (wait and retry) versus no permission on that file (Permanent).
4. An upload that Google accepted before the timeout must not create a second copy on retry (existing replica-key lookup; keep it).
5. Switching to an account that cannot write the root folder must keep the old account.

---

### Task 1: Classify Drive errors; only file errors use up attempts

**Files:**
- Create: `nihomebackend/Services/GoogleDrive/GoogleDriveFailure.cs`
- Modify: `nihomebackend/Services/GoogleDrive/ProjectDriveSyncService.cs` (`RecordFailureAsync`, `PropagateSurveyFailureAsync`, constructor)
- Test: `nihomebackend.tests/Services/GoogleDriveFailureTests.cs` (new), `nihomebackend.tests/Services/ProjectDriveSyncProcessorTests.cs`

**Interfaces:**
- Produces: `public enum GoogleDriveFailureKind { Transient, Connection, Permanent }` and `public static GoogleDriveFailureKind GoogleDriveFailure.Classify(Exception exception)`.

Classification (walk `InnerException`; first match wins; Connection is checked before Transient):

| Kind | Exceptions |
|---|---|
| Connection | `GoogleDriveReconnectRequiredException`; anything `GoogleDriveAuthenticationErrors.IsInvalidGrant` accepts; `TokenResponseException` with error `invalid_client`, `unauthorized_client`, `admin_policy_enforced` or `invalid_rapt`; `GoogleApiException` 401; `GoogleApiException` 403 with reason `storageQuotaExceeded` |
| Transient | `OperationCanceledException` (a timeout; the worker handles real shutdown before classifying); `HttpRequestException` with no status code, 408, 429 or ≥500; `SocketException`; `IOException` except `FileNotFoundException` and `DirectoryNotFoundException`; `GoogleApiException` 408, 429, ≥500; `GoogleApiException` 403 whose `Error.Errors[].Reason` is `rateLimitExceeded`, `userRateLimitExceeded`, `sharingRateLimitExceeded`, `quotaExceeded` or `dailyLimitExceeded`; `TokenResponseException` with status 429 or ≥500 |
| Permanent | everything else |

Worker rules in `RecordFailureAsync`:
- If `ct.IsCancellationRequested`, rethrow without recording.
- `Permanent`: unchanged (3 attempts, then `Failed`).
- `Transient` or `Connection`: status `Pending`, `SyncAttemptCount = max(attempt - 1, 0)` (not counted), `NextSyncAttemptAt = now + 1 minute` for Transient and `+ 5 minutes` for Connection. Survey media mirrors the same count and next time.
- `SyncError`: Transient → `"Google Drive tạm thời không phản hồi. Hệ thống sẽ tự thử lại."`; Connection → `"Google Drive cần kiểm tra cấu hình hoặc kết nối lại. Tệp sẽ tự đồng bộ sau khi kết nối lại."`; Permanent keeps today's text.
- When a file becomes `Failed` (terminal), notify admins once: `INotificationService.CreateForAdminsAsync("GoogleDrive", $"Đồng bộ Google Drive thất bại: {fileName}", $"{projectCode} · {safeError}", $"/admin/operational-projects/{projectId}")`. A notification error is logged and swallowed. (This is Task 6 of the folder-tree plan, moved here because it changes the same method.)

- [ ] **Step 1: Write failing tests.**
  - `GoogleDriveFailureTests`: one `[Theory]` row per table entry above, plus `FileNotFoundException → Permanent`, `GoogleApiException(404) → Permanent`, `GoogleApiException(403, insufficientFilePermissions) → Permanent`, and an `AggregateException`/wrapped `invalid_grant → Connection`.
  - Processor: `Timeout_does_not_use_up_an_attempt_and_retries_in_a_minute` (upload throws `TaskCanceledException`, 5 passes → still `Pending`, count 0); `Credential_failure_keeps_the_file_queued` (invalid_grant → `Pending`, count 0, next ≈ +5 min); `Permanent_failure_still_fails_after_three_attempts_and_notifies_admins_once`; `Retryable_failure_does_not_notify`; survey media mirrors a non-counted failure.
- [ ] **Step 2:** run the two test files → FAIL.
- [ ] **Step 3:** implement; update every `new ProjectDriveSyncProcessor(...)` in tests.
- [ ] **Step 4:** run → PASS.
- [ ] **Step 5:** commit `Keep files queued through Drive timeouts and credential errors`.

---

### Task 2: Switch account without disconnecting first; resume stranded files on reconnect

**Files:**
- Modify: `nihomebackend/Services/GoogleDrive/GoogleDriveOAuthService.cs` (`CompleteAsync`, `DisconnectAsync` revoke helper, `GetStatusAsync`), `nihomebackend/Services/GoogleDrive/GoogleDriveSettingsStore.cs` (`SaveRefreshTokenAsync`), `nihomeweb/src/pages/admin/SettingsCenter.tsx`, `nihomebackend/Data/Seeds/i18n/admin-system.json`
- Test: `nihomebackend.tests/Services/GoogleDriveOAuthServiceTests.cs`, settings store tests where `SaveRefreshTokenAsync` is covered

Decisions:
- `connect()` in `SettingsCenter.tsx` no longer calls `disconnectGoogleDriveOAuth`. When already connected it still asks for confirmation, with new key `settings.drive.switchKeepConfirm`: vi "Đăng nhập tài khoản Google khác? Tài khoản hiện tại vẫn được giữ cho đến khi đăng nhập mới thành công." (+ en, zh, ja). The Disconnect button stays the only sign-out.
- `CompleteAsync`: read the old token and account email before saving. After a successful save, revoke the old token **only when both emails are known and differ** (ignore case). Never revoke for the same account (revoking can cancel the new grant too). Revocation is best effort; reuse the existing revoke code from `DisconnectAsync` as one private method.
- `SaveRefreshTokenAsync`: after the token is saved, set every `ProjectDocument` with `SyncStatus == Failed` back to `Pending` (`SyncAttemptCount 0`, `NextSyncAttemptAt null`, `SyncError null`) in a second save; a concurrency conflict there is logged and does not fail the connect.
- `GetStatusAsync` catch block uses `GoogleDriveFailure.Classify`: Connection → `ReconnectRequired`; `GoogleApiException` 404 → `InvalidRoot` with today's InvalidRoot text; otherwise `Unavailable`. For `invalid_client`/`unauthorized_client` the error text says to check Client ID and Client Secret. Transient text: "Google Drive tạm thời không phản hồi. Hệ thống tự thử lại, không cần kết nối lại."
- UI: show new key `settings.drive.error.unavailableRetrying` for `Unavailable` (same meaning, 4 languages), and while the status is `Unavailable` reload it every 30 seconds (clear the timer on change/unmount).

- [ ] **Step 1: Write failing tests:** `Switching_to_another_account_revokes_the_old_token_only_after_success`; `Reconnecting_the_same_account_does_not_revoke`; `Failed_root_check_keeps_the_current_token_and_revokes_nothing`; `Successful_connect_requeues_failed_documents`; status theory: invalid_grant → `ReconnectRequired`, invalid_client → `ReconnectRequired`, 404 → `InvalidRoot`, `TaskCanceledException` → `Unavailable`.
- [ ] **Step 2:** run → FAIL. **Step 3:** implement. **Step 4:** run → PASS, plus web checks.
- [ ] **Step 5:** commit `Keep the current Drive account until a switch succeeds`.

---

### Task 3: Tell the administrator what only they can fix; document it

**Files:**
- Modify: `nihomeweb/src/pages/admin/SettingsCenter.tsx` (one help line under the connection card), `nihomebackend/Data/Seeds/i18n/admin-system.json`, `docs/user_guide.md` (§3.7.1), `README.md` (Drive section)

- Help line key `settings.drive.help.stayConnected`, vi: "Kết nối được giữ cho đến khi bạn ngắt hoặc đổi tài khoản. Lỗi mạng hay hết thời gian chờ sẽ tự thử lại. Nếu ứng dụng OAuth trên Google Cloud đang ở trạng thái Testing, Google sẽ ngắt sau 7 ngày: hãy chuyển sang In production, hoặc chọn loại Internal nếu dùng Google Workspace." (+ en, zh, ja).
- Docs: the same facts, the three failure kinds and what the admin does for each, and the after-publishing step "reconnect once so Google issues a new token".

- [ ] **Step 1:** edit. **Step 2:** run every command in Global Constraints → all pass.
- [ ] **Step 3:** commit `Explain how the Drive connection stays alive`.

---

## Out of scope

- HTTP-level retry inside a single Drive request (the queue already retries).
- Service-account or domain-wide delegation sign-in (would remove user tokens entirely; needs a Workspace admin decision).
- Changing the Google Cloud publishing status (a person must do it in Google Cloud Console).
