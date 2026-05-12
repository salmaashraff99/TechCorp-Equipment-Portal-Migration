using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;

// EquipmentRequest.aspx.cs - TechCorp International Equipment Portal
// Created: 2010-03-01 | Last Modified: 2022-09-14
// Authors (in order of join): Ahmed Mostafa, Khaled Ibrahim, Nada Samir, Omar Fathy
//
// This page handles: new requests, viewing, editing, approving, rejecting, printing.
// God class - everyone added their feature here because "it was easier".
// 820 lines and counting. Do not add more without refactoring first (nobody ever does).

public partial class EquipmentRequest : System.Web.UI.Page
{
    // hardcoded connection string - same as DBHelper.cs (yes, it's duplicated)
    private string connStr = "Data Source=TECHCORP-SQL01;Initial Catalog=EquipmentPortalDB;User ID=sa;Password=Admin@123;";

    // magic numbers repeated from WorkFlow.cs - any change must be done in both files
    private const int STATUS_DRAFT             = 1;
    private const int STATUS_PENDING_MANAGER   = 2;
    private const int STATUS_PENDING_IT_HEAD   = 3;
    private const int STATUS_PENDING_FINANCE   = 4;
    private const int STATUS_PENDING_IT_OPS    = 5;
    private const int STATUS_FULLY_APPROVED    = 6;
    private const int STATUS_REJECTED_MANAGER  = 7;
    private const int STATUS_REJECTED_IT_HEAD  = 8;
    private const int STATUS_REJECTED_FINANCE  = 9;
    private const int STATUS_REJECTED_IT_OPS   = 10;
    private const int STATUS_CANCELLED         = 11;

    // finance approval threshold - also hardcoded in WorkFlow.cs
    private const decimal FINANCE_THRESHOLD = 5000m;

