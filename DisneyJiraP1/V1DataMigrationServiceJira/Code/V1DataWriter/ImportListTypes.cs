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
    public class ImportListTypes : IImportAssets
    {
        public ImportListTypes(SqlConnection sqlConn, MetaModel MetaAPI, Services DataAPI, MigrationConfiguration Configurations)
            : base(sqlConn, MetaAPI, DataAPI, Configurations) { }

        public override int Import()
        {
            SqlDataReader sdr = GetImportDataFromDBTable("ListTypes");

            int importCount = 0;
            while (sdr.Read())
            {
                try
                {
                    //DUPLICATE CHECK:
                    string currentAssetOID = CheckForDuplicateInV1(sdr["AssetType"].ToString(), "Name", sdr["Name"].ToString());
                    if (string.IsNullOrEmpty(currentAssetOID) == false)
                    {
                        UpdateNewAssetOIDAndStatus("ListTypes", sdr["AssetOID"].ToString(), currentAssetOID, ImportStatuses.SKIPPED, "Duplicate list type.");
                    }
                    else
                    {
                        IAssetType assetType = _metaAPI.GetAssetType(sdr["AssetType"].ToString());
                        Asset asset = _dataAPI.New(assetType, null);

                        IAttributeDefinition fullNameAttribute = assetType.GetAttributeDefinition("Name");
                        asset.SetAttributeValue(fullNameAttribute, sdr["Name"].ToString());

                        IAttributeDefinition descAttribute = assetType.GetAttributeDefinition("Description");
                        asset.SetAttributeValue(descAttribute, sdr["Description"].ToString());

                        _dataAPI.Save(asset);
                        UpdateNewAssetOIDAndStatus("ListTypes", sdr["AssetOID"].ToString(), asset.Oid.Momentless.ToString(), ImportStatuses.IMPORTED, "List type imported.");
                        importCount++;
                    }

                    //SPECIAL CASE: Need to handle conversion of epic list types.
                    if (sdr["AssetType"].ToString() == "StoryStatus" || sdr["AssetType"].ToString() == "StoryCategory" || sdr["AssetType"].ToString() == "WorkitemPriority")
                    {
                        importCount += ConvertEpicListType(sdr["AssetOID"].ToString(), sdr["AssetType"].ToString(), sdr["Name"].ToString(), sdr["Description"].ToString());
                    }
                }
                catch (Exception ex)
                {
                    if (_config.V1Configurations.LogExceptions == true)
                    {
                        UpdateImportStatus("ListTypes", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, ex.Message);
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

        private int ConvertEpicListType(string AssetOID, string AssetType, string Name, string Description)
        {
            int importCount = 0;

            //Convert story list asset to epic list asset.
            string convertedAssetType;
            if (AssetType == "StoryStatus")
                convertedAssetType = "EpicStatus";
            else if (AssetType == "StoryCategory")
                convertedAssetType = "EpicCategory";
            else if (AssetType == "WorkitemPriority")
                convertedAssetType = "EpicPriority";
            else
                return 0;

            string currentAssetOID = CheckForDuplicateInV1(convertedAssetType, "Name", Name);

            if (string.IsNullOrEmpty(currentAssetOID) == false)
            {
                UpdateNewEpicAssetOIDInDB("ListTypes", AssetOID, currentAssetOID);
            }
            else
            {
                IAssetType assetType = _metaAPI.GetAssetType(convertedAssetType);
                Asset asset = _dataAPI.New(assetType, null);

                IAttributeDefinition fullNameAttribute = assetType.GetAttributeDefinition("Name");
                asset.SetAttributeValue(fullNameAttribute, Name);

                IAttributeDefinition descAttribute = assetType.GetAttributeDefinition("Description");
                asset.SetAttributeValue(descAttribute, Description);

                _dataAPI.Save(asset);
                UpdateNewEpicAssetOIDInDB("ListTypes", AssetOID, asset.Oid.Momentless.ToString());
                importCount++;
            }
            return importCount;
        }

        public int CloseListTypes()
        {
            SqlDataReader sdr = GetImportDataFromDBTableForClosing("ListTypes");
            int assetCount = 0;
            while (sdr.Read())
            {
                Asset asset = GetAssetFromV1(sdr["NewAssetOID"].ToString());
                ExecuteOperationInV1(sdr["AssetType"].ToString() + ".Inactivate", asset.Oid);
                assetCount++;
            }
            sdr.Close();
            return assetCount;
        }

    }
}
