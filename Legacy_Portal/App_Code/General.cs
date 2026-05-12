using System;
using System.Data;
using System.Data.SqlClient;
using System.Net.Mail;
using System.Web;

// General.cs - TechCorp International Equipment Portal
// Created: 2009-05-20 | Last Modified: 2019-11-03 | Author: Ahmed Mostafa
// This class is a dumping ground for everything that doesn't fit elsewhere.
// Multiple developers have added methods here over the years with no review.
// WARNING: methods here are used by 12 different pages - change carefully

public class General
{
    // SMTP settings hardcoded - changing these requires a redeployment
    private static string SmtpHost    = "mail.techcorp-int.com";
    private static int    SmtpPort    = 25;
    private static string SmtpFrom    = "no-reply@techcorp-int.com";
    private static string SmtpUser    = "svc_portal@techcorp-int.com";
    private static string SmtpPass    = "P@ssw0rd2019!";   // rotated last in 2019, currently expired

    // reads the current user's email out of Session - set on login page
    // if Session expired this returns null and everything breaks downstream
    public static string GetUserEmail()
    {
        return HttpContext.Current.Session["UserEmail"] != null
            ? HttpContext.Current.Session["UserEmail"].ToString()
            : "";
    }

    public static string GetUserName()
    {
        return HttpContext.Current.Session["UserName"] != null
            ? HttpContext.Current.Session["UserName"].ToString()
            : "";
    }

    public static string GetUserDepartment()
    {
        return HttpContext.Current.Session["Department"] != null
            ? HttpContext.Current.Session["Department"].ToString()
            : "";
    }

    public static int GetUserID()
    {
        try
        {
            return Convert.ToInt32(HttpContext.Current.Session["UserID"]);
        }
        catch
        {
            return 0;   // swallowing cast exception - returns 0 on session expiry
        }
    }

    // hits the DB every single call - no caching whatsoever
    // string concat here is a SQL injection risk but "only internal users use this"
    public static int GetEmployeeID(string username)
    {
        string query = "SELECT EmployeeID FROM Employees WHERE Username = '" + username + "'";
        object result = DBHelper.ExecuteScalar(query);
        if (result != null)
            return Convert.ToInt32(result);
        return -1;
    }

    public static string GetEmployeeEmail(int employeeID)
    {
        string query = "SELECT Email FROM Employees WHERE EmployeeID = " + employeeID;
        object result = DBHelper.ExecuteScalar(query);
        return result != null ? result.ToString() : "";
    }

    public static string GetManagerEmail(int employeeID)
    {
        // joins through 3 tables - this was written in a hurry in 2013, nobody touched it since
        string query = @"SELECT e2.Email
                         FROM Employees e1
                         INNER JOIN Employees e2 ON e1.ManagerID = e2.EmployeeID
                         WHERE e1.EmployeeID = " + employeeID;
        object result = DBHelper.ExecuteScalar(query);
        return result != null ? result.ToString() : "";
    }

    public static string GetITHeadEmail()
    {
        // hardcoded department ID 7 = IT - magic number, nobody documented this
        string query = "SELECT Email FROM Employees WHERE DepartmentID = 7 AND Role = 'DeptHead' AND IsActive = 1";
        object result = DBHelper.ExecuteScalar(query);
        return result != null ? result.ToString() : "it.head@techcorp-int.com";   // fallback hardcoded email
    }

    public static string GetFinanceHeadEmail()
    {
        // hardcoded department ID 4 = Finance
        string query = "SELECT Email FROM Employees WHERE DepartmentID = 4 AND Role = 'DeptHead' AND IsActive = 1";
        object result = DBHelper.ExecuteScalar(query);
        return result != null ? result.ToString() : "finance.head@techcorp-int.com";
    }

    public static string GetITOperationsEmail()
    {
        string query = "SELECT Email FROM Employees WHERE DepartmentID = 7 AND Role = 'ITOps' AND IsActive = 1";
        object result = DBHelper.ExecuteScalar(query);
        return result != null ? result.ToString() : "it.operations@techcorp-int.com";
    }