    protected void Page_Load(object sender, EventArgs e)
    {
        // no authentication check using proper membership - just session check
        if (Session["UserID"] == null || Session["UserEmail"] == null)
        {
            Response.Redirect("Login.aspx?msg=session_expired");
            return;
        }

        int userID     = Convert.ToInt32(Session["UserID"]);
        string userRole = Session["UserRole"] != null ? Session["UserRole"].ToString() : "";

        if (!IsPostBack)
        {
            // read mode from querystring - drives the whole page behavior
            string action   = Request.QueryString["action"]  != null ? Request.QueryString["action"]  : "new";
            string reqIDStr = Request.QueryString["reqid"]   != null ? Request.QueryString["reqid"]   : "0";
            string wfIDStr  = Request.QueryString["wfid"]    != null ? Request.QueryString["wfid"]    : "0";
            string levelStr = Request.QueryString["level"]   != null ? Request.QueryString["level"]   : "0";

            int requestID  = 0;
            int workflowID = 0;
            int level      = 0;

            int.TryParse(reqIDStr,  out requestID);
            int.TryParse(wfIDStr,   out workflowID);
            int.TryParse(levelStr,  out level);

            // store in session so postbacks can access them (instead of hidden fields)
            Session["CurrentRequestID"]  = requestID;
            Session["CurrentWorkFlowID"] = workflowID;
            Session["CurrentLevel"]      = level;
            Session["CurrentAction"]     = action;

            // populate equipment type dropdown - no caching, hits DB every page load
            string equipQuery = "SELECT TypeID, TypeName FROM EquipmentTypes WHERE IsActive = 1 ORDER BY TypeName";
            DataTable equipDT = DBHelper.GetData(equipQuery);
            ddlEquipmentType.DataSource     = equipDT;
            ddlEquipmentType.DataTextField  = "TypeName";
            ddlEquipmentType.DataValueField = "TypeID";
            ddlEquipmentType.DataBind();
            ddlEquipmentType.Items.Insert(0, new ListItem("-- Select Equipment Type --", "0"));

            // populate priority dropdown - hardcoded items instead of DB table
            ddlPriority.Items.Add(new ListItem("Low",      "1"));
            ddlPriority.Items.Add(new ListItem("Normal",   "2"));
            ddlPriority.Items.Add(new ListItem("High",     "3"));
            ddlPriority.Items.Add(new ListItem("Critical", "4"));

            if (action == "new")
            {
                // show new request form
                pnlRequestForm.Visible    = true;
                pnlApprovalForm.Visible   = false;
                pnlViewRequest.Visible    = false;
                btnSubmit.Visible         = true;
                btnSaveDraft.Visible      = true;
                btnApprove.Visible        = false;
                btnReject.Visible         = false;

                // pre-fill employee info from session
                txtRequesterName.Text     = Session["UserName"] != null ? Session["UserName"].ToString() : "";
                txtRequesterEmail.Text    = Session["UserEmail"].ToString();
                txtDepartment.Text        = Session["Department"] != null ? Session["Department"].ToString() : "";
                lblRequestDate.Text       = DateTime.Now.ToString("dd/MM/yyyy");
            }
            else if (action == "view" && requestID > 0)
            {
                // load and display request - huge inline data loading
                string viewQuery = @"SELECT er.*,
                                            e.FullName as RequesterName,
                                            e.Email as RequesterEmail,
                                            e.Department,
                                            et.TypeName as EquipmentTypeName
                                     FROM EquipmentRequests er
                                     INNER JOIN Employees e  ON er.RequesterID = e.EmployeeID
                                     INNER JOIN EquipmentTypes et ON er.EquipmentTypeID = et.TypeID
                                     WHERE er.RequestID = " + requestID;   // SQL injection - requestID from querystring

                DataTable viewDT = DBHelper.GetData(viewQuery);

                if (viewDT.Rows.Count == 0)
                {
                    lblMessage.Text      = "Request not found.";
                    lblMessage.ForeColor = System.Drawing.Color.Red;
                    return;
                }

                DataRow row = viewDT.Rows[0];

                // fill all labels - no model binding, manual field by field
                lblViewRequestID.Text      = row["RequestID"].ToString();
                lblViewRequester.Text      = row["RequesterName"].ToString();
                lblViewDepartment.Text     = row["Department"].ToString();
                lblViewEquipType.Text      = row["EquipmentTypeName"].ToString();
                lblViewQuantity.Text       = row["Quantity"].ToString();
                lblViewPriority.Text       = GetPriorityName(Convert.ToInt32(row["Priority"]));
                lblViewCost.Text           = Convert.ToDecimal(row["EstimatedCost"]).ToString("N2") + " EGP";
                lblViewJustification.Text  = row["Justification"].ToString();
                lblViewStatus.Text         = GetStatusName(Convert.ToInt32(row["Status"]));
                lblViewSubmitDate.Text     = row["SubmitDate"] != DBNull.Value
                                                ? Convert.ToDateTime(row["SubmitDate"]).ToString("dd/MM/yyyy HH:mm")
                                                : "Not yet submitted";

                // load workflow history into GridView
                DataTable historyDT = WorkFlow.GetWorkFlowHistory(requestID);
                gvWorkFlowHistory.DataSource = historyDT;
                gvWorkFlowHistory.DataBind();

                pnlRequestForm.Visible  = false;
                pnlApprovalForm.Visible = false;
                pnlViewRequest.Visible  = true;
                btnSubmit.Visible       = false;
                btnApprove.Visible      = false;
                btnReject.Visible       = false;
            }
            else if (action == "approve" && requestID > 0)
            {
                // approval form - check if user is the correct approver for this level
                bool canApprove = false;

                if (level == 1)
                {
                    // line manager: check if current user is the requester's manager
                    object managerIDObj = DBHelper.ExecuteScalar(
                        "SELECT e.ManagerID FROM EquipmentRequests er " +
                        "INNER JOIN Employees e ON er.RequesterID = e.EmployeeID " +
                        "WHERE er.RequestID = " + requestID);

                    if (managerIDObj != null && Convert.ToInt32(managerIDObj) == userID)
                        canApprove = true;
                }
                else if (level == 2)
                {
                    // IT Head: check department 7 and role
                    if (userRole == "DeptHead" && Session["DepartmentID"] != null && Convert.ToInt32(Session["DepartmentID"]) == 7)
                        canApprove = true;
                }
                else if (level == 3)
                {
                    // Finance: check department 4 and role
                    if (userRole == "DeptHead" && Session["DepartmentID"] != null && Convert.ToInt32(Session["DepartmentID"]) == 4)
                        canApprove = true;
                }
                else if (level == 4)
                {
                    // IT Ops
                    if (userRole == "ITOps")
                        canApprove = true;
                }

                if (!canApprove)
                {
                    lblMessage.Text      = "You are not authorized to approve this request at this level.";
                    lblMessage.ForeColor = System.Drawing.Color.Red;
                    pnlApprovalForm.Visible = false;
                    return;
                }

                // load request for the approval form
                string approveQuery = @"SELECT er.*, et.TypeName, e.FullName, e.Department
                                        FROM EquipmentRequests er
                                        INNER JOIN EquipmentTypes et ON er.EquipmentTypeID = et.TypeID
                                        INNER JOIN Employees e ON er.RequesterID = e.EmployeeID
                                        WHERE er.RequestID = " + requestID;
                DataTable approveDT = DBHelper.GetData(approveQuery);
                DataRow approveRow  = approveDT.Rows[0];

                lblApproveRequestID.Text   = requestID.ToString();
                lblApproveRequester.Text   = approveRow["FullName"].ToString();
                lblApproveEquipType.Text   = approveRow["TypeName"].ToString();
                lblApproveQuantity.Text    = approveRow["Quantity"].ToString();
                lblApproveCost.Text        = Convert.ToDecimal(approveRow["EstimatedCost"]).ToString("N2") + " EGP";
                lblApproveJustification.Text = approveRow["Justification"].ToString();
                lblApprovePriority.Text    = GetPriorityName(Convert.ToInt32(approveRow["Priority"]));
                lblApproveLevel.Text       = GetLevelName(level);

                pnlRequestForm.Visible  = false;
                pnlApprovalForm.Visible = true;
                pnlViewRequest.Visible  = false;
                btnSubmit.Visible       = false;
                btnApprove.Visible      = true;
                btnReject.Visible       = true;
            }
        }
    }

