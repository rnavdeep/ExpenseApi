# Dashboard API Plan — DB changes & endpoints

**Expense.API · ASP.NET Core (.NET 7) · EF Core code-first**

## Context

The frontend (`expense-analyser`) added a post-login **Dashboard** homescreen (phase 4). It currently
renders from a local seed module (`src/data/dashboardSeed.ts`), where each block carries an
`// API NOTE:` for the endpoint that should replace it. This plan implements those endpoints in the API
so the dashboard can run on real data.

Everything the dashboard needs is derivable from the existing schema (`Expenses`, `ExpenseUsers`,
`Documents`) **except a spending category**, which requires one new nullable column on `Expense`.

Follow the repo's two skills: **`db-table-migration`** (the column change) and **`add-api-endpoint`**
(the three endpoints). Strict layering applies: Controller → `IExpenseRepository`/`ExpenseRepository`
→ `UserDocumentsDbContext`; return DTOs via AutoMapper; resolve the current user inside the repository
from the JWT `NameIdentifier` claim (as the existing methods do).

## What the dashboard consumes (and its source)

| Dashboard block | Source | New work? |
|---|---|---|
| Recent expenses (4) | existing `GET api/Expense` (paginated, sort `CreatedAt` desc) | none |
| KPI: Total spent | `SUM(Expenses.Amount)` where `CreatedById = me`, in period | endpoint |
| KPI: You owe | `SUM(ExpenseUsers.UserAmount)` where `UserId = me` and expense not created by me | endpoint |
| KPI: Owed to you | `SUM(ExpenseUsers.UserAmount)` for expenses I created, `UserId != me` | endpoint |
| KPI: Receipts scanned | `COUNT(Documents)` where `UserId = me`, `UploadedAt` in period | endpoint |
| Bar chart: spending over time | `SUM(Amount) GROUP BY month(CreatedAt)` | endpoint |
| Donut: by category | `SUM(Amount) GROUP BY Category` | **column + endpoint** |
| Balances: per person | `ExpenseUsers.UserAmount` grouped by counterparty | endpoint |

---

## Part 1 — DB change: add `Category` to Expense (`db-table-migration` skill)

`Models/Domain/Expense/Expense.cs` has no category today (`Id, Title, Description, Amount, CreatedAt,
CreatedById, …`). Add a **nullable** string so existing rows stay valid (null ⇒ treated as "Other").

1. **Entity** — add to `Expense.cs` (with the `/// <summary>` doc every entity uses):
   ```csharp
   /// <summary>
   /// Spending category for dashboard breakdowns (e.g. Groceries, Dining). Null = uncategorised.
   /// </summary>
   public string? Category { get; set; }
   ```
   No `OnModelCreating` change needed (plain scalar column). Optionally add an index to speed the
   dashboard group-bys: `modelBuilder.Entity<Expense>().HasIndex(e => new { e.CreatedById, e.CreatedAt });`

2. **Migration** (EF code-first, app-data context):
   ```bash
   dotnet ef migrations add AddExpenseCategory \
     --context UserDocumentsDbContext -o Migrations/UserDocumentsDb
   dotnet ef database update --context UserDocumentsDbContext
   ```
   Inspect the generated `Up`/`Down` — it should be a single `AddColumn<string>("Category", "Expenses",
   nullable: true)`, no drops.

