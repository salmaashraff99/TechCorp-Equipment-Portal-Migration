# Migration Journey

## Overview

This document records the step-by-step process of migrating TechCorp International's Equipment Request Portal from a legacy ASP.NET WebForms monolith to a .NET 9 microservices architecture. Each step explains what was done, why it was done in that order, and what problem it solved.

---

## Step-by-Step Migration

### Step 1 — Understand and Document the Legacy System

**What:** Read and analysed all four legacy files (`DBHelper.cs`, `General.cs`, `WorkFlow.cs`, `EquipmentRequest.aspx.cs`) before writing a single line of new code.

**Why:** Migrations fail when teams rewrite features they did not fully understand. The legacy code contained implicit business rules that were not obvious from the UI — the Finance approval threshold, the exact status code integers used by downstream reports, and the dual-email behaviour on approval that some managers had come to rely on.

**Output:** `docs/01_legacy_problems.md` — a catalogue of every problem found with exact file and line references.

---

### Step 2 — Design the Target Architecture

**What:** Designed the three-service split (Equipment, Notification, APIGateway) and the shared library before writing any implementation code.

**Why:** Without an agreed design, each service risks growing into a new monolith. Deciding service boundaries first forced a clear answer to the question: "What does each service own?" The answer — Equipment owns the request lifecycle, Notification owns all outbound communication — created a clean boundary that prevented repeating the old pattern of one class doing everything.

**Key architectural decisions made at this stage:**

| Decision | Rationale |
|---|---|
| Notification as a separate service, not a library | Email infrastructure can change independently without touching Equipment_Service |
| Ocelot for the gateway | Routing stays as JSON configuration, not application code |
| `TechCorp.Shared` as a class library (not a NuGet package) | Simpler for a single-repo project; both services reference it directly |
| `DTO_Response<T>` as a universal envelope | Every endpoint in the system has the same response shape; clients parse it identically |

---

### Step 3 — Create the Solution Skeleton

**What:** Created the `.sln` file and all four projects using the `dotnet new` CLI. Added project references and NuGet packages before writing any business logic.

**Commands run:**

```bash
dotnet new sln -n TechCorp.Microservices
dotnet new classlib -n TechCorp.Shared      -o Shared/TechCorp.Shared
dotnet new webapi   -n Equipment_Service     -o Equipment_Service
dotnet new webapi   -n Notification_Service  -o Notification_Service
dotnet new webapi   -n APIGateway            -o APIGateway
dotnet sln add ...  # all four projects
dotnet add Equipment_Service   reference Shared/TechCorp.Shared
dotnet add Notification_Service reference Shared/TechCorp.Shared
```

**Why establish skeleton first:** Once references are wired and the solution builds empty, any code placed in the wrong project causes an immediate compile error. The structure enforces the architecture.

---

### Step 4 — Build the Shared Contracts

**What:** Created `DTO_Response<T>` and the `StatusCode` enum in `TechCorp.Shared`.

**Why first:** These types are referenced by both services. Building them first means the compiler immediately catches any deviation from the agreed contract. The `StatusCode` enum directly replaced the 11 magic status numbers that were the source of the most fragile coupling in the legacy system.

---

### Step 5 — Build Equipment_Service (Inside-Out)

**What:** Built the service layer by layer from the core outward: Models → DbContext → Repository interfaces → Repository implementations → UnitOfWork → Services → Controllers.

**Why this order:**

| Layer | Precondition | Reason |
|---|---|---|
| Models | Nothing | EF Core entities define the schema; everything else references them |
| DbContext | Models | Needs entities to define DbSets and configure relationships |
| Repository interfaces | DbContext | Define the data access contract before implementing it |
| Repository implementations | Interfaces + DbContext | Fulfill the contract using EF Core |
| IUnitOfWork / UnitOfWork | All repositories | Groups repos and wraps transactions |
| Services | IUnitOfWork | Business logic is complete before any HTTP is involved |
| Controllers | Services | Thin HTTP adapter layer added last |

This inside-out order means the application compiles and contains valid business logic before a single HTTP endpoint exists.

---

### Step 6 — Implement the Approval Workflow

**What:** Translated the inline SQL approval logic from `WorkFlow.cs` into `WorkFlow_Service.cs`.

**Problems solved during this step:**

| Legacy problem | Solution applied |
|---|---|
| Status codes as comment-only documentation | `StatusCode` enum in `TechCorp.Shared` |
| Finance threshold hardcoded in 2 separate files | Single `const decimal FinanceThreshold = 5000m` in `WorkFlow_Service` |
| No database transaction — partial state on failure | `BeginTransactionAsync / CommitAsync / RollbackAsync` |
| No self-approval guard | Explicit check: `if (request.RequesterId == dto.ApproverId) return Fail(...)` |
| Email sent inline with business logic | Delegated entirely to Notification_Service via HTTP post-commit |
| Finance step always inserted | Finance step created conditionally only when `EstimatedCost > 5000` |

