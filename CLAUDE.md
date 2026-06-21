# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

**Expense.API** — an ASP.NET Core (.NET 7) Web API for expense tracking. Users upload receipt/expense documents to AWS S3, the API extracts data via AWS Textract (async, polled in the background), and users can share expenses with friends and receive real-time notifications over SignalR. Auth is JWT-based; caching/sessions use Redis; persistence is SQL Server via EF Core.

## Tech stack

- **Framework**: ASP.NET Core, **net7.0** (nullable + implicit usings enabled)
- **ORM**: Entity Framework Core 7 (SQL Server)
- **Auth**: JWT bearer + ASP.NET Core Identity (`ExpenseAuthDbContext`)
- **Mapping**: AutoMapper (single profile in `Mappings/AutomapperProfiles.cs`)
- **AWS**: S3 (documents), Textract (OCR), SecretsManager
- **Realtime**: SignalR (`TextractNotificationHub` at `/api/textractNotification`)
- **Cache/session**: Redis (StackExchange.Redis)
- **Logging**: Serilog (console + file in `Logs/`)
- **Docs**: Swagger/Swashbuckle (Development only, at `/swagger`)

## Layout

```
Controllers/          HTTP layer — thin, inject repositories + IMapper
Repositories/<Feature>/   I<Feature>Repository.cs + <Feature>Repository.cs — business logic + EF Core
Models/Domain/<Feature>/  EF Core entities (persisted)
Models/DTO/               Request/response DTOs (flat folder)
Mappings/                 AutomapperProfiles.cs — all domain↔DTO maps
Data/                     UserDocumentsDbContext (app data), ExpenseAuthDbContext (Identity/auth)
Configurations/           DI + service setup (extension methods called from Program.cs)
Middlewares/              ExceptionHandlerMiddleware (global error handling)
CustomActionFilters/      ValidateModelAtrribute (auto-400 on invalid model)
Migrations/               EF Core migrations
```

Root namespace: `Expense.API.*`. Solution file: `ExpenseApi.sln`.

## Architecture rules

- **Strict layering**: Controller → Repository (interface + impl) → DbContext. Controllers must not access the DbContext directly or hold business logic.
- Register every repository in `Configurations/RepositoryConfig.cs` (`AddScoped`).
- Return **DTOs**, never domain entities, from controllers — map via `IMapper`.
- The current user is resolved inside repositories from the JWT `NameIdentifier` claim via `IHttpContextAccessor`, not passed down from controllers.
- Two DbContexts: **`UserDocumentsDbContext`** for app data (expenses, documents, users, friends, notifications), **`ExpenseAuthDbContext`** for Identity/auth. Pick the right one.
- EF Core entity IDs are `Guid`; use async EF APIs (`...Async`).
- `Program.cs` wires everything through `Configure*` extension methods in `Configurations/`.

To add an endpoint, use the **`add-api-endpoint`** skill (`.claude/skills/add-api-endpoint/`), which encodes the full step-by-step pattern. To add or change database tables, use the **`db-table-migration`** skill (`.claude/skills/db-table-migration/`), which includes a reference of the current relational schema.

## Build & run

```bash
dotnet restore
dotnet build ExpenseApi.sln
dotnet run                      # serves the API; Swagger UI at /swagger in Development
```

Docker (full stack — API + SQL Server + Redis):

```bash
docker compose up --build
```

Configuration comes from environment variables / `.env` (see `.env.example` and `DOCKER_SETUP.md`). Do not commit secrets; `.env` is gitignored.

## EF Core migrations

```bash
dotnet ef migrations add <Name> --context UserDocumentsDbContext   # or ExpenseAuthDbContext
dotnet ef database update --context <ContextName>
```

Always specify `--context` since the project has two DbContexts. Requires `dotnet tool install --global dotnet-ef`.

## Conventions

- Controllers: `[Route("api/[controller]")]`, inherit `Controller`, `[Authorize]` for protected resources; secondary actions use `[Http*]` + `[Route("camelCaseName")]`.
- Async actions return `IActionResult` (`Ok`, `NoContent`, `NotFound`, `BadRequest(e.Message)`).
- Background Textract polling runs as a hosted service (`TextractPollingRepository`).

## Notes

- More background: `README.md`, `guide.md`, `DOCKER_SETUP.md`.
- API versioning is enabled (default v1.0); Swagger only mounts in Development.
