using System.Data;

namespace Shared.Data;

public interface ISqlConnectionFactory
{
    IDbConnection GetOpenConnection();

    IDbConnection CreateNewConnection();

    string GetConnectionString();
}