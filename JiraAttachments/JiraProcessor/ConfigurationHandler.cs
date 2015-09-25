using System.Configuration;
using System.Xml;

namespace JiraAttachmentsCore
{
    public class ConfigurationHandler : IConfigurationSectionHandler
    {
        public object Create(object parent, object configContext, XmlNode section)
        {
            return new JiraConnectionConfiguration(section);
        }
    }
}
