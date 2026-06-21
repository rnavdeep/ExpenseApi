---
name: add-api-endpoint
description: Scaffold a new REST API endpoint in this ASP.NET Core (.NET 7) Expense API following the project's Controller → Repository → EF Core layering. Use when adding a new endpoint, controller, repository method, DTO, or domain entity.
---

# Add an API Endpoint

This project follows a strict **Controller → Repository (interface + impl) → EF Core DbContext** layering with **AutoMapper** between domain entities and DTOs. Follow it exactly so new endpoints stay consistent with the codebase.

## Architecture at a glance

- `Controllers/` — thin HTTP layer. Inject repository interfaces + `IMapper`. No business logic or direct DB access.
- `Repositories/<Feature>/` — one folder per feature, each with `I<Feature>Repository.cs` (interface) and `<Feature>Repository.cs` (implementation). All business logic and EF Core queries live here.
- `Models/Domain/<Feature>/` — EF Core entities (persisted).
- `Models/DTO/` — request/response shapes exposed over HTTP (flat folder, no subfolders).
- `Mappings/AutomapperProfiles.cs` — single profile; register every domain↔DTO map here.
- `Data/UserDocumentsDbContext.cs` — main app DbContext (expenses, users, documents, friends, notifications). `ExpenseAuthDbContext` is Identity/auth only.
- `Configurations/RepositoryConfig.cs` — DI registration for repositories (`ConfigureRepositories`).
- Namespace root is `Expense.API.*`. Target framework: **net7.0**, nullable + implicit usings enabled.

## Conventions to match

- Controllers: `[Route("api/[controller]")]`, inherit `Controller`, add `[Authorize]` at class level for protected resources (see `ExpenseController`). Public-search style endpoints may omit it (see `FriendsController`).
- Route additional actions with `[HttpGet]`/`[HttpPost]` + `[Route("actionName")]` (camelCase route names) or route templates like `[HttpGet("{id}")]`.
- Return `IActionResult` from async actions; use `Ok(...)`, `NoContent()`, `NotFound(...)`, `BadRequest(e.Message)`. Wrap repository calls that throw in try/catch and surface `BadRequest(e.Message)` — this matches the existing style (a global `ExceptionHandlerMiddleware` also exists).
- The current user is read inside repositories from the JWT claim, not passed from the controller:
  ```csharp
  var userName = httpContextAccessor.HttpContext?.User?.Claims
      .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
  ```
- Map domain → DTO with `mapper.Map<TDto>(entity)` in the controller; never return domain entities directly.
- Use `[ApiController]`-style model validation via the `ValidateModelAtrribute` action filter (`CustomActionFilters/`) when you need automatic 400s on invalid models.
- EF Core: async APIs (`FirstOrDefaultAsync`, `ToListAsync`, `AddAsync`, `SaveChangesAsync`). Entity IDs are `Guid`.

## Steps to add an endpoint

Pick the smallest set of steps the change needs.

### A. New method on an existing feature

1. Add the method signature to `Repositories/<Feature>/I<Feature>Repository.cs`.
2. Implement it in `Repositories/<Feature>/<Feature>Repository.cs` (EF Core query, return a DTO or domain type).
3. Add the action to the matching controller, delegating to the repository and mapping the result.
4. If a new shape is returned, add a DTO in `Models/DTO/` and a map in `AutomapperProfiles.cs`.

### B. New feature (new controller + repository)

1. **Domain entity** (only if persisting new data): `Models/Domain/<Feature>/<Feature>.cs`, namespace `Expense.API.Models.Domain`, `Guid Id`, `DateTime CreatedAt { get; set; } = DateTime.UtcNow;`.
2. **DbSet + relationships**: add `public DbSet<Foo> Foos { get; set; }` to `Data/UserDocumentsDbContext.cs` and configure keys/FKs in `OnModelCreating` if needed.
3. **DTO(s)** in `Models/DTO/`, namespace `Expense.API.Models.DTO`.
4. **AutoMapper**: `CreateMap<Foo, FooDto>().ReverseMap();` in `Mappings/AutomapperProfiles.cs`.
5. **Repository interface + impl** in `Repositories/<Feature>/`. Constructor-inject `UserDocumentsDbContext`, and `IHttpContextAccessor` if you need the current user.
6. **Register DI**: add `services.AddScoped<IFooRepository, FooRepository>();` to `Configurations/RepositoryConfig.cs`.
7. **Controller** in `Controllers/<Feature>Controller.cs` following the conventions above.
8. **Migration** (only if the schema changed): see below.

### Migration (schema changes only)

The main context is `UserDocumentsDbContext`. Generate and apply with:

```bash
dotnet ef migrations add <DescriptiveName> --context UserDocumentsDbContext
dotnet ef database update --context UserDocumentsDbContext
```

(Requires the EF CLI: `dotnet tool install --global dotnet-ef`.)

## Verify

```bash
dotnet build ExpenseApi.sln
```

Then run (`dotnet run`) and check the endpoint appears in Swagger at `/swagger` (Development only). See `CLAUDE.md` for full build/run details.

## Reference files to copy patterns from

- Controller (auth + pagination/mapping): `Controllers/ExpenseController.cs`
- Controller (lighter, action routes): `Controllers/FriendsController.cs`
- Repository interface: `Repositories/FriendRequest/IFriendRequestRepository.cs`
- Repository impl (current-user claim, EF queries, DTO projection): `Repositories/FriendRequest/FriendRequestRepository.cs`
- Domain entity: `Models/Domain/FriendRequest/FriendRequest.cs`
- DTO: `Models/DTO/UserDto.cs`
- DI registration: `Configurations/RepositoryConfig.cs`
- Mapping: `Mappings/AutomapperProfiles.cs`
