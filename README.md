EventRelay

EventRelay is a distributed event and webhook delivery platform built with .NET.

It is being developed incrementally to explore microservices, reliable message delivery, event-driven architecture, distributed systems, and cloud infrastructure.

Architecture

The current implementation contains four independently running applications:

* Gateway: ASP.NET Core with YARP for routing incoming API requests.
* ControlPlaneService: Project management API with PostgreSQL persistence.
* EventService: Event ingestion service foundation.
* DeliveryService: Background worker foundation.

Each domain service has its own logical PostgreSQL database.

Technology Stack

* .NET 10
* ASP.NET Core
* Entity Framework Core
* PostgreSQL 17
* YARP
* Docker and Docker Compose
* xUnit
* Testcontainers

Prerequisites

* .NET 10 SDK
* Docker with Docker Compose
* Git

Local Development

1. Clone the repository.
2. Copy .env.example to .env.
3. Set POSTGRES_PASSWORD in .env.
4. Run:

docker compose up --build -d

5. Check container status:

docker compose ps -a

The PostgreSQL databases and ControlPlaneService schema are initialized during startup.

Local Services

Service	Address
Gateway	http://localhost:5000
ControlPlaneService	http://localhost:5101
EventService	http://localhost:5102

DeliveryService runs as a background worker and does not expose an HTTP endpoint.

API Examples

Create a project:

curl -X POST http://localhost:5000/api/projects \
  -H "Content-Type: application/json" \
  -d '{"name":"MyStore","description":"Online shop"}'

Retrieve a project:

curl http://localhost:5000/api/projects/YOUR_PROJECT_ID

Running Tests

dotnet test EventRelay.slnx

Integration tests require a working Docker engine because they use Testcontainers.

Current Project Status

Phase 1 — Microservices Foundation

Implemented: service scaffolding, Gateway routing, Project API, EF Core persistence, PostgreSQL, Docker Compose and foundational tests.

RabbitMQ messaging, webhook delivery, retries, dead-letter queues and replay are planned for subsequent phases. These features are not yet part of the running system.
