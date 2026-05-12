using System;
using System.Data;
using System.Data.SqlClient;

// WorkFlow.cs - TechCorp International Equipment Portal
// Created: 2010-01-15 | Last Modified: 2021-06-22 | Author: Khaled Ibrahim
// Handles the 4-level approval workflow for equipment requests.
// IMPORTANT: status codes are used all over the system - DO NOT change the numbers
//
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
//
// Priority codes:
//   1 = Low, 2 = Normal, 3 = High, 4 = Critical
//
// Finance threshold: 5000 EGP - hardcoded below in multiple places

public class WorkFlow
{
    public static int CreateNewWorkFlow(int requestID, int requesterID, string department)
    {
        int workflowID = 0;

        string query = "INSERT INTO WorkFlowInstances (RequestID, RequesterID, Department, CurrentStatus, CreatedDate) " +
                       "VALUES (" + requestID + ", " + requesterID + ", '" + department + "', 1, GETDATE()); " +
                       "SELECT SCOPE_IDENTITY();";

        object result = DBHelper.ExecuteScalar(query);
        if (result != null)
            workflowID = Convert.ToInt32(result);

        // insert first workflow step record
        string stepQuery = "INSERT INTO WorkFlowSteps (WorkFlowID, StepNumber, StepName, AssignedTo, Status, CreatedDate) " +
                           "VALUES (" + workflowID + ", 1, 'Line Manager Approval', " +
                           "(SELECT ManagerID FROM Employees WHERE EmployeeID = " + requesterID + "), " +
                           "2, GETDATE())";
        DBHelper.ExecuteNonQuery(stepQuery);

        return workflowID;
    }

    public static bool Submit(int requestID, int requesterID)
    {
        try
        {
            // get request details - separate DB call (could be done in one join but was added later)
            string reqQuery = "SELECT * FROM EquipmentRequests WHERE RequestID = " + requestID;
            DataTable reqDT = DBHelper.GetData(reqQuery);

            if (reqDT.Rows.Count == 0)
                return false;

            DataRow req = reqDT.Rows[0];
            decimal estimatedCost = Convert.ToDecimal(req["EstimatedCost"]);
            string department = req["Department"].ToString();
            string equipmentType = req["EquipmentType"].ToString();
            int quantity = Convert.ToInt32(req["Quantity"]);
            string justification = req["Justification"].ToString();
            string priority = req["Priority"].ToString();

            // update request status to Submitted (2)
            string updateQuery = "UPDATE EquipmentRequests SET Status = 2, SubmitDate = GETDATE() " +
                                 "WHERE RequestID = " + requestID;
            DBHelper.ExecuteNonQuery(updateQuery);

            // create workflow instance
            int workflowID = CreateNewWorkFlow(requestID, requesterID, department);

            // get requester info for email
            string empQuery = "SELECT * FROM Employees WHERE EmployeeID = " + requesterID;
            DataTable empDT = DBHelper.GetData(empQuery);
            string requesterName  = empDT.Rows[0]["FullName"].ToString();
            string requesterEmail = empDT.Rows[0]["Email"].ToString();
            int managerID         = Convert.ToInt32(empDT.Rows[0]["ManagerID"]);

            // get manager email - another separate DB call
            string mgrQuery = "SELECT Email, FullName FROM Employees WHERE EmployeeID = " + managerID;
            DataTable mgrDT = DBHelper.GetData(mgrQuery);
            string managerEmail = mgrDT.Rows[0]["Email"].ToString();
            string managerName  = mgrDT.Rows[0]["FullName"].ToString();

            // build approval URL - hardcoded server name
            string approvalUrl = "http://techcorp-portal/EquipmentRequest.aspx?action=approve&reqid=" + requestID +
                                 "&wfid=" + workflowID + "&level=1";

            // email body built with string concat - no templates
            string emailBody = "<html><body>" +
                               "<p>Dear " + managerName + ",</p>" +
                               "<p>Employee <strong>" + requesterName + "</strong> from department <strong>" + department + "</strong> " +
                               "has submitted an equipment request that requires your approval.</p>" +
                               "<table border='1' cellpadding='5'>" +
                               "<tr><td><b>Equipment Type</b></td><td>" + equipmentType + "</td></tr>" +
                               "<tr><td><b>Quantity</b></td><td>" + quantity + "</td></tr>" +
                               "<tr><td><b>Justification</b></td><td>" + justification + "</td></tr>" +
                               "<tr><td><b>Priority</b></td><td>" + priority + "</td></tr>" +
                               "<tr><td><b>Estimated Cost</b></td><td>" + estimatedCost.ToString("N2") + " EGP</td></tr>" +
                               "</table>" +
                               "<br/><p><a href='" + approvalUrl + "'>Click here to review and approve/reject</a></p>" +
                               "<p>This is an automated message from TechCorp Equipment Portal.</p>" +
                               "</body></html>";

            General.SendEmail(managerEmail, "Action Required: Equipment Request from " + requesterName, emailBody);

            // confirm submission email to requester
            string confirmBody = "<html><body>" +
                                 "<p>Dear " + requesterName + ",</p>" +
                                 "<p>Your equipment request has been submitted successfully and is pending approval from your line manager.</p>" +
                                 "<p><b>Request ID:</b> " + requestID + "</p>" +
                                 "<p>You will receive updates as your request moves through the approval process.</p>" +
                                 "</body></html>";

            General.SendEmail(requesterEmail, "Equipment Request Submitted - Ref #" + requestID, confirmBody);

            // log activity
            General.LogActivity(requesterName, "Submitted equipment request #" + requestID);

            return true;
        }
        catch (Exception ex)
        {
            General.LogError("WorkFlow.Submit failed for RequestID=" + requestID + " | " + ex.Message);
            return false;
        }
    }

