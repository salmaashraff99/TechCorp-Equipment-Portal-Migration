# API Documentation

## Overview

All endpoints are accessible via the API Gateway at `http://localhost:5100`. Services can also be called directly during development.

| Service | Direct URL | Via Gateway prefix |
|---|---|---|
| Equipment_Service | `http://localhost:5101` | `http://localhost:5100/equipment/...` |
| Notification_Service | `http://localhost:5102` | `http://localhost:5100/notification/...` |
| Swagger UI (unified) | `http://localhost:5100/swagger` | — |

**All responses share the same envelope:**

```json
{
  "code":    200,
  "error":   false,
  "message": "Success",
  "data":    [ ... ]
}
```

| Field | Type | Description |
|---|---|---|
| `code` | integer | Mirrors the HTTP status code |
| `error` | boolean | `true` when the operation failed |
| `message` | string | Human-readable result or error description |
| `data` | array | Payload items; empty array `[]` on error |

---

## Equipment_Service Endpoints

### GET `/equipment/request/{id}`

Retrieve a single equipment request with full details.

**Path parameters:**

| Name | Type | Description |
|---|---|---|
| `id` | integer | Equipment request ID |

**Success — 200:**

```json
{
  "code": 200,
  "error": false,
  "message": "Success",
  "data": [
    {
      "id": 42,
      "requesterId": 1,
      "requesterName": "John Doe",
      "department": "Engineering",
      "equipmentTypeName": "Laptop",
      "quantity": 2,
      "priorityName": "High",
      "estimatedCost": 5000.00,
      "justification": "Required for new software development project.",
      "statusName": "Pending IT Head",
      "requestDate": "2026-05-01T09:00:00Z",
      "submitDate":  "2026-05-01T09:05:00Z"
    }
  ]
}
```

**Not found — 404:**

```json
{
  "code": 404,
  "error": true,
  "message": "Request not found.",
  "data": []
}
```

---

### GET `/equipment/request/user/{employeeId}`

Retrieve all requests submitted by an employee, ordered by most recent first.

**Path parameters:**

| Name | Type | Description |
|---|---|---|
| `employeeId` | integer | Employee ID |

**Success — 200:**

```json
{
  "code": 200,
  "error": false,
  "message": "Success",
  "data": [
    {
      "id": 42,
      "requesterId": 1,
      "requesterName": "John Doe",
      "department": "Engineering",
      "equipmentTypeName": "Laptop",
      "quantity": 2,
      "priorityName": "High",
      "estimatedCost": 5000.00,
      "justification": "Required for new project.",
      "statusName": "Pending Line Manager",
      "requestDate": "2026-05-10T10:00:00Z",
      "submitDate":  "2026-05-10T10:02:00Z"
    },
    {
      "id": 38,
      "requesterId": 1,
      "requesterName": "John Doe",
      "department": "Engineering",
      "equipmentTypeName": "Monitor",
      "quantity": 1,
      "priorityName": "Normal",
      "estimatedCost": 800.00,
      "justification": "Existing monitor is broken.",
      "statusName": "Fully Approved",
      "requestDate": "2026-04-15T08:30:00Z",
      "submitDate":  "2026-04-15T08:35:00Z"
    }
  ]
}
```

---

### POST `/equipment/request/submit`

Submit a new equipment request. Validates the requester, confirms a line manager exists, and initialises the approval workflow at Level 1.

**Request body:**

```json
{
  "requesterId":     1,
  "equipmentTypeId": 3,
  "quantity":        2,
  "priority":        3,
  "estimatedCost":   5000.00,
  "justification":   "Required for the portal migration project. Two developers need dedicated machines.",
  "department":      "Engineering"
}
```

| Field | Type | Required | Validation rules |
|---|---|---|---|
| `requesterId` | integer | Yes | Must exist in Employees table |
| `equipmentTypeId` | integer | Yes | Must exist in EquipmentTypes table |
| `quantity` | integer | Yes | Greater than zero |
| `priority` | integer | Yes | 1=Low, 2=Normal, 3=High, 4=Critical |
| `estimatedCost` | decimal | Yes | Greater than zero |
| `justification` | string | Yes | Minimum 10 characters |
| `department` | string | Yes | Free text; stored with the request |

**Success — 201:**

```json
{
  "code": 201,
  "error": false,
  "message": "Request submitted successfully. Reference #42",
  "data": ["42"]
}
```

**Justification too short — 406:**

```json
{
  "code": 406,
  "error": true,
  "message": "Justification is required (minimum 10 characters).",
  "data": []
}
```

**No line manager found — 406:**