3. **Keep the manual SQL path in sync.** Per `DOCKER_SETUP.md`/local bring-up, the
   `ExpenseAnalyserDbScripts/` are applied by hand (they don't auto-run). Add
   `ExpenseAnalyserDbScripts/09-add-expense-category.sql`:
   ```sql
   USE ExpenseAnalyserDb;
   IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'Category'
                  AND Object_ID = Object_ID(N'dbo.Expenses'))
   BEGIN
     ALTER TABLE dbo.Expenses ADD Category NVARCHAR(64) NULL;
   END
   ```

4. **Expose Category through the existing create/update flow** so new expenses can be categorised:
   - `Models/DTO/AddExpenseDto.cs` — add `public string? Category { get; set; }`
     (`ExpenseDto : AddExpenseDto`, so it flows to reads automatically).
   - `Models/DTO/UpdateExpenseDto.cs` — add `Category` + constructor param.
   - `Mappings/AutomapperProfiles.cs` — confirm the `Expense ↔ AddExpenseDto/ExpenseDto` maps carry
     `Category` (AutoMapper maps same-named members automatically; verify no `.Ignore`).
   - Follow-up (frontend, out of scope here): add a Category field to the New/Edit Expense form.

---

## Part 2 — Three dashboard endpoints (`add-api-endpoint` skill)

All three are **new methods on the existing feature**: add to `IExpenseRepository` →
`ExpenseRepository` → `ExpenseController` (route prefix `api/Expense`, already `[Authorize]`). Resolve
the current user with the same claim lookup used by `GetExpensesAsync`/`GetExpensesSharedAsync`
(`ClaimTypes.NameIdentifier` → `Users` table → `user.Id`); consider extracting a private
`GetCurrentUserAsync()` helper since the pattern repeats.

**Period semantics** — a `period` query param: `month` (current calendar month), `quarter` (trailing 3
months), `year` (current year). Map it to a `(DateTime from, DateTime to)` window in the repository.

### New DTOs (`Models/DTO/`, namespace `Expense.API.Models.DTO`)

```csharp
public class DashboardSummaryDto
{
    public decimal TotalSpent { get; set; }
    public int ReceiptsScanned { get; set; }
    public double YouOwe { get; set; }
    public double OwedToYou { get; set; }
    public double TotalSpentDeltaPct { get; set; }     // vs previous comparable period
    public List<CategoryBreakdownDto> Categories { get; set; } = new();
}
public class CategoryBreakdownDto { public string Category { get; set; } public decimal Amount { get; set; } }
public class MonthlySpendingDto { public int Year { get; set; } public int Month { get; set; } public string Label { get; set; } public decimal Amount { get; set; } }
public class BalanceEntryDto { public Guid UserId { get; set; } public string UserName { get; set; } public double Amount { get; set; } }
public class OutstandingBalancesDto { public List<BalanceEntryDto> YouOwe { get; set; } = new(); public List<BalanceEntryDto> OwedToYou { get; set; } = new(); }
```
These are plain projections — building them directly in the repository is fine; AutoMapper maps are
optional. Register any maps you do add in `AutomapperProfiles.cs`.

### Repository methods (add to interface + impl)

```csharp
Task<DashboardSummaryDto>      GetDashboardSummaryAsync(string period);
Task<List<MonthlySpendingDto>> GetMonthlySpendingAsync(int months);
Task<OutstandingBalancesDto>   GetOutstandingBalancesAsync();
```

Query notes (all scoped to the resolved `user.Id`, async EF):
- **Summary** — `TotalSpent`/`Categories`: filter `Expenses` by `CreatedById == user.Id` and `CreatedAt`
  in window; `GROUP BY Category` (coalesce null → "Other"). `ReceiptsScanned`:
  `Documents.Count(d => d.UserId == user.Id && d.UploadedAt` in window`)` (use `DocumentJobResults`
  filtered by `Status == completed` instead if "scanned" must mean successfully Textract-processed).
  `YouOwe`: `ExpenseUsers.Where(eu => eu.UserId == user.Id && eu.Expense.CreatedById != user.Id).Sum(UserAmount)`.
  `OwedToYou`: `ExpenseUsers.Where(eu => eu.Expense.CreatedById == user.Id && eu.UserId != user.Id).Sum(UserAmount)`.
  `TotalSpentDeltaPct`: run the TotalSpent query once more over the previous window and compute the %.
- **Monthly** — `Expenses.Where(CreatedById == user.Id && CreatedAt >= startOfWindow)`, group by
  `{ CreatedAt.Year, CreatedAt.Month }`, `Sum(Amount)`, order ascending; project `Label` (e.g. "Jun").
  Fill gap months with 0 in C# so the chart always has `months` points.
- **Balances** — `YouOwe`: `ExpenseUsers` where `UserId == user.Id && Expense.CreatedById != user.Id`,
  group by `Expense.CreatedById`, `Sum(UserAmount)`, join `Users` for the name. `OwedToYou`: where
  `Expense.CreatedById == user.Id && UserId != user.Id`, group by `UserId`, `Sum(UserAmount)`, join name.

### Controller actions (`ExpenseController.cs`)

```csharp
[HttpGet("summary")]
public async Task<IActionResult> GetSummary([FromQuery] string period = "month") =>
    Ok(await expenseRepository.GetDashboardSummaryAsync(period));

[HttpGet("monthly")]
public async Task<IActionResult> GetMonthly([FromQuery] int months = 6) =>
    Ok(await expenseRepository.GetMonthlySpendingAsync(months));

[HttpGet("balances")]
public async Task<IActionResult> GetBalances() =>
    Ok(await expenseRepository.GetOutstandingBalancesAsync());
```
Match the existing try/catch → `BadRequest` style in `ExpenseController`. No new DI registration is
needed (methods live on the already-registered `IExpenseRepository`). Resulting paths (frontend uses
`VITE_APP_API_URL=/api` + `/Expense`): `/api/Expense/summary`, `/api/Expense/monthly`,
`/api/Expense/balances`.

---

## Files touched

| File | Change |
|---|---|
| `Models/Domain/Expense/Expense.cs` | add `Category` (+ optional index in `OnModelCreating`) |
| `Migrations/UserDocumentsDb/*` | new `AddExpenseCategory` migration |
| `ExpenseAnalyserDbScripts/09-add-expense-category.sql` | manual-path ALTER TABLE |
| `Models/DTO/AddExpenseDto.cs`, `UpdateExpenseDto.cs` | carry `Category` |
| `Models/DTO/DashboardSummaryDto.cs` (+ Category/Monthly/Balance DTOs) | **new** |
| `Mappings/AutomapperProfiles.cs` | verify Category maps; add dashboard maps if used |
| `Repositories/Expense/IExpenseRepository.cs` | 3 method signatures |
| `Repositories/Expense/ExpenseRepository.cs` | 3 implementations (+ optional `GetCurrentUserAsync` helper) |
| `Controllers/ExpenseController.cs` | 3 actions |

## Verification

1. `dotnet build ExpenseApi.sln` — clean.
2. `dotnet ef database update --context UserDocumentsDbContext` applies the column; confirm `Expenses`
   has `Category` (and the docker manual path via `09-add-expense-category.sql`).
3. Swagger (`/swagger`, Development): hit `GET /api/Expense/summary?period=month`, `/monthly?months=6`,
   `/balances` with a logged-in JWT cookie; verify shapes match the DTOs and numbers reconcile against
   `GET /api/Expense` data.
4. Cross-check the frontend: swap each seed block in `dashboardSeed.ts` for a fetch to these endpoints
   (the `// API NOTE:` comments mark exactly where) and confirm the dashboard renders identically.
5. Edge cases: user with no expenses → zeros/empty lists (not 404); null `Category` rows bucket into
   "Other"; months with no spend appear as 0 in the bar chart.

## Notes / decisions to confirm

- **Receipts scanned** definition: count of uploaded `Documents` (proposed) vs. successfully
  Textract-processed `DocumentJobResults`. Pick one.
- **`UserAmount` is `double?`** in `ExpenseUser`; null shares are skipped in the sums. Confirm that's the
  intended settlement model (the frontend treats these as currency).
- **Net vs. gross balances**: this plan sums gross owe/owed per direction. If you want a single *net*
  figure per counterparty, subtract the two directions in `GetOutstandingBalancesAsync`.
