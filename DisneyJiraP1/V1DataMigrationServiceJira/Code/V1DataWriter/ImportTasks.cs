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
    public class ImportTasks : IImportAssets
    {
        public ImportTasks(SqlConnection sqlConn, MetaModel MetaAPI, Services DataAPI, MigrationConfiguration Configurations)
            : base(sqlConn, MetaAPI, DataAPI, Configurations) { }

        public override int Import()
        {
            string customV1IDFieldName = GetV1IDCustomFieldName("Task");

            //SqlDataReader sdr = GetImportDataFromSproc("spGetTasksForImport");
            SqlDataReader sdr = GetImportDataFromDBTableWithOrder("Tasks");

            int importCount = 0;
            while (sdr.Read())
            {
                try
                {
                    //CHECK DATA: Task must have a name.
                    if (String.IsNullOrEmpty(sdr["Name"].ToString()))
                    {
                        UpdateImportStatus("Tasks", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, "Task name attribute is required.");
                        continue;
                    }

                    //CHECK DATA: Task must have a parent.
                    if (String.IsNullOrEmpty(sdr["Parent"].ToString()))
                    {
                        UpdateImportStatus("Tasks", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, "Task parent attribute is required.");
                        continue;
                    }

                    IAssetType assetType = _metaAPI.GetAssetType("Task");
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

                    //if (String.IsNullOrEmpty(sdr["Customer"].ToString()) == false)
                    //{
                    //    IAttributeDefinition customerAttribute = assetType.GetAttributeDefinition("Customer");
                    //    asset.SetAttributeValue(customerAttribute, GetNewAssetOIDFromDB(sdr["Customer"].ToString()));
                    //}

                    if (String.IsNullOrEmpty(sdr["Owners"].ToString()) == false)
                    {
                        AddMultiValueRelation(assetType, asset, "Members", "Owners", sdr["Owners"].ToString());
                    }

                    if (String.IsNullOrEmpty(sdr["Goals"].ToString()) == false)
                    {
                        AddMultiValueRelation(assetType, asset, "Goals", sdr["Goals"].ToString());
                    }

                    IAttributeDefinition referenceAttribute = assetType.GetAttributeDefinition("Reference");
                    asset.SetAttributeValue(referenceAttribute, sdr["Reference"].ToString());

                    IAttributeDefinition detailEstimateAttribute = assetType.GetAttributeDefinition("DetailEstimate");
                    asset.SetAttributeValue(detailEstimateAttribute, sdr["DetailEstimate"].ToString());

                    IAttributeDefinition toDoAttribute = assetType.GetAttributeDefinition("ToDo");
                    asset.SetAttributeValue(toDoAttribute, sdr["ToDo"].ToString());

                    IAttributeDefinition estimateAttribute = assetType.GetAttributeDefinition("Estimate");
                    asset.SetAttributeValue(estimateAttribute, sdr["Estimate"].ToString());

                    IAttributeDefinition lastVersionAttribute = assetType.GetAttributeDefinition("LastVersion");
                    asset.SetAttributeValue(lastVersionAttribute, sdr["LastVersion"].ToString());

                    if (String.IsNullOrEmpty(sdr["Category"].ToString()) == false)
                    {
                        IAttributeDefinition categoryAttribute = assetType.GetAttributeDefinition("Category");
                        asset.SetAttributeValue(categoryAttribute, GetNewListTypeAssetOIDFromDB(sdr["Category"].ToString()));
                    }

                    IAttributeDefinition sourceAttribute = assetType.GetAttributeDefinition("Source");
                    asset.SetAttributeValue(sourceAttribute, GetNewListTypeAssetOIDFromDB(sdr["Source"].ToString()));

                    if (String.IsNullOrEmpty(sdr["Status"].ToString()) == false)
                    {
                        //HACK: For Rally import, needs to be refactored.
                        IAttributeDefinition statusAttribute = assetType.GetAttributeDefinition("Status");
                        //asset.SetAttributeValue(statusAttribute, GetNewListTypeAssetOIDFromDB(sdr["Status"].ToString()));
                        asset.SetAttributeValue(statusAttribute, GetNewListTypeAssetOIDFromDB("TaskStatus", sdr["Status"].ToString()));
                    }

                    //HACK: For Rally import, needs to be refactored.
                    IAttributeDefinition parentAttribute = assetType.GetAttributeDefinition("Parent");
                    if (String.IsNullOrEmpty(sdr["ParentType"].ToString()) == true)
                        asset.SetAttributeValue(parentAttribute, GetNewAssetOIDFromDB(sdr["Parent"].ToString()));
                    else
                    {
                        string newAssetOID = null;
                        if (sdr["ParentType"].ToString() == "Story")
                        {
                            newAssetOID = GetNewAssetOIDFromDB(sdr["Parent"].ToString(), "Stories");
                            if (String.IsNullOrEmpty(newAssetOID) == false)
                                asset.SetAttributeValue(parentAttribute, newAssetOID);
                            else
                            {
                                newAssetOID = GetNewAssetOIDFromDB(sdr["Parent"].ToString(), "Epics");
                                if (String.IsNullOrEmpty(newAssetOID) == false)
                                    asset.SetAttributeValue(parentAttribute, newAssetOID);
                                else
                                    throw new Exception("Import failed. Parent could not be found.");
                            }
                        }
                        else
                        {
                            newAssetOID = GetNewAssetOIDFromDB(sdr["Parent"].ToString(), "Defects");
                            if (String.IsNullOrEmpty(newAssetOID) == false)
                                asset.SetAttributeValue(parentAttribute, GetNewAssetOIDFromDB(sdr["Parent"].ToString(), "Defects"));
                            else
                                throw new Exception("Import failed. Parent defect could not be found.");
                        }
                    }

                    _dataAPI.Save(asset);
                    string newAssetNumber = GetAssetNumberV1("Task", asset.Oid.Momentless.ToString());
                    UpdateAssetRecordWithNumber("Tasks", sdr["AssetOID"].ToString(), asset.Oid.Momentless.ToString(), newAssetNumber, ImportStatuses.IMPORTED, "Task imported.");
                    importCount++;
                }
                catch (Exception ex)
                {
                    if (_config.V1Configurations.LogExceptions == true)
                    {
                        string error = ex.Message.Replace("'", ":");
                        UpdateImportStatus("Tasks", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, error);
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

        public int CloseTasks()
        {
            SqlDataReader sdr = GetImportDataFromDBTableForClosing("Tasks");
            int assetCount = 0;

            while (sdr.Read())
            {
                Asset asset = GetAssetFromV1(sdr["NewAssetOID"].ToString());
                ExecuteOperationInV1("Task.Inactivate", asset.Oid);
                assetCount++;
            }
            sdr.Close();
            return assetCount;
        }

    }
}
