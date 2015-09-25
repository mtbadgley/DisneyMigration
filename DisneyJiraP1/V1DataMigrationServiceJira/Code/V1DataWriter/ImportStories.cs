using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using VersionOne.SDK.APIClient;
using V1DataCore;

namespace V1DataWriter
{
    public class ImportStories : IImportAssets
    {
        public ImportStories(SqlConnection sqlConn, MetaModel MetaAPI, Services DataAPI, MigrationConfiguration Configurations)
            : base(sqlConn, MetaAPI, DataAPI, Configurations) { }

        public override int Import()
        {
            string customV1IDFieldName = GetV1IDCustomFieldName("PrimaryWorkitem");
            SqlDataReader sdr = GetImportDataFromDBTableWithOrder("Stories");

            int importCount = 0;
            while (sdr.Read())
            {
                try
                {
                    //SPECIAL CASE: No assigned scope, fail to import.
                    if (String.IsNullOrEmpty(sdr["Scope"].ToString()))
                    {
                        UpdateImportStatus("Stories", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, "Story has no scope.");
                        continue;
                    }

                    IAssetType assetType = _metaAPI.GetAssetType("Story");
                    Asset asset = _dataAPI.New(assetType, null);

                    if (String.IsNullOrEmpty(customV1IDFieldName) == false)
                    {
                        IAttributeDefinition customV1IDAttribute = assetType.GetAttributeDefinition(customV1IDFieldName);
                        asset.SetAttributeValue(customV1IDAttribute, sdr["AssetNumber"].ToString());
                    }

                    IAttributeDefinition fullNameAttribute = assetType.GetAttributeDefinition("Name");
                    asset.SetAttributeValue(fullNameAttribute, AddV1IDToTitle(sdr["Name"].ToString(), sdr["AssetNumber"].ToString()));

                    IAttributeDefinition descAttribute = assetType.GetAttributeDefinition("Description");
                    asset.SetAttributeValue(descAttribute, sdr["Description"].ToString());

                    string timeboxOid = GetNewAssetOIDFromDB(sdr["Timebox"].ToString(), "Iterations");
                    if (String.IsNullOrEmpty(timeboxOid) == false)
                    {
                        IAttributeDefinition iterationAttribute = assetType.GetAttributeDefinition("Timebox");
                        asset.SetAttributeValue(iterationAttribute, timeboxOid);
                    }

                    IAttributeDefinition customerAttribute = assetType.GetAttributeDefinition("Customer");
                    asset.SetAttributeValue(customerAttribute, GetNewAssetOIDFromDB(sdr["Customer"].ToString()));

                    if (String.IsNullOrEmpty(sdr["Owners"].ToString()) == false)
                    {
                        AddMultiValueRelation(assetType, asset, "Members", "Owners", sdr["Owners"].ToString());
                    }

                    IAttributeDefinition teamAttribute = assetType.GetAttributeDefinition("Team");
                    asset.SetAttributeValue(teamAttribute, GetNewAssetOIDFromDB(sdr["Team"].ToString()));

                    if (String.IsNullOrEmpty(sdr["Goals"].ToString()) == false)
                    {
                        AddMultiValueRelation(assetType, asset, "Goals", sdr["Goals"].ToString());
                    }

                    //TO DO: Test for V1 version number for epic conversion. Right now, assume epic.
                    IAttributeDefinition superAttribute = assetType.GetAttributeDefinition("Super");
                    asset.SetAttributeValue(superAttribute, GetNewEpicAssetOIDFromDB(sdr["Super"].ToString()));

                    IAttributeDefinition referenceAttribute = assetType.GetAttributeDefinition("Reference");
                    asset.SetAttributeValue(referenceAttribute, sdr["Reference"].ToString());

                    IAttributeDefinition detailEstimateAttribute = assetType.GetAttributeDefinition("DetailEstimate");
                    asset.SetAttributeValue(detailEstimateAttribute, sdr["DetailEstimate"].ToString());

                    IAttributeDefinition estimateAttribute = assetType.GetAttributeDefinition("Estimate");
                    asset.SetAttributeValue(estimateAttribute, sdr["Estimate"].ToString());

                    IAttributeDefinition toDoAttribute = assetType.GetAttributeDefinition("ToDo");
                    asset.SetAttributeValue(toDoAttribute, sdr["ToDo"].ToString());

                    IAttributeDefinition lastVersionAttribute = assetType.GetAttributeDefinition("LastVersion");
                    asset.SetAttributeValue(lastVersionAttribute, sdr["LastVersion"].ToString());

                    IAttributeDefinition originalEstimateAttribute = assetType.GetAttributeDefinition("OriginalEstimate");
                    asset.SetAttributeValue(originalEstimateAttribute, sdr["OriginalEstimate"].ToString());

                    IAttributeDefinition requestedByAttribute = assetType.GetAttributeDefinition("RequestedBy");
                    asset.SetAttributeValue(requestedByAttribute, sdr["RequestedBy"].ToString());

                    IAttributeDefinition valueAttribute = assetType.GetAttributeDefinition("Value");
                    asset.SetAttributeValue(valueAttribute, sdr["Value"].ToString());

                    IAttributeDefinition scopeAttribute = assetType.GetAttributeDefinition("Scope");
                    asset.SetAttributeValue(scopeAttribute, GetNewAssetOIDFromDB(sdr["Scope"].ToString(), "Projects"));

                    IAttributeDefinition riskAttribute = assetType.GetAttributeDefinition("Risk");
                    asset.SetAttributeValue(riskAttribute, GetNewListTypeAssetOIDFromDB(sdr["Risk"].ToString()));

                    IAttributeDefinition sourceAttribute = assetType.GetAttributeDefinition("Source");
                    if (String.IsNullOrEmpty(_config.V1Configurations.SourceListTypeValue) == false)
                        asset.SetAttributeValue(sourceAttribute, _config.V1Configurations.SourceListTypeValue);
                    else
                        asset.SetAttributeValue(sourceAttribute, GetNewListTypeAssetOIDFromDB(sdr["Source"].ToString()));

                    IAttributeDefinition priorityAttribute = assetType.GetAttributeDefinition("Priority");
                    asset.SetAttributeValue(priorityAttribute, GetNewListTypeAssetOIDFromDB("WorkitemPriority", sdr["Priority"].ToString()));

                    //HACK: For Rally import, needs to be refactored.
                    IAttributeDefinition statusAttribute = assetType.GetAttributeDefinition("Status");
                    //asset.SetAttributeValue(statusAttribute, GetNewListTypeAssetOIDFromDB(sdr["Status"].ToString()));
                    asset.SetAttributeValue(statusAttribute, GetNewListTypeAssetOIDFromDB("StoryStatus", sdr["Status"].ToString()));

                    IAttributeDefinition categoryAttribute = assetType.GetAttributeDefinition("Category");
                    asset.SetAttributeValue(categoryAttribute, GetNewListTypeAssetOIDFromDB("StoryCategory", sdr["Category"].ToString()));

                    IAttributeDefinition parentAttribute = assetType.GetAttributeDefinition("Parent");
                    asset.SetAttributeValue(parentAttribute, GetNewAssetOIDFromDB(sdr["Parent"].ToString(),"FeatureGroups"));

                    if (String.IsNullOrEmpty(sdr["Requests"].ToString()) == false)
                    {
                        AddMultiValueRelation(assetType, asset, "Requests", sdr["Requests"].ToString());
                    }

                    if (String.IsNullOrEmpty(sdr["BlockingIssues"].ToString()) == false)
                    {
                        AddMultiValueRelation(assetType, asset, "BlockingIssues", sdr["BlockingIssues"].ToString());
                    }

                    if (String.IsNullOrEmpty(sdr["Issues"].ToString()) == false)
                    {
                        AddMultiValueRelation(assetType, asset, "Issues", sdr["Issues"].ToString());
                    }

                    IAttributeDefinition benefitsAttribute = assetType.GetAttributeDefinition("Benefits");
                    asset.SetAttributeValue(benefitsAttribute, sdr["Benefits"].ToString());

                    _dataAPI.Save(asset);

                    if (sdr["AssetState"].ToString() == "Template")
                    {
                        ExecuteOperationInV1("Story.MakeTemplate", asset.Oid);
                    }

                    string newAssetNumber = GetAssetNumberV1("Story", asset.Oid.Momentless.ToString());
                    UpdateAssetRecordWithNumber("Stories", sdr["AssetOID"].ToString(), asset.Oid.Momentless.ToString(), newAssetNumber, ImportStatuses.IMPORTED, "Story imported.");
                    importCount++;
                }
                catch (Exception ex)
                {
                    if (_config.V1Configurations.LogExceptions == true)
                    {
                        string error = ex.Message.Replace("'", ":");
                        UpdateImportStatus("Stories", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, error);
                        continue;
                    }
                    else
                    {
                        throw ex;
                    }
                }
            }
            sdr.Close();
            //HACK: Disabling - no dependencies for Syngenta
            //SetStoryDependencies();
            return importCount;
        }

        public void SetStoryDependencies()
        {
            SqlDataReader sdr = GetImportDataFromDBTable("Stories");
            while (sdr.Read())
            {
                IAssetType assetType = _metaAPI.GetAssetType("Story");
                Asset asset = GetAssetFromV1(sdr["NewAssetOID"].ToString());

                if (String.IsNullOrEmpty(sdr["Dependencies"].ToString()) == false)
                {
                    AddMultiValueRelation(assetType, asset, "Stories", "Dependencies", sdr["Dependencies"].ToString());
                }

                if (String.IsNullOrEmpty(sdr["Dependants"].ToString()) == false)
                {
                    AddMultiValueRelation(assetType, asset, "Stories", "Dependants", sdr["Dependants"].ToString());
                }
                _dataAPI.Save(asset);
            }
            sdr.Close();
        }

        public int CloseStories()
        {
            SqlDataReader sdr = GetImportDataFromDBTableForClosing("Stories");
            int assetCount = 0;
            while (sdr.Read())
            {
                Asset asset = GetAssetFromV1(sdr["NewAssetOID"].ToString());
                ExecuteOperationInV1("Story.Inactivate", asset.Oid);
                assetCount++;
            }
            sdr.Close();
            return assetCount;
        }

    }
}