**Notable implementation detail:** The `goto case 3` in `AdvanceWorkFlowAsync` is intentional. When IT Head approves a low-cost request, Finance is skipped by falling through to the IT Ops step creation — this is the clearest way to express the conditional skip without code duplication.

---

### Step 7 — Build Notification_Service

**What:** Created a standalone service that receives notification events, persists them, and dispatches email asynchronously. Added the `retry-failed` endpoint for recovery without re-triggering business events.

**Why a separate service:** In the legacy system, one SMTP timeout could block an approval for 100 seconds. In the new design, Equipment_Service posts to Notification_Service and moves on — the notification is completely decoupled from the approval. Failed notifications accumulate in the `Notifications` table and can be retried by a scheduler or manually without touching the workflow.

---

### Step 8 — Configure the API Gateway

**What:** Configured Ocelot with two route groups and `MMLib.SwaggerForOcelot` for unified API documentation.

**Why Ocelot:** The gateway is pure configuration — JSON route entries with no application code. Adding a third service in the future requires adding one route block and one Swagger endpoint entry. No `Program.cs` changes are needed.

---

### Step 9 — Write Unit Tests

**What:** Created `Equipment_Service.Tests` with 14 tests covering all service methods and all business rule guards. Used hand-written mock repositories and a mock unit of work rather than a mocking framework.

**Why tests were written last (not TDD):** This was a migration of an existing system with known, stable requirements. The architecture was already validated by the service implementation. Writing tests after the services were complete let the test design validate that the repository and unit-of-work abstractions were actually usable — a meaningful architecture test in itself.

**Two production bugs discovered during testing:**

| Bug | Fix applied |
|---|---|
| `SubmitAsync` accepted empty justification strings | Added length guard before `BeginTransactionAsync` |
| `RejectAsync` accepted an empty rejection reason | Added null/whitespace guard before `BeginTransactionAsync` |

Both bugs would have existed indefinitely in the legacy system.

**Hand-written mocks vs. mocking framework:** Mock repositories use explicit control flags (`HasLineManager`, `SelfApproval`, `ShouldFail`) that make each test scenario self-documenting. The test reader can see exactly what state is being simulated without decoding framework-specific setup syntax.

---

## Before vs After Comparison

| Dimension | Legacy WebForms | Modern Microservices |
|---|---|---|
| **Architecture** | Single process, single deployment | 3 independently deployable services |
| **Database access** | Raw `SqlConnection` + string concatenation | EF Core with type-safe queries |
| **SQL Injection** | 8+ vulnerable locations | Zero — EF Core parameterises all queries |
| **Credentials** | Hardcoded in source code (`sa` / expired SMTP) | Configuration / environment variables |
| **Connection management** | Manual `Open/Close` — leaks on exception | EF Core manages connection lifetime |
| **Business logic location** | UI event handlers (`btnSubmit_Click`) | Dedicated service classes |
| **Email** | Synchronous, inline, blocks HTTP thread | Async, separate service, retryable |
| **Transactions** | None — partial DB state on failure | `BeginTransactionAsync / RollbackAsync` |
| **Status codes** | Magic integers in comment block | Typed `StatusCode` enum in shared lib |
| **Finance routing** | Hardcoded threshold in 2 files | Single constant in `WorkFlow_Service` |
| **Self-approval guard** | Absent | Enforced in `ApproveAsync` |
| **Unit tests** | 0 | 14 (all passing, ~1 second) |
| **API contract** | None (WebForms UI only) | OpenAPI / Swagger on every endpoint |
| **Scalability** | Single server; sticky sessions required | Each service scales independently |
| **Deployment** | Full IIS app pool restart for any change | Deploy only the changed service |
| **New service addition** | Modify the monolith | Add one route block to `ocelot.json` |

---

## Challenges Faced and How Solved

| Challenge | Solution |
|---|---|
| Legacy status codes used by downstream SQL reports | Preserved the same integer values in the `StatusCode` enum; they map to the same DB column values |
| `goto` considered harmful | Kept `goto case 3` intentionally — it is the most honest representation of "skip Finance and proceed to IT Ops" in a C# switch |
| `IEmployeeRepository.GetByIdAsync` shadowing base member | Added `new` keyword to the interface declaration; surfaced as compiler warning CS0108 during build |
| Namespace collision: `MockUnitOfWork` class inside `MockUnitOfWork` namespace | Renamed the namespace to `MockUoW` — a class and its containing namespace cannot share a name in C# |
| GitHub push authentication with two different accounts | Set remote URL to `https://username@github.com/...` form; personal access token removed from URL after each push |
