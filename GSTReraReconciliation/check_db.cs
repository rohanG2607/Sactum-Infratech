using System;
using System.Data.SqlClient;

class Program
{
    static void Main()
    {
        try {
            using (SqlConnection conn = new SqlConnection(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=D:\whatsapp downloads\Codes\Sactum Infratech\GSTReraReconciliation\GSTReraReconciliation\App_Data\GSTReraReconciliationDb.mdf;Integrated Security=True;"))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'", conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        Console.WriteLine("Tables in database:");
                        while (reader.Read())
                        {
                            Console.WriteLine("- " + reader.GetString(0));
                        }
                    }
                }
            }
        } catch (Exception ex) {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
