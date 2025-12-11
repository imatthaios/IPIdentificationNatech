# IP Geolocation & Batch Enrichment Service (ASP.NET Core)

This service exposes an ASP.NET Core Web API for **IP address geolocation and enrichment**.  
It supports **single lookups** and **batch processing**, backed by **SQL Server** and an external IP geo provider (e.g. ipstack).

Originally built as a coding assignment, the project is structured and documented as a **production-ready microservice**.

---

## ✨ Features

- `GET /api/geo/{ip}` – Single IP geolocation lookup
- `POST /api/geo/batch` – Submit a batch of IP addresses for asynchronous processing
- `GET /api/geo/batch/{id}` – Retrieve batch processing status & results
- External IP provider integration (configurable via `GeoProvider` settings)
- Layered architecture (API / Application / Domain / Infrastructure)
- SQL Server persistence with EF Core (Dockerized)
- Background batch processing (channels / RabbitMQ-ready)
- Strong focus on **configuration, secrets, and cloud readiness**

---

## 🧱 Architecture

The solution follows a layered architecture with clear separation of concerns:

- **API Layer**
  - ASP.NET Core controllers
  - Request/response DTOs and mapping to domain models
  - HTTP endpoints for sync & async operations

- **Application Layer**
  - Use cases & application services (`IGeoApplicationService`)
  - Result pattern for consistent error handling
  - Abstractions for external geo providers & background processing

- **Domain Layer**
  - Core geo entities, value objects, and invariants
  - No infrastructure dependencies

- **Infrastructure Layer**
  - EF Core DbContext (`IpGeoDbContext`) & repositories (Batches, BatchItems, GeoCache)
  - `GeoProviderClient` using `HttpClient`
  - Background workers (channel-based processor and RabbitMQ-based alternative)
  - Options pattern & DI registrations

- **Database**
  - SQL Server 2022 Express (Docker)
  - Tables for Batches, BatchItems, and IP geolocation cache

---

## 🗺 Architecture Diagram

```mermaid
flowchart TD
    Client[Client] --> Api[API Layer<br/>Controllers / DTOs]
    Api --> App[Application Layer<br/>Use Cases / Services / Result<T>]
    App --> Domain[Domain Layer<br/>Entities / Value Objects]
    App --> Infra[Infrastructure Layer<br/>EF Core / GeoProviderClient / Background Workers]
    Infra --> Db[(SQL Server<br/>IpGeoDb)]
    Infra --> Geo[External Geo Provider<br/>(ipstack, etc.)]
