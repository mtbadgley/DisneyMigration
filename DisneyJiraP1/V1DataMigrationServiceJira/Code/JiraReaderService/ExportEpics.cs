using System;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;
using V1DataCore;

namespace JiraReaderService
{
    public class ExportEpics : IExportAssets
    {
        public ExportEpics(SqlConnection sqlConn, MigrationConfiguration Configurations) : base(sqlConn, Configurations) { }

        public override int Export()
        {
            int assetCounter = 0;
            assetCounter += ProcessExportFile(_config.JiraConfiguration.XmlFileName);
            return assetCounter;
        }

        private int ProcessExportFile(string FileName)
        {
            string SQL = BuildEpicInsertStatement();
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

                if (!_config.JiraConfiguration.EpicIssueTypes.Contains(type)) continue;

                string comments = GetComments(asset.Element("comments"));

                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = _sqlConn;
                    cmd.CommandText = SQL;
                    cmd.CommandType = System.Data.CommandType.Text;

                    cmd.Parameters.AddWithValue("@AssetOID", "Epic-" + asset.Element("key").Value);
                    cmd.Parameters.AddWithValue("@AssetState", GetEpicState(asset.Element("status").Value));
                    cmd.Parameters.AddWithValue("@AssetNumber", asset.Element("key").Value);
                    cmd.Parameters.AddWithValue("@Name", asset.Element("summary").Value);
                    cmd.Parameters.AddWithValue("@Scope", "Scope-1");
                    cmd.Parameters.AddWithValue("@Status", asset.Element("status").Value);
                    cmd.Parameters.AddWithValue("@Swag", DBNull.Value);
                    cmd.Parameters.AddWithValue("@Owners", asset.Element("assignee").Attribute("username").Value);
                    cmd.Parameters.AddWithValue("@Super", DBNull.Value);
                    //cmd.Parameters.AddWithValue("@Order", GetCustomFieldValue(asset.Element("customfields"), "Rank"));
                    //cmd.Parameters.AddWithValue("@Order", "0");

                    // use Global Rank (jira 6.x)
                    cmd.Parameters.AddWithValue("@Order", Convert.ToInt64(GetCustomFieldValue(asset.Element("customfields"), "Global Rank")));

                    //MTB - New Description
                    string businessRules = GetCustomFieldValue(asset.Element("customfields"), "Business Rules");
                    string userStory = GetCustomFieldValue(asset.Element("customfields"), "User Story");
                    string description = asset.Element("description").Value;
                    if (string.IsNullOrEmpty(description))
                    {
                        description = string.Empty;
                    }
                    if (!string.IsNullOrEmpty(userStory))
                    {
                        description = description + "<br /><br /><strong>User Story:</strong><br />" + userStory;
                    }
                    if (!string.IsNullOrEmpty(businessRules))
                    {
                        description = description + "<br /><br /><strong>Business Rules:</strong><br />" + businessRules;
                    }
                    cmd.Parameters.AddWithValue("@Description", AddLinkToDescription(description, asset.Element("link").Value));


                    foreach (var customField in _config.CustomFieldsToMigrate)
                    {
                        if (customField.AssetType == "Epic")
                        {
                            var fieldValue = GetCustomFieldValue(asset.Element("customfields"), customField.SourceName);
                            if (string.IsNullOrEmpty(fieldValue) == false)
                                CreateCustomField("Epic-" + asset.Element("key").Value, customField.SourceName, customField.DataType, fieldValue);
                        }
                    }

                    cmd.ExecuteNonQuery();
                }

                //int linkCnt = SaveAttachmentLinks(asset.Element("key").Value, "Epic", asset.Element("attachments"));
                int linkCnt = 0;

                string compsAndWireframes = GetCustomFieldValue(asset.Element("customfields"), "Comps and Wireframes");
                if (!string.IsNullOrEmpty(compsAndWireframes))
                {
                    linkCnt++;
                    StringBuilder compLinkAssetOid = new StringBuilder();
                    compLinkAssetOid.Append(asset.Element("key").Value);
                    compLinkAssetOid.Append("-");
                    compLinkAssetOid.Append(linkCnt.ToString());
                    SaveLink(compLinkAssetOid.ToString(), asset.Element("key").Value, "Epic", "true", compsAndWireframes, "Comps and Wireframes");
                }

                linkCnt++;
                StringBuilder linkAssetOid = new StringBuilder();
                linkAssetOid.Append(asset.Element("key").Value);
                linkAssetOid.Append("-");
                linkAssetOid.Append(linkCnt.ToString());

                SaveLink(linkAssetOid.ToString(), asset.Element("key").Value, "Epic","true",asset.Element("link").Value,"Jira");

                assetCounter++;
            }
            return assetCounter;
        }

        //NOTE: Rally data contains no "state" field, so asset state is derived from "ScheduleState" field.
        private string GetEpicState(string State)
        {
            switch (State)
            {
                case "Closed":
                    return "Closed";
                default:
                    return "Active";
            }
        }

        private string BuildEpicInsertStatement()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO EPICS (");
            sb.Append("AssetOID,");
            sb.Append("AssetNumber,");
            sb.Append("AssetState,");
            sb.Append("Name,");
            sb.Append("Scope,");
            sb.Append("Owners,");
            sb.Append("Super,");
            sb.Append("Status,");
            sb.Append("Swag,");
            sb.Append("[Order],");
            sb.Append("Description) ");
            sb.Append("VALUES (");
            sb.Append("@AssetOID,");
            sb.Append("@AssetNumber,");
            sb.Append("@AssetState,");
            sb.Append("@Name,");
            sb.Append("@Scope,");
            sb.Append("@Owners,");
            sb.Append("@Super,");
            sb.Append("@Status,");
            sb.Append("@Swag,");
            sb.Append("@Order,");
            sb.Append("@Description);");
            return sb.ToString();
        }

    }
}
