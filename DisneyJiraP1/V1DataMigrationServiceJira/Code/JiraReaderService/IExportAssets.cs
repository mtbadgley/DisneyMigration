using System;
using System.Linq;
using System.Web;
using System.Xml.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using V1DataCore;

namespace JiraReaderService
{
    abstract public class IExportAssets
    {
        protected SqlConnection _sqlConn;
        protected MigrationConfiguration _config;

        public IExportAssets(SqlConnection sqlConn, MigrationConfiguration Configurations)
        {
            _sqlConn = sqlConn;
            _config = Configurations;
        }

        /**************************************************************************************
         * Virtual method that must be implemented in derived classes.
         **************************************************************************************/
        public abstract int Export();

        protected object GetMappedListValue(string AssetType, string FieldName, string OldValue)
        {
            string result;
            result = OldValue;
            
            if (String.IsNullOrEmpty(OldValue))
            {
                return DBNull.Value;
            }
            
            foreach (var listValue in _config.ListValues)
            {
                if (listValue.AssetType.ToLower() == AssetType.ToLower() && 
                    listValue.FieldName.ToLower() == FieldName.ToLower() &&
                    listValue.OldValue.ToLower() == OldValue.ToLower())
                {
                    result = listValue.NewValue;
                }
            }
            return result;
        }

        protected object GetMemberOIDFromDB(string AssetOID)
        {
            string SQL = "SELECT AssetOID FROM Members WHERE AssetOID = '" + AssetOID + "';";
            SqlCommand cmd = new SqlCommand(SQL, _sqlConn);
            string result = (string)cmd.ExecuteScalar();
            if (String.IsNullOrEmpty(result) == false)
                return result;
            else
                return DBNull.Value;
        }

        protected object GetCombinedDescription(string FirstString, string SecondString, string SecondStringHeader)
        {
            if (String.IsNullOrEmpty(FirstString) == true && String.IsNullOrEmpty(SecondString) == true)
                return DBNull.Value;
            else if (String.IsNullOrEmpty(FirstString) == false && String.IsNullOrEmpty(SecondString) == true)
                return FirstString;
            else if (String.IsNullOrEmpty(FirstString) == true && String.IsNullOrEmpty(SecondString) == false)
                return SecondString;
            else
                return FirstString + "<br/><br/><b>" + SecondStringHeader + ":</b><br/>" + SecondString;
        }

        protected string ConvertRallyDate(string RallyDate)
        {
            DateTime dtStart = DateTime.Parse(RallyDate);
            return dtStart.ToString();
        }

        protected string GetRefValue(string RefValue)
        {
            string[] segments = RefValue.Split('/');
            return segments[segments.Length - 1];
        }

        protected SqlDataReader GetProjectsFromDB(string ParentOID)
        {
            string SQL = "SELECT * FROM Projects WITH (NOLOCK) WHERE Parent = '" + ParentOID + "';";
            SqlCommand cmd = new SqlCommand(SQL, _sqlConn);
            SqlDataReader sdr = cmd.ExecuteReader();
            return sdr;
        }

        protected SqlDataReader GetAttachmentsFromDB()
        {
            string SQL = "SELECT * FROM Attachments WITH (NOLOCK);";
            SqlCommand cmd = new SqlCommand(SQL, _sqlConn);
            SqlDataReader sdr = cmd.ExecuteReader();
            return sdr;
        }

        protected string GetRefValueList(XElement ValueList, string ValueName, string RefName)
        {
            string listOfValues = null;

            var values = ValueList.Descendants(ValueName);
            foreach (var value in values)
            {
                if (values.Count() == 1)
                    listOfValues = GetRefValue(value.Attribute(RefName).Value);
                else
                    listOfValues += GetRefValue(value.Attribute(RefName).Value) + ";";
            }
            return listOfValues;
        }

