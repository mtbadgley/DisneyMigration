using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using VersionOne.SDK.APIClient;
using V1DataCore;

namespace V1DataReader
{
    public class ExportMembers : IExportAssets
    {
        public ExportMembers(SqlConnection sqlConn, MetaModel MetaAPI, Services DataAPI, MigrationConfiguration Configurations)
            : base(sqlConn, MetaAPI, DataAPI, Configurations) { }

        public override int Export()
        {
            IAssetType assetType = _metaAPI.GetAssetType("Member");
            Query query = new Query(assetType);

            IAttributeDefinition assetStateAttribute = assetType.GetAttributeDefinition("AssetState");
            query.Selection.Add(assetStateAttribute);

            IAttributeDefinition emailAttribute = assetType.GetAttributeDefinition("Email");
            query.Selection.Add(emailAttribute);

            IAttributeDefinition nicknameAttribute = assetType.GetAttributeDefinition("Nickname");
            query.Selection.Add(nicknameAttribute);

            IAttributeDefinition descriptionAttribute = assetType.GetAttributeDefinition("Description");
            query.Selection.Add(descriptionAttribute);

            IAttributeDefinition nameAttribute = assetType.GetAttributeDefinition("Name");
            query.Selection.Add(nameAttribute);

            IAttributeDefinition phoneAttribute = assetType.GetAttributeDefinition("Phone");
            query.Selection.Add(phoneAttribute);

            IAttributeDefinition defaultRoleAttribute = assetType.GetAttributeDefinition("DefaultRole");
            query.Selection.Add(defaultRoleAttribute);

            IAttributeDefinition usernameAttribute = assetType.GetAttributeDefinition("Username");
            query.Selection.Add(usernameAttribute);

            IAttributeDefinition memberLabelsAttribute = assetType.GetAttributeDefinition("MemberLabels.ID");
            query.Selection.Add(memberLabelsAttribute);

            IAttributeDefinition notifyViaEmailAttribute = assetType.GetAttributeDefinition("NotifyViaEmail");
            query.Selection.Add(notifyViaEmailAttribute);

            IAttributeDefinition sendConversationEmailsAttribute = assetType.GetAttributeDefinition("SendConversationEmails");
            query.Selection.Add(sendConversationEmailsAttribute);

            string SQL = BuildMemberInsertStatement();

            if (_config.V1Configurations.PageSize != 0)
            {
                query.Paging.Start = 0;
                query.Paging.PageSize = _config.V1Configurations.PageSize;
            }

            int assetCounter = 0;
            int assetTotal = 0;

            do
            {
                QueryResult result = _dataAPI.Retrieve(query);
                assetTotal = result.TotalAvaliable;

                foreach (Asset asset in result.Assets)
                {
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        //NAME NPI MASK:
                        object name = GetScalerValue(asset.GetAttribute(nameAttribute));
                        if (_config.V1Configurations.UseNPIMasking == true && name != DBNull.Value)
                        {
                            name = ExportUtils.RemoveNPI(name.ToString());
                        }

                        //DESCRIPTION NPI MASK:
                        object description = GetScalerValue(asset.GetAttribute(descriptionAttribute));
                        if (_config.V1Configurations.UseNPIMasking == true && description != DBNull.Value)
                        {
                            description = ExportUtils.RemoveNPI(description.ToString());
                        }

                        cmd.Connection = _sqlConn;
                        cmd.CommandText = SQL;
                        cmd.CommandType = System.Data.CommandType.Text;
                        cmd.Parameters.AddWithValue("@AssetOID", asset.Oid.ToString());
                        cmd.Parameters.AddWithValue("@AssetState", GetScalerValue(asset.GetAttribute(assetStateAttribute)));
                        cmd.Parameters.AddWithValue("@Email", GetScalerValue(asset.GetAttribute(emailAttribute)));
                        cmd.Parameters.AddWithValue("@Nickname", GetScalerValue(asset.GetAttribute(nicknameAttribute)));
                        cmd.Parameters.AddWithValue("@Description", description);
                        cmd.Parameters.AddWithValue("@Name", name);
                        cmd.Parameters.AddWithValue("@Phone", GetScalerValue(asset.GetAttribute(phoneAttribute)));
                        cmd.Parameters.AddWithValue("@DefaultRole", GetSingleRelationValue(asset.GetAttribute(defaultRoleAttribute)));
                        cmd.Parameters.AddWithValue("@Username", GetScalerValue(asset.GetAttribute(usernameAttribute)));
                        cmd.Parameters.AddWithValue("@MemberLabels", GetMultiRelationValues(asset.GetAttribute(memberLabelsAttribute)));
                        cmd.Parameters.AddWithValue("@NotifyViaEmail", GetScalerValue(asset.GetAttribute(notifyViaEmailAttribute)));
                        cmd.Parameters.AddWithValue("@SendConversationEmails", GetScalerValue(asset.GetAttribute(sendConversationEmailsAttribute)));
                        cmd.ExecuteNonQuery();
                    }
                    assetCounter++;
                }
                query.Paging.Start = assetCounter;
            } while (assetCounter != assetTotal);
            return assetCounter;
        }

        private string BuildMemberInsertStatement()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO MEMBERS (");
            sb.Append("AssetOID,");
            sb.Append("AssetState,");
            sb.Append("Email,");
            sb.Append("Nickname,");
            sb.Append("Description,");
            sb.Append("Name,");
            sb.Append("Phone,");
            sb.Append("DefaultRole,");
            sb.Append("Username,");
            sb.Append("MemberLabels,");
            sb.Append("NotifyViaEmail,");
            sb.Append("SendConversationEmails) ");
            sb.Append("VALUES (");
            sb.Append("@AssetOID,");
            sb.Append("@AssetState,");
            sb.Append("@Email,");
            sb.Append("@Nickname,");
            sb.Append("@Description,");
            sb.Append("@Name,");
            sb.Append("@Phone,");
            sb.Append("@DefaultRole,");
            sb.Append("@Username,");
            sb.Append("@MemberLabels,");
            sb.Append("@NotifyViaEmail,");
            sb.Append("@SendConversationEmails);");
            return sb.ToString();
        }

    }
}
