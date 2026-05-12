# Microservices Architecture Design

## 1. Architecture Overview

The modern solution replaces the single WebForms monolith with three independently deployable services behind an API Gateway. Each service owns its domain logic, its data, and its own database schema.

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
                   │                                              │
                   │  • Single entry point for all clients        │
                   │  • Route-based forwarding  (Ocelot)          │
                   │  • Unified Swagger UI  (SwaggerForOcelot)    │
                   └──────────────┬───────────────┬──────────────┘
                                  │               │
               /equipment/**      │               │   /notification/**
                                  ▼               ▼
          ┌───────────────────────────┐  ┌───────────────────────────┐
          │     EQUIPMENT SERVICE     │  │   NOTIFICATION SERVICE    │
          │       localhost:5101      │  │      localhost:5102        │
          │                           │  │                           │
          │  • Submit requests        │  │  • Send email alerts      │
          │  • 4-level approval flow  │  │  • Persist notification   │
          │  • Reject with reason     │  │    history per request    │
          │  • View request history   │  │  • Retry failed sends     │
          │  • List equipment types   │  │                           │
          └─────────────┬─────────────┘  └─────────────┬─────────────┘
                        │                              │
                        ▼                              ▼
          ┌──────────────────────┐       ┌──────────────────────────┐
          │  SQL Server          │       │  SQL Server              │
          │  TechCorp_EquipmentDB│       │  TechCorp_NotificationDB │
          └──────────────────────┘       └──────────────────────────┘

          ┌────────────────────────────────────────────────────────┐
          │                   TechCorp.Shared                      │
          │              (Class Library — no runtime)              │
          │                                                        │
          │   DTO_Response<T>            StatusCode enum           │
          └────────────────────────────────────────────────────────┘
```

---

## 2. Services Breakdown

### 2.1 Equipment_Service — Port 5101

**Responsibility:** Owns the entire equipment request lifecycle — from submission through each approval level to fulfillment or rejection.

**Internal structure:**

```
Equipment_Service/
├── Models/                 Domain entities
│   ├── EquipmentRequest.cs
│   ├── EquipmentRequestItem.cs
│   ├── EquipmentType.cs
│   ├── Employee.cs
│   └── WorkFlowStep.cs
├── DTOs/                   API contract objects (never expose domain models directly)
│   ├── DTO_EquipmentRequest.cs   (Submit, Approve, Reject, Response, History)
│   └── DTO_Employee.cs           (Lookup)
├── Repositories/
│   ├── Interfaces/
│   │   ├── IGenericRepository.cs
│   │   ├── IEquipmentRequestRepository.cs
│   │   ├── IWorkFlowRepository.cs
│   │   └── IEmployeeRepository.cs
│   └── Implementations/          EF Core implementations
├── Services/               Business logic (zero SQL, zero HTTP context)
│   ├── EquipmentRequest_Service.cs
│   └── WorkFlow_Service.cs
├── Controllers/
│   └── EquipmentRequestController.cs
└── Infrastructure/
    ├── Data/Context.cs     AppDbContext + model configuration
    ├── IUnitOfWork.cs
    └── UnitOfWork.cs
```

**Endpoints:**

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/equipment/request/{id}` | Get a single request with full details |
| `GET` | `/equipment/request/user/{employeeId}` | Get all requests for an employee |
| `POST` | `/equipment/request/submit` | Submit a new equipment request |
| `POST` | `/equipment/request/approve` | Approve the current pending workflow step |
| `POST` | `/equipment/request/reject` | Reject the current pending step (reason required) |
| `GET` | `/equipment/request/{id}/history` | Full approval history for a request |
| `GET` | `/equipment/types` | List all active equipment types |

---

### 2.2 Notification_Service — Port 5102

**Responsibility:** Owns all outbound communication. Receives events from Equipment_Service, persists them, dispatches email, and exposes a retry endpoint for failed deliveries. Equipment_Service never sends email directly.

**Internal structure:**

```
Notification_Service/
├── Models/
│   └── Notification.cs         Persisted record of every send attempt
├── DTOs/
│   ├── DTO_SendNotification.cs
│   └── DTO_NotificationResponse.cs
├── Repositories/
│   ├── Interfaces/INotificationRepository.cs
│   └── Implementations/NotificationRepository.cs
├── Services/
│   └── Notification_Service.cs   (send, retry, build email body)
├── Controllers/
│   └── NotificationController.cs
└── Infrastructure/
    ├── Data/Context.cs
    ├── IUnitOfWork.cs
    └── UnitOfWork.cs
```

**Endpoints:**

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/notification/send` | Receive event, persist it, send email |
| `GET` | `/notification/{requestId}` | Get all notification history for a request |
| `POST` | `/notification/retry-failed` | Retry all unsent notifications (max 3 retries) |

**Key design principle:** Equipment_Service calls `POST /notification/send` inside a `try/catch` and swallows the failure silently. Approvals are never blocked by email infrastructure failures.

---

### 2.3 APIGateway — Port 5100

**Responsibility:** Single entry point for all clients. Routes requests to the correct downstream service using Ocelot. Aggregates Swagger documentation from both services into one UI.

**Routing configuration (`ocelot.json`):**

```json
{
  "Routes": [
    {
      "UpstreamPathTemplate":  "/equipment/{everything}",
      "DownstreamPathTemplate": "/equipment/{everything}",
      "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": 5101 }]
    },
    {
      "UpstreamPathTemplate":  "/notification/{everything}",
      "DownstreamPathTemplate": "/notification/{everything}",
      "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": 5102 }]
    }
  ]
}
```

Clients call `localhost:5100` and never need to know internal service ports. Adding a new service requires adding one route block — no code changes.

---

### 2.4 TechCorp.Shared — Class Library

**Responsibility:** Shared contracts consumed by both services. Contains no runtime logic — only data structures. Every API endpoint in the system returns the same response envelope.

```csharp
// DTO_Response<T> — the universal response shape
public class DTO_Response<T>
{
    public int      code    { get; set; }  // mirrors HTTP status code
    public bool     error   { get; set; }  // true when operation failed
    public string   message { get; set; }  // human-readable result message
    public List<T>  data    { get; set; }  // payload; empty list on error
}

// StatusCode — typed constants replace all magic numbers
public enum StatusCode
{
    OK                  = 200,
    Created             = 201,
    NoContent           = 204,
    NotAcceptable       = 406,
    NotFound            = 404,
    InternalServerError = 500
}
```

---

## 3. Design Patterns Used

### 3.1 Repository Pattern

**What:** Each data entity has an interface (`IEquipmentRequestRepository`) and an EF Core implementation. Services depend on the interface, never the concrete class.

**Why:** The legacy code called static `DBHelper` methods everywhere, making the SQL Server a hard dependency of every method. The repository pattern introduces a seam — in production the seam is filled by EF Core; in unit tests it is filled by hand-written mock repositories. This is what makes 14 unit tests run in under one second with zero database infrastructure.

```
Legacy:  btnSubmit_Click ──→ DBHelper.ExecuteScalar() ──→ SqlConnection ──→ SQL Server
Modern:  WorkFlow_Service ──→ IWorkFlowRepository ──→ (EF Core | MockWorkFlowRepository)
```

---

### 3.2 Unit of Work — Hybrid Pattern

**What:** `IUnitOfWork` groups all repositories and exposes `BeginTransactionAsync / CommitAsync / RollbackAsync / SaveAsync`. Services call `BeginTransactionAsync` at the start of a multi-step operation and `RollbackAsync` if anything fails.

**Why:** The legacy system had no transactions. If `WorkFlow.Submit()` inserted the request record and then failed while creating the workflow step, the database was left with an orphaned record and no recovery path. `IUnitOfWork` wraps each multi-step operation in a database transaction — either everything commits or nothing does.

**Hybrid aspect:** Exposes typed repositories (`Equipment`, `WorkFlow`, `Employees`) for the primary aggregates AND a generic `Repository<T>()` factory for any entity that does not warrant its own dedicated interface.

```csharp
public interface IUnitOfWork : IDisposable
{
    IEquipmentRequestRepository Equipment  { get; }
    IWorkFlowRepository         WorkFlow   { get; }
    IEmployeeRepository         Employees  { get; }
    IGenericRepository<T>       Repository<T>() where T : class;
    Task BeginTransactionAsync();
    Task CommitAsync();
    Task RollbackAsync();
    Task<int> SaveAsync();
}
```

---

### 3.3 DTO Pattern

**What:** Services never return domain model objects (`EquipmentRequest`) across API boundaries. They map to flat DTOs (`DTO_RequestResponse`) before returning.

**Why:** The domain model contains EF Core navigation properties, circular references, and internal state that is unsafe to serialise. DTOs are flat, serialisation-safe, and evolve independently of the database schema. Renaming a DB column only requires updating the repository and the mapper — not the API contract that clients depend on.

---

### 3.4 Dependency Injection

**What:** All services, repositories, and the unit of work are registered in `Program.cs` with `AddScoped<>` and injected via constructors. No `new` keyword appears in service or controller code.

**Why:** The legacy code used static classes everywhere (`DBHelper.GetData()`, `General.SendEmail()`). Static dependencies cannot be replaced or intercepted. DI means every dependency is declared in the constructor, every dependency is replaceable in tests, and the DI container validates the full dependency graph at startup — before any request is served.

---

## 4. The 4-Level Approval Workflow

```
  Employee submits request
           │
           ▼
  ┌────────────────────┐
  │  LEVEL 1           │──── Reject ──→  Status 7: Rejected by Manager
  │  Line Manager      │                 Requester notified by email
  │  (auto-assigned)   │
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
           │                                        │ Approve
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
  CompletionDate stamped
  Requester notified
```

**Business rules enforced in code:**
- A requester cannot approve their own request — `WorkFlow_Service.ApproveAsync` line 46
- Finance level is conditionally created — requests ≤ 5,000 EGP skip from IT Head directly to IT Ops
- Each rejection requires a non-empty reason — validated before any DB write
- The `level` in the approval/rejection DTO must match the current pending workflow step number
- Notification failures never roll back the approval that triggered them

---

## 5. Technology Choices

| Technology | Version | Role | Why chosen |
|---|---|---|---|
| .NET | 9.0 | Runtime | Current release; native AOT readiness, best-in-class performance |
| ASP.NET Core | 9.0 | Web framework | Lightweight, testable, built-in DI, no `HttpContext` coupling |
| Entity Framework Core | 9.0 | ORM | Type-safe queries, no raw SQL, schema migrations |
| Ocelot | 23.4 | API Gateway | Purpose-built .NET gateway; routing is JSON configuration, not code |
| MMLib.SwaggerForOcelot | 5.2 | Gateway Swagger | Aggregates downstream OpenAPI docs into one UI without code |
| Swashbuckle | 7.2 | OpenAPI generation | Industry standard for ASP.NET Core; auto-discovers all controllers |
| xUnit | 2.9 | Test framework | Parallel by default; idiomatic with modern .NET |
| FluentAssertions | 6.12 | Assertions | English-like assertion syntax; failure messages name the violated expectation precisely |
| SQL Server | — | Database | Existing TechCorp infrastructure; full EF Core provider support |