        protected void CreateCustomField(string AssetOID, string FieldName, string FieldType, string FieldValue)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO CUSTOMFIELDS (");
            sb.Append("AssetOID,");
            sb.Append("FieldName,");
            sb.Append("FieldType,");
            sb.Append("FieldValue) ");
            sb.Append("VALUES (");
            sb.Append("@AssetOID,");
            sb.Append("@FieldName,");
            sb.Append("@FieldType,");
            sb.Append("@FieldValue);");

            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = _sqlConn;
                cmd.CommandText = sb.ToString();
                cmd.CommandType = System.Data.CommandType.Text;
                cmd.Parameters.AddWithValue("@AssetOID", AssetOID);
                cmd.Parameters.AddWithValue("@FieldName", FieldName);
                cmd.Parameters.AddWithValue("@FieldType", FieldType);
                cmd.Parameters.AddWithValue("@FieldValue", FieldValue);
                cmd.ExecuteNonQuery();
            }
        }

        protected object GetTestSteps(string AssetOID)
        {
            StringBuilder sb = new StringBuilder();

            string SQL = "SELECT * FROM TestSteps WITH (NOLOCK) WHERE TestCaseOID = '" + AssetOID + "' ORDER BY StepIndex ASC;";
            SqlCommand cmd = new SqlCommand(SQL, _sqlConn);
            SqlDataReader sdr = cmd.ExecuteReader();

            while (sdr.Read())
            {
                sb.AppendLine("<b>STEP " + sdr["StepIndex"].ToString() + "</b><br/>");
                sb.AppendLine("Input: " + sdr["Input"].ToString() + "<br/>");
                sb.AppendLine("Expected result: " + sdr["ExpectedResult"].ToString() + "<br/><br/>");
            }
            sdr.Close();

            if (String.IsNullOrEmpty(sb.ToString()) == false)
                return sb.ToString();
            else
                return DBNull.Value;
        }

