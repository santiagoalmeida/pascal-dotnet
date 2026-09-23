using System.Data.Common;

namespace PascalRuntime;

// Cursor-style SQL access over any of several ADO.NET providers, chosen by a prefix on
// the connection string passed to DbConnect: "sqlite:", "postgres:"/"postgresql:",
// "mysql:", "sqlserver:"/"mssql:", "oracle:". No prefix means a bare SQLite file path
// (backward compatible with the original SQLite-only DbConnect('archivo.db')).
//
// Raw SQL, no ORM: DbQuery + DbNext() to iterate a result set, DbGetXxx(column) to read
// the current row. Each engine's own SQL dialect applies (e.g. Oracle wants '(SELECT ...
// FROM dual)', not the SQLite/Postgres/MySQL '1' trick) — this module doesn't paper over
// that.
//
// Caveat: SQL text is passed through as-is, with no parameterized-query helper yet —
// callers are responsible for escaping/validating anything built from user input.
public static class Db
{
    private static DbConnection? _conn;
    private static DbDataReader? _reader;

    public static void Connect(string connectionString)
    {
        _conn?.Close();
        var (provider, connStr) = SplitProviderPrefix(connectionString);
        _conn = provider switch
        {
            // A SQLite "connection string" here is just a bare file path (with or
            // without the "sqlite:" prefix) — wrap it in the key ADO.NET expects.
            "sqlite" => new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={connStr}"),
            "postgres" or "postgresql" => new Npgsql.NpgsqlConnection(connStr),
            "mysql" => new MySqlConnector.MySqlConnection(connStr),
            "sqlserver" or "mssql" => new Microsoft.Data.SqlClient.SqlConnection(connStr),
            "oracle" => new Oracle.ManagedDataAccess.Client.OracleConnection(connStr),
            _ => throw new ArgumentException($"proveedor de base de datos desconocido: '{provider}'"),
        };
        _conn.Open();
    }

    private static readonly HashSet<string> KnownProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "sqlite", "postgres", "postgresql", "mysql", "sqlserver", "mssql", "oracle",
    };

    private static (string Provider, string ConnectionString) SplitProviderPrefix(string s)
    {
        int idx = s.IndexOf(':');
        if (idx > 0)
        {
            var prefix = s[..idx].ToLowerInvariant();
            if (KnownProviders.Contains(prefix))
                return (prefix, s[(idx + 1)..]);
        }
        return ("sqlite", s); // no recognized prefix: bare SQLite file path
    }

    public static void Execute(string sql)
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public static void Query(string sql)
    {
        _reader?.Dispose();
        var cmd = _conn!.CreateCommand();
        cmd.CommandText = sql;
        _reader = cmd.ExecuteReader();
    }

    public static bool Next() => _reader is not null && _reader.Read();

    public static string GetString(string column)
    {
        int i = _reader!.GetOrdinal(column);
        return _reader.IsDBNull(i) ? "" : Convert.ToString(_reader.GetValue(i)) ?? "";
    }

    public static int GetInt(string column)
    {
        int i = _reader!.GetOrdinal(column);
        return _reader.IsDBNull(i) ? 0 : Convert.ToInt32(_reader.GetValue(i));
    }

    public static double GetFloat(string column)
    {
        int i = _reader!.GetOrdinal(column);
        return _reader.IsDBNull(i) ? 0.0 : Convert.ToDouble(_reader.GetValue(i));
    }

    public static void Close()
    {
        _reader?.Dispose();
        _reader = null;
        _conn?.Close();
        _conn = null;
    }
}
