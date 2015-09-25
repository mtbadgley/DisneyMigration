using System;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;
using V1DataCore;

namespace JiraReaderService
{
    public class ExportDefects : IExportAssets
    {
        public ExportDefects(SqlConnection sqlConn, MigrationConfiguration Configurations) : base(sqlConn, Configurations) { }

        public override int Export()
        {
            int assetCounter = 0;
            assetCounter += ProcessExportFile(_config.JiraConfiguration.XmlFileName);
            return assetCounter;
        }

        private int ProcessExportFile(string FileName)
        {
            string SQL = BuildDefectInsertStatement();
            int assetCounter = 0;

            XDocument xmlDoc = XDocument.Load(FileName);
            var assets = from asset in xmlDoc.XPathSelectElements("rss/channel/item") select asset;

            foreach (var asset in assets)
            {
                //Check to see if this is an Defect, if not continue to the next
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

                if (!_config.JiraConfiguration.DefectIssueTypes.Contains(type)) continue;

                if (!string.IsNullOrEmpty(GetAssetFromDB("Defect-" + asset.Element("key").Value, "Defects") as string)) continue;

                bool comments = ProcessComments(asset.Element("comments"), "Defect-" + asset.Element("key").Value, "Defect");

                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = _sqlConn;
                    cmd.CommandText = SQL;
                    cmd.CommandType = System.Data.CommandType.Text;

                    cmd.Parameters.AddWithValue("@AssetOID", "Defect-" + asset.Element("key").Value);
                    cmd.Parameters.AddWithValue("@AssetNumber", asset.Element("key").Value);
                    cmd.Parameters.AddWithValue("@Name", asset.Element("summary").Value);
                    cmd.Parameters.AddWithValue("@Scope", "Scope-1");
                    cmd.Parameters.AddWithValue("@AssetState", GetDefectState(asset.Element("status").Value));
                    cmd.Parameters.AddWithValue("@Status", GetMappedListValue("Defect", "Status", asset.Element("status").Value));

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

                    string priority = asset.Element("priority").Value;
                    if (string.IsNullOrEmpty(priority) == false)
                    {
                        cmd.Parameters.AddWithValue("@Priority", GetMappedListValue("Defect", "Priority", priority));
                    }
                    cmd.Parameters.AddWithValue("@Owners", asset.Element("assignee").Value);
                    cmd.Parameters.AddWithValue("@Timebox", "Timebox-" + GetCustomFieldValue(asset.Element("customfields"), "Sprint"));

                    if (!string.IsNullOrEmpty(_config.JiraConfiguration.JiraBacklogGroupReference))
                    {
                        string featuregroupname = GetCustomFieldValue(asset.Element("customfields"),
                            _config.JiraConfiguration.JiraBacklogGroupReference);
                        if (!string.IsNullOrEmpty(featuregroupname))
                        {
                            ProcessFeatureGroup(featuregroupname, "Scope-1");
                            cmd.Parameters.AddWithValue("@Parent", featuregroupname);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@Parent", DBNull.Value);
                        }
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("@Parent", DBNull.Value);
                    }

                    if (!string.IsNullOrEmpty(_config.JiraConfiguration.JiraTeamReference))
                    {
                        string teamname = GetCustomFieldValue(asset.Element("customfields"),
                            _config.JiraConfiguration.JiraTeamReference);
                        if (!string.IsNullOrEmpty(teamname))
                        {
                            ProcessTeam(teamname);
                            cmd.Parameters.AddWithValue("@Team", teamname);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@Team", DBNull.Value);
                        }
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("@Team", DBNull.Value);
                    }

                    string epic = GetCustomFieldValue(asset.Element("customfields"), "Epic Link");
                    if (string.IsNullOrEmpty(epic) == false)
                    {
                        cmd.Parameters.AddWithValue("@Super", "Epic-" + epic);
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("@Super", DBNull.Value);
                    }

                    string estimate = GetCustomFieldValue(asset.Element("customfields"), "Points");
                    if (string.IsNullOrEmpty(estimate) == false)
                    {
                        cmd.Parameters.AddWithValue("@Estimate", estimate);
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("@Estimate", DBNull.Value);
                    }


                    //cmd.Parameters.AddWithValue("@Order", Convert.ToInt64(GetCustomFieldValue(asset.Element("customfields"), "Studio Priority")));
                    // use Global Rank (jira 6.x)
                    cmd.Parameters.AddWithValue("@Order", Convert.ToInt64(GetCustomFieldValue(asset.Element("customfields"), "Global Rank")));

                    
                    cmd.Parameters.AddWithValue("@FoundBy", (asset.Element("reporter").Attribute("username").Value));
                    cmd.Parameters.AddWithValue("@FoundInBuild", DBNull.Value);
                    cmd.Parameters.AddWithValue("@FixedInBuild", DBNull.Value);


                    HtmlToText htmlParser = new HtmlToText();
                    cmd.Parameters.AddWithValue("@Environment", htmlParser.Convert(asset.Element("environment").Value));

                    
                    string resolution = string.Empty;
                    XElement xResolution = asset.Element("resolution");
                    if (xResolution != null)
                    {
                        resolution = xResolution.Value;
                        if (string.IsNullOrEmpty(resolution) == false)
                        {
                            cmd.Parameters.AddWithValue("@ResolutionReason", htmlParser.Convert(resolution));
                        }
                    }

                    // MTB - Disney - Process Goals
                    // first process labels
                    string labels = ProcessLabels(asset.Element("labels"), ";");
                    string goals = string.Empty;
                    if (!String.IsNullOrEmpty(labels))
                    {
                        goals = labels;
                        ProcessLabelsAsGoals(labels, "Defect-" + asset.Element("key").Value, "Scope-1");
                    }
                    // next process component
                    var xElement = asset.Element("component");
                    string component = string.Empty;
                    if (xElement != null)
                    {
                        if (!string.IsNullOrEmpty(xElement.Value))
                        {
                            component = xElement.Value;
                        }
                    }
                    if (!string.IsNullOrEmpty(component))
                    {
                        ProcessLabelsAsGoals(component, "Defect-" + asset.Element("key").Value, "Scope-1");
                        goals = goals + component + ";";
                    }

                    if (!string.IsNullOrEmpty(goals))
                    {
                        cmd.Parameters.AddWithValue("@Goals", goals);
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("@Goals", DBNull.Value);
                    }

                    string dependencies = ProcessDependencies(asset.Element("issuelinks"), "outwardlinks");
                    if (String.IsNullOrEmpty(dependencies))
                    {
                        cmd.Parameters.AddWithValue("@Dependencies", DBNull.Value);
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("@Dependencies", dependencies);
                    }
                    cmd.Parameters.AddWithValue("@Dependants", DBNull.Value);

                    foreach (var customField in _config.CustomFieldsToMigrate)
                    {
                        if (customField.AssetType == "Defect")
                        {
                            var fieldValue = GetCustomFieldValue(asset.Element("customfields"), customField.SourceName);
                            if (string.IsNullOrEmpty(fieldValue) == false)
                                CreateCustomField("Defect-" + asset.Element("key").Value, customField.SourceName, customField.DataType, fieldValue);
                        }
                    
                    }


                    cmd.ExecuteNonQuery();
                }

                //int linkCnt = SaveAttachmentLinks(asset.Element("key").Value, "Defect", asset.Element("attachments"));
                //linkCnt++;

                int linkCnt = 0;

                string compsAndWireframes = GetCustomFieldValue(asset.Element("customfields"), "Comps and Wireframes");
                if (!string.IsNullOrEmpty(compsAndWireframes))
                {
                    linkCnt++;
                    StringBuilder compLinkAssetOid = new StringBuilder();
                    compLinkAssetOid.Append(asset.Element("key").Value);
                    compLinkAssetOid.Append("-");
                    compLinkAssetOid.Append(linkCnt.ToString());
                    SaveLink(compLinkAssetOid.ToString(), asset.Element("key").Value, "Defect", "true", compsAndWireframes, "Comps and Wireframes");
                }

                linkCnt++;

                StringBuilder linkAssetOid = new StringBuilder();
                linkAssetOid.Append(asset.Element("key").Value);
                linkAssetOid.Append("-");
                linkAssetOid.Append(linkCnt.ToString());

                SaveLink(linkAssetOid.ToString(), asset.Element("key").Value, "Defect", "true", asset.Element("link").Value, "Jira");

                assetCounter++;
            }
            return assetCounter;
        }

