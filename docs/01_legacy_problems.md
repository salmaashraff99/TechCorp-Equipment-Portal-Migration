# Legacy WebForms Problems Analysis

## 1. Overview of TechCorp Equipment Portal

TechCorp International's Equipment Request Portal was built in 2009 as an ASP.NET WebForms application. Over 13 years it grew from a single page into an 820-line monolith touched by at least four developers, none of whom left tests, documentation, or clean boundaries between concerns.

The portal handled a critical business process: employees requesting IT equipment through a four-level approval chain (Line Manager → IT Head → Finance → IT Operations). Despite its importance, the codebase accumulated every anti-pattern that WebForms encouraged — and several it did not.

**The system at a glance:**

| Attribute | Value |
|---|---|
| Framework | ASP.NET WebForms (.NET Framework 4.x) |
| Created | 2009 |
| Last modified | 2022 |
| Authors | Ahmed Mostafa, Khaled Ibrahim, Nada Samir, Omar Fathy |
| Lines of code | ~820 (code-behind alone) |
| Unit tests | 0 |
| Documented APIs | 0 |
| Security vulnerabilities | Multiple critical |

---

## 2. Problems Found

### 2.1 SQL Injection Vulnerability

The most critical security flaw. User-supplied text is concatenated directly into SQL strings in at least eight places. An attacker who submits a crafted justification field can read, modify, or delete any data in the database — or execute OS commands via `xp_cmdshell`.

**From `EquipmentRequest.aspx.cs`, lines 269–280 (`btnSubmit_Click`):**

```csharp
string insertQuery = "INSERT INTO EquipmentRequests " +
                     "(RequesterID, EquipmentTypeID, Quantity, ...) " +
                     "VALUES (" +
                     userID + ", " +
                     equipTypeID + ", " +
                     "'" + justification + "', " +  // <-- RAW USER INPUT, UNESCAPED
                     "'" + department + "', " +
                     STATUS_DRAFT + ", " +
                     "GETDATE()); SELECT SCOPE_IDENTITY();";
```

A `justification` value of `'); DROP TABLE EquipmentRequests; --` would execute successfully. The same pattern appears in `General.cs` (`GetEmployeeID`, `LogActivity`), `WorkFlow.cs` (`CreateNewWorkFlow`, `Submit`, `Reject`), and `EquipmentRequest.aspx.cs` (`btnSaveDraft_Click`).

---

### 2.2 Hardcoded Credentials

Database credentials are hardcoded in two separate files using the `sa` (SQL Server Administrator) account — the highest-privilege account available. SMTP credentials are similarly hardcoded and noted as expired since 2019.

**From `DBHelper.cs`, line 14:**

```csharp
private static string connStr =
    "Data Source=TECHCORP-SQL01;Initial Catalog=EquipmentPortalDB;User ID=sa;Password=Admin@123;";
```

**Duplicated verbatim in `EquipmentRequest.aspx.cs`, line 18:**

```csharp
private string connStr =
    "Data Source=TECHCORP-SQL01;Initial Catalog=EquipmentPortalDB;User ID=sa;Password=Admin@123;";
```

**SMTP credentials from `General.cs`, lines 16–20:**

```csharp
private static string SmtpUser = "svc_portal@techcorp-int.com";
private static string SmtpPass = "P@ssw0rd2019!";  // rotated last in 2019, currently expired
```

Anyone with access to the source code — or a decompiled DLL — has full database administrator access to the production SQL Server.

---

### 2.3 Connection Leaks (No `using` Blocks)

Every database method in `DBHelper.cs` opens a `SqlConnection` and closes it manually. If any exception is thrown between `Open()` and `Close()`, the connection is never returned to the pool. Under sustained load or errors, the application exhausts the connection pool and stops serving all users.

**From `DBHelper.cs`, lines 19–28:**

