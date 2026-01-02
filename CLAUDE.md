# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Installation and Setup

### Prerequisites

Before starting development, ensure you have:

- **Operating System**: Windows 10/11, macOS 10.15+, or Linux (Ubuntu 18.04+)
- **Git**: Version 2.30 or later
- **Available Ports**: 1433, 5672, 6379, 15672, 5341, 7111 (ensure not in use)
- **Memory**: At least 8GB RAM (16GB recommended for smooth Docker operations)

### IDE Setup

Choose one of the following IDEs:

#### Visual Studio Code
- Install from [code.visualstudio.com](https://code.visualstudio.com/)
- **Required Extensions**:
  - C# Dev Kit
  - Docker
  - REST Client (for API testing)
- **Recommended Extensions**:
  - GitLens
  - Thunder Client
  - Entity Framework Core Power Tools

#### Visual Studio 2022 or later
- Install Community, Professional, or Enterprise edition
- **Required Workloads**:
  - ASP.NET and web development
  - .NET desktop development
- **Required Extensions**:
  - Docker Desktop integration

#### JetBrains Rider
- Install from [jetbrains.com/rider](https://www.jetbrains.com/rider/)
- Built-in support for .NET, Docker, and database tools

### .NET 9.0 SDK

1. Install .NET 9.0 SDK from the [official website](https://dotnet.microsoft.com/download/dotnet/9.0)
2. Verify installation:
   ```bash
   dotnet --version
   # Should output: 9.0.xxx
   ```
3. Verify SDK is available:
   ```bash
   dotnet --list-sdks
   # Should include 9.0.xxx
   ```

### Docker Desktop

1. **Install Docker Desktop** from [docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop)
   - **Windows**: Requires WSL2 enabled
   - **macOS**: Docker Desktop 4.0+ recommended
   - **Linux**: Can use Docker Desktop or Docker Engine + Docker Compose plugin

2. **Docker Compose Included**: Docker Desktop includes Docker Compose v2 built-in (no separate installation needed)

3. **System Requirements**:
   - Windows: WSL2 enabled
   - macOS: Docker Desktop 4.0+
   - Linux: Docker Engine 20.10+

4. **Configuration**:
   - Allocate at least 4GB RAM to Docker (6GB+ recommended)
   - Enable Kubernetes (optional, for advanced scenarios)

5. **Verify installation**:
   ```bash
   docker --version
   docker compose version  # Modern syntax (recommended)
   ```

#### Docker Compose Command Syntax

This project uses **Docker Compose v2** syntax (included with Docker Desktop):

```bash
# Modern syntax (use this)
docker compose up -d
docker compose down
docker compose ps

# Legacy syntax (avoid if possible)
docker-compose up -d
docker-compose down
docker-compose ps
```

#### Linux Alternative (Without Docker Desktop)

If you prefer Docker Engine on Linux:

```bash
# Install Docker Engine
sudo apt update
sudo apt install docker.io

# Install Docker Compose plugin
sudo apt install docker-compose-plugin

# Verify
docker compose version
```

### Repository Setup

1. **Clone the repository**:
   ```bash
   git clone <repository-url>
   cd collateral-appraisal-system-api
   ```

2. **Restore dependencies**:
   ```bash
   dotnet restore collateral-appraisal-system-api.sln
   ```

### Infrastructure Services Setup

The project uses Docker Compose to run required infrastructure services:

1. **Start all services**:
   ```bash
   docker compose up -d
   ```

2. **Services included**:
   - **SQL Server** (port 1433): Main database
     - Username: `sa`
     - Password: `P@ssw0rd`
   - **Redis** (port 6379): Caching layer
   - **RabbitMQ** (ports 5672, 15672): Message broker
     - Username: `admin`
     - Password: `P@ssw0rd`
   - **Seq** (port 5341): Structured logging
     - Username: `admin`
     - Password: `P@ssw0rd`

3. **Verify services are running**:
   ```bash
   docker compose ps
   ```

### Database Setup

1. **Run initial migrations**:
   ```bash
   # For Request module
   dotnet ef database update --project Modules/Request/Request --startup-project Bootstrapper/Api
   
   # For Auth module (if migrations exist)
   dotnet ef database update --project Modules/Auth/Auth --startup-project Bootstrapper/Api
   ```

2. **Verify database connection**:
   - Connect to SQL Server using your preferred tool
   - Server: `localhost,1433`
   - Database: `CollateralAppraisal`
   - Username: `sa`
   - Password: `P@ssw0rd`

### Configuration

The project uses different configuration files for different environments:

- **Development**: `appsettings.Development.json` (pre-configured)
- **Production**: `appsettings.json` (requires actual connection strings)

Default development configuration includes:
- Database: SQL Server on localhost with sa/P@ssw0rd
- Redis: localhost:6379
- RabbitMQ: localhost:5672 with admin/P@ssw0rd

### Build and Run

1. **Build the solution**:
   ```bash
   dotnet build
   ```

2. **Run the application**:
   ```bash
   dotnet run --project Bootstrapper/Api
   ```

3. **Access the application**:
   - API: https://localhost:7111
   - OpenAPI/Swagger: https://localhost:7111/openapi/v1.json (in development)

### Verification Steps

After setup, verify everything works:

1. **Check application startup**:
   ```bash
   curl https://localhost:7111/requests
   # Should return empty array or list of requests
   ```

2. **Access management interfaces**:
   - RabbitMQ Management: http://localhost:15672 (admin/P@ssw0rd)
   - Seq Logging: http://localhost:5341 (admin/P@ssw0rd)

3. **Test API endpoints**:
   ```bash
   # Create a test request
   curl -X POST https://localhost:7111/requests \
     -H "Content-Type: application/json" \
     -d '{"requestDetail": {"amount": 100000, "propertyType": "Residential"}}'
   ```

### Common Issues and Solutions

#### Docker Issues
- **Port conflicts**: Check if ports 1433, 5672, 6379, 15672, 5341 are available
- **Memory issues**: Increase Docker Desktop memory allocation to 6GB+
- **Windows**: Ensure WSL2 is properly configured

#### Database Issues
- **Connection failed**: Verify SQL Server container is running: `docker ps`
- **Migration errors**: Ensure you're running from the project root directory
- **Permission denied**: Try running with elevated privileges

#### Build Issues
- **SDK not found**: Verify .NET 9.0 SDK installation with `dotnet --list-sdks`
- **Package restore fails**: Clear NuGet cache: `dotnet nuget locals all --clear`

#### Runtime Issues
- **Port 7111 in use**: Change port in `launchSettings.json` or kill existing process
- **Authentication errors**: Verify OpenIddict configuration and database migrations

### Development Workflow

Daily development process:

1. **Start infrastructure** (if not running):
   ```bash
   docker compose up -d
   ```

2. **Pull latest changes**:
   ```bash
   git pull origin main
   dotnet restore
   ```

3. **Run migrations** (if new ones exist):
   ```bash
   dotnet ef database update --project Modules/Request/Request --startup-project Bootstrapper/Api
   ```

4. **Start development**:
   ```bash
   dotnet run --project Bootstrapper/Api
   ```

5. **Stop infrastructure** (when done):
   ```bash
   docker compose down
   ```

## Common Commands

### Development Setup

```bash
# Start infrastructure services (SQL Server, Redis, RabbitMQ, Seq)
docker compose up -d

# Build the solution
dotnet build

# Run the main API
dotnet run --project Bootstrapper/Api

# Run tests (if any exist)
dotnet test
```

### Database Operations

```bash
# Add new migration for a specific module
dotnet ef migrations add <MigrationName> --project Modules/Request/Request --startup-project Bootstrapper/Api

# Update database
dotnet ef database update --project Modules/Request/Request --startup-project Bootstrapper/Api
```

### Project Structure Commands

```bash
# Build specific module
dotnet build Modules/Request/Request

# Clean and rebuild
dotnet clean && dotnet build
```

## Architecture Overview

### Modular Monolith Design

- **Bootstrapper/Api**: Main entry point and configuration
- **Modules/**: Business domains as separate modules
  - **Request**: Core request management with full CRUD operations
  - **Auth**: Authentication with dual OAuth2/Keycloak support
  - **Notification**: Event-driven messaging and notifications
  - **Collateral**: Collateral management (minimal implementation)
- **Shared/**: Cross-cutting concerns and infrastructure

### Technology Stack

- **.NET 9.0** with ASP.NET Core
- **Carter** for minimal API endpoints
- **Entity Framework Core** with SQL Server
- **MediatR** for CQRS pattern
- **MassTransit** + RabbitMQ for messaging
- **Redis** for caching
- **OpenIddict** + Keycloak for authentication
- **Serilog** for structured logging

### Domain-Driven Design Patterns

- **Aggregates**: `Request` aggregate with encapsulated business logic
- **Value Objects**: `RequestDetail`, `Address`, `Contact`, `LoanDetail`, etc.
- **Domain Events**: `RequestCreatedEvent` with automatic dispatch
- **Repositories**: Interface-based with caching decorators
- **CQRS**: Commands for writes, queries for reads

### Key Architectural Patterns

#### Module Registration

Each module follows this pattern:

```csharp
services.AddRequestModule(configuration); // Register services
app.UseRequestModule(); // Configure middleware
```

#### Repository Pattern with Caching

```csharp
services.AddScoped<IRequestRepository, RequestRepository>();
services.Decorate<IRequestRepository, CachedRequestRepository>();
```

#### Carter Endpoints

API endpoints are defined using Carter modules:

```csharp
public class CreateRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app) { /* ... */ }
}
```

#### DbContext per Module

- `RequestDbContext` for Request module
- `OpenIddictDbContext` for Auth module
- Each with custom interceptors for auditing and domain events

### Configuration

- **appsettings.json** contains:
  - SQL Server connection string (needs actual values)
  - Redis connection
  - RabbitMQ settings
  - Keycloak realm configuration
  - Serilog logging configuration

### Development Infrastructure

- **docker-compose.yml** provides all required services:
  - SQL Server (port 1433)
  - Redis (port 6379)
  - RabbitMQ (port 5672, management UI 15672)
  - Keycloak (port 8080)
  - Seq logging (port 5341)

### Authentication Strategy

- **OpenIddict**: Self-hosted OAuth2/OpenID Connect server

### Database Schema

- Each module uses its own schema namespace
- Automatic audit fields (`CreatedOn`, `CreatedBy`, `UpdatedOn`, `UpdatedBy`)
- Global conventions applied via `ModelBuilderExtension`

## Important Notes

- The main API project is in `Bootstrapper/Api`
- Each module is self-contained with its own DbContext and migrations
- Domain events are automatically dispatched during save operations
- All repositories use the decorator pattern for caching
- CQRS commands and queries are handled via MediatR
- Integration events flow through MassTransit/RabbitMQ
- Connection strings in appsettings.json need actual values for database access
