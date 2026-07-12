# Routine Phase Plan — Settle-Up + Budgets (backend)

This file drives an autonomous nightly cloud agent ("the runner"). Each run implements
exactly **one** unchecked phase below and opens a PR. Humans: review/merge the PR each
morning; edit specs here any time — the runner always reads this file fresh from `main`.

The frontend counterpart lives in `rnavdeep/expense-analyser` → `docs/ROUTINES_PLAN.md`
(phases F1–F6). The two are decoupled by the fixed API contracts below — implement the
contracts **exactly** (camelCase JSON, field names verbatim), because the frontend is built
against them without waiting for these phases to merge.

## Runner protocol

1. Check for open PRs whose branch starts with `routine/`. If any routine PR is **open and
   unmerged: exit immediately, change nothing.** Merging is the human throttle.
2. Pick the **first unchecked phase** in the checklist below. If all are checked, exit without changes.
3. Create branch `routine/phase-<id>` off `main` (e.g. `routine/phase-b1`).
4. Implement the phase exactly as specified. Imitate the named pattern files closely — match
   their style (repository interfaces + implementations, DTO/AutoMapper conventions, SQL
   script style, test structure). Do not refactor unrelated code, do not implement more than
   one phase.
5. Quality gates:
   - `dotnet build` must pass with no new warnings-as-errors.
   - `dotnet test Tests/Expense.API.IntegrationTests` — **always attempt this**, it's not
     optional-by-default. The suite spins up SQL Server + Redis via Testcontainers and needs
     a Docker daemon plus the ability to pull `mcr.microsoft.com/mssql/server`,
     `redis`, and `testcontainers/ryuk` images. Try: is a daemon reachable (`docker info`),
     and can those images actually be pulled (`docker pull testcontainers/ryuk:0.6.0`)? Only
     if that pull is genuinely blocked (sandbox network policy denies the registry) skip
     running the suite — and say so explicitly in the PR body, including the exact error.
     Otherwise run the full suite (not just the new phase's test file) before opening the PR
     and paste the pass/fail summary into the PR body. GitHub Actions
     (`.github/workflows/integration-tests.yml`) runs the full suite on every push/PR
     regardless and is the gate of record — but a runner that could have caught a failing
     test locally and didn't is a bug in the run, not just bad luck.
   - New/changed behaviour **must** have integration tests, written in the same run as the
     code they cover — not a follow-up. Follow the date/time conventions already used in
     `DashboardTests.cs` and friends: seed "current period" fixtures with `DateTime.UtcNow`
     captured once per test (not a hand-picked day-of-month like the 15th), since a fixture
     dated later in the current month than the day the suite actually runs on silently drops
     out of any `CreatedAt <= now` window and fails only on some days. Seed "prior period"
     fixtures via `.AddMonths(-1)`/`.AddDays(-N)` off that same `now`, not a second hand-picked
     constant.
6. In the same branch, tick this phase's checkbox below (`- [ ]` → `- [x]`).
7. Commit (conventional commits, e.g. `feat(settlement): add model, repository and endpoints (phase B1)`),
   push, and open a PR titled `routine(B<id>): <phase title>`. PR body: what was built, how it
   was tested, any deviations from the spec and why. End the body with:
   `🤖 Generated with [Claude Code](https://claude.com/claude-code)`

## API contracts (fixed — must match the frontend plan verbatim)

All wire shapes camelCase JSON. All endpoints `[Authorize]`, current user resolved the same
way existing controllers do (JWT cookie → `IHttpContextAccessor`). Amounts: dollars.

```
POST /api/Settlement            body { payeeUserId: string(Guid), amount: number, note?: string }
  → 201 SettlementDto { id, payerUserId, payerUserName, payeeUserId, payeeUserName,
                        amount, note: string|null, createdAt: string /* ISO 8601 UTC */ }
     400 when amount <= 0, payee not found, or payee == caller.

GET  /api/Settlement?pageNumber=&pageSize=
  → SettlementDto[]  — settlements where the caller is payer OR payee, newest first.
     404 when the caller has none (matches existing GetExpenses behaviour).

GET  /api/Expense/balances       (existing endpoint — after B2 the amounts are NET of settlements)
  → OutstandingBalancesDto (existing DTO, unchanged shape)

GET  /api/Expense/balances/{userId}
  → BalanceDetailDto { userId, userName, netAmount: number,
                       direction: "youOwe" | "owedToYou" | "settled",
                       entries: BalanceDetailEntryDto[] /* chronological, oldest first */ }
     BalanceDetailEntryDto { type: "expense" | "settlement", id, description, amount,
                             direction: "youOwe" | "owedToYou", createdAt }
     404 when {userId} is not an existing user.

PUT  /api/Budget                body { category: string, monthlyLimit: number }   (upsert per caller+category)
  → 200 BudgetDto { id, category, monthlyLimit }    400 when monthlyLimit <= 0 or category blank.

GET  /api/Budget?period=month
  → BudgetStatusDto[] { category, monthlyLimit, spent }
     `spent` = caller's current-calendar-month expense total in that category (same
     category semantics as GetDashboardSummaryAsync: Category null → "Other").
     404 when the caller has no budgets.

GET  /api/Friends/getFriends        (existing endpoint; gains `userId` in phase B6)
  → FriendsListDto[] { userId: string(Guid), username, date: string, sharedExpenses: ExpenseDto[] }
     `userId` = the *other* party's id (mirrors how `username` is already resolved) — needed by
     the frontend to link each friend row to `/balances/{userId}` (frontend phase F7).

POST /api/Expense/{id}/addUser?userId=   (existing endpoint; after B7 friend-gated)
     400 "Users must be friends before sharing an expense." when there is no accepted
     FriendRequest row between the caller and {userId} (same predicate as
     FriendRequestRepository.GetFriends/GetDropdownUsers: either direction, IsAccepted == 1).
     Existing checks (user/expense exist, not already assigned) are unchanged and still apply.
```

## Codebase orientation (read these before coding)

- Domain models: `Models/Domain/...` (see `Models/Domain/FriendRequest/FriendRequest.cs`,
  `Models/Domain/Notifications/Notification.cs`); DbSets in `Data/UserDocumentsDbContext.cs`.
- DTOs: `Models/DTO/*.cs`; AutoMapper maps in `Mappings/AutomapperProfiles.cs`.
- Repository pattern: interface + implementation per folder (exemplar:
  `Repositories/FriendRequest/`); DI registration in `Configurations/RepositoryConfig.cs`.
- Aggregations exemplar: `Repositories/Expense/ExpenseRepository.cs` —
  `GetDashboardSummaryAsync` (line ~357) and `GetOutstandingBalancesAsync` (line ~439).
- Notifications + SignalR: `ITextractNotification.CreateNotifcation(userId, message, title, isFriendRequest)`
  (note the existing misspelling) plus `IHubContext<TextractNotificationHub>` →
  `.Clients.User(userName).SendAsync("TextractNotification", message)`. Exemplar call site:
  `Repositories/FriendRequest/FriendRequestRepository.cs` (~lines 58–72).
- SQL scripts: `ExpenseAnalyserDbScripts/` — numbered, idempotent (`IF NOT EXISTS` guards, see
  `09-add-expense-category.sql`). Also add an EF migration (`Migrations/`) for each schema change.
- Tests: `Tests/Expense.API.IntegrationTests` — xUnit + FluentAssertions;
  `IntegrationTestBase` gives `RegisterAndLoginAsync`, `CreateBusinessUserAsync`, `WithDbAsync`,
  `Unique`; deterministic seeding via `Infrastructure/SeedData.cs` (`AddExpense`, `AddShare`,
  `AddReceipt`). Exemplar suite: `DashboardTests.cs`.

## Phase checklist

- [x] **B1 — Settlement model, repository, endpoints**
- [x] **B2 — Net balances + per-counterparty balance detail**
- [x] **B3 — Settlement notification**
- [x] **B4 — Budget model, repository, endpoints**
- [x] **B5 — Budget threshold alerts**
- [x] **B6 — Expose `userId` on `GET /api/Friends/getFriends`**
- [ ] **B7 — Require friendship before adding a user to an expense split**

---

### B1 — Settlement model, repository, endpoints

- `Models/Domain/Settlement/Settlement.cs`: `Id (Guid)`, `PayerId (Guid)` + `Payer` nav,
  `PayeeId (Guid)` + `Payee` nav, `Amount (decimal)`, `Note (string?)`,
  `CreatedAt (DateTime, default UtcNow)`. Add `DbSet<Settlement> Settlements` to
  `UserDocumentsDbContext` and configure the two `User` relationships with
  `DeleteBehavior.Restrict` (the test host applies `RestrictCascadeModelCustomizer`, but be
  explicit like other multi-user entities).
- SQL script `ExpenseAnalyserDbScripts/10-settlements.sql` (idempotent, matching the style of
  `05-expense_users.sql`/`09-add-expense-category.sql`) **and** an EF migration.
- `Models/DTO/SettlementDto.cs` + `Models/DTO/CreateSettlementDto.cs` per the contract; map in
  `Mappings/AutomapperProfiles.cs` (user names resolved from the navs).
- `Repositories/Settlement/ISettlementRepository.cs` + `SettlementRepository.cs`:
  `CreateAsync(CreateSettlementDto)` (resolve caller as payer the way FriendRequestRepository
  resolves the current user; validate amount > 0, payee exists, payee != caller) and
  `GetForUserAsync(pageNumber, pageSize)`. Register in `Configurations/RepositoryConfig.cs`.
- `Controllers/SettlementController.cs`: `[Route("api/[controller]")]`, `[Authorize]` —
  `POST /` → 201 with dto; `GET /?pageNumber=&pageSize=` → list or 404 when empty (mirror
  `ExpenseController` conventions and `ValidateModelAtrribute` usage).
- Tests `Tests/Expense.API.IntegrationTests/SettlementTests.cs`: create → 201 + persisted row;
  validation failures → 400; paged list newest-first for both payer and payee; empty → 404.

### B2 — Net balances + per-counterparty balance detail

- Extend `ExpenseRepository.GetOutstandingBalancesAsync` so each counterparty amount is
  **net of settlements**: amount owed from shares minus settlements paid in that direction;
  a counterparty nets across both directions, lands in whichever list (`youOwe`/`owedToYou`)
  the net sign indicates, and is omitted when the net is 0. `DashboardSummaryDto.YouOwe/OwedToYou`
  totals (in `GetDashboardSummaryAsync`) must use the same netting.
- New `GET /api/Expense/balances/{userId}` (controller + `IExpenseRepository` method
  `GetBalanceDetailAsync(Guid counterpartyId)`) returning `BalanceDetailDto` per the contract:
  chronological entries combining the caller↔counterparty expense shares (`type: "expense"`,
  description = expense title, direction relative to the caller) and settlements
  (`type: "settlement"`, description = note or "Settlement"). 404 for unknown user.
- New DTOs `Models/DTO/BalanceDetailDto.cs` (+ entry DTO).
- Tests: extend `DashboardTests.cs` scenarios with settlements asserting exact net numbers
  (partial settle, exact settle → counterparty disappears, over-settle flips direction), and
  add `BalanceDetailTests` covering entry ordering, directions, and 404.

### B3 — Settlement notification

- In `SettlementRepository.CreateAsync`, after saving: create a notification for the **payee**
  via `ITextractNotification.CreateNotifcation(payeeId, message, title, 0)` with title
  `"Settlement received"` and message `"<payerUserName> settled $<amount> with you"`, then
  broadcast `await hub.Clients.User(<payeeUserName>).SendAsync("TextractNotification", message)` —
  copy the exemplar in `FriendRequestRepository` (~lines 58–72), including its error-tolerant
  structure (a notification failure must not fail the settlement).
- Tests: extend `SettlementTests.cs` — after a successful POST, the payee has an unread
  notification with the expected title/message (assert via `WithDbAsync` or the payee's
  `/api/Notification` endpoints, mirroring `FriendsNotificationTests.cs`).

### B4 — Budget model, repository, endpoints

- `Models/Domain/Budget/Budget.cs`: `Id`, `UserId` + `User` nav, `Category (string, 64)`,
  `MonthlyLimit (decimal)`, `UpdatedAt`. Unique index on `(UserId, Category)`. `DbSet<Budget> Budgets`;
  SQL script `ExpenseAnalyserDbScripts/11-budgets.sql` + EF migration.
- DTOs `BudgetDto`, `UpsertBudgetDto`, `BudgetStatusDto` per contract + AutoMapper maps.
- `Repositories/Budget/IBudgetRepository.cs` + `BudgetRepository.cs`:
  `UpsertAsync(UpsertBudgetDto)` (insert-or-update by caller+category, case-insensitive
  category match, validate limit > 0 and category non-blank) and `GetStatusAsync(string period)`
  — for `period=month`, join each budget with the caller's current-calendar-month expense
  total in that category, reusing the category semantics of `GetDashboardSummaryAsync`
  (null Category → "Other"). Register in `RepositoryConfig`.
- `Controllers/BudgetController.cs`: `PUT /api/Budget` → 200 dto (400 invalid);
  `GET /api/Budget?period=month` → list or 404 when the caller has no budgets.
- Tests `BudgetTests.cs`: upsert creates then updates the same row; validation 400s;
  status math (seed categorised + uncategorised expenses via `SeedData.AddExpense`, assert
  exact `spent` incl. the "Other" bucket); prior-month expenses excluded; empty → 404.

### B5 — Budget threshold alerts

- After an expense is created or updated (hook into the repository methods behind
  `POST /api/Expense` and `PUT /api/Expense/{id}` in `ExpenseRepository`), if the expense's
  category has a budget for the current month: compute utilization before/after the change and
  when it **crosses** 80% or 100%, create a notification for the expense owner
  (title `"Budget alert"`, message e.g. `"You've used 82% of your $300 Groceries budget this month"`)
  and broadcast via the hub — same mechanics as B3. Crossing-only semantics = max one alert
  per budget/threshold/month without needing a new table; a notification failure must not
  fail the expense write.
- Tests `BudgetAlertTests.cs`: expense pushing a category from 70%→85% yields exactly one
  80% alert; a further push to 105% yields exactly one 100% alert; staying below 80% or moving
  within 80–99% yields none; uncategorised expenses count against an "Other" budget if one exists.

### B6 — Expose `userId` on `GET /api/Friends/getFriends`

Context: the frontend Friends page (phase F7 in the frontend plan) wants to link each friend
row to `/balances/{userId}` — the balance-detail view from B2 — so a fully settled friend (who
no longer appears in the dashboard's balances panel) is still reachable. That needs the
counterparty's id, which `FriendsListDto` currently doesn't carry.

- `Models/DTO/FriendsListDto.cs`: add `public Guid UserId { get; set; }`.
- `Repositories/FriendRequest/FriendRequestRepository.cs`, `GetFriends()` (~line 146): when
  projecting each `FriendsListDto`, set `UserId = friend.SentByUserId == user.Id ? friend.SentToUserId : friend.SentByUserId`
  — same ternary already used one line below for `Username`, just resolving the id instead of
  the nav's `Username`.
- Tests: extend `FriendsNotificationTests.cs` (or add a `FriendsTests.cs` if no plain CRUD
  suite exists yet) asserting `GetFriends()` / `GET /api/Friends/getFriends` returns the
  correct counterparty id for both request directions (caller as `SentBy` and as `SentTo`).

Acceptance: quality gates pass; `Username`/`Date`/`SharedExpenses` behaviour unchanged.

### B7 — Require friendship before adding a user to an expense split

Context: `ExpenseRepository.CreateExpenseUserAsync` currently has zero relationship checks —
any authenticated caller can add any `userId` to any expense's split, whether or not they're
friends. The frontend picker (`AssignUsers.vue`) already only offers accepted friends, but
nothing stops a direct API call from bypassing that. This phase closes it server-side.
(Out of scope: this phase does **not** add an ownership/authorization check that the caller is
the expense's creator — that's a separate, pre-existing gap this plan isn't addressing here.)

- `Repositories/Expense/ExpenseRepository.cs`, `CreateExpenseUserAsync(ExpenseUser expenseUser)`:
  before the existing "already assigned" check, resolve the caller the same way
  `GetCurrentUserAsync`-style lookups do elsewhere in this file (`ClaimTypes.NameIdentifier` →
  `Users` lookup), then verify an accepted `FriendRequest` row exists between the caller and
  `expenseUser.UserId` — same predicate as `FriendRequestRepository.GetFriends`/
  `GetDropdownUsers` (either direction, `IsAccepted == 1`). If none exists, throw
  `new Exception("Users must be friends before sharing an expense.")` (the controller already
  maps repository exceptions to 400, per `ExpenseController.PostUserToExpense`). Skip the check
  when `expenseUser.UserId` equals the caller's own id (adding yourself, if that ever occurs,
  isn't a friendship question).
- Tests: extend `Tests/Expense.API.IntegrationTests` (add `AssignUsersTests.cs` if no existing
  suite covers `addUser`, otherwise extend the closest one) — adding a non-friend → 400 with the
  exact message; adding an accepted friend → 200 and the existing share-recalculation behaviour
  is unchanged; adding a pending (not-yet-accepted) friend request → still 400.

Acceptance: quality gates pass; no change to `CreateExpenseAsync`, `Put`, or any other endpoint.

---

When every box is checked, the runner has nothing left to do — a human should disable the
routine at https://claude.ai/code/routines (or extend this file with new phases).
