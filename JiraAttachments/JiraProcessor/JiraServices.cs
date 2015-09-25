using System;
using System.Collections.ObjectModel;
using System.Configuration;
using Atlassian.Jira;
using NLog;

namespace JiraAttachmentsCore
{
    public class JiraServices
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();
        private static Jira _jiraserver;
        private static string _uploadImmediately;

        public static int DownloadAttachments(string key, string filepath)
        {
            int count = 0;

            ReadOnlyCollection<Attachment> attachments = GetAttachments(key);

            if (attachments != null)
            {
                foreach (Attachment attachment in attachments)
                {
                    string filename = attachment.FileName;
                    string[] s = { filepath, "\\", key, "_", filename };
                    string fullpath = string.Concat(s);
                    try
                    {
                        _logger.Debug("Item: {0}, Downloading: {1}", key, filename);
                        attachment.Download(fullpath);
                        count++;
                        if (_uploadImmediately == "true")
                        {
                            bool uploaded = UploadToVersionOne.UploadAttachment(fullpath, key, filename);
                            if (uploaded)
                            {
                                _logger.Debug("File Uploaded to V1 - {0}", fullpath);
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Error occured will downloading {0}. Exception: {1}", fullpath, ex);
                    }
                }    
            }

            return count;
        }
        
        public static ReadOnlyCollection<Attachment> GetAttachments(string key)
        {
            try
            {
                Issue jiraissue = GetIssue(key);
                ReadOnlyCollection<Attachment> attachments = jiraissue.GetAttachments();
                return attachments;
            }
            catch (Exception ex)
            {
                _logger.Error("GetAttachments failed, check the key value passed = {0}. Exception message: {1}", key, ex.Message);
                return null;
            }
        } 
        
        public static Issue GetIssue(string key)
        {
            try
            {
                Issue jiraissue = _jiraserver.GetIssue(key);
                return jiraissue;
            }
            catch (Exception ex)
            {
                _logger.Error("GetIssue failed, check the key value passed = {0}. Exception message: {1}",key, ex.Message);
                return null;
            }
        }

        public static bool ConnectToJira()
        {
            JiraConnectionConfiguration config;

            try
            {
                config = (JiraConnectionConfiguration)ConfigurationManager.GetSection("jiraAttachments");
                _uploadImmediately = config.V1Connection.UploadImmediately.ToLower();
                _jiraserver = new Jira(config.JiraConnection.ServerUrl, config.JiraConnection.UserName, config.JiraConnection.Password);
                _logger.Info("Connected to Jira Server - {0}",config.JiraConnection.ServerUrl);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to connect to Jira. Exception message: {0}", ex.Message);
                return false;
            }

        }

    }
}
