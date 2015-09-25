﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using VersionOne.SDK.APIClient;
using V1DataCore;

namespace V1DataWriter
{
    public class ImportDefects : IImportAssets
    {
        public ImportDefects(SqlConnection sqlConn, MetaModel MetaAPI, Services DataAPI, MigrationConfiguration Configurations)
            : base(sqlConn, MetaAPI, DataAPI, Configurations) { }

        public override int Import()
        {
            string customV1IDFieldName = GetV1IDCustomFieldName("PrimaryWorkitem");
            SqlDataReader sdr = GetImportDataFromDBTableWithOrder("Defects");

            int importCount = 0;
            while (sdr.Read())
            {
                try
                {
                    //SPECIAL CASE: No assigned scope, fail to import.
                    if (String.IsNullOrEmpty(sdr["Scope"].ToString()))
                    {
                        UpdateImportStatus("Defects", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, "Defect has no scope.");
                        continue;
                    }

                    IAssetType assetType = _metaAPI.GetAssetType("Defect");
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

                    IAttributeDefinition verifiedByAttribute = assetType.GetAttributeDefinition("VerifiedBy");
                    
                    asset.SetAttributeValue(verifiedByAttribute, GetNewAssetOIDFromDB(sdr["VerifiedBy"].ToString(), "Members"));

                    if (String.IsNullOrEmpty(sdr["Owners"].ToString()) == false)
                    {
                        AddMultiValueRelation(assetType, asset, "Members", "Owners", sdr["Owners"].ToString());
                    }

                    IAttributeDefinition teamAttribute = assetType.GetAttributeDefinition("Team");
                    asset.SetAttributeValue(teamAttribute, GetNewAssetOIDFromDB(sdr["Team"].ToString()));

                    //TO DO: Versions (VersionLabel)???

                    if (String.IsNullOrEmpty(sdr["Goals"].ToString()) == false)
                    {
                        AddMultiValueRelation(assetType, asset, "Goals", sdr["Goals"].ToString());
                    }

                    IAttributeDefinition superAttribute = assetType.GetAttributeDefinition("Super");
                    asset.SetAttributeValue(superAttribute, GetNewAssetOIDFromDB(sdr["Super"].ToString()));

                    IAttributeDefinition referenceAttribute = assetType.GetAttributeDefinition("Reference");
                    asset.SetAttributeValue(referenceAttribute, sdr["Reference"].ToString());

                    IAttributeDefinition detailEstimateAttribute = assetType.GetAttributeDefinition("DetailEstimate");
                    asset.SetAttributeValue(detailEstimateAttribute, sdr["DetailEstimate"].ToString());

                    IAttributeDefinition toDoAttribute = assetType.GetAttributeDefinition("ToDo");
                    asset.SetAttributeValue(toDoAttribute, sdr["ToDo"].ToString());

                    IAttributeDefinition estimateAttribute = assetType.GetAttributeDefinition("Estimate");
                    asset.SetAttributeValue(estimateAttribute, sdr["Estimate"].ToString());

                    IAttributeDefinition environmentAttribute = assetType.GetAttributeDefinition("Environment");
                    asset.SetAttributeValue(environmentAttribute, sdr["Environment"].ToString());

                    IAttributeDefinition resolutionAttribute = assetType.GetAttributeDefinition("Resolution");
                    asset.SetAttributeValue(resolutionAttribute, sdr["Resolution"].ToString());

                    IAttributeDefinition versionAffectedAttribute = assetType.GetAttributeDefinition("VersionAffected");
                    asset.SetAttributeValue(versionAffectedAttribute, sdr["VersionAffected"].ToString());

                    IAttributeDefinition fixedInBuildAttribute = assetType.GetAttributeDefinition("FixedInBuild");
                    asset.SetAttributeValue(fixedInBuildAttribute, sdr["FixedInBuild"].ToString());

                    IAttributeDefinition foundByAttribute = assetType.GetAttributeDefinition("FoundBy");
                    asset.SetAttributeValue(foundByAttribute, sdr["FoundBy"].ToString());

                    IAttributeDefinition scopeAttribute = assetType.GetAttributeDefinition("Scope");
                    asset.SetAttributeValue(scopeAttribute, GetNewAssetOIDFromDB(sdr["Scope"].ToString(), "Projects"));

                    //HACK: For Rally import, needs to be refactored.
                    IAttributeDefinition statusAttribute = assetType.GetAttributeDefinition("Status");
                    //asset.SetAttributeValue(statusAttribute, GetNewListTypeAssetOIDFromDB(sdr["Status"].ToString()));
                    asset.SetAttributeValue(statusAttribute, GetNewListTypeAssetOIDFromDB("StoryStatus", sdr["Status"].ToString()));

                    //HACK: For Rally import, needs to be refactored.
                    IAttributeDefinition typeAttribute = assetType.GetAttributeDefinition("Type");
                    //asset.SetAttributeValue(typeAttribute, GetNewListTypeAssetOIDFromDB(sdr["Type"].ToString()));
                    asset.SetAttributeValue(typeAttribute, GetNewListTypeAssetOIDFromDB("DefectType", sdr["Type"].ToString()));

                    //HACK: For Rally import, needs to be refactored.
                    IAttributeDefinition resolutionReasonAttribute = assetType.GetAttributeDefinition("ResolutionReason");
                    //asset.SetAttributeValue(resolutionReasonAttribute, GetNewListTypeAssetOIDFromDB(sdr["ResolutionReason"].ToString()));
                    asset.SetAttributeValue(resolutionReasonAttribute, GetNewListTypeAssetOIDFromDB("DefectResolution", sdr["ResolutionReason"].ToString()));

                    //HACK: For Rally import, needs to be refactored.
                    IAttributeDefinition sourceAttribute = assetType.GetAttributeDefinition("Source");
                    if (String.IsNullOrEmpty(_config.V1Configurations.SourceListTypeValue) == false)
                        asset.SetAttributeValue(sourceAttribute, _config.V1Configurations.SourceListTypeValue);
                    else
                        asset.SetAttributeValue(sourceAttribute, GetNewListTypeAssetOIDFromDB(sdr["Source"].ToString()));

                    //HACK: For Rally import, needs to be refactored.
                    IAttributeDefinition priorityAttribute = assetType.GetAttributeDefinition("Priority");
                    //asset.SetAttributeValue(priorityAttribute, GetNewListTypeAssetOIDFromDB(sdr["Priority"].ToString()));
                    asset.SetAttributeValue(priorityAttribute, GetNewListTypeAssetOIDFromDB("WorkitemPriority", sdr["Priority"].ToString()));

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

                    _dataAPI.Save(asset);

                    if (sdr["AssetState"].ToString() == "Template")
                    {
                        ExecuteOperationInV1("Defect.MakeTemplate", asset.Oid);
                    }

                    string newAssetNumber = GetAssetNumberV1("Defect", asset.Oid.Momentless.ToString());
                    UpdateNewAssetOIDAndNumberInDB("Defects", sdr["AssetOID"].ToString(), asset.Oid.Momentless.ToString(), newAssetNumber);
                    UpdateImportStatus("Defects", sdr["AssetOID"].ToString(), ImportStatuses.IMPORTED, "Defect imported.");
                    importCount++;
                }
                catch (Exception ex)
                {
                    if (_config.V1Configurations.LogExceptions == true)
                    {
                        UpdateImportStatus("Defects", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, ex.Message);
                        continue;
                    }
                    else
                    {
                        throw ex;
                    }
                }
            }
            sdr.Close();
            //SetDefectDependencies();
            //SetDefectRelationships();
            return importCount;
        }

