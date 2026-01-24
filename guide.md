# ExpenseApi Development Guide

This guide provides comprehensive instructions for developing and extending the ExpenseApi project, including database migrations, API endpoint creation, and documentation of existing endpoints.

## Table of Contents

1. [Creating New Database Tables](#creating-new-database-tables)
2. [Creating New API Endpoints](#creating-new-api-endpoints)
3. [Existing API Endpoints Documentation](#existing-api-endpoints-documentation)
4. [Development Best Practices](#development-best-practices)

---

## Creating New Database Tables

This project uses Entity Framework Core with Code-First approach. Follow these steps to add new tables:

### Step 1: Create Domain Model

Create a new domain model class in the appropriate folder under `Models/Domain/`:

```csharp
// Models/Domain/YourFeature/YourModel.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Expense.API.Models.Domain
{
    public class YourModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Foreign key relationships
        public Guid UserId { get; set; }
        public User? User { get; set; }
    }
}
```

### Step 2: Add to DbContext

Add the new DbSet to `Data/UserDocumentsDbContext.cs`:

```csharp
public class UserDocumentsDbContext : DbContext
{
    // Existing DbSets...
    
    public DbSet<YourModel> YourModels { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure relationships
        modelBuilder.Entity<YourModel>()
            .HasOne(y => y.User)
            .WithMany()
            .HasForeignKey(y => y.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### Step 3: Create DTOs

Create Data Transfer Objects for API contracts in `Models/DTO/`:

```csharp
// Models/DTO/YourModelDto.cs
namespace Expense.API.Models.DTO
{
    public class YourModelDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

// Models/DTO/AddYourModelDto.cs
namespace Expense.API.Models.DTO
{
    public class AddYourModelDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}

// Models/DTO/UpdateYourModelDto.cs
namespace Expense.API.Models.DTO
{
    public class UpdateYourModelDto
    {
        [Required]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}
```

### Step 4: Create Repository Interface

Create the repository interface in `Repositories/YourFeature/`:

```csharp
// Repositories/YourFeature/IYourModelRepository.cs
using Expense.API.Models.Domain;

namespace Expense.API.Repositories.YourFeature
{
    public interface IYourModelRepository
    {
        Task<YourModel?> GetByIdAsync(Guid id);
        Task<List<YourModel>> GetAllAsync();
        Task<YourModel?> CreateAsync(YourModel yourModel);
        Task<YourModel?> UpdateAsync(YourModel yourModel);
        Task<bool> DeleteAsync(Guid id);
    }
}
```

### Step 5: Create Repository Implementation

Implement the repository:

```csharp
// Repositories/YourFeature/YourModelRepository.cs
using Expense.API.Data;
using Expense.API.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace Expense.API.Repositories.YourFeature
{
    public class YourModelRepository : IYourModelRepository
    {
        private readonly UserDocumentsDbContext dbContext;

        public YourModelRepository(UserDocumentsDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task<YourModel?> GetByIdAsync(Guid id)
        {
            return await dbContext.YourModels.FindAsync(id);
        }

        public async Task<List<YourModel>> GetAllAsync()
        {
            return await dbContext.YourModels.ToListAsync();
        }

        public async Task<YourModel?> CreateAsync(YourModel yourModel)
        {
            await dbContext.YourModels.AddAsync(yourModel);
            await dbContext.SaveChangesAsync();
            return yourModel;
        }

        public async Task<YourModel?> UpdateAsync(YourModel yourModel)
        {
            var existingModel = await dbContext.YourModels.FindAsync(yourModel.Id);
            
            if (existingModel == null)
            {
                return null;
            }

            existingModel.Name = yourModel.Name;
            existingModel.Description = yourModel.Description;
            existingModel.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();
            return existingModel;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var model = await dbContext.YourModels.FindAsync(id);
            
            if (model == null)
            {
                return false;
            }

            dbContext.YourModels.Remove(model);
            await dbContext.SaveChangesAsync();
            return true;
        }
    }
}
```

### Step 6: Configure AutoMapper

Add mappings in `Mappings/AutomapperProfiles.cs`:

```csharp
using AutoMapper;
using Expense.API.Models.Domain;
using Expense.API.Models.DTO;

namespace Expense.API.Mappings
{
    public class AutomapperProfiles : Profile
    {
        public AutomapperProfiles()
        {
            // Existing mappings...

            CreateMap<YourModel, YourModelDto>()
                .ReverseMap();

            CreateMap<AddYourModelDto, YourModel>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow));

            CreateMap<UpdateYourModelDto, YourModel>()
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow));
        }
    }
}
```

### Step 7: Register Services

Register the repository in `Configurations/RepositoryConfig.cs`:

```csharp
using Expense.API.Repositories.YourFeature;

public static class RepositoryConfig
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Existing registrations...

        services.AddScoped<IYourModelRepository, YourModelRepository>();
        
        return services;
    }
}
```

### Step 8: Create Database Migration

Generate and apply the migration:

```bash
# Generate migration
dotnet ef migrations add AddYourModelTable --context UserDocumentsDbContext

# Apply migration
dotnet ef database update --context UserDocumentsDbContext
```

### Step 9: Create Controller (See next section)

---

## Creating New API Endpoints

Follow this pattern to create new API endpoints:

### Step 1: Create Controller

Create a new controller in `Controllers/`:

```csharp
// Controllers/YourModelController.cs
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Expense.API.Models.DTO;
using Expense.API.Repositories.YourFeature;

namespace Expense.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Remove if endpoint should be public
    public class YourModelController : ControllerBase
    {
        private readonly IYourModelRepository yourModelRepository;
        private readonly IMapper mapper;

        public YourModelController(IYourModelRepository yourModelRepository, IMapper mapper)
        {
            this.yourModelRepository = yourModelRepository;
            this.mapper = mapper;
        }

        // GET: api/YourModel
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var yourModels = await yourModelRepository.GetAllAsync();
                var yourModelsDto = mapper.Map<List<YourModelDto>>(yourModels);
                return Ok(yourModelsDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        // GET: api/YourModel/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var yourModel = await yourModelRepository.GetByIdAsync(id);
                
                if (yourModel == null)
                {
                    return NotFound($"YourModel with ID {id} not found");
                }

                var yourModelDto = mapper.Map<YourModelDto>(yourModel);
                return Ok(yourModelDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        // POST: api/YourModel
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AddYourModelDto addYourModelDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var yourModel = mapper.Map<YourModel>(addYourModelDto);
                
                // Set the user ID if needed (from authenticated user)
                var userId = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (userId != null)
                {
                    yourModel.UserId = Guid.Parse(userId);
                }

                var createdModel = await yourModelRepository.CreateAsync(yourModel);
                var createdModelDto = mapper.Map<YourModelDto>(createdModel);
                
                return CreatedAtAction(nameof(GetById), new { id = createdModelDto.Id }, createdModelDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        // PUT: api/YourModel/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateYourModelDto updateYourModelDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != updateYourModelDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            try
            {
                var yourModel = mapper.Map<YourModel>(updateYourModelDto);
                var updatedModel = await yourModelRepository.UpdateAsync(yourModel);
                
                if (updatedModel == null)
                {
                    return NotFound($"YourModel with ID {id} not found");
                }

                var updatedModelDto = mapper.Map<YourModelDto>(updatedModel);
                return Ok(updatedModelDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        // DELETE: api/YourModel/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var deleted = await yourModelRepository.DeleteAsync(id);
                
                if (!deleted)
                {
                    return NotFound($"YourModel with ID {id} not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }
}
```

### Step 2: Test with Swagger

1. Run the application: `dotnet run`
2. Navigate to Swagger UI: `https://localhost:5001/swagger`
3. Test your new endpoints

### Best Practices for Controllers

- **Use DTOs**: Never expose domain models directly
- **Validate Input**: Always use model validation
- **Handle Exceptions**: Catch and handle exceptions gracefully
- **Use Appropriate Status Codes**: 200, 201, 400, 401, 403, 404, 500
- **Add Authorization**: Protect sensitive endpoints with `[Authorize]`
- **Document with XML Comments**: Add Swagger documentation

```csharp
/// <summary>
/// Gets all your models
/// </summary>
/// <returns>List of your models</returns>
[HttpGet]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> GetAll()
{
    // Implementation
}
```

---

## Existing API Endpoints Documentation

### Authentication Endpoints

#### 1. Check Session
```http
GET /api/Auth/checkSession
```

**Description**: Validates if the user has an active session

**Response**:
```json
{
  "userId": "string (user ID if authenticated)",
  "isAuthenticated": true
}
```

---

#### 2. Register User
```http
POST /api/Auth/Register
Content-Type: application/json
```

**Request Body**:
```json
{
  "userName": "johndoe",
  "email": "john@example.com",
  "password": "SecurePassword123!",
  "roles": ["Reader"]
}
```

**Response**: Returns created user details

**Available Roles**: `Reader`, `Writer`, `Admin`

---

#### 3. Login
```http
POST /api/Auth/Login
Content-Type: application/json
```

**Request Body**:
```json
{
  "username": "johndoe",
  "password": "SecurePassword123!"
}
```

**Response**:
```json
{
  "isLoggedIn": true,
  "error": ""
}
```

**Note**: JWT token is set as HttpOnly cookie

---

#### 4. Logout
```http
POST /api/Auth/Logout
```

**Response**:
```json
{
  "message": "Logged out successfully"
}
```

---

### Expense Endpoints

#### 1. Get All Expenses (Paginated)
```http
GET /api/Expense?pageNumber=1&pageSize=10&filterBy[propertyName]=Title&filterBy[value]=Lunch&sortFilter[propertyNameSort]=Amount&sortFilter[isAscending]=true
Authorization: Bearer {token}
```

**Query Parameters**:
- `pageNumber` (default: 1)
- `pageSize` (default: 10)
- `filterBy[propertyName]`: Property to filter by
- `filterBy[value]`: Filter value
- `sortFilter[propertyNameSort]`: Property to sort by
- `sortFilter[isAscending]`: Sort direction (true/false)

**Response**:
```json
{
  "expenses": [
    {
      "id": "guid",
      "title": "Lunch",
      "description": "Team lunch",
      "amount": 50.00,
      "createdAt": "2024-01-15T10:30:00Z",
      "createdById": "guid"
    }
  ],
  "totalRows": 100
}
```

---

#### 2. Get Shared Expenses
```http
GET /api/Expense/sharedExpenses?pageNumber=1&pageSize=10
Authorization: Bearer {token}
```

**Description**: Gets expenses shared with the logged-in user

**Response**: Same format as Get All Expenses

---

#### 3. Get Expense Count
```http
GET /api/Expense/count
Authorization: Bearer {token}
```

**Response**:
```json
{
  "totalRows": 100
}
```

---

#### 4. Get Expenses Dropdown
```http
GET /api/Expense/dropdown
Authorization: Bearer {token}
```

**Description**: Gets simplified list for dropdown UI

**Response**:
```json
[
  {
    "id": "guid",
    "title": "Expense Title"
  }
]
```

---

#### 5. Get Expense by ID
```http
GET /api/Expense/{id}
Authorization: Bearer {token}
```

**Response**: Full expense object with related data

---

#### 6. Get Assigned Users
```http
GET /api/Expense/{id}/getAssignedUsers
Authorization: Bearer {token}
```

**Response**:
```json
[
  {
    "expenseId": "guid",
    "userId": "guid",
    "user": {
      "id": "guid",
      "username": "johndoe",
      "email": "john@example.com"
    }
  }
]
```

---

#### 7. Get Documents by Expense ID
```http
GET /api/Expense/docs/{id}
Authorization: Bearer {token}
```

**Response**: List of documents associated with the expense

---

#### 8. Create Expense
```http
POST /api/Expense?title=Team%20Lunch&description=Weekly%20team%20lunch
Authorization: Bearer {token}
```

**Response**:
```json
{
  "id": "guid",
  "title": "Team Lunch",
  "description": "Weekly team lunch",
  "amount": 0,
  "createdAt": "2024-01-15T10:30:00Z",
  "createdById": "guid"
}
```

---

#### 9. Upload Document to Expense
```http
POST /api/Expense/{id}/uploadDoc
Content-Type: multipart/form-data
Authorization: Bearer {token}

file: [binary file]
```

**Response**:
```json
{
  "id": "guid",
  "fileName": "receipt.jpg",
  "s3Url": "https://s3.amazonaws.com/bucket/receipt.jpg",
  "uploadedAt": "2024-01-15T10:30:00Z"
}
```

**Supported Formats**: JPG, JPEG, PNG, PDF, DOCX
**Max File Size**: 10MB

---

#### 10. Add User to Expense
```http
POST /api/Expense/{id}/addUser?userId={userId}
Authorization: Bearer {token}
```

**Response**: 204 No Content on success

---

#### 11. Update Expense
```http
PUT /api/Expense/{id}
Content-Type: application/json
Authorization: Bearer {token}
```

**Request Body**:
```json
{
  "id": "guid",
  "title": "Updated Title",
  "description": "Updated description",
  "amount": 75.50
}
```

**Response**: Updated expense object

---

#### 12. Delete Expense
```http
DELETE /api/Expense/{id}
Authorization: Bearer {token}
```

**Response**: 204 No Content on success

---

#### 13. Get Document Results
```http
GET /api/Expense/{expenseId}/doc/{docId}
Authorization: Bearer {token}
```

**Description**: Gets OCR processing results from AWS Textract

**Response**:
```json
{
  "id": "guid",
  "documentId": "guid",
  "expenseId": "guid",
  "extractedData": {
    "merchant": "Restaurant Name",
    "amount": 50.00,
    "date": "2024-01-15",
    "items": [...]
  },
  "status": "Completed",
  "processedAt": "2024-01-15T10:35:00Z"
}
```

---

### Document Endpoints

#### 1. Upload Document
```http
POST /api/Document/upload
Content-Type: multipart/form-data
Authorization: Bearer {token}

file: [binary file]
```

**Response**: S3 URL of uploaded document

---

#### 2. Download Document
```http
GET /api/Document/download/{fileName}
Authorization: Bearer {token}
```

**Response**: File download

---

#### 3. Get All Downloadable Links
```http
GET /api/Document/downloadLinks
Authorization: Bearer {token}
```

**Response**:
```json
[
  {
    "fileName": "receipt.jpg",
    "s3Url": "https://s3.amazonaws.com/bucket/receipt.jpg",
    "uploadedAt": "2024-01-15T10:30:00Z"
  }
]
```

---

#### 4. Start Textract Processing
```http
POST /api/Document/startTextract?fileName=receipt.jpg&type=Receipt
Authorization: Bearer {token}
```

**Parameters**:
- `fileName`: Name of uploaded file
- `type`: "Receipt" or "Document"

**Response**: Job ID for tracking

---

#### 5. Delete Document
```http
DELETE /api/Document/{id}
Authorization: Bearer {token}
```

**Response**: 204 No Content on success

---

### Friends Endpoints

#### 1. Search User
```http
GET /api/Friends/{searchString}
Authorization: Bearer {token}
```

**Description**: Search user by email or username

**Response**:
```json
{
  "id": "guid",
  "username": "johndoe",
  "email": "john@example.com",
  "firstName": "John",
  "lastName": "Doe"
}
```

---

#### 2. Get Friends
```http
GET /api/Friends/getFriends
Authorization: Bearer {token}
```

**Response**: List of accepted friends

---

#### 3. Get Dropdown Users
```http
GET /api/Friends/getDropdownUsers
Authorization: Bearer {token}
```

**Response**: Simplified user list for dropdown UI

---

#### 4. Send Friend Request
```http
POST /api/Friends/sendRequest
Content-Type: application/json
Authorization: Bearer {token}
```

**Request Body**:
```json
{
  "id": "guid",
  "username": "johndoe"
}
```

**Response**: 204 No Content on success

---

#### 5. Accept Friend Request
```http
POST /api/Friends/acceptRequest
Content-Type: application/json
Authorization: Bearer {token}
```

**Request Body**:
```json
"request-id-guid"
```

**Response**: 204 No Content on success

---

### Notification Endpoints

#### 1. Get Notifications
```http
GET /api/Notification
Authorization: Bearer {token}
```

**Response**:
```json
[
  {
    "id": "guid",
    "userId": "guid",
    "message": "New expense shared with you",
    "type": "ExpenseShare",
    "isRead": false,
    "createdAt": "2024-01-15T10:30:00Z"
  }
]
```

---

#### 2. Mark All as Read
```http
POST /api/Notification/readAll
Authorization: Bearer {token}
```

**Response**: 200 OK on success

---

## Development Best Practices

### Code Organization

1. **Follow the Repository Pattern**: Keep data access logic separate from controllers
2. **Use DTOs**: Never expose domain models to API clients
3. **AutoMapper**: Use AutoMapper for object transformations
4. **Dependency Injection**: Always inject dependencies through constructor

### Database Operations

1. **Use Transactions**: For complex operations involving multiple tables
2. **Async/Await**: Always use async methods for database operations
3. **Dispose Contexts**: DbContext is automatically disposed by DI container
4. **Migrations**: Keep migrations in version control

### API Design

1. **RESTful Principles**: Use proper HTTP verbs and status codes
2. **Consistent Naming**: Use plural nouns for resources (e.g., /Expenses, not /Expense)
3. **Versioning**: Plan for API versioning from the start
4. **Pagination**: Always paginate list endpoints
5. **Filtering and Sorting**: Support filtering and sorting where appropriate

### Security

1. **Authentication**: Always use `[Authorize]` on protected endpoints
2. **Input Validation**: Validate all user input
3. **Error Messages**: Don't expose sensitive information in error messages
4. **CORS**: Configure CORS properly for frontend access

### Testing

1. **Unit Tests**: Test repositories and business logic
2. **Integration Tests**: Test API endpoints
3. **Swagger**: Use Swagger UI for manual testing
4. **Postman**: Create collections for API testing

### Performance

1. **Eager Loading**: Use `.Include()` to load related data efficiently
2. **Select Optimization**: Use `.Select()` to project only needed fields
3. **Caching**: Use Redis for frequently accessed data
4. **Connection Pooling**: EF Core manages this automatically

### Logging

1. **Structured Logging**: Use Serilog for structured logging
2. **Log Levels**: Use appropriate log levels (Info, Warning, Error)
3. **Exception Logging**: Always log exceptions with context
4. **Performance Logging**: Log slow operations

---

## Common Development Commands

```bash
# Build the project
dotnet build

# Run the application
dotnet run

# Add a new migration
dotnet ef migrations add MigrationName --context UserDocumentsDbContext

# Apply migrations
dotnet ef database update --context UserDocumentsDbContext

# Revert last migration
dotnet ef database update previous-migration --context UserDocumentsDbContext

# Remove last migration (if not applied)
dotnet ef migrations remove --context UserDocumentsDbContext

# View generated SQL for a migration
dotnet ef migrations script --context UserDocumentsDbContext

# Run tests
dotnet test

# Clean build artifacts
dotnet clean

# Restore packages
dotnet restore
```

---

## Troubleshooting

### Migration Issues

**Problem**: Migration fails to apply
```bash
# Solution: Check pending migrations
dotnet ef migrations list --context UserDocumentsDbContext

# View migration script
dotnet ef migrations script --context UserDocumentsDbContext
```

### Database Connection Issues

**Problem**: Cannot connect to database
- Check connection string in `appsettings.json`
- Ensure SQL Server is running
- Verify database exists

### AWS Service Issues

**Problem**: S3/Textract calls fail
- Verify AWS credentials in Secrets Manager
- Check IAM permissions
- Ensure service region is correct

### Redis Connection Issues

**Problem**: Redis connection fails
- Check Redis server is running
- Verify connection string
- Ensure Redis is accessible from application

---

## Additional Resources

- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
- [Entity Framework Core Documentation](https://docs.microsoft.com/ef/core)
- [AWS SDK for .NET](https://docs.aws.amazon.com/sdk-for-net/)
- [AutoMapper Documentation](https://automapper.org/)
- [Serilog Documentation](https://serilog.net/)

---

Last Updated: January 2026