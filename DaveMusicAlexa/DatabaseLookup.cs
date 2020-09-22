using System;
using System.Data.SqlClient;
using System.Text;

namespace DaveMusicAlexa
{
    class DatabaseLookup
    {
        public string GetIPAddress(string userID)
        {
            try
            {
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
                builder.DataSource = "alexamusic.database.windows.net";
                builder.UserID = Environment.GetEnvironmentVariable("userID");
                builder.Password = Environment.GetEnvironmentVariable("password");
                builder.InitialCatalog = "alexamusic";

                using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
                {
                    Console.WriteLine("\nQuery data example:");
                    Console.WriteLine("=========================================\n");

                    StringBuilder sb = new StringBuilder();
                    sb.Append("SELECT IPAddress ");
                    sb.Append("FROM Lookup ");
                    sb.Append("where UserID='" + userID + "';");

                    String sql = sb.ToString();

                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        connection.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string theIPAddress = "";
                                theIPAddress = reader.GetString(0);
                                Console.WriteLine(theIPAddress);
                                return theIPAddress;

                            }
                        }
                    }
                }
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
            }
            Console.ReadLine();
            return "";

        }
    }
}
