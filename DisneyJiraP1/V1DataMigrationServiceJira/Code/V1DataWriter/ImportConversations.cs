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
    public class ImportConversations : IImportAssets
    {
        public ImportConversations(SqlConnection sqlConn, MetaModel MetaAPI, Services DataAPI, MigrationConfiguration Configurations) 
            : base(sqlConn, MetaAPI, DataAPI, Configurations) { }

        public override int Import()
        {
            //SqlDataReader sdr = GetImportDataFromSproc("spGetConversationsForImport");
            SqlDataReader sdr = GetImportDataFromDBTable("Conversations");

            int importCount = 0;
                       
            while (sdr.Read())
            {
                try
                {
                    //CHECK DATA: Conversation must have an author.
                    if (_config.V1Configurations.MigrateUnauthoredConversationsAsAdmin == false && String.IsNullOrEmpty(sdr["Author"].ToString()))
                    {
                        UpdateImportStatus("Conversations", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, "Conversation author attribute is required.");
                        continue;
                    }

                    IAssetType assetType = _metaAPI.GetAssetType("Expression");
                    Asset asset = _dataAPI.New(assetType, null);

                    IAttributeDefinition authoredAtAttribute = assetType.GetAttributeDefinition("AuthoredAt");
                    asset.SetAttributeValue(authoredAtAttribute, sdr["AuthoredAt"].ToString());

                    IAttributeDefinition contentAttribute = assetType.GetAttributeDefinition("Content");
                    asset.SetAttributeValue(contentAttribute, sdr["Content"].ToString());

                    string memberOid = string.Empty;
                    if (_config.V1Configurations.MigrateUnauthoredConversationsAsAdmin == true &&
                        String.IsNullOrEmpty(sdr["Author"].ToString()))
                        memberOid = "Member:20";
                    else
                        memberOid = GetNewAssetOIDFromDB(sdr["Author"].ToString(), "Members");

                    IAttributeDefinition authorAttribute = assetType.GetAttributeDefinition("Author");
                    asset.SetAttributeValue(authorAttribute, memberOid);

                    //NOTE: Only works with 13.2 or greater of VersionOne, should be able to remove for earlier versions
                    string belongsTo = string.Empty;
                    IAttributeDefinition belongsToAttribute = assetType.GetAttributeDefinition("BelongsTo");
                    asset.SetAttributeValue(belongsToAttribute, GetConversationBelongsTo(memberOid));

                    //NOTE: Need to switch on V1 version. Right now handles 11.3 to 11.4+. Used to be BaseAssets, is now Mentions.
                    //HACK: Modified to support Rally migration, needs refactoring.
                    if (String.IsNullOrEmpty(sdr["Mentions"].ToString()) == false)
                    {
                        //AddMultiValueRelation(assetType, asset, "Mentions", sdr["BaseAssets"].ToString());
                        AddRallyMentionsValue(assetType, asset, sdr["BaseAssetType"].ToString(), sdr["Mentions"].ToString());
                    }

                    _dataAPI.Save(asset);

                    UpdateNewAssetOIDInDB("Conversations", sdr["AssetOID"].ToString(), asset.Oid.Momentless.ToString());
                    UpdateImportStatus("Conversations", sdr["AssetOID"].ToString(), ImportStatuses.IMPORTED, "Conversation imported.");
                    importCount++;
                }
                catch (Exception ex)
                {
                    if (_config.V1Configurations.LogExceptions == true)
                    {
                        UpdateImportStatus("Conversations", sdr["AssetOID"].ToString(), ImportStatuses.FAILED, ex.Message);
                        continue;
                    }
                    else
                    {
                        throw ex;
                    }
                }
            }
            sdr.Close();
            SetConversationDependencies();
            return importCount;
        }

        //NOTE: Conversations sproc currently uses BaseAsset, needs refactoring to use Mentions for later versions of V1.
        private void SetConversationDependencies()
        {
            SqlDataReader sdr = GetImportDataFromSproc("spGetConversationsForImport");
            //SqlDataReader sdr = GetImportDataFromDBTable("Conversations");

            while (sdr.Read())
            {
                if (sdr["ImportStatus"].ToString() == ImportStatuses.FAILED.ToString()) continue;

                bool canSave = false;

                IAssetType assetType = _metaAPI.GetAssetType("Expression");
                Asset asset = GetAssetFromV1(sdr["NewAssetOID"].ToString());

                //string conversation = GetNewAssetOIDFromDB(sdr["Conversation"].ToString());
                //if (String.IsNullOrEmpty(conversation) == false)
                //{
                //    IAttributeDefinition conversationAttribute = assetType.GetAttributeDefinition("Conversation");
                //    asset.SetAttributeValue(conversationAttribute, conversation);
                //    canSave = true;
                //}

                string inReplyTo = GetNewAssetOIDFromDB(sdr["InReplyTo"].ToString(), "Conversations");
                if (String.IsNullOrEmpty(inReplyTo) == false)
                {
                    IAttributeDefinition inReplyToAttribute = assetType.GetAttributeDefinition("InReplyTo");
                    asset.SetAttributeValue(inReplyToAttribute, inReplyTo);
                    canSave = true;
                }
                if (canSave == true) _dataAPI.Save(asset);
            }
            sdr.Close();
        }

        private string GetConversationBelongsTo(string MemberOid)
        {
            IAssetType assetType = _metaAPI.GetAssetType("Conversation");
            Asset asset = _dataAPI.New(assetType, null);

            IAttributeDefinition authoredAtAttribute = assetType.GetAttributeDefinition("Participants");
            asset.AddAttributeValue(authoredAtAttribute, MemberOid);

            _dataAPI.Save(asset);

            return asset.Oid.Momentless.ToString();
        }
    }
}
