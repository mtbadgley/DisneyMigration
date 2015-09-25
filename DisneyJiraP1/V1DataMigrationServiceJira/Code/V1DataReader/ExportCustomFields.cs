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
    public class ExportCustomFields : IExportAssets
    {

        private string _InternalAssetType;

        public ExportCustomFields(SqlConnection sqlConn, MetaModel MetaAPI, Services DataAPI, MigrationConfiguration Configurations, string InternalAssetType)
            : base(sqlConn, MetaAPI, DataAPI, Configurations)
        {
            _InternalAssetType = InternalAssetType;
        }

        public override int Export()
        {
            IAssetType assetType = _metaAPI.GetAssetType("AttributeDefinition");
            Query query = new Query(assetType);

            IAttributeDefinition nameAttribute = assetType.GetAttributeDefinition("Name");
            query.Selection.Add(nameAttribute);

            IAttributeDefinition isBasicAttribute = assetType.GetAttributeDefinition("IsBasic");
            query.Selection.Add(isBasicAttribute);

            IAttributeDefinition nativeValueAttribute = assetType.GetAttributeDefinition("NativeValue");
            query.Selection.Add(nativeValueAttribute);

            IAttributeDefinition isCustomAttribute = assetType.GetAttributeDefinition("IsCustom");
            query.Selection.Add(isCustomAttribute);

            IAttributeDefinition isReadOnlyAttribute = assetType.GetAttributeDefinition("IsReadOnly");
            query.Selection.Add(isReadOnlyAttribute);

            IAttributeDefinition isRequiredAttribute = assetType.GetAttributeDefinition("IsRequired");
            query.Selection.Add(isRequiredAttribute);

            IAttributeDefinition attributeTypeAttribute = assetType.GetAttributeDefinition("AttributeType");
            query.Selection.Add(attributeTypeAttribute);

            IAttributeDefinition assetNameAttribute = assetType.GetAttributeDefinition("Asset.Name");
            query.Selection.Add(assetNameAttribute);

            //Filter on asset type and if attribute definition is custom.
            FilterTerm assetName = new FilterTerm(assetNameAttribute);
            assetName.Equal(_InternalAssetType);
            FilterTerm isCustom = new FilterTerm(isCustomAttribute);
            isCustom.Equal("true");
            query.Filter = new AndFilterTerm(assetName, isCustom);

            QueryResult result = _dataAPI.Retrieve(query);
            int customFieldCount = 0;
            foreach (Asset asset in result.Assets)
            {
                string attributeName = GetScalerValue(asset.GetAttribute(nameAttribute)).ToString();
                string attributeType = GetScalerValue(asset.GetAttribute(attributeTypeAttribute)).ToString();
                if (attributeName.StartsWith("Custom_"))
                {
                    customFieldCount += GetCustomFields(attributeName, attributeType);
                }
            }
            return customFieldCount;
        }

        private int GetCustomFields(string attributeName, string attributeType)
        {
            IAssetType assetType = _metaAPI.GetAssetType(_InternalAssetType);
            Query query = new Query(assetType);

            IAttributeDefinition nameAttribute = assetType.GetAttributeDefinition(attributeName);
            query.Selection.Add(nameAttribute);

            //Filter to ensure that we have a value.
            FilterTerm filter = new FilterTerm(nameAttribute);
            filter.NotEqual(String.Empty);
            query.Filter = filter;

            QueryResult result = _dataAPI.Retrieve(query);
            string SQL = BuildCustomFieldInsertStatement();

            foreach (Asset asset in result.Assets)
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = _sqlConn;
                    cmd.CommandText = SQL;
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.Parameters.AddWithValue("@AssetOID", asset.Oid.ToString());
                    cmd.Parameters.AddWithValue("@FieldName", attributeName);
                    cmd.Parameters.AddWithValue("@FieldType", attributeType);

                    if (attributeType == "Relation")
                        cmd.Parameters.AddWithValue("@FieldValue", GetSingleListValue(asset.GetAttribute(nameAttribute)));
                    else
                        cmd.Parameters.AddWithValue("@FieldValue", GetScalerValue(asset.GetAttribute(nameAttribute)));

                    cmd.ExecuteNonQuery();
                }
            }
            return result.Assets.Count;
        }

        private string BuildCustomFieldInsertStatement()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO CUSTOMFIELDS (");
            sb.Append("AssetOID,");
            sb.Append("FieldName,");
            sb.Append("FieldType,");
            sb.Append("FieldValue) ");
            sb.Append("VALUES (");
            sb.Append("@AssetOID,");
            sb.Append("@FieldName,");
            sb.Append("@FieldType,");
            sb.Append("@FieldValue);");
            return sb.ToString();
        }


    }
}