    public static bool ApproveLevel1(int requestID, int workflowID, int approverID, string comments)
    {
        try
        {
            // update workflow step
            string stepUpdate = "UPDATE WorkFlowSteps SET Status = 3, ApproverID = " + approverID +
                                ", ApprovalDate = GETDATE(), Comments = '" + comments + "' " +
                                "WHERE WorkFlowID = " + workflowID + " AND StepNumber = 1";
            DBHelper.ExecuteNonQuery(stepUpdate);

            // get cost to decide next step
            string costQuery = "SELECT EstimatedCost, RequesterID, Department, EquipmentType, Quantity FROM EquipmentRequests WHERE RequestID = " + requestID;
            DataTable costDT = DBHelper.GetData(costQuery);
            decimal cost      = Convert.ToDecimal(costDT.Rows[0]["EstimatedCost"]);
            int requesterID   = Convert.ToInt32(costDT.Rows[0]["RequesterID"]);
            string department = costDT.Rows[0]["Department"].ToString();

            // update request status to 3 (Pending IT Head)
            DBHelper.ExecuteNonQuery("UPDATE EquipmentRequests SET Status = 3 WHERE RequestID = " + requestID);

            // insert step 2 workflow record
            string step2Query = "INSERT INTO WorkFlowSteps (WorkFlowID, StepNumber, StepName, Status, CreatedDate) " +
                                "VALUES (" + workflowID + ", 2, 'IT Department Head Approval', 2, GETDATE())";
            DBHelper.ExecuteNonQuery(step2Query);

            // send email notification to IT Head
            SendEmailNotification(requestID, workflowID, 2, requesterID, approverID);

            return true;
        }
        catch (Exception ex)
        {
            General.LogError("WorkFlow.ApproveLevel1 failed: " + ex.Message);
            return false;
        }
    }