    // sends email directly - no queue, no retry, no async
    // if SMTP server is down the whole page request fails
    public static bool SendEmail(string toEmail, string subject, string body)
    {
        try
        {
            MailMessage mail = new MailMessage();
            mail.From = new MailAddress(SmtpFrom, "TechCorp Equipment Portal");
            mail.To.Add(toEmail);
            mail.Subject = subject;
            mail.Body = body;
            mail.IsBodyHtml = true;

            SmtpClient smtp = new SmtpClient(SmtpHost, SmtpPort);
            smtp.Credentials = new System.Net.NetworkCredential(SmtpUser, SmtpPass);
            smtp.Send(mail);
            return true;
        }
        catch (Exception ex)
        {
            // just log to a text file - no logging framework exists
            LogError("SendEmail failed to: " + toEmail + " | Error: " + ex.Message);
            return false;
        }
    }

    public static bool SendEmailCC(string toEmail, string ccEmail, string subject, string body)
    {
        try
        {
            MailMessage mail = new MailMessage();
            mail.From = new MailAddress(SmtpFrom, "TechCorp Equipment Portal");
            mail.To.Add(toEmail);
            if (!string.IsNullOrEmpty(ccEmail))
                mail.CC.Add(ccEmail);
            mail.Subject = subject;
            mail.Body = body;
            mail.IsBodyHtml = true;

            SmtpClient smtp = new SmtpClient(SmtpHost, SmtpPort);
            smtp.Credentials = new System.Net.NetworkCredential(SmtpUser, SmtpPass);
            smtp.Send(mail);
            return true;
        }
        catch (Exception ex)
        {
            LogError("SendEmailCC failed: " + ex.Message);
            return false;
        }
    }

    // raw SQL scalar with string concatenation - used in 8 places across the portal
    public static object ExecuteScalar(string tableName, string column, string whereClause)
    {
        string query = "SELECT " + column + " FROM " + tableName + " WHERE " + whereClause;
        return DBHelper.ExecuteScalar(query);
    }

    // converts a comma-separated string into a DataTable - used in old import pages
    public static DataTable ToTable(string csvData, string delimiter)
    {
        DataTable dt = new DataTable();
        string[] lines = csvData.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0) return dt;

        // first line is header
        string[] headers = lines[0].Split(delimiter.ToCharArray());
        foreach (string h in headers)
            dt.Columns.Add(h.Trim());

        for (int i = 1; i < lines.Length; i++)
        {
            string[] values = lines[i].Split(delimiter.ToCharArray());
            DataRow row = dt.NewRow();
            for (int j = 0; j < headers.Length; j++)
            {
                if (j < values.Length)
                    row[j] = values[j].Trim();
            }
            dt.Rows.Add(row);
        }
        return dt;
    }

    // logs errors to a text file on the server - added 2011, nobody reads these logs
    public static void LogError(string message)
    {
        try
        {
            string logPath = @"C:\TechCorpLogs\EquipmentPortal_Errors.txt";
            string logEntry = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + message + "\r\n";
            System.IO.File.AppendAllText(logPath, logEntry);
        }
        catch
        {
            // if logging itself fails, silently ignore - nothing we can do
        }
    }

    public static void LogActivity(string username, string action)
    {
        try
        {
            string query = "INSERT INTO ActivityLog (Username, Action, LogDate) VALUES ('"
                           + username + "', '" + action + "', GETDATE())";
            DBHelper.ExecuteNonQuery(query);   // SQL injection here too - "internal only"
        }
        catch
        {
            // swallow - activity logging should never break the main flow
        }
    }

    // returns equipment type name from ID - no caching, hits DB every call
    public static string GetEquipmentTypeName(int typeID)
    {
        string query = "SELECT TypeName FROM EquipmentTypes WHERE TypeID = " + typeID;
        object result = DBHelper.ExecuteScalar(query);
        return result != null ? result.ToString() : "Unknown";
    }

    // added by Nada 2020 for the dashboard - duplicate of GetData in DBHelper, never cleaned up
    public static DataTable GetPendingRequestsByDept(string department)
    {
        string query = "SELECT * FROM EquipmentRequests WHERE Department = '" + department
                       + "' AND Status NOT IN (5, 6) ORDER BY RequestDate DESC";
        return DBHelper.GetData(query);
    }

    public static bool IsSessionValid()
    {
        return HttpContext.Current.Session["UserID"] != null
            && HttpContext.Current.Session["UserEmail"] != null;
    }
}