        private string GetDefectState(string State)
        {
            switch (State)
            {
                case "Closed":
                    return "Closed";
                default:
                    return "Active";
            }
        }

        private string GetItemStatus(string State)
        {
            switch (State)
            {
                case "Reopened":
                    return "Dev Review";
                case "Open":
                    return "Ready";
                case "Closed":
                    return "Completed";
                case "Resolved":
                    return "Ready for UAT";
            }
            return "Ready";
        }


        private string BuildDefectInsertStatement()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO Defects (");
            sb.Append("AssetOID,");
            sb.Append("AssetNumber,");
            sb.Append("AssetState,");
            sb.Append("Name,");
            sb.Append("Scope,");
            sb.Append("Owners,");
            sb.Append("Description,");
            sb.Append("Estimate,");
            sb.Append("Status,");
            sb.Append("Environment,");
            sb.Append("Priority,");
            sb.Append("ResolutionReason,");
            sb.Append("FoundInBuild,");
            sb.Append("FixedInBuild,");
            sb.Append("Team,");
            sb.Append("FoundBy,");
            sb.Append("Dependencies,");
            sb.Append("Dependants,");
            sb.Append("Timebox) ");
            sb.Append("VALUES (");
            sb.Append("@AssetOID,");
            sb.Append("@AssetNumber,");
            sb.Append("@AssetState,");
            sb.Append("@Name,");
            sb.Append("@Scope,");
            sb.Append("@Owners,");
            sb.Append("@Description,");
            sb.Append("@Estimate,");
            sb.Append("@Status,");
            sb.Append("@Environment,");
            sb.Append("@Priority,");
            sb.Append("@ResolutionReason,");
            sb.Append("@FoundInBuild,");
            sb.Append("@FixedInBuild,");
            sb.Append("@Team,");
            sb.Append("@FoundBy,");
            sb.Append("@Dependencies,");
            sb.Append("@Dependants,");
            sb.Append("@Timebox);");
            return sb.ToString();
        }

    }
}