    public static bool ApproveLevel2(int requestID, int workflowID, int approverID, string comments)
    {
        try
        {
            string stepUpdate = "UPDATE WorkFlowSteps SET Status = 3, ApproverID = " + approverID +
                                ", ApprovalDate = GETDATE(), Comments = '" + comments + "' " +
                                "WHERE WorkFlowID = " + workflowID + " AND StepNumber = 2";
            DBHelper.ExecuteNonQuery(stepUpdate);

            string costQuery = "SELECT EstimatedCost, RequesterID FROM EquipmentRequests WHERE RequestID = " + requestID;
            DataTable dt  = DBHelper.GetData(costQuery);
            decimal cost  = Convert.ToDecimal(dt.Rows[0]["EstimatedCost"]);
            int requesterID = Convert.ToInt32(dt.Rows[0]["RequesterID"]);

            if (cost > 5000)  // hardcoded finance threshold - also hardcoded on EquipmentRequest.aspx.cs
            {
                // needs Finance approval
                DBHelper.ExecuteNonQuery("UPDATE EquipmentRequests SET Status = 4 WHERE RequestID = " + requestID);

                string step3Query = "INSERT INTO WorkFlowSteps (WorkFlowID, StepNumber, StepName, Status, CreatedDate) " +
                                    "VALUES (" + workflowID + ", 3, 'Finance Approval', 2, GETDATE())";
                DBHelper.ExecuteNonQuery(step3Query);

                SendEmailNotification(requestID, workflowID, 3, requesterID, approverID);
            }
            else
            {
                // skip Finance, go straight to IT Ops
                DBHelper.ExecuteNonQuery("UPDATE EquipmentRequests SET Status = 5 WHERE RequestID = " + requestID);

                string step4Query = "INSERT INTO WorkFlowSteps (WorkFlowID, StepNumber, StepName, Status, CreatedDate) " +
                                    "VALUES (" + workflowID + ", 4, 'IT Operations', 2, GETDATE())";
                DBHelper.ExecuteNonQuery(step4Query);

                SendEmailNotification(requestID, workflowID, 4, requesterID, approverID);
            }

            return true;
        }
        catch (Exception ex)
        {
            General.LogError("WorkFlow.ApproveLevel2 failed: " + ex.Message);
            return false;
        }
    }

    public static bool ApproveLevel3Finance(int requestID, int workflowID, int approverID, string comments)
    {
        try
        {
            string stepUpdate = "UPDATE WorkFlowSteps SET Status = 3, ApproverID = " + approverID +
                                ", ApprovalDate = GETDATE(), Comments = '" + comments + "' " +
                                "WHERE WorkFlowID = " + workflowID + " AND StepNumber = 3";
            DBHelper.ExecuteNonQuery(stepUpdate);

            int requesterID = Convert.ToInt32(DBHelper.ExecuteScalar(
                "SELECT RequesterID FROM EquipmentRequests WHERE RequestID = " + requestID));

            // update to status 5 - pending IT Ops
            DBHelper.ExecuteNonQuery("UPDATE EquipmentRequests SET Status = 5 WHERE RequestID = " + requestID);

            string step4Query = "INSERT INTO WorkFlowSteps (WorkFlowID, StepNumber, StepName, Status, CreatedDate) " +
                                "VALUES (" + workflowID + ", 4, 'IT Operations', 2, GETDATE())";
            DBHelper.ExecuteNonQuery(step4Query);

            SendEmailNotification(requestID, workflowID, 4, requesterID, approverID);

            return true;
        }
        catch (Exception ex)
        {
            General.LogError("WorkFlow.ApproveLevel3Finance failed: " + ex.Message);
            return false;
        }
    }

    public static bool ApproveLevel4ITOps(int requestID, int workflowID, int approverID, string comments)
    {
        try
        {
            string stepUpdate = "UPDATE WorkFlowSteps SET Status = 3, ApproverID = " + approverID +
                                ", ApprovalDate = GETDATE(), Comments = '" + comments + "' " +
                                "WHERE WorkFlowID = " + workflowID + " AND StepNumber = 4";
            DBHelper.ExecuteNonQuery(stepUpdate);

            // fully approved!
            DBHelper.ExecuteNonQuery("UPDATE EquipmentRequests SET Status = 6, CompletionDate = GETDATE() WHERE RequestID = " + requestID);
            DBHelper.ExecuteNonQuery("UPDATE WorkFlowInstances SET CurrentStatus = 6, CompletedDate = GETDATE() WHERE WorkFlowID = " + workflowID);

            int requesterID = Convert.ToInt32(DBHelper.ExecuteScalar(
                "SELECT RequesterID FROM EquipmentRequests WHERE RequestID = " + requestID));

            SendEmailNotification(requestID, workflowID, 6, requesterID, approverID);   // 6 = fully approved

            return true;
        }
        catch (Exception ex)
        {
            General.LogError("WorkFlow.ApproveLevel4ITOps failed: " + ex.Message);
            return false;
        }
    }

