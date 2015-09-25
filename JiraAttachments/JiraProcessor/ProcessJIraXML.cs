using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using NLog;

namespace JiraAttachmentsCore
{
    public class ProcessJiraXML
    {

        private static Logger _logger = LogManager.GetCurrentClassLogger();
        
        public static int ReadFile(string filename, string filepath)
        {
            int count = 0;
            int attachmentcount = 0;

            XDocument xmlDoc = XDocument.Load(filename);
            var items = from item in xmlDoc.XPathSelectElements("rss/channel/item") select item;

            foreach (var item in items)
            {
                var xElement = item.Element("key");
                if (xElement != null)
                {
                    string key = xElement.Value;
                    if (string.IsNullOrEmpty(key) == false)
                    {
                        attachmentcount = attachmentcount + JiraServices.DownloadAttachments(key, filepath);
                        count++;
                    }
                }
            }

            _logger.Info("Total Attachments Processed: {0}", attachmentcount);
            return count;
        }

    }
}
