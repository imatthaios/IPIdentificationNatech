IpGeoService solution
Projects:
 - Api: ASP.NET Core Web API (Controllers, DTOs, mapping)
 - Application: Use cases, interfaces, services returning Result<T>
 - Domain: Entities, value objects
 - Infrastructure: EF DbContext, Repositories, HttpClients, BackgroundService
 - UnitTests / IntegrationTests

### Secrets
- Use User Secrets for local development
- Use Azure Key Vault for production
```json
{
  "ConnectionStrings:DefaultConnection": "Server=localhost,1433;Database=IpGeoDb;User Id=SA;Password=<password>;TrustServerCertificate=True;"
}
```
for linux based the path is:
```cli
~/.microsoft/usersecrets/<secrets_id>/secrets.json
```

for windows based the path is:
```powershell
 %APPDATA%\Microsoft\UserSecrets\<secrets_id>\secrets.json
```
```bash
cd Api
dotnet user-secrets init

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=IpGeoDb;User Id=SA;Password=<password>;TrustServerCertificate=True;"
dotnet user-secrets set "GeoProvider:BaseUrl" "https://api.ipstack.com/"
dotnet user-secrets set "GeoProvider:ApiKey" "<your_ipstack_api_key>"
```

Create a .env file for local development with Docker Compose
```dotenv
# SQL SA password used only for local dev
SQL_SA_PASSWORD=<your-password>

# Connection string for the API (note: host is the docker service name "sqlserver")
CONNECTIONSTRINGS__DEFAULTCONNECTION='Server=sqlserver,1433;Database=IpGeoDb;User Id=sa;Password=<your-password>;TrustServerCertificate=True;'

# Geo provider config
IPGEOPROVIDER__BASEURL=https://api.ipstack.com/
IPGEOPROVIDER__APIKEY=<your_ipstack_api_key>
```

Run the solution with Docker Compose
```bash
docker-compose --env-file .env up --build
```