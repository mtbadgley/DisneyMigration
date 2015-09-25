using System.Configuration;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NLog.Config;

namespace JiraAttachmentsCore
{
    public class JiraConnectionConfiguration
    {
        public struct JiraConnectionInfo
        {
            public string ServerUrl { get; set; }
            public string UserName { get; set; }
            public string Password { get; set; }
            public string ProjectKey { get; set; }
        }

        public struct FileLocationsInfo
        {
            public string SourceFile { get; set; }
            public string TargetDir { get; set; }
        }

        public struct V1ConnectionInfo
        {
            public string ServerUrl { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string CustomField { get; set; }
            public string UploadImmediately { get; set; }
        }

        public JiraConnectionInfo JiraConnection = new JiraConnectionInfo();
        public FileLocationsInfo FileLocations = new FileLocationsInfo();
        public V1ConnectionInfo V1Connection = new V1ConnectionInfo();
 
        public JiraConnectionConfiguration(XmlNode section)
        {
            XDocument xmlDoc = XDocument.Parse(section.OuterXml);
            
            var jiraConn = from item in xmlDoc.Descendants("jiraConnection")
                           select new JiraConnectionInfo
                               {
                                   ServerUrl = string.IsNullOrEmpty(item.Element("serverURL").Value) ? string.Empty : item.Element("serverURL").Value,
                                   UserName = string.IsNullOrEmpty(item.Element("username").Value) ? string.Empty : item.Element("username").Value,
                                   Password = string.IsNullOrEmpty(item.Element("password").Value) ? string.Empty : item.Element("password").Value,
                                   ProjectKey = string.IsNullOrEmpty(item.Element("projectKey").Value) ? string.Empty : item.Element("projectKey").Value 
                               };
            if (jiraConn.Count() == 0)
            {
                throw new ConfigurationErrorsException("jiraConnection information is incorrect in the JiraAttachments.config file.");
            }
            else
            {
                JiraConnection = jiraConn.First();
            }

            var fileLoc = from item in xmlDoc.Descendants("fileLocations")
                select new FileLocationsInfo
                {
                    SourceFile =
                        string.IsNullOrEmpty(item.Element("sourceFile").Value)
                            ? string.Empty
                            : item.Element("sourceFile").Value,
                    TargetDir =
                        string.IsNullOrEmpty(item.Element("targetDir").Value)
                            ? string.Empty
                            : item.Element("targetDir").Value
                };
            if (fileLoc.Count() == 0)
            {
                throw new ConfigurationErrorsException("fileLocations information is incorrect in the JiraAttachments.config file.");
            }
            else
            {
                FileLocations = fileLoc.First();
            }

            var v1conn = from item in xmlDoc.Descendants("versionOneConnection")
                          select new V1ConnectionInfo()
                          {
                              ServerUrl =
                                  string.IsNullOrEmpty(item.Element("serverUrl").Value)
                                      ? string.Empty
                                      : item.Element("serverUrl").Value,
                              Username = 
                                  string.IsNullOrEmpty(item.Element("username").Value)
                                      ? string.Empty
                                      : item.Element("username").Value,
                              Password = 
                                  string.IsNullOrEmpty(item.Element("password").Value)
                                      ? string.Empty
                                      : item.Element("password").Value,
                              CustomField =
                                  string.IsNullOrEmpty(item.Element("customField").Value)
                                      ? string.Empty
                                      : item.Element("customField").Value,
                              UploadImmediately = string.IsNullOrEmpty(item.Element("uploadImmediately").Value)
                                        ? string.Empty
                                        : item.Element("uploadImmediately").Value
                          };
            if (v1conn.Count() == 0)
            {
                throw new ConfigurationErrorsException("VersionOne Connection information is incorrect in the JiraAttachments.config file.");
            }
            else
            {
                V1Connection = v1conn.First();
            }
        }
    }
}
