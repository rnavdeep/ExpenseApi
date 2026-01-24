# Docker Setup Guide for ExpenseApi

This guide provides detailed instructions for running the ExpenseApi application with Docker and Docker Compose.

## Prerequisites

- **Docker**: Version 20.10 or higher
- **Docker Compose**: Version 2.0 or higher
- **AWS Account**: With access to S3 and Textract services
- **At least 8GB RAM**: Recommended for running all containers

## Quick Start

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd ExpenseApi
   ```

2. **Configure environment variables**
   ```bash
   cp .env.example .env
   ```

3. **Edit `.env` file** with your credentials:
   - Set strong passwords for SQL Server databases
   - Provide your AWS credentials
   - Configure AWS region and S3 bucket name

4. **Build and start all services**
   ```bash
   docker-compose up --build
   ```

5. **Access the applications**
   - Frontend: http://localhost:8080
   - Backend API (Swagger): http://localhost:5223/swagger

## Environment Variables

Create a `.env` file in the project root with the following variables:

```bash
# SQL Server Database Passwords
SQL_SERVER_DEMO_SA_PASSWORD=YourStrong@Password123
AUTH_DB_SA_PASSWORD=YourStrong@Password456

# AWS Configuration
AWS_ACCESS_KEY_ID=your-aws-access-key-id
AWS_SECRET_ACCESS_KEY=your-aws-secret-access-key
AWS_REGION=us-east-1
AWS_BUCKET=your-expense-receipts-bucket

# JWT Configuration (Optional - defaults provided)
JWT_KEY=your-super-secret-jwt-key-that-is-at-least-32-characters-long
JWT_ISSUER=ExpenseApi
JWT_AUDIENCE=ExpenseApiUsers
```

## Services Overview

The docker-compose setup includes the following services:

### 1. SQL Server for Application Data (`sql_server_demo`)
- **Image**: `mcr.microsoft.com/mssql/server:latest`
- **Port**: 1433
- **Database**: ExpenseAnalyserDb
- **Health Check**: Verifies SQL Server is ready to accept connections
- **Volume**: Persists database data in `sqlserver_data`

### 2. SQL Server for Authentication (`authenticationDB`)
- **Image**: `mcr.microsoft.com/mssql/server:2019-latest`
- **Port**: 1434
- **Database**: AuthenticationDb
- **Health Check**: Verifies SQL Server is ready to accept connections

### 3. Redis Cache (`redis`)
- **Image**: `redis:latest`
- **Port**: 6379
- **Purpose**: Session management and token storage
- **Health Check**: Pings Redis server

### 4. Backend API (`backend-server`)
- **Image**: Built from Dockerfile (`expense-api:latest`)
- **Port**: 5223 (internal port: 80)
- **Health Check**: Verifies Swagger UI is accessible
- **Environment Variables**:
  - Database connection strings
  - AWS credentials
  - JWT configuration
  - Redis connection

### 5. Frontend (`frontend`)
- **Image**: Pre-built `expense-analyser:latest`
- **Port**: 8080
- **Purpose**: User interface for the expense management system

## Docker Compose Commands

### Start all services
```bash
docker-compose up
```

### Build and start all services
```bash
docker-compose up --build
```

### Start in detached mode (background)
```bash
docker-compose up -d
```

### Stop all services
```bash
docker-compose down
```

### Stop and remove volumes (clears data)
```bash
docker-compose down -v
```

### View logs
```bash
# All services
docker-compose logs

# Specific service
docker-compose logs backend-server

# Follow logs in real-time
docker-compose logs -f backend-server
```

### Restart a specific service
```bash
docker-compose restart backend-server
```

## Troubleshooting

### Port Already in Use

If you see port conflicts, modify the port mappings in `docker-compose.yml`:

```yaml
ports:
  - "5224:80"  # Change 5223 to 5224
