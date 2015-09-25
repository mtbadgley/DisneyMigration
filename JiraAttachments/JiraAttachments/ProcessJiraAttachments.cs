using JiraAttachmentsCore;
using NLog;
using System.Configuration;

namespace JiraAttachments
{
    class ProcessJiraAttachments
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        static void Main()
        {
            _logger.Info("*******************************");
            _logger.Info("***** PROCESSING STARTED ******");

            JiraServices.ConnectToJira();

            JiraConnectionConfiguration config;
            config = (JiraConnectionConfiguration)ConfigurationManager.GetSection("jiraAttachments");

            int count = ProcessJiraXML.ReadFile(config.FileLocations.SourceFile, config.FileLocations.TargetDir);

            _logger.Info("Total Items Processed: {0}", count);
            _logger.Info("***** PROCESSING COMPLETE *****");
        }

    }
}
