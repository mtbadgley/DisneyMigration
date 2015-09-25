using System;
using System.Configuration;
using System.IO;
using NLog;
using VersionOne.SDK.APIClient;

namespace JiraAttachmentsCore
{
    public class UploadToVersionOne
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public static bool UploadAttachment(string filename, string jiraId, string rawfile)
        {
            var config = (JiraConnectionConfiguration)ConfigurationManager.GetSection("jiraAttachments");

            var metaconnector = new VersionOneAPIConnector(config.V1Connection.ServerUrl + "/meta.v1/");
            var dataconnector =
                new VersionOneAPIConnector(config.V1Connection.ServerUrl + "/rest-1.v1/")
                    .WithVersionOneUsernameAndPassword(config.V1Connection.Username, config.V1Connection.Password);
            var attachmentconnector =
                                new VersionOneAPIConnector(config.V1Connection.ServerUrl + "/attachment.img/")
                    .WithVersionOneUsernameAndPassword(config.V1Connection.Username, config.V1Connection.Password);

            MetaModel metaModel = new MetaModel(metaconnector);
            Services services = new Services(metaModel, dataconnector);
            Attachments attachments = new Attachments(attachmentconnector);

            string mimeType = MimeType.Resolve(filename);

            string assetoid = GetAssetOid(jiraId, "PrimaryWorkitem");
            if (String.IsNullOrEmpty(assetoid))
            {
                assetoid = GetAssetOid(jiraId, "Task");
            }
            if (String.IsNullOrEmpty(assetoid)) return false;

            Oid attachmentContext = Oid.FromToken(assetoid, metaModel);

            IAssetType attachmentType = metaModel.GetAssetType("Attachment");
            IAttributeDefinition attachmentNameDef = attachmentType.GetAttributeDefinition("Name");
            IAttributeDefinition attachmentContentDef = attachmentType.GetAttributeDefinition("Content");
            IAttributeDefinition attachmentContentTypeDef = attachmentType.GetAttributeDefinition("ContentType");
            IAttributeDefinition attachmentFileNameDef = attachmentType.GetAttributeDefinition("Filename");

            Asset newAttachment = services.New(attachmentType, attachmentContext);
            newAttachment.SetAttributeValue(attachmentNameDef, "Imported from Jira");
            newAttachment.SetAttributeValue(attachmentContentDef, string.Empty);
            newAttachment.SetAttributeValue(attachmentContentTypeDef, mimeType);
            newAttachment.SetAttributeValue(attachmentFileNameDef, rawfile);
            services.Save(newAttachment);

            //Setup and attach the payload
            string attachmentKey = newAttachment.Oid.Key.ToString();
            int buffersize = 4096;

            using (FileStream input = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                using (Stream output = attachments.GetWriteStream(attachmentKey))
                {
                    byte[] buffer = new byte[buffersize];
                    for (; ; )
                    {
                        int read = input.Read(buffer, 0, buffersize);
                        if (read == 0)
                            break;
                        output.Write(buffer, 0, read);
                    }
                }
            }
            attachments.SetWriteStream(attachmentKey, mimeType);
            return true;
        }

        
        public static string GetAssetOid(string jiraId, string assetTypeStr)
        {
            var config = (JiraConnectionConfiguration)ConfigurationManager.GetSection("jiraAttachments");
            
            var metaconnector = new VersionOneAPIConnector(config.V1Connection.ServerUrl + "/meta.v1/");
            var dataconnector =
                new VersionOneAPIConnector(config.V1Connection.ServerUrl + "/rest-1.v1/")
                    .WithVersionOneUsernameAndPassword(config.V1Connection.Username, config.V1Connection.Password);

            MetaModel metaModel = new MetaModel(metaconnector);
            Services services = new Services(metaModel, dataconnector);

            var assetType = metaModel.GetAssetType(assetTypeStr);
            var query = new Query(assetType);
            var jiraIdAttribute = assetType.GetAttributeDefinition(GetV1IdCustomFieldName(assetTypeStr));
            query.Selection.Add(jiraIdAttribute);
            var jiraIdTerm = new FilterTerm(jiraIdAttribute);
            jiraIdTerm.Equal(jiraId);
            query.Filter = jiraIdTerm;

            var result = services.Retrieve(query);

            if (result.Assets.Count == 0)
            {
                return String.Empty;
            }
            return result.Assets[0].Oid.ToString();
        }

        public static string GetV1IdCustomFieldName(string internalAssetTypeName)
        {
            var config = (JiraConnectionConfiguration)ConfigurationManager.GetSection("jiraAttachments");
            
            if (!String.IsNullOrEmpty(config.V1Connection.CustomField))
            {
                string customFieldName = String.Empty;

                var metaconnector = new VersionOneAPIConnector(config.V1Connection.ServerUrl + "/meta.v1/");
                var dataconnector =
                    new VersionOneAPIConnector(config.V1Connection.ServerUrl + "/rest-1.v1/")
                        .WithVersionOneUsernameAndPassword(config.V1Connection.Username, config.V1Connection.Password);

                MetaModel metaApi = new MetaModel(metaconnector);
                Services dataApi = new Services(metaApi,dataconnector);

                IAssetType assetType = metaApi.GetAssetType("AttributeDefinition");
                Query query = new Query(assetType);

                IAttributeDefinition nameAttribute = assetType.GetAttributeDefinition("Name");
                query.Selection.Add(nameAttribute);

                IAttributeDefinition isCustomAttribute = assetType.GetAttributeDefinition("IsCustom");
                query.Selection.Add(isCustomAttribute);

                IAttributeDefinition assetNameAttribute = assetType.GetAttributeDefinition("Asset.Name");
                query.Selection.Add(assetNameAttribute);

                FilterTerm assetName = new FilterTerm(assetNameAttribute);
                assetName.Equal(internalAssetTypeName);
                FilterTerm isCustom = new FilterTerm(isCustomAttribute);
                isCustom.Equal("true");
                query.Filter = new AndFilterTerm(assetName, isCustom);

                QueryResult result = dataApi.Retrieve(query);

                foreach (Asset asset in result.Assets)
                {
                    string attributeValue = asset.GetAttribute(nameAttribute).Value.ToString();
                    if (attributeValue.StartsWith(config.V1Connection.CustomField))
                    {
                        customFieldName = attributeValue;
                        break;
                    }
                }
                return customFieldName;
            }
            return null;
        }
    }
}
