# ExpenseApi

## Business Case

ExpenseApi is a comprehensive expense management and receipt scanning platform designed to simplify financial tracking and sharing among users. The platform enables individuals and groups to:

1. **Receipt Scanning & Analysis**: Upload receipt images (JPG, JPEG, PNG, PDF, DOCX) and automatically extract expense data using AWS Textract AI-powered OCR technology
2. **Expense Management**: Create, update, and track expenses with detailed descriptions, amounts, and associated documents
3. **Collaborative Sharing**: Share expenses with friends and colleagues, enabling group expense tracking and transparency
4. **Real-time Notifications**: Receive instant notifications when expenses are shared with you or when document processing completes
5. **Document Management**: Store and manage receipt documents securely in AWS S3 with easy download capabilities

**Target Users**: Individuals managing personal expenses, roommates splitting bills, teams tracking shared expenses, and anyone needing organized expense tracking with receipt documentation.

## Technical Overview

### Architecture

ExpenseApi follows a **Repository Pattern** with **Code-First Entity Framework** approach, implementing a clean separation of concerns across multiple layers:

- **Controllers**: Handle HTTP requests and responses
- **Repositories**: Encapsulate data access logic
- **Domain Models**: Represent database entities
- **DTOs**: Data Transfer Objects for API contracts
- **Mappings**: AutoMapper for object transformations
- **Middleware**: Cross-cutting concerns (exception handling, authentication)

### Technology Stack

**Core Framework:**
- **ASP.NET Core 7.0**: Modern, high-performance web framework
- **.NET 7.0**: Latest LTS runtime for enterprise applications

**Database & ORM:**
- **SQL Server**: Relational database for persistent storage
- **Entity Framework Core 7.0**: ORM with Code-First migrations
- **Microsoft.AspNetCore.Identity**: User authentication and authorization

**Cloud Services (AWS):**
- **AWS S3**: Secure document storage for receipts
- **AWS Textract**: AI-powered OCR for receipt data extraction
- **AWS Secrets Manager**: Secure configuration management

**Authentication & Security:**
- **JWT (JSON Web Tokens)**: Stateless authentication
- **Identity Framework**: Role-based access control (Reader, Writer, Admin)
- **ASP.NET Core Authorization**: Policy-based security

**Caching & Real-time:**
- **Redis**: Distributed caching for session management and token storage
- **SignalR**: Real-time notifications for expense sharing and document processing

**Logging & Monitoring:**
- **Serilog**: Structured logging with multiple sinks (Console, File)
- **Custom Exception Middleware**: Centralized error handling

**API Documentation:**
- **Swagger/OpenAPI**: Interactive API documentation and testing interface
- **API Versioning**: Version control for API endpoints

**Utilities:**
- **AutoMapper**: Object-to-object mapping
- **Newtonsoft.Json**: JSON serialization/deserialization

### Database Structure

The application uses two database contexts:

1. **ExpenseAuthDbContext**: Handles ASP.NET Identity tables (Users, Roles, Claims)
2. **UserDocumentsDbContext**: Contains application-specific tables:
   - **Users**: Extended user profile information
   - **Expenses**: Expense records with metadata
   - **Documents**: Receipt and document storage
   - **ExpenseUsers**: Many-to-many relationship between expenses and users
   - **DocumentJobResults**: Results from AWS Textract processing
   - **Notifications**: User notifications
   - **FriendRequests**: Friend request relationships

### Key Features

1. **User Authentication & Authorization**
   - User registration and login with role-based access
   - JWT token-based authentication
   - Session management with Redis caching

2. **Document Processing**
   - Upload documents to AWS S3
   - Automated OCR processing with AWS Textract
   - Background job polling for processing status
   - Real-time notifications when processing completes

3. **Expense Management**
   - CRUD operations for expenses
   - Pagination, filtering, and sorting
   - Document attachment to expenses
   - User assignment and sharing

4. **Social Features**
   - Friend request system
   - Expense sharing between users
   - Real-time notifications via SignalR

5. **API Features**
   - RESTful API design
   - Comprehensive input validation
   - Error handling middleware
   - Interactive Swagger documentation

### Getting Started

#### Option 1: Running with Docker (Recommended)

**Prerequisites:**
- Docker and Docker Compose installed
- AWS Account with S3, Textract access

**Setup Instructions:**
1. Clone the repository
2. Copy the example environment file and configure your settings:
   ```bash
   cp .env.example .env
   ```
3. Edit `.env` file and provide:
   - SQL Server passwords for both databases
   - AWS credentials (Access Key ID, Secret Access Key)
   - AWS Region and S3 bucket name
   - Optional: JWT configuration

4. Build and start all services:
   ```bash
   docker-compose up --build
   ```

5. Access the applications:
   - **Frontend**: http://localhost:8080
   - **Backend API (Swagger)**: http://localhost:5223/swagger

6. To stop all services:
   ```bash
   docker-compose down
   ```

**Docker Services:**
- `sql_server_demo`: SQL Server for application data (port 1433)
- `authenticationDB`: SQL Server for authentication (port 1434)
- `redis`: Redis cache for session management (port 6379)
- `backend-server`: ASP.NET Core API (port 5223)
- `frontend`: Pre-built frontend application (port 8080)

#### Option 2: Running Locally with .NET

**Prerequisites:**
- .NET 7.0 SDK
- SQL Server (or Azure SQL Database)
- Redis server (or Azure Cache for Redis)
- AWS Account with S3, Textract, and Secrets Manager access
- Node.js (for frontend, if applicable)

**Setup Instructions:**
1. Clone the repository
2. Configure AWS credentials and secrets in AWS Secrets Manager
3. Update connection strings in `appsettings.json`
4. Run database migrations: `dotnet ef database update`
5. Run the application: `dotnet run`
6. Access Swagger UI at `https://localhost:5001/swagger`

### API Documentation

Interactive API documentation is available via Swagger UI when the application is running. The API includes endpoints for:

- **Authentication**: Register, Login, Logout, Session validation
- **Expenses**: Create, Read, Update, Delete, Share expenses
- **Documents**: Upload, Download, Delete, Process with Textract
- **Friends**: Search users, Send/Accept friend requests
- **Notifications**: Get notifications, Mark as read

### Project Structure

```
ExpenseApi/
├── Controllers/          # API endpoints
├── Data/                # Database contexts
├── Models/
│   ├── Domain/         # Database entities
│   └── DTO/            # Data transfer objects
├── Repositories/       # Data access layer
├── Mappings/          # AutoMapper profiles
├── Middlewares/       # Custom middleware
├── Configurations/    # Service configurations
├── CustomActionFilters/ # Request validation
└── Migrations/        # EF Core migrations
```

### Security Considerations

- JWT tokens stored in HttpOnly cookies
- Password hashing with ASP.NET Identity
- Role-based authorization on sensitive endpoints
- AWS credentials stored in Secrets Manager
- CORS configuration for cross-origin requests
- File upload validation (type and size limits)

### Performance Optimization

- Redis caching for frequently accessed data
- Lazy loading in Entity Framework
- Pagination for large datasets
- Background job processing for document OCR
- Connection pooling for database access

### Future Enhancements

- Expense categories and tagging
- Budget tracking and alerts
- Export to CSV/PDF reports
- Mobile app integration
- Recurring expenses
- Multi-currency support
- Advanced analytics and reporting
