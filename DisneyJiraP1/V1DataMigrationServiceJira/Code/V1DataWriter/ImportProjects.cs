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
    public class ImportProjects : IImportAssets
    {

        public ImportProjects(SqlConnection sqlConn, MetaModel MetaAPI, Services DataAPI, MigrationConfiguration Configurations)
            : base(sqlConn, MetaAPI, DataAPI, Configurations) { }

        public override int Import()
        {
            string customV1IDFieldName = GetV1IDCustomFieldName("Scope");
            SqlDataReader sdr = GetImportDataFromDBTable("Projects");

            int importCount = 0;
            while (sdr.Read())
            {
                try
                {
                    //SPECIAL CASE: Do not import Scope:0 (System (All Projects)).
                    if (sdr["AssetOID"].ToString() == "Scope:0")
                    {
                        UpdateNewAssetOIDAndStatus("Projects", sdr["AssetOID"].ToString(), sdr["AssetOID"].ToString(), ImportStatuses.SKIPPED, "System project (scope:0) cannot be imported.");
                        continue;
                    }

                    //SPECIAL CASE: If MergeRootProjects config options is "true" and the current project is the source root project, the root becomes the target project.
                    IAssetType assetType = _metaAPI.GetAssetType("Scope");
                    Asset asset = null;
                    IAttributeDefinition parentAttribute = null;
                    IAttributeDefinition fullNameAttribute = null;
                    if (_config.V1Configurations.MergeRootProjects == true && sdr["AssetOID"].ToString() == _config.V1SourceConnection.Project)
                    {
                        asset = GetAssetFromV1(_config.V1TargetConnection.Project);
                    }
                    else
                    {
                        asset = _dataAPI.New(assetType, null);

                        fullNameAttribute = assetType.GetAttributeDefinition("Name");
                        asset.SetAttributeValue(fullNameAttribute, sdr["Name"].ToString());

                        //NOTE: All sub-projects are intially assigned to the parent scope. They will be reassigned later.
                        parentAttribute = assetType.GetAttributeDefinition("Parent");
                        asset.SetAttributeValue(parentAttribute, _config.V1TargetConnection.Project);
                    }

                    if (String.IsNullOrEmpty(customV1IDFieldName) == false)
                    {
                        IAttributeDefinition customV1IDAttribute = assetType.GetAttributeDefinition(customV1IDFieldName);
                        asset.SetAttributeValue(customV1IDAttribute, sdr["AssetOID"].ToString());
                    }

                    IAttributeDefinition descAttribute = assetType.GetAttributeDefinition("Description");
                    asset.SetAttributeValue(descAttribute, sdr["Description"].ToString());

                    IAttributeDefinition scheduleAttribute = assetType.GetAttributeDefinition("Schedule");
                    asset.SetAttributeValue(scheduleAttribute, GetNewAssetOIDFromDB(sdr["Schedule"].ToString(), "Schedules"));

                    IAttributeDefinition ownerAttribute = assetType.GetAttributeDefinition("Owner");
                    asset.SetAttributeValue(ownerAttribute, GetNewAssetOIDFromDB(sdr["Owner"].ToString(), "Members"));

                    IAttributeDefinition beginDateAttribute = assetType.GetAttributeDefinition("BeginDate");
                    asset.SetAttributeValue(beginDateAttribute, sdr["BeginDate"].ToString());

                    IAttributeDefinition endDateAttribute = assetType.GetAttributeDefinition("EndDate");
                    asset.SetAttributeValue(endDateAttribute, sdr["EndDate"].ToString());

                    IAttributeDefinition statusAttribute = assetType.GetAttributeDefinition("Status");
                    asset.SetAttributeValue(statusAttribute, GetNewListTypeAssetOIDFromDB(sdr["Status"].ToString()));

                    IAttributeDefinition referenceAttribute = assetType.GetAttributeDefinition("Reference");
                    asset.SetAttributeValue(referenceAttribute, sdr["Reference"].ToString());

                    if (_config.V1Configurations.MigrateProjectMembership == true)
                    {
                        if (String.IsNullOrEmpty(sdr["Members"].ToString()) == false)
                        {
                            AddMultiValueRelation(assetType, asset, "Members", sdr["Members"].ToString());
                        }
                    }

                    _dataAPI.Save(asset);
                    UpdateNewAssetOIDAndStatus("Projects", sdr["AssetOID"].ToString(), asset.Oid.Momentless.ToString(), ImportStatuses.IMPORTED, "Project imported.");
                    importCount++;
                }
                catch (Exception ex)
                {
                    if (_config.V1Configurations.LogExceptions == true)
                    {
                        UpdateImportStatus("Projects", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, ex.Message);
                        continue;
                    }
                    else
                    {
                        throw ex;
                    }
                }
            }
            sdr.Close();
            //Hack: Turn off for Syngenta
            //SetParentProjects();
            return importCount;
        }

        private void SetParentProjects()
        {
            SqlDataReader sdr = GetImportDataFromDBTable("Projects");
            while (sdr.Read())
            {
                //SPECIAL CASE: Skip Scope:0 (System (All Projects)). 
                if (sdr["AssetOID"].ToString() == "Scope:0") continue;

                //SPECIAL CASE: Skip when current project matches the source project.
                if (sdr["AssetOID"].ToString() == _config.V1SourceConnection.Project) continue;

                Asset asset = GetAssetFromV1(sdr["NewAssetOID"].ToString());

                IAssetType assetType = _metaAPI.GetAssetType("Scope");
                IAttributeDefinition parentAttribute = assetType.GetAttributeDefinition("Parent");

                //If parent is Scope:0, assign to specified parent scope.
                if (sdr["Parent"].ToString() == "Scope:0")
                    asset.SetAttributeValue(parentAttribute, _config.V1TargetConnection.Project);
                else
                    asset.SetAttributeValue(parentAttribute, GetNewAssetOIDFromDB(sdr["Parent"].ToString(), "Projects"));

                _dataAPI.Save(asset);
            }
            sdr.Close();
        }

        public int SetMembershipToRoot()
        {
            SqlDataReader sdr = GetImportDataFromDBTable("Members");

            Asset asset = GetAssetFromV1(_config.V1TargetConnection.Project);
            IAssetType assetType = _metaAPI.GetAssetType("Scope");
            IAttributeDefinition memberAttribute = assetType.GetAttributeDefinition("Members");

            int assetCount = 0;
            while (sdr.Read())
            {
                //Do not assign membership for failed import members or the admin account (cannot self edit membership).
                if (sdr["ImportStatus"].ToString() != ImportStatuses.FAILED.ToString() && sdr["NewAssetOID"].ToString() != "Member:20")
                {
                    asset.AddAttributeValue(memberAttribute, sdr["NewAssetOID"].ToString());
                    _dataAPI.Save(asset);
                    assetCount++;
                }
            }
            sdr.Close();
            return assetCount;
        }

        public int CloseProjects()
        {
            SqlDataReader sdr = GetImportDataFromDBTableForClosing("Projects");
            int assetCount = 0;
            while (sdr.Read())
            {
                Asset asset = GetAssetFromV1(sdr["NewAssetOID"].ToString());
                ExecuteOperationInV1("Scope.Inactivate", asset.Oid);
                assetCount++;
            }
            sdr.Close();
            return assetCount;
        }

    }
}