```csharp
public static DataTable GetData(string query)
{
    DataTable dt = new DataTable();
    SqlConnection con = new SqlConnection(connStr);  // opened here
    SqlDataAdapter da = new SqlDataAdapter(query, con);
    con.Open();
    da.Fill(dt);
    con.Close();  // NEVER REACHED if da.Fill() throws — connection leaks permanently
    return dt;
}
```

The correct pattern wraps the connection in a `using` block, guaranteeing `Dispose()` (and therefore `Close()`) is called even when exceptions occur. This pattern is absent from all seven methods in `DBHelper.cs`.

---

### 2.4 Business Logic in the UI Layer

`btnSubmit_Click` is a 60-line event handler that performs validation, constructs SQL, inserts a database record, creates a workflow instance, increments a session counter, sends an email, and redirects — all in a single UI callback. There is no service layer. The page is the application.

**From `EquipmentRequest.aspx.cs`, lines 223–316:**

```csharp
// direct SQL insert in UI layer
string insertQuery = "INSERT INTO EquipmentRequests ...";
object newIDObj = DBHelper.ExecuteScalar(insertQuery);

// workflow creation — still in the UI event handler
bool submitted = WorkFlow.Submit(newRequestID, userID);

// session used as a cache
Session["MyRequestCount"] = Convert.ToInt32(Session["MyRequestCount"]) + 1;

// email sent from UI layer — SMTP failure blocks the HTTP response
General.SendEmail(userEmail, "Request Submitted Successfully...", emailBody);

Response.Redirect("MyRequests.aspx?msg=submitted&ref=" + newRequestID);
```

If any step fails, the application is left in an inconsistent state with no rollback and no meaningful error.

---

### 2.5 No Separation of Concerns

The entire application is structured as three flat static classes and one code-behind file. There are no interfaces, no layers, and no contracts between components.

| Concern | Where it lives (legacy) |
|---|---|
| Data access | `DBHelper.cs` (static class) |
| Business rules | Split across `WorkFlow.cs` AND `EquipmentRequest.aspx.cs` |
| Email sending | `General.cs` AND `EquipmentRequest.aspx.cs` (duplicated) |
| Input validation | `EquipmentRequest.aspx.cs` (UI layer only) |
| Session management | Every file |
| Workflow routing | `WorkFlow.cs` AND `EquipmentRequest.aspx.cs` (duplicated) |

The Finance approval threshold (`5000`) is hardcoded independently in `WorkFlow.cs` and `EquipmentRequest.aspx.cs`. Changing it requires finding and updating every copy — with no compiler to verify all instances were found.

---

### 2.6 Magic Numbers for Status Codes

Eleven workflow status codes and four priority codes are defined as raw integer literals, scattered across three files. The only documentation is a comment block in `WorkFlow.cs` that explicitly warns against putting them in an enum — because nobody wanted to break the existing references.

**From `WorkFlow.cs`, lines 10–26:**

```csharp
// Status Codes (magic numbers - never put in an enum or constants file):
//   1 = Draft
//   2 = Submitted / Pending Line Manager Approval
//   3 = Line Manager Approved / Pending IT Head Approval
//   4 = IT Head Approved / Pending Finance Approval  (only if cost > 5000)
//   5 = Finance Approved / Pending IT Operations
//   6 = Fully Approved / Fulfilled
//   7 = Rejected by Line Manager
//   8 = Rejected by IT Head
//   9 = Rejected by Finance
//  10 = Rejected by IT Operations
//  11 = Cancelled by Employee
```

The same numbers are re-declared as constants in `EquipmentRequest.aspx.cs` (lines 21–31). If a status value is ever renumbered, the developer must locate and update every occurrence manually across every file.

---

### 2.7 Untestable Code

The codebase has zero unit tests and cannot be unit tested without a complete rewrite. Every method depends on at least one of:

- `HttpContext.Current` (requires a running ASP.NET pipeline)
- `SqlConnection` to a live SQL Server instance
- `SmtpClient` sending real email
- `Session` state populated by a prior page request

There are no interfaces, no dependency injection, and no seams where test doubles can be inserted.

