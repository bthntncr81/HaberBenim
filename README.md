# Haber Platform

A modern news aggregation platform with a .NET 8 backend and Angular frontends.

## 📋 Prerequisites

Before you begin, ensure you have the following installed:

- **Node.js** 20+ (with npm)
- **.NET SDK** 8.0+
- **Docker** (with Docker Compose)

### Verify installations:

```bash
node --version    # Should be v20.x or higher
dotnet --version  # Should be 8.x or higher
docker --version  # Should show Docker version
```

## 🏗️ Project Structure

```
haber-platform/
├── apps/
│   ├── api/                    # .NET 8 Web API
│   │   └── HaberPlatform.Api/
│   ├── admin-web/              # Angular Admin Dashboard
│   └── public-web/             # Angular Public Website
├── infra/
│   └── docker-compose.yml      # PostgreSQL setup
├── docs/                       # Documentation
├── tools/
│   └── scripts/                # Utility scripts
├── HaberPlatform.sln           # .NET Solution file
└── README.md
```

## 🚀 Getting Started

### 1. Start PostgreSQL Database

```bash
cd infra
docker compose up -d
```

Verify PostgreSQL is running:

```bash
docker compose ps
```

PostgreSQL connection details:

- **Host**: localhost
- **Port**: 5432
- **Database**: haber_platform
- **User**: haber
- **Password**: haber123

### 2. Run the API

```bash
cd apps/api/HaberPlatform.Api
dotnet run
```

The API will be available at: **http://localhost:5078**

API Endpoints:

- `GET /health` - Health check
- `GET /api/v1/version` - API version info
- `GET /swagger` - Swagger UI (Development only)

### 3. Run Admin Web (Angular)

```bash
cd apps/admin-web
npm install
npm start
```

Admin dashboard will be available at: **http://localhost:4200**

Routes:

- `/login` - Login page (placeholder)
- `/feed` - News feed
- `/sources` - Source management
- `/rules` - Content rules

### 4. Run Public Web (Angular)

```bash
cd apps/public-web
npm install
npm start
```

Public website will be available at: **http://localhost:4201**

## 🔗 Quick Reference

| Service    | URL                           |
| ---------- | ----------------------------- |
| API        | http://localhost:5078         |
| Swagger UI | http://localhost:5078/swagger |
| Admin Web  | http://localhost:4200         |
| Public Web | http://localhost:4201         |
| PostgreSQL | localhost:5432                |

## 🛠️ Development Commands

### API (.NET)

```bash
# Build the solution
dotnet build

# Run the API
cd apps/api/HaberPlatform.Api
dotnet run

# Run in watch mode
dotnet watch run
```

### Frontend (Angular)

```bash
# Admin Web
cd apps/admin-web
npm install     # Install dependencies
npm start       # Start dev server on port 4200
npm run build   # Production build

# Public Web
cd apps/public-web
npm install     # Install dependencies
npm start       # Start dev server on port 4201
npm run build   # Production build
```

### Database

```bash
# Start PostgreSQL
cd infra && docker compose up -d

# Stop PostgreSQL
cd infra && docker compose down

# Stop and remove data volume
cd infra && docker compose down -v

# View logs
cd infra && docker compose logs -f postgres
```

## 📊 Database Schema

The initial schema includes:

### SystemSetting

| Column       | Type     | Description          |
| ------------ | -------- | -------------------- |
| Id           | UUID     | Primary key          |
| Key          | string   | Setting key (unique) |
| Value        | string   | Setting value        |
| CreatedAtUtc | DateTime | Creation timestamp   |

## 🔧 Configuration

### API Configuration (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=haber_platform;Username=haber;Password=haber123"
  }
}
```

### CORS Policy

The API has a CORS policy named "AdminCors" that allows requests from `http://localhost:4200`.

## 📁 Sprint Roadmap

- [x] **Sprint 0**: Project scaffolding (current)
- [ ] **Sprint 1**: Authentication & Source Management
- [ ] **Sprint 2**: News Feed & RSS Integration
- [ ] **Sprint 3**: Rules Engine & Filtering

## 🔍 Troubleshooting

### npm EACCES Permission Error

If you encounter permission errors when running `npm install`:

```bash
npm ERR! EACCES: permission denied
```

Fix by running:

```bash
sudo chown -R $(whoami) ~/.npm
```

### PostgreSQL Connection Issues

1. Ensure Docker is running
2. Check if the container is up: `docker ps`
3. View logs: `cd infra && docker compose logs postgres`

## 📝 Notes

- This is Sprint 0 scaffolding - all features are placeholders
- Angular apps use standalone components (no NgModules)
- EF Core is configured but no migrations have been run yet

## 📄 License

Private - All rights reserved.
