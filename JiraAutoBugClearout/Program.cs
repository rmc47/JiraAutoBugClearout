using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JiraAutoBugClearout
{
    class Program
    {
        static void Main(string[] args)
        {
            SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();
            csb.InitialCatalog = "JiraDB";
            csb.DataSource = ".\\sql2008";
            csb.IntegratedSecurity = true;
            
            using (SqlConnection conn = new SqlConnection(csb.ConnectionString))
            {
                conn.Open();

                BugClearer clearer = new BugClearer();
                clearer.Connection = conn;

                clearer.DeleteReportCountChanges();
                clearer.StripDownBloatyComments();
            }
        }
    }
}