    // btnSubmit_Click - god method: validates, saves, creates workflow, sends emails
    // all in one event handler. Impossible to unit test.
    protected void btnSubmit_Click(object sender, EventArgs e)
    {
        // manual validation - no ValidationSummary patterns used correctly
        bool isValid = true;
        string validationMsg = "";

        if (ddlEquipmentType.SelectedValue == "0")
        {
            isValid = false;
            validationMsg += "Please select an equipment type.<br/>";
        }
        if (string.IsNullOrEmpty(txtQuantity.Text) || Convert.ToInt32(txtQuantity.Text) <= 0)
        {
            isValid = false;
            validationMsg += "Quantity must be greater than zero.<br/>";
        }
        if (string.IsNullOrEmpty(txtJustification.Text) || txtJustification.Text.Trim().Length < 20)
        {
            isValid = false;
            validationMsg += "Justification must be at least 20 characters.<br/>";
        }
        if (string.IsNullOrEmpty(txtEstimatedCost.Text))
        {
            isValid = false;
            validationMsg += "Please enter an estimated cost.<br/>";
        }

        if (!isValid)
        {
            lblMessage.Text      = validationMsg;
            lblMessage.ForeColor = System.Drawing.Color.Red;
            return;
        }

        int    userID        = Convert.ToInt32(Session["UserID"]);
        int    equipTypeID   = Convert.ToInt32(ddlEquipmentType.SelectedValue);
        int    quantity      = Convert.ToInt32(txtQuantity.Text);
        int    priority      = Convert.ToInt32(ddlPriority.SelectedValue);
        decimal estimatedCost = Convert.ToDecimal(txtEstimatedCost.Text);
        string justification = txtJustification.Text.Trim();
        string department    = Session["Department"].ToString();

        // direct SQL insert with string concatenation in UI layer - SQL injection risk
        // justification is user input going directly into the query
        string insertQuery = "INSERT INTO EquipmentRequests " +
                             "(RequesterID, EquipmentTypeID, Quantity, Priority, EstimatedCost, Justification, Department, Status, RequestDate) " +
                             "VALUES (" +
                             userID + ", " +
                             equipTypeID + ", " +
                             quantity + ", " +
                             priority + ", " +
                             estimatedCost + ", " +
                             "'" + justification + "', " +       // <-- SQL injection: user input unescaped
                             "'" + department + "', " +
                             STATUS_DRAFT + ", " +
                             "GETDATE()); SELECT SCOPE_IDENTITY();";

        object newIDObj = DBHelper.ExecuteScalar(insertQuery);
        if (newIDObj == null)
        {
            lblMessage.Text      = "An error occurred while saving your request. Please try again.";
            lblMessage.ForeColor = System.Drawing.Color.Red;
            return;
        }

        int newRequestID = Convert.ToInt32(newIDObj);

        // submit through workflow - still in UI event handler
        bool submitted = WorkFlow.Submit(newRequestID, userID);

        if (submitted)
        {
            // update session request count - session being used as a cache
            if (Session["MyRequestCount"] != null)
                Session["MyRequestCount"] = Convert.ToInt32(Session["MyRequestCount"]) + 1;

            // log to DB - business logic in UI layer
            string logQuery = "INSERT INTO RequestAuditLog (RequestID, Action, PerformedBy, ActionDate, Notes) " +
                              "VALUES (" + newRequestID + ", 'SUBMITTED', " + userID + ", GETDATE(), 'Submitted via web portal')";
            DBHelper.ExecuteNonQuery(logQuery);

            // send confirmation email directly from UI layer
            string userEmail = Session["UserEmail"].ToString();
            string userName  = Session["UserName"].ToString();
            string emailBody = "<html><body>" +
                               "<p>Dear " + userName + ",</p>" +
                               "<p>Your request has been submitted. Reference number: <b>" + newRequestID + "</b></p>" +
                               "<p>You will receive updates as it moves through the approval process.</p>" +
                               "</body></html>";
            // email sent directly from Page event - if SMTP fails the user sees a confusing error
            General.SendEmail(userEmail, "Request Submitted Successfully - Ref #" + newRequestID, emailBody);

            // redirect on success
            Response.Redirect("MyRequests.aspx?msg=submitted&ref=" + newRequestID);
        }
        else
        {
            lblMessage.Text      = "Request saved but workflow could not be started. Please contact IT Support.";
            lblMessage.ForeColor = System.Drawing.Color.Orange;
        }
    }

