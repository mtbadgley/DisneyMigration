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
    public class ImportIterations : IImportAssets
    {
        public ImportIterations(SqlConnection sqlConn, MetaModel MetaAPI, Services DataAPI, MigrationConfiguration Configurations)
            : base(sqlConn, MetaAPI, DataAPI, Configurations) { }

        public override int Import()
        {
            string customV1IDFieldName = GetV1IDCustomFieldName("Timebox");
            SqlDataReader sdr = GetImportDataFromDBTable("Iterations");

            int importCount = 0;
            while (sdr.Read())
            {
                try
                {
                    //SPECIAL CASE: Orphaned iteration that has no schedule, fail to import.
                    if (String.IsNullOrEmpty(sdr["Schedule"].ToString()))
                    {
                        UpdateImportStatus("Iterations", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, "Iteration has no schedule.");
                        continue;
                    }

                    //DUPLICATE CHECK: Check for existing sprint by name (and schedule).
                    string currentAssetOID = CheckForDuplicateIterationByName(sdr["Schedule"].ToString(), sdr["Name"].ToString());
                    if (string.IsNullOrEmpty(currentAssetOID) == false)
                    {
                        UpdateNewAssetOIDAndStatus("Iterations", sdr["AssetOID"].ToString(), currentAssetOID, ImportStatuses.SKIPPED, "Duplicate iteration, matched on name.");
                        ActivateIteration(sdr["AssetState"].ToString(), currentAssetOID);
                        continue;
                    }

                    //DUPLICATE CHECK: Check for existing sprint by begin date (and schedule) as the name did not match.
                    currentAssetOID = CheckForDuplicateIterationByDate(sdr["Schedule"].ToString(), sdr["BeginDate"].ToString());
                    if (string.IsNullOrEmpty(currentAssetOID) == false)
                    {
                        UpdateNewAssetOIDAndStatus("Iterations", sdr["AssetOID"].ToString(), currentAssetOID, ImportStatuses.UPDATED, "Duplicate iteration, matched on begin date, name was updated.");
                        ActivateIteration(sdr["AssetState"].ToString(), currentAssetOID);
                        UpdateIterationName(currentAssetOID, sdr["Name"].ToString().Trim());
                        continue;
                    }

                    IAssetType assetType = _metaAPI.GetAssetType("Timebox");
                    Asset asset = _dataAPI.New(assetType, null);

                    IAttributeDefinition fullNameAttribute = assetType.GetAttributeDefinition("Name");
                    asset.SetAttributeValue(fullNameAttribute, sdr["Name"].ToString().Trim());

                    if (String.IsNullOrEmpty(customV1IDFieldName) == false)
                    {
                        IAttributeDefinition customV1IDAttribute = assetType.GetAttributeDefinition(customV1IDFieldName);
                        asset.SetAttributeValue(customV1IDAttribute, sdr["AssetOID"].ToString());
                    }

                    IAttributeDefinition descAttribute = assetType.GetAttributeDefinition("Description");
                    asset.SetAttributeValue(descAttribute, sdr["Description"].ToString().Trim());

                    IAttributeDefinition ownerAttribute = assetType.GetAttributeDefinition("Owner");
                    asset.SetAttributeValue(ownerAttribute, GetNewAssetOIDFromDB(sdr["Owner"].ToString(), "Members"));

                    IAttributeDefinition scheduleAttribute = assetType.GetAttributeDefinition("Schedule");
                    asset.SetAttributeValue(scheduleAttribute, GetNewAssetOIDFromDB(sdr["Schedule"].ToString(), "Schedules"));

                    IAttributeDefinition beginDateAttribute = assetType.GetAttributeDefinition("BeginDate");
                    asset.SetAttributeValue(beginDateAttribute, sdr["BeginDate"].ToString());

                    IAttributeDefinition endDateAttribute = assetType.GetAttributeDefinition("EndDate");
                    asset.SetAttributeValue(endDateAttribute, sdr["EndDate"].ToString());

                    IAttributeDefinition targetEstimateAttribute = assetType.GetAttributeDefinition("TargetEstimate");
                    asset.SetAttributeValue(targetEstimateAttribute, sdr["TargetEstimate"].ToString());

                    //NOTE: Initally import all as "Active" state, will set future state after save.
                    IAttributeDefinition stateAttribute = assetType.GetAttributeDefinition("State");
                    asset.SetAttributeValue(stateAttribute, "State:101");

                    _dataAPI.Save(asset);

                    if (sdr["AssetState"].ToString() == "Future")
                    {
                        ExecuteOperationInV1("Timebox.MakeFuture", asset.Oid);
                    }

                    UpdateNewAssetOIDAndStatus("Iterations", sdr["AssetOID"].ToString(), asset.Oid.Momentless.ToString(), ImportStatuses.IMPORTED, "Iteration imported.");
                    importCount++;
                }
                catch (Exception ex)
                {
                    if (_config.V1Configurations.LogExceptions == true)
                    {
                        UpdateImportStatus("Iterations", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, "Iteration failed to import.");
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

        //Reactivate the iteration, will be closed again when CloseIterations is called.
        private void ActivateIteration(string AssetState, string NewAssetOID)
        {
            Asset asset = GetAssetFromV1(NewAssetOID);
            ExecuteOperationInV1("Timebox.Activate", asset.Oid);
        }

        private void UpdateIterationName(string AssetOID, string Name)
        {
            Asset asset = GetAssetFromV1(AssetOID);
            IAssetType assetType = _metaAPI.GetAssetType("Timebox");
            IAttributeDefinition fullNameAttribute = assetType.GetAttributeDefinition("Name");
            asset.SetAttributeValue(fullNameAttribute, Name);
            _dataAPI.Save(asset);
        }

        public int CloseIterations()
        {
            SqlDataReader sdr = GetImportDataFromDBTableForClosing("Iterations");
            int assetCount = 0;
            while (sdr.Read())
            {
                Asset asset = GetAssetFromV1(sdr["NewAssetOID"].ToString());
                ExecuteOperationInV1("Timebox.Close", asset.Oid);
                assetCount++;
            }
            sdr.Close();
            return assetCount;
        }

    }
}
