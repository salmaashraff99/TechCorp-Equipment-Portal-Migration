# TechCorp Equipment Portal Migration

**Legacy ASP.NET WebForms → .NET 9 Microservices**

![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4) ![xUnit](https://img.shields.io/badge/xUnit-2.9-brightgreen) ![Tests](https://img.shields.io/badge/Tests-14%20Passing-success) ![Architecture](https://img.shields.io/badge/Architecture-Microservices-blue)

A complete, end-to-end migration demonstration of TechCorp International's Equipment Request Portal — from a 13-year-old ASP.NET WebForms monolith riddled with SQL injection, hardcoded credentials, and zero tests, to a production-grade .NET 9 microservices architecture with full unit test coverage, an API Gateway, and clean separation of concerns.

---

## What This Project Demonstrates

| Area | Legacy (Before) | Modern (After) |
|---|---|---|
| Architecture | Single WebForms monolith | 3 independently deployable services |
| Database access | Raw `SqlConnection` + string concatenation | EF Core 9 — parameterised queries |
| Security | 8+ SQL injection points, hardcoded `sa` credentials | Zero vulnerabilities |
| Business logic | In UI event handlers (`btnSubmit_Click`) | Dedicated service layer |
| Transactions | None — partial DB state on failure | `BeginTransactionAsync / RollbackAsync` |
| Email | Synchronous, inline, blocks HTTP thread for 100s | Async, decoupled service, retryable |
| Status codes | Magic integers scattered across 3 files | Typed `StatusCode` enum in shared library |
| Unit tests | 0 | 14 — all passing in ~1 second |
| API contract | None (WebForms UI only) | OpenAPI / Swagger on every endpoint |

---

## Repository Structure

```
TechCorp-Equipment-Portal-Migration/
├── Legacy_Portal/                        Original WebForms code (DO NOT DEPLOY)
│   ├── App_Code/
│   │   ├── DBHelper.cs                   Static data access — no using blocks, connection leaks
│   │   ├── General.cs                    Hardcoded SMTP, SQL injection in GetEmployeeID
│   │   └── WorkFlow.cs                   Magic status numbers, no transactions
│   ├── EquipmentRequest.aspx             WebForms markup — inline CSS, AutoPostBack
│   └── EquipmentRequest.aspx.cs          820-line god class — 4 authors, SQL injection
│
├── Microservices/
│   ├── TechCorp.Microservices.sln
│   ├── Shared/
│   │   └── TechCorp.Shared/              Class library — shared contracts only
│   │       ├── DTOs/DTO_Response.cs      Universal response envelope
│   │       └── Enums/StatusCode.cs       Typed replacement for magic integers
│   │
│   ├── Equipment_Service/                Port 5101 — request lifecycle owner
│   │   ├── Models/                       EF Core entities
│   │   ├── DTOs/                         API contract objects
│   │   ├── Repositories/                 Generic + typed interfaces + EF Core implementations
│   │   ├── Services/                     Business logic — zero SQL, zero HTTP context
│   │   ├── Controllers/                  Thin HTTP adapter
│   │   └── Infrastructure/               AppDbContext, IUnitOfWork, UnitOfWork
│   │
│   ├── Notification_Service/             Port 5102 — all outbound communication
│   │   ├── Models/
│   │   ├── DTOs/
│   │   ├── Repositories/
│   │   ├── Services/
│   │   ├── Controllers/
│   │   └── Infrastructure/
│   │
│   ├── APIGateway/                       Port 5100 — single entry point (Ocelot)
│   │   ├── Configurations/ocelot.json    Route configuration — no code changes needed
│   │   └── Program.cs
│   │
│   └── Equipment_Service.Tests/          xUnit — 14 tests, hand-written mocks
│       ├── MockData/
│       ├── MockRepository/
│       ├── MockUnitOfWork/
│       └── Services/
│
└── docs/
    ├── 01_legacy_problems.md             9 catalogued problems with code evidence
    ├── 02_architecture_design.md         Design patterns, service breakdown, tech choices
    ├── 03_migration_steps.md             9-step migration journey, before/after comparison
    └── 04_api_documentation.md           Full API reference + how to run locally
```

---

## Architecture

```
                   ┌──────────────────────────────────────────────┐
                   │             CLIENT APPLICATIONS              │
                   │      (Web UI / Mobile / External Systems)    │
                   └─────────────────────┬────────────────────────┘
                                         │ HTTP
                                         ▼
                   ┌──────────────────────────────────────────────┐
                   │                 API GATEWAY                  │
                   │              localhost : 5100                │
                   │   Route-based forwarding (Ocelot)            │
                   │   Unified Swagger UI (SwaggerForOcelot)      │
                   └──────────────┬───────────────┬──────────────┘
                                  │               │
               /equipment/**      │               │   /notification/**
                                  ▼               ▼
          ┌───────────────────────────┐  ┌───────────────────────────┐
          │     EQUIPMENT SERVICE     │  │   NOTIFICATION SERVICE    │
          │       localhost:5101      │  │      localhost:5102        │
          │                           │  │                           │
          │  Submit requests          │  │  Receive events           │
          │  4-level approval flow    │  │  Send email alerts        │
          │  Reject with reason       │  │  Persist notification log │
          │  View request history     │  │  Retry failed sends       │
          │  List equipment types     │  │                           │
          └─────────────┬─────────────┘  └─────────────┬─────────────┘
                        │                              │
                        ▼                              ▼
          ┌──────────────────────┐       ┌──────────────────────────┐
          │  TechCorp_EquipmentDB│       │  TechCorp_NotificationDB │
          └──────────────────────┘       └──────────────────────────┘

          ┌────────────────────────────────────────────────────────┐
          │  TechCorp.Shared  —  DTO_Response<T>  +  StatusCode    │
          └────────────────────────────────────────────────────────┘
```

---

## 4-Level Approval Workflow

```
  Employee submits request
           │
           ▼
  ┌────────────────────┐
  │  LEVEL 1           │──── Reject ──→  Status 7: Rejected by Line Manager
  │  Line Manager      │
  └────────┬───────────┘
           │ Approve
           ▼
  ┌────────────────────┐
  │  LEVEL 2           │──── Reject ──→  Status 8: Rejected by IT Head
  │  IT Dept. Head     │
  └────────┬───────────┘
           │ Approve
           │
           ├── EstimatedCost > 5,000 EGP ──────────────────┐
           │                                               ▼
           │                               ┌────────────────────┐
           │                               │  LEVEL 3           │──── Reject ──→  Status 9
           │                               │  Finance Dept.     │
           │                               └────────┬───────────┘
           │◄───────────────────────────────────────┘
           │
           │ EstimatedCost ≤ 5,000 EGP (Finance skipped)
           ▼
  ┌────────────────────┐
  │  LEVEL 4           │──── Reject ──→  Status 10: Rejected by IT Ops
  │  IT Operations     │
  └────────┬───────────┘
           │ Approve
           ▼
  Status 6: Fully Approved
```

---

## How to Run Locally

### Prerequisites

- .NET 9 SDK
- SQL Server (local or Docker)
- Optional: local SMTP relay (Papercut, MailHog)

### 1. Configure connection strings

**`Equipment_Service/appsettings.json`**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=TechCorp_EquipmentDB;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Services": {
    "NotificationService": "http://localhost:5102"
  }
}
```

**`Notification_Service/appsettings.json`**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=TechCorp_NotificationDB;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Smtp": { "Host": "localhost", "Port": "25", "From": "no-reply@techcorp-int.com" }
}
```