    protected void btnSaveDraft_Click(object sender, EventArgs e)
    {
        // same SQL injection problems as btnSubmit_Click - copy-pasted and slightly modified
        int    userID       = Convert.ToInt32(Session["UserID"]);
        int    equipTypeID  = Convert.ToInt32(ddlEquipmentType.SelectedValue);
        int    quantity     = txtQuantity.Text != "" ? Convert.ToInt32(txtQuantity.Text) : 0;
        int    priority     = Convert.ToInt32(ddlPriority.SelectedValue);
        string justification = txtJustification.Text.Trim();
        string department   = Session["Department"] != null ? Session["Department"].ToString() : "";

        decimal estimatedCost = 0;
        decimal.TryParse(txtEstimatedCost.Text, out estimatedCost);

        string draftQuery = "INSERT INTO EquipmentRequests " +
                            "(RequesterID, EquipmentTypeID, Quantity, Priority, EstimatedCost, Justification, Department, Status, RequestDate) " +
                            "VALUES (" +
                            userID + ", " +
                            equipTypeID + ", " +
                            quantity + ", " +
                            priority + ", " +
                            estimatedCost + ", " +
                            "'" + justification + "', " +    // SQL injection again
                            "'" + department + "', " +
                            STATUS_DRAFT + ", " +
                            "GETDATE()); SELECT SCOPE_IDENTITY();";

        object draftIDObj = DBHelper.ExecuteScalar(draftQuery);
        if (draftIDObj != null)
        {
            lblMessage.Text      = "Draft saved. Reference #" + draftIDObj.ToString();
            lblMessage.ForeColor = System.Drawing.Color.Green;
        }
        else
        {
            lblMessage.Text      = "Failed to save draft.";
            lblMessage.ForeColor = System.Drawing.Color.Red;
        }
    }