        protected void SaveAttachmentRecords(string ParentAssetOID, string AssetType, XElement Attachments)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO ATTACHMENTS (");
            sb.Append("AssetOID,");
            sb.Append("URL,");
            sb.Append("Asset,");
            sb.Append("AssetType) ");
            sb.Append("VALUES (");
            sb.Append("@AssetOID,");
            sb.Append("@URL,");
            sb.Append("@Asset,");
            sb.Append("@AssetType);");

            var assets = from asset in Attachments.Descendants("Attachment") select asset;

            foreach (var asset in assets)
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = _sqlConn;
                    cmd.CommandText = sb.ToString();
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.Parameters.AddWithValue("@AssetOID", GetRefValue(asset.Attribute("ref").Value));
                    cmd.Parameters.AddWithValue("@URL", asset.Attribute("ref").Value);
                    cmd.Parameters.AddWithValue("@Asset", ParentAssetOID);
                    cmd.Parameters.AddWithValue("@AssetType", AssetType);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        protected int SaveAttachmentLinks(string ParentAssetOid, string AssetType, XElement Attachments)
        {
            int attachmentCnt = 0;

            var attachments = from attachment in Attachments.Descendants("attachment") select attachment;

            foreach (var attachment in attachments)
            {
                attachmentCnt++;
                StringBuilder linkUrl = new StringBuilder();
                linkUrl.Append(_config.JiraConfiguration.JiraUrl);
                linkUrl.Append("/secure/attachment/");
                linkUrl.Append(attachment.Attribute("id").Value);
                linkUrl.Append("/");
                linkUrl.Append(HttpUtility.UrlEncode(attachment.Attribute("name").Value));

                StringBuilder linkName = new StringBuilder();
                linkName.Append("Jira Attachment: ");
                linkName.Append(attachment.Attribute("name").Value);

                StringBuilder assetOid = new StringBuilder();
                assetOid.Append(ParentAssetOid);
                assetOid.Append("-");
                assetOid.Append(attachmentCnt.ToString());

                SaveLink(assetOid.ToString(), ParentAssetOid, AssetType, "false", linkUrl.ToString(), linkName.ToString());
            }
            return attachmentCnt;
        }

        protected string ProcessLabels(XElement Labels, string Delimiter)
        {
            string result;
            result = String.Empty;
            if (Labels != null && Labels.HasElements)
            {
                var labels = from label in Labels.Descendants("label") select label;
                foreach (var label in labels)
                {
                    result = result + label.Value + Delimiter;
                }
            }
            //if (result.Length > 0)
            //{
            //    result = result.Substring(0, (result.Length - 1));
            //}
            return result;
        }

        protected string ProcessDependencies(XElement Dependencies, string key)
        {
            string result;
            result = String.Empty;
            if (Dependencies != null && Dependencies.HasElements)
            {
                var dependencies = from dependency in Dependencies.Descendants(key) select dependency;
                foreach (var dependency in dependencies)
                {
                    if (dependency.Attribute("description").Value == "depends on")
                    {
                        string storyid = "Story-" + dependency.Value;
                        if (String.IsNullOrEmpty(result))
                        {
                            result = storyid;
                        }
                        else
                        {
                            result = result + "," + storyid;
                        }
                    }
                }
            }
            return result;
        }

        
        protected bool ProcessTeam(string TeamName)
        {
            if (!string.IsNullOrEmpty(TeamName))
            {
                string SQL = BuildTeamInsertStatement();
                if (!CheckTeamExists(TeamName))
                {
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = _sqlConn;
                        cmd.CommandText = SQL;
                        cmd.CommandType = CommandType.Text;

                        cmd.Parameters.AddWithValue("@AssetOID", TeamName);
                        cmd.Parameters.AddWithValue("@AssetState", "Active");
                        cmd.Parameters.AddWithValue("@Description", "Imported from Jira");
                        cmd.Parameters.AddWithValue("@Name", TeamName);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            return true;
        }

        protected bool ProcessFeatureGroup(string groupname, string scope)
        {
            if (!string.IsNullOrEmpty(groupname))
            {
                string SQL = BuildFeatureGroupInsertStatement();
                if (!CheckFGExists(groupname))
                {
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = _sqlConn;
                        cmd.CommandText = SQL;
                        cmd.CommandType = CommandType.Text;

                        cmd.Parameters.AddWithValue("@AssetOID",groupname);
                        cmd.Parameters.AddWithValue("@AssetState","Active");
                        cmd.Parameters.AddWithValue("@AssetNumber",groupname);
                        cmd.Parameters.AddWithValue("@Name",groupname);
                        cmd.Parameters.AddWithValue("@Scope",scope);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            return true;
        }

        protected bool ProcessGoal(string goalname, string scope)
        {
            if (!string.IsNullOrEmpty(goalname))
            {
                string SQL = BuildGoalInsertStatement();
                if (!CheckGoalExists(goalname))
                {
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = _sqlConn;
                        cmd.CommandText = SQL;
                        cmd.CommandType = CommandType.Text;

                        cmd.Parameters.AddWithValue("@AssetOID", goalname);
                        cmd.Parameters.AddWithValue("@AssetState", "Active");
                        cmd.Parameters.AddWithValue("@AssetNumber", goalname);
                        cmd.Parameters.AddWithValue("@TargetedBy", scope);
                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@Name", goalname);
                        cmd.Parameters.AddWithValue("@Category", DBNull.Value);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            return true;
        }

        protected bool ProcessLabelsAsGoals(string labels, string AssetOID, string Scope)
        {
            if (!String.IsNullOrEmpty(labels))
            {
                string[] labelarray = labels.Split(';');
                foreach (var label in labelarray)
                {
                    if (!CheckGoalExists(label.ToString()))
                    {
                        ProcessGoal(label.ToString(), Scope);
                    }
                }
            }
            return true;
        }

        protected bool ProcessComments(XElement Comments, string AssetOID, string AssetType)
        {
            if (Comments != null && Comments.HasElements)
            {
                var comments = from comment in Comments.Descendants("comment") select comment;
                string SQL = BuildConversationInsert();

                foreach (var comment in comments)
                {

                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = _sqlConn;
                        cmd.CommandText = SQL;
                        cmd.CommandType = CommandType.Text;

                        cmd.Parameters.AddWithValue("@AssetOID", comment.Attribute("id").Value);
                        cmd.Parameters.AddWithValue("@AssetState", "Active");
                        cmd.Parameters.AddWithValue("@AuthoredAt", ConvertRallyDate(comment.Attribute("created").Value));
                        cmd.Parameters.AddWithValue("@Author",
                            GetMemberOIDFromDB(GetRefValue(comment.Attribute("author").Value)));
                        cmd.Parameters.AddWithValue("@Mentions", AssetOID);
                        cmd.Parameters.AddWithValue("@Conversation", comment.Attribute("id").Value);
                        cmd.Parameters.AddWithValue("@InReplyTo", DBNull.Value);
                        cmd.Parameters.AddWithValue("@BaseAssetType", AssetType);
                        cmd.Parameters.AddWithValue("@Index", "1");

                        //Convert HTML content to plain text.
                        HtmlToText htmlParser = new HtmlToText();
                        cmd.Parameters.AddWithValue("@Content", htmlParser.Convert(comment.Value));

                        cmd.ExecuteNonQuery();

                    }
                }
            }
            return false;
        }


        protected string GetComments(XElement Comments)
        {
            StringBuilder formattedComments = new StringBuilder();

            if (Comments != null)
            {

                formattedComments.Append("<br /><br /><strong>Jira Comments:</strong><br />");

                var comments = from comment in Comments.Descendants("comment") select comment;

                foreach (var comment in comments)
                {
                    formattedComments.Append("[");
                    formattedComments.Append(GetMemberNamefromDB(comment.Attribute("author").Value));
                    formattedComments.Append(" - ");
                    formattedComments.Append(comment.Attribute("created").Value);
                    formattedComments.Append("]<br />");
                    formattedComments.Append(comment.Value);
                    formattedComments.Append("<br /><br />");
                }
                
            }

            return formattedComments.ToString();
        }

        protected string AddLinkToDescription(string Description, string LinkURL)
        {
            StringBuilder sb = new StringBuilder();
            if (!String.IsNullOrEmpty(Description))
            {
                sb.Append(Description);
                sb.Append("&nbsp;<br /><br />");
            }
            sb.Append("<strong>JIRA - </strong>");
            sb.Append("<a href='" + LinkURL + "' target='_blank'>" + LinkURL + "</a>");
            sb.Append("<br />");

            return sb.ToString();
        }

        protected void SaveLink(string AssetOid, string ParentAssetOid, string AssetType, string OnMenu, string LinkUrl, string LinkName)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO LINKS (");
            sb.Append("AssetOID,");
            sb.Append("AssetState,");
            sb.Append("OnMenu,");
            sb.Append("URL,");
            sb.Append("Name,");
            sb.Append("Asset) ");
            sb.Append("VALUES (");
            sb.Append("@AssetOid,");
            sb.Append("@AssetState,");
            sb.Append("@OnMenu,");
            sb.Append("@Url,");
            sb.Append("@Name,");
            sb.Append("@Asset);");

            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = _sqlConn;
                cmd.CommandText = sb.ToString();
                cmd.CommandType = System.Data.CommandType.Text;
                cmd.Parameters.AddWithValue("@AssetOid", AssetOid);
                cmd.Parameters.AddWithValue("@AssetState", "Active");
                cmd.Parameters.AddWithValue("@OnMenu", OnMenu);
                cmd.Parameters.AddWithValue("@Url", LinkUrl);
                cmd.Parameters.AddWithValue("@Name", LinkName);
                cmd.Parameters.AddWithValue("@Asset", AssetType + "-" + ParentAssetOid);
                cmd.ExecuteNonQuery();
            }
        }

        protected string GetCustomFieldValue(XElement CustomFields, string FieldName)
        {
            string customFieldValue = string.Empty;
            if (CustomFields != null)
            {
                var customFields = from customField in CustomFields.Descendants("customfield") select customField;
                
                foreach (XElement customField in customFields)
                {
                    if (customField.Element("customfieldname").Value == FieldName)
                    {
                        XElement fieldValues = customField.Element("customfieldvalues");
                        if (fieldValues != null && fieldValues.HasElements)
                        {
                            customFieldValue = fieldValues.Elements("customfieldvalue").Last().Value;
                        }
                    }
                }
            }
            return customFieldValue;
        }

        protected bool CheckGoalExists(string GoalName)
        {
            string SQL = "SELECT Name FROM Goals WHERE Name = '" + GoalName + "';";
            SqlCommand cmd = new SqlCommand(SQL, _sqlConn);
            string result = (string)cmd.ExecuteScalar();
            if (String.IsNullOrEmpty(result) == false)
                return true;
            else
                return false;
        }

        protected bool CheckFGExists(string GroupName)
        {
            string SQL = "SELECT Name FROM FeatureGroups WHERE Name = '" + GroupName + "';";
            SqlCommand cmd = new SqlCommand(SQL, _sqlConn);
            string result = (string) cmd.ExecuteScalar();
            if (String.IsNullOrEmpty(result) == false)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        protected bool CheckTeamExists(string TeamName)
        {
            string SQL = "SELECT Name FROM Teams WHERE Name = '" + TeamName + "';";
            SqlCommand cmd = new SqlCommand(SQL, _sqlConn);
            string result = (string) cmd.ExecuteScalar();
            if (String.IsNullOrEmpty(result) == false)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        protected string GetMemberNamefromDB(string Author)
        {
            string SQL = "SELECT Name FROM Members WHERE username = '" + Author + "';";
            SqlCommand cmd = new SqlCommand(SQL, _sqlConn);
            string result = (string)cmd.ExecuteScalar();
            if (String.IsNullOrEmpty(result) == false)
                return result;
            else
                return string.Empty;
        }

        protected object GetAssetFromDB(string AssetOID, string Table)
        {
            string SQL = "SELECT AssetOID FROM " + Table + " WHERE AssetOID = '" + AssetOID + "';";
            SqlCommand cmd = new SqlCommand(SQL, _sqlConn);
            string result = (string)cmd.ExecuteScalar();
            if (String.IsNullOrEmpty(result) == false)
                return result;
            else
                return DBNull.Value;
        }

        private string BuildConversationInsert()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO CONVERSATIONS (");
            sb.Append("AssetOID,");
            sb.Append("AssetState,");
            sb.Append("AuthoredAt,");
            sb.Append("Content,");
            sb.Append("Mentions,");
            sb.Append("Author,");
            sb.Append("BaseAssetType,");
            sb.Append("Conversation,");
            sb.Append("InReplyTo,");
            sb.Append("[Index]) ");
            sb.Append("VALUES (");
            sb.Append("@AssetOID,");
            sb.Append("@AssetState,");
            sb.Append("@AuthoredAt,");
            sb.Append("@Content,");
            sb.Append("@Mentions,");
            sb.Append("@Author,");
            sb.Append("@BaseAssetType,");
            sb.Append("@Conversation,");
            sb.Append("@InReplyTo,");
            sb.Append("@Index);");
            return sb.ToString();
        }

        private string BuildGoalInsertStatement()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO GOALS (");
            sb.Append("AssetOID,");
            sb.Append("AssetState,");
            sb.Append("AssetNumber,");
            sb.Append("TargetedBy,");
            sb.Append("Scope,");
            sb.Append("Name,");
            sb.Append("Category) ");
            sb.Append("VALUES (");
            sb.Append("@AssetOID,");
            sb.Append("@AssetState,");
            sb.Append("@AssetNumber,");
            sb.Append("@TargetedBy,");
            sb.Append("@Scope,");
            sb.Append("@Name,");
            sb.Append("@Category);");
            return sb.ToString();
        }

        private string BuildTeamInsertStatement()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO TEAMS (");
            sb.Append("AssetOID,");
            sb.Append("AssetState,");
            sb.Append("Description,");
            sb.Append("Name)");
            sb.Append("VALUES (");
            sb.Append("@AssetOID,");
            sb.Append("@AssetState,");
            sb.Append("@Description,");
            sb.Append("@Name);");
            return sb.ToString();
        }

        private string BuildFeatureGroupInsertStatement()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO FEATUREGROUPS (");
            sb.Append("AssetOID,");
            sb.Append("AssetState,");
            sb.Append("AssetNumber,");
            sb.Append("Name,");
            sb.Append("Scope)");
            sb.Append("VALUES (");
            sb.Append("@AssetOID,");
            sb.Append("@AssetState,");
            sb.Append("@AssetNumber,");
            sb.Append("@Name,");
            sb.Append("@Scope);");
            return sb.ToString();
        }

    }
}
