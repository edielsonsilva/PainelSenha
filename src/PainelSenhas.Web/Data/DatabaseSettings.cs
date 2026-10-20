using Microsoft.Data.Sqlite;

namespace PainelSenhas.Web.Data;

/// <summary>Provedor de banco de dados em uso (<c>Database:Provider</c>). O padrão é <see cref="SqlServer"/>.</summary>
internal enum DatabaseProvider
{
    SqlServer,
    Sqlite
}

/// <summary>
/// Provedor e string de conexão resolvidos da configuração. A aplicação e as ferramentas do EF (migrations)
/// usam esta mesma resolução para nunca apontarem para bancos diferentes.
/// </summary>
internal sealed record DatabaseSettings(DatabaseProvider Provider, string ConnectionString)
{
    public const string SqlServerConnectionName = "SqlServer";

    /// <summary>
    /// SQL Server lê <c>ConnectionStrings:SqlServer</c>; SQLite usa o arquivo <c>painel.db</c> em <paramref name="dataDirectory"/>.
    /// </summary>
    public static DatabaseSettings Resolve(IConfiguration configuration, string dataDirectory)
    {
        var name = configuration["Database:Provider"];
        if (string.IsNullOrWhiteSpace(name) || name.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var connection = configuration.GetConnectionString(SqlServerConnectionName);
            if (string.IsNullOrWhiteSpace(connection))
            {
                throw new InvalidOperationException(
                    $"Defina ConnectionStrings:{SqlServerConnectionName} (ou Database:Provider=Sqlite para usar o arquivo local).");
            }

            return new DatabaseSettings(DatabaseProvider.SqlServer, connection);
        }

        if (name.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var connection = new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(dataDirectory, "painel.db"),
                DefaultTimeout = 2,
                ForeignKeys = true
            }.ToString();
            return new DatabaseSettings(DatabaseProvider.Sqlite, connection);
        }

        throw new InvalidOperationException($"Database:Provider inválido: '{name}'. Use SqlServer ou Sqlite.");
    }
}
