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
    public class ImportGoals : IImportAssets
    {
        public ImportGoals(SqlConnection sqlConn, MetaModel MetaAPI, Services DataAPI, MigrationConfiguration Configurations)
            : base(sqlConn, MetaAPI, DataAPI, Configurations) { }

        public override int Import()
        {
            SqlDataReader sdr = GetImportDataFromDBTable("Goals");

            int importCount = 0;
            while (sdr.Read())
            {
                try
                {
                    //SPECIAL CASE: Orphaned goal that has no assigned scope, fail to import.
                    if (String.IsNullOrEmpty(sdr["Scope"].ToString()))
                    {
                        UpdateImportStatus("Goals", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, "Goal has no scope.");
                        continue;
                    }

                    IAssetType assetType = _metaAPI.GetAssetType("Goal");
                    Asset asset = _dataAPI.New(assetType, null);

                    IAttributeDefinition fullNameAttribute = assetType.GetAttributeDefinition("Name");
                    asset.SetAttributeValue(fullNameAttribute, sdr["Name"].ToString());

                    IAttributeDefinition descAttribute = assetType.GetAttributeDefinition("Description");
                    asset.SetAttributeValue(descAttribute, sdr["Description"].ToString());

                    //if (String.IsNullOrEmpty(sdr["TargetedBy"].ToString()) == false)
                    //{
                    //    AddMultiValueRelation(assetType, asset, "TargetedBy", "Scope:18910");
                    //      AddMultiValueRelation(assetType, asset, "TargetedBy", GetNewAssetOIDFromDB(sdr["TargetedBy"].ToString(), "Projects"));
                    //}
                    //if (String.IsNullOrEmpty(sdr["TargetedBy"].ToString()) == false)
                    //{
                    //    IAttributeDefinition targetedbyAttribute = assetType.GetAttributeDefinition("TargetedBy");
                    //    asset.SetAttributeValue(targetedbyAttribute, GetNewAssetOIDFromDB(sdr["TargetedBy"].ToString(), "Projects")); 
                    //}
                    
                    IAttributeDefinition scopeAttribute = assetType.GetAttributeDefinition("Scope");
                    asset.SetAttributeValue(scopeAttribute, GetNewAssetOIDFromDB(sdr["Scope"].ToString(), "Projects"));

                    IAttributeDefinition priorityAttribute = assetType.GetAttributeDefinition("Priority");
                    asset.SetAttributeValue(priorityAttribute, GetNewListTypeAssetOIDFromDB(sdr["Priority"].ToString()));

                    IAttributeDefinition categoryAttribute = assetType.GetAttributeDefinition("Category");
                    asset.SetAttributeValue(categoryAttribute, GetNewListTypeAssetOIDFromDB(sdr["Category"].ToString()));

                    _dataAPI.Save(asset);
                    string newAssetNumber = GetAssetNumberV1("Goal", asset.Oid.Momentless.ToString());
                    UpdateNewAssetOIDAndNumberInDB("Goals", sdr["AssetOID"].ToString(), asset.Oid.Momentless.ToString(), newAssetNumber);
                    UpdateImportStatus("Goals", sdr["AssetOID"].ToString(), ImportStatuses.IMPORTED, "Goal imported.");
                    importCount++;
                }
                catch (Exception ex)
                {
                    if (_config.V1Configurations.LogExceptions == true)
                    {
                        UpdateImportStatus("Goals", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, ex.Message);
                        continue;
                    }
                    else
                    {
                        throw ex;
                    }
                }
            }
            sdr.Close();
            return importCount;
        }

        public int CloseGoals()
        {
            SqlDataReader sdr = GetImportDataFromDBTableForClosing("Goals");
            int assetCount = 0;
            while (sdr.Read())
            {
                Asset asset = GetAssetFromV1(sdr["NewAssetOID"].ToString());
                ExecuteOperationInV1("Goal.Inactivate", asset.Oid);
                assetCount++;
            }
            sdr.Close();
            return assetCount;
        }

    }
}
