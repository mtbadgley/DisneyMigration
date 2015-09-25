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
    public class ImportMembers : IImportAssets
    {

        public ImportMembers(SqlConnection sqlConn, MetaModel MetaAPI, Services DataAPI, MigrationConfiguration Configurations)
            : base(sqlConn, MetaAPI, DataAPI, Configurations) { }

        public override int Import()
        {
            MigrationConfiguration.AssetInfo assetInfo = _config.AssetsToMigrate.Find(i => i.Name == "Members");
            SqlDataReader sdr = GetImportDataFromDBTable("Members");

            int importCount = 0;
            while (sdr.Read())
            {
                try
                {
                    //SPECIAL CASE: Admin member will not be imported.
                    if (sdr["AssetOID"].ToString() == "Member:20")
                    {
                        UpdateNewAssetOIDAndStatus("Members", sdr["AssetOID"].ToString(), sdr["AssetOID"].ToString(), ImportStatuses.SKIPPED, "Admin member (Member:20) cannot be imported.");
                        continue;
                    }

                    //SPECIAL CASE: Member with no username will not be imported.
                    if (String.IsNullOrEmpty(sdr["Username"].ToString()) == true)
                    {
                        UpdateImportStatus("Members", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, "Member with no username cannot be imported.");
                        continue;
                    }

                    //DUPLICATE CHECK: Check for duplicates if enabled.
                    if (String.IsNullOrEmpty(assetInfo.DuplicateCheckField) == false)
                    {
                        //Ensure that we have a value to check, if not, will attempt to create the member.
                        if (String.IsNullOrEmpty(sdr[assetInfo.DuplicateCheckField].ToString()) == false)
                        {
                            string currentAssetOID = CheckForDuplicateInV1WithFind(assetInfo.InternalName, assetInfo.DuplicateCheckField, sdr[assetInfo.DuplicateCheckField].ToString());
                            if (string.IsNullOrEmpty(currentAssetOID) == false)
                            {
                                UpdateNewAssetOIDAndStatus("Members", sdr["AssetOID"].ToString(), currentAssetOID, ImportStatuses.SKIPPED, "Duplicate member.");
                                continue;
                            }
                        }
                    }

                    IAssetType assetType = _metaAPI.GetAssetType("Member");
                    Asset asset = _dataAPI.New(assetType, null);

                    IAttributeDefinition fullNameAttribute = assetType.GetAttributeDefinition("Name");
                    asset.SetAttributeValue(fullNameAttribute, sdr["Name"].ToString());

                    IAttributeDefinition userNameAttribute = assetType.GetAttributeDefinition("Username");
                    asset.SetAttributeValue(userNameAttribute, sdr["Username"].ToString());

                    IAttributeDefinition passwordAttribute = assetType.GetAttributeDefinition("Password");
                    asset.SetAttributeValue(passwordAttribute, sdr["Password"].ToString());

                    IAttributeDefinition nickNameAttribute = assetType.GetAttributeDefinition("Nickname");
                    asset.SetAttributeValue(nickNameAttribute, sdr["Nickname"].ToString());

                    IAttributeDefinition emailAttribute = assetType.GetAttributeDefinition("Email");
                    asset.SetAttributeValue(emailAttribute, sdr["Email"].ToString());

                    IAttributeDefinition phoneAttribute = assetType.GetAttributeDefinition("Phone");
                    asset.SetAttributeValue(phoneAttribute, sdr["Phone"].ToString());

                    IAttributeDefinition defaultRoleAttribute = assetType.GetAttributeDefinition("DefaultRole");
                    asset.SetAttributeValue(defaultRoleAttribute, sdr["DefaultRole"].ToString());

                    IAttributeDefinition descAttribute = assetType.GetAttributeDefinition("Description");
                    asset.SetAttributeValue(descAttribute, sdr["Description"].ToString());

                    IAttributeDefinition notifyViaEmailAttribute = assetType.GetAttributeDefinition("NotifyViaEmail");
                    asset.SetAttributeValue(notifyViaEmailAttribute, sdr["NotifyViaEmail"].ToString());

                    IAttributeDefinition sendConversationEmailsAttribute = assetType.GetAttributeDefinition("SendConversationEmails");
                    asset.SetAttributeValue(sendConversationEmailsAttribute, sdr["SendConversationEmails"].ToString());

                    _dataAPI.Save(asset);
                    UpdateNewAssetOIDAndStatus("Members", sdr["AssetOID"].ToString(), asset.Oid.Momentless.ToString(), ImportStatuses.IMPORTED, "Member imported.");
                    importCount++;
                }
                catch (Exception ex)
                {
                    if (_config.V1Configurations.LogExceptions == true)
                    {
                        UpdateImportStatus("Members", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, ex.Message);
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

        public int CloseMembers()
        {
            SqlDataReader sdr = GetImportDataFromDBTableForClosingNoSkipped("Members");
            int assetCount = 0;
            while (sdr.Read())
            {
                Asset asset = GetAssetFromV1(sdr["NewAssetOID"].ToString());
                ExecuteOperationInV1("Member.Inactivate", asset.Oid);
                assetCount++;
            }
            sdr.Close();
            return assetCount;
        }

    }
}
