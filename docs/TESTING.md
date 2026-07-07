# Testing the ExpenseApi endpoints

This repo has two complementary ways to test the API endpoints with realistic data:

1. **Automated xUnit integration tests** (`Tests/Expense.API.IntegrationTests`) — spin up real
   SQL Server + Redis in containers, boot the actual app, and assert endpoint behavior. CI-friendly.
2. **A manual seed + HTTP harness** (`ExpenseAnalyserDbScripts/seed-data.sql` + `requests.http`) —
   load realistic data into a running instance and eyeball the responses.

Both were added together; the automated suite is the primary gate.

---

## 1. Automated integration tests

### Run them

```bash
dotnet test Tests/Expense.API.IntegrationTests/Expense.API.IntegrationTests.csproj
```

Requires **Docker running** (Testcontainers starts the databases) and the **.NET 7 SDK**.
First run pulls the SQL Server + Redis images. Current status: **28 tests, all passing (~5 s** after
the containers are warm; the first run also pays container startup).

### How the test host works (`CustomWebAppFactory`)

- Boots the real `Program.cs` via `WebApplicationFactory<Program>` (a `public partial class Program`
  marker was added to `Program.cs` so the test project can reference the entry point).
- Starts **one SQL Server container** (hosting both `AuthenticationDb` and `ExpenseAnalyserDb`) and
  **one Redis container**. Connection strings + JWT + AWS config are injected as environment
  variables *before* the host builds, because `Program.cs` connects to Redis eagerly at startup.
- **AWS is mocked**: `IAmazonS3` and `IAmazonTextract` are replaced with Moq mocks, so AWS-backed
  endpoints are covered without credentials. The background `TextractPollingRepository` hosted
  service is removed so it doesn't hammer the mock.
- Schemas are created from EF: `MigrateAsync()` for the Identity DB (existing migration, seeds the
  Reader/Writer/Admin roles) and `EnsureCreatedAsync()` for the business DB.
- Test classes share one set of containers (`[Collection("Api")]`) and run sequentially; **isolation
  comes from giving every test unique users** rather than resetting the DB.

### Auth in tests

Tests use the **real** flow: `RegisterAndLoginAsync` calls `POST /api/Auth/Register` (with a role —
required, or the business `Users` row isn't created) then `POST /api/Auth/Login`. The JWT is
delivered as the `jwtToken` **cookie** (not a bearer header), and the `HttpClient` cookie container
resends it automatically.

### What's covered

| Area | File | Highlights |
|------|------|-----------|
| Auth | `AuthTests.cs` | register (with/without role), duplicate email, login, wrong password, checkSession, protected-endpoint 401 |
| Expense CRUD | `ExpenseCrudTests.cs` | create/list (user-scoped)/get/update/delete/addUser/getAssignedUsers/count |
| **Dashboard** | `DashboardTests.cs` | exact-number assertions for `summary`, `monthly`, `balances` over a seeded multi-user scenario, plus empty-state and 401 |
| Friends & Notifications | `FriendsNotificationTests.cs` | user search, notification list + readAll, and the missing-auth gaps |
| AWS (mocked) | `AwsEndpointTests.cs` | S3 list-buckets happy path, authorization gates on every AWS endpoint |

The dashboard scenario (see `SeedData.cs` + `DashboardTests.cs`) seeds three users with expenses
across categories and months, receipts, and splits in both owe directions, then asserts the exact
aggregates (`TotalSpent`, `Categories` ordering, `YouOwe`/`OwedToYou`, monthly series, balances).

---

## 2. Manual seed + HTTP harness

Use this to drive a live instance (docker-compose or `dotnet run`).

### Steps

1. **Start the stack** and create the business schema. With docker-compose the databases come up on
   `localhost:1433` / `localhost:1434`; apply the DDL scripts in `ExpenseAnalyserDbScripts/` in order
   (`01`–`07`, `09`) against `ExpenseAnalyserDb` (there is no `08`).
2. **Register the primary user** by running the *Auth* section of `requests.http` (creates the
   Identity account **and** the business `Users` row for `alice`).
3. **Seed transactional data**: run `ExpenseAnalyserDbScripts/seed-data.sql`. It finds `alice`,
   creates counterparties `bob`/`carol`, and inserts expenses/receipts/splits. It is idempotent
   (clears transactional rows and reseeds) — safe only on a test database.
4. **Exercise endpoints** with `requests.http` (VS Code REST Client or JetBrains HTTP client). The
   Dashboard section documents the expected numbers from the seed:

   | Endpoint | Expected (from seed) |
   |----------|----------------------|
   | `GET /api/Expense/summary?period=month` | TotalSpent **495.50**, ReceiptsScanned **2**, YouOwe **45**, OwedToYou **150**, Categories: Travel 300, Groceries 120.50, Dining 45, Other 30 |
   | `GET /api/Expense/monthly?months=6` | current **495.50**, −1 **80**, −2 **150**, −3 **60**, else 0 |
   | `GET /api/Expense/balances` | OwedToYou [bob 100, carol 50]; YouOwe [bob 45] |

The DDL + seed + these aggregates were validated end-to-end against SQL Server 2022.

---

## Fixes and quirks discovered while adding tests

These were found while building the tests. The DDL fixes are applied; the rest are **documented, not
changed** (they affect production behavior and were out of scope):

- **DDL fixed** — `ExpenseAnalyserDbScripts/05-expense_users.sql` was missing the `UserShare` and
  `UserAmount` columns that the domain model and every dashboard query use; they were added. The
  database name was inconsistent (`ExpenseAnalyser` vs `ExpenseAnalyserDb`); all scripts now use
  `ExpenseAnalyserDb`.
- **EF model cascade cycles** — the business model has overlapping cascade-delete paths
  (Documents/DocumentJobResults/ExpenseUsers/Notifications/FriendRequests → Users/Expenses) that
  SQL Server rejects on `EnsureCreated` ("may cause cycles or multiple cascade paths"). Production
  never hits this because it uses raw DDL; the test host works around it with
  `RestrictCascadeModelCustomizer` (downgrades FKs to non-cascading) **for tests only**.
- **Routing quirk** — the S3 controller's route resolves to **`/api/S/list-buckets`**, not
  `/api/S3/...` (the `[controller]` token yields `S`). A client calling `/api/S3/...` will 404.
- **Missing `[Authorize]`** — `FriendsController` and `NotificationController` are ungated, and
  `DELETE /api/Document/{id}` is anonymous. They don't return 401; they either serve data
  (Friends search) or throw and surface as 500 (Notifications, Document delete). Encoded as tests
  so the behavior is visible.
- **`GET /api/Expense/count`** returns a **global** expense count, not the current user's.
