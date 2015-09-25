using System.Collections.ObjectModel;
using Atlassian.Jira;
using NUnit.Framework;
using JiraAttachmentsCore;

namespace JiraAttachmentsCoreTest
{
    [TestFixture]
    public class ProcessJiraAttachmentsTest
    {
        [Test]
        public void ConnectToJiraTest()
        {
            bool connecttojira = JiraServices.ConnectToJira();
            Assert.IsTrue(connecttojira);
        }

        [Test]
        public void GetIssueTest()
        {
            string issuekey = "CC-1";
            Issue jiraissue;
            bool connecttojira = JiraServices.ConnectToJira();
            Assert.IsTrue(connecttojira);
            jiraissue = JiraServices.GetIssue(issuekey);
            Assert.AreEqual(issuekey, jiraissue.Key.ToString());
        }

        [Test]
        public void GetAttachmentsTest()
        {
            string issuekey = "CC-1";
            ReadOnlyCollection<Attachment> attachments = JiraServices.GetAttachments(issuekey);
            Assert.GreaterOrEqual(attachments.Count, 1);
        }

        [Test]
        public void DownloadAttachmentsTest()
        {
            string issuekey = "CC-1";
            int results = JiraServices.DownloadAttachments(issuekey,"C:\\attachments");
            Assert.GreaterOrEqual(results,1);
        }

        [Test]
        public void ProcessJiraXMLTests()
        {
            string filename = "C:\\Files\\CC.xml";
            string filepath = "C:\\attachments";
            int itemsprocesed = ProcessJiraXML.ReadFile(filename, filepath);
            Assert.GreaterOrEqual(itemsprocesed,3);
        }

        [Test]
        public void GetAssetOidTest()
        {
            string assetoid = UploadToVersionOne.GetAssetOid("CPMTWO-1", "PrimaryWorkitem");
            Assert.AreEqual("Story:1051", assetoid);
        }

        [Test]
        public void GetV1AssetIDTest()
        {
            string result = UploadToVersionOne.GetV1IdCustomFieldName("PrimaryWorkitem");
            Assert.AreEqual("Custom_JiraID",result);

        }

        [Test]
        public void UploadAttachmentTest()
        {
            bool result = UploadToVersionOne.UploadAttachment(@"C:\vhattachments\CPMTWO-1_Notes_New.jpg", "CPMTWO-1");
            Assert.IsTrue(result);
        }

    }
}