    protected void btnApprove_Click(object sender, EventArgs e)
    {
        int requestID  = Convert.ToInt32(Session["CurrentRequestID"]);
        int workflowID = Convert.ToInt32(Session["CurrentWorkFlowID"]);
        int level      = Convert.ToInt32(Session["CurrentLevel"]);
        int approverID = Convert.ToInt32(Session["UserID"]);
        string comments = txtApprovalComments.Text.Trim();

        bool result = false;

        if (level == 1)
            result = WorkFlow.ApproveLevel1(requestID, workflowID, approverID, comments);
        else if (level == 2)
            result = WorkFlow.ApproveLevel2(requestID, workflowID, approverID, comments);
        else if (level == 3)
            result = WorkFlow.ApproveLevel3Finance(requestID, workflowID, approverID, comments);
        else if (level == 4)
            result = WorkFlow.ApproveLevel4ITOps(requestID, workflowID, approverID, comments);

        if (result)
        {
            // log approval - raw SQL in UI event handler
            string logQuery = "INSERT INTO RequestAuditLog (RequestID, Action, PerformedBy, ActionDate, Notes) " +
                              "VALUES (" + requestID + ", 'APPROVED_L" + level + "', " + approverID + ", GETDATE(), '" + comments + "')";
            DBHelper.ExecuteNonQuery(logQuery);

            // update approver's approval count in Employees table - weird coupling
            DBHelper.ExecuteNonQuery("UPDATE Employees SET TotalApprovalsGiven = ISNULL(TotalApprovalsGiven,0) + 1 WHERE EmployeeID = " + approverID);

            lblMessage.Text      = "Request approved successfully.";
            lblMessage.ForeColor = System.Drawing.Color.Green;

            // send a 2nd confirmation email from the UI layer (WorkFlow already sends one)
            string approverEmail = Session["UserEmail"].ToString();
            General.SendEmail(approverEmail,
                "Approval Confirmed - Ref #" + requestID,
                "<p>You have successfully approved equipment request #" + requestID + " at level " + level + ".</p>");

            Response.Redirect("Approvals.aspx?msg=approved");
        }
        else
        {
            lblMessage.Text      = "Approval failed. Please try again or contact IT Support.";
            lblMessage.ForeColor = System.Drawing.Color.Red;
        }
    }

    protected void btnReject_Click(object sender, EventArgs e)
    {
        int requestID  = Convert.ToInt32(Session["CurrentRequestID"]);
        int workflowID = Convert.ToInt32(Session["CurrentWorkFlowID"]);
        int level      = Convert.ToInt32(Session["CurrentLevel"]);
        int approverID = Convert.ToInt32(Session["UserID"]);
        string reason  = txtRejectionReason.Text.Trim();

        if (string.IsNullOrEmpty(reason))
        {
            lblMessage.Text      = "Please provide a reason for rejection.";
            lblMessage.ForeColor = System.Drawing.Color.Red;
            return;
        }

        bool result = WorkFlow.Reject(requestID, workflowID, approverID, level, reason);

        if (result)
        {
            string logQuery = "INSERT INTO RequestAuditLog (RequestID, Action, PerformedBy, ActionDate, Notes) " +
                              "VALUES (" + requestID + ", 'REJECTED_L" + level + "', " + approverID + ", GETDATE(), '" + reason + "')";
            DBHelper.ExecuteNonQuery(logQuery);

            lblMessage.Text      = "Request rejected.";
            lblMessage.ForeColor = System.Drawing.Color.Orange;
            Response.Redirect("Approvals.aspx?msg=rejected");
        }
        else
        {
            lblMessage.Text      = "Rejection failed. Please try again.";
            lblMessage.ForeColor = System.Drawing.Color.Red;
        }
    }