```json
{
  "code": 406,
  "error": true,
  "message": "No line manager found for this employee. Cannot submit request.",
  "data": []
}
```

**Requester is their own manager — 406:**

```json
{
  "code": 406,
  "error": true,
  "message": "Requester cannot be their own line manager.",
  "data": []
}
```

**Requester not found — 404:**

```json
{
  "code": 404,
  "error": true,
  "message": "Requester not found.",
  "data": []
}
```

---

### POST `/equipment/request/approve`

Approve the current pending workflow step for a request. After approval the workflow automatically advances to the next step — or completes the request at Level 4.

**Business rules enforced:**
- Approver cannot be the same person as the requester.
- `level` must match the current pending workflow step number.
- If `estimatedCost ≤ 5,000 EGP` and this is Level 2 (IT Head), the Finance step is skipped.
- After a successful commit, a notification event is sent to Notification_Service.

**Request body:**

```json
{
  "requestId":  42,
  "approverId": 7,
  "level":      1,
  "comments":   "Approved. Legitimate business need confirmed."
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `requestId` | integer | Yes | ID of the request to approve |
| `approverId` | integer | Yes | ID of the employee performing the approval |
| `level` | integer | Yes | Must match current pending step (1–4) |
| `comments` | string | No | Optional approval notes, stored on the workflow step |

**Success — 200:**

```json
{
  "code": 200,
  "error": false,
  "message": "Request approved successfully.",
  "data": []
}
```

**Self-approval attempt — 406:**

```json
{
  "code": 406,
  "error": true,
  "message": "You cannot approve your own request.",
  "data": []
}
```

**Wrong level — 406:**

```json
{
  "code": 406,
  "error": true,
  "message": "No pending step at level 2 for this request.",
  "data": []
}
```

**Request not found — 404:**

```json
{
  "code": 404,
  "error": true,
  "message": "Request not found.",
  "data": []
}
```

---

### POST `/equipment/request/reject`

Reject the current pending workflow step. A non-empty reason is mandatory and is stored on the workflow step and on the request record.

**Request body:**

```json
{
  "requestId":  42,
  "approverId": 7,
  "level":      1,
  "reason":     "Budget frozen for Q2. Please resubmit in Q3 with updated cost estimate."
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `requestId` | integer | Yes | ID of the request to reject |
| `approverId` | integer | Yes | ID of the employee performing the rejection |
| `level` | integer | Yes | Must match current pending step (1–4) |
| `reason` | string | Yes | Mandatory; minimum 1 non-whitespace character |

**Rejection status codes set on the request:**

| Level | Rejection status | Status name |
|---|---|---|
| 1 | 7 | Rejected by Line Manager |
| 2 | 8 | Rejected by IT Head |
| 3 | 9 | Rejected by Finance |
| 4 | 10 | Rejected by IT Operations |

**Success — 200:**

```json
{
  "code": 200,
  "error": false,
  "message": "Request rejected.",
  "data": []
}
```

**Missing reason — 406:**

```json
{
  "code": 406,
  "error": true,
  "message": "Rejection reason is required.",
  "data": []
}
```

---

### GET `/equipment/request/{id}/history`

Retrieve the complete approval history for a request, ordered by step number ascending.

**Path parameters:**

| Name | Type | Description |
|---|---|---|
| `id` | integer | Equipment request ID |

**Success — 200:**

```json
{
  "code": 200,
  "error": false,
  "message": "Success",
  "data": [
    {
      "stepNumber":   1,
      "stepName":     "Line Manager Approval",
      "approverName": "Employee #7",
      "statusName":   "Approved",
      "comments":     "Approved. Legitimate business need confirmed.",
      "actionDate":   "2026-05-02T11:30:00Z"
    },
    {
      "stepNumber":   2,
      "stepName":     "IT Department Head Approval",
      "approverName": "Pending",
      "statusName":   "Pending",
      "comments":     null,
      "actionDate":   null
    }
  ]
}
```

---

### GET `/equipment/types`

List all active equipment types with price range data.

**Success — 200:**

```json
{
  "code": 200,
  "error": false,
  "message": "Success",
  "data": [
    {
      "id": 1,
      "typeName":     "Laptop",
      "averagePrice": 2500.00,
      "minPrice":     1500.00,
      "maxPrice":     4000.00,
      "isActive":     true
    },
    {
      "id": 2,
      "typeName":     "High-End Workstation",
      "averagePrice": 8000.00,
      "minPrice":     6000.00,
      "maxPrice":     12000.00,
      "isActive":     true
    }
  ]
}
```

---

## Notification_Service Endpoints

### POST `/notification/send`

Receive a notification event from Equipment_Service, persist the record, and dispatch an email.

**Request body:**

```json
{
  "requestId": 42,
  "actorId":   7,
  "action":    "Approved",
  "level":     1,
  "sentAt":    "2026-05-10T12:00:00Z"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `requestId` | integer | Yes | The equipment request ID |
| `actorId` | integer | Yes | Employee who triggered the event |
| `action` | string | Yes | `"Approved"` or `"Rejected"` |
| `level` | integer | Yes | Approval level where the action occurred |
| `sentAt` | datetime | Yes | UTC timestamp of the action |

**Email sent — 200:**

```json
{
  "code": 200,
  "error": false,
  "message": "Notification sent.",
  "data": []
}
```

**SMTP failure — queued for retry — 500:**

```json
{
  "code": 500,
  "error": true,
  "message": "Notification queued for retry.",
  "data": []
}
```

> **Important:** Equipment_Service swallows this 500. A notification failure never rolls back the approval that triggered it. The notification record remains in the database with `IsSent = false` for later retry.

---

### GET `/notification/{requestId}`

Retrieve the full notification history for a request, ordered by most recent first.

**Path parameters:**

| Name | Type | Description |
|---|---|---|
| `requestId` | integer | Equipment request ID |

**Success — 200:**

```json
{
  "code": 200,
  "error": false,
  "message": "Success",
  "data": [
    {
      "id":             3,
      "requestId":      42,
      "action":         "Approved",
      "level":          2,
      "recipientEmail": "notify-request-42@techcorp-int.com",
      "isSent":         true,
      "retryCount":     0,
      "createdAt":      "2026-05-05T14:00:01Z",
      "sentAt":         "2026-05-05T14:00:02Z",
      "errorMessage":   null
    },
    {
      "id":             1,
      "requestId":      42,
      "action":         "Approved",
      "level":          1,
      "recipientEmail": "notify-request-42@techcorp-int.com",
      "isSent":         false,
      "retryCount":     2,
      "createdAt":      "2026-05-02T11:30:01Z",
      "sentAt":         null,
      "errorMessage":   "Retry 2 failed."
    }
  ]
}
```

---

### POST `/notification/retry-failed`

Retry all unsent notifications where `retryCount < 3`. Intended for scheduled execution or manual operational recovery.

**No request body required.**

**Success — 200:**

```json
{
  "code": 200,
  "error": false,
  "message": "Retried 3 notifications. 2 succeeded.",
  "data": []
}
```

---

## Error Codes Reference

| Code | Enum value | Meaning | Common causes |
|---|---|---|---|
| `200` | `OK` | Operation succeeded | Successful reads, approvals, rejections |
| `201` | `Created` | New resource created | `POST /equipment/request/submit` |
| `404` | `NotFound` | Resource does not exist | Invalid request ID, employee ID, or equipment type ID |
| `406` | `NotAcceptable` | Business rule violated | Self-approval, empty reason, short justification, wrong approval level |
| `500` | `InternalServerError` | Unexpected exception | Unhandled error; check service logs |

---

## How to Run the Project Locally

### Prerequisites

- .NET 9 SDK (`dotnet --version` should print `9.x.x`)
- SQL Server (local instance or Docker)
- Optional: a local SMTP relay for email testing (e.g., Papercut, MailHog)

### 1. Clone the repository

```bash
git clone https://github.com/salmaashraff99/TechCorp-Equipment-Portal-Migration.git
cd TechCorp-Equipment-Portal-Migration/Microservices
```

### 2. Configure connection strings

**`Equipment_Service/appsettings.json`:**

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

**`Notification_Service/appsettings.json`:**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=TechCorp_NotificationDB;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Smtp": {
    "Host": "localhost",
    "Port": "25",
    "From": "no-reply@techcorp-int.com"
  }
}
```

### 3. Start each service in a separate terminal

```bash
# Terminal 1
cd Equipment_Service && dotnet run
# → http://localhost:5101

# Terminal 2
cd Notification_Service && dotnet run
# → http://localhost:5102

# Terminal 3
cd APIGateway && dotnet run
# → http://localhost:5100
```

### 4. Explore the API

Open `http://localhost:5100/swagger` — the unified Swagger UI shows all endpoints from both services.

Individual Swagger UIs:
- `http://localhost:5101/swagger` — Equipment_Service
- `http://localhost:5102/swagger` — Notification_Service

### 5. Run the unit tests

```bash
# From the Microservices/ directory
dotnet test Equipment_Service.Tests/Equipment_Service.Tests.csproj
```

Expected result:

```
Total tests: 14
     Passed: 14
 Total time: ~1 second
```
