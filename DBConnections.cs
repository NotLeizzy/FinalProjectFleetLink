
using Microsoft.Data.SqlClient;

namespace FinalProjectFleetLink
{
    public class DbConnections
    {
        public string ConnectionString { get; } =
            @"Data Source=LAPTOP-L61316P6\MSSQLSERVER03;
              Initial Catalog=FleetLinkDB;
              Integrated Security=True;
              Encrypt=True;
              TrustServerCertificate=True;";

        public SqlConnection GetConnection()
        {
            return new SqlConnection(ConnectionString);
        }
    }
}
