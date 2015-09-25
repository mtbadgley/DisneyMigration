﻿using System;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Data.SqlClient;
using System.IO;
using V1DataCore;

namespace RallyDataReader
{
    public class ExportTests : IExportAssets
    {
        public ExportTests(SqlConnection sqlConn, MigrationConfiguration Configurations) : base(sqlConn, Configurations) { }

        public override int Export()
        {
            int assetCounter = 0;
            string[] files = Directory.GetFiles(_config.RallySourceConnection.ExportFileDirectory, _config.RallySourceConnection.TestExportFilePrefix+ "_*.xml");
            foreach (string file in files)
            {
                assetCounter += ProcessExportFile(file);
            }
            return assetCounter;
        }

        private int ProcessExportFile(string FileName)
        {
            string SQL = BuildTestInsertStatement();
            int assetCounter = 0;

            XDocument xmlDoc = XDocument.Load(FileName);
            var assets = from asset in xmlDoc.Root.Elements("TestCase") select asset;

            foreach (var asset in assets)
            {
                //Do not process regression tests, that is handled by the ExportRegressionTests class.
                if (asset.Element("Type").Value == "Regression") continue;

                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = _sqlConn;
                    cmd.CommandText = SQL;
                    cmd.CommandType = System.Data.CommandType.Text;

                    cmd.Parameters.AddWithValue("@AssetOID", asset.Element("ObjectID").Value);
                    cmd.Parameters.AddWithValue("@AssetNumber", asset.Element("FormattedID").Value);
                    cmd.Parameters.AddWithValue("@Name", asset.Element("Name").Value);

                    //NOTE: Unable to determine asset state from Rally data, so all get set to "Closed".
                    cmd.Parameters.AddWithValue("@AssetState", "Closed");

                    //Mutiple Rally fields are concatenated for the V1 description (Description + Notes + Objective):
                    string combinedDescription = GetCombinedDescription(asset.Element("Description").Value, asset.Element("Notes").Value, "Notes").ToString();
                    combinedDescription = GetCombinedDescription(combinedDescription, asset.Element("Objective").Value, "Objective").ToString();
                    cmd.Parameters.AddWithValue("@Description", combinedDescription);
                    cmd.Parameters.AddWithValue("@Category", asset.Element("Type").Value);

                    //Rally LastVerdict contains Pass|Fail data.
                    if (asset.Descendants("LastVerdict").Any())
                        cmd.Parameters.AddWithValue("@Status", asset.Element("LastVerdict").Value);
                    else
                        cmd.Parameters.AddWithValue("@Status", DBNull.Value);

                    if (asset.Descendants("Owner").Any())
                        cmd.Parameters.AddWithValue("@Owners", GetMemberOIDFromDB(GetRefValue(asset.Element("Owner").Attribute("ref").Value)));
                    else
                        cmd.Parameters.AddWithValue("@Owners", DBNull.Value);

                    if (asset.Descendants("WorkProduct").Any())
                    {
                        cmd.Parameters.AddWithValue("@Parent", GetRefValue(asset.Element("WorkProduct").Attribute("ref").Value));
                        if (asset.Element("WorkProduct").Attribute("type").Value == "HierarchicalRequirement")
                            cmd.Parameters.AddWithValue("@ParentType", "Story");
                        else if (asset.Element("WorkProduct").Attribute("type").Value == "Defect")
                            cmd.Parameters.AddWithValue("@ParentType", "Defect");
                        else
                            cmd.Parameters.AddWithValue("@ParentType", asset.Element("WorkProduct").Attribute("type").Value);
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("@Parent", DBNull.Value);
                        cmd.Parameters.AddWithValue("@ParentType", DBNull.Value);
                    }

                    cmd.Parameters.AddWithValue("@Setup", asset.Element("PreConditions").Value);
                    cmd.Parameters.AddWithValue("@Inputs", asset.Element("ValidationInput").Value);
                    cmd.Parameters.AddWithValue("@Steps", GetTestSteps(asset.Element("ObjectID").Value));
                    cmd.Parameters.AddWithValue("@ExpectedResults", GetCombinedDescription(asset.Element("ValidationExpectedResult").Value, asset.Element("PostConditions").Value, "PostConditions").ToString());
                    cmd.Parameters.AddWithValue("@ActualResults", DBNull.Value);

                    //CUSTOM FIELDS:
                    //Hacked for Tripwire, needs refactoring.
                    if (asset.Descendants("LastRun").Any())
                    {
                        if (String.IsNullOrEmpty(asset.Element("LastRun").Value) == false)
                            CreateCustomField(asset.Element("ObjectID").Value, "LastRun", "Date", ConvertRallyDate(asset.Element("LastRun").Value));
                    }
                    if (asset.Descendants("LastBuild").Any())
                    {
                        if (String.IsNullOrEmpty(asset.Element("LastBuild").Value) == false)
                            CreateCustomField(asset.Element("ObjectID").Value, "LastBuild", "Text", asset.Element("LastBuild").Value);
                    }

                    //ATTACHMENTS: Hack for Tripwire Chould be refactored into its own class.
                    if (System.Convert.ToInt32(asset.Element("Attachments").Element("Count").Value) > 0)
                        SaveAttachmentRecords(asset.Element("ObjectID").Value, "Test", asset.Element("Attachments"));

                    cmd.ExecuteNonQuery();
                }
                assetCounter++;
            }
            return assetCounter;
        }

        private object GetTestState(string State)
        {
            switch (State)
            {
                case "Defined":
                    return "Active";
                case "In-Progress":
                    return "Active";
                case "Completed":
                    return "Closed";
                default:
                    return DBNull.Value;
            }
        }

        private object GetTestStatus(string Status)
        {
            switch (Status)
            {
                case "Pass":
                    return "Passed";
                case "Fail":
                    return "Failed";
                default:
                    return DBNull.Value;
            }
        }

        private string BuildTestInsertStatement()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO TESTS (");
            sb.Append("AssetOID,");
            sb.Append("AssetState,");
            sb.Append("AssetNumber,");
            sb.Append("Owners,");
            sb.Append("Description,");
            sb.Append("Name,");
            sb.Append("Category,");
            sb.Append("Steps,");
            sb.Append("Inputs,");
            sb.Append("Setup,");
            sb.Append("ExpectedResults,");
            sb.Append("ActualResults,");
            sb.Append("Status,");
            sb.Append("Parent,");
            sb.Append("ParentType) ");
            sb.Append("VALUES (");
            sb.Append("@AssetOID,");
            sb.Append("@AssetState,");
            sb.Append("@AssetNumber,");
            sb.Append("@Owners,");
            sb.Append("@Description,");
            sb.Append("@Name,");
            sb.Append("@Category,");
            sb.Append("@Steps,");
            sb.Append("@Inputs,");
            sb.Append("@Setup,");
            sb.Append("@ExpectedResults,");
            sb.Append("@ActualResults,");
            sb.Append("@Status,");
            sb.Append("@Parent,");
            sb.Append("@ParentType);");
            return sb.ToString();
        }

    }
}
