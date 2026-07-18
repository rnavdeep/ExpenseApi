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

## Line-item assignment (phases B8–B10 / frontend F8–F10)

Feature: assign each OCR'd receipt line item to one or more friends; the expense's "shared
with" list and per-person owed amounts are derived automatically from those assignments,
replacing the manual add/remove/share-% flow for any expense that has at least one scanned
line item. Design reference: `docs/Line Item User Assignment (standalone).html` in
`rnavdeep/expense-analyser` (a compressed dc-runtime wireframe bundle — the real markup is
JSON-encoded inside a `<script type="__bundler/template">` tag; extract and `json.loads()` it
to view). Confirmed product rules:

- Every line item always has ≥1 assignee (creator counts). Removing an assignee is only
  allowed when ≥2 remain — no separate "creator" special case is needed, the ≥2 check alone
  satisfies "creator only removable once someone else is on the item".
- Split is even per line item only — no custom %/amount per person.
- Only accepted friends are assignable (same predicate as B7).
- Bulk "assign all items to X" is additive only — it never removes anyone already assigned to
  any item.
- Per-person totals = sum of that person's even split across every line item they're on.
  If `expense.Amount` exceeds the sum of line-item amounts (tax/tip/manual top-ups), that
  remainder is left unassigned — do not redistribute it. Shares need not sum to 100% of
  `expense.Amount`.
- When an expense that already has manually-added `ExpenseUser` rows gets its first `LineItem`
  rows (first successful scan), those existing users are carried forward as the default
  assignees on every new line item — not reset to creator-only.
- Expenses with zero `LineItem` rows (nothing ever scanned) are entirely unaffected: the
  existing addUser/removeUser/updateShares flow keeps working exactly as today.

New API contracts (fixed — must match the frontend plan verbatim):

