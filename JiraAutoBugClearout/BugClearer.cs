using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JiraAutoBugClearout
{
    internal sealed class BugClearer
    {
        public SqlConnection Connection { get; set; }
        public int BloatCommentThreshold { get; set; }
        public string BloatAuthor {get;set;}

        public BugClearer()
        {
            BloatCommentThreshold = 500;
            BloatAuthor = "smartassembly";
        }

        public void DeleteReportCountChanges()
        {
            // Firstly, nuke any audit trail for the Report Count field
            Console.WriteLine("Deleting Report Count field change audit history...");
            using (SqlCommand cmd = Connection.CreateCommand())
            {
                cmd.CommandTimeout = 0;
                cmd.CommandText = "DELETE FROM changeitem WHERE FIELD='Report Count';";
                cmd.ExecuteNonQuery();
            }

            // Now, nuke any change history groups that no longer have any changes associated with them
            Console.WriteLine("\tTidying up audit history orphaned groups...");
            using (SqlCommand cmd = Connection.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM changegroup WHERE id NOT IN (SELECT groupid FROM changeitem);";
                cmd.ExecuteNonQuery();
            }
            Console.WriteLine("\t...done");
        }

        public void StripDownBloatyComments()
        {
            foreach (int issueID in GetBloatyIssues())
            {
                DeleteBloatyComments(issueID);
            }
        }

        private List<int> GetBloatyIssues()
        {
            Console.WriteLine("Getting list of bloaty issues (more than {0} comments by {1}", BloatCommentThreshold, BloatAuthor);
            using (SqlCommand cmd = Connection.CreateCommand())
            {
                cmd.CommandTimeout = 0;
                cmd.CommandText = "SELECT issueid FROM jiraaction WHERE author=@author AND actiontype='comment' GROUP BY issueid HAVING COUNT(*) > @bloatcommentthreshold ORDER BY COUNT(*) DESC;";
                cmd.Parameters.AddWithValue("@author", BloatAuthor);
                cmd.Parameters.AddWithValue("@bloatcommentthreshold", BloatCommentThreshold);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    List<int> issueIds = new List<int>();
                    while (reader.Read())
                    {
                        issueIds.Add(Convert.ToInt32(reader["issueid"]));
                    }
                    Console.WriteLine("\t{0} issues found", issueIds.Count);
                    return issueIds;
                }
            }
        }

        private void DeleteBloatyComments(int issueID)
        {
            Console.WriteLine("Deleting bloaty comments for issue {0}", issueID);
            // How many comments are there to start with?
            int initialCommentCount = GetBloatyCommentCount(issueID);
            Console.WriteLine("\tInitial count: {0}", initialCommentCount);

            // Just in cases...
            if (initialCommentCount <= BloatCommentThreshold)
                return;

            // Firstly, delete comments that have neither an email address or user-entered content
            Console.WriteLine("\tDeleting comments with no user content or email address...");
            using (SqlCommand cmd = Connection.CreateCommand())
            {
                cmd.CommandTimeout = 0;
                int commentsToDelete = initialCommentCount - BloatCommentThreshold;
                cmd.CommandText = @"DELETE FROM jiraaction WHERE id IN (SELECT TOP (@commentsToDelete) id FROM jiraaction WHERE
                    issueid=@issueid AND
                    author=@bloatauthor AND
                    actionbody NOT LIKE '%Email%' AND
                    actionbody NOT LIKE '%UserDescription%'
                    ORDER BY id
                    )";
                cmd.Parameters.AddWithValue("@commentsToDelete", commentsToDelete);
                cmd.Parameters.AddWithValue("@issueid", issueID);
                cmd.Parameters.AddWithValue("@bloatauthor", BloatAuthor);
                cmd.ExecuteNonQuery();
            }
            int firstPassCommentsRemaining = GetBloatyCommentCount(issueID);
            Console.WriteLine("\t...done, leaving {0} comments", firstPassCommentsRemaining);

            if (firstPassCommentsRemaining > BloatCommentThreshold)
            {
                // Need to do a second pass, losing anything which has too many comments with email addresses.
                Console.WriteLine("\tToo many remaining. 2nd pass losing those with email but no comment...");
                using (SqlCommand cmd = Connection.CreateCommand())
                {
                    cmd.CommandTimeout = 0;
                    int commentsToDelete = firstPassCommentsRemaining - BloatCommentThreshold;
                    cmd.CommandText = @"DELETE TOP (" + commentsToDelete + @") FROM jiraaction WHERE id IN (SELECT TOP (@commentsToDelete) id FROM jiraaction WHERE
                    issueid=@issueid AND
                    author=@bloatauthor AND
                    actionbody NOT LIKE '%UserDescription%'
                    ORDER BY id
                    )";
                    cmd.Parameters.AddWithValue("@commentsToDelete", commentsToDelete);
                    cmd.Parameters.AddWithValue("@issueid", issueID);
                    cmd.Parameters.AddWithValue("@bloatauthor", BloatAuthor);
                    cmd.ExecuteNonQuery();
                }
                int secondPassCommentsRemaining = GetBloatyCommentCount(issueID);
                Console.WriteLine("\t...done, leaving {0} comments", secondPassCommentsRemaining);
            }
        }

        private int GetBloatyCommentCount(int issueID)
        {
            using (SqlCommand cmd = Connection.CreateCommand())
            {
                cmd.CommandTimeout = 0;
                cmd.CommandText = "SELECT COUNT(*) FROM jiraaction WHERE issueid=@issueid AND author=@author AND actiontype='comment';";
                cmd.Parameters.AddWithValue("@issueid", issueID);
                cmd.Parameters.AddWithValue("@author", BloatAuthor);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }
    }
}