```csharp
// Testing GetEmployeeID() requires: a running ASP.NET app, a live SQL Server,
// and the correct schema — it will still not be isolated from live data
public static int GetEmployeeID(string username)
{
    string query = "SELECT EmployeeID FROM Employees WHERE Username = '" + username + "'";
    object result = DBHelper.ExecuteScalar(query);
    return result != null ? Convert.ToInt32(result) : -1;
}
```

---

### 2.8 Email Sending in the UI Layer

Email is sent synchronously on the HTTP request thread using a hardcoded SMTP server. If the mail server is unavailable, the user's request hangs until the SMTP timeout expires (default: 100 seconds). There is no retry mechanism and no queue.

**From `EquipmentRequest.aspx.cs`, inside `btnSubmit_Click`:**

```csharp
// SMTP failure here blocks the entire HTTP response for up to 100 seconds
General.SendEmail(userEmail, "Request Submitted Successfully - Ref #" + newRequestID, emailBody);
```

The `btnApprove_Click` handler sends a second confirmation email after `WorkFlow.ApproveLevel1` has already sent one — meaning approvers regularly receive duplicate notifications.

---

### 2.9 God Class (4 Authors, 820+ Lines)

`EquipmentRequest.aspx.cs` handles new requests, editing, viewing, approving, rejecting, cancelling, and printing — written by four different developers over twelve years, each adding their feature to whichever method was closest.

```
// EquipmentRequest.aspx.cs
// Created: 2010-03-01 | Last Modified: 2022-09-14
// Authors: Ahmed Mostafa, Khaled Ibrahim, Nada Samir, Omar Fathy
//
// God class - everyone added their feature here because "it was easier".
// 820 lines and counting. Do not add more without refactoring first
// (nobody ever does).
```

`Page_Load` alone exceeds 100 lines. It reads query string parameters, controls panel visibility, populates dropdowns from the database, loads request details, checks authorization, and builds the approval UI — all in a single method with no extraction.

---

## 3. Impact of These Problems

### 3.1 Security Risks

| Vulnerability | Severity | Consequence |
|---|---|---|
| SQL Injection (8+ locations) | Critical | Full DB read/write/delete; potential OS command execution via `xp_cmdshell` |
| Hardcoded `sa` credentials | Critical | Any developer with source access owns the production database |
| Expired SMTP password | High | Email notifications silently fail; no alerts raised |
| No input sanitisation | High | XSS possible wherever user text is rendered in labels |
| Session-based authorisation | Medium | Session fixation and replay attacks possible |

### 3.2 Maintenance Nightmare

- Changing the Finance threshold requires grep-and-replace across multiple files with no compiler guarantee every instance was found.
- Adding a fifth approval level means editing at least five methods across three files.
- `General.cs` is used by 12 different pages. Any change risks breaking unrelated features with no test coverage to detect it.
- The only architectural documentation in the entire codebase is a comment: `// do not touch this class, everything depends on it`.

### 3.3 Cannot Scale

- Every page load opens a new `SqlConnection` without proper disposal. Under load, the SQL Server connection pool exhausts.
- Email is sent synchronously on the request thread. A slow mail server blocks all concurrent approvals.
- Session state stores business data (`MyRequestCount`, `CurrentRequestID`, `CurrentWorkFlowID`). Horizontal scaling breaks session affinity unless sticky sessions are configured on the load balancer.
- The entire portal runs in a single IIS process. Deploying any feature requires an application pool restart that takes the portal offline for all users.

### 3.4 Cannot Test

- Zero unit tests exist after 13 years of development.
- A regression introduced in `DBHelper.cs` could silently break all 12 dependent pages — discovered only when a user files a bug report.
- No code coverage metric can even be computed because there is nothing to run it against.

### 3.5 Cannot Add New Features Safely

- The approval workflow is fully inline SQL. Adding a new step means editing raw INSERT statements scattered across the codebase.
- There is no API. A mobile app, external integration, or automated process cannot interact with the portal without screen-scraping the WebForms UI.
- Every deployment requires a full application restart, taking the portal offline for all concurrent users simultaneously.
