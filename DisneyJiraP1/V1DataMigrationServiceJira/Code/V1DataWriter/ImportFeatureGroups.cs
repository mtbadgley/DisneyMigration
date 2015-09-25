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
    public class ImportFeatureGroups : IImportAssets
    {
        public ImportFeatureGroups(SqlConnection sqlConn, MetaModel MetaAPI, Services DataAPI, MigrationConfiguration Configurations)
            : base(sqlConn, MetaAPI, DataAPI, Configurations) { }

        public override int Import()
        {
            string customV1IDFieldName = GetV1IDCustomFieldName("Theme");
            SqlDataReader sdr = GetImportDataFromDBTableWithOrder("FeatureGroups");

            int importCount = 0;
            while (sdr.Read())
            {
                try
                {
                    //SPECIAL CASE: Orphaned feature group that has no assigned scope, fail to import.
                    if (String.IsNullOrEmpty(sdr["Scope"].ToString()))
                    {
                        UpdateImportStatus("FeatureGroups", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, "Feature group has no scope.");
                        continue;
                    }

                    IAssetType assetType = _metaAPI.GetAssetType("Theme");
                    Asset asset = _dataAPI.New(assetType, null);

                    if (String.IsNullOrEmpty(customV1IDFieldName) == false)
                    {
                        IAttributeDefinition customV1IDAttribute = assetType.GetAttributeDefinition(customV1IDFieldName);
                        asset.SetAttributeValue(customV1IDAttribute, sdr["AssetNumber"].ToString());
                    }

                    IAttributeDefinition fullNameAttribute = assetType.GetAttributeDefinition("Name");
                    asset.SetAttributeValue(fullNameAttribute, sdr["Name"].ToString());

                    IAttributeDefinition descAttribute = assetType.GetAttributeDefinition("Description");
                    asset.SetAttributeValue(descAttribute, sdr["Description"].ToString());

                    IAttributeDefinition customerAttribute = assetType.GetAttributeDefinition("Customer");
                    asset.SetAttributeValue(customerAttribute, GetNewAssetOIDFromDB(sdr["Customer"].ToString()));

                    if (String.IsNullOrEmpty(sdr["Owners"].ToString()) == false)
                    {
                        AddMultiValueRelation(assetType, asset, "Owners", sdr["Owners"].ToString());
                    }

                    if (String.IsNullOrEmpty(sdr["Goals"].ToString()) == false)
                    {
                        AddMultiValueRelation(assetType, asset, "Goals", sdr["Goals"].ToString());
                    }

                    IAttributeDefinition referenceAttribute = assetType.GetAttributeDefinition("Reference");
                    asset.SetAttributeValue(referenceAttribute, sdr["Reference"].ToString());

                    IAttributeDefinition valueAttribute = assetType.GetAttributeDefinition("Value");
                    asset.SetAttributeValue(valueAttribute, sdr["Value"].ToString());

                    IAttributeDefinition estimateAttribute = assetType.GetAttributeDefinition("Estimate");
                    asset.SetAttributeValue(estimateAttribute, sdr["Estimate"].ToString());

                    IAttributeDefinition lastVersionAttribute = assetType.GetAttributeDefinition("LastVersion");
                    asset.SetAttributeValue(lastVersionAttribute, sdr["LastVersion"].ToString());

                    IAttributeDefinition scopeAttribute = assetType.GetAttributeDefinition("Scope");
                    asset.SetAttributeValue(scopeAttribute, GetNewAssetOIDFromDB(sdr["Scope"].ToString(),"Projects"));

                    IAttributeDefinition riskAttribute = assetType.GetAttributeDefinition("Risk");
                    asset.SetAttributeValue(riskAttribute, GetNewListTypeAssetOIDFromDB(sdr["Risk"].ToString()));

                    IAttributeDefinition priorityAttribute = assetType.GetAttributeDefinition("Priority");
                    asset.SetAttributeValue(priorityAttribute, GetNewListTypeAssetOIDFromDB(sdr["Priority"].ToString()));

                    IAttributeDefinition statusAttribute = assetType.GetAttributeDefinition("Status");
                    asset.SetAttributeValue(statusAttribute, GetNewListTypeAssetOIDFromDB(sdr["Status"].ToString()));

                    IAttributeDefinition categoryAttribute = assetType.GetAttributeDefinition("Category");
                    asset.SetAttributeValue(categoryAttribute, GetNewListTypeAssetOIDFromDB(sdr["Category"].ToString()));

                    IAttributeDefinition sourceAttribute = assetType.GetAttributeDefinition("Source");
                    asset.SetAttributeValue(sourceAttribute, GetNewListTypeAssetOIDFromDB(sdr["Source"].ToString()));

                    IAttributeDefinition areaAttribute = assetType.GetAttributeDefinition("Area");
                    asset.SetAttributeValue(areaAttribute, GetNewListTypeAssetOIDFromDB(sdr["Area"].ToString()));

                    _dataAPI.Save(asset);
                    string newAssetNumber = GetAssetNumberV1("Theme", asset.Oid.Momentless.ToString());
                    UpdateNewAssetOIDAndNumberInDB("FeatureGroups", sdr["AssetOID"].ToString(), asset.Oid.Momentless.ToString(), newAssetNumber);
                    UpdateImportStatus("FeatureGroups", sdr["AssetOID"].ToString(), ImportStatuses.IMPORTED, "Feature group imported.");
                    importCount++;
                }
                catch (Exception ex)
                {
                    if (_config.V1Configurations.LogExceptions == true)
                    {
                        UpdateImportStatus("FeatureGroups", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, ex.Message);
                        continue;
                    }
                    else
                    {
                        throw ex;
                    }
                }
            }
            sdr.Close();
            SetParentFeatureGroups();
            return importCount;
        }

        private void SetParentFeatureGroups()
        {
            SqlDataReader sdr = GetImportDataFromDBTable("FeatureGroups");
            while (sdr.Read())
            {
                IAssetType assetType = _metaAPI.GetAssetType("Theme");
                Asset asset = GetAssetFromV1(sdr["NewAssetOID"].ToString());

                IAttributeDefinition parentAttribute = assetType.GetAttributeDefinition("Parent");
                asset.SetAttributeValue(parentAttribute, GetNewAssetOIDFromDB(sdr["Parent"].ToString()));

                _dataAPI.Save(asset);
            }
            sdr.Close();
        }

        public int CloseFeatureGroups()
        {
            SqlDataReader sdr = GetImportDataFromDBTableForClosing("FeatureGroups");
            int assetCount = 0;
            while (sdr.Read())
            {
                Asset asset = GetAssetFromV1(sdr["NewAssetOID"].ToString());
                ExecuteOperationInV1("Theme.Inactivate", asset.Oid);
                assetCount++;
            }
            sdr.Close();
            return assetCount;
        }

    }
}