    public static bool Reject(int requestID, int workflowID, int approverID, int level, string reason)
    {
        try
        {
            // rejection status depends on level - parallel magic numbers
            int rejectionStatus = 0;
            if (level == 1) rejectionStatus = 7;
            if (level == 2) rejectionStatus = 8;
            if (level == 3) rejectionStatus = 9;
            if (level == 4) rejectionStatus = 10;

            string stepUpdate = "UPDATE WorkFlowSteps SET Status = 4, ApproverID = " + approverID +
                                ", ApprovalDate = GETDATE(), Comments = '" + reason + "' " +
                                "WHERE WorkFlowID = " + workflowID + " AND StepNumber = " + level;
            DBHelper.ExecuteNonQuery(stepUpdate);

            string rejectQuery = "UPDATE EquipmentRequests SET Status = " + rejectionStatus +
                                 ", RejectionReason = '" + reason + "' WHERE RequestID = " + requestID;
            DBHelper.ExecuteNonQuery(rejectQuery);

            DBHelper.ExecuteNonQuery("UPDATE WorkFlowInstances SET CurrentStatus = " + rejectionStatus +
                                     " WHERE WorkFlowID = " + workflowID);

            int requesterID = Convert.ToInt32(DBHelper.ExecuteScalar(
                "SELECT RequesterID FROM EquipmentRequests WHERE RequestID = " + requestID));

            // send rejection email directly in this method (mixing concerns)
            string requesterEmail = General.GetEmployeeEmail(requesterID);
            string requesterName  = DBHelper.ExecuteScalar(
                "SELECT FullName FROM Employees WHERE EmployeeID = " + requesterID).ToString();
            string approverName   = DBHelper.ExecuteScalar(
                "SELECT FullName FROM Employees WHERE EmployeeID = " + approverID).ToString();

            string levelName = "";
            if (level == 1) levelName = "Line Manager";
            if (level == 2) levelName = "IT Department Head";
            if (level == 3) levelName = "Finance";
            if (level == 4) levelName = "IT Operations";

            string emailBody = "<html><body>" +
                               "<p>Dear " + requesterName + ",</p>" +
                               "<p>We regret to inform you that your equipment request (Ref #" + requestID + ") " +
                               "has been <strong>rejected</strong> by " + levelName + " (" + approverName + ").</p>" +
                               "<p><b>Reason:</b> " + reason + "</p>" +
                               "<p>If you believe this decision is incorrect, please contact your manager or submit a new request.</p>" +
                               "</body></html>";

            General.SendEmail(requesterEmail, "Equipment Request Rejected - Ref #" + requestID, emailBody);

            General.LogActivity(approverName, "Rejected equipment request #" + requestID + " at level " + level);

            return true;
        }
        catch (Exception ex)
        {
            General.LogError("WorkFlow.Reject failed: " + ex.Message);
            return false;
        }
    }