```
GET  /api/Expense/{expenseId}/doc/{docId}   (existing endpoint; response gains `lineItems`)
  → existing DocumentResultDto fields, unchanged, plus:
     lineItems: LineItemDto[]
     LineItemDto { id: string(Guid), description: string|null, quantity: string|null,
                   amount: number|null, sortOrder: number, assignees: LineItemAssigneeDto[] }
     LineItemAssigneeDto { userId: string(Guid), userName: string }

PUT  /api/Expense/lineItem/{lineItemId}/assignees/{userId}
  → 200 LineItemDto (the item, with its updated assignees)
     400 when {userId} != caller and has no accepted FriendRequest with the caller (same
         predicate as B7); 400 when {lineItemId} not found.

DELETE /api/Expense/lineItem/{lineItemId}/assignees/{userId}
  → 200 LineItemDto
     400 "Cannot remove the last remaining assignee from a line item." when this would leave
         zero assignees; 400 when the assignment doesn't exist.

PUT  /api/Expense/{expenseId}/lineItems/assignAll/{userId}
  → 200 LineItemDto[]  (every line item on the expense; {userId} added to any it wasn't
       already on — existing assignees on every item are left untouched)
     400 when {userId} has no accepted FriendRequest with the caller.

GET  /api/Expense/{id}/getAssignedUsers   (existing endpoint; ExpenseUserDto gains two fields)
  → ExpenseUserDto[] { ...existing fields, itemsAssignedCount: int?, totalItemsCount: int? }
     Both null when the expense has zero LineItem rows; otherwise itemsAssignedCount = how many
     of the expense's line items this user is on, totalItemsCount = the expense's total line
     item count.

POST /api/Expense/{id}/addUser?userId=        (existing endpoints; after B9 all three reject
DELETE /api/Expense/{id}/removeUser/{userId}   with 400 "This expense's sharing is managed by
PUT  /api/Expense/{id}/updateShares            line-item assignment — use the Document Results
                                                page instead." when the target expense has any
                                                LineItem row)
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
- Line-item/OCR pipeline: `Repositories/ExpenseAnalysis/ExpenseAnalysis.cs` (`StoreResults`,
  `BuildLineItemJson`, `UpdateExpenseUsers`) and `Models/Domain/Document/DocumentJobResult.cs`
  (`ResultLineItems`/`ColumnNames` JSON blobs, populated once a Textract job succeeds).

## Phase checklist

- [x] **B1 — Settlement model, repository, endpoints**
- [x] **B2 — Net balances + per-counterparty balance detail**
- [x] **B3 — Settlement notification**
- [x] **B4 — Budget model, repository, endpoints**
- [x] **B5 — Budget threshold alerts**
- [x] **B6 — Expose `userId` on `GET /api/Friends/getFriends`**
- [x] **B7 — Require friendship before adding a user to an expense split**
- [x] **B8 — LineItem/LineItemAssignment schema**
- [x] **B9 — Line-item assignment repository, business rules, StoreResults integration**
- [ ] **B10 — Controller endpoints + guard old manual-share endpoints**

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

### B8 — LineItem/LineItemAssignment schema

Context: today `DocumentJobResult.ResultLineItems` is just a JSON string built from Textract's
dynamic per-vendor fields — there is no per-row identity, so nothing can be assigned to a user.
This phase adds the normalized, assignable layer underneath that JSON blob without touching the
blob itself (the raw dynamic-column table on the frontend keeps working unchanged).

- `Models/Domain/Document/LineItem.cs` — new entity: `Id (Guid)`, `DocumentJobResultId (Guid)` +
  `DocumentJobResult` nav, `ExpenseId (Guid)` + `Expense` nav (denormalized onto the line item so
  a multi-document expense's items can be queried/aggregated in one pass without joining through
  `DocumentJobResult`), `SortOrder (int)`, `Description (string?)`, `Quantity (string?)`,
  `Amount (decimal?)`, `RawFieldsJson (string)` (the full raw per-vendor Textract field dump for
  that row, so nothing observable today is lost).
  - **Naming collision**: there's already a transient (non-EF) `Models.Domain.LineItem` POCO
    (`Description`/`Quantity`/`Amount` all `string?`) used only in
    `ExpenseAnalysis.StartExpenseExtractAsync` (~line 278) to shape the ad-hoc Textract response
    returned by that method. Rename that one to `TextractLineItemFields` before adding the new
    persisted entity — grep for `Models.Domain.LineItem` first (today it's only that one file)
    and update `ExpenseDocumentResult.LineItems`'s declared type to match.
- `Models/Domain/Document/LineItemAssignment.cs` — join entity, composite PK
  `(LineItemId, UserId)`, `LineItem` nav (FK cascade), `User` nav (FK restrict, mirroring how
  `ExpenseUser`'s `User` FK is configured), `AssignedAt (DateTime, default UtcNow)`.
- `Data/UserDocumentsDbContext.cs`: add `DbSet<LineItem> LineItems` and
  `DbSet<LineItemAssignment> LineItemAssignments`; in `OnModelCreating`, configure
  `LineItemAssignment`'s composite key + both FKs (mirror the existing `ExpenseUser` block,
  lines ~27-38) and `LineItem`'s FK to `DocumentJobResult` (cascade) and to `Expense` (restrict,
  same style as `ExpenseModel.CreatedBy`'s FK).
- EF migration: `dotnet ef migrations add AddLineItemsAndAssignments --context UserDocumentsDbContext`,
  following the two-table `CreateTable`/`ForeignKey`/`CreateIndex` pattern in
  `Migrations/UserDocumentsDb/20260710160900_AddBudgets.cs` — add indexes on
  `LineItems.DocumentJobResultId`, `LineItems.ExpenseId`, and `LineItemAssignments.UserId`.
- Matching hand-written SQL script `ExpenseAnalyserDbScripts/15-line-items-and-assignments.sql`
  (next sequential number after `14-add-expense-allowreceipts.sql`), following
  `12-friend-requests.sql`'s `IF NOT EXISTS` idempotent-guard style.
- No behavior change in this phase — `ExpenseAnalysis.StoreResults` does not populate the new
  tables yet (that's B9). Safe to merge standalone.

Tests: none beyond confirming `dotnet build` and the full existing suite still pass unmodified
(schema-only phase — check `Tests/Expense.API.IntegrationTests/Infrastructure` for however the
test database gets its schema applied, e.g. `Migrate()`/`EnsureCreated()`, and confirm the new
tables come up cleanly there too).

Acceptance: quality gates pass; no repository/controller/DTO behavior changed.

### B9 — Line-item assignment repository, business rules, StoreResults integration

Context: this phase makes assignments real — the write path, the derivation of `ExpenseUser`
shares from assignments, and the first-scan carry-forward behavior — but does not yet expose any
of it over HTTP (that's B10), so it can be developed/tested in isolation against the repository
layer.

- New DTOs `Models/DTO/LineItemDto.cs`, `Models/DTO/LineItemAssigneeDto.cs` per the contract
  above. Add `List<LineItemDto> LineItems { get; set; } = new();` to `Models/DTO/DocumentResultDto.cs`.
- `Mappings/AutomapperProfiles.cs`: `CreateMap<LineItem, LineItemDto>().ForMember(dest => dest.Assignees, opt => opt.MapFrom(src => src.Assignments.Select(a => a.User)));`,
  `CreateMap<User, LineItemAssigneeDto>();`, and extend the existing
  `CreateMap<DocumentJobResult, DocumentResultDto>()` with
  `.ForMember(dest => dest.LineItems, opt => opt.MapFrom(src => src.LineItems))`.
- `Repositories/Expense/IExpenseRepository.cs` + `ExpenseRepository.cs`, new methods:
  - `AssignUserToLineItemAsync(Guid lineItemId, Guid userId)` → `LineItem`: friendship check
    copied from `CreateExpenseUserAsync`'s inline `FriendRequests` query (~lines 78-86, skipped
    when `userId` equals the caller), no-op if the assignment already exists, else insert
    `LineItemAssignment`, call `RecomputeExpenseUsersFromAssignmentsAsync` for the item's
    `ExpenseId`, return the item with `Assignments`/`User` loaded.
  - `RemoveUserFromLineItemAsync(Guid lineItemId, Guid userId)` → `LineItem`: throw
    `new Exception("Cannot remove the last remaining assignee from a line item.")` when current
    `Assignments.Count <= 1` (mirrors `RemoveExpenseUserAsync`'s last-user guard, ~line 138),
    else remove, recompute, return the item.
  - `AssignUserToAllLineItemsAsync(Guid expenseId, Guid userId)` → `List<LineItem>`: one
    friendship check up front (single user, many items), then insert a `LineItemAssignment` for
    every `LineItem` on the expense that `userId` isn't already on (additive only — never touch
    other users' assignments), recompute once at the end, return all items for the expense.
  - `RecomputeExpenseUsersFromAssignmentsAsync(Guid expenseId)`: load every `LineItem` where
    `ExpenseId == expenseId` (across all the expense's documents) with `Assignments`; per user,
    `perUserDollar += lineItem.Amount / lineItem.Assignments.Count` for each item they're on
    (skip items with null `Amount` or zero assignees defensively); `UserAmount = Math.Round(perUserDollar, 2)`;
    `UserShare = expense.Amount > 0 ? perUserDollar / (double)expense.Amount : 0` — per the
    remainder rule, do **not** redistribute any gap between `expense.Amount` and the itemized
    sum. Upsert `ExpenseUser` rows to exactly the set of users with ≥1 assignment anywhere on the
    expense (add missing rows, update existing, remove rows for users with zero assignments
    left) — this keeps `ExpenseUser` as the materialized read model so `GetOutstandingBalancesAsync`/
    `GetBalanceDetailAsync`/`GetAssignUsers` need no changes in this phase.
- `Repositories/ExpenseAnalysis/ExpenseAnalysis.cs`, `StoreResults`: alongside the existing
  `BuildLineItemJson`/`ResultLineItems` population (leave that untouched, it still feeds the raw
  dynamic-column table), parse that same line-item list and persist one `LineItem` row per entry
  (`Description` from the `"ITEM"` field, `Quantity` from whichever field the vendor uses,
  `Amount` parsed from the `"PRICE"`-style field using the same numeric-extraction
  `Regex.Replace(fieldValue, @"[^\d.-]", "")` pattern already used in `GetTotal`), with
  `SortOrder` = the entry's index in the list. For each newly created `LineItem`, seed its
  initial `LineItemAssignment`s from whatever `ExpenseUser` rows already exist for
  `documentJobResult.ExpenseId` (carry-forward); if none exist yet, default to
  `documentJobResult.CreatedById` only. Replace the unconditional `UpdateExpenseUsers(documentJobResult.ExpenseId)`
  call at the end of `StoreResults` with: call `RecomputeExpenseUsersFromAssignmentsAsync` when
  the expense now has ≥1 `LineItem`, otherwise keep calling the old `UpdateExpenseUsers` rescale
  logic (expenses with zero line items are unaffected by this feature).
- Tests: extend `Tests/Expense.API.IntegrationTests/Infrastructure/SeedData.cs` with an
  `AddLineItem(expenseId, documentJobResultId, description, amount, params Guid[] assigneeUserIds)`
  helper (style of `AddExpense`/`AddShare`/`AddReceipt`). New suite
  `Tests/Expense.API.IntegrationTests/LineItemAssignmentTests.cs`: assigning a non-friend → 400
  exact message; assigning an accepted friend → 200 and `RecomputeExpenseUsersFromAssignmentsAsync`
  produces correct `ExpenseUser.UserAmount` for a 2-item/2-person even split; removing the last
  assignee → 400 exact message; bulk-assign is additive (other users' assignments on other items
  untouched); first-scan carry-forward (seed an expense with a pre-existing manually-added
  `ExpenseUser`, simulate `StoreResults` creating `LineItem`s, assert that user is on every new
  item rather than reset to creator-only). Confirm `GetOutstandingBalancesAsync`/
  `GetBalanceDetailAsync` tests still pass unmodified (regression check that `ExpenseUser`'s
  shape/semantics as read by those methods hasn't changed).

Acceptance: quality gates pass; `addUser`/`removeUser`/`updateShares` and their existing tests
are completely unaffected in this phase (the guard rejecting them on line-item expenses is B10).

### B10 — Controller endpoints + guard old manual-share endpoints

- `Repositories/Expense/ExpenseRepository.cs`, `GetDocResult`: eager-load the new relations
  (`.Include(d => d.LineItems).ThenInclude(li => li.Assignments).ThenInclude(a => a.User)`) so
  the B9 AutoMapper map has data to project into `DocumentResultDto.LineItems`.
- `Controllers/ExpenseController.cs`, same file/conventions as the existing `addUser`/
  `removeUser` actions, new endpoints:
  - `[HttpPut("lineItem/{lineItemId}/assignees/{userId}")]` → `AssignUserToLineItemAsync`, maps
    result to `LineItemDto`, 200/400 per contract.
  - `[HttpDelete("lineItem/{lineItemId}/assignees/{userId}")]` → `RemoveUserFromLineItemAsync`.
  - `[HttpPut("{expenseId}/lineItems/assignAll/{userId}")]` → `AssignUserToAllLineItemsAsync`,
    maps to `List<LineItemDto>`.
- Extend `GetAssignUsers` (`ExpenseRepository`) to also compute `ItemsAssignedCount`/
  `TotalItemsCount` per returned `ExpenseUser` — a single query against `LineItemAssignments`/
  `LineItems` for the expense (both null when the expense has zero `LineItem` rows). Add the
  matching fields to `Models/DTO/ExpenseUserDto.cs` and the existing
  `CreateMap<ExpenseUser, ExpenseUserDto>()` map.
- Guard `CreateExpenseUserAsync`, `RemoveExpenseUserAsync`, `UpdateExpenseUserSharesAsync`: at
  the top of each, `if (await userDocumentsDbContext.LineItems.AnyAsync(li => li.ExpenseId == expenseId))`
  → `throw new Exception("This expense's sharing is managed by line-item assignment — use the Document Results page instead.");`
  before any other logic.
- Tests: extend `LineItemAssignmentTests.cs` (or add `ExpenseControllerLineItemTests.cs`) for the
  three new endpoints end-to-end (route → repository → DB), the N-of-M fields appearing
  correctly on `getAssignedUsers`, and — critically — that `addUser`/`removeUser`/`updateShares`
  now 400 with the exact guard message once an expense has any `LineItem`, while the existing
  suite covering those three endpoints on expenses with none still passes completely unchanged.

Acceptance: quality gates pass; manual golden path works end-to-end against a real scanned
expense (assign a line item → `getAssignedUsers` reflects it with correct N-of-M and amount).

---

When every box is checked, the runner has nothing left to do — a human should disable the
routine at https://claude.ai/code/routines (or extend this file with new phases).