        public void SetDefectDependencies()
        {
            SqlDataReader sdr = GetImportDataFromDBTable("Defects");
            while (sdr.Read())
            {
                IAssetType assetType = _metaAPI.GetAssetType("Defect");
                Asset asset = GetAssetFromV1(sdr["NewAssetOID"].ToString());

                if (String.IsNullOrEmpty(sdr["Dependencies"].ToString()) == false)
                {
                    AddMultiValueRelation(assetType, asset, "Defects", "Dependencies", sdr["Dependencies"].ToString());
                }

                if (String.IsNullOrEmpty(sdr["Dependants"].ToString()) == false)
                {
                    AddMultiValueRelation(assetType, asset, "Defects", "Dependants", sdr["Dependants"].ToString());
                }
                _dataAPI.Save(asset);
            }
            sdr.Close();
        }

        public void SetDefectRelationships()
        {
            SqlDataReader sdr = GetImportDataFromDBTable("Defects");
            while (sdr.Read())
            {
                IAssetType assetType = _metaAPI.GetAssetType("Defect");
                Asset asset = GetAssetFromV1(sdr["NewAssetOID"].ToString());

                IAttributeDefinition duplicateOfAttribute = assetType.GetAttributeDefinition("DuplicateOf");
                asset.SetAttributeValue(duplicateOfAttribute, GetNewAssetOIDFromDB(sdr["DuplicateOf"].ToString()));

                if (String.IsNullOrEmpty(sdr["AffectedPrimaryWorkitems"].ToString()) == false)
                {
                    IAttributeDefinition affectedPrimaryWorkitemsAttribute = assetType.GetAttributeDefinition("AffectedPrimaryWorkitems");
                    string[] assetList = sdr["AffectedPrimaryWorkitems"].ToString().Split(';');
                    foreach (string item in assetList)
                    {
                        string assetOID = GetNewAssetOIDFromDB(item, "Stories");
                        if (String.IsNullOrEmpty(assetOID) == false)
                            asset.AddAttributeValue(affectedPrimaryWorkitemsAttribute, assetOID);
                    }
                }

                if (String.IsNullOrEmpty(sdr["AffectedByDefects"].ToString()) == false)
                {
                    AddMultiValueRelation(assetType, asset, "Defects", "AffectedByDefects", sdr["AffectedByDefects"].ToString());
                }
                _dataAPI.Save(asset);
            }
            sdr.Close();
        }

        public int CloseDefects()
        {
            SqlDataReader sdr = GetImportDataFromDBTableForClosing("Defects");
            int assetCount = 0;
            while (sdr.Read())
            {
                Asset asset = GetAssetFromV1(sdr["NewAssetOID"].ToString());
                ExecuteOperationInV1("Defect.Inactivate", asset.Oid);
                assetCount++;
            }
            sdr.Close();
            return assetCount;
        }

    }
}
