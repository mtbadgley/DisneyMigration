using System;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;
using V1DataCore;

namespace JiraReaderService
{
    public class ExportRequests : IExportAssets
    {
        public ExportRequests(SqlConnection sqlConn, MigrationConfiguration Configurations) : base(sqlConn, Configurations) { }

        public override int Export()
        {
            int assetCounter = 0;
            assetCounter += ProcessExportFile(_config.JiraConfiguration.XmlFileName);
            return assetCounter;
        }

        private int ProcessExportFile(string FileName)
        {
            string SQL = BuildRequestInsertStatement();
            int assetCounter = 0;

            XDocument xmlDoc = XDocument.Load(FileName);
            var assets = from asset in xmlDoc.XPathSelectElements("rss/channel/item") select asset;

            foreach (var asset in assets)
            {
                //Check to see if this is an Epic, if not continue to the next
                string type;
                var xType = asset.Element("type");
                if (xType != null)
                {
                    type = xType.Value.ToString().ToLower();
                }
                else
                {
                    continue;
                }

                if (!_config.JiraConfiguration.RequestIssueTypes.Contains(type)) continue;

                if (!string.IsNullOrEmpty(GetAssetFromDB("Request-" + asset.Element("key").Value, "Requests") as string)) continue;

                bool comments = ProcessComments(asset.Element("comments"), "Request-" + asset.Element("key").Value, "Request");

                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = _sqlConn;
                    cmd.CommandText = SQL;
                    cmd.CommandType = System.Data.CommandType.Text;

                    cmd.Parameters.AddWithValue("@AssetOID", "Request-" + asset.Element("key").Value);
                    cmd.Parameters.AddWithValue("@AssetState", GetRequestState(asset.Element("status").Value));
                    cmd.Parameters.AddWithValue("@AssetNumber", asset.Element("key").Value);
                    cmd.Parameters.AddWithValue("@Name", asset.Element("summary").Value);
                    cmd.Parameters.AddWithValue("@Scope", "Scope-1");
                    cmd.Parameters.AddWithValue("@Description", AddLinkToDescription(asset.Element("description").Value, asset.Element("link").Value));
                    cmd.Parameters.AddWithValue("@Status", GetMappedListValue("Request", "Status", asset.Element("status").Value));
                    cmd.Parameters.AddWithValue("@Owner", asset.Element("assignee").Attribute("username").Value);
                    cmd.Parameters.AddWithValue("@RequestedBy", asset.Element("reporter").Value);
                    cmd.Parameters.AddWithValue("@Order", Convert.ToInt64(GetCustomFieldValue(asset.Element("customfields"), "Studio Priority")));
                    string priority = asset.Element("priority").Value;
                    if (string.IsNullOrEmpty(priority) == false)
                    {
                        cmd.Parameters.AddWithValue("@Priority", GetMappedListValue("Request", "Priority", priority));
                    }

                    foreach (var customField in _config.CustomFieldsToMigrate)
                    {
                        if (customField.AssetType == "Request")
                        {
                            var fieldValue = GetCustomFieldValue(asset.Element("customfields"), customField.SourceName);
                            if (string.IsNullOrEmpty(fieldValue) == false)
                                CreateCustomField("Request-" + asset.Element("key").Value, customField.SourceName, customField.DataType, fieldValue);
                        }
                    }

                    cmd.ExecuteNonQuery();
                }

                int linkCnt = SaveAttachmentLinks(asset.Element("key").Value, "Request", asset.Element("attachments"));
                linkCnt++;

                StringBuilder linkAssetOid = new StringBuilder();
                linkAssetOid.Append(asset.Element("key").Value);
                linkAssetOid.Append("-");
                linkAssetOid.Append(linkCnt.ToString());

                SaveLink(linkAssetOid.ToString(), asset.Element("key").Value, "Request", "true", asset.Element("link").Value, "Jira");

                assetCounter++;
            }
            return assetCounter;
        }

        //NOTE: Rally data contains no "state" field, so asset state is derived from "ScheduleState" field.
        private string GetRequestState(string State)
        {
            switch (State)
            {
                case "Closed":
                    return "Closed";
                default:
                    return "Active";
            }
        }

        private string BuildRequestInsertStatement()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO REQUESTS (");
            sb.Append("AssetOID,");
            sb.Append("AssetState,");
            sb.Append("AssetNumber,");
            sb.Append("Owner,");
            sb.Append("Scope,");
            sb.Append("Description,");
            sb.Append("Name,");
            sb.Append("[Order],");
            sb.Append("RequestedBy,");
            sb.Append("Priority,");
            sb.Append("Status)");
            sb.Append("VALUES (");
            sb.Append("@AssetOID,");
            sb.Append("@AssetState,");
            sb.Append("@AssetNumber,");
            sb.Append("@Owner,");
            sb.Append("@Scope,");
            sb.Append("@Description,");
            sb.Append("@Name,");
            sb.Append("@Order,");
            sb.Append("@RequestedBy,");
            sb.Append("@Priority,");
            sb.Append("@Status);");
            return sb.ToString();
        }
    }
}
