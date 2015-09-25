using System;
using System.Data.SqlClient;
using System.Text;
using V1DataCore;

namespace JiraReaderService
{
    public class ExportListTypes : IExportAssets
    {
        public ExportListTypes(SqlConnection sqlConn, MigrationConfiguration Configurations) : base(sqlConn, Configurations) { }

        private int listTypeCount = 0;

        public override int Export()
        {
            foreach (var listValue in _config.ListValues)
            {
                if (!String.IsNullOrEmpty(listValue.NewValue))
                {
                    InsertListType(listValue.ListName, listValue.NewValue);
                }
            }
            
            return listTypeCount;
        }

        private void InsertListType(string ListTypeName, string ListTypeValue)
        {
            string SQL = BuildListTypeInsertStatement();

            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = _sqlConn;
                cmd.CommandText = SQL;
                cmd.CommandType = System.Data.CommandType.Text;
                cmd.Parameters.AddWithValue("@AssetOID", ListTypeName + ":" + ListTypeValue.Replace(" ", "").Replace("'", ""));
                cmd.Parameters.AddWithValue("@AssetType", ListTypeName);
                cmd.Parameters.AddWithValue("@AssetState", "Active");
                cmd.Parameters.AddWithValue("@Description", "Imported from Jira on " + DateTime.Now.ToString() + ".");
                cmd.Parameters.AddWithValue("@Name", ListTypeValue);
                cmd.ExecuteNonQuery();
            }
            listTypeCount++;
        }

        private string BuildListTypeInsertStatement()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO LISTTYPES (");
            sb.Append("AssetOID,");
            sb.Append("AssetType,");
            sb.Append("AssetState,");
            sb.Append("Description,");
            sb.Append("Name) ");
            sb.Append("VALUES (");
            sb.Append("@AssetOID,");
            sb.Append("@AssetType,");
            sb.Append("@AssetState,");
            sb.Append("@Description,");
            sb.Append("@Name);");
            return sb.ToString();
        }
    }
}
