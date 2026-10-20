using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Atendimento;
using PainelSenhas.Web.Features.Emissao;
using PainelSenhas.Web.Features.Painel;
using Respawn;
using Respawn.Graph;

namespace PainelSenhas.Tests.Support;

/// <summary>
/// Aplicação real em memória (WebApplicationFactory) com relógio controlável, sobre um banco SQLite em arquivo temporário
/// ou, se a variável <c>PAINEL_TESTS_SQLSERVER</c> trouxer uma conexão (ex.: LocalDB), sobre um banco SQL Server exclusivo
/// da execução, apagado no fim. Compartilhada por todas as classes de teste; o Respawn limpa as senhas entre os testes.
/// </summary>
public sealed class AppFixture : IAsyncLifetime
{
    public const string TestPassword = "Senha#Teste2026x";
    private const string AdminUser = "admin";

    public const string SqlServerVariable = "PAINEL_TESTS_SQLSERVER";

    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "painel-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string? _sqlServerBase = Environment.GetEnvironmentVariable(SqlServerVariable);
    private readonly string _sqlServerName = "PainelSenhasTests_" + Guid.NewGuid().ToString("N");
    private WebApplicationFactory<Program>? _factory;
    private Respawner? _respawner;

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 12, 15, 0, 0, TimeSpan.Zero));

    public int TreasuryId { get; private set; }

    public int PatrimonyId { get; private set; }

    public ClaimsPrincipal Admin { get; private set; } = new();

    public ClaimsPrincipal Reception { get; private set; } = new();

    public ClaimsPrincipal TreasuryOperator { get; private set; } = new();

    public ClaimsPrincipal PatrimonyOperator { get; private set; } = new();

    public IServiceProvider Services => _factory!.Services;

    public bool UsesSqlServer => !string.IsNullOrWhiteSpace(_sqlServerBase);

    private string DatabasePath => Path.Combine(_dataDirectory, "painel.db");

    private string SqlServerConnection(string database) =>
        new SqlConnectionStringBuilder(_sqlServerBase) { InitialCatalog = database }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        // O Program lê estas chaves antes de o host existir; variáveis de ambiente são visíveis nesse momento
        Environment.SetEnvironmentVariable("Storage__Directory", _dataDirectory);
        Environment.SetEnvironmentVariable("Deployment__RequireHttps", "false");
        Environment.SetEnvironmentVariable("Database__Provider", UsesSqlServer ? "SqlServer" : "Sqlite");
        if (UsesSqlServer)
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__SqlServer", SqlServerConnection(_sqlServerName));
        }

        Environment.SetEnvironmentVariable("Seed__Ticket", "false");
        Environment.SetEnvironmentVariable("Setup__AdminUser", AdminUser);
        Environment.SetEnvironmentVariable("Setup__AdminPassword", TestPassword);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Time);
            }));

        await DatabaseSetup.InitializeAsync(Services, Services.GetRequiredService<IConfiguration>());

        await using (var db = await CreateDbContextAsync())
        {
            TreasuryId = await db.Departments.Where(x => x.Prefix == "T").Select(x => x.Id).SingleAsync();
            PatrimonyId = await db.Departments.Where(x => x.Prefix == "P").Select(x => x.Id).SingleAsync();
        }

        Admin = await PrincipalOfAsync(AdminUser);
        Reception = await CreateUserAsync("recepcao", Roles.Reception);
        TreasuryOperator = await CreateUserAsync("op.tesouraria", Roles.Operator, TreasuryId);
        PatrimonyOperator = await CreateUserAsync("op.patrimonio", Roles.Operator, PatrimonyId);

        await using var connection = CreateRespawnConnection();
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = UsesSqlServer ? DbAdapter.SqlServer : DbAdapter.Sqlite,
            TablesToInclude = [new Table("Events"), new Table("Tickets"), new Table("Sequences")]
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        SqliteConnection.ClearAllPools();
        if (UsesSqlServer)
        {
            await DropSqlServerDatabaseAsync();
        }

        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }

    public HttpClient CreateClient() =>
        _factory!.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>Apaga senhas, eventos e sequências para o próximo teste começar do zero.</summary>
    public async Task ResetAsync()
    {
        await using var connection = CreateRespawnConnection();
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
    }

    private System.Data.Common.DbConnection CreateRespawnConnection() => UsesSqlServer
        ? new SqlConnection(SqlServerConnection(_sqlServerName))
        : new SqliteConnection($"Data Source={DatabasePath}");

    private async Task DropSqlServerDatabaseAsync()
    {
        SqlConnection.ClearAllPools();
        await using var master = new SqlConnection(SqlServerConnection("master"));
        await master.OpenAsync();
        await using var command = master.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{_sqlServerName}') IS NOT NULL BEGIN ALTER DATABASE [{_sqlServerName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_sqlServerName}]; END";
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Avança o relógio até a próxima data operacional da unidade (virada do dia no fuso configurado).</summary>
    public void AdvanceToNextBusinessDay()
    {
        var clock = Services.GetRequiredService<UnitClock>();
        var today = clock.Today;
        while (clock.Today == today)
        {
            Time.Advance(TimeSpan.FromHours(1));
        }
    }

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    public Task<Result<TicketView>> IssueAsync(ClaimsPrincipal who, int departmentId, Guid? key = null, bool preferential = false) =>
        InScopeAsync(sp => sp.GetRequiredService<IssueTicket>().ExecuteAsync(who, departmentId, key ?? Guid.NewGuid(), preferential));

    public Task<Result<long>> CallNextAsync(ClaimsPrincipal who, int departmentId, Guid? key = null) =>
        InScopeAsync(sp => sp.GetRequiredService<AttendQueue>().CallNextAsync(who, departmentId, key ?? Guid.NewGuid()));

    public Task<Result<long>> ActAsync(ClaimsPrincipal who, long ticketId, EventKind kind, Guid? key = null) =>
        InScopeAsync(sp => sp.GetRequiredService<AttendQueue>().ActAsync(who, ticketId, kind, key ?? Guid.NewGuid()));

    internal async Task<AppDbContext> CreateDbContextAsync() =>
        await Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

    /// <summary>Cria um usuário já ativo; para operadores, vincula os departamentos informados.</summary>
    public async Task<ClaimsPrincipal> CreateUserAsync(string userName, string role, params int[] departmentIds)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = userName, AccessRole = role };
        var result = await users.CreateAsync(user, TestPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        if (departmentIds.Length > 0)
        {
            await using var db = await CreateDbContextAsync();
            db.UserDepartments.AddRange(departmentIds.Select(id => new UserDepartment { UserId = user.Id, DepartmentId = id }));
            await db.SaveChangesAsync();
        }

        return await PrincipalOfAsync(userName);
    }

    public async Task<ClaimsPrincipal> PrincipalOfAsync(string userName)
    {
        await using var db = await CreateDbContextAsync();
        var id = await db.Users.Where(x => x.UserName == userName).Select(x => x.Id).SingleAsync();
        return new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "test"));
    }
}

