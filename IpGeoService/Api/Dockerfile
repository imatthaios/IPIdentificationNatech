# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files
COPY Api/Api.csproj Api/
COPY Application/Application.csproj Application/
COPY Domain/Domain.csproj Domain/
COPY Infrastructure/Infrastructure.csproj Infrastructure/

# Restore
RUN dotnet restore Api/Api.csproj

# Copy everything and build
COPY . .
WORKDIR /src/Api
RUN dotnet publish Api.csproj -c Debug -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Copy published app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Api.dll"]