### 2. Start each service

```bash
# Terminal 1
cd Microservices/Equipment_Service && dotnet run
# → http://localhost:5101

# Terminal 2
cd Microservices/Notification_Service && dotnet run
# → http://localhost:5102

# Terminal 3
cd Microservices/APIGateway && dotnet run
# → http://localhost:5100
```

### 3. Explore the unified API

Open `http://localhost:5100/swagger` — all endpoints from both services in one UI.

### 4. Run the unit tests

```bash
cd Microservices
dotnet test Equipment_Service.Tests/Equipment_Service.Tests.csproj
```

Expected:
```
Total tests: 14
     Passed: 14
 Total time: ~1 second
```

---

## Key Highlights

### Legacy Problems Identified

Nine catalogued problems with code evidence — see [`docs/01_legacy_problems.md`](docs/01_legacy_problems.md):

- **SQL Injection** at 8+ locations — user input concatenated directly into SQL strings
- **Hardcoded `sa` credentials** in two separate source files — full DB admin access to anyone with the DLL
- **Connection leaks** — `SqlConnection.Close()` never called when exceptions occur
- **Business logic in the UI layer** — 60-line `btnSubmit_Click` performs validation, DB writes, workflow creation, email, and redirect
- **No separation of concerns** — Finance threshold `5000` hardcoded in two files independently
- **Magic status integers** — 11 status codes documented only in a comment block
- **Zero unit tests** after 13 years of active development
- **Synchronous email on the request thread** — SMTP timeout blocks all users for up to 100 seconds
- **God class** — 820 lines, 4 authors, every feature crammed into one code-behind file