    // sends notification emails - business logic and email content mixed together
    // level meaning: 2=notify IT Head, 3=notify Finance, 4=notify IT Ops, 6=fully approved
    private static void SendEmailNotification(int requestID, int workflowID, int level, int requesterID, int approverID)
    {
        string reqQuery = "SELECT * FROM EquipmentRequests WHERE RequestID = " + requestID;
        DataTable reqDT = DBHelper.GetData(reqQuery);
        if (reqDT.Rows.Count == 0) return;

        DataRow req     = reqDT.Rows[0];
        string equipType    = req["EquipmentType"].ToString();
        int    quantity     = Convert.ToInt32(req["Quantity"]);
        decimal cost        = Convert.ToDecimal(req["EstimatedCost"]);
        string justification = req["Justification"].ToString();
        string priority     = req["Priority"].ToString();

        string requesterName  = DBHelper.ExecuteScalar("SELECT FullName FROM Employees WHERE EmployeeID = " + requesterID).ToString();
        string requesterEmail = General.GetEmployeeEmail(requesterID);
        string approverName   = DBHelper.ExecuteScalar("SELECT FullName FROM Employees WHERE EmployeeID = " + approverID).ToString();

        string approvalUrl = "http://techcorp-portal/EquipmentRequest.aspx?action=approve&reqid=" + requestID +
                             "&wfid=" + workflowID + "&level=" + level;

        string recipientEmail = "";
        string recipientName  = "";
        string stepName       = "";

        if (level == 2)
        {
            recipientEmail = General.GetITHeadEmail();
            recipientName  = "IT Department Head";
            stepName       = "IT Department Head Approval";
        }
        else if (level == 3)
        {
            recipientEmail = General.GetFinanceHeadEmail();
            recipientName  = "Finance Department Head";
            stepName       = "Finance Approval";
        }
        else if (level == 4)
        {
            recipientEmail = General.GetITOperationsEmail();
            recipientName  = "IT Operations Team";
            stepName       = "IT Operations Fulfillment";
        }
        else if (level == 6)
        {
            // fully approved - notify requester
            string fullyApprovedBody = "<html><body>" +
                                       "<p>Dear " + requesterName + ",</p>" +
                                       "<p>Your equipment request (Ref #" + requestID + ") has been <strong>fully approved</strong> " +
                                       "and has been assigned to IT Operations for fulfillment.</p>" +
                                       "<p>Equipment will be delivered within 5-7 working days.</p>" +
                                       "</body></html>";
            General.SendEmail(requesterEmail, "Equipment Request Approved - Ref #" + requestID, fullyApprovedBody);
            return;
        }

        string emailBody = "<html><body>" +
                           "<p>Dear " + recipientName + ",</p>" +
                           "<p>An equipment request from <strong>" + requesterName + "</strong> requires your approval.</p>" +
                           "<table border='1' cellpadding='5'>" +
                           "<tr><td><b>Request ID</b></td><td>" + requestID + "</td></tr>" +
                           "<tr><td><b>Equipment Type</b></td><td>" + equipType + "</td></tr>" +
                           "<tr><td><b>Quantity</b></td><td>" + quantity + "</td></tr>" +
                           "<tr><td><b>Priority</b></td><td>" + priority + "</td></tr>" +
                           "<tr><td><b>Estimated Cost</b></td><td>" + cost.ToString("N2") + " EGP</td></tr>" +
                           "<tr><td><b>Justification</b></td><td>" + justification + "</td></tr>" +
                           "<tr><td><b>Previously Approved By</b></td><td>" + approverName + "</td></tr>" +
                           "</table>" +
                           "<br/><p><a href='" + approvalUrl + "'>Click here to review and approve/reject</a></p>" +
                           "</body></html>";

        General.SendEmail(recipientEmail, "Action Required: Equipment Request Approval - " + stepName + " (Ref #" + requestID + ")", emailBody);

        // also notify requester of progress - another email sent in same method
        string progressBody = "<html><body>" +
                              "<p>Dear " + requesterName + ",</p>" +
                              "<p>Your equipment request (Ref #" + requestID + ") has been approved by " + approverName +
                              " and has been forwarded to " + stepName + ".</p>" +
                              "</body></html>";
        General.SendEmail(requesterEmail, "Equipment Request Update - Ref #" + requestID, progressBody);
    }

    public static DataTable GetWorkFlowHistory(int requestID)
    {
        string query = @"SELECT wfs.StepNumber, wfs.StepName, wfs.Status, wfs.ApprovalDate,
                                wfs.Comments, e.FullName as ApproverName
                         FROM WorkFlowSteps wfs
                         LEFT JOIN Employees e ON wfs.ApproverID = e.EmployeeID
                         INNER JOIN WorkFlowInstances wfi ON wfs.WorkFlowID = wfi.WorkFlowID
                         WHERE wfi.RequestID = " + requestID +
                       " ORDER BY wfs.StepNumber";
        return DBHelper.GetData(query);
    }
}
