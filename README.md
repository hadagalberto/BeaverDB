# BeaverDB - Database Management Panel

BeaverDB is a simple, web-based database management panel similar to PHPMyAdmin, designed to manage multiple database servers including MySQL, PostgreSQL, SQL Server, Redis, and MongoDB. It supports Docker container management for easy database deployment.

## Features

- 🔐 **Secure Authentication** - JWT-based authentication with admin initialization
- 🗄️ **Multi-Database Support** - MySQL, PostgreSQL, SQL Server, MongoDB, Redis
- 🐳 **Docker Integration** - Create and manage database containers directly from the UI
- 📊 **Database Management** - Create, delete, and manage databases
- 📝 **Query Execution** - Execute SQL queries and view results
- 🎨 **Modern UI** - Built with React, TypeScript, and TailwindCSS

## Tech Stack

### Backend
- **.NET 10** - Web API
- **Entity Framework Core** - ORM with PostgreSQL
- **JWT Authentication** - Secure token-based auth
- **Docker.DotNet** - Docker container management
- **Database Drivers**:
  - MySqlConnector
  - Npgsql (PostgreSQL)
  - Microsoft.Data.SqlClient (SQL Server)
  - MongoDB.Driver
  - StackExchange.Redis

### Frontend
- **React 18** with **TypeScript**
- **Vite** - Build tool
- **TailwindCSS** - Styling
- **React Router** - Navigation
- **Axios** - HTTP client

### Infrastructure
- **Docker & Docker Compose** - Containerization
- **PostgreSQL** - Internal database
- **Nginx** - Frontend web server

## Quick Start

### Prerequisites
- Docker and Docker Compose installed
- .NET 10 SDK (for local development)
- Node.js 20+ (for local development)

### Running with Docker Compose

1. Clone the repository:
```bash
git clone <repository-url>
cd BeaverDB
```

2. Start all services:
```bash
docker-compose up -d
```

3. Access the application:
- Frontend: http://localhost:3000
- Backend API: http://localhost:5000

4. Initialize admin account:
- On first visit, you'll be prompted to create an admin account
- Enter username, email, and password
- Click "Create Admin"

### Running Locally (Development)

#### Backend
```bash
cd backend/BeaverDB.API
dotnet restore
dotnet ef database update
dotnet run
```

The API will be available at http://localhost:5000

#### Frontend
```bash
cd frontend
npm install
npm run dev
```

The frontend will be available at http://localhost:5173

## Configuration

### Backend Configuration

Edit `backend/BeaverDB.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=beaverdb;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Key": "your-secret-key-here",
    "Issuer": "BeaverDB",
    "Audience": "BeaverDB"
  },
  "Encryption": {
    "Key": "your-encryption-key-32-chars",
    "IV": "your-iv-16-chars"
  }
}
```

### Frontend Configuration

Edit `frontend/.env`:

```
VITE_API_URL=http://localhost:5000/api
```

## Usage Guide

### Adding a Database Server

1. **External Server** (already running):
   - Click "Add Server"
   - Enter server details (name, type, host, port, credentials)
   - Uncheck "Managed by Docker"
   - Click "Create"

2. **Docker Managed Server**:
   - Click "Add Server"
   - Enter server details
   - Check "Managed by Docker"
   - Click "Create"
   - BeaverDB will automatically create and start a Docker container

### Managing Databases

1. Click on a server card
2. Navigate to "Databases" tab
3. Create, view, or delete databases
4. Click on a database to view tables

### Executing Queries

1. Select a server and database
2. Navigate to "Query" tab
3. Enter your SQL query
4. Click "Execute"
5. View results in the table below

## Security Considerations

⚠️ **Important Security Notes:**

- **Never expose this panel directly to the internet** without additional security measures
- Use strong passwords for all accounts
- Change default JWT and encryption keys in production
- Use environment variables for sensitive configuration
- Consider using a reverse proxy (Nginx/Traefik) with HTTPS
- Restrict access via VPN or firewall rules
- Database credentials are encrypted but stored in the internal database

## API Endpoints

### Authentication
- `POST /api/auth/login` - Login
- `POST /api/auth/init` - Initialize admin (first time only)
- `GET /api/auth/check-init` - Check if initialized
- `GET /api/auth/me` - Get current user

### Servers
- `GET /api/servers` - List all servers
- `POST /api/servers` - Create server
- `GET /api/servers/{id}` - Get server details
- `PUT /api/servers/{id}` - Update server
- `DELETE /api/servers/{id}` - Delete server
- `POST /api/servers/{id}/test-connection` - Test connection
- `POST /api/servers/{id}/start` - Start Docker container
- `POST /api/servers/{id}/stop` - Stop Docker container
- `GET /api/servers/{id}/status` - Get container status

### Databases
- `GET /api/servers/{id}/databases` - List databases
- `POST /api/servers/{id}/databases` - Create database
- `DELETE /api/servers/{id}/databases/{name}` - Delete database
- `GET /api/servers/{id}/databases/{name}/tables` - List tables
- `GET /api/servers/{id}/databases/{name}/tables/{table}/schema` - Get table schema
- `POST /api/servers/{id}/databases/{name}/query` - Execute query

## Docker Container Management

BeaverDB can automatically create and manage Docker containers for:

- **MySQL 8** (port 3306)
- **PostgreSQL 16** (port 5432)
- **SQL Server 2022** (port 1433)
- **MongoDB** (port 27017)
- **Redis** (port 6379)

Containers are created with:
- Automatic restart policy
- Port mapping to host
- Default credentials (configurable)
- Persistent volumes (optional)

## Troubleshooting

### Backend won't start
- Ensure PostgreSQL is running
- Check connection string in appsettings.json
- Run `dotnet ef database update` to apply migrations

### Frontend can't connect to backend
- Verify backend is running on port 5000
- Check CORS settings in backend
- Verify VITE_API_URL in frontend/.env

### Docker containers won't start
- Ensure Docker daemon is running
- Check if ports are already in use
- Verify Docker socket is accessible

## Development

### Adding a New Database Provider

1. Create a new provider class in `backend/BeaverDB.API/Services/DatabaseProviders/`
2. Implement `IDatabaseProvider` interface
3. Add to `DatabaseProviderFactory`
4. Install necessary NuGet package

### Project Structure

```
BeaverDB/
├── backend/
│   └── BeaverDB.API/
│       ├── Controllers/
│       ├── Models/
│       ├── Services/
│       ├── Data/
│       └── Migrations/
├── frontend/
│   └── src/
│       ├── components/
│       ├── pages/
│       ├── services/
│       └── context/
└── docker-compose.yml
```

## License

MIT License

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.

## Roadmap

- [ ] SQL Server provider implementation
- [ ] MongoDB provider implementation
- [ ] Redis command interface
- [ ] User management (create DB users)
- [ ] Table creation/modification UI
- [ ] Data browsing and editing
- [ ] Query history
- [ ] Export/Import functionality
- [ ] Multi-user support with roles
- [ ] Backup and restore features

## Support

For issues and questions, please open an issue on GitHub.