### Clean Architecture Implemented

See [`docs/02_architecture_design.md`](docs/02_architecture_design.md) for the full design:

- **Repository Pattern** — interfaces create a seam between business logic and EF Core; the same seam holds mock repositories in tests
- **Hybrid Unit of Work** — typed repositories + generic `Repository<T>()` factory + `BeginTransactionAsync / RollbackAsync`
- **DTO Pattern** — domain models never cross API boundaries; serialisation-safe flat DTOs evolve independently
- **Dependency Injection** — no `static` classes, no `new` in service code; full graph validated at startup

### 14 Unit Tests — All Passing

Hand-written mock repositories (no mocking framework) with explicit control flags make each scenario self-documenting:

```csharp
// What state is being tested is obvious without decoding framework syntax
_mockEquipment.HasLineManager = false;
var result = await _service.SubmitAsync(ValidSubmitRequest);
result.error.Should().BeTrue();
result.code.Should().Be((int)StatusCode.NotAcceptable);
```

Tests cover all service methods and all business rule guards — including two production bugs discovered during test writing (empty justification, empty rejection reason).

### Full Approval Workflow

Business rules enforced in code:
- Requester cannot approve their own request
- Finance step conditionally created only when `EstimatedCost > 5,000 EGP`
- Rejection requires a non-empty reason
- Approval `level` must match the current pending workflow step
- Notification failures never roll back the approval that triggered them

### API Gateway with Swagger

Adding a new downstream service requires one JSON route block in `ocelot.json` — no `Program.cs` changes, no code deployment. The unified Swagger UI at `http://localhost:5100/swagger` aggregates all endpoints automatically.

---

## Documentation

| Document | Contents |
|---|---|
| [`docs/01_legacy_problems.md`](docs/01_legacy_problems.md) | 9 problems with exact file/line references and original code snippets |
| [`docs/02_architecture_design.md`](docs/02_architecture_design.md) | Service breakdown, design patterns, technology choices, workflow diagram |
| [`docs/03_migration_steps.md`](docs/03_migration_steps.md) | 9-step migration journey, decisions made at each step, before/after table |
| [`docs/04_api_documentation.md`](docs/04_api_documentation.md) | Full API reference — every endpoint, request/response examples, error codes |

---

## Technology Stack

| Technology | Version | Role |
|---|---|---|
| .NET / ASP.NET Core | 9.0 | Runtime and web framework |
| Entity Framework Core | 9.0 | ORM — type-safe queries, no raw SQL |
| Ocelot | 23.4 | API Gateway — JSON routing configuration |
| MMLib.SwaggerForOcelot | 5.2 | Aggregated Swagger UI across all services |
| Swashbuckle | 7.2 | OpenAPI generation per service |
| xUnit | 2.9 | Test framework |
| FluentAssertions | 6.12 | Readable assertion syntax |
| SQL Server | — | Database (existing TechCorp infrastructure) |

---

## 🤖 Built with Agentic AI Development

This project was built using **Claude Code** — Anthropic's terminal-based agentic AI development tool.

### What this means:
- Claude Code ran directly inside the project folder
- It read, analyzed, wrote, and fixed code autonomously
- Every build error was fixed without manual intervention
- The entire migration took hours instead of weeks

### How it was used:
See [CLAUDE.md](CLAUDE.md) for the complete breakdown
of what Claude Code did vs what the developer did.

### The key skill demonstrated:
Not just *using* AI — but *directing* AI:
- Designing architecture before prompting
- Reviewing and correcting AI output
- Understanding every line of generated code
- Knowing when AI is wrong and fixing it

> "The developer is the architect. Claude Code is the implementation assistant."
