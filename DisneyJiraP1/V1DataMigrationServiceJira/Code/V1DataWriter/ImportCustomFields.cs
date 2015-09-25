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
    public class ImportCustomFields : IImportAssets
    {
        private string _assetType;

        public ImportCustomFields(SqlConnection sqlConn, MetaModel MetaAPI, Services DataAPI, MigrationConfiguration Configurations, string AssetType)
            : base(sqlConn, MetaAPI, DataAPI, Configurations) 
        {
            _assetType = AssetType;
        }

        public override int Import()
        {
            MigrationConfiguration.AssetInfo assetInfo = _config.AssetsToMigrate.Find(i => i.Name == _assetType);
            List<MigrationConfiguration.CustomFieldInfo> fields = _config.CustomFieldsToMigrate.FindAll(i => i.AssetType == assetInfo.InternalName);

            int importCount = 0;
            foreach (MigrationConfiguration.CustomFieldInfo field in fields)
            {
                SqlDataReader sdr = GetImportDataFromDBTableForCustomFields(_assetType, field.SourceName);
                while (sdr.Read())
                {
                    //Get the asset from V1.
                    IAssetType assetType = _metaAPI.GetAssetType(assetInfo.InternalName);
                    Asset asset = GetAssetFromV1(sdr["NewAssetOID"].ToString());

                    //Set the custom field value and save it.
                    IAttributeDefinition customFieldAttribute = assetType.GetAttributeDefinition(field.TargetName);
                    if (field.DataType == "Relation")
                    {
                        string listTypeOID = GetCustomListTypeAssetOIDFromV1(field.RelationName, sdr["FieldValue"].ToString());
                        if (String.IsNullOrEmpty(listTypeOID) == false)
                        {
                            asset.SetAttributeValue(customFieldAttribute, listTypeOID);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        asset.SetAttributeValue(customFieldAttribute, sdr["FieldValue"].ToString());
                    }
                    _dataAPI.Save(asset);
                    importCount++;
                }
                sdr.Close();
            }
            return importCount;
        }

    }
}