    protected void btnCancel_Click(object sender, EventArgs e)
    {
        int requestID = Convert.ToInt32(Session["CurrentRequestID"]);
        int userID    = Convert.ToInt32(Session["UserID"]);

        // check if the current user owns this request
        object ownerIDObj = DBHelper.ExecuteScalar(
            "SELECT RequesterID FROM EquipmentRequests WHERE RequestID = " + requestID);

        if (ownerIDObj == null || Convert.ToInt32(ownerIDObj) != userID)
        {
            lblMessage.Text      = "You cannot cancel this request.";
            lblMessage.ForeColor = System.Drawing.Color.Red;
            return;
        }

        // only allow cancellation if still in draft or pending manager (status 1 or 2)
        object statusObj = DBHelper.ExecuteScalar(
            "SELECT Status FROM EquipmentRequests WHERE RequestID = " + requestID);
        int currentStatus = Convert.ToInt32(statusObj);

        if (currentStatus != STATUS_DRAFT && currentStatus != STATUS_PENDING_MANAGER)
        {
            lblMessage.Text      = "This request cannot be cancelled at its current stage.";
            lblMessage.ForeColor = System.Drawing.Color.Red;
            return;
        }

        DBHelper.ExecuteNonQuery("UPDATE EquipmentRequests SET Status = " + STATUS_CANCELLED + " WHERE RequestID = " + requestID);
        DBHelper.ExecuteNonQuery("UPDATE WorkFlowInstances SET CurrentStatus = " + STATUS_CANCELLED + " WHERE RequestID = " + requestID);

        General.LogActivity(Session["UserName"].ToString(), "Cancelled equipment request #" + requestID);

        Response.Redirect("MyRequests.aspx?msg=cancelled");
    }

    // ddlEquipmentType_SelectedIndexChanged: updates cost hint based on equipment type
    // autopostback = true on the dropdown
    protected void ddlEquipmentType_SelectedIndexChanged(object sender, EventArgs e)
    {
        int typeID = Convert.ToInt32(ddlEquipmentType.SelectedValue);
        if (typeID == 0)
        {
            lblCostHint.Text = "";
            return;
        }

        // direct DB call on every dropdown change - no caching
        string priceQuery = "SELECT AveragePrice, MinPrice, MaxPrice FROM EquipmentTypes WHERE TypeID = " + typeID;
        DataTable priceDT = DBHelper.GetData(priceQuery);

        if (priceDT.Rows.Count > 0)
        {
            decimal avg = Convert.ToDecimal(priceDT.Rows[0]["AveragePrice"]);
            decimal min = Convert.ToDecimal(priceDT.Rows[0]["MinPrice"]);
            decimal max = Convert.ToDecimal(priceDT.Rows[0]["MaxPrice"]);
            lblCostHint.Text = string.Format("Typical cost range: {0:N0} - {1:N0} EGP (avg: {2:N0} EGP)", min, max, avg);

            // show finance warning inline in UI
            if (avg > FINANCE_THRESHOLD)
                lblFinanceWarning.Text = "Note: Estimated cost exceeds 5,000 EGP. Finance approval will be required.";
            else
                lblFinanceWarning.Text = "";
        }
    }

    // helper: convert int status to readable string - duplicated in at least 3 other pages
    private string GetStatusName(int status)
    {
        if (status == 1)  return "Draft";
        if (status == 2)  return "Pending Line Manager Approval";
        if (status == 3)  return "Pending IT Head Approval";
        if (status == 4)  return "Pending Finance Approval";
        if (status == 5)  return "Pending IT Operations";
        if (status == 6)  return "Fully Approved";
        if (status == 7)  return "Rejected by Line Manager";
        if (status == 8)  return "Rejected by IT Head";
        if (status == 9)  return "Rejected by Finance";
        if (status == 10) return "Rejected by IT Operations";
        if (status == 11) return "Cancelled";
        return "Unknown (" + status + ")";
    }

    private string GetPriorityName(int priority)
    {
        if (priority == 1) return "Low";
        if (priority == 2) return "Normal";
        if (priority == 3) return "High";
        if (priority == 4) return "Critical";
        return "Unknown";
    }

    private string GetLevelName(int level)
    {
        if (level == 1) return "Line Manager";
        if (level == 2) return "IT Department Head";
        if (level == 3) return "Finance";
        if (level == 4) return "IT Operations";
        return "Unknown";
    }
}