```

### Database Connection Issues

1. Check if SQL Server containers are healthy:
   ```bash
   docker-compose ps
   ```

2. View SQL Server logs:
   ```bash
   docker-compose logs sql_server_demo
   docker-compose logs authenticationDB
   ```

3. Ensure database passwords match in `.env` file

### AWS Credentials Issues

1. Verify AWS credentials are correct in `.env` file
2. Ensure AWS credentials have proper permissions for S3 and Textract
3. Check that S3 bucket exists and is accessible

### Backend Service Not Starting

1. Check backend logs:
   ```bash
   docker-compose logs backend-server
   ```

2. Verify all dependencies are healthy:
   ```bash
   docker-compose ps
   ```

3. Rebuild the backend service:
   ```bash
   docker-compose up --build backend-server
   ```

### Health Check Failures

If health checks fail, increase the `start_period` in `docker-compose.yml`:

```yaml
healthcheck:
  start_period: 120s  # Increase from 60s
```

## Database Initialization

The SQL Server containers will automatically execute scripts from the `ExpenseAnalyserDbScripts` directory when they start. The scripts are mounted as a volume and will run in order:

1. `01-create_db.sql` - Creates the database
2. `02-user.sql` - Creates user tables
3. `03-expenses.sql` - Creates expense tables
4. `04-documents.sql` - Creates document tables
5. `05-expense_users.sql` - Creates expense-user relationships
6. `06-document_job_results.sql` - Creates document job results
7. `07-notification.sql` - Creates notification tables

## Production Deployment

For production deployment, consider these security enhancements:

1. **Use secrets management**: Store sensitive data in Docker Secrets or AWS Secrets Manager
2. **Remove debug ports**: Don't expose SQL Server ports externally
3. **Use HTTPS**: Configure SSL/TLS certificates
4. **Resource limits**: Add memory and CPU limits to containers
5. **Health checks**: Implement more sophisticated health checks
6. **Logging**: Configure centralized logging (e.g., ELK stack)
7. **Monitoring**: Add monitoring tools (e.g., Prometheus, Grafana)

Example production configuration additions:

```yaml
backend-server:
  deploy:
    resources:
      limits:
        cpus: '1'
        memory: 1G
      reservations:
        cpus: '0.5'
        memory: 512M
  restart: unless-stopped
```

## AWS Setup Guide

### Create S3 Bucket

1. Go to AWS S3 Console
2. Create a new bucket with a unique name
3. Configure bucket settings:
   - Block public access: Off (or configure properly for your use case)
   - Versioning: Optional
   - Encryption: Optional but recommended

4. Add bucket policy for your application:
   ```json
   {
     "Version": "2012-10-17",
     "Statement": [
       {
         "Effect": "Allow",
         "Principal": {
           "AWS": "arn:aws:iam::<account-id>:user/<your-iam-user>"
         },
         "Action": [
           "s3:PutObject",
           "s3:GetObject",
           "s3:DeleteObject"
         ],
         "Resource": "arn:aws:s3:::<bucket-name>/*"
       }
     ]
   }
   ```

### Create IAM User

1. Go to AWS IAM Console
2. Create a new user with programmatic access
3. Attach policy with S3 and Textract permissions:
   ```json
   {
     "Version": "2012-10-17",
     "Statement": [
       {
         "Effect": "Allow",
         "Action": [
           "s3:*",
           "textract:*"
         ],
         "Resource": "*"
       }
     ]
   }
   ```

4. Save Access Key ID and Secret Access Key for `.env` file

## Support

For issues or questions:
- Check the logs: `docker-compose logs`
- Verify all services are healthy: `docker-compose ps`
- Review environment variables in `.env` file
- Ensure AWS credentials have proper permissions

## Useful Docker Commands

```bash
# List all containers
docker ps -a

# Execute command in running container
docker-compose exec backend-server bash

# View container resource usage
docker stats

# Clean up unused resources
docker system prune -a

# Rebuild specific service
docker-compose build backend-server
```

## Network Architecture

All services are connected to a custom bridge network named `expense-network`. This allows:
- Services to communicate using container names
- Isolation from external networks
- Easy service discovery

Service communication examples:
- Backend → SQL Server: `sql_server_demo,1433`
- Backend → Redis: `redis:6379`
- Backend → Authentication DB: `authenticationDB,1433`
- Frontend → Backend: `http://backend-server:80` (internal)
- External → Backend: `http://localhost:5223` (mapped port)