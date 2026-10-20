using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PainelSenhas.Web.Data;

/// <summary>
/// Lê a mesma configuração da aplicação (appsettings, segredos do usuário em Development e variáveis) para o <c>dotnet ef</c>.
/// Sem <c>ASPNETCORE_ENVIRONMENT</c>, assume Development para usar o banco local de desenvolvimento.
/// </summary>
internal static class DesignTimeConfiguration
{
    public static (IConfiguration Configuration, string DataDirectory) Load()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true);
        if (environment == Environments.Development)
        {
            // Mesma ordem da aplicação: os segredos do usuário (conexão com senha) vencem o appsettings
            builder.AddUserSecrets(typeof(DesignTimeConfiguration).Assembly, optional: true);
        }

        var configuration = builder.AddEnvironmentVariables().Build();
        var dataDirectory = Path.GetFullPath(configuration["Storage:Directory"] ?? Path.Combine(Directory.GetCurrentDirectory(), "App_Data"));
        return (configuration, dataDirectory);
    }
}

/// <summary>Contexto do SQLite no <c>dotnet ef</c> (<c>--context AppDbContext</c>), sempre sobre o arquivo local.</summary>
internal sealed class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var (configuration, dataDirectory) = DesignTimeConfiguration.Load();
        Directory.CreateDirectory(dataDirectory);
        var settings = DatabaseSettings.Resolve(
            new ConfigurationBuilder().AddConfiguration(configuration).AddInMemoryCollection([new("Database:Provider", "Sqlite")]).Build(),
            dataDirectory);
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(settings.ConnectionString).Options);
    }
}

/// <summary>Contexto do SQL Server no <c>dotnet ef</c> (<c>--context SqlServerDbContext</c>).</summary>
internal sealed class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<SqlServerDbContext>
{
    public SqlServerDbContext CreateDbContext(string[] args)
    {
        var (configuration, dataDirectory) = DesignTimeConfiguration.Load();
        var settings = DatabaseSettings.Resolve(
            new ConfigurationBuilder().AddConfiguration(configuration).AddInMemoryCollection([new("Database:Provider", "SqlServer")]).Build(),
            dataDirectory);
        return new SqlServerDbContext(new DbContextOptionsBuilder<SqlServerDbContext>().UseSqlServer(settings.ConnectionString).Options);
    }
}
