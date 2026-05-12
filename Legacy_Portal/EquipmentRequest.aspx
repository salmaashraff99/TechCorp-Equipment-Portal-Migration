<%@ Page Language="C#" AutoEventWireup="true" CodeFile="EquipmentRequest.aspx.cs" Inherits="EquipmentRequest" %>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Equipment Request - TechCorp International</title>
    <!-- inline styles everywhere - no external CSS framework, no consistency -->
    <style type="text/css">
        body { font-family: Arial, sans-serif; font-size: 13px; background-color: #f0f0f0; }
        .header { background-color: #003366; color: white; padding: 10px 20px; }
        .form-table { background-color: white; border: 1px solid #ccc; padding: 15px; margin: 10px; }
        .form-table td { padding: 5px 10px; vertical-align: top; }
        .label-cell { font-weight: bold; width: 200px; color: #333; }
        .required { color: red; }
        .section-header { background-color: #003366; color: white; padding: 5px 10px; font-weight: bold; margin-top: 10px; }
        .btn-submit { background-color: #003366; color: white; padding: 6px 20px; border: none; cursor: pointer; font-size: 13px; }
        .btn-approve { background-color: #006600; color: white; padding: 6px 20px; border: none; cursor: pointer; }
        .btn-reject  { background-color: #cc0000; color: white; padding: 6px 20px; border: none; cursor: pointer; }
        .btn-cancel  { background-color: #999999; color: white; padding: 6px 20px; border: none; cursor: pointer; }
        .warning-box { background-color: #fff3cd; border: 1px solid #ffc107; padding: 8px; margin: 5px 0; }
        .approved-status { color: green; font-weight: bold; }
        .rejected-status { color: red; font-weight: bold; }
        .pending-status  { color: orange; font-weight: bold; }
    </style>
    <script type="text/javascript">
        // client-side validation - duplicates server-side validation
        function validateForm() {
            if (document.getElementById('<%= ddlEquipmentType.ClientID %>').value == '0') {
                alert('Please select an equipment type.');
                return false;
            }
            var qty = document.getElementById('<%= txtQuantity.ClientID %>').value;
            if (qty == '' || parseInt(qty) <= 0) {
                alert('Please enter a valid quantity.');
                return false;
            }
            var just = document.getElementById('<%= txtJustification.ClientID %>').value;
            if (just.trim().length < 20) {
                alert('Justification must be at least 20 characters.');
                return false;
            }
            return true;
        }
        function confirmReject() {
            return confirm('Are you sure you want to reject this request?');
        }
    </script>
</head>
<body>
    <form id="form1" runat="server">
        <!-- Header -->
        <div class="header">
            <table width="100%">
                <tr>
                    <td><img src="images/techcorp_logo.png" height="40" alt="TechCorp" /></td>
                    <td align="right" style="color:white;">
                        Welcome, <asp:Label ID="lblWelcomeUser" runat="server" /> &nbsp;|&nbsp;
                        <a href="MyRequests.aspx" style="color:white;">My Requests</a> &nbsp;|&nbsp;
                        <a href="Approvals.aspx" style="color:white;">Pending Approvals</a> &nbsp;|&nbsp;
                        <a href="Logout.aspx" style="color:white;">Logout</a>
                    </td>
                </tr>
            </table>
        </div>

        <!-- Message area -->
        <div style="margin:10px;">
            <asp:Label ID="lblMessage" runat="server" />
        </div>

        <!-- NEW REQUEST FORM -->
        <asp:Panel ID="pnlRequestForm" runat="server" Visible="false">
            <div class="section-header">Equipment Request Form</div>
            <table class="form-table" width="100%">
                <tr>
                    <td class="label-cell">Requester Name:</td>
                    <td><asp:TextBox ID="txtRequesterName" runat="server" ReadOnly="true" BackColor="#eeeeee" Width="250px" /></td>
                    <td class="label-cell">Request Date:</td>
                    <td><asp:Label ID="lblRequestDate" runat="server" /></td>
                </tr>
                <tr>
                    <td class="label-cell">Email:</td>
                    <td><asp:TextBox ID="txtRequesterEmail" runat="server" ReadOnly="true" BackColor="#eeeeee" Width="250px" /></td>
                    <td class="label-cell">Department:</td>
                    <td><asp:TextBox ID="txtDepartment" runat="server" ReadOnly="true" BackColor="#eeeeee" Width="200px" /></td>
                </tr>
                <tr>
                    <td class="label-cell">Equipment Type <span class="required">*</span>:</td>
                    <td colspan="3">
                        <asp:DropDownList ID="ddlEquipmentType" runat="server" AutoPostBack="true"
                            OnSelectedIndexChanged="ddlEquipmentType_SelectedIndexChanged" Width="300px" />
                        <br />
                        <asp:Label ID="lblCostHint" runat="server" ForeColor="Gray" Font-Italic="true" />
                    </td>
                </tr>
                <tr>
                    <td class="label-cell">Quantity <span class="required">*</span>:</td>
                    <td><asp:TextBox ID="txtQuantity" runat="server" Width="80px" MaxLength="4" /></td>
                    <td class="label-cell">Priority <span class="required">*</span>:</td>
                    <td><asp:DropDownList ID="ddlPriority" runat="server" Width="150px" /></td>
                </tr>
                <tr>
                    <td class="label-cell">Estimated Cost (EGP) <span class="required">*</span>:</td>
                    <td>
                        <asp:TextBox ID="txtEstimatedCost" runat="server" Width="120px" />
                        <br />
                        <asp:Label ID="lblFinanceWarning" runat="server" ForeColor="OrangeRed" />
                    </td>
                    <td></td><td></td>
                </tr>
                <tr>
                    <td class="label-cell">Justification <span class="required">*</span>:</td>
                    <td colspan="3">
                        <asp:TextBox ID="txtJustification" runat="server" TextMode="MultiLine"
                            Rows="5" Width="500px" MaxLength="2000" />
                        <br /><small>Minimum 20 characters. Explain the business need.</small>
                    </td>
                </tr>
                <tr>
                    <td colspan="4" style="padding-top:15px;">
                        <asp:Button ID="btnSubmit"    runat="server" Text="Submit Request"  CssClass="btn-submit"
                            OnClick="btnSubmit_Click" OnClientClick="return validateForm();" />
                        &nbsp;
                        <asp:Button ID="btnSaveDraft" runat="server" Text="Save as Draft"   CssClass="btn-cancel"
                            OnClick="btnSaveDraft_Click" />
                    </td>
                </tr>
            </table>
        </asp:Panel>

        <!-- APPROVAL FORM -->
        <asp:Panel ID="pnlApprovalForm" runat="server" Visible="false">
            <div class="section-header">Equipment Request - Approval Required</div>
            <div class="warning-box">
                You are reviewing this request as: <strong><asp:Label ID="lblApproveLevel" runat="server" /></strong>
            </div>
            <table class="form-table" width="100%">
                <tr>
                    <td class="label-cell">Request ID:</td>
                    <td><asp:Label ID="lblApproveRequestID" runat="server" /></td>
                    <td class="label-cell">Requester:</td>
                    <td><asp:Label ID="lblApproveRequester" runat="server" /></td>
                </tr>
                <tr>
                    <td class="label-cell">Equipment Type:</td>
                    <td><asp:Label ID="lblApproveEquipType" runat="server" /></td>
                    <td class="label-cell">Quantity:</td>
                    <td><asp:Label ID="lblApproveQuantity" runat="server" /></td>
                </tr>
                <tr>
                    <td class="label-cell">Estimated Cost:</td>
                    <td><asp:Label ID="lblApproveCost" runat="server" /></td>
                    <td class="label-cell">Priority:</td>
                    <td><asp:Label ID="lblApprovePriority" runat="server" /></td>
                </tr>
                <tr>
                    <td class="label-cell">Justification:</td>
                    <td colspan="3"><asp:Label ID="lblApproveJustification" runat="server" /></td>
                </tr>
                <tr>
                    <td class="label-cell">Approval Comments:</td>
                    <td colspan="3">
                        <asp:TextBox ID="txtApprovalComments" runat="server" TextMode="MultiLine" Rows="3" Width="400px" />
                    </td>
                </tr>
                <tr>
                    <td class="label-cell">Rejection Reason:</td>
                    <td colspan="3">
                        <asp:TextBox ID="txtRejectionReason" runat="server" TextMode="MultiLine" Rows="3" Width="400px" />
                        <br /><small>Required if rejecting.</small>
                    </td>
                </tr>
                <tr>
                    <td colspan="4" style="padding-top:15px;">
                        <asp:Button ID="btnApprove" runat="server" Text="Approve" CssClass="btn-approve" OnClick="btnApprove_Click" />
                        &nbsp;&nbsp;
                        <asp:Button ID="btnReject"  runat="server" Text="Reject"  CssClass="btn-reject"  OnClick="btnReject_Click"
                            OnClientClick="return confirmReject();" />
                    </td>
                </tr>
            </table>
        </asp:Panel>

        <!-- VIEW REQUEST -->
        <asp:Panel ID="pnlViewRequest" runat="server" Visible="false">
            <div class="section-header">Equipment Request Details</div>
            <table class="form-table" width="100%">
                <tr>
                    <td class="label-cell">Request ID:</td>
                    <td><asp:Label ID="lblViewRequestID" runat="server" /></td>
                    <td class="label-cell">Status:</td>
                    <td><asp:Label ID="lblViewStatus" runat="server" /></td>
                </tr>
                <tr>
                    <td class="label-cell">Requester:</td>
                    <td><asp:Label ID="lblViewRequester" runat="server" /></td>
                    <td class="label-cell">Department:</td>
                    <td><asp:Label ID="lblViewDepartment" runat="server" /></td>
                </tr>
                <tr>
                    <td class="label-cell">Equipment Type:</td>
                    <td><asp:Label ID="lblViewEquipType" runat="server" /></td>
                    <td class="label-cell">Quantity:</td>
                    <td><asp:Label ID="lblViewQuantity" runat="server" /></td>
                </tr>
                <tr>
                    <td class="label-cell">Priority:</td>
                    <td><asp:Label ID="lblViewPriority" runat="server" /></td>
                    <td class="label-cell">Estimated Cost:</td>
                    <td><asp:Label ID="lblViewCost" runat="server" /></td>
                </tr>
                <tr>
                    <td class="label-cell">Submitted On:</td>
                    <td><asp:Label ID="lblViewSubmitDate" runat="server" /></td>
                    <td></td><td></td>
                </tr>
                <tr>
                    <td class="label-cell">Justification:</td>
                    <td colspan="3"><asp:Label ID="lblViewJustification" runat="server" /></td>
                </tr>
            </table>

            <div class="section-header">Approval History</div>
            <asp:GridView ID="gvWorkFlowHistory" runat="server" AutoGenerateColumns="false"
                Width="100%" CssClass="form-table" BorderColor="#cccccc" BorderWidth="1px">
                <HeaderStyle BackColor="#003366" ForeColor="White" />
                <AlternatingRowStyle BackColor="#f9f9f9" />
                <Columns>
                    <asp:BoundField DataField="StepNumber"   HeaderText="Step"       />
                    <asp:BoundField DataField="StepName"     HeaderText="Stage"      />
                    <asp:BoundField DataField="ApproverName" HeaderText="Approver"   />
                    <asp:BoundField DataField="Status"       HeaderText="Status"     />
                    <asp:BoundField DataField="ApprovalDate" HeaderText="Date"       DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                    <asp:BoundField DataField="Comments"     HeaderText="Comments"   />
                </Columns>
            </asp:GridView>

            <div style="margin:10px;">
                <asp:Button ID="btnCancel" runat="server" Text="Cancel Request" CssClass="btn-cancel"
                    OnClick="btnCancel_Click"
                    OnClientClick="return confirm('Are you sure you want to cancel this request?');" />
                &nbsp;
                <asp:Button ID="btnPrint"  runat="server" Text="Print" CssClass="btn-submit"
                    OnClientClick="window.print(); return false;" />
            </div>
        </asp:Panel>

    </form>
</body>
</html>
